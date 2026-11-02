using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class EvaluationUpstreamReference : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid UpstreamId { get; set; }
    public Guid RuleId { get; set; }
    public long RuleRevision { get; set; }
    internal static void Configure(ModelBuilder model) => model.Entity<EvaluationUpstreamReference>(b => {
        b.ToTable("EvaluationUpstreamReferences", "core");
        b.HasKey(x => new { x.TenantId, x.EvaluationId });
        b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.EvaluationId }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicabilityEvaluation>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Id = x.UpstreamId }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RuleId, Revision = x.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
    });
}
