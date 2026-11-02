using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class CrewCatalogHead : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; }
    public long? ActiveRevision { get; set; }
    public uint Version { get; set; }
}

// Append-only software governance, never a qualification or operational appointment.
public sealed class CrewCatalogEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; }
    public required string State { get; set; }
    public required string ContentJson { get; set; }
    public required string PolicyVersion { get; set; }
    public required string Hash { get; set; }
    public string? PriorHash { get; set; }
    public Guid ActorId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string AuthorityHash { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

internal static class CrewCatalogStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<CrewCatalogHead>(b => {
            b.ToTable("CatalogHeads", "crew"); b.HasKey(x => new { x.TenantId, x.VesselId });
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewCatalogEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ActiveRevision })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CrewCatalogEvent>(b => {
            b.ToTable("CatalogEvents", "crew"); b.HasKey(x => new { x.TenantId, x.VesselId, x.Revision });
            b.HasOne<CrewCatalogHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.PolicyVersion).HasMaxLength(80);
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.PriorHash).HasMaxLength(64);
            b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Reason).HasMaxLength(1000);
        });
    }
}
