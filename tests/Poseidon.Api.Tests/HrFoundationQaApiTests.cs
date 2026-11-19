using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5201 independent-QA follow-up (52.01 paragraph:50/51/93/557): reasons, masked identifiers, role cancellation,
// vessel scope on role end/cancel, period coverage and concurrent convergence on the actual host with owned PostgreSQL.
public sealed partial class DirectoryWorkspaceTests
{
    private const string IdentifierRead = "hr.legal_entity.identifier.read";

    private static object HrReason(uint version, string reason, DateTimeOffset? effectiveTo = null) => new { expectedVersion = version, reason, effectiveTo };

    [Fact]
    [Trait("Requirement", "PLAN-1511-5201")]
    public async Task Hr_reasons_are_stored_and_read_only_through_detail_reads_and_identifiers_are_masked_by_default()
    {
        await using var authorHost = HrHost(); await using var approverHost = HrHost(approver: true);
        await using var registryHost = HrHost(extra: [IdentifierRead]);
        using var author = Client(authorHost); using var approver = Client(approverHost); using var registry = Client(registryHost);
        await Token(author); await Token(approver); await Token(registry);
        var company = SeedVessel.CompanyId;

        // Status reason is stored with the status revision (52.01 paragraph:50).
        var entity = await HrJson(await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate((await HrGet(author, $"hr/legal-entities/{company}")).GetProperty("version").GetUInt32()), put: true));
        entity = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/status", new { expectedVersion = entity.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Synthetic activation reason" }));
        var revisions = (await HrGet(author, $"hr/legal-entities/{company}/field-revisions")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Synthetic activation reason", revisions.Single(r => r.GetProperty("field").GetString() == "LegalEntityStatus").GetProperty("reason").GetString());

        // Correcting a recorded value needs a reason; a first declaration did not. The reason is kept with the new revision.
        var correction = new { expectedVersion = entity.GetProperty("version").GetUInt32(), legalName = "Synthetic Denizcilik A.S.", countryCode = "TR",
            hrTimeZone = "UTC", baseLocale = "tr-TR", effectiveFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), effectiveTo = (DateTimeOffset?)null,
            sourceKind = "Document", sourceRef = "synthetic-registry-page", reason = (string?)null };
        var missing = await HrJson(await HrSend(author, $"hr/legal-entities/{company}", correction, put: true), HttpStatusCode.BadRequest);
        Assert.Equal("correction_reason_required", missing.GetProperty("errors").GetProperty("reason")[0].GetString());
        entity = await HrJson(await HrSend(author, $"hr/legal-entities/{company}", correction with { reason = "Synthetic registry correction" }, put: true));
        revisions = (await HrGet(author, $"hr/legal-entities/{company}/field-revisions")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Synthetic registry correction", revisions.Single(r => r.GetProperty("field").GetString() == "HrTimeZone" && r.GetProperty("value").GetString() == "UTC").GetProperty("reason").GetString());

        // Identifiers: masked for every existing HR permission; full value only with the specific identifier permission.
        var identifier = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers",
            new { type = "TaxId", issuer = "TR-SYNTHETIC-TAX", value = "SYN 445566", sourceKind = "Document", sourceRef = "synthetic-tax-page" }));
        Assert.Equal(JsonValueKind.Null, identifier.GetProperty("value").ValueKind);
        Assert.Equal("*****5566", identifier.GetProperty("maskedValue").GetString());
        var identifierId = identifier.GetProperty("id").GetGuid();
        JsonElement Find(JsonElement e) => e.GetProperty("identifiers").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == identifierId);
        Assert.Equal(JsonValueKind.Null, Find(await HrGet(author, $"hr/legal-entities/{company}")).GetProperty("value").ValueKind);
        Assert.Equal(JsonValueKind.Null, Find(await HrGet(approver, $"hr/legal-entities/{company}")).GetProperty("value").ValueKind);
        var unmasked = Find(await HrGet(registry, $"hr/legal-entities/{company}"));
        Assert.Equal("SYN445566", unmasked.GetProperty("value").GetString());
        Assert.Equal("*****5566", unmasked.GetProperty("maskedValue").GetString());
        Assert.DoesNotContain("445566", (await HrGet(author, "hr/legal-entities")).GetRawText());

        // Retirement reason: stored as a state change, shown in the detail read and never in the command result or audit.
        var retired = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers/{identifierId}/retire", HrReason(identifier.GetProperty("version").GetUInt32(), "Synthetic retire reason")));
        Assert.DoesNotContain("Synthetic retire reason", retired.GetRawText());
        var detail = await HrGet(author, $"hr/legal-entities/{company}");
        var change = detail.GetProperty("stateChanges").EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == identifierId);
        Assert.Equal(("Current", "Retired", "Synthetic retire reason", actorId.ToString()), (change.GetProperty("fromState").GetString(), change.GetProperty("toState").GetString(),
            change.GetProperty("reason").GetString(), change.GetProperty("actorId").GetString()));

        // Without the HR read permission the reasons are not reachable at all.
        await using (var plainHost = HrHost(hr: false))
        {
            using var plain = Client(plainHost); await Token(plain);
            Assert.Equal("permission_denied", HrCode(await HrGet(plain, $"hr/legal-entities/{company}", HttpStatusCode.Forbidden)));
            Assert.Equal("permission_denied", HrCode(await HrGet(plain, $"hr/legal-entities/{company}/field-revisions", HttpStatusCode.Forbidden)));
        }

        // Person verification and employment/plan decisions keep their reasons; list reads never carry them.
        var person = SeedVessel.Id;
        var personRow = await HrGet(author, $"hr/people/{person}");
        personRow = await HrJson(await HrSend(author, $"hr/people/{person}", new { expectedVersion = personRow.GetProperty("version").GetUInt32(), personType = "Seafarer", sourceKind = "Document", sourceRef = "synthetic-crew-file" }, put: true));
        Assert.Equal("correction_reason_required", (await HrJson(await HrSend(author, $"hr/people/{person}", new { expectedVersion = personRow.GetProperty("version").GetUInt32(),
            personType = "Shore", sourceKind = "Document", sourceRef = "synthetic-crew-file" }, put: true), HttpStatusCode.BadRequest)).GetProperty("errors").GetProperty("reason")[0].GetString());
        personRow = await HrJson(await HrSend(approver, $"hr/people/{person}/verify", new { expectedVersion = personRow.GetProperty("version").GetUInt32(), reason = "Synthetic verification reason" }));
        Assert.Contains((await HrGet(author, $"hr/people/{person}/field-revisions")).GetProperty("items").EnumerateArray(),
            r => r.GetProperty("field").GetString() == "MasterState" && r.GetProperty("reason").GetString() == "Synthetic verification reason");
        var employment = await HrJson(await HrSend(author, "hr/employments", new { personId = person, legalEntityId = company,
            employeeNo = "HR-QA-" + Guid.NewGuid().ToString("N")[..8], employmentType = "Employee", startAt = HrNow().AddDays(-120), endAt = (DateTimeOffset?)null }));
        var employmentId = employment.GetProperty("id").GetGuid();
        employment = await HrJson(await HrSend(approver, $"hr/employments/{employmentId}/status", new { expectedVersion = employment.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Synthetic activation of employment" }));
        Assert.Equal(JsonValueKind.Null, employment.GetProperty("stateChanges").ValueKind);
        var employmentChange = Assert.Single((await HrGet(author, $"hr/employments/{employmentId}")).GetProperty("stateChanges").EnumerateArray());
        Assert.Equal(("Draft", "Active", "Synthetic activation of employment", HrApprover.ToString()), (employmentChange.GetProperty("fromState").GetString(),
            employmentChange.GetProperty("toState").GetString(), employmentChange.GetProperty("reason").GetString(), employmentChange.GetProperty("actorId").GetString()));
        Assert.All((await HrGet(author, $"hr/employments?personId={person}")).GetProperty("items").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("stateChanges").ValueKind));

        // Active legal entity date edits must keep every active employment covered (52.01 paragraph:51).
        var current = await HrGet(author, $"hr/legal-entities/{company}");
        Assert.Equal("hr_employment_outside_legal_entity", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}", correction with {
            expectedVersion = current.GetProperty("version").GetUInt32(), effectiveFrom = HrNow().AddDays(-30), reason = "Synthetic narrowing" }, put: true), HttpStatusCode.Conflict)));
        Assert.Equal("hr_employment_outside_legal_entity", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}", correction with {
            expectedVersion = current.GetProperty("version").GetUInt32(), effectiveTo = HrNow().AddDays(365), reason = "Synthetic end" }, put: true), HttpStatusCode.Conflict)));
        // Removing the only employer relation that covers an active employment is refused too.
        var employer = current.GetProperty("roles").EnumerateArray().Single(x => x.GetProperty("role").GetString() == "Employer");
        Assert.Equal("hr_employment_outside_employer_role", HrCode(await HrJson(await HrSend(approver,
            $"hr/legal-entities/{company}/roles/{employer.GetProperty("id").GetGuid()}/cancel", HrReason(employer.GetProperty("version").GetUInt32(), "Synthetic cancel")), HttpStatusCode.Conflict)));

        // Plan approval reason is retained and only the detail read shows it.
        var plan = await HrJson(await HrSend(author, "hr/assignments", new { employmentId, kind = "Shore", vesselId = (Guid?)null, positionId = (Guid?)null,
            plannedFrom = HrNow().AddDays(10), plannedTo = HrNow().AddDays(20), plannedZoneId = "UTC" }));
        var planId = plan.GetProperty("id").GetGuid();
        await HrJson(await HrSend(approver, $"hr/assignments/{planId}/status", new { expectedVersion = plan.GetProperty("version").GetUInt32(), targetState = "Approved", reason = "Synthetic plan approval" }));
        Assert.Equal("Synthetic plan approval", Assert.Single((await HrGet(author, $"hr/assignments/{planId}")).GetProperty("stateChanges").EnumerateArray()).GetProperty("reason").GetString());
        Assert.All((await HrGet(author, $"hr/assignments?employmentId={employmentId}")).GetProperty("items").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("stateChanges").ValueKind));

        // Reasons never reach audit summaries.
        await using var db = Db();
        foreach (var reason in new[] { "Synthetic activation reason", "Synthetic registry correction", "Synthetic retire reason", "Synthetic verification reason", "Synthetic activation of employment", "Synthetic plan approval" })
            Assert.False(await db.AuditEvents.AnyAsync(x => x.ChangeSummary != null && x.ChangeSummary.Contains(reason), Ct), reason);
        Assert.False(await db.AuditEvents.AnyAsync(x => x.ChangeSummary != null && x.ChangeSummary.Contains("445566"), Ct));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5201")]
    public async Task Hr_mistaken_roles_are_cancelled_independently_and_role_end_or_cancel_respect_vessel_scope()
    {
        await using var authorHost = HrHost(); await using var approverHost = HrHost(approver: true);
        // Same tenant-wide HR grants, but tug mode only: ship vessels are outside this actor's server-resolved scope.
        await using var tugHost = HrHost(approver: true, drop: ["mode.cross"]);
        using var author = Client(authorHost); using var approver = Client(approverHost); using var tug = Client(tugHost);
        await Token(author); await Token(approver); await Token(tug);
        var company = SeedVessel.CompanyId;
        var created = await HrJson(await Send(author, "companies", new { code = "HR-CANCEL-" + Guid.NewGuid().ToString("N")[..6], name = "Synthetic Second Co" }));
        var second = created.GetProperty("id").GetGuid();
        var from = HrNow().AddDays(-30);
        object Operator(Guid vessel) => new { role = "Operator", vesselId = vessel, effectiveFrom = from, effectiveTo = (DateTimeOffset?)null, sourceKind = "UserDeclared", sourceRef = (string?)null };

        var mistaken = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles", Operator(SeedVessel.Id)));
        var roleId = mistaken.GetProperty("id").GetGuid();
        Assert.Equal("Recorded", mistaken.GetProperty("state").GetString());
        Assert.Equal("hr_role_overlap", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{second}/roles", Operator(SeedVessel.Id)), HttpStatusCode.Conflict)));
        // Same separation of duties as other HR decisions: the recorder cannot cancel; a reason is required.
        Assert.Equal("separation_of_duties", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles/{roleId}/cancel",
            HrReason(mistaken.GetProperty("version").GetUInt32(), "Self cancel")), HttpStatusCode.Forbidden)));
        Assert.Equal("validation_failed", HrCode(await HrJson(await HrSend(approver, $"hr/legal-entities/{company}/roles/{roleId}/cancel",
            new { expectedVersion = mistaken.GetProperty("version").GetUInt32() }), HttpStatusCode.BadRequest)));
        var cancelled = await HrJson(await HrSend(approver, $"hr/legal-entities/{company}/roles/{roleId}/cancel",
            HrReason(mistaken.GetProperty("version").GetUInt32(), "Recorded against the wrong company")));
        Assert.Equal("Cancelled", cancelled.GetProperty("state").GetString());
        Assert.Equal("invalid_transition", HrCode(await HrJson(await HrSend(approver, $"hr/legal-entities/{company}/roles/{roleId}/cancel",
            HrReason(cancelled.GetProperty("version").GetUInt32(), "Again")), HttpStatusCode.Conflict)));
        Assert.Equal("invalid_transition", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles/{roleId}/end",
            HrReason(cancelled.GetProperty("version").GetUInt32(), "End", HrNow())), HttpStatusCode.Conflict)));
        // The retained cancelled row no longer blocks the correct operator (application rule and exclusion constraint).
        await HrJson(await HrSend(author, $"hr/legal-entities/{second}/roles", Operator(SeedVessel.Id)));
        var detail = await HrGet(author, $"hr/legal-entities/{company}");
        Assert.Equal("Cancelled", detail.GetProperty("roles").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == roleId).GetProperty("state").GetString());
        var change = detail.GetProperty("stateChanges").EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == roleId);
        Assert.Equal(("Recorded", "Cancelled", "Recorded against the wrong company"), (change.GetProperty("fromState").GetString(), change.GetProperty("toState").GetString(), change.GetProperty("reason").GetString()));

        // End and cancel apply the same vessel-scope check as add: an out-of-scope vessel relation does not exist for the actor.
        var ship = Guid.NewGuid();
        await using (var db = Db())
        {
            db.Vessels.Add(new Vessel { TenantId = seed.Tenants[0].Id, Id = ship, CompanyId = company, Code = "HR-SHIP-" + ship.ToString("N")[..6], Name = "Synthetic ship", Kind = VesselKind.Ship });
            await db.SaveChangesAsync(Ct);
        }
        var shipRole = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles", Operator(ship)));
        var shipRoleId = shipRole.GetProperty("id").GetGuid();
        Assert.Equal("resource_not_found", HrCode(await HrJson(await HrSend(tug, $"hr/legal-entities/{company}/roles/{shipRoleId}/end",
            HrReason(shipRole.GetProperty("version").GetUInt32(), "Out of scope end", HrNow())), HttpStatusCode.NotFound)));
        Assert.Equal("resource_not_found", HrCode(await HrJson(await HrSend(tug, $"hr/legal-entities/{company}/roles/{shipRoleId}/cancel",
            HrReason(shipRole.GetProperty("version").GetUInt32(), "Out of scope cancel")), HttpStatusCode.NotFound)));
        Assert.DoesNotContain((await HrGet(tug, $"hr/legal-entities/{company}")).GetProperty("roles").EnumerateArray(), x => x.GetProperty("id").GetGuid() == shipRoleId);
        await using (var db = Db())
        {
            var row = await db.LegalEntityRoles.AsNoTracking().SingleAsync(x => x.Id == shipRoleId, Ct);
            Assert.Equal(("Recorded", (DateTimeOffset?)null), (row.State, row.EffectiveTo));
            Assert.False(await db.HrStateChanges.AnyAsync(x => x.SubjectId == shipRoleId, Ct));
        }
        // An in-scope actor can still end it, with the reason retained.
        var ended = await HrJson(await HrSend(approver, $"hr/legal-entities/{company}/roles/{shipRoleId}/end", HrReason(shipRole.GetProperty("version").GetUInt32(), "Synthetic sale", HrNow())));
        Assert.Equal("Recorded", ended.GetProperty("state").GetString());
        Assert.NotEqual(JsonValueKind.Null, ended.GetProperty("effectiveTo").ValueKind);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5201")]
    public async Task Hr_concurrent_identical_actual_events_converge_and_concurrent_overlapping_approvals_leave_one_winner()
    {
        await using var authorHost = HrHost(); await using var approverHost = HrHost(approver: true);
        using var author = Client(authorHost); using var approver = Client(approverHost);
        await Token(author); await Token(approver);
        var company = SeedVessel.CompanyId;
        var person = SeedVessel.Id;
        var entity = await HrJson(await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate((await HrGet(author, $"hr/legal-entities/{company}")).GetProperty("version").GetUInt32()), put: true));
        await HrJson(await HrSend(author, $"hr/legal-entities/{company}/status", new { expectedVersion = entity.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Synthetic activation" }));
        var personRow = await HrGet(author, $"hr/people/{person}");
        personRow = await HrJson(await HrSend(author, $"hr/people/{person}", new { expectedVersion = personRow.GetProperty("version").GetUInt32(), personType = "Shore", sourceKind = "Document", sourceRef = "synthetic-file" }, put: true));
        await HrJson(await HrSend(approver, $"hr/people/{person}/verify", new { expectedVersion = personRow.GetProperty("version").GetUInt32(), reason = "Synthetic verification" }));
        var employment = await HrJson(await HrSend(author, "hr/employments", new { personId = person, legalEntityId = company,
            employeeNo = "HR-CC-" + Guid.NewGuid().ToString("N")[..8], employmentType = "Employee", startAt = HrNow().AddDays(-60), endAt = (DateTimeOffset?)null }));
        var employmentId = employment.GetProperty("id").GetGuid();
        await HrJson(await HrSend(approver, $"hr/employments/{employmentId}/status", new { expectedVersion = employment.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Synthetic activation" }));

        // Concurrent approvals of overlapping plans for the same person: exactly one wins, the other is a 409 with no business effect.
        async Task<JsonElement> Plan(int from, int to) => await HrJson(await HrSend(author, "hr/assignments", new { employmentId, kind = "Shore", vesselId = (Guid?)null,
            positionId = (Guid?)null, plannedFrom = HrNow().AddDays(from), plannedTo = HrNow().AddDays(to), plannedZoneId = "UTC" }));
        var plans = new[] { await Plan(100, 130), await Plan(110, 140) };
        var approvals = await Task.WhenAll(plans.Select(p => HrSend(approver, $"hr/assignments/{p.GetProperty("id").GetGuid()}/status",
            new { expectedVersion = p.GetProperty("version").GetUInt32(), targetState = "Approved", reason = "Concurrent synthetic approval" })));
        Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.OK);
        var loser = Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains(HrCode(await HrJson(loser, HttpStatusCode.Conflict)), new[] { "hr_assignment_overlap", "version_conflict" });
        await using (var db = Db())
        {
            var ids = plans.Select(p => p.GetProperty("id").GetGuid()).ToArray();
            Assert.Equal(1, await db.HrAssignments.CountAsync(x => ids.Contains(x.Id) && x.State == "Approved", Ct));
            Assert.Equal(1, await db.HrStateChanges.CountAsync(x => ids.Contains(x.SubjectId), Ct));
        }

        // Concurrent identical source events under different idempotency keys converge on one fact.
        var planId = plans[0].GetProperty("id").GetGuid();
        var sourceEventId = "evt-" + Guid.NewGuid().ToString("N");
        var body = new { kind = "CheckIn", occurredAt = HrNow().AddMinutes(-5), originZoneId = "UTC", origin = "SupervisorReported", sourceSystem = "synthetic-logbook", sourceEventId };
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => HrSend(author, $"hr/assignments/{planId}/actual-events", body)));
        var results = await Task.WhenAll(responses.Select(r => HrJson(r)));
        var eventIds = results.Select(r => Assert.Single(r.GetProperty("actual").GetProperty("events").EnumerateArray()).GetProperty("id").GetGuid()).Distinct().ToArray();
        Assert.Single(eventIds);
        // Surrounding whitespace in the source key is the same key, not a new fact; different content under that key is a conflict.
        await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events", body with { sourceSystem = "  synthetic-logbook ", sourceEventId = " " + sourceEventId + " " }));
        Assert.Equal("hr_event_conflict", HrCode(await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events",
            body with { sourceSystem = " synthetic-logbook", occurredAt = body.occurredAt.AddMinutes(-1) }), HttpStatusCode.Conflict)));
        await using var check = Db();
        Assert.Equal(1, await check.HrActualEvents.CountAsync(x => x.SourceEventId == sourceEventId, Ct));
        Assert.Equal(eventIds[0], (await check.HrActualEvents.SingleAsync(x => x.SourceEventId == sourceEventId, Ct)).Id);
    }
}
