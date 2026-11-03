using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Poseidon.Domain;

namespace Poseidon.Api;

public sealed class SecureFileSettings
{
    public string? Adapter { get; set; }
    public string? Root { get; set; }
    public long MaxBytes { get; set; }
    public long MaxPixels { get; set; }
    public int ScanSeconds { get; set; }
    public int ClamdPort { get; set; }
    public int CapabilitySeconds { get; set; }
    public string? RetentionReference { get; set; }
    public string? ScannerReference { get; set; }
    public S3ObjectSettings S3 { get; set; } = new();
    public string[] AllowedTypes { get; set; } = ["image/png"];
    public bool Ready(IHostEnvironment environment) => (Adapter == "S3" && S3.Valid(environment) || environment.IsDevelopment() && Adapter == "LocalDevelopment") &&
        Root != null && Path.IsPathFullyQualified(Root) && !Path.GetFullPath(Root).Equals(Path.GetFullPath(environment.ContentRootPath), StringComparison.OrdinalIgnoreCase) && !Path.GetFullPath(Root).StartsWith(Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && MaxBytes is > 0 and <= 5242880 && MaxPixels > 0 &&
        ScanSeconds is > 0 and <= 300 && ClamdPort is > 0 and <= 65535 && CapabilitySeconds is > 0 and <= 300 &&
        Reference(RetentionReference) && Reference(ScannerReference) && AllowedTypes.Length > 0 && AllowedTypes.All(DocumentBinaryPolicy.Supported);
    private static bool Reference(string? value) => value is { Length: > 0 and <= 64 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

// Only an explicitly configured development adapter. Production provider/credentials/retention are an open gate.
public sealed class FileObjectBusyException() : IOException("file_attempt_conflict");

public sealed class LocalFileObjects(SecureFileSettings settings)
{
    private string PathFor(Guid tenant, Guid id)
    {
        if (tenant == Guid.Empty || id == Guid.Empty || settings.Root == null) throw new IOException("file_storage_unavailable");
        var root = Path.GetFullPath(settings.Root);
        // Operator creates the dedicated root, outside webroot/repository, service-account-only writable.
        for (var dir = new DirectoryInfo(root); dir != null; dir = dir.Parent)
            if (!dir.Exists || (dir.Attributes & FileAttributes.ReparsePoint) != 0 || File.Exists(Path.Combine(dir.FullName, ".git")) || Directory.Exists(Path.Combine(dir.FullName, ".git"))) throw new IOException("file_storage_unavailable");
        var path = Path.Combine(root, tenant.ToString("N") + "-" + id.ToString("N") + ".object");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("file_storage_unavailable");
        return path;
    }
    public FileStream Create(Guid tenant, Guid id)
    {
        var path = PathFor(tenant, id);
        try { return new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous); }
        // Only the OS create-new collision codes are a competing attempt. Permission, missing
        // directories and unrelated I/O failures must not be reported as an ordinary conflict.
        catch (IOException e) when (OperatingSystem.IsWindows() ? (e.HResult & 0xffff) is 80 or 183 : (e.HResult & 0xffff) == 17)
        { throw new FileObjectBusyException(); }
    }
    public FileStream Open(Guid tenant, Guid id) => new(PathFor(tenant, id), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
    public void Remove(Guid tenant, Guid id) => File.Delete(PathFor(tenant, id));
}

public sealed class ClamdScanner(SecureFileSettings settings)
{
    // clamd INSTREAM: loopback only; no URL input, shell execution, remote endpoint or caller clean flag.
    public async Task ScanAsync(Stream bytes, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ScanSeconds));
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, settings.ClamdPort, timeout.Token);
        await using var network = client.GetStream();
        await network.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), timeout.Token);
        var buffer = new byte[65536]; var length = new byte[4]; int count;
        while ((count = await bytes.ReadAsync(buffer, timeout.Token)) > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)count);
            await network.WriteAsync(length, timeout.Token); await network.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
        }
        await network.WriteAsync(new byte[4], timeout.Token);
        // Exactly this success frame; FOUND, ERROR, unknown, truncated and oversized replies fail closed.
        var reply = new byte[11]; await network.ReadExactlyAsync(reply, timeout.Token);
        if (!reply.SequenceEqual(Encoding.ASCII.GetBytes("stream: OK\0"))) throw new InvalidDataException("file_scan_failed");
    }
}

// Data Protection signs/encrypts the complete binding. Membership/capabilities are still re-resolved on access.
public sealed class FileCapabilities(Microsoft.AspNetCore.DataProtection.IDataProtectionProvider provider)
{
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtector protector = provider.CreateProtector("poseidon.files.download.v1");
    public string Issue(Guid tenant, Guid actor, Guid vessel, Guid id, long revision, DateTimeOffset expires) =>
        Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Protect(protector, $"{tenant:D}|{actor:D}|{vessel:D}|{id:D}|{revision}|{expires.ToUnixTimeSeconds()}");
    public bool Valid(string? token, Guid tenant, Guid actor, Guid vessel, Guid id, long revision, DateTimeOffset now)
    {
        if (token is not { Length: > 0 and <= 2048 }) return false;
        try {
            var fields = Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Unprotect(protector, token).Split('|');
            return fields.Length == 6 && fields[0] == tenant.ToString("D") && fields[1] == actor.ToString("D") &&
                fields[2] == vessel.ToString("D") && fields[3] == id.ToString("D") && fields[4] == revision.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                long.TryParse(fields[5], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var expiry) && expiry > now.ToUnixTimeSeconds();
        } catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException) { return false; }
    }
}
