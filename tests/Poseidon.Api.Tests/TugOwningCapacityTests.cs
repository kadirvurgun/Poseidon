using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// AUTHORED / NOT RUN. Software-only fictional source, actors and evidence in owned PG.
// Positive capability is produced through HTTP commands, never inserted verified rows.
public sealed partial class DirectoryWorkspaceTests
{
    private static TugCalculationPolicy FictionalCapacityPolicy => new("MeasuredTimesVerifiedCondition", "SumForPlanning", "FICTIONAL/source/formula-not-a-maritime-rule", [TugRequestPolicy.JobTypes[0]], ["FICTIONAL-AREA"], 3, "Floor", 365, "10");
    [Fact]
    public async Task Actual_certificate_command_expiry_blocks_board_and_current_owning_access_withdrawal_removes_protected_facts() {
        using var host = PlanningHost(); using var c = Client(host);
        var s = host.Services.GetRequiredService<FederationSettings>();
        s.Memberships[0] = s.Memberships[0] with { Permissions = [..s.Memberships[0].Permissions, "crew.read", "crew.write", "crew.identity.read", "crew.certificate.read", "crew.certificate.write"] };
        await Token(c); var job = await PlanningJob(c); var start = DateTimeOffset.UtcNow.AddDays(2);
        var plan = await ChangePlan(c, job, null, new("Window", start, start.AddHours(2))); await ChangePlan(c, job, plan, new("Assign", TugId: Tug.Id));
        var credential = Guid.NewGuid();
        using var created = await Send(c, $"vessels/{Tug.Id}/crew/{Tug.Id}/certificates", new CredentialSaveCommand(credential, null,
            new(new CredentialDeclaration(ExpiryDate: DateOnly.FromDateTime(start.AddDays(-1).UtcDateTime)))));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var board = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}", Ct);
        var row = board!.Items.Single(j => j.Request.Id == job.Id); Assert.Equal("Blocked", row.Readiness);
        Assert.Equal("ResolveOwningReadinessBlockers", row.NextAction);
        Assert.Contains(row.PlanningEvidence!.ReadinessFacts!, f => f.Id == credential && f.State == "Blocked" && f.Revision == 1);
        s.Memberships[0] = s.Memberships[0] with { Permissions = s.Memberships[0].Permissions.Where(p => p != "crew.certificate.read").ToArray() };
        var denied = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}", Ct);
        var facts = denied!.Items.Single(j => j.Request.Id == job.Id).PlanningEvidence!.ReadinessFacts!;
        Assert.DoesNotContain(facts, f => f.Id == credential); Assert.Contains(facts, f => f.Source == "Crew" && f.State == "Unavailable");
        Assert.DoesNotContain(facts, f => f.State == "Current");
    }
    private void CapacityRights(IServiceProvider services) {
        var s = services.GetRequiredService<FederationSettings>();
        s.Memberships[0] = s.Memberships[0] with { Permissions = [..s.Memberships[0].Permissions, ..GraphPermissions,
            "mode.tug", "mode.cross", "tug.read", "tug.write", "tug.plan.write", "equipment.read", "equipment.write", "maintenance.read", "crew.read", "crew.certificate.read", "human.review.read", "human.review.submit", "human.verify"] };
    }
    private void RetainCapacityHuman(IServiceProvider services) {
        var s = services.GetRequiredService<FederationSettings>();
        s.Memberships = [s.Memberships[0], ..s.Memberships.Skip(1).Where(m => m.ActorId != s.Memberships[0].ActorId), s.Memberships[0] with { Subject = "fictional-retained-" + Guid.NewGuid().ToString("N") }];
    }
    private void CapacityHuman(IServiceProvider services, ControlEvidenceDto evidence, ControlEvidenceDto control, bool independent = false) {
        var s = services.GetRequiredService<FederationSettings>(); var now = DateTimeOffset.UtcNow;
        if (independent) RetainCapacityHuman(services);
        s.Memberships[0] = s.Memberships[0] with { ActorKind = "Human", ActorId = independent ? Guid.NewGuid() : s.Memberships[0].ActorId,
            HumanAuthorities = (independent ? Array.Empty<HumanAuthority>() : s.Memberships[0].HumanAuthorities.Where(g => g.EvidenceId != evidence.Id)).Concat(new[] { "human.review.submit", "human.verify" }.Select(cap => new HumanAuthority(Guid.NewGuid(), Tug.Id, evidence.Id, control.Id, ReviewOperation, ReviewRole, cap, now.AddDays(-1), now.AddDays(1)))).ToArray() };
        CapacityRights(services);
    }
    private async Task<ControlEvidenceDto> CapacitySave(HttpClient c, string route, ControlEvidenceCommand cmd, bool update = false, string? key = null) {
        using var response = await Send(c, GraphRoute(route, Tug.Id), cmd, key, update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
    }
    private async Task<ControlEvidenceDto> CapacityVerify(HttpClient c, IServiceProvider services, ControlEvidenceDto evidence, ControlEvidenceDto control, Guid evaluation) {
        CapacityHuman(services, evidence, control); await Token(c);
        var m = services.GetRequiredService<FederationSettings>().Memberships[0]; var grant = m.HumanAuthorities.Single(g => g.EvidenceId == evidence.Id && g.Capability == "human.review.submit");
        var command = new HumanReviewCommand(0, grant.Id, ReviewOperation, evidence.Id, evidence.Revision, evidence.Version, control.Id, control.Revision, control.Version,
            evaluation, null, null, "PENDING", "REQUEST_REVIEW", "FICTIONAL software verification request");
        using var pendingResponse = await Send(c, GraphRoute("verifications", Tug.Id), command); Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        CapacityHuman(services, evidence, control, independent: true); await Token(c);
        var trace = await c.GetFromJsonAsync<JsonElement>($"/api/v1/regulatory-trace?vesselId={Tug.Id}&evidenceId={evidence.Id}&evidenceRevision={evidence.Revision}&controlId={control.Id}&controlRevision={control.Revision}", Ct);
        grant = services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities.Single(g => g.EvidenceId == evidence.Id && g.Capability == "human.verify");
        using var verified = await Send(c, GraphRoute($"verifications/{pending.Id}/events", Tug.Id), command with { ExpectedVersion = pending.Version, AuthorityId = grant.Id,
            EvidenceVersion = trace.GetProperty("evidenceVersion").GetUInt32(), ControlVersion = trace.GetProperty("controlVersion").GetUInt32(), State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED", Rationale = "FICTIONAL independent software test review; no real certificate approval" });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        var current = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"evidence/{evidence.Id}/versions", Tug.Id), Ct);
        return current.GetProperty("items")[0].Deserialize<ControlEvidenceDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private async Task<FileDto> TugCertificateFile(HttpClient c) {
        using var staged = await Send(c, $"vessels/{Tug.Id}/files", new { name = "fictional-certificate.png", mime = "image/png", length = Png.Length, checksum = Convert.ToHexString(SHA256.HashData(Png)) });
        Assert.Equal(HttpStatusCode.OK, staged.StatusCode); var file = (await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{Tug.Id}/files/{file.Id}/bytes") { Content = new ByteArrayContent(Png) };
        request.Content.Headers.ContentType = new("image/png"); using var uploaded = await c.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); return (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
    }
    [Fact]
    public async Task Incomplete_capability_commands_have_atomic_lineage_race_replay_and_current_read_history_denial() {
        await using var host = Host(tugRead: true, tugWrite: true, technicalRead: true); using var c = Client(host);
        CapacityRights(host.Services); await Token(c);
        var cmd = GraphCommand(content: new(Title: "FICTIONAL incomplete capability", TugCapability: new("BollardPullTest")));
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(Send(c, GraphRoute("evidence", Tug.Id), cmd, key), Send(c, GraphRoute("evidence", Tug.Id), cmd, key));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var first = results.First(r => r.StatusCode == HttpStatusCode.OK); var saved = (await first.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        foreach (var r in results) r.Dispose();
        Assert.Contains("TUG_CAPABILITY_INCOMPLETE", saved.Blockers); Assert.Equal("DRAFT", saved.Lifecycle);
        using var replay = await Send(c, GraphRoute("evidence", Tug.Id), cmd, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(saved.Id, (await replay.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!.Id);
        using var incomplete = await Send(c, GraphRoute($"evidence/{saved.Id}/submit", Tug.Id), cmd with { ExpectedVersion = saved.Version, ReasonCode = "SUBMIT" });
        Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode); Assert.Contains("TUG_CAPABILITY_INCOMPLETE", await incomplete.Content.ReadAsStringAsync(Ct));
        var next = await CapacitySave(c, $"evidence/{saved.Id}", cmd with { ExpectedVersion = saved.Version, ReasonCode = "AUTHORING_CORRECTION" }, true);
        using var stale = await Send(c, GraphRoute($"evidence/{saved.Id}", Tug.Id), cmd with { ExpectedVersion = saved.Version, ReasonCode = "AUTHORING_CORRECTION" }, update: true); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var db = Db()) { Assert.Equal(2, await db.TugCapabilityBindings.CountAsync(x => x.Id == saved.Id, Ct)); Assert.Equal(next.Revision, (await db.ControlEvidenceHeads.SingleAsync(x => x.Id == saved.Id, Ct)).Revision); }
        var s = host.Services.GetRequiredService<FederationSettings>(); s.Memberships[0] = s.Memberships[0] with { Permissions = s.Memberships[0].Permissions.Where(p => p != "equipment.read").ToArray() };
        using var deniedReplay = await Send(c, GraphRoute("evidence", Tug.Id), cmd, key); Assert.Equal(HttpStatusCode.Forbidden, deniedReplay.StatusCode);
        using var history = await c.GetAsync("/api/v1/" + GraphRoute($"evidence/{saved.Id}/versions", Tug.Id), Ct);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode); Assert.DoesNotContain("tugCapability", await history.Content.ReadAsStringAsync(Ct));
        using var erase = await Send(c, GraphRoute($"evidence/{saved.Id}", Tug.Id), GraphCommand(next.Version), update: true); Assert.Equal(HttpStatusCode.Forbidden, erase.StatusCode);
    }
    [Fact]
    public async Task Capability_receipt_failure_rolls_back_binding_revision_and_write_audit_as_one_transaction() {
        var fault = new RejectGraphReceipt(); await using var host = Host(interceptor: fault, tugRead: true, tugWrite: true, technicalRead: true);
        using var c = Client(host); CapacityRights(host.Services); await Token(c);
        var cmd = GraphCommand(content: new(Title: "FICTIONAL rollback", TugCapability: new("Condition")));
        var first = await CapacitySave(c, "evidence", cmd); var key = Guid.NewGuid().ToString();
        int before; await using (var db = Db()) before = await db.AuditEvents.CountAsync(Ct);
        fault.Armed = true;
        using var failed = await Send(c, GraphRoute($"evidence/{first.Id}", Tug.Id), cmd with { ExpectedVersion = first.Version, ReasonCode = "AUTHORING_CORRECTION" }, key, true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        await using var verify = Db(); Assert.Equal(1, await verify.TugCapabilityBindings.CountAsync(x => x.Id == first.Id, Ct));
        Assert.Equal(first.Revision, (await verify.ControlEvidenceHeads.SingleAsync(x => x.Id == first.Id, Ct)).Revision);
        Assert.Equal(before, await verify.AuditEvents.CountAsync(Ct)); Assert.False(await verify.ControlEvidenceReceipts.AnyAsync(x => x.Key == key, Ct));
    }
    [Theory]
    [InlineData("current")][InlineData("profile-noop")][InlineData("equipment")][InlineData("authority")][InlineData("source")][InlineData("expired-test")][InlineData("mode")]
    public async Task Qualified_commands_produce_current_exact_capacity_and_current_context_changes_withdraw_it(string change) {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-tug-capacity-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0");
            var policy = SyntheticPlanning();
            await using var host = FileHost(root, scanner.Port).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(policy)));
            using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
            var (rule, _, evaluation) = await AdmittedByCommands(c, host.Services, tugCalculation: FictionalCapacityPolicy, evaluationVessel: Tug.Id);
            CapacityRights(host.Services); await Token(c);
            // Keep planning assignment eligibility exact to the current synthetic actor.
            var settings = host.Services.GetRequiredService<FederationSettings>(); policy.Scopes = [new(Tug.TenantId, settings.Memberships[0].ActorId, Tug.Id, [Tug.Id])];
            var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
            var co = await CapacitySave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL capability control", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole,
                Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] });
            var now = DateTimeOffset.UtcNow;
            uint profileVersion; Asset asset;
            await using (var read = Db()) { profileVersion = (await read.TugProfiles.AsNoTracking().SingleAsync(x => x.VesselId == Tug.Id, Ct)).Version; asset = await read.Assets.AsNoTracking().FirstAsync(x => x.VesselId == Tug.Id, Ct); }
            // Existing incomplete synthetic equipment is an owning record, not inserted qualified evidence.
            var conditionStatement = new TugCapabilityStatement("Condition", asset.Id, asset.Revision, profileVersion, ConditionFactor: "0.987654", SourceLocator: "FICTIONAL/condition", AssetVersion: asset.Version);
            var conditionCmd = GraphCommand(content: new(Title: "FICTIONAL condition", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole, Provenance: "Synthetic test declaration", ObservedAt: now.AddMinutes(-1), ValidFrom: now.AddMinutes(-1), ValidUntil: now.AddHours(2), FreshUntil: now.AddHours(2), TugCapability: conditionStatement)) with { Requirements = [pin], Links = [new("Control", co.Id, co.Revision, co.Version)] };
            var conditionFile = await TugCertificateFile(c);
            conditionCmd = conditionCmd with { File = new(conditionFile.Id, conditionFile.Revision!.Value, conditionFile.Checksum!) };
            var condition = await CapacitySave(c, "evidence", conditionCmd);
            condition = await CapacitySave(c, $"evidence/{condition.Id}/submit", conditionCmd with { ExpectedVersion = condition.Version, ReasonCode = "SUBMIT" });
            condition = await CapacityVerify(c, host.Services, condition, co, evaluation.Id);
            // The independent verifier changes actor; use current owning control xmin.
            var controlPage = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{co.Id}/versions", Tug.Id), Ct);
            co = controlPage.GetProperty("items")[0].Deserialize<ControlEvidenceDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var file = await TugCertificateFile(c);
            var testStatement = new TugCapabilityStatement("BollardPullTest", asset.Id, asset.Revision, profileVersion, "70", "67.123456", now.AddDays(-1), change == "expired-test" ? now.AddMinutes(20) : now.AddYears(1), SourceLocator: "FICTIONAL/certificate", ConditionEvidenceId: condition.Id, ConditionEvidenceRevision: condition.Revision, ConditionEvidenceVersion: condition.Version, AssetVersion: asset.Version);
            var testCmd = GraphCommand(content: conditionCmd.Content with { Title = "FICTIONAL BP test", OwnerId = settings.Memberships[0].ActorId, TugCapability = testStatement }) with { Requirements = [pin], File = new(file.Id, file.Revision!.Value, file.Checksum!), Links = [new("Control", co.Id, co.Revision, co.Version)] };
            var test = await CapacitySave(c, "evidence", testCmd);
            test = await CapacitySave(c, $"evidence/{test.Id}/submit", testCmd with { ExpectedVersion = test.Version, ReasonCode = "SUBMIT" });
            test = await CapacityVerify(c, host.Services, test, co, evaluation.Id);
            policy.Scopes = [new(Tug.TenantId, settings.Memberships[0].ActorId, Tug.Id, [Tug.Id])];
            var job = await PlanningJob(c);
            // Populate actual job context via the ordinary audited intake update.
            using var edited = await Send(c, $"tug-jobs/{job.Id}?vessel={Tug.Id}", new TugSaveCommand(job.Id, job.Version, job.Inputs with { JobType = TugRequestPolicy.JobTypes[0], OperationArea = "FICTIONAL-AREA" }), update: true);
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode); job = (await edited.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
            var plan = await ChangePlan(c, job, null, new("Window", now.AddMinutes(30), now.AddHours(1)));
            plan = await ChangePlan(c, job, plan, new("Assign", TugId: Tug.Id));
            var before = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + PlanningUrl(job.Id), Ct);
            Assert.Equal(change == "expired-test" ? JsonValueKind.Null : JsonValueKind.String, before.GetProperty("evidence").GetProperty("availableBollardPull").ValueKind);
            if (change != "expired-test") Assert.Equal("66.294", before.GetProperty("evidence").GetProperty("availableBollardPull").GetString());
            if (change == "profile-noop") {
                await using var db = Db(); var p = await db.TugProfiles.SingleAsync(x => x.VesselId == Tug.Id, Ct); db.Entry(p).Property(x => x.BollardPullTonnes).IsModified = true; await db.SaveChangesAsync(Ct);
            } else if (change == "equipment") {
                await using var db = Db(); var a = await db.Assets.SingleAsync(x => x.Id == asset.Id, Ct); db.Entry(a).Property(x => x.Revision).IsModified = true; await db.SaveChangesAsync(Ct);
            } else if (change == "authority") {
                foreach (var member in settings.Memberships.Where(m => m.ActorId == settings.Memberships[0].ActorId)) member.HumanAuthorities = [];
            } else if (change == "source") {
                var source = await c.GetFromJsonAsync<RegisterDto>($"/api/v1/source-drafts/{rule.Current.SourceId}", Ct);
                using var updated = await Send(c, $"source-drafts/{source!.Current.Id}", new RegisterCommand(source.Version, source.Current.Content), update: true);
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            } else if (change == "mode") {
                settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(x => x != "mode.tug").ToArray() };
            }
            using var after = await c.GetAsync("/api/v1/" + PlanningUrl(job.Id), Ct);
            if (change == "mode") Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
            else {
                Assert.Equal(HttpStatusCode.OK, after.StatusCode); var actual = await after.Content.ReadFromJsonAsync<JsonElement>(Ct);
                if (change != "current") Assert.Equal(JsonValueKind.Null, actual.GetProperty("evidence").GetProperty("availableBollardPull").ValueKind);
                else Assert.Equal("66.294", actual.GetProperty("evidence").GetProperty("availableBollardPull").GetString());
                Assert.NotEqual("Ready", actual.GetProperty("evidence").GetProperty("readiness").GetString());
                Assert.Equal(plan.Revision, actual.GetProperty("plan").GetProperty("revision").GetInt64());
            }
            await using var verifyDb = Db(); Assert.NotEmpty(await verifyDb.TugCapabilityBindings.Where(b => b.Id == test.Id).ToArrayAsync(Ct));
            Assert.Contains(await verifyDb.AuditEvents.ToArrayAsync(Ct), a => a.EntityType == nameof(TugCapabilityBinding));
        } finally { Directory.Delete(root, recursive: true); }
    }
}
