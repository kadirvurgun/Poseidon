using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-028 critical-write gate: the lifecycle transition and the CONTENT_REVISED reset must prove
// before/after through the unchanged CriticalWriteCoverageCapture.BeforeAfter rule, and never copy
// the private rationale. Projection-only EF tracking: no connection, no SaveChanges, no audit rows.
[Trait("Requirement", "DEV-028")]
public sealed class ControlLifecycleAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
    private const string Correlation = "synthetic-control-lifecycle-correlation";
    private const string Command = "ControlLifecycleTransition";
    private const string Reason = "REGULATORY_WORKFLOW";
    private const string PrivateRationale = "FICTIONAL private lifecycle rationale";
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }

    // Mirrors the audited SaveChanges row construction for a non-receipt entry.
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command = Command, string reason = Reason)
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(),
            EntityType = entry.Metadata.ClrType.Name, EntityId = "synthetic", CorrelationId = Correlation,
            ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }

    private static ControlStateEvent Event(Guid control, string reason, string from, string to) => new()
    {
        TenantId = Tenant, VesselId = Vessel, ControlId = control, Revision = 3, ControlRevision = 4, FromState = from, ToState = to,
        ReasonCode = reason, Rationale = PrivateRationale, ActorId = Guid.NewGuid(), RoleContext = Guid.NewGuid().ToString("D"), SavedAt = T0
    };

    [Theory]
    [InlineData("SUBMIT_FOR_REVIEW", "DRAFT", "IN_REVIEW")]
    [InlineData("APPROVE", "IN_REVIEW", "APPROVED")]
    [InlineData("PUBLISH", "APPROVED", "PUBLISHED")]
    [InlineData("RETIRE", "PUBLISHED", "RETIRED")]
    [InlineData("CONTENT_REVISED", "PUBLISHED", "DRAFT")]
    public void Append_only_lifecycle_events_prove_themselves_without_private_rationale(string reason, string from, string to)
    {
        using var db = Db();
        var control = Guid.NewGuid();
        var entry = db.Entry(Event(control, reason, from, to)); entry.State = EntityState.Added;
        var (row, fields) = Row(entry);
        Assert.Equal(control, row.ResourceId); Assert.Equal(Vessel, row.VesselId);
        Assert.Null(row.BeforeRevision); Assert.Equal(3L, row.AfterRevision);
        Assert.Equal((null as string, to), (fields["ToState"].GetProperty("before").GetString(), fields["ToState"].GetProperty("after").GetString()));
        Assert.Equal(reason, fields["ReasonCode"].GetProperty("after").GetString());
        Assert.Equal("Vessel", fields["ScopeKind"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain(PrivateRationale, row.ChangeSummary);
    }

    [Theory]
    [InlineData("DRAFT", "IN_REVIEW", false)]
    [InlineData("APPROVED", "PUBLISHED", false)]
    [InlineData("PUBLISHED", "DRAFT", true)]
    public void Head_state_change_records_closed_before_after_and_revision(string from, string to, bool revised)
    {
        using var db = Db();
        var head = new ControlEvidenceHead { TenantId = Tenant, VesselId = Vessel, Kind = "Control", Id = Guid.NewGuid(), Revision = 4, Version = 17, ControlState = from };
        var entry = db.Attach(head);
        head.ControlState = to;
        if (revised) head.Revision = 5;
        entry.DetectChanges();
        Assert.Equal(EntityState.Modified, entry.State);
        var (row, fields) = Row(entry);
        Assert.Equal(head.Id, row.ResourceId); Assert.Equal(17u, row.BeforeVersion);
        Assert.Equal(4L, row.BeforeRevision); Assert.Equal(revised ? 5L : 4L, row.AfterRevision);
        Assert.Equal((from, to), (fields["ControlState"].GetProperty("before").GetString(), fields["ControlState"].GetProperty("after").GetString()));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    // 62.01 section 6: a successor PUBLISH records the superseded revision on the event and the pointer move on the head.
    [Fact]
    public void Successor_publication_audits_supersession_and_pointer_before_after()
    {
        using var db = Db();
        var control = Guid.NewGuid();
        var ev = Event(control, "PUBLISH", "APPROVED", "PUBLISHED"); ev.SupersedesRevision = 2;
        var added = db.Entry(ev); added.State = EntityState.Added;
        var (eventRow, eventFields) = Row(added);
        Assert.Equal("2", eventFields["SupersedesRevision"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(eventRow, NoCommittedVersion, eventFields));
        var head = new ControlEvidenceHead { TenantId = Tenant, VesselId = Vessel, Kind = "Control", Id = control, Revision = 4, Version = 17, ControlState = "APPROVED", PublishedRevision = 2 };
        var entry = db.Attach(head);
        head.ControlState = "PUBLISHED"; head.PublishedRevision = 4;
        entry.DetectChanges();
        var (row, fields) = Row(entry);
        Assert.Equal(("2", "4"), (fields["PublishedRevision"].GetProperty("before").GetString(), fields["PublishedRevision"].GetProperty("after").GetString()));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    // A never-transitioned control stores NULL; its first lifecycle change audits the derived DRAFT before-value.
    [Fact]
    public void First_transition_audits_the_derived_draft_before_value()
    {
        using var db = Db();
        var head = new ControlEvidenceHead { TenantId = Tenant, VesselId = Vessel, Kind = "Control", Id = Guid.NewGuid(), Revision = 2, Version = 9 };
        var entry = db.Attach(head);
        head.ControlState = "IN_REVIEW";
        entry.DetectChanges();
        var (row, fields) = Row(entry);
        Assert.Equal(("DRAFT", "IN_REVIEW"), (fields["ControlState"].GetProperty("before").GetString(), fields["ControlState"].GetProperty("after").GetString()));
        Assert.False(fields.ContainsKey("PublishedRevision"));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void A_supersession_on_a_non_publish_event_is_rejected()
    {
        using var db = Db();
        var ev = Event(Guid.NewGuid(), "APPROVE", "IN_REVIEW", "APPROVED"); ev.SupersedesRevision = 2;
        var entry = db.Entry(ev); entry.State = EntityState.Added;
        var error = Assert.Throws<TargetInvocationException>(() => Row(entry));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void Unknown_lifecycle_values_are_rejected_instead_of_audited()
    {
        using var db = Db();
        var entry = db.Entry(Event(Guid.NewGuid(), "ACTIVATE", "DRAFT", "ACTIVE")); entry.State = EntityState.Added;
        var error = Assert.Throws<TargetInvocationException>(() => Row(entry));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
