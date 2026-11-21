using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// S11 integration: real DEV-075 gate readers for owners now present in the bundle. Both read human records only and
// fail closed: missing or in-progress evidence is UNKNOWN (never PASS), an explicit negative human outcome is FAIL.
// The engine still never decides GO; these readers only classify recorded human outcomes.

// REGULATORY <- S11 WP-C DEV-073/093 SME review campaigns. The pilot covers ship and tug, so the latest campaign of
// every vessel per scope must be CLOSED (zero open items); CLOSED_WITH_OPEN_ITEMS is FAIL, OPEN/IN_REVIEW and a scope
// without any campaign are UNKNOWN. Every tenant vessel is a pilot vessel (S11 gate QA N2): a vessel of the scope's kind
// without any campaign is UNKNOWN too, never ignored.
public sealed class RegulatoryReviewPilotGateSource : IPilotGateSource
{
    internal const int MaxCampaigns = 500;
    public string Gate => PilotGatePolicy.Regulatory;

    public async Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) {
        var rows = await db.RegulatoryReviewCampaigns.AsNoTracking().OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Take(MaxCampaigns + 1)
            .Select(c => new CampaignState(c.VesselId, c.Scope, c.State, c.CreatedAt, c.OpenItemsAtClose)).ToArrayAsync(ct);
        var vessels = await db.Vessels.AsNoTracking().OrderBy(v => v.Id).Take(MaxCampaigns + 1)
            .Select(v => new PilotVessel(v.Id, v.Kind == VesselKind.Tug ? RegulatoryReviewPolicy.Tug : RegulatoryReviewPolicy.Ship)).ToArrayAsync(ct);
        return rows.Length > MaxCampaigns || vessels.Length > MaxCampaigns
            ? new(Gate, PilotGatePolicy.Unknown, ["REGULATORY_PROJECTION_LIMIT_EXCEEDED"], "RegulatoryReviewCampaigns")
            : Evaluate(rows, vessels);
    }

    internal sealed record CampaignState(Guid VesselId, string Scope, string State, DateTimeOffset CreatedAt, int? OpenItemsAtClose);
    internal sealed record PilotVessel(Guid Id, string Scope);

    internal static PilotGateInput Evaluate(IReadOnlyCollection<CampaignState> campaigns, IReadOnlyCollection<PilotVessel>? vessels = null) {
        var reasons = new List<string>();
        var fail = false; var unknown = false;
        foreach (var scope in RegulatoryReviewPolicy.Scopes) {
            var latest = campaigns.Where(c => c.Scope == scope).GroupBy(c => c.VesselId)
                .Select(g => g.OrderByDescending(c => c.CreatedAt).First()).ToArray();
            if (latest.Length == 0) { unknown = true; reasons.Add("REGULATORY_REVIEW_MISSING:" + scope); continue; }
            if ((vessels ?? []).Any(v => v.Scope == scope && latest.All(c => c.VesselId != v.Id))) { unknown = true; reasons.Add("REGULATORY_VESSEL_UNREVIEWED:" + scope); }
            foreach (var c in latest) {
                if (c.State == RegulatoryReviewPolicy.Closed) continue;
                if (c.State == RegulatoryReviewPolicy.ClosedWithOpenItems) { fail = true; reasons.Add("REGULATORY_OPEN_ITEMS:" + scope); }
                else if (c.State is RegulatoryReviewPolicy.Open or RegulatoryReviewPolicy.InReview) { unknown = true; reasons.Add("REGULATORY_REVIEW_IN_PROGRESS:" + scope); }
                else { unknown = true; reasons.Add("REGULATORY_STATE_UNRECOGNISED:" + scope); }
            }
        }
        var state = fail ? PilotGatePolicy.Fail : unknown ? PilotGatePolicy.Unknown : PilotGatePolicy.Pass;
        if (state == PilotGatePolicy.Pass) reasons.Add("REGULATORY_REVIEW_CLOSED:SHIP,TUG");
        return new(PilotGatePolicy.Regulatory, state, reasons.Distinct(StringComparer.Ordinal).ToArray(), "RegulatoryReviewCampaigns");
    }
}

