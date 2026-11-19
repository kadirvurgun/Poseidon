using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-097 / T-OPS2-001, DEV-098 / T-OPS2-002: owning lookup registry. Pure, no
// host or database: only paths that never reach a reader's database query.
[Trait("Requirement", "DEV-097")]
[Trait("Requirement", "DEV-098")]
public sealed class DailyReferenceRegistryTests
{
    private static DailyReferenceRegistry Registry(params IDailyReferenceReader[] extra) =>
        new([new DailyCrewAssignmentReferenceReader(new CrewAssignmentPolicy(), new CrewPositionPolicy(), new FederationSettings()),new DailyPortCallReferenceReader(), new DailyEvidenceReferenceReader(), .. extra]);
    private static TrustedActor Actor(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), ["directory.read", .. permissions], true, false, []) { RoleContext = Guid.NewGuid().ToString("D") };
    private static DailyReferenceContext Context(string kind, TrustedActor actor) => new(null!, actor, Guid.NewGuid(), kind, DateTimeOffset.UnixEpoch);

    private sealed class FakeEngineReader(string field) : IDailyReferenceReader
    {
        public string Source => "work-order";
        public bool Supports(string kind, string key) => kind == "Engine" && key == field;
        public bool Available(TrustedActor actor) => true;
        public Task<DailyReferenceCheck> Resolve(DailyReferenceContext c, DailyReferencePin pin, CancellationToken ct) => Task.FromResult(new DailyReferenceCheck("Current", pin.Revision, "TEST"));
        public Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext c, string key, int take, CancellationToken ct) => Task.FromResult(Array.Empty<DailyReferenceOptionDto>());
    }

    // Deck officer_of_watch reader stand-in: effective only on 2026-11-19; resolution depends on log_date.
    private sealed class FakeWatchReader : IDailyReferenceReader
    {
        public int Calls { get; private set; }
        public string Source => "crew-assignment";
        public bool Supports(string kind, string key) => kind == "Deck" && key == "officer_of_watch";
        public bool Available(TrustedActor actor) => true;
        public IReadOnlyCollection<string> DependsOn => ["log_date"];
        public Task<DailyReferenceCheck> Resolve(DailyReferenceContext c, DailyReferencePin pin, CancellationToken ct) {
            Calls++;
            return Task.FromResult(c.Fields?.GetValueOrDefault("log_date")?.Value == "2026-11-19"
                ? new DailyReferenceCheck("Current", pin.Revision, "TEST") : new DailyReferenceCheck("ReviewRequired", pin.Revision, "ASSIGNMENT_NOT_EFFECTIVE_ON_LOG_DATE"));
        }
        public Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext c, string key, int take, CancellationToken ct) => Task.FromResult(Array.Empty<DailyReferenceOptionDto>());
    }

    [Fact]
    public async Task Changing_log_date_reverifies_an_unchanged_officer_of_watch_pin() {
        var reader = new FakeWatchReader(); var registry = new DailyReferenceRegistry([reader]); var ct = TestContext.Current.CancellationToken;
        Assert.Contains("log_date", new DailyCrewAssignmentReferenceReader(new CrewAssignmentPolicy(), new CrewPositionPolicy(), new FederationSettings()).DependsOn);
        var pin = new DailyReferencePin("crew-assignment", Guid.NewGuid(), 1).Token;
        Dictionary<string, DailyValue> Fields(string date) => new() { ["officer_of_watch"] = new("Known", pin), ["log_date"] = new("Known", date) };
        DailyReferenceContext Ctx(Dictionary<string, DailyValue> fields) => new(null!, Actor("crew.read"), Guid.NewGuid(), "Deck", DateTimeOffset.UnixEpoch, fields);
        var saved = Fields("2026-11-19");
        // Unrelated edit: the pin and its dependency are unchanged, so it is not re-resolved.
        Assert.True(await registry.VerifyChanged(Ctx(saved), saved, saved, ct)); Assert.Equal(0, reader.Calls);
        // A new log_date on which the assignment is not effective rejects the unchanged pin.
        var moved = Fields("2026-11-20");
        Assert.False(await registry.VerifyChanged(Ctx(moved), moved, saved, ct)); Assert.Equal(1, reader.Calls);
        // A log_date change where the pin remains effective is accepted after re-verification.
        Assert.True(await registry.VerifyChanged(Ctx(saved), saved, moved, ct)); Assert.Equal(2, reader.Calls);
    }

    [Fact]
    public void Owning_readers_resolve_only_their_source_fields_and_voyage_stays_unavailable() {
        var registry = Registry();
        Assert.True(registry.Resolvable("Deck", "officer_of_watch"));
        Assert.True(registry.Resolvable("Noon", "from_port")); Assert.True(registry.Resolvable("Noon", "to_port"));
        Assert.True(registry.Resolvable("Noon", "evidence_ref")); Assert.True(registry.Resolvable("Deck", "evidence_ref"));
        // No owning voyage registry exists in this product version.
        Assert.False(registry.Resolvable("Noon", "voyage_id"));
        // Engine references belong to the DEV-099 owner; none are registered here.
        foreach (var key in new[] { "work_orders", "watch_engineer", "evidence_ref" }) Assert.False(registry.Resolvable("Engine", key));
        // Server-owned and non-reference keys can never become resolvable.
        Assert.False(registry.Resolvable("Noon", "vessel_id")); Assert.False(registry.Resolvable("Noon", "activity"));
        var noon = registry.Schema("Noon");
        Assert.True(noon.Single(f => f.Key == "voyage_id").ReferenceUnavailable);
        Assert.False(noon.Single(f => f.Key == "from_port").ReferenceUnavailable);
        Assert.True(noon.Single(f => f.Key == "vessel_id").ReadOnly);
        // Capability projection never changes the source schema hash input.
        Assert.Equal(DailyOperationsPolicy.Schema("Noon").Select(f => f.Key), noon.Select(f => f.Key));
    }

    [Fact]
    public void Another_package_plugs_an_engine_reader_without_editing_the_policy_and_ambiguity_fails_closed() {
        var plugged = Registry(new FakeEngineReader("work_orders"));
        Assert.True(plugged.Resolvable("Engine", "work_orders"));
        Assert.False(plugged.Schema("Engine").Single(f => f.Key == "work_orders").ReferenceUnavailable);
        var ambiguous = Registry(new FakeEngineReader("work_orders"), new FakeEngineReader("work_orders"));
        Assert.False(ambiguous.Resolvable("Engine", "work_orders"));
        Assert.Null(ambiguous.For("Engine", "work_orders"));
    }

    [Fact]
    public async Task Unknown_missing_unregistered_invalid_and_unreadable_references_never_become_current() {
        var registry = Registry(); var ct = TestContext.Current.CancellationToken;
        var pin = new DailyReferencePin("crew-assignment", Guid.NewGuid(), 2).Token;
        var noCrew = Context("Deck", Actor("daily.deck.read"));
        Assert.Equal("Unknown", (await registry.Describe(noCrew, "officer_of_watch", new("Unknown", null), ct)).State);
        Assert.Equal("Missing", (await registry.Describe(noCrew, "officer_of_watch", null, ct)).State);
        Assert.Equal("SourceUnavailable", (await registry.Describe(Context("Noon", Actor()), "voyage_id", new("Known", "voyage:" + Guid.NewGuid() + "@1"), ct)).State);
        Assert.Equal("Invalid", (await registry.Describe(noCrew, "officer_of_watch", new("Known", "evidence:" + Guid.NewGuid() + "@1"), ct)).State);
        var hidden = await registry.Describe(noCrew, "officer_of_watch", new("Known", pin), ct);
        Assert.Equal("NotVisible", hidden.State); Assert.Null(hidden.Id);
        // An unchanged earlier pin is not re-resolved on a later unrelated edit.
        var fields = new Dictionary<string, DailyValue> { ["officer_of_watch"] = new("Known", pin) };
        Assert.True(await registry.VerifyChanged(noCrew, fields, fields, ct));
        // A changed pin that the actor cannot resolve is rejected (never Known by fallback).
        Assert.False(await registry.VerifyChanged(noCrew, fields, new Dictionary<string, DailyValue>(), ct));
    }
}
