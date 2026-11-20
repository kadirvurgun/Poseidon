namespace Poseidon.Domain;

// S10 WP-A DEV-065 / T-S10-002 (18.01 Backlog row:66; data-trust-center 13.02 paragraph:123-124, 15.01 paragraph:433,
// 56.01 paragraph:1422). Evidence/data quality signals: MISSING, STALE, CONFLICTING, UNVERIFIED, ORPHAN. Each signal
// carries its source, provenance and freshness so the DPA can see WHY. A signal is never repaired, guessed or hidden:
// unknown freshness is reported as unknown, never as current. Any material signal floors readiness at REVIEW_REQUIRED;
// the policy itself never produces a READY/GREEN state ("false green blocked").
public sealed record DataQualitySignal(string Kind, string Reason, bool Material);

// Inputs are server-observed facts only (never request data). Freshness is the trusted SourceFreshnessPolicy result.
public sealed record DataQualityInput(
    string SubjectKind,
    SourceFreshness Freshness,
    string? DeclaredTimeState,
    string[] MissingFields,
    string[] OrphanReasons,
    string VerificationState,
    string[] ConflictingFields,
    bool Superseded);

public sealed record DataQualityAssessment(string Disposition, DataQualitySignal[] Signals)
{
    public bool HasMaterialSignal => Signals.Any(s => s.Material);
}

public static class DataQualityPolicy
{
    public const string SourceVersion = "18.01/Backlog/row:66;13.02/paragraph:123-124;15.01/paragraph:433;56.01/paragraph:1422";
    public const string Missing = "MISSING", Stale = "STALE", Conflicting = "CONFLICTING", Unverified = "UNVERIFIED", Orphan = "ORPHAN";
    public static readonly string[] Kinds = [Missing, Stale, Conflicting, Unverified, Orphan];
    // REVIEW_REQUIRED when any material signal exists; NO_MATERIAL_SIGNAL otherwise. Neither is a readiness or approval
    // statement and there is deliberately no READY value here.
    public const string ReviewRequired = "REVIEW_REQUIRED", NoMaterialSignal = "NO_MATERIAL_SIGNAL";
    public static readonly string[] Dispositions = [ReviewRequired, NoMaterialSignal];
    public static readonly string[] VerificationStates = ["VERIFIED", "UNVERIFIED", "VERIFICATION_EXPIRED", "VERIFICATION_REJECTED", "UNKNOWN"];
    public static readonly string[] SubjectKinds = ["Evidence", "Control", "ProfileField"];
    public const int MaxPage = 100;

    public static DataQualityAssessment Assess(DataQualityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!SubjectKinds.Contains(input.SubjectKind, StringComparer.Ordinal)) throw new ArgumentException("invalid_subject_kind");
        if (!VerificationStates.Contains(input.VerificationState, StringComparer.Ordinal)) throw new ArgumentException("invalid_verification_state");
        var signals = new List<DataQualitySignal>();
        foreach (var field in (input.MissingFields ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            signals.Add(new(Missing, field, true));
        // Freshness: only a CURRENT observation within a declared window is free of a stale signal. Unknown, unavailable
        // (no qualified policy) and future-dated observations are material: they can never support a green state.
        var freshness = input.Freshness switch
        {
            SourceFreshness.Current => null,
            SourceFreshness.Stale => "OBSERVATION_STALE",
            SourceFreshness.Future => "OBSERVATION_IN_FUTURE",
            SourceFreshness.Unknown => "OBSERVATION_TIME_UNKNOWN",
            _ => "FRESHNESS_POLICY_UNAVAILABLE"
        };
        if (input.SubjectKind != "Control" && freshness != null) signals.Add(new(Stale, freshness, true));
        if (input.DeclaredTimeState is "DECLARED_EXPIRED") signals.Add(new(Stale, "DECLARED_VALIDITY_EXPIRED", true));
        if (input.DeclaredTimeState is "DECLARED_NOT_YET_VALID") signals.Add(new(Stale, "DECLARED_NOT_YET_VALID", true));
        if (input.Superseded) signals.Add(new(Stale, "REVISION_SUPERSEDED", true));
        foreach (var field in (input.ConflictingFields ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            signals.Add(new(Conflicting, field, true));
        if (input.SubjectKind != "Control" && input.VerificationState != "VERIFIED")
            signals.Add(new(Unverified, input.VerificationState == "UNVERIFIED" ? "VERIFICATION_NOT_PERFORMED" : input.VerificationState, true));
        foreach (var reason in (input.OrphanReasons ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            signals.Add(new(Orphan, reason, true));
        var distinct = signals.DistinctBy(s => (s.Kind, s.Reason)).ToArray();
        return new(distinct.Any(s => s.Material) ? ReviewRequired : NoMaterialSignal, distinct);
    }

    // Readiness adapter for consumers: a material signal never lets a consumer state stay READY. Unknown consumer
    // states are passed through unchanged only when they are already non-ready.
    public static string Floor(string readiness, IEnumerable<DataQualitySignal> signals)
    {
        if (!signals.Any(s => s.Material)) return readiness;
        return readiness is "READY" or "PASS" or "GREEN" or "CURRENT" or "NO_MATERIAL_SIGNAL" ? ReviewRequired : readiness;
    }

    // Conflict = the same field declared by different sources with different values. A single source or identical
    // values are not a conflict; an unknown value next to a known one is a conflict (never silently resolved).
    public static string[] Conflicts(IEnumerable<(string Field, string Source, string? Value)> declarations) =>
        declarations.GroupBy(d => d.Field, StringComparer.Ordinal)
            .Where(g => g.Select(d => d.Source).Distinct(StringComparer.Ordinal).Count() > 1 && g.Select(d => d.Value ?? "\u0000UNKNOWN").Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key).Order(StringComparer.Ordinal).ToArray();

    // Freshness from a declared observation/fresh-until pair. Without a declared window there is no qualified policy,
    // so the result is Unavailable (never Current).
    public static SourceFreshness Freshness(DateTimeOffset? observedAt, DateTimeOffset? freshUntil, DateTimeOffset now) =>
        observedAt == null ? SourceFreshness.Unknown :
        freshUntil == null || freshUntil <= observedAt ? SourceFreshness.Unavailable :
        SourceFreshnessPolicy.Assess(observedAt, now, freshUntil.Value - observedAt.Value);
}
