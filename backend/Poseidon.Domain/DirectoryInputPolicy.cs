namespace Poseidon.Domain;

// Structural master-data rules only; no regulatory approval or implicit vessel kind.
public static class DirectoryInputPolicy
{
    public static Dictionary<string, string[]> Validate(string? code, string? name, uint? expectedVersion, bool update)
    {
        var errors = new Dictionary<string, string[]>();
        Check("code", code, 64); Check("name", name, 200);
        if (update && expectedVersion is null or 0) errors["expectedVersion"] = ["required_version"];
        return errors;
        void Check(string field, string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) errors[field] = ["required"];
            else if (value.Length > max || value.Any(char.IsControl)) errors[field] = ["invalid_length_or_control"];
        }
    }

}
