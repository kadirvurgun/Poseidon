using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5802 P5802-AC-01..10: actual host, antiforgery, secure-file transport with the
// clamd protocol peer (not scanner deployment proof) and real PostgreSQL.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] CdBase = ["directory.read", "directory.write", "file.read", "file.write", "audit.read", "mode.tug", "mode.cross"];
    private string CdPath => $"vessels/{SeedVessel.Id}/controlled-documents";

    private void CdActor(IServiceProvider services, Guid id, params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { ActorId = id, Permissions = [.. CdBase, .. extra] };
    }

    private async Task<HttpResponseMessage> CdSend(HttpClient client, string path, object body, uint? version, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        if (version is uint v) message.Headers.TryAddWithoutValidation("If-Match", $"\"{v}\"");
        return await client.SendAsync(message, Ct);
    }

    private async Task<ControlledDocumentDto> CdOk(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, actual {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            return (await response.Content.ReadFromJsonAsync<ControlledDocumentDto>(Ct))!;
        }
    }

    private static async Task CdDenied(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Contains(code, await response.Content.ReadAsStringAsync());
        }
    }

    private async Task<ControlledDocumentDto> CdCreate(HttpClient client, string? key = null) =>
        await CdOk(await CdSend(client, CdPath, new { code = "SYN-" + Guid.NewGuid().ToString("N")[..10], title = "FICTIONAL controlled procedure", classification = "INTERNAL" }, null, key));

    [Fact]
    [Trait("Requirement", "PLAN-1511-5802")]
    public async Task Upload_is_never_verified_and_self_verification_is_denied_while_independent_steps_stay_separate()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-cd-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var client = Client(host); await Token(client);
            CdActor(host.Services, actorId);
            var staged = await StagePng(client);
            FileDto clean;
            using (var uploaded = await PutPng(client, staged.Id)) { Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); clean = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!; }
            var createKey = Guid.NewGuid().ToString();
            var createBody = new { code = "SYN-" + Guid.NewGuid().ToString("N")[..10], title = "FICTIONAL controlled procedure", classification = "INTERNAL" };
            var created = await CdOk(await CdSend(client, CdPath, createBody, null, createKey));
            Assert.Equal(("NONE", "UNCONFIGURED", "NOT_STARTED", 0L), (created.DisposalState, created.RetentionState, created.PhysicalDeletion, created.LatestRevision));
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions", new { fileId = staged.Id }, null), (HttpStatusCode)428, "precondition_required");
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions", new { fileId = staged.Id }, created.Version + 1), HttpStatusCode.PreconditionFailed, "version_conflict");
            var registered = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions", new { fileId = staged.Id }, created.Version));
            var revision = Assert.Single(registered.Revisions);
            // Clean transport + registration is still quarantined, unpublished and UNVERIFIED.
            Assert.Equal(("QUARANTINED", "UNPUBLISHED", "UNVERIFIED", actorId, actorId), (revision.IntakeState, revision.PublicationState, revision.VerificationState, revision.UploadedBy, revision.RegisteredBy));
            Assert.Equal(clean.Checksum, revision.ContentSha256);
            var released = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/release", new { }, registered.Version));
            Assert.Equal(("RELEASED", "UNPUBLISHED", "UNVERIFIED"), (released.Revisions[0].IntakeState, released.Revisions[0].PublicationState, released.Revisions[0].VerificationState));
            Assert.Contains(released.Custody, c => c.EventType == "SCANNED_CLEAN" && c.Source == "SECURE_FILE_PIPELINE" && c.SubjectActorId == actorId);

            // Uploader/registrar holding evidence.verify and document.publish is still not independent.
            CdActor(host.Services, actorId, "evidence.verify", "document.publish");
            var verification = new { method = "MANUAL_SOURCE_COMPARISON", result = "VALID", basis = "FICTIONAL comparison with issuing register entry" };
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/verifications", verification, released.Version), HttpStatusCode.Forbidden, "separation_of_duties");
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/publish", new { }, released.Version), HttpStatusCode.Forbidden, "separation_of_duties");
            await using (var db = Db()) Assert.False(await db.ControlledDocumentVerifications.AnyAsync(x => x.DocumentId == created.Id, Ct));

            // Verifier without the exact action key is denied before any effect.
            var verifierId = Guid.NewGuid();
            CdActor(host.Services, verifierId);
            await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/verifications", verification, released.Version), HttpStatusCode.Forbidden, "permission_denied");
            CdActor(host.Services, verifierId, "evidence.verify", "document.publish");
            var verified = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/verifications", verification, released.Version));
            Assert.Equal(("VALID", "UNPUBLISHED"), (verified.Revisions[0].VerificationState, verified.Revisions[0].PublicationState));
            Assert.Equal(verifierId, Assert.Single(verified.Revisions[0].Verifications).VerifierId);
            var published = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/revisions/1/publish", new { }, verified.Version));
            Assert.Equal(("EFFECTIVE", 1L), (published.Revisions[0].PublicationState, published.CurrentRevision!.Value));
            Assert.Equal(["CREATED", "RECEIVED", "QUARANTINED", "SCANNED_CLEAN", "RELEASED", "VERIFICATION_RECORDED", "PUBLISHED"], published.Custody.Select(x => x.EventType));

            // Exact replay returns the original outcome; a different body under the same key conflicts.
            CdActor(host.Services, actorId);
            var exact = await CdOk(await CdSend(client, CdPath, createBody, null, createKey));
            Assert.Equal((created.Id, created.Version, 0L), (exact.Id, exact.Version, exact.LatestRevision)); // original outcome, not current head
            using (var replay = await CdSend(client, CdPath, new { code = "SYN-REPLAY", title = "x", classification = "INTERNAL" }, null, createKey))
                Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
            await using (var db = Db())
            {
                var audits = await db.AuditEvents.Where(x => x.ResourceId == created.Id).ToArrayAsync(Ct);
                Assert.All(audits, x => Assert.Equal(SeedVessel.Id, x.VesselId));
                Assert.Contains(audits, x => x.EntityType == nameof(ControlledDocumentVerification) && x.ActorId == verifierId.ToString());
                Assert.DoesNotContain(audits, x => (x.ChangeSummary ?? "").Contains("FICTIONAL comparison", StringComparison.Ordinal));
                Assert.Equal(7, await db.ControlledDocumentCustodyEvents.CountAsync(x => x.DocumentId == created.Id, Ct));
            }
            File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + staged.Id.ToString("N") + ".object"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5802")]
    public async Task Legal_hold_blocks_disposal_and_hold_release_and_disposal_approval_need_another_actor()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var records = Guid.NewGuid(); var approver = Guid.NewGuid();
        CdActor(host.Services, records, "record.hold", "record.retention.manage", "record.purge.request");
        var created = await CdCreate(client);
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests", new { reason = "end" }, created.Version), HttpStatusCode.Conflict, "retention_unconfigured");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // QA F5 LEAD decision: a back-dated retain-until is rejected at set time.
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/retention", new { policyRef = "SYN-POLICY-1", retainUntil = today.AddDays(-1) }, created.Version), HttpStatusCode.BadRequest, "invalid_request");
        var retained = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/retention", new { policyRef = "SYN-POLICY-1", retainUntil = today }, created.Version));
        Assert.Equal("ELIGIBLE", retained.RetentionState);
        var held = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/hold", new { authorityRef = "SYN-CASE-1", reason = "FICTIONAL litigation hold" }, retained.Version));
        Assert.True(held.HoldActive);
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests", new { reason = "end" }, held.Version), HttpStatusCode.Conflict, "legal_hold_active");
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/hold/release", new { reason = "self release" }, held.Version), HttpStatusCode.Forbidden, "separation_of_duties");

        CdActor(host.Services, approver, "record.hold", "record.purge.approve");
        var unheld = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/hold/release", new { reason = "FICTIONAL case closed" }, held.Version));
        CdActor(host.Services, records, "record.hold", "record.retention.manage", "record.purge.request");
        var requested = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests", new { reason = "retention elapsed" }, unheld.Version));
        Assert.Equal("REQUESTED", requested.DisposalState);
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests/approve", new { reason = "self" }, requested.Version), HttpStatusCode.Forbidden, "permission_denied");
        // QA F2 / 58.02 §100: a hold placed while the request is pending invalidates the request.
        var lateHold = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/hold", new { authorityRef = "SYN-CASE-2", reason = "late hold" }, requested.Version));
        Assert.Equal(("NONE", "DISPOSAL_REQUEST_INVALIDATED"), (lateHold.DisposalState, lateHold.Custody[^1].EventType));
        CdActor(host.Services, approver, "record.hold", "record.purge.approve");
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests/approve", new { reason = "approve" }, lateHold.Version), HttpStatusCode.Conflict, "invalid_state");
        var released = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/hold/release", new { reason = "late case closed" }, lateHold.Version));
        CdActor(host.Services, records, "record.hold", "record.retention.manage", "record.purge.request");
        var again = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests", new { reason = "retention elapsed again" }, released.Version));
        CdActor(host.Services, approver, "record.hold", "record.purge.approve");
        var approved = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests/approve", new { reason = "approve" }, again.Version));
        // A recorded disposal decision is not physical deletion.
        Assert.Equal(("APPROVED", "NOT_STARTED"), (approved.DisposalState, approved.PhysicalDeletion));
        await using var db = Db();
        var row = await db.ControlledDocuments.AsNoTracking().SingleAsync(x => x.Id == created.Id, Ct);
        Assert.Equal((records, approver), (row.DisposalRequestedBy!.Value, row.DisposalApprovedBy!.Value));
        Assert.Equal((false, records, approver), (row.HoldActive, row.HoldPlacedBy!.Value, row.HoldReleasedBy!.Value));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5802")]
    public async Task Cross_tenant_and_cross_vessel_reads_and_commands_are_not_found_and_links_need_domain_read()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        CdActor(host.Services, actorId);
        var created = await CdCreate(client);
        var other = seed.Vessels.First(x => x.Id != SeedVessel.Id && x.TenantId == SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{other.Id}/controlled-documents/{created.Id}", Ct)).StatusCode);
        // The document exists but belongs to another vessel the actor can also see: not found, no effect.
        await CdDenied(await CdSend(client, $"vessels/{other.Id}/controlled-documents/{created.Id}/revisions", new { fileId = Guid.NewGuid() }, created.Version), HttpStatusCode.NotFound, "resource_not_found");
        var evidence = ControlEvidenceFixtures.Id(SeedVessel.Id, "Evidence");
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/links", new { targetKind = "ControlEvidence", targetId = evidence }, created.Version), HttpStatusCode.Forbidden, "permission_denied");
        CdActor(host.Services, actorId, "evidence.read");
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/links", new { targetKind = "ControlEvidence", targetId = ControlEvidenceFixtures.Id(other.Id, "Evidence") }, created.Version), HttpStatusCode.Conflict, "linked_resource_unavailable");
        var linked = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/links", new { targetKind = "ControlEvidence", targetId = evidence }, created.Version));
        Assert.Equal(("ControlEvidence", evidence), (Assert.Single(linked.Links).TargetKind, linked.Links[0].TargetId));
        Assert.Equal(["CREATED", "LINKED"], linked.Custody.Select(x => x.EventType));
        // QA F2 / §271: a live link keeps active use open, so disposal cannot be requested.
        CdActor(host.Services, actorId, "evidence.read", "record.retention.manage", "record.purge.request");
        var retained = await CdOk(await CdSend(client, $"{CdPath}/{created.Id}/retention", new { policyRef = "SYN-POLICY-1", retainUntil = DateOnly.FromDateTime(DateTime.UtcNow) }, linked.Version));
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/disposal-requests", new { reason = "end" }, retained.Version), HttpStatusCode.Conflict, "active_use_open");
        CdActor(host.Services, actorId);
        using (var hidden = await client.GetAsync($"/api/v1/{CdPath}/{created.Id}", Ct))
        {
            var dto = (await hidden.Content.ReadFromJsonAsync<ControlledDocumentDto>(Ct))!;
            Assert.Empty(dto.Links); // hidden domain links are not disclosed
            // QA F3: the custody list omits the hidden LINKED event without a gap or count.
            Assert.Equal(["CREATED", "RETENTION_SET"], dto.Custody.Select(x => x.EventType));
            Assert.Equal([1L, 2L], dto.Custody.Select(x => x.Position));
        }

        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[0];
        settings.Memberships[0] = original with { TenantId = seed.Tenants[1].Id };
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{CdPath}/{created.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{CdPath}", Ct)).StatusCode);
        await CdDenied(await CdSend(client, $"{CdPath}/{created.Id}/revisions", new { fileId = Guid.NewGuid() }, created.Version), HttpStatusCode.NotFound, "resource_not_found");
        settings.Memberships[0] = original with { Permissions = ["directory.read"] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{CdPath}", Ct)).StatusCode);
        await using var db = Db(seed.Tenants[1].Id);
        Assert.False(await db.ControlledDocuments.AnyAsync(x => x.Id == created.Id, Ct));
    }
}
