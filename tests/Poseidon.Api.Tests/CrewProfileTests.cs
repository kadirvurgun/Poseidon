using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string PersonalPath => $"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/personal-profile";
    private WebApplicationFactory<Program> ProfileHost(bool write = true, bool contact = true) => ImportHost().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, builder) => {
        var config = new Dictionary<string, string?>();
        var fields = Enum.GetValues<CrewPersonalField>();
        for (var i = 0; i < fields.Length; i++) {
            var f = fields[i]; var group = f is CrewPersonalField.EmergencyContact or CrewPersonalField.ContactPhone or CrewPersonalField.Email or CrewPersonalField.PreferredLanguage ? "contact" :
                f is CrewPersonalField.CompanyStartDate or CrewPersonalField.SeaServiceMonths or CrewPersonalField.TankerExperienceMonths or CrewPersonalField.VesselTypeExperience or CrewPersonalField.CompanyExperienceMonths ? "employment" : "identity";
            config[$"CrewProfilePolicy:Fields:{i}:Field"] = f.ToString(); config[$"CrewProfilePolicy:Fields:{i}:Purpose"] = "crew-profile";
            config[$"CrewProfilePolicy:Fields:{i}:ReadCapability"] = $"crew.profile.{group}.read"; config[$"CrewProfilePolicy:Fields:{i}:WriteCapability"] = $"crew.profile.{group}.write";
            if (CrewProfilePolicy.Controlled(f)) config[$"CrewProfilePolicy:Fields:{i}:ControlledValues:0"] = "SYNTHETIC-UNVERIFIED";
        }
        var permissions = new[] { "crew.profile.identity.read", "crew.profile.identity.write", "crew.profile.contact.read", "crew.profile.contact.write", "crew.profile.employment.read", "crew.profile.employment.write" };
        for (var i = 0; i < permissions.Length; i++) config[$"Authentication:Oidc:Memberships:0:Permissions:{27+i}"] =
            (!write && permissions[i].EndsWith("write", StringComparison.Ordinal) || !contact && permissions[i].Contains("contact", StringComparison.Ordinal)) ? "unused" : permissions[i];
        builder.AddInMemoryCollection(config);
    }));
    private CrewPersonalData SyntheticProfile() => new("SYNTHETIC-UNVERIFIED", new(1990, 2, 3), "SYNTHETIC-PASSPORT-000001", new(2030, 1, 2),
        "SYNTHETIC-SEAMAN-000002", new(2031, 2, 3), new(2020, 4, 5), "123456789012345678901234567890.000000000001", "2.5000",
        "SYNTHETIC-UNVERIFIED", "3.000", "SYNTHETIC PRIVATE CONTACT", "SYNTHETIC-UNVERIFIED", "SYNTHETIC PRIVATE PHONE", "synthetic@example.invalid");
    private async Task<CrewPersonalProfileDto> Personal(HttpClient client) => (await client.GetFromJsonAsync<CrewPersonalProfileDto>("/api/v1/" + PersonalPath + "?purpose=crew-profile", Ct))!;

    [Fact]
    public async Task Crew_personal_all_typed_fields_exact_protected_roundtrip_and_safe_immutable_lineage() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client);
        var before = await Personal(client); var data = SyntheticProfile();
        var command = new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose, Enum.GetValues<CrewPersonalField>(), data);
        var key = Guid.NewGuid().ToString(); var response = await Send(client, PersonalPath, command, key, true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(data, (await Personal(client)).Data);
        Assert.True((await (await Send(client, PersonalPath, command, key, true)).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!.Replayed);
        await using var db = Db(); var stored = await db.CrewPersonalProfiles.SingleAsync(x => x.PersonId == SeedVessel.Id, Ct);
        Assert.DoesNotContain(data.PassportNo!, stored.PassportNo!); Assert.DoesNotContain(data.SeaServiceMonths!, stored.SeaServiceMonths!);
        var audit = string.Join('|', await db.AuditEvents.Select(x => x.ChangeSummary).ToArrayAsync(Ct));
        Assert.DoesNotContain(data.PassportNo!, audit); Assert.DoesNotContain(data.Email!, audit); Assert.DoesNotContain(stored.PassportNo!, audit);
        Assert.Equal(2, await db.CrewPersonalProfileRevisions.CountAsync(x => x.PersonId == SeedVessel.Id, Ct));
        var revision = await db.CrewPersonalProfileRevisions.FirstAsync(x => x.PersonId == SeedVessel.Id, Ct); revision.PolicyVersion = "mutated";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Crew_personal_partial_patch_preserves_unmapped_values_and_rejects_stale_version() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var old = await Personal(client);
        var initial = new CrewProfileCommand(old.Version, old.PolicyVersion, old.Purpose, [CrewPersonalField.PassportNo], new(PassportNo: "SYNTHETIC-ORIGINAL"));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, initial, update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, PersonalPath, initial, update: true)).StatusCode);
        var current = await Personal(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, new CrewProfileCommand(current.Version, current.PolicyVersion, current.Purpose,
            [CrewPersonalField.Email], new(Email: "synthetic@example.invalid")), update: true)).StatusCode);
        Assert.Equal("SYNTHETIC-ORIGINAL", (await Personal(client)).Data.PassportNo);
    }
    [Fact]
    public async Task Crew_personal_same_session_revocation_masks_contact_and_blocks_receipt_before_recovery() {
        using var host = ProfileHost(); using var client = ImportSessionClient(host); var session = await ImportSession(client); var before = await Personal(client);
        var command = new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose, [CrewPersonalField.Email], new(Email: "synthetic@example.invalid"));
        var key = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, command, key, true)).StatusCode);
        using var revokedHost = ProfileHost(write: false, contact: false); using var revoked = ImportSessionClient(revokedHost, session.Cookie, session.Csrf);
        var projection = await Personal(revoked); Assert.Null(projection.Data.Email); Assert.DoesNotContain(CrewPersonalField.Email, projection.Readable);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(revoked, PersonalPath, command, key, true)).StatusCode);
    }
    [Fact]
    public async Task Crew_personal_unknown_policy_purpose_catalog_or_extra_unmasked_values_deny() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var before = await Personal(client);
        foreach (var command in new[] {
            new CrewProfileCommand(before.Version, "unknown", before.Purpose, [CrewPersonalField.Email], new(Email: "synthetic@example.invalid")),
            new CrewProfileCommand(before.Version, before.PolicyVersion, "unapproved-purpose", [CrewPersonalField.Email], new(Email: "synthetic@example.invalid")),
            new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose, [CrewPersonalField.Nationality], new(Nationality: "invented-country")),
            new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose, [CrewPersonalField.Email], new(Email: "synthetic@example.invalid", PassportNo: "unmasked"))
        }) Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, PersonalPath, command, update: true)).StatusCode);
    }
    [Fact]
    public async Task Crew_personal_import_uses_same_protected_mutation_preserves_unmapped_fields_and_encrypts_stage() {
        using var host = ProfileHost(); using var client = Client(host); await Token(client); var before = await Personal(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose,
            [CrewPersonalField.PassportNo], new(PassportNo: "SYNTHETIC-KEEP")), update: true)).StatusCode);
        var current = await Personal(client); var assignment = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}", Ct))!;
        var row = new Dictionary<string, string> { ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = assignment.Assignment.Version.ToString(),
            ["profile_base_version"] = current.Version!.Value.ToString(), ["profile_policy_version"] = current.PolicyVersion, ["sea_service_months"] = SyntheticProfile().SeaServiceMonths! };
        var batch = await UploadImport(client, "crew", row); var preview = await PreviewImport(client, batch); Assert.Equal(0, preview.RejectedRows);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        var imported = await Personal(client); Assert.Equal("SYNTHETIC-KEEP", imported.Data.PassportNo); Assert.Equal(row["sea_service_months"], imported.Data.SeaServiceMonths);
        await using var db = Db(); var staged = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct);
        Assert.StartsWith("protected-v1:", staged.TableJson); Assert.DoesNotContain(row["sea_service_months"], staged.TableJson);
    }
    [Fact]
    public async Task Crew_personal_import_same_session_field_revocation_hides_batch_and_history() {
        using var host = ProfileHost(); using var client = ImportSessionClient(host); var session = await ImportSession(client);
        var batch = await UploadImport(client, "crew", new() { ["record_key"] = Guid.NewGuid().ToString(), ["profile_policy_version"] = "synthetic-profile-v1", ["email"] = "synthetic@example.invalid" });
        var preview = await PreviewImport(client, batch); Assert.Equal(0, preview.RejectedRows);
        using var deniedHost = ProfileHost(contact: false); using var denied = ImportSessionClient(deniedHost, session.Cookie, session.Csrf);
        Assert.Equal(HttpStatusCode.NotFound, (await denied.GetAsync("/api/v1/" + Imports + $"/{batch.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(batch.Id.ToString(), await denied.GetStringAsync("/api/v1/" + Imports, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(denied, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
    }
    [Fact]
    public async Task Crew_current_assignment_uses_configured_status_period_provenance_and_real_position_context() {
        using var host = ProfileHost().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CrewAssignmentPolicy:Version"] = "synthetic-period-v1", ["CrewAssignmentPolicy:TimeZone"] = "UTC",
            ["CrewAssignmentPolicy:EffectiveStatuses:0"] = "SyntheticOnboard", ["CrewAssignmentPolicy:PlannedStatuses:0"] = "SyntheticPlanned",
            ["CrewAssignmentPolicy:Provenances:0"] = "SyntheticUnverified", ["Authentication:Oidc:Memberships:0:Permissions:33"] = "crew.assignment.declare"
        })));
        using var client = Client(host); await Token(client);
        var detail = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}", Ct))!;
        var path = $"/api/v1/vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/current-assignment";
        Assert.Contains("Missing", await client.GetStringAsync(path, Ct)); // Seed Draft is not picked.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new CrewSaveCommand(SeedVessel.Id, SeedVessel.Id, detail.Assignment.Version, null, null, SeedVessel.Id,
            today, today.AddDays(1), null, null, DateTimeOffset.UtcNow.AddMinutes(-1), "SyntheticOnboard", "synthetic-period-v1");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"vessels/{SeedVessel.Id}/crew", command)).StatusCode);
        var current = await client.GetStringAsync(path, Ct);
        Assert.Contains("ConfiguredEffectiveDeclaration", current); Assert.Contains(SeedVessel.Id.ToString(), current); Assert.DoesNotContain("READY", current);
        await using var db = Db(); Assert.Equal("SyntheticOnboard", (await db.CrewAssignmentRevisions.OrderByDescending(x => x.Revision).FirstAsync(x => x.AssignmentId == SeedVessel.Id, Ct)).Status);
    }
    [Fact]
    public async Task Import_source_coverage_is_not_a_writable_schema_or_all_fields_imported_claim() {
        using var host = ProfileHost(); using var client = Client(host);
        var contract = (await client.GetFromJsonAsync<ImportSourceCoverageDto>("/api/v1/" + Imports + "/coverage", Ct))!;
        Assert.Equal(111, contract.Fields.Length); Assert.False(contract.IsWritableSchema);
        Assert.Contains(contract.Fields, f => f.Field == "date_of_birth" && f.State == "implemented_configured");
        Assert.Contains(contract.Fields, f => f.Category == "vessel" && f.RemainingGate.Contains("DEV-020", StringComparison.Ordinal));
        Assert.Contains(contract.Fields, f => f.State == "unimplemented_software");
    }
    [Fact]
    public async Task Crew_personal_import_policy_version_change_precedes_historic_batch_receipt_recovery() {
        using var host=ProfileHost(); using var client=ImportSessionClient(host); var session=await ImportSession(client);
        var batch=await UploadImport(client,"crew",new() { ["record_key"]=Guid.NewGuid().ToString(),["profile_policy_version"]="synthetic-profile-v1",["email"]="synthetic@example.invalid" });
        var preview=await PreviewImport(client,batch); Assert.Equal(0,preview.RejectedRows);
        var command=new ImportCommitCommand(preview.PreviewId,long.Parse(preview.Revision),preview.TemplateFingerprint);var key=Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK,(await Send(client,Imports+$"/{batch.Id}/commit",command,key)).StatusCode);
        using var changedHost=ProfileHost().WithWebHostBuilder(b=>b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> { ["CrewProfilePolicy:Version"]="synthetic-profile-v2" })));
        using var changed=ImportSessionClient(changedHost,session.Cookie,session.Csrf);
        Assert.Equal(HttpStatusCode.OK,(await changed.GetAsync("/api/v1/"+Imports+$"/{batch.Id}",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(changed,Imports+$"/{batch.Id}/commit",command,key)).StatusCode);
        await using var db=Db();Assert.Equal(1,await db.CommandReceipts.CountAsync(x=>x.Action=="ImportCommit"&&x.ResourceId==batch.Id,Ct));
    }
    [Fact]
    public async Task Crew_personal_unconfigured_policy_has_no_protected_projection() {
        using var host = ImportHost(); using var client = Client(host);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/" + PersonalPath + "?purpose=crew-profile", Ct)).StatusCode);
    }
}
