using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string EquipmentUrl => $"equipment-hierarchy/{SeedVessel.Id}";
    private string PlansUrl => $"maintenance-plan/{SeedVessel.Id}";
    private EquipmentCommand NewEquipment(Guid? id = null, Guid? parent = null) => new(id ?? Guid.NewGuid(), null, parent,
        null, "FICTIONAL equipment", "Equipment", null, null, null, null, null, null, null, null, null, null);
    [Fact]
    public async Task Equipment_reads_writes_history_references_counts_and_role_spoofing_require_real_object_authority() {
        using var host = Host(); using var client = Client(host); await Token(client);
        client.DefaultRequestHeaders.Add("X-Role", "Chief Engineer");
        foreach (var url in new[] { EquipmentUrl, EquipmentUrl + $"/{SeedVessel.Id}/history", PlansUrl, PlansUrl + "/references", PlansUrl + $"/{SeedVessel.Id}/history" }) {
            var r = await client.GetAsync("/api/v1/" + url, Ct); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            var body = await r.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("items", body); Assert.DoesNotContain(SeedVessel.Id.ToString(), body);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, EquipmentUrl, NewEquipment())).StatusCode);
        using var scoped = Host(scope: false, technicalRead: true, technicalWrite: true); using var outside = Client(scoped); await Token(outside);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(outside, EquipmentUrl, NewEquipment())).StatusCode);
    }
    [Fact]
    public async Task Equipment_commit_receipt_survives_host_restart_replay_without_duplicate_history_or_business_audit() {
        var cmd = NewEquipment(); var key = Guid.NewGuid().ToString(); TechnicalSaved first;
        using (var host = Host(technicalRead: true, technicalWrite: true)) {
            using var client = Client(host); await Token(client);
            var r = await Send(client, EquipmentUrl, cmd, key); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            first = (await r.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!; Assert.True(first.Version > 0);
        }
        int before;
        await using (var db = Db()) before = await db.AuditEvents.CountAsync(Ct);
        using var restarted = Host(technicalRead: true, technicalWrite: true); using var retry = Client(restarted); await Token(retry);
        var replay = await Send(retry, EquipmentUrl, cmd, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var saved = (await replay.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.True(saved.Replayed); Assert.Equal(first.Version, saved.Version);
        await using var verify = Db(); Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct));
        Assert.Single(await verify.AssetRevisions.Where(x => x.AssetId == cmd.Id).ToArrayAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(retry, EquipmentUrl, cmd with { Name = "different" }, key)).StatusCode);
    }
    [Fact]
    public async Task Revoked_permission_is_checked_before_receipt_and_does_not_return_old_result() {
        var cmd = NewEquipment(); var key = Guid.NewGuid().ToString();
        using (var host = Host(technicalRead: true, technicalWrite: true)) {
            using var client = Client(host); await Token(client); Assert.Equal(HttpStatusCode.OK, (await Send(client, EquipmentUrl, cmd, key)).StatusCode);
        }
        using var revoked = Host(technicalRead: true); using var retry = Client(revoked); await Token(retry);
        var r = await Send(retry, EquipmentUrl, cmd, key); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.DoesNotContain("replayed", await r.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Cycle_self_orphan_cross_scope_and_stale_version_cannot_append_revisions() {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var cmd = NewEquipment(parent: SeedVessel.Id); var saved = await Send(client, EquipmentUrl, cmd);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); var child = (await saved.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        var rows = (await client.GetFromJsonAsync<EquipmentSnapshot>("/api/v1/" + EquipmentUrl, Ct))!;
        Assert.True(rows.Complete); var root = rows.Items.Single(x => x.Id == SeedVessel.Id);
        var cycle = NewEquipment(root.Id, child.Id) with { BaseVersion = root.Version };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, cycle)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, NewEquipment(cmd.Id, cmd.Id) with { BaseVersion = child.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, NewEquipment(parent: Guid.NewGuid()))).StatusCode);
        var other = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, NewEquipment(parent: other.Id))).StatusCode);
        var edit = cmd with { BaseVersion = child.Version, Location = "Synthetic update" };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, EquipmentUrl, edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, EquipmentUrl, edit with { Location = "stale" })).StatusCode);
        await using var verify = Db(); Assert.Equal(2, await verify.AssetRevisions.CountAsync(x => x.AssetId == cmd.Id, Ct));
    }
    [Fact]
    public async Task Plan_uses_actual_asset_job_and_owner_and_freezes_calculation_and_policy_deferral_is_audited() {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var owner = Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535");
        var clock = new DateTimeOffset(2026,11,1,0,0,0,TimeSpan.Zero);
        var inputs = new MaintenanceInputs(TriggerType: "Calendar", IntervalValue: "1", IntervalUnit: "month", LastDoneDate: new(2026,10,1),
            TimeZone: "Europe/Istanbul", Provenance: "SyntheticDeclaration", Criticality: "DeclaredCritical");
        var cmd = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, SeedVessel.Id, owner, inputs, clock);
        var response = await Send(client, PlansUrl, cmd); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl, Ct);
        var row = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == cmd.Id);
        Assert.Equal("Due", row.GetProperty("due").GetProperty("state").GetString());
        Assert.Equal("2026-11-01", row.GetProperty("due").GetProperty("nextDueDate").GetString());
        Assert.Equal(1, row.GetProperty("assetRevision").GetInt64()); Assert.Equal("Draft", row.GetProperty("status").GetString());
        var deniedOwner = await Send(client, PlansUrl, cmd with { Id = Guid.NewGuid(), OwnerRoleId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, deniedOwner.StatusCode);
        var badJob = await Send(client, PlansUrl, cmd with { Id = Guid.NewGuid(), JobId = Guid.NewGuid() }); Assert.Equal(HttpStatusCode.NotFound, badJob.StatusCode);
        await using var before = Db(); var revisionCount = await before.MaintenancePlanRevisions.CountAsync(Ct);
        var postpone = await Send(client, PlansUrl + $"/{cmd.Id}/postpone", new { reason = "synthetic", authority = "Chief Engineer", newDue = "2099-01-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, postpone.StatusCode);
        await using var verify = Db(); Assert.Equal(revisionCount, await verify.MaintenancePlanRevisions.CountAsync(Ct));
        Assert.Contains(await verify.SecurityEvents.ToArrayAsync(Ct), x => x.Action == "MaintenanceSave" && x.Result == "validation_failed");
        var history = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl + $"/{cmd.Id}/history", Ct);
        Assert.Equal("1", history[0].GetProperty("inputs").GetProperty("intervalValue").GetString());
    }
    [Fact]
    public async Task Caller_actor_tenant_and_admin_import_inputs_are_not_bound_and_csrf_is_required() {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, NewEquipment())).StatusCode);
        await Token(client);
        var body = new { id = Guid.NewGuid(), baseVersion = (uint?)null, level = "Equipment", actorId = Guid.NewGuid(), tenantId = Guid.NewGuid(), isAdministrativeImport = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, body)).StatusCode);
    }
    [Fact]
    public async Task Current_and_legacy_plan_aliases_share_exact_storage_receipts_and_calculation_history() {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var cmd = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, null, null, new(), DateTimeOffset.UnixEpoch);
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"maintenance-plans?vessel={SeedVessel.Id}", cmd, key)).StatusCode);
        var replay = await Send(client, $"maintenance-plan?vessel={SeedVessel.Id}", cmd, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True((await replay.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!.Replayed);
        var canonical = await client.GetFromJsonAsync<JsonElement>($"/api/v1/maintenance-plan?vessel={SeedVessel.Id}", Ct);
        Assert.Contains(canonical.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == cmd.Id);
        var assets = await client.GetFromJsonAsync<EquipmentSnapshot>($"/api/v1/assets?vessel={SeedVessel.Id}", Ct);
        Assert.True(assets!.Complete); Assert.Contains(assets.Items, x => x.Id == SeedVessel.Id);
        await using var verify = Db(); Assert.Single(await verify.MaintenancePlanRevisions.Where(x => x.PlanId == cmd.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Two_actual_API_connections_with_valid_old_snapshots_cannot_commit_a_combined_cycle() {
        var a = NewEquipment(); var b = NewEquipment(); TechnicalSaved av; TechnicalSaved bv;
        using (var prepare = Host(technicalRead: true, technicalWrite: true)) {
            using var client = Client(prepare); await Token(client);
            var ar = await Send(client, EquipmentUrl, a); var br = await Send(client, EquipmentUrl, b);
            Assert.Equal(HttpStatusCode.OK, ar.StatusCode); Assert.Equal(HttpStatusCode.OK, br.StatusCode);
            av = (await ar.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!; bv = (await br.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        }
        // Controlled schedule only; both real PostgreSQL SELECTs have established their snapshots.
        var barrier = new TechnicalSnapshotBarrier();
        using var host = Host(technicalRead: true, technicalWrite: true, interceptor: barrier);
        using var first = Client(host); using var second = Client(host); await Token(first); await Token(second);
        var results = await Task.WhenAll(Send(first, EquipmentUrl, a with { BaseVersion = av.Version, ParentId = b.Id }),
            Send(second, EquipmentUrl, b with { BaseVersion = bv.Version, ParentId = a.Id }));
        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var verify = Db(); var rows = await verify.Assets.Where(x => x.VesselId == SeedVessel.Id).ToArrayAsync(Ct);
        Assert.Equal(EquipmentHierarchyValidation.Valid, EquipmentHierarchyPolicy.Validate(SeedVessel.TenantId, SeedVessel.Id,
            rows.Select(x => new EquipmentHierarchyNode(x.TenantId,x.VesselId,x.Id,x.ParentId)).ToArray()));
        Assert.Equal(3, await verify.CommandReceipts.CountAsync(x => x.Action == "EquipmentSave", Ct));
    }
    private sealed class TechnicalSnapshotBarrier : DbCommandInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("FROM technical.\"Assets\"", StringComparison.Ordinal)) {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
