using System.Globalization;

namespace Poseidon.Domain;

// T-S1-004: Closed business summary vocabulary. Unknown fields are excluded, never serialized.
public static class AuditValuePolicy
{
    public static bool Includes(string field) => field is "Code" or "Name" or "Kind" or "CompanyId" or
        "SourceVersion" or "RequiredBollardPull" or "RequiredTugCount" or "RequestOwner" or "PlannedStart" or "PlannedEnd" or "RequestedStart" or "RequestedEnd" or "GrossTonnage" or "BollardPullTonnes" or "Revision" or "ContentJson" or "State";
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
            ("State", string state) when state is "Draft" or "Uploaded" or "MappingDraft" or "Previewed" or "Committed" or "Pending" or "Receiving" or "CleanTransport" or "Abandoned" or "SyntheticUnverified" => state,
            ("SourceVersion", string source) when source == TugRequestPolicy.SourceVersion || source == TugDraftPlanning.SourceVersion => source,
            ("RequiredTugCount", int count) when count is >= 1 and <= 1000 => count.ToString(CultureInfo.InvariantCulture),
            ("PlannedStart" or "PlannedEnd" or "RequestedStart" or "RequestedEnd", DateTimeOffset time) => time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ("RequestOwner", Guid role) => role.ToString("D"),
            ("CompanyId", Guid id) => id.ToString("D"),
            ("Revision", long revision) when revision >= 0 => revision.ToString(CultureInfo.InvariantCulture),
            ("RequiredBollardPull" or "GrossTonnage" or "BollardPullTonnes", decimal number) when number >= 0 => number.ToString(CultureInfo.InvariantCulture),
            ("Kind", Enum kind) when kind.ToString() is "Ship" or "Tug" => kind.ToString(),
            ("Kind", string kind) when kind is "Source" or "Requirement" or "Control" or "Evidence" or "Verification" or "Decision" or "Meter" or "Condition" => kind,
            _ => throw new InvalidOperationException("Unsupported audit value type.")
        };
    }
}
