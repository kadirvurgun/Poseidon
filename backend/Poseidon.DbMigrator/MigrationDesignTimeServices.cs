using System.Globalization;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Poseidon.DbMigrator;

// Existing source migrations have ordered IDs later than the host clock. A newly
// scaffolded migration must append to that history, including its target model.
public sealed class MigrationDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services) =>
        services.AddSingleton<IMigrationsIdGenerator, AppendOnlyMigrationIds>();
}

public sealed class AppendOnlyMigrationIds(IMigrationsAssembly migrations) : IMigrationsIdGenerator
{
    private readonly object sync = new();
    private long last;

    public string GenerateId(string name)
    {
        lock (sync) {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var clock = long.Parse(DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var prior = migrations.Migrations.Keys.Where(IsValidId)
                .Select(id => long.Parse(id[..14], CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            last = Math.Max(clock, checked(Math.Max(prior, last) + 1));
            var prefix = last.ToString("D14", CultureInfo.InvariantCulture);
            if (prefix.Length != 14) throw new InvalidOperationException("Migration sequence capacity exceeded.");
            return prefix + "_" + name;
        }
    }
    public string GetName(string id) => IsValidId(id) ? id[15..] : throw new ArgumentException("Invalid migration ID.", nameof(id));
    public bool IsValidId(string value) => value is { Length: > 15 } && value[14] == '_'
        && value.AsSpan(0, 14).IndexOfAnyExceptInRange('0', '9') < 0;
}
