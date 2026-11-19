using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-105 (T-RISK2-001) / DEV-106 (T-RISK2-002); 50.01 Rev3 §3-6 paragraphs 29, 37, 59, 64-99.
[Trait("Requirement", "DEV-105")]
[Trait("Requirement", "DEV-106")]
public sealed class RiskPolicyTests
{
    private static readonly Guid Author = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid Approver = Guid.Parse("00000000-0000-4000-8000-0000000000b2");
    private static RiskMethodContent Method() => new("TENANT-RM", "Tenant-configured method", [new("L1", "Rare"), new("L2", "Likely")],
        [new("S1", "Minor"), new("S2", "Major")], [new("L1", "S1", "R-A", 0), new("L1", "S2", "R-B", 1), new("L2", "S1", "R-B", 1), new("L2", "S2", "R-C", 2)],
        ["SMS-REF-1"], "Configured by the tenant HSEQ owner for test.");
    private static RiskHazard Hazard(RiskRating? initial = null, RiskRating? residual = null, string? owner = "Bosun") => new(Guid.NewGuid(), "Step 1", "Fall from height",
        ["Injury"], initial ?? new("L2", "S2", "Initial rationale"), [new(Guid.NewGuid(), "Harness", "EXISTING", owner, true, ["BAR-1"])], ["BAR-1"],
        residual ?? new("L1", "S2", "Residual rationale"));
    private static RiskAssessmentContent Complete(Guid? method = null) => new("Mast work", "Port maintenance", "MAINTENANCE", "Scope", method ?? Guid.NewGuid(),
        [Hazard()], true, [new(Guid.NewGuid(), "Wind above limit")], [new(Author, "PREPARER")]);

    [Fact]
    public void No_default_matrix_exists_and_missing_method_or_codes_are_not_assessed()
    {
        Assert.Equal("METHOD_UNCONFIGURED", RiskPolicy.Rate(null, new("L2", "S2", null)).State);
        Assert.Null(RiskPolicy.Rate(null, new("L2", "S2", null)).Result);
        Assert.Equal("NOT_ASSESSED", RiskPolicy.Rate(Method(), new(null, "S2", null)).State);
        Assert.Equal("NOT_ASSESSED", RiskPolicy.Rate(Method(), new("L9", "S2", null)).State);
        var rated = RiskPolicy.Rate(Method(), new("L2", "S2", null));
        Assert.Equal(("ASSESSED", "R-C", 2), (rated.State, rated.Result, rated.Rank));
    }

    [Fact]
    public void Method_matrix_requires_every_likelihood_severity_pair_exactly_once()
    {
        Assert.Null(RiskPolicy.MethodFailure(Method()));
        var missing = Method() with { Cells = Method().Cells[..3] };
        Assert.Equal("method_matrix_incomplete", RiskPolicy.MethodFailure(missing));
        var duplicate = Method() with { Cells = [.. Method().Cells[..3], Method().Cells[0]] };
        Assert.Equal("method_matrix_incomplete", RiskPolicy.MethodFailure(duplicate));
        var undefined = Method() with { Cells = [.. Method().Cells[..3], new("L9", "S2", "R-C", 2)] };
        Assert.Equal("method_matrix_incomplete", RiskPolicy.MethodFailure(undefined));
        Assert.Equal("method_matrix_incomplete", RiskPolicy.MethodFailure(Method() with { Likelihoods = [new("L1", "Rare")] }));
        Assert.Equal("invalid_request", RiskPolicy.MethodFailure(Method() with { SourceRefs = [] }));
    }

    [Fact]
    public void Incomplete_draft_is_storable_but_submission_lists_every_blocker()
    {
        var empty = new RiskAssessmentContent(null, null, null, null, null, [], null, [], []);
        Assert.True(RiskPolicy.ValidDraft(empty));
        var blockers = RiskPolicy.SubmitBlockers(empty, null, 0);
        Assert.Contains("risk_method_unconfigured", blockers); Assert.Contains("hazard_missing", blockers);
        Assert.Contains("stop_work_unknown", blockers); Assert.Contains("preparer_or_assessor_missing", blockers);
        Assert.Contains("job_reference_or_scope_missing", blockers);
        var t = RiskPolicy.Submit(RiskPolicy.Draft, blockers);
        Assert.Null(t.ToState); Assert.Equal("risk_record_incomplete", t.Failure);
    }

    [Fact]
    public void Unrated_initial_or_residual_and_missing_control_owner_block_submission()
    {
        var content = Complete() with { Hazards = [Hazard(new(null, "S2", null), new("L1", null, null), owner: null)] };
        var blockers = RiskPolicy.SubmitBlockers(content, Method(), 1);
        Assert.Contains("initial_risk_not_assessed", blockers); Assert.Contains("residual_risk_not_assessed", blockers);
        Assert.Contains("control_owner_missing", blockers);
        Assert.Empty(RiskPolicy.SubmitBlockers(Complete(), Method(), 1));
        Assert.Equal(RiskPolicy.InReview, RiskPolicy.Submit(RiskPolicy.Returned, []).ToState);
        Assert.Equal("invalid_state", RiskPolicy.Submit(RiskPolicy.Approved, []).Failure);
    }

