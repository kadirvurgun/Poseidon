using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private sealed class CatalogAuthenticationOptions : AuthenticationSchemeOptions { public string Subject { get; set; } = ""; }
    private sealed class CatalogTestHandler(IOptionsMonitor<CatalogAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<CatalogAuthenticationOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(!Request.Headers.ContainsKey("X-Directory-Test") ? AuthenticateResult.NoResult() :
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", Options.Subject), new Claim("iss", "https://directory-test.example"), new Claim("role", "admin")], Scheme.Name)), Scheme.Name)));
    }
    private static readonly Guid CatalogReviewer = Guid.Parse("dcb0605f-3952-4672-95b8-a7445ad9e194");
    private static readonly Guid CatalogGrant = Guid.Parse("638f18f9-a0ba-430b-a2a3-924deea0fa93");
    private const string CatalogSource = "Synthetic DEV-047 catalog contract; not production approval";
    private string CatalogPath => $"vessels/{SeedVessel.Id}/crew/position-catalog";
    private WebApplicationFactory<Program> CatalogHost(bool reviewer = false, bool revoke = false, string kind = "Human", DbCommandInterceptor? interceptor = null) =>
        Host(crewRead: true, crewWrite: true, interceptor: interceptor).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => {
            var config = new Dictionary<string, string?> {
                ["Authentication:Oidc:Issuer"] = "https://directory-test.example",
                ["Authentication:Oidc:Authority"] = "https://directory-test.example",
                ["Authentication:Oidc:ClientId"] = "synthetic-catalog"
            };
            // Both genuinely separate synthetic actors remain configured on every host, so historical authority can be re-resolved.
            for (var i = 0; i < 2; i++) {
                var prefix = $"Authentication:Oidc:Memberships:{i}:";
                config[prefix + "Issuer"] = "https://directory-test.example";
                config[prefix + "Subject"] = i == 0 ? "synthetic" : "catalog-reviewer";
                config[prefix + "ActorId"] = (i == 0 ? actorId : CatalogReviewer).ToString();
                config[prefix + "TenantId"] = SeedVessel.TenantId.ToString();
                config[prefix + "ActorKind"] = kind;
                config[prefix + "RoleReferences:0"] = "91c26eb5-cf16-4553-981d-1c275358c535";
                config[prefix + "TenantWideRead"] = "true"; config[prefix + "TenantWideManage"] = "true";
                var permissions = new[] { "directory.read", "directory.write", "crew.read", "crew.write", "crew.history.read", "crew.catalog.govern", "mode.tug", "mode.cross" };
                for (var n = 0; n < permissions.Length; n++) config[prefix + $"Permissions:{n}"] = permissions[n];
                var g = prefix + "CrewCatalogAuthorities:0:";
                config[g + "Id"] = CatalogGrant.ToString(); config[g + "VesselId"] = SeedVessel.Id.ToString();
                config[g + "RoleId"] = "91c26eb5-cf16-4553-981d-1c275358c535";
                config[g + "SourceReference"] = CatalogSource;
                config[g + "From"] = "2020-01-01T00:00:00Z"; config[g + "Until"] = revoke && i == 0 ? "2021-01-01T00:00:00Z" : "2099-01-01T00:00:00Z";
                foreach (var (action, n) in new[] { "draft", "review", "adopt", "withdraw" }.Select((s, index) => (s, index))) config[g + $"Actions:{n}"] = action;
            }
            c.AddInMemoryCollection(config);
        }).ConfigureTestServices(s => s.AddAuthentication(o => {
            o.DefaultAuthenticateScheme = "CatalogSynthetic"; o.DefaultChallengeScheme = "CatalogSynthetic"; o.DefaultForbidScheme = "CatalogSynthetic";
        }).AddScheme<CatalogAuthenticationOptions, CatalogTestHandler>("CatalogSynthetic", o => o.Subject = reviewer ? "catalog-reviewer" : "synthetic")));
    private CrewPositionPolicy CatalogContent(string version = "synthetic-durable-v1") => new() {
        Version = version, SourceReference = CatalogSource, TimeZone = "UTC", PeriodSemantics = "StartInclusiveEndExclusive",
        Positions = [new() { TenantId = SeedVessel.TenantId, VesselId = SeedVessel.Id, PositionId = Guid.Parse("184e08f9-594c-44e2-902b-065e0d859d99"),
            Code = "SYNTHETIC-CATALOG-POSITION", Rank = "SyntheticRank", Department = "SyntheticDepartment", Capacity = "SyntheticCapacity", Provenance = "SyntheticUnverified" }]
    };
    private async Task<uint> CatalogVersion(HttpClient client) {
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/v1/" + CatalogPath, Ct));
        return json.RootElement.GetProperty("version").GetUInt32();
    }
    [Fact]
    public async Task Durable_catalog_commands_require_independent_humans_and_feed_ordinary_assignments_history_and_withdrawal()
    {
        using var authorHost = CatalogHost(); using var reviewerHost = CatalogHost(reviewer: true);
        using var author = Client(authorHost); using var reviewer = Client(reviewerHost); await Token(author); await Token(reviewer);
        var policy = CatalogContent(); var key = Guid.NewGuid().ToString();
        var draft = new CrewCatalogCommand(0, CatalogGrant, "Draft", "Synthetic proposal", policy);
        Assert.Equal(HttpStatusCode.OK, (await Send(author, CatalogPath + "/events", draft, key)).StatusCode);
        var version = await CatalogVersion(author);
        Assert.Equal(HttpStatusCode.OK, (await Send(author, CatalogPath + "/events", draft, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(author, CatalogPath + "/events", draft with { Reason = "Changed" }, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(author, CatalogPath + "/events", new CrewCatalogCommand(version, CatalogGrant, "Reviewed", "Self review"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(version, CatalogGrant, "Reviewed", "Independent software fixture review"))).StatusCode);
        version = await CatalogVersion(reviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(author, CatalogPath + "/events", new CrewCatalogCommand(version, CatalogGrant, "Adopted", "Self adoption"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(version, CatalogGrant, "Adopted", "Synthetic catalog adoption only"))).StatusCode);
        // Active values come from durable governance rather than the different environment-only tuple.
        var lookup = await author.GetStringAsync($"/api/v1/vessels/{SeedVessel.Id}/crew/position-policy", Ct);
        Assert.Contains("synthetic-durable-v1", lookup); Assert.Contains("SyntheticRank", lookup);
        var command = NewCrew() with { PositionId = policy.Positions[0].PositionId, PositionPolicyVersion = policy.Version };
        Assert.Equal(HttpStatusCode.OK, (await Send(author, CrewUrl, command)).StatusCode);
        var profile = (await author.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal("SyntheticRank", profile.Assignment.Rank);
        Assert.Equal(HttpStatusCode.OK, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(await CatalogVersion(reviewer), CatalogGrant, "Withdrawn", "Synthetic withdrawal"))).StatusCode);
        var withdrawn = (await author.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Null(withdrawn.Assignment.Rank); Assert.Equal("PolicyUnavailable", withdrawn.Assignment.PositionState);
        Assert.DoesNotContain("SYNTHETIC-UNVERIFIED", await author.GetStringAsync($"/api/v1/vessels/{SeedVessel.Id}/crew/positions", Ct));
        await using var db = Db();
        Assert.Equal(4, await db.CrewCatalogEvents.CountAsync(Ct));
        Assert.Equal(4, await db.CommandReceipts.CountAsync(x => x.Action == "CrewCatalogSave", Ct));
        var e = await db.CrewCatalogEvents.FirstAsync(Ct); e.Reason = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        await using var auditDb = Db();
        var summaries = await auditDb.AuditEvents.Where(x => x.EntityType == nameof(CrewCatalogEvent)).Select(x => x.ChangeSummary).ToArrayAsync(Ct);
        Assert.Equal(4, summaries.Length); Assert.All(summaries, s => { Assert.DoesNotContain("SyntheticRank", s); Assert.DoesNotContain("Synthetic proposal", s); });
    }
    [Theory]
    [InlineData("Automation")]
    [InlineData("Service")]
    [InlineData("Unspecified")]
    public async Task Catalog_grants_never_let_nonhumans_publish(string kind)
    {
        using var host = CatalogHost(kind: kind); using var client = Client(host); await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, CatalogPath + "/events", new CrewCatalogCommand(0, CatalogGrant, "Draft", "Synthetic", CatalogContent()))).StatusCode);
        await using var db = Db(); Assert.False(await db.CrewCatalogEvents.AnyAsync(Ct));
    }
    [Fact]
    public async Task Missing_catalog_definition_can_remain_draft_but_cannot_be_reviewed_and_revoked_author_cannot_be_replaced_by_admin()
    {
        using var authorHost = CatalogHost(); using var reviewerHost = CatalogHost(reviewer: true);
        using var author = Client(authorHost); using var reviewer = Client(reviewerHost); await Token(author); await Token(reviewer);
        var incomplete = CatalogContent(); incomplete.TimeZone = "";
        Assert.Equal(HttpStatusCode.OK, (await Send(author, CatalogPath + "/events", new CrewCatalogCommand(0, CatalogGrant, "Draft", "Incomplete", incomplete))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(await CatalogVersion(reviewer), CatalogGrant, "Reviewed", "Incomplete review"))).StatusCode);
        using var revokedHost = CatalogHost(revoke: true); using var revoked = Client(revokedHost); await Token(revoked);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(revoked, CatalogPath + "/events", new CrewCatalogCommand(await CatalogVersion(author), CatalogGrant, "Draft", "Expired appointment", CatalogContent()))).StatusCode);
        using var genericHost = Host(crewRead: true, crewWrite: true); using var generic = Client(genericHost); await Token(generic);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(generic, CatalogPath + "/events", new CrewCatalogCommand(0, CatalogGrant, "Draft", "Generic admin", CatalogContent()))).StatusCode);
    }

    [Fact]
    public async Task Revoked_proposer_withdraws_current_catalog_and_receipt_recovery_without_erasing_history()
    {
        using var host = CatalogHost(); using var reviewerHost = CatalogHost(reviewer: true);
        using var client = Client(host); using var reviewer = Client(reviewerHost); await Token(client); await Token(reviewer);
        var command = new CrewCatalogCommand(0, CatalogGrant, "Draft", "Synthetic", CatalogContent()); var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(await CatalogVersion(reviewer), CatalogGrant, "Reviewed", "Independent"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(reviewer, CatalogPath + "/events", new CrewCatalogCommand(await CatalogVersion(reviewer), CatalogGrant, "Adopted", "Independent"))).StatusCode);
        Assert.Contains("SyntheticRank", await client.GetStringAsync($"/api/v1/vessels/{SeedVessel.Id}/crew/position-policy", Ct));
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var reviewerIdentity = settings.Memberships[1];
        settings.Memberships[1] = reviewerIdentity with { Subject = "rebound-synthetic-reviewer" };
        Assert.DoesNotContain("SyntheticRank", await client.GetStringAsync($"/api/v1/vessels/{SeedVessel.Id}/crew/position-policy", Ct));
        settings.Memberships[1] = reviewerIdentity;
        var m = settings.Memberships[0];
        settings.Memberships[0] = m with { CrewCatalogAuthorities = m.CrewCatalogAuthorities.Select(g => g with { Until = DateTimeOffset.UtcNow.AddSeconds(-1) }).ToArray() };
        Assert.DoesNotContain("SyntheticRank", await client.GetStringAsync($"/api/v1/vessels/{SeedVessel.Id}/crew/position-policy", Ct));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
        using var history = JsonDocument.Parse(await client.GetStringAsync("/api/v1/" + CatalogPath, Ct));
        Assert.Equal(3, history.RootElement.GetProperty("items").GetArrayLength());
        // A changed but still live appointment also cannot recover an earlier authority-bound receipt.
        settings.Memberships[0] = m with { CrewCatalogAuthorities = m.CrewCatalogAuthorities.Select(g => g with { Until = g.Until.AddMinutes(1) }).ToArray() };
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
    }

    private sealed class RejectCatalogReceipt : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO core.\"CommandReceipts\"", StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(p => p.Value is string s && s == "CrewCatalogSave")) throw new Npgsql.NpgsqlException("Synthetic catalog receipt failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Receipt_failure_rolls_back_catalog_event_head_and_audit_and_ordinary_retry_commits_once()
    {
        var fault = new RejectCatalogReceipt(); using var host = CatalogHost(interceptor: fault); using var client = Client(host); await Token(client);
        var command = new CrewCatalogCommand(0, CatalogGrant, "Draft", "Synthetic rollback", CatalogContent()); var key = Guid.NewGuid().ToString();
        fault.Armed = true;
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
        await using (var db = Db()) {
            Assert.False(await db.CrewCatalogHeads.AnyAsync(Ct)); Assert.False(await db.CrewCatalogEvents.AnyAsync(Ct));
            Assert.False(await db.CommandReceipts.AnyAsync(r => r.Action == "CrewCatalogSave", Ct));
            Assert.False(await db.AuditEvents.AnyAsync(a => a.EntityType == nameof(CrewCatalogEvent), Ct));
        }
        fault.Armed = false;
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CatalogPath + "/events", command, key)).StatusCode);
        await using var final = Db(); Assert.Equal(1, await final.CrewCatalogEvents.CountAsync(Ct));
        Assert.Equal(1, await final.CommandReceipts.CountAsync(r => r.Action == "CrewCatalogSave", Ct));
        await using var foreign = Db(seed.Tenants.Single(t => t.Id != SeedVessel.TenantId).Id);
        Assert.False(await foreign.CrewCatalogHeads.AnyAsync(Ct)); Assert.False(await foreign.CrewCatalogEvents.AnyAsync(Ct));
    }

    [Theory]
    [InlineData("omitted-base")]
    [InlineData("null-base")]
    [InlineData("duplicate-base")]
    [InlineData("forged-actor")]
    public async Task Raw_catalog_wire_rejects_missing_base_duplicates_and_authority_injection_before_any_mutation(string attack)
    {
        using var host = CatalogHost(); using var client = Client(host); await Token(client);
        var command = new CrewCatalogCommand(0, CatalogGrant, "Draft", "Synthetic wire", CatalogContent());
        var wire = JsonSerializer.Serialize(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wire = attack switch {
            "omitted-base" => wire.Replace("\"expectedVersion\":0,", "", StringComparison.Ordinal),
            "null-base" => wire.Replace("\"expectedVersion\":0", "\"expectedVersion\":null", StringComparison.Ordinal),
            "duplicate-base" => wire.Replace("\"expectedVersion\":0", "\"expectedVersion\":0,\"expectedVersion\":7", StringComparison.Ordinal),
            _ => wire[..^1] + ",\"actorId\":\"" + actorId + "\"}"
        };
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + CatalogPath + "/events") {
            Content = new StringContent(wire, System.Text.Encoding.UTF8, "application/json")
        };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(message, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Db(); Assert.False(await db.CrewCatalogEvents.AnyAsync(Ct));
    }
}
