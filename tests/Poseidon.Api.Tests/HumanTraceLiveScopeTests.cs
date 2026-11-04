using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-030/031/032, T-S3-005/006/007: actual HTTP + owned PostgreSQL.
// Fictional human recording exercises software only. All execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class HumanTraceRowsBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public Task WaitForRows(CancellationToken ct) => read.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        public void Release() => released.TrySetResult();
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) &&
                command.CommandText.Contains("FROM core.\"HumanReviewEvents\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                read.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData("appointment")]
    [InlineData("historical-appointment")]
    [InlineData("role")]
    [InlineData("file-permission")]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("actor-kind")]
    [InlineData("recording-type")]
    [InlineData("recording-subject")]
    [InlineData("vessel-scope")]
    [InlineData("unchanged")]
    public async Task Private_trace_response_rechecks_live_operator_scope_after_the_private_row_statement(string change)
    {
        var barrier = new HumanTraceRowsBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host);
        GraphPermissionsForTest(host.Services); await Token(client);
        var (evidence, control) = await ReviewInputs(client);
        RecordingAppointment(host.Services, evidence.Id, control.Id);
        var verification = await PendingReview(client, host.Services, evidence, control);
        var command = await RecordingCommand(client, host.Services, evidence, control, verification);
        command = command with { Rationale = "FICTIONAL-S3-PRIVATE-RATIONALE",
            Recording = command.Recording! with { HumanDecision = "FICTIONAL-S3-PRIVATE-DECISION" } };
        using var recorded = await Send(client, GraphRoute("decisions"), command);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var decision = (await recorded.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        var route = TraceRoute(evidence, control);
        if (change == "historical-appointment") route += "&asOf=" + Uri.EscapeDataString(decision.AsOf.ToString("O"));
        var stable = await client.GetStringAsync(route, Ct);
        Assert.Contains(command.Rationale, stable); Assert.Contains(command.Recording.HumanDecision, stable);
        long writes; long receipts; long events;
        await using (var db = Db()) {
            writes = await db.AuditEvents.LongCountAsync(Ct);
            receipts = await db.HumanReviewReceipts.LongCountAsync(Ct);
            events = await db.HumanReviewEvents.LongCountAsync(Ct);
        }
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[0];
        barrier.Arm(); var pending = client.GetAsync(route, Ct);
        HttpResponseMessage? response = null;
        try {
            await barrier.WaitForRows(Ct);
            switch (change) {
                case "appointment":
                case "historical-appointment": original.HumanAuthorities = []; break;
                case "role": original.RoleReferences = [Guid.NewGuid().ToString("D")]; break;
                case "file-permission": settings.Memberships[0] = original with { Permissions = original.Permissions.Where(p => p != "file.read").ToArray() }; break;
                case "tenant": settings.Memberships[0] = original with { TenantId = seed.Tenants[1].Id }; break;
                case "actor": settings.Memberships[0] = original with { ActorId = Guid.NewGuid() }; break;
                case "actor-kind": original.ActorKind = "AI"; break;
                case "recording-type":
                case "recording-subject":
                    original.HumanAuthorities = original.HumanAuthorities.Select(g => g.Capability != "human.decide" ? g : g with {
                        Recording = change == "recording-type" ? g.Recording! with { DecisionType = "FICTIONAL-OTHER-TYPE" } :
                            g.Recording! with { SubjectId = Guid.NewGuid() }
                    }).ToArray();
                    break;
                case "vessel-scope": settings.Memberships[0] = original with { TenantWideManage = false, TenantWideRead = false, VesselGrants = [] }; break;
                case "unchanged":
                    // An equivalent operator reload and reordered set do not
                    // revoke access or depend on array/reference identity.
                    settings.Memberships[0] = original with { Permissions = original.Permissions.Reverse().ToArray(),
                        VesselGrants = original.VesselGrants!.Reverse().ToArray(), HumanAuthorities = original.HumanAuthorities.Reverse().ToArray() };
                    break;
            }
            barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            var body = await response.Content.ReadAsStringAsync(Ct);
            if (change == "unchanged") {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains(command.Rationale, body); Assert.Contains(command.Recording.HumanDecision, body);
                Assert.Contains(decision.Id.ToString("D"), body);
            } else {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                foreach (var hidden in new[] { command.Rationale, command.Recording.HumanDecision, decision.Id.ToString("D"),
                    evidence.Id.ToString("D"), control.Id.ToString("D"), actorId.ToString("D"), ReviewOperation.ToString("D") })
                    Assert.DoesNotContain(hidden, body);
                using var next = await client.GetAsync(route, Ct);
                var nextBody = await next.Content.ReadAsStringAsync(Ct);
                Assert.DoesNotContain(command.Rationale, nextBody); Assert.DoesNotContain(command.Recording.HumanDecision, nextBody);
                Assert.DoesNotContain(decision.Id.ToString("D"), nextBody);
                // Changed type/subject grants may retain a generic authorized
                // trace, but cannot recover the formerly assigned private event.
                Assert.True(next.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound ||
                    change is "recording-type" or "recording-subject" or "actor" && next.StatusCode == HttpStatusCode.OK);
                await using var audit = Db(settings.Memberships[0].TenantId);
                Assert.Contains(await audit.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "HumanReview" &&
                    e.Result == "trace_reader_required" && e.ResourceId == null && e.ActorId == settings.Memberships[0].ActorId.ToString());
            }
            await using var verify = Db();
            Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
            Assert.Equal(receipts, await verify.HumanReviewReceipts.LongCountAsync(Ct));
            Assert.Equal(events, await verify.HumanReviewEvents.LongCountAsync(Ct));
            var retained = await verify.HumanReviewEvents.SingleAsync(e => e.Id == decision.Id, Ct);
            Assert.Equal(command.Rationale, retained.Rationale); Assert.Equal(decision.ContextHash, retained.ContextHash);
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    [Theory]
    [InlineData("withdrawn")]
    [InlineData("types")]
    [InlineData("object")]
    public async Task Assigned_audit_reader_pending_response_loses_private_metadata_when_its_live_binding_changes(string change)
    {
        var barrier = new HumanTraceRowsBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host);
        GraphPermissionsForTest(host.Services); await Token(client);
        var (evidence, control) = await ReviewInputs(client);
        RecordingAppointment(host.Services, evidence.Id, control.Id);
        var verification = await PendingReview(client, host.Services, evidence, control);
        var command = await RecordingCommand(client, host.Services, evidence, control, verification);
        using var recorded = await Send(client, GraphRoute("decisions"), command);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var decision = (await recorded.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        var settings = host.Services.GetRequiredService<FederationSettings>(); var original = settings.Memberships[0];
        var now = DateTimeOffset.UtcNow;
        var reader = new HumanTraceReader(Guid.NewGuid(), SeedVessel.Id, evidence.Id, control.Id, ReviewRole,
            ["Decision"], [command.Recording!.DecisionType], now.AddHours(-1), now.AddHours(1));
        settings.Memberships[0] = original with {
            Permissions = ["directory.read", "audit.read", "human.trace.read", "regulatory.draft.read", "control.read", "evidence.read", "file.read"],
            TenantWideManage = false, TenantWideRead = false, VesselGrants = [new(SeedVessel.Id)],
            HumanAuthorities = [], HumanTraceReaders = [reader]
        };
        var route = TraceRoute(evidence, control);
        var stable = await client.GetFromJsonAsync<JsonElement>(route, Ct);
        Assert.Empty(stable.GetProperty("items").EnumerateArray());
        Assert.Contains(stable.GetProperty("auditItems").EnumerateArray(), e => e.GetProperty("id").GetGuid() == decision.Id);
        barrier.Arm(); var pending = client.GetAsync(route, Ct); HttpResponseMessage? response = null;
        try {
            await barrier.WaitForRows(Ct);
            settings.Memberships[0].HumanTraceReaders = change switch {
                "withdrawn" => [], "types" => [reader with { DecisionTypes = ["FICTIONAL-OTHER-TYPE"] }],
                _ => [reader with { EvidenceId = Guid.NewGuid() }]
            };
            barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(decision.Id.ToString("D"), body); Assert.DoesNotContain(decision.ActorId.ToString("D"), body);
            Assert.DoesNotContain(command.Rationale, body); Assert.DoesNotContain(command.Recording.HumanDecision, body);
            using var next = await client.GetAsync(route, Ct);
            Assert.DoesNotContain(decision.Id.ToString("D"), await next.Content.ReadAsStringAsync(Ct));
            Assert.True(next.StatusCode == HttpStatusCode.Forbidden || change == "types" && next.StatusCode == HttpStatusCode.OK);
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }
}
