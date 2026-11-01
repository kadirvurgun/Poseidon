using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// The process-wide diagnostic listener deliberately fails in one case. Isolate its
// lifetime from other test collections; do not let a test listener corrupt their samples.
[CollectionDefinition("ApiRequestTelemetryIsolated", DisableParallelization = true)]
public sealed class ApiRequestTelemetryCollection { }

[Collection("ApiRequestTelemetryIsolated")]
public sealed class ApiRequestTelemetryTests
{
    internal sealed record Sample(string Name, double Value, KeyValuePair<string, object?>[] Tags);
    internal static MeterListener Listen(ConcurrentQueue<Sample> samples)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) => {
            if (instrument.Meter.Name == ApiRequestTelemetry.MeterName) owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => {
            samples.Enqueue(new(instrument.Name, value, tags.ToArray()));
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => {
            samples.Enqueue(new(instrument.Name, value, tags.ToArray()));
        });
        listener.Start(); return listener;
    }
    private static string? Tag(Sample sample, string key) => sample.Tags.Single(t => t.Key == key).Value?.ToString();

    [Fact]
    public async Task Actual_route_template_and_http_outcome_are_measured_without_private_request_context()
    {
        const string secret = "FICTIONAL-PRIVATE-REQUEST-MARKER";
        var samples = new ConcurrentQueue<Sample>(); using var listener = Listen(samples);
        var context = new DefaultHttpContext();
        context.Request.Method = "PATCH"; context.Request.Path = "/api/v1/vessels/" + secret + "/crew/" + secret;
        context.Request.QueryString = new("?name=" + secret); context.Request.Headers["Authorization"] = secret;
        context.TraceIdentifier = secret;
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/v1/vessels/{vessel}/crew/{id}"), 0, EndpointMetadataCollection.Empty, secret));
        await new ApiRequestTelemetry(c => { c.Response.StatusCode = 403; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Contains(samples, s => s.Name == "poseidon.api.requests" && s.Value == 1 && Tag(s, "poseidon.area") == "crew");
        Assert.Contains(samples, s => s.Name == "poseidon.api.errors" && Tag(s, "http.response.status_class") == "4xx");
        Assert.Contains(samples, s => s.Name == "poseidon.api.duration" && s.Value >= 0);
        Assert.All(samples, s => {
            Assert.Equal(new[] { "http.request.method", "http.response.status_class", "poseidon.area", "poseidon.request.failure" }, s.Tags.Select(t => t.Key).ToArray());
            Assert.DoesNotContain(s.Tags, t => t.Value?.ToString()?.Contains(secret, StringComparison.Ordinal) == true);
        });
    }

    [Fact]
    public async Task An_unmatched_private_path_and_arbitrary_method_do_not_become_metric_dimensions()
    {
        var samples = new ConcurrentQueue<Sample>(); using var listener = Listen(samples);
        var context = new DefaultHttpContext(); context.Request.Path = "/private-person-name"; context.Request.Method = "PRIVATE-METHOD";
        await new ApiRequestTelemetry(c => { c.Response.StatusCode = 404; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Contains(samples, s => Tag(s, "poseidon.area") == "unmatched" && Tag(s, "http.request.method") == "OTHER");
        Assert.DoesNotContain(samples.SelectMany(s => s.Tags), t => t.Value?.ToString()?.Contains("PRIVATE", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task Unhandled_failure_is_counted_without_swallowing_or_emitting_the_original_exception()
    {
        var samples = new ConcurrentQueue<Sample>(); using var listener = Listen(samples);
        var original = new InvalidOperationException("FICTIONAL private exception detail");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ApiRequestTelemetry(_ => Task.FromException(original)).InvokeAsync(new DefaultHttpContext()));
        Assert.Same(original, actual);
        Assert.Contains(samples, s => s.Name == "poseidon.api.errors" && Tag(s, "poseidon.request.failure") == "unhandled" && Tag(s, "http.response.status_class") == "5xx");
        Assert.DoesNotContain(samples.SelectMany(s => s.Tags), t => t.Value?.ToString() == original.Message);
    }

    [Fact]
    public async Task A_cancelled_request_is_distinct_and_is_not_reported_as_a_success_or_server_fault()
    {
        var samples = new ConcurrentQueue<Sample>(); using var listener = Listen(samples);
        using var source = new CancellationTokenSource(); source.Cancel();
        var context = new DefaultHttpContext { RequestAborted = source.Token };
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ApiRequestTelemetry(_ => Task.FromException(new OperationCanceledException(source.Token))).InvokeAsync(context));
        Assert.Contains(samples, s => s.Name == "poseidon.api.requests" && Tag(s, "poseidon.request.failure") == "cancelled" && Tag(s, "http.response.status_class") == "4xx");
    }

    [Fact]
    public async Task Optional_metric_listener_failure_cannot_change_a_completed_http_response()
    {
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, owner) => {
            if (instrument.Meter.Name == ApiRequestTelemetry.MeterName) owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw new InvalidOperationException("synthetic diagnostic sink failure"));
        listener.Start(); var context = new DefaultHttpContext();
        await new ApiRequestTelemetry(c => { c.Response.StatusCode = 204; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.Equal(204, context.Response.StatusCode);
    }
}

public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Real_api_host_records_latency_and_a_fresh_correlation_without_request_values_in_metrics()
    {
        var samples = new ConcurrentQueue<ApiRequestTelemetryTests.Sample>(); using var listener = ApiRequestTelemetryTests.Listen(samples);
        using var host = Host(); using var client = Client(host, authenticated: false);
        const string input = "FICTIONAL-PRIVATE-CORRELATION";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/status?private=" + input);
        request.Headers.Add("X-Correlation-ID", input);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(input, Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Contains(samples, s => s.Name == "poseidon.api.duration" && s.Value >= 0 && s.Tags.Any(t => t.Key == "poseidon.area" && t.Value?.ToString() == "system"));
        Assert.DoesNotContain(samples.SelectMany(s => s.Tags), t => t.Value?.ToString()?.Contains(input, StringComparison.Ordinal) == true);
    }
}
