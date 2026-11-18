using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// MAINTENANCE-OBSERVATION-OWNER-RECOVERY-01 AC2/3/4;
// DEV-014/015/019/053, FR46-maintenance-plan-meter_source / TC46-maintenance-plan-meter_source.
// Actual API/antiforgery and owned PostgreSQL; configured sources below are synthetic fixtures.
// Capture replays by UUID/body, not actor-scoped CommandReceipts. Revocation has its own row.
// AUTHORED ONLY: all new-package execution is DEFERRED to the technical bundle gate.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions ObservationRecoveryJson = new(JsonSerializerDefaults.Web);

    private static string ObservationRecoveryBody(MaintenanceObservationCommand command) =>
        JsonSerializer.Serialize(command, ObservationRecoveryJson);

    private async Task<HttpResponseMessage> ObservationRecoverySend(HttpClient client, string path, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        // This endpoint has no Idempotency-Key/CommandReceipt contract.
        return await client.SendAsync(request, Ct);
    }

    private async Task ObservationRecoveryRefreshOwner(HttpClient client)
    {
        // Obtain a real request token from the same actual host and cookie context.
        // Do not synthesize a token or print any session/cookie/provider fields.
        using var response = await client.GetAsync("/api/v1/auth/session", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!;
        Assert.True(current.Authenticated);
        Assert.Equal(actorId.ToString("D"), current.ActorId);
        Assert.Equal(SeedVessel.TenantId, current.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(current.CsrfToken));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", current.CsrfToken);
    }

    private async Task ObservationRecoveryCaptureResult(HttpResponseMessage response, Guid id, bool replayed)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, result.GetProperty("id").GetGuid());
        Assert.Equal(replayed, result.GetProperty("replayed").GetBoolean());
        Assert.Equal(2, result.EnumerateObject().Count());
    }

    private async Task ObservationRecoveryRevokeResult(HttpResponseMessage response, Guid id)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(id, result.GetProperty("id").GetGuid());
        Assert.Equal("Revoked", result.GetProperty("state").GetString());
        Assert.Equal(2, result.EnumerateObject().Count());
    }

    private async Task ObservationRecoveryDenied(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.False(error.TryGetProperty("items", out _));
        Assert.False(error.TryGetProperty("sources", out _));
        Assert.False(error.TryGetProperty("id", out _));
        Assert.False(error.TryGetProperty("replayed", out _));
    }

    private async Task<(byte[] Observation, byte[] Audit)> ObservationRecoveryCaptureState(Guid id)
    {
        await using var db = Db();
        Assert.Equal(1, await db.MaintenanceObservations.CountAsync(x => x.Id == id, Ct));
        var row = await db.MaintenanceObservations.AsNoTracking().SingleAsync(x => x.Id == id, Ct);
        Assert.Equal(SeedVessel.TenantId, row.TenantId);
        Assert.Equal(SeedVessel.Id, row.VesselId);
        Assert.Equal(SeedVessel.Id, row.AssetId);
        Assert.Equal("UserDeclaredUnverified", row.Provenance);
        var audits = db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" &&
            x.EntityType == nameof(MaintenanceObservation) && x.ResourceId == id);
        Assert.Equal(1, await audits.CountAsync(Ct));
        var audit = await audits.SingleAsync(Ct);
        Assert.Equal("Added", audit.Action);
        Assert.Equal(actorId.ToString("D"), audit.ActorId);
        Assert.Equal("User", audit.ActorType);
        Assert.Equal(SeedVessel.TenantId, audit.TenantId);
        Assert.Equal(SeedVessel.Id, audit.VesselId);
        Assert.Null(audit.AfterRevision); Assert.Null(audit.AfterVersion);
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == id, Ct));
        // Keep private values only in memory; failed equality emits no raw row/audit body.
        return (JsonSerializer.SerializeToUtf8Bytes(row), JsonSerializer.SerializeToUtf8Bytes(audit));
    }

    [Theory]
    [InlineData("observation-write")]
    [InlineData("maintenance-write")]
    [InlineData("policy-missing")]
    [InlineData("policy-version")]
    public async Task Observation_capture_retains_exact_UUID_body_through_live_denial_and_fresh_owner_CSRF_recovery(string denial)
    {
        await using var host = ObservationHost();
        using var client = Client(host); await ObservationRecoveryRefreshOwner(client);
        var command = Reading("19.5000") with { SourceRevision = 73 };
        var body = ObservationRecoveryBody(command); // Freeze once; retries use these exact serialized bytes.
        using var committed = await ObservationRecoverySend(client, ObservationPath, body);
        await ObservationRecoveryCaptureResult(committed, command.Id, false);
        var original = await ObservationRecoveryCaptureState(command.Id);
        var memberships = host.Services.GetRequiredService<FederationSettings>();
        var originalMemberships = memberships.Memberships;
        var policies = host.Services.GetRequiredService<MaintenanceObservationSettings>();
        var originalPolicies = policies.Sources;
        try
        {
            if (denial.EndsWith("-write", StringComparison.Ordinal))
            {
                var permission = denial == "observation-write" ? "maintenance.observation.write" : "maintenance.write";
                memberships.Memberships = [originalMemberships[0] with {
                    Permissions = originalMemberships[0].Permissions.Where(p => p != permission).ToArray()
                }];
            }
            else if (denial == "policy-missing") policies.Sources = [];
            else policies.Sources = originalPolicies.Select(rule => rule with { Version = "synthetic-changed-policy" }).ToArray();

            // Revoked write still allows the authorized current read; policy withdrawal
            // reports its absence rather than erasing the previously saved declaration.
            using var current = await client.GetAsync("/api/v1/" + ObservationPath, Ct);
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            var projection = await current.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Contains(projection.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == command.Id);
            if (denial.EndsWith("-write", StringComparison.Ordinal)) Assert.False(projection.GetProperty("canWrite").GetBoolean());
            if (denial == "policy-missing") Assert.Equal("PolicyUnavailable", projection.GetProperty("state").GetString());
            using var denied = await ObservationRecoverySend(client, ObservationPath, body);
            await ObservationRecoveryDenied(denied,
                denial.EndsWith("-write", StringComparison.Ordinal) ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
                denial.EndsWith("-write", StringComparison.Ordinal) ? "resource_not_found" : "observation_policy_denied");
        }
        finally { memberships.Memberships = originalMemberships; policies.Sources = originalPolicies; }

        // A committed capture cannot bypass current CSRF validation either.
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await ObservationRecoverySend(client, ObservationPath, body);
        await ObservationRecoveryDenied(missingCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
        await ObservationRecoveryRefreshOwner(client);
        using var recovered = await ObservationRecoverySend(client, ObservationPath, body);
        await ObservationRecoveryCaptureResult(recovered, command.Id, true);
        using var changed = await ObservationRecoverySend(client, ObservationPath,
            ObservationRecoveryBody(command with { Value = "20.5000" }));
        await ObservationRecoveryDenied(changed, HttpStatusCode.Conflict, "command_reuse_conflict");
        var retained = await ObservationRecoveryCaptureState(command.Id);
        Assert.True(original.Observation.SequenceEqual(retained.Observation), "Capture recovery must retain the original stored observation.");
        Assert.True(original.Audit.SequenceEqual(retained.Audit), "Capture recovery must retain the single original business audit.");
        await using var db = Db();
        Assert.False(await db.MaintenanceObservationRevocations.AnyAsync(x => x.ObservationId == command.Id, Ct));
    }

    [Fact]
    public async Task Observation_revoke_permission_denial_then_same_owner_reauthorization_retains_one_revocation_and_business_audit()
    {
        await using var host = ObservationHost();
        using var client = Client(host); await ObservationRecoveryRefreshOwner(client);
        var command = Reading() with { SourceRevision = 79 };
        using var captured = await ObservationRecoverySend(client, ObservationPath, ObservationRecoveryBody(command));
        await ObservationRecoveryCaptureResult(captured, command.Id, false);
        var originalCapture = await ObservationRecoveryCaptureState(command.Id);
        var path = ObservationPath + $"/{command.Id:D}/revoke";
        const string body = "{}";
        using var first = await ObservationRecoverySend(client, path, body);
        await ObservationRecoveryRevokeResult(first, command.Id);
        byte[] originalRevocation, originalAudit;
        await using (var db = Db())
        {
            originalRevocation = JsonSerializer.SerializeToUtf8Bytes(await db.MaintenanceObservationRevocations.AsNoTracking()
                .SingleAsync(x => x.ObservationId == command.Id, Ct));
            originalAudit = JsonSerializer.SerializeToUtf8Bytes(await db.AuditEvents.AsNoTracking().SingleAsync(x =>
                x.Source == "WebCommand" && x.EntityType == nameof(MaintenanceObservationRevocation) && x.ResourceId == command.Id, Ct));
        }
        var memberships = host.Services.GetRequiredService<FederationSettings>();
        var originalMemberships = memberships.Memberships;
        try
        {
            memberships.Memberships = [originalMemberships[0] with {
                Permissions = originalMemberships[0].Permissions.Where(p => p != "maintenance.observation.write").ToArray()
            }];
            using var denied = await ObservationRecoverySend(client, path, body);
            await ObservationRecoveryDenied(denied, HttpStatusCode.NotFound, "resource_not_found");
        }
        finally { memberships.Memberships = originalMemberships; }
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var missingCsrf = await ObservationRecoverySend(client, path, body);
        await ObservationRecoveryDenied(missingCsrf, HttpStatusCode.BadRequest, "csrf_invalid");
        await ObservationRecoveryRefreshOwner(client);
        using var retry = await ObservationRecoverySend(client, path, body);
        await ObservationRecoveryRevokeResult(retry, command.Id);
        var retainedCapture = await ObservationRecoveryCaptureState(command.Id);
        Assert.True(originalCapture.Observation.SequenceEqual(retainedCapture.Observation));
        Assert.True(originalCapture.Audit.SequenceEqual(retainedCapture.Audit));
        await using (var db = Db())
        {
            Assert.Equal(1, await db.MaintenanceObservationRevocations.CountAsync(x => x.ObservationId == command.Id, Ct));
            var row = await db.MaintenanceObservationRevocations.AsNoTracking().SingleAsync(x => x.ObservationId == command.Id, Ct);
            Assert.Equal(SeedVessel.TenantId, row.TenantId);
            Assert.True(originalRevocation.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(row)));
            var audits = db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" &&
                x.EntityType == nameof(MaintenanceObservationRevocation) && x.ResourceId == command.Id);
            Assert.Equal(1, await audits.CountAsync(Ct));
            var audit = await audits.SingleAsync(Ct);
            Assert.Equal(actorId.ToString("D"), audit.ActorId);
            Assert.Equal(SeedVessel.Id, audit.VesselId);
            Assert.Equal("Added", audit.Action);
            Assert.Null(audit.AfterRevision); Assert.Null(audit.AfterVersion);
            Assert.True(originalAudit.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(audit)));
        }
        using var read = await client.GetAsync("/api/v1/" + ObservationPath, Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var projection = await read.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var retained = projection.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == command.Id);
        Assert.True(retained.GetProperty("revoked").GetBoolean());
    }

    [Fact]
    public async Task Observation_existing_UUID_cannot_project_or_mutate_through_another_asset_or_foreign_tenant()
    {
        await using var host = ObservationHost();
        using var client = Client(host); await ObservationRecoveryRefreshOwner(client);
        var command = Reading() with { SourceRevision = 83 };
        var body = ObservationRecoveryBody(command);
        using var captured = await ObservationRecoverySend(client, ObservationPath, body);
        await ObservationRecoveryCaptureResult(captured, command.Id, false);
        var originalCapture = await ObservationRecoveryCaptureState(command.Id);
        var otherAsset = NewEquipment();
        using var createdAsset = await Send(client, EquipmentUrl, otherAsset);
        Assert.Equal(HttpStatusCode.OK, createdAsset.StatusCode);
        var otherPath = $"equipment-hierarchy/{SeedVessel.Id:D}/{otherAsset.Id:D}/observations";
        var policies = host.Services.GetRequiredService<MaintenanceObservationSettings>();
        var originalPolicies = policies.Sources;
        var memberships = host.Services.GetRequiredService<FederationSettings>();
        var originalMemberships = memberships.Memberships;
        try
        {
            // A second explicitly configured synthetic fixture source ensures that
            // the conflict is the persisted observation's asset binding, not a missing rule.
            var rule = originalPolicies.Single(r => r.TenantId == SeedVessel.TenantId && r.VesselId == SeedVessel.Id &&
                r.AssetId == SeedVessel.Id && r.Source == command.Source);
            policies.Sources = [.. originalPolicies, rule with { AssetId = otherAsset.Id }];
            using var otherRead = await client.GetAsync("/api/v1/" + otherPath, Ct);
            Assert.Equal(HttpStatusCode.OK, otherRead.StatusCode);
            var empty = await otherRead.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(0, empty.GetProperty("items").GetArrayLength());
            using var rebound = await ObservationRecoverySend(client, otherPath, body);
            await ObservationRecoveryDenied(rebound, HttpStatusCode.Conflict, "command_reuse_conflict");
            using var wrongRevoke = await ObservationRecoverySend(client, otherPath + $"/{command.Id:D}/revoke", "{}");
            await ObservationRecoveryDenied(wrongRevoke, HttpStatusCode.NotFound, "resource_not_found");

            memberships.Memberships = [originalMemberships[0] with { TenantId = seed.Tenants[1].Id }];
            await Token(client); // Actual current foreign-tenant session, never a caller tenant header.
            using var foreignRead = await client.GetAsync("/api/v1/" + ObservationPath, Ct);
            await ObservationRecoveryDenied(foreignRead, HttpStatusCode.NotFound, "resource_not_found");
            using var foreignCapture = await ObservationRecoverySend(client, ObservationPath, body);
            await ObservationRecoveryDenied(foreignCapture, HttpStatusCode.NotFound, "resource_not_found");
            using var foreignRevoke = await ObservationRecoverySend(client, ObservationPath + $"/{command.Id:D}/revoke", "{}");
            await ObservationRecoveryDenied(foreignRevoke, HttpStatusCode.NotFound, "resource_not_found");
            await using var foreign = Db(seed.Tenants[1].Id);
            Assert.False(await foreign.MaintenanceObservations.AnyAsync(x => x.Id == command.Id, Ct));
            Assert.False(await foreign.MaintenanceObservationRevocations.AnyAsync(x => x.ObservationId == command.Id, Ct));
        }
        finally { policies.Sources = originalPolicies; memberships.Memberships = originalMemberships; }
        await ObservationRecoveryRefreshOwner(client);
        using var recovered = await ObservationRecoverySend(client, ObservationPath, body);
        await ObservationRecoveryCaptureResult(recovered, command.Id, true);
        var retained = await ObservationRecoveryCaptureState(command.Id);
        Assert.True(originalCapture.Observation.SequenceEqual(retained.Observation));
        Assert.True(originalCapture.Audit.SequenceEqual(retained.Audit));
        await using var db = Db();
        Assert.False(await db.MaintenanceObservations.AnyAsync(x => x.AssetId == otherAsset.Id, Ct));
        Assert.False(await db.MaintenanceObservationRevocations.AnyAsync(x => x.ObservationId == command.Id, Ct));
    }
}
