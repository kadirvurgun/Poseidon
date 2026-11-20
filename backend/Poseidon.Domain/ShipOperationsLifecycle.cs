using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// Exact pin to an existing Control & Evidence revision. A link is lineage for a
// human reviewer: it does not adopt, verify, share or assess the evidence.
public sealed record DowntimeEvidencePin(Guid Id, long Revision)
{
    public override string ToString() => nameof(DowntimeEvidencePin);
}
public sealed record DowntimeLifecycleDto(string State, DowntimeEvidencePin[] ClaimEvidence, string LossOfHireCandidate, string[] AllowedActions);

// DEV-101 SOF finalization and DEV-102 downtime closure lifecycle.
// 60.01 Rev3 paragraphs 1636-1638 / 1680-1688 / 2102-2116; 11.01 rows 2224/2239.
// Every state change is an explicit attributed human command. Nothing here is a
// signature, an external agreement, a financial claim decision or a safety release.
public static class ShipOperationsLifecycle
{
    public const string SofDraft = "DRAFT", SofFinal = "FINAL";
    public const string SofSignature = "SOF_SIGNATURE_NOT_RECORDED";
    public const string SofReason = "HUMAN_SOF_FINALIZATION";
    public const string PostFinalAmendment = "POST_FINAL_AMENDMENT";
    // 11.01 row 2224 K: "Master; Chief Officer; Chief Engineer as scoped".
    public static readonly string[] SofDuties = ["Master", "ChiefOfficer", "ChiefEngineer"];
    // 11.01 row 2239 K adds "DPA/Fleet Manager read/review" for downtime.
    public static readonly string[] DowntimeDuties = ["Master", "ChiefOfficer", "ChiefEngineer", "FleetManager", "DPA"];
    public const string SofScope = "SofFinalize", DowntimeScope = "DowntimeReview";

