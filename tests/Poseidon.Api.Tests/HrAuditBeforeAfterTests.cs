using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5201 CI gate regression (run 44397425390): every HR command audit row must prove its
// before/after through the unchanged CriticalWriteCoverageCapture.BeforeAfter rule. Projection-only
// EF tracking: no connection opens, no SaveChanges, no persisted audit rows or runtime coverage evidence.
[Trait("Requirement", "PLAN-1511-5201")]
public sealed class HrAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Correlation = "synthetic-hr-correlation";

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }

    // Mirrors the audited SaveChanges row construction for a non-receipt entry.
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command)
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entry, null, new AuditCommandContext(command, "PREPARATION_COMMAND"), true])!;
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

    private static bool Proven(EntityEntry entry, string command, Guid receiptResource)
    {
        var (row, fields) = Row(entry, command);
        // The receipt row of the same command carries the committed xmin of its resource.
        var committed = new HashSet<(string, Guid, Guid?)> { (Correlation, Tenant, receiptResource) };
        return CriticalWriteCoverageCapture.BeforeAfter(row, committed, fields);
    }

    private static LegalEntityIdentifier Identifier(Guid company) => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), LegalEntityId = company, Type = "TradeRegistry", Issuer = "SYNTHETIC", Value = "FICTIONAL-990011",
        SourceKind = "Synthetic", CreatedBy = "synthetic", CreatedAt = T0, Version = 7
    };

    private static LegalEntityRole Role(Guid company) => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), LegalEntityId = company, Role = "Employer", EffectiveFrom = T0, SourceKind = "Synthetic",
        CreatedBy = "synthetic", CreatedAt = T0, Version = 9
    };

    private static HrStateChange StateChange(Guid company, string kind, Guid subject, string from, string to) => new()
    {
        TenantId = Tenant, Id = Guid.NewGuid(), LegalEntityId = company, SubjectKind = kind, SubjectId = subject, FromState = from, ToState = to,
        Reason = "FICTIONAL reason", ActorId = "synthetic", RecordedAt = T0
    };

    [Fact]
    public void Identifier_and_role_rows_are_proven_by_their_own_committed_receipt()
    {
        using var db = Db();
        var company = Guid.NewGuid();
        var added = Identifier(company);
        var addedEntry = db.Entry(added); addedEntry.State = EntityState.Added;
        Assert.True(Proven(addedEntry, "HrAddLegalEntityIdentifier", added.Id));
        // The owning legal entity is not the identifier resource: a receipt for another resource proves nothing.
        Assert.False(Proven(addedEntry, "HrAddLegalEntityIdentifier", company));

        var retired = Identifier(company);
        var retiredEntry = db.Attach(retired);
        retired.RetiredAt = T0; retired.RetiredBy = "synthetic";
        retiredEntry.DetectChanges();
        Assert.Equal(EntityState.Modified, retiredEntry.State);
        Assert.True(Proven(retiredEntry, "HrRetireLegalEntityIdentifier", retired.Id));

        var role = Role(company);
        var roleEntry = db.Entry(role); roleEntry.State = EntityState.Added;
        Assert.True(Proven(roleEntry, "HrAddLegalEntityRole", role.Id));
        var ended = Role(company);
        var endedEntry = db.Attach(ended);
        ended.EffectiveTo = T0.AddDays(30); ended.EndedBy = "synthetic";
        endedEntry.DetectChanges();
        Assert.True(Proven(endedEntry, "HrEndLegalEntityRole", ended.Id));
        var cancelled = Role(company);
        var cancelledEntry = db.Attach(cancelled);
        cancelled.State = "Cancelled"; cancelled.CancelledBy = "synthetic"; cancelled.CancelledAt = T0;
        cancelledEntry.DetectChanges();
        Assert.True(Proven(cancelledEntry, "HrCancelLegalEntityRole", cancelled.Id));
        Assert.DoesNotContain("FICTIONAL", Row(addedEntry, "HrAddLegalEntityIdentifier").Row.ChangeSummary);
    }

    [Theory]
    [InlineData("LegalEntityIdentifier", "Current", "Retired", "HrRetireLegalEntityIdentifier")]
    [InlineData("LegalEntityRole", "Recorded", "Ended", "HrEndLegalEntityRole")]
    [InlineData("LegalEntityRole", "Recorded", "Cancelled", "HrCancelLegalEntityRole")]
    [InlineData("Employment", "Draft", "Active", "HrChangeEmploymentStatus")]
    [InlineData("PlannedAssignment", "Planned", "Approved", "HrChangeAssignmentStatus")]
    public void Append_only_state_changes_bind_to_their_owner_without_copying_the_reason(string kind, string from, string to, string command)
    {
        using var db = Db();
        var company = Guid.NewGuid();
        var change = StateChange(company, kind, Guid.NewGuid(), from, to);
        var entry = db.Entry(change); entry.State = EntityState.Added;
        // No receipt version for the owning legal entity: the closed owner binding alone must prove the immutable row.
        Assert.True(Proven(entry, command, Guid.NewGuid()));
        var (row, _) = Row(entry, command);
        Assert.Equal(company, row.ResourceId);
        Assert.DoesNotContain("FICTIONAL", row.ChangeSummary);
    }

    [Fact]
    public void Actual_event_rows_bind_to_their_owner()
    {
        using var db = Db();
        var fact = new HrActualEvent
        {
            TenantId = Tenant, Id = Guid.NewGuid(), AssignmentId = Guid.NewGuid(), PersonId = Guid.NewGuid(), Kind = "CheckIn", OccurredAt = T0,
            OriginZoneId = "Europe/Istanbul", Origin = "SupervisorReported", SourceSystem = "FICTIONAL-SRC", SourceEventId = "FICTIONAL-EVT",
            ContentHash = new string('a', 64), Quality = "Valid", Disposition = "Accepted", RecordedBy = "synthetic", RecordedAt = T0
        };
        var entry = db.Entry(fact); entry.State = EntityState.Added;
        Assert.True(Proven(entry, "HrRecordActualEvent", fact.Id));
        Assert.True(Proven(entry, "HrRecordActualEvent", Guid.NewGuid()));
        Assert.DoesNotContain("FICTIONAL", Row(entry, "HrRecordActualEvent").Row.ChangeSummary);
    }

    [Theory]
    [InlineData("update", 3L, 4L)]
    [InlineData("status", 4L, 5L)]
    [InlineData("touch", 0L, 0L)]
    public void Hr_legal_entity_company_rows_carry_revision_and_closed_status_without_values(string scenario, long before, long after)
    {
        using var db = Db();
        var company = new Company
        {
            TenantId = Tenant, Id = Guid.NewGuid(), Code = "FICTIONAL-CODE", Name = "FICTIONAL company", Version = 11,
            LegalName = "FICTIONAL legal name", CountryCode = "TR", LegalEntityStatus = "Draft", LegalEntityRevision = before
        };
        var entry = db.Attach(company);
        if (scenario == "update") company.LegalName = "FICTIONAL corrected legal name";
        if (scenario == "status") company.LegalEntityStatus = "Active";
        company.LegalEntityRevision = after;
        entry.Property(x => x.LegalEntityRevision).IsModified = true;
        entry.DetectChanges();
        var (row, fields) = Row(entry, scenario == "status" ? "HrChangeLegalEntityStatus" : "HrUpdateLegalEntity");
        Assert.Equal(before, row.BeforeRevision);
        Assert.Equal(after, row.AfterRevision);
        Assert.Equal(11u, row.BeforeVersion);
        Assert.Equal(scenario == "status" ? "Active" : "Draft", fields["LegalEntityStatus"].GetProperty("after").GetString());
        Assert.Equal(scenario == "update", fields.ContainsKey("LegalName"));
        Assert.DoesNotContain("FICTIONAL", row.ChangeSummary);
        // A revision-bumping edit proves itself; an unchanged xmin touch is proven by the committed company receipt.
        Assert.Equal(after > 0, CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), fields));
        Assert.True(Proven(entry, "HrUpdateLegalEntity", company.Id));
    }

    [Fact]
    public void Non_hr_company_rows_keep_the_generic_projection()
    {
        using var db = Db();
        var company = new Company { TenantId = Tenant, Id = Guid.NewGuid(), Code = "FICTIONAL-CODE", Name = "FICTIONAL company", Version = 11 };
        var entry = db.Attach(company);
        company.Name = "FICTIONAL renamed";
        entry.DetectChanges();
        var (row, fields) = Row(entry, "UpdateCompany");
        Assert.True(fields.ContainsKey("Name"));
        Assert.False(fields.ContainsKey("LegalEntityStatus"));
        Assert.Null(row.AfterRevision);
        Assert.True(Proven(entry, "UpdateCompany", company.Id));
    }
}
