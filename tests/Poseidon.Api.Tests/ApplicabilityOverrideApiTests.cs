using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S5 WP-A: DEV-040 (18.01 Backlog row 41 + 60.03 §10), DEV-041 (row 42) and DEV-042 (row 43).
// Real host, antiforgery and owned PostgreSQL; synthetic memberships only. Authored; execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private string OverridesRoute(Guid? vessel = null) => $"vessels/{vessel ?? SeedVessel.Id}/applicability-overrides";
    private void OverrideActor(IServiceProvider services, Guid? actor = null, string kind = "Human", params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor ?? m.ActorId, ActorKind = kind,
            Permissions = [.. m.Permissions.Where(p => !p.StartsWith("applicability.override.", StringComparison.Ordinal)), .. extra] };
    }
    private static ApplicabilityOverrideCommand OverrideCommand(ComplianceApplicabilityDto review, Guid rule, long revision,
        string action = "REQUIRE_ADDITIONAL_CONTROL", string reason = "ADDITIONAL_PRECAUTION", Guid[]? evidence = null) {
        var now = DateTimeOffset.UtcNow;
        return new(review.EvaluationId!.Value, review.InputHash!, review.RuleSetHash!, rule, revision, action, reason, "COMPLIANCE_REVIEW",
            "FICTIONAL precaution rationale; no legal interpretation", evidence ?? [], now, now.AddDays(30), now.AddDays(7));
    }
    private async Task<ApplicabilityOverrideDto> OverrideAccepted(HttpResponseMessage response)
    {
        using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ApplicabilityOverrideDto>(Ct))!; }
    }

    [Fact]
    [Trait("Requirement", "DEV-040")]
    public async Task Override_proposal_is_audited_maker_checker_and_cannot_activate_without_configured_authority()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        // AdmittedByCommands replaces Memberships[0] (new synthetic regulatory human, own permission set), so the
        // override author permission and actor id are established only after the qualified evaluation exists.
        var (rule, _, evaluation) = await AdmittedByCommands(c, host.Services);
        OverrideActor(host.Services, extra: "applicability.override.author"); await Token(c);
        var author = host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;
        var review = (await c.GetFromJsonAsync<ComplianceApplicabilityDto>(CompliancePath("applicability-review"), Ct))!;
        Assert.True(review.Current); Assert.False(review.ManualOverrideAvailable);
        var empty = (await c.GetFromJsonAsync<ApplicabilityOverrideListDto>("/api/v1/" + OverridesRoute(), Ct))!;
        Assert.Equal("UNCONFIGURED", empty.Authority); Assert.Contains("OVERRIDE_AUTHORITY_POLICY_UNAVAILABLE", empty.AuthorityBlockers);
        Assert.True(empty.CanPropose); Assert.False(empty.CanDecide); Assert.Empty(empty.Items);
        var original = await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct);

        var key = Guid.NewGuid().ToString(); var command = OverrideCommand(review, rule.Current.Id, rule.Current.Revision);
        var proposed = await OverrideAccepted(await Send(c, OverridesRoute(), command, key));
        Assert.Equal(("IN_REVIEW", "NOT_ACTIVE", 1L), (proposed.State, proposed.CurrentValidity, proposed.Sequence));
        Assert.Equal(review.Results.Single(r => r.RuleId == rule.Current.Id).Prediction.Result, proposed.BaseResult);
        Assert.Equal(author, proposed.AuthorId); Assert.Null(proposed.ApproverId);
        // Exact retry replays; the same key with another body is rejected.
        Assert.Equal(proposed.Id, (await OverrideAccepted(await Send(c, OverridesRoute(), command, key))).Id);
        await AssertCode(await Send(c, OverridesRoute(), command with { ReasonCode = "SOURCE_CONFLICT_PRECAUTION" }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        // Uncontrolled reason and an interpretation without a reference record are refused.
        await AssertCode(await Send(c, OverridesRoute(), command with { ReasonCode = "VERIFIED_LEGAL_INTERPRETATION" }), HttpStatusCode.UnprocessableEntity, "override_reason_not_allowed");
        await AssertCode(await Send(c, OverridesRoute(), OverrideCommand(review, rule.Current.Id, rule.Current.Revision, "ACCEPT_INTERPRETATION", "VERIFIED_LEGAL_INTERPRETATION")),
            HttpStatusCode.UnprocessableEntity, "override_evidence_required");

        async Task<HttpResponseMessage> Decide(ApplicabilityOverrideDto current, string decision, string? k = null) =>
            await Send(c, $"{OverridesRoute()}/{current.Id}/decisions", new ApplicabilityOverrideDecisionCommand(current.Version, current.Sequence, decision, "FICTIONAL decision reason " + decision), k);
        // The author cannot approve without the approve permission, and not even with it (maker-checker).
        await AssertCode(await Decide(proposed, "APPROVE"), HttpStatusCode.Forbidden, "permission_denied");
        OverrideActor(host.Services, extra: ["applicability.override.author", "applicability.override.approve"]); await Token(c);
        await AssertCode(await Decide(proposed, "APPROVE"), HttpStatusCode.Conflict, "separation_of_duties");
        // A separate human approver has no current authority grant: activation is closed (REG-034 -> 422).
        OverrideActor(host.Services, Guid.NewGuid(), extra: "applicability.override.approve"); await Token(c);
        await AssertCode(await Decide(proposed, "APPROVE"), HttpStatusCode.UnprocessableEntity, "override_authority_unavailable");
        // An AI/service identity can neither propose nor decide.
        OverrideActor(host.Services, Guid.NewGuid(), "AI", "applicability.override.approve", "applicability.override.author"); await Token(c);
        await AssertCode(await Decide(proposed, "APPROVE"), HttpStatusCode.Forbidden, "human_actor_required");
        await AssertCode(await Send(c, OverridesRoute(), command), HttpStatusCode.Forbidden, "human_actor_required");

        OverrideActor(host.Services, author, extra: "applicability.override.author"); await Token(c);
        var listed = Assert.Single((await c.GetFromJsonAsync<ApplicabilityOverrideListDto>("/api/v1/" + OverridesRoute(), Ct))!.Items);
        Assert.Equal("IN_REVIEW", listed.State); Assert.NotNull(listed.Rationale);
        await AssertCode(await Send(c, $"{OverridesRoute()}/{listed.Id}/decisions", new ApplicabilityOverrideDecisionCommand(listed.Version + 1, listed.Sequence, "WITHDRAW", "FICTIONAL stale withdraw")),
            HttpStatusCode.Conflict, "version_conflict");
        var withdrawn = await OverrideAccepted(await Decide(listed, "WITHDRAW"));
        Assert.Equal(("REVOKED", "REVOKED", 2L), (withdrawn.State, withdrawn.CurrentValidity, withdrawn.Sequence));
        Assert.Equal(new[] { "WITHDRAW", "PROPOSE" }, withdrawn.Events.Select(e => e.EventCode).ToArray());
        await AssertCode(await Decide(withdrawn, "WITHDRAW"), HttpStatusCode.Conflict, "invalid_transition");
        // The override never rewrites the recorded evaluation result.
        Assert.Equal(original, await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct));

        await using var db = Db();
        var row = await db.ApplicabilityOverrides.SingleAsync(o => o.Id == proposed.Id, Ct);
        Assert.Equal(("REVOKED", (Guid?)null, (Guid?)null), (row.State, row.ApproverId, row.AuthorityGrantId));
        Assert.Equal(2, await db.ApplicabilityOverrideEvents.CountAsync(e => e.OverrideId == proposed.Id, Ct));
        Assert.Single(await db.ComplianceCommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        var audits = await db.AuditEvents.Where(a => a.ResourceId == proposed.Id && (a.EntityType == nameof(ApplicabilityOverride) || a.EntityType == nameof(ApplicabilityOverrideEvent))).ToArrayAsync(Ct);
        Assert.Equal(4, audits.Length);
        Assert.All(audits, a => { Assert.Equal(SeedVessel.Id, a.VesselId); Assert.DoesNotContain("FICTIONAL", a.ChangeSummary); });
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "ApplicabilityOverrideCommand" && e.Result == "override_authority_unavailable");
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ApplicabilityOverrideEvents\" SET \"ToState\" = 'ACTIVE' WHERE \"OverrideId\" = '{proposed.Id}'"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ApplicabilityOverrides\" SET \"State\" = 'ACTIVE' WHERE \"Id\" = '{proposed.Id}'"));
    }

    [Fact]
    [Trait("Requirement", "DEV-040")]
    public async Task Override_requires_review_after_reevaluation_inputs_change_and_foreign_tenant_cannot_see_it()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services);
        OverrideActor(host.Services, extra: "applicability.override.author"); await Token(c);
        var review = (await c.GetFromJsonAsync<ComplianceApplicabilityDto>(CompliancePath("applicability-review"), Ct))!;
        var proposed = await OverrideAccepted(await Send(c, OverridesRoute(), OverrideCommand(review, rule.Current.Id, rule.Current.Revision)));
        Assert.Equal("NOT_ACTIVE", proposed.CurrentValidity);
        var profile = await Profile(c); await SaveProfile(c, profile, profile.Content with { Flag = new("Unknown") });
        var stale = Assert.Single((await c.GetFromJsonAsync<ApplicabilityOverrideListDto>("/api/v1/" + OverridesRoute(), Ct))!.Items);
        Assert.Equal(("IN_REVIEW", "REQUIRES_REVIEW"), (stale.State, stale.CurrentValidity));
        // A proposal pinned to a superseded/stale evaluation snapshot is refused.
        await AssertCode(await Send(c, OverridesRoute(), OverrideCommand(review, rule.Current.Id, rule.Current.Revision, "ACCEPT_INTERPRETATION", "VERIFIED_LEGAL_INTERPRETATION", [Guid.NewGuid()])),
            HttpStatusCode.BadRequest, "override_evidence_unavailable");
        await AssertCode(await Send(c, OverridesRoute(), OverrideCommand(review, rule.Current.Id, rule.Current.Revision) with { InputHash = new string('A', 64) }),
            HttpStatusCode.Conflict, "evaluation_stale");
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + OverridesRoute(foreign.Id), Ct)).StatusCode);
        // Cross-tenant writes are not-found like the read above (no existence oracle): a tenant-wide author
        // passes Author()/Human() but the tenant Scope() excludes the foreign vessel (ApplicabilityOverrideEndpoints.cs:141).
        await AssertCode(await Send(c, OverridesRoute(foreign.Id), OverrideCommand(review, rule.Current.Id, rule.Current.Revision)), HttpStatusCode.NotFound, "resource_not_found");
        await using var other = Db(foreign.TenantId); Assert.False(await other.ApplicabilityOverrides.AnyAsync(o => o.Id == proposed.Id, Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-041")]
    public async Task Control_candidates_are_draft_only_for_unlinked_applicable_requirements_and_critical_coverage_stays_unavailable()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services);
        var before = (await c.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.Equal(1, before.Plan.UncontrolledRequirementCount);
        var route = $"vessels/{SeedVessel.Id}/compliance-control-plan/candidates";
        var body = new ControlCandidateCommand(before.Applicability.EvaluationId!.Value, before.Applicability.InputHash!, before.Applicability.RuleSetHash!);
        await AssertCode(await Send(c, route, body with { RuleSetHash = new string('A', 64) }), HttpStatusCode.Conflict, "evaluation_stale");
        var key = Guid.NewGuid().ToString();
        ControlCandidateOutcome outcome;
        using (var response = await Send(c, route, body, key)) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); outcome = (await response.Content.ReadFromJsonAsync<ControlCandidateOutcome>(Ct))!; }
        var created = Assert.Single(outcome.Created);
        Assert.Equal((rule.Current.Id, rule.Current.Revision, "DRAFT", "DRAFT"), (created.RuleId, created.RuleRevision, created.Lifecycle, created.ControlState));
        Assert.Contains("OWNER_MISSING", created.Blockers); Assert.Contains("FREQUENCY_UNSPECIFIED", created.Blockers);
        Assert.Contains("EVIDENCE_POLICY_MISSING", created.Blockers); Assert.Contains("VERIFICATION_POLICY_MISSING", created.Blockers);
        Assert.Contains("CRITICALITY_UNKNOWN", created.Blockers); Assert.Contains("CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE", outcome.Blockers);
        using (var replay = await Send(c, route, body, key)) Assert.Equal(created.ControlId, (await replay.Content.ReadFromJsonAsync<ControlCandidateOutcome>(Ct))!.Created.Single().ControlId);
        await AssertCode(await Send(c, route, body), HttpStatusCode.Conflict, "no_control_candidates");
        // The candidate is linked but neither reviewed, published, completed, verified nor effective.
        var after = (await c.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.Equal(created.ControlId, Assert.Single(Assert.Single(after.Plan.Requirements).ControlIds));
        Assert.Equal(0, after.Plan.UncontrolledRequirementCount); Assert.Null(after.Plan.OrphanCriticalCount);
        Assert.Contains("CRITICAL_REQUIREMENT_COVERAGE_UNAVAILABLE", after.Plan.Blockers); Assert.Equal("REVIEW_REQUIRED", after.State);
        var control = Assert.Single(after.Plan.Controls, x => x.Id == created.ControlId);
        Assert.Equal(("DRAFT", "UNVERIFIED", "UNKNOWN"), (control.Lifecycle, control.Verification, control.Effectiveness));
        Assert.Equal("DRAFT", (await ReadLifecycle(c, created.ControlId)).State);
        await AssertCode(await LifecycleCommand(c, created.ControlId, "SUBMIT_FOR_REVIEW"), HttpStatusCode.Conflict, "control_record_incomplete");
        // DEV-042: source freshness drill-down stays UNKNOWN without a published freshness policy.
        var source = Assert.Single(after.Sources);
        Assert.Equal((rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, "UNKNOWN"), (source.SourceId, source.SourceRevision, source.Freshness));
        Assert.Contains("SOURCE_FRESHNESS_POLICY_UNAVAILABLE", source.Blockers); Assert.Equal($"/app/sources/{source.SourceId:D}", source.Href);
        Assert.Contains(rule.Current.Id, source.RuleIds); Assert.Contains("SOURCE_FRESHNESS_POLICY_UNAVAILABLE", after.Blockers);
        Assert.Contains(after.CertificateCoverage, new[] { "AUTHORIZED_PAGE_ONLY", "UNAVAILABLE" }); // never a complete inventory
        await using var db = Db();
        var revision = await db.ControlEvidenceRevisions.SingleAsync(r => r.Id == created.ControlId, Ct);
        Assert.Equal(("REQUIREMENT_CANDIDATE", "DRAFT", (bool?)null), (revision.ReasonCode, revision.Lifecycle, revision.CriticalDeclaration));
        Assert.Empty(await db.ControlStateEvents.Where(e => e.ControlId == created.ControlId).ToArrayAsync(Ct));
        Assert.Contains(await db.AuditEvents.Where(a => a.ResourceId == created.ControlId).Select(a => a.ChangeSummary).ToArrayAsync(Ct),
            s => s!.Contains("ComplianceControlCandidates", StringComparison.Ordinal));
        // A reader without control.write cannot generate.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [.. settings.Memberships[0].Permissions.Where(p => p != "control.write")] };
        await AssertCode(await Send(c, route, body), HttpStatusCode.Forbidden, "permission_denied");
    }
}