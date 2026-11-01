using System.Globalization;

namespace Poseidon.Domain;

// T-S1-004: Closed business summary vocabulary. Unknown fields are excluded, never serialized.
public static class AuditValuePolicy
{
    public static bool Includes(string field) => field is "Code" or "Name" or "Kind" or "CompanyId" or
        "GrossTonnage" or "BollardPullTonnes" or "Revision" or "ContentJson";
    public static string Evidence(string field) => field switch {
        "ContentJson" => "RevisionReference", "Code" or "Name" => "ValuesWithheld",
        _ when Includes(field) => "Allowlisted", _ => throw new ArgumentException("Unsupported audit field.", nameof(field))
    };
    public static string? Value(string field, object? value)
    {
        if (!Includes(field)) throw new ArgumentException("Unsupported audit field.", nameof(field));
        // Do not even call ToString on withheld arbitrary text/body objects.
        if (Evidence(field) != "Allowlisted" || value == null) return null;
        return (field, value) switch {
            ("CompanyId", Guid id) => id.ToString("D"),
            ("Revision", long revision) when revision >= 0 => revision.ToString(CultureInfo.InvariantCulture),
            ("GrossTonnage" or "BollardPullTonnes", decimal number) when number >= 0 => number.ToString(CultureInfo.InvariantCulture),
            ("Kind", Enum kind) when kind.ToString() is "Ship" or "Tug" => kind.ToString(),
            ("Kind", string kind) when kind is "Source" or "Requirement" => kind,
            _ => throw new InvalidOperationException("Unsupported audit value type.")
        };
    }
}
