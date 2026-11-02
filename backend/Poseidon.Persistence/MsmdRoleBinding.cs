using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class MsmdRoleBinding : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "Evidence";
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public Guid PositionId { get; set; }
    public int Count { get; set; }
    public required string SourceLocator { get; set; }
    internal static void Configure(ModelBuilder model) => model.Entity<MsmdRoleBinding>(b => {
        b.ToTable("MsmdRoleBindings", "core", t => t.HasCheckConstraint("CK_MsmdRole", "\"Kind\" = 'Evidence' AND \"Revision\" > 0 AND \"Count\" BETWEEN 1 AND 1000"));
        b.HasKey(x => new { x.TenantId, x.VesselId, x.Id, x.Revision, x.PositionId });
        b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.SourceLocator).HasMaxLength(2000);
        b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CrewPosition>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PositionId, x.VesselId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
    });
}
