using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
namespace Poseidon;

public sealed record VesselPage(IReadOnlyList<Draft> Items, string? NextCursor, bool HasMore);
public sealed record VesselBrowse(string Q, string Flag, string VesselType, int PageSize, Guid? After, string Binding)
{
    private sealed record Cursor(int Version, string After, string Binding);
    public static VesselBrowse Parse(IQueryCollection query, Session scope)
    {
        var allowed = new[] { "q", "flag", "vessel_type", "pageSize", "cursor" };
        if (query.Any(p => !allowed.Contains(p.Key) || p.Value.Count != 1)) throw Invalid();
        string Text(string key, int max) {
            var raw = query[key].ToString();
            if (raw.Length > max * 2 || raw.Any(char.IsControl)) throw Invalid();
            var value = raw.Trim(); if (value.Length > max) throw Invalid(); return value;
        }
        var q = Text("q", 100); var flag = Text("flag", 200); var type = Text("vessel_type", 200);
        var size = 50;
        if (query.ContainsKey("pageSize") && (!int.TryParse(query["pageSize"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out size) || size < 1 || size > 100)) throw Invalid();
        // Query/scope binding is an integrity consistency check, NOT an authorization token.
        // Cursor is intentionally unsigned; changing its position never bypasses SQL access checks.
        var binding = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Q = Fold(q), Flag = flag, Type = type, Size = size, scope.Subject, scope.TenantId, scope.CompanyId,
            Vessels = scope.VesselIds.OrderBy(x => x).ToArray()
        }))));
        Guid? after = null;
        if (query.ContainsKey("cursor")) {
            var encoded = query["cursor"].ToString();
            if (encoded.Length is 0 or > 512) throw Invalid();
            try {
                var cursor = JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(encoded));
                if (cursor is null || cursor.Version != 1 || cursor.Binding != binding || !Guid.TryParseExact(cursor.After,"D",out var id) || id == Guid.Empty) throw Invalid();
                after = id;
            } catch (Exception e) when (e is FormatException or JsonException or ArgumentException) { throw Invalid(); }
        }
        return new(Fold(q), flag, type, size, after, binding);
    }
    public string CursorAfter(Guid id) => WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Cursor(1,id.ToString("D"),Binding)));
    // Deliberately ASCII-only case folding; do not imply Turkish linguistic collation.
    public static string Fold(string value) => string.Concat(value.Select(c => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c));
    public static string Like(string value) => "%" + value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    private static ApiFault Invalid() => new(400, "invalid_vessel_query", "Invalid query or cursor. Reset pagination after changing filters or session scope.");
}
