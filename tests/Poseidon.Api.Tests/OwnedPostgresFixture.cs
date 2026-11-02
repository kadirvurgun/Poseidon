using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Api.Tests;

[CollectionDefinition("directory-postgres")]
public sealed class DirectoryPostgresCollection : ICollectionFixture<OwnedPostgresFixture>;

// Test-only administrator. No environment/production connection overrides, shared schema or Docker cleanup.
public sealed class OwnedPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly ConcurrentDictionary<string, OwnedTestDatabase> owned = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly SemaphoreSlim templateCreation = new(1, 1);
    private OwnedTestDatabase? migratedTemplate;
    public async ValueTask InitializeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await container.StartAsync(deadline.Token); }
        catch { await container.DisposeAsync(); throw; }
    }

    // Fresh leases still have no schema/history. Migration isolation tests use
    // this path; ordinary API cases may opt into a distinct migrated clone.
    public Task<OwnedTestDatabase> CreateAsync(CancellationToken ct) => CreateDatabaseAsync(null, ct);

    public async Task<OwnedTestDatabase> CreateMigratedAsync(CancellationToken ct)
    {
        string source;
        await templateCreation.WaitAsync(ct);
        try
        {
            if (migratedTemplate == null)
            {
                var candidate = await CreateAsync(ct);
                try
                {
                    // Only actual migrations, no seed/business/audit data. The
                    // caller still runs EF's ordinary idempotent seed path.
                    await using var db = new PoseidonDbContext(SeedImporter.Options(candidate.GetConnectionString()),
                        Guid.Empty, ActorContext.SeedImport(), true);
                    await db.Database.MigrateAsync(ct);
                    migratedTemplate = candidate;
                }
                catch { await candidate.DisposeAsync(); throw; }
            }
            source = migratedTemplate.Name;
        }
        finally { templateCreation.Release(); }
        return await CreateDatabaseAsync(source, ct);
    }

    private async Task<OwnedTestDatabase> CreateDatabaseAsync(string? template, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await lifecycle.WaitAsync(deadline.Token);
        try
        {
            var name = "test_" + Guid.NewGuid().ToString("N");
            ValidateName(name);
            if (template != null)
            {
                ValidateName(template);
                if (migratedTemplate?.Name != template || !owned.TryGetValue(template, out var actual) || !ReferenceEquals(actual, migratedTemplate))
                    throw new InvalidOperationException("Migration template is not owned by this fixture.");
            }
            var connection = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name, Pooling = false };
            var lease = new OwnedTestDatabase(this, name, connection.ConnectionString);
            if (!owned.TryAdd(name, lease)) throw new InvalidOperationException("Test database ownership collision.");
            try {
                await using var admin = new NpgsqlConnection(container.GetConnectionString());
                await admin.OpenAsync(deadline.Token);
                var sql = template == null ? $"CREATE DATABASE \"{name}\"" : $"CREATE DATABASE \"{name}\" TEMPLATE \"{template}\"";
                await using var command = new NpgsqlCommand(sql, admin) { CommandTimeout = 20 };
                await command.ExecuteNonQueryAsync(deadline.Token);
            } catch { /* Keep ownership registered: a cancelled CREATE may already have committed. */ throw; }
            return lease;
        }
        finally { lifecycle.Release(); }
    }

    internal static void ValidateName(string name)
    {
        if (!Regex.IsMatch(name, @"\Atest_[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Not a generated test database identifier.");
    }

    internal async ValueTask ReleaseAsync(OwnedTestDatabase lease)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await lifecycle.WaitAsync(deadline.Token);
        try
        {
            ValidateName(lease.Name);
            if (!owned.TryGetValue(lease.Name, out var actual) || !ReferenceEquals(actual, lease))
                throw new InvalidOperationException("Database is not owned by this fixture lease.");
            await using var admin = new NpgsqlConnection(container.GetConnectionString());
            await admin.OpenAsync(deadline.Token);
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{lease.Name}\" WITH (FORCE)", admin) { CommandTimeout = 20 };
            await command.ExecuteNonQueryAsync(deadline.Token);
            owned.TryRemove(lease.Name, out _);
        }
        finally { lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var lease in owned.Values.Where(x => !ReferenceEquals(x, migratedTemplate)).ToArray()) await lease.DisposeAsync();
            if (migratedTemplate != null) await migratedTemplate.DisposeAsync();
        }
        finally { await container.DisposeAsync(); lifecycle.Dispose(); templateCreation.Dispose(); }
    }
}

public sealed class OwnedTestDatabase : IAsyncDisposable
{
    private readonly OwnedPostgresFixture owner;
    private readonly string connection;
    private bool disposed;
    private readonly SemaphoreSlim disposal = new(1, 1);
    internal OwnedTestDatabase(OwnedPostgresFixture owner, string name, string connection)
        => (this.owner, Name, this.connection) = (owner, name, connection);
    internal string Name { get; }
    public string GetConnectionString() => !disposed ? connection : throw new ObjectDisposedException(nameof(OwnedTestDatabase));
    public async ValueTask DisposeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await disposal.WaitAsync(deadline.Token);
        try {
            if (disposed) return;
            await owner.ReleaseAsync(this);
            disposed = true;
        } finally { disposal.Release(); }
    }
}
