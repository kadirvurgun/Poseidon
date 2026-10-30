using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class WriteGuardTests
{
    private static PoseidonDbContext Context(Guid tenant, ActorContext actor) => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), tenant, actor);

    [Fact]
    public async Task Cross_tenant_write_is_denied_before_database_access()
    {
        using var db = Context(Guid.NewGuid(), new ActorContext("user", "User", "request"));
        db.Companies.Add(new Company { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Code = "DENIED", Name = "Denied" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Anonymous_mutation_is_denied_before_database_access()
    {
        var tenant = Guid.NewGuid();
        using var db = Context(tenant, new ActorContext("", "User", "request"));
        db.Companies.Add(new Company { Id = Guid.NewGuid(), TenantId = tenant, Code = "DENIED", Name = "Denied" });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Direct_audit_injection_is_denied_before_database_access()
    {
        var tenant = Guid.NewGuid();
        using var db = Context(tenant, new ActorContext("user", "User", "request"));
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = tenant, ActorId = "impersonated", ActorType = "User",
            Action = "Forged", EntityType = "Vessel", EntityId = "fake", CorrelationId = "fake", OccurredAt = DateTimeOffset.UtcNow
        });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Normal_tenant_context_cannot_run_admin_seed()
    {
        using var db = Context(Guid.NewGuid(), new ActorContext("user", "User", "request"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => SeedImporter.ImportAsync(db, SeedValidationTests.Demo(), TestContext.Current.CancellationToken));
    }
}
