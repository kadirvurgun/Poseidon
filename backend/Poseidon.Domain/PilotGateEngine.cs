using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// DEV-074 / DEV-094 controlled UAT scenario catalog (19.01 v46.3 "Test Pilot UAT Release Gates", controlled candidate
// 22.10.2026): section 3 "Mandatory Ship UAT" paragraphs 68-89 and section 4 "Mandatory Tug UAT" paragraphs 90-123.
// The pass proof is the source text, verbatim; it is the criterion a human UAT participant judges, never an automatic
// verdict. S-UAT-* and T-UAT-* are distinct source identities (65.02 p35/p1349) and are never renumbered.
public sealed record UatScenario(string Id, string Scope, string Journey, string PassProof, string Locator);

public static class UatScenarioCatalog
{
    public const string Version = "19.01-v46.3-2026-10-22";
    public const string Ship = "SHIP", Tug = "TUG";
    public static readonly string[] Scopes = [Ship, Tug];
    private const string Doc = "19.01_POSEIDON_v46.3_Test_Pilot_UAT_Release_Gates_TR.docx";

    public static readonly UatScenario[] Scenarios = [
        new("S-UAT-01", Ship, "Vessel onboarding", "Profile completeness and applicability review", Doc + "/paragraph:72-74"),
        new("S-UAT-02", Ship, "Crew eligibility", "Expired/unverified credential cannot yield ELIGIBLE", Doc + "/paragraph:75-77"),
        new("S-UAT-03", Ship, "PMS/defect", "Critical defect requires safeguard/evidence/verification", Doc + "/paragraph:78-80"),
        new("S-UAT-04", Ship, "Departure", "Critical blocker visible before Master sign-off", Doc + "/paragraph:81-83"),
        new("S-UAT-05", Ship, "Offline", "Supported shipboard record survives disconnect/reconnect", Doc + "/paragraph:84-86"),
        new("S-UAT-06", Ship, "Audit", "Requirement→evidence→verification→decision trace is retrievable", Doc + "/paragraph:87-89"),
        new("T-UAT-01", Tug, "Job intake", "New job created with vessel/type/time/area/priority", Doc + "/paragraph:94-96"),
        new("T-UAT-02", Tug, "Assignment", "BP + overlap + crew/equipment/PMS/certificate checks visible", Doc + "/paragraph:97-99"),
        new("T-UAT-03", Tug, "Readiness", "Critical equipment/crew gap prevents READY", Doc + "/paragraph:100-102"),
        new("T-UAT-04", Tug, "Towage plan", "Configuration/SWL/BP/limits/comms/emergency/contingency controlled", Doc + "/paragraph:103-105"),
        new("T-UAT-05", Tug, "Risk & briefing", "Hazards/barriers/stop-work + participant acknowledgement", Doc + "/paragraph:106-108"),
        new("T-UAT-06", Tug, "Master release", "Shore assignment cannot bypass onboard safety decision", Doc + "/paragraph:109-111"),
        new("T-UAT-07", Tug, "Execution", "Start/suspend/abort/complete + towline/comms/evidence lineage", Doc + "/paragraph:112-114"),
        new("T-UAT-08", Tug, "Offline", "Job/briefing/execution/log sync without data loss; conflict explicit", Doc + "/paragraph:115-117"),
        new("T-UAT-09", Tug, "History", "Completed job evidence searchable in daily/history", Doc + "/paragraph:118-120"),
        new("T-UAT-10", Tug, "Mode switch", "Ship and Tug Today/menus change with selected context", Doc + "/paragraph:121-123"),
    ];

