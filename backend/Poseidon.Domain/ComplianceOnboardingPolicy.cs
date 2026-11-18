namespace Poseidon.Domain;

public sealed record OnboardingField(string Key, string State, bool Critical, bool Complete);
public sealed record OnboardingAssessment(string State, OnboardingField[] Fields, string[] Blockers);

// 18.01/Backlog/row:40. Completeness is a declaration check, never verification,
// source admission, a safety decision, or permission to submit an incomplete draft.
public static class ComplianceOnboardingPolicy
{
    public static OnboardingAssessment Assess(string? code, string? name, RegulatoryDraftContent? profile)
    {
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
        return new(fields.All(f => f.Complete) ? "DECLARED_COMPLETE_UNVERIFIED" : "REVIEW_REQUIRED", fields.ToArray(), blockers.ToArray());
    }
}
