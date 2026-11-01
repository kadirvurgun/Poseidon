using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Poseidon.Api.Tests;

[Trait("Category", "Integration")]
public sealed class OidcProtocolTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("nonce")]
    [InlineData("state")]
    [InlineData("correlation")]
    public async Task Federation_validates_protocol_before_issuing_a_bounded_session(string failure)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        var tenant = Guid.NewGuid();
        if (failure == "valid")
        {
            await postgres.StartAsync(TestContext.Current.CancellationToken);
            var seed = SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));
            tenant = seed.Tenants[0].Id;
            await using var setup = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
            await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
        using var rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "synthetic-key" };
        var backchannel = new TokenEndpoint();
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Test");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Poseidon"] = failure == "valid" ? postgres.GetConnectionString() : null,
                ["Authentication:Oidc:Authority"] = "https://idp.example",
                ["Authentication:Oidc:Issuer"] = "https://idp.example",
                ["Authentication:Oidc:ClientId"] = "synthetic-client",
                ["Authentication:Oidc:Memberships:0:Issuer"] = "https://idp.example",
                ["Authentication:Oidc:Memberships:0:Subject"] = "synthetic-subject",
                ["Authentication:Oidc:Memberships:0:ActorId"] = Guid.NewGuid().ToString(),
                ["Authentication:Oidc:Memberships:0:TenantId"] = tenant.ToString(),
                ["Authentication:Oidc:Memberships:0:TenantWideRead"] = "true",
            ["Authentication:Oidc:Memberships:0:Permissions:0"] = "directory.read"
            }));
            b.ConfigureServices(services => services.PostConfigure<OpenIdConnectOptions>("oidc", o =>
            {
                var metadata = new OpenIdConnectConfiguration
                {
                    Issuer = "https://idp.example", AuthorizationEndpoint = "https://idp.example/authorize",
                    TokenEndpoint = "https://idp.example/token", EndSessionEndpoint = "https://idp.example/logout"
                };
                metadata.SigningKeys.Add(signingKey);
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                o.Backchannel = new HttpClient(backchannel);
                o.ResponseMode = OpenIdConnectResponseMode.Query;
            }));
        });
        using var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        using var challenge = await client.GetAsync("/api/v1/auth/login?returnUrl=https://evil.example", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"].ToString()));
        using var wrongRsa = RSA.Create(2048);
        var token = new JwtSecurityToken(
            issuer: failure == "issuer" ? "https://evil.example" : "https://idp.example",
            audience: failure == "audience" ? "wrong-client" : "synthetic-client",
            claims: [new Claim("sub", "synthetic-subject"), new Claim("nonce", failure == "nonce" ? "invalid" : query["nonce"].ToString()), new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: failure == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(failure == "signature" ? new RsaSecurityKey(wrongRsa) { KeyId = "synthetic-key" } : signingKey, SecurityAlgorithms.RsaSha256));
        backchannel.Token = new JwtSecurityTokenHandler().WriteToken(token);
        var callback = QueryHelpers.AddQueryString("/signin-oidc", new Dictionary<string, string?>
        {
            ["code"] = "synthetic-code", ["state"] = failure == "state" ? "tampered" : query["state"].ToString()
        });
        using var noCookies = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        using var response = await (failure == "correlation" ? noCookies : client).GetAsync(callback, TestContext.Current.CancellationToken);
        if (failure == "valid")
        {
            Assert.Equal("/app", response.Headers.Location?.OriginalString);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-poseidon-session="));
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            var cookieOptions = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get("poseidon-session");
            var protectedValue = cookie.Split(';')[0].Split('=', 2)[1];
            var ticket = cookieOptions.TicketDataFormat.Unprotect(protectedValue);
            Assert.True(ticket != null, "The callback emitted a session cookie, but its protected ticket could not be read by the configured ticket format.");
            var actor = host.Services.GetRequiredService<ActorResolver>().Resolve(ticket!.Principal);
            Assert.True(ticket.Principal.Identity?.IsAuthenticated == true, "The protected callback ticket does not contain an authenticated principal.");
            Assert.True(actor != null, "The protected callback principal does not resolve to the configured trusted membership.");
            Assert.Equal(tenant, actor!.TenantId);
            Assert.True(ticket.Properties.ExpiresUtc <= DateTimeOffset.UtcNow.AddMinutes(11));
            Assert.Null(ticket.Properties.GetTokenValue("access_token"));
            using var explicitCookieClient = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
            explicitCookieClient.DefaultRequestHeaders.Add("Cookie", "__Host-poseidon-session=" + protectedValue);
            var explicitSession = await explicitCookieClient.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", TestContext.Current.CancellationToken);
            Assert.True(explicitSession?.Authenticated == true, "The callback ticket resolves to a trusted actor, but an HTTPS request with that ticket was not authenticated.");
            var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", TestContext.Current.CancellationToken);
            Assert.True(session?.Authenticated == true, "The automatic HTTPS client did not authenticate after the callback issued a valid protected session ticket.");
            Assert.Equal(tenant, session.TenantId);
            ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            using var expiredClient = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
            expiredClient.DefaultRequestHeaders.Add("Cookie", "__Host-poseidon-session=" + cookieOptions.TicketDataFormat.Protect(ticket));
            using var expiredMutation = await expiredClient.PostAsync("/api/v1/auth/logout", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, expiredMutation.StatusCode);
            using var logout = await client.PostAsync("/api/v1/auth/logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = session.CsrfToken }), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            Assert.StartsWith("https://idp.example/logout", logout.Headers.Location!.OriginalString);
            await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), tenant, new("test", "User", "test"));
            var events = await db.Set<SecurityEvent>().ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Contains(events, e => e.Action == "Login" && e.Result == "Success");
            Assert.Contains(events, e => e.Action == "Logout" && e.Result == "Success");
            return;
        }
        Assert.Equal("/app?auth=failed", response.Headers.Location?.OriginalString);
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [], c => c.StartsWith("__Host-poseidon-session="));
        using var mutation = await client.PostAsync("/api/v1/auth/logout", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, mutation.StatusCode);
        using var protectedRead = await client.GetAsync("/api/v1/companies", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedRead.StatusCode);
        if (failure is not ("state" or "correlation")) Assert.Contains("code_verifier=", backchannel.LastBody);
    }

    private sealed class TokenEndpoint : HttpMessageHandler
    {
        public string Token { get; set; } = "";
        public string LastBody { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            {
                id_token = Token, access_token = "synthetic-access-token", token_type = "Bearer", expires_in = 600
            }), Encoding.UTF8, "application/json") };
        }
    }
}