    // DEV-074 AC (18.01 row 75): "Master/DPA/C/E/Crew kritik journey"; DEV-094 (row 95) tug roles of the
    // intake→assignment→readiness→plan→briefing→execution→log journey (19.01 p132: Römorkör Operasyon Sorumlusu).
    public static readonly string[] ShipRoles = ["MASTER", "DPA", "CHIEF_ENGINEER", "CREW"];
    public static readonly string[] TugRoles = ["TUG_MASTER", "TUG_OPERATIONS_RESPONSIBLE", "TUG_CREW"];
    public static string[] Roles(string scope) => scope == Ship ? ShipRoles : scope == Tug ? TugRoles : [];
    public static UatScenario? Find(string id) => Scenarios.FirstOrDefault(s => s.Id == id);
    public static UatScenario[] ForScope(string scope) => Scenarios.Where(s => s.Scope == scope).ToArray();
}

// Human-recorded facts the UAT gate aggregates. Outcomes are participant/facilitator observations; an automated
// (Playwright) evidence reference is an attachment only and never substitutes PASS (DEV-074/094 brief).
public sealed record UatThresholdInput(int MinScenarioPassPercent, int MaxOpenMajorFindings, bool RequireDesignConformance);
public sealed record UatRunInput(Guid Id, string Role, string State, IReadOnlyDictionary<string, string> LatestOutcomes);
public sealed record UatFindingInput(Guid Id, string ScenarioId, string Severity, bool DesignConformance, string State);
public sealed record UatScenarioOutcome(string ScenarioId, int AttestedRuns, int Passed, int Failed, int Blocked, int NotRun, int? PassPercent);
public sealed record UatGateOutcome(string Scope, string State, string GateState, string[] Reasons, UatScenarioOutcome[] Scenarios,
    int OpenBlockers, int OpenMajors, int OpenDesignConformance, int AttestedRuns, int PendingAttestation,
    string Authority = UatPolicy.Authority);

// DEV-074 (18.01 row 75: "Master/DPA/C/E/Crew kritik journey eşik geçer; blocker UX=0; design conformance") and
// DEV-094 (row 95: "Intake→assignment→readiness→plan→briefing→execution→log; blocker UX=0"). 19.01 p39 "Critical
// journeys meet agreed threshold" and p276-277 "UAT critical blocker: 0 at pilot GO". The threshold is agreed and
// recorded by humans; without it the gate is THRESHOLD_UNSET and never passes. The engine reports whether the
// recorded human evidence meets the recorded threshold; it is never a UAT acceptance or a pilot GO.
public static class UatPolicy
{
    public const string Authority = "HumanUatEvidenceOnly;EngineNeverAcceptsUat;NotAPilotGo";
    public const string Pass = "PASS", Fail = "FAIL", Blocked = "BLOCKED", NotRun = "NOT_RUN";
    public static readonly string[] Outcomes = [Pass, Fail, Blocked, NotRun];
    public const string Blocker = "BLOCKER", Major = "MAJOR", Minor = "MINOR";
    public static readonly string[] Severities = [Blocker, Major, Minor];
    public const string Open = "OPEN", Resolved = "RESOLVED", AcceptedRisk = "ACCEPTED_RISK";
    public static readonly string[] FindingStates = [Open, Resolved, AcceptedRisk];
    public const string RunOpen = "OPEN", RunSubmitted = "SUBMITTED", RunAttested = "ATTESTED";
    public static readonly string[] RunStates = [RunOpen, RunSubmitted, RunAttested];
    public static readonly string[] Environments = ["UAT", "PILOT_STAGING", "LAB"];
    // Gate states shared with the pilot go/no-go review.
    public const string ThresholdUnset = "THRESHOLD_UNSET", CriteriaNotMet = "CRITERIA_NOT_MET", EvidenceIncomplete = "EVIDENCE_INCOMPLETE",
        CriteriaMet = "CRITERIA_MET_PENDING_HUMAN_GO";
    public static readonly string[] States = [ThresholdUnset, CriteriaNotMet, EvidenceIncomplete, CriteriaMet];
    public const int TextMax = 500, TitleMax = 200, ReferenceMax = 200, MaxRunsEvaluated = 512, MaxFindingsEvaluated = 2048;

