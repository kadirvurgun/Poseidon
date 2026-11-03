using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S1-FILES-REJECTION-01 / DEV-018 / T-S1-006. Real HTTP/PostgreSQL;
// scripted scanner peers exercise failures and concurrency, not malware deployment efficacy.
public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData("checksum")]
    [InlineData("magic")]
    [InlineData("overflow")]
    [InlineData("truncated")]
    [InlineData("malware")]
    [InlineData("scanner-unavailable")]
    public async Task Rejected_actual_bytes_preserve_exact_pending_intent_and_all_previously_accepted_content(string rejection)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var scanner = new ScanPeer("stream: OK\0");
        await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var acceptedIntent = await StagePng(client);
        using var acceptedResponse = await PutPng(client, acceptedIntent.Id); Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        var accepted = (await acceptedResponse.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        using var rejectingScanner = rejection == "malware" ? new ScanPeer("stream: FOUND\0") : null;
        if (rejectingScanner != null) host.Services.GetRequiredService<SecureFileSettings>().ClamdPort = rejectingScanner.Port;
        if (rejection == "scanner-unavailable") host.Services.GetRequiredService<SecureFileSettings>().ClamdPort = 1;
        await using var db = Db();
        var acceptedFile = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == accepted.Id, Ct);
        var receipt = await db.CommandReceipts.AsNoTracking().SingleAsync(x => x.ResourceId == accepted.Id, Ct);
        var documents = await db.DocumentVersions.AsNoTracking().OrderBy(x => x.DocumentId).ThenBy(x => x.Revision).Select(x => new { x.DocumentId, x.Revision }).ToArrayAsync(Ct);
        var evidence = await db.ControlEvidenceRevisions.AsNoTracking().OrderBy(x => x.Id).ThenBy(x => x.Revision).Select(x => new { x.Id, x.Revision }).ToArrayAsync(Ct);
        var bytes = rejection == "magic" ? Encoding.ASCII.GetBytes("<html>FICTIONAL invalid PNG</html>") :
            rejection == "overflow" ? Png.Concat(Png).ToArray() : rejection == "truncated" ? Png[..^1] : Png;
        using var declared = await Send(client, $"vessels/{SeedVessel.Id}/files", new {
            name = "rejected.png", mime = "image/png", length = rejection == "magic" ? bytes.Length : Png.Length,
            checksum = rejection == "checksum" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(rejection == "magic" ? bytes : Png))
        });
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode); var stage = (await declared.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var auditIds = await db.AuditEvents.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct);
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/bytes") {
            Content = new UnknownLengthContent(bytes)
        };
        put.Content.Headers.ContentType = new("image/png");
        using var rejected = await client.SendAsync(put, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
        await AssertUnchangedRejectedIntent(db, stage);
        Assert.Equal(auditIds, await db.AuditEvents.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct));
        var retained = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == accepted.Id, Ct);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(acceptedFile), System.Text.Json.JsonSerializer.Serialize(retained));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(receipt), System.Text.Json.JsonSerializer.Serialize(await db.CommandReceipts.AsNoTracking().SingleAsync(x => x.ResourceId == accepted.Id, Ct)));
        Assert.Equal(documents, await db.DocumentVersions.AsNoTracking().OrderBy(x => x.DocumentId).ThenBy(x => x.Revision).Select(x => new { x.DocumentId, x.Revision }).ToArrayAsync(Ct));
        Assert.Equal(evidence, await db.ControlEvidenceRevisions.AsNoTracking().OrderBy(x => x.Id).ThenBy(x => x.Revision).Select(x => new { x.Id, x.Revision }).ToArrayAsync(Ct));
        using var access = await client.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{accepted.Id}/access", null, Ct);
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        var token = (await access.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Add("X-File-Capability", token);
        using var download = await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{accepted.Id}/bytes", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync(Ct));
        var path = Assert.Single(Directory.GetFiles(root));
        Assert.EndsWith(accepted.Id.ToString("N") + ".object", path);
        File.Delete(path); Directory.Delete(root, false);
    }

    [Theory]
    [InlineData("retired-type")]
    [InlineData("lowered-limit")]
    [InlineData("wrong-type")]
    [InlineData("wrong-length")]
    public async Task Metadata_or_current_operator_policy_rejection_does_not_claim_or_freeze_pending_intent(string rejection)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var scanner = new ScanPeer("stream: OK\0");
        await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client); var settings = host.Services.GetRequiredService<SecureFileSettings>();
        if (rejection == "retired-type") settings.AllowedTypes = ["application/pdf"];
        if (rejection == "lowered-limit") settings.MaxBytes = Png.Length - 1;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/bytes") {
            Content = new ByteArrayContent(rejection == "wrong-length" ? Png[..^1] : Png)
        };
        request.Content.Headers.ContentType = new(rejection == "wrong-type" ? "application/pdf" : "image/png");
        using var response = await client.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Db(); await AssertUnchangedRejectedIntent(db, stage); Assert.Empty(Directory.GetFiles(root));
        settings.AllowedTypes = ["image/png"]; settings.MaxBytes = 1024 * 1024;
        using var corrected = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.Single(await db.StoredFiles.Where(x => x.Id == stage.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == stage.Id && x.Action == "FileFinalize").ToArrayAsync(Ct));
        File.Delete(Assert.Single(Directory.GetFiles(root)));
        Directory.Delete(root, false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Membership_or_type_policy_withdrawn_during_clean_scan_preserves_pending_state_and_owned_spool_cleanup(bool withdrawPolicy)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        FederationSettings? federation = null; SecureFileSettings? settings = null;
        using var scanner = new ScanPeer("stream: OK\0", beforeReply: () => {
            if (withdrawPolicy) settings!.AllowedTypes = ["application/pdf"];
            else federation!.Memberships[0] = federation.Memberships[0] with { Permissions = ["directory.read", "file.read"] };
            return Task.CompletedTask;
        });
        await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        federation = host.Services.GetRequiredService<FederationSettings>(); settings = host.Services.GetRequiredService<SecureFileSettings>();
        var stage = await StagePng(client);
        using var response = await PutPng(client, stage.Id);
        Assert.Equal(withdrawPolicy ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = Db(); await AssertUnchangedRejectedIntent(db, stage); Assert.Empty(Directory.GetFiles(root));
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task A_parallel_upload_cannot_delete_the_exclusive_spool_owned_by_the_scanning_request()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var scanning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scanner = new ScanPeer("stream: OK\0", beforeReply: async () => { scanning.TrySetResult(); await release.Task.WaitAsync(Ct); });
        await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        host.Services.GetRequiredService<SecureFileSettings>().ScanSeconds = 10;
        var stage = await StagePng(client); var first = PutPng(client, stage.Id);
        try {
            await scanning.Task.WaitAsync(Ct);
            using var duplicate = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Single(Directory.GetFiles(root));
            await using var db = Db();
            var pending = await db.FileStages.AsNoTracking().SingleAsync(x => x.Id == stage.Id, Ct);
            Assert.Equal("Pending", pending.State); Assert.Equal(stage.Version, pending.Version);
            Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        } finally { release.TrySetResult(); }
        using var accepted = await first; Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await using (var db = Db()) {
            Assert.Single(await db.StoredFiles.Where(x => x.Id == stage.Id).ToArrayAsync(Ct));
            Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == stage.Id).ToArrayAsync(Ct));
        }
        File.Delete(Assert.Single(Directory.GetFiles(root))); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Reconciliation_that_fences_a_pending_scan_prevents_late_finalization()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var scanner = new ScanPeer("stream: OK\0", beforeReply: async () => {
            await using var reconciler = Db();
            await FileReconciliation.AbandonAsync(reconciler, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct);
        });
        await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        host.Services.GetRequiredService<SecureFileSettings>().ScanSeconds = 10;
        var stage = await StagePng(client);
        using var response = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = Db();
        Assert.Equal("Abandoned", (await db.FileStages.AsNoTracking().SingleAsync(x => x.Id == stage.Id, Ct)).State);
        Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == stage.Id, Ct));
        Assert.Empty(Directory.GetFiles(root)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Cancelled_stream_preserves_pending_intent_without_receipt_and_removes_only_its_unaccepted_spool()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var content = new InterruptedFileContent(Png[..8], cancelled.Token);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/bytes") { Content = content };
        content.Headers.ContentType = new("image/png");
        var response = client.SendAsync(request, cancelled.Token);
        await content.Started.Task.WaitAsync(Ct);
        try { await WaitForOwnedSpoolCount(root, 1); }
        finally { cancelled.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var result = await response; });
        await WaitForOwnedSpoolCount(root, 0);
        await using var db = Db(); await AssertUnchangedRejectedIntent(db, stage);
        Directory.Delete(root, false);
    }

    private async Task WaitForOwnedSpoolCount(string root, int expected)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (Directory.GetFiles(root).Length != expected) await Task.Delay(20, timeout.Token);
    }

    private sealed class InterruptedFileContent(byte[] prefix, CancellationToken cancelled) : HttpContent
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(prefix, cancelled); await stream.FlushAsync(cancelled);
            Started.TrySetResult(); await Task.Delay(Timeout.Infinite, cancelled);
        }
    }

    private async Task AssertUnchangedRejectedIntent(PoseidonDbContext db, FileDto staged)
    {
        var row = await db.FileStages.AsNoTracking().SingleAsync(x => x.Id == staged.Id, Ct);
        Assert.Equal("Pending", row.State); Assert.Equal(staged.Version, row.Version);
        Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == staged.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == staged.Id, Ct));
        Assert.Single(await db.AuditEvents.Where(x => x.EntityType == nameof(FileStage) && x.ResourceId == staged.Id).ToArrayAsync(Ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EntityType == nameof(StoredFile) && x.ResourceId == staged.Id, Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "FileRequest" && x.ResourceId == staged.Id, Ct));
    }
}
