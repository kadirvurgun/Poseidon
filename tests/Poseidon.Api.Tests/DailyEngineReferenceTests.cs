using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-099 / T-OPS2-003 (18.01 Backlog row 100) engine log WO/defect links and
// DEV-080 / T-TUG-001 (row 81) tug role lens. Real API host + PostgreSQL; execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    private string EngineRefPath(Guid id, Guid? vessel = null) => $"/api/v1/engine-daily-logs/{id}/references?vesselId={vessel ?? SeedVessel.Id}";
    private async Task<HttpResponseMessage> EngineRefSend(HttpClient client, Guid id, DailyEngineReferenceCommand command, string? key = null, Guid? vessel = null) {
        await Token(client); using var request = new HttpRequestMessage(HttpMethod.Post, EngineRefPath(id, vessel)) { Content = JsonContent.Create(command) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("D")); return await client.SendAsync(request, Ct);
    }
    private async Task<DailyRecordDto> EngineDraft(HttpClient client) {
        var created = await DailySend(client, "engine-daily-logs", new(Guid.NewGuid(), 0, new(), "INITIAL_AUTHORING"), Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); return (await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
    }
    private static DailyEngineReferencePin WoPin(WorkOrderRecordDto w) => new("WorkOrder", w.Id, w.Revision, w.Version, w.Hash, w.SourceVersion);
    private static DailyEngineReferencePin DefectPin(DefectRecordDto d) => new("Defect", d.Id, d.Revision, d.Version, d.Hash, d.SourceVersion);

    [Fact]
    public async Task Engine_log_links_exact_readable_work_order_and_defect_revisions_with_receipt_replay_and_audit() {
        await using var host = Host(); using var client = Client(host);
        DailyPermissions(host.Services); WorkOrderPermissions(host.Services); DefectPermissions(host.Services);
        var log = await EngineDraft(client);
        using var wo = await WorkOrderSend(client, WorkOrderDraft() with { Fields = new() { ["work_order_no"] = new("Known", "FICTIONAL-WO-1") } });
        Assert.Equal(HttpStatusCode.OK, wo.StatusCode); var workOrder = (await wo.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        using var df = await DefectSend(client, DefectDraft() with { Fields = new() { ["defect_no"] = new("Known", "FICTIONAL-DEF-1"), ["criticality"] = new("Known", "High") } });
        Assert.Equal(HttpStatusCode.OK, df.StatusCode); var defect = (await df.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;

        var before = (await client.GetFromJsonAsync<DailyEngineReferencesDto>(EngineRefPath(log.Id), Ct))!;
        Assert.True(before.CanLink); Assert.Empty(before.Items); Assert.Equal(0u, before.ReferenceVersion); Assert.Equal("ONLINE_ONLY", before.Offline);
        Assert.All(before.Sources, s => Assert.Equal("AVAILABLE", s.State));
        var candidates = (await client.GetFromJsonAsync<DailyEngineReferenceCandidatePageDto>($"/api/v1/engine-daily-logs/reference-candidates?vesselId={SeedVessel.Id}&kind=WorkOrder", Ct))!;
        var candidate = Assert.Single(candidates.Items, c => c.Pin.Id == workOrder.Id);
        Assert.Equal("FICTIONAL-WO-1", candidate.Label); Assert.Equal("REVIEW_REQUIRED", candidate.Qualification);

        var link = new DailyEngineReferenceCommand(log.Version, log.Revision, 0, "LINK", WoPin(workOrder)); var key = Guid.NewGuid().ToString("D");
        using var first = await EngineRefSend(client, log.Id, link, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var bytes = await first.Content.ReadAsStringAsync(Ct); var saved = JsonSerializer.Deserialize<DailyEngineReferenceEventDto>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("REVIEW_REQUIRED", saved.Qualification); Assert.Equal(1, saved.Sequence); Assert.Equal(actorId, saved.ActorId);
        Assert.Equal(bytes, await (await EngineRefSend(client, log.Id, link, key)).Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await EngineRefSend(client, log.Id, link with { Operation = "UNLINK" }, key)).StatusCode);
        // Stale reference version and duplicate links are refused without a second event.
        Assert.Equal(HttpStatusCode.Conflict, (await EngineRefSend(client, log.Id, link)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await EngineRefSend(client, log.Id, link with { ExpectedReferenceVersion = saved.ReferenceVersion })).StatusCode);
        using var second = await EngineRefSend(client, log.Id, new(log.Version, log.Revision, saved.ReferenceVersion, "LINK", DefectPin(defect)));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var after = (await client.GetFromJsonAsync<DailyEngineReferencesDto>(EngineRefPath(log.Id), Ct))!;
        Assert.Equal(2, after.Items.Length); Assert.All(after.Items, i => { Assert.Equal("LINKED_CURRENT", i.State); Assert.Equal("REVIEW_REQUIRED", i.Qualification); });
        Assert.Equal("FICTIONAL-DEF-1", after.Items.Single(i => i.Kind == "Defect").Label);

        // A later owning revision makes the pinned link superseded, never silently re-pinned.
        using var edited = await WorkOrderSend(client, WorkOrderDraft(workOrder.Id) with { ExpectedVersion = workOrder.Version, Fields = new() { ["work_order_no"] = new("Known", "FICTIONAL-WO-1b") } }, update: true);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var superseded = (await client.GetFromJsonAsync<DailyEngineReferencesDto>(EngineRefPath(log.Id), Ct))!;
        var wi = superseded.Items.Single(i => i.Kind == "WorkOrder");
        Assert.Equal("LINKED_SUPERSEDED", wi.State); Assert.Equal("REFERENCE_CHANGED_SINCE_LINK", wi.Reason); Assert.Equal("FICTIONAL-WO-1", wi.Label);

        await using var db = Db();
        Assert.Equal(2, await db.DailyEngineReferenceEvents.CountAsync(e => e.RecordId == log.Id, Ct));
        Assert.Contains(await db.AuditEvents.Where(e => e.ResourceId == log.Id).ToArrayAsync(Ct), e => e.ResourceKind == "DailyEngineReferenceEvent");
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(e => e.Action == "DailyEngineReferenceCommand" && e.Result == "Success", Ct));
        var immutable = await db.DailyEngineReferenceEvents.FirstAsync(e => e.RecordId == log.Id, Ct); immutable.TargetHash = new string('0', 64);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Stale_or_unreadable_targets_are_refused_and_existing_links_resolve_to_unknown_without_identifiers() {
        await using var host = Host(); using var client = Client(host);
        DailyPermissions(host.Services); WorkOrderPermissions(host.Services);
        var log = await EngineDraft(client);
        using var wo = await WorkOrderSend(client, WorkOrderDraft()); var workOrder = (await wo.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        // Non-current pinned version -> 409; unknown id -> unavailable; never a free-text fallback.
        Assert.Equal(HttpStatusCode.Conflict, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "LINK", WoPin(workOrder) with { Version = workOrder.Version + 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "LINK", WoPin(workOrder) with { Id = Guid.NewGuid() }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await EngineRefSend(client, log.Id, new(log.Version + 1, log.Revision, 0, "LINK", WoPin(workOrder)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "LINK", WoPin(workOrder)))).StatusCode);
        // Defect lookup without defect.read: candidates denied, link denied.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/engine-daily-logs/reference-candidates?vesselId={SeedVessel.Id}&kind=Defect", Ct)).StatusCode);
        // Withdraw work-order read: the existing link is disclosed only as UNKNOWN.
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "workorder.read").ToArray() };
        using var read = await client.GetAsync(EngineRefPath(log.Id), Ct); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var body = await read.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain(workOrder.Id.ToString(), body); Assert.DoesNotContain(workOrder.Hash, body);
        var item = Assert.Single((await read.Content.ReadFromJsonAsync<DailyEngineReferencesDto>(Ct))!.Items);
        Assert.Equal(("UNKNOWN", "REVIEW_REQUIRED", "REFERENCE_NOT_READABLE"), (item.State, item.Qualification, item.Reason)); Assert.Null(item.Pin);
        Assert.Equal(HttpStatusCode.Forbidden, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "UNLINK", WoPin(workOrder)))).StatusCode);
    }

    [Fact]
    public async Task Engine_reference_scope_denies_foreign_vessel_read_only_actor_and_missing_csrf_without_events() {
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId && v.Kind == VesselKind.Ship);
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services); WorkOrderPermissions(host.Services);
        var log = await EngineDraft(client); using var wo = await WorkOrderSend(client, WorkOrderDraft()); var workOrder = (await wo.Content.ReadFromJsonAsync<WorkOrderRecordDto>(Ct))!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(EngineRefPath(log.Id, foreign.Id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "LINK", WoPin(workOrder)), vessel: foreign.Id)).StatusCode);
        using (var noCsrf = new HttpRequestMessage(HttpMethod.Post, EngineRefPath(log.Id)) { Content = JsonContent.Create(new DailyEngineReferenceCommand(log.Version, log.Revision, 0, "LINK", WoPin(workOrder))) }) {
            noCsrf.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D")); using var plain = Client(host);
            Assert.Equal(HttpStatusCode.Forbidden, (await plain.SendAsync(noCsrf, Ct)).StatusCode);
        }
        using var raw = new HttpRequestMessage(HttpMethod.Post, EngineRefPath(log.Id)) { Content = new StringContent(
            $"{{\"expectedRecordVersion\":{log.Version},\"recordRevision\":{log.Revision},\"expectedReferenceVersion\":0,\"operation\":\"LINK\",\"target\":{{}},\"note\":\"free text\"}}", System.Text.Encoding.UTF8, "application/json") };
        await Token(client); raw.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(raw, Ct)).StatusCode);
        DailyPermissions(host.Services, write: false);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "daily.engine.write").ToArray() };
        Assert.Equal(HttpStatusCode.Forbidden, (await EngineRefSend(client, log.Id, new(log.Version, log.Revision, 0, "LINK", WoPin(workOrder)))).StatusCode);
        await using var db = Db(); Assert.Empty(await db.DailyEngineReferenceEvents.Where(e => e.RecordId == log.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Engine_chief_engineer_review_requires_exact_engine_appointment_not_master_or_write_access() {
        await using var host = Host(); using var client = Client(host); DailyPermissions(host.Services);
        var log = await EngineDraft(client);
        var wrong = DailyAppointment(host.Services, log.Id, "Engine", "Master");
        var caps = (await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath("engine-daily-logs", log.Id, suffix: "/review-capabilities"), Ct))!;
        Assert.False(caps.CanRecord); Assert.Empty(caps.Authorities);
        var response = await ReviewSend(client, "engine-daily-logs", log.Id, new(log.Version, 0, log.Revision, wrong.Id, "REVIEWED", "REVIEW_RECORDED", "FICTIONAL"), Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = Db(); Assert.Empty(await db.DailyReviewEvents.Where(r => r.RecordId == log.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Synthetic_engine_reference_fixture_is_idempotent_and_preserves_operator_links() {
        await using (var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, new("system:engine-reference-fixture", "System", "engine-reference-fixture"), administrativeImport: true)) {
            for (var i = 0; i < 2; i++) {
                await using var tx = await db.Database.BeginTransactionAsync(Ct);
                await DailyOperationsFixtures.AddMissingAsync(db, [SeedVessel], Ct); await WorkOrderFixtures.AddMissingAsync(db, [SeedVessel], Ct);
                await DailyEngineReferenceFixtures.AddMissingAsync(db, [SeedVessel], Ct); await tx.CommitAsync(Ct);
            }
        }
        await using var check = Db();
        var row = await check.DailyEngineReferenceEvents.SingleAsync(e => e.Id == DailyEngineReferenceFixtures.EventId(SeedVessel.Id), Ct);
        Assert.Equal(("LINK", "WorkOrder", WorkOrderFixtures.Id(SeedVessel.Id)), (row.Operation, row.TargetKind, row.WorkOrderId!.Value));
        Assert.Empty(await check.DailyEngineReferenceReceipts.ToArrayAsync(Ct));
        Assert.Empty(await check.DailyReviewEvents.Where(r => r.RecordId == row.RecordId).ToArrayAsync(Ct));
    }

    // DEV-080: tug profile root carries the server role lens; Ship Mode screens only with mode.cross.
    [Fact]
    public async Task Tug_context_shows_server_role_lens_and_hides_ship_mode_screens_without_cross_mode() {
        foreach (var cross in new[] { false, true }) {
            using var host = Host(tugMode: true, crossMode: cross, tugRead: true); using var c = Client(host); DailyPermissions(host.Services);
            using var response = await c.GetAsync($"/api/v1/tug-command?vessel={Tug.Id}", Ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)); var root = json.RootElement;
            Assert.Equal(cross, root.GetProperty("crossMode").GetBoolean());
            var destinations = root.GetProperty("shipModeDestinations").EnumerateArray().Select(x => x.GetProperty("key").GetString()).ToArray();
            if (cross) Assert.Equal(new[] { "deck-daily-log", "engine-daily-log", "noon-report" }, destinations.Order(StringComparer.Ordinal).ToArray());
            else Assert.Empty(destinations);
            var lens = root.GetProperty("roleLens");
            var roleState = lens.GetProperty("roleState").GetString();
            Assert.Contains(roleState, new[] { "Configured", "Missing", "Unavailable" });
            if (roleState != "Configured") Assert.Empty(lens.GetProperty("roles").EnumerateArray());
            // Ship Mode screens never appear as Tug-mode destinations on the Tug vessel without cross-mode.
            using var menu = await c.GetAsync($"/api/v1/workspace/lenses/{Tug.Id}", Ct);
            if (!cross && menu.StatusCode == HttpStatusCode.OK)
                Assert.DoesNotContain("engine-daily-log", await menu.Content.ReadAsStringAsync(Ct));
        }
    }
}
