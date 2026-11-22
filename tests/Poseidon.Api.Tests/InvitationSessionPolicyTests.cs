using System.Security.Claims;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-02 QA N3/N4/N6 unit coverage: the invitation-session decision applied by OnTokenValidated, the bounded
// attempt limiter and the pseudonymous identity reference. No host, IdP or database.
[Trait("Requirement", "USER-ADMIN-02")]
public sealed class InvitationSessionPolicyTests
{
    private const string Issuer = "https://idp.example.invalid/realms/poseidon";
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private sealed class Clock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ClaimsPrincipal Validated(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "oidc"));

    private static FederationSettings Settings() => new()
    {
        Issuer = Issuer,
        Memberships = [new Membership(Issuer, "configured-admin", Guid.NewGuid(), Guid.NewGuid(), ["directory.read"])]
    };

    [Fact]
    public void An_invitation_session_is_capped_at_ten_minutes_and_never_outlives_the_id_token()
    {
        var principal = Validated(("sub", "invitee"), ("iss", Issuer), ("email", "not-copied@example.invalid"), ("poseidon:tid", Guid.NewGuid().ToString()));
        var session = FoundationIdentity.InvitationSession(principal, "/app/accept-invitation", Settings(), Now.AddHours(1), Now);
        Assert.NotNull(session);
        Assert.Equal(Now.AddMinutes(10), session.Value.ExpiresUtc);
        // Only sub, iss and the server-stamped source survive; IdP or forged extra claims are dropped.
        Assert.Equal([("sub", "invitee"), ("iss", Issuer), (ActorResolver.SessionSourceClaim, ActorResolver.InvitationSource)],
            session.Value.Principal.Claims.Select(c => (c.Type, c.Value)).ToArray());
        Assert.Equal(Now.AddMinutes(4), FoundationIdentity.InvitationSession(principal, "/app/accept-invitation", Settings(), Now.AddMinutes(4), Now)!.Value.ExpiresUtc);
        Assert.Null(FoundationIdentity.InvitationSession(principal, "/app/accept-invitation", Settings(), Now, Now));
        var resolver = new ActorResolver(Settings());
        Assert.Equal(("" + Issuer, "invitee"), resolver.InvitationIdentityFor(session.Value.Principal));
        Assert.Null(resolver.Resolve(session.Value.Principal)); // grants nothing
    }

    [Theory]
    [InlineData("/app/accept-invitation", true)]
    [InlineData("/app/accept-invitation?x=1", true)]
    [InlineData("/app/accept-invitation#token=abc", true)]
    [InlineData(null, false)]
    [InlineData("/app", false)]
    [InlineData("/app/user-admin", false)]
    [InlineData("/app/accept-invitationX", false)]
    [InlineData("/app/accept-invitation/../user-admin", false)]
    [InlineData("/app/accept-invitation%2F..", false)]
    [InlineData("https://evil.example/app/accept-invitation", false)]
    [InlineData("//evil.example/app/accept-invitation", false)]
    public void Only_a_login_returning_to_the_accept_page_gets_an_invitation_session(string? redirect, bool issued)
    {
        var principal = Validated(("sub", "invitee"), ("iss", Issuer));
        Assert.Equal(issued, FoundationIdentity.InvitationSession(principal, redirect, Settings(), Now.AddHours(1), Now) != null);
    }

    [Fact]
    public void Configured_ambiguous_or_incomplete_identities_never_get_an_invitation_session()
    {
        var settings = Settings();
        Assert.Null(FoundationIdentity.InvitationSession(Validated(("sub", "configured-admin"), ("iss", Issuer)), "/app/accept-invitation", settings, Now.AddHours(1), Now));
        Assert.Null(FoundationIdentity.InvitationSession(Validated(("sub", "a"), ("sub", "b"), ("iss", Issuer)), "/app/accept-invitation", settings, Now.AddHours(1), Now));
        Assert.Null(FoundationIdentity.InvitationSession(Validated(("sub", "invitee")), "/app/accept-invitation", settings, Now.AddHours(1), Now));
        Assert.Null(FoundationIdentity.InvitationSession(new ClaimsPrincipal(new ClaimsIdentity()), "/app/accept-invitation", settings, Now.AddHours(1), Now));
    }

    private static InvitationAttemptLimiter Limiter(TimeProvider clock, int capacity = LocalRateLimitCounters.DefaultCapacity) =>
        new(new SharedRateLimiter(RateLimitPartitionKeys.Ephemeral(), null, new LocalRateLimitCounters(clock, capacity), clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance));

    private static RateLimitOutcome Try(InvitationAttemptLimiter limiter, string subject) =>
        limiter.AcquireAsync(Issuer, subject, TestContext.Current.CancellationToken).GetAwaiter().GetResult().Outcome;

    [Fact]
    public void The_limiter_counts_per_identity_and_audits_only_the_first_refusal_of_a_window()
    {
        var clock = new Clock(Now);
        var limiter = Limiter(clock);
        for (var i = 0; i < InvitationAttemptLimiter.AttemptsPerWindow; i++) Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "a"));
        Assert.Equal(RateLimitOutcome.Limited, Try(limiter, "a"));
        Assert.Equal(RateLimitOutcome.LimitedRepeat, Try(limiter, "a"));
        Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "b"));
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "a"));
    }

    [Fact]
    public void A_full_limiter_evicts_only_past_windows_and_never_resets_current_counters()
    {
        var clock = new Clock(Now);
        var counters = new LocalRateLimitCounters(clock, capacity: 3);
        var limiter = new InvitationAttemptLimiter(new SharedRateLimiter(RateLimitPartitionKeys.Ephemeral(), null, counters, clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SharedRateLimiter>.Instance));
        for (var i = 0; i <= InvitationAttemptLimiter.AttemptsPerWindow; i++) Try(limiter, "flooder");
        Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "b"));
        Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "c"));
        // Full with current-window identities: a new identity is refused (fail closed) and the flooder stays limited.
        Assert.Equal(RateLimitOutcome.LimitedRepeat, Try(limiter, "d"));
        Assert.Equal(RateLimitOutcome.LimitedRepeat, Try(limiter, "flooder"));
        Assert.Equal(3, counters.Tracked);
        // Next window: stale entries are evicted for a new identity; nothing exceeds the capacity.
        clock.Now = clock.Now.AddMinutes(15);
        Assert.Equal(RateLimitOutcome.Allowed, Try(limiter, "d"));
        Assert.True(counters.Tracked <= 3);
    }

    [Fact]
    public void The_identity_reference_is_keyed_stable_and_never_contains_the_subject()
    {
        var keyed = new InvitationIdentityReference("synthetic-only-secret");
        var reference = keyed.For(Issuer, "subject-4711");
        Assert.Matches("^idref:v1:[0-9a-f]{32}$", reference);
        Assert.Equal(reference, new InvitationIdentityReference("synthetic-only-secret").For(Issuer, "subject-4711"));
        Assert.NotEqual(reference, new InvitationIdentityReference("another-secret").For(Issuer, "subject-4711"));
        Assert.NotEqual(reference, keyed.For(Issuer, "subject-4712"));
        Assert.DoesNotContain("4711", reference);
        Assert.Matches("^idref:p1:[0-9a-f]{32}$", new InvitationIdentityReference(null).For(Issuer, "subject-4711"));
    }
}
