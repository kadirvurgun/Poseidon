namespace Poseidon.Domain;

// DEV-034 / T-S4-002 (18.01 Backlog row 35): server-authoritative reconciliation of
// queued offline operations after reconnect. This is a pure classification of the
// durable per-domain receipts; it never executes, re-signs or replays a command and
// never infers success from absence. NotReceived means "still pending on the device".
public enum SyncOperationState
{
    // No durable server receipt exists for this actor/key. The device must keep the
    // operation pending and replay the exact frozen body through its typed adapter.
    NotReceived,
    // The same key and same payload digest already have an immutable server outcome.
    Recorded,
    // The key is known for the same resource but a DIFFERENT payload: equivalent to
    // a 409 command reuse. The device must not overwrite or re-key silently.
    PayloadMismatch,
    // The key is bound to another domain/resource/vessel. No outcome detail is disclosed.
    KeyReused,
    // Current ordinary rights, policy or enrollment do not allow receipt disclosure.
    Unavailable,
}

public sealed record SyncOperationReference(Guid OperationId, string Domain, Guid ResourceId, string PayloadDigest);

public sealed record SyncStoredOutcome(Guid OperationId, string Domain, Guid ResourceId, Guid VesselId, string Digest,
    string Result, bool EnrollmentCurrent);

public static class SyncOperationReconciliation
{
    public const int MaxOperations = 64;
    public const string CrewWatchGroup = "CrewWatchGroup";
    public const string TugPlanWindow = "TugPlanWindow";
    public const string DailyNoon = "DailyNoon";
    public const string DailyDeck = "DailyDeck";
    public const string DailyEngine = "DailyEngine";
    public static IReadOnlyList<string> Domains { get; } = [CrewWatchGroup, TugPlanWindow, DailyNoon, DailyDeck, DailyEngine];

    // Daily offline kinds are stored as Noon/Deck/Engine; the sync domain is explicit.
    public static string? DailyKind(string domain) => domain switch
    {
        DailyNoon => "Noon", DailyDeck => "Deck", DailyEngine => "Engine", _ => null,
    };

    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    // Returns a fixed technical error code, or null when the batch is admissible.
    public static string? Validate(IReadOnlyList<SyncOperationReference>? operations)
    {
        if (operations is null || operations.Count is 0 or > MaxOperations) return "invalid_request";
        var seen = new HashSet<Guid>();
        foreach (var o in operations)
        {
            if (o is null || o.OperationId == Guid.Empty || o.ResourceId == Guid.Empty || !Domains.Contains(o.Domain) ||
                !ValidDigest(o.PayloadDigest) || !seen.Add(o.OperationId))
                return "invalid_request";
        }
        return null;
    }

    public static SyncOperationState Classify(SyncOperationReference request, Guid vessel, bool currentRights, SyncStoredOutcome? stored)
    {
        // Current rights precede even disclosure that a receipt exists.
        if (!currentRights) return SyncOperationState.Unavailable;
        if (stored is null) return SyncOperationState.NotReceived;
        if (stored.OperationId != request.OperationId || stored.Domain != request.Domain ||
            stored.ResourceId != request.ResourceId || stored.VesselId != vessel)
            return SyncOperationState.KeyReused;
        if (!stored.EnrollmentCurrent) return SyncOperationState.Unavailable;
        return string.Equals(stored.Digest, request.PayloadDigest, StringComparison.OrdinalIgnoreCase)
            ? SyncOperationState.Recorded : SyncOperationState.PayloadMismatch;
    }
}
