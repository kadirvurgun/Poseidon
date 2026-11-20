namespace Poseidon.Persistence.Seeding;

// S10 WP-B DEV-119 optional administrative demo fixture (docs/seed-data.md). Not wired into the default seed.
// Composes the existing opt-in FICTIONAL drafts into one synthetic tug job chain per synthetic tug: the default
// synthetic tug job (TugFixtures), its towage plan DRAFT and briefing DRAFT (TowageFixtures) and one tug daily
// log DRAFT (FieldOperationsFixtures). Nothing is submitted, approved, issued, acknowledged, released, started or
// verified: no readiness GO, Master release, execution, acknowledgement, review, verification or receipt is seeded,
// so every readiness/evidence state stays UNKNOWN/UNVERIFIED and the demo shows the gates instead of passing them.
// Add-missing; operator edits are kept. Caller commits.
public static class TugDemoFixtures
{
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic tug demo fixtures require an administrative audited transaction.");
        var tugs = vessels.Where(v => v.Kind == VesselKind.Tug).ToArray();
        await TowageFixtures.AddMissingAsync(db, tugs, ct);
        await FieldOperationsFixtures.AddMissingAsync(db, tugs, ct);
    }
}
