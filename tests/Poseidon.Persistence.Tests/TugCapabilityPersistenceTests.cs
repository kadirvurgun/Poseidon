using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed partial class TugPersistenceTests
{
    [Fact]
    public async Task Incomplete_capability_seed_is_relational_idempotent_and_preserves_operator_revision() {
        var tug = data.Vessels.Single(v => v.Kind == VesselKind.Tug); var id = TugCapabilityFixtures.Id(tug.Id);
        await using (var db = Import()) {
            var binding = await db.TugCapabilityBindings.SingleAsync(x => x.Id == id, Ct);
            Assert.Null(binding.AssetId); Assert.Null(binding.ProfileVersion); Assert.Null(binding.ConditionId);
            Assert.Empty(await db.TugOfflineEnrollments.ToArrayAsync(Ct)); Assert.Empty(await db.TugOfflineOutcomes.ToArrayAsync(Ct));
            Assert.Empty(await db.TugOfflineLifecycles.ToArrayAsync(Ct)); // devices/keys/receipts are never seeded
            var revision = await db.ControlEvidenceRevisions.SingleAsync(x => x.Id == id, Ct);
            var c = JsonSerializer.Deserialize<ControlEvidenceContent>(revision.ContentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Null(c.TugCapability!.MeasuredTonnes); Assert.Null(revision.FileId); Assert.Null(revision.ActorId); Assert.Equal("DRAFT", revision.Lifecycle);
            var h = await db.ControlEvidenceHeads.SingleAsync(x => x.Id == id, Ct); h.Revision++;
            var content = revision.ContentJson;
            db.ControlEvidenceRevisions.Add(new() { TenantId = tug.TenantId, VesselId = tug.Id, Kind = "Evidence", Id = id, Revision = h.Revision,
                ContentJson = content, Hash = RegisterStorage.Hash(content), ReasonCode = "AUTHORING_CORRECTION", SavedAt = DateTimeOffset.UtcNow });
            db.TugCapabilityBindings.Add(new() { TenantId = tug.TenantId, VesselId = tug.Id, Id = id, Revision = h.Revision }); await db.SaveChangesAsync(Ct);
        }
        await using var reseed = Import(); var before = await reseed.AuditEvents.CountAsync(Ct);
        await SeedImporter.ImportAsync(reseed, data, Ct); await SeedImporter.ImportAsync(reseed, data, Ct);
        Assert.Equal(before, await reseed.AuditEvents.CountAsync(Ct)); Assert.Equal(2, (await reseed.ControlEvidenceHeads.SingleAsync(x => x.Id == id, Ct)).Revision);
        Assert.Equal(2, await reseed.TugCapabilityBindings.CountAsync(x => x.Id == id, Ct)); Assert.Empty(await reseed.HumanReviewEvents.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Capability_binding_is_immutable_and_foreign_asset_cannot_cross_tenant_or_tug_context() {
        var tug = data.Vessels.Single(v => v.Kind == VesselKind.Tug); var id = TugCapabilityFixtures.Id(tug.Id);
        await using (var db = Import()) {
            var b = await db.TugCapabilityBindings.SingleAsync(x => x.Id == id, Ct); b.ProfileVersion = 1;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using (var db = Import()) {
            foreach (var sql in new[] { "UPDATE tug.\"CapabilityBindings\" SET \"ProfileVersion\"=1", "TRUNCATE tug.\"CapabilityBindings\"" })
                await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));
        }
        await using (var db = Import()) {
            var head = await db.ControlEvidenceHeads.SingleAsync(x => x.Id == id, Ct); head.Revision++;
            db.ControlEvidenceRevisions.Add(new() { TenantId = tug.TenantId, VesselId = tug.Id, Kind = "Evidence", Id = id, Revision = head.Revision,
                ContentJson = "{}", Hash = RegisterStorage.Hash("{}"), ReasonCode = "AUTHORING_CORRECTION", SavedAt = DateTimeOffset.UtcNow });
            var foreign = data.Vessels.First(v => v.TenantId != tug.TenantId);
            db.TugCapabilityBindings.Add(new() { TenantId = tug.TenantId, VesselId = tug.Id, Id = id, Revision = head.Revision, AssetId = foreign.Id, AssetRevision = 1 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
    }
}
