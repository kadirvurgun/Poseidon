using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace Poseidon.Api;

// DEV-011: actual in-process HTTP observations, not business readiness or durable audit.
// Cardinality is closed. Identifiers, raw paths, query/body/header values and exception
// messages are deliberately absent; existing attributed audit retains correlation context.
public sealed class ApiRequestTelemetry(RequestDelegate next)
{
    public const string MeterName = "Poseidon.Api.Requests";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("poseidon.api.requests", "{request}");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("poseidon.api.errors", "{request}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("poseidon.api.duration", "s");

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var failure = "none";
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) {
            failure = "cancelled"; throw;
        }
        catch { failure = "unhandled"; throw; }
        finally {
            // Once headers are sent the observed HTTP status cannot be rewritten.
            // Retain it and expose the late transport failure separately.
            var code = context.Response.HasStarted ? context.Response.StatusCode :
                failure == "cancelled" ? 499 : failure == "unhandled" ? 500 : context.Response.StatusCode;
            var status = code is >= 100 and <= 599 ? $"{code / 100}xx" : "other";
            var method = context.Request.Method switch {
                "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" => context.Request.Method,
                _ => "OTHER"
            };
            // Only the registered route TEMPLATE selects a closed area, never Request.Path.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            var tags = new TagList { { "http.request.method", method }, { "http.response.status_class", status },
                { "poseidon.area", Area(route) }, { "poseidon.request.failure", failure } };
            try {
                Requests.Add(1, tags);
                if (failure != "none" || code is >= 400 and <= 599) Errors.Add(1, tags);
                Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
            }
            catch {
                // Optional diagnostic listener failure must not alter an already committed
                // command/response. Mandatory security/business audit is NOT caught here.
            }
        }
    }

    private static string Area(string? route)
    {
        if (route is null) return "unmatched";
        if (route == "/health/live" || route == "/api/v1/system/status") return "system";
        if (!route.StartsWith("/api/v1/", StringComparison.Ordinal)) return "other";
        if (route.Contains("/auth/", StringComparison.Ordinal)) return "identity";
        if (route.Contains("/crew", StringComparison.Ordinal) || route.Contains("/certificates", StringComparison.Ordinal)) return "crew";
        if (route.Contains("/tug-", StringComparison.Ordinal)) return "tug";
        if (route.Contains("/equipment", StringComparison.Ordinal) || route.Contains("/maintenance", StringComparison.Ordinal)) return "technical";
        if (route.Contains("/source-drafts", StringComparison.Ordinal) || route.Contains("/requirement-drafts", StringComparison.Ordinal) || route.Contains("/regulatory", StringComparison.Ordinal)) return "regulatory";
        if (route.Contains("/offline", StringComparison.Ordinal)) return "offline";
        if (route.Contains("/imports", StringComparison.Ordinal)) return "integration";
        if (route.Contains("/files", StringComparison.Ordinal) || route.Contains("/documents", StringComparison.Ordinal)) return "files";
        if (route.Contains("/audit", StringComparison.Ordinal)) return "audit";
        if (route.Contains("/vessels", StringComparison.Ordinal) || route.Contains("/companies", StringComparison.Ordinal) || route.Contains("/workspace", StringComparison.Ordinal)) return "directory";
        return "other";
    }
}
