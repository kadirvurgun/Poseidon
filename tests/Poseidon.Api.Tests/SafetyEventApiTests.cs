using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S9 WP-B DEV-060 / T-S9-003 (18.01 row 61) and DEV-061 / T-S9-004 (row 62); 61.01 Rev3 §4-§11.
// Real host, antiforgery and owned PostgreSQL. Synthetic memberships and FICTIONAL content only: a reporter, a
// triager/investigator, an independent reviewer, an action owner, a verifier and an effectiveness reviewer are
// six different synthetic people. Nothing here is an expert severity policy, a legal decision or a real incident.
// AUTHORED; execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] SafetyPermissions = [SafetyEventPolicy.ReadPermission, SafetyEventPolicy.ReportPermission, SafetyEventPolicy.TriagePermission,
        SafetyEventPolicy.InvestigatePermission, SafetyEventPolicy.ReviewPermission, SafetyEventPolicy.ActionWritePermission, SafetyEventPolicy.ActionVerifyPermission,
        SafetyEventPolicy.ActionEffectivenessPermission];
    private void SafetyActor(IServiceProvider services, Guid actor, params string[] permissions)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = "Human", Permissions = [.. m.Permissions.Where(p => !p.StartsWith("hse.safety_", StringComparison.Ordinal)), .. permissions] };
    }
    // A second server-provisioned member (different subject) so an action owner exists in the tenant.
    private static void SafetyMember(IServiceProvider services, Guid actor)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships = [.. settings.Memberships, m with { Subject = "synthetic-safety-" + actor.ToString("N"), ActorId = actor }];
    }
    private string SafetyPath(string suffix = "") => "safety-events" + suffix + "?vesselId=" + SeedVessel.Id;
    private async Task<SafetyEventDto> SafetyView(HttpClient c, Guid id) => (await c.GetFromJsonAsync<SafetyEventDto>("/api/v1/" + SafetyPath("/" + id), Ct))!;
    private static SafetyEventDraftFields NearMiss(DateTimeOffset at) => new("NEAR_MISS", "EXACT", at, null, null, "Europe/Istanbul", "MOORING",
        "FICTIONAL forward mooring station", "FICTIONAL head line parted during heaving",
        "FICTIONAL the head line parted while heaving in; nobody stood in the snap-back zone.", "YES", "FICTIONAL area cleared, line replaced", "HIGH");
    private static SafetyInvestigationContent SafetyInvestigationContent(Guid factor, Guid cause, Guid barrier) => new(
        "FICTIONAL scope: mooring line failure at the forward station",
        [new(factor, "FACT", "EQUIPMENT", "FICTIONAL line exceeded its retirement criteria")],
        [new(cause, "FICTIONAL line inspection interval was not followed", [factor], "SUPPORTED")],
        [new(barrier, "FICTIONAL mooring line inspection", "PREVENTIVE", "FAILED", "BAR-1")], [], []);

    [Fact]
    [Trait("Requirement", "DEV-060")]
    [Trait("Requirement", "DEV-061")]
    public async Task Safety_event_capture_triage_investigation_actions_effectiveness_and_closure_are_separate_human_records()
    {
        await using var host = Host(); using var c = Client(host);
        var reporter = Guid.NewGuid(); var lead = Guid.NewGuid(); var reviewer = Guid.NewGuid(); var owner = Guid.NewGuid(); var verifier = Guid.NewGuid(); var assessor = Guid.NewGuid();
        SafetyMember(host.Services, owner);
        SafetyActor(host.Services, reporter, SafetyEventPolicy.ReportPermission); await Token(c);

        // DEV-060: incomplete draft is storable; submission lists what is missing (POL-DEFAULT-01).
        var key = Guid.NewGuid().ToString();
        var create = new SafetyEventCreateCommand(Guid.NewGuid(), SafetyEventPolicy.Named, new SafetyEventDraftFields(Category: "NEAR_MISS"));
        var draft = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath(), create, key));
        Assert.Equal(("DRAFT", 1L, "NAMED", "ONLINE"), (draft.State, draft.Revision, draft.ReportingMode, draft.Origin));
        Assert.Contains("narrative", draft.Missing); Assert.Equal(reporter, draft.Reporter.ActorId);
        Assert.Equal(draft.Id, (await RiskOk<SafetyEventDto>(await Send(c, SafetyPath(), create, key))).Id);
        await AssertCode(await Send(c, SafetyPath(), create with { Id = Guid.NewGuid() }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/submit"), new SafetyEventSubmitCommand(draft.Version, draft.Hash)), (HttpStatusCode)422, "safety_event_incomplete");
        await AssertCode(await Send(c, SafetyPath(), create with { Id = Guid.NewGuid(), ReportingMode = SafetyEventPolicy.Anonymous }), (HttpStatusCode)422, "anonymous_channel_unavailable");
        var now = DateTimeOffset.UtcNow;
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}"), new SafetyEventUpdateCommand(draft.Version, NearMiss(now.AddDays(1))), update: true),
            (HttpStatusCode)422, "occurrence_in_future");
        var edited = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}"), new SafetyEventUpdateCommand(draft.Version, NearMiss(now.AddHours(-3))), update: true));
        Assert.Equal(2L, edited.Revision); Assert.Empty(edited.Missing); Assert.True(edited.Can.Submit);
        var submitted = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/submit"), new SafetyEventSubmitCommand(edited.Version, edited.Hash)));
        Assert.Equal("SUBMITTED", submitted.State); Assert.Null(submitted.FollowUpSecret);
        // Withdraw only an unsubmitted draft.
        var other = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath(), new SafetyEventCreateCommand(Guid.NewGuid(), SafetyEventPolicy.Named, new SafetyEventDraftFields())));
        var withdrawn = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{other.Id}/withdraw"), new SafetyEventReasonCommand(other.Version, "FICTIONAL duplicate of my other report")));
        Assert.Equal("WITHDRAWN", withdrawn.State);
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/withdraw"), new SafetyEventReasonCommand(submitted.Version, "FICTIONAL try to withdraw")), HttpStatusCode.Conflict, "invalid_state");

        // Triage by someone else; the reporter severity remains a claim. HIGH makes the blocker source report it.
        SafetyActor(host.Services, lead, SafetyPermissions); await Token(c);
        await AssertCode(await c.GetAsync("/api/v1/" + SafetyPath($"/{other.Id}"), Ct), HttpStatusCode.NotFound, "resource_not_found"); // another person's draft
        var triaged = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/triage"),
            new SafetyEventTriageCommand(submitted.Version, "TRIAGE", "HIGH", "YES", "FICTIONAL line failure with potential for serious injury")));
        Assert.Equal(("TRIAGED", "HIGH", "UNCONFIGURED"), (triaged.State, triaged.Severity, triaged.SeverityPolicy));
        var page = (await c.GetFromJsonAsync<SafetyEventPageDto>("/api/v1/" + SafetyPath(), Ct))!;
        Assert.Equal("BLOCKERS_PRESENT", page.Blockers.State);
        Assert.Contains(page.Blockers.Items, b => b.EventId == draft.Id && b.Reason == "HIGH_SEVERITY_OPEN" && b.Blocking);
        Assert.DoesNotContain(page.Items, i => i.Id == other.Id);
        var message = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/messages"), new SafetyEventMessageCommand(Guid.NewGuid(), "TEAM_ONLY", "FICTIONAL team note")));
        Assert.Single(message.Messages);

        // DEV-061: the investigation records contributing factors, a human root cause and the failed barrier.
        var factor = Guid.NewGuid(); var cause = Guid.NewGuid(); var barrier = Guid.NewGuid();
        var opened = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/investigation"),
            new SafetyInvestigationSaveCommand(0, new(null, [], [], [], [], [])), update: true));
        Assert.Equal("INVESTIGATING", opened.State); Assert.Contains("contributing_factors", opened.Investigation!.Missing);
        var saved = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/investigation"),
            new SafetyInvestigationSaveCommand(opened.Investigation.Version, SafetyInvestigationContent(factor, cause, barrier)), update: true));
        var inReview = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/investigation/submit"),
            new SafetyInvestigationSubmitCommand(saved.Investigation!.Version, saved.Investigation.Hash)));
        Assert.Equal("SUBMITTED", inReview.Investigation!.State);
        var review = new SafetyInvestigationReviewCommand(inReview.Investigation.Version, inReview.Investigation.Hash, "APPROVE", "FICTIONAL findings supported by the records");
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/investigation/review"), review), HttpStatusCode.Forbidden, "separation_of_duties");
        SafetyActor(host.Services, reviewer, SafetyPermissions); await Token(c);
        var approved = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/investigation/review"), review));
        Assert.Equal(("ACTION_TRACKING", "APPROVED"), (approved.State, approved.Investigation!.State));
        Assert.Contains("BARRIER_WITHOUT_ACTION:" + barrier.ToString("D"), approved.ClosureBlockers);

        // Corrective action with owner and due date; completion, verification and effectiveness by three people.
        var actionId = Guid.NewGuid();
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/actions"), new SafetyActionCreateCommand(actionId, "CORRECTIVE", "FICTIONAL inspect lines",
            "FICTIONAL inspect and retire mooring lines per criteria", Guid.NewGuid(), DateOnly.FromDateTime(now.UtcDateTime.AddDays(14)), [cause], [barrier])), (HttpStatusCode)422, "owner_unavailable");
        var withAction = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/actions"), new SafetyActionCreateCommand(actionId, "CORRECTIVE", "FICTIONAL inspect lines",
            "FICTIONAL inspect and retire mooring lines per criteria", owner, DateOnly.FromDateTime(now.UtcDateTime.AddDays(14)), [cause], [barrier])));
        var action = Assert.Single(withAction.Actions); Assert.Equal(("OPEN", "PENDING"), (action.State, action.Effectiveness));
        Assert.Equal(["ACTION_NOT_VERIFIED:" + actionId.ToString("D")], withAction.ClosureBlockers);
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/actions/{actionId}/steps"), new SafetyActionStepCommand(1, "COMPLETE", null, "FICTIONAL not my action")),
            HttpStatusCode.Forbidden, "separation_of_duties");
        // The owner now acts as the primary synthetic membership; drop the extra owner-only member so the actor id stays unique.
        var federation = host.Services.GetRequiredService<FederationSettings>(); federation.Memberships = [federation.Memberships[0]];
        SafetyActor(host.Services, owner, SafetyEventPolicy.ReadPermission, SafetyEventPolicy.ActionWritePermission); await Token(c);
        var completed = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/actions/{actionId}/steps"), new SafetyActionStepCommand(1, "COMPLETE", null, "FICTIONAL lines inspected and two retired")));
        Assert.Equal("COMPLETED", completed.Actions[0].State);
        SafetyActor(host.Services, verifier, SafetyPermissions); await Token(c);
        var verified = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/actions/{actionId}/steps"), new SafetyActionStepCommand(2, "VERIFY", "VERIFIED", "FICTIONAL retirement records checked onboard")));
        Assert.Equal(("VERIFIED", "PENDING"), (verified.Actions[0].State, verified.Actions[0].Effectiveness));
        Assert.Equal(["EFFECTIVENESS_NOT_REVIEWED:" + actionId.ToString("D")], verified.ClosureBlockers);
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/actions/{actionId}/steps"), new SafetyActionStepCommand(3, "EFFECTIVENESS", "EFFECTIVE", "FICTIONAL own verification")),
            HttpStatusCode.Forbidden, "separation_of_duties");
        SafetyActor(host.Services, assessor, SafetyPermissions); await Token(c);
        var effective = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/actions/{actionId}/steps"), new SafetyActionStepCommand(3, "EFFECTIVENESS", "EFFECTIVE", "FICTIONAL no recurrence over the review window")));
        Assert.Equal("EFFECTIVE", effective.Effectiveness); Assert.Empty(effective.ClosureBlockers); Assert.Equal("ACTION_TRACKING", effective.State);

        // Independent closure (not the investigation author). Closure is its own record next to effectiveness.
        SafetyActor(host.Services, lead, SafetyPermissions); await Token(c);
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/close"), new SafetyEventReasonCommand(effective.Version, "FICTIONAL closure by investigator")), HttpStatusCode.Forbidden, "separation_of_duties");
        SafetyActor(host.Services, reviewer, SafetyPermissions); await Token(c);
        var closed = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/close"), new SafetyEventReasonCommand(effective.Version, "FICTIONAL all actions verified and effective")));
        Assert.Equal(("CLOSED", reviewer, "EFFECTIVE"), (closed.State, closed.ClosedBy!.Value, closed.Effectiveness));
        Assert.DoesNotContain((await c.GetFromJsonAsync<SafetyEventPageDto>("/api/v1/" + SafetyPath(), Ct))!.Blockers.Items, b => b.EventId == draft.Id);

        await using var db = Db();
        Assert.Equal(2, await db.SafetyEventReceipts.CountAsync(r => r.ActorId == reporter && r.ResourceId == draft.Id && r.Action == "SafetyEventCreate" || r.ActorId == reporter && r.Action == "SafetyEventUpdate" && r.ResourceId == draft.Id, Ct));
        Assert.Equal(3, await db.SafetyActionSteps.CountAsync(s => s.ActionId == actionId, Ct));
        var audit = JsonSerializer.Serialize(await db.AuditEvents.Where(a => a.ResourceId == draft.Id || a.ResourceId == actionId).Select(a => a.ChangeSummary).ToArrayAsync(Ct));
        Assert.DoesNotContain("snap-back", audit); Assert.DoesNotContain("retirement", audit); Assert.DoesNotContain("FICTIONAL", audit);
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public async Task Confidential_report_keeps_the_reporter_in_the_vault_and_uses_a_pseudonymous_follow_up_channel()
    {
        await using var host = Host(); using var c = Client(host);
        var reporter = Guid.NewGuid(); var lead = Guid.NewGuid(); var privacy = Guid.NewGuid();
        SafetyActor(host.Services, reporter, SafetyEventPolicy.ReportPermission); await Token(c);
        await AssertCode(await Send(c, SafetyPath(), new SafetyEventCreateCommand(Guid.NewGuid(), SafetyEventPolicy.Confidential, NearMiss(DateTimeOffset.UtcNow.AddHours(-1)))),
            HttpStatusCode.BadRequest, "invalid_request"); // contact preference must be explicit
        var draft = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath(), new SafetyEventCreateCommand(Guid.NewGuid(), SafetyEventPolicy.Confidential,
            NearMiss(DateTimeOffset.UtcNow.AddHours(-1)), "IN_APP_FOLLOW_UP")));
        Assert.Equal((SafetyEventPolicy.Confidential, (Guid?)null, true), (draft.Reporter.Mode, draft.Reporter.ActorId, draft.Reporter.IsCurrentActor));
        var submitted = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/submit"), new SafetyEventSubmitCommand(draft.Version, draft.Hash)));
        var token = Assert.IsType<string>(submitted.FollowUpSecret);
        Assert.Null((await SafetyView(c, draft.Id)).FollowUpSecret); // shown once

        // A general reader sees the event but not who reported it.
        SafetyActor(host.Services, lead, SafetyPermissions); await Token(c);
        var seen = await SafetyView(c, draft.Id);
        Assert.Equal((SafetyEventPolicy.Confidential, (Guid?)null, false), (seen.Reporter.Mode, seen.Reporter.ActorId, seen.Reporter.IsCurrentActor));
        Assert.False(seen.Can.ReadIdentity);
        await AssertCode(await Send(c, SafetyPath($"/{draft.Id}/identity-access"), new SafetyEventIdentityAccessCommand(Guid.NewGuid(), "WELFARE_SUPPORT", "FICTIONAL check on crew member")),
            HttpStatusCode.Forbidden, "permission_denied");
        var shared = await RiskOk<SafetyEventDto>(await Send(c, SafetyPath($"/{draft.Id}/messages"), new SafetyEventMessageCommand(Guid.NewGuid(), "REPORTER_SHARED", "FICTIONAL thank you, under review")));
        Assert.Single(shared.Messages);

        // Restricted vault read with a recorded purpose.
        SafetyActor(host.Services, privacy, SafetyEventPolicy.ReadPermission, SafetyEventPolicy.IdentityPermission); await Token(c);
        var identity = await RiskOk<SafetyEventIdentityDto>(await Send(c, SafetyPath($"/{draft.Id}/identity-access"),
            new SafetyEventIdentityAccessCommand(Guid.NewGuid(), "INVESTIGATION_FOLLOW_UP", "FICTIONAL investigator needs a witness interview")));
        Assert.Equal((reporter, "IN_APP_FOLLOW_UP"), (identity.ReporterActorId, identity.ContactPreference));

        // Follow-up by token only (header, never URL): shared messages, no team notes, no identity.
        SafetyActor(host.Services, reporter, SafetyEventPolicy.ReportPermission); await Token(c);
        using (var bad = new HttpRequestMessage(HttpMethod.Get, "/api/v1/safety-events/follow-up")) {
            bad.Headers.Add("X-Safety-Follow-Up", token[..23] + new string('A', token.Length - 23));
            await AssertCode(await c.SendAsync(bad, Ct), HttpStatusCode.NotFound, "resource_unavailable");
        }
        var messageId = Guid.NewGuid();
        using var post = new HttpRequestMessage(HttpMethod.Post, "/api/v1/safety-events/follow-up/messages") { Content = JsonContent.Create(new SafetyEventFollowUpMessageCommand(messageId, "FICTIONAL the line was frayed before departure")) };
        post.Headers.Add("X-Safety-Follow-Up", token); post.Headers.Add("Idempotency-Key", messageId.ToString("D"));
        using var posted = await c.SendAsync(post, Ct); Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var followUp = (await posted.Content.ReadFromJsonAsync<SafetyEventFollowUpDto>(Ct))!;
        Assert.Equal(2, followUp.Messages.Length); Assert.All(followUp.Messages, m => Assert.Null(m.ActorId));
        Assert.Contains(followUp.Messages, m => m.AuthorKind == "REPORTER");

        await using var db = Db();
        Assert.Null((await db.SafetyEvents.SingleAsync(e => e.Id == draft.Id, Ct)).ReportedBy);
        Assert.All(await db.SafetyEventRevisions.Where(r => r.EventId == draft.Id).ToArrayAsync(Ct), r => Assert.Null(r.ActorId));
        var vault = await db.SafetyEventIdentities.SingleAsync(i => i.EventId == draft.Id, Ct);
        Assert.Equal(reporter, vault.ReporterId); Assert.NotEqual(token, vault.FollowUpHash); Assert.DoesNotContain(token[23..], vault.FollowUpHash!);
        Assert.Single(await db.SafetyEventIdentityAccesses.Where(x => x.EventId == draft.Id).ToArrayAsync(Ct));
        var summaries = JsonSerializer.Serialize(await db.AuditEvents.Where(a => a.VesselId == SeedVessel.Id).Select(a => a.ChangeSummary).ToArrayAsync(Ct));
        Assert.DoesNotContain(reporter.ToString("D"), summaries); Assert.DoesNotContain(token, summaries); Assert.DoesNotContain("frayed", summaries);
    }

    private SafetyEventOfflineSettings SyntheticSafetyOffline(DailyOfflineClock clock, Guid actor) => new() { Policy = new SafetyEventOfflinePolicy {
        Version = "fictional-safety-offline-v1", SourceVersion = SafetyEventPolicy.SourceVersion, StoragePolicyReference = "FICTIONAL encrypted IndexedDB test only",
        KeyPolicyReference = "FICTIONAL nonextractable key test only", RetentionPolicyReference = "FICTIONAL isolated fixture retention; no approval", RetentionSeconds = 7200, LeaseSeconds = 600,
        NotBefore = clock.Current.AddDays(-1), ExpiresAt = clock.Current.AddDays(1), MaxEnrollments = 8, MaxRecords = 32, MaxEncryptedBytes = 1048576, MaxDeviceRecords = 64, MaxDeviceEncryptedBytes = 4194304,
        Fields = SafetyEventOfflinePolicy.Parts.Select(k => new SafetyEventOfflineField(k, "FICTIONAL classification; not source approval", "Fictional regression only", "RestrictedOperationalDraft")).ToArray(),
        Scopes = [new(SeedVessel.TenantId, actor, SeedVessel.Id)] } };
    private string SafetyOfflinePath(Guid? enrollment = null, string action = "") =>
        "safety-event-offline" + (enrollment == null ? "" : $"/{enrollment:D}") + (action.Length == 0 ? "" : "/" + action) + $"?vesselId={SeedVessel.Id:D}";
    private async Task<SafetyEventOfflineContextDto> EnrollSafetyOffline(HttpClient c)
    {
        await Token(c); using var response = await Send(c, SafetyOfflinePath(), new { }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var e = (await response.Content.ReadFromJsonAsync<SafetyEventOfflineContextDto>(Ct))!;
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); offlineTestKeys.Add(e.Id, key);
        var proof = Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes($"safety-event/{e.VesselId:D}/{e.Id:D}/{e.KeyId:D}/{e.Challenge}"), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        using var enrolled = await Send(c, SafetyOfflinePath(e.Id, "key"), new { publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), proof });
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode); return e;
    }
    private async Task<HttpResponseMessage> DeliverSafety(HttpClient c, SafetyEventOfflineContextDto e, SafetyEventOfflineCapture capture)
    {
        await Token(c); var body = JsonSerializer.Serialize(capture, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + SafetyOfflinePath(e.Id, "replay")) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Add("Idempotency-Key", capture.CommandId.ToString("D")); message.Headers.Add("X-Offline-Hash", digest);
        message.Headers.Add("X-Offline-Signature", Convert.ToBase64String(offlineTestKeys[e.Id].SignData(Encoding.UTF8.GetBytes(digest), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)));
        return await c.SendAsync(message, Ct);
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public async Task Offline_capture_replays_a_signed_named_draft_exactly_once_and_lifecycle_commands_are_audited()
    {
        var clock = new DailyOfflineClock(); var reporter = Guid.NewGuid();
        await using var host = Host().WithWebHostBuilder(b => b.ConfigureTestServices(s => {
            s.RemoveAll<SafetyEventOfflineSettings>(); s.AddSingleton(SyntheticSafetyOffline(clock, reporter)); s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }));
        using var c = Client(host);
        SafetyActor(host.Services, reporter, SafetyEventPolicy.ReportPermission);
        var e = await EnrollSafetyOffline(c);
        Assert.Equal(SafetyEventOfflinePolicy.Parts.Order(), e.AllowedParts.Order());
        var fields = NearMiss(clock.Current.AddMinutes(-30)) with { SeverityClaim = null };
        var capture = new SafetyEventOfflineCapture(Guid.NewGuid(), Guid.NewGuid(), e.KeyId, e.DeviceId, e.PolicyVersion, e.PolicyDigest, e.SourceVersion, e.Operation, e.ServerTime, 180, fields);
        using var first = await DeliverSafety(c, e, capture); Assert.Equal(HttpStatusCode.OK, first.StatusCode); var bytes = await first.Content.ReadAsStringAsync(Ct);
        var outcome = JsonSerializer.Deserialize<SafetyEventOfflineResult>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(("Committed", capture.EventId, 1L), (outcome.Result, outcome.EventId, outcome.Revision));
        using (var retry = await DeliverSafety(c, e, capture)) Assert.Equal(bytes, await retry.Content.ReadAsStringAsync(Ct));
        await AssertCode(await DeliverSafety(c, e, capture with { Fields = fields with { Summary = "FICTIONAL different summary" } }), HttpStatusCode.Conflict, "offline_command_reused");
        await AssertCode(await DeliverSafety(c, e, capture with { CommandId = Guid.NewGuid() }), HttpStatusCode.Conflict, "offline_event_exists");
        var view = await SafetyView(c, capture.EventId);
        Assert.Equal(("DRAFT", "OFFLINE", SafetyEventPolicy.Named), (view.State, view.Origin, view.ReportingMode)); // submit stays online
        Assert.Equal("CONFIGURED_UNVERIFIED", (await c.GetFromJsonAsync<SafetyEventPageDto>("/api/v1/" + SafetyPath(), Ct))!.Capabilities.Offline.State);

        using (var renewed = await Send(c, SafetyOfflinePath(e.Id, "renew"), new { })) Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        using (var revoked = await Send(c, SafetyOfflinePath(e.Id, "revoke"), new { })) Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        await AssertCode(await DeliverSafety(c, e, capture with { CommandId = Guid.NewGuid(), EventId = Guid.NewGuid() }), HttpStatusCode.Forbidden, "offline_policy_denied");
        var second = await EnrollSafetyOffline(c);
        using (var lost = await Send(c, SafetyOfflinePath(second.Id, "lost"), new { })) Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        using (var cleanup = await Send(c, SafetyOfflinePath(second.Id, "cleanup"), new { })) Assert.Equal(HttpStatusCode.OK, cleanup.StatusCode);
        await using var db = Db();
        Assert.Single(await db.SafetyEventOfflineOutcomes.Where(o => o.EventId == capture.EventId).ToArrayAsync(Ct));
        Assert.Equal(new[] { "CleanupAllowed", "Enrolled", "KeyEnrolled", "KeyLost" }, (await db.SafetyEventOfflineLifecycles.Where(l => l.EnrollmentId == second.Id).Select(l => l.Action).ToArrayAsync(Ct)).Order());
        var audit = JsonSerializer.Serialize(await db.AuditEvents.Where(a => a.ResourceId == capture.EventId || a.ResourceId == e.Id).ToArrayAsync(Ct));
        Assert.DoesNotContain("snap-back", audit); Assert.DoesNotContain(e.PolicyDigest, audit); Assert.DoesNotContain("PublicKey", audit);

        // Missing policy never permits enrollment.
        host.Services.GetRequiredService<SafetyEventOfflineSettings>().Policy!.RetentionPolicyReference = "";
        await Token(c); await AssertCode(await Send(c, SafetyOfflinePath(), new { }), HttpStatusCode.Forbidden, "offline_policy_denied");
    }

    [Fact]
    [Trait("Requirement", "DEV-060")]
    public async Task Safety_event_fixture_is_opt_in_idempotent_fictional_and_seeds_no_submission_or_decision()
    {
        await using (var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            await SafetyEventFixtures.AddMissingAsync(db, seed.Vessels, Ct); await SafetyEventFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        await using var check = Db();
        var ids = seed.Vessels.Where(v => v.TenantId == seed.Tenants[0].Id).Select(v => SafetyEventFixtures.EventId(v.Id)).ToArray();
        var rows = await check.SafetyEvents.Where(e => ids.Contains(e.Id)).ToArrayAsync(Ct);
        Assert.Equal(ids.Length, rows.Length);
        Assert.All(rows, r => Assert.Equal(("DRAFT", "SyntheticUnverified", (string?)null, (DateTimeOffset?)null), (r.State, r.Provenance, r.Severity, r.SubmittedAt)));
        Assert.False(await check.SafetyEventTriages.AnyAsync(t => ids.Contains(t.EventId), Ct));
        Assert.False(await check.SafetyInvestigations.AnyAsync(t => ids.Contains(t.EventId), Ct));
        Assert.Equal(ids.Length, await check.SafetyEventRevisions.CountAsync(r => ids.Contains(r.EventId), Ct));
    }
}
