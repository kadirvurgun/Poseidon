using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S11 WP-B DEV-072 (18.01 Backlog row 73, T-S11-004): privacy-safe pilot telemetry over the real
// host, antiforgery and owned PostgreSQL. Synthetic memberships only. AUTHORED; execution in CI.
public sealed partial class DirectoryWorkspaceTests
{
    private static object PilotEvent(string kind, string? journey = null, string? ai = null, int? duration = null, string area = "SHIP") =>
        new { kind, area, journey, aiSource = ai, durationMs = duration, occurredAt = DateTimeOffset.UtcNow.AddMinutes(-3) };

    [Fact]
    [Trait("Requirement", "DEV-072")]
    public async Task Pilot_metrics_are_enrolled_idempotent_pseudonymous_and_small_groups_never_show_a_value()
    {
        await using var host = Host(); using var c = Client(host);
        var settings = host.Services.GetRequiredService<PilotTelemetrySettings>();
        var first = Guid.NewGuid();
        var batch = new { events = new[] { PilotEvent("JOURNEY_STARTED", "SHIP_DEPARTURE"), PilotEvent("JOURNEY_COMPLETED", "SHIP_DEPARTURE"),
            PilotEvent("EVIDENCE_SEARCH", duration: 1200), PilotEvent("ROLE_ACTIVE") } };

        // Not enrolled: no contribution. Enrolled but no approved retention: nothing is collected.
        S9Actor(host.Services, first, "Human", PilotMetricsPolicy.ReadPermission); await Token(c);
        await AssertCode(await Send(c, "pilot-metrics/events", batch), HttpStatusCode.Forbidden, "permission_denied");
        S9Actor(host.Services, first, "Human", PilotMetricsPolicy.ContributePermission, PilotMetricsPolicy.ReadPermission); await Token(c);
        await AssertCode(await Send(c, "pilot-metrics/events", batch), HttpStatusCode.ServiceUnavailable, "retention_unconfigured");
        var caps = (await c.GetFromJsonAsync<PilotMetricCapabilitiesDto>("/api/v1/pilot-metrics/capabilities", Ct))!;
        Assert.Equal((false, true, "UNCONFIGURED", (int?)null), (caps.CanContribute, caps.CanRead, caps.RetentionState, caps.RetentionDays));
        var unconfigured = (await c.GetFromJsonAsync<PilotMetricSummaryDto>("/api/v1/pilot-metrics/summary", Ct))!;
        Assert.All(unconfigured.Items, i => Assert.Equal((MetricStates.Unavailable, (string?)null, "RETENTION_UNCONFIGURED"), (i.State, i.Value, i.Reasons.Single())));

        settings.RetentionDays = 30; settings.RetentionPolicyRef = "synthetic-pilot-retention-v1";
        // Identity, free text and unknown codes are refused: the schema has no such member.
        await AssertCode(await Send(c, "pilot-metrics/events", new { events = new[] { new { kind = "ROLE_ACTIVE", area = "SHIP", occurredAt = DateTimeOffset.UtcNow, actorId = first } } }),
            HttpStatusCode.BadRequest, "invalid_request");
        await AssertCode(await Send(c, "pilot-metrics/events", new { events = new[] { PilotEvent("JOURNEY_COMPLETED", "FICTIONAL report REF-42 by J. Doe") } }),
            HttpStatusCode.BadRequest, "invalid_request");
        await AssertCode(await Send(c, "pilot-metrics/events", new { events = new[] { PilotEvent("AI_SUGGESTION_CORRECTED", ai: "TUG_RECOMMENDATION") } }),
            HttpStatusCode.BadRequest, "invalid_request"); // tug recommendation is not a ship AI source
        await AssertCode(await Send(c, "pilot-metrics/events?tenantId=" + seed.Tenants[1].Id, batch), HttpStatusCode.BadRequest, "invalid_query");
        await AssertCode(await Send(c, "pilot-metrics/events", batch, key: ""), HttpStatusCode.BadRequest, "invalid_idempotency_key");

        var key = Guid.NewGuid().ToString();
        var accepted = await TechOk<PilotMetricIngestDto>(await Send(c, "pilot-metrics/events", batch, key));
        Assert.Equal((4, false, PilotMetricsPolicy.SchemaVersion), (accepted.Accepted, accepted.Replayed, accepted.SchemaVersion));
        var replay = await TechOk<PilotMetricIngestDto>(await Send(c, "pilot-metrics/events", batch, key));
        Assert.Equal((accepted.BatchId, true), (replay.BatchId, replay.Replayed));
        await AssertCode(await Send(c, "pilot-metrics/events", new { events = new[] { PilotEvent("ROLE_ACTIVE") } }, key), HttpStatusCode.Conflict, "idempotency_key_reused");

        await using (var db = Db())
        {
            var row = await db.PilotMetricBatches.SingleAsync(b => b.Id == accepted.BatchId, Ct);
            var text = JsonSerializer.Serialize(row);
            Assert.DoesNotContain(first.ToString(), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(seed.Tenants[0].Id.ToString(), row.EventsJson, StringComparison.OrdinalIgnoreCase);
            Assert.Matches("^[0-9a-f]{32}$", row.ActorBucket); Assert.Matches("^[0-9a-f]{32}$", row.RoleBucket);
            Assert.Equal((0, 0, 4, "synthetic-pilot-retention-v1"), (row.ReceivedHour.Minute, row.ReceivedHour.Second, row.EventCount, row.RetentionPolicyRef));
            Assert.Equal(row.ReceivedHour.AddDays(30), row.ExpiresAt);
            Assert.Single(await db.PilotMetricReceipts.Where(r => r.BatchId == accepted.BatchId).ToArrayAsync(Ct)); // the replay wrote nothing
            var salt = await db.PilotMetricTenantSalts.SingleAsync(Ct);
            var audits = await db.AuditEvents.Where(a => a.EntityType == nameof(PilotMetricBatch) || a.EntityType == nameof(PilotMetricTenantSalt)).ToArrayAsync(Ct);
            Assert.Contains(audits, a => a.EntityType == nameof(PilotMetricBatch) && a.ResourceId == accepted.BatchId && a.ActorId == first.ToString());
            Assert.All(audits, a => Assert.DoesNotContain(row.ActorBucket, a.ChangeSummary ?? ""));
            Assert.All(audits, a => Assert.DoesNotContain(Convert.ToBase64String(salt.Salt), a.ChangeSummary ?? ""));
        }
        await using (var other = Db(seed.Tenants[1].Id)) Assert.False(await other.PilotMetricBatches.AnyAsync(Ct));

        // One contributor: every populated metric is SUPPRESSED, absent observations are UNKNOWN, never 0.
        var small = (await c.GetFromJsonAsync<PilotMetricSummaryDto>("/api/v1/pilot-metrics/summary?windowDays=7", Ct))!;
        var byId = small.Items.ToDictionary(i => i.MetricId);
        Assert.Equal((MetricStates.Suppressed, (string?)null, (int?)null), (byId[PilotMetricsPolicy.JourneyCompletion].State, byId[PilotMetricsPolicy.JourneyCompletion].Value, byId[PilotMetricsPolicy.JourneyCompletion].Contributors));
        Assert.Equal(MetricStates.Suppressed, byId[PilotMetricsPolicy.EvidenceSearchP95].State);
        Assert.Equal((MetricStates.Unknown, "NO_OBSERVATIONS"), (byId[PilotMetricsPolicy.SyncConflicts].State, byId[PilotMetricsPolicy.SyncConflicts].Reasons.Single()));
        Assert.Equal(MetricStates.Unknown, byId[PilotMetricsPolicy.AiCorrectionRate].State);
        Assert.DoesNotContain(small.Items.SelectMany(i => i.Breakdown), b => b.Value != null);

        // Five more pseudonymous contributors: the completion share becomes a value (5 of 6 = 833 permille).
        for (var i = 0; i < 5; i++)
        {
            S9Actor(host.Services, Guid.NewGuid(), "Human", PilotMetricsPolicy.ContributePermission); await Token(c);
            await TechOk<PilotMetricIngestDto>(await Send(c, "pilot-metrics/events", new { events = new[] { PilotEvent(i < 4 ? "JOURNEY_COMPLETED" : "JOURNEY_ABANDONED", "SHIP_DEPARTURE") } }));
        }
        S9Actor(host.Services, first, "Human", PilotMetricsPolicy.ReadPermission); await Token(c);
        var summary = (await c.GetFromJsonAsync<PilotMetricSummaryDto>("/api/v1/pilot-metrics/summary", Ct))!;
        var completion = summary.Items.Single(i => i.MetricId == PilotMetricsPolicy.JourneyCompletion);
        Assert.Equal((MetricStates.Value, "833", (int?)6, "permille"), (completion.State, completion.Value, completion.Contributors, completion.UnitCode));
        Assert.Contains(completion.Breakdown, b => b.Dimension == "JOURNEY" && b.Key == "SHIP_DEPARTURE" && b.Value == "833");
        Assert.Equal(("CONFIGURED", "DECLARED_UNVERIFIED", 7), (summary.RetentionState, summary.Basis, summary.WindowDays));

        // Summary needs explicit read permission and tenant-wide scope; queries cannot select another tenant.
        await AssertCode(await c.GetAsync("/api/v1/pilot-metrics/summary?windowDays=8", Ct), HttpStatusCode.BadRequest, "invalid_query");
        await AssertCode(await c.GetAsync("/api/v1/pilot-metrics/summary?tenantId=" + seed.Tenants[1].Id, Ct), HttpStatusCode.BadRequest, "invalid_query");
        S9Actor(host.Services, first, "Human", PilotMetricsPolicy.ContributePermission); await Token(c);
        await AssertCode(await c.GetAsync("/api/v1/pilot-metrics/summary", Ct), HttpStatusCode.Forbidden, "permission_denied");
        // The generic scalar endpoint refuses the pilot window scope.
        S9Actor(host.Services, first, "Human", PilotMetricsPolicy.ReadPermission); await Token(c);
        await AssertCode(await c.GetAsync("/api/v1/metrics/scalar?metricId=" + PilotMetricsPolicy.JourneyCompletion, Ct), HttpStatusCode.BadRequest, "invalid_metric_scope");
    }
}
