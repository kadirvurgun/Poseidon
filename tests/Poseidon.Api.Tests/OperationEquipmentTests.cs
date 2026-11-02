using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Real host / owned PostgreSQL / ordinary capture, qualification and verification commands.
// All sources, actors and documents are fictional. AUTHORED ONLY, NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class OwningTimeShift : TimeProvider {
        internal TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.Add(Offset);
    }
    private static ApplicabilityRule OwningGoldenSample(string id) {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/applicability.sample-rules.json")));
        return ApplicabilityEngine.Parse(json.RootElement.EnumerateArray().Single(r => r.GetProperty("ruleId").GetString() == id).GetRawText());
    }
    [Theory]
    [InlineData("current")][InlineData("ocean")][InlineData("job-noop")][InlineData("plan-noop")][InlineData("source")][InlineData("verifier-role")][InlineData("expired")][InlineData("unverified-service")][InlineData("unmapped")]
    public async Task Operation_and_service_owning_commands_require_exact_current_relations_and_withdraw_on_changes(string change) {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-operation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); var planning = SyntheticPlanning(); var clock = new OwningTimeShift();
            await using var host = FileHost(root, scanner.Port).WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.AddSingleton(planning); s.AddSingleton<TimeProvider>(clock); })); using var c = Client(host); await Token(c);
            var (rule, _, baseline) = await AdmittedByCommands(c, host.Services, inputs: OwningPolicies, evaluationVessel: Tug.Id, literalBindings: change == "unmapped" ? [] : FictionalOperationBindings);
            CapacityRights(host.Services); await Token(c); var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "tug.owner.eligible"] };
            planning.Scopes = [new(Tug.TenantId, settings.Memberships[0].ActorId, Tug.Id, [Tug.Id])];
            var job = await PlanningJob(c);
            using (var edited = await Send(c, $"tug-jobs/{job.Id}?vessel={Tug.Id}", new TugSaveCommand(job.Id, job.Version, job.Inputs with { JobType = "Harbour Towage", OperationArea = "FICTIONAL-AREA", RequestSource = "Internal", RequestedStart = DateTimeOffset.UtcNow.AddMinutes(-1), RequiredTugCount = 1, Priority = "Normal", RequestOwner = settings.Memberships[0].ActorId }), update: true)) {
                Assert.Equal(HttpStatusCode.OK, edited.StatusCode); job = (await edited.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
            }
            var plan = await ChangePlan(c, job, null, new("Window", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1)));
            plan = await ChangePlan(c, job, plan, new("Assign", TugId: Tug.Id));
            var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
            var control = await CapacitySave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL operation context", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] });
            var authorization = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(ServiceAuthorization: new(job.Id, job.Revision, job.Version, plan.Revision, plan.Version, "AUTHORIZED_TUG_SERVICE", "FICTIONAL shore authorization; no actual issuance")), change != "unverified-service");
            var operation = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(Operation: new(job.Id, job.Revision, job.Version, plan.Revision, plan.Version, change == "ocean" ? "SOLELY_LONG_DISTANCE_OCEAN_TOWAGE" : "CLOSE_QUARTERS_TOWAGE", authorization.Id, authorization.Revision)));
            CapacityRights(host.Services); await Token(c);
            using var response = await Send(c, "applicability/evaluate", new EvaluateCommand(Tug.Id, [rule.Current.Id], OwningInputs: new(OperationId: operation.Id)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); var evaluation = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
            var facts = evaluation.Snapshot.GetProperty("qualifications")[0].GetProperty("facts");
            var expectedKnown = change is not ("unverified-service" or "unmapped");
            foreach (var field in OperationEquipmentInputs.Fields.Take(4)) Assert.Equal(expectedKnown ? "Known" : "Unknown", facts.GetProperty(field).GetProperty("knowledge").GetString());
            if (expectedKnown) { Assert.Equal("HARBOUR_ASSIST", facts.GetProperty("operation.jobType").GetProperty("value").GetString()); Assert.Equal("KOCAELI", facts.GetProperty("vessel.operatingArea").GetProperty("value").GetString()); }
            if (expectedKnown) {
                var golden = facts.Deserialize<Dictionary<string, ContextFact>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                // Only the four operation facts above are command-produced here. The
                // other M79 inputs are explicit fictional complementary domain fixtures;
                // class/compliance owning adapters remain the next task, never accepted.
                foreach (var (field, value) in new (string, object)[] { ("class.rulePack.IACS_M79", true), ("vessel.contractForConstructionDate", "2022-03-14"), ("equipment.towingWinchFitted", true) })
                    golden[field] = new(ApplicabilityEngine.Fields[field], "Known", JsonSerializer.SerializeToElement(value), "FICTIONAL complementary golden fixture", "fixture-only");
                Assert.Equal(change == "ocean" ? "NOT_APPLICABLE" : "APPLICABLE", ApplicabilityEngine.Preview(OwningGoldenSample("RULE-IACS-M79-APPLICABILITY"), golden, evaluation.AsOf).Result);
            }
            if (change == "job-noop") {
                using var noop = await Send(c, $"tug-jobs/{job.Id}?vessel={Tug.Id}", new TugSaveCommand(job.Id, job.Version, job.Inputs), update: true); Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
            } else if (change == "plan-noop") {
                planning.Scopes = [new(Tug.TenantId, settings.Memberships[0].ActorId, Tug.Id, [Tug.Id])];
                await ChangePlan(c, job, plan, new("Window", plan.Inputs.PlannedStart, plan.Inputs.PlannedEnd));
            } else if (change == "source") {
                var source = await c.GetFromJsonAsync<RegisterDto>($"/api/v1/source-drafts/{rule.Current.SourceId}", Ct);
                using var changed = await Send(c, $"source-drafts/{source!.Current.Id}", new RegisterCommand(source.Version, source.Current.Content), update: true); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            } else if (change == "verifier-role") settings.Memberships[0] = settings.Memberships[0] with { HumanAuthorities = [] };
            else if (change == "expired") clock.Offset = TimeSpan.FromHours(3);
            var current = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{evaluation.Id}/current", Ct);
            Assert.Equal(change is "current" or "ocean" or "unverified-service" or "unmapped", current.GetProperty("current").GetBoolean());
            if (change is "job-noop" or "plan-noop" or "verifier-role" or "source") Assert.Contains(current.GetProperty("causes").EnumerateArray(), x => x.GetString() == "OWNING_INPUT_CHANGED");
            Assert.Equal(evaluation.SnapshotHash, (await c.GetFromJsonAsync<EvaluationDto>($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct))!.SnapshotHash);
        } finally { Directory.Delete(root, true); }
    }
    private static QualifiedInputPolicy[] OwningPolicies => [new("vessel.flag", "Declared"), ..OperationEquipmentInputs.Fields.Select(f => new QualifiedInputPolicy(f, "HumanAttested", 24))];
    private static QualifiedLiteralBinding[] FictionalOperationBindings => [
        new("vessel.operatingArea", "FICTIONAL-AREA", "KOCAELI", "FICTIONAL/local-overlay/source"),
        new("operation.jobType", "Harbour Towage", "HARBOUR_ASSIST", "FICTIONAL/job/source"),
        new("operation.profile", "CLOSE_QUARTERS_TOWAGE", "CLOSE_QUARTERS_TOWAGE", "FICTIONAL/operation/source"),
        new("operation.profile", "SOLELY_LONG_DISTANCE_OCEAN_TOWAGE", "SOLELY_LONG_DISTANCE_OCEAN_TOWAGE", "FICTIONAL/ocean-operation/source"),
        new("operation.serviceContext", "AUTHORIZED_TUG_SERVICE", "AUTHORIZED_TUG_SERVICE", "FICTIONAL/service/source") ];
    private async Task<ControlEvidenceDto> OwningDocument(HttpClient c, IServiceProvider services, ControlEvidenceDto control, RequirementPin pin, Guid evaluation, ControlEvidenceContent content, bool verify = true, Guid? vessel = null, Action<HumanReviewDto>? captureVerification = null) {
        var settings = services.GetRequiredService<FederationSettings>();
        var page = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{control.Id}/versions", (vessel ?? Tug.Id)), Ct);
        control = page.GetProperty("items")[0].Deserialize<ControlEvidenceDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var file = await TugCertificateFile(c, vessel); var now = DateTimeOffset.UtcNow;
        var command = GraphCommand(content: content with { OwnerId = settings.Memberships[0].ActorId, OwnerRole = ReviewRole, Title = "FICTIONAL owning document; no real authorization",
            Provenance = "Fictional software test only", ObservedAt = now.AddMinutes(-1), ValidFrom = now.AddMinutes(-1), ValidUntil = now.AddHours(2), FreshUntil = now.AddHours(2) }) with {
                Requirements = [pin], Links = [new("Control", control.Id, control.Revision, control.Version)], File = new(file.Id, file.Revision!.Value, file.Checksum!) };
        var draft = await CapacitySave(c, "evidence", command, vessel: vessel);
        var submitted = await CapacitySave(c, $"evidence/{draft.Id}/submit", command with { ExpectedVersion = draft.Version, ReasonCode = "SUBMIT" }, vessel: vessel);
        return verify ? await CapacityVerify(c, services, submitted, control, evaluation, content.Operation?.JobId ?? content.ServiceAuthorization?.JobId, vessel, captureVerification) : submitted;
    }
    [Theory]
    [InlineData(true, true, true)][InlineData(false, false, true)][InlineData(true, true, false)]
    public async Task Qualified_cargo_and_winch_facts_are_command_produced_with_positive_false_and_unverified_semantics(bool dg, bool fitted, bool verify) {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-owning-inputs-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); await using var host = FileHost(root, scanner.Port); using var c = Client(host); await Token(c);
            var (rule, _, baseline) = await AdmittedByCommands(c, host.Services, inputs: OwningPolicies, evaluationVessel: SeedVessel.Id, literalBindings: FictionalOperationBindings);
            CapacityRights(host.Services); await Token(c);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
            var control = await CapacitySave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL owning input control", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] }, vessel: SeedVessel.Id);
            var cargo = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(CargoPlan: new("FICTIONAL cargo plan revision", dg)), verify, SeedVessel.Id);
            Asset asset; await using (var db = Db()) asset = await db.Assets.AsNoTracking().FirstAsync(a => a.VesselId == SeedVessel.Id, Ct);
            var winch = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(WinchFitment: new(asset.Id, asset.Revision, asset.Version, fitted, "FICTIONAL register assessment")), verify, SeedVessel.Id);
            CapacityRights(host.Services); await Token(c);
            var command = new EvaluateCommand(SeedVessel.Id, [rule.Current.Id], OwningInputs: new(CargoPlanId: cargo.Id, WinchFitmentId: winch.Id)); var key = Guid.NewGuid().ToString();
            using var evaluated = await Send(c, "applicability/evaluate", command, key); Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
            var result = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; var facts = result.Snapshot.GetProperty("qualifications")[0].GetProperty("facts");
            foreach (var (field, expected) in new[] { ("cargo.packagedDangerousGoods", dg), ("equipment.towingWinchFitted", fitted) }) {
                var fact = facts.GetProperty(field); Assert.Equal(verify ? "Known" : "Unknown", fact.GetProperty("knowledge").GetString());
                if (verify) Assert.Equal(expected, fact.GetProperty("value").GetBoolean()); else Assert.Equal(JsonValueKind.Null, fact.GetProperty("value").ValueKind);
            }
            var golden = facts.Deserialize<Dictionary<string, ContextFact>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(!verify ? "REVIEW_REQUIRED" : dg ? "APPLICABLE" : "NOT_APPLICABLE", ApplicabilityEngine.Preview(OwningGoldenSample("RULE-IMDG-PACKAGED-DG"), golden, result.AsOf).Result);
            Assert.True((await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct)).GetProperty("current").GetBoolean());
            var snapshot = result.SnapshotHash;
            // Exact no-op equipment write changes xmin and withdraws its qualified fitment.
            await using (var db = Db()) { var row = await db.Assets.SingleAsync(a => a.Id == asset.Id, Ct); db.Entry(row).Property(a => a.Revision).IsModified = true; await db.SaveChangesAsync(Ct); }
            var current = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct);
            if (verify) Assert.Contains(current.GetProperty("causes").EnumerateArray(), x => x.GetString() == "OWNING_INPUT_CHANGED");
            var retained = await c.GetFromJsonAsync<EvaluationDto>($"/api/v1/applicability/evaluations/{result.Id}", Ct); Assert.Equal(snapshot, retained!.SnapshotHash);
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "equipment.read").ToArray() };
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/applicability/evaluations/{result.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/evidence/{cargo.Id}/versions?vesselId={SeedVessel.Id}", Ct)).StatusCode);
            using var deniedRetry = await Send(c, "applicability/evaluate", command, key); Assert.Equal(HttpStatusCode.Forbidden, deniedRetry.StatusCode);
            var history = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations?vesselId={SeedVessel.Id}", Ct);
            Assert.DoesNotContain(history.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == result.Id);
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Incomplete_cargo_race_noop_and_receipt_rollback_preserve_operator_history() {
        var fault = new RejectGraphReceipt(); await using var host = Host(interceptor: fault, tugRead: true, tugWrite: true, technicalRead: true);
        using var c = Client(host); CapacityRights(host.Services); await Token(c);
        var command = GraphCommand(content: new(Title: "FICTIONAL incomplete cargo", CargoPlan: new())); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Send(c, GraphRoute("evidence", Tug.Id), command, key), Send(c, GraphRoute("evidence", Tug.Id), command, key));
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var first = (await responses.First(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!; foreach (var r in responses) r.Dispose();
        Assert.Contains("OWNING_INPUT_INCOMPLETE", first.Blockers); Assert.Null(first.Content.CargoPlan!.PackagedDangerousGoods);
        var revised = await CapacitySave(c, $"evidence/{first.Id}", command with { ExpectedVersion = first.Version, ReasonCode = "AUTHORING_CORRECTION" }, true);
        Assert.Equal(first.Revision + 1, revised.Revision);
        using var replay = await Send(c, GraphRoute("evidence", Tug.Id), command, key); Assert.Equal(first.Version, (await replay.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!.Version);
        fault.Armed = true;
        using var failed = await Send(c, GraphRoute($"evidence/{first.Id}", Tug.Id), command with { ExpectedVersion = revised.Version, ReasonCode = "AUTHORING_CORRECTION" }, update: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode); fault.Armed = false;
        await using var db = Db(); Assert.Equal(2, await db.OperationEquipmentBindings.CountAsync(b => b.Id == first.Id, Ct));
        Assert.Equal(revised.Revision, (await db.ControlEvidenceHeads.SingleAsync(h => h.Id == first.Id, Ct)).Revision);
        Assert.Equal(2, await db.ControlEvidenceReceipts.CountAsync(r => r.ResourceId == first.Id, Ct));
    }
    [Fact]
    public async Task Wrong_vessel_equipment_and_client_asserted_authority_are_rejected_without_business_receipts() {
        await using var host = Host(tugRead: true, tugWrite: true, technicalRead: true); using var c = Client(host); CapacityRights(host.Services); await Token(c);
        Asset foreign; await using (var db = Db()) foreign = await db.Assets.FirstAsync(a => a.VesselId != Tug.Id, Ct);
        var key = Guid.NewGuid().ToString();
        using var wrong = await Send(c, GraphRoute("evidence", Tug.Id), GraphCommand(content: new(WinchFitment: new(foreign.Id, foreign.Revision, foreign.Version, true, "FICTIONAL wrong vessel negative"))), key);
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "applicability.evaluate"] };
        using var forged = await Send(c, "applicability/evaluate", new { vesselId = Tug.Id, ruleIds = new[] { Guid.NewGuid() }, owningInputs = new { cargoPlanId = Guid.NewGuid(), packagedDangerousGoods = false } });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        await using var check = Db(); Assert.False(await check.ControlEvidenceReceipts.AnyAsync(r => r.Key == key, Ct));
        Assert.Empty(await check.OperationEquipmentBindings.ToArrayAsync(Ct));
    }
}
