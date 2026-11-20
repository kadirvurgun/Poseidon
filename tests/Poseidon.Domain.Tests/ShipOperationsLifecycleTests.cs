using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-101/T-OPS2-005 SOF finalization and DEV-102/T-OPS2-006 downtime lifecycle
// (60.01 Rev3 paragraphs 1636-1638, 1680-1688, T63-T67; 11.01 rows 2224/2239).
public sealed class ShipOperationsLifecycleTests
{
    private static IReadOnlyDictionary<string, ShipOpsFact> Port(Dictionary<string, ShipOpsFact> declared) {
        Assert.True(ShipOperationsPolicy.TryNormalize("Port", declared, out var fields, out _)); return fields;
    }
    private static IReadOnlyDictionary<string, DowntimeFact> Downtime(Dictionary<string, DowntimeFact> declared) {
        Assert.True(DowntimePolicy.TryNormalize(declared, out var fields, out _)); return fields;
    }
    private static readonly Dictionary<string, DowntimeFact> Complete = new() {
        ["downtime_type"] = new("Known", "Technical Failure"), ["start_at"] = new("Known", "2026-11-09T08:00:00Z"), ["end_at"] = new("Known", "2026-11-09T10:00:00Z"),
        ["cause"] = new("Known", "FICTIONAL cause"), ["operational_impact"] = new("Known", "FICTIONAL impact")
    };

    [Fact]
    public void Sof_finalization_requires_known_required_facts_and_explicit_acknowledgement_of_unresolvable_references() {
        var empty = Port(new());
        Assert.Equal(new[] { "activity_type", "event_start" }, ShipOperationsLifecycle.SofMissingRequired(empty).Order(StringComparer.Ordinal).ToArray());
        var fields = Port(new() { ["activity_type"] = new("Known", "Arrival"), ["event_start"] = new("Known", "2026-01-01T00:00:00Z") });
        Assert.Empty(ShipOperationsLifecycle.SofMissingRequired(fields));
        Assert.Equal(new[] { "port", "port_call_id", "voyage_id" }, ShipOperationsLifecycle.SofUnresolvedReferences(fields).Order(StringComparer.Ordinal).ToArray());
        Assert.False(ShipOperationsLifecycle.SofAcknowledged(fields, null));
        Assert.False(ShipOperationsLifecycle.SofAcknowledged(fields, ["voyage_id", "port"]));
        Assert.False(ShipOperationsLifecycle.SofAcknowledged(fields, ["voyage_id", "port", "port", "port_call_id"]));
        Assert.True(ShipOperationsLifecycle.SofAcknowledged(fields, ["voyage_id", "port_call_id", "port"]));
    }

