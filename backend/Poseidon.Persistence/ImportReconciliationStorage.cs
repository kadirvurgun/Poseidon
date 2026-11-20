using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S11 WP-A DEV-069 closure: insert-only row-level lineage written inside the import commit transaction for every
// record the commit created or updated. Identifiers, hashes and revisions only: no source cell values.
public sealed class ImportProvenance : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid BatchId { get; set; }
    public Guid PreviewId { get; set; }
    public int RowNumber { get; set; }
    public string Category { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string TemplateVersion { get; set; } = "";
    public string TemplateFingerprint { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public Guid TargetId { get; set; }
    // Owning revision after the commit; Vessel has no revision column, so its row version is recorded instead.
    public long TargetRevision { get; set; }
    public string RevisionKind { get; set; } = "Revision";
    public string Operation { get; set; } = "CREATE";
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// DEV-070 immutable snapshot. Only the lifecycle columns (State, Revision, SignOffId, SupersededAt) may change.
public sealed class ReconciliationReport : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = ImportReconciliationPolicy.AwaitingSignOff;
    public string PolicyVersion { get; set; } = ImportReconciliationPolicy.PolicyVersion;
    public string BatchesJson { get; set; } = "[]";
    public int SourceCount { get; set; }
    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int NotCommittedCount { get; set; }
    public int MissingCriticalCount { get; set; }
    public string MissingCriticalJson { get; set; } = "[]";
    public string SampleSeed { get; set; } = "";
    public string SampleJson { get; set; } = "[]";
    public string Hash { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SignOffId { get; set; }
    public Guid? SignedBy { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}

// Insert-only human record. Never seeded; a DB CHECK keeps the signer apart from the report author.
public sealed class ReconciliationSignOff : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string ReportHash { get; set; } = "";
    public Guid ReportAuthor { get; set; }
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ExceptionsJson { get; set; } = "[]";
    public string SampleVerifiedJson { get; set; } = "[]";
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = ImportReconciliationPolicy.DataOwnerDuty;
    public string AuthorityHash { get; set; } = "";
    public Guid SignedBy { get; set; }
    public DateTimeOffset SignedAt { get; set; }
}

public sealed class ImportReconciliationReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public string Action { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid ResourceId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class ImportReconciliationStorage
{
    public static readonly string[] ReceiptActions = ["ReconciliationReportCreate", "ReconciliationSignOff"];
    public static readonly string[] Operations = ["CREATE", "UPDATE"];
    public static readonly string[] RevisionKinds = ["Revision", "RowVersion"];
    private static string In(IEnumerable<string> values) => string.Join(", ", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model) {
        model.Entity<ImportProvenance>(b => {
            b.ToTable("ImportProvenance", "integration", t => t.HasCheckConstraint("CK_ImportProvenance_Kind",
                "\"RowNumber\" > 1 AND \"TargetRevision\" > 0 AND \"TargetKind\" IN (" + In(ImportReconciliationPolicy.TargetKinds) + ") AND \"Operation\" IN (" + In(Operations) +
                ") AND \"RevisionKind\" IN (" + In(RevisionKinds) + ") AND \"Category\" IN (" + In(ImportReconciliationPolicy.Categories) + ")"));
            b.HasKey(x => new { x.TenantId, x.BatchId, x.RowNumber });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.TargetKind, x.TargetId });
            b.HasIndex(x => new { x.TenantId, x.BatchId, x.VesselId });
            b.HasIndex(x => new { x.TenantId, x.PreviewId, x.VesselId });
            b.Property(x => x.Category).HasMaxLength(16); b.Property(x => x.SourceHash).HasMaxLength(64); b.Property(x => x.TemplateVersion).HasMaxLength(64);
            b.Property(x => x.TemplateFingerprint).HasMaxLength(128); b.Property(x => x.TargetKind).HasMaxLength(32); b.Property(x => x.RevisionKind).HasMaxLength(16);
            b.Property(x => x.Operation).HasMaxLength(8);
            b.HasOne<ImportBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BatchId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ImportProvenance_ImportBatches_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ImportPreview>().WithMany().HasForeignKey(x => new { x.TenantId, x.PreviewId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ImportProvenance_ImportPreviews_Scope").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ReconciliationReport>(b => {
            b.ToTable("ReconciliationReports", "integration", t => t.HasCheckConstraint("CK_ReconciliationReport_State",
                "\"Revision\" > 0 AND \"State\" IN (" + In(ImportReconciliationPolicy.States) + ") AND \"Provenance\" IN ('UserDeclaredUnverified', 'SyntheticUnverified') AND " +
                "\"SourceCount\" = \"AcceptedCount\" + \"RejectedCount\" + \"NotCommittedCount\" AND \"DuplicateCount\" <= \"RejectedCount\" AND " +
                "(\"State\" <> 'SIGNED_OFF' OR \"SignOffId\" IS NOT NULL AND \"SignedBy\" IS NOT NULL AND \"SignedBy\" <> \"CreatedBy\") AND " +
                "(\"State\" <> 'SUPERSEDED' OR \"SupersededAt\" IS NOT NULL) AND " +
                "(\"Provenance\" <> 'SyntheticUnverified' OR \"SignOffId\" IS NULL AND \"SignedBy\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.CreatedAt });
            // At most one current (not superseded) report per vessel.
            b.HasIndex(x => new { x.TenantId, x.VesselId }).IsUnique().HasFilter("\"SupersededAt\" IS NULL").HasDatabaseName("IX_ReconciliationReports_Current");
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.State).HasMaxLength(24); b.Property(x => x.PolicyVersion).HasMaxLength(64); b.Property(x => x.BatchesJson).HasColumnType("text");
            b.Property(x => x.MissingCriticalJson).HasColumnType("text"); b.Property(x => x.SampleJson).HasColumnType("text");
            b.Property(x => x.SampleSeed).HasMaxLength(32); b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_ReconciliationReports_Vessels_Scope").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ReconciliationSignOff>(b => {
            b.ToTable("ReconciliationSignOffs", "integration", t => t.HasCheckConstraint("CK_ReconciliationSignOff_Decision",
                "\"Decision\" IN (" + In(ImportReconciliationPolicy.Decisions) + ") AND \"Duty\" = 'DataOwner' AND \"SignedBy\" <> \"ReportAuthor\" AND char_length(\"Reason\") >= 10"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ReportId }).IsUnique();
            b.Property(x => x.ReportHash).HasMaxLength(64); b.Property(x => x.Decision).HasMaxLength(32); b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.ExceptionsJson).HasColumnType("text"); b.Property(x => x.SampleVerifiedJson).HasColumnType("text");
            b.Property(x => x.Duty).HasMaxLength(16); b.Property(x => x.AuthorityHash).HasMaxLength(64);
            b.HasOne<ReconciliationReport>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ReportId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_ReconciliationSignOffs_Report").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ImportReconciliationReceipt>(b => {
            b.ToTable("ImportReconciliationReceipts", "integration", t => t.HasCheckConstraint("CK_ImportReconciliationReceipt_Action",
                "\"Action\" IN (" + In(ReceiptActions) + ") AND \"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_ImportReconciliationReceipts_Vessels_Scope").OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static readonly string[] ReportLifecycle = ["State", "Revision", "SignOffId", "SignedBy", "SupersededAt"];

    // Called by PoseidonDbContext before audit projection.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if (changes.Any(e => e.Entity is ImportProvenance or ReconciliationSignOff or ImportReconciliationReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Import provenance, reconciliation sign-offs and receipts are insert-only.");
        if (changes.Any(e => e.Entity is ReconciliationReport && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && !ReportLifecycle.Contains(p.Metadata.Name)) ||
             (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1 ||
             (string)e.Property("State").OriginalValue! == ImportReconciliationPolicy.Superseded))))
            throw new InvalidOperationException("A reconciliation report snapshot is immutable; only its lifecycle advances.");
    }
}
