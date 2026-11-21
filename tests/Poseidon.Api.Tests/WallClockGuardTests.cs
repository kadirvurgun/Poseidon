using System.Text.RegularExpressions;
using Xunit;

namespace Poseidon.Api.Tests;

// Source guard, not a database test: a hidden wall-clock read in product code turns
// date-relative tests into time bombs (NotificationTests.cs:291 failed this way) and
// makes request timestamps untestable. Backend product code reads time only through the
// injected TimeProvider (HttpContext.UtcNow(), PoseidonDbContext.Clock or a `now` parameter).
// The system clock may be named only at the explicit composition/fallback seams below.
[Trait("Category", "Unit")]
public sealed class WallClockGuardTests
{
    private static readonly Regex WallClock = new(@"\bDateTime(?:Offset)?\s*\.\s*(?:UtcNow|Now|Today)\b", RegexOptions.Compiled);
    private static readonly Regex SystemClock = new(@"\bTimeProvider\s*\.\s*System\b", RegexOptions.Compiled);

    // Exact (file, occurrence count) seams allowed to name the system TimeProvider. Keep tiny.
    private static readonly Dictionary<string, int> SystemClockAllowlist = new(StringComparer.Ordinal)
    {
        ["backend/Poseidon.Api/Startup.cs"] = 1,                          // single DI registration
        ["backend/Poseidon.DbMigrator/MigrationDesignTimeServices.cs"] = 1, // design-time migration id tooling
        ["backend/Poseidon.Persistence/PoseidonDbContext.cs"] = 1,        // Clock fallback for offline tools (DbMigrator/seed)
        ["backend/Poseidon.Api/CertificateExtractionProvider.cs"] = 2,    // timeout timer fallback when no TimeProvider is injected
    };

    [Fact]
    public void Backend_product_code_reads_time_only_through_TimeProvider()
    {
        var sources = BackendSources().ToArray();
        Assert.NotEmpty(sources);
        var violations = sources.SelectMany(x => Violations(x.Name, x.Text)).ToArray();
        Assert.True(violations.Length == 0,
            "Use the injected TimeProvider (h.UtcNow(), db.Clock.GetUtcNow() or a DateTimeOffset now parameter):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void System_clock_allowlist_entries_are_all_still_present()
    {
        var sources = BackendSources().ToDictionary(x => x.Name, x => x.Text, StringComparer.Ordinal);
        foreach (var (file, count) in SystemClockAllowlist)
        {
            Assert.True(sources.TryGetValue(file, out var text), $"Stale allowlist entry: {file}");
            Assert.Equal(count, SystemClock.Matches(text!).Count);
        }
    }

    [Fact]
    public void Guard_detects_hidden_wall_clock_shapes()
    {
        const string bad = "var a = DateTimeOffset.UtcNow; var b = DateTime.UtcNow; var c = DateTime.Now;\n" +
            "var d = DateTimeOffset.Now; var e = DateTime.Today; var f = TimeProvider.System.GetUtcNow();";
        Assert.Equal(6, Violations("backend/Poseidon.Api/Sample.cs", bad).Count());
        const string good = "var now = h.UtcNow(); var later = db.Clock.GetUtcNow(); var epoch = DateTimeOffset.UnixEpoch; var max = DateTime.MaxValue;";
        Assert.Empty(Violations("backend/Poseidon.Api/Sample.cs", good));
    }

    private static IEnumerable<string> Violations(string name, string text)
    {
        foreach (Match m in WallClock.Matches(text)) yield return $"{name}:{Line(text, m.Index)}: direct wall-clock read '{m.Value}'";
        var system = SystemClock.Matches(text);
        if (system.Count > (SystemClockAllowlist.TryGetValue(name, out var allowed) ? allowed : 0))
            foreach (Match m in system) yield return $"{name}:{Line(text, m.Index)}: non-allowlisted system clock '{m.Value}'";
    }

    private static int Line(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<(string Name, string Text)> BackendSources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var backend = Path.Combine(root!.FullName, "backend");
        var sep = Path.DirectorySeparatorChar;
        // Migrations are historical generated code; tests are outside backend/ and may use the wall clock.
        return Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(sep + "Migrations" + sep) && !p.Contains(sep + "obj" + sep) && !p.Contains(sep + "bin" + sep))
            .Select(p => (Path.GetRelativePath(root.FullName, p).Replace('\\', '/'), File.ReadAllText(p)));
    }
}
