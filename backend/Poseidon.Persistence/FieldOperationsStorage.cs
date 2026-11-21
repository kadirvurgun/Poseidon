using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S9 WP-D. DEV-089 towage execution (one per job and tug) with an append-only, hash-chained event ledger;
// DEV-090 tug daily log with append-only revisions and Master review records; DEV-111 drydock package with
// append-only revisions. Start, abort, review and verification are human records; DB CHECKs keep the verifier
// apart from the starter/finisher and forbid synthetic executions or synthetic non-draft logs/packages.
public sealed class TowageExecution : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the job's request context
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public long JobRevision { get; set; }
    public Guid TugId { get; set; }
    public Guid AssignmentId { get; set; }
    public long AssignmentRevision { get; set; }
    public Guid PlanId { get; set; }
    public long PlanRevision { get; set; }
    public Guid BriefingId { get; set; }
    public long BriefingRevision { get; set; }
    public Guid ReadinessDecisionId { get; set; }
    public string State { get; set; } = TowageExecutionPolicy.Active;
    public string VerificationState { get; set; } = TowageExecutionPolicy.Pending;
    public long Revision { get; set; } = 1;
    public long Sequence { get; set; }
    public string LastEventHash { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public Guid StartedBy { get; set; }
    public Guid StartAuthorityId { get; set; }
    public string? StartPosition { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public Guid? EndedBy { get; set; }
    public string? EndPosition { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string Hash { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
// One accepted ledger entry. Payload values, reasons and texts never reach audit summaries.
public sealed class TowageExecutionEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ExecutionId { get; set; }
    public long Sequence { get; set; }
    public string Kind { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Guid ActorId { get; set; }
    public Guid? AuthorityId { get; set; }
    public string? Duty { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string PreviousHash { get; set; } = "";
    public string Hash { get; set; } = "";
}

public sealed class TugDailyLog : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the tug
    public Guid Id { get; set; }
    public DateOnly LogDate { get; set; }
    public string Watch { get; set; } = "OTHER";
    public long Revision { get; set; } = 1;
    public string State { get; set; } = TugDailyLogPolicy.Draft;
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string AuthorsJson { get; set; } = "[]";
    public Guid? SubmittedBy { get; set; }
    public long? VerifiedRevision { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
public sealed class TugDailyLogRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid LogId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = TugDailyLogPolicy.Draft;
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public DateTimeOffset? CapturedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
// Master VERIFY/RETURN of one exact submitted revision, with the reconciliation state the Master saw.
public sealed class TugDailyLogReview : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid LogId { get; set; }
    public long Revision { get; set; }
    public long ResultRevision { get; set; }
    public string Decision { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string Reconciliation { get; set; } = "";
    public Guid ActorId { get; set; }
    // QA S9 N5: the submitter and authors of the reviewed revision, kept so the DB CHECK enforces reviewer != author.
    public Guid SubmittedBy { get; set; }
    public string AuthorsJson { get; set; } = "[]";
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset DecidedAt { get; set; }
}

public sealed class DrydockPackage : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = DrydockPolicy.Draft;
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualFinish { get; set; }
    public Guid? CompletedBy { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
// Timeline entry. The verification result text is kept here and withheld from audit summaries.
public sealed class DrydockPackageRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PackageId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = DrydockPolicy.Draft;
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string? Note { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}

public sealed class FieldOperationReceipt : ITenantEntity
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

public static class FieldOperationsStorage
{
    public static readonly string[] ReceiptActions = ["TowageExecutionStart", "TowageExecutionTransition", "TowageExecutionEvent", "TowageExecutionVerify",
        "TugDailyLogCreate", "TugDailyLogUpdate", "TugDailyLogSubmit", "TugDailyLogReview",
        "DrydockCreate", "DrydockUpdate", "DrydockTransition", "DrydockVerify"];
    public static readonly string[] DailyReasons = ["CREATE", "EDIT", "SUBMIT", "VERIFY", "RETURN", "SYNTHETIC_INCOMPLETE_DRAFT"];
    public static readonly string[] DrydockReasons = ["CREATE", "EDIT", "PLAN", "REPLAN", "START", "COMPLETE", "CANCEL", "VERIFY", "REJECT", "SYNTHETIC_INCOMPLETE_DRAFT"];
    private static string In(IEnumerable<string> values) => string.Join(",", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model) {
        model.Entity<TowageExecution>(b => {
            b.ToTable("TowageExecutions", "tug", t => {
                t.HasCheckConstraint("CK_TowageExecution_State", "\"Revision\">0 AND \"Sequence\">0 AND \"State\" IN (" + In(TowageExecutionPolicy.States) + ") AND " +
                    "\"VerificationState\" IN (" + In(TowageExecutionPolicy.VerificationStates) + ") AND \"Provenance\"='UserDeclaredUnverified' AND " +
                    "(\"State\" IN ('ACTIVE','SUSPENDED') OR \"EndedAt\" IS NOT NULL AND \"EndedBy\" IS NOT NULL)");
                t.HasCheckConstraint("CK_TowageExecution_Verification", "\"VerificationState\"='PENDING' AND \"VerifiedBy\" IS NULL OR " +
                    "\"VerificationState\"<>'PENDING' AND \"State\" IN ('COMPLETED','ABORTED') AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedBy\"<>\"StartedBy\" AND \"VerifiedBy\"<>\"EndedBy\"");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.JobId, x.TugId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.TugId, x.StartedAt });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.VerificationState).HasMaxLength(16); b.Property(x => x.LastEventHash).HasMaxLength(64);
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.StartPosition).HasMaxLength(400); b.Property(x => x.EndPosition).HasMaxLength(400);
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<TugJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowageExecutions_Job").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.TugId }).HasConstraintName("FK_TowageExecutions_Tug").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowageExecutionEvent>(b => {
            b.ToTable("TowageExecutionEvents", "tug", t => t.HasCheckConstraint("CK_TowageExecutionEvent_Kind",
                "\"Sequence\">0 AND \"Kind\" IN (" + In(TowageExecutionPolicy.Kinds) + ") AND (\"Duty\" IS NULL OR \"Duty\" IN ('TugMaster','Master'))"));
            b.HasKey(x => new { x.TenantId, x.ExecutionId, x.Sequence });
            b.HasOne<TowageExecution>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ExecutionId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowageExecutionEvents_Execution").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Duty).HasMaxLength(20); b.Property(x => x.PayloadJson).HasColumnType("text");
            b.Property(x => x.PreviousHash).HasMaxLength(64); b.Property(x => x.Hash).HasMaxLength(64);
        });
        model.Entity<TugDailyLog>(b => {
            b.ToTable("TugDailyLogs", "tug", t => t.HasCheckConstraint("CK_TugDailyLog_State",
                "\"Revision\">0 AND \"State\" IN (" + In(TugDailyLogPolicy.States) + ") AND \"Watch\" IN (" + In(TugDailyLogPolicy.Watches) + ") AND " +
                "\"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'VERIFIED' OR \"VerifiedRevision\"=\"Revision\") AND " +
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedRevision\" IS NULL AND \"SubmittedBy\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.LogDate, x.Watch }).IsUnique();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Watch).HasMaxLength(8); b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.AuthorsJson).HasColumnType("text"); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugDailyLogRevision>(b => {
            b.ToTable("TugDailyLogRevisions", "tug", t => t.HasCheckConstraint("CK_TugDailyLogRevision_State",
                "\"Revision\">0 AND \"State\" IN (" + In(TugDailyLogPolicy.States) + ") AND \"ReasonCode\" IN (" + In(DailyReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.LogId, x.Revision });
            b.HasOne<TugDailyLog>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.LogId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TugDailyLogRevisions_Log").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<TugDailyLogReview>(b => {
            b.ToTable("TugDailyLogReviews", "tug", t => t.HasCheckConstraint("CK_TugDailyLogReview_Kind",
                "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND \"Duty\" IN ('TugMaster','Master') AND " +
                "\"Reconciliation\" IN ('RECONCILED','REVIEW_REQUIRED','INCOMPLETE') AND " +
                "\"ActorId\"<>\"SubmittedBy\" AND NOT (\"AuthorsJson\"::jsonb @> jsonb_build_array(\"ActorId\"::text))"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.LogId, x.Revision }).IsUnique();
            b.HasOne<TugDailyLogRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LogId, x.Revision })
                .HasPrincipalKey(x => new { x.TenantId, x.LogId, x.Revision }).HasConstraintName("FK_TugDailyLogReviews_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Decision).HasMaxLength(16); b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.Reconciliation).HasMaxLength(20);
            b.Property(x => x.Duty).HasMaxLength(20); b.Property(x => x.Reason).HasMaxLength(1000); b.Property(x => x.AuthorsJson).HasColumnType("text");
        });
        model.Entity<DrydockPackage>(b => {
            b.ToTable("DrydockPackages", "technical", t => t.HasCheckConstraint("CK_DrydockPackage_State",
                "\"Revision\">0 AND \"State\" IN (" + In(DrydockPolicy.States) + ") AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND " +
                "(\"State\"<>'VERIFIED' OR \"VerifiedBy\" IS NOT NULL AND \"CompletedBy\" IS NOT NULL AND \"VerifiedBy\"<>\"CompletedBy\") AND " +
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedBy\" IS NULL AND \"CompletedBy\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.SavedAt });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DrydockPackageRevision>(b => {
            b.ToTable("DrydockPackageRevisions", "technical", t => t.HasCheckConstraint("CK_DrydockPackageRevision_State",
                "\"Revision\">0 AND \"State\" IN (" + In(DrydockPolicy.States) + ") AND \"ReasonCode\" IN (" + In(DrydockReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.PackageId, x.Revision });
            b.HasOne<DrydockPackage>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PackageId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_DrydockPackageRevisions_Package").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Note).HasMaxLength(1000); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<FieldOperationReceipt>(b => {
            b.ToTable("FieldOperationReceipts", "operations", t => t.HasCheckConstraint("CK_FieldOperationReceipt_Action", "\"Action\" IN (" + In(ReceiptActions) + ") AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static bool OneStep(EntityEntry e) => (long)e.Property("Revision").CurrentValue! == (long)e.Property("Revision").OriginalValue! + 1;

    // Called by PoseidonDbContext before audit projection.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if (changes.Any(e => e.Entity is TowageExecutionEvent or TugDailyLogRevision or TugDailyLogReview or DrydockPackageRevision or FieldOperationReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Field operation ledgers, revisions, reviews and receipts are immutable.");
        if (changes.Any(e => e.Entity is TowageExecution && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "Sequence", "LastEventHash", "State", "VerificationState", "EndedAt", "EndedBy", "EndPosition", "VerifiedBy", "VerifiedAt", "Hash", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Towage execution context is immutable; append one ledger event.");
        if (changes.Any(e => e.Entity is TugDailyLog && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "ContentJson", "Hash", "AuthorsJson", "SubmittedBy", "VerifiedRevision", "CapturedAt", "SyncedAt", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Tug daily log identity is immutable; append one revision.");
        if (changes.Any(e => e.Entity is DrydockPackage && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "ContentJson", "Hash", "ActualStart", "ActualFinish", "CompletedBy", "VerifiedBy", "VerifiedAt", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Drydock package identity is immutable; append one revision.");
    }
}
