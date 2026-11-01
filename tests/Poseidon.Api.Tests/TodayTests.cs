using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;
public sealed partial class DirectoryWorkspaceTests
{
    private string TodayPath => $"/api/v1/poseidon-today?vesselId={SeedVessel.Id}";
    private string WhyPath => $"/api/v1/why-engine?vesselId={SeedVessel.Id}&cardKey=regulatory-draft";

    [Fact]
    public async Task Today_reads_actual_draft_and_why_recomputes_inputs_without_writing_lifecycle()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.Equal(SeedVessel.Id, page.Vessel.Id);
        var card = Assert.Single(page.Cards.Items); Assert.Equal("Advisory", card.Priority); Assert.Null(card.Owner);
        Assert.Equal("DraftUnverified", card.Reason);
        Assert.Equal("Available", page.Coverage[0].Status);
        Assert.All(page.Coverage.Skip(1), x => Assert.Equal("Unavailable", x.Status));
        var initial = await Profile(client);
        await SaveProfile(client, initial, new RegulatoryDraftContent { Gt = new("Known", "0.00000000000000000001"), Flag = new("Unknown"),
            SourceObservedAt = new("Known", "2001-01-01T00:00:00Z"), SourceReference = new("Known", "synthetic-private-reference") });
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
        Assert.Equal(new DraftObservation(3, 13, 1, "DraftUnverified"), why.Inputs);
        Assert.Equal("Unverified", why.Freshness); Assert.Equal("DeclaredUnverified", why.Provenance);
        Assert.Equal(DateTimeOffset.Parse("2001-01-01T00:00:00Z"), DateTimeOffset.Parse(why.SourceObservedAt!));
        Assert.True(why.SavedAt > DateTimeOffset.Parse(why.SourceObservedAt!)); Assert.Null(why.Owner);
        Assert.Contains("VerificationUnavailable", why.Blockers);
        Assert.DoesNotContain("synthetic-private-reference", await client.GetStringAsync(WhyPath, Ct));
        await using var db = Db();
        Assert.Equal(1, await db.CommandReceipts.CountAsync(Ct)); // Actual save only, no Today lifecycle writes.
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "TodayWhyRead" && e.Result == "Success" && e.ResourceId == SeedVessel.Id);
    }

    [Fact]
    public async Task Today_absent_draft_and_empty_page_are_not_unavailable_or_clear()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        using var create = await Send(client, "vessels", new { companyId = SeedVessel.CompanyId, code = "TW-NEW", name = "Synthetic Today vessel", kind = "Tug" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var vessel = (await create.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var today = (await client.GetFromJsonAsync<TodayDto>($"/api/v1/poseidon-today?vesselId={vessel.Id}", Ct))!;
        Assert.Equal("DraftAbsent", Assert.Single(today.Cards.Items).Reason);
        Assert.Equal("Missing", today.Coverage[0].Status);
        var absentWhy = (await client.GetFromJsonAsync<TodayWhyDto>($"/api/v1/why-engine?vesselId={vessel.Id}&cardKey=regulatory-draft", Ct))!;
        Assert.Equal(new DraftObservation(0, 0, 0, "DraftAbsent"), absentWhy.Inputs);
        Assert.Null(absentWhy.SavedAt); Assert.Null(absentWhy.SourceObservedAt);
        Assert.Equal("Missing", absentWhy.Provenance); Assert.Equal("Unverified", absentWhy.Freshness);
        var empty = (await client.GetFromJsonAsync<TodayDto>(TodayPath + "&offset=1&limit=1", Ct))!;
        Assert.Empty(empty.Cards.Items); Assert.False(empty.Cards.HasMore); Assert.Equal(5, empty.Coverage.Length);
        var again = (await client.GetFromJsonAsync<TodayDto>(TodayPath + "&offset=0&limit=1", Ct))!;
        Assert.Equal("regulatory-draft", Assert.Single(again.Cards.Items).Key);
    }

    [Fact]
    public async Task Today_rejects_spoofed_queries_and_never_leaks_out_of_scope_why()
    {
        await using var host = Host(manage: false, read: false, edit: false); using var client = Client(host);
        foreach (var extra in new[] { "&tenantId=" + seed.Tenants[1].Id, "&reason=Ready", "&sourcePath=secret", "&offset=-1", "&limit=26", "&offset=100001", "&limit=no", "&vesselId=" + SeedVessel.Id })
        {
            using var invalid = await client.GetAsync(TodayPath + extra, Ct); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seed.Tenants[1].Id.ToString());
        foreach (var vessel in seed.Vessels.Where(x => x.Id != SeedVessel.Id))
        foreach (var path in new[] { $"/api/v1/poseidon-today?vesselId={vessel.Id}", $"/api/v1/why-engine?vesselId={vessel.Id}&cardKey=regulatory-draft" })
        {
            using var response = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(vessel.Name, await response.Content.ReadAsStringAsync(Ct));
        }
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
        Assert.Equal("ViewDraft", why.NextAction);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [] }];
        foreach (var path in new[] { TodayPath, WhyPath })
        {
            using var response = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "TodayWhyRead" && e.Result == "Denied" && e.ResourceId == null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Today_success_and_denial_fail_closed_when_audit_is_unavailable(bool anonymous)
    {
        await using var host = Host(); using var client = Client(host, !anonymous);
        await Sql("""CREATE TRIGGER today_reject BEFORE INSERT ON audit."SecurityEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
        foreach (var path in new[] { TodayPath, WhyPath, $"/API/V1/POSEIDON-TODAY/?vesselId={SeedVessel.Id}", $"/API/V1/WHY-ENGINE/?vesselId={SeedVessel.Id}&cardKey=regulatory-draft" })
        {
            using var response = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.DoesNotContain(SeedVessel.Name, await response.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task Today_anonymous_denials_are_audited_before_returning_401()
    {
        await using var host = Host(); using var client = Client(host, false);
        foreach (var path in new[] { TodayPath, WhyPath })
        {
            using var response = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        // Anonymous audit rows intentionally have no tenant and are invisible to
        // the normal tenant projection. Inspect only the fixed aggregate here.
        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(Ct);
        await using var count = new Npgsql.NpgsqlCommand("""
            SELECT count(DISTINCT "Action") FROM audit."SecurityEvents"
            WHERE "TenantId" IS NULL AND "ActorId" IS NULL
              AND "Action" IN ('TodayRead', 'TodayWhyRead') AND "Result" = 'Unauthenticated'
            """, connection);
        Assert.Equal(2L, await count.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Why_rechecks_removed_vessel_grant_even_when_other_directory_access_remains()
    {
        await using var host = Host(manage: false, read: false, edit: false); using var client = Client(host);
        using var before = await client.GetAsync(TodayPath, Ct);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("no-store", before.Headers.CacheControl?.ToString());
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var other = seed.Vessels.First(x => x.TenantId == SeedVessel.TenantId && x.Id != SeedVessel.Id);
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [new(other.Id)] }];
        foreach (var path in new[] { WhyPath, TodayPath })
        {
            using var denied = await client.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.DoesNotContain(SeedVessel.Name, await denied.Content.ReadAsStringAsync(Ct));
        }
        await using var db = Db();
        Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "TodayWhyRead" && e.Result == "NotFound" && e.ResourceId == null);
    }

    [Theory]
    [InlineData("&reason=Ready")]
    [InlineData("&sourcePath=synthetic-private-path")]
    [InlineData("&cardKey=regulatory-draft")]
    [InlineData("&tenantId=00000000-0000-0000-0000-000000000000")]
    [InlineData("&offset=0")]
    public async Task Why_rejects_client_supplied_authority_and_duplicate_keys_with_fixed_audit(string extra)
    {
        await using var host = Host(); using var client = Client(host);
        using var response = await client.GetAsync(WhyPath + extra, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("synthetic-private-path", await response.Content.ReadAsStringAsync(Ct));
        await using var db = Db();
        var audit = Assert.Single(await db.Set<SecurityEvent>().Where(e => e.Action == "TodayWhyRead").ToArrayAsync(Ct));
        Assert.Equal("Invalid", audit.Result); Assert.Null(audit.ResourceId);
        Assert.Equal(actorId.ToString(), audit.ActorId); Assert.Equal(SeedVessel.TenantId, audit.TenantId);
    }
}
