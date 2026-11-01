using Microsoft.EntityFrameworkCore;
namespace Poseidon.Persistence;
// Append-only observed declarations, never a verified WorkOrder or fitness decision.
public sealed class MaintenanceObservation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssetId { get; set; }
    public required string Source { get; set; }
    public required string PolicyVersion { get; set; }
    public long SourceRevision { get; set; }
    public required string Kind { get; set; }
    public required string Unit { get; set; }
    public string? Value { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
public sealed class MaintenanceObservationRevocation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ObservationId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public static class MaintenanceObservationStorage
{
    public static void Configure(ModelBuilder model) {
        model.Entity<MaintenanceObservation>(b => {
            b.ToTable("Observations", "technical"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.AssetId, x.Id }).HasName("AK_Observation_Scope");
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.AssetId, x.Source, x.SourceRevision }).IsUnique().HasDatabaseName("IX_Observations_SourceRevision");
            b.HasOne<Asset>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Source).HasMaxLength(80); b.Property(x => x.PolicyVersion).HasMaxLength(80);
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Unit).HasMaxLength(40);
            b.Property(x => x.Provenance).HasMaxLength(40); b.Property(x => x.Value).HasMaxLength(1024);
        });
        model.Entity<MaintenanceObservationRevocation>(b => {
            b.ToTable("ObservationRevocations", "technical"); b.HasKey(x => new { x.TenantId, x.ObservationId });
            b.HasOne<MaintenanceObservation>().WithMany().HasForeignKey(x => new { x.TenantId, x.ObservationId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
