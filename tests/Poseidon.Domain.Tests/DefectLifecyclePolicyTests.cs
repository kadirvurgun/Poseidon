using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-055 / T-S8-004 (18.01 Backlog row 56): Detect->Assess->Safeguard->Action->Verify->Effectiveness;
// a critical defect without safeguard cannot close; temporary measure expiry is monitored.
[Trait("Requirement", "DEV-055")]
public sealed class DefectLifecyclePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Engineer = Guid.Parse("00000000-0000-4000-8000-0000000005a1");
    private static readonly Guid Verifier = Guid.Parse("00000000-0000-4000-8000-0000000005b2");
    private static readonly Guid Chief = Guid.Parse("00000000-0000-4000-8000-0000000005c3");
    private static readonly string[] All = ["defect.write", DefectLifecyclePolicy.VerifyPermission, DefectLifecyclePolicy.EffectivenessPermission, DefectLifecyclePolicy.ClosePermission];

    private sealed class Lifecycle(string? declared = "Critical")
    {
        public readonly List<DefectLifecycleEntry> Records = [];
        public long DefectRevision = 1;
        public DefectLifecycleEvaluation Eval(DateTimeOffset? at = null) => DefectLifecyclePolicy.Evaluate(Records, declared, DefectRevision, at ?? Now);
        public DefectLifecycleDecision Try(string kind, Guid actor, DefectLifecycleCommand c, Guid? measure = null, string[]? permissions = null, DateTimeOffset? at = null)
        {
            var decision = DefectLifecyclePolicy.Decide(kind, Eval(at), Records.Count > 0, actor, permissions ?? All, c, measure);
            if (decision.Status == 200)
            {
                DefectLifecyclePolicy.ValidShape(kind, c, at ?? Now, out var expires);
                Records.Add(new(Guid.NewGuid(), Records.Count + 1, kind, decision.StageAfter, DefectRevision, c.Criticality, c.MeasureKind, c.Temporary == true,
                    expires, measure, kind == "CLOSE" ? DefectLifecyclePolicy.ClosureOutcome : c.Outcome, actor, at ?? Now));
            }
            return decision;
        }
    }
    private static DefectLifecycleCommand Cmd(long rev, string? criticality = null, string? measure = null, string? text = null, bool? temporary = null,
        string? expires = null, string? outcome = null, long? defectRevision = null) => new(rev, criticality, measure, text, temporary, expires, outcome, defectRevision);

    private static Lifecycle ToEffectiveness(bool temporary = false)
    {
        var l = new Lifecycle();
        Assert.Equal(200, l.Try("START", Engineer, Cmd(0, defectRevision: 1)).Status);
        Assert.Equal(200, l.Try("ASSESS", Engineer, Cmd(1, "Critical", text: "FICTIONAL loss of propulsion")).Status);
        Assert.Equal(200, l.Try("SAFEGUARD", Engineer, Cmd(2, measure: "RESTRICTION", text: "FICTIONAL speed limit", temporary: temporary,
            expires: temporary ? "2026-11-21T08:00:00Z" : null)).Status);
        Assert.Equal(200, l.Try("ACTION", Engineer, Cmd(3, text: "FICTIONAL replaced seal")).Status);
        Assert.Equal(200, l.Try("VERIFY", Verifier, Cmd(4, outcome: "VERIFIED", text: "FICTIONAL tested at sea")).Status);
        Assert.Equal(200, l.Try("EFFECTIVENESS", Verifier, Cmd(5, outcome: "EFFECTIVE", text: "FICTIONAL no recurrence observed")).Status);
        return l;
    }

    [Fact]
    public void Full_lifecycle_walks_every_stage_and_closes_only_with_human_records()
    {
        var l = ToEffectiveness();
        Assert.Equal(DefectLifecyclePolicy.Effectiveness, l.Eval().Stage);
        Assert.Empty(l.Eval().CloseBlockers);
        var close = l.Try("CLOSE", Chief, Cmd(6, text: "FICTIONAL returned to service"));
        Assert.Equal((200, "CLOSED"), (close.Status, close.StageAfter));
        Assert.Equal(["DETECT", "ASSESS", "SAFEGUARD", "ACTION", "VERIFY", "EFFECTIVENESS", "CLOSED"], l.Records.Select(r => r.StageAfter));
        Assert.Equal(409, l.Try("ACTION", Engineer, Cmd(7, text: "after closure")).Status);
        Assert.All(l.Eval().Measures, m => Assert.Equal("ENDED_AT_CLOSURE", m.State));
        Assert.Empty(l.Eval().Alerts);
    }

    [Fact]
    public void Critical_defect_cannot_take_action_or_close_without_a_safeguard()
    {
        var l = new Lifecycle();
        l.Try("START", Engineer, Cmd(0, defectRevision: 1));
        Assert.Contains(l.Eval().Alerts, a => a.Code == "CRITICAL_DEFECT_WITHOUT_ACTIVE_SAFEGUARD" && a.Blocking);
        Assert.Equal(409, l.Try("ACTION", Engineer, Cmd(1, text: "skip assessment")).Status);
        l.Try("ASSESS", Engineer, Cmd(1, "Critical", text: "FICTIONAL critical assessment"));
        var action = l.Try("ACTION", Engineer, Cmd(2, text: "FICTIONAL action without safeguard"));
        Assert.Equal((422, "defect_safeguard_required"), (action.Status, action.Code));
        Assert.Contains("CRITICAL_SAFEGUARD_NOT_RECORDED", l.Eval().CloseBlockers);
        Assert.Equal(409, l.Try("CLOSE", Chief, Cmd(2, text: "FICTIONAL premature closure")).Status);
    }

    [Fact]
    public void Declared_critical_wins_over_a_lower_assessment_and_non_critical_may_skip_safeguard()
    {
        var declared = new Lifecycle("Critical");
        declared.Try("START", Engineer, Cmd(0, defectRevision: 1));
        declared.Try("ASSESS", Engineer, Cmd(1, "Low", text: "FICTIONAL low assessment"));
        Assert.True(declared.Eval().Critical);
        Assert.Equal(422, declared.Try("ACTION", Engineer, Cmd(2, text: "x")).Status);
        var low = new Lifecycle("Low");
        low.Try("START", Engineer, Cmd(0, defectRevision: 1));
        low.Try("ASSESS", Engineer, Cmd(1, "Low", text: "FICTIONAL low assessment"));
        Assert.Equal(200, low.Try("ACTION", Engineer, Cmd(2, text: "FICTIONAL cosmetic fix")).Status);
        Assert.DoesNotContain("CRITICAL_SAFEGUARD_NOT_RECORDED", low.Eval().CloseBlockers);
    }

    [Fact]
    public void Verifier_effectiveness_assessor_and_closer_must_not_be_an_action_owner()
    {
        var l = new Lifecycle();
        l.Try("START", Engineer, Cmd(0, defectRevision: 1));
        l.Try("ASSESS", Engineer, Cmd(1, "Critical", text: "FICTIONAL critical"));
        l.Try("SAFEGUARD", Engineer, Cmd(2, measure: "SAFEGUARD", text: "FICTIONAL standby pump", temporary: false));
        l.Try("ACTION", Engineer, Cmd(3, text: "FICTIONAL repair"));
        Assert.Equal((403, "separation_of_duties"), (l.Try("VERIFY", Engineer, Cmd(4, outcome: "VERIFIED", text: "FICTIONAL self check")) is var d ? (d.Status, d.Code) : default));
        Assert.Equal((403, "permission_denied"), (l.Try("VERIFY", Verifier, Cmd(4, outcome: "VERIFIED", text: "FICTIONAL verification"), permissions: ["defect.write"]) is var p ? (p.Status, p.Code) : default));
        Assert.Equal(200, l.Try("VERIFY", Verifier, Cmd(4, outcome: "VERIFIED", text: "FICTIONAL verification")).Status);
        Assert.Equal(403, l.Try("EFFECTIVENESS", Engineer, Cmd(5, outcome: "EFFECTIVE", text: "FICTIONAL self effectiveness")).Status);
        Assert.Equal(200, l.Try("EFFECTIVENESS", Verifier, Cmd(5, outcome: "EFFECTIVE", text: "FICTIONAL effective")).Status);
        Assert.Equal(403, l.Try("CLOSE", Engineer, Cmd(6, text: "FICTIONAL self closure")).Status);
        Assert.Equal((403, "permission_denied"), (l.Try("CLOSE", Chief, Cmd(6, text: "FICTIONAL closure"), permissions: ["defect.write", DefectLifecyclePolicy.VerifyPermission]) is var c ? (c.Status, c.Code) : default));
    }

    [Fact]
    public void Failed_verification_or_effectiveness_returns_to_action_and_new_action_invalidates_verification()
    {
        var l = new Lifecycle();
        l.Try("START", Engineer, Cmd(0, defectRevision: 1));
        l.Try("ASSESS", Engineer, Cmd(1, "Critical", text: "FICTIONAL critical"));
        l.Try("SAFEGUARD", Engineer, Cmd(2, measure: "SAFEGUARD", text: "FICTIONAL standby", temporary: false));
        l.Try("ACTION", Engineer, Cmd(3, text: "FICTIONAL repair"));
        Assert.Equal("ACTION", l.Try("VERIFY", Verifier, Cmd(4, outcome: "NOT_VERIFIED", text: "FICTIONAL leak persists")).StageAfter);
        l.Try("ACTION", Engineer, Cmd(5, text: "FICTIONAL second repair"));
        l.Try("VERIFY", Verifier, Cmd(6, outcome: "VERIFIED", text: "FICTIONAL verified"));
        Assert.Equal("ACTION", l.Try("EFFECTIVENESS", Verifier, Cmd(7, outcome: "NOT_EFFECTIVE", text: "FICTIONAL recurred")).StageAfter);
        Assert.Contains("VERIFICATION_NOT_RECORDED", l.Eval().CloseBlockers);
        // Re-verification needs a new corrective action first.
        Assert.Equal(409, l.Try("VERIFY", Verifier, Cmd(8, outcome: "VERIFIED", text: "FICTIONAL re-verify without action")).Status);
        Assert.Equal(200, l.Try("ACTION", Engineer, Cmd(8, text: "FICTIONAL third repair")).Status);
        Assert.Equal(200, l.Try("VERIFY", Verifier, Cmd(9, outcome: "VERIFIED", text: "FICTIONAL verified again")).Status);
    }

    [Fact]
    public void Temporary_measure_expiry_is_a_blocking_alert_and_blocks_closure()
    {
        var l = ToEffectiveness(temporary: true);
        var later = Now.AddDays(2);
        var e = l.Eval(later);
        Assert.Equal("EXPIRED", Assert.Single(e.Measures).State);
        Assert.Contains(e.Alerts, a => a.Code == "TEMPORARY_MEASURE_EXPIRED" && a.Blocking);
        Assert.Contains("TEMPORARY_MEASURE_EXPIRED", e.CloseBlockers);
        var close = l.Try("CLOSE", Chief, Cmd(6, text: "FICTIONAL closure"), at: later);
        Assert.Equal((422, "defect_closure_blocked"), (close.Status, close.Code));
        Assert.Contains("TEMPORARY_MEASURE_EXPIRED", close.Missing!);
        // A human lift of the lapsed measure is recorded, then closure is possible.
        var measure = e.Measures[0].Id;
        Assert.Equal(200, l.Try("LIFT", Chief, Cmd(6, text: "FICTIONAL no longer required"), measure, at: later).Status);
        Assert.Equal(200, l.Try("CLOSE", Chief, Cmd(7, text: "FICTIONAL returned to service"), at: later).Status);
    }

    [Fact]
    public void Last_active_safeguard_of_a_critical_defect_cannot_be_lifted_before_effectiveness()
    {
        var l = new Lifecycle();
        l.Try("START", Engineer, Cmd(0, defectRevision: 1));
        l.Try("ASSESS", Engineer, Cmd(1, "Critical", text: "FICTIONAL critical"));
        l.Try("SAFEGUARD", Engineer, Cmd(2, measure: "SAFEGUARD", text: "FICTIONAL standby", temporary: false));
        var id = l.Eval().Measures[0].Id;
        Assert.Equal((422, "defect_safeguard_required"), (l.Try("LIFT", Chief, Cmd(3, text: "FICTIONAL lifting too early"), id) is var d ? (d.Status, d.Code) : default));
        Assert.Equal(404, l.Try("LIFT", Chief, Cmd(3, text: "FICTIONAL unknown measure"), Guid.NewGuid()).Status);
    }

    [Fact]
    public void Declaration_change_after_verification_blocks_closure()
    {
        var l = ToEffectiveness();
        l.DefectRevision = 2;
        Assert.Contains("DECLARATION_CHANGED_SINCE_VERIFICATION", l.Eval().CloseBlockers);
    }

    [Theory]
    [InlineData("START", 1, null, null, null, null, null, 1L)]          // ExpectedRevision must be 0
    [InlineData("ASSESS", 1, "Severe", null, "rationale text", null, null, null)]
    [InlineData("ASSESS", 1, "Critical", null, "short", null, null, null)]
    [InlineData("SAFEGUARD", 1, null, "SAFEGUARD", "measure", true, null, null)]  // temporary without expiry
    [InlineData("SAFEGUARD", 1, null, "SAFEGUARD", "measure", true, "2026-11-19T08:00:00Z", null)] // already expired
    [InlineData("SAFEGUARD", 1, null, "SAFEGUARD", "measure", false, "2026-11-25T08:00:00Z", null)]
    [InlineData("SAFEGUARD", 1, null, "SAFEGUARD", "measure", true, "2026-11-25 08:00", null)]
    [InlineData("VERIFY", 1, null, null, "verified text", null, "MAYBE", null)]
    [InlineData("CLOSE", 1, "Critical", null, "closure statement", null, null, null)]
    public void Command_shape_is_closed_per_kind(string kind, long rev, string? criticality, string? measure, string? text, bool? temporary, string? expires, long? defectRevision)
    {
        Assert.False(DefectLifecyclePolicy.ValidShape(kind, new DefectLifecycleCommand(rev, criticality, measure, text, temporary, expires, null, defectRevision) with
            { Outcome = kind == "VERIFY" ? "MAYBE" : null }, Now, out _));
    }

    [Fact]
    public void Unknown_criticality_is_not_silently_healthy()
    {
        var l = new Lifecycle(null);
        l.Try("START", Engineer, Cmd(0, defectRevision: 1));
        var e = l.Eval();
        Assert.False(e.Critical);
        Assert.Contains(e.Alerts, a => a.Code == "CRITICALITY_UNKNOWN_WITHOUT_SAFEGUARD" && a.Blocking);
        Assert.Contains("CRITICALITY_NOT_ASSESSED", e.CloseBlockers);
    }
}
