using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

public static class SeedImporter
{
    // Run through UseSeeding/UseAsyncSeeding so EF's migration lock serializes imports.
    public static void Import(PoseidonDbContext db, SeedData data) =>
        ImportAsync(db, data).GetAwaiter().GetResult();

    public static async Task ImportAsync(PoseidonDbContext db, SeedData data, CancellationToken cancellationToken = default)
    {
        data.Validate();
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException("Seed import requires an explicit administrative context.");
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        await AddMissing(db, data.Tenants, t => [t.Id], cancellationToken).ConfigureAwait(false);
        await AddMissing(db, data.Companies, c => [c.TenantId, c.Id], cancellationToken).ConfigureAwait(false);
        await AddMissing(db, data.Vessels, v => [v.TenantId, v.Id], cancellationToken).ConfigureAwait(false);
        await AddMissing(db, data.VesselProfiles, p => [p.TenantId, p.VesselId], cancellationToken).ConfigureAwait(false);
        await AddMissing(db, data.TugProfiles, p => [p.TenantId, p.VesselId], cancellationToken).ConfigureAwait(false);
        // Actual synthetic draft operations only. Never infer regulatory facts/freshness from legacy profiles.
        foreach (var vessel in data.Vessels)
        {
            if (await db.RegulatoryDrafts.FindAsync([vessel.TenantId, vessel.Id], cancellationToken).ConfigureAwait(false) != null) continue;
            var draft = new RegulatoryDraft
            {
                TenantId = vessel.TenantId, VesselId = vessel.Id, Revision = 1, SavedAt = DateTimeOffset.UtcNow,
                Provenance = "Synthetic", ContentJson = RegulatoryDraftStorage.Serialize(new()
                {
                    Declaration = new("Unknown"), SourceReference = new("Known", "synthetic-demo/draft-v1"), SourceObservedAt = new("Unknown")
                })
            };
            db.RegulatoryDrafts.Add(draft);
            db.RegulatoryDraftRevisions.Add(draft.Snapshot());
        }
        await RegulatoryRegisterFixtures.AddMissingAsync(db, data.Tenants, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddMissing<T>(PoseidonDbContext db, IEnumerable<T> records, Func<T, object[]> key, CancellationToken token) where T : class
    {
        foreach (var record in records)
            if (await db.Set<T>().FindAsync(key(record), token).ConfigureAwait(false) is null)
                db.Set<T>().Add(record);
    }

    public static DbContextOptions<PoseidonDbContext> Options(string connectionString, SeedData? demoData = null)
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connectionString);
        if (demoData is not null)
        {
            demoData.Validate();
            options.UseSeeding((context, _) => Import((PoseidonDbContext)context, demoData));
            options.UseAsyncSeeding((context, _, token) => ImportAsync((PoseidonDbContext)context, demoData, token));
        }
        return options.Options;
    }
}
