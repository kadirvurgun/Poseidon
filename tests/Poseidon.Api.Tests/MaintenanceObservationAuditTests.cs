using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-016 / T-S1-004: actual HTTP + owned PostgreSQL projection assertions.
// Observations remain UserDeclaredUnverified, never completed or verified maintenance.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Observation_audit_anchors_actual_immutable_asset_owned_record_without_exposing_measurement_or_source()
    {
        await using var host = ObservationHost(); using var client = Client(host); await Token(client);
        var command = Reading("87654321.1250") with { SourceRevision = 37 };
        using var response = await Send(client, ObservationPath, command);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = Db();
        var observation = await db.MaintenanceObservations.AsNoTracking().SingleAsync(x => x.Id == command.Id, Ct);
        Assert.Equal(SeedVessel.TenantId, observation.TenantId); Assert.Equal(SeedVessel.Id, observation.VesselId);
        var asset = await db.Assets.AsNoTracking().SingleAsync(x => x.TenantId == observation.TenantId &&
            x.VesselId == observation.VesselId && x.Id == observation.AssetId, Ct);
        Assert.Equal(SeedVessel.Id, asset.Id);
        Assert.Equal("UserDeclaredUnverified", observation.Provenance);
        Assert.Equal(command.Value, observation.Value); Assert.Equal(command.Source, observation.Source);
        Assert.Equal(command.Unit, observation.Unit); Assert.Equal(command.PolicyVersion, observation.PolicyVersion);
        Assert.Equal(37, observation.SourceRevision);
        var audit = Assert.Single(await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" &&
            x.EntityType == nameof(MaintenanceObservation) && x.ResourceId == observation.Id).ToArrayAsync(Ct));
        Assert.Equal(observation.Id.ToString("D"), audit.EntityId.Split('/').Last());
        Assert.Equal(observation.TenantId, audit.TenantId); Assert.Equal(observation.VesselId, audit.VesselId);
        Assert.Equal("MaintenanceObservation", audit.ResourceKind); Assert.Equal("Added", audit.Action);
        Assert.Equal(actorId.ToString("D"), audit.ActorId); Assert.False(string.IsNullOrWhiteSpace(audit.RoleContext));
        Assert.Equal(Assert.Single(response.Headers.GetValues("X-Correlation-ID")), audit.CorrelationId);
        Assert.Null(audit.BeforeVersion); Assert.Null(audit.AfterVersion);
        Assert.Null(audit.BeforeRevision); Assert.Null(audit.AfterRevision); // Never SourceRevision37 as an invented snapshot.
        using var summary = JsonDocument.Parse(audit.ChangeSummary!);
        var fields = summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!);
        Assert.Equal(new[] { "Binding", "CommandName", "Kind", "ScopeKind", "TechnicalReasonCode" }, fields.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("OwnerReference", fields["Binding"].GetProperty("after").GetString());
        Assert.Equal(JsonValueKind.Null, fields["Binding"].GetProperty("before").ValueKind);
        Assert.Equal("Allowlisted", fields["Binding"].GetProperty("evidence").GetString());
        Assert.Equal(command.Kind, fields["Kind"].GetProperty("after").GetString());
        Assert.Equal("MaintenanceObservationCapture", fields["CommandName"].GetProperty("after").GetString());
        Assert.Equal("Vessel", fields["ScopeKind"].GetProperty("after").GetString());
        Assert.DoesNotContain(command.Value!, audit.ChangeSummary!); Assert.DoesNotContain(command.Source, audit.ChangeSummary!);
        Assert.DoesNotContain(command.Unit, audit.ChangeSummary!); Assert.DoesNotContain(command.PolicyVersion, audit.ChangeSummary!);
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == observation.Id, Ct));
    }

    [Fact]
    public async Task Observation_capture_permission_or_parent_scope_denial_does_not_create_observation_or_business_audit()
    {
        await using var deniedHost = ObservationHost(false); using var denied = Client(deniedHost); await Token(denied);
        await using var db = Db();
        var observations = await db.MaintenanceObservations.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct);
        var audits = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct);
        var command = Reading() with { SourceRevision = 41 };
        using var permission = await Send(denied, ObservationPath, command); Assert.Equal(HttpStatusCode.NotFound, permission.StatusCode);
        await using var host = ObservationHost(); using var client = Client(host); await Token(client);
        foreach (var other in seed.Vessels.Where(x => x.Id != SeedVessel.Id))
        {
            var wrongParent = $"equipment-hierarchy/{other.Id}/{SeedVessel.Id}/observations";
            using var mismatch = await Send(client, wrongParent, command); Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);
        }
        Assert.Equal(observations, await db.MaintenanceObservations.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct));
        Assert.Equal(audits, await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct));
        Assert.False(await db.MaintenanceObservations.AnyAsync(x => x.Id == command.Id, Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "MaintenanceObservation" && x.Result == "resource_not_found", Ct));
    }

    [Fact]
    public async Task Observation_revocation_appends_scoped_audit_without_rewriting_original_declaration_or_capture_audit()
    {
        await using var host = ObservationHost(); using var client = Client(host); await Token(client);
        var command = Reading() with { SourceRevision = 47 };
        using var captured = await Send(client, ObservationPath, command); Assert.Equal(HttpStatusCode.OK, captured.StatusCode);
        await using var db = Db();
        var original = JsonSerializer.Serialize(await db.MaintenanceObservations.AsNoTracking().SingleAsync(x => x.Id == command.Id, Ct));
        var captureAudit = await db.AuditEvents.AsNoTracking().SingleAsync(x => x.Source == "WebCommand" &&
            x.EntityType == nameof(MaintenanceObservation) && x.ResourceId == command.Id, Ct);
        var originalAudit = JsonSerializer.Serialize(captureAudit);
        using var revoked = await Send(client, ObservationPath + $"/{command.Id}/revoke", new { });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var replay = await Send(client, ObservationPath + $"/{command.Id}/revoke", new { });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var retained = await db.MaintenanceObservations.AsNoTracking().SingleAsync(x => x.Id == command.Id, Ct);
        Assert.Equal(original, JsonSerializer.Serialize(retained)); Assert.Equal("UserDeclaredUnverified", retained.Provenance);
        Assert.Equal(originalAudit, JsonSerializer.Serialize(await db.AuditEvents.AsNoTracking().SingleAsync(x => x.Id == captureAudit.Id, Ct)));
        var revocation = Assert.Single(await db.MaintenanceObservationRevocations.AsNoTracking().Where(x => x.ObservationId == command.Id).ToArrayAsync(Ct));
        Assert.Equal(retained.TenantId, revocation.TenantId);
        var audit = Assert.Single(await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand" &&
            x.EntityType == nameof(MaintenanceObservationRevocation) && x.ResourceId == command.Id).ToArrayAsync(Ct));
        Assert.Equal(retained.VesselId, audit.VesselId);
        using var summary = JsonDocument.Parse(audit.ChangeSummary!);
        Assert.Contains(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == "Lifecycle" && x.GetProperty("after").GetString() == "Revoked");
        Assert.Null(audit.AfterRevision); Assert.Null(audit.AfterVersion);
        var memberships = host.Services.GetRequiredService<FederationSettings>();
        memberships.Memberships[0] = memberships.Memberships[0] with {
            Permissions = memberships.Memberships[0].Permissions.Where(x => x != "maintenance.observation.write").ToArray()
        };
        var beforeDenied = await db.AuditEvents.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct);
        using var deniedReplay = await Send(client, ObservationPath + $"/{command.Id}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, deniedReplay.StatusCode);
        var newId = Guid.NewGuid();
        using var deniedCapture = await Send(client, ObservationPath, command with { Id = newId, SourceRevision = 48 });
        Assert.Equal(HttpStatusCode.NotFound, deniedCapture.StatusCode);
        Assert.Equal(beforeDenied, await db.AuditEvents.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct));
        Assert.False(await db.MaintenanceObservations.AnyAsync(x => x.Id == newId, Ct));
        Assert.Equal(original, JsonSerializer.Serialize(await db.MaintenanceObservations.AsNoTracking().SingleAsync(x => x.Id == command.Id, Ct)));
    }
}
