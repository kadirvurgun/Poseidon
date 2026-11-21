using Poseidon.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// A deliberately closed projection. Never serialize an entity, command, identity claim or ContentJson.
internal sealed record AuditMetadata(Guid? VesselId, Guid? ResourceId, string? Kind,
    long? BeforeRevision, long? AfterRevision, string Summary)
{
    internal static AuditMetadata From(EntityEntry entry, Guid? parentVessel, AuditCommandContext? command, bool requireResolvedScope)
    {
        var metadata = Project(entry, parentVessel);
        var scopeKind = metadata.VesselId is { } vessel && vessel != Guid.Empty ? "Vessel" : entry.Entity switch
        {
            Person or CrewIdentityRevision or CrewPersonalProfile or CrewPersonalProfileRevision => "TenantPerson",
            PersonFieldRevision or Employment or HrAssignment or HrActualEvent => "TenantPerson",
            LegalEntityFieldRevision or LegalEntityIdentifier or LegalEntityRole => "Tenant",
            HrStateChange s when s.SubjectKind is "Employment" or "PlannedAssignment" => "TenantPerson",
            HrStateChange => "Tenant",
            CommandReceipt r when r.Action is "HrPersonUpdate" or "HrPersonVerify" or "HrEmploymentCreate" or "HrEmploymentStatus" or "HrAssignmentCreate" or "HrAssignmentStatus" or "HrActualEventRecord" => "TenantPerson",
            CommandReceipt r when r.Action is "HrLegalEntityUpdate" or "HrLegalEntityStatus" or "HrIdentifierAdd" or "HrIdentifierRetire" or "HrRoleAdd" or "HrRoleEnd" or "HrRoleCancel" => "Tenant",
            CommandReceipt r when r.Action is "CrewPersonSave" or "CrewIdentityConvert" or "CrewProfileSave" or "CrewPhotoSave" => "TenantPerson",
            UserAccount or AccountMembership or AccountMembershipVesselGrant or MembershipDelegation or DelegationVesselScope or TenantAccessRecovery or MembershipCommandReceipt => "Tenant",
            MembershipGrantRevision or MembershipGrantVessel => "Tenant",
            Tenant or Company or RegisterDraft or RegisterRevision or RuleDraft or RuleRevision or FleetCatalogDraft or FleetCatalogDraftRevision or
                RegulatoryQualificationHead or RegulatoryQualificationEvent or RegulatoryQualificationReceipt or
                ParameterRoot or ParameterRevision or ParameterDecision or ParameterCommandReceipt => "Tenant",
            CommandReceipt r when r.Action is "CompanyCreate" or "CompanyUpdate" or "SourceDraftSave" or "RequirementDraftSave" => "Tenant",
            RiskTemplate or RiskTemplateRevision or RiskTemplateDecision or RiskTemplateSuggestion or RiskMethod => "Tenant",
            RiskCommandReceipt { VesselId: null } => "Tenant",
            // S10 WP-A DEV-113: an import mapping is tenant configuration (no vessel); its receipt carries no vessel either.
            AssuranceImportMapping or AssuranceReceipt { VesselId: null } => "Tenant",
            PilotMetricBatch or PilotMetricTenantSalt or PilotMetricReceipt => "Tenant",
            ApplicabilityReceipt r when r.Action is "author" or "review" => "Tenant",
            UatThreshold or UatRun or UatStepResult or UatFinding or PilotSupportCheck or PilotGateDecision or PilotReceipt => "Tenant",
            PilotProgram or PilotChecklistLink or PilotFeedback or PilotIssue or PilotMeasurement or PilotWeekClose or PilotProgramDecision or PilotProgramReceipt => "Tenant",
            _ => "Unresolved"
        };
        if (requireResolvedScope && scopeKind == "Unresolved")
            throw new InvalidOperationException("Current command audit scope is unavailable.");
        var fields = JsonSerializer.Deserialize<List<JsonElement>>(metadata.Summary) ?? [];
        if (entry.Entity is CommandReceipt or ApplicabilityReceipt or HumanReviewReceipt or ControlEvidenceReceipt or RegulatoryQualificationReceipt or ControlledDocumentReceipt or ComplianceCommandReceipt)
            fields.Add(JsonSerializer.SerializeToElement(Field("Binding", null, "CommittedResource")));
        fields.Add(JsonSerializer.SerializeToElement(Field("ScopeKind", null, scopeKind)));
        if (command != null)
        {
            if (!command.IsValid) throw new InvalidOperationException("Invalid audit command context.");
            fields.Add(JsonSerializer.SerializeToElement(Field("CommandName", null, command.EndpointName)));
            fields.Add(JsonSerializer.SerializeToElement(Field("TechnicalReasonCode", null, command.ReasonCode)));
        }
        var summary = JsonSerializer.Serialize(fields);
        if (fields.Count > 20 || summary.Length > 2048)
            throw new InvalidOperationException("Audit summary exceeds its controlled bounds.");
        return metadata with { Summary = summary };
    }

    private static object Field(string field, string? before, string? after, string evidence = "Allowlisted") =>
        new { field, before, after, evidence };
    private static string Fields(params object[] fields) => JsonSerializer.Serialize(fields);
    private static string Number(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static AuditMetadata Project(EntityEntry entry, Guid? parentVessel)
    {
        if (RiskProjection(entry) is { } risk) return risk;
        if (PmsProjection(entry) is { } pms) return pms;
        if (TechnicalRecordProjection(entry) is { } technical) return technical;
        if (S9WpcProjection(entry) is { } s9wpc) return s9wpc;
        if (PilotMetricsProjection(entry) is { } pilot) return pilot;
        if (TowageProjection(entry) is { } towage) return towage;
        if (SafetyEventProjection(entry) is { } safety) return safety;
        if (AssuranceProjection(entry) is { } assurance) return assurance;
        if (FieldOperationsProjection(entry) is { } field) return field;
        if (TugOperationsOfflineProjection(entry) is { } tugOffline) return tugOffline;
        if (ImportReconciliationProjection(entry) is { } reconciliation) return reconciliation;
        if (PilotGateProjection(entry) is { } pilotGate) return pilotGate;
        if (PilotProgramProjection(entry) is { } pilotProgram) return pilotProgram;
        if(entry.Entity is Defect defect)
            return new(defect.VesselId,defect.Id,"Defect",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,defect.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(defect.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is DefectRevision defectRevision)
            return new(defectRevision.VesselId,defectRevision.DefectId,"DefectRevision",null,defectRevision.Revision,Fields(Field("Revision",null,Number(defectRevision.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is DefectReceipt defectReceipt)
            return new(defectReceipt.VesselId,defectReceipt.DefectId,"DefectReceipt",null,defectReceipt.Revision,Fields(Field("Binding",null,"CommittedResource")));
        if(entry.Entity is WorkOrder work_order)
            return new(work_order.VesselId,work_order.Id,"WorkOrder",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,work_order.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(work_order.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is WorkOrderRevision work_orderRevision)
            return new(work_orderRevision.VesselId,work_orderRevision.WorkOrderId,"WorkOrderRevision",null,work_orderRevision.Revision,Fields(Field("Revision",null,Number(work_orderRevision.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is WorkOrderReceipt work_orderReceipt)
            return new(work_orderReceipt.VesselId,work_orderReceipt.WorkOrderId,"WorkOrderReceipt",null,work_orderReceipt.Revision,Fields(Field("Binding",null,"CommittedResource")));
        if(entry.Entity is Downtime downtime)
            return new(downtime.VesselId,downtime.Id,"Downtime",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,downtime.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(downtime.Revision)),Field("Binding",null,"OwnerReference"),
                    Field("ClosureState",LifecycleState(entry,nameof(Downtime.ClosureState),true),LifecycleState(entry,nameof(Downtime.ClosureState),false)),Field("ClaimEvidence",null,null,"ValuesWithheld")));
        if(entry.Entity is DowntimeRevision downtimeRevision)
            return new(downtimeRevision.VesselId,downtimeRevision.DowntimeId,"DowntimeRevision",null,downtimeRevision.Revision,Fields(Field("Revision",null,Number(downtimeRevision.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is DowntimeReceipt downtimeReceipt)
            return new(downtimeReceipt.VesselId,downtimeReceipt.DowntimeId,"DowntimeReceipt",null,downtimeReceipt.Revision,Fields(Field("Binding",null,"CommittedResource")));
        if(entry.Entity is ShipOperation shipOperation)
            return new(shipOperation.VesselId,shipOperation.Id,"ShipOperation",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,shipOperation.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(shipOperation.Revision)),Field("Binding",null,"OwnerReference"),
                    Field("SofState",LifecycleState(entry,nameof(ShipOperation.SofState),true),LifecycleState(entry,nameof(ShipOperation.SofState),false))));
        if(entry.Entity is ShipOperationRevision shipRevision)
            return new(shipRevision.VesselId,shipRevision.RecordId,"ShipOperationRevision",null,shipRevision.Revision,Fields(Field("Revision",null,Number(shipRevision.Revision)),Field("Binding",null,"OwnerReference")));
        // DEV-101/102 lifecycle: closed action/state/duty codes only; detail JSON withheld.
        // The event row is append-only (Added): it has no prior revision of its own. The
        // record's from/to revision stays in the summary field, never in BeforeRevision.
        if(entry.Entity is ShipOpsLifecycleEvent opsLifecycle)
            return new(opsLifecycle.VesselId,opsLifecycle.RecordId,"ShipOpsLifecycleEvent",null,opsLifecycle.ToRevision,Fields(
                Field("Action",null,Closed(opsLifecycle.Action,["FINALIZE",..Poseidon.Domain.ShipOperationsLifecycle.DowntimeActions])),
                Field("State",opsLifecycle.FromState==null?null:Closed(opsLifecycle.FromState,LifecycleStates),opsLifecycle.ToState==null?null:Closed(opsLifecycle.ToState,LifecycleStates)),
                Field("Duty",null,opsLifecycle.Duty==null?null:Closed(opsLifecycle.Duty,Poseidon.Domain.ShipOperationsLifecycle.DowntimeDuties)),
                Field("Detail",null,null,"ValuesWithheld"),Field("Revision",Number(opsLifecycle.FromRevision),Number(opsLifecycle.ToRevision))));
        if(entry.Entity is ShipOperationReceipt shipReceipt)
            return new(shipReceipt.VesselId,shipReceipt.RecordId,"ShipOperationReceipt",null,null,Fields(Field("Binding",null,"CommittedResource")));
        if(entry.Entity is VesselCertificate certificate)
            return new(certificate.VesselId,certificate.Id,"VesselCertificate",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,certificate.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(certificate.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is VesselCertificateRevision certificateRevision)
            return new(certificateRevision.VesselId,certificateRevision.CertificateId,"VesselCertificateRevision",null,certificateRevision.Revision,
                Fields(Field("Revision",null,Number(certificateRevision.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is CertificateRenewal renewal)
            return new(renewal.VesselId,renewal.Id,"CertificateRenewal",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,renewal.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(renewal.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is CertificateRenewalRevision renewalRevision) {
            // DEV-044: closed workflow state/transition codes only; DecisionJson (authority reference,
            // match decisions, evidence pins) is never copied into the audit summary.
            if(!CertificateRenewalWorkflow.Known(renewalRevision.State) || renewalRevision.Transition!=null && !CertificateRenewalWorkflow.Transitions.ContainsKey(renewalRevision.Transition))
                throw new InvalidOperationException("Invalid renewal workflow metadata.");
            var renewalFields=new List<object>{Field("Revision",null,Number(renewalRevision.Revision)),Field("RenewalState",null,renewalRevision.State),Field("Binding",null,"OwnerReference")};
            if(renewalRevision.Transition!=null)renewalFields.Add(Field("Transition",null,renewalRevision.Transition));
            if(renewalRevision.DecisionJson!=null)renewalFields.Add(Field("Decision",null,null,"ValuesWithheld"));
            return new(renewalRevision.VesselId,renewalRevision.RenewalId,"CertificateRenewalRevision",null,renewalRevision.Revision,Fields([..renewalFields]));
        }
        if(entry.Entity is CertificateSupersession supersession) {
            if(supersession.CertificateRevision<=0 || supersession.ReplacementRevision<=0 || supersession.RenewalRevision<=0 || supersession.CertificateId==supersession.ReplacementCertificateId)
                throw new InvalidOperationException("Invalid certificate supersession metadata.");
            return new(supersession.VesselId,supersession.CertificateId,"CertificateSupersession",null,supersession.CertificateRevision,
                Fields(Field("CertificateRevision",null,Number(supersession.CertificateRevision)),Field("ReplacementRevision",null,Number(supersession.ReplacementRevision)),
                    Field("RenewalRevision",null,Number(supersession.RenewalRevision)),Field("SupersessionState",null,"SUPERSEDED_BY_COMPLETED_RENEWAL"),Field("Binding",null,"OwnerReference")));
        }
        if(entry.Entity is CertificateAssertionHead assertionHead)
            return new(assertionHead.VesselId,assertionHead.CertificateId,"CertificateAssertionHead",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,assertionHead.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(assertionHead.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is CertificateAssertionEvent assertion)
            return new(assertion.VesselId,assertion.CertificateId,"CertificateAssertion",null,assertion.Revision,
                Fields(Field("Revision",null,Number(assertion.Revision)),Field("Binding",null,"RecordedUnassessedAssertion")));
        // DEV-054/DEV-057/DEV-110: closed state/transition codes only. Readings, spares, quantities,
        // captions, file pins, part numbers and actor identities are never copied into audit.
        if(entry.Entity is WorkOrderExecution execution) {
            string? executionBefore=entry.State==EntityState.Added?null:Closed((string)entry.Property("State").OriginalValue!,WorkOrderExecutionPolicy.States);
            return new(execution.VesselId,execution.WorkOrderId,"WorkOrderExecution",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,execution.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(execution.Revision)),
                    Field("ExecutionState",executionBefore,Closed(execution.State,WorkOrderExecutionPolicy.States)),Field("Binding",null,"OwnerReference")));
        }
        if(entry.Entity is WorkOrderExecutionRevision executionRevision)
            return new(executionRevision.VesselId,executionRevision.WorkOrderId,"WorkOrderExecutionRevision",null,executionRevision.Revision,
                Fields(Field("Revision",null,Number(executionRevision.Revision)),Field("Transition",null,Closed(executionRevision.Transition,WorkOrderExecutionPolicy.Transitions)),
                    Field("ExecutionState",null,Closed(executionRevision.State,WorkOrderExecutionPolicy.States)),Field("EvidenceGate",null,Closed(executionRevision.EvidenceGate,WorkOrderExecutionPolicy.GateStates)),
                    Field("Content",null,null,"ValuesWithheld"),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is WorkOrderExecutionReceipt executionReceipt)
            return new(executionReceipt.VesselId,executionReceipt.WorkOrderId,"WorkOrderExecutionReceipt",null,executionReceipt.Revision,
                Fields(Field("Operation",null,Closed(executionReceipt.Action,"WorkOrderExecutionTransition","WorkOrderExecutionEvidence")),Field("Binding",null,"CommittedResource")));
        if(entry.Entity is WorkOrderOfflineEnrollment workOrderEnrollment)
            return new(workOrderEnrollment.VesselId,workOrderEnrollment.WorkOrderId,"WorkOrderOfflineEnrollment",null,null,
                Fields(Field("Binding",entry.State==EntityState.Added?null:"OwnerReference","OwnerReference")));
        if(entry.Entity is WorkOrderOfflineLifecycle workOrderLifecycle)
            return new(workOrderLifecycle.VesselId,workOrderLifecycle.WorkOrderId,"WorkOrderOfflineLifecycle",null,null,
                Fields(Field("Binding",null,"OwnerReference"),Field("Lifecycle",null,Closed(workOrderLifecycle.Action,"Enrolled","Renewed","KeyEnrolled","Revoked","KeyLost","CleanupAllowed"))));
        if(entry.Entity is WorkOrderOfflineOutcome workOrderOutcome)
            return new(workOrderOutcome.VesselId,workOrderOutcome.WorkOrderId,"WorkOrderOfflineOutcome",null,workOrderOutcome.Revision,
                Fields(Field("Binding",null,"OwnerReference"),Field("Outcome",null,Closed(workOrderOutcome.Result,"Committed","MaterialConflict","ServerRetained")),
                    Field("ReasonCode",null,workOrderOutcome.ParentCommandId==null?null:workOrderOutcome.Result=="ServerRetained"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED")));
        // S10 WP-C DEV-066: certificate renewal offline lineage; closed codes only, protected values withheld.
        if(entry.Entity is CertificateRenewalOfflineEnrollment renewalEnrollment)
            return new(renewalEnrollment.VesselId,renewalEnrollment.RenewalId,"CertificateRenewalOfflineEnrollment",null,null,
                Fields(Field("Binding",entry.State==EntityState.Added?null:"OwnerReference","OwnerReference")));
        if(entry.Entity is CertificateRenewalOfflineLifecycle renewalLifecycle)
            return new(renewalLifecycle.VesselId,renewalLifecycle.RenewalId,"CertificateRenewalOfflineLifecycle",null,null,
                Fields(Field("Binding",null,"OwnerReference"),Field("Lifecycle",null,Closed(renewalLifecycle.Action,CertificateRenewalOfflineStorage.LifecycleActions))));
        if(entry.Entity is CertificateRenewalOfflineOutcome renewalOutcome)
            return new(renewalOutcome.VesselId,renewalOutcome.RenewalId,"CertificateRenewalOfflineOutcome",null,renewalOutcome.Revision,
                Fields(Field("Binding",null,"OwnerReference"),Field("Outcome",null,Closed(renewalOutcome.Result,CertificateRenewalOfflineStorage.Results)),
                    Field("ReasonCode",null,renewalOutcome.ParentCommandId==null?null:renewalOutcome.Result=="ServerRetained"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED")));
        if(entry.Entity is CriticalSpare spare)
            return new(spare.VesselId,spare.Id,"CriticalSpare",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,spare.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(spare.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is CriticalSpareRevision spareRevision)
            return new(spareRevision.VesselId,spareRevision.SpareId,"CriticalSpareRevision",null,spareRevision.Revision,
                Fields(Field("Revision",null,Number(spareRevision.Revision)),Field("Transition",null,Closed(spareRevision.Transition,CriticalSparePolicy.SpareTransitions)),
                    Field("Content",null,null,"ValuesWithheld"),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is TechnicalRequisition requisition) {
            string? requisitionBefore=entry.State==EntityState.Added?null:Closed((string)entry.Property("State").OriginalValue!,CriticalSparePolicy.RequisitionStates);
            return new(requisition.VesselId,requisition.Id,"TechnicalRequisition",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,requisition.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(requisition.Revision)),
                    Field("RequisitionState",requisitionBefore,Closed(requisition.State,CriticalSparePolicy.RequisitionStates)),Field("Binding",null,"OwnerReference")));
        }
        if(entry.Entity is TechnicalRequisitionRevision requisitionRevision)
            return new(requisitionRevision.VesselId,requisitionRevision.RequisitionId,"TechnicalRequisitionRevision",null,requisitionRevision.Revision,
                Fields(Field("Revision",null,Number(requisitionRevision.Revision)),Field("Transition",null,Closed(requisitionRevision.Transition,CriticalSparePolicy.RequisitionTransitions)),
                    Field("RequisitionState",null,Closed(requisitionRevision.State,CriticalSparePolicy.RequisitionStates)),Field("Content",null,null,"ValuesWithheld"),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is TechnicalSupplyReceipt supplyReceipt)
            return new(supplyReceipt.VesselId,supplyReceipt.ResourceId,"TechnicalSupplyReceipt",null,supplyReceipt.Revision,
                Fields(Field("Operation",null,Closed(supplyReceipt.Action,"CriticalSpareCreate","CriticalSpareUpdate","TechnicalRequisitionPropose","TechnicalRequisitionDecide")),Field("Binding",null,"CommittedResource")));
        if(entry.Entity is PreSurveyChecklist preSurvey)
            return new(preSurvey.VesselId,preSurvey.Id,"PreSurveyChecklist",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,preSurvey.Revision,
                Fields(Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(preSurvey.Revision)),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is PreSurveyChecklistRevision preSurveyRevision) {
            // DEV-104: closed transition/readiness codes only. Item answers, findings, comments,
            // evidence pins, planned dates and verifier identities are never copied into audit.
            if(!PreSurveyChecklistPolicy.Transitions.Contains(preSurveyRevision.Transition) || !PreSurveyChecklistPolicy.ReadinessStates.Contains(preSurveyRevision.Readiness))
                throw new InvalidOperationException("Invalid pre-survey checklist metadata.");
            return new(preSurveyRevision.VesselId,preSurveyRevision.ChecklistId,"PreSurveyChecklistRevision",null,preSurveyRevision.Revision,
                Fields(Field("Revision",null,Number(preSurveyRevision.Revision)),Field("Transition",null,preSurveyRevision.Transition),Field("Readiness",null,preSurveyRevision.Readiness),
                    Field("Content",null,null,"ValuesWithheld"),Field("Binding",null,"OwnerReference")));
        }
        if(entry.Entity is PreSurveyReceipt preSurveyReceipt)
            return new(preSurveyReceipt.VesselId,preSurveyReceipt.ChecklistId,"PreSurveyReceipt",null,null,Fields(Field("Binding",null,"CommittedResource")));
        // S7 WP-A crew human acts and matrix: closed codes and revisions only; rationale,
        // references and certificate data are withheld (crew PII never enters audit text).
        if(entry.Entity is CrewCredentialAssertion crewAssertion)
            return new(crewAssertion.VesselId,crewAssertion.CredentialId,"CrewCredentialAssertion",null,crewAssertion.Sequence,Fields(
                Field("State",null,Closed(crewAssertion.State,CrewCompetenceStorage.AssertionStates)),Field("Duty",null,Closed(crewAssertion.Duty,CrewCompetenceStorage.CredentialDuties)),
                Field("CredentialRevision",null,Number(crewAssertion.CredentialRevision)),Field("Rationale",null,null,"ValuesWithheld"),Field("Binding",null,"RecordedHumanAssertion")));
        if(entry.Entity is CrewCompetenceRequirement crewRequirement)
            return new(crewRequirement.VesselId,crewRequirement.Id,"CrewCompetenceRequirement",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,crewRequirement.Revision,Fields(
                Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(crewRequirement.Revision)),
                Field("RequirementType",null,Closed(crewRequirement.RequirementType,Poseidon.Domain.CrewRequirementTypes.All)),
                Field("Status",entry.State==EntityState.Added?null:Closed((string)entry.Property("Status").OriginalValue!,CrewCompetenceStorage.RequirementStatuses),Closed(crewRequirement.Status,CrewCompetenceStorage.RequirementStatuses)),
                Field("Mandatory",null,crewRequirement.Mandatory?"true":"false"),Field("Reference",null,null,"ValuesWithheld"),Field("Binding",null,"OwnerReference")));
        if(entry.Entity is CrewCompetenceRecord crewRecord)
            return new(crewRecord.VesselId,crewRecord.AssignmentId,"CrewCompetenceRecord",entry.State==EntityState.Added?null:(long?)entry.Property("Revision").OriginalValue,crewRecord.Revision,Fields(
                Field("Revision",entry.State==EntityState.Added?null:Number((long)entry.Property("Revision").OriginalValue!),Number(crewRecord.Revision)),
                Field("Kind",null,Closed(crewRecord.Kind,Poseidon.Domain.CrewCompetenceKinds.All)),Field("Dates",null,null,"ValuesWithheld"),Field("Reference",null,null,"ValuesWithheld"),Field("Binding",null,"DeclaredUnverified")));
        if(entry.Entity is CrewCompetenceSignOff crewSignOff)
            return new(crewSignOff.VesselId,crewSignOff.RecordId,"CrewCompetenceSignOff",null,crewSignOff.Sequence,Fields(
                Field("Outcome",null,Closed(crewSignOff.Outcome,CrewCompetenceStorage.SignOffOutcomes)),Field("Duty",null,Closed(crewSignOff.Duty,CrewCompetenceStorage.AssessmentDuties)),
                Field("RecordRevision",null,Number(crewSignOff.RecordRevision)),Field("Rationale",null,null,"ValuesWithheld"),Field("Binding",null,"RecordedHumanSignOff")));
        if(entry.Entity is CrewSafeManningDecision manningDecision)
            return new(manningDecision.VesselId,manningDecision.Id,"CrewSafeManningDecision",null,manningDecision.Sequence,Fields(
                Field("Decision",null,Closed(manningDecision.Decision,CrewCompetenceStorage.Decisions)),Field("Duty",null,Closed(manningDecision.Duty,CrewCompetenceStorage.DecisionDuties)),
                Field("EvaluationState",null,Closed(manningDecision.EvaluationState,Poseidon.Domain.CrewEligibilityStates.All)),Field("Rationale",null,null,"ValuesWithheld"),
                Field("Acknowledgements",null,Number(manningDecision.AcknowledgementCount)),Field("Dispensations",null,Number(manningDecision.DispensationCount)),
                Field("AcknowledgementText",null,null,"ValuesWithheld"),Field("Binding",null,"RecordedHumanDecision")));
        if(entry.Entity is CertificateCommandReceipt certificateReceipt)
            return new(certificateReceipt.VesselId,certificateReceipt.ResourceId,"CertificateReceipt",null,null,Fields(Field("Binding",null,"CommittedResource")));
        if (entry.Entity is DailyOfflineEnrollment dailyEnrollment)
            return new(dailyEnrollment.VesselId,dailyEnrollment.RecordId,"DailyOfflineEnrollment",null,null,
                Fields(Field("Binding",entry.State==EntityState.Added?null:"OwnerReference","OwnerReference")));
        if (entry.Entity is DailyOfflineLifecycle dailyLifecycle)
            return new(dailyLifecycle.VesselId,dailyLifecycle.RecordId,"DailyOfflineLifecycle",null,null,
                Fields(Field("Binding",null,"OwnerReference"),Field("Lifecycle",null,Closed(dailyLifecycle.Action,"Enrolled","Renewed","KeyEnrolled","Revoked","KeyLost","CleanupAllowed"))));
        if (entry.Entity is DailyOfflineOutcome dailyOutcome)
            return new(dailyOutcome.VesselId,dailyOutcome.RecordId,"DailyOfflineOutcome",null,dailyOutcome.Revision,
                Fields(Field("Binding",null,"OwnerReference"),Field("Outcome",null,Closed(dailyOutcome.Result,"Committed","MaterialConflict","ServerRetained")),
                    Field("ReasonCode",null,dailyOutcome.ParentCommandId==null?null:dailyOutcome.Result=="ServerRetained"?"CURRENT_SERVER_RETAINED":"INPUT_CORRECTED")));
        if (entry.Entity is DailyReviewHead reviewHead)
            return new(reviewHead.VesselId, reviewHead.RecordId, "DailyReviewHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Sequence").OriginalValue,
                reviewHead.Sequence, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Sequence").OriginalValue!), Number(reviewHead.Sequence)),Field("Binding",null,"OwnerReference")));
        if (entry.Entity is DailyReviewEvent reviewEvent)
            return new(reviewEvent.VesselId,reviewEvent.RecordId,"DailyReviewEvent",null,reviewEvent.Sequence,
                Fields(Field("Revision",null,Number(reviewEvent.Sequence)),Field("Binding",null,"OwnerReference")));
        if (entry.Entity is DailyEngineReferenceHead referenceHead)
            return new(referenceHead.VesselId, referenceHead.RecordId, "DailyEngineReferenceHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Sequence").OriginalValue,
                referenceHead.Sequence, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Sequence").OriginalValue!), Number(referenceHead.Sequence)),Field("Binding",null,"OwnerReference")));
        if (entry.Entity is DailyEngineReferenceEvent referenceEvent)
            return new(referenceEvent.VesselId,referenceEvent.RecordId,"DailyEngineReferenceEvent",null,referenceEvent.Sequence,
                Fields(Field("Revision",null,Number(referenceEvent.Sequence)),Field("Binding",null,"OwnerReference"),
                    Field("Operation",null,Closed(referenceEvent.Operation,"LINK","UNLINK")),Field("TargetKind",null,Closed(referenceEvent.TargetKind,"WorkOrder","Defect"))));
        if (entry.Entity is DailyEngineReferenceReceipt referenceReceipt)
            return new(referenceReceipt.VesselId,referenceReceipt.RecordId,"DailyEngineReferenceReceipt",null,null,Fields(Field("Binding",null,"CommittedResource")));
        if (entry.Entity is DailyReviewReceipt reviewReceipt)
            return new(reviewReceipt.VesselId,reviewReceipt.RecordId,"DailyReviewReceipt",null,null,Fields(Field("Binding",null,"CommittedResource")));
        if (entry.Entity is DailyOperation daily)
            return new(daily.VesselId, daily.Id, "DailyOperation", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                daily.Revision, Fields(Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference"),
                    Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(daily.Revision))));
        if (entry.Entity is DailyOperationRevision dailyRevision)
            return new(dailyRevision.VesselId, dailyRevision.RecordId, "DailyOperationRevision", null, dailyRevision.Revision,
                Fields(Field("Revision", null, Number(dailyRevision.Revision)), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is DailyOperationReceipt dailyReceipt)
            return new(dailyReceipt.VesselId, dailyReceipt.RecordId, "DailyOperationReceipt", null, null, Fields(Field("Binding", null, "CommittedResource")));
        if (entry.Entity is NotificationDispatchState dispatch)
            return new(dispatch.VesselId, dispatch.VesselId, "NotificationDispatch", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                dispatch.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(dispatch.Revision)),
                    Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference")));
        if (entry.Entity is NotificationEpisode episode)
            return new(episode.VesselId, episode.PlanId, "NotificationEpisode", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                episode.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(episode.Revision)),
                    Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference")));
        if (entry.Entity is Notification notice)
            return new(notice.VesselId, notice.Id, "Notification", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                notice.Revision, Fields(Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference"),
                    Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(notice.Revision))));
        if (entry.Entity is NotificationEvent notificationEvent)
            return new(notificationEvent.VesselId, notificationEvent.NotificationId, "NotificationEvent", null, notificationEvent.Sequence,
                Fields(Field("Revision", null, Number(notificationEvent.Sequence)), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is NotificationPreference preference)
            return new(preference.VesselId, preference.ActorId, "NotificationPreference", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                preference.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(preference.Revision)),
                    Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference")));
        if (entry.Entity is NotificationReceipt notificationReceipt)
            return new(notificationReceipt.VesselId, notificationReceipt.ResourceId, "NotificationReceipt", null, null,
                Fields(Field("Binding", null, "CommittedResource")));
        // PLAN-1511-5401: catalog key and closed states only; values, selectors and reasons are withheld.
        if (entry.Entity is ParameterRoot parameterRoot)
            return new(null, parameterRoot.Id, "ParameterRoot", entry.State == EntityState.Added ? null : (long?)entry.Property(nameof(parameterRoot.Revision)).OriginalValue,
                parameterRoot.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property(nameof(parameterRoot.Revision)).OriginalValue!), Number(parameterRoot.Revision)),
                    Field("Key", null, Closed(parameterRoot.Key, ParameterRegistry.Definitions.Select(d => d.Key).ToArray())),
                    Field("SecurityScope", null, Closed(parameterRoot.ScopeKind, "TENANT", "COMPANY", "VESSEL")),
                    Field("PendingChange", entry.State == EntityState.Added ? null : entry.Property(nameof(parameterRoot.PendingRevisionId)).OriginalValue == null ? "None" : "Present",
                        parameterRoot.PendingRevisionId == null ? "None" : "Present"),
                    Field("Selector", null, null, "ValuesWithheld")));
        if (entry.Entity is ParameterRevision parameterRevision)
            return new(null, parameterRevision.RootId, "ParameterRevision", null, parameterRevision.Sequence,
                Fields(Field("State", null, "IN_REVIEW"), Field("Key", null, Closed(parameterRevision.Key, ParameterRegistry.Definitions.Select(d => d.Key).ToArray())),
                    Field("ValueMode", null, Closed(parameterRevision.ValueMode, "VALUE", "INHERIT")),
                    Field("Provenance", null, Closed(parameterRevision.Provenance, "DeclaredUnverified", "SyntheticUnverified")),
                    Field("Value", null, null, "ValuesWithheld"), Field("Reason", null, null, "ValuesWithheld")));
        if (entry.Entity is ParameterDecision parameterDecision)
            return new(null, parameterDecision.RootId, "ParameterDecision", null, parameterDecision.Sequence,
                Fields(Field("Decision", null, Closed(parameterDecision.Outcome, "APPROVED", "REJECTED")),
                    Field("RevisionId", null, parameterDecision.RevisionId.ToString("D")), Field("Reason", null, null, "ValuesWithheld")));
        if (entry.Entity is ParameterCommandReceipt parameterReceipt)
            return new(null, parameterReceipt.RootId, "ParameterCommandReceipt", null, parameterReceipt.RootRevision,
                Fields(Field("Binding", null, "CommittedResource")));
        if (HrProjection(entry) is { } hr) return hr;
        // PLAN-1511-5202: identity values (issuer/subject), permission lists and actor-supplied
        // text never enter audit summaries; only closed lifecycle codes and revisions do.
        // Immutable owner-bound rows (account, grant/scope snapshots) and the zero-revision membership/delegation/recovery
        // creations carry the closed Binding=OwnerReference marker; a later lifecycle change keeps it as its before value.
        string? OwnerBinding() => entry.State == EntityState.Added ? null : "OwnerReference";
        if (entry.Entity is UserAccount account)
            return new(null, account.Id, "Account", null, null, Fields(Field("IdentityLink", null, null, "ValuesWithheld"),
                Field("Source", null, Closed(account.Source, "ConfigurationImport", "SyntheticUnverified", "AdminProvisioned")), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is AccountMembership membership)
        {
            string? Prior(string name) => entry.State == EntityState.Added ? null : entry.Property(name).OriginalValue?.ToString();
            return new(null, membership.Id, "Membership", entry.State == EntityState.Added ? null : (long?)entry.Property(nameof(membership.AccessRevision)).OriginalValue,
                membership.AccessRevision, Fields(
                    Field("Status", Prior(nameof(membership.Status)) is { } s ? Closed(s, "Active", "Suspended", "Revoked") : null, Closed(membership.Status, "Active", "Suspended", "Revoked")),
                    Field("StatusReason", null, Closed(membership.StatusReason, "ConfigurationImport", "SyntheticFixture", "AdministrativeSuspension", "AdministrativeResume", "AdministrativeRevocation", "SecuritySuspension", "SecurityResume", "AdminProvisioning")),
                    Field("AccessRevision", Prior(nameof(membership.AccessRevision)), Number(membership.AccessRevision)),
                    Field("AccountId", null, membership.AccountId.ToString("D")), Field("Binding", OwnerBinding(), "OwnerReference")));
        }
        // USER-ADMIN-01: closed bundle codes, counts and flags only; the permission list itself is withheld.
        if (entry.Entity is MembershipGrantRevision grantRevision)
            return new(null, grantRevision.MembershipId, "MembershipGrant", grantRevision.Revision > 1 ? grantRevision.Revision - 1 : null, grantRevision.Revision, Fields(
                Field("Revision", grantRevision.Revision > 1 ? Number(grantRevision.Revision - 1) : null, Number(grantRevision.Revision)),
                Field("Bundles", null, string.Join(',', Poseidon.Domain.MembershipGrantPolicy.Split(grantRevision.Bundles).Select(b => Closed(b, [.. Poseidon.Domain.MembershipRoleBundles.Codes])))),
                Field("Permissions", null, null, "ValuesWithheld"),
                Field("PermissionCount", null, Number(Poseidon.Domain.MembershipGrantPolicy.Split(grantRevision.Permissions).Length)),
                Field("TenantWideRead", null, grantRevision.TenantWideRead ? "true" : "false"),
                Field("TenantWideManage", null, grantRevision.TenantWideManage ? "true" : "false"),
                Field("CompanyId", null, grantRevision.ActiveCompanyId?.ToString("D")),
                Field("Binding", null, "OwnerReference")));
        if (entry.Entity is MembershipGrantVessel grantVessel)
            return new(grantVessel.VesselId, grantVessel.MembershipId, "MembershipGrantVessel", null, grantVessel.Revision,
                Fields(Field("CanEdit", null, grantVessel.CanEdit ? "true" : "false"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is AccountMembershipVesselGrant grant)
            return new(grant.VesselId, grant.MembershipId, "MembershipVesselGrant", null, null,
                Fields(Field("CanEdit", null, grant.CanEdit ? "true" : "false"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is MembershipDelegation delegation)
            return new(null, delegation.Id, "Delegation", null, null, Fields(
                Field("Status", entry.State == EntityState.Added ? null : "Active", Closed(delegation.Status, "Active", "Revoked")),
                Field("DelegatorMembershipId", null, delegation.DelegatorMembershipId.ToString("D")),
                Field("DelegateMembershipId", null, delegation.DelegateMembershipId.ToString("D")),
                Field("Permissions", null, null, "ValuesWithheld"),
                Field("ReasonCode", null, Closed(delegation.ReasonCode, "PlannedAbsence", "UnplannedAbsence", "WorkloadCover", "Handover", "LegacyUnscoped")),
                Field("CompanyId", null, delegation.CompanyId?.ToString("D")),
                Field("RevocationReason", null, delegation.RevocationReason is { } r ? Closed(r, "AdministrativeRevocation", "MembershipSuspended", "MembershipRevoked") : null),
                Field("Binding", OwnerBinding(), "OwnerReference")));
        if (entry.Entity is DelegationVesselScope scope)
            return new(scope.VesselId, scope.DelegationId, "DelegationVesselScope", null, null,
                Fields(Field("CanEdit", null, scope.CanEdit ? "true" : "false"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is TenantAccessRecovery recovery)
            return new(null, recovery.MembershipId, "AccessRecovery", null, null,
                Fields(Field("State", entry.State == EntityState.Added ? null : "RECOVERY_REQUIRED", recovery.ResolvedAt == null ? "RECOVERY_REQUIRED" : "RESOLVED"),
                    Field("Cause", null, Closed(recovery.Cause, "SecuritySuspension")),
                    Field("Resolution", null, recovery.Resolution is { } resolution ? Closed(resolution, "SecuritySuspensionResumed") : null),
                    Field("Binding", OwnerBinding(), "OwnerReference")));
        if (entry.Entity is MembershipCommandReceipt membershipReceipt)
            return new(null, membershipReceipt.ResourceId, "MembershipCommandReceipt", null, null,
                Fields(Field("Action", null, Closed(membershipReceipt.Action, "MembershipImport", "MembershipSuspend", "MembershipResume", "MembershipRevoke",
                    "MembershipSecuritySuspend", "DelegationCreate", "DelegationRevoke", "MembershipCreate", "MembershipGrantUpdate")), Field("Binding", null, "CommittedResource")));
        // Never copy the declared reference text, value or observation time into audit.
        if (entry.Entity is VesselProfileFieldSource fieldSource)
            return new(fieldSource.VesselId, fieldSource.VesselId, "VesselProfileFieldSource", null, fieldSource.Sequence,
                Fields(Field("FieldPath", null, Closed(fieldSource.FieldPath, [.. VesselProfileFieldPolicy.Paths])),
                    Field("SourceKind", null, Closed(fieldSource.SourceKind, [.. VesselProfileFieldPolicy.SourceKinds])),
                    Field("ValueRevision", null, Number(fieldSource.ValueRevision)), Field("Sequence", null, Number(fieldSource.Sequence)),
                    Field("Provenance", null, Closed(fieldSource.Provenance, "DeclaredUnverified", "SyntheticUnverified")),
                    Field("Binding", null, "OwnerReference")));
        if (entry.Entity is VesselProfileFieldSourceReceipt fieldReceipt)
            return new(fieldReceipt.VesselId, fieldReceipt.VesselId, "VesselProfileFieldSourceReceipt", null, fieldReceipt.Sequence,
                Fields(Field("Binding", null, "CommittedResource")));
        if (entry.Entity is FleetCatalogDraft fleetCatalogHead)
            return new(null, fleetCatalogHead.Id, "FleetCatalogDraft", entry.State == EntityState.Added ? null : (long?)entry.Property(nameof(fleetCatalogHead.Revision)).OriginalValue,
                fleetCatalogHead.Revision, Fields(Field("DraftState", null, "DRAFT")));
        if (entry.Entity is FleetCatalogDraftRevision fleetCatalogRevision)
            return new(null, fleetCatalogRevision.CatalogId, "FleetCatalogDraftRevision", null, fleetCatalogRevision.Revision,
                Fields(Field("DraftState", null, "DRAFT")));
        if (entry.Entity is CommandReceipt { Action: "CrewCatalogSave" } catalogReceipt)
            return new(catalogReceipt.ResourceId, catalogReceipt.ResourceId, "CrewCatalogReceipt", null, catalogReceipt.RegisterRevision, "[]");
        if (entry.Entity is CrewCatalogEvent catalog)
            return new(catalog.VesselId, catalog.VesselId, "CrewCatalog", null, catalog.Revision,
                Fields(Field("State", null, Closed(catalog.State, "Draft", "Reviewed", "Adopted", "Withdrawn")),
                    Field("Revision", null, Number(catalog.Revision)), Field("AuthorityId", null, catalog.AuthorityId.ToString("D")),
                    Field("Reason", null, null, "ValuesWithheld")));
        if (entry.Entity is CrewCatalogHead catalogHead)
            return new(catalogHead.VesselId, catalogHead.VesselId, "CrewCatalogHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue,
                catalogHead.Revision, Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(catalogHead.Revision)),
                    Field("ActiveRevision", entry.State == EntityState.Added ? null : entry.Property("ActiveRevision").OriginalValue is long prior ? Number(prior) : null,
                        catalogHead.ActiveRevision is long active ? Number(active) : null)));
        // Do not put exact source bytes, validation rationale or actor-provided text in audit summaries.
        if (entry.Entity is RegulatoryQualificationEvent qualification)
            return new(null, qualification.RuleId, "RegulatoryQualification", null, qualification.Revision,
                Fields(Field("State", null, Closed(qualification.State, "DRAFT", "IN_REVIEW", "ADOPTED", "AWAITING", "FUTURE_EFFECTIVE", "IN_FORCE", "SUPERSEDED")),
                    Field("RuleRevision", null, Number(qualification.RuleRevision)), Field("Revision", null, Number(qualification.Revision)),
                    Field("AuthorityId", null, qualification.AuthorityId.ToString("D"))));
        if (entry.Entity is RegulatoryFactAttestation fact)
            return new(fact.VesselId, fact.Id, "RegulatoryFactAttestation", null, fact.ProfileRevision,
                Fields(Field("RuleId", null, fact.RuleId.ToString("D")), Field("QualificationRevision", null, Number(fact.QualificationRevision)),
                    Field("AuthorityId", null, fact.AuthorityId.ToString("D"))));
        if (entry.Entity is RegulatoryQualificationHead head)
            return new(null, head.RuleId, "RegulatoryQualificationHead", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue, head.Revision,
                Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("Revision").OriginalValue!), Number(head.Revision))));
        if (entry.Entity is RegulatoryQualificationReceipt receipt)
            return new(null, receipt.RuleId, "RegulatoryQualificationReceipt", null, null, "[]");
        if (entry.Entity is EvaluationQualification binding)
            return new(parentVessel, binding.EvaluationId, "EvaluationQualification", null, binding.QualificationRevision,
                Fields(Field("RuleId", null, binding.RuleId.ToString("D")), Field("QualificationRevision", null, Number(binding.QualificationRevision)),
                    Field("AttestationId", null, binding.AttestationId?.ToString("D"))));
        // PLAN-1511-5802: closed state fields only. Titles, codes, authority refs, reasons,
        // verification basis, hashes and object generations are never copied.
        if (entry.Entity is ControlledDocument controlled)
        {
            long? beforeSequence = entry.State == EntityState.Added ? null : (long?)entry.Property(nameof(controlled.Sequence)).OriginalValue;
            string? Prior(string name) => entry.State == EntityState.Added ? null : entry.Property(name).OriginalValue?.ToString();
            return new(controlled.VesselId, controlled.Id, "ControlledDocument", beforeSequence, controlled.Sequence,
                Fields(Field("Sequence", beforeSequence is long b ? Number(b) : null, Number(controlled.Sequence)),
                    Field("DisposalState", Prior(nameof(controlled.DisposalState)), Closed(controlled.DisposalState, ControlledDocumentPolicy.DisposalStates)),
                    Field("HoldActive", Prior(nameof(controlled.HoldActive)) is { } h ? (h == bool.TrueString ? "true" : "false") : null, controlled.HoldActive ? "true" : "false"),
                    Field("CurrentRevision", entry.State == EntityState.Added ? null : entry.Property(nameof(controlled.CurrentRevision)).OriginalValue is long cr ? Number(cr) : null,
                        controlled.CurrentRevision is long current ? Number(current) : null),
                    Field("Retention", null, null, "ValuesWithheld")));
        }
        if (entry.Entity is ControlledDocumentRevision controlledRevision)
            // Intake release and publication modify the revision row in place: its immutable revision number is the honest
            // before/after reference (the row has no xmin and its receipt carries none), never a content value.
            return new(controlledRevision.VesselId, controlledRevision.DocumentId, "ControlledDocumentRevision",
                entry.State == EntityState.Added ? null : (long?)entry.Property(nameof(controlledRevision.Revision)).OriginalValue, controlledRevision.Revision,
                Fields(Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property(nameof(controlledRevision.Revision)).OriginalValue!), Number(controlledRevision.Revision)),
                    Field("IntakeState", entry.State == EntityState.Added ? null : (string?)entry.Property(nameof(controlledRevision.IntakeState)).OriginalValue,
                        Closed(controlledRevision.IntakeState, ControlledDocumentPolicy.IntakeStates)),
                    Field("PublicationState", entry.State == EntityState.Added ? null : (string?)entry.Property(nameof(controlledRevision.PublicationState)).OriginalValue,
                        Closed(controlledRevision.PublicationState, ControlledDocumentPolicy.PublicationStates)),
                    Field("Binding", null, "FileReference")));
        if (entry.Entity is ControlledDocumentCustodyEvent custody)
            return new(custody.VesselId, custody.DocumentId, "ControlledDocumentCustody", null, custody.Sequence,
                Fields(Field("EventType", null, Closed(custody.EventType, ControlledDocumentPolicy.EventTypes)),
                    Field("Source", null, Closed(custody.Source, "COMMAND", "SECURE_FILE_PIPELINE", "SYNTHETIC_SEED")), Field("Basis", null, null, "ValuesWithheld")));
        if (entry.Entity is ControlledDocumentVerification controlledVerification)
            return new(controlledVerification.VesselId, controlledVerification.DocumentId, "ControlledDocumentVerification", null, controlledVerification.Revision,
                Fields(Field("Method", null, Closed(controlledVerification.Method, ControlledDocumentPolicy.VerificationMethods)),
                    Field("Result", null, Closed(controlledVerification.Result, ControlledDocumentPolicy.VerificationResults)), Field("Basis", null, null, "ValuesWithheld")));
        if (entry.Entity is ControlledDocumentReceipt controlledReceipt)
            return new(controlledReceipt.VesselId, controlledReceipt.DocumentId, "ControlledDocumentReceipt", null, null, "[]");
        // Append-only link rows carry no revision or xmin: the closed owner binding proves them, like other immutable owner-bound rows.
        if (entry.Entity is ControlledDocumentCertificateLink certificateLink)
            return new(certificateLink.VesselId, certificateLink.DocumentId, "ControlledDocumentLink", null, null, Fields(Field("TargetKind", null, "VesselCertificate"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is ControlledDocumentEvidenceLink evidenceLink)
            return new(evidenceLink.VesselId, evidenceLink.DocumentId, "ControlledDocumentLink", null, null, Fields(Field("TargetKind", null, "ControlEvidence"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is ControlledDocumentCredentialLink credentialLink)
            return new(credentialLink.VesselId, credentialLink.DocumentId, "ControlledDocumentLink", null, null, Fields(Field("TargetKind", null, "Credential"), Field("Binding", null, "OwnerReference")));
        // DEV-040 override decisions: closed action/reason/state codes and exact pinned revisions only. The
        // private rationale, evidence ids and validity instants are never copied into the audit summary.
        if (entry.Entity is ApplicabilityOverride ov) {
            static string OverrideState(object? value) => value is "DRAFT" or "IN_REVIEW" or "ACTIVE" or "REVOKED" or "EXPIRED" or "SUPERSEDED"
                ? (string)value : throw new InvalidOperationException("Invalid applicability override metadata.");
            if (ov.Action is not ("ACCEPT_INTERPRETATION" or "REQUIRE_ADDITIONAL_CONTROL") || !ApplicabilityOverridePolicy.Reasons[ov.Action].Contains(ov.ReasonCode) ||
                ov.Purpose != "COMPLIANCE_REVIEW" || ov.Sequence <= 0 || ov.RuleRevision <= 0)
                throw new InvalidOperationException("Invalid applicability override metadata.");
            long? priorSequence = entry.State == EntityState.Added ? null : (long)entry.Property(nameof(ApplicabilityOverride.Sequence)).OriginalValue!;
            var overrideFields = new List<object> {
                Field("Sequence", priorSequence is { } p ? Number(p) : null, Number(ov.Sequence)),
                Field("OverrideState", entry.State == EntityState.Added ? null : OverrideState(entry.Property(nameof(ApplicabilityOverride.State)).OriginalValue), OverrideState(ov.State)),
                Field("Action", null, ov.Action), Field("ReasonCode", null, ov.ReasonCode),
                Field("RuleRevision", null, Number(ov.RuleRevision)), Field("Rationale", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference") };
            if (ov.ApproverId != null) overrideFields.Add(Field("Approver", null, "Recorded"));
            return new(ov.VesselId, ov.Id, "ApplicabilityOverride", priorSequence, ov.Sequence, Fields([.. overrideFields]));
        }
        if (entry.Entity is ApplicabilityOverrideEvent oe) {
            if (!ApplicabilityOverridePolicy.EventCodes.Contains(oe.EventCode) || oe.Sequence <= 0 ||
                oe.ToState is not ("IN_REVIEW" or "ACTIVE" or "REVOKED") || oe.FromState is not (null or "DRAFT" or "IN_REVIEW" or "ACTIVE"))
                throw new InvalidOperationException("Invalid applicability override metadata.");
            return new(oe.VesselId, oe.OverrideId, "ApplicabilityOverrideEvent", null, oe.Sequence, Fields(Field("EventCode", null, oe.EventCode),
                Field("FromState", null, oe.FromState), Field("ToState", null, oe.ToState), Field("Rationale", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        }
        if (entry.Entity is ComplianceCommandReceipt complianceReceipt) {
            if (complianceReceipt.Operation is not ("ApplicabilityOverridePropose" or "ApplicabilityOverrideDecision" or "ComplianceControlCandidates"))
                throw new InvalidOperationException("Invalid compliance receipt metadata.");
            return new(complianceReceipt.VesselId, complianceReceipt.ResourceId, "ComplianceCommandReceipt", null, null, Fields(Field("Operation", null, complianceReceipt.Operation)));
        }
        Guid? vessel = entry.Entity switch {
            OperationEquipmentBinding e => e.VesselId, EvaluationOwningReference e => e.VesselId,
            EvaluationUpstreamReference e => e.VesselId, MsmdRoleBinding e => e.VesselId,
            TugCapabilityBinding e => e.VesselId,
            TugOfflineEnrollment e => e.VesselId, TugOfflineOutcome e => e.VesselId, TugOfflineLifecycle e => e.VesselId,
            HumanReviewHead e => e.VesselId, HumanReviewEvent e => e.VesselId, HumanReviewReceipt e => e.VesselId,
            ControlEvidenceHead e => e.VesselId, ControlEvidenceRevision e => e.VesselId, ControlEvidenceRequirement e => e.VesselId, ControlEvidenceEdge e => e.VesselId, ControlEvidenceReceipt e => e.VesselId,
            ApplicabilityEvaluation e => e.VesselId, ApplicabilityReceipt r => r.VesselId,
            OfflineEnrollment o => o.VesselId, OfflineOutcome o => o.VesselId, OfflineLifecycle o => o.VesselId,
            Credential c => c.VesselId, CredentialRevision c => c.VesselId, ImportBatch b => b.VesselId, ImportPreview p => p.VesselId, ImportRowResult r => r.VesselId,
            TugDraftPlan p => p.VesselId, TugDraftPlanRevision p => p.VesselId, TugDraftPlanTug p => p.VesselId,
            TugJob j => j.VesselId, TugJobRevision j => j.VesselId, TugOwner o => o.VesselId, TugAssignment a => a.VesselId,
            MaintenanceObservation o => o.VesselId,
            Asset a => a.VesselId, AssetRevision r => r.VesselId, MaintenancePlan p => p.VesselId,
            MaintenancePlanRevision r => r.VesselId, MaintenanceJob j => j.VesselId, TechnicalOwner o => o.VesselId,
            CrewAssignment c => c.VesselId, CrewAssignmentRevision c => c.VesselId, CrewPosition c => c.VesselId,
            Document d => d.VesselId, DocumentVersion d => d.VesselId,
            FileStage f => f.VesselId, StoredFile f => f.VesselId,
            Vessel v => v.Id, VesselProfile p => p.VesselId, TugProfile p => p.VesselId,
            RegulatoryDraft d => d.VesselId, RegulatoryDraftRevision r => r.VesselId,
            CommandReceipt r when r.Action is "ProfileSave" or "VesselCreate" or "VesselUpdate" => r.ResourceId,
            _ => parentVessel
        };
        Guid? resource = entry.Entity switch {
            OperationEquipmentBinding e => e.Id, EvaluationOwningReference e => e.EvaluationId,
            EvaluationUpstreamReference e => e.EvaluationId, MsmdRoleBinding e => e.Id,
            TugCapabilityBinding e => e.Id,
            TugOfflineEnrollment e => e.JobId, TugOfflineOutcome e => e.JobId, TugOfflineLifecycle e => e.JobId,
            HumanReviewHead e => e.Id, HumanReviewEvent e => e.Id, HumanReviewReceipt e => e.ResourceId,
            ControlEvidenceHead e => e.Id, ControlEvidenceRevision e => e.Id, ControlEvidenceRequirement e => e.Id, ControlEvidenceEdge e => e.Id, ControlEvidenceReceipt e => e.ResourceId,
            RuleDraft d => d.Id, RuleRevision r => r.Id, ApplicabilityEvaluation e => e.Id,
            EvaluationRule r => r.EvaluationId, ApplicabilityReceipt r => r.ResourceId,
            OfflineEnrollment o => o.AssignmentId, OfflineOutcome o => o.AssignmentId, OfflineLifecycle o => o.AssignmentId,
            Credential c => c.Id, CredentialRevision c => c.CredentialId, ImportBatch b => b.Id, ImportPreview p => p.BatchId, ImportRowResult r => r.PreviewId,
            TugDraftPlan p => p.JobId, TugDraftPlanRevision p => p.JobId, TugDraftPlanTug p => p.JobId,
            TugJob j => j.Id, TugJobRevision j => j.JobId, TugOwner o => o.Id, TugAssignment a => a.JobId,
            MaintenanceObservation o => o.Id, MaintenanceObservationRevocation r => r.ObservationId,
            Asset a => a.Id, AssetRevision r => r.AssetId, MaintenancePlan p => p.Id,
            MaintenancePlanRevision r => r.PlanId, MaintenanceJob j => j.Id, TechnicalOwner o => o.Id,
            CrewPersonalProfile p => p.PersonId, CrewPersonalProfileRevision p => p.PersonId,
            CrewIdentityRevision r => r.PersonId,
            Person p => p.Id, CrewPosition c => c.Id, CrewAssignment c => c.Id, CrewAssignmentRevision c => c.AssignmentId,
            Document d => d.Id, DocumentVersion d => d.DocumentId,
            FileStage f => f.Id, StoredFile f => f.Id,
            Tenant t => t.Id, Company c => c.Id, Vessel v => v.Id,
            RegisterDraft d => d.Id, RegisterRevision r => r.Id, CommandReceipt r => r.ResourceId,
            _ => vessel
        };
        if (entry.Entity is TugOfflineEnrollment) return new(vessel, resource, "TugOfflineEnrollment", null, null,
            Fields(Field("Binding", entry.State == EntityState.Added ? null : "ServerOwnedContext", "ServerOwnedContext")));
        // DEV-081 lifecycle (60.02 §5): closed state codes and revisions only; the cancel reason is withheld.
        if (entry.Entity is TugJob lifecycleJob && entry.State == EntityState.Modified && entry.Property(nameof(TugJob.Status)).IsModified) {
            var priorRevision = (long)entry.Property(nameof(TugJob.Revision)).OriginalValue!;
            var jobFields = new List<object> { Field("Revision", Number(priorRevision), Number(lifecycleJob.Revision)),
                Field("JobState", Closed((string)entry.Property(nameof(TugJob.Status)).OriginalValue!, Poseidon.Domain.TugJobLifecycle.States),
                    Closed(lifecycleJob.Status, Poseidon.Domain.TugJobLifecycle.States)) };
            if (lifecycleJob.Status == Poseidon.Domain.TugJobLifecycle.Cancelled) jobFields.Add(Field("CancelReason", null, null, "ValuesWithheld"));
            return new(lifecycleJob.VesselId, lifecycleJob.Id, "TugJob", priorRevision, lifecycleJob.Revision, Fields([.. jobFields]));
        }
        if (entry.Entity is TugJobRevision lifecycleRevision && entry.State == EntityState.Added && lifecycleRevision.JobState != Poseidon.Domain.TugJobLifecycle.Draft)
            return new(lifecycleRevision.VesselId, lifecycleRevision.JobId, "TugJobRevision", null, lifecycleRevision.Revision, Fields([
                Field("ContentJson", null, null, "RevisionReference"), Field("JobState", null, Closed(lifecycleRevision.JobState, Poseidon.Domain.TugJobLifecycle.States)),
                Field("Revision", null, Number(lifecycleRevision.Revision)),
                .. (lifecycleRevision.CancelReason == null ? Array.Empty<object>() : [Field("CancelReason", null, null, "ValuesWithheld")])]));
        // S7 WP-B (DEV-083..085): closed state/decision codes, revisions and duty only. Reasons, labels, notes,
        // scope/assessment hashes and receipt keys are withheld; no actor identity is copied into summaries.
        if (entry.Entity is TugAssignmentProposal proposal) {
            var added = entry.State == EntityState.Added;
            var proposalFields = new List<object> { Field("Revision", added ? null : Number((long)entry.Property(nameof(TugAssignmentProposal.Revision)).OriginalValue!), Number(proposal.Revision)),
                Field("AssignmentState", added ? null : Closed((string)entry.Property(nameof(TugAssignmentProposal.State)).OriginalValue!, TugAssignmentWorkflow.States), Closed(proposal.State, TugAssignmentWorkflow.States)),
                Field("MasterAck", added ? null : Closed((string)entry.Property(nameof(TugAssignmentProposal.MasterAck)).OriginalValue!, TugAssignmentWorkflow.Acks), Closed(proposal.MasterAck, TugAssignmentWorkflow.Acks)),
                Field("Role", null, Closed(proposal.Role, TugAssignmentWorkflow.Roles)), Field("ScopeHash", null, null, "ValuesWithheld") };
            if (proposal.State == TugAssignmentWorkflow.Cancelled) proposalFields.Add(Field("CancelReason", null, null, "ValuesWithheld"));
            return new(proposal.VesselId, proposal.Id, "TugAssignmentProposal", added ? null : (long?)entry.Property(nameof(TugAssignmentProposal.Revision)).OriginalValue, proposal.Revision, Fields([.. proposalFields]));
        }
        if (entry.Entity is TugAssignmentResponse response)
            return new(response.VesselId, response.AssignmentId, "TugAssignmentResponse", null, null, Fields(
                Field("Decision", null, Closed(response.Decision, TugAssignmentWorkflow.Decisions)), Field("Duty", null, Closed(response.Duty, "TugMaster", "Master")),
                Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is TowEquipmentItem towItem) {
            var added = entry.State == EntityState.Added;
            return new(towItem.VesselId, towItem.Id, "TowEquipment", added ? null : (long?)entry.Property(nameof(TowEquipmentItem.Revision)).OriginalValue, towItem.Revision, Fields(
                Field("Revision", added ? null : Number((long)entry.Property(nameof(TowEquipmentItem.Revision)).OriginalValue!), Number(towItem.Revision)),
                Field("Kind", null, Closed(towItem.Kind, TowEquipmentPolicy.Kinds)),
                Field("Condition", added ? null : Closed((string)entry.Property(nameof(TowEquipmentItem.Condition)).OriginalValue!, TowEquipmentPolicy.Conditions), Closed(towItem.Condition, TowEquipmentPolicy.Conditions)),
                Field("Content", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        }
        if (entry.Entity is TowEquipmentRevision towRevision)
            return new(towRevision.VesselId, towRevision.ItemId, "TowEquipmentRevision", null, towRevision.Revision, Fields(
                Field("Revision", null, Number(towRevision.Revision)), Field("ContentJson", null, null, "RevisionReference")));
        if (entry.Entity is TowEquipmentVerification towVerification)
            return new(towVerification.VesselId, towVerification.ItemId, "TowEquipmentVerification", null, towVerification.ItemRevision, Fields(
                Field("Outcome", null, Closed(towVerification.Outcome, TowEquipmentPolicy.Verifications)), Field("Duty", null, Closed(towVerification.Duty, "ChiefEngineer")),
                Field("ItemRevision", null, Number(towVerification.ItemRevision)), Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is TugReadinessDecision readinessDecision)
            return new(readinessDecision.VesselId, readinessDecision.AssignmentId, "TugReadinessDecision", null, readinessDecision.AssignmentRevision, Fields(
                Field("Decision", null, Closed(readinessDecision.Decision, TugReadinessDecisionPolicy.Decisions)),
                Field("AssessedState", null, Closed(readinessDecision.AssessedState, TugReadinessEngine.States)),
                Field("Duty", null, Closed(readinessDecision.Duty, "TugMaster", "Master")), Field("AssessmentHash", null, null, "ValuesWithheld"),
                Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is TugReadinessReceipt readinessReceipt)
            return new(readinessReceipt.VesselId, readinessReceipt.ResourceId, "TugReadinessReceipt", null, readinessReceipt.Revision, Fields(
                Field("Action", null, Closed(readinessReceipt.Action, TugReadinessStorage.ReceiptActions)), Field("Binding", null, "CommittedResource")));
        // S9 WP-A DEV-059: closed codes only; the reason, hash and snapshot content are withheld.
        if (entry.Entity is DepartureDecision departureDecision)
            return new(departureDecision.VesselId, departureDecision.Id, "DepartureDecision", null, departureDecision.Sequence, Fields(
                Field("Decision", null, Closed(departureDecision.Decision, DepartureDecisionPolicy.Decisions)),
                Field("AssessedState", null, Closed(departureDecision.AssessedState, DepartureReadinessEngine.States)),
                Field("AssessmentMode", null, Closed(departureDecision.AssessmentMode, DepartureReadinessEngine.Modes)),
                Field("Duty", null, Closed(departureDecision.Duty, DepartureDecisionPolicy.MasterDuty)), Field("Sequence", null, Number(departureDecision.Sequence)),
                Field("AssessmentHash", null, null, "ValuesWithheld"), Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is DepartureReceipt departureReceipt)
            return new(departureReceipt.VesselId, departureReceipt.RecordId, "DepartureReceipt", null, departureReceipt.Sequence, Fields(
                Field("Action", null, Closed(departureReceipt.Action, DepartureStorage.ReceiptActions)), Field("Binding", null, "CommittedResource")));
        // S11 WP-C DEV-073/093: closed codes only; notes, source references, editions, issue ids and qualification refs are withheld.
        if (entry.Entity is RegulatoryReviewCampaign reviewCampaign)
            return new(reviewCampaign.VesselId, reviewCampaign.Id, "RegulatoryReviewCampaign", null, null, Fields(
                Field("Scope", null, Closed(reviewCampaign.Scope, RegulatoryReviewPolicy.Scopes)),
                Field("State", entry.State == EntityState.Added ? null : Closed((string)entry.Property("State").OriginalValue!, RegulatoryReviewPolicy.States), Closed(reviewCampaign.State, RegulatoryReviewPolicy.States)),
                Field("ItemCount", null, Number(reviewCampaign.ItemCount)), Field("OpenItemsAtClose", null, reviewCampaign.OpenItemsAtClose is { } open ? Number(open) : null),
                Field("Provenance", null, Closed(reviewCampaign.Provenance, "Recorded", "SyntheticUnverified")),
                Field("PinHash", null, null, "ValuesWithheld"), Field("CloseReason", null, null, "ValuesWithheld"),
                Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference")));
        if (entry.Entity is RegulatoryReviewItem reviewItem)
            return new(reviewItem.VesselId, reviewItem.Id, "RegulatoryReviewItem",
                entry.State == EntityState.Added ? null : (int)entry.Property("VerdictCount").OriginalValue!, reviewItem.VerdictCount, Fields(
                Field("Kind", null, Closed(reviewItem.Kind, RegulatoryReviewPolicy.Kinds)),
                Field("Verdict", entry.State == EntityState.Added ? null : Closed((string)entry.Property("Verdict").OriginalValue!, RegulatoryReviewPolicy.Verdicts), Closed(reviewItem.Verdict, RegulatoryReviewPolicy.Verdicts)),
                Field("SourceAuthority", null, reviewItem.SourceAuthority == null ? null : Closed(reviewItem.SourceAuthority, RegulatoryReviewPolicy.Authorities)),
                Field("OwnerRole", null, Closed(reviewItem.OwnerRole, RegulatoryReviewPolicy.OwnerRoles)),
                Field("Provenance", null, Closed(reviewItem.Provenance, "Recorded", "SyntheticUnverified")),
                Field("SourceReference", null, null, "ValuesWithheld"), Field("TrackedIssueId", null, null, "ValuesWithheld"),
                Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference")));
        if (entry.Entity is RegulatoryReviewVerdict reviewVerdict)
            return new(reviewVerdict.VesselId, reviewVerdict.ItemId, "RegulatoryReviewVerdict", null, reviewVerdict.Sequence, Fields(
                Field("Verdict", null, Closed(reviewVerdict.Verdict, RegulatoryReviewPolicy.Verdicts)),
                Field("SourceAuthority", null, reviewVerdict.SourceAuthority == null ? null : Closed(reviewVerdict.SourceAuthority, RegulatoryReviewPolicy.Authorities)),
                Field("OwnerRole", null, Closed(reviewVerdict.OwnerRole, RegulatoryReviewPolicy.OwnerRoles)),
                Field("QualificationState", null, Closed(reviewVerdict.QualificationState, RegulatoryReviewPolicy.QualificationVerified, "UNVERIFIED")),
                Field("Sequence", null, Number(reviewVerdict.Sequence)), Field("Note", null, null, "ValuesWithheld"), Field("QualificationRef", null, null, "ValuesWithheld"),
                Field("SourceReference", null, null, "ValuesWithheld"), Field("TrackedIssueId", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is RegulatoryReviewReceipt regulatoryReviewReceipt)
            return new(regulatoryReviewReceipt.VesselId, regulatoryReviewReceipt.RecordId, "RegulatoryReviewReceipt", null, null, Fields(
                Field("Action", null, Closed(regulatoryReviewReceipt.Action, RegulatoryReviewStorage.ReceiptActions)), Field("Binding", null, "CommittedResource")));
        if (entry.Entity is TechnicalOwner or TugOwner)
            return new(vessel, resource, "OwnerReference", null, null, Fields(Field("Binding", null, "OwnerReference")));
        if (entry.Entity is CrewPosition)
            return new(vessel, resource, "CrewPosition", null, null, Fields(Field("Binding", null, "PositionReference")));
        if (entry.Entity is MaintenanceJob)
            return new(vessel, resource, "MaintenanceJob", null, null, Fields(Field("Binding", null, "JobReference")));
        if (entry.Entity is MaintenanceObservation observation && entry.State == EntityState.Added)
            // The immutable row itself is the anchor, with tenant/vessel/asset ownership
            // enforced by its composite FK. SourceRevision is not an aggregate revision.
            // Do not copy declared Value/Source/Unit/PolicyVersion or imply verification.
            return new(observation.VesselId, observation.Id, "MaintenanceObservation", null, null,
                Fields(Field("Kind", null, AuditValuePolicy.Value("Kind", observation.Kind)), Field("Binding", null, "OwnerReference")));
        if (entry.Entity is Vessel fencedVessel && entry.State == EntityState.Modified &&
            entry.Property(nameof(Vessel.Name)).IsModified && entry.Properties.Count(p => p.IsModified) == 1 &&
            Equals(entry.Property(nameof(Vessel.Name)).OriginalValue, entry.Property(nameof(Vessel.Name)).CurrentValue))
            // Qualified workflows perform a real SQL/xmin fence on an unchanged vessel
            // name. Record the retained owner reference, not a semantic vessel edit or
            // an invented revision/post-save xmin; actual original xmin is audited separately.
            return new(fencedVessel.Id, fencedVessel.Id, "Vessel", null, null,
                Fields(Field("Name", null, null, "ValuesWithheld"), Field("Binding", "OwnerReference", "OwnerReference")));
        if (entry.Entity is Person person)
            return new(vessel, resource, "Person", entry.State == EntityState.Added ? null : (long?)entry.Property("IdentityRevision").OriginalValue,
                person.IdentityRevision, Fields(Field("Binding", entry.State == EntityState.Added ? null : "TenantPersonRecord", "TenantPersonRecord"),
                    Field("Revision", entry.State == EntityState.Added ? null : Number((long)entry.Property("IdentityRevision").OriginalValue!), Number(person.IdentityRevision))));
        if (entry.Entity is ApplicabilityEvaluation)
            return new(vessel, resource, "ApplicabilityEvaluation", null, null, Fields(Field("Outcome", null, "EvaluationRecorded")));
        if (entry.Entity is ImportPreview)
            return new(vessel, resource, "ImportPreview", null, null, Fields(Field("Outcome", null, "PreviewRecorded")));
        if (entry.Entity is ImportRowResult rowResult)
            return new(vessel, resource, "ImportRowResult", null, null,
                Fields(Field("Outcome", null, Closed(rowResult.Outcome, "valid", "rejected"))));
        if (entry.Entity is ImportBatch && entry.State == EntityState.Modified && LegacyImportStorage.IsConversion(entry))
            return new(vessel, resource, "ImportBatch", (long?)entry.Property("Revision").OriginalValue, (long?)entry.Property("Revision").CurrentValue,
                Fields(Field("Binding", "LegacyStorage", "ProtectedStorage")));
        if (entry.Entity is MaintenanceObservationRevocation)
            return new(vessel, resource, "MaintenanceObservationRevocation", null, null,
                Fields(Field("Lifecycle", null, "Revoked")));
        if (entry.Entity is EvaluationRule evaluatedRule)
            return new(vessel, resource, "EvaluationRule", null, evaluatedRule.RuleRevision,
                Fields(Field("RuleId", null, evaluatedRule.RuleId.ToString("D")), Field("RuleRevision", null, Number(evaluatedRule.RuleRevision))));
        if (entry.Entity is EvaluationUpstreamReference upstream)
            return new(vessel, resource, "EvaluationUpstreamReference", null, upstream.RuleRevision,
                Fields(Field("RuleId", null, upstream.RuleId.ToString("D")), Field("RuleRevision", null, Number(upstream.RuleRevision)),
                    Field("UpstreamId", null, upstream.UpstreamId.ToString("D"))));
        if (entry.Entity is TugOfflineLifecycle tl) {
            if (tl.Action is not ("Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed")) throw new InvalidOperationException("Unsupported tug lifecycle event.");
            return new(vessel, resource, "TugOfflineLifecycle", null, null, Fields(Field("Lifecycle", null, tl.Action)));
        }
        if (entry.Entity is TugOfflineOutcome to) {
            if (to.Result is not ("Committed" or "MaterialConflict")) throw new InvalidOperationException("Unsupported tug replay outcome.");
            return new(vessel, resource, "TugOfflineOutcome", null, to.Revision,
                Fields(Field("Outcome", null, to.Result), Field("ServerVersion", null, Number(to.ServerVersion))));
        }
        if (entry.Entity is OfflineLifecycle lifecycle) {
            if (lifecycle.Action is not ("Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed"))
                throw new InvalidOperationException("Unsupported offline lifecycle event.");
            return new(vessel, resource, "OfflineLifecycle", null, null, JsonSerializer.Serialize(new[] {
                new { field = "Lifecycle", before = (string?)null, after = lifecycle.Action, evidence = "Allowlisted" } }));
        }
        if (entry.Entity is OfflineEnrollment) return new(vessel, resource, "OfflineEnrollment", null, null,
            Fields(Field("Binding", entry.State == EntityState.Added ? null : "ServerOwnedContext", "ServerOwnedContext")));
        if (entry.Entity is OfflineOutcome outcome) {
            if (outcome.Result is not ("Committed" or "MaterialConflict")) throw new InvalidOperationException("Unsupported offline outcome.");
            return new(vessel, resource, "OfflineOutcome", null, outcome.Revision, JsonSerializer.Serialize(new[] {
                new { field = "Outcome", before = (string?)null, after = outcome.Result, evidence = "Allowlisted" } }));
        }
        if (entry.Entity is HumanReviewEvent review) {
            if (review.ReasonCode is not ("REQUEST_REVIEW" or "HUMAN_VERIFIED" or "HUMAN_REJECTED" or "VALIDITY_ELAPSED" or "INPUT_SUPERSEDED" or "HUMAN_DECIDED"))
                throw new InvalidOperationException("Invalid review metadata.");
            return new(vessel, resource, "HumanReview", null, review.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = review.ReasonCode, evidence = "Allowlisted" },
                new { field = "State", before = (string?)null, after = review.State, evidence = "Allowlisted" }
            }));
        }
        // DEV-028: closed lifecycle codes and revisions only; the private rationale is never copied.
        if (entry.Entity is ControlStateEvent cs) {
            if (cs.ReasonCode is not ("SUBMIT_FOR_REVIEW" or "RETURN_FOR_CORRECTION" or "APPROVE" or "PUBLISH" or "RETIRE" or "CONTENT_REVISED") ||
                cs.FromState is not ("DRAFT" or "IN_REVIEW" or "APPROVED" or "PUBLISHED") || cs.ToState is not ("DRAFT" or "IN_REVIEW" or "APPROVED" or "PUBLISHED" or "RETIRED"))
                throw new InvalidOperationException("Invalid control lifecycle metadata.");
            if (cs.SupersedesRevision != null && (cs.ReasonCode != "PUBLISH" || cs.SupersedesRevision <= 0 || cs.SupersedesRevision >= cs.ControlRevision))
                throw new InvalidOperationException("Invalid control lifecycle metadata.");
            var lifecycleFields = new List<object> { Field("ReasonCode", null, cs.ReasonCode), Field("FromState", null, cs.FromState), Field("ToState", null, cs.ToState),
                Field("ControlRevision", null, Number(cs.ControlRevision)), Field("Binding", null, "OwnerReference") };
            // 62.01 §6: publishing a successor supersedes the previously effective published revision.
            if (cs.SupersedesRevision is { } superseded) lifecycleFields.Add(Field("SupersedesRevision", null, Number(superseded)));
            return new(cs.VesselId, cs.ControlId, "ControlState", null, cs.Revision, Fields([.. lifecycleFields]));
        }
        if (entry.Entity is ControlEvidenceHead stateHead && entry.State == EntityState.Modified && entry.Property(nameof(ControlEvidenceHead.ControlState)).IsModified) {
            // A stored NULL is the derived DRAFT of a control that has no lifecycle event yet.
            static string LifecycleState(object? value) => value is null ? "DRAFT" : value is "DRAFT" or "IN_REVIEW" or "APPROVED" or "PUBLISHED" or "RETIRED" ? (string)value : throw new InvalidOperationException("Invalid control lifecycle metadata.");
            static string? Pointer(object? value) => value is long revision ? Number(revision) : null;
            var priorRevision = (long)entry.Property(nameof(ControlEvidenceHead.Revision)).OriginalValue!;
            var headFields = new List<object> { Field("Revision", Number(priorRevision), Number(stateHead.Revision)),
                Field("ControlState", LifecycleState(entry.Property(nameof(ControlEvidenceHead.ControlState)).OriginalValue), LifecycleState(stateHead.ControlState)) };
            if (entry.Property(nameof(ControlEvidenceHead.PublishedRevision)).IsModified)
                headFields.Add(Field("PublishedRevision", Pointer(entry.Property(nameof(ControlEvidenceHead.PublishedRevision)).OriginalValue), Pointer(stateHead.PublishedRevision)));
            return new(stateHead.VesselId, stateHead.Id, "Control", priorRevision, stateHead.Revision, Fields([.. headFields]));
        }
        if (entry.Entity is ControlEvidenceRevision ce) {
            if (ce.Lifecycle is not ("DRAFT" or "SUBMITTED" or "COMPLETED") || ce.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION" or "SYNTHETIC_INCOMPLETE_DRAFT" or "SUBMIT" or "COMPLETE" or "REQUIREMENT_CANDIDATE")) throw new InvalidOperationException("Invalid control/evidence change metadata.");
            return new(vessel, resource, ce.Kind, null, ce.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = ce.ReasonCode, evidence = "Allowlisted" },
                new { field = "Lifecycle", before = (string?)null, after = ce.Lifecycle, evidence = "Allowlisted" }
            }));
        }
        if (entry.Entity is RuleRevision rule) {
            if (rule.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION" or "REQUEST_REVIEW" or "SYNTHETIC_INCOMPLETE_DRAFT") ||
                rule.Lifecycle != "DRAFT" || rule.ReviewStatus is not ("NOT_REQUESTED" or "BLOCKED_SOURCE_ADMISSION")) throw new InvalidOperationException("Invalid rule change metadata.");
            return new(vessel, resource, "RuleRevision", null, rule.Revision, JsonSerializer.Serialize(new[] {
                new { field = "ReasonCode", before = (string?)null, after = rule.ReasonCode, evidence = "Allowlisted" },
                new { field = "ReviewStatus", before = (string?)null, after = rule.ReviewStatus, evidence = "Allowlisted" }
            }));
        }
        var kind = entry.Entity switch { RuleDraft => "RuleDraft", ApplicabilityEvaluation => "ApplicabilityEvaluation", EvaluationRule => "EvaluationRule", RegisterDraft d => d.Kind, RegisterRevision r => r.Kind, _ => null };
        var revisionProperty = entry.Entity is Person ? "IdentityRevision" : entry.Entity is CommandReceipt ? "RegisterRevision" : "Revision";
        var revision = entry.Metadata.FindProperty(revisionProperty) == null ? null : entry.Property(revisionProperty);
        long? before = revision != null && entry.State != EntityState.Added ? (long?)revision.OriginalValue : null;
        long? after = revision != null && entry.State != EntityState.Deleted ? (long?)revision.CurrentValue : null;
        var fields = new List<object>();
        foreach (var property in entry.Properties.OrderBy(p => p.Metadata.Name, StringComparer.Ordinal))
        {
            var name = property.Metadata.Name;
            // Text values, bodies, fingerprints, receipt keys and personal details are never copied.
            if (!AuditValuePolicy.Includes(name)) continue;
            if (entry.State == EntityState.Modified && !property.IsModified) continue;
            string? Value(object? value) {
                try { return AuditValuePolicy.Value(name, value); }
                catch (InvalidOperationException) {
                    // Fixed model/allowlist metadata only, never the rejected value.
                    throw new InvalidOperationException($"Unsupported audit field type: {entry.Metadata.ClrType.Name}.{name} ({value?.GetType().Name}).");
                }
            }
            fields.Add(new { field = name, before = entry.State == EntityState.Added ? null : Value(property.OriginalValue),
                after = entry.State == EntityState.Deleted ? null : Value(property.CurrentValue),
                evidence = AuditValuePolicy.Evidence(name) });
        }
        return new(vessel, resource, kind, before, after, JsonSerializer.Serialize(fields));
    }

    // DEV-105/DEV-106: closed state/decision codes and revisions only. Hazard, control,
    // reason, suggestion and matrix text is never copied (ValuesWithheld/RevisionReference).
    private static AuditMetadata? RiskProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        var states = Poseidon.Domain.RiskPolicy.AssessmentStates; var templates = Poseidon.Domain.RiskPolicy.TemplateStates;
        return entry.Entity switch
        {
            RiskAssessment a => new(a.VesselId, a.Id, "RiskAssessment", Before("Revision"), a.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(a.Revision)),
                Field("State", BeforeState("State", states), Closed(a.State, states)), Field("ContentJson", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            RiskAssessmentRevision r => new(r.VesselId, r.AssessmentId, "RiskAssessmentRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, states)),
                Field("ReasonCode", null, Closed(r.ReasonCode, "CREATE", "TEMPLATE_COPY", "EDIT", "SUBMIT", "APPROVE", "RETURN", "AMEND", "SYNTHETIC_INCOMPLETE_DRAFT")),
                Field("ContentJson", null, null, "RevisionReference"))),
            RiskAssessmentDecision d => new(d.VesselId, d.AssessmentId, "RiskAssessmentDecision", null, d.ResultRevision, Fields(
                Field("Decision", null, Closed(d.Decision, Poseidon.Domain.RiskPolicy.AssessmentDecisions)), Field("Revision", null, Number(d.ResultRevision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            RiskAssessmentAcknowledgement k => new(k.VesselId, k.AssessmentId, "RiskAssessmentAcknowledgement", null, k.Revision, Fields(
                Field("Choice", null, Closed(k.Choice, Poseidon.Domain.RiskPolicy.AckChoices)), Field("Revision", null, Number(k.Revision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            RiskTemplate t => new(null, t.Id, "RiskTemplate", Before("LatestRevision"), t.LatestRevision, Fields(
                Field("Revision", Before("LatestRevision") is { } r ? Number(r) : null, Number(t.LatestRevision)), Field("Binding", null, "OwnerReference"))),
            RiskTemplateRevision t => new(null, t.TemplateId, "RiskTemplateRevision", entry.State == EntityState.Added ? null : t.Revision, t.Revision, Fields(
                Field("Revision", entry.State == EntityState.Added ? null : Number(t.Revision), Number(t.Revision)),
                Field("Status", BeforeState("Status", templates), Closed(t.Status, templates)), Field("ContentJson", null, null, "RevisionReference"))),
            RiskTemplateDecision d => new(null, d.TemplateId, "RiskTemplateDecision", null, d.Revision, Fields(
                Field("Decision", null, Closed(d.Decision, Poseidon.Domain.RiskPolicy.TemplateDecisions)), Field("Revision", null, Number(d.Revision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            RiskTemplateSuggestion g => new(null, g.Id, "RiskTemplateSuggestion", entry.State == EntityState.Added ? null : g.Revision, g.Revision, Fields(
                Field("Status", BeforeState("Status", Poseidon.Domain.RiskPolicy.SuggestionStates), Closed(g.Status, Poseidon.Domain.RiskPolicy.SuggestionStates)),
                Field("Revision", entry.State == EntityState.Added ? null : Number(g.Revision), Number(g.Revision)), Field("Text", null, null, "ValuesWithheld"))),
            RiskMethod m => new(null, m.Id, "RiskMethod", entry.State == EntityState.Added ? null : m.Revision, m.Revision, Fields(
                Field("Status", BeforeState("Status", Poseidon.Domain.RiskPolicy.MethodStates), Closed(m.Status, Poseidon.Domain.RiskPolicy.MethodStates)),
                Field("Revision", entry.State == EntityState.Added ? null : Number(m.Revision), Number(m.Revision)), Field("ContentJson", null, null, "RevisionReference"))),
            RiskCommandReceipt c => new(c.VesselId, c.ResourceId, "RiskCommandReceipt", null, c.Revision, Fields(
                Field("Operation", null, Closed(c.Action, RiskOperations)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // DEV-108/DEV-109: closed kinds/states/decisions and revisions only. Measured values, codes,
    // source references, reasons, requirement references and dates are never copied.
    private static AuditMetadata? PmsProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        var kinds = Poseidon.Domain.PmsMeasurementPolicy.Kinds; var states = Poseidon.Domain.MaintenancePostponementPolicy.States;
        return entry.Entity switch
        {
            MeasurementPoint p => new(p.VesselId, p.Id, "MeasurementPoint", Before("Revision"), p.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(p.Revision)), Field("Kind", null, Closed(p.Kind, kinds)),
                Field("Source", null, Closed(p.Source, Poseidon.Domain.PmsMeasurementPolicy.PointSources)), Field("Definition", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            MeasurementPointRevision r => new(r.VesselId, r.PointId, "MeasurementPointRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("ContentJson", null, null, "RevisionReference"))),
            MeasurementReading m => new(m.VesselId, m.PointId, "MeasurementReading", null, null, Fields(
                Field("SourceType", null, Closed(m.SourceType, Poseidon.Domain.PmsMeasurementPolicy.ReadingSources)), Field("Value", null, null, "ValuesWithheld"),
                Field("Evidence", null, m.EvidenceFileId == null ? "None" : "Referenced"), Field("Binding", null, "OwnerReference"))),
            MaintenancePostponement x => new(x.VesselId, x.PlanId, "MaintenancePostponement", Before("Revision"), x.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(x.Revision)),
                Field("State", BeforeState("State", states), Closed(x.State, states)),
                Field("Criticality", null, Closed(x.Criticality, Poseidon.Domain.EquipmentCriticality.All)),
                Field("PlanRevision", null, Number(x.PlanRevision)), Field("ExtensionDays", null, Number(x.ExtensionDays)),
                Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PmsCommandReceipt c => new(c.VesselId, c.ResourceId, "PmsCommandReceipt", null, c.Revision, Fields(
                Field("Operation", null, Closed(c.Action, PmsStorage.Operations)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // DEV-055 / DEV-103: closed stage, kind, outcome and state codes and revisions only. Assessment,
    // safeguard, action, verification and return-to-service statements, report content, engineer
    // names and reasons are never copied (ValuesWithheld / RevisionReference).
    private static AuditMetadata? TechnicalRecordProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        var stages = Poseidon.Domain.DefectLifecyclePolicy.Stages; var states = Poseidon.Domain.ServiceReportPolicy.States;
        return entry.Entity switch
        {
            DefectLifecycle l => new(l.VesselId, l.DefectId, "DefectLifecycle", Before("Revision"), l.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(l.Revision)),
                Field("Stage", BeforeState("Stage", stages), Closed(l.Stage, stages)), Field("Binding", null, "OwnerReference"))),
            DefectLifecycleRecord r => new(r.VesselId, r.DefectId, "DefectLifecycleRecord", null, r.Sequence, Fields(
                Field("Kind", null, Closed(r.Kind, Poseidon.Domain.DefectLifecyclePolicy.Kinds)), Field("StageAfter", null, Closed(r.StageAfter, stages)),
                Field("Revision", null, Number(r.Sequence)), Field("DefectRevision", null, Number(r.DefectRevision)),
                Field("Outcome", null, r.Outcome == null ? null : Closed(r.Outcome, [.. Poseidon.Domain.DefectLifecyclePolicy.VerifyOutcomes,
                    .. Poseidon.Domain.DefectLifecyclePolicy.EffectivenessOutcomes, Poseidon.Domain.DefectLifecyclePolicy.ClosureOutcome])),
                Field("Text", null, null, "ValuesWithheld"))),
            ServiceReport s => new(s.VesselId, s.Id, "ServiceReport", Before("Revision"), s.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(s.Revision)),
                Field("State", BeforeState("State", states), Closed(s.State, states)), Field("ContentJson", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            ServiceReportRevision v => new(v.VesselId, v.ReportId, "ServiceReportRevision", null, v.Revision, Fields(
                Field("Revision", null, Number(v.Revision)), Field("State", null, Closed(v.State, states)),
                Field("ReasonCode", null, Closed(v.ReasonCode, Poseidon.Domain.ServiceReportPolicy.ReasonCodes)), Field("ContentJson", null, null, "RevisionReference"))),
            ServiceReportVerification d => new(d.VesselId, d.ReportId, "ServiceReportVerification", null, d.ResultRevision, Fields(
                Field("Decision", null, Closed(d.Decision, Poseidon.Domain.ServiceReportPolicy.Decisions)), Field("Revision", null, Number(d.ResultRevision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            TechnicalRecordReceipt c => new(c.VesselId, c.ResourceId, "TechnicalRecordReceipt", null, c.Revision, Fields(
                Field("Operation", null, Closed(c.Action, TechnicalRecordOperations)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // S9 WP-C DEV-062 / DEV-114: closed state, origin, reason, purpose and basis codes plus revisions
    // only. Transcript, field values, notes, attribution text and content hashes are never copied.
    private static AuditMetadata? S9WpcProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        var states = Poseidon.Domain.VoiceEventDraftPolicy.States;
        return entry.Entity switch
        {
            VoiceEventDraft d => new(d.VesselId, d.Id, "VoiceEventDraft", Before("Revision"), d.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(d.Revision)),
                Field("State", entry.State == EntityState.Added ? null : Closed((string)entry.Property("State").OriginalValue!, states), Closed(d.State, states)),
                Field("TranscriptOrigin", null, Closed(d.TranscriptOrigin, Poseidon.Domain.VoiceEventDraftPolicy.Origins)),
                Field("ExtractorVersion", null, Closed(d.ExtractorVersion, Poseidon.Domain.VoiceEventDraftPolicy.ExtractorVersion)),
                Field("RejectionReason", null, d.RejectionReason == null ? null : Closed(d.RejectionReason, Poseidon.Domain.VoiceEventDraftPolicy.RejectionReasons)),
                Field("Handoff", null, Closed(Poseidon.Domain.VoiceSafetyEventHandoff.State(d.State, d.SafetyEventId), Poseidon.Domain.VoiceSafetyEventHandoff.States)),
                Field("HandoffReason", null, d.HandoffReason == null ? null : Closed(d.HandoffReason, Poseidon.Domain.VoiceSafetyEventHandoff.PendingReasons)),
                Field("Content", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            VoiceEventDraftRevision v => new(v.VesselId, v.DraftId, "VoiceEventDraftRevision", null, v.Revision, Fields(
                Field("Revision", null, Number(v.Revision)), Field("State", null, Closed(v.State, states)),
                Field("ReasonCode", null, Closed(v.ReasonCode, Poseidon.Domain.VoiceEventDraftPolicy.ReasonCodes)), Field("FieldsJson", null, null, "RevisionReference"))),
            VoiceEventDraftReceipt c => new(c.VesselId, c.ResourceId, "VoiceEventDraftReceipt", null, c.Revision, Fields(
                Field("Operation", null, Closed(c.Action, VoiceEventDraftStorage.Operations)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            ControlledDocumentAcknowledgement a => new(a.VesselId, a.DocumentId, "ControlledDocumentAcknowledgement", null, a.Revision, Fields(
                Field("Revision", null, Number(a.Revision)), Field("Statement", null, Closed(a.Statement, Poseidon.Domain.ControlledDocumentSecurityPolicy.AcknowledgementStatement)),
                Field("ContentSha256", null, null, "ValuesWithheld"))),
            ControlledDocumentAccessRecord x => new(x.VesselId, x.DocumentId, "ControlledDocumentAccess", null, x.Revision, Fields(
                Field("Revision", null, Number(x.Revision)), Field("Purpose", null, Closed(x.Purpose, Poseidon.Domain.ControlledDocumentSecurityPolicy.Purposes)),
                Field("Classification", null, Closed(x.Classification, Poseidon.Domain.ControlledDocumentPolicy.Classifications)),
                Field("PolicyBasis", null, Closed(x.PolicyBasis, Poseidon.Domain.ControlledDocumentSecurityPolicy.Bases)), Field("Attribution", null, null, "ValuesWithheld"))),
            _ => null
        };
    }

    // S11 WP-B DEV-072: schema version, event count and a fixed operation only. Event codes, buckets,
    // timestamps, the retention reference and the tenant salt are never copied. A purge (Deleted) row
    // carries the same single revision as its insert, so before/after stay provable.
    private static AuditMetadata? PilotMetricsProjection(EntityEntry entry)
    {
        long? Before() => entry.State == EntityState.Added ? null : 1;
        return entry.Entity switch
        {
            PilotMetricBatch b => new(null, b.Id, "PilotMetricBatch", Before(), 1, Fields(
                Field("SchemaVersion", null, Closed(b.SchemaVersion, Poseidon.Domain.PilotMetricsPolicy.SchemaVersion)),
                Field("EventCount", null, Number(b.EventCount)), Field("Events", null, null, "ValuesWithheld"), Field("Buckets", null, null, "ValuesWithheld"))),
            PilotMetricTenantSalt t => new(null, t.TenantId, "PilotMetricTenantSalt", null, 1, Fields(Field("Salt", null, null, "ValuesWithheld"))),
            PilotMetricReceipt r => new(null, r.BatchId, "PilotMetricReceipt", Before(), 1, Fields(
                Field("Operation", null, "PilotMetricsSubmit"), Field("Accepted", null, Number(r.Accepted)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }

    internal static readonly string[] TechnicalRecordOperations = ["DefectLifecycleStart", "DefectLifecycleAssess", "DefectLifecycleSafeguard", "DefectLifecycleLift",
        "DefectLifecycleAction", "DefectLifecycleVerify", "DefectLifecycleEffectiveness", "DefectLifecycleClose",
        "ServiceReportCreate", "ServiceReportUpdate", "ServiceReportSubmit", "ServiceReportVerify", "ServiceReportAmend"];

    // S8 WP-D (DEV-087/088): closed state/decision/choice codes, revisions and duty only. Plan/briefing content,
    // limits, channels, hazards, barriers, reasons and hashes are withheld; no actor identity is copied.
    private static AuditMetadata? TowageProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        var plan = Poseidon.Domain.TowagePlanPolicy.States; var briefing = Poseidon.Domain.TowageBriefingPolicy.States;
        return entry.Entity switch
        {
            TowagePlan p => new(p.VesselId, p.Id, "TowagePlan", Before("Revision"), p.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(p.Revision)),
                Field("State", BeforeState("State", plan), Closed(p.State, plan)), Field("ContentJson", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            TowagePlanRevision r => new(r.VesselId, r.PlanId, "TowagePlanRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, plan)),
                Field("ReasonCode", null, Closed(r.ReasonCode, TowageStorage.PlanReasons)), Field("ContentJson", null, null, "RevisionReference"))),
            TowagePlanDecision d => new(d.VesselId, d.PlanId, "TowagePlanDecision", null, d.ResultRevision, Fields(
                Field("Decision", null, Closed(d.Decision, Poseidon.Domain.TowagePlanPolicy.Decisions)), Field("Revision", null, Number(d.ResultRevision)),
                Field("Duty", null, Closed(d.Duty, "TugMaster", "Master")), Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            TowageBriefing b => new(b.VesselId, b.Id, "TowageBriefing", Before("Revision"), b.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(b.Revision)),
                Field("State", BeforeState("State", briefing), Closed(b.State, briefing)), Field("ContentJson", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            TowageBriefingRevision r => new(r.VesselId, r.BriefingId, "TowageBriefingRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, briefing)),
                Field("ReasonCode", null, Closed(r.ReasonCode, TowageStorage.BriefingReasons)), Field("ContentJson", null, null, "RevisionReference"))),
            TowageBriefingAcknowledgement k => new(k.VesselId, k.BriefingId, "TowageBriefingAcknowledgement", null, k.Revision, Fields(
                Field("Choice", null, Closed(k.Choice, Poseidon.Domain.TowageBriefingPolicy.AckChoices)), Field("Revision", null, Number(k.Revision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            TowageReceipt c => new(c.VesselId, c.ResourceId, "TowageReceipt", null, c.Revision, Fields(
                Field("Action", null, Closed(c.Action, TowageStorage.ReceiptActions)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // S9 WP-B (DEV-060/061): closed state/decision/outcome codes and revisions only. Narrative, summary, location,
    // reasons, message bodies, factor/root-cause/barrier text, follow-up handles/hashes and the vault reporter binding
    // are never copied; no actor identity is copied into the summary.
    private static AuditMetadata? SafetyEventProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        var states = Poseidon.Domain.SafetyEventPolicy.States; var investigation = Poseidon.Domain.SafetyEventPolicy.InvestigationStates;
        string[] outcomes = [.. Poseidon.Domain.SafetyEventPolicy.VerifyOutcomes, .. Poseidon.Domain.SafetyEventPolicy.EffectivenessOutcomes];
        return entry.Entity switch
        {
            SafetyEvent e => new(e.VesselId, e.Id, "SafetyEvent", Before("Revision"), e.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(e.Revision)),
                Field("State", BeforeState("State", states), Closed(e.State, states)),
                Field("ReportingMode", null, Closed(e.ReportingMode, Poseidon.Domain.SafetyEventPolicy.ReportingModes)),
                Field("ContentJson", null, null, "RevisionReference"), Field("Binding", null, "OwnerReference"))),
            SafetyEventRevision r => new(r.VesselId, r.EventId, "SafetyEventRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, states)),
                Field("ReasonCode", null, Closed(r.ReasonCode, SafetyEventStorage.EventReasons)), Field("ContentJson", null, null, "RevisionReference"))),
            SafetyEventIdentity i => new(i.VesselId, i.Id, "SafetyEventIdentity", null, null, Fields(
                Field("Binding", entry.State == EntityState.Added ? null : "ProtectedStorage", "ProtectedStorage"),
                Field("ContactPreference", null, Closed(i.ContactPreference, "NONE", "IN_APP_FOLLOW_UP")), Field("Identity", null, null, "ValuesWithheld"))),
            SafetyEventIdentityAccess x => new(x.VesselId, x.Id, "SafetyEventIdentityAccess", null, x.EventRevision, Fields(
                Field("Purpose", null, Closed(x.Purpose, Poseidon.Domain.SafetyEventPolicy.IdentityPurposes)), Field("Revision", null, Number(x.EventRevision)),
                Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "ProtectedStorage"))),
            // A reporter follow-up message is audited against its own id, never the event id.
            SafetyEventMessage m => new(m.VesselId, m.AuthorKind == "REPORTER" ? m.Id : m.EventId, "SafetyEventMessage", null, m.EventRevision, Fields(
                Field("Visibility", null, Closed(m.Visibility, Poseidon.Domain.SafetyEventPolicy.MessageVisibilities)),
                Field("AuthorKind", null, Closed(m.AuthorKind, "TEAM", "REPORTER")), Field("Body", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            SafetyEventTriage t => new(t.VesselId, t.EventId, "SafetyEventTriage", null, t.ResultRevision, Fields(
                Field("Decision", null, Closed(t.Decision, Poseidon.Domain.SafetyEventPolicy.TriageDecisions)), Field("Revision", null, Number(t.ResultRevision)),
                Field("Severity", null, t.Severity == null ? null : Closed(t.Severity, Poseidon.Domain.SafetyEventPolicy.Severities)),
                Field("Reason", null, null, "ValuesWithheld"))),
            SafetyInvestigation v => new(v.VesselId, v.EventId, "SafetyInvestigation", Before("Revision"), v.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(v.Revision)),
                Field("State", BeforeState("State", investigation), Closed(v.State, investigation)), Field("ContentJson", null, null, "RevisionReference"),
                Field("Binding", null, "OwnerReference"))),
            SafetyInvestigationRevision r => new(r.VesselId, r.EventId, "SafetyInvestigationRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, investigation)),
                Field("ReasonCode", null, Closed(r.ReasonCode, SafetyEventStorage.InvestigationReasons)), Field("ContentJson", null, null, "RevisionReference"))),
            SafetyInvestigationDecision d => new(d.VesselId, d.EventId, "SafetyInvestigationDecision", null, d.Revision, Fields(
                Field("Decision", null, Closed(d.Decision, Poseidon.Domain.SafetyEventPolicy.ReviewDecisions)), Field("Revision", null, Number(d.Revision)),
                Field("Reason", null, null, "ValuesWithheld"))),
            SafetyAction a => new(a.VesselId, a.Id, "SafetyAction", Before("Revision"), a.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(a.Revision)),
                Field("ActionState", BeforeState("State", Poseidon.Domain.SafetyEventPolicy.ActionStates), Closed(a.State, Poseidon.Domain.SafetyEventPolicy.ActionStates)),
                Field("Effectiveness", BeforeState("Effectiveness", Poseidon.Domain.SafetyEventPolicy.EffectivenessStates), Closed(a.Effectiveness, Poseidon.Domain.SafetyEventPolicy.EffectivenessStates)),
                Field("Kind", null, Closed(a.Kind, Poseidon.Domain.SafetyEventPolicy.ActionKinds)), Field("Definition", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            SafetyActionStep s => new(s.VesselId, s.ActionId, "SafetyActionStep", null, s.ActionRevision, Fields(
                Field("Step", null, Closed(s.Kind, Poseidon.Domain.SafetyEventPolicy.StepKinds)), Field("Revision", null, Number(s.ActionRevision)),
                Field("StepOutcome", null, s.Outcome == null ? null : Closed(s.Outcome, outcomes)), Field("Text", null, null, "ValuesWithheld"))),
            SafetyEventReceipt c => new(c.VesselId, c.ResourceId, "SafetyEventReceipt", null, c.Revision, Fields(
                Field("Action", null, Closed(c.Action, SafetyEventStorage.ReceiptActions)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            SafetyEventOfflineEnrollment o => new(o.VesselId, o.Id, "SafetyEventOfflineEnrollment", null, null,
                Fields(Field("Binding", entry.State == EntityState.Added ? null : "OwnerReference", "OwnerReference"))),
            SafetyEventOfflineLifecycle l => new(l.VesselId, l.EnrollmentId, "SafetyEventOfflineLifecycle", null, null,
                Fields(Field("Binding", null, "OwnerReference"), Field("Lifecycle", null, Closed(l.Action, SafetyEventStorage.LifecycleActions)))),
            SafetyEventOfflineOutcome o => new(o.VesselId, o.EventId, "SafetyEventOfflineOutcome", null, o.Revision,
                Fields(Field("Binding", null, "OwnerReference"), Field("Outcome", null, Closed(o.Result, "Committed")))),
            _ => null
        };
    }

    // S9 WP-D (DEV-089/090/111): closed state/kind/decision codes and revisions only. Ledger payloads (loads,
    // weather, positions, evidence references), log content, meter values, scope items, reasons and verification
    // texts are withheld; no actor identity is copied into the summary.
    // S11 WP-D DEV-074/094/075: closed codes only; rationale, notes, titles, statements, references, pseudonyms and
    // build identities are withheld.
    // Pilot program kit (DEV-076/077/078/079/095/096/121): closed codes, sequences and record-type references only;
    // names, notes, summaries, titles, rationales, evidence references, methods, pseudonyms and values are withheld.
    private static AuditMetadata? PilotProgramProjection(EntityEntry entry)
    {
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        static string Flag(bool value) => value ? "True" : "False";
        var items = PilotProgramCatalog.Items.Select(i => i.Code).ToArray();
        long IssueStep(string state) => state switch { PilotIssuePolicy.Triaged => 2, PilotIssuePolicy.Resolved => 3, _ => 1 };
        return entry.Entity switch
        {
            PilotProgram p => new(null, p.Id, "PilotProgram", null, 1, Fields(
                Field("CatalogVersion", null, Closed(p.CatalogVersion, PilotProgramCatalog.Version)), Field("HasTug", null, Flag(p.TugVesselId != null)),
                Field("Provenance", null, Closed(p.Provenance, PilotProgramStorage.Provenances)), Field("Name", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            PilotChecklistLink l => new(null, l.ProgramId, "PilotChecklistLink", entry.State == EntityState.Added ? null : 1, l.WithdrawnAt == null ? 1 : 2, Fields(
                Field("Week", null, Closed(l.Week, PilotProgramCatalog.Weeks)), Field("ItemCode", null, Closed(l.ItemCode, items)),
                Field("RecordType", null, Closed(l.RecordType, PilotProgramCatalog.RecordTypes)), Field("AuditTraced", null, Flag(l.AuditTracedAtLink)),
                Field("Withdrawn", entry.State == EntityState.Added ? null : Flag(entry.Property("WithdrawnAt").OriginalValue != null), Flag(l.WithdrawnAt != null)),
                Field("Note", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PilotFeedback f => new(null, f.ProgramId, "PilotFeedback", null, null, Fields(
                Field("Week", null, Closed(f.Week, PilotProgramCatalog.Weeks)), Field("Area", null, Closed(f.Area, PilotProgramCatalog.Areas)),
                Field("Category", null, Closed(f.Category, PilotFeedbackPolicy.Categories)), Field("Sentiment", null, Closed(f.Sentiment, PilotFeedbackPolicy.Sentiments)),
                Field("Role", null, Closed(f.Role, PilotFeedbackPolicy.Roles)), Field("Summary", null, null, "ValuesWithheld"),
                Field("ParticipantPseudonym", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PilotIssue i => new(null, i.Id, "PilotIssue", entry.State == EntityState.Added ? null : IssueStep((string)entry.Property("State").OriginalValue!), IssueStep(i.State), Fields(
                Field("Week", null, Closed(i.Week, PilotProgramCatalog.Weeks)), Field("Severity", null, Closed(i.Severity, PilotIssuePolicy.Severities)),
                Field("Category", null, Closed(i.Category, PilotIssuePolicy.Categories)), Field("State", BeforeState("State", PilotIssuePolicy.States), Closed(i.State, PilotIssuePolicy.States)),
                Field("Title", null, null, "ValuesWithheld"), Field("TriageNote", null, null, "ValuesWithheld"), Field("Resolution", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            PilotMeasurement m => new(null, m.Id, "PilotMeasurement", null, m.Sequence, Fields(
                Field("MetricId", null, Closed(m.MetricId, PilotProgramCatalog.ValueMetrics.Select(x => x.Id).ToArray())), Field("Mode", null, Closed(m.Mode, PilotProgramCatalog.Modes)),
                Field("Phase", null, Closed(m.Phase, PilotMeasurementPolicy.Phases)), Field("Sequence", null, Number(m.Sequence)),
                Field("Value", null, null, "ValuesWithheld"), Field("EvidenceReference", null, null, "ValuesWithheld"), Field("Method", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            PilotWeekClose c => new(null, c.Id, "PilotWeekClose", null, c.Sequence, Fields(
                Field("Week", null, Closed(c.Week, PilotProgramCatalog.Weeks)), Field("Outcome", null, Closed(c.Outcome, PilotProgramPolicy.CloseOutcomes)),
                Field("Recommendation", null, Closed(c.Recommendation, PilotProgramPolicy.WeekRecommendations)), Field("Duty", null, Closed(c.Duty, PilotProgramPolicy.ProductOwnerDuty)),
                Field("Sequence", null, Number(c.Sequence)), Field("WeekHash", null, null, "ValuesWithheld"), Field("Rationale", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            PilotProgramDecision d => new(null, d.Id, "PilotProgramDecision", null, d.Sequence, Fields(
                Field("Decision", null, Closed(d.Decision, PilotProgramPolicy.Decisions)), Field("Recommendation", null, Closed(d.Recommendation, PilotProgramPolicy.ProgramRecommendations)),
                Field("Duty", null, Closed(d.Duty, PilotProgramPolicy.ProductOwnerDuty)), Field("Sequence", null, Number(d.Sequence)),
                Field("ProgramHash", null, null, "ValuesWithheld"), Field("Rationale", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PilotProgramReceipt r => new(null, r.RecordId, "PilotProgramReceipt", null, null, Fields(
                Field("Action", null, Closed(r.Action, PilotProgramStorage.ReceiptActions)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }

    private static AuditMetadata? PilotGateProjection(EntityEntry entry)
    {
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        static string Flag(bool value) => value ? "True" : "False";
        var scenarios = UatScenarioCatalog.Scenarios.Select(x => x.Id).ToArray();
        // Lifecycle step as the audited revision (run OPEN=1, SUBMITTED=2, ATTESTED=3; finding OPEN=1, resolved=2) so
        // before/after is provable for these xmin-versioned aggregates.
        long Step(string state) => state switch { UatPolicy.RunSubmitted => 2, UatPolicy.RunAttested => 3, UatPolicy.Resolved or UatPolicy.AcceptedRisk => 2, _ => 1 };
        long? BeforeStep() => entry.State == EntityState.Added ? null : Step((string)entry.Property("State").OriginalValue!);
        return entry.Entity switch
        {
            UatThreshold t => new(null, t.Id, "UatThreshold", null, t.Sequence, Fields(
                Field("Scope", null, Closed(t.Scope, UatScenarioCatalog.Scopes)), Field("Sequence", null, Number(t.Sequence)),
                Field("MinScenarioPassPercent", null, Number(t.MinScenarioPassPercent)), Field("MaxOpenMajorFindings", null, Number(t.MaxOpenMajorFindings)),
                Field("RequireDesignConformance", null, Flag(t.RequireDesignConformance)),
                Field("AgreementReference", null, null, "ValuesWithheld"), Field("Rationale", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            UatRun r => new(null, r.Id, "UatRun", BeforeStep(), Step(r.State), Fields(
                Field("Scope", null, Closed(r.Scope, UatScenarioCatalog.Scopes)), Field("Role", null, Closed(r.Role, [.. UatScenarioCatalog.ShipRoles, .. UatScenarioCatalog.TugRoles])),
                Field("Environment", null, Closed(r.Environment, UatPolicy.Environments)), Field("State", BeforeState("State", UatPolicy.RunStates), Closed(r.State, UatPolicy.RunStates)),
                Field("Provenance", null, Closed(r.Provenance, PilotGateStorage.Provenances)),
                Field("ParticipantPseudonym", null, null, "ValuesWithheld"), Field("BuildCommit", null, null, "ValuesWithheld"),
                Field("AttestationStatement", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            UatStepResult x => new(null, x.RunId, "UatStepResult", null, x.Attempt, Fields(
                Field("ScenarioId", null, Closed(x.ScenarioId, scenarios)), Field("Outcome", null, Closed(x.Outcome, UatPolicy.Outcomes)),
                Field("Attempt", null, Number(x.Attempt)), Field("Assisted", null, Flag(x.Assisted)),
                Field("AutomatedEvidence", null, Flag(x.AutomatedEvidenceReference != null)), Field("Note", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            UatFinding f => new(null, f.Id, "UatFinding", BeforeStep(), Step(f.State), Fields(
                Field("ScenarioId", null, Closed(f.ScenarioId, scenarios)), Field("Severity", null, Closed(f.Severity, UatPolicy.Severities)),
                Field("IsUx", null, Flag(f.IsUx)), Field("DesignConformance", null, Flag(f.DesignConformance)),
                Field("State", BeforeState("State", UatPolicy.FindingStates), Closed(f.State, UatPolicy.FindingStates)),
                Field("Title", null, null, "ValuesWithheld"), Field("Resolution", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PilotSupportCheck c => new(null, c.Id, "PilotSupportCheck", null, c.Sequence, Fields(
                Field("Item", null, Closed(c.Item, PilotGatePolicy.SupportItems)), Field("State", null, Closed(c.State, PilotGatePolicy.SupportStates)),
                Field("Sequence", null, Number(c.Sequence)), Field("Note", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            PilotGateDecision d => new(null, d.Id, "PilotGateDecision", null, d.Sequence, Fields(
                Field("Decision", null, Closed(d.Decision, PilotGatePolicy.Decisions)), Field("Recommendation", null, Closed(d.Recommendation, PilotGatePolicy.Recommendations)),
                Field("Duty", null, Closed(d.Duty, PilotGatePolicy.ProductOwnerDuty)), Field("Sequence", null, Number(d.Sequence)),
                Field("ReviewHash", null, null, "ValuesWithheld"), Field("Rationale", null, null, "ValuesWithheld"), Field("Conditions", null, null, "ValuesWithheld"),
                Field("Binding", null, "OwnerReference"))),
            PilotReceipt r => new(null, r.RecordId, "PilotReceipt", null, null, Fields(
                Field("Action", null, Closed(r.Action, PilotGateStorage.ReceiptActions)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }

    private static AuditMetadata? FieldOperationsProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        string? Rev(string property) => Before(property) is { } r ? Number(r) : null;
        var execution = Poseidon.Domain.TowageExecutionPolicy.States; var daily = Poseidon.Domain.TugDailyLogPolicy.States; var dock = Poseidon.Domain.DrydockPolicy.States;
        return entry.Entity switch
        {
            TowageExecution x => new(x.VesselId, x.Id, "TowageExecution", Before("Revision"), x.Revision, Fields(
                Field("Revision", Rev("Revision"), Number(x.Revision)), Field("State", BeforeState("State", execution), Closed(x.State, execution)),
                Field("VerificationState", BeforeState("VerificationState", Poseidon.Domain.TowageExecutionPolicy.VerificationStates), Closed(x.VerificationState, Poseidon.Domain.TowageExecutionPolicy.VerificationStates)),
                Field("Sequence", Rev("Sequence"), Number(x.Sequence)), Field("Binding", null, "OwnerReference"))),
            TowageExecutionEvent e => new(e.VesselId, e.ExecutionId, "TowageExecutionEvent", null, e.Sequence, Fields(
                Field("Kind", null, Closed(e.Kind, Poseidon.Domain.TowageExecutionPolicy.Kinds)), Field("Sequence", null, Number(e.Sequence)),
                Field("PayloadJson", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            TugDailyLog l => new(l.VesselId, l.Id, "TugDailyLog", Before("Revision"), l.Revision, Fields(
                Field("Revision", Rev("Revision"), Number(l.Revision)), Field("State", BeforeState("State", daily), Closed(l.State, daily)),
                Field("ContentJson", null, null, "RevisionReference"), Field("Binding", null, "OwnerReference"))),
            TugDailyLogRevision r => new(r.VesselId, r.LogId, "TugDailyLogRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, daily)),
                Field("ReasonCode", null, Closed(r.ReasonCode, FieldOperationsStorage.DailyReasons)), Field("ContentJson", null, null, "RevisionReference"))),
            TugDailyLogReview v => new(v.VesselId, v.LogId, "TugDailyLogReview", null, v.ResultRevision, Fields(
                Field("Decision", null, Closed(v.Decision, Poseidon.Domain.TugDailyLogPolicy.Decisions)), Field("Revision", null, Number(v.ResultRevision)),
                Field("Duty", null, Closed(v.Duty, "TugMaster", "Master")), Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            DrydockPackage d => new(d.VesselId, d.Id, "DrydockPackage", Before("Revision"), d.Revision, Fields(
                Field("Revision", Rev("Revision"), Number(d.Revision)), Field("State", BeforeState("State", dock), Closed(d.State, dock)),
                Field("ContentJson", null, null, "RevisionReference"), Field("Binding", null, "OwnerReference"))),
            DrydockPackageRevision r => new(r.VesselId, r.PackageId, "DrydockPackageRevision", null, r.Revision, Fields(
                Field("Revision", null, Number(r.Revision)), Field("State", null, Closed(r.State, dock)),
                Field("ReasonCode", null, Closed(r.ReasonCode, FieldOperationsStorage.DrydockReasons)), Field("Note", null, null, "ValuesWithheld"))),
            FieldOperationReceipt c => new(c.VesselId, c.ResourceId, "FieldOperationReceipt", null, c.Revision, Fields(
                Field("Action", null, Closed(c.Action, FieldOperationsStorage.ReceiptActions)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // S10 WP-A (DEV-064/065/113): pack/item kinds, states, revisions and hashes-as-references only. Observation text,
    // inspector comments, column headers, reasons and file checksums are never copied; no actor identity in the summary.
    private static AuditMetadata? AssuranceProjection(EntityEntry entry)
    {
        long? Before(string property) => entry.State == EntityState.Added ? null : (long?)entry.Property(property).OriginalValue;
        string? BeforeState(string property, string[] allowed) => entry.State == EntityState.Added ? null : Closed((string)entry.Property(property).OriginalValue!, allowed);
        return entry.Entity switch
        {
            AuditPackManifest m => new(m.VesselId, m.Id, "AuditPackManifest", null, 1, Fields(
                Field("Label", null, Closed(m.Label, Poseidon.Domain.AuditPackPolicy.Label)), Field("ItemCount", null, Number(m.ItemCount)),
                Field("ManifestHash", null, null, "RevisionReference"), Field("Binding", null, "OwnerReference"))),
            AuditPackItem i => new(i.VesselId, i.PackId, "AuditPackItem", null, i.Revision, Fields(
                Field("Kind", null, Closed(i.Kind, Poseidon.Domain.AuditPackPolicy.Kinds)), Field("State", null, Closed(i.State, Poseidon.Domain.AuditPackPolicy.States)),
                Field("Revision", null, Number(i.Revision)), Field("Hash", null, null, "RevisionReference"))),
            AssuranceImportMapping m => new(null, m.Id, "AssuranceImportMapping", null, m.Version, Fields(
                Field("Version", null, Number(m.Version)), Field("Bindings", null, null, "ValuesWithheld"), Field("Hash", null, null, "RevisionReference"))),
            AssuranceInspection x => new(x.VesselId, x.Id, "AssuranceInspection", Before("Revision"), x.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(x.Revision)),
                Field("State", null, Closed(x.State, Poseidon.Domain.AssurancePolicy.InspectionStates)),
                Field("Applicability", null, Closed(x.Applicability, Poseidon.Domain.AssurancePolicy.Applicable, Poseidon.Domain.AssurancePolicy.ReviewRequired)),
                Field("Reference", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            // A staged import has exactly two record revisions: 1 = PENDING_REVIEW (Added), 2 = decided once (Modified,
            // AssuranceStorage.Guard). The decision row therefore proves before/after as 1 -> 2; the mapping version stays a summary field.
            AssuranceImport x => new(x.VesselId, x.InspectionId, "AssuranceImport", entry.State == EntityState.Added ? null : 1, x.State == "PENDING_REVIEW" ? 1 : 2, Fields(
                Field("ImportState", BeforeState("State", Poseidon.Domain.AssurancePolicy.ImportStates), Closed(x.State, Poseidon.Domain.AssurancePolicy.ImportStates)),
                Field("MappingVersion", null, Number(x.MappingVersion)), Field("Content", null, null, "RevisionReference"), Field("Reason", null, null, "ValuesWithheld"))),
            AssuranceInspectionItem x => new(x.VesselId, x.InspectionId, "AssuranceInspectionItem", null, x.Revision, Fields(
                Field("Revision", null, Number(x.Revision)), Field("Change", null, Closed(x.ChangeKind, Poseidon.Domain.AssurancePolicy.New, Poseidon.Domain.AssurancePolicy.Changed)),
                Field("ContentJson", null, null, "RevisionReference"))),
            AssuranceFinding x => new(x.VesselId, x.Id, "AssuranceFinding", Before("Revision"), x.Revision, Fields(
                Field("Revision", Before("Revision") is { } r ? Number(r) : null, Number(x.Revision)),
                Field("FindingState", null, Closed(x.State, Poseidon.Domain.AssurancePolicy.FindingStates)),
                Field("ActionLinkState", BeforeState("ActionLinkState", Poseidon.Domain.AssurancePolicy.LinkStates), Closed(x.ActionLinkState, Poseidon.Domain.AssurancePolicy.LinkStates)),
                Field("Binding", null, "OwnerReference"))),
            AssuranceEvidence x => new(x.VesselId, x.InspectionId, "AssuranceEvidence", null, x.FileRevision, Fields(
                Field("MediaKind", null, Closed(x.MediaKind, Poseidon.Domain.AssurancePolicy.MediaKinds)), Field("Origin", null, Closed(x.Origin, Poseidon.Domain.AssurancePolicy.Origins)),
                Field("Verification", null, Closed(x.Verification, "UNVERIFIED")), Field("File", null, null, "RevisionReference"))),
            AssuranceReceipt c => new(c.VesselId, c.ResourceId, "AssuranceReceipt", null, c.Revision, Fields(
                Field("Action", null, Closed(c.Action, AssuranceStorage.ReceiptActions)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    // S10 WP-B (DEV-092): snapshot leases and offline outcomes. Captured values, conflict context, outcome bodies,
    // snapshot hashes and actor identities are withheld; only closed operation/result codes and revisions are kept.
    private static AuditMetadata? TugOperationsOfflineProjection(EntityEntry entry) => entry.Entity switch
    {
        TugOperationOfflineSnapshot s => new(s.VesselId, s.JobId, "TugOperationOfflineSnapshot", null, null,
            Fields(Field("Binding", null, "OwnerReference"), Field("Lease", null, null, "ValuesWithheld"))),
        TugOperationOfflineOutcome o => new(o.VesselId, o.ResourceId, "TugOperationOfflineOutcome", null, o.ServerVersion,
            Fields(Field("Operation", null, Closed(o.Operation, Poseidon.Domain.TugOperationsOfflineRules.CaptureOperations)),
                Field("Outcome", null, Closed(o.Result, Poseidon.Domain.TugOperationsOfflineRules.Results)),
                Field("ProtectedContext", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
        _ => null
    };
    // S11 WP-A (DEV-069/070): closed kind/state/decision codes, counts and revisions only. Source hashes, batch pins,
    // missing-critical lists, samples, reasons, exception dispositions and actor identities are withheld.
    private static AuditMetadata? ImportReconciliationProjection(EntityEntry entry)
    {
        var states = Poseidon.Domain.ImportReconciliationPolicy.States;
        string? BeforeState() => entry.State == EntityState.Added ? null : Closed((string)entry.Property("State").OriginalValue!, states);
        long? Before() => entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue;
        return entry.Entity switch
        {
            ImportProvenance p => new(p.VesselId, p.BatchId, "ImportProvenance", null, p.TargetRevision, Fields(
                Field("TargetKind", null, Closed(p.TargetKind, Poseidon.Domain.ImportReconciliationPolicy.TargetKinds)),
                Field("Operation", null, Closed(p.Operation, ImportReconciliationStorage.Operations)), Field("RowNumber", null, Number(p.RowNumber)),
                Field("SourceHash", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            ReconciliationReport r => new(r.VesselId, r.Id, "ReconciliationReport", Before(), r.Revision, Fields(
                Field("Revision", Before() is { } b ? Number(b) : null, Number(r.Revision)), Field("State", BeforeState(), Closed(r.State, states)),
                Field("SourceCount", null, Number(r.SourceCount)), Field("MissingCriticalCount", null, Number(r.MissingCriticalCount)),
                Field("SnapshotJson", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            ReconciliationSignOff o => new(o.VesselId, o.ReportId, "ReconciliationSignOff", null, null, Fields(
                Field("Decision", null, Closed(o.Decision, Poseidon.Domain.ImportReconciliationPolicy.Decisions)), Field("Duty", null, Closed(o.Duty, "DataOwner")),
                Field("Reason", null, null, "ValuesWithheld"), Field("Exceptions", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            ImportReconciliationReceipt c => new(c.VesselId, c.ResourceId, "ImportReconciliationReceipt", null, c.Revision, Fields(
                Field("Action", null, Closed(c.Action, ImportReconciliationStorage.ReceiptActions)), Field("Revision", null, Number(c.Revision)), Field("Binding", null, "CommittedResource"))),
            _ => null
        };
    }
    internal static readonly string[] RiskOperations = ["RiskAssessmentCreate", "RiskAssessmentUpdate", "RiskAssessmentSubmit", "RiskAssessmentReview",
        "RiskAssessmentAmend", "RiskAssessmentAcknowledge", "RiskTemplateCreate", "RiskTemplateUpdate", "RiskTemplateSubmit", "RiskTemplateReview",
        "RiskTemplateRevise", "RiskTemplateSuggest", "RiskTemplateSuggestionReview", "RiskMethodCreate", "RiskMethodPublish"];

    // PLAN-1511-5201: closed HR audit summaries. Names, legal names, registration values, employee numbers,
    // source references and reasons are never copied into audit summaries.
    // HR-owned legal-entity columns on the single company master (LEAD-5601-D01); values are never copied.
    private static readonly string[] HrCompanyValueFields = [nameof(Company.LegalName), nameof(Company.CountryCode), nameof(Company.HrTimeZone),
        nameof(Company.BaseLocale), nameof(Company.EffectiveFrom), nameof(Company.EffectiveTo)];
    private static readonly string[] HrCompanyFields = [.. HrCompanyValueFields, nameof(Company.LegalEntityStatus), nameof(Company.LegalEntityRevision)];

    private static AuditMetadata? HrProjection(EntityEntry entry)
    {
        static object State(EntityEntry e, string property, params string[] allowed) => Field(property,
            e.State == EntityState.Added ? null : Closed((string)e.Property(property).OriginalValue!, allowed), Closed((string)e.Property(property).CurrentValue!, allowed));
        static string Flag(bool value) => value ? "True" : "False";
        return entry.Entity switch
        {
            LegalEntityFieldRevision r => new(null, r.LegalEntityId, "LegalEntityFieldRevision", null, r.Revision, Fields(
                Field("Field", null, Closed(r.Field, "LegalName", "CountryCode", "HrTimeZone", "BaseLocale", "EffectiveFrom", "EffectiveTo", "LegalEntityStatus")),
                Field("SourceKind", null, Closed(r.SourceKind, Poseidon.Domain.HrSourcePolicy.SourceKinds)), Field("Value", null, null, "ValuesWithheld"),
                Field("Reason", null, null, "ValuesWithheld"))),
            PersonFieldRevision r => new(null, r.PersonId, "PersonFieldRevision", null, r.Revision, Fields(
                Field("Field", null, Closed(r.Field, "PersonType", "MasterState")),
                Field("SourceKind", null, Closed(r.SourceKind, Poseidon.Domain.HrSourcePolicy.SourceKinds)), Field("Value", null, null, "ValuesWithheld"),
                Field("Reason", null, null, "ValuesWithheld"))),
            // An HR legal-entity edit proves itself by its LegalEntityRevision and the original/committed company xmin.
            Company c when entry.State == EntityState.Modified && HrCompanyFields.Any(f => entry.Property(f).IsModified) => new(null, c.Id, "LegalEntity",
                (long?)entry.Property(nameof(Company.LegalEntityRevision)).OriginalValue, c.LegalEntityRevision, Fields([
                    Field("Revision", Number((long)entry.Property(nameof(Company.LegalEntityRevision)).OriginalValue!), Number(c.LegalEntityRevision)),
                    State(entry, nameof(Company.LegalEntityStatus), Poseidon.Domain.HrLegalEntityPolicy.Statuses),
                    .. HrCompanyValueFields.Where(f => entry.Property(f).IsModified).Select(f => Field(f, null, null, "ValuesWithheld"))])),
            // Identifier and role rows are their own command resource: the committed receipt carries their post-save xmin
            // and a status change carries the original xmin as BeforeVersion; the owning legal entity stays a closed scope.
            LegalEntityIdentifier i => new(null, i.Id, "LegalEntityIdentifier", null, null, Fields(
                Field("Type", null, Closed(i.Type, Poseidon.Domain.HrIdentifierPolicy.Types)), Field("Value", null, null, "ValuesWithheld"),
                Field("Retired", entry.State == EntityState.Added ? null : Flag(entry.Property("RetiredAt").OriginalValue != null), Flag(i.RetiredAt != null)))),
            LegalEntityRole r => new(null, r.Id, "LegalEntityRole", null, null, Fields(
                Field("Role", null, Closed(r.Role, Poseidon.Domain.HrRolePolicy.Roles)), Field("VesselId", null, r.VesselId?.ToString("D")),
                Field("Ended", entry.State == EntityState.Added ? null : Flag(entry.Property("EffectiveTo").OriginalValue != null), Flag(r.EffectiveTo != null)),
                State(entry, "State", Poseidon.Domain.HrRolePolicy.States))),
            HrStateChange s => new(null, s.LegalEntityId, "HrStateChange", null, null, Fields(
                Field("SubjectKind", null, Closed(s.SubjectKind, Poseidon.Domain.HrStateChangePolicy.SubjectKinds)), Field("SubjectId", null, s.SubjectId.ToString("D")),
                Field("State", Closed(s.FromState, Poseidon.Domain.HrStateChangePolicy.States), Closed(s.ToState, Poseidon.Domain.HrStateChangePolicy.States)),
                Field("Reason", null, null, "ValuesWithheld"), Field("Binding", null, "OwnerReference"))),
            Employment e => new(null, e.Id, "Employment", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue, e.Revision, Fields(
                State(entry, "State", Poseidon.Domain.HrEmploymentPolicy.States), Field("LegalEntityId", null, e.LegalEntityId.ToString("D")),
                Field("EmployeeNo", null, null, "ValuesWithheld"))),
            HrAssignment a => new(a.VesselId, a.Id, "HrAssignment", entry.State == EntityState.Added ? null : (long?)entry.Property("Revision").OriginalValue, a.Revision, Fields(
                State(entry, "State", Poseidon.Domain.HrAssignmentPolicy.States), Field("Kind", null, Closed(a.Kind, Poseidon.Domain.HrAssignmentPolicy.Kinds)),
                Field("Plan", null, "PlannedOnly"))),
            HrActualEvent v => new(null, v.AssignmentId, "HrActualEvent", null, null, Fields(
                Field("Kind", null, Closed(v.Kind, Poseidon.Domain.HrActualTimeline.Kinds)), Field("Disposition", null, Closed(v.Disposition, "Accepted", "PendingReconciliation")),
                Field("Quality", null, Closed(v.Quality, "Valid", "Suspect")), Field("Binding", null, "OwnerReference"))),
            _ => null
        };
    }

    private static readonly string[] LifecycleStates = ["DRAFT", "FINAL", "NOT_SUBMITTED", "OPEN", "IN_REVIEW", "VERIFIED", "CLOSED"];
    private static string? LifecycleState(EntityEntry e, string property, bool before) {
        var value = before ? (e.State == EntityState.Added ? null : e.Property(property).OriginalValue) : e.Property(property).CurrentValue;
        return value is string text ? Closed(text, LifecycleStates) : null;
    }
    private static string Closed(string value, params string[] allowed) => allowed.Contains(value, StringComparer.Ordinal)
        ? value : throw new InvalidOperationException("Invalid controlled audit state.");
}
