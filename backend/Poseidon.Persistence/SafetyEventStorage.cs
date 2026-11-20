using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S9 WP-B DEV-060 safety event / near-miss head. Mutable head (xmin) with append-only revisions. ReportedBy is set
// only for NAMED reports; a CONFIDENTIAL report keeps its reporter exclusively in SafetyEventIdentity (vault).
// The head never stores the last editor, so a general read cannot reveal a protected reporter.
public sealed class SafetyEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public string EventNo { get; set; } = "";
    public long Revision { get; set; } = 1;
    public string State { get; set; } = SafetyEventPolicy.Draft;
    public string ReportingMode { get; set; } = SafetyEventPolicy.Named;
    public string? Category { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid? ReportedBy { get; set; }
    public string? Severity { get; set; }
    public string? InvestigationRequired { get; set; }
    public string Origin { get; set; } = "ONLINE";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
// ActorId is null when the author is the protected (CONFIDENTIAL) reporter.
public sealed class SafetyEventRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EventId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = SafetyEventPolicy.Draft;
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid? ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
// Identity vault (61.01 §10): restricted store, read only through a recorded purpose by hse.safety_event.identity.read.
// The follow-up handle/hash give the reporter a pseudonymous channel; the secret itself is never stored.
public sealed class SafetyEventIdentity : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid ReporterId { get; set; }
    public string ContactPreference { get; set; } = "NONE";
    public string? FollowUpHandle { get; set; }
    public string? FollowUpHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class SafetyEventIdentityAccess : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public long EventRevision { get; set; }
    public Guid ActorId { get; set; }
    public string Purpose { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset AccessedAt { get; set; }
}
public sealed class SafetyEventMessage : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public long EventRevision { get; set; }
    public string Visibility { get; set; } = "TEAM_ONLY";
    public string AuthorKind { get; set; } = "TEAM";
    public Guid? ActorId { get; set; }
    public string Body { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class SafetyEventTriage : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public long Revision { get; set; }
    public long ResultRevision { get; set; }
    public string Decision { get; set; } = "";
    public string? Severity { get; set; }
    public string? InvestigationRequired { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
// DEV-061 investigation (one per event). APPROVED pins ApprovedRevision; a REJECTED investigation is revised into a new DRAFT.
public sealed class SafetyInvestigation : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EventId { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = "DRAFT";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string AuthorsJson { get; set; } = "[]";
    public Guid? SubmittedBy { get; set; }
    public long? ApprovedRevision { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class SafetyInvestigationRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EventId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = "DRAFT";
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class SafetyInvestigationDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public long Revision { get; set; }
    public string Decision { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
// Corrective/preventive action. State (OPEN/COMPLETED/VERIFIED) and Effectiveness are separate columns: Verified != Effective.
public sealed class SafetyAction : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid OwnerId { get; set; }
    public DateOnly DueDate { get; set; }
    public string RootCauseIdsJson { get; set; } = "[]";
    public string BarrierIdsJson { get; set; } = "[]";
    public Guid? FollowUpOf { get; set; }
    public long InvestigationRevision { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = "OPEN";
    public string Effectiveness { get; set; } = "PENDING";
    public Guid? CompletedBy { get; set; }
    public Guid? VerifiedBy { get; set; }
    public Guid? EffectivenessBy { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class SafetyActionStep : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid ActionId { get; set; }
    public long ActionRevision { get; set; }
    public string Kind { get; set; } = "";
    public string? Outcome { get; set; }
    public string Text { get; set; } = "";
    public string EvidenceJson { get; set; } = "[]";
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class SafetyEventReceipt : ITenantEntity
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
// DEV-060 offline capture (DEV-033 protocol): per actor+vessel enrollment, P-256 key, signed idempotent replay.
public sealed class SafetyEventOfflineEnrollment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid KeyId { get; set; }
    public string Challenge { get; set; } = "";
    public string? PublicKey { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string PolicyDigest { get; set; } = "";
    public string AuthorityDigest { get; set; } = "";
    public string SourceVersion { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RetainUntil { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public uint Version { get; set; }
}
public sealed class SafetyEventOfflineLifecycle : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public Guid VesselId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class SafetyEventOfflineOutcome : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid CommandId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid VesselId { get; set; }
    public Guid EventId { get; set; }
    public string Digest { get; set; } = "";
    public string Result { get; set; } = "Committed";
    public long Revision { get; set; }
    public string OutcomeJson { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public static class SafetyEventStorage
{
    public static readonly string[] ReceiptActions = ["SafetyEventCreate", "SafetyEventUpdate", "SafetyEventSubmit", "SafetyEventWithdraw", "SafetyEventTriage",
        "SafetyEventIdentityAccess", "SafetyEventMessage", "SafetyInvestigationSave", "SafetyInvestigationSubmit", "SafetyInvestigationReview",
        "SafetyActionCreate", "SafetyActionStep", "SafetyEventClose"];
    public static readonly string[] EventReasons = ["CREATE", "EDIT", "SUBMIT", "WITHDRAW", "TRIAGE", "RETURN", "INVESTIGATION_OPENED", "INVESTIGATION_APPROVED",
        "INVESTIGATION_REJECTED", "CLOSE", "OFFLINE_CAPTURE", "SYNTHETIC_INCOMPLETE_DRAFT"];
    public static readonly string[] InvestigationReasons = ["CREATE", "EDIT", "REVISE", "SUBMIT", "APPROVE", "RETURN", "REJECT"];
    public static readonly string[] LifecycleActions = ["Enrolled", "Renewed", "KeyEnrolled", "Revoked", "KeyLost", "CleanupAllowed"];
    private static string In(IEnumerable<string> values) => string.Join(",", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<SafetyEvent>(b => {
            b.ToTable("SafetyEvents", "hse", t => t.HasCheckConstraint("CK_SafetyEvent_State",
                "\"Revision\">0 AND \"State\" IN (" + In(SafetyEventPolicy.States) + ") AND \"ReportingMode\" IN (" + In(SafetyEventPolicy.ReportingModes) + ") AND " +
                "\"Origin\" IN ('ONLINE','OFFLINE') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND " +
                "(\"Category\" IS NULL OR \"Category\" IN (" + In(SafetyEventPolicy.Categories) + ")) AND " +
                "(\"Severity\" IS NULL OR \"Severity\" IN (" + In(SafetyEventPolicy.Severities) + ")) AND " +
                "(\"InvestigationRequired\" IS NULL OR \"InvestigationRequired\" IN ('YES','NO','UNKNOWN')) AND " +
                "((\"ReportingMode\"='NAMED') = (\"ReportedBy\" IS NOT NULL)) AND (\"State\"<>'CLOSED' OR \"ClosedBy\" IS NOT NULL AND length(\"ClosureReason\")>=10) AND " +
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"Severity\" IS NULL AND \"SubmittedAt\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.EventNo }).IsUnique(); b.HasIndex(x => new { x.TenantId, x.VesselId, x.State });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.EventNo).HasMaxLength(32); b.Property(x => x.State).HasMaxLength(20); b.Property(x => x.ReportingMode).HasMaxLength(16);
            b.Property(x => x.Category).HasMaxLength(20); b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.Severity).HasMaxLength(16); b.Property(x => x.InvestigationRequired).HasMaxLength(8); b.Property(x => x.Origin).HasMaxLength(8);
            b.Property(x => x.ClosureReason).HasMaxLength(2000); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_SafetyEvents_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SafetyEventRevision>(b => {
            b.ToTable("SafetyEventRevisions", "hse", t => t.HasCheckConstraint("CK_SafetyEventRevision_State",
                "\"Revision\">0 AND \"State\" IN (" + In(SafetyEventPolicy.States) + ") AND \"ReasonCode\" IN (" + In(EventReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.EventId, x.Revision });
            Child(b, "SafetyEventRevisions");
            b.Property(x => x.State).HasMaxLength(20); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<SafetyEventIdentity>(b => {
            b.ToTable("SafetyEventIdentities", "hse", t => t.HasCheckConstraint("CK_SafetyEventIdentity_Contact",
                "\"ContactPreference\" IN ('NONE','IN_APP_FOLLOW_UP') AND ((\"FollowUpHandle\" IS NULL) = (\"FollowUpHash\" IS NULL))"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.EventId }).IsUnique();
            b.HasIndex(x => x.FollowUpHandle).IsUnique().HasFilter("\"FollowUpHandle\" IS NOT NULL");
            Child(b, "SafetyEventIdentities");
            b.Property(x => x.ContactPreference).HasMaxLength(20); b.Property(x => x.FollowUpHandle).HasMaxLength(32); b.Property(x => x.FollowUpHash).HasMaxLength(64);
        });
        model.Entity<SafetyEventIdentityAccess>(b => {
            b.ToTable("SafetyEventIdentityAccesses", "hse", t => t.HasCheckConstraint("CK_SafetyEventIdentityAccess_Purpose",
                "\"Purpose\" IN (" + In(SafetyEventPolicy.IdentityPurposes) + ") AND length(\"Reason\")>=10"));
            b.HasKey(x => new { x.TenantId, x.Id }); Child(b, "SafetyEventIdentityAccesses");
            b.Property(x => x.Purpose).HasMaxLength(32); b.Property(x => x.Reason).HasMaxLength(1000);
        });
        model.Entity<SafetyEventMessage>(b => {
            b.ToTable("SafetyEventMessages", "hse", t => t.HasCheckConstraint("CK_SafetyEventMessage_Kind",
                "\"Visibility\" IN ('TEAM_ONLY','REPORTER_SHARED') AND \"AuthorKind\" IN ('TEAM','REPORTER') AND " +
                "((\"AuthorKind\"='REPORTER') = (\"ActorId\" IS NULL)) AND (\"AuthorKind\"<>'REPORTER' OR \"Visibility\"='REPORTER_SHARED') AND length(\"Body\") BETWEEN 1 AND 4000"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.EventId, x.CreatedAt }); Child(b, "SafetyEventMessages");
            b.Property(x => x.Visibility).HasMaxLength(16); b.Property(x => x.AuthorKind).HasMaxLength(10); b.Property(x => x.Body).HasMaxLength(4000);
            b.Property(x => x.Hash).HasMaxLength(64);
        });
        model.Entity<SafetyEventTriage>(b => {
            b.ToTable("SafetyEventTriages", "hse", t => t.HasCheckConstraint("CK_SafetyEventTriage_Decision",
                "\"Decision\" IN ('TRIAGE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND " +
                "(\"Decision\"='RETURN') = (\"Severity\" IS NULL AND \"InvestigationRequired\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.EventId, x.Revision }).IsUnique(); Child(b, "SafetyEventTriages");
            b.Property(x => x.Decision).HasMaxLength(8); b.Property(x => x.Severity).HasMaxLength(16); b.Property(x => x.InvestigationRequired).HasMaxLength(8);
            b.Property(x => x.Reason).HasMaxLength(2000);
        });
        model.Entity<SafetyInvestigation>(b => {
            b.ToTable("SafetyInvestigations", "hse", t => t.HasCheckConstraint("CK_SafetyInvestigation_State",
                "\"Revision\">0 AND \"State\" IN (" + In(SafetyEventPolicy.InvestigationStates) + ") AND (\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\")"));
            b.HasKey(x => new { x.TenantId, x.EventId }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.EventId });
            b.Property(x => x.Version).IsRowVersion(); Child(b, "SafetyInvestigations");
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.AuthorsJson).HasColumnType("text");
        });
        model.Entity<SafetyInvestigationRevision>(b => {
            b.ToTable("SafetyInvestigationRevisions", "hse", t => t.HasCheckConstraint("CK_SafetyInvestigationRevision_State",
                "\"Revision\">0 AND \"State\" IN (" + In(SafetyEventPolicy.InvestigationStates) + ") AND \"ReasonCode\" IN (" + In(InvestigationReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.EventId, x.Revision });
            b.HasOne<SafetyInvestigation>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.EventId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.EventId }).HasConstraintName("FK_SafetyInvestigationRevisions_Investigation").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64);
        });
        model.Entity<SafetyInvestigationDecision>(b => {
            b.ToTable("SafetyInvestigationDecisions", "hse", t => t.HasCheckConstraint("CK_SafetyInvestigationDecision_Kind",
                "\"Decision\" IN ('APPROVE','RETURN','REJECT') AND length(\"Reason\")>=10"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.EventId, x.Revision }).IsUnique();
            b.HasOne<SafetyInvestigationRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.EventId, x.Revision })
                .HasPrincipalKey(x => new { x.TenantId, x.EventId, x.Revision }).HasConstraintName("FK_SafetyInvestigationDecisions_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Decision).HasMaxLength(8); b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.Reason).HasMaxLength(2000);
        });
        model.Entity<SafetyAction>(b => {
            b.ToTable("SafetyActions", "hse", t => t.HasCheckConstraint("CK_SafetyAction_State",
                "\"Revision\">0 AND \"Kind\" IN ('CORRECTIVE','PREVENTIVE') AND \"State\" IN ('OPEN','COMPLETED','VERIFIED') AND " +
                "\"Effectiveness\" IN ('PENDING','EFFECTIVE','NOT_EFFECTIVE','INCONCLUSIVE') AND (\"Effectiveness\"='PENDING' OR \"State\"='VERIFIED' AND \"EffectivenessBy\" IS NOT NULL) AND " +
                "(\"State\"<>'VERIFIED' OR \"VerifiedBy\" IS NOT NULL AND \"CompletedBy\" IS NOT NULL) AND (\"FollowUpOf\" IS NULL OR \"FollowUpOf\"<>\"Id\")"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id }); b.HasIndex(x => new { x.TenantId, x.EventId });
            b.Property(x => x.Version).IsRowVersion(); Child(b, "SafetyActions");
            b.Property(x => x.Kind).HasMaxLength(12); b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.RootCauseIdsJson).HasColumnType("text"); b.Property(x => x.BarrierIdsJson).HasColumnType("text");
            b.Property(x => x.State).HasMaxLength(12); b.Property(x => x.Effectiveness).HasMaxLength(16);
        });
        model.Entity<SafetyActionStep>(b => {
            b.ToTable("SafetyActionSteps", "hse", t => t.HasCheckConstraint("CK_SafetyActionStep_Kind",
                "\"Kind\" IN ('COMPLETE','VERIFY','EFFECTIVENESS') AND \"ActionRevision\">1 AND length(\"Text\")>=10 AND " +
                "(\"Kind\"<>'COMPLETE' OR \"Outcome\" IS NULL) AND (\"Kind\"<>'VERIFY' OR \"Outcome\" IN ('VERIFIED','NOT_VERIFIED')) AND " +
                "(\"Kind\"<>'EFFECTIVENESS' OR \"Outcome\" IN ('EFFECTIVE','NOT_EFFECTIVE','INCONCLUSIVE'))"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.ActionId, x.ActionRevision }).IsUnique();
            b.HasOne<SafetyAction>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ActionId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_SafetyActionSteps_Action").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.Outcome).HasMaxLength(16); b.Property(x => x.Text).HasMaxLength(4000);
            b.Property(x => x.EvidenceJson).HasColumnType("text");
        });
        model.Entity<SafetyEventReceipt>(b => {
            b.ToTable("SafetyEventReceipts", "hse", t => t.HasCheckConstraint("CK_SafetyEventReceipt_Action", "\"Action\" IN (" + In(ReceiptActions) + ") AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_SafetyEventReceipts_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SafetyEventOfflineEnrollment>(b => {
            b.ToTable("SafetyEventEnrollments", "offline", t => t.HasCheckConstraint("CK_SafetyEventOfflineEnrollment_State",
                "\"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\""));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId }).HasName("AK_SafetyEventOfflineEnrollment_Scope");
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.VesselId, x.CreatedAt }).HasDatabaseName("IX_SafetyEventOfflineEnrollment_Owner");
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_SafetyEventOffline_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Challenge).HasMaxLength(64); b.Property(x => x.PublicKey).HasMaxLength(256); b.Property(x => x.PolicyVersion).HasMaxLength(80);
            b.Property(x => x.PolicyDigest).HasMaxLength(64); b.Property(x => x.AuthorityDigest).HasMaxLength(64); b.Property(x => x.SourceVersion).HasMaxLength(192);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<SafetyEventOfflineLifecycle>(b => {
            b.ToTable("SafetyEventLifecycle", "offline", t => t.HasCheckConstraint("CK_SafetyEventOfflineLifecycle_Action", "\"Action\" IN (" + In(LifecycleActions) + ")"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.Property(x => x.Action).HasMaxLength(24);
            b.HasOne<SafetyEventOfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId }).HasConstraintName("FK_SafetyEventOfflineLifecycle_Enrollment").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<SafetyEventOfflineOutcome>(b => {
            b.ToTable("SafetyEventOutcomes", "offline", t => t.HasCheckConstraint("CK_SafetyEventOfflineOutcome_State", "\"Result\" IN ('Committed') AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.CommandId });
            b.HasIndex(x => new { x.TenantId, x.ActorId, x.EnrollmentId, x.ReceivedAt, x.CommandId }).HasDatabaseName("IX_SafetyEventOfflineOutcome_History");
            b.HasOne<SafetyEventOfflineEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.ActorId, x.VesselId }).HasConstraintName("FK_SafetyEventOfflineOutcome_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<SafetyEvent>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.EventId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_SafetyEventOfflineOutcome_Event").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Digest).HasMaxLength(64); b.Property(x => x.Result).HasMaxLength(16); b.Property(x => x.OutcomeJson).HasColumnType("text");
        });
    }

    private static void Child<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> b, string table) where T : class =>
        b.HasOne<SafetyEvent>().WithMany().HasForeignKey("TenantId", "VesselId", "EventId")
            .HasPrincipalKey(nameof(SafetyEvent.TenantId), nameof(SafetyEvent.VesselId), nameof(SafetyEvent.Id)).HasConstraintName("FK_" + table + "_Event").OnDelete(DeleteBehavior.Restrict);

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static bool OneStep(EntityEntry e) => (long)e.Property("Revision").CurrentValue! == (long)e.Property("Revision").OriginalValue! + 1;

    // Called by PoseidonDbContext before audit projection: history, decisions, vault accesses, messages, steps,
    // receipts and offline outcomes are append-only; heads advance by exactly one revision; nothing is deleted.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is SafetyEventRevision or SafetyEventIdentityAccess or SafetyEventMessage or SafetyEventTriage or SafetyInvestigationRevision
                or SafetyInvestigationDecision or SafetyActionStep or SafetyEventReceipt or SafetyEventOfflineOutcome or SafetyEventOfflineLifecycle && e.State != EntityState.Added))
            throw new InvalidOperationException("Safety event history, decisions, messages, vault accesses and receipts are immutable.");
        if (changes.Any(e => e.Entity is SafetyEvent && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "Category", "ContentJson", "Hash", "Severity", "InvestigationRequired", "SavedAt", "SubmittedAt", "ClosedBy", "ClosedAt", "ClosureReason") || !OneStep(e)))))
            throw new InvalidOperationException("Safety event identity and reporter are immutable; append one revision.");
        // The vault only gains a follow-up channel once (at submission); the reporter binding never changes.
        if (changes.Any(e => e.Entity is SafetyEventIdentity && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "FollowUpHandle", "FollowUpHash", "ContactPreference") || e.Property("FollowUpHandle").OriginalValue != null))))
            throw new InvalidOperationException("Safety event identity vault rows are immutable.");
        if (changes.Any(e => e.Entity is SafetyInvestigation && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "ContentJson", "Hash", "AuthorsJson", "SubmittedBy", "ApprovedRevision", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Safety investigation history is append-only; advance one revision.");
        if (changes.Any(e => e.Entity is SafetyAction && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "Effectiveness", "CompletedBy", "VerifiedBy", "EffectivenessBy") || !OneStep(e)))))
            throw new InvalidOperationException("Safety action definition and owner are immutable; record one step.");
        if (changes.Any(e => e.Entity is SafetyEventOfflineEnrollment && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            Changed(e, "PublicKey", "ExpiresAt", "RevokedAt"))))
            throw new InvalidOperationException("Safety event offline enrollment binding is immutable.");
    }
}