    [Fact]
    public void Stop_work_unknown_is_never_no_and_conditions_require_true()
    {
        Assert.False(RiskPolicy.ValidDraft(Complete() with { StopWorkRequired = false }));
        Assert.Contains("stop_work_conditions_missing", RiskPolicy.SubmitBlockers(Complete() with { StopWorkConditions = [] }, Method(), 1));
        Assert.DoesNotContain("stop_work_unknown", RiskPolicy.SubmitBlockers(Complete() with { StopWorkRequired = false, StopWorkConditions = [] }, Method(), 1));
    }

    [Fact]
    public void Approval_is_a_human_decision_separate_from_authors_and_submitter()
    {
        Assert.Equal("separation_of_duties", RiskPolicy.Review(RiskPolicy.InReview, "APPROVE", Author, [Author], null, "Reviewed against SMS").Failure);
        Assert.Equal("separation_of_duties", RiskPolicy.Review(RiskPolicy.InReview, "APPROVE", Approver, [Author], Approver, "Reviewed against SMS").Failure);
        Assert.Equal("invalid_reason", RiskPolicy.Review(RiskPolicy.InReview, "APPROVE", Approver, [Author], Author, "short").Failure);
        Assert.Equal("invalid_reason", RiskPolicy.Review(RiskPolicy.InReview, "AUTO", Approver, [Author], Author, "Reviewed against SMS").Failure);
        Assert.Equal(RiskPolicy.Approved, RiskPolicy.Review(RiskPolicy.InReview, "APPROVE", Approver, [Author], Author, "Reviewed against SMS").ToState);
        Assert.Equal(RiskPolicy.Returned, RiskPolicy.Review(RiskPolicy.InReview, "RETURN", Approver, [Author], Author, "Residual rationale missing").ToState);
        Assert.Equal("invalid_state", RiskPolicy.Review(RiskPolicy.Draft, "APPROVE", Approver, [Author], Author, "Reviewed against SMS").Failure);
    }

    [Fact]
    public void Amendment_reopens_review_only_from_approved_with_reason()
    {
        Assert.Equal(RiskPolicy.Draft, RiskPolicy.Amend(RiskPolicy.Approved, "Weather limits changed by master").ToState);
        Assert.Equal("invalid_reason", RiskPolicy.Amend(RiskPolicy.Approved, null).Failure);
        Assert.Equal("invalid_state", RiskPolicy.Amend(RiskPolicy.InReview, "Weather limits changed by master").Failure);
    }

    [Fact]
    public void Acknowledgement_is_per_person_on_the_approved_revision_and_dispute_is_not_closed_by_others()
    {
        var crew = Guid.NewGuid();
        var content = Complete() with { Participants = [new(Author, "PREPARER"), new(crew, "PARTICIPANT")] };
        Assert.Equal("invalid_state", RiskPolicy.AcknowledgementFailure(RiskPolicy.InReview, null, 3, content, crew, "ACK", null));
        Assert.Equal("invalid_state", RiskPolicy.AcknowledgementFailure(RiskPolicy.Approved, 4, 3, content, crew, "ACK", null));
        Assert.Equal("acknowledgment_role_required", RiskPolicy.AcknowledgementFailure(RiskPolicy.Approved, 4, 4, content, Approver, "ACK", null));
        Assert.Equal("invalid_reason", RiskPolicy.AcknowledgementFailure(RiskPolicy.Approved, 4, 4, content, crew, "DISPUTE", null));
        Assert.Null(RiskPolicy.AcknowledgementFailure(RiskPolicy.Approved, 4, 4, content, crew, "ACK", null));
        Assert.Equal("PENDING", RiskPolicy.AcknowledgementState(RiskPolicy.Approved, content, [(crew, "ACK")]));
        Assert.Equal("COMPLETE", RiskPolicy.AcknowledgementState(RiskPolicy.Approved, content, [(crew, "ACK"), (Author, "ACK")]));
        Assert.Equal("DISPUTED", RiskPolicy.AcknowledgementState(RiskPolicy.Approved, content, [(crew, "DISPUTE"), (Author, "ACK")]));
        Assert.Equal("NOT_APPLICABLE", RiskPolicy.AcknowledgementState(RiskPolicy.Draft, content, []));
    }

