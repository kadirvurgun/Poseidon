using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S9 WP-C: DEV-062 (18.01 row 63, T-S9-005), DEV-063 (row 64, T-S9-006), DEV-114 (row 115, T-DOC2-001).
// Real host, antiforgery and owned PostgreSQL; synthetic memberships and FICTIONAL content only.
// AUTHORED; execution DEFERRED to CI (no Docker in this package).
public sealed partial class DirectoryWorkspaceTests
{
    private void S9Actor(IServiceProvider services, Guid actor, string kind, params string[] permissions)
    {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { ActorId = actor, ActorKind = kind, Permissions = [.. CdBase, .. permissions] };
    }
    private string Voice(string suffix = "") => $"voice-event-drafts{suffix}?vesselId={SeedVessel.Id}";

    [Fact]
    [Trait("Requirement", "DEV-062")]
    [Trait("Requirement", "DEV-063")]
    public async Task Voice_draft_is_traced_human_confirmed_or_rejected_and_never_submitted()
    {
        await using var host = Host(); using var c = Client(host);
        var crew = Guid.NewGuid();
        S9Actor(host.Services, crew, "Human", "voice.draft.read", "voice.draft.write"); await Token(c);

        // No local engine ships: capability is explicit and transcription is a 503, never a fake transcript.
        var caps = (await c.GetFromJsonAsync<VoiceDraftCapabilitiesDto>("/api/v1/" + Voice("/capabilities"), Ct))!;
        Assert.Equal((false, "LOCAL_ENGINE_NOT_CONFIGURED", "NOT_RETAINED", VoiceEventDraftPolicy.ExtractorVersion),
            (caps.SpeechAvailable, caps.SpeechUnavailableReason, caps.AudioRetention, caps.ExtractorVersion));
        using (var audio = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + Voice("/transcriptions")) { Content = new ByteArrayContent([0x1A, 0x45, 0xDF, 0xA3]) })
        {
            audio.Content.Headers.ContentType = new("audio/webm");
            await AssertCode(await c.SendAsync(audio, Ct), HttpStatusCode.ServiceUnavailable, "speech_capability_unavailable");
        }

