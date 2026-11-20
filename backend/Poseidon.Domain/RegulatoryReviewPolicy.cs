using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// S11 WP-C DEV-073 / T-S11-005 (18.01 Backlog row 74) and DEV-093 / T-TUG-014 (row 94); 19.01 paragraphs 124-129.
// A regulatory SME review campaign pins the pilot vessel profile and the applicability/source items under review.
// The system never confirms anything: every item starts REVIEW_REQUIRED and only a server-provisioned, qualified human
// reviewer (never the campaign author) records CONFIRMED / NOT_APPLICABLE_CONFIRMED against an authoritative source and
// version. Open ambiguity stays REVIEW_REQUIRED/AMBIGUOUS with a tracked issue; closing never activates a rule or permit.
public static class RegulatoryReviewPolicy
{
    public const string Version = "regulatory-sme-review-v1";
    public const string Ship = "SHIP", Tug = "TUG";
    public static readonly string[] Scopes = [Ship, Tug];

    public const string Open = "OPEN", InReview = "IN_REVIEW", ClosedWithOpenItems = "CLOSED_WITH_OPEN_ITEMS", Closed = "CLOSED";
    public static readonly string[] States = [Open, InReview, ClosedWithOpenItems, Closed];
    public static readonly string[] CloseOutcomes = [ClosedWithOpenItems, Closed];

    public const string Confirmed = "CONFIRMED", Ambiguous = "AMBIGUOUS", NotApplicableConfirmed = "NOT_APPLICABLE_CONFIRMED", ReviewRequired = "REVIEW_REQUIRED";
    public static readonly string[] Verdicts = [Confirmed, Ambiguous, NotApplicableConfirmed, ReviewRequired];

    // 19.01 p125/p127: flag/class/local/company SMS ownership; manufacturer and industry guidance are tagged separately.
    public static readonly string[] Authorities = ["STATUTORY", "FLAG", "CLASS", "PORT_LOCAL", "COMPANY_SMS", "MANUFACTURER", "INDUSTRY_GUIDANCE"];

    // 18.01 RACI (Regulatory applicability: Regulatory SME A/R, BA R, PO A) and the DEV-073/093 owner role (Regulatory Lead).
    // DPA is the ISM Code designated person for company SMS sources. UNASSIGNED is the honest default, never "owned".
    public const string Unassigned = "UNASSIGNED";
    public static readonly string[] OwnerRoles = [Unassigned, "REGULATORY_LEAD", "REGULATORY_SME", "PRODUCT_OWNER", "DPA", "PILOT_CUSTOMER"];

    public const string RuleItem = "APPLICABILITY_RULE", SourceCandidate = "SOURCE_CANDIDATE", TugLimit = "TUG_LIMIT", OwnershipSlot = "OWNERSHIP_SLOT";
    public static readonly string[] Kinds = [RuleItem, SourceCandidate, TugLimit, OwnershipSlot];

    public const string QualificationVerified = "VERIFIED";
    public const int NoteMin = 10, NoteMax = 2000, ReferenceMax = 200, EditionMax = 80, IssueMax = 64, CloseReasonMax = 2000;
    public const int MaxRuleItems = 64;

