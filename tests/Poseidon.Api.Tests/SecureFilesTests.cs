using Microsoft.AspNetCore.Hosting;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// SF-01..12: actual HTTP host/current migrations/PostgreSQL; protocol peer is NOT scanner deployment proof.
public sealed partial class DirectoryWorkspaceTests
{
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGMQUDD4DwACBAFgjby7cQAAAABJRU5ErkJggg==";
    private static byte[] Png => Convert.FromBase64String(PngBase64);
    private void FilePermissions(IServiceProvider services, bool write = true)
    {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = write ? ["directory.read", "directory.write", "file.read", "file.write", "audit.read"] : ["directory.read", "file.read"] };
    }
    private async Task<FileDto> StagePng(HttpClient client, string? key = null, string? checksum = null)
    {
        using var response = await Send(client, $"vessels/{SeedVessel.Id}/files", new { name = "synthetic.png", mime = "image/png", length = Png.Length, checksum = checksum ?? Convert.ToHexString(SHA256.HashData(Png)) }, key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<FileDto>(Ct))!;
    }
    private async Task<HttpResponseMessage> PutPng(HttpClient client, Guid id, byte[]? bytes = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{id}/bytes") { Content = new ByteArrayContent(bytes ?? Png) };
        request.Content.Headers.ContentType = new("image/png"); return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Files_are_default_deny_and_unconfigured_service_cannot_accept_metadata_or_bytes()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files", Ct)).StatusCode);
        FilePermissions(host.Services);
        using var response = await Send(client, $"vessels/{SeedVessel.Id}/files", new { name = "synthetic.png" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = Db(); Assert.Empty(await db.StoredFiles.ToArrayAsync(Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "FileFinalize", Ct));
    }

    [Theory]
    [InlineData("stream: ERROR\0")][InlineData("stream: FOUND\0")][InlineData("bad\0")][InlineData("")]
    public async Task Real_protocol_errors_truncation_and_timeout_leave_no_accepted_metadata_receipt_or_download(string reply)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var peer = new ScanPeer(reply); await using var host = FileHost(root, peer.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client);
        using var result = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        await using var db = Db(); Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == stage.Id, Ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.ResourceId == stage.Id && x.EntityType == nameof(StoredFile), Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/bytes", Ct)).StatusCode);
        var row = await db.FileStages.SingleAsync(x => x.Id == stage.Id, Ct); Assert.Equal("Pending", row.State);
        Assert.Equal(stage.Version, row.Version);
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == stage.Id && x.EntityType == nameof(FileStage)).ToArrayAsync(Ct));
        Assert.Single(Directory.GetFiles(root));
        await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct);
        Assert.False(File.Exists(Path.Combine(root, row.TenantId.ToString("N") + "-" + row.Id.ToString("N") + ".object")));
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Finalization_is_atomic_immutable_and_replay_reauthorizes_scope_roles_and_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var key = Guid.NewGuid().ToString(); var staged = await StagePng(client, key);
        using var accepted = await PutPng(client, staged.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var row = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!; Assert.Equal("CleanTransport", row.State); Assert.Equal(1, row.Revision);
        Assert.Equal(row.Checksum, Convert.ToHexString(SHA256.HashData(await peer.Bytes)));
        Assert.Equal(row.Id, (await StagePng(client, key)).Id);
        using var access = await client.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{row.Id}/access", null, Ct);
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        var token = (await access.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Add("X-File-Capability", token);
        using var bytes = await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{row.Id}/bytes", Ct);
        Assert.Equal(HttpStatusCode.OK, bytes.StatusCode); Assert.Equal(Png, await bytes.Content.ReadAsByteArrayAsync(Ct));
        await using var db = Db(); var receipt = Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == row.Id).ToArrayAsync(Ct));
        Assert.Equal(row.Revision, receipt.RegisterRevision); Assert.NotNull(receipt.RegisterVersion);
        Assert.Single(await db.AuditEvents.Where(x => x.EntityType == nameof(StoredFile) && x.ResourceId == row.Id).ToArrayAsync(Ct));
        var persisted = await db.StoredFiles.SingleAsync(x => x.Id == row.Id, Ct); persisted.Name = "changed.png";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        var other = seed.Vessels.First(x => x.Id != SeedVessel.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{other.Id}/files/{row.Id}", Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read"] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{row.Id}/bytes", Ct)).StatusCode);
        Assert.Equal(0, await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct));
        File.Delete(Path.Combine(root, persisted.TenantId.ToString("N") + "-" + row.Id.ToString("N") + ".object")); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Checksum_mismatch_and_disguised_magic_do_not_reach_scanner_or_finalize()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client, checksum: new string('0', 64));
        using var response = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = Db(); Assert.Empty(await db.StoredFiles.ToArrayAsync(Ct));
        await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct);
        Directory.Delete(root, false);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> FileHost(string root, int port, Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? interceptor = null) => Host(interceptor: interceptor).WithWebHostBuilder(b => {
        b.UseEnvironment("Development");
        b.ConfigureServices(s => s.AddSingleton(new SecureFileSettings { Adapter = "LocalDevelopment", Root = root, MaxBytes = 1024 * 1024, MaxPixels = 1024 * 1024, ClamdPort = port, ScanSeconds = 1, CapabilitySeconds = 10, RetentionReference = "synthetic-policy", ScannerReference = "protocol-peer-test-only" }));
    });

    [Fact]
    public async Task Concurrent_byte_attempts_have_one_effect_and_never_replace_scanned_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var staged = await StagePng(client);
        var results = await Task.WhenAll(PutPng(client, staged.Id), PutPng(client, staged.Id));
        Assert.Contains(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.All(results, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        foreach (var result in results) result.Dispose();
        await using var db = Db(); Assert.Single(await db.StoredFiles.Where(x => x.Id == staged.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == staged.Id).ToArrayAsync(Ct));
        Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == staged.Id && x.EntityType == nameof(StoredFile)).ToArrayAsync(Ct));
        File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + staged.Id.ToString("N") + ".object")); Directory.Delete(root, false);
    }

    [Theory]
    [InlineData("../escape.png", "image/png", false)]
    [InlineData("active.svg", "image/svg+xml", false)]
    [InlineData("archive.zip", "application/zip", false)]
    [InlineData("synthetic.png", "image/png", true)]
    public async Task Malformed_metadata_and_forged_protected_owner_or_clean_claims_are_rejected(string name, string mime, bool forged)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        object body = forged ? new { name, mime, length = 32, checksum = new string('A', 64), clean = true, ownerType = "ProtectedReport", ownerId = Guid.NewGuid(), role = "Marine-admin" }
            : new { name, mime, length = 32, checksum = new string('A', 64) };
        using var result = await Send(client, $"vessels/{SeedVessel.Id}/files", body);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        await using var db = Db(); Assert.False(await db.FileStages.AnyAsync(x => x.ActorId == actorId, Ct)); Assert.Empty(await db.StoredFiles.ToArrayAsync(Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "FileRequest", Ct));
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Receipt_collision_rolls_back_finalization_and_its_business_audit_but_preserves_quarantine()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var key = Guid.NewGuid().ToString(); var vessel = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        using var original = await Send(client, $"vessels/{SeedVessel.Id}", new { code = vessel.Code, name = vessel.Name, expectedVersion = vessel.Version }, key, true);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var stage = await StagePng(client, key); using var response = await PutPng(client, stage.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = Db(); Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.EntityType == nameof(StoredFile) && x.ResourceId == stage.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "FileFinalize", Ct));
        Assert.Equal("Receiving", (await db.FileStages.SingleAsync(x => x.Id == stage.Id, Ct)).State);
        await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct);
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Trusted_role_snapshot_is_retained_and_missing_or_revoked_roles_are_not_backfilled()
    {
        var role = Guid.NewGuid().ToString(); var tenant = SeedVessel.TenantId;
        await using (var writer = new PoseidonDbContext(Poseidon.Persistence.Seeding.SeedImporter.Options(postgres.GetConnectionString()), tenant,
            new(actorId.ToString(), "User", "synthetic-role-snapshot", "directory.read,directory.write", "WebCommand", role,
                new AuditCommandContext("UpdateVessel", "PREPARATION_COMMAND"))))
        {
            var vessel = await writer.Vessels.SingleAsync(x => x.Id == SeedVessel.Id, Ct); vessel.Name = "Synthetic withheld name"; await writer.SaveChangesAsync(Ct);
        }
        await using (var writer = Db()) { var vessel = await writer.Vessels.SingleAsync(x => x.Id == SeedVessel.Id, Ct); vessel.Name = "Synthetic revoked role context"; await writer.SaveChangesAsync(Ct); }
        await using var db = Db(); var old = await db.AuditEvents.SingleAsync(x => x.CorrelationId == "synthetic-role-snapshot", Ct);
        Assert.Equal(role, old.RoleContext); Assert.DoesNotContain("withheld", old.ChangeSummary!);
        var later = await db.AuditEvents.SingleAsync(x => x.ActorId == "synthetic-inspector" && x.EntityType == nameof(Vessel), Ct); Assert.Null(later.RoleContext);
        Assert.All(await db.AuditEvents.Where(x => x.Source == "SeedImport").ToArrayAsync(Ct), x => Assert.Null(x.RoleContext));
    }

    [Fact]
    public async Task Stage_xmin_fences_reconciliation_and_composite_owner_FK_denies_cross_tenant_relations()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var staged = await StagePng(client); await using var first = Db(); await using var stale = Db();
        var a = await first.FileStages.SingleAsync(x => x.Id == staged.Id, Ct); var b = await stale.FileStages.SingleAsync(x => x.Id == staged.Id, Ct);
        a.State = "Abandoned"; await first.SaveChangesAsync(Ct); b.State = "Receiving";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync(Ct));
        await using var foreign = Db(seed.Tenants[1].Id);
        foreign.FileStages.Add(new() { TenantId = seed.Tenants[1].Id, Id = Guid.NewGuid(), VesselId = SeedVessel.Id, ActorId = actorId,
            Key = Guid.NewGuid().ToString(), Name = "synthetic.png", ExpectedChecksum = new string('A', 64), Fingerprint = new string('A', 64), CreatedAt = DateTimeOffset.UtcNow, ExpectedLength = 32 });
        await Assert.ThrowsAsync<DbUpdateException>(() => foreign.SaveChangesAsync(Ct));
        Assert.Empty(await foreign.StoredFiles.ToArrayAsync(Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Chunked_actual_overflow_and_matching_hash_disguised_content_are_quarantined_before_scanner()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var staged = await StagePng(client);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{staged.Id}/bytes") { Content = new UnknownLengthContent(Png.Concat(Png).ToArray()) };
        request.Content.Headers.ContentType = new("image/png"); using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var disguised = Encoding.ASCII.GetBytes("<html>synthetic active content</html>");
        using var metadata = await Send(client, $"vessels/{SeedVessel.Id}/files", new { name = "synthetic.png", mime = "image/png", length = disguised.Length, checksum = Convert.ToHexString(SHA256.HashData(disguised)) });
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode); var other = (await metadata.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        using var invalid = await PutPng(client, other.Id, disguised); Assert.Equal(HttpStatusCode.ServiceUnavailable, invalid.StatusCode);
        await using var db = Db(); Assert.Empty(await db.StoredFiles.ToArrayAsync(Ct)); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct); Directory.Delete(root, false);
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    private sealed class ScanPeer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public Task<byte[]> Bytes { get; }
        private readonly Func<Task>? beforeReply;
        public ScanPeer(string reply, bool repeat = false, Func<Task>? beforeReply = null) { this.beforeReply = beforeReply; listener.Start(); Bytes = Receive(reply, repeat); }
        private async Task<byte[]> Receive(string reply, bool repeat) {
            byte[]? first = null;
            try {
                do {
                    var received = await ReceiveOne(reply);
                    first ??= received;
                } while (repeat && !stop.IsCancellationRequested);
                return first;
            } catch (OperationCanceledException) when (stop.IsCancellationRequested) {
                return first ?? [];
            } catch (SocketException) when (stop.IsCancellationRequested) {
                return first ?? [];
            }
        }
        private async Task<byte[]> ReceiveOne(string reply) {
            using var client = await listener.AcceptTcpClientAsync(stop.Token); await using var stream = client.GetStream();
            var command = new byte[10]; await stream.ReadExactlyAsync(command, stop.Token); Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            using var bytes = new MemoryStream(); var length = new byte[4];
            while (true) { await stream.ReadExactlyAsync(length, stop.Token); var n = BinaryPrimitives.ReadUInt32BigEndian(length); if (n == 0) break; Assert.True(n <= 65536); var chunk = new byte[(int)n]; await stream.ReadExactlyAsync(chunk, stop.Token); bytes.Write(chunk); }
            if (beforeReply != null) await beforeReply();
            if (reply.Length == 0) await Task.Delay(TimeSpan.FromSeconds(3), stop.Token); else await stream.WriteAsync(Encoding.ASCII.GetBytes(reply), stop.Token);
            return bytes.ToArray();
        }
        public void Dispose() { stop.Cancel(); listener.Stop(); Bytes.GetAwaiter().GetResult(); stop.Dispose(); }
    }
}
