using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S8 WP-A: DEV-052 / DEV-053 / DEV-108 / DEV-109 over the actual host and real PostgreSQL.
// AUTHORED; execution in CI (no local Docker). All data FICTIONAL; no approval is seeded.
public sealed partial class DirectoryWorkspaceTests
{
    private string PointsUrl => $"equipment-hierarchy/{SeedVessel.Id}/measurement-points";
    private static readonly Guid PmsOwnerRole = Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535");

    private void PmsActor(IServiceProvider services, Guid actor, params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, Permissions = [.. m.Permissions.Where(p => !p.StartsWith("maintenance.postponement.", StringComparison.Ordinal) &&
            p != "maintenance.measurement.write"), .. extra] };
    }
    private async Task<T> PmsOk<T>(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
        }
    }
    private async Task PmsCode(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
    }
    private async Task<Guid> PmsAsset(HttpClient client, string? criticality)
    {
        var id = Guid.NewGuid();
        using var r = await Send(client, EquipmentUrl, NewEquipment(id, SeedVessel.Id) with { Code = "FICTIONAL-" + id.ToString("N")[..8], Criticality = criticality });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        return id;
    }

    [Fact]
    [Trait("Requirement", "DEV-052")]
    public async Task Equipment_criticality_is_closed_versioned_and_a_cycle_is_reported_explicitly()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        await PmsCode(await Send(client, EquipmentUrl, NewEquipment(parent: SeedVessel.Id) with { Criticality = "Very critical" }), HttpStatusCode.BadRequest, "validation_failed");
        var parent = await PmsAsset(client, "CRITICAL");
        var child = Guid.NewGuid();
        var saved = await PmsOk<TechnicalSaved>(await Send(client, EquipmentUrl, NewEquipment(child, parent) with { Criticality = "STANDARD", Maker = "FICTIONAL maker",
            Model = "FICTIONAL model", SerialNumber = "FICTIONAL-SN-1", Location = "FICTIONAL engine room" }));
        var snapshot = (await client.GetFromJsonAsync<EquipmentSnapshot>("/api/v1/" + EquipmentUrl, Ct))!;
        var parentRow = snapshot.Items.Single(x => x.Id == parent);
        await PmsCode(await Send(client, EquipmentUrl, NewEquipment(parent, child) with { BaseVersion = parentRow.Version, Criticality = "CRITICAL" }), HttpStatusCode.BadRequest, "hierarchy_cycle");
        var edit = NewEquipment(child, parent) with { BaseVersion = saved.Version, Criticality = "IMPORTANT", Maker = "FICTIONAL maker",
            Model = "FICTIONAL model B", SerialNumber = "FICTIONAL-SN-1", Location = "FICTIONAL engine room" };
        Assert.Equal(2, (await PmsOk<TechnicalSaved>(await Send(client, EquipmentUrl, edit))).Revision);
        var history = (await client.GetFromJsonAsync<EquipmentHistoryRow[]>($"/api/v1/{EquipmentUrl}/{child}/history", Ct))!;
        Assert.Equal(new[] { ("IMPORTANT", "FICTIONAL model B"), ("STANDARD", "FICTIONAL model") }, history.Select(h => (h.Asset.Criticality!, h.Asset.Model!)));
        // Another vessel's tree is not reachable from this vessel scope.
        var other = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, EquipmentUrl, NewEquipment(parent: other.Id))).StatusCode);
    }

    [Fact]
    [Trait("Requirement", "DEV-108")]
    public async Task Measurement_points_record_history_propagate_sub_counters_and_drive_a_threshold_plan_only_when_configured()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        var engineer = Guid.NewGuid(); PmsActor(host.Services, engineer); await Token(client);
        var asset = await PmsAsset(client, "STANDARD");
        var parent = new MeasurementPointSaveCommand(Guid.NewGuid(), null, asset, "FICTIONAL-RH-" + asset.ToString("N")[..6], "RUNNING_HOURS", "HOUR", "MANUAL", 1);
        await PmsCode(await Send(client, PointsUrl, parent), HttpStatusCode.Forbidden, "permission_denied");
        PmsActor(host.Services, engineer, "maintenance.measurement.write"); await Token(client);
        await PmsCode(await Send(client, PointsUrl, parent with { Unit = "BAR" }), HttpStatusCode.UnprocessableEntity, "measurement_definition_invalid");
        var parentPoint = (await PmsOk<MeasurementPointOutcome>(await Send(client, PointsUrl, parent))).Point;
        // No reading yet: the counter state is UNKNOWN, never a positive state.
        Assert.Equal(("UNKNOWN", "UNCONFIGURED"), (parentPoint.Band, parentPoint.PolicyState));
        var sub = parent with { Id = Guid.NewGuid(), Code = parent.Code + "-TC", ParentPointId = parent.Id };
        await PmsOk<MeasurementPointOutcome>(await Send(client, PointsUrl, sub));
        // A parent cannot become a descendant of its own sub counter.
        await PmsCode(await Send(client, PointsUrl, parent with { ExpectedVersion = parentPoint.Version, ParentPointId = sub.Id }), HttpStatusCode.UnprocessableEntity, "hierarchy_cycle");
        var now = DateTimeOffset.UtcNow;
        await PmsOk<MeasurementReadingOutcome>(await Send(client, $"{PointsUrl}/{sub.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "100.0", now.AddHours(-3), "MANUAL")));
        await PmsOk<MeasurementReadingOutcome>(await Send(client, $"{PointsUrl}/{parent.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "1000.0", now.AddHours(-2), "MANUAL")));
        var reading = new MeasurementReadingCommand(Guid.NewGuid(), "1010.0", now.AddHours(-1), "MANUAL", "FICTIONAL engine log page 12");
        var key = Guid.NewGuid().ToString();
        var outcome = await PmsOk<MeasurementReadingOutcome>(await Send(client, $"{PointsUrl}/{parent.Id}/readings", reading, key));
        Assert.Equal(("1010.0", "10.0"), (outcome.Reading.Value, outcome.Reading.Delta));
        var derived = Assert.Single(outcome.Propagated);
        Assert.Equal((sub.Id, "110.0", "PROPAGATED", reading.Id), (derived.PointId, derived.Value, derived.SourceType, derived.PropagatedFromReadingId!.Value));
        // Replay returns the committed outcome and never repeats propagation.
        var replay = await PmsOk<MeasurementReadingOutcome>(await Send(client, $"{PointsUrl}/{parent.Id}/readings", reading, key));
        Assert.Single(replay.Propagated);
        await PmsCode(await Send(client, $"{PointsUrl}/{parent.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "990.0", now.AddMinutes(-30), "MANUAL")),
            HttpStatusCode.UnprocessableEntity, "counter_decreasing");
        await PmsCode(await Send(client, $"{PointsUrl}/{parent.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "1020.0", now.AddMinutes(-30), "DEVICE")),
            HttpStatusCode.UnprocessableEntity, "measurement_definition_invalid");
        var history = (await client.GetFromJsonAsync<MeasurementReadingPage>($"/api/v1/{PointsUrl}/{sub.Id}/readings", Ct))!;
        Assert.Equal(new[] { "110.0", "100.0" }, history.Items.Select(x => x.Value));

        // Temperature point: no tenant threshold -> UNCONFIGURED band and a REVIEW_REQUIRED plan.
        var temp = new MeasurementPointSaveCommand(Guid.NewGuid(), null, asset, "FICTIONAL-EXH-" + asset.ToString("N")[..6], "TEMPERATURE", "CEL", "MANUAL", 1, "-50", "700");
        await PmsOk<MeasurementPointOutcome>(await Send(client, PointsUrl, temp));
        await PmsOk<MeasurementReadingOutcome>(await Send(client, $"{PointsUrl}/{temp.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "460.0", now.AddMinutes(-5), "MANUAL")));
        await PmsCode(await Send(client, $"{PointsUrl}/{temp.Id}/readings", new MeasurementReadingCommand(Guid.NewGuid(), "701.0", now.AddMinutes(-4), "MANUAL")),
            HttpStatusCode.UnprocessableEntity, "reading_out_of_range");
        var page = (await client.GetFromJsonAsync<MeasurementPage>($"/api/v1/{PointsUrl}", Ct))!;
        Assert.Equal(("UNCONFIGURED", "UNCONFIGURED"), (page.Items.Single(p => p.Id == temp.Id).Band, page.Items.Single(p => p.Id == temp.Id).PolicyState));
        var plan = new MaintenanceCommand(Guid.NewGuid(), null, asset, SeedVessel.Id, PmsOwnerRole, new(TriggerType: "Condition", TimeZone: "UTC",
            Provenance: "SyntheticDeclaration", Criticality: "DeclaredCritical", MeasurementPointId: temp.Id, TriggerOn: "WARNING"), DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, plan)).StatusCode);
        JsonElement Row(JsonElement p) => p.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == plan.Id);
        var unconfigured = Row(await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl, Ct)).GetProperty("due");
        Assert.Equal(("ReviewRequired", "MeasurementPolicyUnconfigured"), (unconfigured.GetProperty("state").GetString(), unconfigured.GetProperty("reason").GetString()));
        var settings = host.Services.GetRequiredService<MaintenanceObservationSettings>();
        settings.MeasurementPolicies = [new(SeedVessel.TenantId, SeedVessel.Id, temp.Code, "synthetic-threshold-v1", "FICTIONAL OEM manual reference", 60,
            NormalMin: "200", NormalMax: "420", WarningMax: "450", AlarmMax: "500")];
        var configured = Row(await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl, Ct)).GetProperty("due");
        Assert.Equal(("Due", "planning-current-v1/synthetic-threshold-v1/WARNING"), (configured.GetProperty("state").GetString(), configured.GetProperty("algorithm").GetString()));
        Assert.Equal("WARNING", (await client.GetFromJsonAsync<MeasurementPage>($"/api/v1/{PointsUrl}", Ct))!.Items.Single(p => p.Id == temp.Id).Band);

        await using var db = Db();
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.ActorId == engineer.ToString() && (e.EntityType == nameof(MeasurementReading) || e.EntityType == nameof(MeasurementPoint))).ToArrayAsync(Ct);
        Assert.Contains(audit, e => e.EntityType == nameof(MeasurementReading) && e.ChangeSummary!.Contains("MeasurementReadingRecord"));
        Assert.Contains(audit, e => e.EntityType == nameof(MeasurementPoint) && e.ChangeSummary!.Contains("MeasurementPointSave"));
        Assert.DoesNotContain("1010.0", JsonSerializer.Serialize(audit)); Assert.DoesNotContain("FICTIONAL engine log", JsonSerializer.Serialize(audit));
        Assert.Equal(1, await db.MeasurementReadings.CountAsync(r => r.PropagatedFromReadingId == reading.Id, Ct));
    }

    private async Task<(Guid Plan, DateOnly Due)> PmsCalendarPlan(HttpClient client, Guid asset)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var plan = new MaintenanceCommand(Guid.NewGuid(), null, asset, SeedVessel.Id, PmsOwnerRole, new(TriggerType: "Calendar", IntervalValue: "20", IntervalUnit: "day",
            LastDoneDate: today.AddDays(-10), TimeZone: "UTC", Provenance: "SyntheticDeclaration", Criticality: "DeclaredCritical"), DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, plan)).StatusCode);
        return (plan.Id, today.AddDays(10));
    }

    [Fact]
    [Trait("Requirement", "DEV-109")]
    public async Task Postponement_is_a_request_and_a_separate_human_decision_bounded_by_the_tenant_max_extension()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        var chief = Guid.NewGuid(); var manager = Guid.NewGuid();
        PmsActor(host.Services, chief, "maintenance.postponement.request"); await Token(client);
        var asset = await PmsAsset(client, "STANDARD");
        var (planId, due) = await PmsCalendarPlan(client, asset);
        var path = $"{PlansUrl}/{planId}";
        var view = (await client.GetFromJsonAsync<MaintenancePostponementPage>($"/api/v1/{path}/postponements", Ct))!;
        Assert.Equal((due, "UNCONFIGURED", true, false), (view.OriginalDue!.Value, view.PolicyState, view.CanRequest, view.CanDecide));
        await PmsCode(await Send(client, $"{path}/postpone", new MaintenancePostponementRequestCommand(Guid.NewGuid(), due, "FICTIONAL spare parts delayed")),
            HttpStatusCode.UnprocessableEntity, "postponement_deadline_not_after_due");
        await PmsCode(await Send(client, $"{path}/postpone", new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(5), "short")),
            HttpStatusCode.UnprocessableEntity, "invalid_reason");
        // An unconfigured tenant can request (draft) but nobody can approve.
        var request = new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(20), "FICTIONAL spare parts arrive at the next port call");
        var requested = (await PmsOk<MaintenancePostponementOutcome>(await Send(client, $"{path}/postpone", request))).Postponement;
        Assert.Equal(("REQUESTED", due, 20, "PENDING_DECISION", chief), (requested.State, requested.OriginalDue, requested.ExtensionDays, requested.ReadinessImpact, requested.RequestedBy));
        await PmsCode(await Send(client, $"{path}/postpone", request with { Id = Guid.NewGuid() }), HttpStatusCode.Conflict, "postponement_pending");
        var decision = $"{path}/postponements/{request.Id}/decision";
        var approve = new MaintenancePostponementDecisionCommand(requested.Version, "APPROVE", "FICTIONAL technical manager review");
        PmsActor(host.Services, chief, "maintenance.postponement.request", "maintenance.postponement.decide"); await Token(client);
        await PmsCode(await Send(client, decision, approve), HttpStatusCode.Forbidden, "separation_of_duties");
        PmsActor(host.Services, manager, "maintenance.postponement.decide"); await Token(client);
        await PmsCode(await Send(client, decision, approve), HttpStatusCode.UnprocessableEntity, "postponement_policy_unconfigured");
        var settings = host.Services.GetRequiredService<MaintenanceObservationSettings>();
        settings.PostponementPolicies = [new(SeedVessel.TenantId, "FICTIONAL-DEFERRAL-POLICY", "synthetic-v1", 15)];
        await PmsCode(await Send(client, decision, approve), HttpStatusCode.UnprocessableEntity, "postponement_extension_exceeds_policy");
        settings.PostponementPolicies = [new(SeedVessel.TenantId, "FICTIONAL-DEFERRAL-POLICY", "synthetic-v2", 30)];
        var approved = (await PmsOk<MaintenancePostponementOutcome>(await Send(client, decision, approve))).Postponement;
        Assert.Equal(("APPROVED", manager, "synthetic-v2", 30, "CONDITIONAL_DEFERRED"), (approved.State, approved.DecidedBy!.Value, approved.PolicyVersion, approved.MaxExtensionDays!.Value, approved.ReadinessImpact));
        await PmsCode(await Send(client, decision, approve with { ExpectedVersion = approved.Version }), HttpStatusCode.Conflict, "invalid_state");
        // The original due is unchanged; the plan shows the overlay only.
        var plan = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl, Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == planId);
        Assert.Equal(due.ToString("yyyy-MM-dd"), plan.GetProperty("due").GetProperty("nextDueDate").GetString());
        Assert.Equal("CONDITIONAL_DEFERRED", plan.GetProperty("postponement").GetProperty("readinessImpact").GetString());
        // AT-034: a chained request is measured from the first original due date.
        PmsActor(host.Services, chief, "maintenance.postponement.request"); await Token(client);
        await PmsCode(await Send(client, $"{path}/postpone", new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(31), "FICTIONAL second postponement request")),
            HttpStatusCode.UnprocessableEntity, "postponement_extension_exceeds_policy");
        await PmsCode(await Send(client, $"{path}/postpone", new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(15), "FICTIONAL second postponement request")),
            HttpStatusCode.UnprocessableEntity, "postponement_deadline_not_after_previous");

        await using var db = Db();
        var stored = await db.MaintenancePostponements.AsNoTracking().SingleAsync(x => x.Id == request.Id, Ct);
        Assert.Equal((chief, manager, "APPROVED", due), (stored.RequestedBy, stored.DecidedBy!.Value, stored.State, stored.OriginalDue));
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.EntityType == nameof(MaintenancePostponement) && e.ResourceId == planId).ToArrayAsync(Ct);
        Assert.Contains(audit, e => e.ActorId == chief.ToString() && e.ChangeSummary!.Contains("MaintenancePostpone"));
        Assert.Contains(audit, e => e.ActorId == manager.ToString() && e.ChangeSummary!.Contains("MaintenancePostponementDecide"));
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        await using var security = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        var events = await security.Set<SecurityEvent>().AsNoTracking().Where(x => x.Action == "MaintenancePostponementCommand").ToArrayAsync(Ct);
        Assert.Contains(events, x => x.Result == "postponement_policy_unconfigured");
        Assert.Contains(events, x => x.Result == "separation_of_duties");
    }

    [Fact]
    [Trait("Requirement", "DEV-109")]
    public async Task Critical_or_unknown_criticality_postponement_needs_an_approved_risk_review_and_marks_readiness_review_required()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host);
        var chief = Guid.NewGuid(); var manager = Guid.NewGuid();
        PmsActor(host.Services, chief, "maintenance.postponement.request"); await Token(client);
        var asset = await PmsAsset(client, null); // criticality undeclared => UNKNOWN, treated like CRITICAL
        var (planId, due) = await PmsCalendarPlan(client, asset);
        host.Services.GetRequiredService<MaintenanceObservationSettings>().PostponementPolicies = [new(SeedVessel.TenantId, "FICTIONAL-DEFERRAL-POLICY", "synthetic-v1", 30)];
        await PmsCode(await Send(client, $"{PlansUrl}/{planId}/postpone", new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(5),
            "FICTIONAL request citing a missing risk review", RiskAssessmentId: Guid.NewGuid())), HttpStatusCode.BadRequest, "linked_resource_unavailable");
        var request = new MaintenancePostponementRequestCommand(Guid.NewGuid(), due.AddDays(5), "FICTIONAL class surveyor attendance pending");
        var requested = (await PmsOk<MaintenancePostponementOutcome>(await Send(client, $"{PlansUrl}/{planId}/postpone", request))).Postponement;
        Assert.Equal(("UNKNOWN", "REVIEW_REQUIRED"), (requested.Criticality, requested.ReadinessImpact));
        var plan = (await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PlansUrl, Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == planId);
        Assert.Equal(("UNKNOWN", "REVIEW_REQUIRED"), (plan.GetProperty("assetCriticality").GetString(), plan.GetProperty("postponement").GetProperty("readinessImpact").GetString()));
        PmsActor(host.Services, manager, "maintenance.postponement.decide"); await Token(client);
        var view = (await client.GetFromJsonAsync<MaintenancePostponementPage>($"/api/v1/{PlansUrl}/{planId}/postponements", Ct))!;
        Assert.Equal(new[] { "risk_review_required" }, view.Items.Single().DecisionBlockers);
        await PmsCode(await Send(client, $"{PlansUrl}/{planId}/postponements/{request.Id}/decision",
            new MaintenancePostponementDecisionCommand(requested.Version, "APPROVE", "FICTIONAL attempted approval")), HttpStatusCode.UnprocessableEntity, "risk_review_required");
        // Rejecting needs no policy or risk review; it is still a recorded decision by a different person.
        var rejected = (await PmsOk<MaintenancePostponementOutcome>(await Send(client, $"{PlansUrl}/{planId}/postponements/{request.Id}/decision",
            new MaintenancePostponementDecisionCommand(requested.Version, "REJECT", "FICTIONAL reject: perform before departure")))).Postponement;
        Assert.Equal(("REJECTED", manager, "UNCHANGED"), (rejected.State, rejected.DecidedBy!.Value, rejected.ReadinessImpact));
        await using var db = Db();
        Assert.Null((await db.MaintenancePostponements.AsNoTracking().SingleAsync(x => x.Id == request.Id, Ct)).PolicyRef);
    }

    [Fact]
    [Trait("Requirement", "DEV-108")]
    public async Task Optional_pms_fixture_is_idempotent_fictional_and_seeds_no_reading_threshold_or_decision()
    {
        await using (var db = new PoseidonDbContext(Poseidon.Persistence.Seeding.SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            await Poseidon.Persistence.Seeding.PmsFixtures.AddMissingAsync(db, seed.Vessels, Ct); await Poseidon.Persistence.Seeding.PmsFixtures.AddMissingAsync(db, seed.Vessels, Ct);
            await tx.CommitAsync(Ct);
        }
        await using var verify = Db();
        var point = await verify.MeasurementPoints.AsNoTracking().SingleAsync(p => p.Id == Poseidon.Persistence.Seeding.PmsFixtures.PointId(SeedVessel.Id), Ct);
        Assert.Equal(("SyntheticUnverified", "Draft", 1L), (point.Provenance, point.Status, point.Revision));
        Assert.StartsWith("FICTIONAL-", point.Code);
        Assert.Equal(1, await verify.MeasurementPointRevisions.CountAsync(r => r.PointId == point.Id, Ct));
        Assert.Equal(0, await verify.MeasurementReadings.CountAsync(Ct));
        Assert.Equal(0, await verify.MaintenancePostponements.CountAsync(Ct));
    }
}
