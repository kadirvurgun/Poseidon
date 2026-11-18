using System.Text;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// Internal authoring contract. Draft rows are not accepted VesselTypeCatalogRevision
// sources, native schema bindings, live selectors or legal/operational approvals.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FleetCatalogEntryDraft(Guid Id, string? TypeCode, string? Label, string? Mode,
    string[] AllowedProfileCodes, string[] RequiredInputPaths);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FleetCatalogDraftContent(string? Title, FleetCatalogEntryDraft[] Entries);

public static class FleetCatalogDraftPolicy
{
    // Technical authoring bound, not a normative limit on the production catalog.
    public const int MaximumDocumentBytes = 1024 * 1024;
    public static FleetCatalogDraftContent Normalize(FleetCatalogDraftContent input)
    {
        if (input.Entries is null) throw new ArgumentException("CATALOG_DRAFT_INVALID");
        var ids = new HashSet<Guid>(); var codes = new HashSet<string>(StringComparer.Ordinal);
        var entries = input.Entries.Select(entry => {
            if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) || entry.Mode is not (null or "SHIP" or "TUG") ||
                entry.AllowedProfileCodes is null || entry.RequiredInputPaths is null ||
                entry.AllowedProfileCodes.Length > 50 || entry.RequiredInputPaths.Length > 100)
                throw new ArgumentException("CATALOG_DRAFT_INVALID");
            var code = Text(entry.TypeCode, 64);
            if (code is not null && !codes.Add(code)) throw new ArgumentException("CATALOG_DRAFT_DUPLICATE_CODE");
            var profiles = entry.AllowedProfileCodes.Select(x => Text(x, 64) ?? throw new ArgumentException("CATALOG_DRAFT_INVALID")).ToArray();
            var paths = entry.RequiredInputPaths.Select(x => Text(x, 200) ?? throw new ArgumentException("CATALOG_DRAFT_INVALID")).ToArray();
            if (profiles.Distinct(StringComparer.Ordinal).Count() != profiles.Length || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length || paths.Any(x => !Pointer(x)))
                throw new ArgumentException("CATALOG_DRAFT_INVALID");
            return entry with { TypeCode = code, Label = Text(entry.Label, 200), AllowedProfileCodes = profiles, RequiredInputPaths = paths };
        }).ToArray();
        return new(Text(input.Title, 200), entries);
    }

    private static string? Text(string? value, int max)
    {
        if (value is null) return null;
        if (value.Any(char.IsControl)) throw new ArgumentException("CATALOG_DRAFT_INVALID");
        var text = value.Trim().Normalize(NormalizationForm.FormC);
        if (text.Length > max) throw new ArgumentException("CATALOG_DRAFT_INVALID");
        return text.Length == 0 ? null : text;
    }

    private static bool Pointer(string path)
    {
        if (!path.StartsWith('/')) return false;
        for (var i = 0; i < path.Length; i++)
            if (path[i] == '~' && (++i == path.Length || path[i] is not ('0' or '1'))) return false;
        return true;
    }
}
