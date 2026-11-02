using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class RegulatoryQualificationPolicyTests
{
    private static QualificationContent Complete() => new(Convert.ToBase64String("FICTIONAL source bytes"u8.ToArray()), "FICTIONAL publisher", "1", Guid.NewGuid(), Guid.NewGuid(), "FICTIONAL review",
        [new("vessel.flag", "Declared"), new("vessel.gt", "HumanAttested", 24)], new(Guid.NewGuid(), "FICTIONAL frequency", "FICTIONAL evidence", "FICTIONAL verifier", 24, false, false, "synthetic/only"));
    [Fact]
    public void Draft_can_be_incomplete_but_review_and_qualification_cannot_be_manufactured()
    {
        var lifecycle = new RuleLifecycle("DRAFT", "2026-01-01"); var day = new DateOnly(2026, 11, 2);
        Assert.Null(RegulatoryQualificationPolicy.Transition(null, "DRAFT", new(), false, day, lifecycle));
        Assert.NotEmpty(RegulatoryQualificationPolicy.Missing(new()));
        var partial = Complete() with { Control = new(null, "FICTIONAL frequency", null, null, null, null, null, null) };
        RegulatoryQualificationPolicy.Validate(partial);
        Assert.Contains("CONTROL_POLICY_MISSING", RegulatoryQualificationPolicy.Missing(partial));
        Assert.Equal("incomplete_qualification", RegulatoryQualificationPolicy.Transition("DRAFT", "IN_REVIEW", new(), false, day, lifecycle));
        Assert.Equal("independent_reviewer_required", RegulatoryQualificationPolicy.Transition("IN_REVIEW", "IN_FORCE", Complete(), false, day, lifecycle));
        Assert.Null(RegulatoryQualificationPolicy.Transition("IN_REVIEW", "IN_FORCE", Complete(), true, day, lifecycle));
    }
    [Fact]
    public void Future_lifecycle_never_becomes_in_force_by_time_and_current_date_is_guarded()
    {
        var c = Complete(); var later = new RuleLifecycle("FUTURE_EFFECTIVE", "2027-01-01");
        Assert.Equal("outside_effective_dates", RegulatoryQualificationPolicy.Transition("ADOPTED", "IN_FORCE", c, true, new(2026, 11, 2), later));
        Assert.Equal("impact_review_required", RegulatoryQualificationPolicy.Transition("ADOPTED", "FUTURE_EFFECTIVE", c, true, new(2026, 11, 2), later));
        Assert.Equal("acceptance_trigger_required", RegulatoryQualificationPolicy.Transition("ADOPTED", "AWAITING", c, true, new(2026, 11, 2), later));
        Assert.Equal("superseding_rule_required", RegulatoryQualificationPolicy.Transition("IN_FORCE", "SUPERSEDED", c, true, new(2026, 11, 2), later));
    }
    [Fact]
    public void Verified_profile_policy_cannot_be_downgraded_to_declaration_and_source_bytes_have_exact_hash()
    {
        var c = Complete(); RegulatoryQualificationPolicy.Validate(c);
        Assert.Throws<ArgumentException>(() => RegulatoryQualificationPolicy.Validate(c with { Inputs = [new("vessel.gt", "Declared")] }));
        Assert.NotEqual(RegulatoryQualificationPolicy.SourceHash(c), RegulatoryQualificationPolicy.SourceHash(c with { SourceBytesBase64 = Convert.ToBase64String("FICTIONAL source bytes\n"u8.ToArray()) }));
        Assert.Throws<ArgumentException>(() => RegulatoryQualificationPolicy.Validate(c with { Inputs = [new("vessel.flag", "Declared"), new("vessel.flag", "HumanAttested")] }));
    }
}
