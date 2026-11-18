namespace Poseidon.Domain;

public sealed record OnboardingField(string Key, string State, bool Critical, bool Complete);
public sealed record OnboardingBlocker(Guid SubjectId, string FieldPath, string ReasonCode, string ResolutionType);
public sealed record OnboardingAssessment(string State, OnboardingField[] Fields, string[] Blockers,
    OnboardingBlocker[] BlockerDetails);

// 18.01/Backlog/row:40 and 59.01/paragraph:55. Completeness is a declaration
// check, never verification, source admission, a safety decision, or permission
// to submit an incomplete draft.
public static class ComplianceOnboardingPolicy
{
    public static OnboardingAssessment Assess(Guid subjectId, string? code, string? name, RegulatoryDraftContent? profile)
    {
        if (subjectId == Guid.Empty) throw new ArgumentException("Onboarding subject is required.", nameof(subjectId));
        var fields = new List<OnboardingField>();
        var errors = profile == null ? new Dictionary<string, string[]>() : RegulatoryDraftPolicy.Normalize(profile).Errors;
        void Field(string key, string? state, string? value) {
            var validState = state is "Known" or "Unknown" or "Missing" ? state : "Unknown";
            if (key.StartsWith("profile.", StringComparison.Ordinal) && errors.ContainsKey(key[8..])) validState = "Unknown";
            var complete = validState == "Known" && !string.IsNullOrWhiteSpace(value);
            if (validState == "Known" && !complete) validState = "Missing";
            fields.Add(new(key, validState, true, complete));
        }
        Field("vessel.code", "Known", code); Field("vessel.name", "Known", name);
        foreach (var (key, input) in new (string, TextInput?)[] {
            ("profile.imo", profile?.Imo), ("profile.flag", profile?.Flag),
            ("profile.classSociety", profile?.ClassSociety), ("profile.shipType", profile?.ShipType),
            ("profile.buildDate", profile?.BuildDate), ("profile.tradingArea", profile?.TradingArea),
            ("profile.cargoContext", profile?.CargoContext) }) Field(key, input?.State ?? "Missing", input?.Value);
        Field("profile.gt", profile?.Gt.State ?? "Missing", profile?.Gt.Value);
        var blockers = new List<string>();
        if (profile == null) blockers.Add("PROFILE_MISSING");
        if (fields.Any(f => f.State == "Missing")) blockers.Add("REQUIRED_DATA_MISSING");
        if (fields.Any(f => f.State == "Unknown")) blockers.Add("REQUIRED_DATA_UNKNOWN");
        blockers.Add("VERIFICATION_NOT_ESTABLISHED");
        var details = new List<OnboardingBlocker>();
        if (profile == null) details.Add(new(subjectId, "profile", "PROFILE_MISSING", "PROFILE_DECLARATION"));
        details.AddRange(fields.Where(field => !field.Complete).Select(field => new OnboardingBlocker(subjectId,
            field.Key, field.State == "Unknown" ? "REQUIRED_DATA_UNKNOWN" : "REQUIRED_DATA_MISSING",
            field.State == "Unknown" ? "POLICY_OR_EVIDENCE" : "REQUIRED_DECLARATION")));
        details.Add(new(subjectId, "profile", "VERIFICATION_NOT_ESTABLISHED", "QUALIFIED_EVIDENCE"));
        return new(fields.All(f => f.Complete) ? "DECLARED_COMPLETE_UNVERIFIED" : "REVIEW_REQUIRED",
            fields.ToArray(), blockers.ToArray(), details.ToArray());
    }
}
