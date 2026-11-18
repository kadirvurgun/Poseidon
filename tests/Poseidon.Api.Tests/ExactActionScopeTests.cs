using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5402 / 54.02 Rev4 A02-004 step 8, A03-007 step 8: exact-action decisions are
// re-resolved from the CURRENT membership inside the write transaction immediately before commit.
// Real PostgreSQL/API; synthetic membership only, no production identity or role catalog.
public sealed partial class DirectoryWorkspaceTests
{
    // Pauses the request right after its command receipt INSERT, i.e. inside the open write
    // transaction and after the first authorization check, so the test can revoke membership.
    private sealed class ReceiptInsertBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed, skip;
        // skip: number of later receipt INSERTs to let pass before pausing (e.g. import row receipts).
        public void Arm(int skipInserts = 0) { Interlocked.Exchange(ref skip, skipInserts); Interlocked.Exchange(ref armed, 1); }
        public Task WaitUntilReachedAsync(CancellationToken ct) => reached.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        public void Release() => released.TrySetResult();
        private async Task Pause(DbCommand command, CancellationToken ct)
        {
            if (command.CommandText.Contains("INSERT INTO", StringComparison.Ordinal) &&
                command.CommandText.Contains("\"CommandReceipts\"", StringComparison.Ordinal) &&
                Volatile.Read(ref armed) == 1 && Interlocked.Decrement(ref skip) < 0 &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                reached.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default)
        { await Pause(command, ct); return result; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken ct = default)
        { await Pause(command, ct); return result; }
    }

    private async Task<HttpResponseMessage> RevokeWhilePaused(WebApplicationFactory<Program> host, ReceiptInsertBarrier barrier, Task<HttpResponseMessage> pending, Func<Membership, Membership> change)
    {
        await barrier.WaitUntilReachedAsync(Ct);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [change(settings.Memberships[0])];
        barrier.Release();
        return await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
    }

