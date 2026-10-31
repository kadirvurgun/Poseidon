using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TextInput(string State = "Missing", string? Value = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NumberInput(string State = "Missing", decimal? Value = null);

// Fixed source-backed fields; no client-supplied field definitions or mandatory/catalog policy.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegulatoryDraftContent
{
    public TextInput Imo { get; init; } = new();
    public TextInput Flag { get; init; } = new();
    public TextInput ShipType { get; init; } = new();
    public TextInput ClassSociety { get; init; } = new();
    public TextInput ClassNumber { get; init; } = new();
    public NumberInput Gt { get; init; } = new();
    public NumberInput Nt { get; init; } = new();
    public NumberInput Dwt { get; init; } = new();
    public NumberInput Loa { get; init; } = new();
    public NumberInput Breadth { get; init; } = new();
    public NumberInput Draft { get; init; } = new();
    public TextInput BuildDate { get; init; } = new();
    public TextInput TradingArea { get; init; } = new();
    public TextInput CargoContext { get; init; } = new();
    public TextInput Declaration { get; init; } = new();
    public TextInput SourceReference { get; init; } = new();
    public TextInput SourceObservedAt { get; init; } = new();
}

public static class RegulatoryDraftPolicy
{
    // Structural storage bounds, not claimed approved operational mandatory/range policies.
    public const int TextLimit = 500;
    public static (RegulatoryDraftContent Content, Dictionary<string, string[]> Errors) Normalize(RegulatoryDraftContent input)
    {
        var errors = new Dictionary<string, string[]>();
        TextInput Text(TextInput? value, string key)
        {
            if (value is null || value.State is not ("Missing" or "Unknown" or "Known") ||
                (value.State != "Known" && value.Value != null))
            { errors[key] = ["invalid_state_value"]; return new(); }
            if (value.State != "Known") return value;
            var text = DraftValue.Text(value.Value);
            if (text.State == DataState.Missing) return new();
            var trimmed = value.Value!.Trim();
            if (trimmed.Length > TextLimit || trimmed.Any(char.IsControl)) errors[key] = ["invalid_text"];
            return new("Known", trimmed);
        }
        NumberInput Number(NumberInput? value, string key)
        {
            if (value is null || value.State is not ("Missing" or "Unknown" or "Known") ||
                (value.State == "Known" ? value.Value == null : value.Value != null))
            { errors[key] = ["invalid_state_value"]; return new(); }
            if (value.Value < 0) errors[key] = ["non_negative_required"];
            return value;
        }
        var result = input with
        {
            Imo = Text(input.Imo, "imo"), Flag = Text(input.Flag, "flag"), ShipType = Text(input.ShipType, "shipType"),
            ClassSociety = Text(input.ClassSociety, "classSociety"), ClassNumber = Text(input.ClassNumber, "classNumber"),
            Gt = Number(input.Gt, "gt"), Nt = Number(input.Nt, "nt"), Dwt = Number(input.Dwt, "dwt"),
            Loa = Number(input.Loa, "loa"), Breadth = Number(input.Breadth, "breadth"), Draft = Number(input.Draft, "draft"),
            BuildDate = Text(input.BuildDate, "buildDate"), TradingArea = Text(input.TradingArea, "tradingArea"),
            CargoContext = Text(input.CargoContext, "cargoContext"), Declaration = Text(input.Declaration, "declaration"),
            SourceReference = Text(input.SourceReference, "sourceReference"), SourceObservedAt = Text(input.SourceObservedAt, "sourceObservedAt")
        };
        if (result.Imo.State == "Known" && (result.Imo.Value!.Length != 7 || result.Imo.Value!.Any(c => !char.IsAsciiDigit(c))))
            errors["imo"] = ["seven_digits_required"];
        if (result.BuildDate.State == "Known" && !DateOnly.TryParseExact(result.BuildDate.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors["buildDate"] = ["iso_date_required"];
        if (result.SourceObservedAt.State == "Known")
        {
            var timestamp = result.SourceObservedAt.Value!;
            if (!(timestamp.EndsWith('Z') || (timestamp.Length >= 6 && timestamp[^3] == ':' && timestamp[^6] is '+' or '-')) ||
                !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                errors["sourceObservedAt"] = ["offset_timestamp_required"];
            else result = result with { SourceObservedAt = new("Known", parsed.ToUniversalTime().ToString("O")) };
        }
        return (result, errors);
    }
}
