namespace Poseidon.Persistence;

// Optional runtime seam. Migration/import contexts have no storage or policy authority.
public interface IQualifiedInputRuntime
{
    Task<bool> FileIntact(StoredFile file, CancellationToken ct);
    Task<bool> AssignmentPolicyCurrent(PoseidonDbContext db, CrewAssignment assignment, CrewPosition position, DateTimeOffset now, CancellationToken ct);
    string? DocumentPolicyVersion { get; }
    string? DocumentPurpose { get; }
    Task<string> PolicyFingerprint(PoseidonDbContext db, Guid vessel, DateTimeOffset now, CancellationToken ct);
}
