using System.Text.RegularExpressions;
using Xunit;

namespace Poseidon.Api.Tests;

// RATE-LIMIT-01 controlled raw-SQL design (AGENTS.md): the only raw writers outside the audited SaveChanges path are the
// counter upsert and the bounded expiry delete in RateLimiting.cs. Pin them to the one technical table, and keep every other
// backend source (migrations excepted) away from that table, so the exception cannot grow into a business-write bypass.
[Trait("Category", "Unit")]
[Trait("Requirement", "RATE-LIMIT-01")]
public sealed class RateLimitRawSqlBoundaryTests
{
    private static readonly Regex Table = new(@"\b([a-z_]{3,})\.""([A-Za-z]+)""", RegexOptions.Compiled);
    private static readonly Regex Write = new(@"\b(INSERT|UPDATE|DELETE|TRUNCATE|ALTER|DROP|CREATE|GRANT)\b", RegexOptions.Compiled);

    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }

    [Fact]
    public void Rate_limit_sql_touches_only_the_counter_table()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "backend", "Poseidon.Persistence", "RateLimiting.cs"));
        var sql = Regex.Matches(text, "\"\"\"(.*?)\"\"\"", RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(2, sql.Length);
        Assert.All(sql, statement =>
        {
            var tables = Table.Matches(statement).Select(m => m.Groups[1].Value + "." + m.Groups[2].Value).Distinct().ToArray();
            Assert.Equal(["ratelimit.RateLimitCounters"], tables);
            Assert.All(Write.Matches(statement).Select(m => m.Value), verb => Assert.Contains(verb, new[] { "INSERT", "UPDATE", "DELETE" }));
        });
    }

    [Fact]
    public void No_other_backend_source_writes_the_counter_table()
    {
        var backend = Path.Combine(Root(), "backend");
        var offenders = Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.EndsWith("RateLimiting.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("RateLimitCounters\"", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(backend, path)).ToArray();
        Assert.Empty(offenders);
    }
}
