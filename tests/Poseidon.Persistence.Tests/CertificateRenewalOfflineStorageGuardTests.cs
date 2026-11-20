using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S10 WP-C DEV-066: certificate renewal offline lineage is immutable in the audited SaveChanges path before any
// database access; the enrollment may only renew its lease, be revoked once and bind its first public key.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-066")]
public sealed class CertificateRenewalOfflineStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c066");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Offline_outcome_and_lifecycle_rows_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        var outcome = new CertificateRenewalOfflineOutcome { TenantId = Tenant, ActorId = Guid.NewGuid(), CommandId = Guid.NewGuid(), Result = "MaterialConflict" };
        db.Attach(outcome); outcome.Result = "Committed";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var lifecycle = new CertificateRenewalOfflineLifecycle { TenantId = Tenant, Id = Guid.NewGuid(), Action = "Enrolled" };
        db.Attach(lifecycle); db.Remove(lifecycle);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Enrollment_ownership_and_bound_key_are_immutable()
    {
        using var db = Context();
        var bound = new CertificateRenewalOfflineEnrollment { TenantId = Tenant, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = Guid.NewGuid(), RenewalId = Guid.NewGuid(), PublicKey = "k1" };
        db.Attach(bound); bound.PublicKey = "k2";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var owned = new CertificateRenewalOfflineEnrollment { TenantId = Tenant, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = Guid.NewGuid(), RenewalId = Guid.NewGuid() };
        db.Attach(owned); owned.RenewalId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var revoked = new CertificateRenewalOfflineEnrollment { TenantId = Tenant, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = Guid.NewGuid(), RenewalId = Guid.NewGuid(), RevokedAt = DateTimeOffset.UtcNow };
        db.Attach(revoked); revoked.RevokedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_closed_results_single_resolution_and_renewal_head_binding()
    {
        using var db = Context();
        var outcome = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CertificateRenewalOfflineOutcome))!;
        Assert.Contains(outcome.GetCheckConstraints(), c => c.Name == "CK_CertificateRenewalOfflineOutcome_State" && c.Sql.Contains("'ServerRetained'") && c.Sql.Contains("\"ParentCommandId\" IS NOT NULL"));
        var resolution = Assert.Single(outcome.GetIndexes(), i => i.GetDatabaseName() == "UX_CertificateRenewalOfflineOutcome_Resolution");
        Assert.True(resolution.IsUnique); Assert.Equal("\"ParentCommandId\" IS NOT NULL", resolution.GetFilter());
        var enrollment = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CertificateRenewalOfflineEnrollment))!;
        var head = Assert.Single(enrollment.GetForeignKeys(), f => f.GetConstraintName() == "FK_CertificateRenewalOffline_Renewal");
        Assert.Equal(typeof(CertificateRenewal), head.PrincipalEntityType.ClrType); Assert.Equal(DeleteBehavior.Restrict, head.DeleteBehavior);
        Assert.Equal("offline", enrollment.GetSchema());
    }
}
