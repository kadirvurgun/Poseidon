using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5202 / P5202-AC-01..04: closed lifecycle, last-admin narrowing (54.02 Rev4
// paragraph:807), session admission and delegation bounds (52.02 §6).
[Trait("Requirement", "PLAN-1511-5202")]
public sealed class AccountMembershipPolicyTests
{
    [Theory]
    [InlineData(MembershipStatus.Active, MembershipTransition.Suspend, true, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Active, MembershipTransition.Revoke, true, MembershipStatus.Revoked)]
    [InlineData(MembershipStatus.Suspended, MembershipTransition.Revoke, true, MembershipStatus.Revoked)]
    [InlineData(MembershipStatus.Suspended, MembershipTransition.Resume, true, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Active, MembershipTransition.SecuritySuspend, true, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Active, MembershipTransition.Resume, false, MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended, MembershipTransition.Suspend, false, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Suspended, MembershipTransition.SecuritySuspend, false, MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Revoked, MembershipTransition.Resume, false, MembershipStatus.Revoked)]
    [InlineData(MembershipStatus.Revoked, MembershipTransition.Revoke, false, MembershipStatus.Revoked)]
    [InlineData(MembershipStatus.Revoked, MembershipTransition.Suspend, false, MembershipStatus.Revoked)]
    public void Lifecycle_is_a_closed_state_machine_and_revoked_is_terminal(MembershipStatus from, MembershipTransition transition, bool allowed, MembershipStatus next)
    {
        var decision = MembershipLifecyclePolicy.Decide(from, transition, false);
        Assert.Equal(allowed, decision.Allowed);
        Assert.Equal(next, decision.Next);
        Assert.Equal(allowed ? null : MembershipLifecyclePolicy.InvalidTransition, decision.ErrorCode);
        Assert.False(decision.RecoveryRequired);
    }

    [Theory]
    [InlineData(MembershipTransition.Suspend)]
    [InlineData(MembershipTransition.Revoke)]
    public void Voluntary_removal_of_the_last_active_administrator_is_rejected(MembershipTransition transition)
    {
        var decision = MembershipLifecyclePolicy.Decide(MembershipStatus.Active, transition, true);
        Assert.False(decision.Allowed);
        Assert.Equal(MembershipLifecyclePolicy.LastAdmin, decision.ErrorCode);
        Assert.Equal(MembershipStatus.Active, decision.Next);
    }

    [Fact]
    public void Forced_security_suspension_is_not_blocked_and_raises_recovery_instead_of_a_hidden_admin()
    {
        var decision = MembershipLifecyclePolicy.Decide(MembershipStatus.Active, MembershipTransition.SecuritySuspend, true);
        Assert.True(decision.Allowed);
        Assert.Equal(MembershipStatus.Suspended, decision.Next);
        Assert.True(decision.RecoveryRequired);
        Assert.Equal("SecuritySuspension", decision.ReasonCode);
        Assert.False(MembershipLifecyclePolicy.Decide(MembershipStatus.Active, MembershipTransition.SecuritySuspend, false).RecoveryRequired);
    }

    [Fact]
    public void Revoking_an_already_suspended_last_admin_does_not_remove_an_active_admin()
    {
        Assert.True(MembershipLifecyclePolicy.Decide(MembershipStatus.Suspended, MembershipTransition.Revoke, true).Allowed);
    }

    [Fact]
    public void Last_admin_requires_target_to_be_the_only_active_administrator()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        Assert.True(MembershipLifecyclePolicy.IsLastActiveAdministrator(a, [a]));
        Assert.True(MembershipLifecyclePolicy.IsLastActiveAdministrator(a, [a, a]));
        Assert.False(MembershipLifecyclePolicy.IsLastActiveAdministrator(a, [a, b]));
        Assert.False(MembershipLifecyclePolicy.IsLastActiveAdministrator(a, [b]));
        Assert.False(MembershipLifecyclePolicy.IsLastActiveAdministrator(a, []));
    }

    [Theory]
    [InlineData(null, 0L, 0L, true)]   // Not imported: operator bootstrap source governs.
    [InlineData(MembershipStatus.Active, 0L, 0L, true)]
    [InlineData(MembershipStatus.Active, 2L, 2L, true)]
    [InlineData(MembershipStatus.Active, 2L, 0L, false)]  // Resumed after a revocation: old session stays dead.
    [InlineData(MembershipStatus.Active, 0L, -1L, false)] // Malformed stamp.
    [InlineData(MembershipStatus.Suspended, 1L, 1L, false)]
    [InlineData(MembershipStatus.Revoked, 1L, 5L, false)]
    public void Session_admission_fails_closed_on_inactive_or_newer_access_revision(MembershipStatus? status, long persisted, long session, bool admitted) =>
        Assert.Equal(admitted, MembershipLifecyclePolicy.AdmitsSession(status, persisted, session));

    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static DelegationRequest Request(string[]? permissions = null, DateTimeOffset? from = null, DateTimeOffset? until = null, Guid? delegateId = null) =>
        new(Guid.Parse("10000000-0000-4000-8000-000000000001"), delegateId ?? Guid.Parse("10000000-0000-4000-8000-000000000002"),
            permissions ?? ["crew.read"], from ?? Now, until ?? Now.AddDays(7));

    [Fact]
    public void Valid_delegation_is_bounded_by_the_source_authority()
    {
        Assert.Empty(DelegationPolicy.Validate(Request(), ["crew.read", "crew.write"], Now));
        Assert.Equal(["exceeds_source_authority"], DelegationPolicy.Validate(Request(["crew.write"]), ["crew.read"], Now)["permissions"]);
    }

    [Theory]
    [InlineData("membership.manage")]
    [InlineData("membership.security.suspend")]
    [InlineData("delegation.create")]
    [InlineData("audit.read")]
    [InlineData("directory.read")]
    public void Administrative_and_redelegation_permissions_are_never_delegable(string permission) =>
        Assert.Equal(["not_delegable"], DelegationPolicy.Validate(Request([permission]), [permission], Now)["permissions"]);

    [Fact]
    public void Delegation_window_and_identity_are_validated()
    {
        var self = Guid.Parse("10000000-0000-4000-8000-000000000001");
        Assert.Equal(["self_delegation"], DelegationPolicy.Validate(Request(delegateId: self), ["crew.read"], Now)["delegateMembershipId"]);
        Assert.Equal(["must_follow_validFrom"], DelegationPolicy.Validate(Request(until: Now), ["crew.read"], Now)["validUntil"]);
        Assert.Equal(["window_too_long"], DelegationPolicy.Validate(Request(until: Now.AddDays(91)), ["crew.read"], Now)["validUntil"]);
        Assert.Equal(["backdated"], DelegationPolicy.Validate(Request(from: Now.AddHours(-1)), ["crew.read"], Now)["validFrom"]);
        Assert.Equal(["required"], DelegationPolicy.Validate(Request([]), ["crew.read"], Now)["permissions"]);
        Assert.Equal(["duplicate"], DelegationPolicy.Validate(Request(["crew.read", "crew.read"]), ["crew.read"], Now)["permissions"]);
        Assert.Contains("validUntil", DelegationPolicy.Validate(new(Guid.NewGuid(), Guid.NewGuid(), ["crew.read"], Now, null), ["crew.read"], Now).Keys);
    }

    [Fact]
    public void Delegation_state_is_computed_from_server_time_and_never_extends_itself()
    {
        var from = Now; var until = Now.AddDays(1);
        Assert.Equal("Pending", DelegationPolicy.State(false, from, until, Now.AddMinutes(-1), true, true));
        Assert.Equal("Effective", DelegationPolicy.State(false, from, until, Now, true, true));
        Assert.Equal("Expired", DelegationPolicy.State(false, from, until, until, true, true));
        Assert.Equal("Ineffective", DelegationPolicy.State(false, from, until, Now, false, true));
        Assert.Equal("Ineffective", DelegationPolicy.State(false, from, until, Now, true, false));
        Assert.Equal("Revoked", DelegationPolicy.State(true, from, until, Now, true, true));
        Assert.Equal("a,b", DelegationPolicy.Canonical(["b", "a", "b"]));
    }
}
