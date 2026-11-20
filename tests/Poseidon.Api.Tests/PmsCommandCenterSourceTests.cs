using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S8 integration: DEV-056 command center consumes the DEV-054 work order state and DEV-110 spare gap
// read contracts. Unit level (no database): mapping, denial and DI wiring. A denied or unavailable
// source stays ACCESS_REQUIRED / UNKNOWN and is never shown as zero or quiet.
[Trait("Requirement", "DEV-056")]
public sealed class PmsCommandCenterSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Vessel = Guid.NewGuid();

    private static TrustedActor Actor(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), ["directory.read", .. permissions], true, false, []);
    private static PmsSourceContext Context(TrustedActor actor) => new(null!, actor, Vessel, Now, CancellationToken.None);
    private static WorkOrderStateFact Wo(string state, string gate, bool closed = false, params string[] missing) =>
        new(Guid.NewGuid(), 3, state, gate, missing, gate == "HUMAN_VERIFIED", closed, Now.AddHours(-1));
    private static SpareGapFact Spare(string stock, string criticality, bool requisition = false) =>
        new(Guid.NewGuid(), 2, "FICTIONAL-PN", "FICTIONAL spare", criticality, stock, stock == "LOW_STOCK" ? "1" : null, stock == "LOW_STOCK" ? "2" : null, requisition);

    private sealed class ThrowingWorkOrders : IWorkOrderStateSource
    {
        public bool Called { get; private set; }
        public Task<WorkOrderStateFact[]> ReadAsync(PoseidonDbContext db, TrustedActor actor, Guid vessel, CancellationToken ct)
        {
            Called = true; throw new InvalidDataException("work_order_execution_unavailable");
        }
    }
    private sealed class ThrowingSpares : ISpareGapSource
    {
        public bool Called { get; private set; }
        public Task<SpareGapFact[]> ReadAsync(PoseidonDbContext db, TrustedActor actor, Guid vessel, CancellationToken ct)
        {
            Called = true; throw new InvalidDataException("critical_spare_unavailable");
        }
    }

    [Fact]
    public void Open_work_orders_are_listed_with_why_and_closed_ones_stay_quiet()
    {
        var result = PmsCommandCenterSourceMapping.WorkOrders([
            Wo("CLOSED", "HUMAN_VERIFIED", true),
            Wo("IN_PROGRESS", "MISSING", false, "PHOTO"),
            Wo("RELEASED", "REVIEW_REQUIRED"),
            Wo("EVIDENCE_SUBMITTED", "PRESENT_PENDING_HUMAN_VERIFICATION"),
            Wo("VERIFIED", "HUMAN_VERIFIED")], Now);
        Assert.True(result.Available);
        Assert.Equal(1, result.QuietCount);
        Assert.Equal(4, result.Items.Length);
        Assert.All(result.Items, i => Assert.NotEqual("CRITICAL", i.Severity));
        Assert.Contains(result.Items, i => i.Severity == "REVIEW" && i.Why.Any(w => w.Code == "REQUIRED_EVIDENCE_UNCONFIGURED"));
        Assert.Contains(result.Items, i => i.Why.Any(w => w.Code == "REQUIRED_EVIDENCE_MISSING" && w.Detail == "PHOTO"));
        Assert.Contains(result.Items, i => i.Why.Any(w => w.Code == "AWAITING_HUMAN_VERIFICATION"));
        Assert.Contains(result.Items, i => i.Why.Any(w => w.Code == "AWAITING_HUMAN_CLOSURE"));
        var section = PmsCommandCenterPolicy.Section("open_work_orders", result.Items, result.QuietCount, result.AsOf);
        Assert.Equal((PmsCommandCenterPolicy.Exceptions, (int?)4, 1), (section.State, section.Count, section.QuietCount));
    }

    [Fact]
    public void Unrecognized_work_order_state_is_review_and_a_bounded_projection_is_unknown()
    {
        var odd = PmsCommandCenterSourceMapping.WorkOrders([Wo("SOMETHING_NEW", "HUMAN_VERIFIED")], Now);
        Assert.Equal("REVIEW", Assert.Single(odd.Items).Severity);
        var full = PmsCommandCenterSourceMapping.WorkOrders(Enumerable.Range(0, PmsCommandCenterSourceMapping.WorkOrderBound)
            .Select(_ => Wo("CLOSED", "HUMAN_VERIFIED", true)).ToArray(), Now);
        Assert.Equal((false, "WORK_ORDER_PROJECTION_LIMIT_EXCEEDED", 0), (full.Available, full.Reason, full.QuietCount));
        var empty = PmsCommandCenterSourceMapping.WorkOrders([], Now);
        Assert.Equal(PmsCommandCenterPolicy.Quiet, PmsCommandCenterPolicy.Section("open_work_orders", empty.Items, empty.QuietCount, empty.AsOf).State);
    }

    [Fact]
    public void Spare_gaps_rank_declared_critical_low_stock_and_never_hide_unknown_stock()
    {
        var result = PmsCommandCenterSourceMapping.SpareGaps([
            Spare("LOW_STOCK", "CRITICAL_DECLARED", requisition: true),
            Spare("LOW_STOCK", "NON_CRITICAL_DECLARED"),
            Spare("LOW_STOCK", "UNKNOWN"),
            Spare("STOCK_UNKNOWN", "CRITICAL_DECLARED"),
            Spare("MINIMUM_UNCONFIGURED", "NON_CRITICAL_DECLARED"),
            Spare("CONSUMPTION_UNRESOLVED", "UNKNOWN")], currentSpares: 9, Now);
        Assert.True(result.Available);
        Assert.Equal(3, result.QuietCount);
        Assert.Equal(["CRITICAL", "ATTENTION", "REVIEW", "REVIEW", "REVIEW", "REVIEW"], result.Items.Select(i => i.Severity));
        Assert.Contains(result.Items[0].Why, w => w.Code == "REQUISITION_PROPOSED_PENDING_HUMAN_DECISION");
        Assert.Contains(result.Items[2].Why, w => w.Code == "SPARE_CRITICALITY_UNKNOWN");
        Assert.All(result.Items.Skip(3), i => Assert.Equal(PmsCommandCenterPolicy.FreshnessUnknown, i.Freshness));
        var section = PmsCommandCenterPolicy.Section("critical_spare_gaps", result.Items, result.QuietCount, result.AsOf);
        Assert.Equal("BLOCKED", PmsCommandCenterPolicy.Readiness([section]));
        var bounded = PmsCommandCenterSourceMapping.SpareGaps([], PmsCommandCenterSourceMapping.SpareBound + 1, Now);
        Assert.Equal((false, "SPARE_PROJECTION_LIMIT_EXCEEDED"), (bounded.Available, bounded.Reason));
    }

    [Fact]
    public async Task Sources_deny_without_the_owning_read_permission_before_reading()
    {
        var workOrders = new ThrowingWorkOrders(); var spares = new ThrowingSpares();
        var wo = await new WorkOrderExecutionPmsSource(workOrders).OpenWorkOrdersAsync(Context(Actor("equipment.read", "maintenance.read")));
        var sp = await new CriticalSparePmsSource(spares).CriticalSpareGapsAsync(Context(Actor("equipment.read", "workorder.read")));
        Assert.Equal((false, true, "WORK_ORDER_ACCESS_REQUIRED"), (wo.Available, wo.AccessDenied, wo.Reason));
        Assert.Equal((false, true, "SPARE_ACCESS_REQUIRED"), (sp.Available, sp.AccessDenied, sp.Reason));
        Assert.False(workOrders.Called); Assert.False(spares.Called);
        var section = PmsCommandCenterPolicy.Unavailable("critical_spare_gaps", sp.Reason!, sp.AccessDenied);
        Assert.Equal((PmsCommandCenterPolicy.AccessRequired, (int?)null), (section.State, section.Count));
    }

    [Fact]
    public async Task Unreadable_work_order_source_content_is_unknown_not_quiet()
    {
        var source = new ThrowingWorkOrders();
        var result = await new WorkOrderExecutionPmsSource(source)
            .OpenWorkOrdersAsync(Context(Actor("equipment.read", "maintenance.read", "workorder.read")));
        Assert.True(source.Called);
        Assert.Equal((false, false, "WORK_ORDER_STATE_SOURCE_UNAVAILABLE"), (result.Available, result.AccessDenied, result.Reason));
    }

    [Fact]
    public async Task Host_wires_the_real_sources_ahead_of_the_unknown_fallbacks()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Test"));
        Assert.IsType<WorkOrderExecutionPmsSource>(host.Services.GetRequiredService<IPmsWorkOrderStateSource>());
        Assert.IsType<CriticalSparePmsSource>(host.Services.GetRequiredService<IPmsSpareGapSource>());
        Assert.Single(host.Services.GetServices<IPmsWorkOrderStateSource>());
        Assert.Single(host.Services.GetServices<IPmsSpareGapSource>());
    }
}
