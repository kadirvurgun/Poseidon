using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class HrPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public string Connection => postgres.GetConnectionString();
    public PoseidonDbContext Db(Guid? tenant = null, string actor = "synthetic-hr-author") =>
        new(SeedImporter.Options(Connection), tenant ?? Data.Tenants[0].Id, new(actor, "User", "hr-5201-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(Connection), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// PLAN-1511-5201 on real PostgreSQL: single company/person master, tenant isolation, race backstops, append-only history.
[Trait("Category", "Integration")]
[Trait("Requirement", "PLAN-1511-5201")]
public sealed class HrPersistenceTests(HrPostgresFixture fixture) : IClassFixture<HrPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset T0 = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private Company SeedCompany => fixture.Data.Companies[0];
    private Vessel SeedVessel => fixture.Data.Vessels.First(v => v.TenantId == fixture.Data.Tenants[0].Id);

    private async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Connection); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }

    [Fact]
    public async Task Migration_extends_existing_companies_and_persons_in_place_without_inventing_values()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(Ct);
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261117090000_FleetCatalogDraftFoundation", Ct);
        var (tenant, company, person) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using (var connection = new NpgsqlConnection(postgres.GetConnectionString()))
        {
            await connection.OpenAsync(Ct);
            // Pre-existing rows written by the previous schema (controlled migration fixture, not a business write).
            await using var insert = new NpgsqlCommand($"""
                INSERT INTO core."Tenants" ("Id","Code","Name") VALUES ('{tenant}','LEGACY-T','Legacy tenant');
                INSERT INTO core."Companies" ("TenantId","Id","Code","Name") VALUES ('{tenant}','{company}','LEGACY-CO','Legacy display name');
                INSERT INTO crew."Persons" ("TenantId","Id","IdentityStorageState","IdentityRevision","EmploymentState","Provenance")
                  VALUES ('{tenant}','{person}','LegacyUnavailable',0,'Unknown','UserDeclaredUnverified');
                """, connection);
            await insert.ExecuteNonQueryAsync(Ct);
        }
        await migrator.MigrateAsync(null, Ct);
        db.ChangeTracker.Clear();
        var row = await db.Companies.AsNoTracking().SingleAsync(x => x.Id == company, Ct);
        // Same id (companyId == legalEntityId), same code/name; unknown legal values stay unknown and the entity is an incomplete Draft.
        Assert.Equal(("LEGACY-CO", "Legacy display name", tenant), (row.Code, row.Name, row.TenantId));
        Assert.Equal("Draft", row.LegalEntityStatus); Assert.Equal(0, row.LegalEntityRevision);
        Assert.Null(row.LegalName); Assert.Null(row.CountryCode); Assert.Null(row.HrTimeZone); Assert.Null(row.BaseLocale); Assert.Null(row.EffectiveFrom);
        var p = await db.Persons.AsNoTracking().SingleAsync(x => x.Id == person, Ct);
        Assert.Equal(("Unknown", "Draft", 0L), (p.PersonType, p.MasterState, p.MasterRevision));
        Assert.Empty(await db.LegalEntityFieldRevisions.ToArrayAsync(Ct));
        Assert.Empty(await db.Employments.ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Active_legal_entity_requires_complete_values_at_database_level()
    {
        var id = Guid.NewGuid();
        await using var db = fixture.Db();
        db.Companies.Add(new Company { TenantId = db.TenantId, Id = id, Code = "HR-DB-" + id.ToString("N")[..6], Name = "Synthetic", LegalEntityStatus = "Active" });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal("23514", PostgresFailure.Provider(error)?.SqlState);
    }

    [Fact]
    public async Task Seed_is_idempotent_unverified_and_contains_no_approved_or_actual_records()
    {
        await using (var again = fixture.Import()) await SeedImporter.ImportAsync(again, fixture.Data, Ct);
        await using var db = fixture.Import();
        Assert.Equal(fixture.Data.Companies.Length, await db.LegalEntityRoles.CountAsync(x => x.Role == "Employer" && x.SourceKind == "Synthetic", Ct));
        Assert.Equal(fixture.Data.Vessels.Length, await db.Employments.CountAsync(x => x.EmployeeNo.StartsWith("SYN-"), Ct));
        var seeded = await db.Employments.Where(x => x.EmployeeNo.StartsWith("SYN-")).ToArrayAsync(Ct);
        Assert.All(seeded, e => Assert.Equal("Draft", e.State));
        var ids = seeded.Select(x => x.Id).ToArray();
        var plans = await db.HrAssignments.Where(x => ids.Contains(x.EmploymentId)).ToArrayAsync(Ct);
        Assert.Equal(fixture.Data.Vessels.Length, plans.Length);
        Assert.All(plans, a => { Assert.Equal("Planned", a.State); Assert.Null(a.ApprovedBy); });
        Assert.All(await db.LegalEntityRoles.ToArrayAsync(Ct), r => Assert.Equal("Unverified", r.VerificationState));
        Assert.All(await db.Persons.ToArrayAsync(Ct), p => Assert.NotEqual("Verified", p.MasterState));
        Assert.Empty(await db.HrActualEvents.Where(x => x.SourceSystem.StartsWith("SYN")).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Tenant_filters_hide_other_tenant_rows_and_cross_tenant_writes_are_rejected()
    {
        var other = fixture.Data.Tenants[1].Id;
        await using var db = fixture.Db(other);
        Assert.False(await db.Companies.AnyAsync(x => x.Id == SeedCompany.Id, Ct));
        Assert.False(await db.Employments.AnyAsync(x => x.LegalEntityId == SeedCompany.Id, Ct));
        Assert.False(await db.LegalEntityIdentifiers.AnyAsync(x => x.LegalEntityId == SeedCompany.Id, Ct));
        db.LegalEntityRoles.Add(new LegalEntityRole { TenantId = fixture.Data.Tenants[0].Id, Id = Guid.NewGuid(), LegalEntityId = SeedCompany.Id,
            Role = "Employer", EffectiveFrom = T0, SourceKind = "UserDeclared", CreatedBy = "x", CreatedAt = T0 });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync(Ct));
        // A same-tenant reference to a foreign-tenant company cannot be expressed: the composite FK includes TenantId.
        await using var own = fixture.Db(other);
        own.LegalEntityRoles.Add(new LegalEntityRole { TenantId = other, Id = Guid.NewGuid(), LegalEntityId = SeedCompany.Id,
            Role = "Employer", EffectiveFrom = T0, SourceKind = "UserDeclared", CreatedBy = "x", CreatedAt = T0 });
        var fk = await Assert.ThrowsAsync<DbUpdateException>(() => own.SaveChangesAsync(Ct));
        Assert.Equal("23503", PostgresFailure.Provider(fk)?.SqlState);
    }

    [Fact]
    public async Task Database_exclusion_constraints_block_overlapping_operator_employee_number_and_approved_occupancy()
    {
        var tenant = fixture.Data.Tenants[0].Id;
        var company = Guid.NewGuid(); var other = Guid.NewGuid();
        await using (var db = fixture.Db())
        {
            db.Companies.Add(new Company { TenantId = tenant, Id = company, Code = "HR-EX-" + company.ToString("N")[..6], Name = "Synthetic A" });
            db.Companies.Add(new Company { TenantId = tenant, Id = other, Code = "HR-EX-" + other.ToString("N")[..6], Name = "Synthetic B" });
            await db.SaveChangesAsync(Ct);
        }
        // A vessel without other operator rows keeps this test independent of other test classes.
        var vessel = Guid.NewGuid();
        await using (var db = fixture.Db()) { db.Vessels.Add(new Vessel { TenantId = tenant, Id = vessel, CompanyId = company, Code = "HR-V-" + vessel.ToString("N")[..6], Name = "Synthetic vessel", Kind = VesselKind.Ship }); await db.SaveChangesAsync(Ct); }
        LegalEntityRole Operator(Guid entity, int from, int? to) => new() { TenantId = tenant, Id = Guid.NewGuid(), LegalEntityId = entity, Role = "Operator",
            VesselId = vessel, EffectiveFrom = T0.AddDays(from), EffectiveTo = to is { } d ? T0.AddDays(d) : null, SourceKind = "UserDeclared", CreatedBy = "x", CreatedAt = T0 };
        await using (var db = fixture.Db()) { db.LegalEntityRoles.Add(Operator(company, 0, 100)); await db.SaveChangesAsync(Ct); }
        await using (var db = fixture.Db())
        {
            db.LegalEntityRoles.Add(Operator(other, 50, null));
            Assert.Equal("23P01", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
        await using (var db = fixture.Db()) { db.LegalEntityRoles.Add(Operator(other, 100, null)); await db.SaveChangesAsync(Ct); }

        Employment Employment(string number, int from, int? to, string state = "Draft") => new() { TenantId = tenant, Id = Guid.NewGuid(), PersonId = SeedVessel.Id,
            LegalEntityId = company, EmployeeNo = number, EmploymentType = "Employee", StartAt = T0.AddDays(from), EndAt = to is { } d ? T0.AddDays(d) : null,
            State = state, CreatedBy = "creator", CreatedAt = T0 };
        var first = Employment("EX-1", 0, 365);
        await using (var db = fixture.Db()) { db.Employments.Add(first); await db.SaveChangesAsync(Ct); }
        await using (var db = fixture.Db())
        {
            db.Employments.Add(Employment("EX-1", 100, null));
            Assert.Equal("23P01", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
        await using (var db = fixture.Db()) { db.Employments.Add(Employment("EX-1", 365, null)); db.Employments.Add(Employment("EX-1", 10, 20, "Cancelled")); await db.SaveChangesAsync(Ct); }

        HrAssignment Plan(int from, int to, string state) => new() { TenantId = tenant, Id = Guid.NewGuid(), EmploymentId = first.Id, PersonId = first.PersonId,
            LegalEntityId = company, Kind = "Vessel", VesselId = vessel, PlannedFrom = T0.AddDays(from), PlannedTo = T0.AddDays(to), PlannedZoneId = "UTC",
            State = state, PlannedBy = "planner", PlannedAt = T0, ApprovedBy = state == "Approved" ? "approver" : null, ApprovedAt = state == "Approved" ? T0 : null };
        await using (var db = fixture.Db())
        {
            // Plan alternatives may overlap; approved occupancy may not.
            db.HrAssignments.AddRange(Plan(0, 30, "Approved"), Plan(10, 40, "Planned"), Plan(15, 45, "Planned"));
            await db.SaveChangesAsync(Ct);
        }
        await using (var db = fixture.Db())
        {
            db.HrAssignments.Add(Plan(20, 50, "Approved"));
            Assert.Equal("23P01", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
        await using (var db = fixture.Db())
        {
            // The planner cannot be the approver, even below the API.
            var self = Plan(60, 70, "Approved"); self.ApprovedBy = self.PlannedBy;
            db.HrAssignments.Add(self);
            Assert.Equal("23514", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
    }

    [Fact]
    public async Task Field_revisions_and_actual_facts_are_append_only_in_context_and_database()
    {
        await using (var db = fixture.Db())
        {
            db.LegalEntityFieldRevisions.Add(new LegalEntityFieldRevision { TenantId = db.TenantId, LegalEntityId = SeedCompany.Id, Revision = 9001,
                Field = "LegalName", Value = "Synthetic Legal Name", SourceKind = "UserDeclared", ActorId = "synthetic-hr-author", RecordedAt = T0 });
            await db.SaveChangesAsync(Ct);
            var audit = await db.AuditEvents.Where(x => x.EntityType == nameof(LegalEntityFieldRevision) && x.ResourceId == SeedCompany.Id).ToArrayAsync(Ct);
            Assert.NotEmpty(audit);
            Assert.All(audit, x => Assert.DoesNotContain("Synthetic Legal Name", x.ChangeSummary));
        }
        await using (var db = fixture.Db())
        {
            var row = await db.LegalEntityFieldRevisions.SingleAsync(x => x.Revision == 9001, Ct);
            row.Value = "Tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        foreach (var sql in new[] { "UPDATE hr.\"LegalEntityFieldRevisions\" SET \"Value\"='x'", "DELETE FROM hr.\"LegalEntityFieldRevisions\"",
            "TRUNCATE hr.\"LegalEntityFieldRevisions\"", "DELETE FROM hr.\"ActualEvents\"", "UPDATE hr.\"PersonFieldRevisions\" SET \"Value\"='x'" })
            Assert.Equal("55000", (await Assert.ThrowsAsync<PostgresException>(() => Sql(sql))).SqlState);
    }

    // QA follow-up: reasons are retained as append-only state-change rows and withheld from audit summaries (52.01 paragraph:50).
    [Fact]
    public async Task State_changes_keep_reasons_append_only_and_out_of_audit_summaries()
    {
        const string reason = "Synthetic retirement reason 5201";
        var id = Guid.NewGuid();
        await using (var db = fixture.Db())
        {
            db.HrStateChanges.Add(new HrStateChange { TenantId = db.TenantId, Id = id, LegalEntityId = SeedCompany.Id, SubjectKind = "LegalEntityIdentifier",
                SubjectId = Guid.NewGuid(), FromState = "Current", ToState = "Retired", Reason = reason, ActorId = "synthetic-hr-author", RecordedAt = T0 });
            db.LegalEntityFieldRevisions.Add(new LegalEntityFieldRevision { TenantId = db.TenantId, LegalEntityId = SeedCompany.Id, Revision = 9101,
                Field = "CountryCode", Value = "TR", SourceKind = "UserDeclared", Reason = reason, ActorId = "synthetic-hr-author", RecordedAt = T0 });
            await db.SaveChangesAsync(Ct);
            var audit = await db.AuditEvents.Where(x => (x.EntityType == nameof(HrStateChange) || x.EntityType == nameof(LegalEntityFieldRevision)) && x.ResourceId == SeedCompany.Id).ToArrayAsync(Ct);
            Assert.Contains(audit, x => x.EntityType == nameof(HrStateChange));
            Assert.All(audit, x => { Assert.DoesNotContain(reason, x.ChangeSummary); Assert.Contains("ValuesWithheld", x.ChangeSummary); });
        }
        await using (var db = fixture.Db())
        {
            var row = await db.HrStateChanges.SingleAsync(x => x.Id == id, Ct);
            row.Reason = "Tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using (var other = fixture.Db(fixture.Data.Tenants[1].Id)) Assert.False(await other.HrStateChanges.AnyAsync(x => x.Id == id, Ct));
        foreach (var sql in new[] { "UPDATE hr.\"StateChanges\" SET \"Reason\"='x'", "DELETE FROM hr.\"StateChanges\"", "TRUNCATE hr.\"StateChanges\"" })
            Assert.Equal("55000", (await Assert.ThrowsAsync<PostgresException>(() => Sql(sql))).SqlState);
        // An empty reason is rejected below the API as well.
        await using (var db = fixture.Db())
        {
            db.HrStateChanges.Add(new HrStateChange { TenantId = db.TenantId, Id = Guid.NewGuid(), LegalEntityId = SeedCompany.Id, SubjectKind = "Employment",
                SubjectId = Guid.NewGuid(), FromState = "Draft", ToState = "Active", Reason = "  ", ActorId = "x", RecordedAt = T0 });
            Assert.Equal("23514", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
    }

    [Fact]
    public async Task Cancelled_roles_are_retained_terminal_independent_and_excluded_from_cardinality()
    {
        var tenant = fixture.Data.Tenants[0].Id;
        var (company, other, vessel) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using (var db = fixture.Db())
        {
            db.Companies.Add(new Company { TenantId = tenant, Id = company, Code = "HR-CX-" + company.ToString("N")[..6], Name = "Synthetic C" });
            db.Companies.Add(new Company { TenantId = tenant, Id = other, Code = "HR-CX-" + other.ToString("N")[..6], Name = "Synthetic D" });
            db.Vessels.Add(new Vessel { TenantId = tenant, Id = vessel, CompanyId = company, Code = "HR-CV-" + vessel.ToString("N")[..6], Name = "Synthetic vessel", Kind = VesselKind.Ship });
            await db.SaveChangesAsync(Ct);
        }
        var mistaken = new LegalEntityRole { TenantId = tenant, Id = Guid.NewGuid(), LegalEntityId = company, Role = "Operator", VesselId = vessel,
            EffectiveFrom = T0, SourceKind = "UserDeclared", CreatedBy = "author", CreatedAt = T0 };
        await using (var db = fixture.Db()) { db.LegalEntityRoles.Add(mistaken); await db.SaveChangesAsync(Ct); }
        // The creator cannot cancel their own relation, even below the API.
        await using (var db = fixture.Db())
        {
            var row = await db.LegalEntityRoles.SingleAsync(x => x.Id == mistaken.Id, Ct);
            (row.State, row.CancelledBy, row.CancelledAt) = ("Cancelled", "author", T0);
            Assert.Equal("23514", PostgresFailure.Provider(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)))?.SqlState);
        }
        await using (var db = fixture.Db())
        {
            var row = await db.LegalEntityRoles.SingleAsync(x => x.Id == mistaken.Id, Ct);
            (row.State, row.CancelledBy, row.CancelledAt) = ("Cancelled", "reviewer", T0);
            await db.SaveChangesAsync(Ct);
        }
        // The cancelled operator no longer blocks the correct operator for the same vessel and period.
        await using (var db = fixture.Db())
        {
            db.LegalEntityRoles.Add(new LegalEntityRole { TenantId = tenant, Id = Guid.NewGuid(), LegalEntityId = other, Role = "Operator", VesselId = vessel,
                EffectiveFrom = T0, SourceKind = "UserDeclared", CreatedBy = "author", CreatedAt = T0 });
            await db.SaveChangesAsync(Ct);
        }
        // Cancellation is terminal: the retained row cannot be reinstated.
        await using (var db = fixture.Db())
        {
            var row = await db.LegalEntityRoles.SingleAsync(x => x.Id == mistaken.Id, Ct);
            Assert.Equal("Cancelled", row.State);
            (row.State, row.CancelledBy, row.CancelledAt) = ("Recorded", null, null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
    }

    // QA follow-up: the occupancy exclusion constraint holds under truly concurrent approvals (one commits, one 23P01).
    [Fact]
    public async Task Concurrent_overlapping_approvals_are_resolved_by_the_exclusion_constraint()
    {
        var tenant = fixture.Data.Tenants[0].Id;
        var company = Guid.NewGuid();
        var person = SeedVessel.Id;
        var employment = Guid.NewGuid();
        await using (var db = fixture.Db())
        {
            db.Companies.Add(new Company { TenantId = tenant, Id = company, Code = "HR-CC-" + company.ToString("N")[..6], Name = "Synthetic E" });
            db.Employments.Add(new Employment { TenantId = tenant, Id = employment, PersonId = person, LegalEntityId = company, EmployeeNo = "CC-" + employment.ToString("N")[..8],
                EmploymentType = "Employee", StartAt = T0.AddYears(5), CreatedBy = "creator", CreatedAt = T0 });
            await db.SaveChangesAsync(Ct);
        }
        HrAssignment Approved(int from, int to) => new() { TenantId = tenant, Id = Guid.NewGuid(), EmploymentId = employment, PersonId = person, LegalEntityId = company,
            Kind = "Shore", PlannedFrom = T0.AddYears(5).AddDays(from), PlannedTo = T0.AddYears(5).AddDays(to), PlannedZoneId = "UTC", State = "Approved",
            PlannedBy = "planner", PlannedAt = T0, ApprovedBy = "approver", ApprovedAt = T0 };
        // Both transactions insert before either commits; the barrier makes the overlap genuinely concurrent.
        using var barrier = new Barrier(2);
        async Task<string?> Approve(HrAssignment plan)
        {
            await using var db = fixture.Db();
            await using var tx = await db.Database.BeginTransactionAsync(Ct);
            await db.Database.ExecuteSqlRawAsync("SELECT 1", Ct);
            barrier.SignalAndWait(TimeSpan.FromSeconds(30), Ct);
            try
            {
                db.HrAssignments.Add(plan);
                await db.SaveChangesAsync(Ct);
                await tx.CommitAsync(Ct);
                return null;
            }
            catch (Exception error) when (PostgresFailure.IsDatabase(error)) { return PostgresFailure.Provider(error)?.SqlState; }
        }
        var results = await Task.WhenAll(Task.Run(() => Approve(Approved(0, 30)), Ct), Task.Run(() => Approve(Approved(10, 40)), Ct));
        Assert.Single(results, x => x == null);
        Assert.Single(results, x => x == "23P01");
        await using var check = fixture.Db();
        Assert.Equal(1, await check.HrAssignments.CountAsync(x => x.EmploymentId == employment && x.State == "Approved", Ct));
    }

    [Fact]
    public async Task Plan_values_and_identity_are_immutable_and_history_is_never_deleted()
    {
        await using var db = fixture.Db();
        var plan = await db.HrAssignments.FirstAsync(Ct);
        plan.PlannedTo = plan.PlannedTo.AddDays(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        var employment = await db.Employments.FirstAsync(Ct);
        db.Employments.Remove(employment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
}
