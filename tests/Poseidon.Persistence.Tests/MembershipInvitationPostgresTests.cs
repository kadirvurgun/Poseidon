using Npgsql;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-ADMIN-02 (QA N2): the database itself enforces "a NULL subject only for an unaccepted AdminProvisioned account" and
// uniqueness over bound accounts only. Raw SQL bypasses the EF guard on purpose: this is the second line of defence.
[Trait("Category", "Integration")]
[Trait("Requirement", "USER-ADMIN-02")]
public sealed class MembershipInvitationPostgresTests(AccountMembershipPostgresFixture fixture) : IClassFixture<AccountMembershipPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<string?> Insert(Guid tenant, string? subject, string source, string issuer = "https://idp.example")
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO identity."Accounts" ("TenantId", "Id", "Issuer", "Subject", "AccountType", "Source", "CreatedAt")
            VALUES (@tenant, @id, @issuer, @subject, 'Human', @source, now())
            """, connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("issuer", issuer);
        command.Parameters.Add(new NpgsqlParameter("subject", NpgsqlTypes.NpgsqlDbType.Varchar) { Value = (object?)subject ?? DBNull.Value });
        command.Parameters.AddWithValue("source", source);
        try { await command.ExecuteNonQueryAsync(Ct); return null; }
        catch (PostgresException error) { return error.SqlState + ":" + error.ConstraintName; }
    }

    [Theory]
    [InlineData("ConfigurationImport")]
    [InlineData("SyntheticUnverified")]
    public async Task A_null_or_empty_subject_is_refused_by_the_database_for_every_source_but_an_admin_invitation(string source)
    {
        var tenant = fixture.Data.Tenants[0].Id;
        Assert.Equal("23514:CK_Accounts_Closed", await Insert(tenant, null, source));
        Assert.Equal("23514:CK_Accounts_Closed", await Insert(tenant, "", source));
        Assert.Equal("23514:CK_Accounts_Closed", await Insert(tenant, "", "AdminProvisioned"));
    }

    [Fact]
    public async Task Unbound_invitation_accounts_reserve_nothing_and_bound_subjects_stay_unique_across_tenants()
    {
        const string issuer = "https://idp-invitation.example";
        Assert.Null(await Insert(fixture.Data.Tenants[0].Id, null, "AdminProvisioned", issuer));
        Assert.Null(await Insert(fixture.Data.Tenants[1].Id, null, "AdminProvisioned", issuer));
        var subject = "bound-" + Guid.NewGuid().ToString("N");
        Assert.Null(await Insert(fixture.Data.Tenants[0].Id, subject, "AdminProvisioned", issuer));
        Assert.Equal("23505:IX_Accounts_IssuerSubject", await Insert(fixture.Data.Tenants[1].Id, subject, "AdminProvisioned", issuer));
    }
}
