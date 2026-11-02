using System.Text.RegularExpressions;
using Xunit;

namespace Poseidon.Api.Tests;

// Source guard, not a database test: PostgreSQL SSI can abort any statement of a
// SERIALIZABLE transaction with 40001/40P01, and Npgsql's execution strategy may wrap
// it. A provider-shaped catch on one SqlState lets a racing duplicate surface as 500
// (flaky CI). Classification must go through PostgresFailure.
public sealed class SerializableTransactionGuardTests
{
    private static readonly Regex NarrowCatch = new(@"catch\s*\(\s*(?:Npgsql\.)?PostgresException\b", RegexOptions.Compiled);
    private static readonly Regex SqlStateLiteral = new(@"SqlState\s*==\s*""40(?:001|P01)""", RegexOptions.Compiled);

    [Fact]
    public void Serializable_transactions_classify_conflicts_through_PostgresFailure()
    {
        var sources = BackendSources().Where(x => x.Text.Contains("IsolationLevel.Serializable")).ToArray();
        Assert.NotEmpty(sources);
        var violations = sources.SelectMany(x => Violations(x.Name, x.Text)).ToArray();
        Assert.True(violations.Length == 0, "Use PostgresFailure.IsSerialization/IsDatabase instead of a provider-shaped catch:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Guard_detects_the_historical_narrow_catch_shape()
    {
        const string bad = "await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);\n" +
            "try { } catch (Npgsql.PostgresException e) when (e.SqlState == \"40001\") { }";
        Assert.Equal(3, Violations("Sample.cs", bad).Count());
        const string good = "await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);\n" +
            "try { } catch (Exception e) when (PostgresFailure.IsDatabase(e)) { }";
        Assert.Empty(Violations("Sample.cs", good));
    }

    private static IEnumerable<string> Violations(string name, string text)
    {
        if (!text.Contains("PostgresFailure.")) yield return $"{name}: SERIALIZABLE transaction without PostgresFailure classification";
        foreach (Match m in NarrowCatch.Matches(text)) yield return $"{name}:{Line(text, m.Index)}: provider-shaped PostgresException catch";
        foreach (Match m in SqlStateLiteral.Matches(text)) yield return $"{name}:{Line(text, m.Index)}: literal serialization SqlState comparison";
    }

    private static int Line(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<(string Name, string Text)> BackendSources()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var backend = Path.Combine(root!.FullName, "backend");
        return Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar) &&
                        !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(p => (Path.GetRelativePath(root.FullName, p), File.ReadAllText(p)));
    }
}
