using System.Text.RegularExpressions;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// S6 lost two server permissions silently: endpoints checked a.Permissions.Contains("…") for
// strings that ActorResolver.TrustedPermissions dropped, so every caller was denied and only a
// PostgreSQL lifecycle test noticed. This source guard (no host, no database) fails when an API
// permission check or permission constant names a permission the trusted allowlist filters out.
[Trait("Category", "Unit")]
public sealed partial class PermissionAllowlistGuardTests
{
    private static DirectoryInfo Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Poseidon.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        return root;
    }

    // Permission-shaped literals used by the server authorization predicates.
    internal static SortedDictionary<string, string> ReferencedPermissions()
    {
        var root = Root();
        var found = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Add(string permission, string file) => found.TryAdd(permission, Path.GetFileName(file));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "backend", "Poseidon.Api"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (Match call in ContainsCall().Matches(text))
                foreach (Match literal in PermissionLiteral().Matches(Arguments(text, call.Index + call.Length)))
                    Add(literal.Groups["p"].Value, file);
            foreach (Match method in PermissionMethod().Matches(text))
                foreach (Match literal in PermissionLiteral().Matches(method.Groups["body"].Value))
                    Add(literal.Groups["p"].Value, file);
        }
        foreach (var project in new[] { "Poseidon.Api", "Poseidon.Domain" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "backend", project), "*.cs"))
            {
                var text = File.ReadAllText(file);
                foreach (Match constant in PermissionConstant().Matches(text)) Add(constant.Groups["p"].Value, file);
                foreach (Match method in PermissionMethod().Matches(text))
                    foreach (Match literal in PermissionLiteral().Matches(method.Groups["body"].Value))
                        Add(literal.Groups["p"].Value, file);
            }
        return found;
    }

    // The balanced argument list of a Permissions.Contains( call starting at index.
    private static string Arguments(string text, int index)
    {
        var depth = 1;
        var end = index;
        while (end < text.Length && depth > 0)
        {
            if (text[end] == '(') depth++;
            else if (text[end] == ')') depth--;
            end++;
        }
        return text[index..Math.Max(index, end - 1)];
    }

    [Fact]
    public void Every_permission_checked_by_the_api_survives_the_trusted_allowlist()
    {
        var referenced = ReferencedPermissions();
        Assert.True(referenced.Count > 80, $"Scanner matched only {referenced.Count} permissions.");
        var trusted = ActorResolver.TrustedPermissions([.. referenced.Keys]).ToHashSet(StringComparer.Ordinal);
        var missing = referenced.Where(x => !trusted.Contains(x.Key)).Select(x => $"{x.Value}: {x.Key}").ToArray();
        Assert.True(missing.Length == 0, "Permissions checked by the API but dropped by ActorResolver.TrustedPermissions: " + string.Join(", ", missing));
    }

    [Fact]
    public void Scanner_covers_literal_ternary_constant_and_switch_forms()
    {
        var referenced = ReferencedPermissions();
        Assert.Contains("crew.read", referenced.Keys);                       // a.Permissions.Contains("crew.read")
        Assert.Contains("notification.preference.write", referenced.Keys);   // Contains(id != null ? "…" : "…")
        Assert.Contains("applicability.override.approve", referenced.Keys);  // const string ApprovePermission
        Assert.Contains("certificate.renewal.verify", referenced.Keys);      // static string? Permission(string action) => switch
        Assert.Empty(ActorResolver.TrustedPermissions(["not.a.server.permission"]));
    }

    [GeneratedRegex(@"Permissions\.Contains\(")]
    private static partial Regex ContainsCall();

    [GeneratedRegex(@"""(?<p>[a-z_]+(?:\.[a-z_]+)+)""")]
    private static partial Regex PermissionLiteral();

    [GeneratedRegex(@"const\s+string\s+\w*Permission\w*\s*=\s*""(?<p>[a-z_]+(?:\.[a-z_]+)+)""")]
    private static partial Regex PermissionConstant();

    // Expression-bodied permission mappers, e.g. static string? Permission(string action) => action switch { … };
    [GeneratedRegex(@"static\s+string\??\s+\w*Permission\w*\([^)]*\)\s*=>(?<body>[^;]*);")]
    private static partial Regex PermissionMethod();
}
