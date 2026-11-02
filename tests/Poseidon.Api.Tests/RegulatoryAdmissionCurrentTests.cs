using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Real host, antiforgery and disposable PostgreSQL. No production appointments or sign-offs.
public sealed partial class DirectoryWorkspaceTests
{
    private void RegulatoryHuman(IServiceProvider services, Guid rule, Guid source, long sourceRevision, Guid human, string kind = "Human", string? mechanism = null)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0]; var now = DateTimeOffset.UtcNow;
        var contract = new RegulatorySourceContract("synthetic/source-contract", "FICTIONAL_DECLARATION", mechanism);
        settings.Memberships[0] = m with { ActorId = human, ActorKind = kind, Permissions = [..GraphPermissions, "rules.write", "rules.review", "applicability.evaluate", "regulatory.qualify", "regulatory.facts.attest"],
            RegulatoryAuthorities = [new(Guid.NewGuid(), rule, source, sourceRevision, ReviewRole, ["draft", "review", "qualify", "own"], now.AddDays(-1), now.AddDays(2), contract),
                new(Guid.NewGuid(), rule, source, sourceRevision, ReviewRole, ["attest"], now.AddDays(-1), now.AddDays(2), contract, SeedVessel.Id)] };
    }
    private async Task<(RuleDraftDto Rule, RegisterDto Source, RegisterDto Requirement)> AdmissionInputs(HttpClient c, JsonElement? document = null)
    {
        var source = await CreateRegister(c, content: new() { Title = new("Known", "FICTIONAL source"), Authority = new("Known", "FICTIONAL publisher"), Locator = new("Known", "synthetic/only") });
        var requirement = await CreateRegister(c, "requirement-drafts", new() { Instrument = new("Known", "FICTIONAL requirement"), Amendment = new("Known", "1") }, source.Current.Id, source.Current.Revision);
        using var response = await Send(c, "rules", new RuleAuthorCommand(0, ApplicabilityEngine.Contract, document ?? FictionalRule(), source.Current.Id, source.Current.Revision, requirement.Current.Id, requirement.Current.Revision, "INITIAL_AUTHORING"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return ((await response.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!, source, requirement);
    }
    private async Task<QualificationDto> Qualify(HttpClient c, IServiceProvider services, RuleDraftDto rule, string state, QualificationContent? content = null, QualificationDto? prior = null, string? key = null)
    {
        var grant = services.GetRequiredService<FederationSettings>().Memberships[0].RegulatoryAuthorities.Single(g => g.VesselId == null);
        using var response = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", new QualificationCommand(prior?.Version ?? 0, rule.Current.Revision, grant.Id, state, content), key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<QualificationDto>(Ct))!;
    }
    private async Task<(RuleDraftDto Rule, QualificationDto Qualification, EvaluationDto Evaluation)> AdmittedByCommands(HttpClient c, IServiceProvider services, JsonElement? document = null, QualifiedInputPolicy[]? inputs = null, string expected = "APPLICABLE", QualifiedControlPolicy? controlPolicy = null, TugCalculationPolicy? tugCalculation = null, Guid? evaluationVessel = null)
    {
        GraphPermissionsForTest(services);
        var settings = services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..GraphPermissions, "rules.write", "applicability.evaluate"] };
        var (rule, source, _) = await AdmissionInputs(c, document);
        var author = Guid.NewGuid(); RegulatoryHuman(services, rule.Current.Id, source.Current.Id, source.Current.Revision, author); await Token(c);
        var content = new QualificationContent(Convert.ToBase64String("FICTIONAL source bytes, no maritime authority"u8.ToArray()), "FICTIONAL publisher", "1", author, ReviewRole,
            "FICTIONAL software review", inputs ?? [new("vessel.flag", "Declared")], controlPolicy ?? new(ReviewRole, "FICTIONAL frequency", "FICTIONAL evidence policy", "FICTIONAL independent review", 24, false, false, "synthetic/policy"), TugCalculation: tugCalculation);
        var draft = await Qualify(c, services, rule, "DRAFT", content);
        var review = await Qualify(c, services, rule, "IN_REVIEW", prior: draft);
        // Retain the first owner's real synthetic server appointment while the reader changes.
        var owner = settings.Memberships[0] with { Subject = "synthetic-non-session-owner" };
        settings.Memberships = [settings.Memberships[0], owner];
        RegulatoryHuman(services, rule.Current.Id, source.Current.Id, source.Current.Revision, Guid.NewGuid()); await Token(c);
        var qualified = await Qualify(c, services, rule, "IN_FORCE", prior: review);
        if (evaluationVessel != null) settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "mode.tug", "mode.cross"] };
        using var evaluated = await Send(c, "applicability/evaluate", new EvaluateCommand(evaluationVessel ?? SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode); var evaluation = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        Assert.True(evaluation.Authoritative); Assert.False(evaluation.Simulation); Assert.Equal(expected, Assert.Single(evaluation.Results).Prediction.Result);
        return (rule, qualified, evaluation);
    }
    [Fact]
    public async Task Command_qualified_source_evaluates_then_control_evidence_independent_verification_recording_and_assigned_auditor_read()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, evaluation) = await AdmittedByCommands(c, host.Services);
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var m = host.Services.GetRequiredService<FederationSettings>().Memberships[0];
        var coCommand = GraphCommand(content: new(Title: "FICTIONAL control", OwnerId: m.ActorId, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] };
        var co = await GraphSave(c, "controls", coCommand);
        using (var submitted = await Send(c, GraphRoute($"controls/{co.Id}/submit"), coCommand with { ExpectedVersion = co.Version, ReasonCode = "SUBMIT" })) {
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode); co = (await submitted.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!; Assert.Equal("SUBMITTED", co.Lifecycle); Assert.Equal("UNVERIFIED", co.Verification);
        }
        using (var completed = await Send(c, GraphRoute($"controls/{co.Id}/complete"), coCommand with { ExpectedVersion = co.Version, ReasonCode = "COMPLETE" })) {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode); co = (await completed.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!; Assert.Equal("COMPLETED", co.Completion); Assert.Equal("UNKNOWN", co.Effectiveness);
        }
        var now = DateTimeOffset.UtcNow;
        var ev = await GraphSave(c, "evidence", GraphCommand(content: new(Title: "FICTIONAL declaration evidence", OwnerId: m.ActorId, OwnerRole: ReviewRole, Provenance: "Synthetic declaration, no legal proof", ObservedAt: now.AddMinutes(-2), ValidFrom: now.AddMinutes(-2), ValidUntil: now.AddHours(1), FreshUntil: now.AddHours(1))) with { Requirements = [pin], Links = [new("Control", co.Id, co.Revision, co.Version)] });
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id);
        using var pendingResponse = await Send(c, GraphRoute("verifications"), ReviewCommand(host.Services, ev, co) with { EvaluationId = evaluation.Id }); Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pending = (await pendingResponse.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, newActor: Guid.NewGuid()); await Token(c);
        var verify = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        using var verified = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), verify with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED" });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode); var verification = (await verified.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        RecordingAppointment(host.Services, ev.Id, co.Id);
        using var recorded = await Send(c, GraphRoute("decisions"), await RecordingCommand(c, host.Services, ev, co, verification));
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode); var decision = (await recorded.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
        Assert.Equal("UNASSESSED", decision.Recording!.Qualification);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { ActorId = Guid.NewGuid(), Permissions = ["directory.read", "audit.read", "human.trace.read"], TenantWideManage = false, TenantWideRead = false,
            VesselGrants = [new(SeedVessel.Id)], HumanAuthorities = [], HumanTraceReaders = [new(Guid.NewGuid(), SeedVessel.Id, ev.Id, co.Id, ReviewRole, ["Verification", "Decision"], [decision.Recording.Content.DecisionType], now.AddHours(-1), now.AddHours(1))] };
        await Token(c);
        using var trace = await c.GetAsync(TraceRoute(ev, co), Ct); Assert.Equal(HttpStatusCode.OK, trace.StatusCode);
        var metadata = await trace.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Empty(metadata.GetProperty("items").EnumerateArray()); Assert.Empty(metadata.GetProperty("authorities").EnumerateArray());
        Assert.True(metadata.GetProperty("complete").GetBoolean()); Assert.Equal("RECORDED_CHAIN", metadata.GetProperty("disposition").GetString());
        Assert.Contains(metadata.GetProperty("auditItems").EnumerateArray(), e => e.GetProperty("id").GetGuid() == decision.Id && e.GetProperty("state").GetString() == "RECORDED");
        Assert.DoesNotContain(decision.Rationale, metadata.GetRawText()); Assert.DoesNotContain(decision.Recording.Content.HumanDecision, metadata.GetRawText()); Assert.DoesNotContain("decisionJson", metadata.GetRawText());
        using var mutate = await Send(c, GraphRoute("decisions"), new { state = "RECORDED", rationale = decision.Rationale }); Assert.Equal(HttpStatusCode.Forbidden, mutate.StatusCode);
        settings.Memberships[0].HumanTraceReaders = [];
        using var revoked = await c.GetAsync(TraceRoute(ev, co) + "&asOf=" + Uri.EscapeDataString(decision.AsOf.ToString("O")), Ct); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        await using var db = Db(); Assert.Single(await db.ApplicabilityEvaluations.ToArrayAsync(Ct)); Assert.NotEmpty(await db.EvaluationQualifications.ToArrayAsync(Ct));
    }

    [Theory]
    [InlineData("AI")]
    [InlineData("Service")]
    [InlineData("Unspecified")]
    [InlineData("revoked")]
    [InlineData("wrong-role")]
    [InlineData("wrong-source")]
    [InlineData("duplicate")]
    public async Task Qualification_is_not_granted_by_claimed_admin_actor_or_revoked_or_wrong_binding(string kind)
    {
        await using var host = ApplicabilityHost(); using var c = Client(host); await Token(c);
        var (rule, source, _) = await AdmissionInputs(c); RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, 1, actorId, kind is "AI" or "Service" or "Unspecified" ? kind : "Human");
        var m = host.Services.GetRequiredService<FederationSettings>().Memberships[0]; var grant = m.RegulatoryAuthorities[0];
        m.RegulatoryAuthorities[0] = kind switch { "revoked" => grant with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) }, "wrong-role" => grant with { RoleId = Guid.NewGuid() }, "wrong-source" => grant with { SourceId = Guid.NewGuid() }, _ => grant };
        if (kind == "duplicate") m.RegulatoryAuthorities = [..m.RegulatoryAuthorities, grant];
        using var response = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", new QualificationCommand(0, 1, grant.Id, "DRAFT", new()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = Db(); Assert.Empty(await db.RegulatoryQualificationEvents.Where(x => x.RuleId == rule.Current.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Changed_profile_invalidates_current_projection_without_recomputing_immutable_evaluation()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (_, _, evaluation) = await AdmittedByCommands(c, host.Services);
        var bytes = await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct);
        var profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        using var edit = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", new SaveRegulatoryDraft(profile.Version, profile.Content with { Flag = new("Unknown") }), update: true); Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var current = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{evaluation.Id}/current", Ct);
        Assert.False(current.GetProperty("current").GetBoolean()); Assert.Contains("PROFILE_CHANGED", current.GetRawText());
        Assert.Equal(bytes, await c.GetStringAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct));
    }
    [Fact]
    public async Task Required_source_mechanism_blocks_only_its_type_and_noop_receipt_race_is_atomic()
    {
        await using var host = ApplicabilityHost(); using var c = Client(host); await Token(c);
        var (rule, source, _) = await AdmissionInputs(c); RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, 1, actorId, mechanism: "FICTIONAL_REQUIRED_SIGNATURE");
        var grant = host.Services.GetRequiredService<FederationSettings>().Memberships[0].RegulatoryAuthorities[0];
        var cmd = new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", new()); var key = Guid.NewGuid().ToString();
        var outcomes = await Task.WhenAll(Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", cmd, key), Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", cmd, key));
        foreach (var r in outcomes) { Assert.Equal(HttpStatusCode.OK, r.StatusCode); r.Dispose(); }
        using var stale = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", cmd); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = Db(); Assert.Single(await db.RegulatoryQualificationEvents.Where(x => x.RuleId == rule.Current.Id).ToArrayAsync(Ct)); Assert.Single(await db.RegulatoryQualificationReceipts.Where(x => x.Key == key).ToArrayAsync(Ct));
    }
    [Theory]
    [InlineData("signature", "source_mechanism_unavailable")]
    [InlineData("self", "independent_reviewer_required")]
    [InlineData("future", "outside_effective_dates")]
    public async Task Qualification_review_records_real_type_specific_blockers_without_a_blanket_gate(string scenario, string code)
    {
        await using var host = ApplicabilityHost(); using var c = Client(host); await Token(c);
        var doc = JsonSerializer.Deserialize<ApplicabilityRule>(FictionalRule(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (scenario == "future") doc = doc with { Lifecycle = new("FUTURE_EFFECTIVE", "9999-01-01") };
        var (rule, source, _) = await AdmissionInputs(c, JsonSerializer.SerializeToElement(doc, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var mechanism = scenario == "signature" ? "FICTIONAL_REQUIRED_SIGNATURE" : null;
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, 1, actorId, mechanism: mechanism);
        var draft = await Qualify(c, host.Services, rule, "DRAFT", new(Convert.ToBase64String("FICTIONAL source bytes"u8.ToArray()), "FICTIONAL publisher", "1", actorId, ReviewRole, "FICTIONAL review", [], null));
        using (var bytes = await c.GetAsync($"/api/v1/regulatory-source-governance/{rule.Current.Id}/revisions/{draft.Revision}/source", Ct)) {
            Assert.Equal(HttpStatusCode.OK, bytes.StatusCode); Assert.Equal("FICTIONAL source bytes", await bytes.Content.ReadAsStringAsync(Ct));
        }
        var review = await Qualify(c, host.Services, rule, "IN_REVIEW", prior: draft);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        if (scenario != "self") {
            settings.Memberships = [settings.Memberships[0], settings.Memberships[0] with { Subject = "synthetic-retained-owner" }];
            RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, 1, Guid.NewGuid(), mechanism: mechanism); await Token(c);
        }
        var grant = settings.Memberships[0].RegulatoryAuthorities[0];
        using var qualified = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", new QualificationCommand(review.Version, rule.Current.Revision, grant.Id, "IN_FORCE"));
        Assert.Equal(HttpStatusCode.Conflict, qualified.StatusCode); Assert.Contains(code, await qualified.Content.ReadAsStringAsync(Ct));
        settings.Memberships[0].RegulatoryAuthorities = [];
        using var revokedRead = await c.GetAsync($"/api/v1/regulatory-source-governance/{rule.Current.Id}/revisions/{draft.Revision}/source", Ct); Assert.Equal(HttpStatusCode.Forbidden, revokedRead.StatusCode);
    }
    private sealed class RejectQualificationReceipt : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO core.\"RegulatoryQualificationReceipts\"", StringComparison.Ordinal)) throw new Npgsql.NpgsqlException("Synthetic qualification receipt failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Known_declared_GT_stays_unverified_until_exact_current_profile_is_attested_through_command()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var document = JsonSerializer.Deserialize<ApplicabilityRule>(FictionalRule(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        document = document with { When = JsonSerializer.SerializeToElement(new { field = "vessel.gt", @operator = "GTE", value = 400 }) };
        var (rule, q, _) = await AdmittedByCommands(c, host.Services, JsonSerializer.SerializeToElement(document, new JsonSerializerOptions(JsonSerializerDefaults.Web)), [new("vessel.gt", "HumanAttested", 24)], "REVIEW_REQUIRED");
        var profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        using var profileResponse = await Send(c, $"vessels/{SeedVessel.Id}/regulatory-profile", new SaveRegulatoryDraft(profile.Version, profile.Content with { Gt = new("Known", "420") }), update: true);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode); profile = (await profileResponse.Content.ReadFromJsonAsync<RegulatoryDraftDto>(Ct))!;
        using var unverified = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal("REVIEW_REQUIRED", (await unverified.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!.Results[0].Prediction.Result);
        // Evaluation touched the profile xmin, so capture the current version again.
        profile = (await c.GetFromJsonAsync<RegulatoryDraftDto>($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct))!;
        var page = await c.GetFromJsonAsync<JsonElement>($"/api/v1/regulatory-source-governance/{rule.Current.Id}?vesselId={SeedVessel.Id}", Ct);
        var current = page.GetProperty("items")[0];
        var grant = host.Services.GetRequiredService<FederationSettings>().Memberships[0].RegulatoryAuthorities.Single(g => g.VesselId != null);
        var attest = new FactAttestationCommand(grant.Id, SeedVessel.Id, profile.Revision, profile.Version, q.Revision, current.GetProperty("version").GetUInt32(), ["vessel.gt"], DateTimeOffset.UtcNow.AddHours(1));
        using var attested = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/fact-attestations", attest); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        using var evaluated = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]));
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode); var result = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        Assert.Equal("APPLICABLE", result.Results[0].Prediction.Result);
        await using var db = Db(); var binding = Assert.Single(await db.EvaluationQualifications.Where(x => x.EvaluationId == result.Id).ToArrayAsync(Ct)); Assert.NotNull(binding.AttestationId);
        using var spoofed = await Send(c, "applicability/evaluate", new { vesselId = SeedVessel.Id, ruleIds = new[] { rule.Current.Id }, facts = new { gt = "10000" } }); Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        using var future = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id], DateTimeOffset.UtcNow.AddDays(1).ToString("O"))); Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }
    [Theory]
    [InlineData("absent")]
    [InlineData("revoked")]
    [InlineData("role")]
    [InlineData("subject")]
    [InlineData("tenant")]
    [InlineData("vessel")]
    [InlineData("type")]
    public async Task Auditor_reader_scope_is_live_independent_of_author_and_never_reveals_private_decision_fields(string failure)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); RecordingAppointment(host.Services, ev.Id, co.Id);
        var pending = await PendingReview(c, host.Services, ev, co);
        using var saved = await Send(c, GraphRoute("decisions"), await RecordingCommand(c, host.Services, ev, co, pending)); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var decision = (await saved.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!; var now = DateTimeOffset.UtcNow;
        var settings = host.Services.GetRequiredService<FederationSettings>(); var grant = new HumanTraceReader(Guid.NewGuid(), SeedVessel.Id, ev.Id, co.Id, ReviewRole, ["Decision"], [decision.Recording!.Content.DecisionType], now.AddDays(-1), now.AddHours(1));
        grant = failure switch { "revoked" => grant with { ValidUntil = now.AddSeconds(-1) }, "role" => grant with { RoleId = Guid.NewGuid() }, "subject" => grant with { EvidenceId = Guid.NewGuid() }, "type" => grant with { DecisionTypes = ["UNASSIGNED_TYPE"] }, _ => grant };
        settings.Memberships[0] = settings.Memberships[0] with { ActorId = Guid.NewGuid(), TenantWideManage = false, TenantWideRead = false, Permissions = ["directory.read", "audit.read", "human.trace.read"],
            VesselGrants = failure == "vessel" ? [] : [new(SeedVessel.Id)], HumanAuthorities = [], HumanTraceReaders = failure == "absent" ? [] : [grant] };
        if (failure == "tenant") settings.Memberships[0] = settings.Memberships[0] with { TenantId = seed.Tenants[1].Id };
        await Token(c);
        using var result = await c.GetAsync(TraceRoute(ev, co) + "&asOf=" + Uri.EscapeDataString(decision.AsOf.ToString("O")), Ct);
        if (failure == "type") { Assert.Equal(HttpStatusCode.OK, result.StatusCode); var value = await result.Content.ReadFromJsonAsync<JsonElement>(Ct); Assert.Empty(value.GetProperty("auditItems").EnumerateArray()); Assert.False(value.GetProperty("auditHasMore").GetBoolean()); }
        else Assert.True(result.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        var body = await result.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain(decision.Rationale, body); Assert.DoesNotContain(decision.Recording.Content.HumanDecision, body);
    }
    [Fact]
    public async Task Assigned_auditor_cannot_read_or_count_a_historical_document_protected_evidence_chain()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-auditor-exclusion-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            // Real file/API/PostgreSQL transport; protocol peer is not scanner deployment proof.
            using var scanner = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, scanner.Port); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
            var (ev, co) = await ReviewInputs(c);
            var prefix = $"vessels/{SeedVessel.Id}/documents/{SeedVessel.Id}/versions/1/files";
            using var stage = await Send(c, prefix, new { name = "synthetic-private.png", mime = "image/png", length = Png.Length, checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png)) });
            Assert.Equal(HttpStatusCode.OK, stage.StatusCode); var file = (await stage.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/{prefix}/{file.Id}/bytes") { Content = new ByteArrayContent(Png) }; put.Content.Headers.ContentType = new("image/png");
            using var uploaded = await c.SendAsync(put, Ct); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            await using (var db = Db()) {
                var stored = await db.StoredFiles.SingleAsync(x => x.Id == file.Id, Ct);
                // Negative-only historical relationship fixture. The normal evidence
                // command cannot create this protected relationship; no admission or
                // verification success path is supplied by this insertion.
                var head = await db.ControlEvidenceHeads.SingleAsync(x => x.Kind == "Evidence" && x.Id == ev.Id, Ct); head.Revision++;
                var revision = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(x => x.Kind == "Evidence" && x.Id == ev.Id && x.Revision == ev.Revision, Ct);
                revision.Revision = head.Revision; revision.FileId = stored.Id; revision.FileRevision = stored.Revision; revision.Checksum = stored.Checksum; revision.SavedAt = DateTimeOffset.UtcNow; revision.Hash = RegisterStorage.Hash("negative historical protected fixture");
                db.ControlEvidenceRevisions.Add(revision); db.ControlEvidenceEdges.Add(new() { TenantId = db.TenantId, VesselId = SeedVessel.Id, Kind = "Evidence", Id = ev.Id, Revision = revision.Revision, TargetKind = "Control", TargetId = co.Id, TargetRevision = co.Revision, TargetVersion = co.Version }); await db.SaveChangesAsync(Ct);
            }
            var now = DateTimeOffset.UtcNow; var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { ActorKind = "Human", Permissions = ["directory.read", "audit.read", "human.trace.read"], HumanAuthorities = [],
                HumanTraceReaders = [new(Guid.NewGuid(), SeedVessel.Id, ev.Id, co.Id, ReviewRole, ["Verification", "Decision"], [], now.AddDays(-1), now.AddHours(1))] };
            using var denied = await c.GetAsync(TraceRoute(ev with { Revision = ev.Revision + 1 }, co), Ct); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain(file.Id.ToString(), body); Assert.DoesNotContain("auditItems", body); Assert.DoesNotContain("count", body); Assert.DoesNotContain("synthetic-private", body);
        } finally { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root, false); }
    }
    [Fact]
    public async Task Failed_receipt_rolls_back_qualification_input_touches_events_and_audit()
    {
        var fault = new RejectQualificationReceipt(); await using var host = Host(interceptor: fault); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var settings = host.Services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..GraphPermissions, "rules.write"] };
        var (rule, source, _) = await AdmissionInputs(c); RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, 1, actorId);
        var grant = settings.Memberships[0].RegulatoryAuthorities[0]; long before;
        await using (var db = Db()) before = await db.AuditEvents.LongCountAsync(Ct);
        fault.Armed = true;
        using var response = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events", new QualificationCommand(0, 1, grant.Id, "DRAFT", new())); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var check = Db(); Assert.Equal(before, await check.AuditEvents.LongCountAsync(Ct)); Assert.Empty(await check.RegulatoryQualificationEvents.Where(x => x.RuleId == rule.Current.Id).ToArrayAsync(Ct));
    }
}
