using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Api.Tests;

[Trait("Category", "Integration")]
public sealed class FoundationApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly Guid actorId = Guid.NewGuid();
    private SeedData seed = null!;
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        seed = SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();

    private WebApplicationFactory<Program> Host(bool permission = true, bool mapped = true) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Test");
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Poseidon"] = postgres.GetConnectionString(),
            ["Authentication:Oidc:Memberships:0:Issuer"] = "https://synthetic.example",
            ["Authentication:Oidc:Memberships:0:Subject"] = mapped ? "synthetic-one" : "unmapped",
            ["Authentication:Oidc:Memberships:0:ActorId"] = actorId.ToString(),
            ["Authentication:Oidc:Memberships:0:TenantId"] = seed.Tenants[0].Id.ToString(),
            ["Authentication:Oidc:Memberships:0:Permissions:0"] = permission ? "directory.read" : "unrecognized"
        }));
        b.ConfigureServices(services => services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = "SyntheticTest";
            o.DefaultChallengeScheme = "SyntheticTest";
            o.DefaultForbidScheme = "SyntheticTest";
        }).AddScheme<AuthenticationSchemeOptions, SyntheticHandler>("SyntheticTest", _ => { }));
    });

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

    [Fact]
    public async Task Tenant_reads_ignore_spoofed_scope_and_cross_tenant_details_are_404()
    {
        await using var host = Host(); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "one");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seed.Tenants[1].Id.ToString());
        client.DefaultRequestHeaders.Add("X-Actor-Id", "spoofed");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "spoofed");
        var rows = await client.GetFromJsonAsync<PageDto<CompanyDto>>("/api/v1/companies?tenantId=" + seed.Tenants[1].Id, TestContext.Current.CancellationToken);
        Assert.Single(rows!.Items);
        Assert.All(rows!.Items, row => Assert.Contains(seed.Companies, c => c.Id == row.Id && c.TenantId == seed.Tenants[0].Id));
        Assert.DoesNotContain(rows.Items, row => seed.Companies.Any(c => c.Id == row.Id && c.TenantId == seed.Tenants[1].Id));
        var foreign = seed.Vessels.First(x => x.TenantId == seed.Tenants[1].Id);
        using var denied = await client.GetAsync($"/api/v1/vessels/{foreign.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.NotEqual("spoofed", Assert.Single(denied.Headers.GetValues("X-Correlation-ID")));
        var fleet = await client.GetFromJsonAsync<PageDto<VesselDto>>("/api/v1/vessels?kind=Tug&limit=1", TestContext.Current.CancellationToken);
        Assert.Equal("Tug", Assert.Single(fleet!.Items).Kind);
        using var invalid = await client.GetAsync("/api/v1/companies?limit=101", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using var db = TenantDb();
        var events = await db.Set<SecurityEvent>().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains(events, e => e.Action == "CompaniesRead" && e.Result == "Success" && e.ActorId == actorId.ToString());
        Assert.Contains(events, e => e.Action == "VesselsRead" && e.Result == "NotFound" && e.ResourceId == foreign.Id);
        Assert.All(events, e => Assert.Equal(seed.Tenants[0].Id, e.TenantId));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Missing_role_or_mapping_cannot_read_and_denial_is_persisted(bool permission, bool mapped)
    {
        await using var host = Host(permission, mapped); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "one");
        using var response = await client.GetAsync("/api/v1/companies", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new Npgsql.NpgsqlCommand("SELECT count(*) FROM audit.\"SecurityEvents\" WHERE \"Action\"='AccessDenied' AND \"Result\"='Denied'", connection);
        Assert.True((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0);
    }

    [Fact]
    public async Task Anonymous_setup_and_csrf_are_safe_without_production_login_backdoors()
    {
        await using var host = Host(); using var client = Client(host);
        using var anonymous = await client.GetAsync("/api/v1/companies", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", TestContext.Current.CancellationToken);
        Assert.True(session!.SetupRequired); Assert.False(session.Authenticated);
        using var login = await client.GetAsync("/api/v1/auth/login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "one");
        using var logout = await client.PostAsync("/api/v1/auth/logout", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, logout.StatusCode);
        using var write = await client.PostAsJsonAsync("/api/v1/companies", new { tenantId = seed.Tenants[1].Id, name = "spoofed" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
    }

    private PoseidonDbContext TenantDb() => new(SeedImporter.Options(postgres.GetConnectionString()), seed.Tenants[0].Id, new("test", "User", "test"));

    [Fact]
    public async Task Business_data_is_not_delivered_when_security_audit_insert_fails()
    {
        await using (var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString()))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new Npgsql.NpgsqlCommand("""
                CREATE TRIGGER test_reject_security_insert BEFORE INSERT ON audit."SecurityEvents"
                FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                """, connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        await using var host = Host(); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "one");
        using var response = await client.GetAsync("/api/v1/companies", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(seed.Companies[0].Name, body);
        Assert.Contains("service_unavailable", body);
    }

    [Fact]
    public async Task Missing_database_returns_safe_unavailable_instead_of_an_empty_business_list()
    {
        await using var host = Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Poseidon"] = null, ["POSEIDON_DATABASE"] = null
        })));
        using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Synthetic-Identity", "one");
        using var response = await client.GetAsync("/api/v1/companies", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("service_unavailable", body);
        Assert.DoesNotContain("Host=", body);
        Assert.DoesNotContain("synthetic-one", body);
    }

    // Compiled only into the test project; the API never registers this handler.
    private sealed class SyntheticHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(!Request.Headers.ContainsKey("X-Synthetic-Identity")
            ? AuthenticateResult.NoResult()
            : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "synthetic-one"), new Claim("iss", "https://synthetic.example"), new Claim("role", "admin"), new Claim("tenant", "spoofed")
            ], Scheme.Name)), Scheme.Name)));
    }
}
