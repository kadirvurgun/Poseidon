namespace Poseidon.Domain;

// Observation freshness, never truth/verification/readiness. Exact boundary is
// current through maxAge; callers supply their trusted source policy and clock.
public enum SourceFreshness { Unavailable, Unknown, Future, Stale, Current }
public static class SourceFreshnessPolicy
{
    public static SourceFreshness Assess(DateTimeOffset? observedAt, DateTimeOffset now, TimeSpan? maxAge) =>
        maxAge is null || maxAge <= TimeSpan.Zero ? SourceFreshness.Unavailable :
        observedAt is null ? SourceFreshness.Unknown : observedAt > now ? SourceFreshness.Future :
        now - observedAt > maxAge ? SourceFreshness.Stale : SourceFreshness.Current;
}
