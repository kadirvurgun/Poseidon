using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV039..042 / T-S5-001..004. Authored real API/PostgreSQL regressions; NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private string CompliancePath(string view, Guid? vessel = null) => $"/api/v1/vessels/{vessel ?? SeedVessel.Id}/compliance-{view}";

    [Fact]
    public async Task Truncated_requirement_links_do_not_advertise_endless_control_record_pages() {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var (rule, _, _) = await AdmittedByCommands(client, host.Services);
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value,
            rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var created = new List<Guid>();
        for (var i = 0; i < 26; i++) created.Add((await GraphSave(client, "controls", GraphCommand() with { Requirements = [pin] })).Id);
        var first = (await client.GetFromJsonAsync<ComplianceControlPlanDto>(CompliancePath("control-plan"), Ct))!;
        Assert.True(first.HasMore); Assert.Equal(25, first.Controls.Length);
        var last = (await client.GetFromJsonAsync<ComplianceControlPlanDto>(CompliancePath("control-plan") + "?offset=25", Ct))!;
        // The seed's incomplete draft is a real visible control. Account for it
        // without deleting it or excluding unlinked controls from the product.
        var seeded = ControlEvidenceFixtures.Id(SeedVessel.Id, "Control");
        Assert.False(last.HasMore); Assert.Equal(2, last.Controls.Length);
        var returned = first.Controls.Concat(last.Controls).Select(c => c.Id).ToArray();
        Assert.Equal(27, returned.Length); Assert.Equal(27, returned.Distinct().Count());
        Assert.Equal(created.Append(seeded).Order(), returned.Order());
        Assert.Contains(last.Controls, c => c.Id == seeded && c.Verification == "UNVERIFIED");
        Assert.Equal(25, Assert.Single(last.Requirements).ControlIds.Length);
        Assert.All(Assert.Single(last.Requirements).ControlIds, id => Assert.Contains(id, created));
        Assert.Contains("CONTROL_LINKS_TRUNCATED", Assert.Single(last.Requirements).Blockers);
        Assert.Contains("BOUNDED_PROJECTION", last.Blockers);
    }

    [Fact]
    public async Task Canonical_passport_alias_keeps_exact_vessel_scope_and_rejects_duplicate_selectors() {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var dto = (await client.GetFromJsonAsync<CompliancePassportDto>($"/api/v1/vessel-compliance-passport?vesselId={SeedVessel.Id}", Ct))!;
        Assert.Equal(SeedVessel.Id, dto.VesselId); Assert.Equal("REVIEW_REQUIRED", dto.State);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessel-compliance-passport?vesselId={foreign.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/vessel-compliance-passport?vesselId={SeedVessel.Id}&vesselId={foreign.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Qualified_requirements_plan_uses_exact_rule_source_pins_then_withdraws_currentness_after_profile_change() {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var (rule, _, evaluation) = await AdmittedByCommands(client, host.Services);
        var before = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.True(before.Applicability.Available); Assert.Equal(evaluation.Id, before.Applicability.EvaluationId);
        Assert.Equal(evaluation.SnapshotHash, before.Applicability.InputHash); Assert.Equal(evaluation.RuleSetHash, before.Applicability.RuleSetHash);
        Assert.Equal(JsonSerializer.Serialize(evaluation.Snapshot), JsonSerializer.Serialize(before.Applicability.Inputs!.Value));
        Assert.Equal(1, before.Plan.UncontrolledRequirementCount); Assert.Null(before.Plan.OrphanCriticalCount);
        var seeded = ControlEvidenceFixtures.Id(SeedVessel.Id, "Control");
        Assert.Equal(seeded, Assert.Single(before.Plan.Controls).Id);
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value,
            rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var control = await GraphSave(client, "controls", GraphCommand() with { Requirements = [pin] });
        var linked = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        var requirement = Assert.Single(linked.Plan.Requirements);
        Assert.Equal(rule.Current.Id, requirement.RuleId); Assert.Equal(rule.Current.Revision, requirement.RuleRevision);
        Assert.Equal(pin.RequirementId, requirement.RequirementId); Assert.Equal(pin.SourceRevision, requirement.SourceRevision);
        Assert.Equal(control.Id, Assert.Single(requirement.ControlIds)); Assert.Equal("LINKED", requirement.State);
        Assert.Equal(0, linked.Plan.UncontrolledRequirementCount); Assert.Equal("REVIEW_REQUIRED", linked.State);
        Assert.Equal(new[] { seeded, control.Id }.Order(), linked.Plan.Controls.Select(c => c.Id).Order());
        Assert.Equal("UNVERIFIED", Assert.Single(linked.Plan.Controls, c => c.Id == control.Id).Verification);
        Assert.Equal("UNVERIFIED", Assert.Single(linked.Plan.Controls, c => c.Id == seeded).Verification);
        var original = await client.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct);
        var profile = await Profile(client); await SaveProfile(client, profile, profile.Content with { Flag = new("Unknown") });
        var stale = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.True(stale.Applicability.Available); Assert.False(stale.Applicability.Current);
        Assert.Contains("PROFILE_CHANGED", stale.Applicability.Blockers); Assert.Empty(stale.Plan.Requirements);
        Assert.Null(stale.Plan.UncontrolledRequirementCount); Assert.Equal(evaluation.Id, stale.Applicability.EvaluationId);
        Assert.Equal(evaluation.SnapshotHash, stale.Applicability.InputHash);
        Assert.Equal(JsonSerializer.Serialize(evaluation.Results), JsonSerializer.Serialize(stale.Applicability.Results));
        Assert.Equal(original, await client.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct));
    }

    [Fact]
    public async Task Onboarding_uses_saved_profile_revision_and_cannot_submit_missing_or_unknown_inputs() {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var saved = await SaveProfile(client, before, before.Content with { Flag = new("Unknown") });
        long writes; long receipts;
        await using (var db = Db()) { writes = await db.AuditEvents.LongCountAsync(Ct); receipts = await db.CommandReceipts.LongCountAsync(Ct); }
        var dto = (await client.GetFromJsonAsync<ComplianceOnboardingDto>(CompliancePath("onboarding"), Ct))!;
        Assert.Equal(saved.Revision, dto.ProfileRevision); Assert.Equal(saved.SavedAt, dto.SavedAt);
        Assert.Equal("REVIEW_REQUIRED", dto.State); Assert.Equal("Unverified", dto.Freshness); Assert.False(dto.CanSubmit);
        Assert.Equal("Unknown", Assert.Single(dto.Fields, f => f.Key == "profile.flag").State);
        await using var verify = Db(); Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
        Assert.Equal(receipts, await verify.CommandReceipts.LongCountAsync(Ct));
        Assert.Contains(await verify.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "ComplianceWorkspaceRead" && e.Result == "Success" && e.ResourceId == SeedVessel.Id);
    }
    [Fact]
    public async Task Onboarding_full_declaration_is_not_an_approval_and_prior_profile_history_is_immutable() {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var saved = await SaveProfile(client, before, before.Content with {
            Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL"), ClassSociety = new("Known", "FICTIONAL"),
            ShipType = new("Known", "FICTIONAL"), Gt = new("Known", "1000.00000000000000000001"),
            BuildDate = new("Known", "2000-01-01"), TradingArea = new("Known", "FICTIONAL"), CargoContext = new("Known", "FICTIONAL") });
        var dto = (await client.GetFromJsonAsync<ComplianceOnboardingDto>(CompliancePath("onboarding"), Ct))!;
        Assert.Equal("DECLARED_COMPLETE_UNVERIFIED", dto.State); Assert.False(dto.CanSubmit);
        Assert.Equal(saved.Revision, dto.ProfileRevision); Assert.All(dto.Fields, f => Assert.True(f.Complete));
        Assert.Contains("VERIFICATION_NOT_ESTABLISHED", dto.Blockers);
        Assert.Equal(saved.Content, (await Profile(client)).Content);
        var history = (await client.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>($"/api/v1/{ProfilePath(SeedVessel.Id)}/history", Ct))!;
        Assert.Equal(before.Content, history.Items.Single(r => r.Revision == before.Revision).Content);
    }
    [Fact]
    public async Task Passport_reuses_current_graph_heads_and_never_reports_missing_authority_as_zero_or_verified() {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var first = await GraphSave(client, "controls", GraphCommand(content: new(Title: "FICTIONAL first", Critical: true)));
        var current = await GraphSave(client, $"controls/{first.Id}", GraphCommand(first.Version, first.Content with { Title = "FICTIONAL current" }), update: true);
        long writes; long receipts;
        await using (var db = Db()) { writes = await db.AuditEvents.LongCountAsync(Ct); receipts = await db.ControlEvidenceReceipts.LongCountAsync(Ct); }
        var dto = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.Equal("REVIEW_REQUIRED", dto.State); Assert.Equal("UNAVAILABLE", dto.CertificateCoverage);
        Assert.False(dto.Applicability.Available); Assert.False(dto.Applicability.ManualOverrideAvailable);
        Assert.Equal($"/app/fleet/{SeedVessel.Id:D}/passport#applicability", dto.Applicability.Href);
        Assert.Null(dto.Plan.UncontrolledRequirementCount); Assert.Null(dto.Plan.OrphanCriticalCount);
        Assert.Equal(current.Revision, Assert.Single(dto.Plan.Controls, c => c.Id == current.Id).Revision);
        Assert.DoesNotContain(dto.Plan.Controls, c => c.Id == first.Id && c.Revision == first.Revision);
        Assert.Contains("CERTIFICATE_READ_ACCESS_UNAVAILABLE", dto.Blockers);
        Assert.Empty(dto.Certificates.Items); Assert.False(dto.Certificates.HasMore);
        Assert.Contains("CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED", dto.Blockers);
        await using var verify = Db(); Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
        Assert.Equal(receipts, await verify.ControlEvidenceReceipts.LongCountAsync(Ct));
    }
    [Theory]
    [InlineData("passport")]
    [InlineData("applicability-review")]
    [InlineData("control-plan")]
    public async Task Compliance_current_workspaces_recheck_full_read_scope_and_reject_authority_query_spoofing(string view) {
        await using var host = Host(); using var client = Client(host); await Token(client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(CompliancePath(view), Ct)).StatusCode);
        GraphPermissionsForTest(host.Services);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CompliancePath(view) + "?tenantId=" + seed.Tenants[1].Id, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(CompliancePath(view) + "?offset=0&offset=25", Ct)).StatusCode);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", foreign.TenantId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(CompliancePath(view, foreign.Id), Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "file.read").ToArray() };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(CompliancePath(view), Ct)).StatusCode);
    }
    [Fact]
    public async Task Onboarding_hides_file_bound_profile_completeness_after_file_read_is_withdrawn() {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s5-onboarding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var host = FileHost(root, scanner.Port); using var client = Client(host); FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client); using var uploaded = await PutPng(client, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var before = await Profile(client);
            await SaveProfile(client, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!), Flag = new("Known", "FICTIONAL-PRIVATE") });
            var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
            settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "file.read").ToArray() };
            using var denied = await client.GetAsync(CompliancePath("onboarding"), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("fields", body); Assert.DoesNotContain("profileRevision", body);
            Assert.DoesNotContain("FICTIONAL-PRIVATE", body); Assert.DoesNotContain(file.Id.ToString(), body);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
