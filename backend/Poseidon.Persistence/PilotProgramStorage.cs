using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// Pilot program kit (DEV-076/077/078/079/095/096/121), schema "pilot". Tenant-scoped human records. Nothing here
// completes a real pilot: links reference existing records verified by the server, feedback/issues/measurements are
// human observations and week closes/program decisions are the appointed Product Owner's human records. Only the
// opt-in FICTIONAL program header is seeded (PilotProgramFixtures); no link, feedback, issue, measurement, close or
// decision is ever seeded.

public sealed class PilotProgram : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid ShipVesselId { get; set; }
    public Guid? TugVesselId { get; set; }
    public DateOnly StartDate { get; set; }
    // IANA time zone of the pilot's calendar day ("triaged same day").
    public required string TimeZone { get; set; }
    public required string CatalogVersion { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}

// Human link of one checklist item to one existing record. Withdrawn once (never deleted); the probe re-runs on read.
public sealed class PilotChecklistLink : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public required string Week { get; set; }
    public required string ItemCode { get; set; }
    public required string RecordType { get; set; }
    public Guid RecordId { get; set; }
    public Guid? RecordVesselId { get; set; }
    public bool AuditTracedAtLink { get; set; }
    public string? Note { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public Guid? WithdrawnBy { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
    public string? WithdrawalReason { get; set; }
    public uint Version { get; set; }
}

// Structured daily feedback. Pseudonym and role group only; free text is minimised and expires with the operator's
// retention policy (purged through the audited path).
public sealed class PilotFeedback : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public required string Week { get; set; }
    public required string Area { get; set; }
    public required string Category { get; set; }
    public required string Sentiment { get; set; }
    public required string Role { get; set; }
    public required string Summary { get; set; }
    public required string ParticipantPseudonym { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public required string RetentionPolicyRef { get; set; }
}

public sealed class PilotIssue : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public required string Week { get; set; }
    public required string Area { get; set; }
    public required string Category { get; set; }
    public required string Severity { get; set; }
    public required string Title { get; set; }
    public string State { get; set; } = PilotIssuePolicy.Open;
    public Guid ReportedBy { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public Guid? TriagedBy { get; set; }
    public DateTimeOffset? TriagedAt { get; set; }
    public string? TriageNote { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ResolutionReference { get; set; }
    public uint Version { get; set; }
}

// Human baseline or pilot-period measurement; append-only lineage per (program, metric, mode, phase).
public sealed class PilotMeasurement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public required string MetricId { get; set; }
    public required string Mode { get; set; }
    public required string Phase { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousMeasurementId { get; set; }
    public decimal Value { get; set; }
    public required string Unit { get; set; }
    public int SampleSize { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public required string EvidenceReference { get; set; }
    public required string Method { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// Product Owner week close bound to the week hash. Append-only per (program, week).
public sealed class PilotWeekClose : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public required string Week { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousCloseId { get; set; }
    public required string Outcome { get; set; }
    public required string Recommendation { get; set; }
    public required string WeekHash { get; set; }
    public required string SnapshotJson { get; set; }
    public required string Rationale { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public required string Duty { get; set; }
    public required string AuthorityHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// Product Owner CONTINUE/EXPAND/FIX/STOP decision bound to the program hash. Append-only per program.
public sealed class PilotProgramDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousDecisionId { get; set; }
    public required string Decision { get; set; }
    public required string Recommendation { get; set; }
    public required string ProgramHash { get; set; }
    public required string SnapshotJson { get; set; }
    public required string Rationale { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public required string Duty { get; set; }
    public required string AuthorityHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class PilotProgramReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid RecordId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class PilotProgramStorage
{
    public static readonly string[] ReceiptActions = ["PilotProgramCreate", "PilotChecklistLink", "PilotChecklistWithdraw", "PilotFeedbackRecord", "PilotIssueReport",
        "PilotIssueTriage", "PilotIssueResolve", "PilotMeasurementRecord", "PilotWeekClose", "PilotProgramDecide"];
    public static readonly string[] Provenances = ["UserDeclaredUnverified", "SyntheticUnverified"];
    private static string In(IEnumerable<string> values) => "(" + string.Join(", ", values.Select(v => "'" + v + "'")) + ")";

    internal static void Configure(ModelBuilder model) {
        model.Entity<PilotProgram>(b => {
            b.ToTable("PilotPrograms", "pilot", t => {
                t.HasCheckConstraint("CK_PilotProgram_Provenance", "\"Provenance\" IN " + In(Provenances));
                t.HasCheckConstraint("CK_PilotProgram_Vessels", "\"TugVesselId\" IS NULL OR \"TugVesselId\" <> \"ShipVesselId\"");
                t.HasCheckConstraint("CK_PilotProgram_Name", "char_length(\"Name\") >= 3");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
            b.Property(x => x.Name).HasMaxLength(PilotProgramPolicy.NameMax); b.Property(x => x.TimeZone).HasMaxLength(64);
            b.Property(x => x.CatalogVersion).HasMaxLength(40); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.ShipVesselId }).HasConstraintName("FK_PilotPrograms_ShipVessel").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.TugVesselId }).HasConstraintName("FK_PilotPrograms_TugVessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotChecklistLink>(b => {
            b.ToTable("PilotChecklistLinks", "pilot", t => {
                t.HasCheckConstraint("CK_PilotChecklistLink_Values", "\"Week\" IN " + In(PilotProgramCatalog.Weeks) + " AND \"ItemCode\" IN " +
                    In(PilotProgramCatalog.Items.Select(i => i.Code)) + " AND \"RecordType\" IN " + In(PilotProgramCatalog.RecordTypes));
                t.HasCheckConstraint("CK_PilotChecklistLink_Withdrawal", "(\"WithdrawnAt\" IS NULL) = (\"WithdrawnBy\" IS NULL) AND (\"WithdrawnAt\" IS NULL) = (\"WithdrawalReason\" IS NULL)");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            // One active link per (item, record); a withdrawn link can be relinked as a new row.
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.ItemCode, x.RecordType, x.RecordId }).IsUnique().HasFilter("\"WithdrawnAt\" IS NULL");
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Week).HasMaxLength(4); b.Property(x => x.ItemCode).HasMaxLength(40); b.Property(x => x.RecordType).HasMaxLength(40);
            b.Property(x => x.Note).HasMaxLength(PilotProgramPolicy.NoteMax); b.Property(x => x.WithdrawalReason).HasMaxLength(PilotProgramPolicy.NoteMax);
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotFeedback>(b => {
            b.ToTable("PilotFeedback", "pilot", t => {
                t.HasCheckConstraint("CK_PilotFeedback_Values", "\"Week\" IN " + In(PilotProgramCatalog.Weeks) + " AND \"Area\" IN " + In(PilotProgramCatalog.Areas) +
                    " AND \"Category\" IN " + In(PilotFeedbackPolicy.Categories) + " AND \"Sentiment\" IN " + In(PilotFeedbackPolicy.Sentiments) +
                    " AND \"Role\" IN " + In(PilotFeedbackPolicy.Roles));
                t.HasCheckConstraint("CK_PilotFeedback_Retention", "\"ExpiresAt\" > \"RecordedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.RecordedAt });
            b.HasIndex(x => new { x.TenantId, x.ExpiresAt });
            b.Property(x => x.Week).HasMaxLength(4); b.Property(x => x.Area).HasMaxLength(8); b.Property(x => x.Category).HasMaxLength(20);
            b.Property(x => x.Sentiment).HasMaxLength(10); b.Property(x => x.Role).HasMaxLength(40); b.Property(x => x.Summary).HasMaxLength(PilotFeedbackPolicy.SummaryMax);
            b.Property(x => x.ParticipantPseudonym).HasMaxLength(32); b.Property(x => x.RetentionPolicyRef).HasMaxLength(128);
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotIssue>(b => {
            b.ToTable("PilotIssues", "pilot", t => {
                t.HasCheckConstraint("CK_PilotIssue_Values", "\"Week\" IN " + In(PilotProgramCatalog.Weeks) + " AND \"Area\" IN " + In(PilotProgramCatalog.Areas) +
                    " AND \"Category\" IN " + In(PilotIssuePolicy.Categories) + " AND \"Severity\" IN " + In(PilotIssuePolicy.Severities) +
                    " AND \"State\" IN " + In(PilotIssuePolicy.States));
                t.HasCheckConstraint("CK_PilotIssue_Triage", "(\"State\" = 'OPEN') = (\"TriagedAt\" IS NULL) AND (\"TriagedAt\" IS NULL) = (\"TriagedBy\" IS NULL)" +
                    " AND (\"TriagedAt\" IS NULL) = (\"TriageNote\" IS NULL) AND (\"TriagedAt\" IS NULL OR \"TriagedAt\" >= \"ReportedAt\")");
                t.HasCheckConstraint("CK_PilotIssue_Resolution", "(\"State\" = 'RESOLVED') = (\"ResolvedAt\" IS NOT NULL AND \"ResolvedBy\" IS NOT NULL AND \"Resolution\" IS NOT NULL)" +
                    " AND (\"State\" = 'RESOLVED' OR \"ResolutionReference\" IS NULL) AND (\"ResolvedAt\" IS NULL OR \"ResolvedAt\" >= \"TriagedAt\")");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.Week, x.State });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Week).HasMaxLength(4); b.Property(x => x.Area).HasMaxLength(8); b.Property(x => x.Category).HasMaxLength(20);
            b.Property(x => x.Severity).HasMaxLength(4); b.Property(x => x.Title).HasMaxLength(PilotIssuePolicy.TitleMax); b.Property(x => x.State).HasMaxLength(10);
            b.Property(x => x.TriageNote).HasMaxLength(PilotIssuePolicy.NoteMax); b.Property(x => x.Resolution).HasMaxLength(PilotIssuePolicy.NoteMax);
            b.Property(x => x.ResolutionReference).HasMaxLength(PilotMeasurementPolicy.ReferenceMax);
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotMeasurement>(b => {
            b.ToTable("PilotMeasurements", "pilot", t => {
                t.HasCheckConstraint("CK_PilotMeasurement_Values", "\"MetricId\" IN " + In(PilotProgramCatalog.ValueMetrics.Select(m => m.Id)) + " AND \"Mode\" IN " +
                    In(PilotProgramCatalog.Modes) + " AND \"Phase\" IN " + In(PilotMeasurementPolicy.Phases) + " AND \"Unit\" IN " + In(PilotProgramCatalog.Units));
                t.HasCheckConstraint("CK_PilotMeasurement_Range", "\"Value\" >= 0 AND (\"Unit\" <> 'PERCENT' OR \"Value\" <= 100) AND \"SampleSize\" >= 1 AND \"PeriodTo\" >= \"PeriodFrom\"");
                t.HasCheckConstraint("CK_PilotMeasurement_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousMeasurementId\" IS NULL)");
                // A measurement without a source/evidence reference is never stored (63.05 p142: no value without basis).
                t.HasCheckConstraint("CK_PilotMeasurement_Evidence", "char_length(\"EvidenceReference\") >= 1 AND char_length(\"Method\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.MetricId, x.Mode, x.Phase, x.Sequence }).IsUnique();
            b.Property(x => x.MetricId).HasMaxLength(48); b.Property(x => x.Mode).HasMaxLength(8); b.Property(x => x.Phase).HasMaxLength(10);
            b.Property(x => x.Unit).HasMaxLength(10); b.Property(x => x.Value).HasPrecision(18, 2);
            b.Property(x => x.EvidenceReference).HasMaxLength(PilotMeasurementPolicy.ReferenceMax); b.Property(x => x.Method).HasMaxLength(PilotMeasurementPolicy.MethodMax);
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotWeekClose>(b => {
            b.ToTable("PilotWeekCloses", "pilot", t => {
                t.HasCheckConstraint("CK_PilotWeekClose_Values", "\"Week\" IN " + In(PilotProgramCatalog.Weeks) + " AND \"Outcome\" IN " + In(PilotProgramPolicy.CloseOutcomes) +
                    " AND \"Recommendation\" IN " + In(PilotProgramPolicy.WeekRecommendations));
                // ACCEPTED exists only against a ready week.
                t.HasCheckConstraint("CK_PilotWeekClose_Accepted", "\"Outcome\" <> 'ACCEPTED' OR \"Recommendation\" = 'READY_FOR_HUMAN_CLOSE'");
                t.HasCheckConstraint("CK_PilotWeekClose_Duty", "\"Duty\" = 'ProductOwner'");
                t.HasCheckConstraint("CK_PilotWeekClose_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousCloseId\" IS NULL)");
                t.HasCheckConstraint("CK_PilotWeekClose_Rationale", "char_length(\"Rationale\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.Week, x.Sequence }).IsUnique();
            b.Property(x => x.Week).HasMaxLength(4); b.Property(x => x.Outcome).HasMaxLength(16); b.Property(x => x.Recommendation).HasMaxLength(40);
            b.Property(x => x.WeekHash).HasMaxLength(64); b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Rationale).HasMaxLength(PilotProgramPolicy.RationaleMax); b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotProgramDecision>(b => {
            b.ToTable("PilotProgramDecisions", "pilot", t => {
                t.HasCheckConstraint("CK_PilotProgramDecision_Values", "\"Decision\" IN " + In(PilotProgramPolicy.Decisions) + " AND \"Recommendation\" IN " +
                    In(PilotProgramPolicy.ProgramRecommendations));
                // EXPAND exists only against an expand-eligible program review.
                t.HasCheckConstraint("CK_PilotProgramDecision_Expand", "\"Decision\" <> 'EXPAND' OR \"Recommendation\" = 'EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION'");
                t.HasCheckConstraint("CK_PilotProgramDecision_Duty", "\"Duty\" = 'ProductOwner'");
                t.HasCheckConstraint("CK_PilotProgramDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                t.HasCheckConstraint("CK_PilotProgramDecision_Rationale", "char_length(\"Rationale\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ProgramId, x.Sequence }).IsUnique();
            b.Property(x => x.Decision).HasMaxLength(10); b.Property(x => x.Recommendation).HasMaxLength(48);
            b.Property(x => x.ProgramHash).HasMaxLength(64); b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Rationale).HasMaxLength(PilotProgramPolicy.RationaleMax); b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            b.HasOne<PilotProgram>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProgramId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotProgramReceipt>(b => {
            b.ToTable("PilotProgramReceipts", "pilot", t => t.HasCheckConstraint("CK_PilotProgramReceipt_Action", "\"Action\" IN " + In(ReceiptActions)));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
    }

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static string Original(EntityEntry e, string name) => (string)e.Property(name).OriginalValue!;
    private static string Current(EntityEntry e, string name) => (string)e.Property(name).CurrentValue!;

    // Append-only human records. A link is withdrawn once; an issue moves only OPEN -> TRIAGED -> RESOLVED; feedback is
    // only ever deleted (retention purge), never rewritten.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if (changes.Any(e => e.Entity is PilotProgram or PilotMeasurement or PilotWeekClose or PilotProgramDecision or PilotProgramReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Pilot program records are append-only.");
        if (changes.Any(e => e.Entity is PilotFeedback && e.State == EntityState.Modified))
            throw new InvalidOperationException("Pilot feedback is never rewritten; it only expires.");
        if (changes.Any(e => e.Entity is PilotChecklistLink l && (e.State == EntityState.Deleted ||
                e.State == EntityState.Added && l.WithdrawnAt != null ||
                e.State == EntityState.Modified && (Changed(e, "WithdrawnBy", "WithdrawnAt", "WithdrawalReason") || e.Property("WithdrawnAt").OriginalValue != null))))
            throw new InvalidOperationException("A checklist link is recorded once and withdrawn at most once; its target is immutable.");
        if (changes.Any(e => e.Entity is PilotIssue i && (e.State == EntityState.Deleted ||
                e.State == EntityState.Added && (i.State != PilotIssuePolicy.Open || i.TriagedAt != null || i.ResolvedAt != null) ||
                e.State == EntityState.Modified && (Changed(e, "State", "TriagedBy", "TriagedAt", "TriageNote", "ResolvedBy", "ResolvedAt", "Resolution", "ResolutionReference") ||
                    !(Original(e, "State") == PilotIssuePolicy.Open && Current(e, "State") == PilotIssuePolicy.Triaged && !e.Property("ResolvedAt").IsModified ||
                      Original(e, "State") == PilotIssuePolicy.Triaged && Current(e, "State") == PilotIssuePolicy.Resolved && !e.Property("TriagedAt").IsModified)))))
            throw new InvalidOperationException("A pilot issue moves only OPEN -> TRIAGED -> RESOLVED; its severity and content are immutable.");
    }
}
