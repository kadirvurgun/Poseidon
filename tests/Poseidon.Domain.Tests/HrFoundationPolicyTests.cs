using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5201 / 52.01 Rev6 paragraph:49 (half-open periods), :52 (no default reference), :93 (masked registration),
// :277/:327 (employment activation), :404-:408 (plan vs actual), :557/:561/:563 (actual intake), 59.01 paragraph:273-286 (role cardinality).
[Trait("Requirement", "PLAN-1511-5201")]
public sealed class HrFoundationPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static HrPeriod P(int fromDays, int? toDays) => new(T0.AddDays(fromDays), toDays is { } d ? T0.AddDays(d) : null);

    [Fact]
    public void Periods_are_half_open_and_open_ended_periods_cover_the_future()
    {
        Assert.False(P(0, 10).Overlaps(P(10, 20)));            // end is exclusive
        Assert.True(P(0, 11).Overlaps(P(10, 20)));
        Assert.True(P(0, null).Overlaps(P(500, 501)));
        Assert.True(P(0, null).Covers(P(5, 10)));
        Assert.False(P(0, 10).Covers(P(5, null)));            // a bounded parent never covers an open child
        Assert.False(P(1, 10).Covers(P(0, 5)));
        Assert.False(new HrPeriod(T0, T0).IsValid);
    }

    [Fact]
    public void Legal_entity_drafts_may_be_incomplete_but_activation_lists_every_missing_field_without_defaults()
    {
        Assert.Empty(HrLegalEntityPolicy.Validate(null, null, null, null, null, null));
        Assert.Equal(["legalName", "countryCode", "hrTimeZone", "baseLocale", "effectiveFrom"],
            HrLegalEntityPolicy.MissingForActivation(null, null, null, null, null));
        Assert.Empty(HrLegalEntityPolicy.MissingForActivation("Synthetic Denizcilik AS", "TR", "Europe/Istanbul", "tr-TR", T0));
        var errors = HrLegalEntityPolicy.Validate(" ", "tr", "Turkey Standard Time", "de-DE", T0, T0);
        Assert.Equal(["baseLocale", "countryCode", "effectiveTo", "hrTimeZone", "legalName"], errors.Keys.Order(StringComparer.Ordinal));
        Assert.True(HrLegalEntityPolicy.ValidTimeZone("UTC"));
        Assert.True(HrLegalEntityPolicy.ValidTimeZone("Europe/Istanbul"));
        Assert.False(HrLegalEntityPolicy.ValidTimeZone("Mars/Olympus"));
    }

    [Theory]
    [InlineData("Draft", "Active", true)]
    [InlineData("Active", "Inactive", true)]
    [InlineData("Inactive", "Active", true)]
    [InlineData("Draft", "Inactive", false)]
    [InlineData("Active", "Draft", false)]
    public void Legal_entity_status_transitions_are_closed(string from, string to, bool allowed) =>
        Assert.Equal(allowed, HrLegalEntityPolicy.CanTransition(from, to));

    [Fact]
    public void Registration_identifiers_are_typed_normalized_and_masked_in_lists()
    {
        Assert.Empty(HrIdentifierPolicy.Validate("TradeRegistry", "tr-istanbul-ticaret-sicili", "123 456"));
        Assert.Equal("123456", HrIdentifierPolicy.Normalize(" 123 456 "));
        Assert.Equal("TR-ISTANBUL", HrIdentifierPolicy.NormalizeIssuer(" tr-istanbul "));
        Assert.Contains("value", HrIdentifierPolicy.Validate("ImoCompanyNumber", "IMO", "12345").Keys);
        Assert.Empty(HrIdentifierPolicy.Validate("ImoCompanyNumber", "IMO", "1234567"));
        Assert.Contains("value", HrIdentifierPolicy.Validate("Lei", "GLEIF", "SHORT").Keys);
        Assert.Contains("type", HrIdentifierPolicy.Validate("Passport", "X", "1").Keys);
        Assert.Equal("******3456", HrIdentifierPolicy.Mask("SYN0123456"));
        Assert.Equal("***", HrIdentifierPolicy.Mask("ABC"));
    }

    [Fact]
    public void Role_cardinality_follows_source_ownership_and_operator_rules()
    {
        var vessel = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var current = new[] {
            new HrRolePolicy.Existing(Guid.NewGuid(), a, "Operator", vessel, P(0, 100)),
            new HrRolePolicy.Existing(Guid.NewGuid(), a, "RegisteredOwner", vessel, P(0, null)),
            new HrRolePolicy.Existing(Guid.NewGuid(), a, "Employer", null, P(0, null))
        };
        // At most one operator per vessel and period; a different company may follow after the end.
        Assert.NotNull(HrRolePolicy.Conflict(current, b, "Operator", vessel, P(50, 150)));
        Assert.Null(HrRolePolicy.Conflict(current, b, "Operator", vessel, P(100, 150)));
        // Co-ownership is possible, but the same owner cannot overlap itself.
        Assert.Null(HrRolePolicy.Conflict(current, b, "RegisteredOwner", vessel, P(10, 20)));
        Assert.NotNull(HrRolePolicy.Conflict(current, a, "RegisteredOwner", vessel, P(10, 20)));
        Assert.NotNull(HrRolePolicy.Conflict(current, a, "Employer", null, P(10, 20)));
        Assert.Null(HrRolePolicy.Conflict(current, b, "Employer", null, P(10, 20)));
        Assert.Contains("vesselId", HrRolePolicy.Validate("Operator", null, T0, null).Keys);
        Assert.Contains("vesselId", HrRolePolicy.Validate("Employer", vessel, T0, null).Keys);
        Assert.Contains("role", HrRolePolicy.Validate("Payroll", null, T0, null).Keys);
    }

    [Fact]
    public void Person_verification_is_independent_and_unknown_type_is_never_converted()
    {
        Assert.Equal("Unknown", HrPersonPolicy.Types[0]);
        Assert.Equal("hr_reference_incomplete", HrPersonPolicy.VerifyRejection("Draft", "Unknown", "a", "b"));
        Assert.Equal("separation_of_duties", HrPersonPolicy.VerifyRejection("Draft", "Seafarer", "a", "a"));
        Assert.Equal("separation_of_duties", HrPersonPolicy.VerifyRejection("Draft", "Seafarer", null, "a"));
        Assert.Equal("invalid_transition", HrPersonPolicy.VerifyRejection("Verified", "Seafarer", "a", "b"));
        Assert.Null(HrPersonPolicy.VerifyRejection("Draft", "Seafarer", "a", "b"));
    }

    [Fact]
    public void Employment_activation_requires_verified_person_active_employer_and_independent_approval()
    {
        var employment = P(10, 200); var entity = P(0, null); var employer = new[] { P(0, null) };
        Assert.Null(HrEmploymentPolicy.ActivationRejection("Verified", "Active", entity, employer, employment, "creator", "approver"));
        Assert.Equal("separation_of_duties", HrEmploymentPolicy.ActivationRejection("Verified", "Active", entity, employer, employment, "same", "same"));
        Assert.Equal("hr_person_not_verified", HrEmploymentPolicy.ActivationRejection("Draft", "Active", entity, employer, employment, "c", "a"));
        Assert.Equal("hr_legal_entity_inactive", HrEmploymentPolicy.ActivationRejection("Verified", "Draft", entity, employer, employment, "c", "a"));
        Assert.Equal("hr_legal_entity_inactive", HrEmploymentPolicy.ActivationRejection("Verified", "Active", P(20, null), employer, employment, "c", "a"));
        Assert.Equal("hr_employer_role_missing", HrEmploymentPolicy.ActivationRejection("Verified", "Active", entity, [P(0, 50)], employment, "c", "a"));
        Assert.Contains("employmentType", HrEmploymentPolicy.Validate("E1", "Payroll", T0, null).Keys);
        Assert.Contains("endAt", HrEmploymentPolicy.Validate("E1", "Employee", T0, T0).Keys);
        Assert.False(HrEmploymentPolicy.CanTransition("Ended", "Active"));
        Assert.False(HrEmploymentPolicy.CanTransition("Draft", "Ended"));
    }

    [Fact]
    public void Approved_occupancy_never_overlaps_and_plan_must_sit_inside_employment_with_operator_path()
    {
        var employment = P(0, 365); var plan = P(30, 120);
        Assert.Null(HrAssignmentPolicy.ApprovalRejection("Active", employment, plan, true, [P(0, null)], [P(0, 30), P(120, 200)], "planner", "approver"));
        Assert.Equal("separation_of_duties", HrAssignmentPolicy.ApprovalRejection("Active", employment, plan, true, [P(0, null)], [], "x", "x"));
        Assert.Equal("hr_employment_not_active", HrAssignmentPolicy.ApprovalRejection("Draft", employment, plan, true, [P(0, null)], [], "p", "a"));
        Assert.Equal("hr_period_outside_employment", HrAssignmentPolicy.ApprovalRejection("Active", P(40, 365), plan, true, [P(0, null)], [], "p", "a"));
        Assert.Equal("hr_operator_path_missing", HrAssignmentPolicy.ApprovalRejection("Active", employment, plan, true, [P(0, 60)], [], "p", "a"));
        Assert.Null(HrAssignmentPolicy.ApprovalRejection("Active", employment, plan, false, [], [], "p", "a"));
        Assert.Equal("hr_assignment_overlap", HrAssignmentPolicy.ApprovalRejection("Active", employment, plan, true, [P(0, null)], [P(100, 130)], "p", "a"));
        Assert.Contains("plannedTo", HrAssignmentPolicy.Validate("Vessel", Guid.NewGuid(), null, T0, null, "UTC").Keys);
        Assert.Contains("vesselId", HrAssignmentPolicy.Validate("Shore", Guid.NewGuid(), null, T0, T0.AddDays(1), "UTC").Keys);
        Assert.Contains("plannedZoneId", HrAssignmentPolicy.Validate("Shore", null, null, T0, T0.AddDays(1), null).Keys);
    }

    [Fact]
    public void Actual_facts_are_separate_from_the_plan_and_doubtful_facts_are_retained_for_reconciliation()
    {
        var now = T0.AddDays(10);
        Assert.Equal(("Valid", "PendingReconciliation"), HrActualTimeline.Classify([], "CheckIn", T0, planApproved: false, now));
        Assert.Equal(("Suspect", "PendingReconciliation"), HrActualTimeline.Classify([], "CheckIn", now.AddHours(1), true, now));
        Assert.Equal(("Valid", "Accepted"), HrActualTimeline.Classify([], "CheckIn", T0, true, now));
        var checkedIn = new[] { new HrActualTimeline.Fact(Guid.NewGuid(), "CheckIn", T0, "Valid", "Accepted") };
        // A second check-in while open is kept, never adds a duplicate session.
        Assert.Equal(("Valid", "PendingReconciliation"), HrActualTimeline.Classify(checkedIn, "CheckIn", T0.AddHours(2), true, now));
        Assert.Equal(("Valid", "PendingReconciliation"), HrActualTimeline.Classify(checkedIn, "CheckOut", T0.AddHours(-1), true, now));
        Assert.Equal(("Valid", "Accepted"), HrActualTimeline.Classify(checkedIn, "CheckOut", T0.AddDays(3), true, now));
        Assert.Equal(("Valid", "PendingReconciliation"), HrActualTimeline.Classify([], "CheckOut", T0, true, now));

        var none = HrActualTimeline.Derive([]);
        Assert.Equal("NotStarted", none.Status); Assert.Null(none.ActualFrom); Assert.Null(none.ActualTo);
        var open = HrActualTimeline.Derive([.. checkedIn, new(Guid.NewGuid(), "CheckIn", T0.AddHours(2), "Valid", "PendingReconciliation")]);
        Assert.Equal("InProgress", open.Status); Assert.Equal(T0, open.ActualFrom); Assert.Null(open.ActualTo); Assert.Equal(1, open.PendingReconciliation);
        var closed = HrActualTimeline.Derive([.. checkedIn, new(Guid.NewGuid(), "CheckOut", T0.AddDays(3), "Valid", "Accepted")]);
        Assert.Equal("Completed", closed.Status); Assert.Equal(T0.AddDays(3), closed.ActualTo); Assert.Single(closed.Sessions);
    }
}
