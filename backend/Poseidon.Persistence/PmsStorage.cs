using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-108: declared measurement point definition (59.04 §9 MeasurementPoint). Revisions are
// append-only snapshots; the head carries xmin concurrency.
public sealed class MeasurementPoint : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string Code { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Source { get; set; } = "MANUAL";
    public int Precision { get; set; }
    public string? RangeMin { get; set; }
    public string? RangeMax { get; set; }
    public Guid? ParentPointId { get; set; }
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public uint Version { get; set; }
    public MeasurementPointRevision Snapshot() => new() { TenantId = TenantId, VesselId = VesselId, PointId = Id, Revision = Revision,
        ContentJson = JsonSerializer.Serialize(new { AssetId, Code, Kind, Unit, Source, Precision, RangeMin, RangeMax, ParentPointId, Status, Provenance }),
        ActorId = ActorId, SavedAt = SavedAt };
}
public sealed class MeasurementPointRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PointId { get; set; }
    public long Revision { get; set; }
    public string ContentJson { get; set; } = "{}";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
// Append-only declared reading. PROPAGATED rows are server-derived sub-counter readings that
// cite their parent reading. Evidence is a reference only, never verified evidence.
public sealed class MeasurementReading : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid PointId { get; set; }
    public Guid AssetId { get; set; }
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
    public DateTimeOffset MeasuredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string SourceType { get; set; } = "MANUAL";
    public string? SourceReference { get; set; }
    public Guid? EvidenceFileId { get; set; }
    public Guid? PreviousReadingId { get; set; }
    public string? Delta { get; set; }
    public Guid? PropagatedFromReadingId { get; set; }
    public Guid ActorId { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
// DEV-109: one postponement request and its single human decision. Defects and work orders are
// read-only id references (owned by other modules); no foreign key is added to their tables.
public sealed class MaintenancePostponement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public long PlanRevision { get; set; }
    public Guid AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string DefectIdsJson { get; set; } = "[]";
    public string RequirementRefsJson { get; set; } = "[]";
    public DateOnly OriginalDue { get; set; }
    public DateOnly RequestedDeadline { get; set; }
    public int ExtensionDays { get; set; }
    public string Reason { get; set; } = "";
    public Guid? RiskAssessmentId { get; set; }
    public string Criticality { get; set; } = "UNKNOWN";
    public string State { get; set; } = "REQUESTED";
    public Guid RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public string? PolicyRef { get; set; }
    public string? PolicyVersion { get; set; }
    public int? MaxExtensionDays { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}
public sealed class PmsCommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public string Action { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid VesselId { get; set; }
    public Guid ResourceId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

internal static class PmsStorage
{
    internal static readonly string[] Operations = ["MeasurementPointSave", "MeasurementReadingRecord", "MaintenancePostpone", "MaintenancePostponementDecide"];

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<MeasurementPoint>(b => {
            b.ToTable("MeasurementPoints", "technical", t => {
                t.HasCheckConstraint("CK_MeasurementPoint_Draft", "\"Status\" = 'Draft'");
                t.HasCheckConstraint("CK_MeasurementPoint_Kind", "\"Kind\" IN ('RUNNING_HOURS','RPM','PRESSURE','TEMPERATURE','VIBRATION')");
                t.HasCheckConstraint("CK_MeasurementPoint_Source", "\"Source\" IN ('MANUAL','DEVICE')");
                t.HasCheckConstraint("CK_MeasurementPoint_Precision", "\"Precision\" BETWEEN 0 AND 6");
                t.HasCheckConstraint("CK_MeasurementPoint_SelfParent", "\"ParentPointId\" IS NULL OR \"ParentPointId\" <> \"Id\"");
                t.HasCheckConstraint("CK_MeasurementPoint_CounterParent", "\"ParentPointId\" IS NULL OR \"Kind\" = 'RUNNING_HOURS'");
                t.HasCheckConstraint("CK_MeasurementPoint_Revision", "\"Revision\" > 0");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.AssetId });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.ParentPointId });
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Kind).HasMaxLength(24); b.Property(x => x.Unit).HasMaxLength(16);
            b.Property(x => x.Source).HasMaxLength(16); b.Property(x => x.RangeMin).HasMaxLength(60); b.Property(x => x.RangeMax).HasMaxLength(60);
            b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.Provenance).HasMaxLength(40);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Asset>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssetId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<MeasurementPoint>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ParentPointId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MeasurementPointRevision>(b => {
            b.ToTable("MeasurementPointRevisions", "technical", t => t.HasCheckConstraint("CK_MeasurementPointRevision_Revision", "\"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.VesselId, x.PointId, x.Revision });
            b.Property(x => x.ContentJson).HasColumnType("jsonb");
            b.HasOne<MeasurementPoint>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PointId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MeasurementReading>(b => {
            b.ToTable("MeasurementReadings", "technical", t => {
                t.HasCheckConstraint("CK_MeasurementReading_Source", "\"SourceType\" IN ('MANUAL','DEVICE','IMPORT','PROPAGATED')");
                t.HasCheckConstraint("CK_MeasurementReading_Propagated", "(\"SourceType\" = 'PROPAGATED') = (\"PropagatedFromReadingId\" IS NOT NULL)");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.PointId, x.MeasuredAt });
            b.Property(x => x.Value).HasMaxLength(60); b.Property(x => x.Unit).HasMaxLength(16); b.Property(x => x.SourceType).HasMaxLength(16);
            b.Property(x => x.SourceReference).HasMaxLength(200); b.Property(x => x.Delta).HasMaxLength(60); b.Property(x => x.Provenance).HasMaxLength(40);
            b.HasOne<MeasurementPoint>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PointId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaintenancePostponement>(b => {
            b.ToTable("Postponements", "technical", t => {
                t.HasCheckConstraint("CK_Postponement_State", "\"State\" IN ('REQUESTED','APPROVED','REJECTED')");
                t.HasCheckConstraint("CK_Postponement_Deadline", "\"RequestedDeadline\" > \"OriginalDue\"");
                t.HasCheckConstraint("CK_Postponement_Criticality", "\"Criticality\" IN ('CRITICAL','IMPORTANT','STANDARD','UNKNOWN')");
                // A decision is a human record by a different person than the requester.
                t.HasCheckConstraint("CK_Postponement_Decision", "(\"State\" = 'REQUESTED' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR " +
                    "(\"State\" <> 'REQUESTED' AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" <> \"RequestedBy\" AND \"DecisionReason\" IS NOT NULL)");
                // An approval always cites the configured policy and stays within its max extension.
                t.HasCheckConstraint("CK_Postponement_ApprovedPolicy", "\"State\" <> 'APPROVED' OR (\"PolicyRef\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND " +
                    "\"MaxExtensionDays\" IS NOT NULL AND \"ExtensionDays\" <= \"MaxExtensionDays\")");
                t.HasCheckConstraint("CK_Postponement_Revision", "\"Revision\" > 0 AND \"PlanRevision\" > 0");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.PlanId });
            b.Property(x => x.DefectIdsJson).HasColumnType("jsonb"); b.Property(x => x.RequirementRefsJson).HasColumnType("jsonb");
            b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.DecisionReason).HasMaxLength(2000);
            b.Property(x => x.Criticality).HasMaxLength(16); b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.PolicyRef).HasMaxLength(80); b.Property(x => x.PolicyVersion).HasMaxLength(80);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<MaintenancePlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PmsCommandReceipt>(b => {
            b.ToTable("PmsCommandReceipts", "technical", t => t.HasCheckConstraint("CK_PmsCommandReceipt_Revision", "\"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Action).HasMaxLength(48); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
    }

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));

    // Called before audit projection: history and outcomes are append-only; a postponement only
    // moves once from REQUESTED to a recorded decision.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is MeasurementPointRevision or MeasurementReading or PmsCommandReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Measurement history, readings and PMS command outcomes are immutable.");
        if (changes.Any(e => e.Entity is MeasurementPoint && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Code", "Kind", "Unit", "Source", "Precision", "RangeMin", "RangeMax", "ParentPointId", "Revision", "ActorId", "SavedAt") ||
             (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Measurement point ownership is immutable; append one revision.");
        if (changes.Any(e => e.Entity is MaintenancePostponement && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            ((string)e.Property("State").OriginalValue! != "REQUESTED" ||
             Changed(e, "State", "DecidedBy", "DecidedAt", "DecisionReason", "PolicyRef", "PolicyVersion", "MaxExtensionDays", "Revision") ||
             (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("A postponement records exactly one decision; requests and decisions are immutable.");
    }
}
