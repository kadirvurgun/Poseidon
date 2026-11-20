using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-067 / T-S10-004: pilot workload performance baseline (18.01 Backlog row DEV-067,
// "Today/readiness/audit queries bounded; agreed p95 envelope recorded; slow query traces actionable").
// Category=Performance is excluded from every api_test_gate.py scope (unit/integration/all) so it never
// changes the default CI counts or duration. Run only through scripts/perf_baseline.py, which computes
// p50/p95 from the raw samples written here. This test MEASURES; it asserts no SLA and no p95 limit:
// the agreed envelope is a product owner/QA decision that is not recorded by an AI.
// Fixture is synthetic and FICTIONAL: drafts only, no approval/verification/closure records.
public sealed partial class DirectoryWorkspaceTests
{
    internal const double PerfSlowQueryThresholdMs = 500;

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Requirement", "DEV-067")]
    public async Task Pilot_workload_daily_surfaces_are_measured_with_slow_query_trace()
    {
        var vessels = PerfSetting("POSEIDON_PERF_VESSELS", 10, 1, 50);
        var workOrders = PerfSetting("POSEIDON_PERF_WORK_ORDERS", 1000, 1, 5000);
        var iterations = PerfSetting("POSEIDON_PERF_ITERATIONS", 30, 5, 500);
        var warmup = PerfSetting("POSEIDON_PERF_WARMUP", 3, 0, 50);
        var trace = new PerfCommandTrace();
        await using var baseline = Host(search: true, regulatoryRead: true, regulatoryWrite: true, auditRead: true, crewRead: true,
            technicalRead: true, technicalWrite: true, tugRead: true, tugMode: true, certificateRead: true, applicabilityRead: true,
            interceptor: trace);
        // Same synthetic all-permission membership as the endpoint profile probe; no production grant.
        await using var host = baseline.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            ProfileProbePermissions.Select((permission, index) => new KeyValuePair<string, string?>(
                $"Authentication:Oidc:Memberships:0:Permissions:{60 + index}", permission)))));
        using var client = Client(host); await Token(client);
        WorkOrderPermissions(host.Services);

        // Synthetic fleet: the seeded tenant vessel plus FICTIONAL extra vessels through the audited API path.
        trace.Endpoint = "fixture";
        var fleet = new List<Guid> { SeedVessel.Id };
        for (var i = 1; i < vessels; i++)
        {
            using var created = await Send(client, "vessels", new { companyId = SeedVessel.CompanyId, code = $"PF-{i:00}", name = $"FICTIONAL perf vessel {i:00}", kind = "Tug" });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            fleet.Add((await created.Content.ReadFromJsonAsync<VesselDto>(Ct))!.Id);
        }
        // Draft work orders only (state DRAFT / REVIEW_REQUIRED); no completion, verification or closure.
        for (var i = 0; i < workOrders; i++)
        {
            var command = WorkOrderDraft() with { Fields = new() { ["findings"] = new("Known", $"FICTIONAL perf work order {i:0000}") } };
            using var saved = await WorkOrderSend(client, command);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        var probes = new (string Key, string Surface, Func<string[]> Paths)[]
        {
            ("today", "Today", () => [$"/api/v1/poseidon-today?vesselId={SeedVessel.Id}"]),
            ("today-why", "Today", () => [$"/api/v1/why-engine?vesselId={SeedVessel.Id}&cardKey=regulatory-draft"]),
            ("today-fleet-sweep", "Today", () => fleet.Select(v => $"/api/v1/poseidon-today?vesselId={v}").ToArray()),
            ("readiness-critical-states", "Readiness", () => [$"/api/v1/vessels/{SeedVessel.Id}/critical-states"]),
            ("readiness-compliance-passport", "Readiness", () => [$"/api/v1/vessels/{SeedVessel.Id}/compliance-passport"]),
            ("audit-ready-chains", "AuditReady", () => [$"/api/v1/audit-ready/chains?vesselId={SeedVessel.Id}"]),
            ("work-orders-list", "WorkOrders", () => [WorkOrderPath()]),
        };
        var endpoints = new JsonArray();
        foreach (var probe in probes)
        {
            trace.Endpoint = probe.Key;
            var samples = new JsonArray(); var statuses = new JsonArray(); var commands = new JsonArray();
            for (var round = 0; round < warmup + iterations; round++)
            {
                var before = trace.Commands;
                var started = Stopwatch.GetTimestamp();
                var status = 200;
                foreach (var path in probe.Paths())
                {
                    using var response = await client.GetAsync(path, Ct);
                    await response.Content.ReadAsByteArrayAsync(Ct);
                    if (!response.IsSuccessStatusCode) status = (int)response.StatusCode;
                }
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (round < warmup) continue;
                samples.Add(Math.Round(elapsed, 3)); statuses.Add(status); commands.Add(trace.Commands - before);
            }
            endpoints.Add(new JsonObject
            {
                ["key"] = probe.Key, ["surface"] = probe.Surface, ["requestsPerSample"] = probe.Paths().Length,
                ["samplesMs"] = samples, ["statuses"] = statuses, ["dbCommands"] = commands,
            });
        }

        var raw = new JsonObject
        {
            ["schemaVersion"] = "poseidon.perf-raw.v1",
            ["requirement"] = "DEV-067",
            ["transport"] = "in-process TestServer + owned Testcontainers PostgreSQL",
            ["fixture"] = new JsonObject { ["vessels"] = fleet.Count, ["workOrders"] = workOrders, ["evidence"] = "NOT_INCLUDED", ["kind"] = "FICTIONAL_DRAFTS_ONLY" },
            ["iterations"] = iterations, ["warmup"] = warmup,
            ["slowQueryThresholdMs"] = PerfSlowQueryThresholdMs,
            ["endpoints"] = endpoints,
            ["slowQueries"] = trace.Slow(),
        };
        var output = Environment.GetEnvironmentVariable("POSEIDON_PERF_OUTPUT");
        if (!string.IsNullOrEmpty(output))
            await File.WriteAllTextAsync(output, raw.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Ct);
        // The core daily surface must have been measured on its success path; other surfaces report
        // their actual status so scripts/perf_baseline.py excludes non-success samples from percentiles.
        Assert.All(endpoints[0]!["statuses"]!.AsArray(), s => Assert.Equal(200, s!.GetValue<int>()));
    }

    private static int PerfSetting(string name, int fallback, int min, int max) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value >= min && value <= max ? value : fallback;

    // Command timing only (EF CommandExecutedEventData.Duration). SQL text is parameterized; values are
    // never logged. Requests are sequential, so the current endpoint key attributes each command.
    private sealed class PerfCommandTrace : DbCommandInterceptor
    {
        private readonly Lock gate = new();
        private readonly List<(string Endpoint, double Ms, string Sql)> slow = [];
        private int commands;
        public volatile string Endpoint = "startup";
        public int Commands { get { lock (gate) return commands; } }

        private void Record(DbCommand command, CommandExecutedEventData data)
        {
            var ms = data.Duration.TotalMilliseconds;
            lock (gate)
            {
                commands++;
                if (ms > PerfSlowQueryThresholdMs && slow.Count < 200)
                    slow.Add((Endpoint, Math.Round(ms, 3), command.CommandText.Length > 4000 ? command.CommandText[..4000] : command.CommandText));
            }
        }

        public JsonArray Slow()
        {
            lock (gate)
                return new JsonArray(slow.Select(s => (JsonNode)new JsonObject { ["endpoint"] = s.Endpoint, ["durationMs"] = s.Ms, ["sql"] = s.Sql }).ToArray());
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        { Record(command, eventData); return result; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        { Record(command, eventData); return ValueTask.FromResult(result); }
        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
        { Record(command, eventData); return result; }
        public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        { Record(command, eventData); return ValueTask.FromResult(result); }
        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        { Record(command, eventData); return result; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        { Record(command, eventData); return ValueTask.FromResult(result); }
    }
}