    // POL-DEFAULT-01: required facts must be Known before finalization. Required
    // references with no owning lookup module (voyage, port call, port) can
    // never be Known; they block finalization unless the human finalizer
    // explicitly acknowledges each one, and the acknowledgement is recorded.
    public static string[] SofMissingRequired(IReadOnlyDictionary<string, ShipOpsFact> fields) => ShipOperationsPolicy.Schema("Port")
        .Where(f => f.Required && !f.ReadOnly && !f.ReferenceUnavailable && fields.GetValueOrDefault(f.Key)?.State != "Known").Select(f => f.Key).ToArray();
    public static string[] SofUnresolvedReferences(IReadOnlyDictionary<string, ShipOpsFact> fields) => ShipOperationsPolicy.Schema("Port")
        .Where(f => f.Required && f.ReferenceUnavailable && fields.GetValueOrDefault(f.Key)?.State != "Known").Select(f => f.Key).ToArray();
    public static bool SofAcknowledged(IReadOnlyDictionary<string, ShipOpsFact> fields, string[]? acknowledged) =>
        acknowledged != null && acknowledged.Length == acknowledged.Distinct(StringComparer.Ordinal).Count() &&
        acknowledged.Order(StringComparer.Ordinal).SequenceEqual(SofUnresolvedReferences(fields).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    // Downtime closure lifecycle (60.01 1686-1688): null = declared draft not yet
    // submitted. No implicit OPEN, VERIFIED or CLOSED default exists.
    public const string NotSubmitted = "NOT_SUBMITTED", Open = "OPEN", InReview = "IN_REVIEW", Verified = "VERIFIED", Closed = "CLOSED";
    public const string Submit = "SUBMIT_FOR_REVIEW", Return = "RETURN_FOR_CORRECTION", Verify = "VERIFY", Close = "CLOSE",
        LinkEvidence = "LINK_CLAIM_EVIDENCE", UnlinkEvidence = "UNLINK_CLAIM_EVIDENCE";
    public static readonly string[] DowntimeActions = [Submit, Return, Verify, Close, LinkEvidence, UnlinkEvidence];
    public static readonly string[] ReviewActions = [Return, Verify, Close];
    public const int MaxClaimEvidence = 20;
    public static string State(string? stored) => stored ?? NotSubmitted;
    public static bool Editable(string? stored) => stored is null or Open;
    public static string? Next(string? stored, string action) => (State(stored), action) switch {
        (NotSubmitted or Open, Submit) => InReview,
        (InReview, Return) => Open,
        (InReview, Verify) => Verified,
        (Verified, Close) => Closed,
        (NotSubmitted or Open, LinkEvidence or UnlinkEvidence) => stored,
        _ => "INVALID"
    };
    public static bool Valid(string? stored, string action) => DowntimeActions.Contains(action) && Next(stored, action) != "INVALID";
    public static string[] Allowed(string? stored) => DowntimeActions.Where(a => Valid(stored, a)).ToArray();
    // Submission and verification need every required fact, including an end:
    // an open interruption is never verified or closed against "now".
    public static string[] DowntimeMissingRequired(IReadOnlyDictionary<string, DowntimeFact> fields) => DowntimePolicy.Schema()
        .Where(f => !f.ReadOnly && (f.Required || f.Key == "end_at") && fields.GetValueOrDefault(f.Key)?.State != "Known").Select(f => f.Key).ToArray();
    // 60.01 1682 / T67: YES/NO/REVIEW_REQUIRED is a claim candidate only. The
    // computed candidate for any declared interruption period is REVIEW_REQUIRED
    // for a human; YES/NO, entitlement and amount are never derived here.
    public static string LossOfHireCandidate(IReadOnlyDictionary<string, DowntimeFact> fields) =>
        fields.GetValueOrDefault("start_at") is { State: "Known", Value: { } start } && DowntimePolicy.TryInstant(start, out _) ? "REVIEW_REQUIRED" : "UNKNOWN";
    public static string[] DowntimeBlockers(string[] draftBlockers, string? stored, int evidence, string candidate) {
        var state = State(stored); var result = draftBlockers.ToList();
        if (candidate == "REVIEW_REQUIRED") { result.Remove("LOSS_OF_HIRE_NOT_ASSESSED"); result.Add("LOSS_OF_HIRE_CANDIDATE_HUMAN_REVIEW_REQUIRED"); }
        if (evidence > 0) { result.Remove("CLAIM_EVIDENCE_ADOPTION_UNAVAILABLE"); result.Add("CLAIM_EVIDENCE_LINK_IS_NOT_VERIFICATION"); }
        if (state is Verified or Closed) {
            result.RemoveAll(b => b is "HUMAN_VERIFICATION_NOT_RECORDED" or "VERIFIED_CLOSURE_UNAVAILABLE" or "CLOSURE_DEFAULT_UNAVAILABLE");
            result.Add("HUMAN_VERIFICATION_IS_NOT_SAFETY_RELEASE");
        }
        if (state == InReview) result.Add("HUMAN_REVIEW_PENDING");
        return result.Distinct().ToArray();
    }
    public static string[] SofBlockers(string[] draftBlockers, string? sofState, long? finalRevision) {
        var result = draftBlockers.ToList();
        if (sofState == SofFinal) {
            result.RemoveAll(b => b is "SOF_FINALIZATION_UNAVAILABLE" or "HUMAN_REVIEW_NOT_RECORDED");
            result.AddRange(["SOF_FINAL_IS_INTERNAL_NOT_EXTERNAL_AGREEMENT", SofSignature]);
        } else if (finalRevision != null) result.Add("SOF_AMENDED_AFTER_FINAL_REVIEW_REQUIRED");
        return result.Distinct().ToArray();
    }
    private static readonly Regex Source = new(@"\A[\p{L}\p{N} .,:/()#_-]{1,200}\z", RegexOptions.CultureInvariant);
    public static bool AppointmentSource(string? value) => value != null && Source.IsMatch(value) && !string.IsNullOrWhiteSpace(value);
}
