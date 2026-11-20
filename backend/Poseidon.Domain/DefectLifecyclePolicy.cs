using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// DEV-055 / T-S8-004 (18.01 Backlog row 56): Detect -> Assess -> Safeguard -> Action -> Verify ->
// Effectiveness -> Closed. Every step is an append-only human record. The engine only checks
// order, completeness and separation of duties; it never verifies, accepts effectiveness or
// returns equipment to service by itself. 14.02/11.01 row 464 (rts_verification) is a
// Controlled Decision of the Chief Engineer / technical owner, so closure is a separate
// human permission (defect.close). ISM Code 10.3 (critical equipment) supports treating a
// declared OR assessed Critical defect as critical; criticality catalogs are not invented.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DefectLifecycleCommand([property: JsonRequired] long ExpectedRevision, string? Criticality = null, string? MeasureKind = null,
    string? Text = null, bool? Temporary = null, string? ExpiresAt = null, string? Outcome = null, long? DefectRevision = null)
{
    public override string ToString() => nameof(DefectLifecycleCommand);
}

public sealed record DefectLifecycleEntry(Guid Id, long Sequence, string Kind, string StageAfter, long DefectRevision, string? Criticality,
    string? MeasureKind, bool Temporary, DateTimeOffset? ExpiresAt, Guid? MeasureId, string? Outcome, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record DefectMeasureView(Guid Id, string Kind, bool Temporary, DateTimeOffset? ExpiresAt, string State, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record DefectLifecycleAlert(string Code, bool Blocking, Guid? MeasureId, DateTimeOffset? At);
public sealed record DefectLifecycleEvaluation(string Stage, bool Critical, string? AssessedCriticality, DefectMeasureView[] Measures,
    DefectLifecycleAlert[] Alerts, string[] CloseBlockers, Guid[] ActionOwners, DateTimeOffset AssessedAt, bool AwaitingVerification = false);
public sealed record DefectLifecycleDecision(int Status, string? Code, string StageAfter = "", string[]? Missing = null)
{
    public static DefectLifecycleDecision Ok(string stage) => new(200, null, stage);
    public static DefectLifecycleDecision Fail(int status, string code, string[]? missing = null) => new(status, code, "", missing);
}

public static class DefectLifecyclePolicy
{
    public const string Detect = "DETECT", Assess = "ASSESS", Safeguard = "SAFEGUARD", Action = "ACTION", Verify = "VERIFY",
        Effectiveness = "EFFECTIVENESS", Closed = "CLOSED";
    public static readonly string[] Stages = [Detect, Assess, Safeguard, Action, Verify, Effectiveness, Closed];
    public static readonly string[] Kinds = ["START", "ASSESS", "SAFEGUARD", "LIFT", "ACTION", "VERIFY", "EFFECTIVENESS", "CLOSE"];
    public static readonly string[] Criticalities = ["Critical", "High", "Medium", "Low"];
    public static readonly string[] MeasureKinds = ["SAFEGUARD", "RESTRICTION"];
    public static readonly string[] VerifyOutcomes = ["VERIFIED", "NOT_VERIFIED"];
    public static readonly string[] EffectivenessOutcomes = ["EFFECTIVE", "NOT_EFFECTIVE"];
    public const string ClosureOutcome = "RETURNED_TO_SERVICE";
    public const int MinStatement = 10, MaxText = 4000;

    public const string VerifyPermission = "defect.verify";
    public const string EffectivenessPermission = "defect.effectiveness";
    public const string ClosePermission = "defect.close";

    private static bool Text(string? value, int min = 1) => value != null && value.Trim().Length >= min && value.Length <= MaxText &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));

    // Closed per-kind command shape. Fields that do not belong to the kind are rejected, not ignored.
    public static bool ValidShape(string kind, DefectLifecycleCommand c, DateTimeOffset now, out DateTimeOffset? expiresAt)
    {
        expiresAt = null;
        if (c.ExpectedRevision < 0) return false;
        bool None(params object?[] values) => values.All(v => v == null);
        switch (kind)
        {
            case "START":
                return c.ExpectedRevision == 0 && c.DefectRevision is > 0 && None(c.Criticality, c.MeasureKind, c.Text, c.Temporary, c.ExpiresAt, c.Outcome);
            case "ASSESS":
                return c.ExpectedRevision > 0 && c.Criticality != null && Criticalities.Contains(c.Criticality, StringComparer.Ordinal) && Text(c.Text, MinStatement) &&
                    None(c.MeasureKind, c.Temporary, c.ExpiresAt, c.Outcome, c.DefectRevision);
            case "SAFEGUARD":
                if (c.ExpectedRevision <= 0 || c.MeasureKind == null || !MeasureKinds.Contains(c.MeasureKind, StringComparer.Ordinal) || !Text(c.Text) ||
                    c.Temporary == null || !None(c.Criticality, c.Outcome, c.DefectRevision)) return false;
                if (c.Temporary == false) return c.ExpiresAt == null;
                // A temporary measure without a deadline cannot be monitored; an already-expired one protects nothing.
                if (!DefectPolicy.TryInstant(c.ExpiresAt, out var due) || due <= now) return false;
                expiresAt = due; return true;
            case "LIFT":
            case "ACTION":
                return c.ExpectedRevision > 0 && Text(c.Text, kind == "LIFT" ? MinStatement : 1) &&
                    None(c.Criticality, c.MeasureKind, c.Temporary, c.ExpiresAt, c.Outcome, c.DefectRevision);
            case "VERIFY":
                return c.ExpectedRevision > 0 && c.Outcome != null && VerifyOutcomes.Contains(c.Outcome, StringComparer.Ordinal) && Text(c.Text, MinStatement) &&
                    None(c.Criticality, c.MeasureKind, c.Temporary, c.ExpiresAt, c.DefectRevision);
            case "EFFECTIVENESS":
                return c.ExpectedRevision > 0 && c.Outcome != null && EffectivenessOutcomes.Contains(c.Outcome, StringComparer.Ordinal) && Text(c.Text, MinStatement) &&
                    None(c.Criticality, c.MeasureKind, c.Temporary, c.ExpiresAt, c.DefectRevision);
            case "CLOSE":
                return c.ExpectedRevision > 0 && Text(c.Text, MinStatement) &&
                    None(c.Criticality, c.MeasureKind, c.Temporary, c.ExpiresAt, c.Outcome, c.DefectRevision);
            default: return false;
        }
    }

    // Evaluated on every read with the server clock: expiry monitoring never depends on a scheduler.
    public static DefectLifecycleEvaluation Evaluate(IReadOnlyList<DefectLifecycleEntry> records, string? declaredCriticality,
        long currentDefectRevision, DateTimeOffset serverNow)
    {
        var now = serverNow.ToUniversalTime();
        var ordered = records.OrderBy(r => r.Sequence).ToArray();
        var stage = ordered.Length == 0 ? Detect : ordered[^1].StageAfter;
        var assessed = ordered.LastOrDefault(r => r.Kind == "ASSESS")?.Criticality;
        // Conservative: either a declared or an assessed Critical makes the closure rule apply.
        var critical = assessed == "Critical" || declaredCriticality == "Critical";
        var lifted = ordered.Where(r => r.Kind == "LIFT" && r.MeasureId != null).Select(r => r.MeasureId!.Value).ToHashSet();
        var closed = stage == Closed;
        var measures = ordered.Where(r => r.Kind == "SAFEGUARD").Select(r => new DefectMeasureView(r.Id, r.MeasureKind ?? "SAFEGUARD", r.Temporary, r.ExpiresAt,
            lifted.Contains(r.Id) ? "LIFTED" : closed ? "ENDED_AT_CLOSURE" : r.Temporary && r.ExpiresAt <= now ? "EXPIRED" : "ACTIVE", r.ActorId, r.RecordedAt)).ToArray();
        var lastAction = ordered.LastOrDefault(r => r.Kind == "ACTION")?.Sequence ?? 0;
        // A failed effectiveness review invalidates the verification of that cycle as well.
        var reset = ordered.LastOrDefault(r => r.Kind == "ACTION" || r.Kind == "EFFECTIVENESS" && r.Outcome == "NOT_EFFECTIVE")?.Sequence ?? 0;
        var verified = ordered.LastOrDefault(r => r.Kind == "VERIFY" && r.Sequence > reset);
        // Verification needs a corrective action recorded after the last verification/effectiveness outcome.
        var awaiting = ordered.LastOrDefault(r => r.Kind is "ACTION" or "VERIFY" or "EFFECTIVENESS")?.Kind == "ACTION";
        var effective = ordered.LastOrDefault(r => r.Kind == "EFFECTIVENESS" && verified != null && r.Sequence > verified.Sequence);
        var owners = ordered.Where(r => r.Kind == "ACTION").Select(r => r.ActorId).Distinct().Order().ToArray();
        var alerts = new List<DefectLifecycleAlert>();
        if (!closed)
        {
            foreach (var m in measures.Where(m => m.State == "EXPIRED")) alerts.Add(new("TEMPORARY_MEASURE_EXPIRED", true, m.Id, m.ExpiresAt));
            if ((critical || assessed == null && declaredCriticality is not ("High" or "Medium" or "Low")) && !measures.Any(m => m.State == "ACTIVE"))
                alerts.Add(new(critical ? "CRITICAL_DEFECT_WITHOUT_ACTIVE_SAFEGUARD" : "CRITICALITY_UNKNOWN_WITHOUT_SAFEGUARD", true, null, null));
            if (assessed == null) alerts.Add(new("CRITICALITY_NOT_ASSESSED", false, null, null));
        }
        var blockers = new List<string>();
        if (assessed == null) blockers.Add("CRITICALITY_NOT_ASSESSED");
        if (critical && measures.Length == 0) blockers.Add("CRITICAL_SAFEGUARD_NOT_RECORDED");
        if (lastAction == 0) blockers.Add("CORRECTIVE_ACTION_NOT_RECORDED");
        if (verified?.Outcome != "VERIFIED") blockers.Add("VERIFICATION_NOT_RECORDED");
        if (effective?.Outcome != "EFFECTIVE") blockers.Add("EFFECTIVENESS_NOT_RECORDED");
        if (verified != null && verified.DefectRevision != currentDefectRevision) blockers.Add("DECLARATION_CHANGED_SINCE_VERIFICATION");
        if (measures.Any(m => m.State == "EXPIRED")) blockers.Add("TEMPORARY_MEASURE_EXPIRED");
        if (closed) blockers.Clear();
        return new(stage, critical, assessed, measures, [.. alerts], [.. blockers], owners, now, awaiting);
    }

    // Order, authority and separation of duties for one new record. Permissions are server
    // trusted (ActorResolver); the actor id is the authenticated subject, never a request field.
    public static DefectLifecycleDecision Decide(string kind, DefectLifecycleEvaluation e, bool started, Guid actor,
        IEnumerable<string> permissions, DefectLifecycleCommand c, Guid? measureId = null)
    {
        if (kind == "START") return started ? DefectLifecycleDecision.Fail(409, "defect_lifecycle_exists") : DefectLifecycleDecision.Ok(Detect);
        if (!started) return DefectLifecycleDecision.Fail(409, "invalid_state");
        if (e.Stage == Closed) return DefectLifecycleDecision.Fail(409, "invalid_state");
        bool In(params string[] stages) => stages.Contains(e.Stage, StringComparer.Ordinal);
        var owner = e.ActionOwners.Contains(actor);
        var active = e.Measures.Any(m => m.State == "ACTIVE");
        switch (kind)
        {
            case "ASSESS":
                return In(Detect, Assess, Safeguard, Action) ? DefectLifecycleDecision.Ok(e.Stage == Detect ? Assess : e.Stage) : DefectLifecycleDecision.Fail(409, "invalid_state");
            case "SAFEGUARD":
                return e.AssessedCriticality == null ? DefectLifecycleDecision.Fail(409, "invalid_state")
                    : DefectLifecycleDecision.Ok(e.Stage == Assess ? Safeguard : e.Stage);
            case "LIFT":
            {
                var measure = e.Measures.SingleOrDefault(m => m.Id == measureId);
                if (measure == null) return DefectLifecycleDecision.Fail(404, "resource_not_found");
                if (measure.State is not ("ACTIVE" or "EXPIRED")) return DefectLifecycleDecision.Fail(409, "invalid_state");
                // A critical defect may not lose its last active protection before effectiveness is recorded.
                if (e.Critical && e.Stage != Effectiveness && measure.State == "ACTIVE" && e.Measures.Count(m => m.State == "ACTIVE") == 1)
                    return DefectLifecycleDecision.Fail(422, "defect_safeguard_required");
                return DefectLifecycleDecision.Ok(e.Stage);
            }
            case "ACTION":
                if (In(Detect) || e.AssessedCriticality == null) return DefectLifecycleDecision.Fail(409, "invalid_state");
                if (e.Critical && !active) return DefectLifecycleDecision.Fail(422, "defect_safeguard_required");
                return DefectLifecycleDecision.Ok(Action);
            case "VERIFY":
                if (!In(Action) || !e.AwaitingVerification) return DefectLifecycleDecision.Fail(409, "invalid_state");
                if (!permissions.Contains(VerifyPermission)) return DefectLifecycleDecision.Fail(403, "permission_denied");
                if (owner) return DefectLifecycleDecision.Fail(403, "separation_of_duties");
                return DefectLifecycleDecision.Ok(c.Outcome == "VERIFIED" ? Verify : Action);
            case "EFFECTIVENESS":
                if (!In(Verify)) return DefectLifecycleDecision.Fail(409, "invalid_state");
                if (!permissions.Contains(EffectivenessPermission)) return DefectLifecycleDecision.Fail(403, "permission_denied");
                if (owner) return DefectLifecycleDecision.Fail(403, "separation_of_duties");
                return DefectLifecycleDecision.Ok(c.Outcome == "EFFECTIVE" ? Effectiveness : Action);
            case "CLOSE":
                if (!In(Effectiveness)) return DefectLifecycleDecision.Fail(409, "invalid_state");
                if (!permissions.Contains(ClosePermission)) return DefectLifecycleDecision.Fail(403, "permission_denied");
                if (owner) return DefectLifecycleDecision.Fail(403, "separation_of_duties");
                if (e.CloseBlockers.Length > 0) return DefectLifecycleDecision.Fail(422, "defect_closure_blocked", e.CloseBlockers);
                return DefectLifecycleDecision.Ok(Closed);
            default:
                return DefectLifecycleDecision.Fail(400, "invalid_request");
        }
    }

    public static bool IsClosed(string? stage) => stage == Closed;
}
