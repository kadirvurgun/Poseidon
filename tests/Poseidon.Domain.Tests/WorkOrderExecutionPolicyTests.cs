using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S8 WP-B DEV-054 / T-S8-003 (18.01 row55; 13.04 p91) and DEV-110 / T-SUP2-001 (row111; 11.01 rows 1869/1879).
public sealed class WorkOrderExecutionPolicyTests
{
    private static readonly Guid Releaser=Guid.NewGuid(), Engineer=Guid.NewGuid(), Verifier=Guid.NewGuid(), Closer=Guid.NewGuid();
    private static readonly DateTimeOffset T0=new(2026,11,20,8,0,0,TimeSpan.Zero);
    private static readonly CertificateFilePin Photo=new(Guid.NewGuid(),1,new string('A',64));
    private static WorkOrderEvidence Full=>new([new("MEGGER_INSULATION","12.5","MOhm",T0)],[new(Guid.NewGuid(),1,"2")],
        [new("PHOTO",Photo,"Before"),new("DOCUMENT",new(Guid.NewGuid(),1,new string('b',64)))],"FICTIONAL work done","none");

    private static WorkOrderExecutionContent Ok((WorkOrderExecutionContent? Content,WorkOrderExecutionError? Error) r) {
        Assert.Null(r.Error);return r.Content!;
    }
    private static WorkOrderExecutionContent InProgress(string[]? required) {
        var c=WorkOrderExecutionPolicy.Released(Releaser,T0,new string('0',64),required);
        return Ok(WorkOrderExecutionPolicy.Apply(c,"START",Engineer,T0));
    }
    private static WorkOrderExecutionContent Submitted(string[]? required,WorkOrderEvidence? evidence=null) {
        var c=Ok(WorkOrderExecutionPolicy.RecordEvidence(InProgress(required),evidence??Full,Engineer));
        return Ok(WorkOrderExecutionPolicy.Apply(c,"SUBMIT",Engineer,T0,expectedEvidenceHash:WorkOrderExecutionPolicy.EvidenceHash(c.Evidence)));
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public void Execution_follows_released_in_progress_evidence_submitted_then_independent_human_verification_and_closure() {
        var released=WorkOrderExecutionPolicy.Released(Releaser,T0,new string('0',64),["READING","PHOTO"]);
        Assert.Equal("RELEASED",released.State);
        Assert.Equal("invalid_transition",WorkOrderExecutionPolicy.RecordEvidence(released,Full,Engineer).Error!.Code);
        Assert.Equal("invalid_transition",WorkOrderExecutionPolicy.Apply(released,"SUBMIT",Engineer,T0,expectedEvidenceHash:new string('0',64)).Error!.Code);
        var submitted=Submitted(["READING","PHOTO"]);
        Assert.Equal("EVIDENCE_SUBMITTED",submitted.State);Assert.Equal("PRESENT_PENDING_HUMAN_VERIFICATION",WorkOrderExecutionPolicy.Gate(submitted).State);
        var hash=WorkOrderExecutionPolicy.EvidenceHash(submitted.Evidence);
        // The executor cannot verify or close their own work.
        Assert.Equal("separation_of_duties",WorkOrderExecutionPolicy.Apply(submitted,"VERIFY",Engineer,T0,expectedEvidenceHash:hash).Error!.Code);
        Assert.Equal("evidence_changed",WorkOrderExecutionPolicy.Apply(submitted,"VERIFY",Verifier,T0,expectedEvidenceHash:new string('F',64)).Error!.Code);
        var verified=Ok(WorkOrderExecutionPolicy.Apply(submitted,"VERIFY",Verifier,T0,expectedEvidenceHash:hash));
        Assert.Equal("VERIFIED",verified.State);Assert.Equal("HUMAN_VERIFIED",WorkOrderExecutionPolicy.Gate(verified).State);
        Assert.Equal("separation_of_duties",WorkOrderExecutionPolicy.Apply(verified,"CLOSE",Engineer,T0,expectedEvidenceHash:hash).Error!.Code);
        var closed=Ok(WorkOrderExecutionPolicy.Apply(verified,"CLOSE",Closer,T0,expectedEvidenceHash:hash));
        Assert.Equal("CLOSED",closed.State);Assert.Contains("RETURN_TO_SERVICE_NOT_RECORDED",WorkOrderExecutionPolicy.Blockers(closed));
        Assert.Equal("invalid_transition",WorkOrderExecutionPolicy.Apply(closed,"CLOSE",Closer,T0,expectedEvidenceHash:hash).Error!.Code);
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public void Missing_required_evidence_blocks_submission_and_an_unconfigured_policy_blocks_verification_and_closure() {
        var partial=Ok(WorkOrderExecutionPolicy.RecordEvidence(InProgress(["READING","DOCUMENT"]),Full with{Files=[]},Engineer));
        Assert.Equal(["DOCUMENT"],WorkOrderExecutionPolicy.Gate(partial).Missing);
        Assert.Equal("required_evidence_missing",WorkOrderExecutionPolicy.Apply(partial,"SUBMIT",Engineer,T0,expectedEvidenceHash:WorkOrderExecutionPolicy.EvidenceHash(partial.Evidence)).Error!.Code);
        // No evidence at all is never a submission.
        var empty=InProgress(null);
        Assert.Equal("required_evidence_missing",WorkOrderExecutionPolicy.Apply(empty,"SUBMIT",Engineer,T0,expectedEvidenceHash:WorkOrderExecutionPolicy.EvidenceHash(empty.Evidence)).Error!.Code);
        // Unconfigured policy: submission is possible but the gate stays REVIEW_REQUIRED and verification is refused.
        var unconfigured=Submitted(null);var hash=WorkOrderExecutionPolicy.EvidenceHash(unconfigured.Evidence);
        Assert.Equal("REVIEW_REQUIRED",WorkOrderExecutionPolicy.Gate(unconfigured).State);
        Assert.Contains("REQUIRED_EVIDENCE_POLICY_UNCONFIGURED",WorkOrderExecutionPolicy.Blockers(unconfigured));
        Assert.Equal("required_evidence_unconfigured",WorkOrderExecutionPolicy.Apply(unconfigured,"VERIFY",Verifier,T0,expectedEvidenceHash:hash).Error!.Code);
        // The executor cannot declare the requirement; an independent releaser can, then verification proceeds.
        Assert.Equal("separation_of_duties",WorkOrderExecutionPolicy.Apply(unconfigured,"REQUIREMENTS",Engineer,T0,required:["READING"]).Error!.Code);
        var configured=Ok(WorkOrderExecutionPolicy.Apply(unconfigured,"REQUIREMENTS",Releaser,T0,required:["PHOTO","READING"]));
        Assert.Equal(["READING","PHOTO"],configured.RequiredEvidence!);
        Assert.Equal("VERIFIED",Ok(WorkOrderExecutionPolicy.Apply(configured,"VERIFY",Verifier,T0,expectedEvidenceHash:hash)).State);
        Assert.False(WorkOrderExecutionPolicy.ValidRequired([]));Assert.False(WorkOrderExecutionPolicy.ValidRequired(["READING","READING"]));Assert.False(WorkOrderExecutionPolicy.ValidRequired(["NONE"]));
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public void Return_requires_a_reason_reopens_work_and_rejects_unknown_codes() {
        var submitted=Submitted(["READING"]);var hash=WorkOrderExecutionPolicy.EvidenceHash(submitted.Evidence);
        Assert.Equal("invalid_reason",WorkOrderExecutionPolicy.Apply(submitted,"RETURN",Verifier,T0,expectedEvidenceHash:hash).Error!.Code);
        Assert.Equal("invalid_reason",WorkOrderExecutionPolicy.Apply(submitted,"RETURN",Verifier,T0,expectedEvidenceHash:hash,reason:"LOOKS_FINE").Error!.Code);
        var returned=Ok(WorkOrderExecutionPolicy.Apply(submitted,"RETURN",Verifier,T0,expectedEvidenceHash:hash,reason:"EVIDENCE_INSUFFICIENT"));
        Assert.Equal("IN_PROGRESS",returned.State);Assert.Null(returned.Submission);Assert.Contains("RETURNED_BY_VERIFIER",WorkOrderExecutionPolicy.Blockers(returned));
        // A verifier who now edits the evidence becomes an executor and loses verification authority.
        var edited=Ok(WorkOrderExecutionPolicy.RecordEvidence(returned,submitted.Evidence with{Findings="FICTIONAL extra"},Verifier));
        var resubmitted=Ok(WorkOrderExecutionPolicy.Apply(edited,"SUBMIT",Engineer,T0,expectedEvidenceHash:WorkOrderExecutionPolicy.EvidenceHash(edited.Evidence)));
        Assert.Equal("separation_of_duties",WorkOrderExecutionPolicy.Apply(resubmitted,"VERIFY",Verifier,T0,expectedEvidenceHash:WorkOrderExecutionPolicy.EvidenceHash(edited.Evidence)).Error!.Code);
    }

    [Fact]
    [Trait("Requirement","DEV-054")]
    public void Evidence_validation_keeps_declared_lexical_values_and_rejects_malformed_or_duplicate_inputs() {
        Assert.True(WorkOrderExecutionPolicy.ValidEvidence(Full));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Readings=[new("X","1,5")]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Readings=[new("X","1"),new("X","2")]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Spares=[new(null,null,"1")]}));
        Assert.True(WorkOrderExecutionPolicy.ValidEvidence(Full with{Spares=[new(null,null,"1","FICTIONAL gasket")]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Spares=[new(Guid.NewGuid(),1,"0")]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Spares=[new(Guid.NewGuid(),1,"01")]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Files=[new("VIDEO",Photo)]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Files=[new("PHOTO",Photo),new("DOCUMENT",Photo)]}));
        Assert.False(WorkOrderExecutionPolicy.ValidEvidence(Full with{Readings=[new("X","1",TakenAt:new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.FromHours(3)))]}));
        // Checksum casing does not change the bound evidence hash.
        var lower=Full;var upper=lower with{Files=lower.Files.Select(f=>f with{File=f.File with{Checksum=f.File.Checksum.ToUpperInvariant()}}).ToArray()};
        Assert.Equal(WorkOrderExecutionPolicy.EvidenceHash(lower),WorkOrderExecutionPolicy.EvidenceHash(upper));
    }

    private static CriticalSpareContent Spare(string? min,string? max,string? count,string criticality="CRITICAL_DECLARED")=>
        new(new("FICTIONAL-1","FICTIONAL spare","pcs",criticality,null,min,max),count==null?null:new(count,T0));

    [Fact]
    [Trait("Requirement","DEV-110")]
    public void Stock_projection_never_reports_unknown_count_or_minimum_as_healthy_and_detects_low_stock() {
        Assert.Equal("STOCK_UNKNOWN",CriticalSparePolicy.Project(Spare("2","10",null),[]).State);
        Assert.Equal("MINIMUM_UNCONFIGURED",CriticalSparePolicy.Project(Spare(null,"10","5"),[]).State);
        Assert.Equal("CONSUMPTION_UNRESOLVED",CriticalSparePolicy.Project(Spare("2","10","5"),null).State);
        var above=CriticalSparePolicy.Project(Spare("2","10","5"),[new(Guid.NewGuid(),"2",T0.AddHours(-1))]);
        Assert.Equal("ABOVE_MINIMUM",above.State);Assert.Equal("5",above.Projected);Assert.Null(above.ProposedQuantity);
        var low=CriticalSparePolicy.Project(Spare("2","10","5"),[new(Guid.NewGuid(),"3",T0.AddHours(1))]);
        Assert.Equal("LOW_STOCK",low.State);Assert.Equal("2",low.Projected);Assert.Equal("8",low.ProposedQuantity);
        var noMax=CriticalSparePolicy.Project(Spare("2",null,"1"),[]);
        Assert.Equal("LOW_STOCK",noMax.State);Assert.Null(noMax.ProposedQuantity);Assert.Contains("PROPOSED_QUANTITY_REVIEW_REQUIRED",noMax.Reasons);
        Assert.Contains("SPARE_CRITICALITY_UNKNOWN",CriticalSparePolicy.Project(Spare("2","10","5","UNKNOWN"),[]).Reasons);
        Assert.False(CriticalSparePolicy.ValidDefinition(Spare("5","2","1").Definition));
        Assert.False(CriticalSparePolicy.ValidDefinition(Spare("-1",null,null).Definition));
        Assert.False(CriticalSparePolicy.ValidDefinition(Spare("1",null,null,"VERY_HIGH").Definition));
        Assert.False(CriticalSparePolicy.ValidCount(new("1",T0.AddDays(1)),T0));
    }

    [Fact]
    [Trait("Requirement","DEV-110")]
    public void Requisition_is_proposed_only_for_low_stock_and_approved_by_a_different_human_as_an_export_reference() {
        var spare=Guid.NewGuid();var proposer=Guid.NewGuid();var approver=Guid.NewGuid();
        var healthy=Spare("2","10","5");
        Assert.Equal("stock_not_low",CriticalSparePolicy.Propose(spare,1,healthy,CriticalSparePolicy.Project(healthy,[]),"UNKNOWN",0,proposer,T0).Error);
        var low=Spare("2",null,"1");var (proposal,error)=CriticalSparePolicy.Propose(spare,1,low,CriticalSparePolicy.Project(low,[]),"Critical",1,proposer,T0);
        Assert.Null(error);Assert.Equal("PROPOSED",proposal!.State);Assert.Null(proposal.ProposedQuantity);Assert.Equal("Critical",proposal.EquipmentCriticality);
        Assert.Equal("separation_of_duties",CriticalSparePolicy.Decide(proposal,"APPROVE",proposer,T0,"3",null).Error);
        Assert.Equal("quantity_required",CriticalSparePolicy.Decide(proposal,"APPROVE",approver,T0,null,null).Error);
        Assert.Equal("invalid_reason",CriticalSparePolicy.Decide(proposal,"REJECT",approver,T0,null,"NOPE").Error);
        var approved=CriticalSparePolicy.Decide(proposal,"APPROVE",approver,T0,"3",null).Content!;
        Assert.Equal("APPROVED_FOR_ERP_EXPORT",approved.State);Assert.Equal("3",approved.ApprovedQuantity);Assert.Equal(approver,approved.DecidedBy);
        Assert.Equal("invalid_transition",CriticalSparePolicy.Decide(approved,"REJECT",Guid.NewGuid(),T0,null,"OTHER_SEE_ERP").Error);
        Assert.Equal("REJECTED",CriticalSparePolicy.Decide(proposal,"REJECT",approver,T0,null,"STOCK_COUNT_DISPUTED").Content!.State);
        Assert.DoesNotContain(CriticalSparePolicy.RequisitionStates,s=>s.Contains("ORDER") || s.Contains("PURCHASE"));
    }
}
