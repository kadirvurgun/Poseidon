using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// USER-ADMIN-02 / UA2-AC-01, UA2-AC-02: token format, hashing, acceptance rule and the Invited lifecycle.
[Trait("Requirement", "USER-ADMIN-02")]
public sealed class MembershipInvitationPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("7b0c2f0e-3a51-4c9d-9d1e-5f2a8c6b4e10");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    private const string Issuer = "https://idp.example/realms/poseidon";

    private static InvitationFacts Facts(InvitationStatus status = InvitationStatus.Pending, string? expected = null, MembershipStatus membership = MembershipStatus.Invited,
        DateTimeOffset? expires = null) => new(status, expires ?? Now.AddDays(7), expected, Issuer, membership);

    [Fact]
    public void Tokens_are_high_entropy_single_format_and_round_trip_their_tenant()
    {
        var secrets = Enumerable.Range(0, 64).Select(_ => MembershipInvitationPolicy.NewSecret()).ToArray();
        Assert.Equal(64, secrets.Distinct(StringComparer.Ordinal).Count());
        Assert.All(secrets, s => Assert.Equal(43, s.Length));
        var token = MembershipInvitationPolicy.Format(Tenant, secrets[0]);
        Assert.Equal(MembershipInvitationPolicy.TokenLength, token.Length);
        Assert.True(MembershipInvitationPolicy.TryParse(token, out var tenant, out var secret));
        Assert.Equal((Tenant, secrets[0]), (tenant, secret));
        // The stored value is a 64-hex SHA-256 of the secret only; it never equals or contains the token.
        var hash = MembershipInvitationPolicy.Hash(secret);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(secret, hash, StringComparison.Ordinal);
        Assert.Equal(hash, MembershipInvitationPolicy.Hash(secret));
        Assert.NotEqual(hash, MembershipInvitationPolicy.Hash(secrets[1]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pi1.not-a-token")]
    [InlineData("pi2.7b0c2f0e3a514c9d9d1e5f2a8c6b4e10.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("pi1.7B0C2F0E3A514C9D9D1E5F2A8C6B4E10.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("pi1.00000000000000000000000000000000.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("pi1.7b0c2f0e3a514c9d9d1e5f2a8c6b4e10.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("pi1.7b0c2f0e3a514c9d9d1e5f2a8c6b4e10.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Malformed_tokens_are_rejected(string? token) => Assert.False(MembershipInvitationPolicy.TryParse(token, out _, out _));

    [Fact]
    public void Only_a_pending_unexpired_invitation_for_the_expected_identity_is_accepted()
    {
        Assert.Equal(InvitationRefusal.None, MembershipInvitationPolicy.Decide(Facts(), Issuer, "anyone", Now));
        Assert.Equal(InvitationRefusal.None, MembershipInvitationPolicy.Decide(Facts(expected: "holder"), Issuer, "holder", Now));
        Assert.Equal(InvitationRefusal.SubjectMismatch, MembershipInvitationPolicy.Decide(Facts(expected: "holder"), Issuer, "Holder", Now));
        Assert.Equal(InvitationRefusal.IssuerMismatch, MembershipInvitationPolicy.Decide(Facts(), "https://other.example", "holder", Now));
        Assert.Equal(InvitationRefusal.Expired, MembershipInvitationPolicy.Decide(Facts(expires: Now), Issuer, "holder", Now));
        foreach (var closed in new[] { InvitationStatus.Accepted, InvitationStatus.Revoked, InvitationStatus.Superseded })
            Assert.Equal(InvitationRefusal.Closed, MembershipInvitationPolicy.Decide(Facts(closed), Issuer, "holder", Now));
        foreach (var membership in new[] { MembershipStatus.Active, MembershipStatus.Suspended, MembershipStatus.Revoked })
            Assert.Equal(InvitationRefusal.MembershipNotInvited, MembershipInvitationPolicy.Decide(Facts(membership: membership), Issuer, "holder", Now));
    }

    [Fact]
    public void Refusals_have_registered_audit_codes_and_one_neutral_public_code()
    {
        foreach (var refusal in Enum.GetValues<InvitationRefusal>().Where(r => r != InvitationRefusal.None))
            Assert.StartsWith("invitation_", MembershipInvitationPolicy.AuditCode(refusal), StringComparison.Ordinal);
        Assert.Equal("invitation_unavailable", MembershipInvitationPolicy.NeutralRefusal);
    }

    [Fact]
    public void Expiry_is_derived_from_server_time_and_the_lifetime_is_the_source_default()
    {
        Assert.Equal(TimeSpan.FromHours(24), MembershipInvitationPolicy.Lifetime);
        Assert.Equal("Pending", MembershipInvitationPolicy.State(InvitationStatus.Pending, Now.AddSeconds(1), Now));
        Assert.Equal("Expired", MembershipInvitationPolicy.State(InvitationStatus.Pending, Now, Now));
        Assert.Equal("Accepted", MembershipInvitationPolicy.State(InvitationStatus.Accepted, Now.AddDays(-1), Now));
        foreach (var status in Enum.GetValues<InvitationStatus>())
            Assert.Equal(status, MembershipInvitationPolicy.Parse(MembershipInvitationPolicy.Wire(status)));
    }

    [Theory]
    [InlineData(" holder", false)]
    [InlineData("holder\n", false)]
    [InlineData("", false)]
    [InlineData("f3c1d2e4-0000-4000-8000-000000000001", true)]
    public void Expected_subjects_are_bounded_and_untrimmed(string subject, bool valid) =>
        Assert.Equal(valid, MembershipInvitationPolicy.ValidExpectedSubject(subject));

    [Fact]
    public void An_invited_membership_can_only_be_revoked_until_it_is_accepted()
    {
        var revoke = MembershipLifecyclePolicy.Decide(MembershipStatus.Invited, MembershipTransition.Revoke, false);
        Assert.Equal((true, MembershipStatus.Revoked, "InvitationRevoked"), (revoke.Allowed, revoke.Next, revoke.ReasonCode));
        foreach (var transition in new[] { MembershipTransition.Suspend, MembershipTransition.Resume, MembershipTransition.SecuritySuspend })
            Assert.False(MembershipLifecyclePolicy.Decide(MembershipStatus.Invited, transition, false).Allowed);
        // An invited membership never admits a session.
        Assert.False(MembershipLifecyclePolicy.AdmitsSession(MembershipStatus.Invited, 0, 0));
        Assert.Equal(MembershipStatus.Invited, MembershipLifecyclePolicy.Parse("Invited"));
        Assert.Equal("Invited", MembershipLifecyclePolicy.Wire(MembershipStatus.Invited));
    }
}
