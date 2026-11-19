using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-035 / T-S4-003 (18.01 Backlog row 36): material conflict center index. Pure projection/query
// rules run without PostgreSQL; the host/PG cases below are Integration (DirectoryWorkspaceTests trait).
public sealed class SyncConflictCenterProjectionTests
{
    private static QueryCollection Q(params (string Key, string[] Values)[] items) =>
        new(items.ToDictionary(i => i.Key, i => new StringValues(i.Values)));
    private static readonly Guid Vessel = Guid.Parse("7c1e3b0a-1111-4f00-8000-000000000035");

    [Fact]
    public void Query_is_closed_canonical_and_bounded()
    {
        Assert.True(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")])), out var v, out var o));
        Assert.Equal(Vessel, v); Assert.Equal(0, o);
        Assert.True(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")]), ("offset", ["25"])), out _, out o)); Assert.Equal(25, o);
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Guid.Empty.ToString("D")])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("N")])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D"), Vessel.ToString("D")])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")]), ("actorId", [Guid.NewGuid().ToString()])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")]), ("offset", ["-25"])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")]), ("offset", ["7"])), out _, out _));
        Assert.False(SyncConflictCenterEndpoints.TryQuery(Q(("vesselId", [Vessel.ToString("D")]), ("offset", ["1025"])), out _, out _));
    }

    [Fact]
    public void Projection_lists_open_first_never_marks_a_conflict_resolved_by_another_conflict_and_pages_deterministically()
    {
        var t0 = new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.Zero); var enrollment = Guid.NewGuid();
        var resolved = new SyncConflictRow("crewAssignment", Guid.NewGuid(), null, enrollment, Guid.NewGuid(), 1, 2, t0, t0.AddMinutes(1));
        var superseded = new SyncConflictRow("tugPlan", Guid.NewGuid(), null, enrollment, Guid.NewGuid(), 3, 4, t0, t0.AddMinutes(2));
        var open = new SyncConflictRow("dailyRecord", Guid.NewGuid(), "Noon", enrollment, Guid.NewGuid(), 5, 6, null, t0);
        var successors = new[] {
            new SyncConflictSuccessor(enrollment, resolved.CommandId, Guid.NewGuid(), "Committed"),
            new SyncConflictSuccessor(enrollment, superseded.CommandId, Guid.NewGuid(), "MaterialConflict"),
            // Same parent id under another enrollment never resolves this one.
            new SyncConflictSuccessor(Guid.NewGuid(), open.CommandId, Guid.NewGuid(), "Committed") };
        var (items, more) = SyncConflictCenterEndpoints.Project([resolved, superseded, open], successors, 0);
        Assert.False(more);
        Assert.Equal([open.CommandId, superseded.CommandId, resolved.CommandId], items.Select(i => i.CommandId));
        Assert.Equal(["Open", "Superseded", "Resolved"], items.Select(i => i.State));
        Assert.Null(items[0].CapturedAt); Assert.Null(items[0].ResolutionCommandId); Assert.Equal("Noon", items[0].ResourceKind);
        Assert.Equal("Committed", items[2].ResolutionResult);

        var many = Enumerable.Range(0, 30).Select(i => new SyncConflictRow("crewAssignment", Guid.NewGuid(), null, enrollment,
            Guid.Parse($"00000000-0000-4000-8000-{i:D12}"), 1, 2, t0, t0)).ToArray();
        var first = SyncConflictCenterEndpoints.Project(many.Reverse(), [], 0); var second = SyncConflictCenterEndpoints.Project(many, [], 25);
        Assert.True(first.HasMore); Assert.Equal(25, first.Items.Length); Assert.False(second.HasMore); Assert.Equal(5, second.Items.Length);
        Assert.Empty(first.Items.Select(i => i.CommandId).Intersect(second.Items.Select(i => i.CommandId)));
    }
}

public sealed partial class DirectoryWorkspaceTests
{
    private string ConflictCenterPath => $"/api/v1/sync/conflicts?vesselId={SeedVessel.Id:D}";