        // Typed transcript: time without a stated day is not turned into a date.
        const string note = "FICTIONAL near miss on the main deck, a mooring line whipped across the walkway at 09:15. We cordoned off the area.";
        var id = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        var create = new VoiceDraftCreateCommand(id, note, "TYPED", DateOnly.FromDateTime(DateTime.UtcNow), null);
        var created = await TechOk<VoiceDraftDto>(await Send(c, Voice(), create, key));
        Assert.Equal(("DRAFT", "TYPED", 1L, "NOT_READY", "NOT_RETAINED"), (created.State, created.Origin, created.Revision, created.Handoff, created.AudioRetention));
        Assert.Equal(("NEAR_MISS", "MAIN_DECK", (string?)null), (created.Fields.EventType, created.Fields.Location, created.Fields.OccurredAt));
        Assert.Contains("occurredAt", created.Missing);
        Assert.All(created.Candidate.Fields, f => Assert.True(f.Spans.Length > 0 && f.Confidence > 0 && f.Rule.Length > 0));
        Assert.Equal(VoiceEventDraftPolicy.ExtractorVersion, created.Candidate.ExtractorVersion);
        Assert.Contains(created.Candidate.Hints, h => h.Code == "TIME_WITHOUT_DATE");
        Assert.Equal(created.Id, (await TechOk<VoiceDraftDto>(await Send(c, Voice(), create, key))).Id); // exact replay
        await AssertCode(await Send(c, Voice(), create with { Transcript = "FICTIONAL other" }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertCode(await Send(c, Voice(), create with { Id = Guid.NewGuid(), Origin = "LOCAL_SPEECH", SpeechAttestation = "forged" }), HttpStatusCode.BadRequest, "speech_attestation_invalid");

        // Incomplete confirmation is blocked with the missing list (POL-DEFAULT-01).
        using (var blocked = await Send(c, Voice($"/{id}/confirm"), new VoiceDraftConfirmCommand(created.Version, created.Hash)))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
            var body = await blocked.Content.ReadFromJsonAsync<TechnicalRecordFailure>(Ct);
            Assert.Equal("voice_draft_incomplete", body!.Code); Assert.Contains("occurredAt", body.Missing);
        }
        var edited = await TechOk<VoiceDraftDto>(await Send(c, Voice($"/{id}"), new VoiceDraftUpdateCommand(created.Version,
            created.Fields with { OccurredAt = "2026-11-19T09:15", Location = "FORECASTLE" }, []), update: true));
        Assert.Equal(("USER_ENTERED", "USER_EDITED", "EXTRACTED"), (edited.FieldOrigins["occurredAt"], edited.FieldOrigins["location"], edited.FieldOrigins["eventType"]));
        Assert.Empty(edited.Missing);
        await AssertCode(await Send(c, Voice($"/{id}/confirm"), new VoiceDraftConfirmCommand(edited.Version, created.Hash)), HttpStatusCode.Conflict, "version_conflict");

        // Confirmation is a human record: an AI principal with the permission is refused.
        S9Actor(host.Services, Guid.NewGuid(), "AI", "voice.draft.read", "voice.draft.write"); await Token(c);
        await AssertCode(await Send(c, Voice($"/{id}/confirm"), new VoiceDraftConfirmCommand(edited.Version, edited.Hash)), HttpStatusCode.Forbidden, "human_actor_required");
        S9Actor(host.Services, crew, "Human", "voice.draft.read"); await Token(c);
        await AssertCode(await Send(c, Voice($"/{id}/confirm"), new VoiceDraftConfirmCommand(edited.Version, edited.Hash)), HttpStatusCode.Forbidden, "permission_denied");
        S9Actor(host.Services, crew, "Human", "voice.draft.read", "voice.draft.write"); await Token(c);
        var confirmed = await TechOk<VoiceDraftDto>(await Send(c, Voice($"/{id}/confirm"), new VoiceDraftConfirmCommand(edited.Version, edited.Hash)));
        // S9 integration: without hse.safety_event.report no event draft is created and the reason is explicit.
        Assert.Equal(("CONFIRMED", crew, "PENDING_EVENT_DRAFT_INTEGRATION", (Guid?)null, "SAFETY_EVENT_REPORT_PERMISSION_REQUIRED"),
            (confirmed.State, confirmed.ConfirmedBy, confirmed.Handoff, confirmed.SafetyEventId, confirmed.HandoffReason));
        var corrections = confirmed.Corrections!.ToDictionary(x => x.Key, x => x.Outcome);
        Assert.Equal(("ACCEPTED", "CORRECTED", "ADDED"), (corrections["eventType"], corrections["location"], corrections["occurredAt"]));
        await AssertCode(await Send(c, Voice($"/{id}"), new VoiceDraftUpdateCommand(confirmed.Version, confirmed.Fields, []), update: true), HttpStatusCode.Conflict, "invalid_state");

        // User rejection is recorded with its reason and counted by the metrics observation.
        var other = await TechOk<VoiceDraftDto>(await Send(c, Voice(), new VoiceDraftCreateCommand(Guid.NewGuid(), "FICTIONAL test note, nothing happened.", "TYPED",
            DateOnly.FromDateTime(DateTime.UtcNow), null)));
        await AssertCode(await Send(c, Voice($"/{other.Id}/reject"), new VoiceDraftRejectCommand(other.Version, "OTHER", null)), HttpStatusCode.BadRequest, "invalid_request");
        var rejected = await TechOk<VoiceDraftDto>(await Send(c, Voice($"/{other.Id}/reject"), new VoiceDraftRejectCommand(other.Version, "NOT_AN_EVENT", null)));
        Assert.Equal(("REJECTED", "NOT_AN_EVENT"), (rejected.State, rejected.Rejection!.ReasonCode));
        var metrics = (await c.GetFromJsonAsync<VoiceDraftMetricsDto>("/api/v1/" + Voice("/metrics"), Ct))!;
        Assert.Equal(("OPERATIONAL_OBSERVATION_NOT_ACCURACY_CLAIM", 1, 1), (metrics.State, metrics.Confirmed, metrics.Rejected));
        Assert.Equal(1, metrics.RejectionsByReason["NOT_AN_EVENT"]);

        // Other vessel scope and unknown query keys are refused; transcript text never enters audit.
        var foreign = seed.Vessels.First(v => v.Id != SeedVessel.Id);
        using (var wrongVessel = await c.GetAsync($"/api/v1/voice-event-drafts/{id}?vesselId={foreign.Id}", Ct)) Assert.NotEqual(HttpStatusCode.OK, wrongVessel.StatusCode);
        await AssertCode(await c.GetAsync($"/api/v1/voice-event-drafts?vesselId={SeedVessel.Id}&tenantId={Guid.NewGuid()}", Ct), HttpStatusCode.BadRequest, "invalid_query");
        await using var db = Db();
        Assert.Equal(3, await db.VoiceEventDraftRevisions.CountAsync(r => r.DraftId == id, Ct)); // CREATE, EDIT, CONFIRM; the replay added none
        var audits = await db.AuditEvents.Where(a => a.ResourceId == id).ToArrayAsync(Ct);
        Assert.NotEmpty(audits);
        Assert.DoesNotContain(audits, a => (a.ChangeSummary ?? "").Contains("mooring line", StringComparison.Ordinal));
        Assert.All(await db.VoiceEventDrafts.Where(d => d.Id == id || d.Id == other.Id).ToArrayAsync(Ct), d => Assert.Equal("NOT_RETAINED", d.AudioRetention));
        Assert.False(await db.SafetyEvents.AnyAsync(e => e.ReportedBy == crew, Ct));

        // S9 integration DEV-062 x DEV-060: a human confirmer who may report safety events gets a NAMED event DRAFT in the
        // same audited transaction; it is reported by that human, never submitted, and the voice draft links it.
        var reporter = Guid.NewGuid();
        S9Actor(host.Services, reporter, "Human", "voice.draft.read", "voice.draft.write", SafetyEventPolicy.ReportPermission); await Token(c);
        var handed = await TechOk<VoiceDraftDto>(await Send(c, Voice(), new VoiceDraftCreateCommand(Guid.NewGuid(), note, "TYPED", DateOnly.FromDateTime(DateTime.UtcNow), null)));
        handed = await TechOk<VoiceDraftDto>(await Send(c, Voice($"/{handed.Id}"), new VoiceDraftUpdateCommand(handed.Version,
            handed.Fields with { OccurredAt = "2026-11-19T09:15" }, []), update: true));
        var linked = await TechOk<VoiceDraftDto>(await Send(c, Voice($"/{handed.Id}/confirm"), new VoiceDraftConfirmCommand(handed.Version, handed.Hash)));
        Assert.Equal(("CONFIRMED", "EVENT_DRAFT_CREATED", (string?)null), (linked.State, linked.Handoff, linked.HandoffReason));
        var eventId = Assert.IsType<Guid>(linked.SafetyEventId);
        await using var check = Db();
        var safety = await check.SafetyEvents.SingleAsync(e => e.Id == eventId, Ct);
        Assert.Equal(("DRAFT", SafetyEventPolicy.Named, (Guid?)reporter, "NEAR_MISS", "ONLINE", 1L), (safety.State, safety.ReportingMode, safety.ReportedBy, safety.Category, safety.Origin, safety.Revision));
        Assert.Null(safety.SubmittedAt);
        var content = JsonSerializer.Deserialize<SafetyEventDraftFields>(safety.ContentJson, SafetyEventCommands.Json)!;
        Assert.Equal(("DATE_ONLY", (DateOnly?)new DateOnly(2026, 11, 19), (string?)null, (string?)null), (content.OccurrencePrecision, content.OccurrenceDate, content.ImmediateActionTaken, content.SeverityClaim));
        Assert.Equal(note, content.Narrative);
        Assert.Single(await check.SafetyEventRevisions.Where(r => r.EventId == eventId).ToArrayAsync(Ct));
        Assert.Empty(await check.SafetyEventReceipts.Where(r => r.ResourceId == eventId).ToArrayAsync(Ct));
        Assert.Equal(eventId, (await check.VoiceEventDrafts.SingleAsync(d => d.Id == handed.Id, Ct)).SafetyEventId);
        Assert.Contains(await check.AuditEvents.Where(a => a.ResourceId == eventId).ToArrayAsync(Ct), a => a.EntityType == nameof(SafetyEvent) && a.ActorId == reporter.ToString());
    }

