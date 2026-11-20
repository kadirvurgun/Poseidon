using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-111 / T-PMS2-004 (18.01 Backlog row 112): "Drydock timeline; scope items; WO/defect/survey/cert links;
// readiness and evidence; supplier/budget remain external/integration-only." 11.01 sheet 2316 rows 1542-1559
// (drydock-planning fields: project, planned start/finish, yard, scope item, equipment, work package, class and
// statutory requirement, priority, estimated cost/currency/vendor, status, completion evidence, verification result),
// 13.03 p85, 59.04 p1038/p2378-2400. Links pin the exact target revision; a changed target is STALE, an invisible
// or missing one UNAVAILABLE. Cost/currency/vendor are external reference values only: no totals, approval or
// procurement. Linkage completeness is never a class/flag survey result or a seaworthiness statement.
public static class DrydockPolicy
{
    public const string SourceVersion = "18.01:row112;11.01:2316/rows1542-1559;13.03:p85;59.04:p1038,p2378-2400";
    public const string Draft = "DRAFT", Planned = "PLANNED", InProgress = "IN_PROGRESS", Completed = "COMPLETED", Verified = "VERIFIED", Cancelled = "CANCELLED";
    public static readonly string[] States = [Draft, Planned, InProgress, Completed, Verified, Cancelled];
    public static readonly string[] Actions = ["PLAN", "REPLAN", "START", "COMPLETE", "CANCEL"];
    public static readonly string[] VerificationDecisions = ["VERIFY", "REJECT"];
    // Structural classification of a scope item by what it links to; not a class/flag catalogue.
    public static readonly string[] Categories = ["CLASS_SURVEY", "STATUTORY_SURVEY", "CERTIFICATE_RENEWAL", "WORK_ORDER", "DEFECT_REPAIR", "OTHER"];
    public static readonly string[] Priorities = ["HIGH", "NORMAL", "LOW"];
    public static readonly string[] LinkKinds = ["WORK_ORDER", "DEFECT", "CERTIFICATE", "SURVEY_CHECKLIST"];
    public static readonly string[] LinkStates = ["CURRENT", "STALE", "UNAVAILABLE", "OVERDUE"];
    public static readonly string[] ItemStates = ["UNLINKED", "LINKED", "OVERDUE", "REVIEW_REQUIRED"];
    public const string Incomplete = "INCOMPLETE", ReviewRequired = "REVIEW_REQUIRED", Attention = "ATTENTION", LinksCurrent = "LINKS_CURRENT";
    public const int MaxItems = 200, MaxLinks = 20, MaxEvidence = 50;

    private static bool S(string? v, int max = 200) => TowageExecutionPolicy.Short(v, max);
    private static bool Opt(string? v, int max = 200) => v == null || S(v, max);

    public static bool ValidDraft(DrydockContent? c) {
        if (c?.Items == null || c.External == null || c.CompletionEvidence == null) return false;
        if (c.Items.Length > MaxItems || c.CompletionEvidence.Length > MaxEvidence) return false;
        if (!Opt(c.ProjectReference) || !Opt(c.Yard)) return false;
        var x = c.External;
        if (x.EstimatedCost != null && !Regex.IsMatch(x.EstimatedCost, @"\A[0-9]{1,12}(?:\.[0-9]{1,2})?\z") || x.Currency != null && !Regex.IsMatch(x.Currency, @"\A[A-Z]{3}\z") ||
            !Opt(x.Vendor) || !Opt(x.BudgetReference)) return false;
        var ids = new HashSet<Guid>();
        foreach (var i in c.Items) {
            if (i == null || i.Id == Guid.Empty || !ids.Add(i.Id) || !Categories.Contains(i.Category) || !S(i.Title) || i.EquipmentId == Guid.Empty ||
                !Opt(i.WorkPackage) || !Opt(i.ClassRequirement, 1000) || !Opt(i.StatutoryRequirement, 1000) || i.Priority != null && !Priorities.Contains(i.Priority) ||
                i.Links == null || i.Links.Length > MaxLinks) return false;
            if (i.Links.Any(l => l == null || !LinkKinds.Contains(l.Kind) || l.TargetId == Guid.Empty || l.PinnedRevision < 1)) return false;
            if (i.Links.Select(l => (l.Kind, l.TargetId)).Distinct().Count() != i.Links.Length) return false;
        }
        return c.CompletionEvidence.All(e => e != null && e.Id != Guid.Empty && S(e.Reference, 400) && Opt(e.Note, 1000)) &&
            c.CompletionEvidence.Select(e => e.Id).Distinct().Count() == c.CompletionEvidence.Length;
    }

    public static (string? To, string? Failure) Transition(string state, string action) {
        if (!Actions.Contains(action)) return (null, "validation_failed");
        string? to = (state, action) switch {
            (Draft, "PLAN") => Planned, (Planned, "REPLAN") => Draft, (Planned, "START") => InProgress,
            (InProgress, "COMPLETE") => Completed, (Draft or Planned, "CANCEL") => Cancelled, _ => null };
        return to == null ? (null, "invalid_state") : (to, null);
    }

