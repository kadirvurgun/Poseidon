using Microsoft.EntityFrameworkCore;
namespace Poseidon.Persistence.Seeding;

// Explicit disposable Test-only fixture. Never normal seeding/startup or an HTTP endpoint.
public static class LegacyCrewBrowserFixture
{
    public static readonly Guid BatchId = Guid.Parse("ceea0001-0000-4000-8000-000000000001");
    // Trusted Test-only CLI identity, never a tenant/actor/content override or HTTP input.
    // Lowercase D format gives the recorded cohort UUID one unambiguous spelling.
    public static Guid ParseBatchId(string? value) {
        if (value is not { Length: 36 } || !Guid.TryParseExact(value, "D", out var id) ||
            id == Guid.Empty || value != id.ToString("D"))
            throw new ArgumentException("A nonempty canonical fixture UUID is required.", nameof(value));
        return id;
    }
    public static async Task AddAsync(string connection, string environment, SeedData seed, Guid? fixtureBatchId = null) {
        if (environment != "Test") throw new InvalidOperationException("Legacy browser fixture requires the disposable Test environment.");
        var batch = fixtureBatchId ?? BatchId;
        if (batch == Guid.Empty) throw new ArgumentException("A nonempty fixture UUID is required.", nameof(fixtureBatchId));
        // Fixed repository synthetic scope. No caller-selected actor, tenant, role, policy, key or content.
        var vessel = seed.Vessels.First();
        await using var db = new PoseidonDbContext(SeedImporter.Options(connection), vessel.TenantId,
            new ActorContext("system:synthetic-legacy-crew-fixture", "System", Guid.NewGuid().ToString("D"), Source: "SyntheticFixture"));
        if (!await db.Vessels.AnyAsync(x => x.Id == vessel.Id && x.CompanyId == vessel.CompanyId))
            throw new InvalidOperationException("Fixed synthetic scope must already exist in the disposable database.");
        if (await db.ImportBatches.AnyAsync(x => x.Id == batch)) return; // Preserve operator edits and previous conversion.
        db.ImportBatches.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Id = batch,
            ActorId = Guid.Empty, SessionHash = "synthetic-expired-session", Category = "crew", TemplateVersion = "synthetic-legacy-unsupported-v0",
            TemplateFingerprint = "synthetic-unsupported-schema", TableJson = " {\"obsoleteHeaders\":[\"FICTIONAL\"],\"obsoleteRows\":[[\"FICTIONAL LEGACY CELL\"]]} ",
            BindingsJson = "[]", SourceHash = "synthetic-unavailable-original-bytes", SourceName = "synthetic-legacy.xlsx",
            SourceLocator = "SyntheticFixture/legacy-format-v0/original-capture-unknown", RowCount = 1, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); // Attributed ordinary tenant-scoped audit, no raw SQL/admin context.
    }
}
