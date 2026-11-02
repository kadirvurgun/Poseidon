using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class CrewLookupPolicyTests
{
    [Fact]
    public void Lookup_tuple_is_exact_scoped_versioned_and_effective_without_mixing_approved_dimensions() {
        var tenant = Guid.NewGuid(); var vessel = Guid.NewGuid(); var id = Guid.NewGuid();
        var position = new CrewPosition { TenantId = tenant, VesselId = vessel, Id = id, Code = "SYNTHETIC",
            Rank = "SYNTHETIC-R", Department = null, Provenance = "SyntheticUnverified" };
        var rule = new CrewPositionRule { TenantId = tenant, VesselId = vessel, PositionId = id, Code = "SYNTHETIC",
            Rank = "SYNTHETIC-R", Department = null, Provenance = "SyntheticUnverified", From = new(2026, 11, 1), To = new(2026, 12, 1) };
        var policy = new CrewPositionPolicy { Version = "synthetic-v1", SourceReference = "synthetic-test-only", TimeZone = "UTC",
            PeriodSemantics = "StartInclusiveEndExclusive", Positions = [rule] };
        Assert.True(policy.Allows(position, new(2026, 11, 1))); Assert.False(policy.Allows(position, new(2026, 12, 1)));
        position.Department = "invented"; Assert.False(policy.Allows(position, new(2026, 11, 1))); position.Department = null;
        position.TenantId = Guid.NewGuid(); Assert.False(policy.Allows(position, new(2026, 11, 1))); position.TenantId = tenant;
        Assert.False(policy.AllowsReference(tenant, vessel, id, "old", [position]));
        policy.Positions = [rule, rule]; Assert.False(policy.Configured);
    }
    [Fact]
    public void Missing_timezone_period_or_policy_is_unavailable_but_an_incomplete_draft_may_keep_position_unknown() {
        var policy = new CrewPositionPolicy();
        Assert.False(policy.Configured); Assert.True(policy.AllowsReference(Guid.NewGuid(), Guid.NewGuid(), null, null, []));
        policy.Version = "synthetic"; policy.SourceReference = "synthetic-test"; Assert.False(policy.Configured);
        policy.TimeZone = "UTC"; Assert.False(policy.Configured);
        policy.PeriodSemantics = "StartInclusiveEndExclusive"; Assert.True(policy.Configured);
        policy.TimeZone = "UNAVAILABLE-SYNTHETIC-ZONE"; Assert.False(policy.Configured);
    }
    [Fact]
    public void Identity_missing_field_identifier_or_natural_identity_policy_does_not_default_to_permission() {
        var policy = new CrewIdentityPolicy { Version = "synthetic", Purpose = "synthetic", SourceReference = "synthetic-test-only",
            TenantIds = [Guid.NewGuid()], EmployeeNumberMode = "ImmutableAfterCapture", Fields = [
                new() { Purpose = "synthetic", ReadCapability = "crew.profile.identity.read", WriteCapability = "crew.profile.identity.write" },
                new() { Field = Poseidon.Domain.CrewIdentityField.EmployeeNo, Purpose = "synthetic", ReadCapability = "crew.profile.identity.read", WriteCapability = "crew.profile.identity.write" }
            ] };
        Assert.False(policy.Configured);
        policy.Fields[0].Field = Poseidon.Domain.CrewIdentityField.FullName; Assert.True(policy.Configured);
        policy.EmployeeNumberMode = ""; Assert.False(policy.Configured);
    }
}
