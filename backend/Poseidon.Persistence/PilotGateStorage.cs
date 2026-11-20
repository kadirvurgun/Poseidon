using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S11 WP-D DEV-074/094 UAT records and DEV-075 pilot go/no-go records (schema "pilot"). Tenant-scoped. Every row is a
// human record or a human-attested observation; nothing here is a UAT acceptance or a GO by itself. Never seeded
// beyond the opt-in FICTIONAL OPEN run draft (UatFixtures); DB CHECKs block a synthetic run from leaving OPEN.

// Human-agreed UAT threshold per scope (19.01 p39 "agreed threshold"). Append-only lineage; the latest counts.
public sealed class UatThreshold : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Scope { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousThresholdId { get; set; }
    public int MinScenarioPassPercent { get; set; }
    public int MaxOpenMajorFindings { get; set; }
    public bool RequireDesignConformance { get; set; }
    // Where the human agreement lives (steering minutes id, ticket); a reference, not a signature.
    public required string AgreementReference { get; set; }
    public required string Rationale { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// One UAT session of one role (pseudonymous participant) against one build in one environment.
public sealed class UatRun : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Scope { get; set; }
    public required string Role { get; set; }
    public required string Environment { get; set; }
    public required string BuildCommit { get; set; }
    public required string ParticipantPseudonym { get; set; }
    public required string CatalogVersion { get; set; }
    public string State { get; set; } = UatPolicy.RunOpen;
    public Guid FacilitatorActorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    // The independent attestation (actor != facilitator). Empty until a human records it; never generated.
    public Guid? AttestedBy { get; set; }
    public DateTimeOffset? AttestedAt { get; set; }
    public string? AttestationStatement { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}

// One recorded outcome of one catalog scenario in one run. Append-only attempts; the latest attempt counts.
public sealed class UatStepResult : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public required string ScenarioId { get; set; }
    public int Attempt { get; set; }
    public required string Outcome { get; set; }
    public int? DurationSeconds { get; set; }
    public bool Assisted { get; set; }
    // Optional automated (e.g. Playwright) evidence reference: an attachment only, never a PASS substitute.
    public string? AutomatedEvidenceReference { get; set; }
    public string? Note { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class UatFinding : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public required string Scope { get; set; }
    public required string ScenarioId { get; set; }
    public required string Severity { get; set; }
    public bool IsUx { get; set; }
    public bool DesignConformance { get; set; }
    public required string Title { get; set; }
    public string State { get; set; } = UatPolicy.Open;
    public Guid ReportedBy { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ResolutionReference { get; set; }
    public uint Version { get; set; }
}

// Human support/on-call readiness mark per checklist item (append-only lineage per item; latest counts).
public sealed class PilotSupportCheck : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Item { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousCheckId { get; set; }
    public required string State { get; set; }
    public string? EvidenceReference { get; set; }
    public required string Note { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// DEV-075 Product Owner decision bound to the exact review hash and snapshot. Append-only lineage per tenant.
public sealed class PilotGateDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid? PreviousDecisionId { get; set; }
    public required string Decision { get; set; }
    public required string Recommendation { get; set; }
    public required string ReviewHash { get; set; }
    // Closed codes only: gate states/reasons, P0 count, recommendation.
    public required string SnapshotJson { get; set; }
    public required string ConditionsJson { get; set; }
    public required string Rationale { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public Guid RoleId { get; set; }
    public required string Duty { get; set; }
    public required string AuthorityHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class PilotReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid RecordId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class PilotGateStorage
{
    public static readonly string[] ReceiptActions = ["UatThresholdRecord", "UatRunCreate", "UatResultRecord", "UatFindingRecord", "UatFindingResolve",
        "UatRunSubmit", "UatRunAttest", "PilotSupportCheckRecord", "PilotGateDecide"];
    public static readonly string[] Provenances = ["UserDeclaredUnverified", "SyntheticUnverified"];
    private static string In(IEnumerable<string> values) => "(" + string.Join(", ", values.Select(v => "'" + v + "'")) + ")";

    internal static void Configure(ModelBuilder model) {
        model.Entity<UatThreshold>(b => {
            b.ToTable("UatThresholds", "pilot", t => {
                t.HasCheckConstraint("CK_UatThreshold_Scope", "\"Scope\" IN " + In(UatScenarioCatalog.Scopes));
                t.HasCheckConstraint("CK_UatThreshold_Values", "\"MinScenarioPassPercent\" BETWEEN 1 AND 100 AND \"MaxOpenMajorFindings\" BETWEEN 0 AND 1000");
                t.HasCheckConstraint("CK_UatThreshold_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousThresholdId\" IS NULL)");
                t.HasCheckConstraint("CK_UatThreshold_Rationale", "char_length(\"Rationale\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Scope, x.Sequence }).IsUnique();
            b.Property(x => x.Scope).HasMaxLength(8); b.Property(x => x.AgreementReference).HasMaxLength(UatPolicy.ReferenceMax);
            b.Property(x => x.Rationale).HasMaxLength(UatPolicy.TextMax);
        });
        model.Entity<UatRun>(b => {
            b.ToTable("UatRuns", "pilot", t => {
                t.HasCheckConstraint("CK_UatRun_ScopeRole", "\"Scope\" = 'SHIP' AND \"Role\" IN " + In(UatScenarioCatalog.ShipRoles) +
                    " OR \"Scope\" = 'TUG' AND \"Role\" IN " + In(UatScenarioCatalog.TugRoles));
                t.HasCheckConstraint("CK_UatRun_State", "\"State\" IN " + In(UatPolicy.RunStates) + " AND \"Environment\" IN " + In(UatPolicy.Environments) +
                    " AND \"Provenance\" IN " + In(Provenances));
                t.HasCheckConstraint("CK_UatRun_Submitted", "(\"State\" = 'OPEN') = (\"SubmittedAt\" IS NULL)");
                // Attestation exists exactly in ATTESTED and is never by the facilitator (separation of duties).
                t.HasCheckConstraint("CK_UatRun_Attestation", "(\"State\" = 'ATTESTED') = (\"AttestedBy\" IS NOT NULL AND \"AttestedAt\" IS NOT NULL AND \"AttestationStatement\" IS NOT NULL)" +
                    " AND (\"AttestedBy\" IS NULL OR \"AttestedBy\" <> \"FacilitatorActorId\")");
                t.HasCheckConstraint("CK_UatRun_Synthetic", "\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN'");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Scope, x.CreatedAt });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Scope).HasMaxLength(8); b.Property(x => x.Role).HasMaxLength(40); b.Property(x => x.Environment).HasMaxLength(20);
            b.Property(x => x.BuildCommit).HasMaxLength(40); b.Property(x => x.ParticipantPseudonym).HasMaxLength(32); b.Property(x => x.CatalogVersion).HasMaxLength(40);
            b.Property(x => x.State).HasMaxLength(12); b.Property(x => x.AttestationStatement).HasMaxLength(UatPolicy.TextMax); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<UatStepResult>(b => {
            b.ToTable("UatStepResults", "pilot", t => {
                t.HasCheckConstraint("CK_UatStepResult_Outcome", "\"Outcome\" IN " + In(UatPolicy.Outcomes) + " AND \"Attempt\" >= 1");
                t.HasCheckConstraint("CK_UatStepResult_Duration", "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" BETWEEN 0 AND 86400");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.RunId, x.ScenarioId, x.Attempt }).IsUnique();
            b.Property(x => x.ScenarioId).HasMaxLength(12); b.Property(x => x.Outcome).HasMaxLength(10);
            b.Property(x => x.AutomatedEvidenceReference).HasMaxLength(UatPolicy.ReferenceMax); b.Property(x => x.Note).HasMaxLength(UatPolicy.TextMax);
            b.HasOne<UatRun>().WithMany().HasForeignKey(x => new { x.TenantId, x.RunId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<UatFinding>(b => {
            b.ToTable("UatFindings", "pilot", t => {
                t.HasCheckConstraint("CK_UatFinding_Values", "\"Severity\" IN " + In(UatPolicy.Severities) + " AND \"State\" IN " + In(UatPolicy.FindingStates) +
                    " AND \"Scope\" IN " + In(UatScenarioCatalog.Scopes));
                // A blocker is only cleared by a fix (RESOLVED), never by risk acceptance (19.01 p277).
                t.HasCheckConstraint("CK_UatFinding_Blocker", "\"Severity\" <> 'BLOCKER' OR \"State\" <> 'ACCEPTED_RISK'");
                t.HasCheckConstraint("CK_UatFinding_Resolution", "(\"State\" = 'OPEN') = (\"ResolvedBy\" IS NULL AND \"ResolvedAt\" IS NULL AND \"Resolution\" IS NULL)" +
                    " AND (\"ResolvedBy\" IS NULL OR \"ResolvedBy\" <> \"ReportedBy\")");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Scope, x.State });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Scope).HasMaxLength(8); b.Property(x => x.ScenarioId).HasMaxLength(12); b.Property(x => x.Severity).HasMaxLength(10);
            b.Property(x => x.Title).HasMaxLength(UatPolicy.TitleMax); b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.Resolution).HasMaxLength(UatPolicy.TextMax); b.Property(x => x.ResolutionReference).HasMaxLength(UatPolicy.ReferenceMax);
            b.HasOne<UatRun>().WithMany().HasForeignKey(x => new { x.TenantId, x.RunId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotSupportCheck>(b => {
            b.ToTable("PilotSupportChecks", "pilot", t => {
                t.HasCheckConstraint("CK_PilotSupportCheck_Values", "\"Item\" IN " + In(PilotGatePolicy.SupportItems) + " AND \"State\" IN " + In(PilotGatePolicy.SupportStates));
                t.HasCheckConstraint("CK_PilotSupportCheck_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousCheckId\" IS NULL)");
                // READY must point at the evidence a human checked (runbook, rota, contact list).
                t.HasCheckConstraint("CK_PilotSupportCheck_Evidence", "\"State\" <> 'READY' OR \"EvidenceReference\" IS NOT NULL");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Item, x.Sequence }).IsUnique();
            b.Property(x => x.Item).HasMaxLength(40); b.Property(x => x.State).HasMaxLength(10);
            b.Property(x => x.EvidenceReference).HasMaxLength(UatPolicy.ReferenceMax); b.Property(x => x.Note).HasMaxLength(UatPolicy.TextMax);
        });
        model.Entity<PilotGateDecision>(b => {
            b.ToTable("PilotGateDecisions", "pilot", t => {
                t.HasCheckConstraint("CK_PilotGateDecision_Decision", "\"Decision\" IN " + In(PilotGatePolicy.Decisions) +
                    " AND \"Recommendation\" IN " + In(PilotGatePolicy.Recommendations));
                // GO exists only against a GO-eligible review (every gate PASS, P0 known zero).
                t.HasCheckConstraint("CK_PilotGateDecision_Go", "\"Decision\" <> 'GO' OR \"Recommendation\" = 'GO_ELIGIBLE_PENDING_HUMAN_DECISION'");
                t.HasCheckConstraint("CK_PilotGateDecision_Duty", "\"Duty\" = 'ProductOwner'");
                t.HasCheckConstraint("CK_PilotGateDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                t.HasCheckConstraint("CK_PilotGateDecision_Rationale", "char_length(\"Rationale\") >= 10");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Sequence }).IsUnique();
            b.Property(x => x.Decision).HasMaxLength(16); b.Property(x => x.Recommendation).HasMaxLength(40);
            b.Property(x => x.ReviewHash).HasMaxLength(64); b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Rationale).HasMaxLength(PilotGatePolicy.RationaleMax);
            b.Property(x => x.SnapshotJson).HasColumnType("jsonb"); b.Property(x => x.ConditionsJson).HasColumnType("jsonb");
        });
        model.Entity<PilotReceipt>(b => {
            b.ToTable("PilotReceipts", "pilot", t => t.HasCheckConstraint("CK_PilotReceipt_Action", "\"Action\" IN " + In(ReceiptActions)));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
    }
    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static string Original(EntityEntry e, string name) => (string)e.Property(name).OriginalValue!;
    private static string Current(EntityEntry e, string name) => (string)e.Property(name).CurrentValue!;

    // Append-only human records; a run only moves OPEN -> SUBMITTED -> ATTESTED and a finding only OPEN -> resolved.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if (changes.Any(e => e.Entity is UatThreshold or UatStepResult or PilotSupportCheck or PilotGateDecision or PilotReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("UAT and pilot gate records are append-only.");
        if (changes.Any(e => e.Entity is UatRun r && (e.State == EntityState.Deleted ||
                e.State == EntityState.Added && (r.State != UatPolicy.RunOpen || r.SubmittedAt != null || r.AttestedBy != null) ||
                e.State == EntityState.Modified && (Changed(e, "State", "SubmittedAt", "AttestedBy", "AttestedAt", "AttestationStatement") ||
                    !(Original(e, "State") == UatPolicy.RunOpen && Current(e, "State") == UatPolicy.RunSubmitted && !e.Property("AttestedBy").IsModified ||
                      Original(e, "State") == UatPolicy.RunSubmitted && Current(e, "State") == UatPolicy.RunAttested && !e.Property("SubmittedAt").IsModified)))))
            throw new InvalidOperationException("A UAT run moves only OPEN -> SUBMITTED -> ATTESTED; its scope, role, build and participant are immutable.");
        if (changes.Any(e => e.Entity is UatFinding f && (e.State == EntityState.Deleted || e.State == EntityState.Added && f.State != UatPolicy.Open ||
                e.State == EntityState.Modified && (Changed(e, "State", "ResolvedBy", "ResolvedAt", "Resolution", "ResolutionReference") || Original(e, "State") != UatPolicy.Open))))
            throw new InvalidOperationException("A UAT finding is reported OPEN and resolved once; its severity and content are immutable.");
    }
}
