using System.Security.Cryptography;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed class QualifiedInputRuntime(FileObjects objects, CrewAssignmentPolicy assignments, CrewPositionPolicy positions, IConfiguration configuration) : IQualifiedInputRuntime
{
    private CrewFilePolicy Files => configuration.GetSection("CrewFilePolicy").Get<CrewFilePolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
    public string? DocumentPolicyVersion => Files.Available && Files.Kinds.Contains("CertificateDocument") ? Files.Version : null;
    public string? DocumentPurpose => Files.Available && Files.Kinds.Contains("CertificateDocument") ? Files.Purpose : null;
    public string PolicyFingerprint => assignments.Fingerprint() + ":" + positions.Fingerprint();
    public bool AssignmentPolicyCurrent(CrewAssignment a, CrewPosition p, DateTimeOffset now) =>
        assignments.Configured && assignments.ReferenceDate(now) is { } date && positions.Allows(p, date) &&
        a.AssignmentPolicyVersion == assignments.Version && a.AssignmentPolicyFingerprint == assignments.Fingerprint() &&
        a.PositionPolicyVersion == positions.Version && a.PositionPolicyFingerprint == positions.Fingerprint() &&
        assignments.EffectiveStatuses.Contains(a.Status) && assignments.Provenances.Contains(a.Provenance) &&
        a.SourceObservedAt != null && a.SourceObservedAt <= now && a.From <= date && (a.To == null || date < a.To);
    public async Task<bool> FileIntact(StoredFile file, CancellationToken ct) {
        try {
            await using var bytes = await objects.OpenAsync(file, ct);
            return bytes.Length == file.Length && Convert.ToHexString(await SHA256.HashDataAsync(bytes, ct)) == file.Checksum;
        } catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or CryptographicException) { return false; }
    }
}
