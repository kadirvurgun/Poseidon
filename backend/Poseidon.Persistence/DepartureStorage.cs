using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// DEV-059 Master departure decision (RELEASE|HOLD|DENY). Append-only lineage per vessel: Sequence 1..n with the
// previous decision id; bound to the exact advisory assessment hash, window and mode the Master saw. Never seeded.
public sealed class DepartureDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousDecisionId { get; set; }
    public required string Decision { get; set; }
    public required string AssessedState { get; set; }
    public required string AssessmentMode { get; set; }
    public required string AssessmentHash { get; set; }
    public DateOnly WindowFrom { get; set; }
    public DateOnly WindowTo { get; set; }
    // Closed codes only (category states, blocker/review/condition codes): the context the Master decided on.
    public required string SnapshotJson { get; set; }
    public required string Reason { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public required string Duty { get; set; }
    public required string AuthorityHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class DepartureReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid RecordId { get; set; }
    public long Sequence { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class DepartureStorage
{
    public static readonly string[] ReceiptActions = ["DepartureSignoffRecord"];
    internal static void Configure(ModelBuilder model) {
        model.Entity<DepartureDecision>(b => {
            b.ToTable("DepartureDecisions", "ship", t => {
                // RELEASE exists only against a CURRENT READY assessment (60.02/60.01 p952); HOLD/DENY always recordable.
                t.HasCheckConstraint("CK_DepartureDecision_Decision", "\"Decision\" IN ('RELEASE', 'HOLD', 'DENY') AND (\"Decision\" <> 'RELEASE' OR \"AssessedState\" = 'READY' AND \"AssessmentMode\" = 'CURRENT')");
                t.HasCheckConstraint("CK_DepartureDecision_State", "\"AssessedState\" IN ('READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED')");
                t.HasCheckConstraint("CK_DepartureDecision_Mode", "\"AssessmentMode\" IN ('CURRENT', 'FORECAST')");
                t.HasCheckConstraint("CK_DepartureDecision_Duty", "\"Duty\" = 'Master'");
                t.HasCheckConstraint("CK_DepartureDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                t.HasCheckConstraint("CK_DepartureDecision_Window", "\"WindowTo\" >= \"WindowFrom\"");
                t.HasCheckConstraint("CK_DepartureDecision_Reason", "char_length(\"Reason\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Sequence }).IsUnique();
            b.Property(x => x.Decision).HasMaxLength(10); b.Property(x => x.AssessedState).HasMaxLength(20); b.Property(x => x.AssessmentMode).HasMaxLength(10);
            b.Property(x => x.AssessmentHash).HasMaxLength(64); b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Reason).HasMaxLength(DepartureDecisionPolicy.ReasonMax); b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DepartureReceipt>(b => {
            b.ToTable("DepartureReceipts", "ship", t => t.HasCheckConstraint("CK_DepartureReceipt_Action",
                "\"Action\" IN (" + string.Join(", ", ReceiptActions.Select(a => "'" + a + "'")) + ")"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
