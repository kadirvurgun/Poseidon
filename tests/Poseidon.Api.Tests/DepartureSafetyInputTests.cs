using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S9 integration DEV-058 x DEV-060: the departure readiness assessor consumes the WP-B ISafetyEventBlockerSource read
// contract as its "safety" input. Unit level (no host, no database); FICTIONAL event ids only.
[Trait("Category", "Unit")]
public sealed class DepartureSafetyInputTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);

    private sealed class FixedSource(SafetyEventBlockerSnapshot snapshot) : ISafetyEventBlockerSource
    {
        public int Calls { get; private set; }
        public Task<SafetyEventBlockerSnapshot> ForVessel(PoseidonDbContext db, TrustedActor actor, Guid vessel, DateTimeOffset now, CancellationToken ct)
        { Calls++; Assert.Equal(Vessel, vessel); return Task.FromResult(snapshot); }
    }

    private static TrustedActor Actor(params string[] permissions) => new(Guid.NewGuid(), Tenant, ["directory.read", .. permissions], true, false, [new(Vessel, false)]);
    private static HttpContext Context(ISafetyEventBlockerSource source) =>
        new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(source).BuildServiceProvider() };

    [Fact]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    public async Task Blockers_present_from_the_safety_source_fail_the_safety_input_with_the_event_as_source() {
        var blocking = Guid.NewGuid();
        var source = new FixedSource(new(Vessel, "BLOCKERS_PRESENT",
            [new(blocking, "SE-FICTIONAL-1", "NEAR_MISS", "HIGH", "TRIAGED", "HIGH_SEVERITY_OPEN", true)], ["SeverityPolicyUnconfigured"], Now));
        var checks = await DepartureReadinessAssessor.Safety(Context(source), null!, Actor(SafetyEventPolicy.ReadPermission), Vessel, Now, CancellationToken.None);
        var check = Assert.Single(checks);
        Assert.Equal(("safety", DepartureReadinessEngine.Fail, "SAFETY_EVENT:HIGH_SEVERITY_OPEN", "SafetyEvent", (Guid?)blocking),
            (check.Category, check.Result, check.Reason, check.SourceKind, check.SourceId));
        Assert.Equal(1, source.Calls);
    }

    [Theory]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    [InlineData("REVIEW_REQUIRED", "UNKNOWN", "SAFETY_EVENT_REVIEW_REQUIRED")]
    [InlineData("UNAVAILABLE", "UNKNOWN", "SAFETY_EVENT_SOURCE_UNAVAILABLE")]
    [InlineData("NO_OPEN_HIGH_SEVERITY_EVENT", "PASS", "NO_OPEN_HIGH_SEVERITY_EVENT")]
    public async Task Safety_source_states_map_to_the_gate_results(string state, string result, string reason) {
        var source = new FixedSource(new(Vessel, state, [], ["SeverityPolicyUnconfigured"], Now));
        var checks = await DepartureReadinessAssessor.Safety(Context(source), null!, Actor(SafetyEventPolicy.ReadPermission), Vessel, Now, CancellationToken.None);
        Assert.Contains(checks, c => c.Category == "safety" && c.Result == result && c.Reason == reason);
        Assert.All(checks, c => Assert.Equal("safety", c.Category));
    }

    [Fact]
    [Trait("Requirement", "DEV-058")]
    [Trait("Requirement", "DEV-060")]
    public async Task Without_safety_event_read_the_real_source_is_unavailable_and_the_input_is_unknown_never_pass() {
        var checks = await DepartureReadinessAssessor.Safety(Context(new SafetyEventBlockerSource()), null!, Actor("departure.readiness.read"), Vessel, Now, CancellationToken.None);
        var check = Assert.Single(checks);
        Assert.Equal((DepartureReadinessEngine.Unknown, "SAFETY_EVENT_SOURCE_UNAVAILABLE"), (check.Result, check.Reason));
    }
}
