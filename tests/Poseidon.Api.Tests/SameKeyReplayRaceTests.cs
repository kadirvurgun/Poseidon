using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// Same-Idempotency-Key duplicate that misses the receipt in its pre-transaction
// lookup while the winner has not committed yet, then reaches its in-transaction
// version/precondition check after the winner committed business change and
// receipt atomically. The duplicate must replay the committed receipt instead of
// answering 409. Deterministic form of the PR #31 race class: ReceiptLookupWindow
// holds the late request right after its receipt miss until the winner returned.
// Actual API/CSRF/owned PostgreSQL; the barrier controls scheduling, not data.
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<(HttpResponseMessage Winner, HttpResponseMessage Late)> SameKeyRace(ReceiptLookupWindow window, Func<Task<HttpResponseMessage>> send)
    {
        window.Arm();
        var late = send();
        HttpResponseMessage? winner = null;
        try {
            var first = await Task.WhenAny(window.Entered, late).WaitAsync(TimeSpan.FromSeconds(60), Ct);
            Assert.True(first == window.Entered, "The duplicate completed without entering the receipt-lookup window.");
            winner = await send();
            Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        } catch { window.Release(); winner?.Dispose(); (await late).Dispose(); throw; }
        window.Release();
        return (winner, await late);
    }

    private static async Task AssertExactReplay(HttpResponseMessage winner, HttpResponseMessage late, CancellationToken ct)
    {
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        Assert.Equal(await winner.Content.ReadAsStringAsync(ct), await late.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task Defect_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("DefectReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); DefectPermissions(host.Services);
        var command = DefectDraft(); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => DefectSend(c, command, key));
        using (winner) using (late) {
            Assert.Equal(HttpStatusCode.OK, late.StatusCode);
            Assert.Equal(DefectFacts((await winner.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!), DefectFacts((await late.Content.ReadFromJsonAsync<DefectRecordDto>(Ct))!));
        }
        await using var db = Db(); Assert.Equal(1, await db.DefectRevisions.CountAsync(r => r.DefectId == command.Id, Ct)); Assert.Single(await db.DefectReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Downtime_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("DowntimeReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); DowntimePermissions(host.Services);
        var command = DowntimeDraft(); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => DowntimeSend(c, command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.DowntimeRevisions.CountAsync(r => r.DowntimeId == command.Id, Ct)); Assert.Single(await db.DowntimeReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task WorkOrder_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("WorkOrderReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); WorkOrderPermissions(host.Services);
        var command = WorkOrderDraft(); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => WorkOrderSend(c, command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.WorkOrderRevisions.CountAsync(r => r.WorkOrderId == command.Id, Ct)); Assert.Single(await db.WorkOrderReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task ShipOps_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("ShipOperationReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); ShipOpsPermissions(host.Services);
        var command = new ShipOpsDraftCommand(Guid.NewGuid(), 0, new() { ["bdn_no"] = new("Known", "FICTIONAL replay window") }, "INITIAL_AUTHORING"); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => ShipOpsSend(c, "Fuel", command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.ShipOperationRevisions.CountAsync(r => r.RecordId == command.Id, Ct)); Assert.Single(await db.ShipOperationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Daily_draft_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("DailyOperationReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); DailyPermissions(host.Services);
        var command = new DailyDraftCommand(Guid.NewGuid(), 0, new() { ["weather_summary"] = new("Known", "FICTIONAL replay window") }, "INITIAL_AUTHORING"); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => DailySend(c, "noon-reports", command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.DailyOperationRevisions.CountAsync(r => r.RecordId == command.Id, Ct)); Assert.Single(await db.DailyOperationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Daily_review_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("DailyReviewReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); DailyPermissions(host.Services);
        using var created = await DailySend(c, "noon-reports", new DailyDraftCommand(Guid.NewGuid(), 0, new(), "INITIAL_AUTHORING"), Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); var row = (await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var grant = DailyAppointment(host.Services, row.Id, "Noon", "Master");
        var command = new DailyReviewCommand(row.Version, 0, row.Revision, grant.Id, "REVIEWED", "REVIEW_RECORDED", "FICTIONAL replay window review"); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => ReviewSend(c, "noon-reports", row.Id, command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Single(await db.DailyReviewEvents.Where(r => r.RecordId == row.Id).ToArrayAsync(Ct)); Assert.Single(await db.DailyReviewReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Certificate_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("CertificateReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); CertificatePermissions(host.Services); await Token(c);
        var command = new CertificateSaveCommand(Guid.NewGuid(), 0, new(new(new(Issuer: "FICTIONAL declared issuer")), DocumentName: "FICTIONAL replay window"), "INITIAL_AUTHORING"); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => Send(c, CertificatePath(), command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.VesselCertificateRevisions.CountAsync(r => r.CertificateId == command.Id, Ct)); Assert.Single(await db.CertificateCommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Renewal_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("CertificateReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); RenewalPermissions(host.Services); await Token(c);
        var original = await CertificateForRenewal(c);
        var role = Guid.Parse(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0]);
        var content = new CertificateRenewalContent(OwnerRoleId: role, RequiredSurvey: true, RequiredDocuments: true, RequiredActions: null, PlannedLocation: "FICTIONAL replay window");
        var command = new CertificateRenewalCommand(Guid.NewGuid(), 0, original.Id, original.Revision, original.Version, content, "INITIAL_AUTHORING"); var key = Guid.NewGuid().ToString("D");
        var (winner, late) = await SameKeyRace(window, () => Send(c, RenewalPath(), command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Equal(1, await db.CertificateRenewalRevisions.CountAsync(r => r.RenewalId == command.Id, Ct)); Assert.Single(await db.CertificateCommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Certificate_assertion_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-assertion-race-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            var window = new ReceiptLookupWindow("CertificateReceipts");
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); await using var host = FileHost(root, scanner.Port, window); using var c = Client(host);
            FilePermissions(host.Services); CertificatePermissions(host.Services); await Token(c); var certificate = await CertificateWithScannedFile(c);
            var grant = AssertionAppointment(host.Services, certificate.Id);
            var command = new CertificateAssertionCommand(certificate.Version, certificate.Revision, 0, grant.Id, "Verified", "FICTIONAL replay window assertion"); var key = Guid.NewGuid().ToString("D");
            var (winner, late) = await SameKeyRace(window, () => Send(c, AssertionPath(certificate.Id), command, key));
            using (winner) using (late) await AssertExactReplay(winner, late, Ct);
            await using var db = Db(); Assert.Single(await db.CertificateAssertionEvents.Where(e => e.CertificateId == certificate.Id).ToArrayAsync(Ct)); Assert.Single(await db.CertificateCommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Extraction_confirm_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var root = ExtractionRoot(); var secret = ExtractionSecret(); using var scanner = new ScanPeer("stream: OK\0"); var p = ExtractionPolicy(secret); using var peer = new ExtractionPeer { Policy = p };
        try {
            var window = new ReceiptLookupWindow("CertificateReceipts");
            await using var host = ExtractionHost(root, scanner.Port, p, peer, interceptor: window); using var c = Client(host); ExtractionPermissions(host.Services); await Token(c);
            var original = await ExtractionSource(c); var preview = await ExtractSource(c, original); var command = ConfirmFacts(preview); var key = Guid.NewGuid().ToString("D");
            var (winner, late) = await SameKeyRace(window, () => Send(c, ExtractionPath(original) + "/confirm", command, key));
            using (winner) using (late) await AssertExactReplay(winner, late, Ct);
            await using var db = Db(); Assert.Equal(2, await db.VesselCertificateRevisions.CountAsync(r => r.CertificateId == original.Id, Ct)); Assert.Single(await db.CertificateCommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        } finally { Environment.SetEnvironmentVariable(secret, null); RemoveExtractionFiles(root); }
    }

    [Fact]
    public async Task Notification_ack_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("NotificationReceipts"); await using var host = NotificationHost(interceptor: window); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, PlansUrl, CurrentCalendar())).StatusCode); await RefreshNotifications(c);
        var notice = (await Inbox(c)).Items[0]; var command = new NotificationAckCommand(notice.Version); var key = Guid.NewGuid().ToString();
        var (winner, late) = await SameKeyRace(window, () => Send(c, NotificationPath($"/{notice.Id}/acknowledge"), command, key));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Single(await db.NotificationEvents.Where(e => e.NotificationId == notice.Id && e.Kind == "Acknowledged").ToArrayAsync(Ct)); Assert.Single(await db.NotificationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Notification_preference_duplicate_key_reaching_its_transaction_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("NotificationReceipts"); await using var host = NotificationHost(interceptor: window); using var c = Client(host); NotificationPermissions(host.Services); await Token(c);
        var command = new NotificationPreferenceCommand(0, true); var key = Guid.NewGuid().ToString();
        var (winner, late) = await SameKeyRace(window, () => Send(c, NotificationPath("/preference"), command, key, update: true));
        using (winner) using (late) await AssertExactReplay(winner, late, Ct);
        await using var db = Db(); Assert.Single(await db.NotificationPreferences.Where(x => x.ActorId == actorId).ToArrayAsync(Ct)); Assert.Single(await db.NotificationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Document_create_duplicate_key_committed_by_the_winner_after_the_receipt_miss_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("CommandReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); FilePermissions(host.Services); await Token(c);
        var path = $"vessels/{SeedVessel.Id}/documents"; var key = Guid.NewGuid().ToString();
        await using (var before = Db()) Assert.Empty(await before.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        var (winner, late) = await SameKeyRace(window, () => Send(c, path, new { title = "FICTIONAL replay window" }, key));
        DocumentDraftDto saved;
        using (winner) using (late) {
            Assert.Equal(HttpStatusCode.OK, late.StatusCode); saved = (await winner.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct))!;
            Assert.Equal(saved, await late.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct));
        }
        await using var db = Db(); var receipt = Assert.Single(await db.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct)); Assert.Equal(saved.Id, receipt.ResourceId);
        Assert.Empty(await db.DocumentVersions.Where(v => v.VesselId == SeedVessel.Id && v.Title == "FICTIONAL replay window" && v.DocumentId != saved.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Document_revision_duplicate_key_reaching_its_etag_check_after_the_winner_committed_replays_instead_of_conflicting()
    {
        var window = new ReceiptLookupWindow("CommandReceipts"); await using var host = Host(interceptor: window); using var c = Client(host); FilePermissions(host.Services); await Token(c);
        var path = $"vessels/{SeedVessel.Id}/documents";
        using var created = await Send(c, path, new { title = (string?)null }); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct))!; c.DefaultRequestHeaders.IfMatch.ParseAdd($"\"{draft.Version}\"");
        var key = Guid.NewGuid().ToString();
        var (winner, late) = await SameKeyRace(window, () => Send(c, $"{path}/{draft.Id}/versions", new { title = "FICTIONAL replay window" }, key));
        using (winner) using (late) {
            Assert.Equal(HttpStatusCode.OK, late.StatusCode);
            Assert.Equal(await winner.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct), await late.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct));
        }
        await using var db = Db(); Assert.Equal(2, await db.DocumentVersions.CountAsync(v => v.DocumentId == draft.Id, Ct)); Assert.Single(await db.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }
}
