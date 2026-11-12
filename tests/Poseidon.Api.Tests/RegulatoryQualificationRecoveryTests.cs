using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S3-QUALIFICATION-OWNER-RECOVERY-01 QOR-01/02/03/06/08; DEV-026 / T-S3-001.
// Active18.01 Backlog27,15.01 paragraph71,19.01 paragraphs265/267.
// Actual host, antiforgery and owned PostgreSQL. Synthetic DRAFT only: no source
// adoption, human sign-off or operational authority is manufactured here.
// AUTHORED_ONLY: execute at the next frozen larger bundle gate, not while authoring.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions QualificationRecoveryJson = new(JsonSerializerDefaults.Web);

    private async Task<HttpResponseMessage> QualificationRecoveryPost(HttpClient client, Guid rule, string body, string key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/regulatory-source-governance/{rule}/events")
        {
            // A retry must preserve the original serialized body, not reconstruct
            // a command from whichever version/authority the editor now displays.
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        message.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(message, Ct);
    }

    private async Task QualificationRecoveryRefreshSession(HttpClient client, Guid owner, Guid tenant)
    {
        using var response = await client.GetAsync("/api/v1/auth/session", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.True(session.Authenticated);
        Assert.Equal(owner.ToString("D"), session.ActorId);
        Assert.Equal(tenant, session.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
    }

    private async Task QualificationRecoveryDenial(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        // A denial must not disclose the original private receipt/projection.
        foreach (var field in new[] { "content", "authorityHash", "hash", "revision", "items", "outcomeJson" })
            Assert.False(problem.TryGetProperty(field, out _));
    }

    private async Task<byte[]> QualificationRecoveryCommittedState(Guid rule, string key, QualificationDto saved)
    {
        await using var db = Db();
        var head = await db.RegulatoryQualificationHeads.AsNoTracking().SingleAsync(x => x.RuleId == rule, Ct);
        Assert.Equal(saved.Revision, head.Revision);
        Assert.Equal(saved.Version, head.Version);
        var entry = Assert.Single(await db.RegulatoryQualificationEvents.AsNoTracking().Where(x => x.RuleId == rule).ToArrayAsync(Ct));
        Assert.Equal("DRAFT", entry.State);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(saved.Hash, entry.Hash);
        var receipt = Assert.Single(await db.RegulatoryQualificationReceipts.AsNoTracking().Where(x => x.RuleId == rule).ToArrayAsync(Ct));
        Assert.Equal(key, receipt.Key);
        Assert.Equal(actorId, receipt.ActorId);
        Assert.Equal(SeedVessel.TenantId, receipt.TenantId);
        Assert.Equal(saved.AuthorityHash, receipt.AuthorityHash);
        var replay = JsonSerializer.Deserialize<QualificationDto>(receipt.OutcomeJson, QualificationRecoveryJson)!;
        Assert.Equal(saved.Revision, replay.Revision);
        Assert.Equal(saved.Version, replay.Version);
        Assert.Equal(saved.Hash, replay.Hash);

        var ruleText = rule.ToString("D");
        var types = new[] { nameof(RegulatoryQualificationHead), nameof(RegulatoryQualificationEvent), nameof(RegulatoryQualificationReceipt) };
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" &&
            types.Contains(x.EntityType) && x.EntityId.Contains(ruleText)).OrderBy(x => x.Id).ToArrayAsync(Ct);
        foreach (var type in types)
        {
            var audit = Assert.Single(audits, x => x.EntityType == type);
            Assert.Equal("Added", audit.Action);
            Assert.Equal(actorId.ToString("D"), audit.ActorId);
            Assert.Equal("User", audit.ActorType);
            Assert.Equal(SeedVessel.TenantId, audit.TenantId);
        }
        var receiptAudit = Assert.Single(audits, x => x.EntityType == nameof(RegulatoryQualificationReceipt));
        Assert.Equal(saved.Version, receiptAudit.AfterVersion);
        Assert.Equal(saved.Revision, receiptAudit.AfterRevision);
        // Compare in memory with a boolean assertion. No raw row, source payload,
        // session, cookie or audit body is emitted on a failed equality check.
        return JsonSerializer.SerializeToUtf8Bytes(new { head, entry, receipt, audits }, QualificationRecoveryJson);
    }

    [Theory]
    [InlineData("read-permission", "permission_denied")]
    [InlineData("write-permission", "human_authority_required")]
    [InlineData("appointment-missing", "human_authority_required")]
    [InlineData("appointment-expired", "human_authority_required")]
    [InlineData("appointment-replaced", "human_authority_required")]
    [InlineData("appointment-binding", "human_authority_required")]
    [Trait("Recovery", "RegulatoryQualification")]
    public async Task Qualification_committed_draft_survives_denied_exact_retry_and_original_owner_recovers_without_new_revision(string denial, string code)
    {
        await using var host = Host();
        using var client = Client(host);
        GraphPermissionsForTest(host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..GraphPermissions, "rules.write"] };
        await Token(client);
        var (rule, source, _) = await AdmissionInputs(client);
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, actorId);
        var owner = settings.Memberships[0];
        var grant = owner.RegulatoryAuthorities.Single(x => x.VesselId == null);
        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        var command = new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", new(Publisher: "FICTIONAL recovery declaration"));
        var body = JsonSerializer.Serialize(command, QualificationRecoveryJson);
        var key = Guid.NewGuid().ToString("D");
        using var committed = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var originalBytes = await committed.Content.ReadAsByteArrayAsync(Ct);
        var saved = JsonSerializer.Deserialize<QualificationDto>(originalBytes, QualificationRecoveryJson)!;
        var originalState = await QualificationRecoveryCommittedState(rule.Current.Id, key, saved);

        try
        {
            settings.Memberships = [denial switch
            {
                "read-permission" => owner with { Permissions = owner.Permissions.Where(x => x != "regulatory.draft.read").ToArray() },
                "write-permission" => owner with { Permissions = owner.Permissions.Where(x => x != "regulatory.qualify").ToArray() },
                "appointment-missing" => owner with { RegulatoryAuthorities = [] },
                "appointment-expired" => owner with { RegulatoryAuthorities = owner.RegulatoryAuthorities.Select(x => x.Id == grant.Id ? x with { ValidUntil = DateTimeOffset.UtcNow.AddSeconds(-1) } : x).ToArray() },
                "appointment-replaced" => owner with { RegulatoryAuthorities = owner.RegulatoryAuthorities.Select(x => x.Id == grant.Id ? x with { Id = Guid.NewGuid() } : x).ToArray() },
                "appointment-binding" => owner with { RegulatoryAuthorities = owner.RegulatoryAuthorities.Select(x => x.Id == grant.Id ? x with { Contract = x.Contract with { Type = "FICTIONAL_CHANGED_BINDING" } } : x).ToArray() },
                _ => throw new InvalidOperationException("Unknown synthetic denial case")
            }];
            await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
            using var denied = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
            await QualificationRecoveryDenial(denied, HttpStatusCode.Forbidden, code);
        }
        finally { settings.Memberships = [owner]; }

        // Existing committed receipts do not bypass current antiforgery checks.
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        await QualificationRecoveryDenial(missingCsrf, HttpStatusCode.Forbidden, "csrf_invalid");
        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        using var recovered = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(originalBytes.SequenceEqual(await recovered.Content.ReadAsByteArrayAsync(Ct)));
        Assert.True(originalState.SequenceEqual(await QualificationRecoveryCommittedState(rule.Current.Id, key, saved)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Recovery", "RegulatoryQualification")]
    public async Task Qualification_original_receipt_is_not_disclosed_to_replacement_actor_or_tenant(bool changeTenant)
    {
        await using var host = Host();
        using var client = Client(host);
        GraphPermissionsForTest(host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..GraphPermissions, "rules.write"] };
        await Token(client);
        var (rule, source, _) = await AdmissionInputs(client);
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, actorId);
        var owner = settings.Memberships[0];
        var grant = owner.RegulatoryAuthorities.Single(x => x.VesselId == null);
        var command = new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", new());
        var body = JsonSerializer.Serialize(command, QualificationRecoveryJson);
        var key = Guid.NewGuid().ToString("D");
        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        using var committed = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var originalBytes = await committed.Content.ReadAsByteArrayAsync(Ct);
        var saved = JsonSerializer.Deserialize<QualificationDto>(originalBytes, QualificationRecoveryJson)!;
        var originalState = await QualificationRecoveryCommittedState(rule.Current.Id, key, saved);
        var stranger = changeTenant ? owner with { TenantId = seed.Tenants[1].Id } : owner with { ActorId = Guid.NewGuid() };
        try
        {
            settings.Memberships = [stranger];
            await QualificationRecoveryRefreshSession(client, stranger.ActorId, stranger.TenantId);
            using var denied = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
            await QualificationRecoveryDenial(denied,
                changeTenant ? HttpStatusCode.NotFound : HttpStatusCode.Conflict,
                changeTenant ? "resource_not_found" : "version_conflict");
            await using var db = Db(stranger.TenantId);
            Assert.False(await db.RegulatoryQualificationReceipts.AnyAsync(x => x.ActorId == stranger.ActorId && x.Key == key, Ct));
        }
        finally { settings.Memberships = [owner]; }

        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        using var recovered = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(originalBytes.SequenceEqual(await recovered.Content.ReadAsByteArrayAsync(Ct)));
        Assert.True(originalState.SequenceEqual(await QualificationRecoveryCommittedState(rule.Current.Id, key, saved)));
    }

    [Fact]
    [Trait("Recovery", "RegulatoryQualification")]
    public async Task Qualification_exact_receipt_rejects_reconstructed_body_but_original_stale_editor_envelope_still_recovers()
    {
        await using var host = Host();
        using var client = Client(host);
        GraphPermissionsForTest(host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [..GraphPermissions, "rules.write"] };
        await Token(client);
        var (rule, source, _) = await AdmissionInputs(client);
        RegulatoryHuman(host.Services, rule.Current.Id, source.Current.Id, source.Current.Revision, actorId);
        var owner = settings.Memberships[0];
        var grant = owner.RegulatoryAuthorities.Single(x => x.VesselId == null);
        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        var command = new QualificationCommand(0, rule.Current.Revision, grant.Id, "DRAFT", new());
        var body = JsonSerializer.Serialize(command, QualificationRecoveryJson);
        var key = Guid.NewGuid().ToString("D");
        using var committed = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var originalBytes = await committed.Content.ReadAsByteArrayAsync(Ct);
        var saved = JsonSerializer.Deserialize<QualificationDto>(originalBytes, QualificationRecoveryJson)!;
        var originalState = await QualificationRecoveryCommittedState(rule.Current.Id, key, saved);

        // An editor reload must not replace the retained ExpectedVersion/body on
        // a replay, even when the current head now exposes the committed xmin.
        var reconstructed = JsonSerializer.Serialize(command with { ExpectedVersion = saved.Version }, QualificationRecoveryJson);
        using var wrongBody = await QualificationRecoveryPost(client, rule.Current.Id, reconstructed, key);
        await QualificationRecoveryDenial(wrongBody, HttpStatusCode.Conflict, "idempotency_key_reused");
        using var newKey = await QualificationRecoveryPost(client, rule.Current.Id, body, Guid.NewGuid().ToString("D"));
        await QualificationRecoveryDenial(newKey, HttpStatusCode.Conflict, "version_conflict");
        await QualificationRecoveryRefreshSession(client, actorId, owner.TenantId);
        using var recovered = await QualificationRecoveryPost(client, rule.Current.Id, body, key);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(originalBytes.SequenceEqual(await recovered.Content.ReadAsByteArrayAsync(Ct)));
        Assert.True(originalState.SequenceEqual(await QualificationRecoveryCommittedState(rule.Current.Id, key, saved)));
    }
}
