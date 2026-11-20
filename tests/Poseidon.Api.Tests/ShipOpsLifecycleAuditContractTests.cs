using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-101/T-OPS2-005 and DEV-102/T-OPS2-006 critical-write gate. The lifecycle
// writes must prove before/after through the unchanged
// CriticalWriteCoverageCapture.BeforeAfter rule. CI run 44733601530 found 4 invalid
// rows, one ShipOpsLifecycleEvent per FINALIZE/lifecycle command: the append-only
// event carried BeforeRevision=FromRevision although its Action is Added.
[Trait("Requirement", "DEV-101")]
[Trait("Requirement", "DEV-102")]
public sealed class ShipOpsLifecycleAuditContractTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>(); options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command, string reason = "HUMAN_REVIEW_WORKFLOW")
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
            EntityId = "synthetic", CorrelationId = "synthetic-shipops-correlation", ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }
    private static ShipOpsLifecycleEvent Event(string module, string action, string? duty, string? from, string? to) => new()
    {
        TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), Module = module, RecordId = Guid.NewGuid(), FromRevision = 3, ToRevision = 4, Action = action,
        FromState = from, ToState = to, ActorId = Guid.NewGuid(), RoleId = duty == null ? null : Guid.NewGuid(), AuthorityId = duty == null ? null : Guid.NewGuid(), Duty = duty,
        ReasonCode = "SYNTHETIC", DetailJson = """["voyage_id"]""", RecordHash = new string('A', 64), SavedAt = DateTimeOffset.UnixEpoch
    };

    [Theory]
    [InlineData("PortSof", "FINALIZE", "Master", "DRAFT", "FINAL", "PortSofFinalize")]
    [InlineData("Downtime", "SUBMIT_FOR_REVIEW", null, "NOT_SUBMITTED", "IN_REVIEW", "DowntimeLifecycleCommand")]
    [InlineData("Downtime", "VERIFY", "ChiefEngineer", "IN_REVIEW", "VERIFIED", "DowntimeLifecycleCommand")]
    [InlineData("Downtime", "CLOSE", "ChiefEngineer", "VERIFIED", "CLOSED", "DowntimeLifecycleCommand")]
    public void Appended_lifecycle_event_has_no_before_revision_and_proves_before_after(string module, string action, string? duty, string from, string to, string command)
    {
        using var db = Db();
        var entry = db.Entry(Event(module, action, duty, from, to)); entry.State = EntityState.Added;
        var (row, fields) = Row(entry, command);
        Assert.Equal((Vessel, (long?)null, (long?)4), (row.VesselId!.Value, row.BeforeRevision, row.AfterRevision));
        Assert.Equal("3", fields["Revision"].GetProperty("before").GetString());
        Assert.Equal(action, fields["Action"].GetProperty("after").GetString());
        Assert.Equal("ValuesWithheld", fields["Detail"].GetProperty("evidence").GetString());
        Assert.DoesNotContain("voyage_id", row.ChangeSummary);
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void Sof_finalize_head_update_records_closed_state_and_proves_before_after()
    {
        using var db = Db();
        var head = new ShipOperation { TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), Kind = "Port", Revision = 3, Version = 17, SofState = null };
        var entry = db.Attach(head); head.Revision = 4; head.SofState = "FINAL"; head.FinalRevision = 4; entry.DetectChanges();
        var (row, fields) = Row(entry, "PortSofFinalize");
        Assert.Equal((17u, 3L, 4L), (row.BeforeVersion!.Value, row.BeforeRevision!.Value, row.AfterRevision!.Value));
        Assert.Equal("FINAL", fields["SofState"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void Unknown_lifecycle_action_is_rejected_by_the_closed_projection()
    {
        using var db = Db();
        var entry = db.Entry(Event("Downtime", "APPROVE_CLAIM", null, "IN_REVIEW", "CLOSED")); entry.State = EntityState.Added;
        var error = Assert.Throws<TargetInvocationException>(() => Row(entry, "DowntimeLifecycleCommand"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
