using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S10 WP-A DEV-064 audit pack: insert-only manifest + pinned items. A new compilation is a new manifest.
public sealed class AuditPackManifest : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public string Label { get; set; } = AuditPackPolicy.Label;
    public string SourceVersion { get; set; } = AuditPackPolicy.SourceVersion;
    public DateTimeOffset AsOf { get; set; }
    public string ManifestHash { get; set; } = "";
    public int ItemCount { get; set; }
    public int UnavailableChains { get; set; }
    public bool Truncated { get; set; }
    public Guid? PreviousPackId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class AuditPackItem : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PackId { get; set; }
    public int Ordinal { get; set; }
    public string Kind { get; set; } = "";
    public Guid ResourceId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = "";
    public string? Hash { get; set; }
    public Guid EvidenceId { get; set; }
    public Guid ControlId { get; set; }
    public DateTimeOffset? SavedAt { get; set; }
    public Guid? FileId { get; set; }
    public long? FileRevision { get; set; }
}

// S10 WP-A DEV-113 versioned import mapping (tenant configuration). A change is a new version row; rows never change.
public sealed class AssuranceImportMapping : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string SourceVersion { get; set; } = AssurancePolicy.SourceVersion;
    public string BindingsJson { get; set; } = "[]";
    public string Hash { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class AssuranceInspection : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public string Programme { get; set; } = AssurancePolicy.Programme;
    public string ReferenceNo { get; set; } = "";
    public DateOnly InspectionDate { get; set; }
    public string State { get; set; } = "DRAFT";
    public string Classification { get; set; } = AssurancePolicy.Unknown;
    public string Applicability { get; set; } = AssurancePolicy.ReviewRequired;
    public long Revision { get; set; } = 1;
    public string Provenance { get; set; } = "ImportedUnverified";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public uint Version { get; set; }
}
// One staged import. PENDING_REVIEW until a different human accepts or rejects it; the diff is pinned by hash.
public sealed class AssuranceImport : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public Guid MappingId { get; set; }
    public int MappingVersion { get; set; }
    public string MappingHash { get; set; } = "";
    public string SourceVersion { get; set; } = AssurancePolicy.SourceVersion;
    public string TableHash { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public string DiffJson { get; set; } = "[]";
    public string DiffHash { get; set; } = "";
    public string State { get; set; } = "PENDING_REVIEW";
    public Guid ImportedBy { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionJson { get; set; }
    public string? Reason { get; set; }
    public uint Version { get; set; }
}
// Append-only item revisions; the current value is the highest revision. Written only by a human acceptance.
public sealed class AssuranceInspectionItem : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid InspectionId { get; set; }
    public string QuestionRef { get; set; } = "";
    public long Revision { get; set; }
    public string ChangeKind { get; set; } = AssurancePolicy.New;
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid ImportId { get; set; }
    public Guid AcceptedBy { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
// A finding is always OPEN and UNVERIFIED here: no import, re-import or link closes or verifies it (DB CHECK).
public sealed class AssuranceFinding : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public string QuestionRef { get; set; } = "";
    public long FirstItemRevision { get; set; }
    public long LatestItemRevision { get; set; }
    public bool ObservationPresent { get; set; } = true;
    public string State { get; set; } = "OPEN";
    public string Verification { get; set; } = "UNVERIFIED";
    public Guid? ActionId { get; set; }
    public string ActionLinkState { get; set; } = "UNLINKED";
    public Guid? LinkedBy { get; set; }
    public DateTimeOffset? LinkedAt { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
// Field photo/video evidence (CLEAN SecureFile pin). Captured offline or online; never verified by capture.
public sealed class AssuranceEvidence : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid InspectionId { get; set; }
    public string QuestionRef { get; set; } = "";
    public Guid FileId { get; set; }
    public long FileRevision { get; set; }
    public string Checksum { get; set; } = "";
    public string MediaKind { get; set; } = "PHOTO";
    public DateTimeOffset CapturedAt { get; set; }
    public string Origin { get; set; } = "ONLINE";
    public string Verification { get; set; } = "UNVERIFIED";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class AssuranceReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? VesselId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public string Action { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResourceId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class AssuranceStorage
{
    public static readonly string[] ReceiptActions = ["AuditPackCreate", "AssuranceMappingCreate", "AssuranceImportStage", "AssuranceImportDecide",
        "AssuranceEvidenceCapture", "AssuranceFindingLink"];
    private static string In(IEnumerable<string> values) => string.Join(",", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AuditPackManifest>(b => {
            b.ToTable("AuditPackManifests", "assurance", t => t.HasCheckConstraint("CK_AuditPackManifest_Label",
                "\"Label\"='" + AuditPackPolicy.Label + "' AND \"ItemCount\">=0 AND \"ItemCount\"<=" + AuditPackPolicy.MaxItems + " AND \"UnavailableChains\">=0 AND " +
                "\"ManifestHash\" ~ '^[A-F0-9]{64}$' AND (\"PreviousPackId\" IS NULL OR \"PreviousPackId\"<>\"Id\")"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.CreatedAt });
            b.Property(x => x.Label).HasMaxLength(48); b.Property(x => x.SourceVersion).HasMaxLength(192); b.Property(x => x.ManifestHash).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_AuditPackManifests_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuditPackItem>(b => {
            b.ToTable("AuditPackItems", "assurance", t => t.HasCheckConstraint("CK_AuditPackItem_Kind",
                "\"Ordinal\">=0 AND \"Revision\">=0 AND \"Kind\" IN (" + In(AuditPackPolicy.Kinds) + ") AND \"State\" IN (" + In(AuditPackPolicy.States) + ") AND " +
                "((\"State\"='Unavailable') = (\"Hash\" IS NULL)) AND ((\"FileId\" IS NULL) = (\"FileRevision\" IS NULL)) AND " +
                "(\"State\"<>'Recorded' OR \"Kind\" IN ('Verification','Decision')) AND (\"State\"<>'Superseded' OR \"Kind\"='Evidence')"));
            b.HasKey(x => new { x.TenantId, x.PackId, x.Ordinal });
            b.HasOne<AuditPackManifest>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PackId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_AuditPackItems_Manifest").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.Hash).HasMaxLength(64);
        });
        model.Entity<AssuranceImportMapping>(b => {
            b.ToTable("AssuranceImportMappings", "assurance", t => t.HasCheckConstraint("CK_AssuranceImportMapping_Version",
                "\"Version\">0 AND length(\"Key\") BETWEEN 1 AND 64 AND \"Hash\" ~ '^[A-F0-9]{64}$'"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.Key, x.Version }).IsUnique();
            b.Property(x => x.Key).HasMaxLength(64); b.Property(x => x.SourceVersion).HasMaxLength(192); b.Property(x => x.BindingsJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64);
        });
        model.Entity<AssuranceInspection>(b => {
            b.ToTable("AssuranceInspections", "assurance", t => t.HasCheckConstraint("CK_AssuranceInspection_State",
                "\"Revision\">0 AND \"Programme\"='" + AssurancePolicy.Programme + "' AND \"State\" IN (" + In(AssurancePolicy.InspectionStates) + ") AND " +
                "\"Classification\" IN ('TANKER','NOT_TANKER','UNKNOWN') AND \"Applicability\" IN ('APPLICABLE','REVIEW_REQUIRED') AND " +
                "(\"Classification\"<>'NOT_TANKER') AND ((\"Classification\"='TANKER') = (\"Applicability\"='APPLICABLE')) AND " +
                "\"Provenance\" IN ('ImportedUnverified','SyntheticUnverified') AND length(\"ReferenceNo\") BETWEEN 1 AND 64"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.ReferenceNo }).IsUnique();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Programme).HasMaxLength(16); b.Property(x => x.ReferenceNo).HasMaxLength(64); b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.Classification).HasMaxLength(16); b.Property(x => x.Applicability).HasMaxLength(16); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_AssuranceInspections_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AssuranceImport>(b => {
            b.ToTable("AssuranceImports", "assurance", t => t.HasCheckConstraint("CK_AssuranceImport_Decision",
                "\"State\" IN (" + In(AssurancePolicy.ImportStates) + ") AND \"MappingVersion\">0 AND \"DiffHash\" ~ '^[A-F0-9]{64}$' AND " +
                "((\"State\"='PENDING_REVIEW') = (\"DecidedBy\" IS NULL)) AND ((\"DecidedBy\" IS NULL) = (\"DecidedAt\" IS NULL)) AND " +
                "(\"DecidedBy\" IS NULL OR \"DecidedBy\"<>\"ImportedBy\" AND length(\"Reason\")>=10)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.InspectionId, x.State });
            // At most one staged import per inspection waits for review at a time.
            b.HasIndex(x => new { x.TenantId, x.InspectionId }).IsUnique().HasFilter("\"State\" = 'PENDING_REVIEW'").HasDatabaseName("IX_AssuranceImports_OnePending");
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.MappingHash).HasMaxLength(64); b.Property(x => x.SourceVersion).HasMaxLength(192); b.Property(x => x.TableHash).HasMaxLength(64);
            b.Property(x => x.ItemsJson).HasColumnType("text"); b.Property(x => x.DiffJson).HasColumnType("text"); b.Property(x => x.DiffHash).HasMaxLength(64);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.DecisionJson).HasColumnType("text"); b.Property(x => x.Reason).HasMaxLength(2000);
            Inspection(b, "AssuranceImports");
            b.HasOne<AssuranceImportMapping>().WithMany().HasForeignKey(x => new { x.TenantId, x.MappingId }).HasConstraintName("FK_AssuranceImports_Mapping").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AssuranceInspectionItem>(b => {
            b.ToTable("AssuranceInspectionItems", "assurance", t => t.HasCheckConstraint("CK_AssuranceInspectionItem_Change",
                "\"Revision\">0 AND \"ChangeKind\" IN ('NEW','CHANGED') AND (\"ChangeKind\"='NEW') = (\"Revision\"=1) AND \"Hash\" ~ '^[A-F0-9]{64}$'"));
            b.HasKey(x => new { x.TenantId, x.InspectionId, x.QuestionRef, x.Revision });
            b.Property(x => x.QuestionRef).HasMaxLength(16); b.Property(x => x.ChangeKind).HasMaxLength(8); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64);
            Inspection(b, "AssuranceInspectionItems");
        });
        model.Entity<AssuranceFinding>(b => {
            b.ToTable("AssuranceFindings", "assurance", t => t.HasCheckConstraint("CK_AssuranceFinding_Open",
                "\"Revision\">0 AND \"State\"='OPEN' AND \"Verification\"='UNVERIFIED' AND \"LatestItemRevision\">=\"FirstItemRevision\" AND " +
                "\"ActionLinkState\" IN ('UNLINKED','LINKED') AND ((\"ActionLinkState\"='LINKED') = (\"ActionId\" IS NOT NULL)) AND " +
                "((\"ActionId\" IS NULL) = (\"LinkedBy\" IS NULL))"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.InspectionId, x.QuestionRef }).IsUnique();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.QuestionRef).HasMaxLength(16); b.Property(x => x.State).HasMaxLength(8); b.Property(x => x.Verification).HasMaxLength(16);
            b.Property(x => x.ActionLinkState).HasMaxLength(10);
            Inspection(b, "AssuranceFindings");
            b.HasOne<SafetyAction>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ActionId }).HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id })
                .IsRequired(false).HasConstraintName("FK_AssuranceFindings_SafetyAction").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AssuranceEvidence>(b => {
            b.ToTable("AssuranceEvidence", "assurance", t => t.HasCheckConstraint("CK_AssuranceEvidence_Unverified",
                "\"Verification\"='UNVERIFIED' AND \"MediaKind\" IN ('PHOTO','VIDEO') AND \"Origin\" IN ('ONLINE','OFFLINE') AND \"FileRevision\">0 AND " +
                "\"Checksum\" ~ '^[A-Fa-f0-9]{64}$' AND \"CapturedAt\"<=\"CreatedAt\""));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.InspectionId, x.QuestionRef });
            b.Property(x => x.QuestionRef).HasMaxLength(16); b.Property(x => x.Checksum).HasMaxLength(64); b.Property(x => x.MediaKind).HasMaxLength(8);
            b.Property(x => x.Origin).HasMaxLength(8); b.Property(x => x.Verification).HasMaxLength(16);
            Inspection(b, "AssuranceEvidence");
        });
        model.Entity<AssuranceReceipt>(b => {
            b.ToTable("AssuranceReceipts", "assurance", t => t.HasCheckConstraint("CK_AssuranceReceipt_Action", "\"Action\" IN (" + In(ReceiptActions) + ") AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
    }

    private static void Inspection<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> b, string table) where T : class =>
        b.HasOne<AssuranceInspection>().WithMany().HasForeignKey("TenantId", "VesselId", "InspectionId")
            .HasPrincipalKey(nameof(AssuranceInspection.TenantId), nameof(AssuranceInspection.VesselId), nameof(AssuranceInspection.Id))
            .HasConstraintName("FK_" + table + "_Inspection").OnDelete(DeleteBehavior.Restrict);

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static bool OneStep(EntityEntry e) => (long)e.Property("Revision").CurrentValue! == (long)e.Property("Revision").OriginalValue! + 1;

    // Called by PoseidonDbContext before audit projection: manifests, pack items, mappings, item revisions, evidence and
    // receipts are insert-only; an import is decided once; a finding only gains its action link once and never closes.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is AuditPackManifest or AuditPackItem or AssuranceImportMapping or AssuranceInspectionItem or AssuranceEvidence or AssuranceReceipt
                && e.State != EntityState.Added))
            throw new InvalidOperationException("Audit packs, mappings, item revisions, evidence and receipts are immutable.");
        if (changes.Any(e => e.Entity is AssuranceInspection && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Assurance inspection identity is immutable; advance one revision.");
        if (changes.Any(e => e.Entity is AssuranceImport && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "State", "DecidedBy", "DecidedAt", "DecisionJson", "Reason") || (string)e.Property("State").OriginalValue! != "PENDING_REVIEW"))))
            throw new InvalidOperationException("A staged assurance import is decided once and its content is immutable.");
        if (changes.Any(e => e.Entity is AssuranceFinding && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "LatestItemRevision", "ObservationPresent", "ActionId", "ActionLinkState", "LinkedBy", "LinkedAt", "Revision") || !OneStep(e) ||
             e.Property("ActionId").IsModified && e.Property("ActionId").OriginalValue != null))))
            throw new InvalidOperationException("Assurance findings are never closed or verified here; the action link is set once.");
    }
}
