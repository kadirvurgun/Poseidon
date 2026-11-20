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

// S8 WP-C: DEV-055 (18.01 row 56, T-S8-004), DEV-103 (row 104, T-PMS2-001), DEV-056 (row 57, T-S8-005).
// Real host, antiforgery and owned PostgreSQL; synthetic memberships and FICTIONAL content only.
// AUTHORED; execution DEFERRED to CI (no Docker in this package).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] TechnicalRecordPermissions = ["equipment.read", "equipment.write", "defect.read", "defect.write", "defect.verify", "defect.effectiveness",
        "defect.close", "service.report.read", "service.report.write", "service.report.verify", "maintenance.read", "workorder.read", "file.read"];
    private void TechActor(IServiceProvider services, Guid actor, params string[] permissions) => TechActorOfKind(services, actor, "Human", permissions);
    private void TechActorOfKind(IServiceProvider services, Guid actor, string kind, params string[] permissions)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = kind, Permissions = [.. m.Permissions.Where(p => !TechnicalRecordPermissions.Contains(p)), .. permissions] };
    }
    private string Lifecycle(Guid defect, string suffix = "", Guid? vessel = null) => $"defects/{defect}/lifecycle{suffix}?vesselId={vessel ?? SeedVessel.Id}";
    private async Task<T> TechOk<T>(HttpResponseMessage response)
    {
        using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<T>(Ct))!; }
    }
    private async Task<DefectRecordDto> CriticalDefect(HttpClient c)
    {
        var command = DefectDraft() with { Fields = new() { ["criticality"] = new("Known", "Critical"), ["notes"] = new("Known", "FICTIONAL main engine seal leak") } };
        using var created = await DefectSend(c, command);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!;
    }

    [Fact]
    [Trait("Requirement", "DEV-055")]
    public async Task Critical_defect_lifecycle_requires_safeguard_independent_verification_effectiveness_and_human_closure()
    {
        await using var host = Host(); using var c = Client(host);
        var engineer = Guid.NewGuid(); var verifier = Guid.NewGuid(); var chief = Guid.NewGuid();
        TechActor(host.Services, engineer, TechnicalRecordPermissions); await Token(c);
        var defect = await CriticalDefect(c);
        await Token(c);

        var key = Guid.NewGuid().ToString();
        var started = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, DefectRevision: defect.Revision), key));
        Assert.Equal(("DETECT", 1L, true), (started.Stage, started.Revision, started.Critical));
        Assert.Contains(started.Alerts, a => a.Code == "CRITICAL_DEFECT_WITHOUT_ACTIVE_SAFEGUARD" && a.Blocking);
        Assert.Equal(started.Revision, (await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, DefectRevision: defect.Revision), key))).Revision);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, DefectRevision: defect.Revision)), HttpStatusCode.Conflict, "defect_lifecycle_exists");

        var assessed = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/assess"), new DefectLifecycleCommand(1, "Critical", Text: "FICTIONAL loss of propulsion risk")));
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/actions"), new DefectLifecycleCommand(assessed.Revision, Text: "FICTIONAL repair before safeguard")),
            HttpStatusCode.UnprocessableEntity, "defect_safeguard_required");
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/assess"), new DefectLifecycleCommand(1, "Critical", Text: "FICTIONAL stale revision")), HttpStatusCode.Conflict, "version_conflict");
        var guarded = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/safeguards"), new DefectLifecycleCommand(assessed.Revision, MeasureKind: "RESTRICTION",
            Text: "FICTIONAL reduced speed", Temporary: true, ExpiresAt: DateTimeOffset.UtcNow.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ssZ"))));
        Assert.Equal("SAFEGUARD", guarded.Stage); Assert.Equal("ACTIVE", Assert.Single(guarded.Measures).State);
        await AssertCode(await Send(c, Lifecycle(defect.Id, $"/safeguards/{guarded.Measures[0].Id}/lift"), new DefectLifecycleCommand(guarded.Revision, Text: "FICTIONAL lifting the only safeguard")),
            HttpStatusCode.UnprocessableEntity, "defect_safeguard_required");
        var second = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/safeguards"), new DefectLifecycleCommand(guarded.Revision, MeasureKind: "SAFEGUARD",
            Text: "FICTIONAL standby pump in service", Temporary: false)));
        var lifted = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, $"/safeguards/{guarded.Measures[0].Id}/lift"),
            new DefectLifecycleCommand(second.Revision, Text: "FICTIONAL restriction replaced by standby")));
        Assert.Equal("LIFTED", lifted.Measures.Single(m => m.Id == guarded.Measures[0].Id).State);
        var acted = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/actions"), new DefectLifecycleCommand(lifted.Revision, Text: "FICTIONAL replaced seal")));
        Assert.Equal([engineer], acted.ActionOwners); Assert.False(acted.Can.Verify);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/verify"), new DefectLifecycleCommand(acted.Revision, Text: "FICTIONAL self verification", Outcome: "VERIFIED")),
            HttpStatusCode.Forbidden, "separation_of_duties");

        // B1: verification is a human record; an AI / unspecified principal with the permission is denied and not offered it.
        foreach (var kind in new[] { "AI", "Unspecified" })
        {
            TechActorOfKind(host.Services, Guid.NewGuid(), kind, "equipment.read", "defect.read", "defect.verify"); await Token(c);
            Assert.False((await c.GetFromJsonAsync<DefectLifecycleDto>("/api/v1/" + Lifecycle(defect.Id), Ct))!.Can.Verify);
            await AssertCode(await Send(c, Lifecycle(defect.Id, "/verify"), new DefectLifecycleCommand(acted.Revision, Text: "FICTIONAL machine verification", Outcome: "VERIFIED")),
                HttpStatusCode.Forbidden, "human_actor_required");
        }
        TechActor(host.Services, verifier, "equipment.read", "defect.read", "defect.verify", "defect.effectiveness"); await Token(c);
        var verified = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/verify"), new DefectLifecycleCommand(acted.Revision, Text: "FICTIONAL pressure test held", Outcome: "VERIFIED")));
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/close"), new DefectLifecycleCommand(verified.Revision, Text: "FICTIONAL closure attempt")), HttpStatusCode.Forbidden, "permission_denied");
        TechActorOfKind(host.Services, Guid.NewGuid(), "AI", "equipment.read", "defect.read", "defect.effectiveness"); await Token(c);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/effectiveness"), new DefectLifecycleCommand(verified.Revision, Text: "FICTIONAL machine effectiveness", Outcome: "EFFECTIVE")),
            HttpStatusCode.Forbidden, "human_actor_required");
        TechActor(host.Services, verifier, "equipment.read", "defect.read", "defect.verify", "defect.effectiveness"); await Token(c);
        var effective = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/effectiveness"), new DefectLifecycleCommand(verified.Revision, Text: "FICTIONAL no recurrence", Outcome: "EFFECTIVE")));
        Assert.Equal("EFFECTIVENESS", effective.Stage); Assert.Empty(effective.CloseBlockers);

        TechActorOfKind(host.Services, Guid.NewGuid(), "Unspecified", "equipment.read", "defect.read", "defect.close"); await Token(c);
        Assert.False((await c.GetFromJsonAsync<DefectLifecycleDto>("/api/v1/" + Lifecycle(defect.Id), Ct))!.Can.Close);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/close"), new DefectLifecycleCommand(effective.Revision, Text: "FICTIONAL machine closure")),
            HttpStatusCode.Forbidden, "human_actor_required");
        TechActor(host.Services, chief, "equipment.read", "defect.read", "defect.close"); await Token(c);
        var closed = await TechOk<DefectLifecycleDto>(await Send(c, Lifecycle(defect.Id, "/close"), new DefectLifecycleCommand(effective.Revision, Text: "FICTIONAL returned to service")));
        Assert.Equal(("CLOSED", chief), (closed.Stage, closed.ClosedBy!.Value));
        Assert.Equal(["START", "ASSESS", "SAFEGUARD", "SAFEGUARD", "LIFT", "ACTION", "VERIFY", "EFFECTIVENESS", "CLOSE"], closed.Records.Select(r => r.Kind));

        await using var db = Db();
        var actor = new TrustedActor(chief, SeedVessel.TenantId, ["directory.read", "equipment.read", "defect.read"], true, false, []);
        var check = Assert.Single(await TugReadinessAssessor.Defects(db, actor, SeedVessel.Id, Ct), x => x.SourceId == defect.Id);
        Assert.Equal((TugReadinessEngine.Pass, "DefectClosed"), (check.Result, check.Reason));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == defect.Id && e.EntityType != nameof(Defect) && e.EntityType != nameof(DefectRevision) && e.EntityType != nameof(DefectReceipt)).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(DefectLifecycleRecord) && e.ChangeSummary!.Contains("DefectLifecycleClose") && e.ActorId == chief.ToString() && e.VesselId == SeedVessel.Id);
        Assert.Contains(audit, e => e.EntityType == nameof(DefectLifecycleRecord) && e.ChangeSummary!.Contains("DefectLifecycleVerify") && e.ActorId == verifier.ToString());
        Assert.Equal(9, await db.TechnicalRecordReceipts.CountAsync(r => r.ResourceId == defect.Id, Ct));
        var record = await db.DefectLifecycleRecords.FirstAsync(r => r.DefectId == defect.Id && r.Kind == "VERIFY", Ct);
        record.Outcome = "NOT_VERIFIED"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-055")]
    public async Task Defect_lifecycle_denies_foreign_vessel_missing_permission_and_unclosed_shape()
    {
        await using var host = Host(); using var c = Client(host);
        TechActor(host.Services, Guid.NewGuid(), TechnicalRecordPermissions); await Token(c);
        var defect = await CriticalDefect(c); await Token(c);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/start", Guid.NewGuid()), new DefectLifecycleCommand(0, DefectRevision: defect.Revision)), HttpStatusCode.NotFound, "resource_not_found");
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, DefectRevision: defect.Revision + 1)), HttpStatusCode.Conflict, "defect_declaration_changed");
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, "Critical", DefectRevision: defect.Revision)), HttpStatusCode.BadRequest, "invalid_request");
        TechActor(host.Services, Guid.NewGuid(), "equipment.read", "defect.read"); await Token(c);
        await AssertCode(await Send(c, Lifecycle(defect.Id, "/start"), new DefectLifecycleCommand(0, DefectRevision: defect.Revision)), HttpStatusCode.Forbidden, "permission_denied");
        var read = (await c.GetFromJsonAsync<DefectLifecycleDto>("/api/v1/" + Lifecycle(defect.Id), Ct))!;
        Assert.False(read.Started); Assert.Contains("LIFECYCLE_NOT_STARTED", read.CloseBlockers); Assert.False(read.Can.Start);
        await using var db = Db();
        Assert.False(await db.DefectLifecycles.AnyAsync(l => l.DefectId == defect.Id, Ct));
    }

    private static ServiceReportContent FictionalService() => new("FICTIONAL Marine Services", "FICTIONAL Engineer", "2026-11-01", "FICTIONAL overhaul scope",
        "FICTIONAL overhaul performed", [new("FICTIONAL seal kit", "PN-1", "2", "pcs")], [new("Vibration", "2.1", "mm/s", "WITHIN_LIMITS")],
        "FICTIONAL wear found", "FICTIONAL recommendation", null);

    [Fact]
    [Trait("Requirement", "DEV-103")]
    public async Task Service_report_links_equipment_and_defect_and_is_verified_by_a_non_author_with_immutable_history()
    {
        await using var host = Host(); using var c = Client(host);
        var author = Guid.NewGuid(); var verifier = Guid.NewGuid();
        TechActor(host.Services, author, TechnicalRecordPermissions); await Token(c);
        var equipment = NewEquipment(); using (var created = await Send(c, EquipmentUrl, equipment)) Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var defect = await CriticalDefect(c); await Token(c);
        var path = $"service-reports?vesselId={SeedVessel.Id}";
        var id = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        var draftContent = FictionalService() with { EngineerName = null };
        var create = new ServiceReportCreateCommand(id, "FICTIONAL-SR-" + Guid.NewGuid().ToString("N")[..6], draftContent, new(null, null, [defect.Id], []));
        var draft = await TechOk<ServiceReportDto>(await Send(c, path, create, key));
        Assert.Equal(("DRAFT", 1L), (draft.State, draft.Revision)); Assert.Contains("engineer_name", draft.Missing); Assert.Contains("equipment_id", draft.Missing);
        Assert.Equal("CURRENT", Assert.Single(draft.Defects).Currency);
        Assert.Equal(draft.Id, (await TechOk<ServiceReportDto>(await Send(c, path, create, key))).Id);
        await AssertCode(await Send(c, path, create with { ReportNo = "OTHER" }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        var failure = await Send(c, $"service-reports/{id}/submit?vesselId={SeedVessel.Id}", new ServiceReportSubmitCommand(draft.Version, draft.Hash));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failure.StatusCode);
        Assert.Contains("engineer_name", (await failure.Content.ReadFromJsonAsync<TechnicalRecordFailure>(Ct))!.Missing);

        var updated = await TechOk<ServiceReportDto>(await Send(c, $"service-reports/{id}?vesselId={SeedVessel.Id}",
            new ServiceReportUpdateCommand(draft.Version, FictionalService(), new(equipment.Id, null, [defect.Id], [])), update: true));
        Assert.Empty(updated.Missing); Assert.Equal("Equipment", updated.Equipment!.Kind);
        var submitted = await TechOk<ServiceReportDto>(await Send(c, $"service-reports/{id}/submit?vesselId={SeedVessel.Id}", new ServiceReportSubmitCommand(updated.Version, updated.Hash)));
        Assert.Equal("PENDING_VERIFICATION", submitted.VerificationState); Assert.False(submitted.CanVerify);
        await AssertCode(await Send(c, $"service-reports/{id}/verify?vesselId={SeedVessel.Id}", new ServiceReportVerifyCommand(submitted.Version, submitted.Hash, "VERIFY", "FICTIONAL self verification")),
            HttpStatusCode.Forbidden, "separation_of_duties");

        foreach (var kind in new[] { "AI", "Unspecified" })
        {
            TechActorOfKind(host.Services, Guid.NewGuid(), kind, "equipment.read", "service.report.read", "service.report.verify"); await Token(c);
            Assert.False((await c.GetFromJsonAsync<ServiceReportDto>($"/api/v1/service-reports/{id}?vesselId={SeedVessel.Id}", Ct))!.CanVerify);
            await AssertCode(await Send(c, $"service-reports/{id}/verify?vesselId={SeedVessel.Id}", new ServiceReportVerifyCommand(submitted.Version, submitted.Hash, "VERIFY", "FICTIONAL machine verification")),
                HttpStatusCode.Forbidden, "human_actor_required");
        }
        TechActor(host.Services, verifier, "equipment.read", "service.report.read", "service.report.verify"); await Token(c);
        var verified = await TechOk<ServiceReportDto>(await Send(c, $"service-reports/{id}/verify?vesselId={SeedVessel.Id}",
            new ServiceReportVerifyCommand(submitted.Version, submitted.Hash, "VERIFY", "FICTIONAL checked against service sheet")));
        Assert.Equal(("VERIFIED", verified.Revision), (verified.State, verified.VerifiedRevision!.Value));
        Assert.Equal(verifier, Assert.Single(verified.Verifications).ActorId);

        TechActor(host.Services, author, TechnicalRecordPermissions); await Token(c);
        await AssertCode(await Send(c, $"service-reports/{id}?vesselId={SeedVessel.Id}", new ServiceReportUpdateCommand(verified.Version, FictionalService(), new(equipment.Id, null, [], [])), update: true),
            HttpStatusCode.Conflict, "invalid_state");
        var amended = await TechOk<ServiceReportDto>(await Send(c, $"service-reports/{id}/amend?vesselId={SeedVessel.Id}", new ServiceReportAmendCommand(verified.Version, "FICTIONAL corrected part number")));
        Assert.Equal(("DRAFT", "EARLIER_REVISION_VERIFIED"), (amended.State, amended.VerificationState));
        var snapshot = (await c.GetFromJsonAsync<ServiceReportRevisionDto>($"/api/v1/service-reports/{id}/revisions/{verified.Revision}?vesselId={SeedVessel.Id}", Ct))!;
        Assert.Equal(("VERIFIED", verified.Hash), (snapshot.State, snapshot.Hash));
        var listed = (await c.GetFromJsonAsync<ServiceReportPageDto>($"/api/v1/service-reports?vesselId={SeedVessel.Id}&assetId={equipment.Id}", Ct))!;
        Assert.Contains(listed.Items, i => i.Id == id);

        await using var db = Db();
        Assert.Equal(1, await db.ServiceReportVerifications.CountAsync(v => v.ReportId == id, Ct));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(ServiceReportVerification) && e.ChangeSummary!.Contains("ServiceReportVerify") && e.ActorId == verifier.ToString());
        var history = await db.ServiceReportRevisions.FirstAsync(r => r.ReportId == id && r.State == "VERIFIED", Ct);
        history.ContentJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-056")]
    public async Task Pms_command_center_lists_exceptions_keeps_unknown_sources_unknown_and_withholds_forbidden_sections()
    {
        await using var host = Host(); using var c = Client(host);
        TechActor(host.Services, Guid.NewGuid(), TechnicalRecordPermissions); await Token(c);
        var defect = await CriticalDefect(c);
        var center = (await c.GetFromJsonAsync<PmsCommandCenterDto>($"/api/v1/pms/command-center?vesselId={SeedVessel.Id}", Ct))!;
        Assert.Equal(PmsCommandCenterPolicy.SectionKeys, center.Sections.Select(s => s.Key));
        Assert.Equal("BLOCKED", center.ReadinessState);
        var alerts = center.Sections.Single(s => s.Key == "condition_alerts");
        var item = Assert.Single(alerts.Items, i => i.SourceId == defect.Id);
        Assert.Contains(item.Why, w => w.Code == "DEFECT_LIFECYCLE_NOT_STARTED");
        // S8 integration: open_work_orders reads the real DEV-054 execution source (work order read granted). The per-test
        // database has no released work order, so it is exactly QUIET with count 0 (never UNKNOWN). critical_spare_gaps stays
        // ACCESS_REQUIRED without spare.read and never zero/quiet.
        var workOrders = center.Sections.Single(s => s.Key == "open_work_orders");
        Assert.Equal((PmsCommandCenterPolicy.Quiet, (int?)0, 0), (workOrders.State, workOrders.Count, workOrders.QuietCount));
        var spares = center.Sections.Single(s => s.Key == "critical_spare_gaps");
        Assert.Equal((PmsCommandCenterPolicy.AccessRequired, (int?)null, "SPARE_ACCESS_REQUIRED"), (spares.State, spares.Count, spares.Reason));
        TechActor(host.Services, Guid.NewGuid(), [.. TechnicalRecordPermissions, "spare.read"]); await Token(c);
        var withSpares = (await c.GetFromJsonAsync<PmsCommandCenterDto>($"/api/v1/pms/command-center?vesselId={SeedVessel.Id}", Ct))!;
        var spareSection = withSpares.Sections.Single(s => s.Key == "critical_spare_gaps");
        // No critical spare is in the default seed (CriticalSpareFixtures is opt-in): exactly QUIET, count 0.
        Assert.Equal((PmsCommandCenterPolicy.Quiet, (int?)0, 0), (spareSection.State, spareSection.Count, spareSection.QuietCount));

        // End-to-end WP-B adapter: a released work order without a configured evidence policy is an open REVIEW exception.
        var (order, _) = await ReleasedWorkOrder(c, host.Services, Guid.NewGuid(), null);
        TechActor(host.Services, Guid.NewGuid(), TechnicalRecordPermissions); await Token(c);
        var released = (await c.GetFromJsonAsync<PmsCommandCenterDto>($"/api/v1/pms/command-center?vesselId={SeedVessel.Id}", Ct))!;
        var open = released.Sections.Single(s => s.Key == "open_work_orders");
        Assert.Equal((PmsCommandCenterPolicy.Exceptions, (int?)1), (open.State, open.Count));
        var woItem = Assert.Single(open.Items);
        Assert.Equal(("WorkOrder", order.Id, "REVIEW"), (woItem.Kind, woItem.SourceId, woItem.Severity));
        Assert.Contains(woItem.Why, w => w.Code == "WORK_ORDER_OPEN" && w.Detail == "RELEASED");
        Assert.Contains(woItem.Why, w => w.Code == "REQUIRED_EVIDENCE_UNCONFIGURED");
        TechActor(host.Services, Guid.NewGuid(), "equipment.read"); await Token(c);
        var limited = (await c.GetFromJsonAsync<PmsCommandCenterDto>($"/api/v1/pms/command-center?vesselId={SeedVessel.Id}", Ct))!;
        Assert.Equal(PmsCommandCenterPolicy.AccessRequired, limited.Sections.Single(s => s.Key == "condition_alerts").State);
        Assert.Equal(PmsCommandCenterPolicy.AccessRequired, limited.Sections.Single(s => s.Key == "open_work_orders").State);
        Assert.Equal("REVIEW_REQUIRED", limited.ReadinessState);
        Assert.Empty(limited.Sections.SelectMany(s => s.Items));
        using var forbidden = await c.GetAsync($"/api/v1/pms/command-center?vesselId={Guid.NewGuid()}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        TechActor(host.Services, Guid.NewGuid(), "defect.read"); await Token(c);
        using var denied = await c.GetAsync($"/api/v1/pms/command-center?vesselId={SeedVessel.Id}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "DEV-103")]
    public async Task Technical_record_fixture_is_idempotent_fictional_and_seeds_no_verification_lifecycle_or_receipt()
    {
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            await TechnicalRecordFixtures.AddMissingAsync(db, seed.Vessels, Ct); await TechnicalRecordFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        Assert.Equal(seed.Vessels.Length, await db.ServiceReports.CountAsync(Ct));
        Assert.All(await db.ServiceReports.ToArrayAsync(Ct), r => Assert.Equal(("DRAFT", "SyntheticUnverified", (long?)null), (r.State, r.Provenance, r.VerifiedRevision)));
        Assert.Empty(await db.ServiceReportVerifications.ToArrayAsync(Ct)); Assert.Empty(await db.DefectLifecycles.ToArrayAsync(Ct));
        Assert.Empty(await db.DefectLifecycleRecords.ToArrayAsync(Ct)); Assert.Empty(await db.TechnicalRecordReceipts.ToArrayAsync(Ct));
        // DB CHECK refuses a synthetic verified report even through the administrative context.
        await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            var row = await db.ServiceReports.FirstAsync(Ct); row.Revision++; row.State = "VERIFIED"; row.VerifiedRevision = row.Revision;
            await db.SaveChangesAsync(Ct);
        });
    }
}
