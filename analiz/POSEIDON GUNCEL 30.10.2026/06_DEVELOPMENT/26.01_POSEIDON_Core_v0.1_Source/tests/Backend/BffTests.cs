using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;
using System.Text.Json;
namespace Poseidon.Tests;
// Serialize fixture DDL; the explicit concurrent-client transaction tests still run in parallel.
[Collection("PostgreSQL schema")]
public partial class BffTests
{
    const string Issuer = "https://issuer.invalid";
    // Process-only test storage and data protection: intentionally outside runtime source.
    sealed class Infrastructure : IBffInfrastructure, ITicketStore
    {
        readonly ConcurrentDictionary<string, AuthenticationTicket> tickets = new();
        public ITicketStore Tickets => this;
        public IDataProtectionProvider DataProtection { get; } = new EphemeralDataProtectionProvider();
        public bool IsEphemeral => true;
        public int Count => tickets.Count;
        public AuthenticationTicket OnlyTicket => Assert.Single(tickets.Values);
        public void Expire() { foreach (var ticket in tickets.Values) ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1); }
        public Task<string> StoreAsync(AuthenticationTicket ticket) { var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)); tickets[key] = ticket; return Task.FromResult(key); }
        public Task RenewAsync(string key, AuthenticationTicket ticket) { tickets[key] = ticket; return Task.CompletedTask; }
        public Task<AuthenticationTicket?> RetrieveAsync(string key) {
            if (tickets.TryGetValue(key, out var ticket) && ticket.Properties.ExpiresUtc > DateTimeOffset.UtcNow) return Task.FromResult<AuthenticationTicket?>(ticket);
            tickets.TryRemove(key, out _); return Task.FromResult<AuthenticationTicket?>(null);
        }
        public Task RemoveAsync(string key) { tickets.TryRemove(key, out _); return Task.CompletedTask; }
    }
    sealed class Resolver : IIdentityGrantResolver
    {
        public bool Revoked;
        public bool CanWrite = true;
        public bool CanManageInventory;
        public bool CanManageCertificates;
        public bool CanCaptureEvidence;
        public Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public Guid Company = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001");
        public Task<IdentityGrant?> ResolveAsync(ExternalIdentity identity, CancellationToken ct) => Task.FromResult<IdentityGrant?>(Revoked ? null : new(identity, Tenant, Company, [], CanWrite, CanManageInventory, CanManageCertificates, CanCaptureEvidence));
    }
    sealed class FixtureLogin : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            app.Use(async (c, n) => {
                if (c.Request.Path == "/test/signin") {
                    var identity = new ExternalIdentity(Issuer, c.Request.Query["subject"].FirstOrDefault() ?? "fixture-subject");
                    var grant = await c.RequestServices.GetRequiredService<IIdentityGrantResolver>().ResolveAsync(identity, default);
                    await c.SignInAsync(BffAuthentication.CookieScheme, IdentityBoundary.Map(identity, grant), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
                    c.Response.StatusCode = 204; return;
                }
                await n(c);
            }); next(app);
        };
    }
    sealed class App(bool infrastructure = true, string environment = "Testing", string? invalid = null, FakeProvider? provider = null, bool database = false, TimeProvider? certificateClock = null, string? evidenceRoot = null, string? evidenceDatabase = null) : ConfiguredAppFactory
    {
        public Infrastructure Store { get; } = new();
        public Resolver Grants { get; } = new();
        protected override string EnvironmentName => environment;
        protected override Dictionary<string, string?> Settings {
            get {
                var settings = new Dictionary<string,string?> {
                    ["SyntheticIdentity:Enabled"]="false", ["Database:Initialize"]="false", ["ConnectionStrings:Poseidon"]=database ? evidenceDatabase ?? TestDatabase.Connection : "Host=unused;Database=unused;Username=unused",
                    ["Authentication:Authority"]=Issuer, ["Authentication:Audience"]="api-resource",
                    ["Bff:Enabled"]="true", ["Bff:Authority"]=Issuer, ["Bff:Issuer"]=Issuer,
                    ["Bff:ClientId"]="test-web-client", ["Bff:ClientSecret"]="test-only-not-a-real-credential", ["Bff:Origin"]="https://localhost"
                };
                if(evidenceRoot is not null){settings["EvidenceIntake:Enabled"]="true";settings["EvidenceIntake:Root"]=evidenceRoot;}
                if (invalid == "same-audience") settings["Authentication:Audience"] = "test-web-client";
                else if (invalid is not null) settings[invalid] = invalid.EndsWith("Enabled") ? "true" : "";
                return settings;
            }
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureTestServices(s => {
            if(certificateClock is not null)s.AddSingleton<DraftStore>(services=>new DraftStore(services.GetRequiredService<Npgsql.NpgsqlDataSource>(),certificateClock));
            if (infrastructure) { s.AddSingleton<IBffInfrastructure>(Store); s.AddSingleton<IIdentityGrantResolver>(Grants); }
            s.AddTransient<IStartupFilter, FixtureLogin>();
            s.PostConfigure<OpenIdConnectOptions>(BffAuthentication.OidcScheme, o => {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer, AuthorizationEndpoint = Issuer+"/authorize", TokenEndpoint = Issuer+"/token" };
                if (provider is not null) { configuration.SigningKeys.Add(provider.Key); o.Backchannel = new HttpClient(provider); }
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
        });
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    }
    // All issuer transport stays in-process. The actual OIDC handler still redeems the code,
    // verifies the signed ID token, state/correlation/nonce and runs its claim actions.
    sealed class FakeProvider(string scenario) : HttpMessageHandler
    {
        readonly RSA rsa = RSA.Create(2048);
        public RsaSecurityKey Key => new(rsa) { KeyId = "ephemeral-callback-test" };
        public string Nonce = "", Challenge = "";
        public int Redemptions;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(Issuer + "/token", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("isolated-code", form["code"].ToString());
            Assert.Equal("authorization_code", form["grant_type"].ToString());
            Assert.Equal("https://localhost/bff/callback", form["redirect_uri"].ToString());
            Assert.Equal("test-web-client", form["client_id"].ToString());
            Assert.Equal(Challenge, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))));
            Redemptions++;
            var claims = new[] { new Claim("sub", "callback-subject"), new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64), new Claim("nonce", scenario == "nonce" ? "wrong-nonce" : Nonce),
                new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim("company_id", Guid.NewGuid().ToString()),
                new Claim("permission", "admin"), new Claim("email", "untrusted@example.invalid") };
            var token = new JwtSecurityToken(scenario == "issuer" ? "https://wrong.invalid" : Issuer,
                scenario == "audience" ? "wrong-audience" : "test-web-client", claims,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(Key, SecurityAlgorithms.RsaSha256));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { token_type = "Bearer", access_token = "unused-test-access-token", expires_in = 300,
                id_token = new JwtSecurityTokenHandler().WriteToken(token) }) };
        }
        protected override void Dispose(bool disposing) { if (disposing) rsa.Dispose(); base.Dispose(disposing); }
    }
    [Theory][InlineData("valid")][InlineData("issuer")][InlineData("audience")][InlineData("nonce")]
    public async Task Actual_oidc_callback_preserves_server_identity_and_rejects_invalid_tokens(string scenario)
    {
        using var provider = new FakeProvider(scenario);
        await using var app = new App(provider: provider); using var client = app.Client();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        var challenge = await client.PostAsync("/bff/login?returnUrl=/vessels", null);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        provider.Nonce = query["nonce"].ToString(); provider.Challenge = query["code_challenge"].ToString();
        var form = new Dictionary<string,string> { ["code"] = "isolated-code", ["state"] = query["state"].ToString() };
        var callback = await client.PostAsync("/bff/callback", new FormUrlEncodedContent(form));
        Assert.Equal(1, provider.Redemptions);
        if (scenario != "valid") {
            Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode); Assert.Equal(0, app.Store.Count);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/session")).StatusCode); return;
        }
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode); Assert.Equal("/vessels", callback.Headers.Location!.OriginalString);
        Assert.Equal(1, app.Store.Count);
        var stored = app.Store.OnlyTicket;
        Assert.Equal(Issuer, stored.Principal.FindFirstValue("iss"));
        Assert.DoesNotContain(stored.Principal.Claims, c => c.Type == "email" || c.Value == "admin");
        Assert.Empty(stored.Properties.GetTokens());
        var sessionResponse = await client.GetAsync("/bff/session");
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<Session>();
        Assert.Equal(IdentityBoundary.ActorKey(new(Issuer, "callback-subject")), session!.Subject);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), session.TenantId);
        Assert.Empty(session.VesselIds);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/bff/callback", new FormUrlEncodedContent(form))).StatusCode);
        Assert.Equal(1, provider.Redemptions); // correlation cookie consumed: replay cannot redeem again
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/bff/session")).StatusCode);
    }
    static async Task<HttpResponseMessage> BrowserSave(HttpClient client, string path, object data, bool csrf = true, string? key = null, long? version = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { saveMode = "DRAFT", data }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        if (version.HasValue) request.Headers.Add("If-Match", $"\"{version}\"");
        if (csrf) request.Headers.Add("X-CSRF-TOKEN", await Csrf(client));
        if (!client.DefaultRequestHeaders.Contains("X-Poseidon-Scope")) {
            var sessionResponse = await client.GetAsync("/bff/session");
            if (sessionResponse.IsSuccessStatusCode) request.Headers.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp((await sessionResponse.Content.ReadFromJsonAsync<Session>())!));
        }
        return await client.SendAsync(request);
    }
    [Fact] public async Task Browser_routes_require_cookie_and_csrf_before_persistence()
    {
        await using var app = new App(); using var client = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/api/v1/vessels")).StatusCode);
        await client.GetAsync("/test/signin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/api/v1/vessels")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", "browser-cannot-grant-authority");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/api/v1/vessels")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");
        Assert.Equal(HttpStatusCode.BadRequest, (await BrowserSave(client, "/bff/api/v1/vessels", new { name = "unsaved" }, csrf: false)).StatusCode);
        app.Grants.CanWrite = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await BrowserSave(client, "/bff/api/v1/vessels", new { name = "unsaved" })).StatusCode);
        app.Store.Expire();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/api/v1/certificates")).StatusCode);
    }
    [PostgresFact] public async Task Callback_cookie_drafts_scope_versions_logout_and_replay_use_real_postgres()
    {
        using var provider = new FakeProvider("valid");
        await using var app = new App(provider: provider, database: true); using var client = app.Client();
        var store = app.Services.GetRequiredService<DraftStore>();
        await store.Migrate(default); await store.Seed(default);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        var challenge = await client.PostAsync("/bff/login?returnUrl=/", null);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        provider.Nonce = query["nonce"].ToString(); provider.Challenge = query["code_challenge"].ToString();
        var callback = await client.PostAsync("/bff/callback", new FormUrlEncodedContent(new Dictionary<string,string> { ["code"] = "isolated-code", ["state"] = query["state"].ToString() }));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var sessionCookie = callback.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("__Host-Poseidon.Session=", StringComparison.Ordinal)).Split(';')[0];
        var scope = (await client.GetFromJsonAsync<Session>("/bff/session"))!;
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp(scope));
        var key = Guid.NewGuid().ToString();
        var created = await BrowserSave(client, "/bff/api/v1/vessels", new { name = "BFF isolated fixture" }, key: key);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var vessel = (await created.Content.ReadFromJsonAsync<Draft>())!;
        var replay = await BrowserSave(client, "/bff/api/v1/vessels", new { name = "BFF isolated fixture" }, key: key);
        Assert.Equal(vessel.DraftId, (await replay.Content.ReadFromJsonAsync<Draft>())!.DraftId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/bff/api/v1/vessels/{vessel.DraftId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await BrowserSave(client, "/bff/api/v1/vessels", new { company_id = Guid.NewGuid().ToString() })).StatusCode);
        var certificateResponse = await BrowserSave(client, "/bff/api/v1/certificates", new { vessel_id = vessel.DraftId.ToString(), type = "BFF test" });
        Assert.Equal(HttpStatusCode.OK, certificateResponse.StatusCode);
        var certificate = (await certificateResponse.Content.ReadFromJsonAsync<Draft>())!;
        Assert.Equal(HttpStatusCode.OK, (await BrowserSave(client, $"/bff/api/v1/certificates/{certificate.DraftId}", new { vessel_id = vessel.DraftId.ToString(), type = "Updated test" }, version: 1)).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await BrowserSave(client, $"/bff/api/v1/certificates/{certificate.DraftId}", new { vessel_id = vessel.DraftId.ToString() }, version: 1)).StatusCode);
        // A separately validated subject in the same company has no vessel grant.
        await using var restricted = new App(database: true); using var restrictedClient = restricted.Client();
        await restrictedClient.GetAsync("/test/signin");
        restrictedClient.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp((await restrictedClient.GetFromJsonAsync<Session>("/bff/session"))!));
        Assert.Equal(HttpStatusCode.NotFound, (await restrictedClient.GetAsync($"/bff/api/v1/vessels/{vessel.DraftId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await BrowserSave(restrictedClient, "/bff/api/v1/certificates", new { vessel_id = vessel.DraftId.ToString() })).StatusCode);
        var oldTenant = app.Grants.Tenant;
        app.Grants.Tenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        app.Grants.Company = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000001");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/bff/api/v1/certificates/{certificate.DraftId}")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp((await client.GetFromJsonAsync<Session>("/bff/session"))!));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/bff/api/v1/certificates/{certificate.DraftId}")).StatusCode);
        app.Grants.Tenant = oldTenant; app.Grants.Company = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001");
        client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp((await client.GetFromJsonAsync<Session>("/bff/session"))!));
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/bff/logout", null)).StatusCode);
        using var stolen = app.Client(); stolen.DefaultRequestHeaders.Add("Cookie", sessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.GetAsync("/bff/api/v1/vessels")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await BrowserSave(stolen, "/bff/api/v1/vessels", new { name = "No retry after logout" }, csrf: false, key: key)).StatusCode);
        Assert.Equal(0, app.Store.Count);
    }
    [Fact] public void Missing_infrastructure_fails_closed() { using var app = new App(false); Assert.Throws<InvalidOperationException>(() => app.Client()); }
    [Fact] public void Ephemeral_infrastructure_is_rejected_in_production() { using var app = new App(environment:"Production"); Assert.Throws<InvalidOperationException>(() => app.Client()); }
    [Theory][InlineData("Bff:Authority")][InlineData("Bff:Issuer")][InlineData("Bff:Origin")][InlineData("Bff:ClientId")][InlineData("Bff:ClientSecret")][InlineData("SyntheticIdentity:Enabled")][InlineData("Authentication:Audience")][InlineData("Authentication:Authority")][InlineData("same-audience")]
    public void Missing_or_incompatible_configuration_rejected(string key) { using var app = new App(invalid:key); Assert.Throws<InvalidOperationException>(() => app.Client()); }
    [Fact] public async Task Bff_is_disabled_by_default()
    {
        await using var app = new ConfiguredAppFactory(); using var client = app.CreateClient();
        var schemes = await app.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.DoesNotContain(schemes, s => s.Name == BffAuthentication.CookieScheme || s.Name == BffAuthentication.OidcScheme);
    }
    static async Task<string> Csrf(HttpClient client) => (await client.GetFromJsonAsync<Dictionary<string,string>>("/bff/antiforgery"))!["requestToken"];
    [Fact] public async Task Login_requires_csrf_and_local_return_and_uses_code_pkce()
    {
        await using var app = new App(); using var client = app.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/bff/login", null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/bff/login?returnUrl=https://evil.invalid", null)).StatusCode);
        var challenge = await client.PostAsync("/bff/login?returnUrl=/vessels", null);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = challenge.Headers.Location!.ToString();
        Assert.Contains("response_type=code", location); Assert.Contains("code_challenge=", location); Assert.Contains("code_challenge_method=S256", location);
        var options = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(BffAuthentication.OidcScheme);
        Assert.False(options.SaveTokens); Assert.True(options.RequireHttpsMetadata);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/bff/callback", new FormUrlEncodedContent(new Dictionary<string,string>{{"code","forged"},{"state","forged"}}))).StatusCode);
    }
    [Fact] public async Task Cookie_is_server_backed_and_does_not_authenticate_bearer_api_logout_requires_csrf()
    {
        await using var app = new App(); using var client = app.Client();
        var login = await client.GetAsync("/test/signin");
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-Poseidon.Session=", cookie); Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        Assert.DoesNotContain("fixture-subject", cookie); Assert.Equal(1, app.Store.Count);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/bff/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/session")).StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, (await client.GetAsync("/bff/logout")).StatusCode); Assert.Equal(1, app.Store.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/bff/logout", null)).StatusCode); Assert.Equal(1, app.Store.Count);
        client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp((await client.GetFromJsonAsync<Session>("/bff/session"))!));
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/bff/logout", null)).StatusCode); Assert.Equal(0, app.Store.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/session")).StatusCode);
    }
    [Theory][InlineData("subject")][InlineData("company")][InlineData("missing")]
    public async Task Deferred_logout_from_A_cannot_terminate_replacement_B(string change)
    {
        await using var app = new App(); using var client = app.Client();
        await client.GetAsync("/test/signin?subject=account-A");
        var displayed = (await client.GetFromJsonAsync<Session>("/bff/session"))!;
        // Cookie replacement happens after tab A displayed its session, before it obtains CSRF.
        if (change == "company") app.Grants.Company = Guid.NewGuid();
        await client.GetAsync(change == "subject" ? "/test/signin?subject=account-B" : "/test/signin?subject=account-A");
        var replacement = (await client.GetFromJsonAsync<Session>("/bff/session"))!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        if (change != "missing") client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp(displayed));
        var count = app.Store.Count;
        var rejected = await client.PostAsync("/bff/logout", null);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.DoesNotContain(rejected.Headers, h => h.Key == "Set-Cookie");
        Assert.Equal(count, app.Store.Count);
        var stillActive = (await client.GetFromJsonAsync<Session>("/bff/session"))!;
        Assert.Equal(replacement.Subject, stillActive.Subject); Assert.Equal(replacement.CompanyId, stillActive.CompanyId);
        client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");
        client.DefaultRequestHeaders.Add("X-Poseidon-Scope", IdentityBoundary.ScopeStamp(replacement));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/bff/logout", null)).StatusCode);
        Assert.Equal(count - 1, app.Store.Count);
    }
    [Fact] public async Task Revoked_membership_revokes_session()
    {
        await using var app = new App(); using var client = app.Client(); await client.GetAsync("/test/signin"); app.Grants.Revoked = true;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/session")).StatusCode); Assert.Equal(0, app.Store.Count);
    }
    [Fact] public async Task Expired_and_tampered_sessions_are_rejected()
    {
        await using var app = new App(); using var client = app.Client(); await client.GetAsync("/test/signin"); app.Store.Expire();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/session")).StatusCode);
        using var other = app.Client(); other.DefaultRequestHeaders.Add("Cookie", "__Host-Poseidon.Session=forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/bff/session")).StatusCode);
    }
    [Fact] public async Task Csrf_tokens_cannot_be_replayed_by_another_browser()
    {
        await using var app = new App(); using var first = app.Client(); using var second = app.Client();
        var token = await Csrf(first); await Csrf(second); second.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await second.PostAsync("/bff/login", null)).StatusCode);
    }
    [Theory][InlineData("http://localhost/bff/antiforgery")][InlineData("https://other.invalid/bff/antiforgery")]
    public async Task Wrong_origin_or_insecure_transport_rejected(string url)
    {
        await using var app = new App(); using var client = app.Client(); Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
    }
}