    // Pseudonym only (no names/e-mail/crew numbers): 3-32 of A-Z, 0-9 and '-'.
    public static bool ValidPseudonym(string? value) => value is { Length: >= 3 and <= 32 } && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-');
    // A build identity is a 7-40 lowercase hex git commit.
    public static bool ValidCommit(string? value) => value is { Length: >= 7 and <= 40 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidText(string? value, int min, int max) => value is { } v && v.Trim() == v && v.Length >= min && v.Length <= max && !v.Any(char.IsControl);
    public static bool ValidOptionalReference(string? value) => value == null || ValidText(value, 1, ReferenceMax);
    public static bool ValidThreshold(UatThresholdInput t) => t.MinScenarioPassPercent is >= 1 and <= 100 && t.MaxOpenMajorFindings is >= 0 and <= 1000;
    // A blocker can only be resolved (fixed and retested); risk acceptance never clears it (19.01 p277).
    public static string? ResolutionRefusal(string severity, string target) =>
        !FindingStates.Contains(target, StringComparer.Ordinal) || target == Open ? "validation_failed" :
        severity == Blocker && target == AcceptedRisk ? "blocker_risk_acceptance_not_permitted" : null;

    public static UatGateOutcome Evaluate(string scope, UatThresholdInput? threshold, IReadOnlyCollection<UatRunInput> runs, IReadOnlyCollection<UatFindingInput> findings)
    {
        if (!UatScenarioCatalog.Scopes.Contains(scope, StringComparer.Ordinal)) throw new ArgumentException("invalid_uat_scope");
        if (runs.Count > MaxRunsEvaluated || findings.Count > MaxFindingsEvaluated) throw new ArgumentException("uat_projection_limit");
        var catalog = UatScenarioCatalog.ForScope(scope);
        var reasons = new List<string>();
        var open = findings.Where(f => f.State == Open).ToArray();
        var blockers = open.Count(f => f.Severity == Blocker);
        var majors = open.Count(f => f.Severity == Major);
        var design = open.Count(f => f.DesignConformance);
        // Only runs a second human attested count; submitted-but-unattested runs are visible as pending.
        var attested = runs.Where(r => r.State == RunAttested).ToArray();
        var pending = runs.Count(r => r.State == RunSubmitted);
        var scenarios = catalog.Select(s => {
            var outcomes = attested.Select(r => r.LatestOutcomes.TryGetValue(s.Id, out var o) ? o : NotRun).ToArray();
            int Count(string o) => outcomes.Count(x => x == o);
            var executed = outcomes.Length - Count(NotRun);
            return new UatScenarioOutcome(s.Id, outcomes.Length, Count(Pass), Count(Fail), Count(Blocked), Count(NotRun),
                executed == 0 ? null : (int)Math.Floor(Count(Pass) * 100m / executed));
        }).ToArray();

        if (blockers > 0) reasons.Add("BLOCKER_FINDINGS_OPEN");
        if (threshold == null) reasons.Add("THRESHOLD_UNSET");
        if (attested.Length == 0) reasons.Add("NO_ATTESTED_RUNS");
        if (pending > 0) reasons.Add("ATTESTATION_PENDING");
        foreach (var role in UatScenarioCatalog.Roles(scope).Where(role => attested.All(r => r.Role != role))) reasons.Add("ROLE_NOT_COVERED:" + role);
        var fail = blockers > 0;
        foreach (var s in scenarios) {
            if (s.PassPercent == null) { reasons.Add("SCENARIO_NOT_EXECUTED:" + s.ScenarioId); continue; }
            if (threshold != null && s.PassPercent < threshold.MinScenarioPassPercent) { reasons.Add("SCENARIO_BELOW_THRESHOLD:" + s.ScenarioId); fail = true; }
        }
        if (threshold != null && majors > threshold.MaxOpenMajorFindings) { reasons.Add("MAJOR_FINDINGS_ABOVE_THRESHOLD"); fail = true; }
        if (threshold is { RequireDesignConformance: true } && design > 0) { reasons.Add("DESIGN_CONFORMANCE_FINDINGS_OPEN"); fail = true; }

        // A FAIL is reported even without a threshold (an open blocker is always a NO-GO); a missing threshold or
        // incomplete evidence is never treated as passed.
        string state, gate;
        if (fail) { state = CriteriaNotMet; gate = Fail; }
        else if (threshold == null) { state = ThresholdUnset; gate = "UNKNOWN"; }
        else if (reasons.Count > 0) { state = EvidenceIncomplete; gate = "UNKNOWN"; }
        else { state = CriteriaMet; gate = Pass; }
        return new(scope, state, gate, reasons.Distinct(StringComparer.Ordinal).ToArray(), scenarios, blockers, majors, design, attested.Length, pending);
    }
}

// DEV-075 pilot go/no-go review (18.01 row 76: "Security/regulatory/offline/DR/UAT/data/trace kapıları PASS; P0=0;
// support/on-call hazır"; DoR DoD Gates row 4 "Pilot Gate ... PASS"; 23.03 §5 NO-GO triggers p97-110; 19.01 p276-279).
// Advisory aggregation only: UNKNOWN or DEFERRED gates and a non-zero or unknown P0 count make the recommendation
// NO_GO_RECOMMENDED. The engine never emits GO; the decision is the Product Owner's human record.
public sealed record PilotGateInput(string Gate, string State, string[] Reasons, string Source);
public sealed record PilotGateReviewOutcome(string Recommendation, PilotGateInput[] Gates, int? OpenP0, string[] NoGoReasons, string Hash,
    string Authority = PilotGatePolicy.Authority);

public static class PilotGatePolicy
{
    public const string Authority = "ProductOwnerHumanDecisionRequired;EngineNeverDecidesGo;AiCannotDecide";
    public const string SourceLocator = "18.01/sheet:Backlog/row:76;18.01/sheet:DoR DoD Gates/row:4;23.03/paragraph:97-110;19.01/paragraph:130-136,276-279";
    public const string Pass = "PASS", Fail = "FAIL", Unknown = "UNKNOWN", Deferred = "DEFERRED";
    public static readonly string[] GateStates = [Pass, Fail, Unknown, Deferred];
    public const string Security = "SECURITY", Regulatory = "REGULATORY", Offline = "OFFLINE", Dr = "DR", UatShip = "UAT_SHIP", UatTug = "UAT_TUG",
        Data = "DATA", Trace = "TRACE", Support = "SUPPORT";
    public static readonly string[] Gates = [Security, Regulatory, Offline, Dr, UatShip, UatTug, Data, Trace, Support];
    public const string GoEligible = "GO_ELIGIBLE_PENDING_HUMAN_DECISION", NoGoRecommended = "NO_GO_RECOMMENDED";
    public static readonly string[] Recommendations = [GoEligible, NoGoRecommended];
    public const string Go = "GO", NoGo = "NO_GO", ConditionalGo = "CONDITIONAL_GO";
    public static readonly string[] Decisions = [Go, NoGo, ConditionalGo];
    public const string ProductOwnerDuty = "ProductOwner";
    // 23.03 p107-110 "Monitoring blind -> No production release"; 18.01 row 76 "support/on-call hazır".
    public static readonly string[] SupportItems = ["SUPPORT_CONTACTS", "ONCALL_ROTA", "ESCALATION_PATH", "ROLLBACK_OWNER", "INCIDENT_COMMUNICATION", "MONITORING_ALERTING"];
    public const string SupportReady = "READY", SupportNotReady = "NOT_READY";
    public static readonly string[] SupportStates = [SupportReady, SupportNotReady];
    public const int RationaleMin = 10, RationaleMax = 2000, ConditionMax = 300, MaxConditions = 20;

