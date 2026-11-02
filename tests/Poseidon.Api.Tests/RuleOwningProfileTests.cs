using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual host + owned real PostgreSQL, CSRF, ordinary commands and immutable history.
// Scanner peer and humans are fictional software fixtures. NOT RUN in authoring.
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<FactAttestationCommand> OwningAttestation(HttpClient c, IServiceProvider services, RuleDraftDto rule, QualificationDto q, string field)
    {
        var profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        var page = await c.GetFromJsonAsync<JsonElement>($"/api/v1/regulatory-source-governance/{rule.Current.Id}?vesselId={SeedVessel.Id}", Ct);
        var grant = services.GetRequiredService<FederationSettings>().Memberships[0].RegulatoryAuthorities.Single(g => g.VesselId != null);
        return new(grant.Id, SeedVessel.Id, profile.Revision, profile.Version, q.Revision,
            page.GetProperty("items")[0].GetProperty("version").GetUInt32(), [field], DateTimeOffset.UtcNow.AddMinutes(20));
    }
    private JsonElement OwningRule(string field, string op, object value)
    {
        var document = JsonSerializer.Deserialize<ApplicabilityRule>(FictionalRule(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return JsonSerializer.SerializeToElement(document with { When = JsonSerializer.SerializeToElement(new { field, @operator = op, value }) }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    [Fact]
    public async Task Exact_power_flows_from_profile_through_qualified_human_command_and_source_current_invalidation()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, q, _) = await AdmittedByCommands(c, host.Services, OwningRule("vessel.mainPropulsionKw", "GTE", 3600), [new("vessel.mainPropulsionKw", "HumanAttested", 24)], "REVIEW_REQUIRED");
        var before = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        var cmd = new SaveRegulatoryDraft(before.Version, before.Content with { MainPropulsionKw = new("Known", "3600.000000"), SourceObservedAt = new("Known", DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O")) });
        using var saved = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", cmd, update: true); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var unverified = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal("REVIEW_REQUIRED", (await unverified.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!.Results[0].Prediction.Result);
        var attestation = await OwningAttestation(c, host.Services, rule, q, "vessel.mainPropulsionKw");
        using var attested = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", attestation); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        using var evaluated = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode); var evaluation = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        Assert.Equal("APPLICABLE", evaluation.Results[0].Prediction.Result);
        var fact = evaluation.Snapshot.GetProperty("qualifications")[0].GetProperty("facts").GetProperty("vessel.mainPropulsionKw");
        Assert.Equal("3600", fact.GetProperty("value").GetString()); Assert.Equal("RecordedHumanAttestation", fact.GetProperty("provenance").GetString());
        await using (var db = Db()) {
            var binding = Assert.Single(await db.EvaluationQualifications.Where(x => x.EvaluationId == evaluation.Id).ToArrayAsync(Ct)); Assert.NotNull(binding.AttestationId);
            Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityType == nameof(RegulatoryFactAttestation), Ct));
        }
        var current = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        using var noop = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", new SaveRegulatoryDraft(current.Version, current.Content), update: true);
        Assert.Equal(HttpStatusCode.OK, noop.StatusCode); var newer = (await noop.Content.ReadFromJsonAsync<RegulatoryDraftDto>(Ct))!;
        Assert.NotEqual(current.Version, newer.Version); Assert.Equal(current.Revision + 1, newer.Revision);
        using var staleAttestation = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", attestation); Assert.Equal(HttpStatusCode.Conflict, staleAttestation.StatusCode);
        var validity = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{evaluation.Id}/current", Ct);
        Assert.False(validity.GetProperty("current").GetBoolean()); Assert.Contains("PROFILE_CHANGED", validity.GetRawText());
        using var after = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal("REVIEW_REQUIRED", (await after.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!.Results[0].Prediction.Result);
    }
    [Fact]
    public async Task Construction_date_requires_exact_nonpersonal_record_and_integrity_not_build_date_or_reference_text()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-owning-record-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, scanner.Port); using var c = Client(host);
            GraphPermissionsForTest(host.Services); await Token(c);
            var (rule, q, _) = await AdmittedByCommands(c, host.Services, OwningRule("vessel.contractForConstructionDate", "DATE_GTE", "2022-01-01"), [new("vessel.contractForConstructionDate", "HumanAttested", 24)], "REVIEW_REQUIRED");
            var profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
            var content = profile.Content with { BuildDate = new("Known", "2023-04-01"), ContractForConstructionDate = new("Known", "2022-03-14"), ConstructionRecordReference = new("Known", "FICTIONAL class/build contract record") };
            using (var draft = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", new SaveRegulatoryDraft(profile.Version, content), update: true)) Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
            var withoutRecord = await OwningAttestation(c, host.Services, rule, q, "vessel.contractForConstructionDate");
            using (var refused = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", withoutRecord)) Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            var stage = await StagePng(c); using var accepted = await PutPng(c, stage.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var file = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
            content = content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!) };
            var key = Guid.NewGuid().ToString(); var save = new SaveRegulatoryDraft(profile.Version, content);
            using var linked = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", save, key, update: true); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
            var linkedJson = await linked.Content.ReadAsStringAsync(Ct);
            var linkedProfile = JsonSerializer.Deserialize<RegulatoryDraftDto>(linkedJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var attestation = await OwningAttestation(c, host.Services, rule, q, "vessel.contractForConstructionDate");
            using var attested = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", attestation); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
            using var evaluated = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id])); Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
            var result = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; Assert.Equal("APPLICABLE", result.Results[0].Prediction.Result);
            Assert.Equal("2022-03-14", result.Snapshot.GetProperty("facts").GetProperty("vessel.contractForConstructionDate").GetProperty("value").GetString());
            await using (var db = Db()) {
                var revision = await db.RegulatoryDraftRevisions.SingleAsync(x => x.VesselId == SeedVessel.Id && x.Revision == linkedProfile.Revision, Ct);
                Assert.Equal(file.Id, revision.ConstructionFileId); Assert.Equal(file.Checksum, revision.ConstructionFileChecksum);
                Assert.Equal(file.Revision, revision.ConstructionFileRevision);
                var receipt = await db.CommandReceipts.SingleAsync(x => x.Key == key, Ct);
                Assert.Equal(SeedVessel.Id, receipt.ProfileVesselId); Assert.Equal(linkedProfile.Revision, receipt.RegisterRevision);
                Assert.Equal(linkedProfile.Version, receipt.RegisterVersion);
                Assert.Equal(linkedJson, receipt.ProfileOutcomeJson);
            }
            await using (var db = Db()) {
                var head = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct);
                head.ConstructionFileRevision = file.Revision + 1;
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); // Exact revision/checksum FK, not just a GUID.
            }
            // Current rights precede profile/history/evaluation bytes, pagination and receipt replay.
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "file.read").ToArray() };
            using var denied = await c.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var history = await c.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile/history", Ct); Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
            using var replay = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", save, key, update: true); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
            using var hiddenEvaluation = await c.GetAsync($"/api/v1/applicability/evaluations/{result.Id}", Ct); Assert.Equal(HttpStatusCode.NotFound, hiddenEvaluation.StatusCode);
            var hiddenHistory = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations?vesselId={SeedVessel.Id}", Ct);
            Assert.DoesNotContain(hiddenHistory.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == result.Id);
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Provider_policy_does_not_promote_power_even_when_a_declared_value_exists()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services, OwningRule("vessel.mainPropulsionKw", "GTE", 3600), [new("vessel.mainPropulsionKw", "ProviderVerified")], "REVIEW_REQUIRED");
        var p = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        using var saved = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", new SaveRegulatoryDraft(p.Version, p.Content with { MainPropulsionKw = new("Known", "3600") }), update: true); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var response = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        var e = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; Assert.Equal("REVIEW_REQUIRED", e.Results[0].Prediction.Result);
        Assert.Equal("RequiredProviderUnavailable", e.Snapshot.GetProperty("qualifications")[0].GetProperty("facts").GetProperty("vessel.mainPropulsionKw").GetProperty("provenance").GetString());
    }
    [Theory]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("mutated-role")]
    public async Task Source_attestation_authority_revocation_withdraws_current_power_without_rewriting_the_recorded_result(string change)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, q, _) = await AdmittedByCommands(c, host.Services, OwningRule("vessel.mainPropulsionKw", "GTE", 3600), [new("vessel.mainPropulsionKw", "HumanAttested")], "REVIEW_REQUIRED");
        var profile = await Profile(c);
        await SaveProfile(c, profile, profile.Content with { MainPropulsionKw = new("Known", "3600") });
        using var attested = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", await OwningAttestation(c, host.Services, rule, q, "vessel.mainPropulsionKw")); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        using var response = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        var evaluation = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; Assert.Equal("APPLICABLE", evaluation.Results[0].Prediction.Result);
        var original = await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct);
        var member = host.Services.GetRequiredService<FederationSettings>().Memberships[0];
        member.RegulatoryAuthorities = change == "revoked" ? member.RegulatoryAuthorities.Where(g => g.VesselId == null).ToArray() :
            member.RegulatoryAuthorities.Select(g => g.VesselId == null ? g : change == "expired" ? g with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) } : g with { RoleId = Guid.NewGuid() }).ToArray();
        var validity = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{evaluation.Id}/current", Ct);
        Assert.False(validity.GetProperty("current").GetBoolean()); Assert.Contains("ATTESTATION_AUTHORITY_CHANGED", validity.GetRawText());
        Assert.Equal(original, await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct));
        using var current = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal("REVIEW_REQUIRED", (await current.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!.Results[0].Prediction.Result);
    }
    private sealed class OwningClock : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = DateTimeOffset.UtcNow.AddMinutes(5);
        public override DateTimeOffset GetUtcNow() => Current;
    }
    [Fact]
    public async Task Expired_recorded_attestation_never_repromotes_a_known_profile_value_in_a_new_evaluation()
    {
        var clock = new OwningClock();
        await using var host = Host().WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(clock)));
        using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, q, _) = await AdmittedByCommands(c, host.Services, OwningRule("vessel.mainPropulsionKw", "GTE", 3600), [new("vessel.mainPropulsionKw", "HumanAttested")], "REVIEW_REQUIRED");
        var profile = await Profile(c); await SaveProfile(c, profile, profile.Content with { MainPropulsionKw = new("Known", "3600") });
        var command = (await OwningAttestation(c, host.Services, rule, q, "vessel.mainPropulsionKw")) with { ValidUntil = clock.Current.AddMinutes(10) };
        using var attested = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", command); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        using var response = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        var original = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; Assert.Equal("APPLICABLE", original.Results[0].Prediction.Result);
        clock.Current = clock.Current.AddMinutes(11);
        var validity = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{original.Id}/current", Ct);
        Assert.False(validity.GetProperty("current").GetBoolean()); Assert.Contains("ATTESTATION_EXPIRED", validity.GetRawText());
        using var current = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal("REVIEW_REQUIRED", (await current.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!.Results[0].Prediction.Result);
    }
    [Fact]
    public async Task Protected_person_file_cannot_be_rebound_as_a_construction_record_even_for_a_current_person_reader()
    {
        var root = PersonFileRoot();
        try {
            using var scanner = new ScanPeer("stream: OK\0"); await using var host = PersonFileHost(root, scanner.Port); using var c = Client(host);
            PersonFilePermissions(host.Services); await Token(c);
            using var stage = await Send(c, PersonalVersion(), PersonalPng()); Assert.Equal(HttpStatusCode.OK, stage.StatusCode);
            var staged = (await stage.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            using var accepted = await PutPersonalPng(c, PersonalVersion(), staged.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var file = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            GraphPermissionsForTest(host.Services); await Token(c);
            var profile = await Profile(c); var key = Guid.NewGuid().ToString();
            using var denied = await Send(c, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(profile.Version, profile.Content with {
                ContractForConstructionDate = new("Known", "2022-03-14"), ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!) }), key, true);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain(file.Id.ToString(), body); Assert.DoesNotContain(file.Checksum!, body);
            await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
            Assert.Equal(profile.Revision, (await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct)).Revision);
        } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
