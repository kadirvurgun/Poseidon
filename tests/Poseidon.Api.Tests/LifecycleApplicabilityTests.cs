using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// Real host/owned PostgreSQL/CSRF tests. LA-03..10. DEFERRED; never run here.
public sealed partial class DirectoryWorkspaceTests
{
    private static JsonElement FictionalRule(int version = 1) => JsonSerializer.SerializeToElement(new {
        ruleId = "SYNTHETIC-NOT-A-LEGAL-RULE", version,
        source = new { sourceId = "UNVERIFIED-FIXTURE", locator = "synthetic/fixture", authorityLayer = "PRODUCT_CONTROL" },
        lifecycle = new { state = "DRAFT", effectiveFrom = "2000-01-01" }, scope = new { },
        when = new { field = "context.mode", @operator = "EXISTS" }, then = new { result = "APPLICABLE" }, unknownPolicy = "REVIEW_REQUIRED"
    });
    private RuleAuthorCommand Author(uint version = 0, int revision = 1) => new(version, ApplicabilityEngine.Contract, FictionalRule(revision),
        RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id), 1, RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id), 1,
        version == 0 ? "INITIAL_AUTHORING" : "AUTHORING_CORRECTION");
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ApplicabilityHost(bool write = true, bool evaluate = true, bool review = true) =>
        Host(regulatoryRead: true, regulatoryWrite: write, applicabilityRead: true, applicabilityEvaluate: evaluate, ruleWrite: write, ruleReview: review);
    [Fact]
    public async Task Explicit_capabilities_default_deny_even_for_directory_and_regulatory_writer()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        using var deniedRead = await client.GetAsync("/api/v1/rules", Ct); Assert.Equal(HttpStatusCode.Forbidden, deniedRead.StatusCode);
        using var deniedWrite = await Send(client, "rules", Author()); Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        using var deniedEvaluate = await Send(client, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [Guid.NewGuid()])); Assert.Equal(HttpStatusCode.Forbidden, deniedEvaluate.StatusCode);
        using var deniedWhy = await client.GetAsync($"/api/v1/applicability/evaluations/{Guid.NewGuid()}", Ct); Assert.Equal(HttpStatusCode.Forbidden, deniedWhy.StatusCode);
    }
    [Fact]
    public async Task Source_revision_hash_and_role_string_do_not_adopt_and_review_is_blocked_immutable()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        using var created = await Send(client, "rules", Author()); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        var command = new RuleReviewCommand(original.Version, "IN_FORCE", "REQUEST_REVIEW"); var key = Guid.NewGuid().ToString();
        using var requested = await Send(client, $"rules/{original.Current.Id}/review", command, key);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode); var after = (await requested.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        Assert.Equal("DRAFT", after.Current.Lifecycle); Assert.Equal("IN_FORCE", after.Current.RequestedLifecycle); Assert.Equal("BLOCKED_SOURCE_ADMISSION", after.Current.ReviewStatus);
        Assert.NotEqual(original.Version, after.Version); Assert.NotEqual(original.Current.Hash, after.Current.Hash);
        using var replay = await Send(client, $"rules/{original.Current.Id}/review", command, key); Assert.Equal(await requested.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        using var stale = await Send(client, $"rules/{original.Current.Id}", Author(original.Version, 3), update: true); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = Db(); Assert.Equal(2, await db.RuleRevisions.CountAsync(x => x.Id == original.Current.Id, Ct));
        Assert.Equal("RecordedControlledCode", (await db.AuditEvents.Where(x => x.EntityType == nameof(RuleRevision) && x.ResourceId == original.Current.Id).OrderByDescending(x => x.OccurredAt).FirstAsync(Ct)).ReasonStatus);
    }
    [Fact]
    public async Task Simulation_is_nonmutating_and_changed_profile_and_rule_produce_distinct_snapshots()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        using var created = await Send(client, "rules", Author()); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var r = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        var command = new EvaluateCommand(SeedVessel.Id, [r.Current.Id]); var key = Guid.NewGuid().ToString();
        using var first = await Send(client, "applicability/simulate", command, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var evaluation = (await first.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!;
        Assert.True(evaluation.Preview); Assert.False(evaluation.Authoritative); Assert.Equal("REVIEW_REQUIRED", evaluation.CurrentResult);
        Assert.Equal(RegisterStorage.Hash(evaluation.Snapshot.GetRawText()), evaluation.SnapshotHash);
        await using (var db = Db()) {
            var profile = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct);
            profile.Revision++; profile.ContentJson = RegulatoryDraftStorage.Serialize(new() { Gt = new("Known", "9007199254740992.0000000001"), Flag = new("Unknown") });
            profile.SavedAt = DateTimeOffset.UtcNow; db.RegulatoryDraftRevisions.Add(profile.Snapshot()); await db.SaveChangesAsync(Ct);
        }
        using var changed = await Send(client, $"rules/{r.Current.Id}", Author(r.Version, 2), update: true); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var historical = await client.GetAsync($"/api/v1/applicability/evaluations/{evaluation.Id}", Ct); Assert.Equal(HttpStatusCode.NotFound, historical.StatusCode);
        await using (var db = Db()) {
            Assert.False(await db.ApplicabilityEvaluations.AnyAsync(x => x.Id == evaluation.Id, Ct));
            Assert.False(await db.ApplicabilityReceipts.AnyAsync(x => x.Key == key, Ct));
        }
        using var newer = await Send(client, "applicability/simulate", command); Assert.Equal(HttpStatusCode.OK, newer.StatusCode);
        var next = (await newer.Content.ReadFromJsonAsync<EvaluationDto>(Ct))!; Assert.NotEqual(evaluation.SnapshotHash, next.SnapshotHash); Assert.NotEqual(evaluation.RuleSetHash, next.RuleSetHash);
        Assert.Equal(1, evaluation.Results.Single().Revision); Assert.Equal(2, next.Results.Single().Revision);
    }
    [Fact]
    public async Task Idempotent_retry_rechecks_live_capabilities_and_scope_before_disclosing_receipt()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString(); var command = Author();
        using var saved = await Send(client, "rules", command, key); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        await using var deniedHost = Host(regulatoryRead: true, regulatoryWrite: true, applicabilityRead: true, ruleWrite: false); using var denied = Client(deniedHost); await Token(denied);
        using var replay = await Send(denied, "rules", command, key); Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode); Assert.False(replay.Headers.Contains("Idempotency-Replayed"));
        await using var vesselOnly = Host(manage: false, read: false, regulatoryRead: true, applicabilityRead: true, applicabilityEvaluate: true); using var scoped = Client(vesselOnly); await Token(scoped);
        using var noCatalog = await scoped.GetAsync("/api/v1/rules", Ct); Assert.Equal(HttpStatusCode.Forbidden, noCatalog.StatusCode);
    }
    [Fact]
    public async Task Cross_tenant_source_and_rule_details_and_arbitrary_input_authority_are_denied()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        using var foreign = await Send(client, "rules", Author() with { SourceId = RegulatoryRegisterFixtures.SourceId(seed.Tenants[1].Id) }); Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        using var hidden = await client.GetAsync($"/api/v1/rules/{ApplicabilityFixtures.Id(seed.Tenants[1].Id)}/versions", Ct); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenRule = await Send(client, "applicability/simulate", new EvaluateCommand(SeedVessel.Id, [ApplicabilityFixtures.Id(seed.Tenants[1].Id)])); Assert.Equal(HttpStatusCode.NotFound, hiddenRule.StatusCode);
        using var hiddenVessel = await Send(client, "applicability/evaluate", new EvaluateCommand(seed.Vessels.First(x => x.TenantId == seed.Tenants[1].Id).Id, [ApplicabilityFixtures.Id(seed.Tenants[0].Id)])); Assert.Equal(HttpStatusCode.NotFound, hiddenVessel.StatusCode);
        using var spoof = await Send(client, "applicability/evaluate", new { vesselId = SeedVessel.Id, ruleIds = new[] { Guid.NewGuid() }, actorContext = new { admin = true }, vessel = new { flag = "TR", sourceVerified = true }, unknownPolicy = "NOT_APPLICABLE" }); Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
    }
    [Fact]
    public async Task Noop_saves_check_xmin_and_incomplete_drafts_cannot_evaluate()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var incomplete = Author() with { Rule = null, SourceId = null, SourceRevision = null, RequirementId = null, RequirementRevision = null };
        using var first = await Send(client, "rules", incomplete); Assert.Equal(HttpStatusCode.OK, first.StatusCode); var r = (await first.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        using var noop = await Send(client, $"rules/{r.Current.Id}", incomplete with { ExpectedVersion = r.Version }, update: true); Assert.Equal(HttpStatusCode.OK, noop.StatusCode);
        var after = (await noop.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!; Assert.NotEqual(r.Version, after.Version);
        using var conflict = await Send(client, $"rules/{r.Current.Id}", incomplete with { ExpectedVersion = r.Version }, update: true); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var evaluation = await Send(client, "applicability/simulate", new EvaluateCommand(SeedVessel.Id, [r.Current.Id])); Assert.Equal(HttpStatusCode.Conflict, evaluation.StatusCode);
    }
    [Fact]
    public async Task Receipt_failure_rolls_back_rule_history_and_audit_without_leaking_error_details()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        await using var before = Db(); var revisionCount = await before.RuleRevisions.CountAsync(Ct); var auditCount = await before.AuditEvents.CountAsync(x => x.EntityType == nameof(RuleRevision), Ct);
        await Sql("CREATE TRIGGER reject_applicability_receipt BEFORE INSERT ON core.\"ApplicabilityReceipts\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        try {
            using var failed = await Send(client, "rules", Author()); Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.DoesNotContain("Postgres", await failed.Content.ReadAsStringAsync(Ct));
            await using var db = Db(); Assert.Equal(revisionCount, await db.RuleRevisions.CountAsync(Ct)); Assert.Equal(auditCount, await db.AuditEvents.CountAsync(x => x.EntityType == nameof(RuleRevision), Ct));
        } finally { await Sql("DROP TRIGGER reject_applicability_receipt ON core.\"ApplicabilityReceipts\";"); }
    }
    [Fact]
    public async Task Authoritative_evaluate_is_configured_denied_and_never_creates_trace_or_activation_from_drafts()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        using var created = await Send(client, "rules", Author()); var rule = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        var key = Guid.NewGuid().ToString();
        using var evaluation = await Send(client, "applicability/evaluate", new EvaluateCommand(SeedVessel.Id, [rule.Current.Id]), key);
        Assert.Equal(HttpStatusCode.Conflict, evaluation.StatusCode); Assert.Contains("source_admission_pending", await evaluation.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Empty(await db.ApplicabilityEvaluations.ToArrayAsync(Ct)); Assert.Empty(await db.EvaluationRules.ToArrayAsync(Ct));
        Assert.False(await db.ApplicabilityReceipts.AnyAsync(x => x.Key == key, Ct));
    }
    [Fact]
    public async Task Concurrent_author_commands_have_one_committed_revision_and_replay_original_xmin()
    {
        await using var host = ApplicabilityHost(); using var a = Client(host); using var b = Client(host); await Token(a); await Token(b);
        using var created = await Send(a, "rules", Author()); var rule = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        var command = Author(rule.Version, 2); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Send(a, $"rules/{rule.Current.Id}", command, key, true), Send(b, $"rules/{rule.Current.Id}", command, key, true));
        try {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(await responses[0].Content.ReadAsStringAsync(Ct), await responses[1].Content.ReadAsStringAsync(Ct));
            var saved = (await responses[0].Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!; Assert.NotEqual(rule.Version, saved.Version);
            using var later = await Send(a, $"rules/{rule.Current.Id}", Author(saved.Version, 3), update: true); Assert.Equal(HttpStatusCode.OK, later.StatusCode);
            using var retry = await Send(a, $"rules/{rule.Current.Id}", command, key, true); Assert.Equal(await responses[0].Content.ReadAsStringAsync(Ct), await retry.Content.ReadAsStringAsync(Ct));
            using var reused = await Send(a, $"rules/{rule.Current.Id}", command with { ReasonCode = "INITIAL_AUTHORING" }, key, true); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
            await using var db = Db(); Assert.Equal(3, await db.RuleRevisions.CountAsync(x => x.Id == rule.Current.Id, Ct));
        } finally { foreach (var r in responses) r.Dispose(); }
    }
    [Fact]
    public async Task Exact_numeric_rule_lexeme_survives_request_immutable_storage_response_and_receipt()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var text = FictionalRule().GetRawText().Replace("{\"field\":\"context.mode\",\"operator\":\"EXISTS\"}", "{\"field\":\"vessel.gt\",\"operator\":\"GT\",\"value\":9007199254740992.0000000001}");
        using var document = JsonDocument.Parse(text); var key = Guid.NewGuid().ToString();
        var command = Author() with { Rule = document.RootElement.Clone() };
        using var saved = await Send(client, "rules", command, key); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var row = (await saved.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        Assert.Equal(text, row.Current.RuleJson); Assert.Equal("9007199254740992.0000000001", row.Current.Rule!.Value.GetProperty("when").GetProperty("value").GetRawText());
        using var replay = await Send(client, "rules", command, key); Assert.Equal(await saved.Content.ReadAsStringAsync(Ct), await replay.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.Equal(text, (await db.RuleRevisions.SingleAsync(x => x.Id == row.Current.Id, Ct)).RuleJson);
    }
    [Fact]
    public async Task Caller_publication_or_source_verification_claim_is_not_admitted_to_authoring()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var node = System.Text.Json.Nodes.JsonNode.Parse(FictionalRule().GetRawText())!;
        node["publicationStatus"] = "PUBLISHED"; node["sourceVerificationStatus"] = "VERIFIED";
        var key = Guid.NewGuid().ToString();
        using var response = await Send(client, "rules", Author() with { Rule = JsonSerializer.SerializeToElement(node) }, key);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Db(); Assert.False(await db.ApplicabilityReceipts.AnyAsync(x => x.Key == key, Ct));
    }
}
