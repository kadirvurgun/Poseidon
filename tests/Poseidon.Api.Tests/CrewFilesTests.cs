using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-047/069: actual API/current migrations/PostgreSQL. Scanner protocol peer is not a deployed scanner proof.
public sealed partial class DirectoryWorkspaceTests
{
    private string PersonalFiles => $"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/personal-files";
    private string PersonalVersion(string kind = "Photo", long revision = 1) => $"{PersonalFiles}/{kind}/versions/{revision}";
    private WebApplicationFactory<Program> PersonFileHost(string root, int port, bool configured = true) => FileHost(root, port)
        .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["CrewFilePolicy:Version"] = configured ? "synthetic-person-files-v1" : null,
            ["CrewFilePolicy:Purpose"] = "synthetic-person-file-test", ["CrewFilePolicy:SourceVersion"] = "poseidon-v46.3-20261030",
            ["CrewFilePolicy:Kinds:0"] = "Photo", ["CrewFilePolicy:Kinds:1"] = "CertificateDocument", ["CrewFilePolicy:Kinds:2"] = "Passport"
        })));
    private void PersonFilePermissions(IServiceProvider services) {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "directory.write", "file.read", "file.write",
            "crew.read", "crew.write", "crew.identity.read", "crew.identity.write", "crew.documents.read", "crew.documents.write", "crew.certificate.read", "crew.certificate.write"] };
    }
    private object PersonalPng(string kind = "Photo", DateOnly? expiry = null) => new { name = kind == "Photo" ? "photo.png" : "document.png",
        mime = "image/png", length = Png.Length, checksum = Convert.ToHexString(SHA256.HashData(Png)), expiresOn = expiry };
    private async Task<HttpResponseMessage> PutPersonalPng(HttpClient client, string path, Guid id) {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/{path}/{id}/bytes") { Content = new ByteArrayContent(Png) };
        request.Content.Headers.ContentType = new("image/png"); return await client.SendAsync(request, Ct);
    }
    private static string PersonFileRoot() { var root = Path.Combine(Path.GetTempPath(), "poseidon-person-files-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }

    [Fact]
    public async Task Person_file_policy_is_default_deny_even_with_vessel_file_and_identity_access() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1, false); using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + PersonalFiles, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, PersonalVersion(), PersonalPng())).StatusCode);
        await using var db = Db(); Assert.False(await db.FileStages.AnyAsync(x => x.PersonId != null, Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Person_file_versions_replay_exact_metadata_and_never_appear_in_generic_routes() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        var key = Guid.NewGuid().ToString();
        using var response = await Send(client, PersonalVersion("Passport"), PersonalPng("Passport", new(2030, 1, 2)), key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var row = (await response.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var replay = await Send(client, PersonalVersion("Passport"), PersonalPng("Passport", new(2030, 1, 2)), key);
        Assert.Equal(row.Id, (await replay.Content.ReadFromJsonAsync<FileDto>(Ct))!.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, PersonalVersion("Passport"), PersonalPng("Passport", new(2030, 1, 3)), key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, PersonalVersion("Passport"), PersonalPng("Passport"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PersonalVersion("Passport", 2), PersonalPng("Passport"))).StatusCode);
        var manifest = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PersonalFiles, Ct);
        Assert.Equal(2, manifest.GetProperty("items").GetArrayLength()); Assert.Equal(3, manifest.GetProperty("next").GetProperty("Passport").GetInt32());
        Assert.Contains(manifest.GetProperty("items").EnumerateArray(), x => x.GetProperty("expiresOn").GetString() == "2030-01-02");
        var generic = await client.GetFromJsonAsync<FileListDto>($"/api/v1/vessels/{SeedVessel.Id}/files", Ct);
        Assert.DoesNotContain(generic!.Items, x => x.Id == row.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{row.Id}", Ct)).StatusCode);
        await using var db = Db(); var stored = await db.FileStages.SingleAsync(x => x.Id == row.Id, Ct);
        Assert.Equal(SeedVessel.Id, stored.PersonId); Assert.Equal("poseidon-v46.3-20261030", stored.PersonalSourceVersion);
        stored.PersonalExpiresOn = new(2040, 1, 1); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Person_file_real_scanned_bytes_recheck_same_session_membership_and_policy_before_access_and_replay() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client); var key = Guid.NewGuid().ToString();
        var stage = (await (await Send(client, PersonalVersion(), PersonalPng(), key)).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await PutPersonalPng(client, PersonalVersion(), stage.Id)).StatusCode);
        Assert.Equal(Png, await peer.Bytes);
        var grant = await (await client.PostAsync($"/api/v1/{PersonalVersion()}/{stage.Id}/access", null, Ct)).Content.ReadFromJsonAsync<JsonElement>(Ct);
        client.DefaultRequestHeaders.Add("X-File-Capability", grant.GetProperty("token").GetString());
        using var bytes = await client.GetAsync($"/api/v1/{PersonalVersion()}/{stage.Id}/bytes", Ct);
        Assert.Equal(HttpStatusCode.OK, bytes.StatusCode); Assert.Equal(Png, await bytes.Content.ReadAsByteArrayAsync(Ct));
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "crew.documents.read").ToArray() };
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{PersonalVersion()}/{stage.Id}/bytes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, PersonalVersion(), PersonalPng(), key)).StatusCode);
        PersonFilePermissions(host.Services); host.Services.GetRequiredService<IConfiguration>()["CrewFilePolicy:Version"] = "synthetic-person-files-v2";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{PersonalVersion()}/{stage.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, PersonalVersion(), PersonalPng(), key)).StatusCode);
        await using var db = Db(); Assert.Single(await db.StoredFiles.Where(x => x.Id == stage.Id).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == stage.Id).ToArrayAsync(Ct));
        File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + stage.Id.ToString("N") + ".object")); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Person_file_wrong_person_route_and_forged_owner_or_photo_expiry_are_rejected() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, PersonalVersion(), PersonalPng(expiry: new(2030, 1, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, PersonalVersion(), new { name = "photo.png", mime = "image/png", length = Png.Length, checksum = new string('A', 64), personId = Guid.NewGuid(), clean = true })).StatusCode);
        var stage = (await (await Send(client, PersonalVersion(), PersonalPng())).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var created = (await (await Send(client, CrewUrl, NewCrew())).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
        var wrong = PersonalVersion().Replace($"crew/{SeedVessel.Id}/", $"crew/{created.Id}/", StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{wrong}/{stage.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/{wrong}/{stage.Id}/access", null, Ct)).StatusCode);
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Person_file_concurrent_version_intents_have_one_effect_and_preserve_prior_versions() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        var responses = await Task.WhenAll(Send(client, PersonalVersion(), PersonalPng()), Send(client, PersonalVersion(), PersonalPng()));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var r in responses) r.Dispose();
        await using var db = Db(); Assert.Single(await db.FileStages.Where(x => x.PersonId == SeedVessel.Id).ToArrayAsync(Ct));
        Assert.False(await db.StoredFiles.AnyAsync(Ct)); Assert.False(await db.CommandReceipts.AnyAsync(Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Person_file_scanner_rejection_preserves_quarantine_without_referenceable_bytes() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: FOUND\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        var path = PersonalVersion("CertificateDocument");
        var stage = (await (await Send(client, path, PersonalPng("CertificateDocument"))).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await PutPersonalPng(client, path, stage.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PutPersonalPng(client, path, stage.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/{path}/{stage.Id}/access", null, Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == stage.Id, Ct));
        await FileReconciliation.AbandonAsync(db, new LocalFileObjects(new() { Root = root }), DateTimeOffset.UtcNow, 10, Ct); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Certificate_reference_uses_exact_person_scanned_version_and_current_read_rights_before_replay() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        var path = PersonalVersion("CertificateDocument");
        var stage = (await (await Send(client, path, PersonalPng("CertificateDocument"))).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var metadata = new CertificateMetadata(new(null, "FICTIONAL-CERT", null, null, null, null), DocumentReference: stage.Id);
        var command = new CredentialSaveCommand(Guid.NewGuid(), null, metadata); var key = Guid.NewGuid().ToString();
        var endpoint = $"{CrewUrl}/{SeedVessel.Id}/certificates";
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, endpoint, command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutPersonalPng(client, path, stage.Id)).StatusCode);
        var another = (await (await Send(client, CrewUrl, NewCrew())).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, $"{CrewUrl}/{another.Id}/certificates", command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, endpoint, command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, endpoint, command, key)).StatusCode);
        var inputPath = $"/api/v1/{CrewUrl}/{SeedVessel.Id}/owning-inputs";
        using (var observed = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(inputPath, Ct))) {
            var certificate = observed.RootElement.GetProperty("inputs").EnumerateArray().Single(x => x.GetProperty("field").GetString() == "certificate_state");
            Assert.Equal("DeclaredUnverified", certificate.GetProperty("state").GetString());
        }
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "crew.documents.read").ToArray() };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, endpoint, command, key)).StatusCode);
        var visible = await client.GetFromJsonAsync<CredentialDto[]>("/api/v1/" + endpoint, Ct);
        Assert.DoesNotContain(visible!, x => x.Id == command.Id);
        using (var observed = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(inputPath, Ct))) {
            var certificate = observed.RootElement.GetProperty("inputs").EnumerateArray().Single(x => x.GetProperty("field").GetString() == "certificate_state");
            Assert.Equal("Withheld", certificate.GetProperty("state").GetString());
        }
        await using var db = Db(); var stored = await db.Credentials.SingleAsync(x => x.Id == command.Id, Ct);
        Assert.Equal(stage.Id, stored.DocumentReference); Assert.Null(stored.LegacyDocumentReference);
        Assert.Single(await db.CommandReceipts.Where(x => x.ResourceId == command.Id).ToArrayAsync(Ct));
        File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + stage.Id.ToString("N") + ".object")); Directory.Delete(root, false);
    }

    [Fact]
    public async Task PostgreSQL_enforces_person_tenant_fk_immutable_version_metadata_and_preserves_seed_rows() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host); PersonFilePermissions(host.Services); await Token(client);
        var stage = (await (await Send(client, PersonalVersion(), PersonalPng())).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct);
        // Adversarial direct SQL in a test proves the DB guard independently of the normal audited mutation path.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE files.\"FileStages\" SET \"PersonalPurpose\" = 'forged' WHERE \"Id\" = {stage.Id}", Ct));
        Assert.Equal("synthetic-person-file-test", (await db.FileStages.AsNoTracking().SingleAsync(x => x.Id == stage.Id, Ct)).PersonalPurpose);
        Assert.Equal(before, await db.AuditEvents.CountAsync(Ct));
        var foreignPerson = seed.Vessels.First(x => x.TenantId == seed.Tenants[1].Id).Id;
        db.FileStages.Add(new() { TenantId = SeedVessel.TenantId, Id = Guid.NewGuid(), VesselId = SeedVessel.Id, ActorId = actorId,
            PersonId = foreignPerson, PersonalKind = "Photo", PersonalRevision = 1, PersonalPurpose = "synthetic-person-file-test",
            PersonalPolicyVersion = "synthetic-person-files-v1", PersonalSourceVersion = "poseidon-v46.3-20261030",
            Key = Guid.NewGuid().ToString(), Fingerprint = new string('A', 64), Name = "photo.png", ExpectedChecksum = new string('A', 64), ExpectedLength = 1, CreatedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        Assert.Equal(before, await db.AuditEvents.CountAsync(Ct));
        // Existing deterministic synthetic stages still have no person, bytes, scan or operational authority.
        var synthetic = await db.FileStages.SingleAsync(x => x.Id == SeedVessel.Id, Ct);
        Assert.Null(synthetic.PersonId); Assert.Equal("SyntheticUnverified", synthetic.State);
        Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == synthetic.Id, Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Actual_Xlsx_certificate_consumer_rejects_pending_file_then_commits_scanned_person_reference_and_withdraws_on_revocation() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = [.. settings.Memberships[0].Permissions, "import.read", "import.write"] }; await Token(client);
        var path = PersonalVersion("CertificateDocument");
        var stage = (await (await Send(client, path, PersonalPng("CertificateDocument"))).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var id = Guid.NewGuid();
        var batch = await UploadImport(client, "certificate", new() { ["record_key"] = id.ToString(), ["document_ref"] = stage.Id.ToString() });
        var rejected = await PreviewImport(client, batch); Assert.Equal(1, rejected.RejectedRows);
        Assert.Contains(rejected.Rows.Single().Issues, x => x.Code == "unresolved_reference");
        Assert.Equal(HttpStatusCode.OK, (await PutPersonalPng(client, path, stage.Id)).StatusCode);
        var preview = await PreviewImport(client, batch); Assert.Equal(1, preview.ValidRows);
        var command = new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision, System.Globalization.CultureInfo.InvariantCulture), preview.TemplateFingerprint);
        var key = Guid.NewGuid().ToString(); var endpoint = Imports + $"/{batch.Id}/commit";
        Assert.Equal(HttpStatusCode.OK, (await Send(client, endpoint, command, key)).StatusCode);
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(x => x != "crew.documents.read").ToArray() };
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, endpoint, command, key)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + Imports + $"/{batch.Id}", Ct)).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + Imports, Ct);
        Assert.DoesNotContain(history.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == batch.Id);
        await using var db = Db(); Assert.Equal(stage.Id, (await db.Credentials.SingleAsync(x => x.Id == id, Ct)).DocumentReference);
        Assert.Single(await db.CommandReceipts.Where(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id).ToArrayAsync(Ct));
        File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + stage.Id.ToString("N") + ".object")); Directory.Delete(root, false);
    }
}
