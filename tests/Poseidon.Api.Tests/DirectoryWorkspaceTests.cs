using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Api.Tests;

// DIR-WORKSPACE-01 / DEV-014/015/016/019; actual API, antiforgery and PostgreSQL, no production test login.
[Trait("Category", "Integration")]
public sealed partial class DirectoryWorkspaceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly Guid actorId = Guid.NewGuid();
    private SeedData seed = null!;
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(Ct);
        seed = SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await db.Database.MigrateAsync(Ct);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
    private Vessel SeedVessel => seed.Vessels.First(v => v.TenantId == seed.Tenants[0].Id);
    private WebApplicationFactory<Program> Host(bool manage = true, bool read = true, bool edit = true, bool scope = true, DbCommandInterceptor? interceptor = null, bool search = false) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Test");
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Poseidon"] = postgres.GetConnectionString(),
            ["Authentication:Oidc:Memberships:0:Issuer"] = "https://directory-test.example",
            ["Authentication:Oidc:Memberships:0:Subject"] = "synthetic",
            ["Authentication:Oidc:Memberships:0:ActorId"] = actorId.ToString(),
            ["Authentication:Oidc:Memberships:0:TenantId"] = seed.Tenants[0].Id.ToString(),
            ["Authentication:Oidc:Memberships:0:Permissions:0"] = "directory.read",
            ["Authentication:Oidc:Memberships:0:Permissions:1"] = edit ? "directory.write" : "unused",
            ["Authentication:Oidc:Memberships:0:Permissions:2"] = search ? "workspace.search" : "unused",
            ["Authentication:Oidc:Memberships:0:TenantWideManage"] = (scope && manage).ToString(),
            ["Authentication:Oidc:Memberships:0:TenantWideRead"] = (scope && read).ToString(),
            ["Authentication:Oidc:Memberships:0:VesselGrants:0:VesselId"] = scope ? SeedVessel.Id.ToString() : Guid.Empty.ToString(),
            ["Authentication:Oidc:Memberships:0:VesselGrants:0:CanEdit"] = edit.ToString()
        }));
        b.ConfigureServices(s => s.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = "DirectorySynthetic"; o.DefaultChallengeScheme = "DirectorySynthetic"; o.DefaultForbidScheme = "DirectorySynthetic";
        }).AddScheme<AuthenticationSchemeOptions, TestHandler>("DirectorySynthetic", _ => { }));
        if (interceptor != null) b.ConfigureTestServices(s =>
        {
            s.RemoveAll<PoseidonDbContext>();
            s.AddScoped(sp =>
            {
                var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                var actor = sp.GetRequiredService<ActorResolver>().Resolve(http.User) ?? throw new UnauthorizedAccessException();
                return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>()
                    .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(interceptor).Options,
                    actor.TenantId, new ActorContext(actor.Id.ToString(), "User", http.TraceIdentifier));
            });
        });
    });
    private static HttpClient Client(WebApplicationFactory<Program> host, bool authenticated = true)
    {
        var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        if (authenticated) client.DefaultRequestHeaders.Add("X-Directory-Test", "synthetic");
        return client;
    }
    private async Task Token(HttpClient client)
    {
        var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
    }
    private async Task<HttpResponseMessage> Send(HttpClient client, string path, object body, string? key = null, bool update = false)
    {
        using var message = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post, "/api/v1/" + path) { Content = JsonContent.Create(body) };
        if (key != "") message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(message, Ct);
    }
    private PoseidonDbContext Db(Guid? tenant = null) => new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? seed.Tenants[0].Id, new("synthetic-inspector", "User", "inspection"));
    private async Task Sql(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync(Ct);
        await using var command = new Npgsql.NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<(string Code, string Name, uint Version)> DirectoryRow(HttpClient client, string path, bool company)
    {
        if (company)
        {
            var row = (await client.GetFromJsonAsync<CompanyDto>("/api/v1/" + path, Ct))!;
            return (row.Code, row.Name, row.Version);
        }
        var vessel = (await client.GetFromJsonAsync<VesselDto>("/api/v1/" + path, Ct))!;
        return (vessel.Code, vessel.Name, vessel.Version);
    }

    // DIR-WORKSPACE-01; active 15.01 paragraph:10 and 14.01 paragraph:140:
    // every mutable aggregate update checks xmin, including normalized unchanged values.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unchanged_update_is_an_attributed_touch_and_successful_replay_does_not_write_again(bool company)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var id = company ? SeedVessel.CompanyId : SeedVessel.Id;
        var path = $"{(company ? "companies" : "vessels")}/{id}";
        var before = await DirectoryRow(client, path, company);
        Guid[] auditIds;
        await using (var db = Db()) auditIds = await db.AuditEvents.Select(a => a.Id).ToArrayAsync(Ct);
        var key = Guid.NewGuid().ToString();
        var body = new { code = " " + before.Code + " ", name = " " + before.Name + " ", expectedVersion = before.Version };
        using var touch = await Send(client, path, body, key, true);
        Assert.Equal(HttpStatusCode.OK, touch.StatusCode);
        var touched = await DirectoryRow(client, path, company);
        Assert.Equal(before.Code, touched.Code); Assert.Equal(before.Name, touched.Name);
        Assert.NotEqual(before.Version, touched.Version);
        using var replay = await Send(client, path, body, key, true);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.Equal(touched, await DirectoryRow(client, path, company));
        await using var fresh = Db();
        var receipt = Assert.Single(await fresh.CommandReceipts.ToArrayAsync(Ct));
        Assert.Equal(key, receipt.Key); Assert.Equal(id, receipt.ResourceId); Assert.Equal(actorId, receipt.ActorId);
        var events = (await fresh.AuditEvents.ToArrayAsync(Ct)).Where(a => !auditIds.Contains(a.Id)).ToArray();
        Assert.Equal(2, events.Length);
        var business = Assert.Single(events, a => a.EntityType == (company ? nameof(Company) : nameof(Vessel)));
        Assert.Equal("Modified", business.Action); Assert.Equal(actorId.ToString(), business.ActorId); Assert.Equal("User", business.ActorType);
        Assert.EndsWith(id.ToString(), business.EntityId);
        Assert.Equal(Assert.Single(touch.Headers.GetValues("X-Correlation-ID")), business.CorrelationId);
        var receiptAudit = Assert.Single(events, a => a.EntityType == nameof(CommandReceipt));
        Assert.Equal("Added", receiptAudit.Action); Assert.Equal(business.CorrelationId, receiptAudit.CorrelationId);
        if (company)
        {
            var persisted = await fresh.Companies.SingleAsync(c => c.Id == id, Ct);
            Assert.Equal(touched.Version, persisted.Version);
        }
        else
        {
            var persisted = await fresh.Vessels.SingleAsync(v => v.Id == id, Ct);
            Assert.Equal(touched.Version, persisted.Version);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unchanged_update_loses_to_edit_after_original_select_without_receipt_or_business_audit(bool company)
    {
        var barrier = new DirectorySelectBarrier(company ? "Companies" : "Vessels");
        await using var host = Host(interceptor: barrier);
        using var originalClient = Client(host); await Token(originalClient);
        using var competingClient = Client(host); await Token(competingClient);
        var id = company ? SeedVessel.CompanyId : SeedVessel.Id;
        var path = $"{(company ? "companies" : "vessels")}/{id}";
        var before = await DirectoryRow(originalClient, path, company);
        Guid[] auditIds;
        await using (var db = Db()) auditIds = await db.AuditEvents.Select(a => a.Id).ToArrayAsync(Ct);
        var originalKey = Guid.NewGuid().ToString(); var competingKey = Guid.NewGuid().ToString();
        barrier.Arm();
        var pending = Send(originalClient, path,
            new { code = " " + before.Code + " ", name = " " + before.Name + " ", expectedVersion = before.Version }, originalKey, true);
        HttpResponseMessage? original = null;
        try
        {
            await barrier.WaitUntilReadAsync(Ct);
            // The original reader already owns its MVCC result. Only this request is paused.
            using var competing = await Send(competingClient, path,
                new { code = "RACE-WINNER", name = "Competing edit", expectedVersion = before.Version }, competingKey, true);
            Assert.Equal(HttpStatusCode.OK, competing.StatusCode);
            var winner = await DirectoryRow(competingClient, path, company);
            Assert.NotEqual(before.Version, winner.Version);
            barrier.Release();
            original = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Conflict, original.StatusCode);
            Assert.Contains("version_conflict", await original.Content.ReadAsStringAsync(Ct));
            Assert.False(original.Headers.Contains("Idempotency-Replayed"));

            await using var fresh = Db();
            var receipts = await fresh.CommandReceipts.ToArrayAsync(Ct);
            Assert.DoesNotContain(receipts, r => r.Key == originalKey);
            var receipt = Assert.Single(receipts);
            Assert.Equal(competingKey, receipt.Key); Assert.Equal(id, receipt.ResourceId); Assert.Equal(actorId, receipt.ActorId);
            var events = (await fresh.AuditEvents.ToArrayAsync(Ct)).Where(a => !auditIds.Contains(a.Id)).ToArray();
            Assert.Equal(2, events.Length);
            var business = Assert.Single(events, a => a.EntityType == (company ? nameof(Company) : nameof(Vessel)));
            Assert.Equal("Modified", business.Action); Assert.Equal(actorId.ToString(), business.ActorId); Assert.Equal("User", business.ActorType);
            Assert.EndsWith(id.ToString(), business.EntityId);
            var competingCorrelation = Assert.Single(competing.Headers.GetValues("X-Correlation-ID"));
            var originalCorrelation = Assert.Single(original.Headers.GetValues("X-Correlation-ID"));
            Assert.Equal(competingCorrelation, business.CorrelationId);
            Assert.DoesNotContain(events, a => a.CorrelationId == originalCorrelation);
            var receiptAudit = Assert.Single(events, a => a.EntityType == nameof(CommandReceipt));
            Assert.Equal("Added", receiptAudit.Action); Assert.Equal(competingCorrelation, receiptAudit.CorrelationId);
            Assert.Contains(await fresh.Set<SecurityEvent>().ToArrayAsync(Ct),
                e => e.CorrelationId == originalCorrelation && e.Result == "version_conflict");
            if (company)
            {
                var persisted = await fresh.Companies.SingleAsync(c => c.Id == id, Ct);
                Assert.Equal(("RACE-WINNER", "Competing edit", winner.Version), (persisted.Code, persisted.Name, persisted.Version));
            }
            else
            {
                var persisted = await fresh.Vessels.SingleAsync(v => v.Id == id, Ct);
                Assert.Equal(("RACE-WINNER", "Competing edit", winner.Version), (persisted.Code, persisted.Name, persisted.Version));
            }
        }
        finally
        {
            barrier.Release();
            original ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            original.Dispose();
        }
    }

    private sealed class DirectorySelectBarrier(string table) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public Task WaitUntilReadAsync(CancellationToken cancellationToken) => read.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        public void Release() => released.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) &&
                command.CommandText.Contains($"FROM core.\"{table}\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                read.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task Create_normalizes_replays_after_host_restart_and_audits_exactly_one_business_write()
    {
        var key = Guid.NewGuid().ToString(); var body = new { code = " RETRY-C ", name = " Synthetic Retry " };
        Guid id;
        await using (var host = Host())
        {
            using var client = Client(host); await Token(client);
            using var first = await Send(client, "companies", body, key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var row = (await first.Content.ReadFromJsonAsync<CompanyDto>(Ct))!;
            id = row.Id; Assert.Equal("RETRY-C", row.Code); Assert.Equal("Synthetic Retry", row.Name); Assert.True(row.Version > 0);
            // Treat first response as lost. New host/client must recover from the durable receipt alone.
        }
        await using (var host = Host())
        {
            using var client = Client(host); await Token(client);
            using var retry = await Send(client, "companies", new { code = "RETRY-C", name = "Synthetic Retry" }, key);
            Assert.Equal(id, (await retry.Content.ReadFromJsonAsync<CompanyDto>(Ct))!.Id);
            Assert.Equal("true", Assert.Single(retry.Headers.GetValues("Idempotency-Replayed")));
            using var mismatch = await Send(client, "companies", new { code = "RETRY-C", name = "Changed" }, key);
            Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
            Assert.Contains("idempotency_key_reused", await mismatch.Content.ReadAsStringAsync(Ct));
        }
        await using var db = Db();
        Assert.Equal(1, await db.Companies.CountAsync(c => c.Code == "RETRY-C", Ct));
        Assert.Equal(1, await db.CommandReceipts.CountAsync(r => r.Key == key, Ct));
        var events = await db.AuditEvents.Where(e => e.EntityType == nameof(Company) && e.EntityId.EndsWith(id.ToString())).ToArrayAsync(Ct);
        Assert.Single(events); Assert.Equal(actorId.ToString(), events[0].ActorId); Assert.Equal("Added", events[0].Action);
        Assert.DoesNotContain(await db.Set<SecurityEvent>().Where(e => e.Action == "CompanyCreate").ToArrayAsync(Ct), e => e.Result == "Success");
    }

    [Fact]
    public async Task Concurrent_identical_creates_and_updates_have_one_effect()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString(); var body = new { code = "CONCURRENT", name = "Synthetic Concurrent" };
        var responses = await Task.WhenAll(Send(client, "companies", body, key), Send(client, "companies", body, key));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            var rows = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<CompanyDto>(Ct)));
            Assert.Equal(rows[0]!.Id, rows[1]!.Id);
            var update = new { code = "CONCURRENT", name = "Updated", expectedVersion = rows[0]!.Version };
            var updateKey = Guid.NewGuid().ToString();
            var writes = await Task.WhenAll(Send(client, $"companies/{rows[0]!.Id}", update, updateKey, true), Send(client, $"companies/{rows[0]!.Id}", update, updateKey, true));
            try { Assert.All(writes, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode)); } finally { foreach (var write in writes) write.Dispose(); }
            await using var db = Db();
            Assert.Equal(2, await db.CommandReceipts.CountAsync(Ct));
            Assert.Equal(2, await db.AuditEvents.CountAsync(e => e.EntityType == nameof(Company) && e.EntityId.EndsWith(rows[0]!.Id.ToString()), Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Restricted_editor_cannot_read_other_vessels_or_indirect_company_metadata_or_widen_scope()
    {
        await using var host = Host(manage: false, read: false); using var client = Client(host); await Token(client);
        var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", Ct);
        Assert.False(session!.CanCreate); Assert.False(session.CanReadCompanies);
        var fleet = await client.GetFromJsonAsync<PageDto<VesselDto>>("/api/v1/vessels", Ct);
        Assert.Equal(SeedVessel.Id, Assert.Single(fleet!.Items).Id); Assert.True(fleet.Items[0].CanEdit);
        var companies = await client.GetFromJsonAsync<PageDto<CompanyDto>>("/api/v1/companies", Ct); Assert.Empty(companies!.Items);
        using var company = await client.GetAsync($"/api/v1/companies/{SeedVessel.CompanyId}", Ct); Assert.Equal(HttpStatusCode.NotFound, company.StatusCode);
        var other = seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Id != SeedVessel.Id);
        using var invisible = await client.GetAsync($"/api/v1/vessels/{other.Id}", Ct); Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        using var deniedUpdate = await Send(client, $"vessels/{other.Id}", new { code = other.Code, name = "denied", expectedVersion = fleet.Items[0].Version }, update: true);
        Assert.Equal(HttpStatusCode.NotFound, deniedUpdate.StatusCode);
        using var changed = await Send(client, $"vessels/{SeedVessel.Id}", new { code = SeedVessel.Code, name = "Restricted edit", expectedVersion = fleet.Items[0].Version }, update: true);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var editedVessel = (await changed.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        Assert.Equal(SeedVessel.CompanyId, editedVessel.CompanyId); Assert.Equal(SeedVessel.Kind.ToString(), editedVessel.Kind);
        using var move = await Send(client, $"vessels/{SeedVessel.Id}", new { code = SeedVessel.Code, name = "move", expectedVersion = fleet.Items[0].Version, companyId = Guid.NewGuid() }, update: true);
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        using var create = await Send(client, "vessels", new { code = "DENIED", name = "Denied", kind = "Ship", companyId = SeedVessel.CompanyId });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using var createCompany = await Send(client, "companies", new { code = "DENIED", name = "Denied" }); Assert.Equal(HttpStatusCode.Forbidden, createCompany.StatusCode);
    }

    [Fact]
    public async Task Missing_scope_denies_by_default_and_live_membership_revocation_blocks_receipt_replay()
    {
        await using (var host = Host(scope: false))
        {
            using var client = Client(host);
            using var response = await client.GetAsync("/api/v1/vessels", Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using var mapped = Host(); using var writer = Client(mapped); await Token(writer);
        var key = Guid.NewGuid().ToString(); var body = new { code = "REVOKE", name = "Synthetic" };
        using var first = await Send(writer, "companies", body, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var settings = mapped.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { TenantWideManage = false, TenantWideRead = false, VesselGrants = [] }];
        using var replay = await Send(writer, "companies", body, key); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Fact]
    public async Task Viewer_csrf_and_unauthenticated_denials_never_mutate_and_are_audited()
    {
        await using var host = Host(edit: false); using var viewer = Client(host); await Token(viewer);
        var vessel = await viewer.GetFromJsonAsync<VesselDto>($"/api/v1/vessels/{SeedVessel.Id}", Ct); Assert.False(vessel!.CanEdit);
        using var forbidden = await Send(viewer, $"vessels/{vessel.Id}", new { code = vessel.Code, name = "Denied", expectedVersion = vessel.Version }, update: true);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var anonymous = Client(host, false);
        using var unauth = await Send(anonymous, "companies", new { code = "BAD", name = "Denied" }); Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);
        await using var managerHost = Host(); using var manager = Client(managerHost);
        using var csrf = await Send(manager, "companies", new { code = "BAD", name = "Denied" }); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        await using var db = Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Result == "csrf_invalid");
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Result == "permission_denied");
    }

    [Fact]
    public async Task Duplicate_and_stale_edits_return_stable_conflicts_without_overwriting()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var company = seed.Companies.First(c => c.TenantId == seed.Tenants[0].Id);
        var current = (await client.GetFromJsonAsync<CompanyDto>($"/api/v1/companies/{company.Id}", Ct))!;
        using var duplicate = await Send(client, "companies", new { code = current.Code, name = "Other" }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("duplicate_code", await duplicate.Content.ReadAsStringAsync(Ct));
        using var update = await Send(client, $"companies/{current.Id}", new { code = current.Code, name = "First edit", expectedVersion = current.Version }, update: true);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using var stale = await Send(client, $"companies/{current.Id}", new { code = current.Code, name = "Stale overwrite", expectedVersion = current.Version }, update: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("version_conflict", await stale.Content.ReadAsStringAsync(Ct));
        var after = await client.GetFromJsonAsync<CompanyDto>($"/api/v1/companies/{current.Id}", Ct); Assert.Equal("First edit", after!.Name);
    }

    [Theory]
    [InlineData("Ship")]
    [InlineData("Tug")]
    public async Task Vessel_create_uses_explicit_kind_tenant_and_company_without_fabricated_profiles(string kind)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seed.Tenants[1].Id.ToString());
        using var result = await Send(client, "vessels", new { code = "NEW-" + kind, name = "Synthetic Vessel", kind, companyId = SeedVessel.CompanyId });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var row = (await result.Content.ReadFromJsonAsync<VesselDto>(Ct))!; Assert.Equal(kind, row.Kind);
        await using var db = Db(); Assert.True(await db.Vessels.AnyAsync(v => v.Id == row.Id, Ct));
        Assert.False(await db.VesselProfiles.AnyAsync(p => p.VesselId == row.Id, Ct));
        Assert.False(await db.TugProfiles.AnyAsync(p => p.VesselId == row.Id, Ct));
        var foreign = seed.Companies.First(c => c.TenantId != seed.Tenants[0].Id);
        using var cross = await Send(client, "vessels", new { code = "CROSS", name = "Denied", kind, companyId = foreign.Id }); Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
    }

    [Theory]
    [InlineData("{\"code\":\"C\",\"name\":\"N\",\"tenantId\":\"spoofed\"}")]
    [InlineData("{\"code\":\"C\",\"name\":\"N\",\"actorId\":\"spoofed\"}")]
    [InlineData("{\"code\":\"C\",\"name\":\"N\",\"companyId\":\"not-a-guid\",\"kind\":\"Ship\"}")]
    [InlineData("{\"code\":\"C\",\"name\":\"N\",\"companyId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"code\":\"C\\n\",\"name\":\"N\",\"kind\":0}")]
    public async Task Malformed_or_mass_assignment_input_is_safe_and_audited(string body)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vessels") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var result = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var text = await result.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("Npgsql", text); Assert.DoesNotContain("spoofed", text);
        await using var db = Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "VesselCreate" && e.Result != "Success");
    }

    [Fact]
    public async Task Receipt_or_business_audit_failure_rolls_back_all_effects_and_retry_recovers()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        foreach (var table in new[] { "core.\"CommandReceipts\"", "audit.\"AuditEvents\"" })
        {
            await Sql($"CREATE TRIGGER reject_test_insert BEFORE INSERT ON {table} FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
            var key = Guid.NewGuid().ToString(); var code = table.StartsWith("core", StringComparison.Ordinal) ? "RECEIPT-FAIL" : "AUDIT-FAIL";
            var body = new { code, name = "Synthetic atomic write" };
            using var failure = await Send(client, "companies", body, key); Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            await using (var db = Db())
            {
                Assert.False(await db.Companies.AnyAsync(c => c.Code == code, Ct)); Assert.False(await db.CommandReceipts.AnyAsync(r => r.Key == key, Ct));
                Assert.DoesNotContain(await db.Set<SecurityEvent>().Where(e => e.Action == "CompanyCreate").ToArrayAsync(Ct), e => e.Result == "Success");
            }
            await Sql($"DROP TRIGGER reject_test_insert ON {table};");
            using var retry = await Send(client, "companies", body, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        }
    }

    [Fact]
    public async Task Required_attempt_audit_failure_prevents_mutation_and_does_not_expose_database_details()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        await Sql("CREATE TRIGGER reject_test_security BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        using var response = await Send(client, "companies", new { code = "UNRECORDED", name = "Synthetic" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Postgres", await response.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.False(await db.Companies.AnyAsync(c => c.Code == "UNRECORDED", Ct));
    }


    [Fact]
    public async Task Invalid_keys_versions_and_search_are_bounded_and_receipts_do_not_cross_actors_or_tenants()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var body = new { code = "KEY-TEST", name = "Synthetic" };
        foreach (var key in new[] { "", "short", new string('K', 81), "contains-invalid-symbol!" })
        {
            using var invalid = await Send(client, "companies", body, key);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains("invalid_idempotency_key", await invalid.Content.ReadAsStringAsync(Ct));
        }
        var acceptedKey = new string('K', 80);
        using var created = await Send(client, "companies", body, acceptedKey); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdRow = (await created.Content.ReadFromJsonAsync<CompanyDto>(Ct))!;
        using var missingVersion = await Send(client, $"companies/{createdRow.Id}", new { code = "KEY-TEST", name = "Ignored" }, update: true);
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        var foreignVessel = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        using var foreignUpdate = await Send(client, $"vessels/{foreignVessel.Id}", new { code = "X", name = "Denied", expectedVersion = 1 }, update: true);
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
        using var oversizedSearch = await client.GetAsync("/api/v1/vessels?q=" + new string('x', 101), Ct); Assert.Equal(HttpStatusCode.BadRequest, oversizedSearch.StatusCode);
        var search = await client.GetFromJsonAsync<PageDto<CompanyDto>>("/api/v1/companies?q=KEY-TEST", Ct); Assert.Equal(createdRow.Id, Assert.Single(search!.Items).Id);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { ActorId = Guid.NewGuid() }];
        await Token(client);
        using var otherActor = await Send(client, "companies", body, acceptedKey);
        Assert.Equal(HttpStatusCode.Conflict, otherActor.StatusCode); Assert.False(otherActor.Headers.Contains("Idempotency-Replayed"));
        settings.Memberships = [settings.Memberships[0] with { TenantId = seed.Tenants[1].Id }];
        await Token(client);
        using var otherTenant = await Send(client, "companies", body, acceptedKey);
        Assert.Equal(HttpStatusCode.OK, otherTenant.StatusCode);
        Assert.NotEqual(createdRow.Id, (await otherTenant.Content.ReadFromJsonAsync<CompanyDto>(Ct))!.Id);
    }


    [Fact]
    public async Task Concurrent_key_reuse_with_different_payload_rolls_back_losing_entity_and_audit()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(
            Send(client, "companies", new { code = "RACE-A", name = "Synthetic A" }, key),
            Send(client, "companies", new { code = "RACE-B", name = "Synthetic B" }, key));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            var losing = Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("idempotency_key_reused", await losing.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            var entity = Assert.Single(await db.Companies.Where(c => c.Code == "RACE-A" || c.Code == "RACE-B").ToArrayAsync(Ct));
            Assert.Single(await db.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
            Assert.Single(await db.AuditEvents.Where(a => a.EntityType == nameof(Company) && a.EntityId.EndsWith(entity.Id.ToString())).ToArrayAsync(Ct));
            Assert.Equal(seed.Companies.Count(c => c.TenantId == seed.Tenants[0].Id) + 1,
                await db.AuditEvents.CountAsync(a => a.EntityType == nameof(Company), Ct));
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    private sealed class TestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(!Request.Headers.ContainsKey("X-Directory-Test") ? AuthenticateResult.NoResult() :
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic"), new Claim("iss", "https://directory-test.example"), new Claim("role", "admin")], Scheme.Name)), Scheme.Name)));
    }
}
