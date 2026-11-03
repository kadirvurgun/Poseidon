using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Poseidon.Persistence.Tests;

// Projection and pre-database guards only. PostgreSQL atomicity is covered separately.
public sealed class AuditCommandContextTests
{
    private const string Role = "9a216160-8ee4-43e0-a319-fb731e8cf12f";
    private static readonly AuditCommandContext Command = new("CreateCompany", "PREPARATION_COMMAND");

    private static PoseidonDbContext Context(ActorContext actor, bool suppressDatabase = false)
    {
        var builder = new DbContextOptionsBuilder<PoseidonDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable_audit_guard;Username=unused");
        if (suppressDatabase) builder.AddInterceptors(new SuppressDatabaseSave());
        return new(builder.Options, Guid.Parse("2ba18fc0-7702-405b-acdd-957e4e520e48"), actor);
    }

    private static void AddCompany(PoseidonDbContext db) => db.Companies.Add(new()
    {
        TenantId = db.TenantId, Id = Guid.NewGuid(), Code = "SYNTHETIC", Name = "FICTIONAL withheld personal text"
    });

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Administrator", true)]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    [InlineData(Role + "," + Role, true)]
    [InlineData(Role, false)]
    public async Task Current_web_writes_require_actual_role_references_and_command_context_before_database_access(string? role, bool command)
    {
        await using var db = Context(new("synthetic-user", "User", "synthetic-request", Source: "WebCommand", RoleContext: role,
            Command: command ? Command : null));
        AddCompany(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Current web writes require trusted role and command provenance.", error.Message);
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Theory]
    [InlineData("CreateCompany", "PREPARATION_COMMAND", true)]
    [InlineData("FileUpload", "FILE_TRANSPORT", true)]
    [InlineData("Offline.Replay-01", "OFFLINE_LIFECYCLE", true)]
    [InlineData("RuleReview", "REGULATORY_WORKFLOW", true)]
    [InlineData("HumanReview", "HUMAN_REVIEW_WORKFLOW", true)]
    [InlineData("QualifySource", "SOURCE_QUALIFICATION", true)]
    [InlineData("", "PREPARATION_COMMAND", false)]
    [InlineData("Create Company", "PREPARATION_COMMAND", false)]
    [InlineData("Create\nCompany", "PREPARATION_COMMAND", false)]
    [InlineData("Üye", "PREPARATION_COMMAND", false)]
    [InlineData("CreateCompany", "user entered rationale", false)]
    [InlineData("CreateCompany", "READY", false)]
    public void Command_provenance_has_a_bounded_nonpersonal_vocabulary(string endpoint, string reason, bool valid) =>
        Assert.Equal(valid, new AuditCommandContext(endpoint, reason).IsValid);

    [Fact]
    public void Command_names_are_bounded_and_invalid_context_never_enters_an_audit_summary()
    {
        Assert.True(new AuditCommandContext(new string('A', 80), "PREPARATION_COMMAND").IsValid);
        Assert.False(new AuditCommandContext(new string('A', 81), "PREPARATION_COMMAND").IsValid);
        using var db = Context(new("synthetic-user", "User", "synthetic-request", Command: new("untrusted/private-value", "PREPARATION_COMMAND")));
        AddCompany(db);
        var error = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Equal("Invalid audit command context.", error.Message);
        Assert.DoesNotContain("untrusted/private-value", error.ToString());
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public async Task An_unclassified_current_receipt_fails_before_database_access_instead_of_inventing_tenant_scope()
    {
        await using var db = Context(new("synthetic-user", "User", "synthetic-request", Source: "WebCommand", RoleContext: Role, Command: Command));
        db.CommandReceipts.Add(new()
        {
            TenantId = db.TenantId, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "UnclassifiedFutureWrite",
            ResourceId = Guid.NewGuid(), Fingerprint = new string('A', 64), CommittedAt = DateTimeOffset.UtcNow
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Current command audit scope is unavailable.", error.Message);
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void Current_projection_records_actual_technical_provenance_without_copying_user_text()
    {
        using var db = Context(new("synthetic-user", "User", "synthetic-request", Source: "WebCommand", RoleContext: Role, Command: Command), true);
        AddCompany(db);
        db.SaveChanges();
        var audit = Assert.Single(db.ChangeTracker.Entries<AuditEvent>()).Entity;
        Assert.Equal("RecordedControlledCode", audit.ReasonStatus);
        Assert.Equal(Role, audit.RoleContext);
        Assert.Null(audit.VesselId);
        Assert.DoesNotContain("FICTIONAL withheld personal text", audit.ChangeSummary);
        Assert.DoesNotContain("SYNTHETIC", audit.ChangeSummary);
        using var fields = JsonDocument.Parse(audit.ChangeSummary!);
        Assert.Equal("CreateCompany", Field(fields, "CommandName"));
        Assert.Equal("PREPARATION_COMMAND", Field(fields, "TechnicalReasonCode"));
        Assert.Equal("Tenant", Field(fields, "ScopeKind"));
        Assert.InRange(fields.RootElement.GetArrayLength(), 1, 20);
        Assert.InRange(audit.ChangeSummary!.Length, 1, 2048);
    }

    [Fact]
    public void Person_owned_aggregates_do_not_guess_a_vessel_from_a_coincidentally_equal_identifier()
    {
        using var db = Context(new("synthetic-user", "User", "synthetic-request", Source: "WebCommand", RoleContext: Role,
            Command: new("CrewPersonSave", "PREPARATION_COMMAND")), true);
        var person = new Person { TenantId = db.TenantId, Id = Guid.NewGuid(), Provenance = "SyntheticUnverified" };
        db.Persons.Add(person);
        db.CrewPersonalProfiles.Add(new() { TenantId = db.TenantId, PersonId = person.Id });
        db.CrewIdentityRevisions.Add(new()
        {
            TenantId = db.TenantId, PersonId = person.Id, Revision = 1, PolicyVersion = "synthetic-empty",
            Purpose = "synthetic-training", Operation = "Capture", SavedAt = DateTimeOffset.UtcNow
        });
        db.CrewPersonalProfileRevisions.Add(new()
        {
            TenantId = db.TenantId, PersonId = person.Id, Revision = 1, PolicyVersion = "synthetic-empty",
            Provenance = "SyntheticUnverified", SavedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        var rows = db.ChangeTracker.Entries<AuditEvent>().Select(x => x.Entity).ToArray();
        Assert.Equal(4, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Null(row.VesselId); Assert.Equal(person.Id, row.ResourceId);
            using var summary = JsonDocument.Parse(row.ChangeSummary!);
            Assert.Equal("TenantPerson", Field(summary, "ScopeKind"));
        });
    }

    [Fact]
    public void System_and_unattributed_legacy_context_do_not_invent_a_human_role_or_command()
    {
        foreach (var actor in new[] { ActorContext.SeedImport(), new ActorContext("synthetic-legacy", "User", "synthetic-old") })
        {
            using var db = Context(actor, true);
            AddCompany(db);
            db.SaveChanges();
            var audit = Assert.Single(db.ChangeTracker.Entries<AuditEvent>()).Entity;
            Assert.Null(audit.RoleContext);
            Assert.Equal(actor.Type == "System" ? "SystemOperation" : "HistoricalUnavailable", audit.ReasonStatus);
            using var summary = JsonDocument.Parse(audit.ChangeSummary!);
            Assert.DoesNotContain(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() is "CommandName" or "TechnicalReasonCode");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Renewed_enrollment_records_real_retained_binding_without_copying_sensitive_values(bool tug)
    {
        using var db = Context(new("synthetic-user", "User", "synthetic-request", Source: "WebCommand", RoleContext: Role,
            Command: new("OfflineRenew", "OFFLINE_LIFECYCLE")), true);
        var vessel = Guid.NewGuid();
        object enrollment = tug ? new TugOfflineEnrollment
        {
            TenantId = db.TenantId, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = vessel,
            JobId = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UnixEpoch, Version = 5
        } : new OfflineEnrollment
        {
            TenantId = db.TenantId, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(), VesselId = vessel,
            AssignmentId = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UnixEpoch, Version = 5
        };
        db.Attach(enrollment);
        db.Entry(enrollment).Property("ExpiresAt").CurrentValue = DateTimeOffset.UtcNow.AddMinutes(15);
        db.SaveChanges();
        var audit = Assert.Single(db.ChangeTracker.Entries<AuditEvent>()).Entity;
        Assert.Equal(vessel, audit.VesselId); Assert.Equal((uint)5, audit.BeforeVersion);
        Assert.Null(audit.AfterVersion);
        using var summary = JsonDocument.Parse(audit.ChangeSummary!);
        var binding = Assert.Single(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == "Binding");
        Assert.Equal("ServerOwnedContext", binding.GetProperty("before").GetString());
        Assert.Equal("ServerOwnedContext", binding.GetProperty("after").GetString());
        Assert.DoesNotContain(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() is "ExpiresAt" or "KeyId" or "PublicKey" or "PublicSigningKey");
    }

    [Fact]
    public void Catalog_and_qualification_projection_normalizes_arrays_and_withholds_rationale_policy_and_hash_values()
    {
        using var db = Context(ActorContext.SeedImport(), true);
        var privateText = "FICTIONAL private rationale";
        var content = JsonSerializer.Serialize(new { note = privateText });
        var hash = new string('A', 64); var sourceHash = new string('B', 64); var authorityHash = new string('C', 64);
        var vessel = Guid.NewGuid(); var rule = Guid.NewGuid();
        db.CrewCatalogEvents.Add(new()
        {
            TenantId = db.TenantId, VesselId = vessel, Revision = 1, State = "Draft", ContentJson = content,
            PolicyVersion = "FICTIONAL-private-policy", Hash = hash, AuthorityHash = authorityHash,
            Reason = privateText, SavedAt = DateTimeOffset.UtcNow
        });
        db.RegulatoryQualificationEvents.Add(new()
        {
            TenantId = db.TenantId, RuleId = rule, Revision = 1, RuleRevision = 1, State = "DRAFT", ContentJson = content,
            SourceHash = sourceHash, Hash = hash, AuthorityHash = authorityHash, ContractJson = content,
            PolicyVersion = "FICTIONAL-private-policy", SavedAt = DateTimeOffset.UtcNow
        });
        db.RegulatoryFactAttestations.Add(new()
        {
            TenantId = db.TenantId, Id = Guid.NewGuid(), VesselId = vessel, RuleId = rule, QualificationRevision = 1,
            ProfileRevision = 1, FieldsJson = content, Hash = hash, AuthorityHash = authorityHash, SavedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        var rows = db.ChangeTracker.Entries<AuditEvent>().Select(x => x.Entity).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.DoesNotContain(privateText, row.ChangeSummary);
            Assert.DoesNotContain("FICTIONAL-private-policy", row.ChangeSummary);
            Assert.DoesNotContain(hash, row.ChangeSummary); Assert.DoesNotContain(sourceHash, row.ChangeSummary);
            Assert.DoesNotContain(authorityHash, row.ChangeSummary);
            using var summary = JsonDocument.Parse(row.ChangeSummary!);
            Assert.Equal(JsonValueKind.Array, summary.RootElement.ValueKind);
            Assert.InRange(summary.RootElement.GetArrayLength(), 1, 20);
            Assert.All(summary.RootElement.EnumerateArray(), field => Assert.Equal(4, field.EnumerateObject().Count()));
        });
    }

    private static string? Field(JsonDocument summary, string name) => Assert.Single(summary.RootElement.EnumerateArray(),
        x => x.GetProperty("field").GetString() == name).GetProperty("after").GetString();

    private sealed class SuppressDatabaseSave : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            InterceptionResult<int>.SuppressWithResult(0);
    }
}
