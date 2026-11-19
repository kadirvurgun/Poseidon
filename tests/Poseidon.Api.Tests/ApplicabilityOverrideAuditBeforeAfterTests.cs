using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-040/041 critical-write gate: override propose/decide and candidate generation prove before/after through
// the unchanged CriticalWriteCoverageCapture.BeforeAfter rule and never copy the private rationale.
// Projection-only EF tracking: no connection, no SaveChanges, no audit rows.
[Trait("Requirement", "DEV-040")]
public sealed class ApplicabilityOverrideAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
    private const string PrivateRationale = "FICTIONAL private override rationale text";
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command, string reason = "REGULATORY_WORKFLOW")
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(),
            EntityType = entry.Metadata.ClrType.Name, EntityId = "synthetic", CorrelationId = "synthetic-override-correlation",
            ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }
    private static ApplicabilityOverride Head(string state = "IN_REVIEW", long sequence = 1) => new()
    {
        TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), EvaluationId = Guid.NewGuid(), RuleId = Guid.NewGuid(), RuleRevision = 3,
        BaseResult = "APPLICABLE", InputHash = new string('A', 64), RuleSetHash = new string('B', 64), Purpose = "COMPLIANCE_REVIEW",
        Action = "REQUIRE_ADDITIONAL_CONTROL", ReasonCode = "ADDITIONAL_PRECAUTION", Rationale = PrivateRationale, EvidenceIdsJson = "[]",
        ValidFrom = T0, ExpiresAt = T0.AddDays(30), ReviewAt = T0.AddDays(7), AuthorId = Guid.NewGuid(), State = state, Sequence = sequence, CreatedAt = T0, Version = 11
    };

    [Fact]
    public void Proposal_head_and_event_prove_themselves_without_private_rationale()
    {
        using var db = Db();
        var head = Head(); var added = db.Entry(head); added.State = EntityState.Added;
        var (row, fields) = Row(added, "ApplicabilityOverridePropose");
        Assert.Equal((head.Id, Vessel, (long?)null, (long?)1), (row.ResourceId!.Value, row.VesselId!.Value, row.BeforeRevision, row.AfterRevision));
        Assert.Equal("IN_REVIEW", fields["OverrideState"].GetProperty("after").GetString());
        Assert.Equal("ValuesWithheld", fields["Rationale"].GetProperty("evidence").GetString());
        Assert.Equal("Vessel", fields["ScopeKind"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain(PrivateRationale, row.ChangeSummary);
        var ev = db.Entry(new ApplicabilityOverrideEvent { TenantId = Tenant, VesselId = Vessel, OverrideId = head.Id, Sequence = 1, ToState = "IN_REVIEW", EventCode = "PROPOSE",
            Rationale = PrivateRationale, ActorId = head.AuthorId, SavedAt = T0 }); ev.State = EntityState.Added;
        var (eventRow, eventFields) = Row(ev, "ApplicabilityOverridePropose");
        Assert.Equal("PROPOSE", eventFields["EventCode"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(eventRow, NoCommittedVersion, eventFields));
        Assert.DoesNotContain(PrivateRationale, eventRow.ChangeSummary);
    }

    [Fact]
    public void Decision_records_closed_state_before_after_and_sequence()
    {
        using var db = Db();
        var head = Head(); var entry = db.Attach(head);
        head.State = "REVOKED"; head.Sequence = 2; entry.DetectChanges();
        var (row, fields) = Row(entry, "ApplicabilityOverrideDecision");
        Assert.Equal((11u, 1L, 2L), (row.BeforeVersion!.Value, row.BeforeRevision!.Value, row.AfterRevision!.Value));
        Assert.Equal(("IN_REVIEW", "REVOKED"), (fields["OverrideState"].GetProperty("before").GetString(), fields["OverrideState"].GetProperty("after").GetString()));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void Receipt_and_candidate_revision_are_bound_and_allowlisted()
    {
        using var db = Db();
        var receipt = db.Entry(new ComplianceCommandReceipt { TenantId = Tenant, VesselId = Vessel, ActorId = Guid.NewGuid(), Key = "synthetic-key-0001",
            Operation = "ComplianceControlCandidates", ResourceId = Vessel, Fingerprint = new string('C', 64), OutcomeJson = "{}", SavedAt = T0 }); receipt.State = EntityState.Added;
        var (row, fields) = Row(receipt, "ComplianceControlCandidates", "PREPARATION_COMMAND");
        Assert.Equal("CommittedResource", fields["Binding"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        var revision = db.Entry(new ControlEvidenceRevision { TenantId = Tenant, VesselId = Vessel, Kind = "Control", Id = Guid.NewGuid(), Revision = 1,
            ContentJson = "{}", Hash = new string('D', 64), ReasonCode = "REQUIREMENT_CANDIDATE", Lifecycle = "DRAFT", SavedAt = T0 }); revision.State = EntityState.Added;
        var (candidateRow, candidateFields) = Row(revision, "ComplianceControlCandidates", "PREPARATION_COMMAND");
        Assert.Equal("REQUIREMENT_CANDIDATE", candidateFields["ReasonCode"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(candidateRow, NoCommittedVersion, candidateFields));
    }

    [Fact]
    public void Uncontrolled_reason_or_unknown_event_is_rejected_instead_of_audited()
    {
        using var db = Db();
        var head = Head(); head.ReasonCode = "VERIFIED_LEGAL_INTERPRETATION";
        var entry = db.Entry(head); entry.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(entry, "ApplicabilityOverridePropose")).InnerException);
        var ev = db.Entry(new ApplicabilityOverrideEvent { TenantId = Tenant, VesselId = Vessel, OverrideId = Guid.NewGuid(), Sequence = 2, FromState = "IN_REVIEW",
            ToState = "ACTIVE", EventCode = "AUTO_APPROVE", ActorId = Guid.NewGuid(), SavedAt = T0 }); ev.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(ev, "ApplicabilityOverrideDecision")).InnerException);
    }
}
