using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class OperationalHealthObservationTests
{
    [Fact]
    public void Unobserved_tenant_metrics_are_unavailable_and_other_tenants_observations_never_supply_zeros_or_green()
    {
        var observed = Guid.NewGuid(); var other = Guid.NewGuid();
        OperationalHealthObservations.Request(observed, true, 20);
        OperationalHealthObservations.Audit(observed, false, 5);
        OperationalHealthObservations.Freshness(observed, SourceFreshness.Unknown);
        var first = OperationalHealthObservations.Read(observed);
        Assert.Equal(1, first.Api.Requests); Assert.Equal(1, first.Api.Errors); Assert.Equal(20, first.Api.AverageLatencyMs);
        Assert.Equal(0, first.Audit.Persisted); Assert.Equal(1, first.Audit.Unavailable); Assert.Equal(5, first.Audit.AverageLatencyMs);
        Assert.Equal(1, first.Freshness.Unknown); Assert.Equal(0, first.Freshness.Current);
        var hidden = OperationalHealthObservations.Read(other);
        Assert.Equal("Unavailable", hidden.Api.State); Assert.Null(hidden.Api.Requests); Assert.Null(hidden.Api.Errors); Assert.Null(hidden.Api.AverageLatencyMs);
        Assert.Equal("Unavailable", hidden.Audit.State); Assert.Null(hidden.Audit.Persisted); Assert.Null(hidden.Audit.Unavailable);
        Assert.Equal("Unavailable", hidden.Freshness.State); Assert.Null(hidden.Freshness.Current); Assert.Null(hidden.Freshness.Unknown);
        Assert.Equal("TenantProcessSinceStart", first.Api.Scope);
    }
}

// DEV-034/035/037/038: real host, owned PostgreSQL and genuine replay proof fixture.
// Authored only; no tests, browser, provider or build execution in this package.
public sealed partial class DirectoryWorkspaceTests
{
    private string HealthPath => $"/api/v1/system-health?vesselId={SeedVessel.Id:D}";
    private void HealthPermissions(IServiceProvider services)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = [..member.Permissions, "system.health.read", "audit.read"] };
    }
    [Fact]
    public async Task Health_requires_explicit_operator_capability_and_actual_vessel_scope_and_reports_configuration_as_unprobed()
    {
        await using var host = Host(); using var c = Client(host); using var anonymous = Client(host, false);
        using var unauthenticated = await anonymous.GetAsync(HealthPath, Ct); Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using var missing = await c.GetAsync(HealthPath, Ct); Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        HealthPermissions(host.Services); await Token(c);
        using var invalid = await c.GetAsync("/api/v1/companies?limit=0", Ct); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var health = (await c.GetFromJsonAsync<SystemHealthDto>(HealthPath, Ct))!;
        Assert.Equal(SeedVessel.Id, health.VesselId); Assert.Equal("TenantProcessSinceStart", health.Api.Scope);
        Assert.Equal("Observed", health.Api.State); Assert.True(health.Api.Requests > 0); Assert.True(health.Api.Errors > 0);
        Assert.True(health.Api.AverageLatencyMs >= 0); Assert.True(health.Audit.Persisted > 0);
        Assert.Equal("SelectedVesselServerOutcomes", health.Sync.Scope); Assert.Equal(0, health.Sync.Committed);
        Assert.Equal(0, health.Sync.UnresolvedConflicts); Assert.Null(health.Sync.LocalBacklog);
        Assert.Equal("Unavailable", health.Files.State); Assert.Equal("Unavailable", health.Files.ScanState);
        var other = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        c.DefaultRequestHeaders.Add("X-Tenant-Id", other.TenantId.ToString());
        using var hidden = await c.GetAsync($"/api/v1/system-health?vesselId={other.Id}", Ct); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.DoesNotContain("committed", await hidden.Content.ReadAsStringAsync(Ct));
        using var spoof = await c.GetAsync(HealthPath + "&actorId=" + actorId, Ct); Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
    }
    [Fact]
    public async Task Health_counts_actual_server_conflicts_and_their_audited_resolution_but_never_infers_client_queue_backlog()
    {
        await using var host = OfflineHost(); using var c = Client(host); HealthPermissions(host.Services); await Token(c);
        var first = await EnrollOffline(c); var other = await EnrollOffline(c);
        Assert.Equal(HttpStatusCode.OK, (await Deliver(c, other, Captured(other, "SYNTHETIC-B"))).StatusCode);
        var local = Captured(first); using var conflicted = await Deliver(c, first, local);
        Assert.Equal(HttpStatusCode.OK, conflicted.StatusCode);
        var conflict = (await conflicted.Content.ReadFromJsonAsync<OfflineOutcomeDto>(Ct))!; Assert.Equal("MaterialConflict", conflict.Result);
        var before = (await c.GetFromJsonAsync<SystemHealthDto>(HealthPath, Ct))!;
        Assert.Equal(1, before.Sync.Committed); Assert.Equal(1, before.Sync.UnresolvedConflicts); Assert.Null(before.Sync.LocalBacklog);
        var resolution = Captured(first, null) with { ParentCommandId = local.CommandId, BaseVersion = conflict.ServerVersion,
            ExpectedServerVersion = conflict.ServerVersion, Choice = "useCaptured", Reason = "FICTIONAL explicit human reconciliation" };
        using var resolved = await Deliver(c, first, resolution); Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        using var retry = await Deliver(c, first, resolution); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var after = (await c.GetFromJsonAsync<SystemHealthDto>(HealthPath, Ct))!;
        Assert.Equal(2, after.Sync.Committed); Assert.Equal(0, after.Sync.UnresolvedConflicts); Assert.Null(after.Sync.LocalBacklog);
        await using var db = Db(); Assert.Equal("MaterialConflict", (await db.OfflineOutcomes.SingleAsync(o => o.CommandId == local.CommandId, Ct)).Result);
        Assert.Equal(3, await db.OfflineOutcomes.CountAsync(Ct));
    }
    [Fact]
    public async Task Health_denies_pending_response_after_capability_withdrawal_and_hides_metrics_when_audit_writer_fails()
    {
        var barrier = new DirectorySelectBarrier("Vessels"); await using var host = Host(interceptor: barrier); using var c = Client(host);
        HealthPermissions(host.Services); await Token(c); barrier.Arm(); var pending = c.GetAsync(HealthPath, Ct);
        HttpResponseMessage? response = null;
        try {
            await barrier.WaitUntilReadAsync(Ct);
            var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
            settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "system.health.read").ToArray() };
            barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.DoesNotContain("latency", await response.Content.ReadAsStringAsync(Ct));
            HealthPermissions(host.Services);
            await Sql("""CREATE TRIGGER health_reject BEFORE INSERT ON audit."SecurityEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
            using var unavailable = await c.GetAsync(HealthPath, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            Assert.DoesNotContain("unresolvedConflicts", await unavailable.Content.ReadAsStringAsync(Ct));
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }
}
