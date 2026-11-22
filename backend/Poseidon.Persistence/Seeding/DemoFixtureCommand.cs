using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence.Seeding;

// USER-GUIDE-01 UG-2 (D4/D5): explicit DbMigrator command that loads the opt-in FICTIONAL demo
// fixtures into an already seeded Development/Test database, so documentation screenshots show a
// populated but undecided workspace. Never startup, never an HTTP endpoint, never Production.
// It only composes existing add-missing administrative fixtures (TugDemoFixtures: towage plan DRAFT,
// briefing DRAFT, tug daily log DRAFT; AccountMembershipFixtures: one grant-less fictional member). No readiness GO, release, execution, acknowledgement, review,
// verification, signature, credential or receipt is seeded; every gate stays UNKNOWN/UNVERIFIED.
// Writes go through the audited SaveChanges path of an administrative-import context, inside one
// transaction; a second run adds nothing and operator edits are preserved.
public static class DemoFixtureCommand
{
    public const string Flag = "--demo-fixtures";

    /// <summary>Exact environment allow-list; any other value (including different casing) is rejected.</summary>
    public static void EnsureAllowed(string environment)
    {
        if (environment is not ("Development" or "Test"))
            throw new InvalidOperationException("Synthetic demo fixtures are allowed only in Development or Test.");
    }

    public static async Task RunAsync(string connection, string environment, SeedData seed, CancellationToken ct = default)
    {
        EnsureAllowed(environment);
        seed.Validate();
        await using var db = new PoseidonDbContext(SeedImporter.Options(connection), Guid.Empty,
            new ActorContext("system:synthetic-demo-fixture", "System", Guid.NewGuid().ToString("D"), Source: "SyntheticFixture"),
            administrativeImport: true);
        // Fixtures extend the synthetic seed; they never create tenants or vessels themselves.
        var vesselIds = seed.Vessels.Select(v => v.Id).ToArray();
        if (await db.Vessels.CountAsync(v => vesselIds.Contains(v.Id), ct) != vesselIds.Length)
            throw new InvalidOperationException("Run the synthetic seed import before loading demo fixtures.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await TugDemoFixtures.AddMissingAsync(db, seed.Vessels, ct);
        // One FICTIONAL account/membership per tenant on the reserved ".invalid" issuer: no grant,
        // role, vessel scope, delegation, administrator status or credential (AccountMembershipFixtures).
        await AccountMembershipFixtures.AddMissingAsync(db, seed.Tenants, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
