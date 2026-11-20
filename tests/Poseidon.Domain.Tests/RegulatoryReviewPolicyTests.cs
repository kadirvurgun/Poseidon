using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S11 WP-C: DEV-073 / T-S11-005 (18.01 Backlog row 74) and DEV-093 / T-TUG-014 (row 94); 19.01 paragraphs 124-129.
// FICTIONAL inputs only; the policy never confirms anything by itself.
public sealed class RegulatoryReviewPolicyTests
{
    private static readonly RegulatoryReviewPolicy.ReviewerFacts Qualified = new(true, ["STATUTORY", "FLAG", "CLASS", "MANUFACTURER"], false);
    private static RegulatoryReviewPolicy.VerdictInput Input(string verdict = "CONFIRMED", string? authority = "STATUTORY", string owner = "REGULATORY_LEAD",
        string? reference = null, string? edition = null, string? issue = null, string note = "FICTIONAL reviewer note for the pilot profile") =>
        new(verdict, authority, owner, reference, edition, issue, note);
    private static RegulatoryReviewPolicy.ItemFacts Pinned(bool stale = false) => new(RegulatoryReviewPolicy.RuleItem, true, stale);
    private static RegulatoryReviewPolicy.ItemFacts Unpinned(string kind = RegulatoryReviewPolicy.SourceCandidate) => new(kind, false, false);

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void A_qualified_non_author_confirms_a_pinned_source_item_with_authority_and_owner() {
        Assert.Null(RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.InReview, Pinned(), Input(), Qualified));
        Assert.Null(RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(), Input("NOT_APPLICABLE_CONFIRMED"), Qualified));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void Unknown_or_ambiguous_stays_review_required_with_a_tracked_issue() {
        Assert.Equal("tracked_issue_required", RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(), Input("AMBIGUOUS"), Qualified));
        Assert.Equal("tracked_issue_required", RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(), Input("REVIEW_REQUIRED"), Qualified));
        Assert.Null(RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(), Input("AMBIGUOUS", issue: "REG-1042"), Qualified));
        // An unqualified reviewer may still track an open item, a stale item can still be tracked.
        Assert.Null(RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(true), Input("REVIEW_REQUIRED", issue: "GH#17"), Qualified with { QualificationVerified = false }));
        Assert.True(RegulatoryReviewPolicy.IsOpen(RegulatoryReviewPolicy.ReviewRequired, false));
        Assert.True(RegulatoryReviewPolicy.IsOpen(RegulatoryReviewPolicy.Ambiguous, false));
        Assert.False(RegulatoryReviewPolicy.IsOpen(RegulatoryReviewPolicy.Confirmed, false));
        Assert.Equal(RegulatoryReviewPolicy.ReviewRequired, RegulatoryReviewPolicy.Effective(RegulatoryReviewPolicy.Confirmed, true));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void Confirmation_is_refused_without_qualification_authority_owner_source_or_with_a_stale_pin() {
        var open = RegulatoryReviewPolicy.Open;
        Assert.Equal("reviewer_qualification_unverified", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(), Qualified with { QualificationVerified = false }));
        Assert.Equal("separation_of_duties", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(), Qualified with { IsCampaignAuthor = true }));
        Assert.Equal("separation_of_duties", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input("AMBIGUOUS", issue: "REG-1"), Qualified with { IsCampaignAuthor = true }));
        Assert.Equal("item_revision_stale", RegulatoryReviewPolicy.Refusal(open, Pinned(true), Input(), Qualified));
        Assert.Equal("source_authority_required", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(authority: null), Qualified));
        Assert.Equal("reviewer_authority_required", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(authority: "PORT_LOCAL"), Qualified));
        Assert.Equal("source_owner_required", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(owner: "UNASSIGNED"), Qualified));
        // An unpinned candidate needs the authoritative reference AND edition.
        Assert.Equal("source_reference_required", RegulatoryReviewPolicy.Refusal(open, Unpinned(), Input(reference: "IMO ISM Code"), Qualified));
        Assert.Null(RegulatoryReviewPolicy.Refusal(open, Unpinned(), Input(reference: "IMO ISM Code", edition: "2018 consolidated"), Qualified));
        Assert.Equal("campaign_closed", RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Closed, Pinned(), Input(), Qualified));
        Assert.Equal("validation_failed", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(note: "short"), Qualified));
        Assert.Equal("validation_failed", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(verdict: "APPROVED"), Qualified));
        Assert.Equal("validation_failed", RegulatoryReviewPolicy.Refusal(open, Pinned(), Input(owner: "ADMIN"), Qualified));
    }

    [Fact]
    [Trait("Requirement", "DEV-093")]
    public void Tug_limits_are_never_legal_defaults_guidance_is_separate_and_no_approval_claim_is_recorded() {
        // 19.01 p126: each towage limit parameter is an item; without a controlled source it is blocked.
        Assert.Equal(TowagePlanPolicy.LimitParameters, RegulatoryReviewPolicy.TugLimitParameters);
        Assert.Contains("LIMIT_SOURCE_MISSING", RegulatoryReviewPolicy.Blockers(RegulatoryReviewPolicy.TugLimit, "REVIEW_REQUIRED", false, false, null, null, false));
        Assert.Equal("source_reference_required", RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Unpinned(RegulatoryReviewPolicy.TugLimit),
            Input(authority: "MANUFACTURER"), Qualified));
        // 19.01 p127: manufacturer / industry guidance is not statutory law.
        Assert.Equal("GUIDANCE_NOT_LAW", RegulatoryReviewPolicy.Category("MANUFACTURER"));
        Assert.Equal("GUIDANCE_NOT_LAW", RegulatoryReviewPolicy.Category("INDUSTRY_GUIDANCE"));
        Assert.Equal("STATUTORY_LAW", RegulatoryReviewPolicy.Category("FLAG"));
        Assert.Equal("CLASS_RULE", RegulatoryReviewPolicy.Category("CLASS"));
        Assert.Equal("UNASSIGNED", RegulatoryReviewPolicy.Category(null));
        // 19.01 p129: no class/type approval claim.
        foreach (var claim in new[] { "Winch is type approved", "class-approved release", "Certified by class", "tip onaylı vinç", "Klas onaylı" })
            Assert.Equal("unsupported_approval_claim", RegulatoryReviewPolicy.Refusal(RegulatoryReviewPolicy.Open, Pinned(), Input(note: claim + " FICTIONAL"), Qualified));
        Assert.Equal(["PORT_LOCAL", "COMPANY_SMS", "MANUFACTURER", "INDUSTRY_GUIDANCE"], RegulatoryReviewPolicy.OwnershipSlots(RegulatoryReviewPolicy.Tug));
        Assert.All(RegulatoryReviewPolicy.Candidates.Where(c => c.ReviewScopes.Contains(RegulatoryReviewPolicy.Tug)), c => Assert.Contains(c.Authority, new[] { "STATUTORY", "FLAG", "CLASS" }));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void Closing_with_open_items_is_explicit_and_every_open_item_is_tracked() {
        const string reason = "FICTIONAL pilot review window ended";
        Assert.Equal("open_items_remain", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.InReview, RegulatoryReviewPolicy.Closed, 2, 0, reason));
        Assert.Equal("open_item_untracked", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.InReview, RegulatoryReviewPolicy.ClosedWithOpenItems, 2, 1, reason));
        Assert.Null(RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.InReview, RegulatoryReviewPolicy.ClosedWithOpenItems, 2, 0, reason));
        Assert.Equal("no_open_items", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.InReview, RegulatoryReviewPolicy.ClosedWithOpenItems, 0, 0, reason));
        Assert.Null(RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.InReview, RegulatoryReviewPolicy.Closed, 0, 0, reason));
        Assert.Equal("campaign_closed", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.ClosedWithOpenItems, RegulatoryReviewPolicy.Closed, 0, 0, reason));
        Assert.Equal("validation_failed", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.Open, "ACTIVATED", 0, 0, reason));
        Assert.Equal("unsupported_approval_claim", RegulatoryReviewPolicy.CloseRefusal(RegulatoryReviewPolicy.Open, RegulatoryReviewPolicy.Closed, 0, 0, "Set is type approved now"));
    }

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public void Blockers_name_staleness_unresolved_sources_unverified_candidates_and_untracked_items() {
        var b = RegulatoryReviewPolicy.Blockers(RegulatoryReviewPolicy.SourceCandidate, "REVIEW_REQUIRED", true, false, null, null, true);
        Assert.Equal(["ITEM_REVISION_STALE", "AUTHORITATIVE_SOURCE_UNRESOLVED", "CANDIDATE_UNVERIFIED", "TRACKED_ISSUE_MISSING"], b);
        Assert.Contains("SOURCE_OWNERSHIP_UNASSIGNED", RegulatoryReviewPolicy.Blockers(RegulatoryReviewPolicy.OwnershipSlot, "REVIEW_REQUIRED", false, false, null, null, false));
        Assert.Empty(RegulatoryReviewPolicy.Blockers(RegulatoryReviewPolicy.RuleItem, "CONFIRMED", false, true, null, null, false));
    }
}
