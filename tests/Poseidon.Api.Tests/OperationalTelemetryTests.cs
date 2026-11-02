using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public class OperationalTelemetryTests
{
    [Fact]
    public void Unavailable_current_conflict_and_recovery_are_distinct_bounded_observations()
    {
        var samples = new ConcurrentQueue<(string Name, long Value, string[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (i, l) => { if (i.Meter.Name == OperationalTelemetry.MeterName) l.EnableMeasurementEvents(i); };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            samples.Enqueue((instrument.Name, value, tags.ToArray().Select(t => t.Key + "=" + t.Value).ToArray()));
        });
        listener.Start();
        OperationalTelemetry.ObserveFreshness(ObservationArea.Technical, SourceFreshness.Unavailable);
        OperationalTelemetry.ObserveFreshness(ObservationArea.Technical, SourceFreshness.Current);
        OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, SourceFreshness.Unknown);
        OperationalTelemetry.ObserveReplay(ReplayObservation.MaterialConflict);
        OperationalTelemetry.ObserveReplay(ReplayObservation.Recovered);
        Assert.Contains(samples, s => s.Tags.Contains("poseidon.freshness=unavailable"));
        Assert.Contains(samples, s => s.Tags.Contains("poseidon.freshness=current"));
        Assert.Contains(samples, s => s.Tags.Contains("poseidon.sync.outcome=material_conflict"));
        Assert.Contains(samples, s => s.Tags.Contains("poseidon.sync.outcome=recovered"));
        Assert.All(samples, s => Assert.Equal(1, s.Value));
        Assert.All(samples, s => Assert.All(s.Tags, t => Assert.DoesNotContain("Id", t)));
    }

    [Fact]
    public void Invalid_enum_input_cannot_create_unbounded_dimensions_or_success_states()
    {
        var samples = new ConcurrentQueue<string>(); using var listener = new MeterListener();
        listener.InstrumentPublished = (i, l) => { if (i.Meter.Name == OperationalTelemetry.MeterName) l.EnableMeasurementEvents(i); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => { foreach (var t in tags) samples.Enqueue(t.Key + "=" + t.Value); });
        listener.Start();
        OperationalTelemetry.ObserveFreshness((ObservationArea)int.MaxValue, (SourceFreshness)int.MaxValue);
        OperationalTelemetry.ObserveReplay((ReplayObservation)int.MaxValue);
        Assert.Contains("poseidon.area=other", samples); Assert.Contains("poseidon.freshness=unavailable", samples);
        Assert.Contains("poseidon.sync.outcome=other", samples);
        Assert.DoesNotContain(samples, x => x.Contains(int.MaxValue.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Optional_listener_failure_cannot_change_committed_outcomes()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (i, l) => { if (i.Meter.Name == OperationalTelemetry.MeterName) l.EnableMeasurementEvents(i); };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("synthetic-listener-failure"));
        listener.Start();
        OperationalTelemetry.ObserveReplay(ReplayObservation.Committed);
        OperationalTelemetry.ObserveFreshness(ObservationArea.Tug, SourceFreshness.Stale);
    }
}
