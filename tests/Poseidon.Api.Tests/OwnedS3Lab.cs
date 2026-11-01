using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual S3 provider only. Missing owned driver input is a failing gate, never a skipped test.
internal sealed class OwnedS3Lab : IAsyncDisposable
{
    public S3ObjectSettings Settings { get; }
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "poseidon-s3-spool-" + Guid.NewGuid().ToString("N"));
    public IHostEnvironment Environment { get; } = new LabEnvironment();
    private OwnedS3Lab(S3ObjectSettings settings) { Settings = settings; Directory.CreateDirectory(Root); }
    public static async Task<OwnedS3Lab> CreateAsync(CancellationToken ct)
    {
        Assert.True(Guid.TryParseExact(System.Environment.GetEnvironmentVariable("POSEIDON_GATE_S3_OWNER"), "N", out _), "Owned real S3 driver required.");
        var settings = new S3ObjectSettings { Endpoint = System.Environment.GetEnvironmentVariable("POSEIDON_GATE_S3_ENDPOINT"),
            Bucket = "poseidon-owned-" + Guid.NewGuid().ToString("N"), OwnedLoopback = true,
            AccessKeyReference = "POSEIDON_GATE_S3_ACCESS", SecretKeyReference = "POSEIDON_GATE_S3_SECRET" };
        var result = new OwnedS3Lab(settings);
        if (!settings.Valid(result.Environment)) { Directory.Delete(result.Root, false); throw new IOException("owned_s3_configuration_required"); }
        try {
            using var client = settings.Client();
            await client.PutBucketAsync(new PutBucketRequest { BucketName = settings.Bucket }, ct);
            await client.PutBucketVersioningAsync(new PutBucketVersioningRequest { BucketName = settings.Bucket, VersioningConfig = new S3BucketVersioningConfig { Status = VersionStatus.Enabled } }, ct);
            return result;
        } catch { Directory.Delete(result.Root, false); throw new IOException("owned_s3_initialization_failed"); }
    }
    public SecureFileSettings FileSettings(int scanner) => new() { Adapter = "S3", Root = Root, S3 = Settings,
        AllowedTypes = ["image/png", "image/jpeg", "application/pdf"], MaxBytes = 1024 * 1024, MaxPixels = 1024 * 1024,
        ScanSeconds = 10, ClamdPort = scanner, CapabilitySeconds = 10, RetentionReference = "synthetic-lab-policy", ScannerReference = "owned-loopback-clamd" };
    public async ValueTask DisposeAsync()
    {
        try {
            using var client = Settings.Client();
            var versions = await client.ListVersionsAsync(new ListVersionsRequest { BucketName = Settings.Bucket, MaxKeys = 1000 });
            if (versions.IsTruncated == true) throw new IOException("owned_bucket_cleanup_limit");
            foreach (var version in versions.Versions ?? []) await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Settings.Bucket, Key = version.Key, VersionId = version.VersionId });
            await client.DeleteBucketAsync(new DeleteBucketRequest { BucketName = Settings.Bucket });
        } catch { /* Driver removes the exact owned ephemeral container; never expose SDK errors. */ }
        foreach (var file in Directory.EnumerateFiles(Root, "*.object")) File.Delete(file);
        Directory.Delete(Root, false);
    }
    private sealed class LabEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OwnedS3Lab";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
