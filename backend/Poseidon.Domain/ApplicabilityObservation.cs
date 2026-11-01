namespace Poseidon.Domain;

// 09.01 paragraphs42–58,198–207,234–236; 09.02 Input Catalog row24.
// Trusted engine primitive only. No catalog activation, profile-name dispatch,
// source verification, readiness aggregation or runtime authorization is provided.
public enum PredicateKnowledge { Missing, Unknown, Stale, Unverified, Conflicting, KnownFalse, KnownTrue }
public enum SourceCurrency { Unknown, Current, Future, Superseded }
public enum ApplicabilityObservation { ReviewRequired, NotCurrent, NotApplicable, Applicable }
public static class ApplicabilityObservationPolicy
{
    public static ApplicabilityObservation Assess(PredicateKnowledge knowledge, SourceCurrency currency)
    {
        if (!Enum.IsDefined(knowledge) || !Enum.IsDefined(currency))
            throw new ArgumentOutOfRangeException(nameof(knowledge));
        if (currency is SourceCurrency.Future or SourceCurrency.Superseded)
            return ApplicabilityObservation.NotCurrent;
        if (currency != SourceCurrency.Current)
            return ApplicabilityObservation.ReviewRequired;
        return knowledge switch {
            PredicateKnowledge.KnownTrue => ApplicabilityObservation.Applicable,
            PredicateKnowledge.KnownFalse => ApplicabilityObservation.NotApplicable,
            _ => ApplicabilityObservation.ReviewRequired
        };
    }
}
