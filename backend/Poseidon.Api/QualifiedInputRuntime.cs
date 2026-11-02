using System.Security.Cryptography;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed class QualifiedInputRuntime(FileObjects objects, CrewAssignmentPolicy assignments, CrewPositionPolicy positions, FederationSettings settings, IConfiguration configuration) : IQualifiedInputRuntime
{
    private CrewFilePolicy Files => configuration.GetSection("CrewFilePolicy").Get<CrewFilePolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
    public string? DocumentPolicyVersion => Files.Available && Files.Kinds.Contains("CertificateDocument") ? Files.Version : null;
    public string? DocumentPurpose => Files.Available && Files.Kinds.Contains("CertificateDocument") ? Files.Purpose : null;
    public async Task<string> PolicyFingerprint(PoseidonDbContext db, Guid vessel, DateTimeOffset now, CancellationToken ct) =>
        assignments.Fingerprint() + ":" + (await CrewCatalogEndpoints.Resolve(db, settings, positions, vessel, now, ct)).Fingerprint();
    public async Task<bool> AssignmentPolicyCurrent(PoseidonDbContext db, CrewAssignment a, CrewPosition p, DateTimeOffset now, CancellationToken ct) {
        var current = await CrewCatalogEndpoints.Resolve(db, settings, positions, a.VesselId, now, ct);
        return assignments.Configured && assignments.ReferenceDate(now) is { } date && current.Allows(p, date) &&
        a.AssignmentPolicyVersion == assignments.Version && a.AssignmentPolicyFingerprint == assignments.Fingerprint() &&
        a.PositionPolicyVersion == current.Version && a.PositionPolicyFingerprint == current.Fingerprint() &&
        assignments.EffectiveStatuses.Contains(a.Status) && assignments.Provenances.Contains(a.Provenance) &&
        a.SourceObservedAt != null && a.SourceObservedAt <= now && a.From <= date && (a.To == null || date < a.To);
    }
    public async Task<bool> FileIntact(StoredFile file, CancellationToken ct) {
        try {
            await using var bytes = await objects.OpenAsync(file, ct);
            return bytes.Length == file.Length && Convert.ToHexString(await SHA256.HashDataAsync(bytes, ct)) == file.Checksum;
        } catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or CryptographicException) { return false; }
    }
}