// DATA <- S11 WP-A DEV-070 reconciliation reports and data owner sign-offs. Every current (not superseded) vessel
// report must be signed off by the data owner; REJECTED is FAIL, awaiting sign-off or no report at all is UNKNOWN.
// ACCEPTED_WITH_EXCEPTIONS passes only because every open item was dispositioned by the data owner (WP-A rule); the
// exception is kept visible as a reason for the human Product Owner. Every tenant vessel is a pilot vessel (S11 gate QA
// N2): a vessel without a current report is UNKNOWN, never ignored.
public sealed class DataReconciliationPilotGateSource : IPilotGateSource
{
    internal const int MaxReports = 500;
    public string Gate => PilotGatePolicy.Data;

    public async Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) {
        var reports = await db.ReconciliationReports.AsNoTracking().Where(r => r.State != ImportReconciliationPolicy.Superseded)
            .OrderBy(r => r.VesselId).ThenBy(r => r.Id).Take(MaxReports + 1).Select(r => new { r.Id, r.VesselId, r.State, r.SignOffId }).ToArrayAsync(ct);
        var vessels = await db.Vessels.AsNoTracking().OrderBy(v => v.Id).Take(MaxReports + 1).Select(v => v.Id).ToArrayAsync(ct);
        if (reports.Length > MaxReports || vessels.Length > MaxReports) return new(Gate, PilotGatePolicy.Unknown, ["DATA_PROJECTION_LIMIT_EXCEEDED"], "ReconciliationReports");
        var signOffIds = reports.Where(r => r.SignOffId != null).Select(r => r.SignOffId!.Value).ToArray();
        var decisions = await db.ReconciliationSignOffs.AsNoTracking().Where(s => signOffIds.Contains(s.Id))
            .Select(s => new { s.Id, s.ReportId, s.Decision }).ToArrayAsync(ct);
        return Evaluate(reports.Select(r => new ReportState(r.State,
            decisions.SingleOrDefault(d => d.Id == r.SignOffId && d.ReportId == r.Id)?.Decision)).ToArray(),
            vessels.Count(v => reports.All(r => r.VesselId != v)));
    }

    internal sealed record ReportState(string State, string? Decision);

    internal static PilotGateInput Evaluate(IReadOnlyCollection<ReportState> current, int unreportedVessels = 0) {
        if (current.Count == 0) return new(PilotGatePolicy.Data, PilotGatePolicy.Unknown, ["DATA_RECONCILIATION_MISSING"], "ReconciliationReports");
        var reasons = new List<string>();
        var fail = false; var unknown = unreportedVessels > 0;
        if (unknown) reasons.Add("DATA_RECONCILIATION_MISSING_FOR_VESSEL");
        foreach (var r in current) {
            if (r.State == ImportReconciliationPolicy.AwaitingSignOff) { unknown = true; reasons.Add("DATA_SIGN_OFF_PENDING"); }
            else if (r.State != ImportReconciliationPolicy.SignedOff || r.Decision == null) { unknown = true; reasons.Add("DATA_SIGN_OFF_UNRESOLVED"); }
            else if (r.Decision == ImportReconciliationPolicy.Rejected) { fail = true; reasons.Add("DATA_RECONCILIATION_REJECTED"); }
            else if (r.Decision == ImportReconciliationPolicy.AcceptedWithExceptions) reasons.Add("DATA_ACCEPTED_WITH_EXCEPTIONS");
            else if (r.Decision != ImportReconciliationPolicy.Accepted) { unknown = true; reasons.Add("DATA_DECISION_UNRECOGNISED"); }
        }
        var state = fail ? PilotGatePolicy.Fail : unknown ? PilotGatePolicy.Unknown : PilotGatePolicy.Pass;
        if (state == PilotGatePolicy.Pass) reasons.Add("DATA_OWNER_SIGNED_OFF");
        return new(PilotGatePolicy.Data, state, reasons.Distinct(StringComparer.Ordinal).ToArray(), "ReconciliationReports");
    }
}
