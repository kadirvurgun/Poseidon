using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task S3_context_changed_while_Receiving_claim_is_awaited_cannot_redirect_or_commit_provider_bytes()
    {
        await using var lab = await OwnedS3Lab.CreateAsync(Ct); using var scanner = new ScanPeer("stream: OK\0");
        var config = lab.FileSettings(scanner.Port); var pause = new ReceivingClaimPause();
        await using var host = Host(interceptor: pause).WithWebHostBuilder(b => {
            b.UseEnvironment("Development"); b.ConfigureServices(s => s.AddSingleton(config));
        });
        using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client); var pending = PutPng(client, stage.Id); var bucket = config.S3.Bucket;
        try {
            await pause.Entered.Task.WaitAsync(Ct);
            config.S3.Bucket = "different-synthetic-bucket";
        } finally { pause.Release.TrySetResult(); }
        using var response = await pending; Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = Db();
        Assert.Equal("Receiving", (await db.FileStages.AsNoTracking().SingleAsync(x => x.Id == stage.Id, Ct)).State);
        Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == stage.Id, Ct));
        config.S3.Bucket = bucket;
        using var storage = lab.Settings.Client();
        var versions = await storage.ListVersionsAsync(new ListVersionsRequest { BucketName = bucket, Prefix = $"poseidon/{SeedVessel.TenantId:N}/{stage.Id:N}" }, Ct);
        Assert.Empty(versions.Versions ?? []);
    }

    // S1-FILES-REJECTION-01: a new provider context cannot consume or freeze an old intent.
    [Fact]
    public async Task Changed_S3_context_rejects_before_claiming_pending_intent_or_touching_provider_bytes()
    {
        await using var lab = await OwnedS3Lab.CreateAsync(Ct); var config = lab.FileSettings(1);
        await using var host = Host().WithWebHostBuilder(b => { b.UseEnvironment("Development"); b.ConfigureServices(s => s.AddSingleton(config)); });
        using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var stage = await StagePng(client);
        var originalBucket = config.S3.Bucket; config.S3.Bucket = "different-synthetic-bucket";
        using var response = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = Db(); await AssertUnchangedRejectedIntent(db, stage);
        Assert.Empty(Directory.GetFiles(config.Root!));
        config.S3.Bucket = originalBucket;
        using var storage = lab.Settings.Client();
        var versions = await storage.ListVersionsAsync(new ListVersionsRequest { BucketName = originalBucket, Prefix = $"poseidon/{SeedVessel.TenantId:N}/{stage.Id:N}" }, Ct);
        Assert.Empty(versions.Versions ?? []);
    }

    // FSC-S3-01: real PostgreSQL/API/S3/clamd with actual versioned document bytes.
    [Fact]
    public async Task Real_S3_document_upload_reads_exact_provider_version_and_reauthorizes_every_owner_access()
    {
        await using var lab = await OwnedS3Lab.CreateAsync(Ct);
        Assert.True(int.TryParse(Environment.GetEnvironmentVariable("POSEIDON_GATE_CLAMD_PORT"), out var scanner) && scanner > 0, "Real clamd required.");
        var config = lab.FileSettings(scanner);
        await using var host = Host().WithWebHostBuilder(b => { b.UseEnvironment("Development"); b.ConfigureServices(s => s.AddSingleton(config)); });
        using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var prefix = $"vessels/{SeedVessel.Id}/documents/{SeedVessel.Id}/versions/1/files";
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nFICTIONAL unverified document binary\n%%EOF\n");
        var key = Guid.NewGuid().ToString();
        using var staged = await Send(client, prefix, new { name = "synthetic.pdf", mime = "application/pdf", length = bytes.Length, checksum = Convert.ToHexString(SHA256.HashData(bytes)) }, key);
        Assert.Equal(HttpStatusCode.OK, staged.StatusCode); var row = (await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/{prefix}/{row.Id}/bytes") { Content = new ByteArrayContent(bytes) };
        put.Content.Headers.ContentType = new("application/pdf");
        using var accepted = await client.SendAsync(put, Ct); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await using var db = Db(); var file = await db.StoredFiles.SingleAsync(x => x.Id == row.Id, Ct);
        Assert.NotNull(file.ProviderVersion); Assert.NotEqual("null", file.ProviderVersion); Assert.NotNull(file.ProviderETag);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), file.Checksum); Assert.NotEqual(file.Checksum, file.ProviderETag);
        using var storage = lab.Settings.Client();
        using var actual = await storage.GetObjectAsync(new GetObjectRequest { BucketName = lab.Settings.Bucket, Key = $"poseidon/{file.TenantId:N}/{file.Id:N}", VersionId = file.ProviderVersion }, Ct);
        using var copied = new MemoryStream(); await actual.ResponseStream.CopyToAsync(copied, Ct); Assert.Equal(bytes, copied.ToArray());
        using var issued = await client.PostAsync($"/api/v1/{prefix}/{row.Id}/access", null, Ct);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var token = (await issued.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Add("X-File-Capability", token);
        using var download = await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{row.Id}/bytes", Ct)).StatusCode);
        // Replacement at the same key creates another provider version; current metadata still selects the scanned original.
        await storage.PutObjectAsync(new PutObjectRequest { BucketName = lab.Settings.Bucket, Key = $"poseidon/{file.TenantId:N}/{file.Id:N}", ContentBody = "unscanned replacement" }, Ct);
        using var exact = await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct); Assert.Equal(bytes, await exact.Content.ReadAsByteArrayAsync(Ct));
        var capabilities = host.Services.GetRequiredService<FileCapabilities>();
        client.DefaultRequestHeaders.Remove("X-File-Capability");
        client.DefaultRequestHeaders.Add("X-File-Capability", capabilities.Issue(file.TenantId, actorId, file.VesselId, file.Id, file.Revision, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-File-Capability");
        client.DefaultRequestHeaders.Add("X-File-Capability", capabilities.Issue(file.TenantId, Guid.NewGuid(), file.VesselId, file.Id, file.Revision, DateTimeOffset.UtcNow.AddSeconds(30)));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-File-Capability"); client.DefaultRequestHeaders.Add("X-File-Capability", token);
        var federation = host.Services.GetRequiredService<FederationSettings>(); var original = federation.Memberships[0];
        federation.Memberships[0] = original with { TenantId = seed.Tenants[1].Id };
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct)).StatusCode);
        federation.Memberships[0] = original with { Permissions = ["directory.read"] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{prefix}/{row.Id}/bytes", Ct)).StatusCode);
    }

    // FSC-S3-02: real provider conditional write and fail-closed configuration, no protocol fake.
    [Fact]
    public async Task Real_S3_collision_and_missing_versioning_cannot_publish_a_file()
    {
        await using var lab = await OwnedS3Lab.CreateAsync(Ct); var settings = lab.FileSettings(1);
        var local = new LocalFileObjects(settings); var objects = new FileObjects(settings, local, lab.Environment);
        var tenant = Guid.NewGuid(); var id = Guid.NewGuid(); var bytes = Png;
        await using (var spool = objects.Create(tenant, id)) await spool.WriteAsync(bytes, Ct);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var committed = await objects.CommitAsync(tenant, id, hash, bytes.Length, "image/png", Ct); Assert.NotNull(committed.Version);
        await Assert.ThrowsAsync<IOException>(() => objects.CommitAsync(tenant, id, hash, bytes.Length, "image/png", Ct));
        using var storage = lab.Settings.Client();
        await storage.PutBucketVersioningAsync(new PutBucketVersioningRequest { BucketName = lab.Settings.Bucket, VersioningConfig = new S3BucketVersioningConfig { Status = Amazon.S3.VersionStatus.Suspended } }, Ct);
        var second = Guid.NewGuid(); await using (var spool = objects.Create(tenant, second)) await spool.WriteAsync(bytes, Ct);
        await Assert.ThrowsAsync<IOException>(() => objects.CommitAsync(tenant, second, hash, bytes.Length, "image/png", Ct));
        await objects.RemoveAsync(tenant, id, Ct); local.Remove(tenant, second);
    }

    // FSC-S3-03: actual provider bytes precede a DB receipt collision; reconciliation removes
    // an unreferenced version only after the audited xmin fence has made finalization impossible.
    [Fact]
    public async Task Real_S3_receipt_rollback_retains_quarantine_then_reconciliation_removes_orphan_versions()
    {
        await using var lab = await OwnedS3Lab.CreateAsync(Ct);
        Assert.True(int.TryParse(Environment.GetEnvironmentVariable("POSEIDON_GATE_CLAMD_PORT"), out var scanner) && scanner > 0, "Real clamd required.");
        var config = lab.FileSettings(scanner);
        await using var host = Host().WithWebHostBuilder(b => { b.UseEnvironment("Development"); b.ConfigureServices(s => s.AddSingleton(config)); });
        using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var key = Guid.NewGuid().ToString(); var vessel = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"vessels/{SeedVessel.Id}", new { code = vessel.Code, name = vessel.Name, expectedVersion = vessel.Version }, key, true)).StatusCode);
        var stage = await StagePng(client, key);
        using var uploaded = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.ServiceUnavailable, uploaded.StatusCode);
        await using var db = Db();
        Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.ResourceId == stage.Id && x.EntityType == "StoredFile", Ct));
        Assert.Equal("Receiving", (await db.FileStages.SingleAsync(x => x.Id == stage.Id, Ct)).State);
        using var storage = lab.Settings.Client(); var objectKey = $"poseidon/{SeedVessel.TenantId:N}/{stage.Id:N}";
        var before = await storage.ListVersionsAsync(new ListVersionsRequest { BucketName = lab.Settings.Bucket, Prefix = objectKey }, Ct);
        Assert.Single(before.Versions!);
        var objects = host.Services.GetRequiredService<FileObjects>();
        await FileReconciliation.AbandonAsync(db, objects, DateTimeOffset.UtcNow, 100, Ct);
        var after = await storage.ListVersionsAsync(new ListVersionsRequest { BucketName = lab.Settings.Bucket, Prefix = objectKey }, Ct);
        Assert.Empty(after.Versions ?? []);
        Assert.Equal("Abandoned", (await db.FileStages.SingleAsync(x => x.Id == stage.Id, Ct)).State);
        Assert.Equal(HttpStatusCode.Conflict, (await PutPng(client, stage.Id)).StatusCode);
    }
}
