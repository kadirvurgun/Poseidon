using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual host + PostgreSQL + real ordinary file/source/independent review commands.
// Fictional source semantics only. AUTHORED / NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private static QualificationContent CompliancePolicy(QualificationContent c) => c with {
        Inputs = [..c.Inputs!, ..ComplianceInputs.Fields.Select(f => new QualifiedInputPolicy(f, "HumanAttested", 24))],
        ClassPack = new("FICTIONAL-PACK", "FICTIONAL-EDITION", "FICTIONAL/class/source"),
        OwningDocuments = ComplianceInputs.Kinds.Select(k => new QualifiedOwningDocumentPolicy(k, ReviewRole, ReviewRole, "FICTIONAL/compliance/source")).ToArray()
    };
    [Theory]
    [InlineData(true, true, "verifier")] [InlineData(false, true, "verifier")] [InlineData(true, false, "verifier")]
    [InlineData(true, true, "pack-revision")] [InlineData(true, true, "object-integrity")] [InlineData(true, true, "source-expiry")]
    public async Task Class_pack_msmd_and_source_evidence_require_current_independent_lineage(bool m79, bool verify, string change) {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-compliance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); await using var host = FileHost(root, scanner.Port); using var c = Client(host); await Token(c);
            var (rule, _, baseline) = await AdmittedByCommands(c, host.Services, qualification: CompliancePolicy);
            CapacityRights(host.Services); await Token(c); var settings = host.Services.GetRequiredService<FederationSettings>();
            var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
            var control = await CapacitySave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL compliance control", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] }, vessel: SeedVessel.Id);
            var pack = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(ClassPack: new("FICTIONAL-PACK", "FICTIONAL-EDITION", "FICTIONAL survey record", m79)), verify, SeedVessel.Id);
            HumanReviewDto? msmdVerification = null;
            var msmd = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(Msmd: new("FICTIONAL MSMD; no actual issuance", "FICTIONAL issuer", [new(SeedVessel.Id, 1, "FICTIONAL/msmd/role")])), verify, SeedVessel.Id, v => msmdVerification = v);
            CapacityRights(host.Services); await Token(c);
            var command = new EvaluateCommand(SeedVessel.Id, [rule.Current.Id], OwningInputs: new(ClassPackId: pack.Id, MsmdId: msmd.Id, SourceEvidenceId: pack.Id));
            using var response = await Send(c, "applicability/evaluate", command); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
            var facts = result.Snapshot.GetProperty("qualifications")[0].GetProperty("facts");
            foreach (var field in new[] { "class.rulePack.IACS_M79", "regulatory.msmdVerified", "evidence.sourceVerified" }) {
                Assert.Equal(verify ? "Known" : "Unknown", facts.GetProperty(field).GetProperty("knowledge").GetString());
                if (verify) Assert.Equal(field == "class.rulePack.IACS_M79" ? m79 : true, facts.GetProperty(field).GetProperty("value").GetBoolean());
            }
            Assert.Equal("Unknown", facts.GetProperty("crew.requiredRoleEligible").GetProperty("knowledge").GetString());
            Assert.True((await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct)).GetProperty("current").GetBoolean());
            if (verify) {
                RetainCapacityHuman(host.Services); settings.Memberships[0] = settings.Memberships[0] with { ActorId = Guid.NewGuid() };
                RecordingAppointment(host.Services, msmd.Id, control.Id); CapacityRights(host.Services); await Token(c);
                var controlPage = await c.GetFromJsonAsync<JsonElement>($"/api/v1/controls/{control.Id}/versions?vesselId={SeedVessel.Id}", Ct);
                var currentControl = controlPage.GetProperty("items")[0].Deserialize<ControlEvidenceDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                using var recorded = await Send(c, GraphRoute("decisions", SeedVessel.Id), await RecordingCommand(c, host.Services, msmd, currentControl, msmdVerification!));
                Assert.Equal(HttpStatusCode.OK, recorded.StatusCode); var decision = (await recorded.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!;
                Assert.Equal("UNASSESSED", decision.Recording!.Qualification);
                var writer = settings.Memberships[0]; var now = DateTimeOffset.UtcNow;
                settings.Memberships[0] = writer with { ActorId = Guid.NewGuid(), Permissions = ["directory.read", "audit.read", "human.trace.read", "file.read", "evidence.read", "control.read", "regulatory.draft.read", "crew.read"], TenantWideManage = false, TenantWideRead = false,
                    VesselGrants = [new(SeedVessel.Id)], HumanAuthorities = [], HumanTraceReaders = [new(Guid.NewGuid(), SeedVessel.Id, msmd.Id, control.Id, ReviewRole, ["Verification", "Decision"], [decision.Recording.Content.DecisionType], now.AddHours(-1), now.AddHours(1))] };
                await Token(c); var trace = await c.GetFromJsonAsync<JsonElement>(TraceRoute(msmd, currentControl), Ct);
                Assert.Contains(trace.GetProperty("auditItems").EnumerateArray(), e => e.GetProperty("id").GetGuid() == decision.Id);
                Assert.Empty(trace.GetProperty("items").EnumerateArray());
                Assert.DoesNotContain("FICTIONAL private rationale", trace.GetRawText());
                var reader = settings.Memberships[0];
                settings.Memberships[0] = reader with { Permissions = reader.Permissions.Where(p => p != "regulatory.draft.read").ToArray() };
                Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(TraceRoute(msmd, currentControl), Ct)).StatusCode);
                settings.Memberships[0] = reader with { HumanTraceReaders = [] };
                Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(TraceRoute(msmd, currentControl), Ct)).StatusCode);
                settings.Memberships[0] = writer; await Token(c);
            }
            // Current verifier appointment withdrawal invalidates current truth;
            // immutable outcome bytes and source trace remain historical.
            if (change == "pack-revision") {
                await CapacitySave(c, $"evidence/{pack.Id}", GraphCommand(content: pack.Content with { ClassPack = pack.Content.ClassPack! with { Edition = "FICTIONAL replacement edition" } }) with { ExpectedVersion = pack.Version, Requirements = pack.Requirements, File = pack.File, ReasonCode = "AUTHORING_CORRECTION" }, update: true, vessel: SeedVessel.Id);
            } else if (change == "source-expiry") settings.Memberships = settings.Memberships.Select(m => m with { RegulatoryAuthorities = m.RegulatoryAuthorities.Select(g => g with { ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1) }).ToArray() }).ToArray();
            else if (change == "object-integrity") {
                await using var scope = host.Services.CreateAsyncScope();
                StoredFile file; await using (var read = Db()) file = await read.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == pack.File!.Id, Ct);
                string path; await using (var bytes = await scope.ServiceProvider.GetRequiredService<FileObjects>().OpenAsync(file, Ct)) path = ((FileStream)bytes).Name;
                await File.WriteAllBytesAsync(path, [1, 2, 3], Ct);
            } else settings.Memberships = settings.Memberships.Select(m => m with { HumanAuthorities = [] }).ToArray();
            var current = await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct);
            if (verify) Assert.False(current.GetProperty("current").GetBoolean());
            Assert.Equal(result.SnapshotHash, (await c.GetFromJsonAsync<EvaluationDto>($"/api/v1/applicability/evaluations/{result.Id}", Ct))!.SnapshotHash);
            await using var db = Db(); Assert.Equal(2, await db.EvaluationOwningReferences.CountAsync(p => p.EvaluationId == result.Id, Ct));
        } finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Protected_assessment_denies_before_capture_history_replay_and_counts() {
        await using var host = Host(tugRead: true, tugWrite: true, technicalRead: true); using var c = Client(host); CapacityRights(host.Services); await Token(c);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "crew.identity.read", "crew.profile.medical.read", "crew.assessment.read", "crew.documents.read"] };
        var command = GraphCommand(content: new(Title: "FICTIONAL incomplete protected assessment", CrewRoleAssessment: new())); var key = Guid.NewGuid().ToString();
        using var created = await Send(c, GraphRoute("evidence", Tug.Id), command, key); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var row = (await created.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "crew.identity.read").ToArray() };
        using var replay = await Send(c, GraphRoute("evidence", Tug.Id), command, key); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/evidence/{row.Id}/versions?vesselId={Tug.Id}", Ct)).StatusCode);
        using var evaluate = await Send(c, "applicability/evaluate", new EvaluateCommand(Tug.Id, [Guid.NewGuid()], OwningInputs: new(RoleAssessmentIds: [row.Id])));
        Assert.Equal(HttpStatusCode.Forbidden, evaluate.StatusCode);
    }
    [Fact]
    public async Task Upstream_ism_is_a_same_vessel_exact_rule_evaluation_and_source_change_withdraws_it() {
        await using var host = Host(); using var c = Client(host); await Token(c);
        var (upstreamRule, _, upstream) = await AdmittedByCommands(c, host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var retained = settings.Memberships.ToArray();
        var (consumerRule, _, _) = await AdmittedByCommands(c, host.Services, qualification: q => q with {
            Inputs = [..q.Inputs!, new("regulatory.ismApplicable", "HumanAttested")],
            IsmDependency = new(upstreamRule.Current.Id, upstreamRule.Current.Revision, "FICTIONAL/upstream-ISM-source") });
        settings.Memberships = [..settings.Memberships, ..retained.Where(m => !settings.Memberships.Any(n => n.ActorId == m.ActorId)).Select(m => m with { Subject = "fictional-retained-ism-" + Guid.NewGuid().ToString("N") })];
        CapacityRights(host.Services); await Token(c);
        var command = new EvaluateCommand(SeedVessel.Id, [consumerRule.Current.Id], OwningInputs: new(Ism: new(upstream.Id, upstreamRule.Current.Id, upstreamRule.Current.Revision)));
        using var response = await Send(c, "applicability/evaluate", command); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        var fact = result.Snapshot.GetProperty("qualifications")[0].GetProperty("facts").GetProperty("regulatory.ismApplicable");
        Assert.Equal("Known", fact.GetProperty("knowledge").GetString()); Assert.True(fact.GetProperty("value").GetBoolean());
        var source = (await c.GetFromJsonAsync<RegisterDto>($"/api/v1/source-drafts/{upstreamRule.Current.SourceId}", Ct))!;
        using var changed = await Send(c, $"source-drafts/{source.Current.Id}", new RegisterCommand(source.Version, source.Current.Content), update: true); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.False((await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct)).GetProperty("current").GetBoolean());
        using var wrongVessel = await Send(c, "applicability/evaluate", command with { VesselId = Tug.Id }); Assert.Equal(HttpStatusCode.Forbidden, wrongVessel.StatusCode);
        await using var db = Db(); Assert.Equal(upstream.Id, (await db.EvaluationUpstreamReferences.SingleAsync(r => r.EvaluationId == result.Id, Ct)).UpstreamId);
    }
    [Fact]
    public async Task Compliance_draft_duplicate_noop_and_receipt_failure_are_atomic() {
        var fault = new RejectGraphReceipt(); await using var host = Host(interceptor: fault, tugRead: true, tugWrite: true, technicalRead: true); using var c = Client(host); CapacityRights(host.Services); await Token(c);
        var command = GraphCommand(content: new(Title: "FICTIONAL class draft", ClassPack: new())); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Send(c, GraphRoute("evidence", Tug.Id), command, key), Send(c, GraphRoute("evidence", Tug.Id), command, key));
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var first = (await responses.First(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!; foreach (var r in responses) r.Dispose();
        var noop = await CapacitySave(c, $"evidence/{first.Id}", command with { ExpectedVersion = first.Version, ReasonCode = "AUTHORING_CORRECTION" }, update: true);
        Assert.NotEqual(first.Version, noop.Version); Assert.Equal(first.Revision + 1, noop.Revision);
        using var replay = await Send(c, GraphRoute("evidence", Tug.Id), command, key); Assert.Equal(first.Version, (await replay.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!.Version);
        fault.Armed = true;
        using var failure = await Send(c, GraphRoute($"evidence/{first.Id}", Tug.Id), command with { ExpectedVersion = noop.Version, ReasonCode = "AUTHORING_CORRECTION" }, update: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
        await using var db = Db(); Assert.Equal(noop.Revision, (await db.ControlEvidenceHeads.SingleAsync(h => h.Id == first.Id, Ct)).Revision);
        Assert.Equal(2, await db.OperationEquipmentBindings.CountAsync(b => b.Id == first.Id, Ct));
    }

    [Theory]
    [InlineData(true, true, "none")] [InlineData(false, true, "none")] [InlineData(true, false, "none")]
    [InlineData(true, true, "current")] [InlineData(false, true, "current")]
    [InlineData(true, true, "credential-noop")] [InlineData(true, true, "document-policy-revoked")]
    [InlineData(true, true, "tug-current")] [InlineData(false, true, "tug-current")]
    [InlineData(true, true, "tug-count-shortage")] [InlineData(true, true, "tug-endorsement-unknown")]
    [InlineData(true, true, "tug-endorsement-expired")] [InlineData(true, true, "tug-private-policy")]
    public async Task Command_qualified_role_criteria_pin_assignment_position_period_msmd_and_withdraw_after_noop(bool satisfied, bool verify, string credentialCase) {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-role-input-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var host = PersonFileHost(root, scanner.Port).WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(new CrewAssignmentPolicy {
                Version = "FICTIONAL-role-assignment-v1", TimeZone = "UTC", EffectiveStatuses = ["FICTIONAL-onboard"], PlannedStatuses = ["FICTIONAL-planned"], Provenances = ["UserDeclaredUnverified"] })));
            using var c = Client(host); await Token(c);
            var tugComposition = credentialCase.StartsWith("tug-", StringComparison.Ordinal);
            if (tugComposition) {
                // A fictional fixture vessel is classified before any source/evaluation
                // capture. All subsequent proof is created by ordinary HTTP commands.
                await using var setup = Db(); var fixtureVessel = await setup.Vessels.SingleAsync(v => v.Id == SeedVessel.Id, Ct);
                fixtureVessel.Kind = VesselKind.Tug;
                // The generic seed keeps another unqualified person in this
                // position with an open period. Isolate this one-person scenario
                // by declaring only that unrelated fixture outside the job
                // window through audited setup; production still counts every
                // overlapping unknown candidate and no seed proof is invented.
                var unrelated = await setup.CrewAssignments.SingleAsync(a => a.Id == SeedVessel.Id &&
                    a.VesselId == SeedVessel.Id && a.PositionId == SeedVessel.Id, Ct);
                unrelated.From = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
                await setup.SaveChangesAsync(Ct);
            }
            // Admission rebuilds synthetic graph permissions; pass the actual
            // Tug evaluation scope so its helper provisions mode rights after
            // that reset, for both the producer and consumer baselines.
            var (producer, _, baseline) = await AdmittedByCommands(c, host.Services, qualification: CompliancePolicy, evaluationVessel: tugComposition ? SeedVessel.Id : null);
            var settings = host.Services.GetRequiredService<FederationSettings>(); CapacityRights(host.Services);
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "crew.write", "crew.assignment.declare", "crew.identity.write", "crew.profile.identity.read", "crew.profile.identity.write", "crew.identity.read", "crew.profile.medical.read", "crew.assessment.read", "crew.documents.read", "crew.documents.write", "crew.certificate.write", "file.read", "file.write"] }; await Token(c);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var declaration = NewCrew() with { From = today, To = today.AddDays(tugComposition ? 2 : 1), PositionId = SeedVessel.Id, PositionPolicyVersion = "synthetic-position-v1", DeclaredStatus = "FICTIONAL-onboard", AssignmentPolicyVersion = "FICTIONAL-role-assignment-v1", SourceObservedAt = DateTimeOffset.UtcNow.AddSeconds(-1) };
            using var saved = await Send(c, CrewUrl, declaration); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            CrewAssignment assignment; CrewPosition position;
            await using (var db = Db()) { assignment = await db.CrewAssignments.AsNoTracking().SingleAsync(a => a.Id == declaration.Id, Ct); position = await db.CrewPositions.AsNoTracking().SingleAsync(p => p.Id == SeedVessel.Id, Ct); }
            CredentialInputReference? credential = null; CredentialSaveCommand? credentialCommand = null;
            var certificatePath = $"{CrewUrl}/{assignment.Id}/certificates";
            if (credentialCase != "none") {
                // A real person-bound ordinary upload/scan/store + credential command, not a DB-only verified fixture.
                // The scanner protocol peer proves software behavior only, not production scanner trust.
                var filePath = $"{CrewUrl}/{assignment.Id}/personal-files/CertificateDocument/versions/1";
                using var staged = await Send(c, filePath, PersonalPng("CertificateDocument")); Assert.Equal(HttpStatusCode.OK, staged.StatusCode);
                var file = (await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;
                using var uploaded = await PutPersonalPng(c, filePath, file.Id); Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
                credentialCommand = new(Guid.NewGuid(), null, new(new("FICTIONAL source-assessed type", "FICTIONAL", "FICTIONAL issuer; no real trust", today.AddDays(-1), today.AddDays(2), null), EndorsementRequired: false, DocumentReference: file.Id));
                if (credentialCase == "tug-endorsement-unknown") credentialCommand = credentialCommand with { Metadata = credentialCommand.Metadata with { EndorsementRequired = null } };
                if (credentialCase == "tug-endorsement-expired") credentialCommand = credentialCommand with { Metadata = credentialCommand.Metadata with {
                    EndorsementRequired = true, EndorsementNumber = "FICTIONAL expired endorsement", EndorsementExpiry = today.AddDays(-1) } };
                using var recorded = await Send(c, certificatePath, credentialCommand); Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
                var savedCredential = (await recorded.Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
                credential = new(savedCredential.Id, savedCredential.Revision, savedCredential.Version);
            }
            var pin = new RequirementPin(producer.Current.RequirementId!.Value, producer.Current.RequirementRevision!.Value, producer.Current.SourceId!.Value, producer.Current.SourceRevision!.Value, producer.Current.Id, producer.Current.Revision);
            var control = await CapacitySave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL source role control", OwnerId: settings.Memberships[0].ActorId, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] }, vessel: SeedVessel.Id);
            var msmd = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(Msmd: new("FICTIONAL issued record", "FICTIONAL issuer", [new(position.Id, credentialCase == "tug-count-shortage" ? 2 : 1, "FICTIONAL/msmd/role")])), vessel: SeedVessel.Id);
            var assessment = await OwningDocument(c, host.Services, control, pin, baseline.Id, new(CrewRoleAssessment: new(assignment.Id, assignment.Revision, assignment.Version, position.Id, position.Version, today, declaration.To, "FICTIONAL-CRITERION", "FICTIONAL independently qualified assessment record", satisfied, credential)), verify, SeedVessel.Id);
            var retained = settings.Memberships.ToArray();
            var (consumer, _, _) = await AdmittedByCommands(c, host.Services, qualification: q => CompliancePolicy(q) with { Eligibility = new(position.Id, "FICTIONAL/role/policy", [new("FICTIONAL-CRITERION", producer.Current.Id, producer.Current.Revision, "FICTIONAL/criterion/source", credentialCase != "none")]) }, evaluationVessel: tugComposition ? SeedVessel.Id : null);
            settings.Memberships = [..settings.Memberships, ..retained.Where(m => !settings.Memberships.Any(n => n.ActorId == m.ActorId)).Select(m => m with { Subject = "fictional-retained-role-" + Guid.NewGuid().ToString("N") })];
            CapacityRights(host.Services); settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "crew.identity.read", "crew.profile.medical.read", "crew.assessment.read", "crew.documents.read", "crew.write", "crew.assignment.declare", "crew.profile.identity.read", "crew.profile.identity.write", "crew.identity.write"] }; await Token(c);
            using var evaluated = await Send(c, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [consumer.Current.Id], OwningInputs: new(MsmdId: msmd.Id, RoleAssessmentIds: [assessment.Id])));
            Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode); var result = (await evaluated.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
            var fact = result.Snapshot.GetProperty("qualifications")[0].GetProperty("facts").GetProperty("crew.requiredRoleEligible");
            var expectedKnown = verify && credentialCase is not ("tug-endorsement-unknown" or "tug-endorsement-expired");
            Assert.Equal(expectedKnown ? "Known" : "Unknown", fact.GetProperty("knowledge").GetString());
            if (expectedKnown) Assert.Equal(satisfied && credentialCase != "tug-count-shortage", fact.GetProperty("value").GetBoolean());
            if (tugComposition) await CheckQualifiedTugCrewComposition(c, host.Services, satisfied, credentialCase, msmd.Id);
            if (credentialCase == "credential-noop") {
                settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..settings.Memberships[0].Permissions, "crew.certificate.write", "crew.documents.write"] }; await Token(c);
                using var changedCredential = await Send(c, certificatePath, credentialCommand! with { BaseVersion = credential!.Version });
                Assert.Equal(HttpStatusCode.OK, changedCredential.StatusCode);
                Assert.False((await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct)).GetProperty("current").GetBoolean());
            }
            if (credentialCase == "document-policy-revoked") {
                host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["CrewFilePolicy:Version"] = "FICTIONAL-revoked-document-policy";
                Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/applicability/evaluations/{result.Id}", Ct)).StatusCode);
                // Restore the test's original explicit policy to continue unchanged assignment/revocation assertions.
                host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["CrewFilePolicy:Version"] = "synthetic-person-files-v1";
            }
            using var changed = await Send(c, CrewUrl, declaration with { PersonId = assignment.PersonId, BaseVersion = assignment.Version }); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.False((await c.GetFromJsonAsync<JsonElement>($"/api/v1/applicability/evaluations/{result.Id}/current", Ct)).GetProperty("current").GetBoolean());
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "crew.assessment.read").ToArray() };
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/applicability/evaluations/{result.Id}", Ct)).StatusCode);
        } finally { Directory.Delete(root, true); }
    }

}
