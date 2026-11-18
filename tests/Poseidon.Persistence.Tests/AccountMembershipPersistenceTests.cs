using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class AccountMembershipPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public string ConnectionString => postgres.GetConnectionString();
    public PoseidonDbContext Db(Guid? tenant = null, string actor = "synthetic-membership-admin") =>
        new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? Data.Tenants[0].Id, new(actor, "User", "membership-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// PLAN-1511-5202 / P5202-AC-05..07: real PostgreSQL constraints, tenant isolation, immutability and audit.
[Trait("Category", "Integration")]
[Trait("Requirement", "PLAN-1511-5202")]
public sealed class AccountMembershipPersistenceTests(AccountMembershipPostgresFixture fixture) : IClassFixture<AccountMembershipPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (UserAccount Account, AccountMembership Membership) New(Guid tenant, string issuer = "https://idp.example", string? subject = null)
    {
        var account = new UserAccount { TenantId = tenant, Id = Guid.NewGuid(), Issuer = issuer, Subject = subject ?? "sub-" + Guid.NewGuid().ToString("N"),
            Source = "ConfigurationImport", CreatedAt = DateTimeOffset.UtcNow };
        return (account, new AccountMembership { TenantId = tenant, Id = Guid.NewGuid(), AccountId = account.Id, Status = "Active",
            StatusReason = "ConfigurationImport", StatusChangedAt = DateTimeOffset.UtcNow, Source = "ConfigurationImport", CreatedAt = DateTimeOffset.UtcNow });
    }

    [Fact]
    public async Task Issuer_subject_is_unique_across_tenants_and_audit_withholds_identity_values()
    {
        var subject = "unique-" + Guid.NewGuid().ToString("N");
        var (account, membership) = New(fixture.Data.Tenants[0].Id, subject: subject);
        await using (var db = fixture.Db())
        {
            db.UserAccounts.Add(account); db.AccountMemberships.Add(membership);
            await db.SaveChangesAsync(Ct);
            var audit = await db.AuditEvents.AsNoTracking().Where(x => x.ResourceId == account.Id || x.ResourceId == membership.Id).ToArrayAsync(Ct);
            Assert.Equal(2, audit.Length);
            Assert.All(audit, x => { Assert.Equal("synthetic-membership-admin", x.ActorId); Assert.DoesNotContain(subject, x.ChangeSummary); Assert.DoesNotContain("idp.example", x.ChangeSummary); });
            Assert.Contains(audit, x => x.ChangeSummary!.Contains("\"Active\"") && x.ResourceKind == "Membership");
        }
        var (duplicate, _) = New(fixture.Data.Tenants[1].Id, subject: subject);
        await using (var other = fixture.Db(fixture.Data.Tenants[1].Id))
        {
            other.UserAccounts.Add(duplicate);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(Ct));
            Assert.Equal("23505", PostgresFailure.Provider(error)?.SqlState);
        }
    }

    [Fact]
    public async Task Tenant_filter_hides_other_tenant_memberships_and_cross_tenant_writes_are_rejected()
    {
        var (account, membership) = New(fixture.Data.Tenants[0].Id);
        await using (var db = fixture.Db()) { db.UserAccounts.Add(account); db.AccountMemberships.Add(membership); await db.SaveChangesAsync(Ct); }
        await using var other = fixture.Db(fixture.Data.Tenants[1].Id);
        Assert.False(await other.AccountMemberships.AnyAsync(x => x.Id == membership.Id, Ct));
        Assert.False(await other.UserAccounts.AnyAsync(x => x.Id == account.Id, Ct));
        var (foreign, foreignMembership) = New(fixture.Data.Tenants[0].Id);
        other.UserAccounts.Add(foreign); other.AccountMemberships.Add(foreignMembership);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.SaveChangesAsync(Ct));
        other.ChangeTracker.Clear();
        // Composite FK: a tenant-1 membership cannot point at a tenant-0 account even through the database.
        await Assert.ThrowsAnyAsync<Exception>(() => other.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO identity.\"Memberships\" (\"TenantId\",\"Id\",\"AccountId\",\"Status\",\"StatusReason\",\"StatusChangedAt\",\"AccessRevision\",\"Source\",\"CreatedAt\") VALUES ({fixture.Data.Tenants[1].Id},{Guid.NewGuid()},{account.Id},'Active','ConfigurationImport',now(),0,'ConfigurationImport',now())", Ct));
    }

    [Fact]
    public async Task Lifecycle_changes_advance_one_access_revision_and_revoked_is_terminal()
    {
        var (account, membership) = New(fixture.Data.Tenants[0].Id);
        await using var db = fixture.Db();
        db.UserAccounts.Add(account); db.AccountMemberships.Add(membership); await db.SaveChangesAsync(Ct);
        membership.Status = "Suspended"; membership.StatusReason = "AdministrativeSuspension";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // No revision advance.
        membership.AccessRevision = 1; await db.SaveChangesAsync(Ct);
        membership.RoleContext = Guid.NewGuid().ToString("D");
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // Grant snapshot immutable.
        db.Entry(membership).Property(x => x.RoleContext).IsModified = false; membership.RoleContext = null;
        membership.Status = "Revoked"; membership.StatusReason = "AdministrativeRevocation"; membership.AccessRevision = 2;
        await db.SaveChangesAsync(Ct);
        membership.Status = "Active"; membership.StatusReason = "AdministrativeResume"; membership.AccessRevision = 3;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        var stored = await db.AccountMemberships.AsNoTracking().SingleAsync(x => x.Id == membership.Id, Ct);
        Assert.Equal("Revoked", stored.Status); Assert.Equal(2, stored.AccessRevision);
        var account2 = await db.UserAccounts.SingleAsync(x => x.Id == account.Id, Ct);
        account2.Subject = "rebound";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // Identity rebind is not a field edit.
        db.ChangeTracker.Clear();
        db.AccountMemberships.Remove(await db.AccountMemberships.SingleAsync(x => x.Id == membership.Id, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Database_constraints_reject_unknown_states_and_invalid_delegation_windows()
    {
        var (account, membership) = New(fixture.Data.Tenants[0].Id);
        var (account2, membership2) = New(fixture.Data.Tenants[0].Id);
        await using var db = fixture.Db();
        db.UserAccounts.AddRange(account, account2); db.AccountMemberships.AddRange(membership, membership2); await db.SaveChangesAsync(Ct);
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.\"Memberships\" SET \"Status\"='Ready' WHERE \"Id\"={membership.Id}", Ct));
        var now = DateTimeOffset.UtcNow;
        db.MembershipDelegations.Add(new MembershipDelegation { TenantId = db.TenantId, Id = Guid.NewGuid(), DelegatorMembershipId = membership.Id,
            DelegateMembershipId = membership2.Id, Permissions = "crew.read", ValidFrom = now, ValidUntil = now.AddDays(1), Status = "Active",
            ApprovedBy = "synthetic-membership-admin", CreatedAt = now, ReasonCode = "PlannedAbsence" });
        await db.SaveChangesAsync(Ct);
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO identity.\"Delegations\" (\"TenantId\",\"Id\",\"DelegatorMembershipId\",\"DelegateMembershipId\",\"Permissions\",\"ValidFrom\",\"ValidUntil\",\"Status\",\"ApprovedBy\",\"CreatedAt\",\"ReasonCode\") VALUES ({db.TenantId},{Guid.NewGuid()},{membership.Id},{membership2.Id},'crew.read',now(),now() - interval '1 day','Active','x',now(),'PlannedAbsence')", Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO identity.\"Delegations\" (\"TenantId\",\"Id\",\"DelegatorMembershipId\",\"DelegateMembershipId\",\"Permissions\",\"ValidFrom\",\"ValidUntil\",\"Status\",\"ApprovedBy\",\"CreatedAt\",\"ReasonCode\") VALUES ({db.TenantId},{Guid.NewGuid()},{membership.Id},{membership.Id},'crew.read',now(),now() + interval '1 day','Active','x',now(),'PlannedAbsence')", Ct));
        var delegation = await db.MembershipDelegations.SingleAsync(x => x.DelegatorMembershipId == membership.Id, Ct);
        delegation.ValidUntil = delegation.ValidUntil.AddDays(30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // A window never extends itself.
    }

    // QA follow-up: delegation scope snapshot + closed reason (52.02 paragraph:102) and one-time
    // audited closure of RECOVERY_REQUIRED.
    [Fact]
    public async Task Delegation_scope_snapshot_is_immutable_reason_is_closed_and_recovery_closes_once()
    {
        var (account, membership) = New(fixture.Data.Tenants[0].Id);
        var (account2, membership2) = New(fixture.Data.Tenants[0].Id);
        var vessel = fixture.Data.Vessels.First(v => v.TenantId == fixture.Data.Tenants[0].Id);
        var otherVessel = fixture.Data.Vessels.First(v => v.TenantId == fixture.Data.Tenants[1].Id);
        await using var db = fixture.Db();
        db.UserAccounts.AddRange(account, account2); db.AccountMemberships.AddRange(membership, membership2); await db.SaveChangesAsync(Ct);
        var now = DateTimeOffset.UtcNow;
        MembershipDelegation Delegation(string reason) => new() { TenantId = db.TenantId, Id = Guid.NewGuid(), DelegatorMembershipId = membership.Id,
            DelegateMembershipId = membership2.Id, Permissions = "crew.read", ValidFrom = now, ValidUntil = now.AddDays(1), Status = "Active",
            ApprovedBy = "synthetic-membership-admin", CreatedAt = now, ReasonCode = reason, CompanyId = vessel.CompanyId };
        db.MembershipDelegations.Add(Delegation("Free text reason"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        db.MembershipDelegations.Add(Delegation(DelegationReasonLegacy));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // Legacy marker is migration-only.
        db.ChangeTracker.Clear();
        var delegation = Delegation("PlannedAbsence");
        db.MembershipDelegations.Add(delegation);
        var scope = new DelegationVesselScope { TenantId = db.TenantId, DelegationId = delegation.Id, VesselId = vessel.Id, CanEdit = false };
        db.DelegationVesselScopes.Add(scope);
        await db.SaveChangesAsync(Ct);
        var audit = await db.AuditEvents.AsNoTracking().Where(x => x.ResourceId == delegation.Id).ToArrayAsync(Ct);
        Assert.Contains(audit, x => x.ResourceKind == "Delegation" && x.ChangeSummary!.Contains("PlannedAbsence") && !x.ChangeSummary.Contains("crew.read"));
        Assert.Contains(audit, x => x.ResourceKind == "DelegationVesselScope" && x.VesselId == vessel.Id);
        scope.CanEdit = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // Scope snapshot is immutable.
        db.ChangeTracker.Clear();
        var tracked = await db.MembershipDelegations.SingleAsync(x => x.Id == delegation.Id, Ct);
        tracked.ReasonCode = "Handover";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        // Database second line: unknown reason and a cross-tenant vessel are rejected.
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.\"Delegations\" SET \"ReasonCode\"='Other' WHERE \"Id\"={delegation.Id}", Ct));
        // NULL never satisfies a closed set (SQL three-valued logic hardened in the constraint).
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.\"Delegations\" SET \"Status\"='Revoked', \"RevokedAt\"=now(), \"RevocationReason\"=NULL WHERE \"Id\"={delegation.Id}", Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO identity.\"DelegationVesselScopes\" (\"TenantId\",\"DelegationId\",\"VesselId\",\"CanEdit\") VALUES ({db.TenantId},{delegation.Id},{otherVessel.Id},false)", Ct));

        var recovery = new TenantAccessRecovery { TenantId = db.TenantId, Id = Guid.NewGuid(), MembershipId = membership.Id, Cause = "SecuritySuspension", RaisedAt = now };
        db.TenantAccessRecoveries.Add(recovery); await db.SaveChangesAsync(Ct);
        recovery.ResolvedAt = now.AddMinutes(1); recovery.Resolution = "Whatever";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        recovery.Resolution = "SecuritySuspensionResumed";
        await db.SaveChangesAsync(Ct);
        Assert.Contains(await db.AuditEvents.AsNoTracking().Where(x => x.ResourceId == membership.Id && x.ResourceKind == "AccessRecovery").ToArrayAsync(Ct),
            x => x.ChangeSummary!.Contains("SecuritySuspensionResumed"));
        recovery.ResolvedAt = now.AddMinutes(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); // Closed exactly once.
        db.ChangeTracker.Clear();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE identity.\"AccessRecoveries\" SET \"Resolution\"=NULL WHERE \"Id\"={recovery.Id}", Ct));
    }

    private const string DelegationReasonLegacy = "LegacyUnscoped";

    [Fact]
    public async Task Optional_fixture_is_idempotent_synthetic_and_requires_administrative_import()
    {
        await using (var ordinary = fixture.Db())
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => AccountMembershipFixtures.AddMissingAsync(ordinary, fixture.Data.Tenants, Ct));
        await using (var import = fixture.Import())
        {
            await AccountMembershipFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct);
            await import.SaveChangesAsync(Ct);
        }
        int audits;
        await using (var db = fixture.Db()) audits = await db.AuditEvents.CountAsync(Ct);
        await using (var import = fixture.Import())
        {
            await AccountMembershipFixtures.AddMissingAsync(import, fixture.Data.Tenants, Ct);
            await import.SaveChangesAsync(Ct);
        }
        await using var verify = fixture.Db();
        Assert.Equal(audits, await verify.AuditEvents.CountAsync(Ct));
        var account = await verify.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == AccountMembershipFixtures.AccountId(verify.TenantId), Ct);
        Assert.Equal(AccountMembershipFixtures.SyntheticIssuer, account.Issuer);
        Assert.EndsWith(".invalid", account.Issuer);
        Assert.Equal("SyntheticUnverified", account.Source);
        var membership = await verify.AccountMemberships.AsNoTracking().SingleAsync(x => x.Id == AccountMembershipFixtures.MembershipId(verify.TenantId), Ct);
        Assert.Null(membership.RoleContext); Assert.Null(membership.ActiveCompanyId);
        Assert.False(await verify.AccountMembershipVesselGrants.AnyAsync(x => x.MembershipId == membership.Id, Ct));
        Assert.False(await verify.MembershipDelegations.AnyAsync(x => x.DelegatorMembershipId == membership.Id || x.DelegateMembershipId == membership.Id, Ct));
    }
}