    [Fact]
    public async Task Conflict_center_lists_only_own_scoped_conflicts_without_values_and_tracks_reasoned_resolution()
    {
        await using var host = OfflineHost(); using var c = Client(host); using var anonymous = Client(host, false); await Token(c);
        using var unauthenticated = await anonymous.GetAsync(ConflictCenterPath, Ct); Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        var empty = (await c.GetFromJsonAsync<SyncConflictPageDto>(ConflictCenterPath, Ct))!;
        Assert.Empty(empty.Items); Assert.Contains(empty.Sources, s => s.Source == "crewAssignment" && s.State == "Included");

        var first = await EnrollOffline(c); var other = await EnrollOffline(c);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(c, other, Captured(other, "SYNTHETIC-B"))).StatusCode);
        var local = Captured(first); using var conflicted = await Deliver(c, first, local);
        var conflict = (await conflicted.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!; Assert.Equal("MaterialConflict", conflict.Result);

        using var listed = await c.GetAsync(ConflictCenterPath, Ct); var raw = await listed.Content.ReadAsStringAsync(Ct);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.DoesNotContain("SYNTHETIC-A", raw); Assert.DoesNotContain("SYNTHETIC-B", raw); // values stay behind the protected detail
        var page = (await listed.Content.ReadFromJsonAsync<SyncConflictPageDto>(Ct))!;
        var item = Assert.Single(page.Items);
        Assert.Equal(("crewAssignment", local.CommandId, first.Id, "Open"), (item.Source, item.CommandId, item.EnrollmentId, item.State));
        Assert.Equal(local.BaseVersion, item.BaseVersion); Assert.Equal(conflict.ServerVersion, item.ServerVersionAtConflict);

        // Listing never resolves: the conflict stays open until an explicit reasoned expected-version command.
        Assert.Equal("Open", Assert.Single((await c.GetFromJsonAsync<SyncConflictPageDto>(ConflictCenterPath, Ct))!.Items).State);
        var resolution = Captured(first, null) with { ParentCommandId = local.CommandId, BaseVersion = conflict.ServerVersion,
            ExpectedServerVersion = conflict.ServerVersion, Choice = "keepServer", Reason = "FICTIONAL explicit human reconciliation" };
        Assert.Equal(HttpStatusCode.OK, (await Deliver(c, first, resolution)).StatusCode);
        var after = Assert.Single((await c.GetFromJsonAsync<SyncConflictPageDto>(ConflictCenterPath, Ct))!.Items);
        Assert.Equal(("Resolved", resolution.CommandId, "Committed"), (after.State, after.ResolutionCommandId, after.ResolutionResult));

        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().Where(e => e.Action == "SyncConflictList").ToArrayAsync(Ct), e => e.Result == "Success" && e.ResourceId == SeedVessel.Id);
    }

    [Fact]
    public async Task Conflict_center_rejects_foreign_vessel_and_forged_query_and_never_lists_committed_receipts()
    {
        await using var host = OfflineHost(); using var c = Client(host); await Token(c);
        var e = await EnrollOffline(c); var other = await EnrollOffline(c);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(c, other, Captured(other, "SYNTHETIC-B"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(c, e, Captured(e))).StatusCode);
        using var spoof = await c.GetAsync(ConflictCenterPath + "&actorId=" + Guid.NewGuid(), Ct); Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        using var badOffset = await c.GetAsync(ConflictCenterPath + "&offset=3", Ct); Assert.Equal(HttpStatusCode.BadRequest, badOffset.StatusCode);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        using var hidden = await c.GetAsync($"/api/v1/sync/conflicts?vesselId={foreign.Id:D}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.DoesNotContain("commandId", await hidden.Content.ReadAsStringAsync(Ct));
        // Committed receipts are never listed as conflicts; only the actor's own MaterialConflict row appears.
        var page = (await c.GetFromJsonAsync<SyncConflictPageDto>(ConflictCenterPath, Ct))!;
        var only = Assert.Single(page.Items); Assert.Equal(e.Id, only.EnrollmentId);
        await using var db = Db();
        Assert.Equal(await db.OfflineOutcomes.Where(o => o.Result == "MaterialConflict").Select(o => o.CommandId).SingleAsync(Ct), only.CommandId);
        Assert.Contains(await db.Set<SecurityEvent>().Where(x => x.Action == "SyncConflictList").ToArrayAsync(Ct), x => x.Result == "Invalid");
        Assert.Contains(await db.Set<SecurityEvent>().Where(x => x.Action == "SyncConflictList").ToArrayAsync(Ct), x => x.Result == "NotFound");
    }
}
