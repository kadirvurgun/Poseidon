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