    // Candidate sources imported from contracts/sources/pilot-ownership.v1.json (scripts/pilot_source_ownership.py, DEV-008).
    // The plan is AUTHORED_UNVERIFIED with reviewerQualification UNVERIFIED and activationBlocked: the candidate authority label
    // is a plan classification for the reviewer to confirm or correct, never an established fact.
    public sealed record OwnershipCandidate(string Code, string PlanScope, string Authority, string Issuer, string Instrument, string Locator, string[] ReviewScopes);
    public static readonly OwnershipCandidate[] Candidates = [
        new("SRC-ISM", "ISM", "STATUTORY", "IMO", "ISM Code / SOLAS IX", "sheet:Kaynak Register/row:2", [Ship, Tug]),
        new("SRC-SOLAS", "SOLAS", "STATUTORY", "IMO", "SOLAS 1974 as amended", "sheet:Kaynak Register/row:3", [Ship]),
        new("SRC-STCW", "STCW", "STATUTORY", "IMO", "STCW Convention & Code", "sheet:Kaynak Register/row:4", [Ship]),
        new("SRC-MLC", "MLC", "STATUTORY", "ILO", "MLC 2006 as amended", "sheet:Kaynak Register/row:5", [Ship]),
        new("SRC-MARPOL", "MARPOL", "STATUTORY", "IMO", "MARPOL Annex I-VI", "sheet:Kaynak Register/row:6", [Ship]),
        new("SRC-TR-ISM-2023-19", "Flag", "FLAG", "UAB DGM", "ISM Kod Gereklilikleri Hakkında Uygulama Talimatı 2023/19", "sheet:Kaynak Register/row:15", [Ship, Tug]),
        new("SRC-IACS-M79", "Class", "CLASS", "IACS / Türk Loydu mirror", "UR M79 - Towing winch emergency release systems", "sheet:Kaynak Register/row:17", [Tug]),
        new("SRC-TL-OCEAN-TOW", "Class", "CLASS", "Türk Loydu", "Chapter 58 - Ocean Towage", "sheet:Kaynak Register/row:18", [Tug]),
        new("SRC-TL-BP", "Class", "CLASS", "Türk Loydu", "Bollard Pull Test Certification", "sheet:Kaynak Register/row:19", [Tug]),
    ];
    public const string CandidateSourcePlan = "contracts/sources/pilot-ownership.v1.json";

    // Source ownership that the pilot must assign but for which no candidate source exists: an explicit open slot.
    public static string[] OwnershipSlots(string scope) => scope == Tug
        ? ["PORT_LOCAL", "COMPANY_SMS", "MANUFACTURER", "INDUSTRY_GUIDANCE"]
        : ["PORT_LOCAL", "COMPANY_SMS"];

    // 19.01 p126: weather/current/visibility/BP limits are never legal defaults without a controlled source.
    public static string[] TugLimitParameters => TowagePlanPolicy.LimitParameters;

    public static string Category(string? authority) => authority switch {
        "STATUTORY" or "FLAG" or "PORT_LOCAL" => "STATUTORY_LAW",
        "CLASS" => "CLASS_RULE",
        "COMPANY_SMS" => "COMPANY_SMS",
        "MANUFACTURER" or "INDUSTRY_GUIDANCE" => "GUIDANCE_NOT_LAW",
        _ => "UNASSIGNED"
    };

    public static bool Confirming(string verdict) => verdict is Confirmed or NotApplicableConfirmed;
    public static bool Terminal(string state) => state is Closed or ClosedWithOpenItems;
    // A stale item is reviewed again: its effective verdict is REVIEW_REQUIRED regardless of the recorded one.
    public static string Effective(string verdict, bool stale) => stale ? ReviewRequired : verdict;
    public static bool IsOpen(string verdict, bool stale) => !Confirming(Effective(verdict, stale));

