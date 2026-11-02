using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed partial class TugPersistenceTests
{
    [Fact]
    public async Task Synthetic_plan_is_incomplete_idempotent_and_reseed_preserves_operator_history_without_appointments_or_evidence() {
        var tug = data.Vessels.Single(v => v.Kind == VesselKind.Tug);
        await using (var db = Import()) {
            var plan = Assert.Single(await db.TugDraftPlans.ToArrayAsync(Ct)); Assert.Equal(tug.Id, plan.VesselId);
            var snapshot = Assert.Single(await db.TugDraftPlanRevisions.ToArrayAsync(Ct)); Assert.Equal("SyntheticUnverified", snapshot.Provenance);
            Assert.Null(snapshot.Inputs().PlannedStart); Assert.Null(snapshot.Inputs().PlannedEnd); Assert.Empty(snapshot.Inputs().AssignedTugs);
            Assert.Empty(await db.TugDraftPlanTugs.ToArrayAsync(Ct)); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
            plan.Revision++; plan.PlannedStart = DateTimeOffset.UnixEpoch.AddHours(1);
            db.TugDraftPlanRevisions.Add(new() { TenantId = plan.TenantId, VesselId = plan.VesselId, JobId = plan.JobId, Revision = plan.Revision,
                RequestRevision = plan.RequestRevision, PolicyVersion = "synthetic-edited", ContentJson = JsonSerializer.Serialize(new TugPlanInputs(plan.PlannedStart, null, [])), SavedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct);
        }
        int audit;
        await using (var db = Import()) { audit = await db.AuditEvents.CountAsync(Ct); await SeedImporter.ImportAsync(db, data, Ct); await SeedImporter.ImportAsync(db, data, Ct); }
        await using var verify = Import(); Assert.Equal(audit, await verify.AuditEvents.CountAsync(Ct));
        Assert.Equal(2, (await verify.TugDraftPlans.SingleAsync(Ct)).Revision); Assert.Equal(2, await verify.TugDraftPlanRevisions.CountAsync(Ct));
        Assert.Empty(await verify.TugAssignments.ToArrayAsync(Ct)); Assert.Empty(await verify.CommandReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Tenant_filters_composite_fk_and_kind_guard_reject_foreign_or_ship_assignment_even_in_controlled_import_context() {
        var tug = data.Vessels.Single(v => v.Kind == VesselKind.Tug); var foreign = data.Vessels.First(v => v.TenantId != tug.TenantId);
        await using (var scoped = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), foreign.TenantId, new("synthetic", "User", "plan-scope"))) {
            Assert.Empty(await scoped.TugDraftPlans.ToArrayAsync(Ct)); Assert.Empty(await scoped.TugDraftPlanRevisions.ToArrayAsync(Ct)); Assert.Empty(await scoped.TugDraftPlanTugs.ToArrayAsync(Ct));
        }
        foreach (var invalid in new[] { foreign.Id, data.Vessels.First(v => v.Kind == VesselKind.Ship && v.TenantId == tug.TenantId).Id }) {
            await using var db = Import(); db.TugDraftPlanTugs.Add(new() { TenantId = tug.TenantId, VesselId = tug.Id, JobId = tug.Id, Revision = 1, TugId = invalid });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
    }
    [Fact]
    public async Task Real_postgres_blocks_mutating_or_truncating_plan_history_and_removing_plan_identity() {
        await using (var db = Import()) {
            var r = await db.TugDraftPlanRevisions.SingleAsync(Ct); r.Provenance = "forbidden";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync(Ct);
        foreach (var sql in new[] { "UPDATE tug.\"DraftPlanRevisions\" SET \"Provenance\"='forbidden'", "TRUNCATE tug.\"DraftPlanTugs\"", "DELETE FROM tug.\"DraftPlans\"", "UPDATE tug.\"DraftPlans\" SET \"Revision\"=99" }) {
            await using var command = new Npgsql.NpgsqlCommand(sql, connection); await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        }
        await using var verify = Import(); Assert.Equal(1, (await verify.TugDraftPlans.SingleAsync(Ct)).Revision);
    }
}
