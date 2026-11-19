namespace Poseidon.Domain;

// Explicit operator routing for declared maintenance exceptions. Configuration
// does not verify a source, appoint a safety role or deliver an external message.
public sealed record NotificationRule(Guid TenantId, Guid VesselId, string Key, string Version, string SourceReference,
    string TriggerState, string Severity, bool Mandatory, int? EscalateAfterSeconds, Guid? EscalationRoleId)
{
    public NotificationRule() : this(Guid.Empty, Guid.Empty, "", "", "", "", "", false, null, null) { }
}

public static class NotificationPolicy
{
    public static bool Valid(NotificationRule rule) => rule.TenantId != Guid.Empty && rule.VesselId != Guid.Empty &&
        Identifier(rule.Key, 64) && Identifier(rule.Version, 80) && rule.SourceReference is { Length: > 0 and <= 500 } &&
        rule.SourceReference.Trim() == rule.SourceReference && !rule.SourceReference.Any(char.IsControl) &&
        rule.TriggerState is "Due" or "ReviewRequired" && rule.Severity is "Info" or "Warning" or "Critical" &&
        (rule.EscalateAfterSeconds == null && rule.EscalationRoleId == null ||
            rule.EscalateAfterSeconds is > 0 and <= 2592000 && rule.EscalationRoleId is { } role && role != Guid.Empty);
    private static bool Identifier(string? value, int limit) => value is { Length: > 0 } && value.Length <= limit &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    public static bool Trigger(NotificationRule rule, string state, bool ownerEligible) =>
        Valid(rule) && ownerEligible && rule.TriggerState == state;
    public static bool IsMandatory(string severity, bool mandatory) => severity == "Critical" || mandatory;
    public static bool Visible(string severity, bool mandatory, bool mutedAdvisory) =>
        !mutedAdvisory || IsMandatory(severity, mandatory);
    public static bool EscalationDue(string state, DateTimeOffset? escalateAt, DateTimeOffset now) =>
        state == "Open" && escalateAt != null && escalateAt <= now;
}

// 58.01 PL-035/PL-036 quiet hours. A personal preference only defers OPTIONAL
// (non-mandatory, non-Critical) in-app visibility; it never delays a mandatory
// route, extends a legal/safety deadline or proves that anyone saw a notice.
public sealed record QuietHours(TimeOnly Start, TimeOnly End, string ZoneId);
public sealed record QuietWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public static class NotificationQuietHours
{
    public const int ZoneIdLimit = 64;

    // Null result means accepted. All-null is "no quiet hours" (empty setting has none).
    public static string? Validate(string? start, string? end, string? zoneId, out QuietHours? hours)
    {
        hours = null;
        if (start == null && end == null && zoneId == null) return null;
        if (start == null || end == null || zoneId == null) return "quiet_hours_incomplete";
        if (!TimeOnly.TryParseExact(start, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var from) ||
            !TimeOnly.TryParseExact(end, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var to))
            return "quiet_hours_invalid";
        // Start equal to end would be either an empty or a 24 h suppression; both are rejected.
        if (from == to) return "quiet_hours_invalid";
        if (Zone(zoneId) == null) return "quiet_hours_zone_unknown";
        hours = new(from, to, zoneId);
        return null;
    }

    // Only explicit IANA identifiers are accepted; Windows display ids or offsets are not a user zoneId.
    public static TimeZoneInfo? Zone(string? zoneId)
    {
        if (zoneId is not { Length: > 0 and <= ZoneIdLimit } || zoneId.Trim() != zoneId ||
            !zoneId.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-' or '+')) return null;
        if (zoneId != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(zoneId, out _)) return null;
        return TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone) ? zone : null;
    }

    // Resolve a recipient-zone wall-clock time to UTC. DST gap: first valid local
    // instant after the gap. DST overlap: the later occurrence (smaller offset).
    public static DateTimeOffset Resolve(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        for (var guard = 0; zone.IsInvalidTime(local) && guard < 48 * 60; guard++) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Min() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    // The quiet interval containing `now`, if any. Intervals may cross midnight.
    public static QuietWindow? Active(QuietHours hours, TimeZoneInfo zone, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        foreach (var day in new[] { today, today.AddDays(-1) }) {
            var starts = Resolve(day.ToDateTime(hours.Start), zone);
            var ends = Resolve((hours.End > hours.Start ? day : day.AddDays(1)).ToDateTime(hours.End), zone);
            if (ends > starts && starts <= now && now < ends) return new(starts, ends);
        }
        return null;
    }

    // Mandatory/Critical notices are always visible immediately. An optional notice
    // raised inside the active quiet window is deferred until the window ends;
    // anything already raised before the window remains visible.
    public static bool Deferred(string severity, bool mandatory, DateTimeOffset createdAt, QuietWindow? window) =>
        window != null && !NotificationPolicy.IsMandatory(severity, mandatory) && createdAt >= window.StartsAt;
}
