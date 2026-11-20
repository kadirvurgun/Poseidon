using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Poseidon.Api;

// DEV-062: speech-to-text is a LOCAL-ONLY capability. No cloud speech service and no browser Web
// Speech API (commonly cloud-backed) is used. Configuration names an in-process engine; there is no
// endpoint, host or secret field at all. Without a configured AND registered local engine the
// transcription endpoint answers 503 speech_capability_unavailable and the user types or pastes
// the transcript instead. Raw audio is never persisted (RetainAudio must stay false; retention of
// audio would need an approved privacy/retention policy that does not exist yet).
public sealed class SpeechTranscriptionSettings
{
    public const string LocalEngine = "LocalEngine";
    public bool Enabled { get; set; }
    public string? Provider { get; set; }
    public string? EngineVersion { get; set; }
    public string? ModelVersion { get; set; }
    public string? PrivacyReference { get; set; }
    public bool RetainAudio { get; set; }
    public int MaxBytes { get; set; } = 2_097_152;
    public int TimeoutSeconds { get; set; } = 30;
    private static bool Version(string? value) => value is { Length: > 0 and <= 128 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or ':' or '/' or '@');
    internal bool Configured => Enabled && Provider == LocalEngine && Version(EngineVersion) && Version(ModelVersion) && PrivacyReference is { Length: > 0 and <= 200 } &&
        !RetainAudio && MaxBytes is >= 1024 and <= 10_485_760 && TimeoutSeconds is >= 1 and <= 120;
}

public sealed record SpeechSegment(int Start, int End, decimal Confidence);
public sealed record SpeechTranscript(string Text, string EngineVersion, string ModelVersion, decimal? MeanConfidence, SpeechSegment[] Segments);

public sealed class SpeechCapabilityUnavailableException() : Exception("speech_capability_unavailable");

// An actual local engine (e.g. an in-process offline model) implements this. The repository ships
// no engine; the default registration is the explicit Unavailable implementation below.
public interface ISpeechTranscriptionProvider
{
    bool Available { get; }
    string EngineVersion { get; }
    Task<SpeechTranscript> TranscribeAsync(ReadOnlyMemory<byte> audio, string mediaType, CancellationToken ct);
}

public sealed class UnavailableSpeechTranscriptionProvider : ISpeechTranscriptionProvider
{
    public bool Available => false;
    public string EngineVersion => "none";
    public Task<SpeechTranscript> TranscribeAsync(ReadOnlyMemory<byte> audio, string mediaType, CancellationToken ct) => throw new SpeechCapabilityUnavailableException();
}

// Binds a server-produced transcript to tenant/actor/vessel, its SHA-256 and the engine/model
// versions, so a draft created from it records speech provenance the client cannot invent.
public sealed class SpeechAttestations(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("poseidon.voice.transcript.v1");
    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public string Issue(Guid tenant, Guid actor, Guid vessel, string transcriptSha256, string engine, string model, decimal? confidence, DateTimeOffset expires) =>
        protector.Protect(string.Join('|', tenant.ToString("D"), actor.ToString("D"), vessel.ToString("D"), transcriptSha256, engine, model,
            confidence?.ToString(CultureInfo.InvariantCulture) ?? "", expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
    public (string Engine, string Model, decimal? Confidence)? Read(string? token, Guid tenant, Guid actor, Guid vessel, string transcriptSha256, DateTimeOffset now)
    {
        if (token is not { Length: > 0 and <= 4096 }) return null;
        try
        {
            var f = protector.Unprotect(token).Split('|');
            if (f.Length != 8 || f[0] != tenant.ToString("D") || f[1] != actor.ToString("D") || f[2] != vessel.ToString("D") || f[3] != transcriptSha256 ||
                !long.TryParse(f[7], NumberStyles.None, CultureInfo.InvariantCulture, out var expiry) || expiry <= now.ToUnixTimeSeconds()) return null;
            decimal? confidence = f[6].Length == 0 ? null : decimal.Parse(f[6], NumberStyles.Number, CultureInfo.InvariantCulture);
            return (f[4], f[5], confidence);
        }
        catch (Exception e) when (e is CryptographicException or FormatException or OverflowException) { return null; }
    }
}
