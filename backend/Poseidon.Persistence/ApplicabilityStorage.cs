using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class RuleDraft : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
}
public sealed class RuleRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public required string Contract { get; set; }
    public string? RuleJson { get; set; }
    public required string Hash { get; set; }
    public string? SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public long? SourceRevision { get; set; }
    public string? RequirementKind { get; set; }
    public Guid? RequirementId { get; set; }
    public long? RequirementRevision { get; set; }
    public Guid ActorId { get; set; }
    public required string RoleContext { get; set; }
    public required string ReasonCode { get; set; }
    public required string RequestedLifecycle { get; set; }
    public string Lifecycle { get; set; } = "DRAFT";
    public string ReviewStatus { get; set; } = "NOT_REQUESTED";
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class ApplicabilityEvaluation : ITenantEntity
{
    public bool IncludesProtectedInputs { get; set; }
    public bool IncludesOwningInputs { get; set; }
    public bool IncludesOperationInputs { get; set; }
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public long? ProfileRevision { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset AsOf { get; set; }
    public required string AdapterVersion { get; set; }
    public required string SnapshotJson { get; set; }
    public required string SnapshotHash { get; set; }
    public required string ResultJson { get; set; }
    public required string RuleSetHash { get; set; }
}
public sealed class EvaluationRule : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid RuleId { get; set; }
    public long RuleRevision { get; set; }
}
// Exact outcome bytes/xmin are committed atomically. No current-object reconstruction.
public sealed class ApplicabilityReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? RuleResourceId { get; set; }
    public Guid? EvaluationResourceId { get; set; }
    public Guid? VesselId { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public static class ApplicabilityStorage
{
    public static string Hash(RuleRevision r) => RegisterStorage.Hash(System.Text.Json.JsonSerializer.Serialize(new {
        r.Contract, r.RuleJson, r.SourceId, r.SourceRevision, r.RequirementId, r.RequirementRevision, r.RequestedLifecycle, r.Lifecycle, r.ReviewStatus
    }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RuleDraft>(b => {
            b.HasKey(x => new { x.TenantId, x.Id }); b.Property(x => x.Version).IsRowVersion();
            b.ToTable("RuleDrafts", t => t.HasCheckConstraint("CK_RuleDrafts_Revision", "\"Revision\" > 0"));
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RuleRevision>(b => {
            b.HasKey(x => new { x.TenantId, x.Id, x.Revision });
            b.HasOne<RuleDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.SourceKind).HasMaxLength(16); b.Property(x => x.RequirementKind).HasMaxLength(16);
            b.HasOne<RegisterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SourceKind, x.SourceId, x.SourceRevision }).HasPrincipalKey(x => new { x.TenantId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegisterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequirementKind, x.RequirementId, x.RequirementRevision }).HasPrincipalKey(x => new { x.TenantId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("RuleRevisions", t => t.HasCheckConstraint("CK_RuleRevisions_Admission", "\"Revision\" > 0 AND \"Lifecycle\" = 'DRAFT' AND \"ReviewStatus\" IN ('NOT_REQUESTED','BLOCKED_SOURCE_ADMISSION') AND ((\"SourceKind\" IS NULL AND \"SourceId\" IS NULL AND \"SourceRevision\" IS NULL) OR (\"SourceKind\" IS NOT NULL AND \"SourceKind\" = 'Source' AND \"SourceId\" IS NOT NULL AND \"SourceRevision\" IS NOT NULL AND \"SourceRevision\" > 0)) AND ((\"RequirementKind\" IS NULL AND \"RequirementId\" IS NULL AND \"RequirementRevision\" IS NULL) OR (\"RequirementKind\" IS NOT NULL AND \"RequirementKind\" = 'Requirement' AND \"RequirementId\" IS NOT NULL AND \"RequirementRevision\" IS NOT NULL AND \"RequirementRevision\" > 0))"));
        });
        model.Entity<ApplicabilityEvaluation>(b => {
            b.Property(x => x.IncludesProtectedInputs).HasDefaultValue(false);
            b.Property(x => x.IncludesOwningInputs).HasDefaultValue(false); b.Property(x => x.IncludesOperationInputs).HasDefaultValue(false);
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegulatoryDraftRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ProfileRevision }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<EvaluationRule>(b => {
            b.HasKey(x => new { x.TenantId, x.EvaluationId, x.RuleId });
            b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, x.EvaluationId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId, Revision = x.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ApplicabilityReceipt>(b => {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key }); b.Property(x => x.Key).HasMaxLength(80);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleDraft>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleResourceId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.EvaluationResourceId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
