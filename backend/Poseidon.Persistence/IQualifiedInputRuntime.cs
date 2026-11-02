namespace Poseidon.Persistence;

// Optional runtime seam. Migration/import contexts have no storage or policy authority.
public interface IQualifiedInputRuntime
{
    Task<bool> FileIntact(StoredFile file, CancellationToken ct);
    bool AssignmentPolicyCurrent(CrewAssignment assignment, CrewPosition position, DateTimeOffset now);
    string? DocumentPolicyVersion { get; }
    string? DocumentPurpose { get; }
    string PolicyFingerprint { get; }
}
