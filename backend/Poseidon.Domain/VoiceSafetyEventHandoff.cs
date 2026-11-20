namespace Poseidon.Domain;

// S9 integration DEV-062 x DEV-060 (18.01 Backlog row 63 "confirmed candidate becomes an event DRAFT, never submitted";
// 61.01 Rev3 §2-§5). A human-confirmed voice candidate is handed to the owning safety event module as a NAMED DRAFT
// reported by the confirming human. Only values with an exact, unambiguous counterpart are carried; everything the
// voice draft does not state exactly stays empty so the reporter completes it (POL-DEFAULT-01): no category is guessed,
// no immediate-action YES/NO is inferred from free text, no time zone is assumed, nothing is submitted or triaged.
public static class VoiceSafetyEventHandoff
{
    public const string NotReady = "NOT_READY", Pending = "PENDING_EVENT_DRAFT_INTEGRATION", Created = "EVENT_DRAFT_CREATED";
    public static readonly string[] States = [NotReady, Pending, Created];
    // Closed reasons why a confirmed candidate stays pending (stored on the voice draft).
    public const string ReportPermissionRequired = "SAFETY_EVENT_REPORT_PERMISSION_REQUIRED";
    public static readonly string[] PendingReasons = [ReportPermissionRequired];

    public static string State(string voiceState, Guid? safetyEventId) =>
        voiceState != VoiceEventDraftPolicy.Confirmed ? NotReady : safetyEventId == null ? Pending : Created;

    // Exact category counterparts only (ISM 9.1 hazardous situation / non-conformity have no 1:1 safety category).
    public static string? Category(string? eventType) => eventType switch { "NEAR_MISS" => "NEAR_MISS", "INCIDENT" => "INCIDENT", _ => null };

    public static SafetyEventDraftFields Map(VoiceDraftFields f, DateTimeOffset now)
    {
        // The voice draft holds a ship-local "yyyy-MM-ddTHH:mm" without a zone: only the local date is exact.
        DateOnly? date = VoiceEventDraftPolicy.TryOccurredAt(f.OccurredAt, out var local) ? DateOnly.FromDateTime(local) : null;
        var location = f.Location == null ? null : string.IsNullOrWhiteSpace(f.LocationDetail) ? f.Location : f.Location + ": " + f.LocationDetail.Trim();
        if (location is { Length: > 500 }) location = location[..500];
        var fields = new SafetyEventDraftFields(Category: Category(f.EventType), OccurrencePrecision: date == null ? null : "DATE_ONLY", OccurrenceDate: date,
            LocationText: location, Narrative: string.IsNullOrWhiteSpace(f.Description) ? null : f.Description,
            ImmediateActionText: string.IsNullOrWhiteSpace(f.ImmediateAction) ? null : f.ImmediateAction);
        // A date the safety module would refuse as future is left for the reporter instead of failing the confirmation.
        if (SafetyEventPolicy.InFuture(fields, now)) fields = fields with { OccurrencePrecision = null, OccurrenceDate = null };
        return fields;
    }
}
