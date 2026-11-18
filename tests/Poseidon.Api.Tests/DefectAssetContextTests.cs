using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// Owning API/real PostgreSQL sources; authoring does not execute these cases.
public sealed partial class DirectoryWorkspaceTests
{
    private string DefectAssetPath(Guid id, Guid? vessel = null) =>
        $"/api/v1/vessels/{vessel ?? SeedVessel.Id}/defect-asset-context/{id}";

    private static void EnableDefectContext(IServiceProvider services)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = [.. member.Permissions, "defect.read"] };
    }

    [Fact]
    public async Task Defect_context_binds_actual_equipment_xmin_and_snapshot_without_fabricating_a_historical_committed_version()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        EnableDefectContext(host.Services); await Token(client);
        var command = NewEquipment() with { Name = "FICTIONAL source equipment", Criticality = "DeclaredCritical", Notes = "FICTIONAL private source notes" };
        using var saved = await Send(client, EquipmentUrl, command); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var first = (await saved.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        var context = (await client.GetFromJsonAsync<DefectAssetContextDto>(DefectAssetPath(command.Id) + "?revision=1", Ct))!;
        Assert.True(context.Current); Assert.Equal(first.Version, context.Pin.Version); Assert.Equal(first.Revision, context.Pin.Revision);
        Assert.Equal(command.Id, context.Pin.Id); Assert.Equal(64, context.Pin.Hash.Length);
        Assert.Equal("REVIEW_REQUIRED", context.Qualification); Assert.Equal(command.Name, context.Asset.Name);
        Assert.Equal("DeclaredCritical", context.Asset.Criticality);
        using var raw = await client.GetAsync(DefectAssetPath(command.Id), Ct);
        var body = await raw.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("FICTIONAL private source notes", body); Assert.DoesNotContain("responsibleRoleId", body);
        Assert.DoesNotContain("serialNumber", body); Assert.DoesNotContain("tenantId", body);
        using var changed = await Send(client, EquipmentUrl, command with { BaseVersion = first.Version, Name = "FICTIONAL changed equipment" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var old = await client.GetAsync(DefectAssetPath(command.Id) + "?revision=1", Ct);
        Assert.Equal(HttpStatusCode.Conflict, old.StatusCode);
        Assert.Contains("asset_context_stale", await old.Content.ReadAsStringAsync(Ct));
        Assert.DoesNotContain("FICTIONAL source equipment", await old.Content.ReadAsStringAsync(Ct));
        var current = (await client.GetFromJsonAsync<DefectAssetContextDto>(DefectAssetPath(command.Id), Ct))!;
        Assert.Equal(2, current.Pin.Revision); Assert.NotEqual(context.Pin.Hash, current.Pin.Hash);
    }

    [Fact]
    public async Task Defect_context_requires_both_actual_permissions_and_rejects_query_authority_and_foreign_vessel_inputs()
    {
        using var host = Host(technicalRead: true); using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Permissions", "defect.read equipment.read");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(DefectAssetPath(SeedVessel.Id), Ct)).StatusCode);
        EnableDefectContext(host.Services);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        // TechnicalFixtures creates a real root asset with the vessel UUID in
        // each tenant. Prove existence before checking isolation: a random or
        // mismatched nonexistent asset would make this denial vacuous.
        await using (var foreignDb = Db(foreign.TenantId))
            Assert.True(await foreignDb.Assets.AnyAsync(a => a.VesselId == foreign.Id && a.Id == foreign.Id, Ct));
        using var foreignContext = await client.GetAsync(DefectAssetPath(foreign.Id, foreign.Id), Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreignContext.StatusCode);
        Assert.DoesNotContain("pin", await foreignContext.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(DefectAssetPath(foreign.Id), Ct)).StatusCode);
        foreach (var suffix in new[] { "?tenantId=" + foreign.TenantId, "?revision=1&revision=1", "?revision=0", "?revision=-1", "?revision=1&actorId=" + SeedVessel.Id })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(DefectAssetPath(SeedVessel.Id) + suffix, Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "equipment.read").ToArray() };
        using var denied = await client.GetAsync(DefectAssetPath(SeedVessel.Id), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.DoesNotContain("pin", await denied.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("source-head")]
    public async Task Defect_context_is_withheld_when_permission_or_exact_head_changes_during_the_success_audit(string change)
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        EnableDefectContext(host.Services); await Token(client);
        var command = NewEquipment() with { Name = "FICTIONAL old private equipment" };
        using var saved = await Send(client, EquipmentUrl, command); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var first = (await saved.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        var key = (long)Random.Shared.Next(1, int.MaxValue);
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", blocker)) {
            acquire.Parameters.AddWithValue("key", key); await acquire.ExecuteNonQueryAsync(Ct);
        }
        await Sql($$"""
            CREATE FUNCTION audit.s8_defect_asset_pause() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
            CREATE TRIGGER s8_defect_asset_pause BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DefectAssetContextRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s8_defect_asset_pause();
            """);
        var pending = client.GetAsync(DefectAssetPath(command.Id), Ct); HttpResponseMessage? response = null;
        try {
            await WaitForComplianceAuditLock(key);
            if (change == "permission") {
                var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
                settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "defect.read").ToArray() };
            } else {
                using var changed = await Send(client, EquipmentUrl, command with { BaseVersion = first.Version, Name = "FICTIONAL new equipment" });
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            }
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) {
                release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct);
            }
            response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("FICTIONAL old private equipment", body); Assert.DoesNotContain("pin", body);
        }
        finally {
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) {
                release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct);
            }
            response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose();
            await Sql("""DROP TRIGGER s8_defect_asset_pause ON audit."SecurityEvents"; DROP FUNCTION audit.s8_defect_asset_pause();""");
        }
    }
}
