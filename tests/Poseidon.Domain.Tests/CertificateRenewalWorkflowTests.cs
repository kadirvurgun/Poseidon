using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-044 / T-S6-002 (18.01 row45; 60.04 VC-012 p180-200, SoD p196/p252-253).
[Trait("Requirement", "DEV-044")]
public sealed class CertificateRenewalWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private static readonly Guid Owner = Guid.NewGuid(), Replacement = Guid.NewGuid();
    private static readonly CertificateFilePin Pin = new(Guid.NewGuid(), 1, new string('a', 64));
    private static readonly CertificateRenewalContent Ready = new(OwnerRoleId: Owner, DueDate: Today.AddDays(30), RequiredSurvey: true, RequiredDocuments: false, RequiredActions: true);
    private static readonly CertificateRenewalDecision None = new();
    private static RenewalReplacementFacts Facts(DateOnly? issue = null, bool current = true, CertificateFilePin? document = null) =>
        new(Replacement, 2, issue ?? Today.AddDays(-1), (issue ?? Today).AddYears(5), document ?? Pin, current);
    private static string? Check(string action, string state, CertificateRenewalContent? content = null, CertificateRenewalDecision? d = null, bool owner = true,
        DateOnly? expiry = null, RenewalReplacementFacts? replacement = null) =>
        CertificateRenewalWorkflow.Check(action, state, content ?? Ready, d ?? None, owner, expiry ?? Today.AddDays(40), Today, replacement);

    [Fact]
    public void State_machine_follows_source_order_and_terminal_states_close()
    {
        Assert.Equal(new[] { "CANCEL", "OPEN" }, CertificateRenewalWorkflow.Allowed("DRAFT"));
        Assert.Equal(new[] { "AWAIT_AUTHORITY", "RECORD_ISSUED_CANDIDATE", "REJECT" }, CertificateRenewalWorkflow.Allowed("APPLICATION_RECORDED"));
        Assert.Equal(new[] { "COMPLETE" }, CertificateRenewalWorkflow.Allowed("VERIFIED"));
        foreach (var terminal in new[] { "COMPLETED", "CANCELLED", "REJECTED" })
        {
            Assert.Empty(CertificateRenewalWorkflow.Allowed(terminal)); Assert.True(CertificateRenewalWorkflow.Terminal(terminal));
            Assert.Equal("terminal_review", Check("OPEN", terminal));
        }
        Assert.Equal("invalid_transition", Check("COMPLETE", "ISSUED_UNVERIFIED", replacement: Facts()));
        Assert.Equal("invalid_transition", Check("AUTO_VERIFY", "ISSUED_UNVERIFIED"));
        Assert.True(CertificateRenewalWorkflow.Editable("PREPARING")); Assert.False(CertificateRenewalWorkflow.Editable("READY_TO_APPLY"));
    }

    [Fact]
    public void Open_requires_eligible_owner_due_date_and_known_original_expiry()
    {
        Assert.Null(Check("OPEN", "DRAFT"));
        Assert.Equal("renewal_owner_required", Check("OPEN", "DRAFT", owner: false));
        Assert.Equal("renewal_owner_required", Check("OPEN", "DRAFT", Ready with { OwnerRoleId = null }));
        Assert.Equal("renewal_due_required", Check("OPEN", "DRAFT", Ready with { DueDate = null }));
        Assert.Equal("renewal_expiry_unknown", CertificateRenewalWorkflow.Check("OPEN", "DRAFT", Ready, None, true, null, Today, null));
    }

    [Fact]
    public void Validate_never_treats_unknown_requirements_or_unknown_completion_as_done()
    {
        var complete = new CertificateRenewalDecision(SurveyStatus: "COMPLETE", DocumentsStatus: "NOT_REQUIRED", ActionsStatus: "COMPLETE");
        Assert.Null(Check("VALIDATE", "PREPARING", d: complete));
        Assert.Equal("renewal_requirements_unknown", Check("VALIDATE", "PREPARING", Ready with { RequiredActions = null }, complete));
        Assert.Equal("renewal_requirements_incomplete", Check("VALIDATE", "PREPARING", d: complete with { SurveyStatus = "UNKNOWN" }));
        // A required survey cannot be declared not required.
        Assert.Equal("renewal_requirements_incomplete", Check("VALIDATE", "PREPARING", d: complete with { SurveyStatus = "NOT_REQUIRED" }));
        Assert.Equal("renewal_requirements_incomplete", Check("VALIDATE", "PREPARING", d: None));
    }

    [Fact]
    public void Application_needs_past_date_and_authority_reference()
    {
        Assert.Null(Check("RECORD_APPLICATION", "READY_TO_APPLY", d: new(ApplicationDate: Today, AuthorityReference: "FICTIONAL-APP-1")));
        Assert.Equal("renewal_application_incomplete", Check("RECORD_APPLICATION", "READY_TO_APPLY", d: new(ApplicationDate: Today.AddDays(1), AuthorityReference: "FICTIONAL")));
        Assert.Equal("renewal_application_incomplete", Check("RECORD_APPLICATION", "READY_TO_APPLY", d: new(ApplicationDate: Today, AuthorityReference: " ")));
    }

    [Fact]
    public void Issued_candidate_needs_current_dated_replacement_with_its_own_evidence_pin()
    {
        var decision = new CertificateRenewalDecision(NewCertificateId: Replacement, NewCertificateRevision: 2, NewCertificateVersion: 7, NewDocument: Pin);
        Assert.Null(Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision, replacement: Facts()));
        Assert.Equal("renewal_replacement_required", Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision, replacement: null));
        Assert.Equal("linked_version_conflict", Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision, replacement: Facts(current: false)));
        Assert.Equal("renewal_replacement_dates_unknown", Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision,
            replacement: new(Replacement, 2, null, Today.AddYears(5), Pin, true)));
        Assert.Equal("renewal_evidence_required", Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision with { NewDocument = null }, replacement: Facts()));
        Assert.Equal("renewal_evidence_required", Check("RECORD_ISSUED_CANDIDATE", "AWAITING_AUTHORITY", d: decision,
            replacement: Facts(document: new(Guid.NewGuid(), 1, new string('b', 64)))));
        var applied = CertificateRenewalWorkflow.Apply("RECORD_ISSUED_CANDIDATE", Ready, decision, Facts());
        Assert.Equal((Replacement, 2L, Pin, Today.AddDays(-1)), (applied.NewCertificateId!.Value, applied.NewCertificateRevision!.Value, applied.NewDocument!, applied.NewIssueDate!.Value));
    }

    [Fact]
    public void Verify_requires_every_check_to_match_and_unknown_is_not_a_match()
    {
        var pinned = Ready with { NewCertificateId = Replacement, NewCertificateRevision = 2, NewCertificateVersion = 7, NewDocument = Pin };
        var match = new CertificateRenewalDecision(IssuerMatch: "MATCH", NumberMatch: "MATCH", SubjectMatch: "MATCH", DateMatch: "MATCH", ScopeMatch: "MATCH");
        Assert.Null(Check("VERIFY", "ISSUED_UNVERIFIED", pinned, match, replacement: Facts()));
        Assert.Equal("renewal_verification_not_matched", Check("VERIFY", "ISSUED_UNVERIFIED", pinned, match with { DateMatch = "UNKNOWN" }, replacement: Facts()));
        Assert.Equal("renewal_verification_not_matched", Check("VERIFY", "ISSUED_UNVERIFIED", pinned, match with { IssuerMatch = null }, replacement: Facts()));
        Assert.Equal("linked_version_conflict", Check("VERIFY", "ISSUED_UNVERIFIED", pinned, match, replacement: Facts(current: false)));
        Assert.Equal("renewal_replacement_required", Check("VERIFY", "ISSUED_UNVERIFIED", Ready, match, replacement: Facts()));
    }

    [Fact]
    public void Complete_waits_for_the_effective_boundary_of_the_replacement()
    {
        var pinned = Ready with { NewCertificateId = Replacement, NewCertificateRevision = 2, NewCertificateVersion = 7, NewDocument = Pin };
        Assert.Null(Check("COMPLETE", "VERIFIED", pinned, replacement: Facts(Today)));
        Assert.Equal("renewal_replacement_not_effective", Check("COMPLETE", "VERIFIED", pinned, replacement: Facts(Today.AddDays(1))));
    }

    [Fact]
    public void Cancel_after_application_needs_a_receipt_and_reject_needs_authority_evidence()
    {
        Assert.Null(Check("CANCEL", "READY_TO_APPLY"));
        Assert.Equal("renewal_cancellation_receipt_required", Check("CANCEL", "APPLICATION_RECORDED"));
        Assert.Equal("renewal_evidence_required", Check("REJECT", "AWAITING_AUTHORITY"));
        Assert.Null(Check("REJECT", "AWAITING_AUTHORITY", d: new(Evidence: Pin)));
    }

    [Fact]
    public void Separation_of_duties_keeps_recorder_author_verifier_and_completer_distinct()
    {
        Guid recorder = Guid.NewGuid(), author = Guid.NewGuid(), verifier = Guid.NewGuid(), other = Guid.NewGuid();
        Assert.False(CertificateRenewalWorkflow.Separated("VERIFY", recorder, recorder, author, null));
        Assert.False(CertificateRenewalWorkflow.Separated("VERIFY", author, recorder, author, null));
        Assert.True(CertificateRenewalWorkflow.Separated("VERIFY", verifier, recorder, author, null));
        Assert.False(CertificateRenewalWorkflow.Separated("COMPLETE", verifier, recorder, author, verifier));
        Assert.False(CertificateRenewalWorkflow.Separated("COMPLETE", recorder, recorder, author, verifier));
        Assert.False(CertificateRenewalWorkflow.Separated("COMPLETE", other, recorder, author, null));
        Assert.True(CertificateRenewalWorkflow.Separated("COMPLETE", other, recorder, author, verifier));
        Assert.True(CertificateRenewalWorkflow.HumanOnly("VERIFY")); Assert.True(CertificateRenewalWorkflow.HumanOnly("COMPLETE"));
        Assert.Equal("certificate.renewal.verify", CertificateRenewalWorkflow.Permission("VERIFY"));
        Assert.Equal("certificate.renewal.complete", CertificateRenewalWorkflow.Permission("COMPLETE"));
    }

    [Fact]
    public void Decision_facts_belong_only_to_their_owning_transition()
    {
        Assert.True(CertificateRenewalWorkflow.DecisionShape("OPEN", None));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("OPEN", new(IssuerMatch: "MATCH")));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("VERIFY", new(IssuerMatch: "PROBABLY")));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("VALIDATE", new(SurveyStatus: "DONE")));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("RECORD_ISSUED_CANDIDATE", new(NewCertificateId: Replacement)));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("COMPLETE", new(Evidence: Pin)));
        Assert.False(CertificateRenewalWorkflow.DecisionShape("RECORD_APPLICATION", new(ApplicationDate: Today, AuthorityReference: "bad\u0001")));
        Assert.True(CertificateRenewalWorkflow.DecisionShape("REJECT", new(Evidence: Pin)));
    }

    [Fact]
    public void Blockers_keep_source_qualification_open_and_only_drop_human_verification_after_verify()
    {
        Assert.Contains("HUMAN_VERIFICATION_REQUIRED", CertificateRenewalWorkflow.Blockers("ISSUED_UNVERIFIED", Ready, true));
        var verified = CertificateRenewalWorkflow.Blockers("VERIFIED", Ready, true);
        Assert.DoesNotContain("HUMAN_VERIFICATION_REQUIRED", verified); Assert.Contains("SOURCE_QUALIFICATION_REQUIRED", verified);
        Assert.Contains("COMPLETION_PREREQUISITES_REQUIRED", verified);
        Assert.Contains("SOURCE_QUALIFICATION_REQUIRED", CertificateRenewalWorkflow.Blockers("COMPLETED", Ready, true));
        Assert.Contains("RENEWAL_CLOSED_WITHOUT_REPLACEMENT", CertificateRenewalWorkflow.Blockers("CANCELLED", Ready, true));
        Assert.Contains("RENEWAL_REQUIREMENTS_UNKNOWN", CertificateRenewalWorkflow.Blockers("DRAFT", new(), false));
    }
}
