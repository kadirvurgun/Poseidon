using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5501-SLICE-01 P5501S-AC-06..10: actual host, real PostgreSQL.
// Traits are per method: a class-level trait would tag the whole partial class.
public sealed partial class DirectoryWorkspaceTests
{
    private static string MetricPath(string metric, Guid? vessel = null) =>
        $"/api/v1/metrics/scalar?metricId={metric}" + (vessel == null ? "" : $"&vesselId={vessel}");
    private async Task<MetricScalarDto> Metric(HttpClient client, string metric, Guid? vessel = null)
    {
        using var response = await client.GetAsync(MetricPath(metric, vessel), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return (await response.Content.ReadFromJsonAsync<MetricScalarDto>(Ct))!;
    }
    private async Task AddCrew(Guid tenant, Guid vessel, int persons)
    {
        await using var db = Db(tenant);
        for (var i = 0; i < persons; i++)
        {
            var person = Guid.NewGuid();
            db.Persons.Add(new() { TenantId = tenant, Id = person });
            var assignment = new CrewAssignment { TenantId = tenant, Id = Guid.NewGuid(), VesselId = vessel, PersonId = person };
            db.CrewAssignments.Add(assignment); db.CrewAssignmentRevisions.Add(assignment.Snapshot(DateTimeOffset.UtcNow));
        }
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Metric_catalog_lists_only_definitions_the_actor_may_view()
    {
        await using (var host = Host())
        {
            using var client = Client(host);
            using var response = await client.GetAsync("/api/v1/metrics/definitions", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            var catalog = (await response.Content.ReadFromJsonAsync<MetricCatalogDto>(Ct))!;
            Assert.Equal("metrics.v1", catalog.ContractVersion);
            Assert.Equal([MetricRegistry.OnboardingVesselsReviewRequired, MetricRegistry.OnboardingRequiredFieldsUnknown], catalog.Items.Select(x => x.Id));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/metrics/definitions?tenantId=" + seed.Tenants[1].Id, Ct)).StatusCode);
        }
        await using (var host = Host(crewRead: true))
        {
            using var client = Client(host);
            var catalog = (await client.GetFromJsonAsync<MetricCatalogDto>("/api/v1/metrics/definitions", Ct))!;
            // S11 WP-B: pilot metric definitions additionally require pilot.metrics.read and are omitted here.
            Assert.Equal(MetricRegistry.All.Where(x => !x.RequiredPermissions.Contains(PilotMetricsPolicy.ReadPermission)).Select(x => x.Id), catalog.Items.Select(x => x.Id));
            Assert.All(catalog.Items, x => Assert.Equal(["view"], x.AllowedActions));
        }
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Metric_scalar_rejects_spoofed_queries_unknown_metrics_and_out_of_scope_vessels()
    {
        await using var host = Host(); using var client = Client(host);
        var metric = MetricRegistry.OnboardingVesselsReviewRequired;
        foreach (var path in new[] { "/api/v1/metrics/scalar", MetricPath(metric) + "&tenantId=" + seed.Tenants[1].Id,
            MetricPath(metric) + "&metricId=" + metric, MetricPath(metric) + "&vesselId=not-a-guid",
            MetricPath(metric) + "&vesselId=" + Guid.Empty, MetricPath(metric) + "&value=0" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(MetricPath("formula.injected"), Ct)).StatusCode);
        // Crew metric needs crew.read; the onboarding metrics need only directory.read.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MetricPath(MetricRegistry.CrewDeclaredPersons), Ct)).StatusCode);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.TenantId.ToString());
        using var hidden = await client.GetAsync(MetricPath(metric, foreign.Id), Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.DoesNotContain(foreign.Name, await hidden.Content.ReadAsStringAsync(Ct));
        using var anonymous = Client(host, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(MetricPath(metric), Ct)).StatusCode);
        await using var db = Db();
        var events = await db.Set<SecurityEvent>().Where(e => e.Action == "MetricScalarRead").ToArrayAsync(Ct);
        Assert.Contains(events, e => e.Result == "invalid_metric_query"); Assert.Contains(events, e => e.Result == "metric_not_found");
        Assert.Contains(events, e => e.Result == "permission_denied"); Assert.Contains(events, e => e.Result == "resource_not_found");
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Onboarding_scalars_count_only_current_scope_and_keep_tenant_isolation()
    {
        var tenantVessels = seed.Vessels.Where(v => v.TenantId == SeedVessel.TenantId).ToArray();
        Assert.True(tenantVessels.Length >= 2); Assert.Contains(seed.Vessels, v => v.TenantId != SeedVessel.TenantId);
        await using (var host = Host())
        {
            using var client = Client(host); await Token(client);
            var fleet = await Metric(client, MetricRegistry.OnboardingVesselsReviewRequired);
            Assert.Equal(MetricStates.Value, fleet.State); Assert.Equal(MetricQualities.Complete, fleet.Quality);
            // Synthetic seed declarations are incomplete: every visible vessel needs review; other tenant never counted.
            Assert.Equal(tenantVessels.Length.ToString(), fleet.Value);
            Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, tenantVessels.Length, tenantVessels.Length, 0, 0, 0), fleet.Scope);
            Assert.Equal("projection.compliance-onboarding.v1", fleet.SourceProjectionId); Assert.Equal(1, fleet.DefinitionVersion);
            Assert.Equal("DECLARED_UNVERIFIED", fleet.Basis); Assert.True(fleet.AsOf <= DateTimeOffset.UtcNow);
            Assert.True(fleet.SourceWatermark == null || fleet.SourceWatermark <= fleet.AsOf);
            var fleetUnknownBefore = long.Parse((await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown)).Value!);
            var vesselUnknownBefore = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown, SeedVessel.Id);
            Assert.Equal(MetricStates.Value, vesselUnknownBefore.State); Assert.Equal(MetricQualities.Complete, vesselUnknownBefore.Quality);
            var before = await Profile(client);
            var saved = await SaveProfile(client, before, new RegulatoryDraftContent { Flag = new("Unknown"), Imo = new("Unknown") });
            var unknown = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown, SeedVessel.Id);
            Assert.Equal("2", unknown.Value); Assert.Equal(saved.SavedAt, unknown.SourceWatermark);
            Assert.Equal(new MetricScopeDto("VESSEL", SeedVessel.Id, 1, 1, 0, 0, 0), unknown.Scope);
            var expectedFleet = fleetUnknownBefore - long.Parse(vesselUnknownBefore.Value!) + 2;
            Assert.Equal(expectedFleet.ToString(), (await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown)).Value);
        }
        await using (var host = Host(manage: false, read: false, edit: false))
        {
            using var client = Client(host);
            var own = await Metric(client, MetricRegistry.OnboardingVesselsReviewRequired);
            // Restricted actor: value over its single granted vessel only, never the tenant total.
            Assert.Equal("1", own.Value); Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, 1, 1, 0, 0, 0), own.Scope);
            var other = tenantVessels.First(v => v.Id != SeedVessel.Id);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(MetricPath(MetricRegistry.OnboardingVesselsReviewRequired, other.Id), Ct)).StatusCode);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { VesselGrants = [] };
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MetricPath(MetricRegistry.OnboardingVesselsReviewRequired), Ct)).StatusCode);
        }
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "MetricScalarRead" && e.Result == "Success" && e.ResourceId == SeedVessel.Id);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Withheld_file_bound_profile_makes_the_scalar_unknown_or_partial_never_zero()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-5501-metric-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client); using var uploaded = await PutPng(client, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var before = await Profile(client);
            await SaveProfile(client, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!), Flag = new("Known", "FICTIONAL-PRIVATE"), ShipType = new("Unknown") });
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "mode.tug", "mode.cross"] };
            using var response = await client.GetAsync(MetricPath(MetricRegistry.OnboardingRequiredFieldsUnknown, SeedVessel.Id), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("FICTIONAL-PRIVATE", body); Assert.DoesNotContain(file.Id.ToString(), body);
            var vessel = (await response.Content.ReadFromJsonAsync<MetricScalarDto>(Ct))!;
            Assert.Equal(MetricStates.Unknown, vessel.State); Assert.Null(vessel.Value); Assert.Equal(MetricQualities.Unknown, vessel.Quality);
            Assert.Equal(new MetricScopeDto("VESSEL", SeedVessel.Id, 1, 0, 1, 0, 0), vessel.Scope); Assert.Null(vessel.SourceWatermark);
            var fleet = await Metric(client, MetricRegistry.OnboardingVesselsReviewRequired);
            var visible = seed.Vessels.Count(v => v.TenantId == SeedVessel.TenantId);
            Assert.Equal(MetricStates.Value, fleet.State); Assert.Equal(MetricQualities.Partial, fleet.Quality);
            Assert.Equal((visible - 1).ToString(), fleet.Value); Assert.Equal(1, fleet.UnknownCount);
            Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, visible, visible - 1, 1, 0, 0), fleet.Scope);
            Assert.Contains("PARTIAL_SOURCE_COVERAGE", fleet.Reasons); Assert.Contains("SOURCE_WITHHELD", fleet.Reasons);
            Assert.DoesNotContain("SOURCE_UNREADABLE", fleet.Reasons); Assert.Equal(["SOURCE_WITHHELD"], vessel.Reasons);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Crew_scalar_suppresses_small_and_complementary_groups_within_current_scope()
    {
        var local = seed.Vessels.Where(v => v.TenantId == SeedVessel.TenantId && v.Id != SeedVessel.Id).First();
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        // Migration/seed fixtures may already declare assignments: top up to exact targets.
        async Task<int> Persons(Guid tenant, Guid vessel) {
            await using var db = Db(tenant);
            return await db.CrewAssignments.Where(a => a.VesselId == vessel).Select(a => a.PersonId).Distinct().CountAsync(Ct);
        }
        async Task TopUp(Guid tenant, Guid vessel, int target) {
            var current = await Persons(tenant, vessel); Assert.True(current <= target);
            await AddCrew(tenant, vessel, target - current);
        }
        await TopUp(SeedVessel.TenantId, SeedVessel.Id, 6);
        await TopUp(SeedVessel.TenantId, local.Id, 2);
        await TopUp(foreign.TenantId, foreign.Id, 9);
        await using (var host = Host(crewRead: true))
        {
            using var client = Client(host);
            var seedVessel = await Metric(client, MetricRegistry.CrewDeclaredPersons, SeedVessel.Id);
            Assert.Equal(MetricStates.Value, seedVessel.State); Assert.Equal("6", seedVessel.Value);
            var small = await Metric(client, MetricRegistry.CrewDeclaredPersons, local.Id);
            Assert.Equal(MetricStates.Suppressed, small.State); Assert.Null(small.Value); Assert.Contains("SMALL_GROUP", small.Reasons);
            var fleet = await Metric(client, MetricRegistry.CrewDeclaredPersons);
            // 8 would let the 2-person vessel be differenced out of 8 - 6.
            Assert.Equal(MetricStates.Suppressed, fleet.State); Assert.Null(fleet.Value); Assert.Contains("COMPLEMENTARY_SUPPRESSION", fleet.Reasons);
            await TopUp(SeedVessel.TenantId, local.Id, 5);
            var published = await Metric(client, MetricRegistry.CrewDeclaredPersons);
            await using var db = Db();
            var visibleIds = seed.Vessels.Where(v => v.TenantId == SeedVessel.TenantId).Select(v => v.Id).ToArray();
            var expected = await db.CrewAssignments.Where(a => visibleIds.Contains(a.VesselId)).Select(a => a.PersonId).Distinct().CountAsync(Ct);
            Assert.InRange(expected, 5, 11);
            // Other tenant's nine persons are never included.
            Assert.Equal(MetricStates.Value, published.State); Assert.Equal(expected.ToString(), published.Value);
            Assert.Equal("person", published.UnitCode); Assert.Equal("projection.crew-assignment-declarations.v1", published.SourceProjectionId);
        }
        await using (var host = Host(manage: false, read: false, edit: false, crewRead: true))
        {
            using var client = Client(host);
            var own = await Metric(client, MetricRegistry.CrewDeclaredPersons);
            Assert.Equal("6", own.Value); Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, 1, 1, 0, 0, 0), own.Scope);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(MetricPath(MetricRegistry.CrewDeclaredPersons, local.Id), Ct)).StatusCode);
        }
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Vessel_without_profile_is_a_not_collected_source_never_zero_unknown_fields()
    {
        var bare = Guid.NewGuid();
        await using (var db = Db()) {
            db.Vessels.Add(new() { TenantId = SeedVessel.TenantId, Id = bare, CompanyId = SeedVessel.CompanyId,
                Code = "SYN-5501-NOPROFILE", Name = "FICTIONAL vessel without profile", Kind = SeedVessel.Kind });
            await db.SaveChangesAsync(Ct);
        }
        var visible = seed.Vessels.Count(v => v.TenantId == SeedVessel.TenantId) + 1;
        await using (var host = Host())
        {
            using var client = Client(host);
            var vessel = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown, bare);
            Assert.Equal(MetricStates.Unknown, vessel.State); Assert.Null(vessel.Value); Assert.Equal(MetricQualities.Unknown, vessel.Quality);
            Assert.Equal(["SOURCE_NOT_COLLECTED"], vessel.Reasons); Assert.Equal(2, vessel.DefinitionVersion);
            Assert.Equal(new MetricScopeDto("VESSEL", bare, 1, 0, 0, 0, 1), vessel.Scope);
            var fleet = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown);
            Assert.Equal(MetricStates.Value, fleet.State); Assert.Equal(MetricQualities.Partial, fleet.Quality);
            Assert.Equal(["PARTIAL_SOURCE_COVERAGE", "SOURCE_NOT_COLLECTED"], fleet.Reasons);
            Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, visible, visible - 1, 0, 0, 1), fleet.Scope);
            // A missing declaration is itself a review need: that count stays a complete read.
            var review = await Metric(client, MetricRegistry.OnboardingVesselsReviewRequired, bare);
            Assert.Equal("1", review.Value); Assert.Equal(MetricQualities.Complete, review.Quality);
        }
        await using (var host = Host(manage: false, read: false, edit: false))
        {
            using var client = Client(host);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { VesselGrants = [new(bare)] };
            // Fleet where no visible vessel has a profile: UNKNOWN, not COMPLETE 0.
            var fleet = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown);
            Assert.Equal(MetricStates.Unknown, fleet.State); Assert.Null(fleet.Value); Assert.Equal(["SOURCE_NOT_COLLECTED"], fleet.Reasons);
            Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, 1, 0, 0, 0, 1), fleet.Scope);
        }
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Undecodable_profile_is_unreadable_not_access_withheld()
    {
        // Test-owned database only: simulate stored-source corruption below the audited write path.
        await Sql($"UPDATE core.\"RegulatoryDrafts\" SET \"ContentJson\" = '{{not json' WHERE \"TenantId\" = '{SeedVessel.TenantId}' AND \"VesselId\" = '{SeedVessel.Id}'");
        var visible = seed.Vessels.Count(v => v.TenantId == SeedVessel.TenantId);
        await using var host = Host(); using var client = Client(host);
        var vessel = await Metric(client, MetricRegistry.OnboardingRequiredFieldsUnknown, SeedVessel.Id);
        Assert.Equal(MetricStates.Unknown, vessel.State); Assert.Null(vessel.Value); Assert.Equal(["SOURCE_UNREADABLE"], vessel.Reasons);
        Assert.Equal(new MetricScopeDto("VESSEL", SeedVessel.Id, 1, 0, 0, 1, 0), vessel.Scope);
        var fleet = await Metric(client, MetricRegistry.OnboardingVesselsReviewRequired);
        Assert.Equal(MetricQualities.Partial, fleet.Quality); Assert.Equal((visible - 1).ToString(), fleet.Value);
        Assert.Equal(["PARTIAL_SOURCE_COVERAGE", "SOURCE_UNREADABLE"], fleet.Reasons);
        Assert.Equal(new MetricScopeDto("TENANT_VISIBLE", null, visible, visible - 1, 0, 1, 0), fleet.Scope);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5501-SLICE")]
    public async Task Metric_scalar_fails_closed_without_value_when_its_audit_cannot_persist()
    {
        await using var host = Host(); using var client = Client(host);
        // This test's independently owned PostgreSQL database only.
        await Sql("CREATE FUNCTION audit.reject_metric_read() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Action\" = 'MetricScalarRead' THEN RAISE EXCEPTION 'synthetic metric audit failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_metric_read BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH ROW EXECUTE FUNCTION audit.reject_metric_read();");
        using var response = await client.GetAsync(MetricPath(MetricRegistry.OnboardingVesselsReviewRequired), Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("synthetic metric audit failure", body);
        foreach (var field in new[] { "\"value\"", "\"state\"", "\"scope\"", "\"metricId\"" }) Assert.DoesNotContain(field, body);
    }
}
