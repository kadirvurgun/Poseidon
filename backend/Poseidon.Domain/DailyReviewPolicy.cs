namespace Poseidon.Domain;

// Source-declared workflow labels only. Recording a human review is not
// verification, controlled finalization, submission or a safety decision.
public static class DailyReviewPolicy
{
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
