using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
var connection = Environment.GetEnvironmentVariable("POSEIDON_DATABASE")
    ?? throw new InvalidOperationException("Set POSEIDON_DATABASE in the process environment.");
SeedData? data = null;
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] != "--seed") throw new ArgumentException("Usage: [--seed path/to/synthetic-demo.json]");
    if (environment is not ("Development" or "Test")) throw new InvalidOperationException("Synthetic demo seed is allowed only in Development or Test.");
    data = SeedData.Load(args[1]);
}
await using var db = new PoseidonDbContext(SeedImporter.Options(connection, data), Guid.Empty, ActorContext.SeedImport(), administrativeImport: true);
await db.Database.MigrateAsync();
Console.WriteLine(data is null ? "Migrations applied; no demo data imported." : "Migrations and synthetic seed import completed.");