    // P5402-AC-10
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_profile_save_revoked_inside_transaction_rolls_back_draft_history_and_receipt()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        int revisions; await using (var count = Db()) revisions = await count.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct);
        barrier.Arm();
        var pending = Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, before.Content with { Flag = new("Unknown") }), update: true);
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { TenantWideManage = false, VesselGrants = [new(SeedVessel.Id, false)] });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("permission_denied", await response.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
            Assert.Equal(revisions, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
            Assert.Equal(before.Version, (await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct)).Version);
            Assert.DoesNotContain(await db.AuditEvents.Where(x => x.ActorId == actorId.ToString()).ToArrayAsync(Ct), x => x.EntityType == nameof(RegulatoryDraftRevision));
            Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "ProfileSave" && x.Result == "permission_denied");
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-11: an unrelated authority change bumps the epoch; the decision is recomputed, not reused or blanket-rejected.
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_profile_save_recomputes_on_unrelated_epoch_change_and_commits()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        barrier.Arm();
        var pending = Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, before.Content), update: true);
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { Permissions = [.. m.Permissions, "workspace.search"] });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var db = Db(); Assert.Single(await db.CommandReceipts.ToArrayAsync(Ct));
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-12
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_vessel_update_revoked_inside_transaction_leaves_directory_row_unchanged()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host); await Token(client);
        var path = $"vessels/{SeedVessel.Id}";
        var before = await DirectoryRow(client, path, false);
        barrier.Arm();
        var pending = Send(client, path, new { code = before.Code, name = "Synthetic revoked edit", expectedVersion = before.Version }, update: true);
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { TenantWideManage = false, VesselGrants = [new(SeedVessel.Id, false)] });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("permission_denied", await response.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            var row = await db.Vessels.SingleAsync(v => v.Id == SeedVessel.Id, Ct);
            Assert.Equal((before.Name, before.Version), (row.Name, row.Version));
            Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
            Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "VesselUpdate" && x.Result == "permission_denied");
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-13
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_register_create_revoked_inside_transaction_creates_no_draft()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = Host(interceptor: barrier, regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        int drafts; await using (var db = Db()) drafts = await db.RegisterDrafts.CountAsync(Ct);
        barrier.Arm();
        var pending = Send(client, "source-drafts", new RegisterCommand(0, new() { Title = new("Known", "FICTIONAL 5402 revoked") }));
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { Permissions = m.Permissions.Where(p => p != "regulatory.draft.write").ToArray() });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await using var db = Db();
            Assert.Equal(drafts, await db.RegisterDrafts.CountAsync(Ct));
            Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
            Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "RegisterSave" && x.Result == "permission_denied");
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-15: the commit-time recheck includes the live vessel scope, not only the exact action.
    // Kind visibility (mode.tug/mode.cross) removed mid-transaction while directory.write and
    // TenantWideManage stay intact must still roll back the vessel update.
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_vessel_update_with_kind_scope_removed_inside_transaction_rolls_back()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host); await Token(client);
        var path = $"vessels/{SeedVessel.Id}";
        var before = await DirectoryRow(client, path, false);
        barrier.Arm();
        var pending = Send(client, path, new { code = before.Code, name = "Synthetic mode-scoped edit", expectedVersion = before.Version }, update: true);
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { Permissions = SeedVessel.Kind == VesselKind.Tug
                ? m.Permissions.Where(p => p != "mode.tug").ToArray()
                : [.. m.Permissions.Where(p => p != "mode.cross"), "mode.tug"] });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("permission_denied", await response.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            var row = await db.Vessels.SingleAsync(v => v.Id == SeedVessel.Id, Ct);
            Assert.Equal((before.Name, before.Version), (row.Name, row.Version));
            Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    private async Task<(ImportBatchDto Batch, ImportCommitCommand Command, VesselDto Before, int VesselAudits, int Receipts)> VesselImportReady(HttpClient client)
    {
        var before = (await client.GetFromJsonAsync<VesselDto>("/api/v1/vessels/" + SeedVessel.Id, Ct))!;
        var batch = await UploadImport(client, "vessel", new Dictionary<string, string> { ["record_key"] = before.Id.ToString(),
            ["code"] = before.Code, ["name"] = "FICTIONAL 5402 import revoked",
            ["base_version"] = before.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var preview = await PreviewImport(client, batch); Assert.Equal(1, preview.ValidRows);
        await using var db = Db();
        return (batch, new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision, System.Globalization.CultureInfo.InvariantCulture), preview.TemplateFingerprint),
            before, await db.AuditEvents.CountAsync(x => x.EntityType == "Vessel", Ct), await db.CommandReceipts.CountAsync(Ct));
    }

    private async Task AssertImportRolledBack(HttpResponseMessage response, ImportBatchDto batch, VesselDto before, int vesselAudits, int receipts)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("permission_denied", await response.Content.ReadAsStringAsync(Ct));
        await using var db = Db();
        var row = await db.Vessels.SingleAsync(v => v.Id == SeedVessel.Id, Ct);
        Assert.Equal((before.Name, before.Version), (row.Name, row.Version));
        Assert.Equal(vesselAudits, await db.AuditEvents.CountAsync(x => x.EntityType == "Vessel", Ct));
        Assert.Equal(receipts, await db.CommandReceipts.CountAsync(Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct));
        Assert.NotEqual("Committed", (await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct)).State);
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "ImportSave" && x.Result == "permission_denied");
    }

    // P5402-AC-16: vessel-row exact action revoked inside the outer SERIALIZABLE import transaction
    // (paused after the row receipt INSERT) rolls back the whole batch with 403 permission_denied.
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_import_vessel_row_revoked_inside_batch_transaction_returns_403_and_persists_nothing()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = ImportHost(interceptor: barrier); using var client = Client(host); await Token(client);
        var (batch, command, before, audits, receipts) = await VesselImportReady(client);
        barrier.Arm();
        var pending = Send(client, Imports + $"/{batch.Id}/commit", command, Guid.NewGuid().ToString());
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { TenantWideManage = false, VesselGrants = [new(SeedVessel.Id, false)] });
            await AssertImportRolledBack(response, batch, before, audits, receipts);
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-17: the batch-level revalidation right before the outer commit (paused after the
    // ImportCommit receipt INSERT, i.e. after every row succeeded) also rolls back with 403.
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_import_batch_revoked_before_outer_commit_returns_403_and_persists_nothing()
    {
        var barrier = new ReceiptInsertBarrier();
        await using var host = ImportHost(interceptor: barrier); using var client = Client(host); await Token(client);
        var (batch, command, before, audits, receipts) = await VesselImportReady(client);
        barrier.Arm(skipInserts: 1); // let the row receipt pass; pause on the ImportCommit receipt
        var pending = Send(client, Imports + $"/{batch.Id}/commit", command, Guid.NewGuid().ToString());
        HttpResponseMessage? response = null;
        try
        {
            response = await RevokeWhilePaused(host, barrier, pending, m => m with { TenantWideManage = false, VesselGrants = [new(SeedVessel.Id, false)] });
            await AssertImportRolledBack(response, batch, before, audits, receipts);
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    // P5402-AC-03/04 against the host's real server-provisioned membership (not a request input).
    [Fact]
    [Trait("Requirement", "PLAN-1511-5402")]
    public async Task Exact_decision_from_provisioned_membership_denies_out_of_scope_purpose_vessel_and_tenant()
    {
        await using var host = Host(manage: false, read: false, regulatoryRead: true, regulatoryWrite: true);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "synthetic"), new Claim("iss", "https://directory-test.example")], "synthetic"));
        var actor = host.Services.GetRequiredService<ActorResolver>().Resolve(principal);
        Assert.NotNull(actor);
        var tenant = seed.Tenants[0].Id;
        var save = new ExactAccessRequest(ExactActionCatalog.VesselRegulatoryProfileDraftSave, ExactActionCatalog.PurposeRegulatoryProfileDraft, tenant, SeedVessel.Id);
        Assert.True(ExactActionAccess.Evaluate(actor, save).Allowed);
        Assert.Equal(ExactAccessReasons.PurposeNotPermitted, ExactActionAccess.Evaluate(actor, save with { Purpose = ExactActionCatalog.PurposeRegisterDraft }).ReasonCode);
        Assert.Equal(ExactAccessReasons.PurposeNotPermitted, ExactActionAccess.Evaluate(actor, save with { Purpose = "export" }).ReasonCode);
        var other = seed.Vessels.First(v => v.TenantId == tenant && v.Id != SeedVessel.Id);
        Assert.Equal(ExactAccessReasons.NoGrant, ExactActionAccess.Evaluate(actor, save with { VesselId = other.Id }).ReasonCode);
        Assert.Equal(ExactAccessReasons.ScopeMismatch, ExactActionAccess.Evaluate(actor, save with { TenantId = Guid.NewGuid() }).ReasonCode);
        // Vessel-only grant + regulatory permissions still confer no tenant register authority.
        Assert.Equal(ExactAccessReasons.NoGrant, ExactActionAccess.Evaluate(actor,
            new(ExactActionCatalog.RegulatorySourceCreate, ExactActionCatalog.PurposeRegisterDraft, tenant, null)).ReasonCode);
        Assert.False(actor!.CanWriteRegulatory);
        // Same membership, client-declared role label (TestHandler role=admin) is not an authority input.
        Assert.Equal(ExactAccessReasons.ActionNotEnabled, ExactActionAccess.Evaluate(actor,
            new("regulatory.source.approve", ExactActionCatalog.PurposeRegisterDraft, tenant, null)).ReasonCode);
    }
}