    public static bool ValidReason(string? code) => code is { Length: > 0 and <= 160 } && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or ':' or '-');

    // Support gate from the latest human mark per item: all READY -> PASS; any NOT_READY -> FAIL; missing -> UNKNOWN.
    public static PilotGateInput SupportGate(IReadOnlyDictionary<string, string> latest)
    {
        var reasons = new List<string>();
        var fail = false;
        foreach (var item in SupportItems) {
            if (!latest.TryGetValue(item, out var state)) reasons.Add("SUPPORT_ITEM_UNMARKED:" + item);
            else if (state != SupportReady) { reasons.Add("SUPPORT_ITEM_NOT_READY:" + item); fail = true; }
        }
        return new(Support, fail ? Fail : reasons.Count > 0 ? Unknown : Pass, reasons.ToArray(), "PilotSupportCheck");
    }

    public static PilotGateReviewOutcome Evaluate(IReadOnlyCollection<PilotGateInput> inputs, int? openP0)
    {
        var gates = Gates.Select(g => {
            var own = inputs.Where(i => i != null && i.Gate == g).ToArray();
            if (own.Length != 1) return new PilotGateInput(g, Unknown, [own.Length == 0 ? "GATE_SOURCE_MISSING" : "GATE_SOURCE_AMBIGUOUS"], "None");
            var i = own[0];
            if (!GateStates.Contains(i.State, StringComparer.Ordinal) || i.Reasons.Any(r => !ValidReason(r)) || i.Reasons.Length > 256 || i.Source is not { Length: > 0 and <= 80 })
                return new PilotGateInput(g, Unknown, ["GATE_SOURCE_INVALID"], "None");
            // A PASS that carries no evidence of what passed is still a PASS only if the source says so; reasons are codes.
            return new PilotGateInput(g, i.State, i.Reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), i.Source);
        }).ToArray();
        var reasons = gates.Where(g => g.State != Pass).Select(g => g.Gate + ":" + g.State).ToList();
        if (openP0 == null) reasons.Add("P0:UNKNOWN");
        else if (openP0 > 0) reasons.Add("P0:OPEN");
        var recommendation = reasons.Count == 0 ? GoEligible : NoGoRecommended;
        var canonical = string.Join("\n", gates.Select(g => g.Gate + "|" + g.State + "|" + g.Source + "|" + string.Join(",", g.Reasons))) +
            "\nP0|" + (openP0?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "UNKNOWN") + "\n" + recommendation;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new(recommendation, gates, openP0, reasons.ToArray(), hash);
    }

    // GO only on GO_ELIGIBLE (every gate PASS, P0 known zero). CONDITIONAL_GO only with no FAIL/UNKNOWN gate, P0
    // known zero and explicit conditions (a DEFERRED gate may be carried as a condition). NO_GO is always recordable.
    public static string? Refusal(string decision, PilotGateReviewOutcome review, IReadOnlyCollection<string> conditions)
    {
        if (!Decisions.Contains(decision, StringComparer.Ordinal)) return "validation_failed";
        if (decision == NoGo) return conditions.Count == 0 ? null : "validation_failed";
        if (review.Gates.Any(g => g.State == Fail)) return "pilot_gate_failed";
        if (review.OpenP0 is not 0) return "pilot_gate_p0_open_or_unknown";
        if (decision == Go) return review.Recommendation == GoEligible && conditions.Count == 0 ? null : review.Recommendation == GoEligible ? "validation_failed" : "pilot_gate_not_passed";
        if (review.Gates.Any(g => g.State == Unknown)) return "pilot_gate_unknown";
        return conditions.Count is > 0 and <= MaxConditions ? null : "conditions_required";
    }

    public static bool ValidRationale(string? value) => UatPolicy.ValidText(value, RationaleMin, RationaleMax);
    public static bool ValidCondition(string? value) => UatPolicy.ValidText(value, 3, ConditionMax);
}
