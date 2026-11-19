using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-044 critical-write gate: renewal transitions and supersession prove before/after through the
// unchanged CriticalWriteCoverageCapture.BeforeAfter rule; decision content is withheld from audit.
// Projection-only EF tracking: no connection, no SaveChanges, no audit rows.
[Trait("Requirement", "DEV-044")]
public sealed class CertificateRenewalAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
    private const string PrivateReference = "FICTIONAL-AUTHORITY-REFERENCE-77";
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>(); options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command = "CertificateRenewalTransition", string reason = "REGULATORY_WORKFLOW")
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
            EntityId = "synthetic", CorrelationId = "synthetic-renewal-correlation", ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }
    private static CertificateRenewalRevision Revision(string state, string? transition, string? decision = null) => new()
    {
        TenantId = Tenant, VesselId = Vessel, RenewalId = Guid.NewGuid(), CertificateId = Guid.NewGuid(), CertificateRevision = 1, CertificateVersion = 9, Revision = 3, RecordVersion = 12,
        ContentJson = "{}", Hash = new string('A', 64), SourceVersion = "synthetic", State = state, Transition = transition, DecisionJson = decision, ActorId = Guid.NewGuid(), SavedAt = T0
    };

    [Fact]
    public void Transition_revision_records_closed_state_and_withholds_decision_content()
    {
        using var db = Db();
        var entry = db.Entry(Revision("APPLICATION_RECORDED", "RECORD_APPLICATION", $$"""{"authorityReference":"{{PrivateReference}}"}""")); entry.State = EntityState.Added;
        var (row, fields) = Row(entry);
        Assert.Equal((Vessel, (long?)null, (long?)3), (row.VesselId!.Value, row.BeforeRevision, row.AfterRevision));
        Assert.Equal("APPLICATION_RECORDED", fields["RenewalState"].GetProperty("after").GetString());
        Assert.Equal("RECORD_APPLICATION", fields["Transition"].GetProperty("after").GetString());
        Assert.Equal("ValuesWithheld", fields["Decision"].GetProperty("evidence").GetString());
        Assert.Equal("Vessel", fields["ScopeKind"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain(PrivateReference, row.ChangeSummary);
    }

    [Fact]
    public void Head_revision_increment_and_receipt_are_bound()
    {
        using var db = Db();
        var head = new CertificateRenewal { TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), CertificateId = Guid.NewGuid(), Revision = 2, Version = 21 };
        var entry = db.Attach(head); head.Revision = 3; entry.DetectChanges();
        var (row, fields) = Row(entry);
        Assert.Equal((21u, 2L, 3L), (row.BeforeVersion!.Value, row.BeforeRevision!.Value, row.AfterRevision!.Value));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        var receipt = db.Entry(new CertificateCommandReceipt { TenantId = Tenant, VesselId = Vessel, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString("D"), ResourceId = head.Id,
            CertificateId = head.CertificateId, RenewalId = head.Id, Action = "RenewalTransition", Fingerprint = new string('C', 64), OutcomeJson = "{}", SavedAt = T0 }); receipt.State = EntityState.Added;
        var (receiptRow, receiptFields) = Row(receipt);
        Assert.Equal("CommittedResource", receiptFields["Binding"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(receiptRow, NoCommittedVersion, receiptFields));
    }

    [Fact]
    public void Supersession_records_exact_revisions_without_actor_or_dates()
    {
        using var db = Db();
        var s = new CertificateSupersession { TenantId = Tenant, VesselId = Vessel, CertificateId = Guid.NewGuid(), CertificateRevision = 4, ReplacementCertificateId = Guid.NewGuid(),
            ReplacementRevision = 1, RenewalId = Guid.NewGuid(), RenewalRevision = 8, EffectiveDate = new(2026, 11, 1), ActorId = Guid.NewGuid(), CompletedAt = T0 };
        var entry = db.Entry(s); entry.State = EntityState.Added;
        var (row, fields) = Row(entry);
        Assert.Equal((s.CertificateId, (long?)4), (row.ResourceId!.Value, row.AfterRevision));
        Assert.Equal("8", fields["RenewalRevision"].GetProperty("after").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain(s.ActorId.ToString("D"), row.ChangeSummary);
    }

    [Fact]
    public void Unknown_state_or_transition_is_rejected_instead_of_audited()
    {
        using var db = Db();
        var bad = db.Entry(Revision("AUTO_VERIFIED", null)); bad.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(bad)).InnerException);
        var transition = db.Entry(Revision("VERIFIED", "OCR_VERIFY")); transition.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(transition)).InnerException);
    }
}
