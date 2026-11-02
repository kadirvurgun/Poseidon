namespace Poseidon.Domain;

// FR46-crew-roster-watch_group; active15.01 paragraphs228–245.
// Only a draft watch declaration. No identity, qualification, appointment or readiness decision.
public static class OfflineWatchCapture
{
    public const string Operation = "crew.draft.watch-group.v1";
    public const string Source = "3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018";
    public static bool ValidValue(string? value, IReadOnlyCollection<string> values) => value == null ||
        value.Length is > 0 and <= 64 && !value.Any(char.IsControl) && values.Contains(value, StringComparer.Ordinal);
    public static bool ValidCaptureTime(DateTimeOffset captured, int offsetMinutes, DateTimeOffset enrolled,
        DateTimeOffset received, DateTimeOffset expires) => offsetMinutes is >= -840 and <= 840 &&
        captured.Offset == TimeSpan.Zero && captured >= enrolled && captured <= received && captured < expires;
}