    [Fact]
    public void Sof_state_is_server_owned_and_final_never_claims_signature_or_external_agreement() {
        var json = System.Text.Json.JsonSerializer.Serialize(Port(new()), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var vessel = Guid.NewGuid();
        Assert.Equal("DRAFT", ShipOperationsPolicy.ReadFields("Port", vessel, json)["sof_state"].Value);
        Assert.Equal("FINAL", ShipOperationsPolicy.ReadFields("Port", vessel, json, "FINAL")["sof_state"].Value);
        Assert.Equal("DRAFT", ShipOperationsPolicy.ReadFields("Port", vessel, json, "VERIFIED")["sof_state"].Value);
        var draft = ShipOperationsPolicy.Blockers("Port", Port(new()));
        var final = ShipOperationsLifecycle.SofBlockers(draft, "FINAL", 3);
        Assert.Contains("SOF_SIGNATURE_NOT_RECORDED", final); Assert.Contains("SOF_FINAL_IS_INTERNAL_NOT_EXTERNAL_AGREEMENT", final);
        Assert.DoesNotContain("SOF_FINALIZATION_UNAVAILABLE", final);
        Assert.Contains("SOF_AMENDED_AFTER_FINAL_REVIEW_REQUIRED", ShipOperationsLifecycle.SofBlockers(draft, "DRAFT", 3));
        Assert.DoesNotContain("SOF_AMENDED_AFTER_FINAL_REVIEW_REQUIRED", ShipOperationsLifecycle.SofBlockers(draft, "DRAFT", null));
    }

    [Theory]
    [InlineData(null, "SUBMIT_FOR_REVIEW", "IN_REVIEW")][InlineData("OPEN", "SUBMIT_FOR_REVIEW", "IN_REVIEW")]
    [InlineData("IN_REVIEW", "RETURN_FOR_CORRECTION", "OPEN")][InlineData("IN_REVIEW", "VERIFY", "VERIFIED")]
    [InlineData("VERIFIED", "CLOSE", "CLOSED")][InlineData(null, "LINK_CLAIM_EVIDENCE", null)]
    public void Downtime_lifecycle_follows_explicit_commands(string? from, string action, string? to) {
        Assert.True(ShipOperationsLifecycle.Valid(from, action)); Assert.Equal(to, ShipOperationsLifecycle.Next(from, action));
    }

    [Theory]
    [InlineData(null, "VERIFY")][InlineData(null, "CLOSE")][InlineData("IN_REVIEW", "CLOSE")][InlineData("CLOSED", "RETURN_FOR_CORRECTION")]
    [InlineData("VERIFIED", "LINK_CLAIM_EVIDENCE")][InlineData("IN_REVIEW", "SUBMIT_FOR_REVIEW")][InlineData(null, "APPROVE_CLAIM")]
    public void Downtime_lifecycle_has_no_shortcut_to_verified_or_closed(string? from, string action) =>
        Assert.False(ShipOperationsLifecycle.Valid(from, action));

    [Fact]
    public void Submitted_verified_and_closed_content_is_read_only() {
        Assert.True(ShipOperationsLifecycle.Editable(null)); Assert.True(ShipOperationsLifecycle.Editable("OPEN"));
        Assert.False(ShipOperationsLifecycle.Editable("IN_REVIEW")); Assert.False(ShipOperationsLifecycle.Editable("VERIFIED")); Assert.False(ShipOperationsLifecycle.Editable("CLOSED"));
        Assert.Equal("NOT_SUBMITTED", ShipOperationsLifecycle.State(null));
    }

    [Fact]
    public void Open_period_blocks_submission_and_verification() {
        var open = new Dictionary<string, DowntimeFact>(Complete); open.Remove("end_at");
        Assert.Equal(new[] { "end_at" }, ShipOperationsLifecycle.DowntimeMissingRequired(Downtime(open)));
        Assert.Empty(ShipOperationsLifecycle.DowntimeMissingRequired(Downtime(Complete)));
    }

    [Fact]
    public void Loss_of_hire_is_only_a_review_required_candidate_never_yes_or_no() {
        Assert.Equal("REVIEW_REQUIRED", ShipOperationsLifecycle.LossOfHireCandidate(Downtime(Complete)));
        Assert.Equal("UNKNOWN", ShipOperationsLifecycle.LossOfHireCandidate(Downtime(new())));
        var blockers = ShipOperationsLifecycle.DowntimeBlockers(DowntimePolicy.Blockers(Downtime(Complete)), null, 0, "REVIEW_REQUIRED");
        Assert.Contains("LOSS_OF_HIRE_CANDIDATE_HUMAN_REVIEW_REQUIRED", blockers); Assert.DoesNotContain("LOSS_OF_HIRE_NOT_ASSESSED", blockers);
        Assert.Contains("VERIFIED_CLOSURE_UNAVAILABLE", blockers);
    }

    [Fact]
    public void Verified_closure_and_evidence_links_carry_explicit_non_release_qualifiers() {
        var draft = DowntimePolicy.Blockers(Downtime(Complete));
        var verified = ShipOperationsLifecycle.DowntimeBlockers(draft, "VERIFIED", 2, "REVIEW_REQUIRED");
        Assert.DoesNotContain("VERIFIED_CLOSURE_UNAVAILABLE", verified); Assert.Contains("HUMAN_VERIFICATION_IS_NOT_SAFETY_RELEASE", verified);
        Assert.Contains("CLAIM_EVIDENCE_LINK_IS_NOT_VERIFICATION", verified); Assert.DoesNotContain("CLAIM_EVIDENCE_ADOPTION_UNAVAILABLE", verified);
        Assert.Contains("HUMAN_REVIEW_PENDING", ShipOperationsLifecycle.DowntimeBlockers(draft, "IN_REVIEW", 0, "REVIEW_REQUIRED"));
    }

    [Fact]
    public void Lifecycle_duties_follow_the_source_role_lists() {
        Assert.Equal(new[] { "Master", "ChiefOfficer", "ChiefEngineer" }, ShipOperationsLifecycle.SofDuties);
        Assert.Contains("FleetManager", ShipOperationsLifecycle.DowntimeDuties); Assert.DoesNotContain("FleetManager", ShipOperationsLifecycle.SofDuties);
        Assert.True(ShipOperationsLifecycle.AppointmentSource("Company appointment letter 2026/14"));
        Assert.False(ShipOperationsLifecycle.AppointmentSource(" ")); Assert.False(ShipOperationsLifecycle.AppointmentSource("x\u0000"));
    }
}
