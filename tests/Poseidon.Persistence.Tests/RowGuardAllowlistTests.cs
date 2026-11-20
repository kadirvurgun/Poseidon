using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-101/T-OPS2-005, DEV-102/T-OPS2-006: the PostgreSQL row guards are the last
// line of ownership immutability. A lifecycle column the EF SaveChanges guard lets
// a command change must also be allowed by the newest database guard definition,
// otherwise the first real FINALIZE / lifecycle UPDATE raises P0001 (PR #44: 503
// at ShipOpsLifecycleTests.cs:47 and :81 while every no-write branch passed).
[Trait("Category", "Unit")]
public sealed class RowGuardAllowlistTests
{
    private static string LatestGuard(string function)
    {
        var definitions = typeof(PoseidonDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Id: t.GetCustomAttributes(typeof(MigrationAttribute), false).OfType<MigrationAttribute>().SingleOrDefault()?.Id, Type: t))
            .Where(x => x.Id != null).OrderBy(x => x.Id, StringComparer.Ordinal)
            .SelectMany(x => ((Migration)Activator.CreateInstance(x.Type)!).UpOperations.OfType<SqlOperation>().Select(o => o.Sql))
            .SelectMany(sql => Regex.Matches(sql, @"CREATE (?:OR REPLACE )?FUNCTION operations\." + function + @"\(\).*?END \$\$;", RegexOptions.Singleline).Select(m => m.Value))
            .ToArray();
        Assert.NotEmpty(definitions);
        return definitions[^1];
    }

    private static string[] Allowlist(string guard)
    {
        var arrays = Regex.Matches(guard, @"to_jsonb\((?:OLD|NEW)\)\s*-\s*ARRAY\[([^\]]*)\]").Select(m => m.Groups[1].Value).Distinct().ToArray();
        var only = Assert.Single(arrays);
        return only.Split(',').Select(c => c.Trim().Trim('\'')).ToArray();
    }

    [Fact]
    public void Ship_operation_guard_allows_sof_lifecycle_columns_and_pins_final_revision()
    {
        var guard = LatestGuard("guard_ship_operation");
        var columns = Allowlist(guard);
        Assert.Contains("SofState", columns);
        Assert.Contains("FinalRevision", columns);
        // Ownership columns stay outside the allowlist.
        Assert.DoesNotContain("TenantId", columns);
        Assert.DoesNotContain("VesselId", columns);
        Assert.DoesNotContain("Kind", columns);
        Assert.Contains("Immutable SOF final revision pin", guard);
    }

    [Fact]
    public void Downtime_guard_allows_closure_lifecycle_columns_and_keeps_closed_terminal()
    {
        var guard = LatestGuard("guard_downtime_draft");
        var columns = Allowlist(guard);
        Assert.Contains("ClosureState", columns);
        Assert.Contains("ClaimEvidenceJson", columns);
        Assert.DoesNotContain("TenantId", columns);
        Assert.DoesNotContain("VesselId", columns);
        Assert.Contains("Closed downtime is immutable", guard);
    }
}