    // 19.01 p129: no class/type approval or certification claim until a formal external assessment.
    private static readonly Regex Claim = new(@"type[\s-]*approv|class[\s-]*approv|approved\s+by\s+class|certified|certificated|tip\s+onay|klas\s+onay|sertifikal",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool UnsupportedClaim(params string?[] values) => values.Any(v => v != null && Claim.IsMatch(v));
    private static readonly Regex Issue = new(@"\A[A-Za-z0-9][A-Za-z0-9._#/-]{1,63}\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static bool ValidIssue(string? v) => v != null && v.Length <= IssueMax && Issue.IsMatch(v);
    public static bool Text(string? v, int min, int max) => v != null && v.Length >= min && v.Length <= max && v.Trim() == v && !v.Any(c => char.IsControl(c) && c is not ('\n' or '\r'));
    private static bool Opt(string? v, int max) => v == null || Text(v, 1, max);

    public sealed record ItemFacts(string Kind, bool HasPinnedSource, bool Stale);
    public sealed record VerdictInput(string Verdict, string? SourceAuthority, string OwnerRole, string? SourceReference, string? SourceEdition, string? TrackedIssueId, string Note);
    public sealed record ReviewerFacts(bool QualificationVerified, string[] Authorities, bool IsCampaignAuthor);

    // Shape validation (400) separate from review refusals (409).
    public static bool ValidVerdict(VerdictInput v) =>
        Verdicts.Contains(v.Verdict) && OwnerRoles.Contains(v.OwnerRole) && (v.SourceAuthority == null || Authorities.Contains(v.SourceAuthority)) &&
        Opt(v.SourceReference, ReferenceMax) && Opt(v.SourceEdition, EditionMax) && (v.TrackedIssueId == null || ValidIssue(v.TrackedIssueId)) && Text(v.Note, NoteMin, NoteMax);

    public static string? Refusal(string campaignState, ItemFacts item, VerdictInput v, ReviewerFacts r) {
        if (!ValidVerdict(v)) return "validation_failed";
        if (UnsupportedClaim(v.Note, v.SourceReference, v.SourceEdition)) return "unsupported_approval_claim";
        if (Terminal(campaignState)) return "campaign_closed";
        if (r.IsCampaignAuthor) return "separation_of_duties";
        // A stale item can still be tracked as open (so the campaign can close with open items), never confirmed.
        if (!Confirming(v.Verdict)) return v.TrackedIssueId == null ? "tracked_issue_required" : null;
        if (item.Stale) return "item_revision_stale";
        if (!r.QualificationVerified) return "reviewer_qualification_unverified";
        if (v.SourceAuthority == null) return "source_authority_required";
        if (!r.Authorities.Contains(v.SourceAuthority)) return "reviewer_authority_required";
        if (v.OwnerRole == Unassigned) return "source_owner_required";
        // Authoritative source AND version: a pinned register source revision, or an explicit reference plus edition.
        if (!item.HasPinnedSource && (v.SourceReference == null || v.SourceEdition == null)) return "source_reference_required";
        return null;
    }

    public static string? CloseRefusal(string state, string outcome, int open, int untrackedOpen, string? reason) {
        if (!CloseOutcomes.Contains(outcome) || !Text(reason, NoteMin, CloseReasonMax)) return "validation_failed";
        if (UnsupportedClaim(reason)) return "unsupported_approval_claim";
        if (Terminal(state)) return "campaign_closed";
        if (outcome == Closed && open > 0) return "open_items_remain";
        if (outcome == ClosedWithOpenItems && open == 0) return "no_open_items";
        // An open ambiguity remains REVIEW_REQUIRED and must be a tracked issue (18.01 row 74).
        if (untrackedOpen > 0) return "open_item_untracked";
        return null;
    }

    // Item-level blockers shown to the reader (codes only).
    public static string[] Blockers(string kind, string verdict, bool stale, bool hasPinnedSource, string? sourceReference, string? trackedIssueId, bool candidateUnverified) {
        var b = new List<string>();
        if (stale) b.Add("ITEM_REVISION_STALE");
        if (kind == TugLimit && sourceReference == null) b.Add("LIMIT_SOURCE_MISSING");
        if (kind == OwnershipSlot && verdict == ReviewRequired) b.Add("SOURCE_OWNERSHIP_UNASSIGNED");
        if (!hasPinnedSource && sourceReference == null && kind != TugLimit) b.Add("AUTHORITATIVE_SOURCE_UNRESOLVED");
        if (candidateUnverified && !Confirming(verdict)) b.Add("CANDIDATE_UNVERIFIED");
        if (IsOpen(verdict, stale) && trackedIssueId == null) b.Add("TRACKED_ISSUE_MISSING");
        return [.. b];
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
