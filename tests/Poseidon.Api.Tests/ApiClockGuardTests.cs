using System.Text.RegularExpressions;
using Xunit;

namespace Poseidon.Api.Tests;

// Source guard, not a database test: request-path code reads time only from the DI TimeProvider
// (HttpContext.UtcNow() / an injected TimeProvider). A static wall-clock read beside a replaced test
// clock mixes two clocks in one request and produced date-bomb failures (NotificationTests).
public sealed class ApiClockGuardTests
{
    private static readonly Regex WallClock = new(@"\bDateTime(?:Offset)?\s*\.\s*(?:UtcNow|Now|Today)\b", RegexOptions.Compiled);

    // Justified exceptions only; each entry names why it is not request-path time.
    private static readonly Dictionary<string, string> Allowlist = new(StringComparer.Ordinal)
    {
        // Operator reconciliation entry point (no HttpContext); the check only rejects a cutoff that
        // has not yet elapsed on the real wall clock before staged objects are removed.
        ["FileReconciliation.cs"] = "operator reconciliation cutoff guard",
    };

    [Fact]
    public void Api_sources_read_time_from_the_injected_TimeProvider()
    {
        var sources = ApiSources().ToArray();
        Assert.NotEmpty(sources);
        var violations = sources.Where(x => !Allowlist.ContainsKey(Path.GetFileName(x.Name)))
            .SelectMany(x => Violations(x.Name, x.Text)).ToArray();
        Assert.True(violations.Length == 0, "Read time via HttpContext.UtcNow() or an injected TimeProvider:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Allowlisted_files_still_exist_and_still_need_the_exception()
    {
        var sources = ApiSources().ToDictionary(x => Path.GetFileName(x.Name), x => x.Text, StringComparer.Ordinal);
        foreach (var file in Allowlist.Keys)
        {
            Assert.True(sources.ContainsKey(file), $"Stale clock allowlist entry: {file}");
            Assert.NotEmpty(Violations(file, sources[file]));
        }
    }

    [Fact]
    public void Guard_detects_static_wall_clock_reads()
    {
        Assert.Equal(4, Violations("Sample.cs", "var a = DateTimeOffset.UtcNow; var b = DateTime.UtcNow; var c = DateTimeOffset.Now; var d = DateTime.Today;").Count());
        Assert.Empty(Violations("Sample.cs", "var now = h.UtcNow(); var t = time.GetUtcNow(); var e = DateTimeOffset.UnixEpoch;"));
    }

    private static IEnumerable<string> Violations(string name, string text)
    {
        foreach (Match m in WallClock.Matches(text)) yield return $"{name}:{text.AsSpan(0, m.Index).Count('\n') + 1}: {m.Value}";
    }

    private static IEnumerable<(string Name, string Text)> ApiSources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var api = Path.Combine(root!.FullName, "backend", "Poseidon.Api");
        return Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(p => (Path.GetRelativePath(root.FullName, p), File.ReadAllText(p)));
    }
}
