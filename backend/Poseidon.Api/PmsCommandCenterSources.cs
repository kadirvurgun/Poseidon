using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Api;

// S8 integration (DEV-056 x DEV-054 / DEV-110): the PMS command center reads WorkOrder execution state
// and critical spare gaps through the WP-B read contracts. The adapters keep the command center's
// UNKNOWN / ACCESS_REQUIRED semantics: a denied or unavailable source never becomes zero or quiet.
// The owning readers return an empty set when the caller lacks rights, so each adapter checks the
// owning module's read predicate first; vessel/tenant scope is already enforced by the command center
// read gate (out-of-scope vessel -> 404) and again by the readers themselves.
internal static class PmsCommandCenterSourceMapping
{
    // The WP-B readers bound their result set; reaching the bound means the projection may be partial.
    internal const int WorkOrderBound = 500, SpareBound = 500;
    private static readonly string[] OpenStates = ["RELEASED", "IN_PROGRESS", "EVIDENCE_SUBMITTED", "VERIFIED"];

    // facts: open (non-CLOSED) executions from the WP-B reader; closedCount: current CLOSED executions, counted quiet.
    // A work order that was never released has no execution and is not listed (it is a planned record, not an open job).
    internal static PmsSourceResult WorkOrders(IReadOnlyCollection<WorkOrderStateFact> facts, DateTimeOffset now, int closedCount = 0)
    {
        if (facts.Count >= WorkOrderBound) return PmsSourceResult.Unavailable("WORK_ORDER_PROJECTION_LIMIT_EXCEEDED");
        var items = new List<PmsCommandItem>(); var quiet = Math.Max(0, closedCount);
        foreach (var f in facts)
        {
            if (f.Closed || f.State == "CLOSED") { quiet++; continue; }
            if (!OpenStates.Contains(f.State, StringComparer.Ordinal))
            {
                // Unknown execution state: shown for review, never treated as closed/healthy.
                items.Add(new("WorkOrder", f.WorkOrderId, f.ExecutionRevision, "REVIEW", [new("WORK_ORDER_STATE_UNRECOGNIZED")], f.UpdatedAt, PmsCommandCenterPolicy.FreshnessUnknown));
                continue;
            }
            var why = new List<PmsWhy> { new("WORK_ORDER_OPEN", f.State) };
            var severity = "ATTENTION";
            switch (f.EvidenceGate)
            {
                case "REVIEW_REQUIRED": severity = "REVIEW"; why.Add(new("REQUIRED_EVIDENCE_UNCONFIGURED")); break;
                case "MISSING": why.Add(new("REQUIRED_EVIDENCE_MISSING", f.MissingEvidence.Length == 0 ? null : string.Join(",", f.MissingEvidence))); break;
                case "PRESENT_PENDING_HUMAN_VERIFICATION": why.Add(new("AWAITING_HUMAN_VERIFICATION")); break;
                case "HUMAN_VERIFIED": why.Add(new(f.State == "VERIFIED" ? "AWAITING_HUMAN_CLOSURE" : "EVIDENCE_HUMAN_VERIFIED")); break;
                default: severity = "REVIEW"; why.Add(new("EVIDENCE_GATE_UNRECOGNIZED")); break;
            }
            items.Add(new("WorkOrder", f.WorkOrderId, f.ExecutionRevision, severity, [.. why], f.UpdatedAt, PmsCommandCenterPolicy.Current));
        }
        return new(true, null, [.. items], quiet, now);
    }

    // facts: the WP-B gap set (ABOVE_MINIMUM already filtered out). currentSpares: current critical spare
    // records for the vessel, used for the quiet count and to detect a truncated projection.
    internal static PmsSourceResult SpareGaps(IReadOnlyCollection<SpareGapFact> facts, int currentSpares, DateTimeOffset now)
    {
        if (currentSpares > SpareBound) return PmsSourceResult.Unavailable("SPARE_PROJECTION_LIMIT_EXCEEDED");
        var items = new List<PmsCommandItem>();
        foreach (var f in facts)
        {
            var why = new List<PmsWhy> { new("SPARE_STOCK_STATE", f.StockState) };
            if (f.Criticality == "UNKNOWN") why.Add(new("SPARE_CRITICALITY_UNKNOWN"));
            else if (f.Criticality == "CRITICAL_DECLARED") why.Add(new("SPARE_DECLARED_CRITICAL"));
            if (f.OpenRequisition) why.Add(new("REQUISITION_PROPOSED_PENDING_HUMAN_DECISION"));
            string severity; string freshness;
            if (f.StockState == "LOW_STOCK")
            {
                if (f.Minimum != null) why.Add(new("MINIMUM_QUANTITY", f.Minimum));
                if (f.Projected != null) why.Add(new("PROJECTED_QUANTITY", f.Projected));
                severity = f.Criticality switch { "CRITICAL_DECLARED" => "CRITICAL", "NON_CRITICAL_DECLARED" => "ATTENTION", _ => "REVIEW" };
                freshness = PmsCommandCenterPolicy.Current;
            }
            else
            {
                // STOCK_UNKNOWN / MINIMUM_UNCONFIGURED / CONSUMPTION_UNRESOLVED or an unrecognized state.
                severity = "REVIEW"; freshness = PmsCommandCenterPolicy.FreshnessUnknown;
            }
            items.Add(new("CriticalSpare", f.SpareId, f.Revision, severity, [.. why], null, freshness));
        }
        return new(true, null, [.. items], Math.Max(0, currentSpares - facts.Count), now);
    }
}

internal sealed class WorkOrderExecutionPmsSource(IWorkOrderStateSource source) : IPmsWorkOrderStateSource
{
    public async Task<PmsSourceResult> OpenWorkOrdersAsync(PmsSourceContext context)
    {
        if (!WorkOrderEndpoints.CanRead(context.Actor)) return PmsSourceResult.Denied("WORK_ORDER_ACCESS_REQUIRED");
        try
        {
            var facts = await source.ReadAsync(context.Db, context.Actor, context.VesselId, context.Ct);
            var closed = await context.Db.WorkOrderExecutions.AsNoTracking().CountAsync(x => x.VesselId == context.VesselId && x.State == "CLOSED", context.Ct);
            return PmsCommandCenterSourceMapping.WorkOrders(facts, context.Now, closed);
        }
        catch (Exception e) when (e is InvalidDataException or JsonException) { return PmsSourceResult.Unavailable("WORK_ORDER_STATE_SOURCE_UNAVAILABLE"); }
    }
}

internal sealed class CriticalSparePmsSource(ISpareGapSource source) : IPmsSpareGapSource
{
    public async Task<PmsSourceResult> CriticalSpareGapsAsync(PmsSourceContext context)
    {
        if (!CriticalSpareEndpoints.CanRead(context.Actor)) return PmsSourceResult.Denied("SPARE_ACCESS_REQUIRED");
        try
        {
            var current = await context.Db.CriticalSpares.AsNoTracking().CountAsync(x => x.VesselId == context.VesselId, context.Ct);
            if (current > PmsCommandCenterSourceMapping.SpareBound) return PmsSourceResult.Unavailable("SPARE_PROJECTION_LIMIT_EXCEEDED");
            return PmsCommandCenterSourceMapping.SpareGaps(await source.ReadAsync(context.Db, context.Actor, context.VesselId, context.Ct), current, context.Now);
        }
        catch (Exception e) when (e is InvalidDataException or JsonException) { return PmsSourceResult.Unavailable("SPARE_GAP_SOURCE_UNAVAILABLE"); }
    }
}
