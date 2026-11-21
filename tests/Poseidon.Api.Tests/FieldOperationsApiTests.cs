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

// S9 WP-D DEV-089 (18.01 row 90, T-TUG-010), DEV-090 (row 91, T-TUG-011), DEV-111 (row 112, T-PMS2-004):
// real PostgreSQL + actual host. Synthetic identities, appointments and FICTIONAL content only. The readiness GO is
// a Master human record that the product writes only through Decide on READY; because the advisory engine cannot
// reach READY with synthetic sources, the execution test inserts clearly FICTIONAL GO stand-in rows bound to the exact
// current assessment hash (test database only, never a seed). AUTHORED; execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private void FieldActor(IServiceProvider services, Guid actor, params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        string[] owned = ["tug.execution.verify", "drydock.read", "drydock.write", "drydock.verify"];
        // The single fixture membership carries the synthetic Tug Master appointment; remember it per host so a
        // crew identity can be modelled without it and the Master identity gets it back.
        if (!fieldAppointments.TryGetValue(settings, out var appointments)) fieldAppointments[settings] = appointments = m.TugOperationsAuthorities;
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = "Human", TugOperationsAuthorities = appointments,
            Permissions = [.. m.Permissions.Where(p => !p.StartsWith("hse.", StringComparison.Ordinal) && !owned.Contains(p)), .. RiskPermissions, .. extra] };
    }
    private readonly Dictionary<FederationSettings, TugOperationsAuthority[]> fieldAppointments = new(ReferenceEqualityComparer.Instance);
    // Crew: same tug scope and permissions, but no Tug Master/Master appointment of their own.
    private void FieldCrew(IServiceProvider services, Guid actor)
    {
        FieldActor(services, actor);
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { TugOperationsAuthorities = [] };
    }
    // Crew (Human without appointment) or a non-human principal that keeps the synthetic appointment rows.
    private void FieldPrincipal(IServiceProvider services, Guid actor, string kind)
    {
        if (kind == "Human") { FieldCrew(services, actor); return; }
        FieldActor(services, actor);
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { ActorKind = kind };
    }
    private async Task<T> FieldGet<T>(HttpClient c, string path) => (await c.GetFromJsonAsync<T>("/api/v1/" + path, Ct))!;

    [Fact]
    [Trait("Requirement", "DEV-089")]
    public async Task Towage_execution_starts_only_on_a_current_go_keeps_an_append_only_timeline_and_needs_an_independent_verifier()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        Guid planner = Guid.NewGuid(), master = Guid.NewGuid(), crew = Guid.NewGuid();
        FieldActor(host.Services, planner); await Token(c);
        var job = await SubmittedJob(c);
        var proposal = await RiskOk<TugAssignmentDto>(await Send(c, $"tug-assignments?vessel={Tug.Id}", Proposal(job)));
        var release = await RiskOk<TowEquipmentDto>(await Send(c, $"tug-tow-equipment?vessel={Tug.Id}", new TowEquipmentSaveCommand(Guid.NewGuid(), null,
            new TowEquipmentInputs("EmergencyRelease", "FICTIONAL release", null, null, null, "Serviceable", true, null, null, null))));
        var plan = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans"), new TowagePlanCreateCommand(Guid.NewGuid(), job.Id, TowagePlan(release.Id))));
        var submitted = await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{plan.Id}/submit"), new TowageSubmitCommand(plan.Version, plan.Hash)));
        var method = await PublishedRiskMethod(c, host.Services, planner, master);
        FieldActor(host.Services, planner); await Token(c);
        var hazard = new RiskHazard(Guid.NewGuid(), "Connect towline", "FICTIONAL girting", ["Capsize"], new("L2", "S2", "FICTIONAL rationale"),
            [new(Guid.NewGuid(), "FICTIONAL tow angle limit", "EXISTING", "Tug Master", true, ["BAR-1"])], ["BAR-1"], new("L1", "S2", "FICTIONAL rationale"));
        var ra = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath(vessel: Tug.Id), new RiskAssessmentCreateCommand(Guid.NewGuid(), "FICTIONAL-EXE-" + Guid.NewGuid().ToString("N")[..6],
            new RiskAssessmentContent("FICTIONAL towage JSA", "FICTIONAL harbour towage", "TOWAGE", "FICTIONAL scope", method.Id, [hazard], true,
                [new(Guid.NewGuid(), "FICTIONAL limits exceeded")], [new(planner, "PREPARER")]), [new("TugJob", job.Id)])));
        var raSubmitted = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{ra.Id}/submit", Tug.Id), new RiskAssessmentSubmitCommand(ra.Version, ra.Hash)));
        var briefing = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings"), new TowageBriefingCreateCommand(Guid.NewGuid(), job.Id, TowageBriefing(ra.Id, crew))));

        FieldActor(host.Services, master); await Token(c);
        await RiskOk<TowagePlanDto>(await Send(c, TowagePath("towage-plans", $"/{plan.Id}/decisions"),
            new TowagePlanDecisionCommand(submitted.Version, submitted.Hash, "APPROVE", "FICTIONAL Tug Master plan review", TugMasterGrant)));
        await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{ra.Id}/review", Tug.Id), new RiskAssessmentReviewCommand(raSubmitted.Version, raSubmitted.Hash, "APPROVE", "FICTIONAL HSE review record")));
        var issued = await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{briefing.Id}/issue"), new TowageBriefingIssueCommand(briefing.Version, briefing.Hash, TugMasterGrant)));
        FieldCrew(host.Services, crew); await Token(c);
        await RiskOk<TowageBriefingDto>(await Send(c, TowagePath("towage-risk-briefings", $"/{briefing.Id}/acknowledgements"), new TowageBriefingAcknowledgeCommand(issued.Revision, issued.Hash, "ACK")));

        // No GO human record yet: the start gate is blocked and the server refuses the start.
        FieldActor(host.Services, master); await Token(c);
        var gate = Assert.Single((await FieldGet<TowageExecutionJobView>(c, $"towage-executions?vessel={Tug.Id}&job={job.Id}")).Tugs);
        Assert.False(gate.CanStart); Assert.Contains("readiness_go_missing", gate.Blockers); Assert.Null(gate.ExecutionId);
        var executionId = Guid.NewGuid();
        await AssertCode(await Send(c, $"towage-executions?vessel={Tug.Id}", new TowageExecutionStartCommand(executionId, job.Id, Tug.Id, TugMasterGrant, Guid.NewGuid(), null)),
            (HttpStatusCode)422, "towage_start_blocked");

        // The advisory engine cannot reach READY with synthetic sources (no whole-window weather source, 60.02 p196).
        // The product writes a GO only through Decide on READY (TugReadinessEngine/TugReadinessDecisionPolicy); these
        // stand-in rows are test arrangement of that accepted S7 record, bound to the exact current assessment hash
        // (test database only, never a seed).
        var readiness = await TowageReadiness(c, proposal.Id);
        async Task<Guid> StandInGo(string assessedState, DateTimeOffset at) {
            var id = Guid.NewGuid();
            await using var db = Db();
            db.TugReadinessDecisions.Add(new() { TenantId = Tug.TenantId, VesselId = Tug.Id, Id = id, AssignmentId = proposal.Id, AssignmentRevision = proposal.Revision, TugId = Tug.Id,
                Decision = "GO", AssessedState = assessedState, AssessmentHash = readiness.AssessmentHash, Reason = "FICTIONAL test-only GO stand-in", ActorId = master,
                AuthorityId = TugMasterGrant, Duty = "TugMaster", RecordedAt = at });
            await db.SaveChangesAsync(Ct);
            return id;
        }
        // QA N2: a CONDITIONAL GO (admitted by the DB CHECK) on the current hash never starts an operation.
        var conditional = await StandInGo(TugReadinessEngine.Conditional, DateTimeOffset.UtcNow.AddSeconds(-30));
        var notReady = Assert.Single((await FieldGet<TowageExecutionJobView>(c, $"towage-executions?vessel={Tug.Id}&job={job.Id}")).Tugs);
        Assert.False(notReady.CanStart); Assert.Equal(["readiness_not_ready"], notReady.Blockers); Assert.Equal(conditional, notReady.ReadinessDecisionId);
        await AssertCode(await Send(c, $"towage-executions?vessel={Tug.Id}", new TowageExecutionStartCommand(executionId, job.Id, Tug.Id, TugMasterGrant, conditional, null)),
            (HttpStatusCode)422, "towage_start_blocked");

        var go = await StandInGo(TugReadinessEngine.Ready, DateTimeOffset.UtcNow);
        var ready = Assert.Single((await FieldGet<TowageExecutionJobView>(c, $"towage-executions?vessel={Tug.Id}&job={job.Id}")).Tugs);
        Assert.True(ready.CanStart, string.Join(",", ready.Blockers)); Assert.Equal(go, ready.ReadinessDecisionId);
        var start = new TowageExecutionStartCommand(executionId, job.Id, Tug.Id, TugMasterGrant, go, "FICTIONAL berth 3");
        // A foreign or unappointed authority (shore/AI/system) is refused before any gate is read.
        await AssertCode(await Send(c, $"towage-executions?vessel={Tug.Id}", start with { AuthorityId = Guid.NewGuid() }), HttpStatusCode.Forbidden, "tug_authority_required");
        // QA B3: crew without an appointment, and AI/system principals even with the synthetic appointment, never start.
        foreach (var (who, kind) in new[] { (crew, "Human"), (Guid.NewGuid(), "AI"), (Guid.NewGuid(), "System") }) {
            FieldPrincipal(host.Services, who, kind); await Token(c);
            await AssertCode(await Send(c, $"towage-executions?vessel={Tug.Id}", start), HttpStatusCode.Forbidden, "tug_authority_required");
        }
        await using (var none = Db()) Assert.False(await none.TowageExecutions.AnyAsync(x => x.Id == executionId, Ct));
        FieldActor(host.Services, master); await Token(c);
        var key = Guid.NewGuid().ToString();
        var started = await RiskOk<TowageExecutionView>(await Send(c, $"towage-executions?vessel={Tug.Id}", start, key));
        Assert.Equal(("ACTIVE", "PENDING", 1L, "STARTED"), (started.Execution.State, started.Execution.VerificationState, started.Execution.Sequence, started.Timeline.Single().Kind));
        Assert.Equal((plan.Id, briefing.Id, go), (started.Execution.PlanId, started.Execution.BriefingId, started.Execution.ReadinessDecisionId));
        Assert.Equal(executionId, (await RiskOk<TowageExecutionView>(await Send(c, $"towage-executions?vessel={Tug.Id}", start, key))).Execution.Id);
        await AssertCode(await Send(c, $"towage-executions?vessel={Tug.Id}", start with { Id = Guid.NewGuid() }), (HttpStatusCode)422, "towage_start_blocked");

        // Crew record observations with their units; the ledger refuses an impossible towline sequence.
        FieldCrew(host.Services, crew); await Token(c);
        string Events() => $"towage-executions/{executionId}/events?vessel={Tug.Id}";
        TowageEventInput E(string kind, string? value = null, string? unit = null, string? result = null, string? text = null, string? reference = null) =>
            new(kind, DateTimeOffset.UtcNow, value, unit, result, text, reference, null);
        await RiskOk<TowageExecutionView>(await Send(c, Events(), new TowageExecutionEventCommand(E("TOWLINE_CONNECTED", text: "FICTIONAL centre lead"))));
        await RiskOk<TowageExecutionView>(await Send(c, Events(), new TowageExecutionEventCommand(E("LOAD_OBSERVED", "42.5", "t"))));
        var released = await RiskOk<TowageExecutionView>(await Send(c, Events(), new TowageExecutionEventCommand(E("TOWLINE_RELEASED"))));
        await AssertCode(await Send(c, Events(), new TowageExecutionEventCommand(E("TOWLINE_RELEASED"))), HttpStatusCode.Conflict, "invalid_state");
        await AssertCode(await Send(c, Events(), new TowageExecutionEventCommand(E("LOAD_OBSERVED", "42.5"))), HttpStatusCode.BadRequest, "validation_failed");
        Assert.False(released.Ledger.TowlineConnected); Assert.NotNull(released.Ledger.TowlineConnectedAt); Assert.NotNull(released.Ledger.TowlineReleasedAt);
        Assert.Equal(("42.5", "t"), (released.Ledger.MaxTowLoad.Single().Value, released.Ledger.MaxTowLoad.Single().Unit));
        Assert.Equal("UNKNOWN", released.Ledger.CommunicationState); Assert.False(released.CanTransition);
        // QA B3: crew record observations, but crew, AI and system principals never SUSPEND or ABORT the operation.
        // QA S9 N4: an AI/system principal does not write ledger observations either (a person's record, 60.02 p228).
        foreach (var (who, kind) in new[] { (crew, "Human"), (Guid.NewGuid(), "AI"), (Guid.NewGuid(), "System") }) {
            FieldPrincipal(host.Services, who, kind); await Token(c);
            if (kind != "Human")
                await AssertCode(await Send(c, Events(), new TowageExecutionEventCommand(E("NOTE", text: "FICTIONAL machine note"))), HttpStatusCode.Forbidden, "human_actor_required");
            foreach (var action in new[] { "SUSPEND", "ABORT" })
                await AssertCode(await Send(c, $"towage-executions/{executionId}/transitions?vessel={Tug.Id}", new TowageExecutionTransitionCommand(released.Execution.Version, action,
                    "FICTIONAL unauthorised stop", DateTimeOffset.UtcNow, TugMasterGrant, null)), HttpStatusCode.Forbidden, "tug_authority_required");
        }

        // Tug Master transitions with reasons; COMPLETE needs an end position.
        FieldActor(host.Services, master); await Token(c);
        string Transitions() => $"towage-executions/{executionId}/transitions?vessel={Tug.Id}";
        var suspended = await RiskOk<TowageExecutionView>(await Send(c, Transitions(), new TowageExecutionTransitionCommand(released.Execution.Version, "SUSPEND",
            "FICTIONAL hold for traffic", DateTimeOffset.UtcNow, TugMasterGrant, null)));
        Assert.Equal("SUSPENDED", suspended.Execution.State);
        await AssertCode(await Send(c, Transitions(), new TowageExecutionTransitionCommand(suspended.Execution.Version, "COMPLETE", "FICTIONAL completion note long enough",
            DateTimeOffset.UtcNow, TugMasterGrant, "FICTIONAL berth 4")), HttpStatusCode.Conflict, "invalid_state");
        var resumed = await RiskOk<TowageExecutionView>(await Send(c, Transitions(), new TowageExecutionTransitionCommand(suspended.Execution.Version, "RESUME",
            "FICTIONAL traffic clear", DateTimeOffset.UtcNow, TugMasterGrant, null)));
        await AssertCode(await Send(c, Transitions(), new TowageExecutionTransitionCommand(resumed.Execution.Version, "COMPLETE", "FICTIONAL completion note long enough",
            DateTimeOffset.UtcNow, TugMasterGrant, null)), (HttpStatusCode)422, "towage_record_incomplete");
        var completed = await RiskOk<TowageExecutionView>(await Send(c, Transitions(), new TowageExecutionTransitionCommand(resumed.Execution.Version, "COMPLETE",
            "FICTIONAL vessel all fast at berth 4", DateTimeOffset.UtcNow, TugMasterGrant, "FICTIONAL berth 4")));
        Assert.Equal(("COMPLETED", master), (completed.Execution.State, completed.Execution.EndedBy!.Value));
        // Terminal: only evidence/notes; no new operating observation.
        var evidence = await RiskOk<TowageExecutionView>(await Send(c, Events(), new TowageExecutionEventCommand(E("EVIDENCE", result: "PHOTO", reference: "FICTIONAL-PHOTO-1"))));
        await AssertCode(await Send(c, Events(), new TowageExecutionEventCommand(E("LOAD_OBSERVED", "10", "t"))), HttpStatusCode.Conflict, "invalid_state");

        // Verification: never the Tug Master who started/completed; a different person records it.
        FieldActor(host.Services, master, "tug.execution.verify"); await Token(c);
        var verify = new TowageExecutionVerifyCommand(evidence.Execution.Version, evidence.Execution.Hash, "VERIFIED", "FICTIONAL record reviewed");
        await AssertCode(await Send(c, $"towage-executions/{executionId}/verification?vessel={Tug.Id}", verify), HttpStatusCode.Forbidden, "separation_of_duties");
        FieldActor(host.Services, planner, "tug.execution.verify"); await Token(c);
        var verified = await RiskOk<TowageExecutionView>(await Send(c, $"towage-executions/{executionId}/verification?vessel={Tug.Id}", verify));
        Assert.Equal(("VERIFIED", planner), (verified.Execution.VerificationState, verified.Execution.VerifiedBy!.Value));
        Assert.Equal(["STARTED", "TOWLINE_CONNECTED", "LOAD_OBSERVED", "TOWLINE_RELEASED", "SUSPENDED", "RESUMED", "COMPLETED", "EVIDENCE", "VERIFIED"], verified.Timeline.Select(t => t.Kind));

        await using var check = Db();
        var ledger = await check.TowageExecutionEvents.Where(e => e.ExecutionId == executionId).OrderBy(e => e.Sequence).ToArrayAsync(Ct);
        Assert.Equal(Enumerable.Range(1, 9).Select(i => (long)i), ledger.Select(e => e.Sequence));
        Assert.All(ledger.Skip(1), (e, i) => Assert.Equal(ledger[i].Hash, e.PreviousHash));
        var audit = await check.AuditEvents.Where(e => e.ResourceId == executionId).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(TowageExecutionEvent) && e.ActorId == crew.ToString());
        ledger[2].PayloadJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-090")]
    public async Task Tug_daily_log_replays_offline_commands_refuses_meter_sequence_conflicts_and_needs_an_independent_master_review()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        Guid author = Guid.NewGuid(), master = Guid.NewGuid();
        FieldActor(host.Services, author); await Token(c);
        var date = new DateOnly(2030, 1, 1).AddDays(Random.Shared.Next(0, 3000));
        TugDailyLogContent Content(string closing = "106", Guid[]? defects = null) => new("UTC", [new(Guid.NewGuid(), "FICTIONAL ME-PORT", "100", closing, null)],
            new("1200", "L", "FICTIONAL sounding record"), [], defects ?? [], "FICTIONAL day summary for the synthetic tug");
        var route = $"tug-daily-logs?vessel={Tug.Id}";
        await AssertCode(await Send(c, route, new TugDailyLogCreateCommand(Guid.NewGuid(), date, "OTHER", null, Content("90"))), HttpStatusCode.Conflict, "meter_sequence_conflict");
        var create = new TugDailyLogCreateCommand(Guid.NewGuid(), date, "OTHER", DateTimeOffset.UtcNow.AddMinutes(-30), Content());
        var key = Guid.NewGuid().ToString();
        var created = await RiskOk<TugDailyLogView>(await Send(c, route, create, key));
        Assert.Equal(("DRAFT", "SYNCED", 1L), (created.Log.State, created.Log.SyncState, created.Log.Revision));
        Assert.NotNull(created.Log.CapturedAt);
        // An offline replay with the same key returns the committed log, never a second one.
        Assert.Equal(created.Log.Id, (await RiskOk<TugDailyLogView>(await Send(c, route, create, key))).Log.Id);
        await AssertCode(await Send(c, route, create with { Id = Guid.NewGuid() }), HttpStatusCode.Conflict, "version_conflict");
        var path = $"tug-daily-logs/{create.Id}?vessel={Tug.Id}";
        await AssertCode(await Send(c, path, new TugDailyLogUpdateCommand(created.Log.Version, null, Content(defects: [Guid.NewGuid()])), update: true),
            (HttpStatusCode)422, "linked_resource_unavailable");
        var updated = await RiskOk<TugDailyLogView>(await Send(c, path, new TugDailyLogUpdateCommand(created.Log.Version, DateTimeOffset.UtcNow, Content("108")), update: true));
        await AssertCode(await Send(c, path, new TugDailyLogUpdateCommand(created.Log.Version, null, Content()), update: true), HttpStatusCode.Conflict, "version_conflict");
        Assert.Equal(2L, updated.Log.Revision); Assert.Equal("8", updated.Reconciliation.MeterHours);
        Assert.NotEqual(TugDailyLogPolicy.Reconciled, updated.Reconciliation.State);
        Assert.Contains(updated.Reconciliation.Reasons, r => r.Code == "meter_previous_unknown");
        Assert.Equal("UNKNOWN:SafetyEventSourceUnavailable", updated.SafetyEvents);
        var submitted = await RiskOk<TugDailyLogView>(await Send(c, $"tug-daily-logs/{create.Id}/submit?vessel={Tug.Id}", new TugDailyLogSubmitCommand(updated.Log.Version, updated.Log.Hash)));
        Assert.Equal(("SUBMITTED", "ATTENTION"), (submitted.Log.State, submitted.MasterReview));
        var review = new TugDailyLogReviewCommand(submitted.Log.Version, submitted.Log.Hash, "VERIFY", "FICTIONAL Master end of day review", TugMasterGrant);
        await AssertCode(await Send(c, $"tug-daily-logs/{create.Id}/review?vessel={Tug.Id}", review), HttpStatusCode.Forbidden, "separation_of_duties");
        FieldActor(host.Services, master); await Token(c);
        var reviewed = await RiskOk<TugDailyLogView>(await Send(c, $"tug-daily-logs/{create.Id}/review?vessel={Tug.Id}", review));
        Assert.Equal(("VERIFIED", "REVIEWED"), (reviewed.Log.State, reviewed.MasterReview));
        var record = Assert.Single(reviewed.Reviews);
        Assert.Equal(("VERIFY", "REVIEW_REQUIRED", master), (record.Decision, record.Reconciliation, record.ActorId));
        await AssertCode(await Send(c, path, new TugDailyLogUpdateCommand(reviewed.Log.Version, null, Content()), update: true), HttpStatusCode.Conflict, "invalid_state");
        Assert.Contains((await FieldGet<TugDailyLogListView>(c, route)).Items, i => i.Id == create.Id && i.MasterReview == "REVIEWED");

        await using var db = Db();
        Assert.Equal(["CREATE", "EDIT", "SUBMIT", "VERIFY"], await db.TugDailyLogRevisions.Where(r => r.LogId == create.Id).OrderBy(r => r.Revision).Select(r => r.ReasonCode).ToArrayAsync(Ct));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == create.Id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(TugDailyLogReview) && e.ActorId == master.ToString());
        // QA S9 N5: the database itself refuses a review row whose reviewer submitted or authored the revision.
        var stored = await db.TugDailyLogReviews.AsNoTracking().SingleAsync(r => r.LogId == create.Id, Ct);
        Assert.Equal((author, false), (stored.SubmittedBy, stored.AuthorsJson.Contains(master.ToString("D"), StringComparison.Ordinal)));
        foreach (var (reviewer, submitter, authors) in new[] { (author, author, "[]"), (master, Guid.NewGuid(), "[\"" + master.ToString("D") + "\"]") }) {
            db.ChangeTracker.Clear();
            db.TugDailyLogReviews.Add(new() { TenantId = stored.TenantId, VesselId = stored.VesselId, Id = Guid.NewGuid(), LogId = create.Id, Revision = 1, ResultRevision = 2,
                Decision = "RETURN", ContentHash = stored.ContentHash, Reconciliation = stored.Reconciliation, ActorId = reviewer, SubmittedBy = submitter, AuthorsJson = authors,
                AuthorityId = stored.AuthorityId, Duty = stored.Duty, Reason = "FICTIONAL self review attempt" });
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
            Assert.Equal(("23514", "CK_TugDailyLogReview_Kind"), (PostgresFailure.Provider(failure)?.SqlState, PostgresFailure.Provider(failure)?.ConstraintName));
        }
    }

    [Fact]
    [Trait("Requirement", "DEV-111")]
    public async Task Drydock_package_pins_work_orders_shows_stale_and_overdue_links_and_needs_an_independent_verifier()
    {
        using var host = ReadinessHost(true); using var c = Client(host);
        Guid planner = Guid.NewGuid(), superintendent = Guid.NewGuid();
        FieldActor(host.Services, planner, "drydock.read", "drydock.write", "drydock.verify", "workorder.read", "equipment.read", "maintenance.read"); await Token(c);
        var vessel = SeedVessel.Id; var wo = Guid.NewGuid();
        await using (var db = Db()) {
            db.WorkOrders.Add(new() { TenantId = SeedVessel.TenantId, VesselId = vessel, Id = wo, FieldsJson = "{\"due_date\":{\"state\":\"Known\",\"value\":\"2020-01-01\"}}",
                SourceVersion = "FICTIONAL", Hash = new string('A', 64), SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct);
        }
        DrydockContent Content(long pin, params DrydockEvidence[] evidence) => new("FICTIONAL-DD-" + wo.ToString("N")[..6], "FICTIONAL yard", new(2031, 1, 10), new(2031, 1, 30),
            new("125000.00", "EUR", "FICTIONAL vendor", null),
            [new(Guid.NewGuid(), "WORK_ORDER", "FICTIONAL hull cleaning", null, null, null, null, "HIGH", [new("WORK_ORDER", wo, pin)]),
             new(Guid.NewGuid(), "OTHER", "FICTIONAL paint touch-up", null, null, null, null, null, [])], evidence);
        var route = $"drydock-packages?vessel={vessel}"; var id = Guid.NewGuid();
        await AssertCode(await Send(c, route, new DrydockCreateCommand(id, Content(2))), HttpStatusCode.Conflict, "version_conflict");
        await AssertCode(await Send(c, route, new DrydockCreateCommand(id, Content(1) with { Items = [new(Guid.NewGuid(), "DEFECT_REPAIR", "FICTIONAL", null, null, null, null, null,
            [new("DEFECT", Guid.NewGuid(), 1)])] })), (HttpStatusCode)422, "linked_resource_unavailable");
        // QA S9 N9: an equipment link needs equipment.read even when the asset exists on this vessel.
        var asset = Guid.NewGuid();
        await using (var db = Db()) { db.Assets.Add(new() { TenantId = db.TenantId, Id = asset, VesselId = vessel, Level = "Equipment" }); await db.SaveChangesAsync(Ct); }
        DrydockContent WithEquipment() => Content(1) with { Items = [new(Guid.NewGuid(), "OTHER", "FICTIONAL winch overhaul", asset, null, null, null, null, [])] };
        // FieldActor keeps the fixture's base permissions, so equipment.read is removed from the membership explicitly.
        FieldActor(host.Services, planner, "drydock.read", "drydock.write", "drydock.verify", "workorder.read", "maintenance.read");
        var fieldSettings = host.Services.GetRequiredService<FederationSettings>();
        fieldSettings.Memberships[0] = fieldSettings.Memberships[0] with { Permissions = [.. fieldSettings.Memberships[0].Permissions.Where(p => p != "equipment.read")] };
        await Token(c);
        await AssertCode(await Send(c, route, new DrydockCreateCommand(Guid.NewGuid(), WithEquipment())), (HttpStatusCode)422, "linked_resource_unavailable");
        FieldActor(host.Services, planner, "drydock.read", "drydock.write", "drydock.verify", "workorder.read", "equipment.read", "maintenance.read"); await Token(c);
        var created = await RiskOk<DrydockView>(await Send(c, route, new DrydockCreateCommand(id, Content(1))));
        Assert.Equal(("DRAFT", DrydockPolicy.Attention, 1, 1), (created.Package.State, created.Readiness.State, created.Readiness.Unlinked, created.Readiness.Overdue));
        Assert.Contains(created.Readiness.Items.SelectMany(i => i.Links), l => l.State == "OVERDUE" && l.Reason == "WorkOrderDuePassed");

        // The work order changes after it was pinned: the link turns STALE and planning is blocked until re-pinned.
        await using (var db = Db()) {
            var row = await db.WorkOrders.SingleAsync(x => x.Id == wo, Ct);
            row.Revision = 2; row.FieldsJson = "{\"due_date\":{\"state\":\"Known\",\"value\":\"2031-02-01\"}}"; row.SavedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Ct);
        }
        var stale = await FieldGet<DrydockView>(c, $"drydock-packages/{id}?vessel={vessel}");
        Assert.Equal(DrydockPolicy.ReviewRequired, stale.Readiness.State); Assert.Contains("link_stale", stale.PlanBlockers);
        string Transition() => $"drydock-packages/{id}/transitions?vessel={vessel}";
        await AssertCode(await Send(c, Transition(), new DrydockTransitionCommand(stale.Package.Version, stale.Package.Hash, "PLAN", null)), (HttpStatusCode)422, "drydock_record_incomplete");
        var repinned = await RiskOk<DrydockView>(await Send(c, $"drydock-packages/{id}?vessel={vessel}", new DrydockUpdateCommand(stale.Package.Version, Content(2)), update: true));
        Assert.Equal(DrydockPolicy.Attention, repinned.Readiness.State); Assert.Equal(0, repinned.Readiness.Overdue);
        var planned = await RiskOk<DrydockView>(await Send(c, Transition(), new DrydockTransitionCommand(repinned.Package.Version, repinned.Package.Hash, "PLAN", null)));
        var started = await RiskOk<DrydockView>(await Send(c, Transition(), new DrydockTransitionCommand(planned.Package.Version, planned.Package.Hash, "START", null)));
        await AssertCode(await Send(c, Transition(), new DrydockTransitionCommand(started.Package.Version, started.Package.Hash, "COMPLETE", null)), (HttpStatusCode)422, "drydock_record_incomplete");
        var evidenced = await RiskOk<DrydockView>(await Send(c, $"drydock-packages/{id}?vessel={vessel}",
            new DrydockUpdateCommand(started.Package.Version, Content(2, new DrydockEvidence(Guid.NewGuid(), "FICTIONAL-CLASS-REPORT-1", null))), update: true));
        var completed = await RiskOk<DrydockView>(await Send(c, Transition(), new DrydockTransitionCommand(evidenced.Package.Version, evidenced.Package.Hash, "COMPLETE", "FICTIONAL undocking")));
        Assert.Equal(("COMPLETED", planner), (completed.Package.State, completed.Package.CompletedBy!.Value));
        var verify = new DrydockVerifyCommand(completed.Package.Version, completed.Package.Hash, "VERIFY", "FICTIONAL superintendent verification");
        await AssertCode(await Send(c, $"drydock-packages/{id}/verification?vessel={vessel}", verify), HttpStatusCode.Forbidden, "separation_of_duties");
        FieldActor(host.Services, superintendent, "drydock.read", "drydock.verify", "workorder.read", "equipment.read", "maintenance.read"); await Token(c);
        var verified = await RiskOk<DrydockView>(await Send(c, $"drydock-packages/{id}/verification?vessel={vessel}", verify));
        Assert.Equal(("VERIFIED", superintendent), (verified.Package.State, verified.Package.VerifiedBy!.Value));
        Assert.Equal(["CREATE", "EDIT", "PLAN", "START", "EDIT", "COMPLETE", "VERIFY"], verified.Timeline.Select(t => t.ReasonCode));
        FieldActor(host.Services, superintendent, "workorder.read"); await Token(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/v1/drydock-packages?vessel={vessel}", Ct)).StatusCode);

        await using var check = Db();
        var audit = await check.AuditEvents.Where(e => e.ResourceId == id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(DrydockPackageRevision) && e.ActorId == superintendent.ToString());
    }

    [Fact]
    [Trait("Requirement", "DEV-090")]
    public async Task Field_operation_fixture_is_opt_in_idempotent_fictional_and_seeds_no_execution_review_or_verification()
    {
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            await FieldOperationsFixtures.AddMissingAsync(db, seed.Vessels, Ct); await FieldOperationsFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        var tugs = seed.Vessels.Where(v => v.Kind == VesselKind.Tug).Select(v => FieldOperationsFixtures.DailyLogId(v.Id)).ToArray();
        var docks = seed.Vessels.Select(v => FieldOperationsFixtures.DrydockId(v.Id)).ToArray();
        var logs = await db.TugDailyLogs.Where(l => tugs.Contains(l.Id)).ToArrayAsync(Ct);
        var packages = await db.DrydockPackages.Where(p => docks.Contains(p.Id)).ToArrayAsync(Ct);
        Assert.Equal(tugs.Length, logs.Length); Assert.Equal(docks.Length, packages.Length);
        Assert.All(logs, l => Assert.Equal(("DRAFT", "SyntheticUnverified", (Guid?)null, 1L), (l.State, l.Provenance, l.SubmittedBy, l.Revision)));
        Assert.All(packages, p => Assert.Equal(("DRAFT", "SyntheticUnverified", (Guid?)null), (p.State, p.Provenance, p.VerifiedBy)));
        Assert.Empty(await db.TugDailyLogReviews.Where(r => tugs.Contains(r.LogId)).ToArrayAsync(Ct));
        Assert.Empty(await db.FieldOperationReceipts.Where(r => tugs.Contains(r.ResourceId) || docks.Contains(r.ResourceId)).ToArrayAsync(Ct));
        Assert.Equal(tugs.Length + docks.Length, await db.TugDailyLogRevisions.CountAsync(r => tugs.Contains(r.LogId), Ct) + await db.DrydockPackageRevisions.CountAsync(r => docks.Contains(r.PackageId), Ct));
    }
}
