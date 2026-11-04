using System.Diagnostics;
using System.Diagnostics.Metrics;
using Poseidon.Domain;

namespace Poseidon.Api;

public enum ObservationArea { Technical, Tug }
public enum ReplayObservation { Committed, MaterialConflict, Recovered }
// No arbitrary label input. Counter values describe events in this process,
// not global device backlog or durable lifetime totals. Optional listener failure
// cannot rewrite business/audit outcomes or an already committed response.
public static class OperationalTelemetry
{
    public const string MeterName = "Poseidon.Operations";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Freshness = Meter.CreateCounter<long>("poseidon.source.freshness.observations", "{observation}");
    private static readonly Counter<long> Replay = Meter.CreateCounter<long>("poseidon.sync.replay.outcomes", "{observation}");
    public static void ObserveFreshness(ObservationArea area, SourceFreshness state, Guid? tenant = null)
    {
        OperationalHealthObservations.Freshness(tenant, state);
        var source = area switch { ObservationArea.Technical => "technical", ObservationArea.Tug => "tug", _ => "other" };
        var status = state switch { SourceFreshness.Current => "current", SourceFreshness.Stale => "stale", SourceFreshness.Future => "future", SourceFreshness.Unknown => "unknown", _ => "unavailable" };
        try { Freshness.Add(1, new TagList { { "poseidon.area", source }, { "poseidon.freshness", status } }); } catch { }
    }
    public static void ObserveReplay(ReplayObservation observation)
    {
        var result = observation switch { ReplayObservation.Committed => "committed", ReplayObservation.MaterialConflict => "material_conflict", ReplayObservation.Recovered => "recovered", _ => "other" };
        try { Replay.Add(1, new KeyValuePair<string, object?>("poseidon.sync.outcome", result)); } catch { }
    }
}
