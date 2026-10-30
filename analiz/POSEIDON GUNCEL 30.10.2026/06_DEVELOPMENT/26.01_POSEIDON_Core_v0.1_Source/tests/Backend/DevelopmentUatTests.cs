using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
namespace Poseidon.Tests;
public class DevelopmentUatTests
{
    sealed class BoundaryLogs : ILoggerProvider, ILogger {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ConcurrentQueue<string> SessionMessages { get; } = new();
        public ILogger CreateLogger(string categoryName)=>this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
        public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? exception,Func<TState,Exception?,string> formatter) { if(id.Id==4811)Messages.Enqueue(formatter(state,exception));if(id.Id==4812)SessionMessages.Enqueue(formatter(state,exception)); }
    }
    sealed class Loopback(string peer) : IStartupFilter {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => a => {
            a.Use((c,n) => {c.Connection.RemoteIpAddress=IPAddress.Parse(peer);return n(c);}); next(a);
        };
    }
    sealed class App(string environment="Development", string? invalid=null, string origin="https://localhost", string peer="127.0.0.1") : ConfiguredAppFactory {
        public BoundaryLogs Logs { get; } = new();
        readonly string root=MakeRoot();
        static string MakeRoot() { var p=Path.Combine(Path.GetTempPath(),"poseidon-uat-test-"+Guid.NewGuid());Directory.CreateDirectory(p);File.WriteAllText(Path.Combine(p,"index.html"),"synthetic UI");return p; }
        protected override string EnvironmentName=>environment;
        protected override Dictionary<string,string?> Settings { get {
            var s=new Dictionary<string,string?> { ["DevelopmentUat:Enabled"]="true",["DevelopmentUat:Origin"]=origin,["DevelopmentUat:StaticRoot"]=root,["SyntheticIdentity:Enabled"]="false",["Bff:Enabled"]="false",["Database:Initialize"]="false",["EvidenceIntake:Enabled"]="false",["ConnectionStrings:Poseidon"]="Host=unused;Database=unused;Username=unused" };
            if(invalid!=null)s[invalid]=invalid.EndsWith("Enabled")?"true":"https://evil.invalid";return s;
        } }
        protected override IHost CreateHost(IHostBuilder b){b.ConfigureServices(s=>s.AddSingleton<IStartupFilter>(new Loopback(peer)));b.ConfigureLogging(l=>l.AddProvider(Logs));return base.CreateHost(b);}
        public HttpClient Client()=>CreateClient(new WebApplicationFactoryClientOptions {BaseAddress=new(origin),AllowAutoRedirect=false});
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing && Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory][InlineData("Production",null)][InlineData("Testing",null)][InlineData("Development","DevelopmentUat:Origin")][InlineData("Development","SyntheticIdentity:Enabled")][InlineData("Development","Bff:Enabled")][InlineData("Development","EvidenceIntake:Enabled")]
    public void Unsafe_runtime_configuration_is_rejected(string environment,string? invalid){using var a=new App(environment,invalid);Assert.Throws<InvalidOperationException>(()=>a.Client());}
    static async Task<string> Csrf(HttpClient c)=>(await c.GetFromJsonAsync<JsonElement>("/bff/antiforgery")).GetProperty("requestToken").GetString()!;
    [Fact] public async Task Development_cookie_login_csrf_scope_logout_and_bearer_separation() {
        using var a=new App();using var c=a.Client();c.DefaultRequestHeaders.Add("Origin","https://localhost");
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        var login=await c.PostAsync("/bff/login?returnUrl=/",null);Assert.Equal(HttpStatusCode.Redirect,login.StatusCode);
        var cookie=string.Join(";",login.Headers.GetValues("Set-Cookie"));Assert.Contains("__Host-Poseidon.Session",cookie);Assert.Contains("secure",cookie);Assert.Contains("httponly",cookie);Assert.DoesNotContain("uat-manager",cookie);
        var session=await c.GetFromJsonAsync<Session>("/bff/session");Assert.NotNull(session);Assert.True(session!.Synthetic);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/v1/session")).StatusCode);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsync("/bff/login",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsync("/bff/logout",null)).StatusCode);
        c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(session!));
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync("/bff/logout",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/bff/session")).StatusCode);
    }
    [Fact] public void Framework_key_warmup_uses_only_process_memory() {
        using var a=new App();using var c=a.Client();
        var options=a.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>>().Value;
        Assert.Equal("DevelopmentKeyRepository",options.XmlRepository!.GetType().Name);
        var provider=a.Services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        Assert.IsType<Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider>(provider);
    }
    [Fact] public async Task Process_store_expiry_and_revocation_are_enforced() {
        var store=new DevelopmentSessions();
        var identity=new ExternalIdentity(DevelopmentUat.Issuer,"uat-manager");
        var principal=IdentityBoundary.Map(identity,await new DevelopmentGrants().ResolveAsync(identity,default));
        var ticket=new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal,new Microsoft.AspNetCore.Authentication.AuthenticationProperties {ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(-1)},BffAuthentication.CookieScheme);
        var key=await store.StoreAsync(ticket);Assert.Null(await store.RetrieveAsync(key));
        ticket.Properties.ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(20);key=await store.StoreAsync(ticket);Assert.NotNull(await store.RetrieveAsync(key));
        Assert.Null(await new DevelopmentSessions().RetrieveAsync(key));await store.RemoveAsync(key);Assert.Null(await store.RetrieveAsync(key));
    }
    [Fact] public async Task Another_process_infrastructure_rejects_previous_cookie() {
        using var a=new App();using var c=a.Client();c.DefaultRequestHeaders.Add("Origin","https://localhost");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        var response=await c.PostAsync("/bff/login",null);var cookie=response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("__Host-Poseidon.Session=")).Split(';')[0];
        using var b=new App();using var other=b.Client();other.DefaultRequestHeaders.Add("Cookie",cookie);Assert.Equal(HttpStatusCode.Unauthorized,(await other.GetAsync("/bff/session")).StatusCode);
    }
    [Theory][InlineData(null)][InlineData("https://evil.invalid")]
    public async Task Missing_or_cross_origin_rejected_before_login(string? origin){using var a=new App();using var c=a.Client();if(origin!=null)c.DefaultRequestHeaders.Add("Origin",origin);c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);}
    [Fact] public async Task Forged_forwarded_host_does_not_override_wrong_host(){using var a=new App();using var c=a.Client();c.DefaultRequestHeaders.Host="evil.invalid";c.DefaultRequestHeaders.Add("X-Forwarded-Host","localhost");Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/")).StatusCode);}
    [Theory][InlineData("//evil.invalid")][InlineData("/bff/session")]
    public async Task Unsafe_return_rejected(string target){using var a=new App();using var c=a.Client();c.DefaultRequestHeaders.Add("Origin","https://localhost");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login?returnUrl="+Uri.EscapeDataString(target),null)).StatusCode);}
    [Fact] public async Task Transformed_origin_requires_browser_assertion_and_csrf_and_supports_logout() {
        using var a=new App(origin:External);using var c=a.Client();
        c.DefaultRequestHeaders.Host="localhost:8080";
        c.DefaultRequestHeaders.Add("Origin","https://localhost:8080");
        c.DefaultRequestHeaders.Add("X-Poseidon-Uat-Origin",External);
        c.DefaultRequestHeaders.Add("Forwarded","host=evil.invalid;proto=http");
        c.DefaultRequestHeaders.Add("X-Forwarded-Host","evil.invalid");
        c.DefaultRequestHeaders.Add("X-Forwarded-Proto","http");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        var response=await c.PostAsync("/bff/login",null);
        Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
        var cookies=string.Join(";",response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure",cookies);Assert.Contains("httponly",cookies);
        var session=await c.GetFromJsonAsync<Session>("/bff/session");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(session!));
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync("/bff/logout",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/bff/session")).StatusCode);
    }
    [Theory]
    [InlineData(null,External,"localhost:8080","127.0.0.1")]
    [InlineData("null",External,"localhost:8080","127.0.0.1")]
    [InlineData("https://evil.invalid",External,"localhost:8080","127.0.0.1")]
    [InlineData("http://localhost:8080",External,"localhost:8080","127.0.0.1")]
    [InlineData("https://localhost:8080",null,"localhost:8080","127.0.0.1")]
    [InlineData("https://localhost:8080","https://evil.invalid","localhost:8080","127.0.0.1")]
    [InlineData("https://localhost:8080",External,"localhost:8081","127.0.0.1")]
    [InlineData("https://localhost:8080",External,"synthetic-owner-8080.app.github.dev","127.0.0.1")]
    [InlineData("https://localhost:8080",External,"localhost:8080","192.0.2.1")]
    public async Task Transformed_origin_does_not_broaden_authority(string? origin,string? assertion,string host,string peer) {
        using var a=new App(origin:External,peer:peer);using var c=a.Client();
        // Obtain a real CSRF pair before applying the rejected request metadata.
        if(peer=="127.0.0.1")c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Host=host;
        if(origin!=null)c.DefaultRequestHeaders.Add("Origin",origin);
        if(assertion!=null)c.DefaultRequestHeaders.Add("X-Poseidon-Uat-Origin",assertion);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
    }
    [Theory][InlineData("Origin")][InlineData("X-Poseidon-Uat-Origin")]
    public async Task Duplicate_origin_evidence_is_rejected(string header) {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Add("Origin","https://localhost:8080");
        c.DefaultRequestHeaders.Add("X-Poseidon-Uat-Origin",External);
        c.DefaultRequestHeaders.TryAddWithoutValidation(header,header=="Origin"?"https://localhost:8080":External);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
    }
    [Fact] public async Task Cross_origin_preflight_has_no_cors_permission() {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";
        using var request=new HttpRequestMessage(HttpMethod.Options,"/bff/login");
        request.Headers.Add("Origin","https://evil.invalid");
        request.Headers.Add("Access-Control-Request-Method","POST");
        request.Headers.Add("Access-Control-Request-Headers","x-poseidon-uat-origin,x-csrf-token");
        var response=await c.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Headers"));
    }
    [Fact] public async Task Logout_relogin_creates_fresh_twenty_minute_ticket_and_old_cookie_stays_revoked() {
        using var a=new App(origin:External);using var c=a.Client();
        c.DefaultRequestHeaders.Host="localhost:8080";
        c.DefaultRequestHeaders.Add("Origin","https://localhost:8080");
        c.DefaultRequestHeaders.Add("X-Poseidon-Uat-Origin",External);
        var options=a.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(BffAuthentication.CookieScheme);
        var store=a.Services.GetRequiredService<IBffInfrastructure>().Tickets;
        async Task<(string Cookie,string Key,Microsoft.AspNetCore.Authentication.AuthenticationTicket Ticket)> Login() {
            c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
            var before=DateTimeOffset.UtcNow;
            var response=await c.PostAsync("/bff/login",null);Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
            var cookie=response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("__Host-Poseidon.Session=")).Split(';')[0];
            var ticket=options.TicketDataFormat.Unprotect(cookie.Split('=',2)[1])!;
            var key=ticket.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;
            var stored=(await store.RetrieveAsync(key))!;
            Assert.InRange(stored.Properties.ExpiresUtc!.Value,before.AddMinutes(20).AddSeconds(-1),DateTimeOffset.UtcNow.AddMinutes(20));
            Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/bff/session")).StatusCode);
            return (cookie,key,stored);
        }
        var first=await Login();
        var session=await c.GetFromJsonAsync<Session>("/bff/session");
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(session!));
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync("/bff/logout",null)).StatusCode);
        Assert.Null(await store.RetrieveAsync(first.Key));
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/bff/session")).StatusCode);
        var second=await Login();Assert.NotEqual(first.Key,second.Key);Assert.NotEqual(first.Cookie,second.Cookie);
        using var old=a.CreateClient(new WebApplicationFactoryClientOptions {BaseAddress=new(External),HandleCookies=false,AllowAutoRedirect=false});
        old.DefaultRequestHeaders.Host="localhost:8080";old.DefaultRequestHeaders.Add("Cookie",first.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized,(await old.GetAsync("/bff/session")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/bff/session")).StatusCode);
        // Stale requests cannot remove the replacement session's ticket.
        Assert.NotNull(await store.RetrieveAsync(second.Key));
    }
    [Fact] public async Task Repeated_login_conflict_does_not_extend_ticket_and_expired_cookie_can_login_fresh() {
        using var a=new App(origin:External);using var c=a.Client();
        c.DefaultRequestHeaders.Host="localhost:8080";c.DefaultRequestHeaders.Add("Origin","https://localhost:8080");
        c.DefaultRequestHeaders.Add("X-Poseidon-Uat-Origin",External);
        async Task<string> Token() { c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var token=await Csrf(c);c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token);return token; }
        await Token();var login=await c.PostAsync("/bff/login",null);Assert.Equal(HttpStatusCode.NoContent,login.StatusCode);
        var cookie=login.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("__Host-Poseidon.Session=")).Split(';')[0];
        var options=a.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(BffAuthentication.CookieScheme);
        var handle=options.TicketDataFormat.Unprotect(cookie.Split('=',2)[1])!.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;
        var store=a.Services.GetRequiredService<IBffInfrastructure>().Tickets;
        var ticket=(await store.RetrieveAsync(handle))!;var expiry=ticket.Properties.ExpiresUtc;
        // Simulate another tab still presenting the same valid browser cookie.
        await Token();var attempts=await Task.WhenAll(c.PostAsync("/bff/login",null),c.PostAsync("/bff/login",null));
        Assert.All(attempts,x=>Assert.Equal(HttpStatusCode.Conflict,x.StatusCode));
        Assert.Equal(expiry,(await store.RetrieveAsync(handle))!.Properties.ExpiresUtc);
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        // Expire this process-only test ticket; no runtime clock or TTL modification.
        ticket.Properties.ExpiresUtc=DateTimeOffset.UtcNow.AddSeconds(-1);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/bff/session")).StatusCode);
        await Token();Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync("/bff/login",null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/bff/session")).StatusCode);
        var lines=a.Logs.SessionMessages.ToArray();
        Assert.Contains(lines,x=>x.Contains(" LOGIN_EXISTING_SESSION "));
        Assert.Contains(lines,x=>x.Contains(" LOGIN_CSRF_REJECTED "));
        Assert.Contains(lines,x=>x.Contains(" TICKET_EXPIRED "));
        Assert.Contains(lines,x=>x.Contains(" AUTH_401_COOKIE_PRESENT "));
        Assert.Equal(2,lines.Count(x=>x.Contains(" LOGIN_CREATED ")));
        Assert.All(lines,x=>{Assert.DoesNotContain(handle,x);Assert.DoesNotContain(cookie,x);Assert.DoesNotContain("uat-manager",x);Assert.DoesNotContain(External,x);});
    }
    const string External = "https://synthetic-owner-8080.app.github.dev";
    [Theory][InlineData("localhost:8080")][InlineData("synthetic-owner-8080.app.github.dev")]
    public async Task Observed_transport_keeps_canonical_origin_cookie_and_logout(string host) {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host=host;
        c.DefaultRequestHeaders.Add("Forwarded","for=192.0.2.1;host=evil.invalid;proto=http");
        c.DefaultRequestHeaders.Add("X-Forwarded-Host","evil.invalid");c.DefaultRequestHeaders.Add("X-Forwarded-Proto","http");
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync("/")).StatusCode);
        c.DefaultRequestHeaders.Add("Origin",External);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Remove("Origin");c.DefaultRequestHeaders.Add("Origin","http://localhost:8080");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        c.DefaultRequestHeaders.Remove("Origin");c.DefaultRequestHeaders.Add("Origin",External);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login?returnUrl=https://evil.invalid",null)).StatusCode);
        var login=await c.PostAsync("/bff/login?returnUrl=/",null);
        Assert.Equal(HttpStatusCode.Redirect,login.StatusCode);Assert.Equal("/",login.Headers.Location!.ToString());
        var cookies=string.Join(";",login.Headers.GetValues("Set-Cookie"));Assert.Contains("secure",cookies);Assert.Contains("httponly",cookies);Assert.DoesNotContain("evil.invalid",cookies);
        var session=(await c.GetFromJsonAsync<Session>("/bff/session"))!;
        c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await Csrf(c));
        c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(session));
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync("/bff/logout",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/bff/session")).StatusCode);
    }
    [Theory][InlineData("localhost")][InlineData("localhost:8081")][InlineData("127.0.0.1:8080")][InlineData("[::1]:8080")][InlineData("evil.invalid")][InlineData("localhost:8080,evil.invalid")]
    public async Task Only_exact_observed_transport_is_allowed(string host) {
        using var a=new App(origin:External);using var c=a.Client();
        c.DefaultRequestHeaders.TryAddWithoutValidation("Host",host);
        c.DefaultRequestHeaders.Add("X-Forwarded-Host",new Uri(External).Host);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/")).StatusCode);
    }
    [Theory][InlineData("localhost:8080")][InlineData("synthetic-owner-8080.app.github.dev")]
    public async Task Non_loopback_transport_is_rejected(string host) {
        using var a=new App(origin:External,peer:"192.0.2.1");using var c=a.Client();c.DefaultRequestHeaders.Host=host;
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/")).StatusCode);
    }
    [Fact] public async Task Localhost_emulation_does_not_enable_codespaces_transport_alias() {
        using var a=new App();using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/")).StatusCode);
        Assert.False(new DevelopmentUat().Enabled);
    }
    [Theory][InlineData(null,"ORIGIN_MISSING")][InlineData("null","ORIGIN_NULL")]
    [InlineData("http://localhost:8080","ORIGIN_LOCAL_HTTP_8080")][InlineData("https://localhost:8080","ORIGIN_LOCAL_HTTPS_8080")]
    [InlineData("https://private-payload.invalid","ORIGIN_OTHER")]
    public async Task Origin_rejection_logs_only_fixed_classification(string? origin,string code) {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";
        if(origin!=null)c.DefaultRequestHeaders.Add("Origin",origin);
        c.DefaultRequestHeaders.Add("Cookie","untrusted_cookie=PRIVATE_COOKIE");
        var response=await c.PostAsync("/bff/login?private_query=PRIVATE_QUERY",new StringContent("PRIVATE_BODY"));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal("POSEIDON_UAT_REJECT LOGIN "+code,Assert.Single(a.Logs.Messages));
    }
    [Fact] public async Task Multiple_origin_header_is_classified_and_still_rejected() {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";
        c.DefaultRequestHeaders.TryAddWithoutValidation("Origin",new[]{External,"https://private-payload.invalid"});
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        Assert.Equal("POSEIDON_UAT_REJECT LOGIN ORIGIN_MULTIPLE",Assert.Single(a.Logs.Messages));
    }
    [Theory][InlineData("192.0.2.1","localhost:8080","PEER_NOT_LOOPBACK")][InlineData("127.0.0.1","untrusted.invalid","HOST_NOT_ALLOWED")]
    public async Task Transport_rejection_logs_only_fixed_classification(string peer,string host,string code) {
        using var a=new App(origin:External,peer:peer);using var c=a.Client();c.DefaultRequestHeaders.Host=host;
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        Assert.Equal("POSEIDON_UAT_REJECT LOGIN "+code,Assert.Single(a.Logs.Messages));
    }
    [Fact] public async Task Accepted_boundary_does_not_log_rejection_and_csrf_still_rejects() {
        using var a=new App(origin:External);using var c=a.Client();c.DefaultRequestHeaders.Host="localhost:8080";c.DefaultRequestHeaders.Add("Origin",External);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/bff/login",null)).StatusCode);
        Assert.Empty(a.Logs.Messages);
    }
}
