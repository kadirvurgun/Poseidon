using Poseidon.Domain;

namespace Poseidon.Api;

// Tenant attribution is internal and is never a metric label. This process cache
// excludes unattributed/anonymous observations and is not a durable/global total.
internal static class OperationalHealthObservations
{
    internal static readonly object TenantItem = new();
    private sealed class Bucket
    {
        internal readonly object Gate = new();
        internal long Requests, Errors, AuditSuccess, AuditFailure;
        internal double RequestMs, AuditMs;
        internal readonly long[] Freshness = new long[5];
    }
    private static readonly object Gate = new();
    private static readonly Dictionary<Guid, Bucket> Tenants = [];
    private static Bucket? Find(Guid? tenant, bool create)
    {
        if (tenant is null || tenant == Guid.Empty) return null;
        lock (Gate) {
            if (Tenants.TryGetValue(tenant.Value, out var existing)) return existing;
            // Bounded engineering storage; exhaustion is Unavailable, not zero.
            if (!create || Tenants.Count >= 4096) return null;
            var added = new Bucket(); Tenants.Add(tenant.Value, added); return added;
        }
    }
    internal static void Request(Guid? tenant, bool error, double milliseconds)
    {
        var bucket = Find(tenant, true); if (bucket == null || !double.IsFinite(milliseconds) || milliseconds < 0) return;
        lock (bucket.Gate) { bucket.Requests++; if (error) bucket.Errors++; bucket.RequestMs += milliseconds; }
    }
    internal static void Audit(Guid? tenant, bool success, double milliseconds)
    {
        var bucket = Find(tenant, true); if (bucket == null || !double.IsFinite(milliseconds) || milliseconds < 0) return;
        lock (bucket.Gate) { if (success) bucket.AuditSuccess++; else bucket.AuditFailure++; bucket.AuditMs += milliseconds; }
    }
    internal static void Freshness(Guid? tenant, SourceFreshness state)
    {
        var bucket = Find(tenant, true); if (bucket == null) return;
        var index = state switch { SourceFreshness.Current => 0, SourceFreshness.Stale => 1, SourceFreshness.Future => 2, SourceFreshness.Unknown => 3, _ => 4 };
        lock (bucket.Gate) bucket.Freshness[index]++;
    }
    internal static (HealthApiDto Api, HealthAuditDto Audit, HealthFreshnessDto Freshness) Read(Guid tenant)
    {
        const string scope = "TenantProcessSinceStart";
        var api = new HealthApiDto("Unavailable", scope, null, null, null);
        var audit = new HealthAuditDto("Unavailable", scope, null, null, null);
        var freshness = new HealthFreshnessDto("Unavailable", scope, null, null, null, null, null);
        var bucket = Find(tenant, false); if (bucket == null) return (api, audit, freshness);
        lock (bucket.Gate) {
            if (bucket.Requests > 0) api = new("Observed", scope, bucket.Requests, bucket.Errors, bucket.RequestMs / bucket.Requests);
            var audits = bucket.AuditSuccess + bucket.AuditFailure;
            if (audits > 0) audit = new("Observed", scope, bucket.AuditSuccess, bucket.AuditFailure, bucket.AuditMs / audits);
            if (bucket.Freshness.Any(n => n > 0)) freshness = new("Observed", scope,
                bucket.Freshness[0], bucket.Freshness[1], bucket.Freshness[2], bucket.Freshness[3], bucket.Freshness[4]);
            return (api, audit, freshness);
        }
    }
}
