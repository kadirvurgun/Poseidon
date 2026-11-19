using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-034 / T-S4-002 (18.01 Backlog row 35): server-authoritative reconnect classification.
[Trait("Requirement", "DEV-034")]
public sealed class SyncOperationReconciliationTests
{
    private static readonly Guid Vessel = Guid.Parse("cccc0000-0000-0000-0000-000000000001");
    private static readonly Guid Resource = Guid.Parse("cccc0000-0000-0000-0000-000000000002");
    private static readonly Guid Operation = Guid.Parse("cccc0000-0000-0000-0000-000000000003");
    private static readonly string Digest = new('a', 64);
    private static SyncOperationReference Request(string? digest = null, string domain = SyncOperationReconciliation.CrewWatchGroup) =>
        new(Operation, domain, Resource, digest ?? Digest);
    private static SyncStoredOutcome Stored(string? digest = null, string domain = SyncOperationReconciliation.CrewWatchGroup,
        Guid? resource = null, Guid? vessel = null, bool current = true) =>
        new(Operation, domain, resource ?? Resource, vessel ?? Vessel, digest ?? Digest.ToUpperInvariant(), "Committed", current);

    [Fact]
    public void Same_key_and_same_payload_recovers_the_recorded_receipt_case_insensitively()
    {
        // Server digests are uppercase hex; the browser computes lowercase hex.
        Assert.Equal(SyncOperationState.Recorded, SyncOperationReconciliation.Classify(Request(), Vessel, true, Stored()));
    }

    [Fact]
    public void Same_key_with_different_payload_is_a_mismatch_never_a_recorded_success()
    {
        Assert.Equal(SyncOperationState.PayloadMismatch,
            SyncOperationReconciliation.Classify(Request(new string('b', 64)), Vessel, true, Stored()));
    }

    [Fact]
    public void Missing_receipt_stays_not_received_and_is_never_inferred_as_saved()
    {
        Assert.Equal(SyncOperationState.NotReceived, SyncOperationReconciliation.Classify(Request(), Vessel, true, null));
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("vessel")]
    [InlineData("domain")]
    public void Key_bound_to_another_resource_vessel_or_domain_is_reuse_without_outcome(string part)
    {
        var other = Guid.Parse("dddd0000-0000-0000-0000-000000000001");
        var stored = part switch
        {
            "resource" => Stored(resource: other),
            "vessel" => Stored(vessel: other),
            _ => Stored(domain: SyncOperationReconciliation.TugPlanWindow),
        };
        Assert.Equal(SyncOperationState.KeyReused, SyncOperationReconciliation.Classify(Request(), Vessel, true, stored));
    }

    [Fact]
    public void Current_rights_precede_any_receipt_disclosure()
    {
        Assert.Equal(SyncOperationState.Unavailable, SyncOperationReconciliation.Classify(Request(), Vessel, false, Stored()));
        Assert.Equal(SyncOperationState.Unavailable, SyncOperationReconciliation.Classify(Request(), Vessel, false, null));
    }

    [Fact]
    public void Revoked_or_policy_withdrawn_enrollment_withholds_the_receipt()
    {
        Assert.Equal(SyncOperationState.Unavailable, SyncOperationReconciliation.Classify(Request(), Vessel, true, Stored(current: false)));
    }

    [Fact]
    public void Admissible_batch_is_bounded_unique_and_typed()
    {
        Assert.Null(SyncOperationReconciliation.Validate([Request()]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate(null));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request(), Request()]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request(domain: "Unsupported")]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request(new string('g', 64))]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request(new string('a', 63))]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request() with { OperationId = Guid.Empty }]));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([Request() with { ResourceId = Guid.Empty }]));
        var max = Enumerable.Range(1, SyncOperationReconciliation.MaxOperations)
            .Select(i => Request() with { OperationId = new Guid(i, 0, 0, new byte[8]) }).ToArray();
        Assert.Null(SyncOperationReconciliation.Validate(max));
        Assert.Equal("invalid_request", SyncOperationReconciliation.Validate([.. max, Request() with { OperationId = Guid.NewGuid() }]));
    }

    [Fact]
    public void Daily_domains_map_only_to_the_three_supported_kinds()
    {
        Assert.Equal("Noon", SyncOperationReconciliation.DailyKind(SyncOperationReconciliation.DailyNoon));
        Assert.Equal("Deck", SyncOperationReconciliation.DailyKind(SyncOperationReconciliation.DailyDeck));
        Assert.Equal("Engine", SyncOperationReconciliation.DailyKind(SyncOperationReconciliation.DailyEngine));
        Assert.Null(SyncOperationReconciliation.DailyKind(SyncOperationReconciliation.CrewWatchGroup));
    }
}
