using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// S11 WP-C DEV-073/093: regulatory SME review campaign for one pilot vessel and one scope (SHIP|TUG). The campaign pins the
// vessel profile revision and the applicability evaluation it was generated from; at most one active campaign per vessel+scope.
public sealed class RegulatoryReviewCampaign : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public required string Scope { get; set; }
    public string State { get; set; } = RegulatoryReviewPolicy.Open;
    public long? ProfileRevision { get; set; }
    public Guid? EvaluationId { get; set; }
    public required string PinHash { get; set; }
    public int ItemCount { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public int? OpenItemsAtClose { get; set; }
    public required string PolicyVersion { get; set; }
    public string Provenance { get; set; } = "Recorded";
    public uint Version { get; set; }
}

// One reviewable item with its revision pins. Only the current verdict columns change (each change appends a verdict row);
// pins, kind and subject are immutable. Default and synthetic items are REVIEW_REQUIRED with no reviewer.
public sealed class RegulatoryReviewItem : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid VesselId { get; set; }
    public int Ordinal { get; set; }
    public required string Kind { get; set; }
    public required string SubjectKey { get; set; }
    public Guid? RuleId { get; set; }
    public long? RuleRevision { get; set; }
    public Guid? RequirementId { get; set; }
    public long? RequirementRevision { get; set; }
    public Guid? SourceId { get; set; }
    public long? SourceRevision { get; set; }
    public string? CandidateSourceCode { get; set; }
    public string? CandidateAuthority { get; set; }
    public bool CandidateUnverified { get; set; }
    public string? PredictedResult { get; set; }
    public string Verdict { get; set; } = RegulatoryReviewPolicy.ReviewRequired;
    public string? SourceAuthority { get; set; }
    public string OwnerRole { get; set; } = RegulatoryReviewPolicy.Unassigned;
    public string? SourceReference { get; set; }
    public string? SourceEdition { get; set; }
    public string? TrackedIssueId { get; set; }
    public Guid? ReviewerId { get; set; }
    public Guid? ReviewerRoleId { get; set; }
    public Guid? ReviewerAuthorityId { get; set; }
    public string? QualificationRef { get; set; }
    public int VerdictCount { get; set; }
    public DateTimeOffset? VerdictAt { get; set; }
    public Guid CreatedBy { get; set; }
    public string Provenance { get; set; } = "Recorded";
    public uint Version { get; set; }
}

