using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S9 WP-D DEV-089/090/111 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per synthetic tug: one FICTIONAL tug daily log DRAFT (no time zone, meters, fuel, links or summary). Per synthetic
// vessel: one FICTIONAL drydock package DRAFT (no dates, yard, scope items, links or evidence). No towage execution
// is seeded at all (an execution needs a human GO and Tug Master start), and no submission, Master review, start,
// completion or verification; DB CHECKs reject synthetic executions and synthetic non-DRAFT logs/packages.
// Add-missing; operator edits are kept.
public static class FieldOperationsFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.field-ops." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid DailyLogId(Guid tug) => Id("daily-log", tug);
    public static Guid DrydockId(Guid vessel) => Id("drydock", vessel);
    public static readonly DateOnly SyntheticLogDate = new(2026, 1, 1);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic field-operation fixtures require an administrative audited transaction.");
        var now = db.Clock.GetUtcNow();
        foreach (var v in vessels)
        {
            if (v.Kind == VesselKind.Tug && !await db.TugDailyLogs.AnyAsync(l => l.TenantId == v.TenantId && l.Id == DailyLogId(v.Id), ct))
            {
                var content = new TugDailyLogContent(null, [], new(null, null, null), [], [], null);
                if (!TugDailyLogPolicy.ValidDraft(content)) throw new InvalidOperationException("Synthetic daily log schema unavailable.");
                var l = new TugDailyLog { TenantId = v.TenantId, VesselId = v.Id, Id = DailyLogId(v.Id), LogDate = SyntheticLogDate, Watch = "OTHER",
                    ContentJson = JsonSerializer.Serialize(content, FieldOpsJson.Options), SyncedAt = now, SavedAt = now, Provenance = "SyntheticUnverified" };
                l.Hash = TugDailyLogPolicy.Hash(l.VesselId, l.Id, l.Revision, l.State, l.LogDate, l.Watch, l.ContentJson);
                db.TugDailyLogs.Add(l); await db.SaveChangesAsync(ct);
                db.TugDailyLogRevisions.Add(new() { TenantId = l.TenantId, VesselId = l.VesselId, LogId = l.Id, Revision = 1, State = l.State, ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT",
                    ContentJson = l.ContentJson, Hash = l.Hash, SyncedAt = now, SavedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
            if (!await db.DrydockPackages.AnyAsync(p => p.TenantId == v.TenantId && p.Id == DrydockId(v.Id), ct))
            {
                var content = new DrydockContent(null, null, null, null, new(null, null, null, null), [], []);
                if (!DrydockPolicy.ValidDraft(content)) throw new InvalidOperationException("Synthetic drydock schema unavailable.");
                var p = new DrydockPackage { TenantId = v.TenantId, VesselId = v.Id, Id = DrydockId(v.Id), ContentJson = JsonSerializer.Serialize(content, FieldOpsJson.Options),
                    SavedAt = now, Provenance = "SyntheticUnverified" };
                p.Hash = DrydockPolicy.Hash(p.VesselId, p.Id, p.Revision, p.State, p.ContentJson);
                db.DrydockPackages.Add(p); await db.SaveChangesAsync(ct);
                db.DrydockPackageRevisions.Add(new() { TenantId = p.TenantId, VesselId = p.VesselId, PackageId = p.Id, Revision = 1, State = p.State, ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT",
                    ContentJson = p.ContentJson, Hash = p.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
        }
        // Caller commits.
    }
}
