using System.Text.RegularExpressions;
using Xunit;

namespace Poseidon.Persistence.Tests;

// Every security event append is checked by SecurityEventCodes.Allows; an unregistered
// (action, result) pair throws at runtime, turning the intended response (for example the
// WorkspaceRead 503 context_unavailable) into an audit-store failure. This guards every
// direct literal pair written by the API layer.
[Trait("Category", "Unit")]
public sealed partial class ApiSecurityAuditLiteralCodeTests
{
    [Fact]
    public void Workspace_context_unavailable_audit_result_is_registered() =>
        Assert.True(SecurityEventCodes.Allows("WorkspaceRead", "Unavailable"));

    [Fact]
    public void Every_literal_action_result_pair_written_by_the_api_is_registered()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Poseidon.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var scanned = 0;
        var unregistered = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "backend", "Poseidon.Api"), "*.cs"))
        {
            foreach (Match match in LiteralPair().Matches(File.ReadAllText(file)))
            {
                scanned++;
                var action = match.Groups["action"].Value;
                var result = match.Groups["result"].Value;
                if (!SecurityEventCodes.Allows(action, result)) unregistered.Add($"{Path.GetFileName(file)}: {action}/{result}");
            }
        }
        Assert.True(scanned > 100, $"Scanner matched only {scanned} literal pairs.");
        Assert.Empty(unregistered);
    }

    [GeneratedRegex(""""\.(?:Try)?WriteAsync\(\s*\w+\s*,\s*"(?<action>\w+)"\s*,\s*"(?<result>\w+)"""")]
    private static partial Regex LiteralPair();
}