// Append-only human verdict lineage (Sequence 1..n per item) bound to the item pin hash and the reviewer appointment.
public sealed class RegulatoryReviewVerdict : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ItemId { get; set; }
    public Guid VesselId { get; set; }
    public int Sequence { get; set; }
    public required string Verdict { get; set; }
    public string? SourceAuthority { get; set; }
    public required string OwnerRole { get; set; }
    public string? SourceReference { get; set; }
    public string? SourceEdition { get; set; }
    public string? TrackedIssueId { get; set; }
    public required string Note { get; set; }
    public required string PinHash { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public required string QualificationRef { get; set; }
    public required string QualificationState { get; set; }
    public required string AuthorityHash { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class RegulatoryReviewReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid CampaignId { get; set; }
    public Guid RecordId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}

public static class RegulatoryReviewStorage
{
    public static readonly string[] ReceiptActions = ["RegulatoryReviewCreate", "RegulatoryReviewVerdict", "RegulatoryReviewClose"];
    public static readonly string[] ItemVerdictColumns = ["Verdict", "SourceAuthority", "OwnerRole", "SourceReference", "SourceEdition", "TrackedIssueId",
        "ReviewerId", "ReviewerRoleId", "ReviewerAuthorityId", "QualificationRef", "VerdictCount", "VerdictAt"];
    public static readonly string[] CampaignMutableColumns = ["State", "ClosedBy", "ClosedAt", "CloseReason", "OpenItemsAtClose"];
    private static string In(IEnumerable<string> values) => string.Join(", ", values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RegulatoryReviewCampaign>(b => {
            b.ToTable("RegulatoryReviewCampaigns", t => {
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_Scope", "\"Scope\" IN (" + In(RegulatoryReviewPolicy.Scopes) + ")");
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_State", "\"State\" IN (" + In(RegulatoryReviewPolicy.States) + ")");
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_Closure", "(\"State\" IN ('CLOSED', 'CLOSED_WITH_OPEN_ITEMS')) = (\"ClosedBy\" IS NOT NULL AND \"ClosedAt\" IS NOT NULL AND \"CloseReason\" IS NOT NULL AND \"OpenItemsAtClose\" IS NOT NULL)");
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_ClosedOpen", "\"State\" <> 'CLOSED' OR \"OpenItemsAtClose\" = 0");
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_WithOpen", "\"State\" <> 'CLOSED_WITH_OPEN_ITEMS' OR \"OpenItemsAtClose\" > 0");
                // A synthetic campaign is never reviewed or closed.
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_Synthetic", "\"Provenance\" IN ('Recorded', 'SyntheticUnverified') AND (\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN')");
                t.HasCheckConstraint("CK_RegulatoryReviewCampaign_Items", "\"ItemCount\" >= 0");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Scope }).IsUnique().HasFilter("\"State\" = 'OPEN' OR \"State\" = 'IN_REVIEW'")
                .HasDatabaseName("UX_RegulatoryReviewCampaigns_Active");
            b.Property(x => x.Scope).HasMaxLength(8); b.Property(x => x.State).HasMaxLength(32); b.Property(x => x.PinHash).HasMaxLength(64);
            b.Property(x => x.CloseReason).HasMaxLength(RegulatoryReviewPolicy.CloseReasonMax); b.Property(x => x.PolicyVersion).HasMaxLength(40); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryReviewItem>(b => {
            b.ToTable("RegulatoryReviewItems", t => {
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Kind", "\"Kind\" IN (" + In(RegulatoryReviewPolicy.Kinds) + ")");
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Verdict", "\"Verdict\" IN (" + In(RegulatoryReviewPolicy.Verdicts) + ")");
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Authority", "(\"SourceAuthority\" IS NULL OR \"SourceAuthority\" IN (" + In(RegulatoryReviewPolicy.Authorities) + ")) AND (\"CandidateAuthority\" IS NULL OR \"CandidateAuthority\" IN (" + In(RegulatoryReviewPolicy.Authorities) + "))");
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Owner", "\"OwnerRole\" IN (" + In(RegulatoryReviewPolicy.OwnerRoles) + ")");
                // A confirming verdict carries a human reviewer, a qualification reference, an authority label and an owner.
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Confirmed", "\"Verdict\" NOT IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"ReviewerId\" IS NOT NULL AND \"QualificationRef\" IS NOT NULL AND \"SourceAuthority\" IS NOT NULL AND \"OwnerRole\" <> 'UNASSIGNED' AND \"VerdictCount\" > 0");
                // A recorded verdict (any) has a reviewer; a reviewer is never the campaign author.
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Reviewer", "(\"VerdictCount\" = 0) = (\"ReviewerId\" IS NULL) AND (\"ReviewerId\" IS NULL OR \"ReviewerId\" <> \"CreatedBy\")");
                t.HasCheckConstraint("CK_RegulatoryReviewItem_Synthetic", "\"Provenance\" IN ('Recorded', 'SyntheticUnverified') AND (\"Provenance\" <> 'SyntheticUnverified' OR \"Verdict\" = 'REVIEW_REQUIRED' AND \"VerdictCount\" = 0)");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.TenantId, x.CampaignId, x.Ordinal }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CampaignId, x.Kind, x.SubjectKey }).IsUnique();
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.SubjectKey).HasMaxLength(80); b.Property(x => x.CandidateSourceCode).HasMaxLength(40);
            b.Property(x => x.CandidateAuthority).HasMaxLength(32); b.Property(x => x.PredictedResult).HasMaxLength(40); b.Property(x => x.Verdict).HasMaxLength(32);
            b.Property(x => x.SourceAuthority).HasMaxLength(32); b.Property(x => x.OwnerRole).HasMaxLength(32);
            b.Property(x => x.SourceReference).HasMaxLength(RegulatoryReviewPolicy.ReferenceMax); b.Property(x => x.SourceEdition).HasMaxLength(RegulatoryReviewPolicy.EditionMax);
            b.Property(x => x.TrackedIssueId).HasMaxLength(RegulatoryReviewPolicy.IssueMax); b.Property(x => x.QualificationRef).HasMaxLength(200); b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<RegulatoryReviewCampaign>().WithMany().HasForeignKey(x => new { x.TenantId, x.CampaignId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RuleRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.RuleId, x.RuleRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryReviewVerdict>(b => {
            b.ToTable("RegulatoryReviewVerdicts", t => {
                t.HasCheckConstraint("CK_RegulatoryReviewVerdict_Verdict", "\"Verdict\" IN (" + In(RegulatoryReviewPolicy.Verdicts) + ")");
                t.HasCheckConstraint("CK_RegulatoryReviewVerdict_Confirmed", "\"Verdict\" NOT IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"QualificationState\" = 'VERIFIED' AND \"SourceAuthority\" IS NOT NULL AND \"OwnerRole\" <> 'UNASSIGNED'");
                t.HasCheckConstraint("CK_RegulatoryReviewVerdict_Open", "\"Verdict\" IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"TrackedIssueId\" IS NOT NULL");
                t.HasCheckConstraint("CK_RegulatoryReviewVerdict_Note", "char_length(\"Note\") >= 10 AND \"Sequence\" >= 1");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.ItemId, x.Sequence }).IsUnique();
            b.Property(x => x.Verdict).HasMaxLength(32); b.Property(x => x.SourceAuthority).HasMaxLength(32); b.Property(x => x.OwnerRole).HasMaxLength(32);
            b.Property(x => x.SourceReference).HasMaxLength(RegulatoryReviewPolicy.ReferenceMax); b.Property(x => x.SourceEdition).HasMaxLength(RegulatoryReviewPolicy.EditionMax);
            b.Property(x => x.TrackedIssueId).HasMaxLength(RegulatoryReviewPolicy.IssueMax); b.Property(x => x.Note).HasMaxLength(RegulatoryReviewPolicy.NoteMax);
            b.Property(x => x.PinHash).HasMaxLength(64); b.Property(x => x.QualificationRef).HasMaxLength(200); b.Property(x => x.QualificationState).HasMaxLength(20);
            b.Property(x => x.AuthorityHash).HasMaxLength(64);
            b.HasOne<RegulatoryReviewItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.ItemId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegulatoryReviewCampaign>().WithMany().HasForeignKey(x => new { x.TenantId, x.CampaignId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryReviewReceipt>(b => {
            b.ToTable("RegulatoryReviewReceipts", t => t.HasCheckConstraint("CK_RegulatoryReviewReceipt_Action", "\"Action\" IN (" + In(ReceiptActions) + ")"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(36); b.Property(x => x.Action).HasMaxLength(40); b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<RegulatoryReviewCampaign>().WithMany().HasForeignKey(x => new { x.TenantId, x.CampaignId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
