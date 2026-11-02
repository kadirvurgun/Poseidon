using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using System.Security.Cryptography;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Configuration is operator-only. No request can choose a host, bucket, credentials or object key.
public sealed class S3ObjectSettings
{
    public string? Endpoint { get; set; }
    public string? Bucket { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? AccessKeyReference { get; set; }
    public string? SecretKeyReference { get; set; }
    public bool OwnedLoopback { get; set; }
    public bool Valid(IHostEnvironment environment)
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            Bucket is not { Length: >= 3 and <= 63 } || Bucket.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-') ||
            Region.Length is < 1 or > 64 || Region.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return false;
        var loopback = IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
        if (OwnedLoopback ? !environment.IsDevelopment() || !loopback || uri.Scheme != "http" : uri.Scheme != "https" || loopback) return false;
        try { return Resolve(AccessKeyReference) != null && Resolve(SecretKeyReference) != null; }
        catch (InvalidOperationException) { return false; }
    }
    private static string? Resolve(string? reference) => OperatorSecretReferences.Resolve(reference, Environment.GetEnvironmentVariable);
    internal string ContextHash() => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{new Uri(Endpoint!).AbsoluteUri}|{Bucket}|{Region}")));
    internal AmazonS3Client Client()
    {
        var access = Resolve(AccessKeyReference); var secret = Resolve(SecretKeyReference);
        if (access == null || secret == null) throw new IOException("file_storage_unavailable");
        Amazon.AWSConfigs.LoggingConfig.LogTo = Amazon.LoggingOptions.None;
        Amazon.AWSConfigs.LoggingConfig.LogResponses = Amazon.ResponseLoggingOption.Never;
        Amazon.AWSConfigs.LoggingConfig.LogMetrics = false;
        // Explicit credentials only: no AWS profile, metadata endpoint or environment credential chain.
        return new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config {
            ServiceURL = Endpoint, AuthenticationRegion = Region, ForcePathStyle = true,
            MaxErrorRetry = 0, Timeout = TimeSpan.FromSeconds(60), LogMetrics = false, LogResponse = false,
            IgnoreConfiguredEndpointUrls = true,
            HttpClientFactory = new DirectHttpClientFactory()
        });
    }
    private sealed class DirectHttpClientFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
    }
}

