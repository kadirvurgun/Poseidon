using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// FR46/TC46-crew-profile-medical_expiry / DEV-047. AUTHORED, NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private WebApplicationFactory<Program> MedicalHost(bool read = true, bool write = true) => ProfileHost().WithWebHostBuilder(b =>
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CrewProfilePolicy:Fields:15:ReadCapability"] = "crew.profile.medical.read",
            ["CrewProfilePolicy:Fields:15:WriteCapability"] = "crew.profile.medical.write",
            ["Authentication:Oidc:Memberships:0:Permissions:40"] = read ? "crew.profile.medical.read" : "unused",
            ["Authentication:Oidc:Memberships:0:Permissions:41"] = write ? "crew.profile.medical.write" : "unused"
        })));

    [Fact]
    public async Task Medical_expiry_requires_separate_current_capability_and_never_changes_fitness()
    {
        using var host = MedicalHost(); using var client = ImportSessionClient(host);
        var session = await ImportSession(client); var current = await Personal(client);
        var command = new CrewProfileCommand(current.Version, current.PolicyVersion, current.Purpose,
            [CrewPersonalField.MedicalExpiry], new(MedicalExpiry: new(2031, 2, 3)));
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, command, key, true)).StatusCode);
        Assert.Equal(new DateOnly(2031, 2, 3), (await Personal(client)).Data.MedicalExpiry);
        var profile = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}", Ct))!;
        Assert.Equal("Unknown", profile.FitnessState); Assert.Equal("Unknown", profile.Assignment.EligibilityState);
        using var deniedHost = MedicalHost(read: false, write: false);
        using var denied = ImportSessionClient(deniedHost, session.Cookie, session.Csrf);
        var masked = await Personal(denied);
        Assert.Null(masked.Data.MedicalExpiry); Assert.DoesNotContain(CrewPersonalField.MedicalExpiry, masked.Readable);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(denied, PersonalPath, command, key, true)).StatusCode);
        Assert.DoesNotContain("2031-02-03", await denied.GetStringAsync("/api/v1/" + PersonalPath + "/history", Ct));
    }

    [Fact]
    public async Task Medical_expiry_is_encrypted_reseed_preserved_and_not_in_audit()
    {
        using var host = MedicalHost(); using var client = Client(host); await Token(client);
        var before = await Personal(client);
        var command = new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose,
            [CrewPersonalField.MedicalExpiry], new(MedicalExpiry: new(2031, 2, 3)));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalPath, command, update: true)).StatusCode);
        await using var db = Db();
        var stored = await db.CrewPersonalProfiles.AsNoTracking().SingleAsync(x => x.PersonId == SeedVessel.Id, Ct);
        Assert.NotNull(stored.MedicalExpiry); Assert.DoesNotContain("2031-02-03", stored.MedicalExpiry!);
        Assert.DoesNotContain("2031-02-03", string.Join('|', await db.AuditEvents.Select(x => x.ChangeSummary).ToArrayAsync(Ct)));
        // Normal add-missing seed must not manufacture or overwrite health facts.
        var revision = stored.Revision;
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty,
            new("system:seed-import", "System", "medical-reseed"), true))
            await SeedImporter.ImportAsync(admin, seed, Ct);
        var retained = await db.CrewPersonalProfiles.AsNoTracking().SingleAsync(x => x.PersonId == SeedVessel.Id, Ct);
        Assert.Equal(stored.MedicalExpiry, retained.MedicalExpiry); Assert.Equal(revision, retained.Revision);
    }

    [Fact]
    public async Task Medical_expiry_import_uses_ordinary_protection_and_current_batch_access()
    {
        using var host = MedicalHost(); using var client = ImportSessionClient(host);
        var session = await ImportSession(client); var before = await Personal(client);
        var assignment = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}", Ct))!;
        var batch = await UploadImport(client, "crew", new() {
            ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = assignment.Assignment.Version.ToString(),
            ["profile_base_version"] = before.Version!.Value.ToString(), ["profile_policy_version"] = before.PolicyVersion,
            ["medical_expiry"] = "2031-02-03"
        });
        var preview = await PreviewImport(client, batch); Assert.Equal(0, preview.RejectedRows);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{batch.Id}/commit",
            new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        Assert.Equal(new DateOnly(2031, 2, 3), (await Personal(client)).Data.MedicalExpiry);
        using var deniedHost = MedicalHost(read: false, write: false);
        using var denied = ImportSessionClient(deniedHost, session.Cookie, session.Csrf);
        Assert.Equal(HttpStatusCode.NotFound, (await denied.GetAsync("/api/v1/" + Imports + $"/{batch.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Generic_identity_field_configuration_cannot_grant_medical_access()
    {
        using var host = ProfileHost(); using var client = Client(host); await Token(client);
        var before = await Personal(client);
        Assert.DoesNotContain(CrewPersonalField.MedicalExpiry, before.Readable);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, PersonalPath,
            new CrewProfileCommand(before.Version, before.PolicyVersion, before.Purpose, [CrewPersonalField.MedicalExpiry],
                new(MedicalExpiry: new(2031, 2, 3))), update: true)).StatusCode);
    }
}
