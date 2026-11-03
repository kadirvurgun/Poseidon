using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Persistence.Tests;

// Projection-only EF tracking unit fixtures: no connection opens, SaveChanges,
// persisted audit rows, source qualification or runtime coverage evidence.
public sealed class VesselFenceAuditProjectionTests
{
    [Theory]
    [InlineData("unchanged-name-only", true)]
    [InlineData("changed-name", false)]
    [InlineData("additional-code-property", false)]
    [InlineData("additional-company-property", false)]
    [InlineData("added", false)]
    public void Owner_binding_marks_only_actual_unchanged_name_only_modified_vessel_fences(string scenario, bool expected)
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        var tenant = Guid.NewGuid();
        using var db = new PoseidonDbContext(options.Options, tenant, new("synthetic-projection", "System", "synthetic-projection"));
        var vessel = new Vessel { TenantId = tenant, Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
            Code = "FICTIONAL-UNIT", Name = "FICTIONAL withheld original name", Kind = VesselKind.Ship, Version = 17 };
        var entry = db.Attach(vessel);
        entry.Property(x => x.Name).IsModified = true;
        if (scenario == "changed-name") vessel.Name = "FICTIONAL withheld changed name";
        if (scenario == "additional-code-property") entry.Property(x => x.Code).IsModified = true;
        if (scenario == "additional-company-property") entry.Property(x => x.CompanyId).IsModified = true;
        if (scenario == "added") entry.State = EntityState.Added;
        var projectionType = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var method = projectionType.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!;
        var projection = method.Invoke(null, [entry, null, new AuditCommandContext("ApplicabilityEvaluate", "REGULATORY_WORKFLOW"), true])!;
        var raw = (string)projectionType.GetProperty("Summary")!.GetValue(projection)!;
        using var summary = JsonDocument.Parse(raw);
        var markers = summary.RootElement.EnumerateArray().Where(x => x.GetProperty("field").GetString() == "Binding").ToArray();
        Assert.Equal(expected ? 1 : 0, markers.Length);
        if (expected)
        {
            Assert.Equal("OwnerReference", markers[0].GetProperty("before").GetString());
            Assert.Equal("OwnerReference", markers[0].GetProperty("after").GetString());
            Assert.Equal(vessel.Id, projectionType.GetProperty("VesselId")!.GetValue(projection));
            Assert.Equal(vessel.Id, projectionType.GetProperty("ResourceId")!.GetValue(projection));
            Assert.Null(projectionType.GetProperty("BeforeRevision")!.GetValue(projection));
            Assert.Null(projectionType.GetProperty("AfterRevision")!.GetValue(projection));
        }
        Assert.DoesNotContain("FICTIONAL withheld", raw);
    }
}
