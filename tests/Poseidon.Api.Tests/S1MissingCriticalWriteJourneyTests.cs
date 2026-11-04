using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-016/019 / T-S1-002/003/004/007. Real host and owned PostgreSQL;
// ordinary synthetic unverified lookup policy grants no operational appointment.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task S1_new_position_reference_is_really_inserted_audited_and_preserved_after_replay_and_permission_withdrawal()
    {
        await using var host = Host(crewRead: true, crewWrite: true); using var client = Client(host); await Token(client);
        var policy = host.Services.GetRequiredService<CrewPositionPolicy>();
        var position = new CrewPositionRule
        {
            TenantId = seed.Tenants[0].Id, VesselId = SeedVessel.Id, PositionId = Guid.NewGuid(),
            Code = "S1-NEW-UNVERIFIED", Provenance = "UserDeclaredUnverified"
        };
        policy.Positions = [.. policy.Positions, position];
        var command = new CrewPositionCommand(position.PositionId, position.Code, null, null, null, policy.Version);
        byte[] assignmentsBefore, revisionsBefore;
        await using (var before = Db())
        {
            Assert.False(await before.CrewPositions.AnyAsync(x => x.Id == position.PositionId, Ct));
            assignmentsBefore = await S1HashMappedRows<CrewAssignment>(before, Ct);
            revisionsBefore = await S1HashMappedRows<CrewAssignmentRevision>(before, Ct);
        }
        var path = $"vessels/{SeedVessel.Id}/crew/positions";
        using var created = await Send(client, path, command); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(position.PositionId, (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid());
        await S1AssertCommittedCommand(created, "CrewPositionSave", "PREPARATION_COMMAND", position.PositionId, SeedVessel.Id, actorId);
        await using (var db = Db())
        {
            var persisted = Assert.Single(await db.CrewPositions.AsNoTracking().Where(x => x.Id == position.PositionId).ToArrayAsync(Ct));
            Assert.Equal(position.TenantId, persisted.TenantId); Assert.Equal(position.VesselId, persisted.VesselId);
            Assert.Equal(position.Code, persisted.Code); Assert.Equal("UserDeclaredUnverified", persisted.Provenance);
            Assert.Null(persisted.Rank); Assert.Null(persisted.Capacity); Assert.Null(persisted.Department);
            var audit = Assert.Single(await db.AuditEvents.Where(x => x.EntityType == nameof(CrewPosition) && x.ResourceId == position.PositionId).ToArrayAsync(Ct));
            Assert.Equal("Added", audit.Action); Assert.Null(audit.BeforeVersion); Assert.Null(audit.BeforeRevision);
            Assert.False(audit.ChangeSummary!.Contains(position.Code, StringComparison.Ordinal), "Position values escaped the closed audit summary.");
            var assignmentsAfter = await S1HashMappedRows<CrewAssignment>(db, Ct);
            var revisionsAfter = await S1HashMappedRows<CrewAssignmentRevision>(db, Ct);
            Assert.True(CryptographicOperations.FixedTimeEquals(assignmentsBefore, assignmentsAfter), "Position insertion changed an assignment.");
            Assert.True(CryptographicOperations.FixedTimeEquals(revisionsBefore, revisionsAfter), "Position insertion changed assignment history.");
        }
        var retained = await S1BusinessSnapshot();
        using var replay = await Send(client, path, command); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        S1AssertSameBusinessSnapshot(retained, await S1BusinessSnapshot());
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with
        { Permissions = settings.Memberships[0].Permissions.Where(x => x != "crew.position.write").ToArray() };
        using var denied = await Send(client, path, command); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("permission_denied", (await denied.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        S1AssertSameBusinessSnapshot(retained, await S1BusinessSnapshot());
    }

    [Fact]
    public async Task S1_offline_revoke_really_commits_and_blocks_recovery_without_changing_assignment_or_retained_capture()
    {
        await using var host = OfflineHost(); using var client = Client(host); await Token(client);
        var enrollment = await EnrollOffline(client); var capture = Captured(enrollment);
        using var delivered = await Deliver(client, enrollment, capture); Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        OfflineEnrollment original;
        byte[] assignmentsBefore, revisionsBefore, outcomesBefore, receiptsBefore;
        await using (var before = Db())
        {
            original = await before.OfflineEnrollments.AsNoTracking().SingleAsync(x => x.Id == enrollment.Id, Ct);
            Assert.Null(original.RevokedAt);
            Assert.False(string.IsNullOrEmpty(original.KeyChallenge));
            Assert.False(string.IsNullOrEmpty(original.ProtectedBase));
            Assert.False(string.IsNullOrEmpty(original.PublicSigningKey));
            assignmentsBefore = await S1HashMappedRows<CrewAssignment>(before, Ct);
            revisionsBefore = await S1HashMappedRows<CrewAssignmentRevision>(before, Ct);
            outcomesBefore = await S1HashMappedRows<OfflineOutcome>(before, Ct);
            receiptsBefore = await S1HashMappedRows<CommandReceipt>(before, Ct);
        }
        using var revoked = await Send(client, $"{OfflinePath}/{enrollment.Id}/revoke", new { });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.Equal("RetainedLocked", (await revoked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("state").GetString());
        await S1AssertCommittedCommand(revoked, "OfflineRevoke", "OFFLINE_LIFECYCLE", enrollment.AssignmentId, enrollment.VesselId, actorId);
        await using (var db = Db())
        {
            var current = await db.OfflineEnrollments.AsNoTracking().SingleAsync(x => x.Id == enrollment.Id, Ct);
            Assert.NotNull(current.RevokedAt); Assert.NotEqual(original.Version, current.Version);
            Assert.Equal((original.TenantId, original.ActorId, original.VesselId, original.AssignmentId, original.DeviceId, original.KeyId,
                original.BaseVersion, original.BaseRevision, original.CreatedAt, original.ExpiresAt),
                (current.TenantId, current.ActorId, current.VesselId, current.AssignmentId, current.DeviceId, current.KeyId,
                current.BaseVersion, current.BaseRevision, current.CreatedAt, current.ExpiresAt));
            Assert.True(original.ProtectedBase == current.ProtectedBase && original.PublicSigningKey == current.PublicSigningKey &&
                original.KeyChallenge == current.KeyChallenge && original.PolicyDigest == current.PolicyDigest &&
                original.PolicyVersion == current.PolicyVersion && original.SourceVersion == current.SourceVersion,
                "Revocation changed the retained immutable capture context.");
            var lifecycle = Assert.Single(await db.OfflineLifecycles.Where(x => x.EnrollmentId == enrollment.Id && x.Action == "Revoked").ToArrayAsync(Ct));
            Assert.Equal(actorId, lifecycle.ActorId); Assert.Equal(enrollment.VesselId, lifecycle.VesselId);
            Assert.Equal(enrollment.AssignmentId, lifecycle.AssignmentId); Assert.Equal(current.RevokedAt, lifecycle.RecordedAt);
            var correlation = Assert.Single(revoked.Headers.GetValues("X-Correlation-ID"));
            var enrollmentAudit = Assert.Single(await db.AuditEvents.Where(x => x.CorrelationId == correlation &&
                x.EntityType == nameof(OfflineEnrollment)).ToArrayAsync(Ct));
            Assert.Equal("Modified", enrollmentAudit.Action); Assert.Equal(original.Version, enrollmentAudit.BeforeVersion);
            var assignmentsAfter = await S1HashMappedRows<CrewAssignment>(db, Ct);
            var revisionsAfter = await S1HashMappedRows<CrewAssignmentRevision>(db, Ct);
            var outcomesAfter = await S1HashMappedRows<OfflineOutcome>(db, Ct);
            var receiptsAfter = await S1HashMappedRows<CommandReceipt>(db, Ct);
            Assert.True(CryptographicOperations.FixedTimeEquals(assignmentsBefore, assignmentsAfter), "Revocation changed the ordinary assignment.");
            Assert.True(CryptographicOperations.FixedTimeEquals(revisionsBefore, revisionsAfter), "Revocation changed assignment history.");
            Assert.True(CryptographicOperations.FixedTimeEquals(outcomesBefore, outcomesAfter), "Revocation changed immutable command outcomes.");
            Assert.True(CryptographicOperations.FixedTimeEquals(receiptsBefore, receiptsAfter), "Revocation changed ordinary command receipts.");
            foreach (var audit in await db.AuditEvents.Where(x => x.CorrelationId == correlation).ToArrayAsync(Ct))
                Assert.False(audit.ChangeSummary!.Contains(original.KeyChallenge, StringComparison.Ordinal) ||
                    audit.ChangeSummary.Contains(original.ProtectedBase, StringComparison.Ordinal) ||
                    audit.ChangeSummary.Contains(original.PublicSigningKey!, StringComparison.Ordinal), "Private offline context escaped the closed audit summary.");
        }
        var retained = await S1BusinessSnapshot();
        using var deniedRecovery = await Deliver(client, enrollment, capture);
        Assert.Equal(HttpStatusCode.Forbidden, deniedRecovery.StatusCode);
        Assert.Equal("offline_policy_denied", (await deniedRecovery.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        S1AssertSameBusinessSnapshot(retained, await S1BusinessSnapshot());
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with
        { Permissions = settings.Memberships[0].Permissions.Where(x => x != "crew.write").ToArray() };
        using var deniedRevoke = await Send(client, $"{OfflinePath}/{enrollment.Id}/revoke", new { });
        Assert.Equal(HttpStatusCode.NotFound, deniedRevoke.StatusCode);
        S1AssertSameBusinessSnapshot(retained, await S1BusinessSnapshot());
    }
}
