using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class HumanReviewHead : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
}
public sealed class HumanReviewEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public required string Kind { get; set; }
    public required string State { get; set; }
    public Guid EvidenceId { get; set; }
    public long EvidenceRevision { get; set; }
    public string EvidenceKind { get; set; } = "Evidence";
    public Guid ControlId { get; set; }
    public long ControlRevision { get; set; }
    public string ControlKind { get; set; } = "Control";
    public Guid? EvaluationId { get; set; }
    public Guid? VerificationId { get; set; }
    public long? VerificationRevision { get; set; }
    public Guid SubmitterId { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public Guid OperationId { get; set; }
    public required string ReasonCode { get; set; }
    public required string Rationale { get; set; }
    public required string AuthorityHash { get; set; }
    public required string ContextHash { get; set; }
    // Null for historical events. Never backfill human text or authority.
    public string? DecisionJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
}
public sealed class HumanReviewReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public Guid ResourceId { get; set; }
    public required string Fingerprint { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public static class HumanReviewStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<ApplicabilityEvaluation>().HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
        model.Entity<HumanReviewHead>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Id }); b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<HumanReviewEvent>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Id, x.Revision });
            b.Property(x => x.EvidenceKind).HasMaxLength(16); b.Property(x => x.ControlKind).HasMaxLength(16);
            b.HasOne<HumanReviewHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.EvidenceKind, Id = x.EvidenceId, Revision = x.EvidenceRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.ControlKind, Id = x.ControlId, Revision = x.ControlRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.EvaluationId }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<HumanReviewEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.VerificationId, Revision = x.VerificationRevision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("HumanReviewEvents", t => t.HasCheckConstraint("CK_HumanReviewEvents", Constraint));
        });
        model.Entity<HumanReviewReceipt>(b => {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key }); b.Property(x => x.Key).HasMaxLength(80);
            b.HasOne<HumanReviewHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.ResourceId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
    internal const string Constraint = "\"Revision\" > 0 AND \"EvidenceRevision\" > 0 AND \"ControlRevision\" > 0 AND \"EvidenceKind\" = 'Evidence' AND \"ControlKind\" = 'Control' AND ((\"Kind\" = 'Verification' AND \"State\" IN ('PENDING','VERIFIED','REJECTED','EXPIRED','SUPERSEDED') AND \"VerificationId\" IS NULL AND \"VerificationRevision\" IS NULL AND \"DecisionJson\" IS NULL) OR (\"Kind\" = 'Decision' AND \"State\" IN ('PENDING','REJECTED','RECORDED') AND \"VerificationId\" IS NOT NULL AND \"VerificationRevision\" IS NOT NULL AND \"VerificationRevision\" > 0 AND ((\"State\" = 'RECORDED' AND \"DecisionJson\" IS NOT NULL AND \"ReasonCode\" = 'HUMAN_DECIDED') OR (\"State\" <> 'RECORDED' AND \"DecisionJson\" IS NULL))))";
}
