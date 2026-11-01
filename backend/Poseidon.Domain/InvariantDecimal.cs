using System.Globalization;

namespace Poseidon.Domain;

// Decimal wire/storage values are strings. Validate the coefficient before parsing:
// decimal.TryParse alone can silently round an over-precise fraction.
public static class InvariantDecimal
{
    public static bool TryNormalize(string? value, out string canonical)
    {
        canonical = "";
        if (value is null || value.Length is < 1 or > 59) return false;
        var parts = value.Split('.');
        if (parts.Length > 2 || parts.Any(p => p.Length == 0 || p.Any(c => !char.IsAsciiDigit(c)))) return false;
        var integer = parts[0].TrimStart('0');
        if (integer.Length == 0) integer = "0";
        var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
        var coefficient = (integer + fraction).TrimStart('0');
        const string maximum = "79228162514264337593543950335";
        if (fraction.Length > 28 || coefficient.Length > maximum.Length ||
            (coefficient.Length == maximum.Length && string.CompareOrdinal(coefficient, maximum) > 0)) return false;
        canonical = integer + (fraction.Length == 0 ? "" : "." + fraction);
        return decimal.TryParse(canonical, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
    }
}
