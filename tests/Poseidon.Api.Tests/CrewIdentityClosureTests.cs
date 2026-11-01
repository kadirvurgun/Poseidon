using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// AUTHORED / NOT RUN. Actual API host + isolated real PostgreSQL fixture.
public sealed partial class DirectoryWorkspaceTests
{
    private string IdentityPath => $"{CrewUrl}/{SeedVessel.Id}/person";
    private async Task<CrewProfile> IdentityDetail(HttpClient client) =>
        (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{SeedVessel.Id}", Ct))!;
    private async Task<CrewProfile> ConvertIdentity(HttpClient client) {
        var before = await IdentityDetail(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath + "/convert", new CrewIdentityConversionCommand(
            before.PersonVersion!.Value, "synthetic-identity-v1", "synthetic-crew-identity"))).StatusCode);
        return await IdentityDetail(client);
    }
    private CrewPersonCommand IdentityCommand(CrewProfile profile, string? name = "FICTIONAL closure", string? employee = "0001") =>
        new(profile.PersonVersion!.Value, name, employee, "synthetic-identity-v1", "synthetic-crew-identity");

    [Fact]
    public async Task Identity_legacy_unavailable_conversion_is_lossless_attributed_encrypted_and_reseed_preserves_operator_edits() {
        await using (var db = Db()) {
            var person = await db.Persons.SingleAsync(x => x.Id == SeedVessel.Id, Ct);
            person.FullName = "FICTIONAL LEGACY IDENTITY"; person.EmployeeNo = "000001"; await db.SaveChangesAsync(Ct);
        }
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var legacy = await IdentityDetail(client); Assert.Equal("LegacyUnavailable", legacy.Assignment.PrivacyScope); Assert.Null(legacy.Assignment.FullName);
        var command = new CrewIdentityConversionCommand(legacy.PersonVersion!.Value, "synthetic-identity-v1", "synthetic-crew-identity");
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath + "/convert", command, key)).StatusCode);
        var current = await IdentityDetail(client); Assert.Equal("FICTIONAL LEGACY IDENTITY", current.Assignment.FullName); Assert.Equal("000001", current.Assignment.EmployeeNo);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath + "/convert", command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, IdentityPath + "/convert", command)).StatusCode);
        await using var verify = Db(); var stored = await verify.Persons.SingleAsync(x => x.Id == SeedVessel.Id, Ct);
        Assert.Null(stored.FullName); Assert.Null(stored.EmployeeNo); Assert.DoesNotContain(current.Assignment.FullName!, stored.ProtectedFullName!);
        Assert.Equal(1, await verify.CrewIdentityRevisions.CountAsync(x => x.PersonId == stored.Id, Ct));
        var audit = await verify.AuditEvents.Where(x => x.EntityType == nameof(Person) || x.EntityType == nameof(CrewIdentityRevision)).ToArrayAsync(Ct);
        Assert.Contains(audit, x => x.ActorId == actorId.ToString());
        Assert.All(audit, x => { Assert.DoesNotContain("FICTIONAL LEGACY IDENTITY", x.ChangeSummary ?? ""); Assert.DoesNotContain(stored.ProtectedFullName!, x.ChangeSummary ?? ""); });
        await using (var reseed = new PoseidonDbContext(Poseidon.Persistence.Seeding.SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true)) {
            await Poseidon.Persistence.Seeding.SeedImporter.ImportAsync(reseed, seed, Ct);
        }
        await verify.Entry(stored).ReloadAsync(Ct); Assert.Equal("ProtectedV1", stored.IdentityStorageState); Assert.Null(stored.FullName);
        Assert.Equal(current.Assignment.FullName, (await IdentityDetail(client)).Assignment.FullName);
    }
    [Fact]
    public async Task Identity_exact_retry_stale_write_parallel_base_history_and_no_rejected_receipts() {
        using var host = ImportHost(); using var client = Client(host); await Token(client); var before = await ConvertIdentity(client);
        var command = IdentityCommand(before); var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, command, key, true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, command, key, true)).StatusCode);
        var rejected = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.Conflict, (await Send(client, IdentityPath, command, rejected, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, IdentityPath, command with { FullName = "FICTIONAL changed retry" }, key, true)).StatusCode);
        var current = await IdentityDetail(client); var parallel = IdentityCommand(current, "FICTIONAL winner");
        var race = await Task.WhenAll(Send(client, IdentityPath, parallel, update: true), Send(client, IdentityPath, parallel with { FullName = "FICTIONAL alternative" }, update: true));
        Assert.Single(race.Where(x => x.StatusCode == HttpStatusCode.OK)); Assert.Single(race.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        var history = await client.GetStringAsync("/api/v1/" + IdentityPath + "/history", Ct);
        Assert.Contains("Declaration", history); Assert.DoesNotContain(command.FullName!, history); Assert.DoesNotContain(command.EmployeeNo!, history);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == rejected, Ct));
        var revision = await db.CrewIdentityRevisions.FirstAsync(x => x.PersonId == SeedVessel.Id, Ct); revision.Operation = "mutate";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Identity_original_cookie_revocation_precedes_projection_history_import_counts_and_recovery() {
        using var host = ProfileHost(); using var client = ImportSessionClient(host); await ImportSession(client);
        var before = await ConvertIdentity(client); var command = IdentityCommand(before); var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, command, key, true)).StatusCode);
        var batch = await UploadImport(client, "crew", new() { ["record_key"] = Guid.NewGuid().ToString(), ["full_name"] = "FICTIONAL stage" });
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(x => x != "crew.profile.identity.read").ToArray() };
        var masked = await IdentityDetail(client); Assert.Null(masked.Assignment.FullName); Assert.Null(masked.PersonVersion);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, IdentityPath, command, key, true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/" + IdentityPath + "/history", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + Imports + $"/{batch.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + Imports + "/templates/crew/download", Ct)).StatusCode);
        Assert.DoesNotContain(batch.Id.ToString(), await client.GetStringAsync("/api/v1/" + Imports, Ct));
    }
    [Fact]
    public async Task Identity_immutable_employee_rule_and_missing_policy_deny_without_private_errors_or_receipts() {
        using var host = ImportHost(); using var client = Client(host); await Token(client); var before = await ConvertIdentity(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, IdentityCommand(before), update: true)).StatusCode);
        var policy = host.Services.GetRequiredService<CrewIdentityPolicy>(); policy.EmployeeNumberMode = "ImmutableAfterCapture";
        var current = await IdentityDetail(client); var key = Guid.NewGuid().ToString();
        var denied = await Send(client, IdentityPath, IdentityCommand(current, employee: "1"), key, true);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.DoesNotContain("0001", await denied.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, IdentityCommand(current, "FICTIONAL renamed", "0001"), update: true)).StatusCode);
        policy.Fields[0].Field = null;
        Assert.Null((await IdentityDetail(client)).Assignment.FullName);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Identity_existing_person_import_partial_update_reuses_ordinary_handler_and_is_encrypted_and_replayable(bool unchanged) {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var before = await ConvertIdentity(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, IdentityPath, IdentityCommand(before), update: true)).StatusCode);
        var current = await IdentityDetail(client);
        var importedName = unchanged ? current.Assignment.FullName! : "FICTIONAL IMPORT UPDATE";
        var batch = await UploadImport(client, "crew", new() { ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = current.Assignment.Version.ToString(),
            ["person_id"] = current.Assignment.PersonId.ToString(), ["person_base_version"] = current.PersonVersion!.Value.ToString(), ["full_name"] = importedName });
        var preview = await PreviewImport(client, batch); Assert.Equal(0, preview.RejectedRows);
        var command = new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint); var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{batch.Id}/commit", command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{batch.Id}/commit", command, key)).StatusCode);
        var imported = await IdentityDetail(client); Assert.Equal(importedName, imported.Assignment.FullName); Assert.Equal("0001", imported.Assignment.EmployeeNo);
        await using var db = Db(); var stage = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct);
        Assert.StartsWith("protected-v1:", stage.TableJson); Assert.DoesNotContain(importedName, stage.TableJson);
        Assert.True(await db.CrewIdentityRevisions.AnyAsync(x => x.PersonId == current.Assignment.PersonId && x.Operation == "ImportedDeclaration", Ct));
        Assert.Equal(2, await db.CommandReceipts.CountAsync(x => x.Action == "CrewPersonSave" && x.ResourceId == current.Assignment.PersonId, Ct));
        var policy = host.Services.GetRequiredService<CrewIdentityPolicy>(); policy.Version = "synthetic-revoked-v2";
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, Imports + $"/{batch.Id}/commit", command, key)).StatusCode);
        Assert.DoesNotContain(batch.Id.ToString(), await client.GetStringAsync("/api/v1/" + Imports, Ct));
    }
    [Fact]
    public async Task Identity_import_wrong_person_missing_base_or_stale_second_row_never_commits_any_row() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var current = await ConvertIdentity(client);
        var newId = Guid.NewGuid();
        var batch = await UploadImport(client, "crew", new() { ["record_key"] = newId.ToString(), ["full_name"] = "FICTIONAL NEW" },
            new() { ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = current.Assignment.Version.ToString(), ["full_name"] = "FICTIONAL REJECTED",
                ["person_id"] = Guid.NewGuid().ToString(), ["person_base_version"] = current.PersonVersion!.Value.ToString() });
        var preview = await PreviewImport(client, batch); Assert.Equal(1, preview.RejectedRows);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        await using var db = Db(); Assert.False(await db.CrewAssignments.AnyAsync(x => x.Id == newId, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct));
        Assert.NotEqual("FICTIONAL REJECTED", (await IdentityDetail(client)).Assignment.FullName);
    }
    [Fact]
    public async Task Position_policy_is_scoped_versioned_exact_default_deny_and_ordinary_import_consistent() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var policy = host.Services.GetRequiredService<CrewPositionPolicy>();
        var dto = await client.GetStringAsync("/api/v1/" + CrewUrl + "/position-policy", Ct); Assert.Contains("synthetic-position-v1", dto);
        var current = await IdentityDetail(client);
        var command = NewCrew(SeedVessel.Id) with { PersonId = SeedVessel.Id, BaseVersion = current.Assignment.Version, PositionId = SeedVessel.Id, PositionPolicyVersion = policy.Version };
        policy.Positions[0].TenantId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, CrewUrl, command)).StatusCode);
        Assert.DoesNotContain(SeedVessel.Id.ToString(), await client.GetStringAsync("/api/v1/" + CrewUrl + "/positions", Ct));
        var batch = await UploadImport(client, "crew", new() { ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = current.Assignment.Version.ToString(), ["position_ref"] = SeedVessel.Id.ToString() });
        Assert.Equal(1, (await PreviewImport(client, batch)).RejectedRows);
        policy.Positions[0].TenantId = seed.Tenants[0].Id; policy.Version = "";
        Assert.Contains("Unavailable", await client.GetStringAsync("/api/v1/" + CrewUrl + "/position-policy", Ct));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, NewCrew())).StatusCode); // Incomplete draft preserves Unknown.
    }
    [Fact]
    public async Task Identity_import_second_ordinary_handler_conflict_rolls_back_first_person_assignment_history_audit_and_receipts() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var current = await ConvertIdentity(client);
        var second = NewCrew() with { PersonId = SeedVessel.Id, From = null, To = null };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, second)).StatusCode);
        var other = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{second.Id}", Ct))!;
        Dictionary<string, string> Row(CrewProfile p, string name) => new() {
            ["record_key"] = p.Assignment.Id.ToString(), ["base_version"] = p.Assignment.Version.ToString(),
            ["person_id"] = SeedVessel.Id.ToString(), ["person_base_version"] = current.PersonVersion!.Value.ToString(), ["full_name"] = name
        };
        var batch = await UploadImport(client, "crew", Row(current, "FICTIONAL FIRST"), Row(other, "FICTIONAL SECOND"));
        var preview = await PreviewImport(client, batch); Assert.Equal(0, preview.RejectedRows);
        int beforeReceipts; int beforeRevisions; int beforeAudit;
        await using (var before = Db()) {
            beforeReceipts = await before.CommandReceipts.CountAsync(Ct); beforeRevisions = await before.CrewIdentityRevisions.CountAsync(Ct);
            beforeAudit = await before.AuditEvents.CountAsync(Ct);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        await using var db = Db(); Assert.Equal(beforeReceipts, await db.CommandReceipts.CountAsync(Ct));
        Assert.Equal(beforeRevisions, await db.CrewIdentityRevisions.CountAsync(Ct)); Assert.Equal(beforeAudit, await db.AuditEvents.CountAsync(Ct));
        Assert.Equal(current.Assignment.Revision, (await IdentityDetail(client)).Assignment.Revision);
        Assert.Equal(current.Assignment.FullName, (await IdentityDetail(client)).Assignment.FullName);
    }
    [Fact]
    public async Task Identity_cross_tenant_and_other_person_paths_and_missing_protection_never_reveal_or_create_receipts() {
        using var host = ImportHost(); using var client = Client(host); await Token(client); var current = await IdentityDetail(client);
        var key = Guid.NewGuid().ToString(); var command = IdentityCommand(current);
        foreach (var vessel in seed.Vessels.Where(v => v.TenantId != seed.Tenants[0].Id)) {
            var path = $"vessels/{vessel.Id}/crew/{vessel.Id}/person";
            Assert.Equal(HttpStatusCode.NotFound, (await Send(client, path, command, update: true)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + path + "/history", Ct)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, $"{CrewUrl}/{Guid.NewGuid()}/person", command, update: true)).StatusCode);
        var protectionPolicy = host.Services.GetRequiredService<CrewProfilePolicy>(); protectionPolicy.KeyReferences.Clear();
        var unavailable = await Send(client, IdentityPath + "/convert", new CrewIdentityConversionCommand(current.PersonVersion!.Value,
            "synthetic-identity-v1", "synthetic-crew-identity"), key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.DoesNotContain(command.FullName!, await unavailable.Content.ReadAsStringAsync(Ct));
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
        Assert.Equal("LegacyUnavailable", (await db.Persons.SingleAsync(x => x.Id == SeedVessel.Id, Ct)).IdentityStorageState);
    }
}
