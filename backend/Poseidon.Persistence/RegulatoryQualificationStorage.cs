using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class RegulatoryQualificationHead : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RuleId { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
}
public sealed class RegulatoryQualificationEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RuleId { get; set; }
    public long Revision { get; set; }
    public long RuleRevision { get; set; }
    public required string State { get; set; }
    public required string ContentJson { get; set; }
    public required string SourceHash { get; set; }
    public required string Hash { get; set; }
    public string? PriorHash { get; set; }
    public Guid? SupersedingRuleId { get; set; }
    public long? SupersedingRuleRevision { get; set; }
    public Guid AuthorId { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public required string AuthorityHash { get; set; }
    public required string ContractJson { get; set; }
    public required string PolicyVersion { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class RegulatoryFactAttestation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public long QualificationRevision { get; set; }
    public Guid VesselId { get; set; }
    public long ProfileRevision { get; set; }
    public required string FieldsJson { get; set; }
    public Guid ActorId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string AuthorityHash { get; set; }
    public required string Hash { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
}
public sealed class EvaluationQualification : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid RuleId { get; set; }
    public long QualificationRevision { get; set; }
    public Guid? AttestationId { get; set; }
}
public sealed class RegulatoryQualificationReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public Guid RuleId { get; set; }
    public required string Fingerprint { get; set; }
    public required string AuthorityHash { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public static class RegulatoryQualificationStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RegulatoryQualificationHead>(b => {
            b.HasKey(x => new { x.TenantId, x.RuleId }); b.Property(x => x.Version).IsRowVersion();
            b.HasOne<RuleDraft>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryQualificationEvent>(b => {
            b.HasKey(x => new { x.TenantId, x.RuleId, x.Revision });
            b.HasOne<RegulatoryQualificationHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.RuleId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId, Revision = x.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.SupersedingRuleId, Revision = x.SupersedingRuleRevision }).OnDelete(DeleteBehavior.Restrict);
            b.ToTable("RegulatoryQualificationEvents", t => t.HasCheckConstraint("CK_RegulatoryQualificationEvents", "\"Revision\" > 0 AND \"State\" IN ('DRAFT','IN_REVIEW','ADOPTED','AWAITING','FUTURE_EFFECTIVE','IN_FORCE','SUPERSEDED')"));
        });
        model.Entity<RegulatoryFactAttestation>(b => {
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<RegulatoryQualificationEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.RuleId, Revision = x.QualificationRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegulatoryDraftRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Revision = x.ProfileRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<EvaluationQualification>(b => {
            b.HasKey(x => new { x.TenantId, x.EvaluationId, x.RuleId });
            b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.EvaluationId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegulatoryQualificationEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.RuleId, Revision = x.QualificationRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegulatoryFactAttestation>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.AttestationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryQualificationReceipt>(b => {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key }); b.Property(x => x.Key).HasMaxLength(80);
            b.HasOne<RuleDraft>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