    [Fact]
    [Trait("Requirement", "DEV-114")]
    public async Task Isps_restricted_document_hides_from_readers_gates_content_by_permission_and_retains_acknowledgement()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s9wpc-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var client = Client(host);
            var author = Guid.NewGuid(); var publisher = Guid.NewGuid(); var reader = Guid.NewGuid();
            S9Actor(host.Services, author, "Human", "document.isps.read"); await Token(client);
            var staged = await StagePng(client);
            using (var uploaded = await PutPng(client, staged.Id)) Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            await AssertCode(await CdSend(client, CdPath, new { code = "SYN-" + Guid.NewGuid().ToString("N")[..10], title = "FICTIONAL ship security plan", classification = "INTERNAL", ispsRestricted = true }, null),
                HttpStatusCode.BadRequest, "isps_classification_required");
            var created = await CdOk(await CdSend(client, CdPath, new { code = "SYN-" + Guid.NewGuid().ToString("N")[..10], title = "FICTIONAL ship security plan", classification = "RESTRICTED", ispsRestricted = true }, null));
            Assert.True(created.IspsRestricted);
            var registered = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions", new { fileId = staged.Id }, created.Version));
            var released = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/release", new { }, registered.Version));
            // Generic vessel-file access cannot bypass the controlled-document policy.
            await AssertCode(await client.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{staged.Id}/access", null, Ct), HttpStatusCode.Forbidden, "controlled_document_download_policy");
            Assert.Contains((await client.GetFromJsonAsync<FileListDto>($"/api/v1/vessels/{SeedVessel.Id}/files", Ct))!.Items, x => x.Id == staged.Id);
            // QA B1 (ISPS Code A/9.7): a file.read holder without document.isps.read cannot see or probe the SSP file on the
            // generic routes: list omits it, detail/access/bytes answer exactly like a non-existent file (no oracle).
            S9Actor(host.Services, Guid.NewGuid(), "Human"); await Token(client);
            Assert.DoesNotContain((await client.GetFromJsonAsync<FileListDto>($"/api/v1/vessels/{SeedVessel.Id}/files", Ct))!.Items, x => x.Id == staged.Id);
            foreach (var probe in new[] { staged.Id, Guid.NewGuid() })
            {
                await AssertCode(await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{probe}", Ct), HttpStatusCode.NotFound, "resource_not_found");
                await AssertCode(await client.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{probe}/access", null, Ct), HttpStatusCode.NotFound, "resource_not_found");
                using var bytes = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vessels/{SeedVessel.Id}/files/{probe}/bytes");
                bytes.Headers.Add("X-File-Capability", "probe");
                await AssertCode(await client.SendAsync(bytes, Ct), HttpStatusCode.NotFound, "resource_not_found");
            }
            // With document.isps.read the same actor sees the stage again (the controlled-document policy still gates bytes).
            S9Actor(host.Services, Guid.NewGuid(), "Human", "document.isps.read"); await Token(client);
            Assert.Contains((await client.GetFromJsonAsync<FileListDto>($"/api/v1/vessels/{SeedVessel.Id}/files", Ct))!.Items, x => x.Id == staged.Id);
            Assert.Equal(staged.Id, (await client.GetFromJsonAsync<FileDto>($"/api/v1/vessels/{SeedVessel.Id}/files/{staged.Id}", Ct))!.Id);
            S9Actor(host.Services, publisher, "Human", "document.publish", "document.isps.read"); await Token(client);
            var published = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/publish", new { }, released.Version));
            var hash = published.Revisions[0].ContentSha256;

            // Without document.isps.read the document is indistinguishable from a missing one.
            S9Actor(host.Services, reader, "Human", "document.acknowledge", "document.restricted.download"); await Token(client);
            var list = (await client.GetFromJsonAsync<ControlledDocumentListDto>($"/api/v1/{CdPath}", Ct))!;
            Assert.DoesNotContain(list.Items, x => x.Id == created.Id);
            using (var hidden = await client.GetAsync($"/api/v1/{CdPath}/{created.Id}", Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/acknowledgements", new { displayedContentSha256 = hash, statementAccepted = true }, published.Version + 7),
                HttpStatusCode.NotFound, "resource_not_found");

            // ISPS reader without the restricted-content permission: metadata only, no bytes, no acknowledgement.
            S9Actor(host.Services, reader, "Human", "document.acknowledge", "document.isps.read"); await Token(client);
            var detail = (await client.GetFromJsonAsync<ControlledDocumentDto>($"/api/v1/{CdPath}/{created.Id}", Ct))!;
            Assert.Equal(("DENIED_RESTRICTED", "CONTROLLED_COPY_RENDERER_UNAVAILABLE", false), (detail.ContentPolicy!.Download, detail.ContentPolicy.Print, detail.Revisions[0].CanAcknowledge));
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/content-access", new { purpose = "VIEW" }, null), HttpStatusCode.Forbidden, "document_download_restricted");
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/acknowledgements", new { displayedContentSha256 = hash, statementAccepted = true }, detail.Version),
                HttpStatusCode.Forbidden, "content_access_required");

            // Permitted reader: attributed VIEW record + short-lived capability; the bytes route re-checks the policy.
            S9Actor(host.Services, reader, "Human", "document.acknowledge", "document.isps.read", "document.restricted.download"); await Token(client);
            ControlledDocumentContentAccessDto access;
            using (var granted = await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/content-access", new { purpose = "VIEW" }, null))
            {
                Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
                access = (await granted.Content.ReadFromJsonAsync<ControlledDocumentContentAccessDto>(Ct))!;
            }
            Assert.Equal(("RESTRICTED", "VIEW", "RESTRICTED_EXPLICIT_PERMISSION", hash), (access.Classification, access.Purpose, access.PolicyBasis, access.ContentSha256));
            Assert.Contains("UNCONTROLLED WHEN PRINTED", access.Attribution);
            using (var bytes = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vessels/{SeedVessel.Id}/files/{access.FileId}/bytes"))
            {
                bytes.Headers.Add("X-File-Capability", access.Capability);
                using var response = await client.SendAsync(bytes, Ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(Ct));
            }
            // Revoking the permission blocks the stream even with a still-valid capability.
            S9Actor(host.Services, reader, "Human", "document.acknowledge", "document.isps.read"); await Token(client);
            using (var bytes = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vessels/{SeedVessel.Id}/files/{access.FileId}/bytes"))
            {
                bytes.Headers.Add("X-File-Capability", access.Capability);
                using var response = await client.SendAsync(bytes, Ct);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            // Acknowledgement: exact displayed hash of the EFFECTIVE revision, once per reader.
            S9Actor(host.Services, reader, "Human", "document.acknowledge", "document.isps.read", "document.restricted.download"); await Token(client);
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/acknowledgements", new { displayedContentSha256 = new string('0', 64), statementAccepted = true }, detail.Version),
                HttpStatusCode.Conflict, "acknowledgement_revision_stale");
            var acknowledged = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/acknowledgements", new { displayedContentSha256 = hash, statementAccepted = true }, detail.Version));
            Assert.NotNull(acknowledged.Revisions[0].AcknowledgedByMeAt);
            Assert.Equal(reader, Assert.Single(acknowledged.Acknowledgements!).ActorId);
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/acknowledgements", new { displayedContentSha256 = hash, statementAccepted = true }, acknowledged.Version),
                HttpStatusCode.Conflict, "already_acknowledged");
            await using var db = Db();
            Assert.Equal(1, await db.ControlledDocumentAcknowledgements.CountAsync(x => x.DocumentId == created.Id, Ct));
            var record = Assert.Single(await db.ControlledDocumentAccessRecords.Where(x => x.DocumentId == created.Id).ToArrayAsync(Ct));
            Assert.Equal((reader, "VIEW"), (record.ActorId, record.Purpose));
            Assert.DoesNotContain(await db.ControlledDocumentReceipts.Where(x => x.DocumentId == created.Id).Select(x => x.OutcomeJson).ToArrayAsync(Ct),
                o => o.Contains(access.Capability, StringComparison.Ordinal));
            Assert.DoesNotContain(await db.AuditEvents.Where(x => x.ResourceId == created.Id).ToArrayAsync(Ct), a => (a.ChangeSummary ?? "").Contains("UNCONTROLLED", StringComparison.Ordinal));
            File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + staged.Id.ToString("N") + ".object"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Requirement", "DEV-062")]
    public async Task Voice_draft_fixture_is_opt_in_idempotent_fictional_and_seeds_no_confirmation()
    {
        await using var db = new PoseidonDbContext(Poseidon.Persistence.Seeding.SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct))
        {
            await Poseidon.Persistence.Seeding.VoiceEventDraftFixtures.AddMissingAsync(db, seed.Vessels, Ct);
            await Poseidon.Persistence.Seeding.VoiceEventDraftFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        var ids = seed.Vessels.Select(v => Poseidon.Persistence.Seeding.VoiceEventDraftFixtures.DraftId(v.Id)).ToArray();
        var rows = await db.VoiceEventDrafts.Where(d => ids.Contains(d.Id)).ToArrayAsync(Ct);
        Assert.Equal(ids.Length, rows.Length);
        Assert.All(rows, d => Assert.Equal(("DRAFT", "SyntheticUnverified", "TYPED", (Guid?)null, "NOT_RETAINED"), (d.State, d.Provenance, d.TranscriptOrigin, d.ConfirmedBy, d.AudioRetention)));
        Assert.Equal(ids.Length, await db.VoiceEventDraftRevisions.CountAsync(r => ids.Contains(r.DraftId), Ct));
        Assert.Empty(await db.VoiceEventDraftReceipts.Where(r => ids.Contains(r.ResourceId)).ToArrayAsync(Ct));
    }
}