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

// S3 WP-B: DEV-028 (18.01 Backlog row 29), DEV-029 (row 30), DEV-030 (row 31).
// Real host, antiforgery and owned PostgreSQL; synthetic appointments only. Authored; execution DEFERRED (no Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private string LifecycleRoute(Guid control) => GraphRoute($"controls/{control}/lifecycle");
    private async Task<ControlLifecycleDto> ReadLifecycle(HttpClient c, Guid control) =>
        (await c.GetFromJsonAsync<ControlLifecycleDto>("/api/v1/" + LifecycleRoute(control), Ct))!;
    private async Task<HttpResponseMessage> LifecycleCommand(HttpClient c, Guid control, string reason, string? key = null, ControlLifecycleDto? current = null)
    {
        current ??= await ReadLifecycle(c, control);
        return await Send(c, LifecycleRoute(control), new ControlLifecycleCommand(current.Version, current.Revision, reason, "FICTIONAL lifecycle reason; no official approval"), key);
    }
    private async Task<ControlLifecycleOutcome> Accepted(HttpResponseMessage response)
    {
        using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ControlLifecycleOutcome>(Ct))!; }
    }
    private async Task AssertCode(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response) { Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()); }
    }
    // Pending reviews touch the input heads' xmin; links must carry the live control version.
    private async Task<GraphPin[]> CurrentLinks(HttpClient c, ControlEvidenceDto evidence)
    {
        var controls = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls"), Ct)).GetProperty("items").EnumerateArray().ToArray();
        return evidence.Links.Select(l => l with { ExpectedVersion = controls.Single(x => x.GetProperty("id").GetGuid() == l.Id).GetProperty("version").GetUInt32() }).ToArray();
    }
    private void SwitchActor(IServiceProvider services, Guid actor, params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, Permissions = [..m.Permissions.Where(p => p != "control.approve"), ..extra] };
    }

    // DEV-028 AC: owner/frequency/evidence rule/verification rule/state and requirement links are recorded.
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task Control_lifecycle_requires_record_reviewer_independence_and_qualified_source_and_retired_is_terminal()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services);
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var author = host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;
        var command = GraphCommand(content: new(Title: "FICTIONAL lifecycle control", OwnerId: author, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency",
            EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] };
        var co = await GraphSave(c, "controls", command);
        var initial = await ReadLifecycle(c, co.Id);
        Assert.Equal("DRAFT", initial.State); Assert.Empty(initial.Events); Assert.Equal("UNKNOWN", initial.Effectiveness);
        Assert.True(initial.CanWrite); Assert.False(initial.CanReview);
        // A DRAFT authoring revision that has not passed the submission blockers cannot enter review.
        await AssertCode(await LifecycleCommand(c, co.Id, "SUBMIT_FOR_REVIEW"), HttpStatusCode.Conflict, "control_record_incomplete");
        using (var submitted = await Send(c, GraphRoute($"controls/{co.Id}/submit"), command with { ExpectedVersion = co.Version, ReasonCode = "SUBMIT" }))
            co = (await submitted.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        Assert.Equal("SUBMITTED", co.Lifecycle);
        // Authoring alone cannot review; reviewer-only reasons need control.approve.
        await AssertCode(await LifecycleCommand(c, co.Id, "APPROVE"), HttpStatusCode.Forbidden, "permission_denied");
        var key = Guid.NewGuid().ToString(); var beforeReview = await ReadLifecycle(c, co.Id);
        var review = await Accepted(await LifecycleCommand(c, co.Id, "SUBMIT_FOR_REVIEW", key, beforeReview));
        Assert.Equal("IN_REVIEW", review.State); Assert.Equal(co.Revision, review.Event.ControlRevision); Assert.Equal(author, review.Event.ActorId);
        // Exact retry replays the receipt; the same key with a different body conflicts.
        Assert.Equal(review.Version, (await Accepted(await LifecycleCommand(c, co.Id, "SUBMIT_FOR_REVIEW", key, beforeReview))).Version);
        await AssertCode(await Send(c, LifecycleRoute(co.Id), new ControlLifecycleCommand(beforeReview.Version, beforeReview.Revision, "SUBMIT_FOR_REVIEW", "FICTIONAL different reason"), key), HttpStatusCode.Conflict, "idempotency_key_reused");
        // Self-approval by the submitter/author is separation-of-duties, not success.
        SwitchActor(host.Services, author, "control.approve"); await Token(c);
        await AssertCode(await LifecycleCommand(c, co.Id, "APPROVE"), HttpStatusCode.Conflict, "separation_of_duties");
        // A stale editor token or revision never transitions.
        var current = await ReadLifecycle(c, co.Id);
        await AssertCode(await Send(c, LifecycleRoute(co.Id), new ControlLifecycleCommand(current.Version + 1, current.Revision, "APPROVE", "FICTIONAL stale")), HttpStatusCode.Conflict, "version_conflict");
        await AssertCode(await Send(c, LifecycleRoute(co.Id), new ControlLifecycleCommand(current.Version, current.Revision - 1, "APPROVE", "FICTIONAL stale")), HttpStatusCode.Conflict, "control_revision_conflict");
        var reviewer = Guid.NewGuid(); SwitchActor(host.Services, reviewer, "control.approve"); await Token(c);
        await AssertCode(await LifecycleCommand(c, co.Id, "PUBLISH"), HttpStatusCode.Conflict, "invalid_transition");
        var approved = await Accepted(await LifecycleCommand(c, co.Id, "APPROVE"));
        Assert.Equal("APPROVED", approved.State); Assert.Equal(reviewer, approved.Event.ActorId);
        var published = await Accepted(await LifecycleCommand(c, co.Id, "PUBLISH"));
        Assert.Equal("PUBLISHED", published.State); Assert.Equal("UNKNOWN", published.Effectiveness);
        // Publication is not completion, verification or effectiveness.
        var listed = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls"), Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == co.Id);
        Assert.Equal("UNVERIFIED", listed.GetProperty("verification").GetString()); Assert.Equal("UNKNOWN", listed.GetProperty("effectiveness").GetString());
        var retired = await Accepted(await LifecycleCommand(c, co.Id, "RETIRE"));
        Assert.Equal("RETIRED", retired.State);
        await AssertCode(await LifecycleCommand(c, co.Id, "PUBLISH"), HttpStatusCode.Conflict, "control_retired");
        SwitchActor(host.Services, author); await Token(c);
        var head = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls"), Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == co.Id);
        await AssertCode(await Send(c, GraphRoute($"controls/{co.Id}"), command with { ExpectedVersion = head.GetProperty("version").GetUInt32(), ReasonCode = "AUTHORING_CORRECTION" }, update: true), HttpStatusCode.Conflict, "control_retired");
        await AssertCode(await LifecycleCommand(c, co.Id, "SUBMIT_FOR_REVIEW"), HttpStatusCode.Conflict, "control_retired");
        var history = await ReadLifecycle(c, co.Id);
        Assert.Equal(new[] { "RETIRE", "PUBLISH", "APPROVE", "SUBMIT_FOR_REVIEW" }, history.Events.Select(e => e.ReasonCode).ToArray());
        Assert.All(history.Events, e => Assert.NotNull(e.Rationale)); // author/reviewer read
        await using var db = Db();
        Assert.Equal(4, await db.ControlStateEvents.CountAsync(x => x.ControlId == co.Id, Ct));
        Assert.Single(await db.ControlEvidenceReceipts.Where(x => x.Key == key).ToArrayAsync(Ct));
        var audits = await db.AuditEvents.Where(x => x.EntityType == nameof(ControlStateEvent) && x.ResourceId == co.Id).ToArrayAsync(Ct);
        Assert.Equal(4, audits.Length);
        Assert.All(audits, a => { Assert.Equal(SeedVessel.Id, a.VesselId); Assert.DoesNotContain("FICTIONAL lifecycle reason", a.ChangeSummary); });
        Assert.Contains(await db.AuditEvents.Where(x => x.EntityType == nameof(ControlEvidenceHead) && x.ResourceId == co.Id).Select(x => x.ChangeSummary).ToArrayAsync(Ct),
            s => s!.Contains("\"ControlState\"", StringComparison.Ordinal) && s.Contains("RETIRED", StringComparison.Ordinal));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ControlStateEvents\" SET \"ToState\" = 'DRAFT' WHERE \"ControlId\" = '{co.Id}'"));
    }

    // DEV-028: "prior approval is not current" after a new revision (62.01 paragraph:182, 199).
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task A_content_revision_returns_an_approved_control_to_draft_with_an_audited_event()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (rule, _, _) = await AdmittedByCommands(c, host.Services);
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value, rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var author = host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;
        var command = GraphCommand(content: new(Title: "FICTIONAL revised control", OwnerId: author, OwnerRole: ReviewRole, Frequency: "FICTIONAL frequency",
            EvidencePolicy: "FICTIONAL evidence policy", VerificationPolicy: "FICTIONAL independent review", Critical: true)) with { Requirements = [pin] };
        var co = await GraphSave(c, "controls", command);
        using (var submitted = await Send(c, GraphRoute($"controls/{co.Id}/submit"), command with { ExpectedVersion = co.Version, ReasonCode = "SUBMIT" }))
            co = (await submitted.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        await Accepted(await LifecycleCommand(c, co.Id, "SUBMIT_FOR_REVIEW"));
        SwitchActor(host.Services, Guid.NewGuid(), "control.approve"); await Token(c);
        await Accepted(await LifecycleCommand(c, co.Id, "APPROVE"));
        SwitchActor(host.Services, author); await Token(c);
        var approvedHead = await ReadLifecycle(c, co.Id);
        var corrected = await GraphSave(c, $"controls/{co.Id}", command with { ExpectedVersion = approvedHead.Version, ReasonCode = "AUTHORING_CORRECTION",
            Content = command.Content with { Frequency = "FICTIONAL revised frequency" } }, update: true);
        var after = await ReadLifecycle(c, co.Id);
        Assert.Equal("DRAFT", after.State); Assert.Equal(corrected.Revision, after.Revision);
        var reset = after.Events.First();
        Assert.Equal(("CONTENT_REVISED", "APPROVED", "DRAFT", corrected.Revision), (reset.ReasonCode, reset.FromState, reset.ToState, reset.ControlRevision));
        // The old approval stays immutable history bound to its own revision.
        Assert.Contains(after.Events, e => e.ReasonCode == "APPROVE" && e.ControlRevision == co.Revision);
        await using var db = Db();
        Assert.Single(await db.AuditEvents.Where(x => x.EntityType == nameof(ControlStateEvent) && x.ResourceId == co.Id && x.ChangeSummary!.Contains("CONTENT_REVISED")).ToArrayAsync(Ct));
    }

    // DEV-028 AC: "orphan critical control raporlanır" — missing requirement link or owner; UNKNOWN criticality stays visible.
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task Orphan_report_lists_critical_and_unknown_controls_without_link_or_owner_and_not_retired_or_noncritical()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var member = host.Services.GetRequiredService<FederationSettings>().Memberships[0]; var role = Guid.Parse(member.RoleReferences!.Single());
        var pin = new RequirementPin(RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id), 1, RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id), 1);
        var noLinkNoOwner = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL orphan", Critical: true)));
        var linkedNoOwner = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL ownerless", Critical: true)) with { Requirements = [pin] });
        var ownedNoLink = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL unlinked", Critical: true, OwnerId: member.ActorId, OwnerRole: role)));
        var complete = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL complete", Critical: true, OwnerId: member.ActorId, OwnerRole: role)) with { Requirements = [pin] });
        var unknown = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL unknown")));
        var nonCritical = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL routine", Critical: false)));
        var report = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls/orphans"), Ct);
        Assert.False(report.GetProperty("available").GetBoolean()); // still never an all-clear
        var findings = report.GetProperty("criticalOrphans").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetGuid(), x => x.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).ToArray());
        Assert.Equal(new[] { "REQUIREMENT_LINK_MISSING", "OWNER_MISSING" }, findings[noLinkNoOwner.Id]);
        Assert.Equal(new[] { "OWNER_MISSING" }, findings[linkedNoOwner.Id]);
        Assert.Equal(new[] { "REQUIREMENT_LINK_MISSING" }, findings[ownedNoLink.Id]);
        Assert.Equal(new[] { "CRITICALITY_UNKNOWN", "REQUIREMENT_LINK_MISSING", "OWNER_MISSING" }, findings[unknown.Id]);
        Assert.False(findings.ContainsKey(complete.Id)); Assert.False(findings.ContainsKey(nonCritical.Id));
        Assert.All(report.GetProperty("criticalOrphans").EnumerateArray(), x => Assert.Equal("DRAFT", x.GetProperty("controlState").GetString()));
        // Tenant/vessel isolation holds for the extended report.
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + GraphRoute("controls/orphans", seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id).Id), Ct)).StatusCode);
    }

    // DEV-029 AC: immutable metadata/checksum/provenance; many-to-many both directions; stale/expired/superseded visible.
    [Fact]
    [Trait("Requirement", "DEV-029")]
    public async Task One_control_is_supported_by_many_evidence_and_one_evidence_supports_many_controls_with_visible_time_states()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var source = RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id);
        var pin = new RequirementPin(RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id), 1, source, 1);
        var second = await CreateRegister(c, "requirement-drafts", new(), source, 1);
        var pin2 = new RequirementPin(second.Current.Id, second.Current.Revision, source, 1);
        var a = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin] });
        var b = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin2] });
        var now = DateTimeOffset.UtcNow;
        var stale = await GraphSave(c, "evidence", GraphCommand(content: new(Title: "FICTIONAL stale", Provenance: "Synthetic declaration", ObservedAt: now.AddDays(-2), FreshUntil: now.AddDays(-1))) with
            { Requirements = [pin, pin2], Links = [new("Control", a.Id, a.Revision, a.Version), new("Control", b.Id, b.Revision, b.Version)] });
        var expired = await GraphSave(c, "evidence", GraphCommand(content: new(Title: "FICTIONAL expired", Provenance: "Synthetic declaration", ObservedAt: now.AddDays(-3), ValidUntil: now.AddDays(-1))) with
            { Requirements = [pin], Links = [new("Control", a.Id, a.Revision, a.Version)] });
        Assert.Equal("DECLARED_STALE", stale.TimeState); Assert.Equal("DECLARED_EXPIRED", expired.TimeState);
        Assert.Equal(2, stale.Links.Length); Assert.Equal(2, stale.Requirements.Length);
        var supportA = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{a.Id}/supporting-evidence"), Ct);
        var itemsA = supportA.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { stale.Id, expired.Id }.Order(), itemsA.Select(x => x.GetProperty("evidence").GetProperty("id").GetGuid()).Order());
        Assert.All(itemsA, x => { Assert.True(x.GetProperty("pinCurrent").GetBoolean()); Assert.Equal("UNVERIFIED", x.GetProperty("evidence").GetProperty("verification").GetString()); });
        var supportB = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{b.Id}/supporting-evidence"), Ct);
        Assert.Equal(stale.Id, Assert.Single(supportB.GetProperty("items").EnumerateArray()).GetProperty("evidence").GetProperty("id").GetGuid());
        // Correction appends; the prior revision's hash, metadata and links stay byte-identical history.
        var corrected = await GraphSave(c, $"evidence/{stale.Id}", GraphCommand(stale.Version, stale.Content with { Provenance = "Synthetic corrected declaration" }) with
            { Requirements = [pin], Links = [new("Control", a.Id, a.Revision, a.Version)] }, update: true);
        var versions = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"evidence/{stale.Id}/versions"), Ct)).GetProperty("items").EnumerateArray().ToArray();
        var original = versions.Single(x => x.GetProperty("revision").GetInt64() == 1);
        Assert.Equal(stale.Hash, original.GetProperty("hash").GetString());
        Assert.Equal("Synthetic declaration", original.GetProperty("content").GetProperty("provenance").GetString());
        Assert.Equal("SUPERSEDED", original.GetProperty("timeState").GetString()); Assert.Equal(2, original.GetProperty("links").GetArrayLength());
        Assert.NotEqual(stale.Hash, corrected.Hash);
        // Control B no longer has current support once the evidence relinked; history keeps the old pin.
        Assert.Empty((await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{b.Id}/supporting-evidence"), Ct)).GetProperty("items").EnumerateArray());
        // A control revision leaves existing evidence pinned to the earlier revision, visibly.
        var a2 = await GraphSave(c, $"controls/{a.Id}", GraphCommand(a.Version, a.Content) with { Requirements = [pin] }, update: true);
        var supportA2 = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"controls/{a.Id}/supporting-evidence"), Ct);
        Assert.Equal(a2.Revision, supportA2.GetProperty("controlRevision").GetInt64());
        Assert.All(supportA2.GetProperty("items").EnumerateArray(), x => { Assert.False(x.GetProperty("pinCurrent").GetBoolean()); Assert.Equal(1, x.GetProperty("pinnedControlRevision").GetInt64()); });
        await using var db = Db();
        Assert.Equal(original.GetProperty("hash").GetString(), (await db.ControlEvidenceRevisions.SingleAsync(x => x.Id == stale.Id && x.Revision == 1, Ct)).Hash);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Sql($"UPDATE core.\"ControlEvidenceRevisions\" SET \"Hash\" = 'forged' WHERE \"Id\" = '{stale.Id}'"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + GraphRoute($"controls/{a.Id}/supporting-evidence", seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id).Id), Ct)).StatusCode);
    }

    // DEV-030 AC: evidence existence alone never closes the state; read side is assigned-scope only.
    [Fact]
    [Trait("Requirement", "DEV-030")]
    public async Task Evidence_existence_alone_never_creates_or_closes_a_verification_and_reads_are_assignment_scoped()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c);
        // No review exists: evidence + link + control never imply a verification row or state.
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/" + GraphRoute("verifications"), Ct)).StatusCode);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); await Token(c);
        var empty = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("verifications"), Ct);
        Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.True(empty.GetProperty("assigned").GetBoolean());
        Assert.Equal("UNVERIFIED", ev.Verification); Assert.Equal("UNVERIFIED", co.Verification);
        var pending = await PendingReview(c, host.Services, ev, co);
        using (var list = await c.GetAsync("/api/v1/" + GraphRoute("verifications"), Ct)) {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode); Assert.Contains("no-store", list.Headers.CacheControl!.ToString());
            var item = Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
            Assert.Equal(pending.Id, item.GetProperty("id").GetGuid()); Assert.Equal("PENDING", item.GetProperty("currentState").GetString());
            Assert.False(item.TryGetProperty("rationale", out _)); // private rationale is detail-only
        }
        // A new evidence revision (more existence) never moves PENDING toward VERIFIED.
        var evidenceHead = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("evidence"), Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == ev.Id);
        await GraphSave(c, $"evidence/{ev.Id}", GraphCommand(evidenceHead.GetProperty("version").GetUInt32(), ev.Content) with { Requirements = ev.Requirements, Links = await CurrentLinks(c, ev) }, update: true);
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"verifications/{pending.Id}"), Ct);
        Assert.Equal("PENDING", detail.GetProperty("current").GetProperty("state").GetString());
        Assert.Equal("SUPERSEDED_REVIEW_REQUIRED", detail.GetProperty("current").GetProperty("currentState").GetString());
        Assert.Equal(pending.Rationale, detail.GetProperty("current").GetProperty("rationale").GetString());
        await using (var db = Db()) Assert.DoesNotContain(await db.HumanReviewEvents.Where(x => x.EvidenceId == ev.Id).Select(x => x.State).ToArrayAsync(Ct), s => s == "VERIFIED");
        // Unassigned actor: no rows and no detail; other tenant vessel: not found.
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].HumanAuthorities = [];
        Assert.Empty((await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("verifications"), Ct)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + GraphRoute($"verifications/{pending.Id}"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + GraphRoute("verifications", seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id).Id), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/verifications?vesselId=" + SeedVessel.Id + "&state=VERIFIED", Ct)).StatusCode);
        await using var check = Db();
        Assert.Contains(await check.Set<SecurityEvent>().Where(e => e.Action == "HumanReviewRead").Select(e => e.Result).ToArrayAsync(Ct), r => r == "Success");
    }

    // DEV-030 AC: SUPERSEDED when the control or the evidence is revised; recorded state is never rewritten.
    [Theory]
    [Trait("Requirement", "DEV-030")]
    [InlineData("Control")]
    [InlineData("Evidence")]
    public async Task Revising_the_control_or_evidence_makes_a_pending_review_supersedable_and_only_then(string revised)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c);
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id); var pending = await PendingReview(c, host.Services, ev, co);
        var early = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        await AssertCode(await Send(c, GraphRoute($"verifications/{pending.Id}/events"), early with { State = "SUPERSEDED", ReasonCode = "INPUT_SUPERSEDED" }), HttpStatusCode.Conflict, "supersession_not_proven");
        var route = revised == "Control" ? "controls" : "evidence"; var target = revised == "Control" ? co : ev;
        var head = (await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute(route), Ct)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == target.Id);
        await GraphSave(c, $"{route}/{target.Id}", GraphCommand(head.GetProperty("version").GetUInt32(), target.Content) with { Requirements = target.Requirements, Links = revised == "Control" ? [] : await CurrentLinks(c, ev) }, update: true);
        var listed = Assert.Single((await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("verifications"), Ct)).GetProperty("items").EnumerateArray());
        Assert.Equal("PENDING", listed.GetProperty("state").GetString()); Assert.Equal("SUPERSEDED_REVIEW_REQUIRED", listed.GetProperty("currentState").GetString());
        var command = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        using var superseded = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), command with { State = "SUPERSEDED", ReasonCode = "INPUT_SUPERSEDED" });
        Assert.Equal(HttpStatusCode.OK, superseded.StatusCode);
        var detail = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"verifications/{pending.Id}"), Ct);
        Assert.Equal("SUPERSEDED", detail.GetProperty("current").GetProperty("state").GetString());
        Assert.Equal(new[] { "SUPERSEDED", "PENDING" }, detail.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("state").GetString()).ToArray());
        // Terminal: a superseded review cannot later be verified.
        var after = await CurrentReviewCommand(c, host.Services, ev, co, (await superseded.Content.ReadFromJsonAsync<HumanReviewDto>(Ct))!, "human.verify");
        using var terminal = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), after with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED" });
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);
    }

    // DEV-030 AC: verifier authorization + SoD (verifier ≠ submitter, ≠ evidence uploader, ≠ control author).
    [Theory]
    [Trait("Requirement", "DEV-030")]
    [InlineData("submitter")]
    [InlineData("uploader")]
    [InlineData("no-verify-capability")]
    [InlineData("submit-only-appointment")]
    public async Task Verifier_must_hold_an_exact_verify_appointment_and_differ_from_submitter_and_uploader(string scenario)
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c); var author = host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;
        var submitter = scenario == "uploader" ? Guid.NewGuid() : author;
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, newActor: submitter); await Token(c);
        var pending = await PendingReview(c, host.Services, ev, co);
        // "submitter": same actor verifies; "uploader": the evidence/control author verifies after another submitted.
        var verifier = scenario is "submitter" ? submitter : scenario == "uploader" ? author : Guid.NewGuid();
        ProvisionSyntheticHuman(host.Services, ev.Id, co.Id, newActor: verifier); await Token(c);
        var command = await CurrentReviewCommand(c, host.Services, ev, co, pending, "human.verify");
        var settings = host.Services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        if (scenario == "no-verify-capability") settings.Memberships[0] = m with { Permissions = m.Permissions.Where(p => p != "human.verify").ToArray() };
        if (scenario == "submit-only-appointment") command = command with { AuthorityId = m.HumanAuthorities.Single(g => g.Capability == "human.review.submit").Id };
        using var response = await Send(c, GraphRoute($"verifications/{pending.Id}/events"), command with { State = "VERIFIED", ReasonCode = "HUMAN_VERIFIED" });
        if (scenario is "submitter" or "uploader") await AssertCode(response, HttpStatusCode.Conflict, "separation_of_duties");
        else await AssertCode(response, HttpStatusCode.Forbidden, "human_authority_required");
        await using var db = Db();
        Assert.Equal(new[] { "PENDING" }, await db.HumanReviewEvents.Where(x => x.Id == pending.Id).Select(x => x.State).ToArrayAsync(Ct));
    }

    // DEV-028 seed: the only honest initial state; no review/approval/publication event is seeded.
    [Fact]
    [Trait("Requirement", "DEV-028")]
    public async Task Seeded_controls_start_draft_without_lifecycle_events_and_evidence_has_no_lifecycle()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var seeded = ControlEvidenceFixtures.Id(SeedVessel.Id, "Control");
        var lifecycle = await ReadLifecycle(c, seeded);
        Assert.Equal("DRAFT", lifecycle.State); Assert.Empty(lifecycle.Events);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + LifecycleRoute(ControlEvidenceFixtures.Id(SeedVessel.Id, "Evidence")), Ct)).StatusCode);
        await using var db = Db();
        Assert.Empty(await db.ControlStateEvents.ToArrayAsync(Ct));
        Assert.All(await db.ControlEvidenceHeads.Where(x => x.Kind == "Evidence").ToArrayAsync(Ct), x => Assert.Null(x.ControlState));
        Assert.All(await db.ControlEvidenceHeads.Where(x => x.Kind == "Control").ToArrayAsync(Ct), x => Assert.Equal("DRAFT", x.ControlState));
    }
}
