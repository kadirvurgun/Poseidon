using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S11 WP-D DEV-074/094 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per tenant: one FICTIONAL OPEN ship UAT run and one FICTIONAL OPEN tug UAT run (pseudonym FICTIONAL-*, facilitated
// by a deterministic synthetic actor id without membership). No result, finding, threshold, submission,
// attestation, support check, pilot gate decision or receipt is seeded; DB CHECKs reject a synthetic run leaving
// OPEN. Add-missing; operator edits are kept.
public static class UatFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.uat." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid RunId(Guid tenant, string scope) => Id("run-" + scope, tenant);
    public static Guid SyntheticFacilitator(Guid tenant) => Id("facilitator", tenant);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Guid> tenants, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic UAT fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        foreach (var tenant in tenants.Distinct())
            foreach (var (scope, role) in new[] { (UatScenarioCatalog.Ship, "MASTER"), (UatScenarioCatalog.Tug, "TUG_MASTER") })
            {
                var id = RunId(tenant, scope);
                if (await db.UatRuns.AnyAsync(r => r.TenantId == tenant && r.Id == id, ct)) continue;
                db.UatRuns.Add(new UatRun { TenantId = tenant, Id = id, Scope = scope, Role = role, Environment = "LAB", BuildCommit = "0000000",
                    ParticipantPseudonym = "FICTIONAL-" + scope + "-01", CatalogVersion = UatScenarioCatalog.Version, FacilitatorActorId = SyntheticFacilitator(tenant),
                    CreatedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
        // Caller commits.
    }
}
