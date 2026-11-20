using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// DEV-087 towage plan (one per tug job). Mutable head (xmin), append-only revisions and Master/Tug Master
// approval records. An approval is a human record bound to one exact revision/content hash; it is never a
// permit, a GO decision, or an expert-approved limit catalogue. Synthetic rows can only be DRAFT.
public sealed class TowagePlan : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; } // the job's request context
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public long JobRevision { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = TowagePlanPolicy.Draft;
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public string AuthorsJson { get; set; } = "[]";
    public Guid? SubmittedBy { get; set; }
    public long? ApprovedRevision { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
public sealed class TowagePlanRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PlanId { get; set; }
    public long Revision { get; set; }
    public long JobRevision { get; set; }
    public string State { get; set; } = TowagePlanPolicy.Draft;
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
// Master/Tug Master APPROVE/RETURN of one exact in-review revision. Reason text never reaches audit summaries.
public sealed class TowagePlanDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public long Revision { get; set; }
    public long ResultRevision { get; set; }
    public string Decision { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public Guid TugId { get; set; }
    public Guid ActorId { get; set; }
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset DecidedAt { get; set; }
}

// DEV-088 towage risk & briefing (one per tug job). ISSUED pins the approved plan revision and the approved
// risk assessment revision; every participant acknowledges the exact issued revision for themselves.
public sealed class TowageBriefing : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = TowageBriefingPolicy.Draft;
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public Guid? PlanId { get; set; }
    public long? PlanRevision { get; set; }
    public long? RiskRevision { get; set; }
    public Guid? IssuedBy { get; set; }
    public Guid? IssuedAuthorityId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}
public sealed class TowageBriefingRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid BriefingId { get; set; }
    public long Revision { get; set; }
    public string State { get; set; } = TowageBriefingPolicy.Draft;
    public string ReasonCode { get; set; } = "";
    public string ContentJson { get; set; } = "{}";
    public string Hash { get; set; } = "";
    public long? PlanRevision { get; set; }
    public long? RiskRevision { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}
public sealed class TowageBriefingAcknowledgement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
    public Guid BriefingId { get; set; }
    public long Revision { get; set; }
    public Guid ActorId { get; set; }
    public string Choice { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string? Reason { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class TowageReceipt : ITenantEntity
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

public static class TowageStorage
{
    public static readonly string[] ReceiptActions = ["TowagePlanCreate", "TowagePlanUpdate", "TowagePlanSubmit", "TowagePlanDecide", "TowagePlanAmend",
        "TowageBriefingCreate", "TowageBriefingUpdate", "TowageBriefingIssue", "TowageBriefingAmend", "TowageBriefingAcknowledge"];
    public static readonly string[] PlanReasons = ["CREATE", "EDIT", "SUBMIT", "APPROVE", "RETURN", "AMEND", "SYNTHETIC_INCOMPLETE_DRAFT"];
    public static readonly string[] BriefingReasons = ["CREATE", "EDIT", "ISSUE", "AMEND", "SYNTHETIC_INCOMPLETE_DRAFT"];
    private static string In(IEnumerable<string> values) => string.Join(",", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model) {
        model.Entity<TowagePlan>(b => {
            b.ToTable("TowagePlans", "tug", t => t.HasCheckConstraint("CK_TowagePlan_State",
                "\"Revision\">0 AND \"State\" IN (" + In(TowagePlanPolicy.States) + ") AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND " +
                "(\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ApprovedRevision\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.JobId }).IsUnique();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.AuthorsJson).HasColumnType("text"); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<TugJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowagePlans_Job").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowagePlanRevision>(b => {
            b.ToTable("TowagePlanRevisions", "tug", t => t.HasCheckConstraint("CK_TowagePlanRevision_State",
                "\"Revision\">0 AND \"State\" IN (" + In(TowagePlanPolicy.States) + ") AND \"ReasonCode\" IN (" + In(PlanReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.PlanId, x.Revision });
            b.HasOne<TowagePlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.PlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowagePlanRevisions_Plan").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<TowagePlanDecision>(b => {
            b.ToTable("TowagePlanDecisions", "tug", t => {
                t.HasCheckConstraint("CK_TowagePlanDecision_Kind", "\"Decision\" IN ('APPROVE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10");
                t.HasCheckConstraint("CK_TowagePlanDecision_Duty", "\"Duty\" IN ('TugMaster','Master')");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.PlanId, x.Revision }).IsUnique();
            b.HasOne<TowagePlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.PlanId, x.Revision })
                .HasPrincipalKey(x => new { x.TenantId, x.PlanId, x.Revision }).HasConstraintName("FK_TowagePlanDecisions_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Decision).HasMaxLength(16); b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(20);
            b.Property(x => x.Reason).HasMaxLength(1000);
        });
        model.Entity<TowageBriefing>(b => {
            b.ToTable("TowageBriefings", "tug", t => t.HasCheckConstraint("CK_TowageBriefing_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','ISSUED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND " +
                "(\"State\"<>'ISSUED' OR \"PlanRevision\" IS NOT NULL AND \"RiskRevision\" IS NOT NULL AND \"IssuedBy\" IS NOT NULL AND \"IssuedAuthorityId\" IS NOT NULL) AND " +
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"IssuedBy\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.JobId }).IsUnique();
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Hash).HasMaxLength(64);
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<TugJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowageBriefings_Job").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TowageBriefingRevision>(b => {
            b.ToTable("TowageBriefingRevisions", "tug", t => t.HasCheckConstraint("CK_TowageBriefingRevision_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','ISSUED') AND \"ReasonCode\" IN (" + In(BriefingReasons) + ")"));
            b.HasKey(x => new { x.TenantId, x.BriefingId, x.Revision });
            b.HasOne<TowageBriefing>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.BriefingId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_TowageBriefingRevisions_Briefing").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.ReasonCode).HasMaxLength(32); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Hash).HasMaxLength(64); b.Property(x => x.Provenance).HasMaxLength(32);
        });
        model.Entity<TowageBriefingAcknowledgement>(b => {
            b.ToTable("TowageBriefingAcknowledgements", "tug", t => t.HasCheckConstraint("CK_TowageBriefingAcknowledgement_Choice",
                "\"Choice\" IN ('ACK','DISPUTE') AND (\"Choice\"<>'DISPUTE' OR length(\"Reason\")>=10)"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => new { x.TenantId, x.BriefingId, x.Revision, x.ActorId }).IsUnique();
            b.HasOne<TowageBriefingRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.BriefingId, x.Revision })
                .HasPrincipalKey(x => new { x.TenantId, x.BriefingId, x.Revision }).HasConstraintName("FK_TowageBriefingAcknowledgements_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Choice).HasMaxLength(16); b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.Reason).HasMaxLength(1000);
        });
        model.Entity<TowageReceipt>(b => {
            b.ToTable("TowageReceipts", "tug", t => t.HasCheckConstraint("CK_TowageReceipt_Action", "\"Action\" IN (" + In(ReceiptActions) + ") AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static bool Changed(EntityEntry e, params string[] allowed) => e.Properties.Any(p => p.IsModified && !allowed.Contains(p.Metadata.Name));
    private static bool OneStep(EntityEntry e) => (long)e.Property("Revision").CurrentValue! == (long)e.Property("Revision").OriginalValue! + 1;

    // Called by PoseidonDbContext before audit projection; history, decisions, acknowledgements and receipts are append-only.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if (changes.Any(e => e.Entity is TowagePlanRevision or TowagePlanDecision or TowageBriefingRevision or TowageBriefingAcknowledgement or TowageReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Towage history, decisions, acknowledgements and receipts are immutable.");
        if (changes.Any(e => e.Entity is TowagePlan && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "JobRevision", "State", "ContentJson", "Hash", "AuthorsJson", "SubmittedBy", "ApprovedRevision", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Towage plan ownership is immutable; append one revision.");
        if (changes.Any(e => e.Entity is TowageBriefing && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (Changed(e, "Revision", "State", "ContentJson", "Hash", "PlanId", "PlanRevision", "RiskRevision", "IssuedBy", "IssuedAuthorityId", "ActorId", "SavedAt") || !OneStep(e)))))
            throw new InvalidOperationException("Towage briefing ownership is immutable; append one revision.");
    }
}
