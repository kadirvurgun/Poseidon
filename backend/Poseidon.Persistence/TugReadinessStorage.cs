using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// DEV-083: shore-proposed tug assignment (mutable head, xmin concurrency). The Tug Master's
// acknowledgement is a separate immutable human record; ACCEPT never confirms or releases.
public sealed class TugAssignmentProposal : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the job's request context
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public long JobRevision { get; set; }
    public Guid TugId { get; set; }
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset PlannedEnd { get; set; }
    public required string Role { get; set; }
    public string State { get; set; } = TugAssignmentWorkflow.Proposed;
    public string MasterAck { get; set; } = TugAssignmentWorkflow.Pending;
    public required string ScopeHash { get; set; }
    public Guid ProposedBy { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

// Immutable Master/Tug Master acknowledgement (ACCEPT|REJECT) of one exact scope hash.
public sealed class TugAssignmentResponse : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid TugId { get; set; }
    public required string Decision { get; set; }
    public required string ScopeHash { get; set; }
    public string? Reason { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string Duty { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// DEV-085 tow equipment declaration on one tug (mutable head + immutable revisions).
public sealed class TowEquipmentItem : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the tug
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public string? Label { get; set; }
    public decimal? SwlTonnes { get; set; }
    public DateOnly? LastTestDate { get; set; }
    public DateOnly? NextTestDue { get; set; }
    public string Condition { get; set; } = TowEquipmentPolicy.ConditionUnknown;
    public bool? Critical { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? CertificateId { get; set; }
    public string? Notes { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public Guid ActorId { get; set; }
    public TowEquipmentInputs Inputs() => new(Kind, Label, SwlTonnes?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        LastTestDate, NextTestDue, Condition, Critical, AssetId, CertificateId, Notes);
    public void Apply(TowEquipmentInputs x) {
        TowEquipmentPolicy.TrySwl(x.SwlTonnes, out var swl);
        Kind = x.Kind; Label = x.Label; SwlTonnes = swl; LastTestDate = x.LastTestDate; NextTestDue = x.NextTestDue; Condition = x.Condition;
        Critical = x.Critical; AssetId = x.AssetId; CertificateId = x.CertificateId; Notes = x.Notes;
    }
    public TowEquipmentRevision Snapshot() => new() { TenantId = TenantId, VesselId = VesselId, ItemId = Id, Revision = Revision,
        ContentJson = JsonSerializer.Serialize(Inputs()), Provenance = Provenance, SavedAt = SavedAt, ActorId = ActorId };
}
public sealed class TowEquipmentRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ItemId { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public Guid ActorId { get; set; }
    public TowEquipmentInputs Inputs() => JsonSerializer.Deserialize<TowEquipmentInputs>(ContentJson)!;
}
// Immutable Chief Engineer verification of one exact item revision.
public sealed class TowEquipmentVerification : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public long ItemRevision { get; set; }
    public required string Outcome { get; set; }
    public string? Reason { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string Duty { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// DEV-084 Master/Tug Master GO/NO_GO human record, bound to one exact advisory assessment.
public sealed class TugReadinessDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the job's request context
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public long AssignmentRevision { get; set; }
    public Guid TugId { get; set; }
    public required string Decision { get; set; }
    public required string AssessedState { get; set; }
    public required string AssessmentHash { get; set; }
    public required string Reason { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string Duty { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// Idempotency receipt for the S7 tug readiness command family.
public sealed class TugReadinessReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? RecordId { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class TugReadinessStorage
{
    public static readonly string[] ReceiptActions = ["TugAssignmentPropose", "TugAssignmentRespond", "TugAssignmentWithdraw",
        "TugReadinessDecide", "TugTowEquipmentSave", "TugTowEquipmentUpdate", "TugTowEquipmentVerify"];
    internal static void Configure(ModelBuilder model) {
        model.Entity<TugAssignmentProposal>(b => {
            b.ToTable("AssignmentProposals", "tug", t => {
                t.HasCheckConstraint("CK_TugAssignmentProposal_State", "\"State\" IN ('Proposed', 'Cancelled') AND (\"State\" <> 'Cancelled' OR \"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL AND \"CancelledBy\" IS NOT NULL)");
                t.HasCheckConstraint("CK_TugAssignmentProposal_Ack", "\"MasterAck\" IN ('Pending', 'Accepted', 'Rejected')");
                t.HasCheckConstraint("CK_TugAssignmentProposal_Role", "\"Role\" IN ('Lead', 'Support', 'Escort')");
                t.HasCheckConstraint("CK_TugAssignmentProposal_Window", "\"PlannedEnd\" > \"PlannedStart\"");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            // One active proposal per job and tug; a cancelled one remains as history.
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.JobId, x.TugId }).IsUnique().HasFilter("\"State\" = 'Proposed'");
            b.HasIndex(x => new { x.TenantId, x.TugId, x.State, x.PlannedStart });
            b.Property(x => x.Role).HasMaxLength(20); b.Property(x => x.State).HasMaxLength(20); b.Property(x => x.MasterAck).HasMaxLength(20);
            b.Property(x => x.ScopeHash).HasMaxLength(64); b.Property(x => x.CancelReason).HasMaxLength(TugAssignmentWorkflow.ReasonMax);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<TugJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.TugId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugAssignmentResponse>(b => {
            b.ToTable("AssignmentResponses", "tug", t => {
                t.HasCheckConstraint("CK_TugAssignmentResponse_Decision", "\"Decision\" IN ('ACCEPT', 'REJECT') AND (\"Decision\" <> 'REJECT' OR \"Reason\" IS NOT NULL)");
                t.HasCheckConstraint("CK_TugAssignmentResponse_Duty", "\"Duty\" IN ('TugMaster', 'Master')");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.AssignmentId }).IsUnique();
            b.Property(x => x.Decision).HasMaxLength(10); b.Property(x => x.ScopeHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Reason).HasMaxLength(TugAssignmentWorkflow.ReasonMax);
            b.HasOne<TugAssignmentProposal>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowEquipmentItem>(b => {
            b.ToTable("TowEquipment", "tug", t => {
                t.HasCheckConstraint("CK_TowEquipment_Kind", "\"Kind\" IN ('Winch', 'TowHook', 'Towline', 'EmergencyRelease', 'FiFi')");
                t.HasCheckConstraint("CK_TowEquipment_Condition", "\"Condition\" IN ('Serviceable', 'Degraded', 'Failed', 'Unknown')");
                t.HasCheckConstraint("CK_TowEquipment_Swl", "\"SwlTonnes\" IS NULL OR \"SwlTonnes\" > 0");
                t.HasCheckConstraint("CK_TowEquipment_Test", "\"NextTestDue\" IS NULL OR \"LastTestDate\" IS NULL OR \"NextTestDue\" >= \"LastTestDate\"");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Kind });
            b.Property(x => x.Kind).HasMaxLength(20); b.Property(x => x.Condition).HasMaxLength(20); b.Property(x => x.Label).HasMaxLength(TowEquipmentPolicy.TextMax);
            b.Property(x => x.Notes).HasMaxLength(TowEquipmentPolicy.NotesMax); b.Property(x => x.SwlTonnes).HasPrecision(8, 2); b.Property(x => x.Provenance).HasMaxLength(40);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowEquipmentRevision>(b => {
            b.ToTable("TowEquipmentRevisions", "tug"); b.HasKey(x => new { x.TenantId, x.VesselId, x.ItemId, x.Revision });
            b.Property(x => x.ContentJson).HasColumnType("jsonb"); b.Property(x => x.Provenance).HasMaxLength(40);
            b.HasOne<TowEquipmentItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ItemId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowEquipmentVerification>(b => {
            b.ToTable("TowEquipmentVerifications", "tug", t => {
                t.HasCheckConstraint("CK_TowEquipmentVerification_Outcome", "\"Outcome\" IN ('VERIFIED', 'NOT_VERIFIED') AND (\"Outcome\" <> 'NOT_VERIFIED' OR \"Reason\" IS NOT NULL)");
                t.HasCheckConstraint("CK_TowEquipmentVerification_Duty", "\"Duty\" = 'ChiefEngineer'");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.ItemId, x.ItemRevision });
            b.Property(x => x.Outcome).HasMaxLength(20); b.Property(x => x.Duty).HasMaxLength(20); b.Property(x => x.Reason).HasMaxLength(TowEquipmentPolicy.NotesMax);
            b.HasOne<TowEquipmentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ItemId, x.ItemRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugReadinessDecision>(b => {
            b.ToTable("ReadinessDecisions", "tug", t => {
                t.HasCheckConstraint("CK_TugReadinessDecision_Decision", "\"Decision\" IN ('GO', 'NO_GO') AND (\"Decision\" <> 'GO' OR \"AssessedState\" IN ('READY', 'CONDITIONAL'))");
                t.HasCheckConstraint("CK_TugReadinessDecision_State", "\"AssessedState\" IN ('READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED')");
                t.HasCheckConstraint("CK_TugReadinessDecision_Duty", "\"Duty\" IN ('TugMaster', 'Master')");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.AssignmentId, x.RecordedAt });
            b.Property(x => x.Decision).HasMaxLength(10); b.Property(x => x.AssessedState).HasMaxLength(20); b.Property(x => x.AssessmentHash).HasMaxLength(64);
            b.Property(x => x.Duty).HasMaxLength(20); b.Property(x => x.Reason).HasMaxLength(TugReadinessEngine.ReasonMax);
            b.HasOne<TugAssignmentProposal>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.AssignmentId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugReadinessReceipt>(b => {
            b.ToTable("ReadinessReceipts", "tug", t => t.HasCheckConstraint("CK_TugReadinessReceipt_Action",
                "\"Action\" IN (" + string.Join(", ", ReceiptActions.Select(a => "'" + a + "'")) + ")"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
