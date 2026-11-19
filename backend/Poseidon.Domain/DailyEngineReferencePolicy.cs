using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-099 / T-OPS2-003 (18.01 Backlog row 100): "defects, WO link" for the engine
// daily log. A link pins one exact owning WorkOrder/Defect revision (id+revision+
// version+hash+source contract). It is a lineage declaration only: it neither
// verifies, closes nor approves the linked record, and it is not DEV-052 acceptance.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DailyEngineReferencePin([property: JsonRequired] string Kind, [property: JsonRequired] Guid Id,
    [property: JsonRequired] long Revision, [property: JsonRequired] uint Version,
    [property: JsonRequired] string Hash, [property: JsonRequired] string SourceVersion)
{
    public override string ToString() => nameof(DailyEngineReferencePin);
}

public sealed record DailyEngineReferenceLink(DailyEngineReferencePin Pin, long Sequence);

public static class DailyEngineReferencePolicy
{
    public const string SourceVersion = "18.01:09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674:100;14.02:3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018:2177-2178:v46.3";
    public static readonly string[] Kinds = ["WorkOrder", "Defect"];
    public static readonly string[] Operations = ["LINK", "UNLINK"];
    // Technical bound for one engine log entry, not an operational limit.
    public const int MaxActiveLinks = 64;

    public static string? OwningSource(string kind) => kind switch {
        "WorkOrder" => WorkOrderPolicy.SourceVersion, "Defect" => DefectPolicy.SourceVersion, _ => null
    };

    public static bool Valid(DailyEngineReferencePin? pin) => pin != null && Kinds.Contains(pin.Kind) && pin.Id != Guid.Empty &&
        pin.Revision > 0 && pin.Version > 0 && pin.Hash is { Length: 64 } && pin.Hash.All(char.IsAsciiHexDigit) &&
        pin.SourceVersion == OwningSource(pin.Kind);

    // Latest event per target wins; an UNLINK removes the target from the active set.
    public static DailyEngineReferenceLink[] Active(IEnumerable<(string Operation, DailyEngineReferencePin Pin, long Sequence)> events) =>
        events.GroupBy(e => (e.Pin.Kind, e.Pin.Id)).Select(g => g.MaxBy(e => e.Sequence))
            .Where(e => e.Operation == "LINK").Select(e => new DailyEngineReferenceLink(e.Pin, e.Sequence))
            .OrderBy(l => l.Sequence).ToArray();

    // Returns null when the transition is allowed, otherwise a fixed result code.
    // Re-linking the same target to a newer exact revision is an explicit repin.
    public static string? Transition(DailyEngineReferenceLink[] active, string operation, DailyEngineReferencePin pin)
    {
        if (!Operations.Contains(operation) || !Valid(pin)) return "invalid_request";
        var current = active.SingleOrDefault(l => l.Pin.Kind == pin.Kind && l.Pin.Id == pin.Id);
        if (operation == "UNLINK") return current != null && current.Pin == pin ? null : "invalid_transition";
        if (current != null && current.Pin == pin) return "invalid_transition";
        if (current != null && current.Pin.Revision > pin.Revision) return "linked_version_conflict";
        if (current == null && active.Length >= MaxActiveLinks) return "invalid_request";
        return null;
    }
}
