namespace Poseidon;

// Ship number arithmetic only. No assignment, registry, applicability or company-number validation.
public static class ImoNumber
{
    public static bool HasFormat(string value) => value.Length == 7 && value.All(c => c is >= '0' and <= '9');
    public static bool HasCheckDigit(string value) {
        if (!HasFormat(value)) return false;
        var sum = 0;
        for (var i = 0; i < 6; i++) sum += (value[i] - '0') * (7 - i);
        return sum % 10 == value[6] - '0';
    }
    public static void Validate(string value) {
        if (!HasFormat(value)) throw new ApiFault(422, "invalid_imo", "imo_no must contain exactly seven ASCII digits.");
        if (!HasCheckDigit(value)) throw new ApiFault(422, "invalid_imo_checksum", "imo_no check digit does not match. Check the source or omit the optional value; registry assignment is not verified.");
    }
}