// Local exclusive spool is quarantine, never a production storage fallback. Metadata cannot be
// finalized until the actual provider version has been read back and SHA256 checked.
public sealed class FileObjects(SecureFileSettings settings, LocalFileObjects local, IHostEnvironment environment)
{
    public bool Owns(FileStage stage) => stage.StorageReference == (settings.Adapter == "S3" ? settings.S3.ContextHash() : null);
    private static string Key(Guid tenant, Guid id)
    {
        if (tenant == Guid.Empty || id == Guid.Empty) throw new IOException("file_storage_unavailable");
        return $"poseidon/{tenant:N}/{id:N}";
    }
    private void Ready() { if (!settings.Ready(environment)) throw new IOException("file_storage_unavailable"); }
    public FileStream Create(Guid tenant, Guid id) { Ready(); return local.Create(tenant, id); }
    public async Task<(string? Version, string? ETag)> CommitAsync(Guid tenant, Guid id, string checksum, long length, string mime, CancellationToken ct)
    {
        Ready(); if (settings.Adapter != "S3") return (null, null);
        try {
            using var client = settings.S3.Client();
            var versioning = await client.GetBucketVersioningAsync(new GetBucketVersioningRequest { BucketName = settings.S3.Bucket }, ct);
            if (versioning.VersioningConfig?.Status != VersionStatus.Enabled) throw new IOException("file_storage_unavailable");
            await using var stream = local.Open(tenant, id);
            var sha = Convert.ToBase64String(Convert.FromHexString(checksum));
            var response = await client.PutObjectAsync(new PutObjectRequest {
                BucketName = settings.S3.Bucket, Key = Key(tenant, id), InputStream = stream,
                AutoCloseStream = false, ContentType = mime, IfNoneMatch = "*", ChecksumSHA256 = sha
            }, ct);
            if (string.IsNullOrEmpty(response.VersionId) || response.VersionId == "null" || response.VersionId.Length > 1024 ||
                string.IsNullOrEmpty(response.ETag) || response.ETag.Length > 256 || response.ChecksumSHA256 != sha)
                throw new IOException("file_storage_unavailable");
            await using var verified = await FetchAsync(client, tenant, id, response.VersionId, response.ETag, checksum, length, ct);
            return (response.VersionId, response.ETag);
        } catch (Exception e) when (e is AmazonServiceException or AmazonClientException or HttpRequestException) {
            throw new IOException("file_storage_unavailable");
        }
    }
    public async Task<FileStream> OpenAsync(StoredFile file, CancellationToken ct)
    {
        Ready();
        // Historical local files are not silently resolved through a different configured backend.
        if (settings.Adapter != "S3") {
            if (file.ProviderVersion != null) throw new IOException("file_storage_unavailable");
            return local.Open(file.TenantId, file.Id);
        }
        if (string.IsNullOrEmpty(file.ProviderVersion) || string.IsNullOrEmpty(file.ProviderETag) || file.ProviderReference != settings.S3.ContextHash()) throw new IOException("file_storage_unavailable");
        try {
            using var client = settings.S3.Client();
            return await FetchAsync(client, file.TenantId, file.Id, file.ProviderVersion, file.ProviderETag, file.Checksum, file.Length, ct);
        } catch (Exception e) when (e is AmazonServiceException or AmazonClientException or HttpRequestException) {
            throw new IOException("file_storage_unavailable");
        }
    }
    private async Task<FileStream> FetchAsync(IAmazonS3 client, Guid tenant, Guid id, string version, string etag, string checksum, long length, CancellationToken ct)
    {
        using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = settings.S3.Bucket, Key = Key(tenant, id), VersionId = version, ChecksumMode = ChecksumMode.ENABLED }, ct);
        if (response.VersionId != version || response.ETag != etag || response.ContentLength != length || length > settings.MaxBytes ||
            response.ChecksumSHA256 != Convert.ToBase64String(Convert.FromHexString(checksum))) throw new IOException("file_bytes_unavailable");
        // A private generated spool name, never the object key/name supplied by a caller.
        var temporary = local.Create(tenant, Guid.NewGuid());
        var path = temporary.Name;
        try {
            var observed = await SecureFilePolicy.CopyAsync(response.ResponseStream, temporary, Math.Min(length, settings.MaxBytes), ct);
            if (observed.Length != length || observed.Checksum != checksum) throw new IOException("file_bytes_unavailable");
            await temporary.FlushAsync(ct); temporary.Position = 0;
            // Reopen with delete-on-close, keeping the same private generated file.
            await temporary.DisposeAsync();
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        } catch { await temporary.DisposeAsync(); File.Delete(path); throw; }
    }
    public async Task RemoveAsync(Guid tenant, Guid id, CancellationToken ct)
    {
        Ready();
        if (settings.Adapter == "S3") {
            try {
                using var client = settings.S3.Client();
                var key = Key(tenant, id);
                var versions = await client.ListVersionsAsync(new ListVersionsRequest { BucketName = settings.S3.Bucket, Prefix = key, MaxKeys = 100 }, ct);
                if (versions.IsTruncated == true || versions.Versions?.Count > 10) throw new IOException("file_reconciliation_required");
                foreach (var version in versions.Versions ?? []) {
                    if (version.Key != key) throw new IOException("file_reconciliation_required");
                    await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = settings.S3.Bucket, Key = key, VersionId = version.VersionId }, ct);
                }
            } catch (Exception e) when (e is AmazonServiceException or AmazonClientException or HttpRequestException) { throw new IOException("file_storage_unavailable"); }
        }
        local.Remove(tenant, id);
    }
}
