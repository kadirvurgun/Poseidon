using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// REG-SOURCE-DRAFT-01. Declared preparation only; these fields confer no legal authority.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterContent
{
    public TextInput Title { get; init; } = new();
    public TextInput Authority { get; init; } = new();
    public TextInput Locator { get; init; } = new();
    public TextInput Instrument { get; init; } = new();
    public TextInput Amendment { get; init; } = new();
    public TextInput EffectiveDate { get; init; } = new();
    public TextInput Declaration { get; init; } = new();
    public TextInput ObservedAt { get; init; } = new();
}

public static class RegulatoryRegisterPolicy
{
    public static Dictionary<string, string[]> Validate(RegisterContent content, string kind)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (key, value, bound) in new (string, TextInput?, int)[] {
            ("title", content.Title, 200), ("authority", content.Authority, 200),
            ("locator", content.Locator, 1000), ("instrument", content.Instrument, 200),
            ("amendment", content.Amendment, 200), ("effectiveDate", content.EffectiveDate, 10),
            ("declaration", content.Declaration, 1000), ("observedAt", content.ObservedAt, 40) })
        {
            if (value == null || value.State is not ("Missing" or "Unknown" or "Known") ||
                (value.State == "Known" ? string.IsNullOrWhiteSpace(value.Value) || value.Value.Length > bound || value.Value.Any(char.IsControl) : value.Value != null))
                errors[key] = ["invalid_state_value_or_bound"];
        }
        if (errors.Count > 0) return errors;
        if (content.EffectiveDate.State == "Known" && !DateOnly.TryParseExact(content.EffectiveDate.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors["effectiveDate"] = ["iso_date_required"];
        if (content.ObservedAt.State == "Known" &&
            (!System.Text.RegularExpressions.Regex.IsMatch(content.ObservedAt.Value!, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$") ||
             !DateTimeOffset.TryParse(content.ObservedAt.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
            errors["observedAt"] = ["offset_timestamp_required"];
        if (kind == "Source" && content.Instrument.State != "Missing") errors["instrument"] = ["requirement_only"];
        if (kind == "Requirement" && (content.Title.State != "Missing" || content.Authority.State != "Missing" || content.Locator.State != "Missing"))
            errors["source"] = ["select_immutable_source_revision"];
        return errors;
    }
}
