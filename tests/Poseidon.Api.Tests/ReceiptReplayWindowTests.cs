using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// CI16-CURRENT-COMPLIANCE / 14.01 paragraphs 68,175: a committed receipt
// must win over stale editor tokens even when its duplicate started earlier.
// Actual API/CSRF/owned PostgreSQL; the barrier controls scheduling, not data.
public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Human_recording_duplicate_replays_when_winner_commits_between_receipt_lookup_and_transaction(bool revoke)
    {
        var window = new ReceiptLookupWindow("HumanReviewReceipts");
        await using var host = Host(interceptor: window); using var c = Client(host);
        GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var verification = await PendingReview(c, host.Services, ev, co);
        var cmd = await RecordingCommand(c, host.Services, ev, co, verification);
        var key = Guid.NewGuid().ToString();
        window.Arm();
        var delayed = Send(c, GraphRoute("decisions"), cmd, key);
        try {
            await window.Entered.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            using var winner = await Send(c, GraphRoute("decisions"), cmd, key);
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
            var original = await winner.Content.ReadAsStringAsync(Ct);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            if (revoke) settings.Memberships[0].HumanAuthorities = [];
            window.Release();
            var duplicate = await delayed;
            Assert.Equal(revoke ? HttpStatusCode.Forbidden : HttpStatusCode.OK, duplicate.StatusCode);
            if (!revoke) Assert.Equal(original, await duplicate.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            Assert.Single(await db.HumanReviewEvents.Where(e => e.Kind == "Decision").ToArrayAsync(Ct));
            Assert.Single(await db.HumanReviewReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
            settings.Memberships[0].HumanAuthorities = [];
            using var revoked = await Send(c, GraphRoute("decisions"), cmd, key);
            Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        } finally { window.Release(); using var completed = await delayed; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_qualification_duplicate_replays_when_winner_commits_between_receipt_lookup_and_transaction(bool revoke)
    {
        var window = new ReceiptLookupWindow("RegulatoryQualificationReceipts");
        await using var host = Host(interceptor: window, regulatoryRead: true, regulatoryWrite: true,
            applicabilityRead: true, applicabilityEvaluate: true, ruleWrite: true, ruleReview: true);
        using var c = Client(host); await Token(c);
        var (rule, source, _) = await AdmissionInputs(c);
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, actorId,
            mechanism: "FICTIONAL_REQUIRED_SIGNATURE");
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var grant = settings.Memberships[0].RegulatoryAuthorities.Single(g => g.VesselId == null);
        var cmd = new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", new());
        var key = Guid.NewGuid().ToString(); var route = $"regulatory-source-governance/{rule.Current.Id}/events";
        window.Arm();
        var delayed = Send(c, route, cmd, key);
        try {
            await window.Entered.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            using var winner = await Send(c, route, cmd, key);
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
            var original = await winner.Content.ReadAsStringAsync(Ct);
            if (revoke) settings.Memberships[0].RegulatoryAuthorities = [];
            window.Release();
            var duplicate = await delayed;
            Assert.Equal(revoke ? HttpStatusCode.Forbidden : HttpStatusCode.OK, duplicate.StatusCode);
            if (!revoke) Assert.Equal(original, await duplicate.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            Assert.Single(await db.RegulatoryQualificationEvents.Where(e => e.RuleId == rule.Current.Id).ToArrayAsync(Ct));
            Assert.Single(await db.RegulatoryQualificationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
            settings.Memberships[0].RegulatoryAuthorities = [];
            using var revoked = await Send(c, route, cmd, key);
            Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        } finally { window.Release(); using var completed = await delayed; }
    }

    private sealed class ReceiptLookupWindow(string table) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public Task Entered => entered.Task;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public void Release() => released.TrySetResult();
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.Database.CurrentTransaction == null &&
                command.CommandText.Contains($"FROM core.\"{table}\"", StringComparison.Ordinal) &&
                !result.HasRows && Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                entered.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