    [Fact]
    public void Template_publication_requires_separate_HSEQ_publisher_and_completeness()
    {
        var template = new RiskTemplateContent("Hot work", "HOT_WORK", ["SHIP"], ["FIRE-WATCH"],
            [new(Guid.NewGuid(), null, "Ignition", ["Fire"], [new(Guid.NewGuid(), "Fire watch", "EXISTING", "Bosun", true, [])], [])], ["SMS-7.2"], "HSEQ", null);
        Assert.True(RiskPolicy.ValidTemplateDraft(template));
        Assert.Empty(RiskPolicy.TemplateBlockers(template, 1));
        Assert.Contains("change_reason_missing", RiskPolicy.TemplateBlockers(template, 2));
        Assert.Contains("approval_owner_missing", RiskPolicy.TemplateBlockers(template with { ApprovalOwner = null }, 1));
        Assert.Equal("separation_of_duties", RiskPolicy.TemplateReview(RiskPolicy.InReview, "PUBLISH", Author, [Author], null, "Fleet standard reviewed").Failure);
        Assert.Equal(RiskPolicy.Published, RiskPolicy.TemplateReview(RiskPolicy.InReview, "PUBLISH", Approver, [Author], Author, "Fleet standard reviewed").ToState);
        Assert.Equal("invalid_state", RiskPolicy.TemplateReview(RiskPolicy.Draft, "PUBLISH", Approver, [Author], Author, "Fleet standard reviewed").Failure);
        Assert.False(RiskPolicy.ValidTemplateDraft(template with { VesselKinds = ["BARGE"] }));
        Assert.False(RiskPolicy.ValidTemplateDraft(template with { WorkType = "DIVING" }));
    }

    [Fact]
    public void Future_template_revision_never_supersedes_the_current_one_early_and_old_revisions_are_retained()
    {
        var now = DateTimeOffset.Parse("2026-11-20T00:00:00Z");
        (long, DateTimeOffset)[] published = [(1, now.AddDays(-10)), (2, now.AddDays(5))];
        Assert.Equal("CURRENT", RiskPolicy.EffectiveState(RiskPolicy.Published, now.AddDays(-10), 1, published, now));
        Assert.Equal("SCHEDULED", RiskPolicy.EffectiveState(RiskPolicy.Published, now.AddDays(5), 2, published, now));
        Assert.Equal(RiskPolicy.Superseded, RiskPolicy.EffectiveState(RiskPolicy.Published, now.AddDays(-10), 1, published, now.AddDays(6)));
        Assert.Equal(RiskPolicy.Superseded, RiskPolicy.EffectiveState(RiskPolicy.Superseded, now.AddDays(-30), 0, published, now));
        Assert.Equal("NOT_EFFECTIVE", RiskPolicy.EffectiveState(RiskPolicy.Draft, null, 3, published, now));
    }

    [Fact]
    public void Applying_a_template_copies_hazards_and_controls_without_ratings_owners_participants_or_approval()
    {
        var hazard = Guid.NewGuid(); var control = Guid.NewGuid();
        var template = new RiskTemplateContent("Bunkering", "BUNKERING", ["SHIP"], ["SOPEP"],
            [new(hazard, "Connect hose", "Spill", ["Pollution"], [new(control, "Drip trays", "EXISTING", "Chief Officer", true, ["SCUPPERS"])], ["SCUPPERS"])], ["SMS-5"], "HSEQ", null);
        var copy = RiskPolicy.FromTemplate(template, Guid.NewGuid);
        var h = Assert.Single(copy.Hazards);
        Assert.NotEqual(hazard, h.Id); Assert.NotEqual(control, h.Controls[0].Id);
        Assert.Null(h.Initial.Likelihood); Assert.Null(h.Residual.Severity); Assert.Null(h.Controls[0].Owner);
        Assert.Contains("SOPEP", h.BarrierRefs); Assert.Contains("SCUPPERS", h.BarrierRefs);
        Assert.Empty(copy.Participants); Assert.Null(copy.MethodId); Assert.Null(copy.StopWorkRequired);
        Assert.True(RiskPolicy.ValidDraft(copy));
        Assert.True(RiskPolicy.AppliesTo(template, tug: false)); Assert.False(RiskPolicy.AppliesTo(template, tug: true));
        Assert.True(RiskPolicy.AppliesTo(template with { VesselKinds = [] }, tug: true));
    }

    [Fact]
    public void Draft_bounds_reject_duplicate_ids_control_codes_and_oversized_text()
    {
        var c = Complete(); var id = c.Hazards[0].Id;
        Assert.False(RiskPolicy.ValidDraft(c with { Hazards = [c.Hazards[0], c.Hazards[0] with { Controls = [] }] }));
        Assert.False(RiskPolicy.ValidDraft(c with { Hazards = [c.Hazards[0] with { Controls = [c.Hazards[0].Controls[0] with { Kind = "VERIFIED" }] }] }));
        Assert.False(RiskPolicy.ValidDraft(c with { Title = new string('x', 201) }));
        Assert.False(RiskPolicy.ValidDraft(c with { Participants = [new(Author, "PREPARER"), new(Author, "PREPARER")] }));
        Assert.False(RiskPolicy.ValidDraft(c with { Participants = [new(Author, "APPROVER")] }));
        Assert.NotEqual(Guid.Empty, id);
    }
}