    public static string[] Blockers(string action, DrydockContent c, DrydockReadiness readiness) {
        var b = new List<string>();
        if (action == "PLAN") {
            if (c.ProjectReference == null) b.Add("project_reference_missing");
            if (c.Yard == null) b.Add("yard_missing");
            if (c.PlannedStart == null) b.Add("planned_start_missing");
            if (c.PlannedFinish == null) b.Add("planned_finish_missing");
            if (c.PlannedStart != null && c.PlannedFinish != null && c.PlannedFinish < c.PlannedStart) b.Add("planned_dates_invalid");
            if (c.Items.Length == 0) b.Add("scope_items_missing");
            if (readiness.Stale > 0) b.Add("link_stale");
            if (readiness.Unavailable > 0) b.Add("link_unavailable");
        }
        if (action == "COMPLETE" && c.CompletionEvidence.Length == 0) b.Add("completion_evidence_missing");
        return [.. b.Order(StringComparer.Ordinal)];
    }

    public static string? VerificationFailure(string state, string decision, Guid verifier, Guid? completedBy, string? reason) {
        if (state != Completed) return "invalid_state";
        if (!VerificationDecisions.Contains(decision) || !TowageExecutionPolicy.Long(reason, 10, 1000)) return "invalid_reason";
        if (verifier == Guid.Empty || verifier == completedBy) return "separation_of_duties";
        return null;
    }

    // One pinned link against the current owning record as the reader may see it.
    public static DrydockLinkStatus Link(DrydockLink link, DrydockTargetFact? target, DateOnly today) {
        if (target == null || !target.Visible || target.CurrentRevision == null) return new(link.Kind, link.TargetId, link.PinnedRevision, null, "UNAVAILABLE", "TargetUnavailable", null);
        if (target.CurrentRevision != link.PinnedRevision) return new(link.Kind, link.TargetId, link.PinnedRevision, target.CurrentRevision, "STALE", "TargetChangedSincePinned", target.Due);
        if (!target.Closed && target.Due is { } due && due < today) return new(link.Kind, link.TargetId, link.PinnedRevision, target.CurrentRevision, "OVERDUE", target.DueReason ?? "DueDatePassed", due);
        return new(link.Kind, link.TargetId, link.PinnedRevision, target.CurrentRevision, "CURRENT", target.Closed ? "TargetClosed" : "TargetOpen", target.Due);
    }

    public static DrydockReadiness Readiness(DrydockContent c, IReadOnlyDictionary<(string Kind, Guid Id), DrydockTargetFact> targets, DateOnly today) {
        var items = c.Items.Select(i => {
            var links = i.Links.Select(l => Link(l, targets.GetValueOrDefault((l.Kind, l.TargetId)), today)).ToArray();
            var state = links.Length == 0 ? "UNLINKED" : links.Any(l => l.State is "STALE" or "UNAVAILABLE") ? "REVIEW_REQUIRED" : links.Any(l => l.State == "OVERDUE") ? "OVERDUE" : "LINKED";
            return new DrydockItemStatus(i.Id, state, links);
        }).ToArray();
        var all = items.SelectMany(i => i.Links).ToArray();
        int stale = all.Count(l => l.State == "STALE"), unavailable = all.Count(l => l.State == "UNAVAILABLE"), overdue = all.Count(l => l.State == "OVERDUE");
        var unlinked = items.Count(i => i.State == "UNLINKED");
        var state = c.Items.Length == 0 || c.PlannedStart == null || c.PlannedFinish == null ? Incomplete :
            stale + unavailable > 0 ? ReviewRequired : unlinked + overdue > 0 ? Attention : LinksCurrent;
        return new(state, items, unlinked, overdue, stale, unavailable);
    }

    public static string Hash(Guid vessel, Guid id, long revision, string state, string contentJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', "poseidon.drydock-package.v1", vessel.ToString("D"), id.ToString("D"),
            revision.ToString(CultureInfo.InvariantCulture), state, contentJson))));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockLink([property: JsonRequired] string Kind, [property: JsonRequired] Guid TargetId, [property: JsonRequired] long PinnedRevision)
{ public override string ToString() => nameof(DrydockLink); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockScopeItem([property: JsonRequired] Guid Id, [property: JsonRequired] string Category, [property: JsonRequired] string Title, Guid? EquipmentId,
    string? WorkPackage, string? ClassRequirement, string? StatutoryRequirement, string? Priority, [property: JsonRequired] DrydockLink[] Links)
{ public override string ToString() => nameof(DrydockScopeItem); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockExternal(string? EstimatedCost, string? Currency, string? Vendor, string? BudgetReference)
{ public override string ToString() => nameof(DrydockExternal); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockEvidence([property: JsonRequired] Guid Id, [property: JsonRequired] string Reference, string? Note)
{ public override string ToString() => nameof(DrydockEvidence); }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockContent(string? ProjectReference, string? Yard, DateOnly? PlannedStart, DateOnly? PlannedFinish, [property: JsonRequired] DrydockExternal External,
    [property: JsonRequired] DrydockScopeItem[] Items, [property: JsonRequired] DrydockEvidence[] CompletionEvidence)
{ public override string ToString() => nameof(DrydockContent); }

public sealed record DrydockTargetFact(string Kind, Guid Id, bool Visible, long? CurrentRevision, DateOnly? Due, bool Closed, string? DueReason = null);
public sealed record DrydockLinkStatus(string Kind, Guid TargetId, long PinnedRevision, long? CurrentRevision, string State, string Reason, DateOnly? Due);
public sealed record DrydockItemStatus(Guid ItemId, string State, DrydockLinkStatus[] Links);
public sealed record DrydockReadiness(string State, DrydockItemStatus[] Items, int Unlinked, int Overdue, int Stale, int Unavailable,
    string Meaning = "LinkageCompletenessOnly;NotASurveyOrClassResult");
