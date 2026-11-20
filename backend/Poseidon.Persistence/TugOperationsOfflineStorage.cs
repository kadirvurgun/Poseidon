using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// DEV-092 job snapshot lease: issued only by an authenticated prefetch command for the actor's own tug job scope.
// Insert-only; never seeded. The snapshot content itself is not stored server-side (it is re-read from the owning
// records); only its hash, policy binding and lease are kept so a replay can prove which base it was captured on.
public sealed class TugOperationOfflineSnapshot : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public string AuthorityDigest { get; set; } = "";
    public string SnapshotHash { get; set; } = "";
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

// One immutable outcome per actor command id (idempotent receipt). A MaterialConflict is resolved only by a later
// human command whose ParentCommandId points at it (Committed or ServerRetained); conflicts are never auto-resolved.
public sealed class TugOperationOfflineOutcome : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid CommandId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public string Operation { get; set; } = "";
    public Guid ResourceId { get; set; }
    public Guid? ParentCommandId { get; set; }
    public string Digest { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public string Result { get; set; } = "";
    public uint BaseVersion { get; set; }
    public uint ServerVersion { get; set; }
    public string ConflictFields { get; set; } = "";
    public string ProtectedContext { get; set; } = "";
    public string OutcomeJson { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public int OffsetMinutes { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public static class TugOperationsOfflineStorage
{
    private static string In(IEnumerable<string> values) => string.Join(",", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<TugOperationOfflineSnapshot>(b => {
            b.ToTable("OperationOfflineSnapshots", "tug", t => t.HasCheckConstraint("CK_TugOperationOfflineSnapshot_Lease", "\"ExpiresAt\" > \"IssuedAt\""));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId }).HasName("AK_TugOperationOfflineSnapshot_Scope");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.VesselId, x.JobId, x.IssuedAt }).HasDatabaseName("IX_TugOperationOfflineSnapshot_Owner");
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_TugOperationOfflineSnapshot_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PolicyVersion).HasMaxLength(80); b.Property(x => x.PolicyDigest).HasMaxLength(64);
            b.Property(x => x.AuthorityDigest).HasMaxLength(64); b.Property(x => x.SnapshotHash).HasMaxLength(64);
        });
        model.Entity<TugOperationOfflineOutcome>(b => {
            b.ToTable("OperationOfflineOutcomes", "tug", t => t.HasCheckConstraint("CK_TugOperationOfflineOutcome_State",
                "\"Result\" IN (" + In(TugOperationsOfflineRules.Results) + ") AND \"Operation\" IN (" + In(TugOperationsOfflineRules.CaptureOperations) + ") AND " +
                "(\"Result\"<>'ServerRetained' OR \"ParentCommandId\" IS NOT NULL) AND (\"Result\"<>'MaterialConflict' OR length(\"ConflictFields\")>0)"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.CommandId });
            b.HasAlternateKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.SnapshotId }).HasName("AK_TugOperationOfflineOutcome_Snapshot");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.VesselId, x.Result, x.ReceivedAt }).HasDatabaseName("IX_TugOperationOfflineOutcome_Conflicts");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.ParentCommandId }).IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL")
                .HasDatabaseName("IX_TugOperationOfflineOutcome_Parent");
            b.HasOne<TugOperationOfflineSnapshot>().WithMany().HasForeignKey(x => new { x.TenantId, x.SnapshotId, x.ActorId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId }).HasConstraintName("FK_TugOperationOfflineOutcome_Snapshot").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugOperationOfflineOutcome>().WithMany().HasForeignKey(x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.SnapshotId })
                .HasPrincipalKey(x => new { x.TenantId, x.ActorId, x.CommandId, x.SnapshotId }).HasConstraintName("FK_TugOperationOfflineOutcome_Parent").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Operation).HasMaxLength(32); b.Property(x => x.Digest).HasMaxLength(64); b.Property(x => x.PayloadDigest).HasMaxLength(64);
            b.Property(x => x.Result).HasMaxLength(20); b.Property(x => x.ConflictFields).HasMaxLength(256);
            b.Property(x => x.ProtectedContext).HasColumnType("text"); b.Property(x => x.OutcomeJson).HasColumnType("text");
            b.Property(x => x.BaseVersion).HasConversion<long>(); b.Property(x => x.ServerVersion).HasConversion<long>();
        });
    }

    // Called by PoseidonDbContext before audit projection: snapshots and outcomes are insert-only.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is TugOperationOfflineSnapshot or TugOperationOfflineOutcome && e.State != EntityState.Added))
            throw new InvalidOperationException("Tug operation offline snapshots and outcomes are immutable.");
    }
}
