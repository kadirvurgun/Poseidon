using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// Asset owns the common System/Equipment/Component identity, not a table per screen alias.
public sealed class Asset : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string Level { get; set; } = "Equipment";
    public string? SystemFamily { get; set; }
    public string? EquipmentType { get; set; }
    public string? Criticality { get; set; }
    public string? Maker { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public DateOnly? CommissionDate { get; set; }
    public Guid? ResponsibleRoleId { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public AssetRevision Snapshot() => new() { TenantId = TenantId, VesselId = VesselId, AssetId = Id,
        Revision = Revision, ContentJson = JsonSerializer.Serialize(this), SavedAt = DateTimeOffset.UtcNow };
}
public sealed class AssetRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssetId { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
// Role IDs come exclusively from eligible server-provisioned memberships, not labels/claims.
public sealed class TechnicalOwner : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
}
public sealed class MaintenanceJob : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
public sealed class MaintenancePlan : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid? JobId { get; set; }
    public Guid? OwnerRoleId { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public MaintenancePlanRevision Snapshot(DateTimeOffset reference, long assetRevision) {
        var inputs = JsonSerializer.Deserialize<MaintenanceInputs>(ContentJson)!;
        var due = MaintenanceCalculation.Evaluate(inputs, reference);
        if (JobId == null || OwnerRoleId == null || (string.IsNullOrWhiteSpace(inputs.Criticality) || inputs.Criticality == "Unknown"))
            due = due with { State = "ReviewRequired", Reason = due.State == "ReviewRequired" ? due.Reason : "IncompletePlanContext" };
        return new() { TenantId = TenantId, VesselId = VesselId, PlanId = Id, AssetId = AssetId,
            AssetRevision = assetRevision, JobId = JobId, OwnerRoleId = OwnerRoleId, Revision = Revision,
            ContentJson = ContentJson, CalculationJson = JsonSerializer.Serialize(due),
            ReferenceAt = reference, SavedAt = DateTimeOffset.UtcNow, Provenance = Provenance };
    }
}
public sealed class MaintenancePlanRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PlanId { get; set; }
    public Guid AssetId { get; set; }
    public long AssetRevision { get; set; }
    public Guid? JobId { get; set; }
    public Guid? OwnerRoleId { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public required string CalculationJson { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset ReferenceAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

internal static class TechnicalStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<TechnicalOwner>(b => {
            b.ToTable("Owners", "technical"); b.HasKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Asset>(b => {
            b.HasIndex("TenantId", "VesselId");
            b.HasIndex("TenantId", "VesselId", "ParentId");
            b.HasIndex("TenantId", "VesselId", "ResponsibleRoleId");

            b.ToTable("Assets", "technical", t => {
                t.HasCheckConstraint("CK_Asset_SelfParent", "\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\"");
                t.HasCheckConstraint("CK_Asset_Level", "\"Level\" IN ('System','Equipment','Component')");
                t.HasCheckConstraint("CK_Asset_Draft", "\"Status\" = 'Draft'");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Code }).IsUnique();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ParentId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TechnicalOwner>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ResponsibleRoleId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<AssetRevision>(b => {
            b.ToTable("AssetRevisions", "technical"); b.HasKey(x => new { x.TenantId, x.VesselId, x.AssetId, x.Revision });
            b.Property(x => x.ContentJson).HasColumnType("jsonb");
            b.HasOne<Asset>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaintenanceJob>(b => {
            b.ToTable("Jobs", "technical"); b.HasKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Code }).IsUnique();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaintenancePlan>(b => {
            b.HasIndex("TenantId", "VesselId", "AssetId");
            b.HasIndex("TenantId", "VesselId", "JobId");
            b.HasIndex("TenantId", "VesselId", "OwnerRoleId");

            b.ToTable("Plans", "technical", t => t.HasCheckConstraint("CK_Plan_Draft", "\"Status\" = 'Draft'"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.Property(x => x.Version).IsRowVersion(); b.Property(x => x.ContentJson).HasColumnType("jsonb");
            b.HasOne<Asset>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<MaintenanceJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TechnicalOwner>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.OwnerRoleId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaintenancePlanRevision>(b => {
            b.HasIndex("TenantId", "VesselId", "AssetId", "AssetRevision");
            b.HasIndex("TenantId", "VesselId", "JobId");
            b.HasIndex("TenantId", "VesselId", "OwnerRoleId");

            b.ToTable("PlanRevisions", "technical"); b.HasKey(x => new { x.TenantId, x.VesselId, x.PlanId, x.Revision });
            b.Property(x => x.ContentJson).HasColumnType("jsonb"); b.Property(x => x.CalculationJson).HasColumnType("jsonb");
            b.HasOne<MaintenancePlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<AssetRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId, x.AssetRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<MaintenanceJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TechnicalOwner>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.OwnerRoleId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
