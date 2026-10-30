using Microsoft.EntityFrameworkCore.Design;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;

namespace Poseidon.DbMigrator;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PoseidonDbContext>
{
    public PoseidonDbContext CreateDbContext(string[] args) => new(
        SeedImporter.Options(Environment.GetEnvironmentVariable("POSEIDON_DATABASE")
            ?? "Host=localhost;Database=poseidon_design_only;Username=design_only"),
        Guid.Empty, new ActorContext("system:migrations", "System", "design-time"), administrativeImport: true);
}
