using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// DEV-040 / 60.03 §10 OverrideDecision. The head is the only mutable row (state, approver, grant and event
// sequence), guarded by xmin; every change appends an ApplicabilityOverrideEvent. The rule, source and the
// recorded evaluation result are never modified: the override pins them by exact id/revision/hash.
// Rationale is private (never copied into audit summaries).
public sealed class ApplicabilityOverride : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid RuleId { get; set; }
    public long RuleRevision { get; set; }
    public Guid? RequirementId { get; set; }
    public long? RequirementRevision { get; set; }
    public Guid? SourceId { get; set; }
    public long? SourceRevision { get; set; }
    public required string BaseResult { get; set; }
    public required string InputHash { get; set; }
    public required string RuleSetHash { get; set; }
    public required string Purpose { get; set; }
    public required string Action { get; set; }
    public required string ReasonCode { get; set; }
    public required string Rationale { get; set; }
    public required string EvidenceIdsJson { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ReviewAt { get; set; }
    public Guid AuthorId { get; set; }
    public string? AuthorRoleContext { get; set; }
    public Guid? ApproverId { get; set; }
    public Guid? AuthorityGrantId { get; set; }
    public required string State { get; set; }
    public long Sequence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class ApplicabilityOverrideEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid OverrideId { get; set; }
    public long Sequence { get; set; }
    public string? FromState { get; set; }
    public required string ToState { get; set; }
    public required string EventCode { get; set; }
    public string? Rationale { get; set; }
    public Guid ActorId { get; set; }
    public string? RoleContext { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
// Idempotency receipt of the S5 compliance commands (override propose/decide, control candidate
// generation). Append-only; the outcome replays only after current authorization is re-checked.
public sealed class ComplianceCommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Operation { get; set; }
    public Guid ResourceId { get; set; }
    public required string Fingerprint { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public static class ApplicabilityOverrideStorage
{
    internal const string StateList = "'DRAFT','IN_REVIEW','ACTIVE','REVOKED','EXPIRED','SUPERSEDED'";
    internal const string HeadConstraint = "\"State\" IN (" + StateList + ") AND \"Sequence\" > 0 AND \"RuleRevision\" > 0 AND " +
        "\"Purpose\" = 'COMPLIANCE_REVIEW' AND \"Action\" IN ('ACCEPT_INTERPRETATION','REQUIRE_ADDITIONAL_CONTROL') AND " +
        "((\"Action\" = 'ACCEPT_INTERPRETATION' AND \"ReasonCode\" IN ('VERIFIED_LEGAL_INTERPRETATION','FLAG_STATE_EXEMPTION_REFERENCE','CLASS_EXEMPTION_REFERENCE')) OR " +
        "(\"Action\" = 'REQUIRE_ADDITIONAL_CONTROL' AND \"ReasonCode\" IN ('ADDITIONAL_PRECAUTION','SOURCE_CONFLICT_PRECAUTION'))) AND " +
        "\"ExpiresAt\" > \"ValidFrom\" AND \"ReviewAt\" <= \"ExpiresAt\" AND char_length(\"Rationale\") BETWEEN 20 AND 4000 AND " +
        "(\"State\" <> 'ACTIVE' OR (\"ApproverId\" IS NOT NULL AND \"AuthorityGrantId\" IS NOT NULL AND \"ApproverId\" <> \"AuthorId\")) AND " +
        "(\"ApproverId\" IS NULL OR \"ApproverId\" <> \"AuthorId\")";
    internal const string EventConstraint = "\"Sequence\" > 0 AND \"ToState\" IN (" + StateList + ") AND " +
        "((\"EventCode\" = 'PROPOSE' AND \"FromState\" IS NULL AND \"ToState\" = 'IN_REVIEW') OR " +
        "(\"EventCode\" = 'APPROVE' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'ACTIVE') OR " +
        "(\"EventCode\" = 'REJECT' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'REVOKED') OR " +
        "(\"EventCode\" = 'WITHDRAW' AND \"FromState\" IN ('DRAFT','IN_REVIEW') AND \"ToState\" = 'REVOKED') OR " +
        "(\"EventCode\" = 'REVOKE' AND \"FromState\" = 'ACTIVE' AND \"ToState\" = 'REVOKED'))";
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<ApplicabilityOverride>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.BaseResult).HasMaxLength(32); b.Property(x => x.InputHash).HasMaxLength(64); b.Property(x => x.RuleSetHash).HasMaxLength(64);
            b.Property(x => x.Purpose).HasMaxLength(32); b.Property(x => x.Action).HasMaxLength(32); b.Property(x => x.ReasonCode).HasMaxLength(48);
            b.Property(x => x.Rationale).HasMaxLength(4000); b.Property(x => x.EvidenceIdsJson).HasMaxLength(1024); b.Property(x => x.State).HasMaxLength(16);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.RuleId, x.Purpose, x.State });
            b.ToTable("ApplicabilityOverrides", t => t.HasCheckConstraint("CK_ApplicabilityOverrides", HeadConstraint));
        });
        model.Entity<ApplicabilityOverrideEvent>(b => {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.OverrideId, x.Sequence });
            b.Property(x => x.FromState).HasMaxLength(16); b.Property(x => x.ToState).HasMaxLength(16); b.Property(x => x.EventCode).HasMaxLength(16);
            b.Property(x => x.Rationale).HasMaxLength(4000);
            b.HasOne<ApplicabilityOverride>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.OverrideId }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ApplicabilityOverrideEvents", t => t.HasCheckConstraint("CK_ApplicabilityOverrideEvents", EventConstraint));
        });
        model.Entity<ComplianceCommandReceipt>(b => {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Operation).HasMaxLength(48); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("ComplianceCommandReceipts", t => t.HasCheckConstraint("CK_ComplianceCommandReceipts",
                "\"Operation\" IN ('ApplicabilityOverridePropose','ApplicabilityOverrideDecision','ComplianceControlCandidates')"));
        });
    }
}
