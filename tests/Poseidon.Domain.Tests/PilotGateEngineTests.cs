using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// S11 WP-D DEV-074 / DEV-094 (18.01 rows 75/95; 19.01 §3-4 p68-123, p39, p276-277) and DEV-075 (18.01 row 76; DoR DoD
// Gates row 4; 23.03 §5 p97-110). The engines aggregate human records; they never accept UAT and never emit GO.
public sealed class PilotGateEngineTests
{
    private static readonly UatThresholdInput Threshold = new(100, 0, true);
    private static Dictionary<string, string> All(string scope, string outcome) =>
        UatScenarioCatalog.ForScope(scope).ToDictionary(s => s.Id, _ => outcome, StringComparer.Ordinal);
    private static UatRunInput[] AttestedShipRuns(string outcome = UatPolicy.Pass) =>
        UatScenarioCatalog.ShipRoles.Select(r => new UatRunInput(Guid.NewGuid(), r, UatPolicy.RunAttested, All("SHIP", outcome))).ToArray();

    [Fact]
    [Trait("Requirement", "DEV-074")]
    [Trait("Requirement", "DEV-094")]
    public void Catalog_carries_the_six_ship_and_ten_tug_source_scenarios_with_their_pass_proofs_verbatim()
    {
        Assert.Equal(Enumerable.Range(1, 6).Select(i => $"S-UAT-{i:00}"), UatScenarioCatalog.ForScope("SHIP").Select(s => s.Id));
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"T-UAT-{i:00}"), UatScenarioCatalog.ForScope("TUG").Select(s => s.Id));
        Assert.Equal("Critical blocker visible before Master sign-off", UatScenarioCatalog.Find("S-UAT-04")!.PassProof);
        Assert.Equal("Shore assignment cannot bypass onboard safety decision", UatScenarioCatalog.Find("T-UAT-06")!.PassProof);
        Assert.All(UatScenarioCatalog.Scenarios, s => Assert.StartsWith("19.01_POSEIDON_v46.3_Test_Pilot_UAT_Release_Gates_TR.docx/paragraph:", s.Locator));
        Assert.Equal(16, UatScenarioCatalog.Scenarios.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public void Without_a_human_threshold_the_gate_is_threshold_unset_and_never_passes_even_with_all_pass_results()
    {
        var o = UatPolicy.Evaluate("SHIP", null, AttestedShipRuns(), []);
        Assert.Equal((UatPolicy.ThresholdUnset, "UNKNOWN"), (o.State, o.GateState));
        Assert.Contains("THRESHOLD_UNSET", o.Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public void An_open_blocker_fails_the_gate_with_or_without_threshold_and_risk_acceptance_never_clears_it()
    {
        var blocker = new UatFindingInput(Guid.NewGuid(), "S-UAT-04", UatPolicy.Blocker, false, UatPolicy.Open);
        Assert.Equal(("CRITERIA_NOT_MET", "FAIL"), (UatPolicy.Evaluate("SHIP", Threshold, AttestedShipRuns(), [blocker]) is var a ? (a.State, a.GateState) : default));
        Assert.Equal("FAIL", UatPolicy.Evaluate("SHIP", null, AttestedShipRuns(), [blocker]).GateState);
        Assert.Equal("blocker_risk_acceptance_not_permitted", UatPolicy.ResolutionRefusal(UatPolicy.Blocker, UatPolicy.AcceptedRisk));
        Assert.Null(UatPolicy.ResolutionRefusal(UatPolicy.Blocker, UatPolicy.Resolved));
        Assert.Null(UatPolicy.ResolutionRefusal(UatPolicy.Major, UatPolicy.AcceptedRisk));
        Assert.Equal("validation_failed", UatPolicy.ResolutionRefusal(UatPolicy.Minor, UatPolicy.Open));
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public void Only_attested_runs_count_every_role_and_scenario_must_be_covered_and_the_recorded_threshold_is_applied()
    {
        // Submitted-but-unattested evidence never counts.
        var pending = UatScenarioCatalog.ShipRoles.Select(r => new UatRunInput(Guid.NewGuid(), r, UatPolicy.RunSubmitted, All("SHIP", UatPolicy.Pass))).ToArray();
        var o = UatPolicy.Evaluate("SHIP", Threshold, pending, []);
        Assert.Equal((UatPolicy.EvidenceIncomplete, "UNKNOWN"), (o.State, o.GateState));
        Assert.Contains("ATTESTATION_PENDING", o.Reasons); Assert.Contains("NO_ATTESTED_RUNS", o.Reasons);

        // A missing role (Crew) keeps the evidence incomplete.
        var noCrew = AttestedShipRuns().Where(r => r.Role != "CREW").ToArray();
        Assert.Contains("ROLE_NOT_COVERED:CREW", UatPolicy.Evaluate("SHIP", Threshold, noCrew, []).Reasons);

        // NOT_RUN is not a pass; a FAIL below the 100% threshold fails the scenario.
        var runs = AttestedShipRuns();
        var failed = runs.Select((r, i) => i == 0 ? r with { LatestOutcomes = new Dictionary<string, string>(All("SHIP", UatPolicy.Pass)) { ["S-UAT-03"] = UatPolicy.Fail } } : r).ToArray();
        var f = UatPolicy.Evaluate("SHIP", Threshold, failed, []);
        Assert.Equal("FAIL", f.GateState); Assert.Contains("SCENARIO_BELOW_THRESHOLD:S-UAT-03", f.Reasons);
        Assert.Equal(75, f.Scenarios.Single(s => s.ScenarioId == "S-UAT-03").PassPercent);
        // The same evidence passes a human-agreed 75% threshold.
        Assert.Equal("PASS", UatPolicy.Evaluate("SHIP", new(75, 0, true), failed, []).GateState);

        var notRun = runs.Select(r => r with { LatestOutcomes = All("SHIP", UatPolicy.Pass).Where(x => x.Key != "S-UAT-05").ToDictionary(x => x.Key, x => x.Value) }).ToArray();
        Assert.Contains("SCENARIO_NOT_EXECUTED:S-UAT-05", UatPolicy.Evaluate("SHIP", Threshold, notRun, []).Reasons);
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    [Trait("Requirement", "DEV-094")]
    public void Criteria_met_is_reported_only_as_pending_human_go_and_design_conformance_findings_fail_when_required()
    {
        var o = UatPolicy.Evaluate("SHIP", Threshold, AttestedShipRuns(), []);
        Assert.Equal((UatPolicy.CriteriaMet, "PASS"), (o.State, o.GateState));
        Assert.Equal(UatPolicy.Authority, o.Authority);
        Assert.Contains("NotAPilotGo", o.Authority);
        var design = new UatFindingInput(Guid.NewGuid(), "S-UAT-01", UatPolicy.Minor, true, UatPolicy.Open);
        Assert.Equal("FAIL", UatPolicy.Evaluate("SHIP", Threshold, AttestedShipRuns(), [design]).GateState);
        Assert.Equal("PASS", UatPolicy.Evaluate("SHIP", Threshold with { RequireDesignConformance = false }, AttestedShipRuns(), [design]).GateState);
        // Tug scope is a separate filter: ship evidence never satisfies tug UAT.
        Assert.Equal("UNKNOWN", UatPolicy.Evaluate("TUG", Threshold, AttestedShipRuns().Select(r => r with { Role = "TUG_MASTER" }).ToArray(), []).GateState);
    }

    [Fact]
    [Trait("Requirement", "DEV-074")]
    public void Pseudonym_and_build_validation_keep_identity_out_of_uat_records()
    {
        Assert.True(UatPolicy.ValidPseudonym("P-MASTER-01"));
        Assert.False(UatPolicy.ValidPseudonym("john.doe@example.com"));
        Assert.False(UatPolicy.ValidPseudonym("Ali Veli"));
        Assert.True(UatPolicy.ValidCommit("f1c2d51"));
        Assert.False(UatPolicy.ValidCommit("F1C2D51"));
        Assert.False(UatPolicy.ValidCommit("main"));
    }

    private static PilotGateInput[] AllPass() => PilotGatePolicy.Gates.Select(g => new PilotGateInput(g, "PASS", [], "Test")).ToArray();

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Unknown_deferred_or_missing_gates_and_unknown_or_open_p0_recommend_no_go_and_the_engine_never_emits_go()
    {
        var eligible = PilotGatePolicy.Evaluate(AllPass(), 0);
        Assert.Equal(PilotGatePolicy.GoEligible, eligible.Recommendation);
        Assert.DoesNotContain(PilotGatePolicy.Go, PilotGatePolicy.Recommendations);

        Assert.Equal(PilotGatePolicy.NoGoRecommended, PilotGatePolicy.Evaluate(AllPass(), null).Recommendation);
        Assert.Contains("P0:UNKNOWN", PilotGatePolicy.Evaluate(AllPass(), null).NoGoReasons);
        Assert.Contains("P0:OPEN", PilotGatePolicy.Evaluate(AllPass(), 1).NoGoReasons);
        var deferred = AllPass().Select(g => g.Gate == "DR" ? g with { State = "DEFERRED", Reasons = ["DR_REHEARSAL_SOURCE_NOT_INTEGRATED:DEV-071"] } : g).ToArray();
        Assert.Contains("DR:DEFERRED", PilotGatePolicy.Evaluate(deferred, 0).NoGoReasons);
        var missing = PilotGatePolicy.Evaluate(AllPass().Where(g => g.Gate != "TRACE").ToArray(), 0);
        Assert.Equal(("UNKNOWN", "GATE_SOURCE_MISSING"), (missing.Gates.Single(g => g.Gate == "TRACE").State, missing.Gates.Single(g => g.Gate == "TRACE").Reasons.Single()));
        var duplicate = PilotGatePolicy.Evaluate([.. AllPass(), new("SECURITY", "PASS", [], "Other")], 0);
        Assert.Equal("GATE_SOURCE_AMBIGUOUS", duplicate.Gates.Single(g => g.Gate == "SECURITY").Reasons.Single());
        Assert.Equal(PilotGatePolicy.NoGoRecommended, duplicate.Recommendation);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Go_is_refused_on_fail_or_unknown_conditional_go_needs_conditions_and_no_go_is_always_recordable()
    {
        var eligible = PilotGatePolicy.Evaluate(AllPass(), 0);
        Assert.Null(PilotGatePolicy.Refusal("GO", eligible, []));
        Assert.Null(PilotGatePolicy.Refusal("NO_GO", eligible, []));

        var failed = PilotGatePolicy.Evaluate(AllPass().Select(g => g.Gate == "SECURITY" ? g with { State = "FAIL" } : g).ToArray(), 0);
        Assert.Equal("pilot_gate_failed", PilotGatePolicy.Refusal("GO", failed, []));
        Assert.Equal("pilot_gate_failed", PilotGatePolicy.Refusal("CONDITIONAL_GO", failed, ["Fix SEC-1 before day 2"]));
        Assert.Null(PilotGatePolicy.Refusal("NO_GO", failed, []));

        var unknown = PilotGatePolicy.Evaluate(AllPass().Select(g => g.Gate == "REGULATORY" ? g with { State = "UNKNOWN" } : g).ToArray(), 0);
        Assert.Equal("pilot_gate_not_passed", PilotGatePolicy.Refusal("GO", unknown, []));
        Assert.Equal("pilot_gate_unknown", PilotGatePolicy.Refusal("CONDITIONAL_GO", unknown, ["Condition"]));

        var p0 = PilotGatePolicy.Evaluate(AllPass(), null);
        Assert.Equal("pilot_gate_p0_open_or_unknown", PilotGatePolicy.Refusal("GO", p0, []));

        var deferred = PilotGatePolicy.Evaluate(AllPass().Select(g => g.Gate == "DR" ? g with { State = "DEFERRED" } : g).ToArray(), 0);
        // S11 gate QA N1: a DEFERRED gate was never evaluated, so neither GO nor CONDITIONAL_GO is recordable (422).
        Assert.Equal("pilot_gate_deferred", PilotGatePolicy.Refusal("GO", deferred, []));
        Assert.Equal("pilot_gate_deferred", PilotGatePolicy.Refusal("CONDITIONAL_GO", deferred, []));
        Assert.Equal("pilot_gate_deferred", PilotGatePolicy.Refusal("CONDITIONAL_GO", deferred, ["DR rehearsal evidence before production cutover"]));
        Assert.Null(PilotGatePolicy.Refusal("NO_GO", deferred, []));
        Assert.Equal("validation_failed", PilotGatePolicy.Refusal("NO_GO", deferred, ["no conditions on NO_GO"]));

        // CONDITIONAL_GO only on an all-PASS review, with explicit non-gate conditions.
        Assert.Equal("conditions_required", PilotGatePolicy.Refusal("CONDITIONAL_GO", eligible, []));
        Assert.Null(PilotGatePolicy.Refusal("CONDITIONAL_GO", eligible, ["Hypercare call at 08:00 on day 1"]));
        Assert.Equal("validation_failed", PilotGatePolicy.Refusal("GO", eligible, ["GO carries no conditions"]));
        var allDeferred = PilotGatePolicy.Evaluate(AllPass().Select(g => g with { State = "DEFERRED" }).ToArray(), 0);
        Assert.All(new[] { "GO", "CONDITIONAL_GO" }, d => Assert.Equal("pilot_gate_deferred", PilotGatePolicy.Refusal(d, allDeferred, ["Condition"])));
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Refusal_status_maps_deferred_gate_to_422_other_gate_states_to_409_and_malformed_to_400()
    {
        Assert.Equal(422, PilotGatePolicy.RefusalStatus("pilot_gate_deferred"));
        Assert.Equal(409, PilotGatePolicy.RefusalStatus("pilot_gate_failed"));
        Assert.Equal(409, PilotGatePolicy.RefusalStatus("pilot_gate_unknown"));
        Assert.Equal(409, PilotGatePolicy.RefusalStatus("pilot_gate_p0_open_or_unknown"));
        Assert.Equal(400, PilotGatePolicy.RefusalStatus("conditions_required"));
        Assert.Equal(400, PilotGatePolicy.RefusalStatus("validation_failed"));
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Review_hash_changes_with_any_gate_state_reason_or_p0_count()
    {
        var a = PilotGatePolicy.Evaluate(AllPass(), 0).Hash;
        Assert.Equal(64, a.Length);
        Assert.Equal(a, PilotGatePolicy.Evaluate(AllPass().Reverse().ToArray(), 0).Hash);
        Assert.NotEqual(a, PilotGatePolicy.Evaluate(AllPass(), 1).Hash);
        Assert.NotEqual(a, PilotGatePolicy.Evaluate(AllPass().Select(g => g.Gate == "UAT_TUG" ? g with { Reasons = ["X"] } : g).ToArray(), 0).Hash);
    }

    [Fact]
    [Trait("Requirement", "DEV-075")]
    public void Support_gate_needs_every_item_marked_ready_by_a_human()
    {
        Assert.Equal("UNKNOWN", PilotGatePolicy.SupportGate(new Dictionary<string, string>()).State);
        var ready = PilotGatePolicy.SupportItems.ToDictionary(i => i, _ => "READY");
        Assert.Equal("PASS", PilotGatePolicy.SupportGate(ready).State);
        ready["ONCALL_ROTA"] = "NOT_READY";
        var fail = PilotGatePolicy.SupportGate(ready);
        Assert.Equal("FAIL", fail.State); Assert.Contains("SUPPORT_ITEM_NOT_READY:ONCALL_ROTA", fail.Reasons);
    }
}
