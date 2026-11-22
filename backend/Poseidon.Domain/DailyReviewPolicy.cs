namespace Poseidon.Domain;

// Source-declared workflow labels only. Recording a human review is not
// verification, controlled finalization, submission or a safety decision.
public static class DailyReviewPolicy
{
    // Independent review (13.03 paragraph:35-37 and 86-89: crew/OOW keep the watch logs, Master / Chief Officer / Chief
    // Engineer review them; 60.01 Rev3 paragraph:544-546 "Kendi kaydini verify" is forbidden; ISM Code 10.2/12): whoever
    // authored any revision up to and including the reviewed revision never records its review. Mirrors the tug daily
    // log precedent (QA S9 N5, CK_TugDailyLogReview_Kind) and the database trigger DailyReviewEvent_ReviewerNotAuthor.
    public const string ReviewerIsAuthor="REVIEWER_IS_RECORD_AUTHOR";
    public static bool IndependentReviewer(Guid reviewer,IEnumerable<Guid> authors) => reviewer!=Guid.Empty && !authors.Contains(reviewer);
    public static bool Duty(string kind,string duty) => kind switch {
        "Noon" => duty=="Master", "Deck" => duty is "Master" or "ChiefOfficer", "Engine" => duty=="ChiefEngineer", _ => false
    };
    public static string[] Decisions(string kind) => kind switch {
        "Noon" => ["REVIEWED"], "Deck" or "Engine" => ["REVIEWED","RETURNED"], _ => []
    };
    public static bool Valid(string kind,string decision,string reason,string? rationale) => Decisions(kind).Contains(decision) &&
        (decision=="REVIEWED" && reason=="REVIEW_RECORDED" || decision=="RETURNED" && reason=="RETURNED_FOR_CORRECTION") &&
        rationale is {Length:>0 and <=2000} && !string.IsNullOrWhiteSpace(rationale) && !rationale.Any(c=>char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
}
