using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    // POL-DEFAULT-01: exercise actual JSON absence rather than serializing a DTO
    // that always supplies both flags. All actors/contracts here are synthetic.
    [Theory]
    [InlineData("requireFile", "omitted")]
    [InlineData("requireAcknowledgment", "omitted")]
    [InlineData("requireFile", "null")]
    [InlineData("requireAcknowledgment", "null")]
    [InlineData("requireFile", "false")]
    [InlineData("requireAcknowledgment", "false")]
    [InlineData("requireFile", "true")]
    [InlineData("requireAcknowledgment", "true")]
    public async Task Qualification_raw_choices_preserve_unknown_drafts_and_gate_review_and_adoption(string field, string wire)
    {
        await using var host = ApplicabilityHost(); using var c = Client(host); await Token(c);
        var (rule, source, _) = await AdmissionInputs(c);
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, actorId);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var grant = settings.Memberships[0].RegulatoryAuthorities[0];
        var content = new QualificationContent(Convert.ToBase64String("FICTIONAL raw-wire source"u8.ToArray()),
            "FICTIONAL publisher", "1", actorId, ReviewRole, "FICTIONAL review", [],
            new(ReviewRole, "FICTIONAL frequency", "FICTIONAL evidence", "FICTIONAL verifier", 24, false, false, "synthetic/policy", ReviewRole));
        var body = JsonSerializer.SerializeToNode(new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", content),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var policy = body["content"]!["control"]!.AsObject();
        if (wire == "omitted") Assert.True(policy.Remove(field));
        else policy[field] = wire == "null" ? null : JsonValue.Create(wire == "true");
        var raw = body.ToJsonString(); var key = Guid.NewGuid().ToString();
        async Task<HttpResponseMessage> RawSave()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/regulatory-source-governance/{rule.Current.Id}/events") {
                Content = new StringContent(raw, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", key);
            return await c.SendAsync(request, Ct);
        }
        using var saved = await RawSave(); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var bytes = await saved.Content.ReadAsStringAsync(Ct);
        var draft = JsonSerializer.Deserialize<QualificationDto>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        bool? expected = wire is "omitted" or "null" ? null : wire == "true";
        Assert.Equal(expected, field == "requireFile" ? draft.Content.Control!.RequireFile : draft.Content.Control!.RequireAcknowledgment);
        using (var replay = await RawSave()) {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(bytes, await replay.Content.ReadAsStringAsync(Ct));
        }
        await using (var db = Db()) {
            var stored = Assert.Single(await db.RegulatoryQualificationEvents.Where(e => e.RuleId == rule.Current.Id).ToArrayAsync(Ct));
            using var json = JsonDocument.Parse(stored.ContentJson);
            var value = json.RootElement.GetProperty("control").GetProperty(field);
            Assert.Equal(expected == null ? JsonValueKind.Null : expected.Value ? JsonValueKind.True : JsonValueKind.False, value.ValueKind);
            Assert.Single(await db.RegulatoryQualificationReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        }
        using var reviewResponse = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events",
            new QualificationCommand(draft.Version, rule.Current.Revision, grant.Id, "IN_REVIEW"));
        if (expected == null) {
            Assert.Contains("CONTROL_POLICY_MISSING", draft.Blockers);
            Assert.Equal(HttpStatusCode.Conflict, reviewResponse.StatusCode);
            Assert.Contains("incomplete_qualification", await reviewResponse.Content.ReadAsStringAsync(Ct));
        } else Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = expected == null ? draft : (await reviewResponse.Content.ReadFromJsonAsync<QualificationDto>(Ct))!;
        settings.Memberships = [settings.Memberships[0], settings.Memberships[0] with { Subject = "synthetic-retained-wire-owner" }];
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, Guid.NewGuid()); await Token(c);
        grant = settings.Memberships[0].RegulatoryAuthorities[0];
        using var adopt = await Send(c, $"regulatory-source-governance/{rule.Current.Id}/events",
            new QualificationCommand(review.Version, rule.Current.Revision, grant.Id, "ADOPTED"));
        Assert.Equal(expected == null ? HttpStatusCode.Conflict : HttpStatusCode.OK, adopt.StatusCode);
        if (expected != null) {
            var adopted = (await adopt.Content.ReadFromJsonAsync<QualificationDto>(Ct))!;
            Assert.Equal(expected, field == "requireFile" ? adopted.Content.Control!.RequireFile : adopted.Content.Control!.RequireAcknowledgment);
            Assert.Empty(adopted.Blockers);
        }
        await using var final = Db();
        var history = await final.RegulatoryQualificationEvents.Where(e => e.RuleId == rule.Current.Id).OrderBy(e => e.Revision).ToArrayAsync(Ct);
        Assert.Equal(expected == null ? new[] { "DRAFT" } : new[] { "DRAFT", "IN_REVIEW", "ADOPTED" }, history.Select(e => e.State).ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Qualified_explicit_choices_control_real_evidence_submission_without_inventing_universal_requirements(bool file, bool acknowledgment)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var policy = new QualifiedControlPolicy(ReviewRole, "FICTIONAL frequency", "FICTIONAL evidence policy", "FICTIONAL independent review", 24,
            file, acknowledgment, "synthetic/policy", acknowledgment ? ReviewRole : null);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services, controlPolicy: policy);
        var m = host.Services.GetRequiredService<FederationSettings>().Memberships[0]; var now = DateTimeOffset.UtcNow;
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value,
            rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var command = GraphCommand(content: new(Title: "FICTIONAL declaration evidence", OwnerId: m.ActorId, OwnerRole: ReviewRole,
            Provenance: "FICTIONAL declaration", ObservedAt: now.AddMinutes(-1), ValidFrom: now.AddMinutes(-1),
            ValidUntil: now.AddHours(1), FreshUntil: now.AddHours(1))) with { Requirements = [pin] };
        var evidence = await GraphSave(c, "evidence", command);
        using var submitted = await Send(c, GraphRoute($"evidence/{evidence.Id}/submit"), command with { ExpectedVersion = evidence.Version, ReasonCode = "SUBMIT" });
        Assert.Equal(file || acknowledgment ? HttpStatusCode.Conflict : HttpStatusCode.OK, submitted.StatusCode);
        if (file || acknowledgment) {
            var problem = await submitted.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var blockers = problem.GetProperty("blockers").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Equal(file, blockers.Contains("CLEAN_FILE_MISSING"));
            Assert.Equal(acknowledgment, blockers.Contains("SOURCE_ACKNOWLEDGMENT_REQUIRED"));
            await using var db = Db();
            Assert.Single(await db.ControlEvidenceRevisions.Where(e => e.Kind == "Evidence" && e.Id == evidence.Id).ToArrayAsync(Ct));
        } else {
            var result = (await submitted.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
            Assert.Equal("SUBMITTED", result.Lifecycle); Assert.Equal("UNVERIFIED", result.Verification); Assert.Equal("UNKNOWN", result.Effectiveness);
        }
    }
}
