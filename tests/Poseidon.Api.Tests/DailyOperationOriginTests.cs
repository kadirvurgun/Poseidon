using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-100/101 consume actual saved DEV-097/099 declarations. Owned real API/PG
// source tests; no test/build/database/provider execution at authoring time.
public sealed partial class DirectoryWorkspaceTests
{
    private string OriginPath(string kind, Guid id, Guid? vessel = null) =>
        $"/api/v1/vessels/{vessel ?? SeedVessel.Id}/ship-operation-origins/{kind}/{id}";

    [Fact]
    public async Task Daily_origin_context_preserves_exact_saved_history_and_never_converts_unstructured_summary_to_fuel_mass() {
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services);
        var command = new DailyDraftCommand(Guid.NewGuid(), 0, new() { ["fuel_summary"] = new("Known", "FICTIONAL unstructured fuel summary"), ["rob_summary"] = new("Unknown", null) }, "INITIAL_AUTHORING");
        using var created = await DailySend(client, "noon-reports", command, Guid.NewGuid().ToString("D")); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var origin = (await client.GetFromJsonAsync<DailyOperationOriginDto>(OriginPath("Noon", first.Id), Ct))!;
        Assert.True(origin.Current); Assert.Equal(first.Hash, origin.Pin.Hash); Assert.Equal(first.Version, origin.Pin.Version); Assert.Equal(first.Revision, origin.Pin.Revision);
        Assert.Equal("DRAFT", origin.State); Assert.Equal("REVIEW_REQUIRED", origin.Qualification);
        Assert.Equal("FICTIONAL unstructured fuel summary", Assert.Single(origin.Fields, field => field.Key == "fuel_summary").Value.Value);
        Assert.Equal("Unknown", Assert.Single(origin.Fields, field => field.Key == "rob_summary").Value.State);
        Assert.DoesNotContain(origin.Fields, field => field.Key is "quantity_received_mt" or "quantity_consumed_mt" or "reviewed_by" or "evidence_ref");
        var changed = command with { ExpectedVersion = first.Version, ReasonCode = "AUTHORING_CORRECTION", Fields = new() { ["fuel_summary"] = new("Known", "FICTIONAL corrected summary") } };
        using var updated = await DailySend(client, "noon-reports", changed, Guid.NewGuid().ToString("D"), update: true); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var historical = (await client.GetFromJsonAsync<DailyOperationOriginDto>(OriginPath("Noon", first.Id) + "?revision=1", Ct))!;
        Assert.False(historical.Current); Assert.Equal(origin.Pin, historical.Pin);
        Assert.Equal("FICTIONAL unstructured fuel summary", Assert.Single(historical.Fields, field => field.Key == "fuel_summary").Value.Value);
        var current = (await client.GetFromJsonAsync<DailyOperationOriginDto>(OriginPath("Noon", first.Id), Ct))!;
        Assert.True(current.Current); Assert.Equal(2, current.Pin.Revision); Assert.NotEqual(first.Hash, current.Pin.Hash);
    }

    [Fact]
    public async Task Origin_read_requires_owning_daily_permission_and_exact_vessel_scope_without_query_authority_fallback() {
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services);
        var command = new DailyDraftCommand(Guid.NewGuid(), 0, new(), "INITIAL_AUTHORING");
        using var saved = await DailySend(client, "engine-daily-logs", command, Guid.NewGuid().ToString("D")); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var original = settings.Memberships[0];
        client.DefaultRequestHeaders.Add("X-Permissions", "daily.engine.read operations.fuel.write");
        var foreign = seed.Vessels.First(vessel => vessel.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(OriginPath("Engine", command.Id, foreign.Id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(OriginPath("Engine", command.Id) + "?tenantId=" + foreign.TenantId, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(OriginPath("Engine", command.Id) + "?revision=1&revision=2", Ct)).StatusCode);
        settings.Memberships[0] = original with { Permissions = original.Permissions.Where(permission => permission != "daily.engine.read").Append("operations.fuel.read").ToArray() };
        var denied = await client.GetAsync(OriginPath("Engine", command.Id), Ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain("fields", await denied.Content.ReadAsStringAsync(Ct));
    }

    private Task InstallOriginAuditPause(long key) => Sql($$"""
        CREATE FUNCTION audit.s6_origin_pause() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
        CREATE TRIGGER s6_origin_pause BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
          WHEN (NEW."Action"='DailyOperationsRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_origin_pause();
        """);
    private Task RemoveOriginAuditPause() => Sql("""DROP TRIGGER s6_origin_pause ON audit."SecurityEvents"; DROP FUNCTION audit.s6_origin_pause();""");

    [Theory]
    [InlineData("permission")]
    [InlineData("source-head")]
    public async Task Origin_context_is_withheld_if_its_scope_or_current_head_changes_during_success_audit(string change) {
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services);
        var command = new DailyDraftCommand(Guid.NewGuid(), 0, new() { ["fuel_summary"] = new("Known", "FICTIONAL private original") }, "INITIAL_AUTHORING");
        using var created = await DailySend(client, "noon-reports", command, Guid.NewGuid().ToString("D")); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var key = (long)Random.Shared.Next(1, int.MaxValue);
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", blocker)) { acquire.Parameters.AddWithValue("key", key); await acquire.ExecuteNonQueryAsync(Ct); }
        // Test-only DDL synchronizes an actual read-audit await; source-head
        // mutation below still uses the ordinary audited owning command.
        await InstallOriginAuditPause(key);
        var pending = client.GetAsync(OriginPath("Noon", first.Id), Ct); HttpResponseMessage? response = null;
        try {
            await WaitForComplianceAuditLock(key);
            if (change == "permission") {
                var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
                settings.Memberships[0] = member with { Permissions = member.Permissions.Where(permission => permission != "daily.noon.read").ToArray() };
            }
            else {
                using var changed = await DailySend(client, "noon-reports", command with { ExpectedVersion = first.Version, ReasonCode = "AUTHORING_CORRECTION", Fields = new() { ["fuel_summary"] = new("Known", "FICTIONAL new head") } }, Guid.NewGuid().ToString("D"), update: true);
                Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            }
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("FICTIONAL private original", body); Assert.DoesNotContain("fuel_summary", body);
        }
        finally {
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); await RemoveOriginAuditPause();
        }
    }
}
