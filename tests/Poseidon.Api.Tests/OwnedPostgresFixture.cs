using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Npgsql;
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
    public async ValueTask InitializeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await container.StartAsync(deadline.Token); }
        catch { await container.DisposeAsync(); throw; }
    }

    public async Task<OwnedTestDatabase> CreateAsync(CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await lifecycle.WaitAsync(deadline.Token);
        try
        {
            var name = "test_" + Guid.NewGuid().ToString("N");
            ValidateName(name);
            var connection = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = name, Pooling = false };
            var lease = new OwnedTestDatabase(this, name, connection.ConnectionString);
            if (!owned.TryAdd(name, lease)) throw new InvalidOperationException("Test database ownership collision.");
            try {
                await using var admin = new NpgsqlConnection(container.GetConnectionString());
                await admin.OpenAsync(deadline.Token);
                await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin) { CommandTimeout = 20 };
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
        try { foreach (var lease in owned.Values.ToArray()) await lease.DisposeAsync(); }
        finally { await container.DisposeAsync(); lifecycle.Dispose(); }
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
