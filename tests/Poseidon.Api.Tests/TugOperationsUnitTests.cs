using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S10 WP-B DEV-091 / DEV-092 pure request/policy rules (no PostgreSQL). Host and PostgreSQL behavior lives in
// TugOperationsApiTests (Integration, DirectoryWorkspaceTests).
public sealed class TugOperationsUnitTests
{
    private static QueryCollection Q(params (string Key, string Value)[] items) => new(items.ToDictionary(i => i.Key, i => new StringValues(i.Value)));

    [Fact]
    [Trait("Requirement", "DEV-091")]
    public void History_filter_is_closed_canonical_and_bounded()
    {
        var tug = Guid.NewGuid();
        Assert.True(TugOperationHistoryEndpoints.TryFilter(Q(), out var none));
        Assert.Null(none.Vessel); Assert.Null(none.Tug); Assert.Null(none.Job); Assert.Null(none.Outcome); Assert.Null(none.After);
        Assert.True(TugOperationHistoryEndpoints.TryFilter(Q(("tugId", tug.ToString("D")), ("from", "2031-01-01"), ("to", "2031-01-31"), ("outcome", "ABORTED")), out var f));
        Assert.Equal(tug, f.Tug); Assert.Equal("ABORTED", f.Outcome);
        Assert.Equal(new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero), f.To); // "to" is inclusive of the whole UTC day
        var cursor = TugOperationHistoryPolicy.Cursor(DateTimeOffset.UnixEpoch, tug);
        Assert.True(TugOperationHistoryEndpoints.TryFilter(Q(("cursor", cursor)), out var paged)); Assert.Equal(tug, paged.After!.Value.Id);
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("actorId", tug.ToString())), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("tugId", tug.ToString("N"))), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("tugId", Guid.Empty.ToString())), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("outcome", "DELETED")), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("from", "2031-02-01"), ("to", "2031-01-01")), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("from", "2030-01-01"), ("to", "2031-12-31")), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("from", "01/02/2031")), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(Q(("cursor", "not-a-cursor!")), out _));
        Assert.False(TugOperationHistoryEndpoints.TryFilter(new QueryCollection(new Dictionary<string, StringValues> { ["tugId"] = new([tug.ToString(), tug.ToString()]) }), out _));
    }

    private static TugOperationsOfflinePolicy Policy() => new() { Version = "fictional-tug-ops-offline-v1", SourceVersion = TugOperationsOfflineRules.SourceVersion,
        Classification = TugOperationsOfflinePolicy.RequiredClassification, StoragePolicyReference = "FICTIONAL encrypted IndexedDB test only",
        KeyPolicyReference = "FICTIONAL nonextractable key test only", RetentionPolicyReference = "FICTIONAL fixture retention; no approval", LeaseSeconds = 3600, MaxActiveSnapshots = 4 };

    [Fact]
    [Trait("Requirement", "DEV-092")]
    public void Offline_policy_is_unavailable_unless_every_operator_reference_is_present()
    {
        Assert.Null(new TugOperationsOfflineSettings().Current);
        Assert.NotNull(new TugOperationsOfflineSettings { Policy = Policy() }.Current);
        foreach (var broken in new Action<TugOperationsOfflinePolicy>[] { p => p.Classification = "Public", p => p.SourceVersion = "other", p => p.RetentionPolicyReference = "",
            p => p.StoragePolicyReference = "x\u0001", p => p.LeaseSeconds = 30, p => p.LeaseSeconds = 90000, p => p.MaxActiveSnapshots = 0, p => p.Version = "" })
        {
            var p = Policy(); broken(p);
            Assert.Null(new TugOperationsOfflineSettings { Policy = p }.Current);
        }
        var a = Policy(); var b = Policy(); b.LeaseSeconds = 7200;
        Assert.NotEqual(a.Fingerprint(), b.Fingerprint());
        Assert.Equal(a.Fingerprint(), Policy().Fingerprint());
    }
}
