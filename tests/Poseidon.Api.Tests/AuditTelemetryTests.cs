using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Audit_health_metrics_are_real_writer_observations_without_identity_labels()
    {
        var measurements = new ConcurrentBag<(string Name, int Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) => {
            if (instrument.Meter.Name == "Poseidon.Audit") meter.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.Length)));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.Length)));
        listener.Start();
        await using var host = Host(auditRead: true); using var client = Client(host);
        Assert.True((await client.GetAsync("/api/v1/audit", Ct)).IsSuccessStatusCode);
        Assert.Contains(measurements, x => x.Name == "poseidon.audit.security.persisted");
        Assert.Contains(measurements, x => x.Name == "poseidon.audit.security.duration");
        await Sql("CREATE FUNCTION audit.test_metric_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic writer failure'; END $$; CREATE TRIGGER test_metric_failure BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH ROW EXECUTE FUNCTION audit.test_metric_failure();");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/audit", Ct)).StatusCode);
        Assert.Contains(measurements, x => x.Name == "poseidon.audit.security.unavailable");
        Assert.All(measurements, x => Assert.Equal(0, x.Tags));
    }
}
