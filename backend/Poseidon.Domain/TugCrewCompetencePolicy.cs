namespace Poseidon.Domain;

// One criterion observed by the shared crew-eligibility engine (WP-A) or the minimum-manning reader.
// No person name, medical fact or certificate number is carried; only closed codes.
public sealed record TugCrewCriterion(string Criterion, string Result, string Reason, bool Verified, Guid? PositionId = null);

// DEV-086 / T-TUG-007 (18.01 Backlog row 87: "Shared crew engine + tug-specific towage competence; positive
// readiness blocked by missing/unverified credential/competence"). 10.03 Eligibility Engine row 8: towage
// competence "Not verified -> ATTENTION/NOT_ELIGIBLE by criticality". All five criteria must be observed and
// verified before PASS. A missing evaluator or criterion stays UNKNOWN (REVIEW_REQUIRED), never PASS.
public static class TugCrewCompetencePolicy
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:87;10.03/sheet:Eligibility Engine/row:8";
    public const string MinimumManning = "MinimumManning", Credential = "Credential", TowageCompetence = "TowageCompetence",
        Familiarization = "Familiarization", WorkRest = "WorkRest";
    public static readonly string[] Criteria = [MinimumManning, Credential, TowageCompetence, Familiarization, WorkRest];

    public static TugReadinessCheck[] Assess(IEnumerable<TugCrewCriterion> observed)
    {
        var rows = observed.Where(x => x != null && Criteria.Contains(x.Criterion, StringComparer.Ordinal) &&
            TugReadinessEngine.Results.Contains(x.Result, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(x.Reason)).ToArray();
        var result = new List<TugReadinessCheck>();
        foreach (var criterion in Criteria) {
            var own = rows.Where(r => r.Criterion == criterion).ToArray();
            if (own.Length == 0) { result.Add(new(Category(criterion), TugReadinessEngine.Unknown, criterion + ":NotObserved")); continue; }
            foreach (var r in own) {
                // An unverified PASS/CONDITIONAL is not evidence; it can only lower the state to UNKNOWN.
                var value = r.Result is TugReadinessEngine.Pass or TugReadinessEngine.Condition && !r.Verified ? TugReadinessEngine.Unknown : r.Result;
                var reason = value != r.Result ? "Unverified" : r.Reason;
                result.Add(new(Category(criterion), value, criterion + ":" + reason, true, r.PositionId));
            }
        }
        return result.ToArray();
    }

    // Minimum manning belongs to the shared crew coverage; the tug-specific criteria are crew_competence.
    private static string Category(string criterion) => criterion == MinimumManning ? "crew" : "crew_competence";
}
