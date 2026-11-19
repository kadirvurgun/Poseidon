using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class ControlEvidenceHead : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public required string Kind { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
    // DEV-028 definition lifecycle of the candidate (latest) revision; Control only, always null
    // for Evidence. NULL on a Control means "no lifecycle event yet" and derives to DRAFT
    // (ControlLifecyclePolicy.State); no stored row is rewritten to manufacture a state.
    // Mutable head state, guarded by xmin; every change appends a ControlStateEvent.
    public string? ControlState { get; set; }
    // 62.01 §6 current pointer: the effective PUBLISHED revision. It stays effective while a
    // successor candidate is drafted/reviewed/approved and only moves on the successor's PUBLISH
    // (audited, SupersedesRevision on the event). RETIRE clears it. Always null for Evidence.
    public long? PublishedRevision { get; set; }
}
// Append-only DEV-028 lifecycle history. Revision is the per-control event sequence; the
// exact control revision it binds is ControlRevision. Rationale is private (never audited).
public sealed class ControlStateEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public string ControlKind { get; set; } = "Control";
    public Guid ControlId { get; set; }
    public long Revision { get; set; }
    public long ControlRevision { get; set; }
    public required string FromState { get; set; }
    public required string ToState { get; set; }
    public required string ReasonCode { get; set; }
    // PUBLISH of a successor: the previously effective published revision it supersedes.
    public long? SupersedesRevision { get; set; }
    public string? Rationale { get; set; }
    public Guid ActorId { get; set; }
    public string? RoleContext { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class ControlEvidenceRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public required string Kind { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public required string Hash { get; set; }
    public Guid? FileId { get; set; }
    public long? FileRevision { get; set; }
    public string? Checksum { get; set; }
    public Guid? ActorId { get; set; }
    public string? RoleContext { get; set; }
    public required string ReasonCode { get; set; }
    public bool? CriticalDeclaration { get; set; }
    public string Lifecycle { get; set; } = "DRAFT";
    public DateTimeOffset SavedAt { get; set; }
}
// Snapshot membership is append-only: a new revision can replace its link set,
// while old decisions keep the exact previous set. No cascade deletion.
public sealed class ControlEvidenceRequirement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public required string Kind { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public Guid RequirementId { get; set; }
    public long RequirementRevision { get; set; }
    public string RequirementKind { get; set; } = "Requirement";
    public Guid SourceId { get; set; }
    public long SourceRevision { get; set; }
    public string SourceKind { get; set; } = "Source";
    public Guid? RuleId { get; set; }
    public long? RuleRevision { get; set; }
}
public sealed class ControlEvidenceEdge : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public required string Kind { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public required string TargetKind { get; set; }
    public Guid TargetId { get; set; }
    public long TargetRevision { get; set; }
    public uint TargetVersion { get; set; }
}
public sealed class ControlEvidenceReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Kind { get; set; }
    public Guid ResourceId { get; set; }
    public required string Fingerprint { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public static class ControlEvidenceStorage
{
    internal const string StateConstraint = "(\"Kind\" = 'Control' AND (\"ControlState\" IS NULL OR \"ControlState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED','RETIRED')) AND " +
        "(\"PublishedRevision\" IS NULL OR (\"PublishedRevision\" > 0 AND \"PublishedRevision\" <= \"Revision\" AND \"ControlState\" IS NOT NULL AND \"ControlState\" <> 'RETIRED')) AND " +
        "(\"ControlState\" IS DISTINCT FROM 'PUBLISHED' OR \"PublishedRevision\" IS NOT DISTINCT FROM \"Revision\")) OR " +
        "(\"Kind\" = 'Evidence' AND \"ControlState\" IS NULL AND \"PublishedRevision\" IS NULL)";
    internal const string EventConstraint = "\"ControlKind\" = 'Control' AND \"Revision\" > 0 AND \"ControlRevision\" > 0 AND " +
        "\"FromState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED','RETIRED') AND " +
        "((\"ReasonCode\" = 'SUBMIT_FOR_REVIEW' AND \"FromState\" = 'DRAFT' AND \"ToState\" = 'IN_REVIEW') OR " +
        "(\"ReasonCode\" = 'RETURN_FOR_CORRECTION' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'DRAFT') OR " +
        "(\"ReasonCode\" = 'APPROVE' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'APPROVED') OR " +
        "(\"ReasonCode\" = 'PUBLISH' AND \"FromState\" = 'APPROVED' AND \"ToState\" = 'PUBLISHED') OR " +
        "(\"ReasonCode\" = 'RETIRE' AND \"FromState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" = 'RETIRED') OR " +
        "(\"ReasonCode\" = 'CONTENT_REVISED' AND \"FromState\" IN ('IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" = 'DRAFT')) AND " +
        "(\"SupersedesRevision\" IS NULL OR (\"ReasonCode\" = 'PUBLISH' AND \"SupersedesRevision\" > 0 AND \"SupersedesRevision\" < \"ControlRevision\"))";
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<StoredFile>().HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id, x.Revision, x.Checksum }).HasName("AK_StoredFiles_Evidence");
        model.Entity<ControlEvidenceHead>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id }); b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ControlState).HasMaxLength(16);
            b.ToTable("ControlEvidenceHeads", t => {
                t.HasCheckConstraint("CK_ControlEvidenceHeads", "\"Kind\" IN ('Control','Evidence') AND \"Revision\" > 0");
                t.HasCheckConstraint("CK_ControlEvidenceHeads_ControlState", StateConstraint);
            });
        });
        model.Entity<ControlStateEvent>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.ControlId, x.Revision });
            b.Property(x => x.ControlKind).HasMaxLength(16); b.Property(x => x.FromState).HasMaxLength(16); b.Property(x => x.ToState).HasMaxLength(16);
            b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.Rationale).HasMaxLength(2000);
            b.HasOne<ControlEvidenceHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.ControlKind, Id = x.ControlId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.ControlKind, Id = x.ControlId, Revision = x.ControlRevision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ControlStateEvents", t => t.HasCheckConstraint("CK_ControlStateEvents", EventConstraint));
        });
        model.Entity<ControlEvidenceRevision>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }); b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Checksum).HasMaxLength(64);
            b.HasOne<ControlEvidenceHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.FileId, Revision = x.FileRevision, x.Checksum })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id, x.Revision, x.Checksum }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ControlEvidenceRevisions", t => t.HasCheckConstraint("CK_ControlEvidenceRevisions", "\"Revision\" > 0 AND \"Lifecycle\" IN ('DRAFT','SUBMITTED','COMPLETED') AND ((\"FileId\" IS NULL AND \"FileRevision\" IS NULL AND \"Checksum\" IS NULL) OR (\"Kind\" = 'Evidence' AND \"FileId\" IS NOT NULL AND \"FileRevision\" IS NOT NULL AND \"FileRevision\" > 0 AND \"Checksum\" IS NOT NULL))"));
        });
        model.Entity<ControlEvidenceRequirement>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision, x.RequirementId, x.RequirementRevision });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.RequirementKind).HasMaxLength(16); b.Property(x => x.SourceKind).HasMaxLength(16);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegisterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Kind = x.RequirementKind, Id = x.RequirementId, Revision = x.RequirementRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegisterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Kind = x.SourceKind, Id = x.SourceId, Revision = x.SourceRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId, Revision = x.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ControlEvidenceRequirements", t => t.HasCheckConstraint("CK_ControlEvidenceRequirements", "\"RequirementKind\" = 'Requirement' AND \"SourceKind\" = 'Source' AND \"RequirementRevision\" > 0 AND \"SourceRevision\" > 0 AND ((\"RuleId\" IS NULL AND \"RuleRevision\" IS NULL) OR (\"RuleId\" IS NOT NULL AND \"RuleRevision\" IS NOT NULL AND \"RuleRevision\" > 0))"));
        });
        model.Entity<ControlEvidenceEdge>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision, x.TargetKind, x.TargetId, x.TargetRevision });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.TargetKind).HasMaxLength(16);
            b.Property(x => x.TargetVersion).HasColumnType("bigint").HasConversion<long>();
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.TargetKind, Id = x.TargetId, Revision = x.TargetRevision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ControlEvidenceEdges", t => t.HasCheckConstraint("CK_ControlEvidenceEdges", "\"Kind\" = 'Evidence' AND \"TargetKind\" IN ('Control','Evidence') AND \"TargetRevision\" > 0 AND \"Id\" <> \"TargetId\""));
        });
        model.Entity<ControlEvidenceReceipt>(b => {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key }); b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Kind).HasMaxLength(16);
            b.HasOne<ControlEvidenceHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, Id = x.ResourceId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
