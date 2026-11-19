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

// S3-WPA / DEV-026 / T-S3-001 (18.01 Backlog row 27). Real host + owned PostgreSQL.
// Qualification rows below are FICTIONAL per-test inserts in an isolated database, not seed,
// not human authority and not legal qualification. Execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private async Task QualifyFictional(Guid rule, long ruleRevision, string state)
    {
        await using var db = Db(); var tenant = seed.Tenants[0].Id;
        var head = await db.RegulatoryQualificationHeads.SingleOrDefaultAsync(x => x.RuleId == rule, Ct);
        if (head == null) { head = new() { TenantId = tenant, RuleId = rule }; db.RegulatoryQualificationHeads.Add(head); }
        head.Revision++;
        var json = JsonSerializer.Serialize(new QualificationContent(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.RegulatoryQualificationEvents.Add(new() { TenantId = tenant, RuleId = rule, Revision = head.Revision, RuleRevision = ruleRevision, State = state,
            ContentJson = json, SourceHash = "", Hash = RegisterStorage.Hash($"FICTIONAL-{rule}-{head.Revision}-{state}"), AuthorId = Guid.Empty, ActorId = Guid.Empty,
            AuthorityId = Guid.Empty, RoleId = Guid.Empty, AuthorityHash = "", ContractJson = "null", PolicyVersion = RegulatoryQualificationPolicy.Version, SavedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
    }
    private async Task<RuleDraftDto> FictionalBoundRule(HttpClient client)
    {
        using var created = await Send(client, "rules", Author()); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var rule = (await created.Content.ReadFromJsonAsync<RuleDraftDto>(Ct))!;
        Assert.Equal(RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id), rule.Current.RequirementId); Assert.Equal(1, rule.Current.RequirementRevision);
        return rule;
    }

    // WPA-026-A1: each recorded qualification head maps to exactly one requirement lifecycle term.
    [Theory]
    [InlineData("DRAFT", "DRAFT")]
    [InlineData("IN_REVIEW", "DRAFT")]
    [InlineData("ADOPTED", "ADOPTED")]
    [InlineData("AWAITING", "ADOPTED")]
    [InlineData("FUTURE_EFFECTIVE", "FUTURE")]
    [InlineData("IN_FORCE", "IN_FORCE")]
    [InlineData("SUPERSEDED", "SUPERSEDED")]
    public async Task Requirement_lifecycle_is_derived_from_the_current_qualification_head_and_never_stored(string state, string lifecycle)
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
        var initial = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal("DRAFT", initial.Lifecycle); Assert.Equal("NO_QUALIFIED_RULE", initial.LifecycleBasis); Assert.Empty(initial.Qualifications);
        var rule = await FictionalBoundRule(client);
        uint version; string content;
        await using (var db = Db()) { var d = await db.RegisterDrafts.SingleAsync(x => x.Id == requirement, Ct); version = d.Version; content = d.ContentJson; }
        await QualifyFictional(rule.Current.Id, rule.Current.Revision, state);
        using var response = await client.GetAsync($"/api/v1/requirements/{requirement}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var read = (await response.Content.ReadFromJsonAsync<RequirementDto>(Ct))!;
        Assert.Equal(lifecycle, read.Lifecycle); Assert.Equal("RECORDED_QUALIFICATION", read.LifecycleBasis);
        var q = Assert.Single(read.Qualifications);
        Assert.Equal((rule.Current.Id, rule.Current.Revision, state, lifecycle, true, 1L), (q.RuleId, q.RuleRevision, q.State, q.Lifecycle, q.Current, q.RequirementRevision));
        var page = (await client.GetFromJsonAsync<PageDto<RequirementDto>>("/api/v1/requirements?limit=100", Ct))!;
        Assert.Equal(lifecycle, Assert.Single(page.Items, x => x.Id == requirement).Lifecycle);
        // A read projection: the register row, its xmin and its content are untouched.
        await using (var db = Db()) {
            var d = await db.RegisterDrafts.SingleAsync(x => x.Id == requirement, Ct);
            Assert.Equal(version, d.Version); Assert.Equal(content, d.ContentJson);
            var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
            var audit = await db.Set<SecurityEvent>().SingleAsync(x => x.CorrelationId == correlation, Ct);
            Assert.Equal(("RequirementRead", "Success", requirement, actorId.ToString()), (audit.Action, audit.Result, audit.ResourceId!.Value, audit.ActorId));
            Assert.Contains(await db.Set<SecurityEvent>().Where(x => x.Action == "RequirementList").Select(x => x.Result).ToArrayAsync(Ct), x => x == "Success");
        }
    }

    // WPA-026-B1: a new requirement revision never silently overwrites the in-force one.
    [Fact]
    public async Task New_requirement_revision_starts_as_draft_while_prior_in_force_revision_stays_readable()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
        var rule = await FictionalBoundRule(client);
        await QualifyFictional(rule.Current.Id, rule.Current.Revision, "IN_FORCE");
        var before = (await client.GetFromJsonAsync<RegisterDto>($"/api/v1/requirement-drafts/{requirement}", Ct))!;
        var original = before.Current.Content;
        var edited = await UpdateRegister(client, before, "requirement-drafts", original with { Amendment = new("Known", "FICTIONAL amendment 2") });
        Assert.Equal(before.Current.Revision + 1, edited.Current.Revision);
        var read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal(edited.Current.Revision, read.Revision); Assert.Equal("DRAFT", read.Lifecycle); Assert.Equal("NO_QUALIFIED_RULE", read.LifecycleBasis);
        var prior = Assert.Single(read.Qualifications);
        Assert.Equal(("IN_FORCE", "IN_FORCE", false, before.Current.Revision), (prior.State, prior.Lifecycle, prior.Current, prior.RequirementRevision));
        var history = (await client.GetFromJsonAsync<PageDto<RegisterRevisionDto>>($"/api/v1/requirement-drafts/{requirement}/history", Ct))!;
        var kept = Assert.Single(history.Items, x => x.Revision == before.Current.Revision);
        Assert.Equal(original, kept.Content); Assert.Equal(before.Current.Hash, kept.Hash);
    }

    // WPA-026-B2: a stale or concurrent edit is a 409, never a last-write-wins overwrite.
    [Fact]
    public async Task Stale_and_concurrent_requirement_edits_conflict_without_overwriting_the_current_revision()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        var source = await CreateRegister(client, content: new() { Title = new("Known", "FICTIONAL source") });
        var created = await CreateRegister(client, "requirement-drafts", new() { Instrument = new("Known", "FICTIONAL A") }, source.Current.Id, source.Current.Revision);
        var winner = await UpdateRegister(client, created, "requirement-drafts", created.Current.Content with { Instrument = new("Known", "FICTIONAL B") });
        using var stale = await Send(client, $"requirement-drafts/{created.Current.Id}",
            new RegisterCommand(created.Version, created.Current.Content with { Instrument = new("Known", "FICTIONAL C") }, source.Current.Id, source.Current.Revision), update: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("version_conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        var racers = await Task.WhenAll(new[] { "FICTIONAL D", "FICTIONAL E" }.Select(text => Send(client, $"requirement-drafts/{created.Current.Id}",
            new RegisterCommand(winner.Version, winner.Current.Content with { Instrument = new("Known", text) }, source.Current.Id, source.Current.Revision), update: true)));
        try {
            Assert.Single(racers, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(racers, r => r.StatusCode == HttpStatusCode.Conflict);
        } finally { foreach (var r in racers) r.Dispose(); }
        await using var db = Db();
        var revisions = await db.RegisterRevisions.Where(x => x.Id == created.Current.Id).OrderBy(x => x.Revision).ToArrayAsync(Ct);
        Assert.Equal(new long[] { 1, 2, 3 }, revisions.Select(x => x.Revision));
        Assert.DoesNotContain(revisions, x => x.ContentJson.Contains("FICTIONAL C"));
        Assert.Contains("FICTIONAL A", revisions[0].ContentJson); Assert.Contains("FICTIONAL B", revisions[1].ContentJson);
        var read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{created.Current.Id}", Ct))!;
        Assert.Equal(3, read.Revision); Assert.Equal("DRAFT", read.Lifecycle);
    }

    // WPA-026-A2: tenant isolation, explicit capability, audited invalid queries, JSON error code.
    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?offset=-1")]
    [InlineData("?unsupported=1")]
    [InlineData("?q=a&q=b")]
    [InlineData("?q=%00")]
    public async Task Requirements_reject_invalid_queries_with_audit_and_hide_other_tenants(string query)
    {
        await using var host = Host(regulatoryRead: true); using var client = Client(host); await Token(client);
        using var invalid = await client.GetAsync("/api/v1/requirements" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid_query", (await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        using var detailQuery = await client.GetAsync($"/api/v1/requirements/{RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id)}?limit=1", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, detailQuery.StatusCode);
        var foreign = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[1].Id);
        using var hidden = await client.GetAsync($"/api/v1/requirements/{foreign}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal("resource_not_found", (await hidden.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        var page = (await client.GetFromJsonAsync<PageDto<RequirementDto>>("/api/v1/requirements?limit=100", Ct))!;
        Assert.DoesNotContain(page.Items, x => x.Id == foreign);
        await using (var db = Db()) {
            var correlation = Assert.Single(invalid.Headers.GetValues("X-Correlation-ID"));
            Assert.Equal("Invalid", (await db.Set<SecurityEvent>().SingleAsync(x => x.CorrelationId == correlation && x.Action == "RequirementList", Ct)).Result);
            var notFound = Assert.Single(hidden.Headers.GetValues("X-Correlation-ID"));
            Assert.Equal("NotFound", (await db.Set<SecurityEvent>().SingleAsync(x => x.CorrelationId == notFound && x.Action == "RequirementRead", Ct)).Result);
        }
        await using var deniedHost = Host(regulatoryRead: false); using var denied = Client(deniedHost); await Token(denied);
        using var forbidden = await denied.GetAsync("/api/v1/requirements", Ct); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var anonymous = Client(host, false); using var unauthenticated = await anonymous.GetAsync("/api/v1/requirements", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    // S3 gate (non-blocking WPA finding): mixed rule states keep the derived value but publish
    // the MIXED review basis; the multi-rule aggregation has no source and is an open expert decision.
    [Fact]
    public async Task Mixed_bound_rule_states_publish_the_mixed_review_basis_alongside_the_derived_value()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
        var first = await FictionalBoundRule(client); var second = await FictionalBoundRule(client);
        await QualifyFictional(first.Current.Id, first.Current.Revision, "IN_FORCE");
        await QualifyFictional(second.Current.Id, second.Current.Revision, "DRAFT");
        var read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal(("IN_FORCE", "MIXED_QUALIFICATION_STATES"), (read.Lifecycle, read.LifecycleBasis));
        Assert.Equal(2, read.Qualifications.Count(x => x.Current));
        await QualifyFictional(second.Current.Id, second.Current.Revision, "IN_FORCE");
        read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal(("IN_FORCE", "RECORDED_QUALIFICATION"), (read.Lifecycle, read.LifecycleBasis));
    }

    // S3 gate (non-blocking WPA finding): RequirementEndpoints previously took a global
    // Take(ids.Length*100), which could silently drop bound rules. Over the per-requirement
    // limit the projection is explicitly UNAVAILABLE/QUALIFICATION_LIMIT_EXCEEDED, never partial.
    [Fact]
    public async Task More_bound_rules_than_the_projection_limit_are_reported_unavailable_instead_of_truncated()
    {
        await using var host = ApplicabilityHost(); using var client = Client(host); await Token(client);
        var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
        var rules = new List<RuleDraftDto>();
        for (var i = 0; i < 101; i++) rules.Add(await FictionalBoundRule(client));
        foreach (var rule in rules.Take(100)) await QualifyFictional(rule.Current.Id, rule.Current.Revision, "IN_FORCE");
        // Exactly at the limit every bound rule is carried and the value is derived.
        var read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal(("IN_FORCE", "RECORDED_QUALIFICATION"), (read.Lifecycle, read.LifecycleBasis));
        Assert.Equal(100, read.Qualifications.Length);
        await QualifyFictional(rules[^1].Current.Id, rules[^1].Current.Revision, "IN_FORCE");
        read = (await client.GetFromJsonAsync<RequirementDto>($"/api/v1/requirements/{requirement}", Ct))!;
        Assert.Equal(("UNAVAILABLE", "QUALIFICATION_LIMIT_EXCEEDED"), (read.Lifecycle, read.LifecycleBasis));
        Assert.Empty(read.Qualifications);
        var listed = Assert.Single((await client.GetFromJsonAsync<PageDto<RequirementDto>>("/api/v1/requirements?limit=100", Ct))!.Items, x => x.Id == requirement);
        Assert.Equal(("UNAVAILABLE", "QUALIFICATION_LIMIT_EXCEEDED"), (listed.Lifecycle, listed.LifecycleBasis));
    }
}
