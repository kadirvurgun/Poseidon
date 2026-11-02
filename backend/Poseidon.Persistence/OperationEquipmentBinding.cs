using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

public sealed class EvaluationOwningReference : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EvaluationId { get; set; }
    public string Kind { get; set; } = "Evidence";
    public Guid Id { get; set; }
    public long Revision { get; set; }
}
// Append-only owning relationships of the real Evidence revision, not evaluator facts.
public sealed class OperationEquipmentBinding : ITenantEntity
{
    internal const string DocumentConstraint = "\"Kind\" = 'Evidence' AND \"ServiceKind\" = 'Evidence' AND \"Revision\" > 0 AND \"DocumentKind\" IN ('Operation','ServiceAuthorization','CargoPlan','WinchFitment','ClassPack','Msmd','CrewRoleAssessment')";
    internal const string JobConstraint = "(\"JobId\" IS NULL AND \"JobRevision\" IS NULL AND \"PlanRevision\" IS NULL) OR (\"JobId\" IS NOT NULL AND \"JobRevision\" IS NOT NULL AND \"JobRevision\" > 0 AND (\"PlanRevision\" IS NULL OR \"PlanRevision\" > 0))";
    internal const string AssetConstraint = "(\"AssetId\" IS NULL AND \"AssetRevision\" IS NULL) OR (\"AssetId\" IS NOT NULL AND \"AssetRevision\" IS NOT NULL AND \"AssetRevision\" > 0)";
    internal const string ServiceConstraint = "(\"ServiceId\" IS NULL AND \"ServiceRevision\" IS NULL) OR (\"ServiceId\" IS NOT NULL AND \"ServiceRevision\" IS NOT NULL AND \"ServiceRevision\" > 0 AND \"ServiceId\" <> \"Id\")";
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "Evidence";
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public required string DocumentKind { get; set; }
    public Guid? CredentialId { get; set; }
    public long? CredentialRevision { get; set; }
    public Guid? AssignmentId { get; set; }
    public long? AssignmentRevision { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? JobId { get; set; }
    public long? JobRevision { get; set; }
    public long? PlanRevision { get; set; }
    public Guid? AssetId { get; set; }
    public long? AssetRevision { get; set; }
    public string ServiceKind { get; set; } = "Evidence";
    public Guid? ServiceId { get; set; }
    public long? ServiceRevision { get; set; }
    public static OperationEquipmentBinding From(Guid tenant, Guid vessel, Guid id, long revision, ControlEvidenceContent c) => new() {
        TenantId = tenant, VesselId = vessel, Id = id, Revision = revision, DocumentKind = OperationEquipmentInputs.Kind(c),
        CredentialId = c.CrewRoleAssessment?.Credential?.Id, CredentialRevision = c.CrewRoleAssessment?.Credential?.Revision,
        AssignmentId = c.CrewRoleAssessment?.AssignmentId, AssignmentRevision = c.CrewRoleAssessment?.AssignmentRevision, PositionId = c.CrewRoleAssessment?.PositionId,
        JobId = c.Operation?.JobId ?? c.ServiceAuthorization?.JobId, JobRevision = c.Operation?.JobRevision ?? c.ServiceAuthorization?.JobRevision,
        PlanRevision = c.Operation?.PlanRevision ?? c.ServiceAuthorization?.PlanRevision,
        AssetId = c.WinchFitment?.AssetId, AssetRevision = c.WinchFitment?.AssetRevision,
        ServiceId = c.Operation?.ServiceAuthorizationId, ServiceRevision = c.Operation?.ServiceAuthorizationRevision
    };
    internal static void Configure(ModelBuilder model) {
        model.Entity<EvaluationOwningReference>(b => {
            b.ToTable("EvaluationOwningReferences", "core");
            b.HasKey(x => new { x.TenantId, x.EvaluationId, x.Id }); b.Property(x => x.Kind).HasMaxLength(16);
            b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.EvaluationId }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OperationEquipmentBinding>(b => {
            b.ToTable("OperationEquipmentBindings", "core", t => {
                t.HasCheckConstraint("CK_Owning_Document", DocumentConstraint); t.HasCheckConstraint("CK_Owning_Job", JobConstraint);
                t.HasCheckConstraint("CK_Compliance_Assignment", "(\"AssignmentId\" IS NULL AND \"AssignmentRevision\" IS NULL) OR (\"AssignmentId\" IS NOT NULL AND \"AssignmentRevision\" > 0)");
                t.HasCheckConstraint("CK_Owning_Asset", AssetConstraint); t.HasCheckConstraint("CK_Owning_Service", ServiceConstraint);
            });
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.ServiceKind).HasMaxLength(16);
            b.Property(x => x.DocumentKind).HasMaxLength(32);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CredentialRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.CredentialId, Revision = x.CredentialRevision }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.CredentialId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewAssignmentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssignmentId, Revision = x.AssignmentRevision }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.AssignmentId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewPosition>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PositionId, x.VesselId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugJobRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, Revision = x.JobRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugDraftPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, Revision = x.PlanRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId, Revision = x.AssetRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.ServiceKind, Id = x.ServiceId, Revision = x.ServiceRevision }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
