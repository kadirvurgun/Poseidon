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

// TECHNICAL-COMMAND-OWNER-RECOVERY-01 AC1/2/3/5; DEV-014/015/019/052/053.
// Actual API host, antiforgery and owned PostgreSQL. Synthetic server membership
// changes exercise authorization, not a client actor header or an invented role.
// AUTHORED; new-package execution is DEFERRED to the technical bundle gate.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly JsonSerializerOptions TechnicalRecoveryJson = new(JsonSerializerDefaults.Web);

    private (Guid Id, string Path, string Body) TechnicalRecoveryCommand(bool plan)
    {
        var id = Guid.NewGuid();
        object command = plan
            ? new MaintenanceCommand(id, null, SeedVessel.Id, null, null,
                new MaintenanceInputs(TriggerType: "Meter", IntervalValue: "0.100000000000000002",
                    Provenance: "SyntheticUnverifiedRecovery"), DateTimeOffset.UnixEpoch)
            : NewEquipment(id) with { Name = "FICTIONAL owner-retained equipment" };
        return (id, plan ? PlansUrl : EquipmentUrl, JsonSerializer.Serialize(command, command.GetType(), TechnicalRecoveryJson));
    }

    private async Task<HttpResponseMessage> TechnicalRecoverySend(HttpClient client, string path, string body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path)
        {
            // Retain the same serialized bytes and UUID/base across all attempts.
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request, Ct);
    }

    private async Task TechnicalRecoveryDenial(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(code, error.GetProperty("code").GetString());
        // Inspect only safe contract flags; never print session/private payloads.
        Assert.False(error.TryGetProperty("replayed", out _));
        Assert.False(error.TryGetProperty("id", out _));
        Assert.False(error.TryGetProperty("items", out _));
    }

    private async Task TechnicalRecoveryCommittedOnce(bool plan, Guid id, string key, TechnicalSaved first)
    {
        await using var db = Db();
        var receipt = Assert.Single(await db.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
        Assert.Equal(actorId, receipt.ActorId);
        Assert.Equal(SeedVessel.TenantId, receipt.TenantId);
        Assert.Equal(id, receipt.ResourceId);
        Assert.Equal(plan ? "MaintenanceSave" : "EquipmentSave", receipt.Action);
        Assert.Equal(first.Revision, receipt.RegisterRevision);
        Assert.Equal(first.Version, receipt.RegisterVersion);
        if (plan)
        {
            var row = await db.MaintenancePlans.SingleAsync(p => p.Id == id, Ct);
            Assert.Equal(first.Revision, row.Revision);
            Assert.Equal(first.Version, row.Version);
            Assert.Single(await db.MaintenancePlanRevisions.Where(r => r.PlanId == id).ToArrayAsync(Ct));
        }
        else
        {
            var row = await db.Assets.SingleAsync(a => a.Id == id, Ct);
            Assert.Equal(first.Revision, row.Revision);
            Assert.Equal(first.Version, row.Version);
            Assert.Single(await db.AssetRevisions.Where(r => r.AssetId == id).ToArrayAsync(Ct));
        }
        var entityType = plan ? nameof(MaintenancePlan) : nameof(Asset);
        var idText = id.ToString();
        var business = Assert.Single(await db.AuditEvents.Where(a => a.EntityType == entityType && a.EntityId.EndsWith(idText)).ToArrayAsync(Ct));
        Assert.Equal("Added", business.Action);
        Assert.Equal(actorId.ToString(), business.ActorId);
        Assert.Equal("User", business.ActorType);
        Assert.Equal(SeedVessel.TenantId, business.TenantId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Technical_exact_receipt_is_not_adopted_by_replacement_actor_or_tenant_and_original_owner_can_recover(bool plan)
    {
        await using var host = Host(technicalRead: true, technicalWrite: true);
        using var client = Client(host); await Token(client);
        var command = TechnicalRecoveryCommand(plan);
        var key = Guid.NewGuid().ToString();
        using var committed = await TechnicalRecoverySend(client, command.Path, command.Body, key);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var first = (await committed.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.Equal(command.Id, first.Id); Assert.False(first.Replayed);

        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[0];
        var replacementActor = Guid.NewGuid();
        try
        {
            settings.Memberships = [original with { ActorId = replacementActor }];
            await Token(client);
            using var otherActor = await TechnicalRecoverySend(client, command.Path, command.Body, key);
            await TechnicalRecoveryDenial(otherActor, HttpStatusCode.Conflict, "version_conflict");
            await using (var db = Db())
                Assert.False(await db.CommandReceipts.AnyAsync(r => r.Key == key && r.ActorId == replacementActor, Ct));

            // Even an identical actor UUID in another tenant cannot read the receipt.
            settings.Memberships = [original with { TenantId = seed.Tenants[1].Id }];
            await Token(client);
            using var otherTenant = await TechnicalRecoverySend(client, command.Path, command.Body, key);
            await TechnicalRecoveryDenial(otherTenant, HttpStatusCode.NotFound, "resource_not_found");
            await using (var db = Db(seed.Tenants[1].Id))
                Assert.False(await db.CommandReceipts.AnyAsync(r => r.Key == key, Ct));
        }
        finally { settings.Memberships = [original]; }

        await Token(client); // A newly issued current-owner CSRF token is allowed.
        using var retry = await TechnicalRecoverySend(client, command.Path, command.Body, key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var recovered = (await retry.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.True(recovered.Replayed);
        Assert.Equal(first with { Replayed = true }, recovered);
        await TechnicalRecoveryCommittedOnce(plan, command.Id, key, first);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Technical_revoked_write_denies_old_receipt_then_reauthorized_owner_replays_original_bytes_without_new_history_or_business_audit(bool plan)
    {
        await using var host = Host(technicalRead: true, technicalWrite: true);
        using var client = Client(host); await Token(client);
        var command = TechnicalRecoveryCommand(plan);
        var key = Guid.NewGuid().ToString();
        using var committed = await TechnicalRecoverySend(client, command.Path, command.Body, key);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var first = (await committed.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[0];
        try
        {
            var revokedPermission = plan ? "maintenance.write" : "equipment.write";
            settings.Memberships = [original with { Permissions = original.Permissions!.Where(p => p != revokedPermission).ToArray() }];
            await Token(client);
            using var denied = await TechnicalRecoverySend(client, command.Path, command.Body, key);
            await TechnicalRecoveryDenial(denied, HttpStatusCode.Forbidden, "permission_denied");
            await TechnicalRecoveryCommittedOnce(plan, command.Id, key, first);
        }
        finally { settings.Memberships = [original]; }

        await Token(client);
        using var retry = await TechnicalRecoverySend(client, command.Path, command.Body, key);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(first with { Replayed = true }, await retry.Content.ReadFromJsonAsync<TechnicalSaved>(Ct));
        // Same key cannot be silently rebased onto a new UUID or changed declaration.
        var changed = JsonSerializer.Deserialize<JsonElement>(command.Body);
        var changedBody = JsonSerializer.Serialize(changed.EnumerateObject().ToDictionary(p => p.Name,
            p => p.Name == "id" ? JsonSerializer.SerializeToElement(Guid.NewGuid()) : p.Value), TechnicalRecoveryJson);
        using var reused = await TechnicalRecoverySend(client, command.Path, changedBody, key);
        await TechnicalRecoveryDenial(reused, HttpStatusCode.Conflict, "idempotency_key_reused");
        await TechnicalRecoveryCommittedOnce(plan, command.Id, key, first);
    }

    [Fact]
    public async Task Technical_job_reauthorization_uses_actual_uuid_body_protocol_without_a_command_receipt()
    {
        await using var host = Host(technicalRead: true, technicalWrite: true);
        using var client = Client(host); await Token(client);
        var command = new MaintenanceJobCommand(Guid.NewGuid(), "FICTIONAL-RECOVERY", "Synthetic retained job");
        var body = JsonSerializer.Serialize(command, TechnicalRecoveryJson);
        var path = PlansUrl + "/jobs";
        // Unlike equipment/plan save, jobs have no Idempotency-Key header contract.
        using var first = await TechnicalRecoverySend(client, path, body, null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var originalResult = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(command.Id, originalResult.GetProperty("id").GetGuid());
        Assert.False(originalResult.TryGetProperty("replayed", out _));

        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[0];
        try
        {
            settings.Memberships = [original with { Permissions = original.Permissions!.Where(p => p != "maintenance.write").ToArray() }];
            await Token(client);
            using var denied = await TechnicalRecoverySend(client, path, body, null);
            await TechnicalRecoveryDenial(denied, HttpStatusCode.Forbidden, "permission_denied");
        }
        finally { settings.Memberships = [original]; }
        await Token(client);
        using var recovered = await TechnicalRecoverySend(client, path, body, null);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var replay = await recovered.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(command.Id, replay.GetProperty("id").GetGuid());
        Assert.False(replay.TryGetProperty("replayed", out _));
        using var changed = await TechnicalRecoverySend(client, path,
            JsonSerializer.Serialize(command with { Title = "Different declaration" }, TechnicalRecoveryJson), null);
        await TechnicalRecoveryDenial(changed, HttpStatusCode.Conflict, "version_conflict");
        await using var db = Db();
        var job = Assert.Single(await db.MaintenanceJobs.Where(j => j.Id == command.Id).ToArrayAsync(Ct));
        Assert.Equal(command.Code, job.Code); Assert.Equal(command.Title, job.Title);
        Assert.False(await db.CommandReceipts.AnyAsync(r => r.ResourceId == command.Id, Ct));
        var idText = command.Id.ToString();
        var business = Assert.Single(await db.AuditEvents.Where(a => a.EntityType == nameof(MaintenanceJob) && a.EntityId.EndsWith(idText)).ToArrayAsync(Ct));
        Assert.Equal("Added", business.Action); Assert.Equal(actorId.ToString(), business.ActorId);
    }
}
