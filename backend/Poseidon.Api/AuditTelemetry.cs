using System.Diagnostics.Metrics;

namespace Poseidon.Api;

// DEV-011: low cardinality, no actor/tenant/vessel/resource/correlation metric labels.
internal static class AuditTelemetry
{
    private static readonly Meter Meter = new("Poseidon.Audit", "1.0.0");
    private static readonly Counter<long> Persisted = Meter.CreateCounter<long>("poseidon.audit.security.persisted", "events");
    private static readonly Counter<long> Unavailable = Meter.CreateCounter<long>("poseidon.audit.security.unavailable", "attempts");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("poseidon.audit.security.duration", "ms");
    internal static void Record(bool success, TimeSpan elapsed)
    {
        if (success) Persisted.Add(1); else Unavailable.Add(1);
        Latency.Record(elapsed.TotalMilliseconds);
    }
}
