using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-113 / T-ASS2-001 critical-write gate. The AssuranceImportDecide rows must prove before/after through the
// unchanged CriticalWriteCoverageCapture.BeforeAfter rule. CI run 44787515329 (6ea3a2b) found 2 invalid rows: each
// decided AssuranceImport (Modified) carried no BeforeRevision and only an "ImportState" field outside the gate's
// closed state vocabulary. A staged import has exactly two record revisions: 1 PENDING_REVIEW, 2 decided once.
[Trait("Requirement", "DEV-113")]
public sealed class AssuranceAuditContractTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>(); options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command)
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [entry, null, new AuditCommandContext(command, "HUMAN_REVIEW_WORKFLOW"), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
            EntityId = "synthetic", CorrelationId = "synthetic-assurance-correlation", ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }
    private static AssuranceImport Import() => new()
    {
        TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), InspectionId = Guid.NewGuid(), MappingId = Guid.NewGuid(), MappingVersion = 2,
        MappingHash = new string('A', 64), TableHash = new string('B', 64), DiffHash = new string('C', 64), ImportedBy = Guid.NewGuid(), ImportedAt = DateTimeOffset.UnixEpoch, Version = 41
    };

    [Fact]
    public void Staged_import_is_revision_one_without_a_before_revision()
    {
        using var db = Db();
        var import = Import(); var entry = db.Entry(import); entry.State = EntityState.Added;
        var (row, fields) = Row(entry, "AssuranceImportStage");
        Assert.Equal((import.InspectionId, (long?)null, (long?)1), (row.ResourceId!.Value, row.BeforeRevision, row.AfterRevision));
        Assert.Equal("2", fields["MappingVersion"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Theory]
    [InlineData("ACCEPTED")]
    [InlineData("REJECTED")]
    public void Decided_import_proves_before_after_as_revision_one_to_two(string decision)
    {
        using var db = Db();
        var import = Import(); var entry = db.Attach(import);
        import.State = decision; import.DecidedBy = Guid.NewGuid(); import.DecidedAt = DateTimeOffset.UnixEpoch; import.Reason = "FICTIONAL synthetic decision";
        entry.DetectChanges();
        var (row, fields) = Row(entry, "AssuranceImportDecide");
        Assert.Equal((41u, 1L, 2L), (row.BeforeVersion!.Value, row.BeforeRevision!.Value, row.AfterRevision!.Value));
        Assert.Equal(("PENDING_REVIEW", decision), (fields["ImportState"].GetProperty("before").GetString(), fields["ImportState"].GetProperty("after").GetString()));
        Assert.Equal("ValuesWithheld", fields["Reason"].GetProperty("evidence").GetString());
        Assert.DoesNotContain("FICTIONAL", row.ChangeSummary);
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void Tenant_mapping_and_vessel_less_receipt_resolve_tenant_scope()
    {
        using var db = Db();
        var mapping = db.Entry(new AssuranceImportMapping { TenantId = Tenant, Id = Guid.NewGuid(), Key = "sire2-synthetic", Version = 1, Hash = new string('D', 64) });
        mapping.State = EntityState.Added;
        var receipt = db.Entry(new AssuranceReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString("D"), Action = "AssuranceMappingCreate",
            Fingerprint = new string('E', 64), ResourceId = mapping.Entity.Id, Revision = 1 });
        receipt.State = EntityState.Added;
        foreach (var (row, fields) in new[] { Row(mapping, "AssuranceMappingCreate"), Row(receipt, "AssuranceMappingCreate") })
        {
            Assert.Equal("Tenant", fields["ScopeKind"].GetProperty("after").GetString());
            Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        }
    }
}
