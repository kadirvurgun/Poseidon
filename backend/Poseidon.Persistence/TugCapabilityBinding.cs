using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Relational ownership of typed evidence. Optional references permit incomplete drafts.
// The ordinary evidence head/revision/receipt transaction owns every insert.
public sealed class TugCapabilityBinding : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public string Kind { get; set; } = "Evidence";
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public Guid? AssetId { get; set; }
    public long? AssetRevision { get; set; }
    public uint? ProfileVersion { get; set; }
    public string ConditionKind { get; set; } = "Evidence";
    public Guid? ConditionId { get; set; }
    public long? ConditionRevision { get; set; }
    public uint? ConditionVersion { get; set; }
    internal static void Configure(ModelBuilder model) {
        model.Entity<TugCapabilityBinding>(b => {
            b.ToTable("CapabilityBindings", "tug", t => t.HasCheckConstraint("CK_TugCapabilityBinding", "\"Kind\" = 'Evidence' AND \"ConditionKind\" = 'Evidence' AND ((\"AssetId\" IS NULL AND \"AssetRevision\" IS NULL) OR (\"AssetId\" IS NOT NULL AND \"AssetRevision\" IS NOT NULL AND \"AssetRevision\" > 0)) AND ((\"ConditionId\" IS NULL AND \"ConditionRevision\" IS NULL AND \"ConditionVersion\" IS NULL) OR (\"ConditionId\" IS NOT NULL AND \"ConditionRevision\" IS NOT NULL AND \"ConditionVersion\" IS NOT NULL AND \"ConditionRevision\" > 0 AND \"ConditionVersion\" > 0 AND \"ConditionId\" <> \"Id\"))"));
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.ConditionKind).HasMaxLength(16);
            b.Property(x => x.ProfileVersion).HasConversion<long?>(); b.Property(x => x.ConditionVersion).HasConversion<long?>();
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId, Revision = x.AssetRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugProfile>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, Kind = x.ConditionKind, Id = x.ConditionId, Revision = x.ConditionRevision }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
