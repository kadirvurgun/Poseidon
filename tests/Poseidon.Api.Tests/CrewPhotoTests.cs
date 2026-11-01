using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// FR46/TC46-crew-profile-photo_ref: real host + migrated PostgreSQL, never a scan/provider acceptance claim.
public sealed partial class DirectoryWorkspaceTests
{
    private string PhotoPath => $"{CrewUrl}/{SeedVessel.Id}/photo";
    private Task<CrewPhotoDto?> ReadPhoto(HttpClient client) => client.GetFromJsonAsync<CrewPhotoDto>("/api/v1/" + PhotoPath, Ct);
    private static CrewPhotoCommand PhotoCommand(CrewPhotoDto row, Guid? id) => new(row.Version, id, row.PolicyVersion, row.Purpose);
    private async Task<Guid> CleanPhoto(HttpClient client) {
        var row = (await (await Send(client, PersonalVersion(), PersonalPng())).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await PutPersonalPng(client, PersonalVersion(), row.Id)).StatusCode); return row.Id;
    }
    private void DeletePhotoObject(string root, Guid id) {
        File.Delete(Path.Combine(root, SeedVessel.TenantId.ToString("N") + "-" + id.ToString("N") + ".object")); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Photo_selection_never_infers_latest_and_rejects_pending_foreign_and_wrong_kind_references() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host);
        PersonFilePermissions(host.Services); await Token(client);
        var initial = (await ReadPhoto(client))!; Assert.Equal("Missing", initial.State); Assert.Null(initial.Selected);
        var pending = (await (await Send(client, PersonalVersion(), PersonalPng())).Content.ReadFromJsonAsync<FileDto>(Ct))!;
        foreach (var id in new[] { pending.Id, SeedVessel.Id, Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, PhotoPath, PhotoCommand(initial, id), update: true)).StatusCode);
        Assert.Equal("Missing", (await ReadPhoto(client))!.State);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "CrewPhotoSave", Ct));
        Assert.False(await db.CrewPersonalProfileRevisions.AnyAsync(x => x.IsPhotoSelection, Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Explicit_photo_selection_clear_and_old_retry_pin_original_revision_and_preserve_reseed_and_audit_privacy() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client); var id = await CleanPhoto(client);
        try {
            var initial = (await ReadPhoto(client))!; Assert.Equal("Missing", initial.State); // Upload alone selects nothing.
            var command = PhotoCommand(initial, id); var key = Guid.NewGuid().ToString();
            var saved = (await (await Send(client, PhotoPath, command, key, true)).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
            var current = (await ReadPhoto(client))!; Assert.Equal("SelectedUnverified", current.State); Assert.Equal(id, current.Selected!.Id);
            var cleared = await Send(client, PhotoPath, PhotoCommand(current, null), update: true); Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
            var replay = (await (await Send(client, PhotoPath, command, key, true)).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
            Assert.True(replay.Replayed); Assert.Equal(saved.Revision, replay.Revision); Assert.Equal(saved.Version, replay.Version);
            Assert.Equal("Missing", (await ReadPhoto(client))!.State); // Recovery must not reapply selection.
            var memberships = host.Services.GetRequiredService<FederationSettings>();
            memberships.Memberships[0] = memberships.Memberships[0] with { Permissions = [.. memberships.Memberships[0].Permissions, "crew.history.read"] };
            var history = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PhotoPath + "/history", Ct);
            var selectedHistory = history.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, selectedHistory.Length);
            Assert.Equal(JsonValueKind.Null, selectedHistory[0].GetProperty("photoFileId").ValueKind);
            Assert.Equal(id, selectedHistory[1].GetProperty("photoFileId").GetGuid());
            await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, new("system:seed-import", "System", "photo-reseed"), true))
                await SeedImporter.ImportAsync(admin, seed, Ct);
            await using var db = Db();
            Assert.Null((await db.CrewPersonalProfiles.SingleAsync(x => x.PersonId == SeedVessel.Id, Ct)).PhotoFileId);
            var revisions = await db.CrewPersonalProfileRevisions.Where(x => x.PersonId == SeedVessel.Id && x.IsPhotoSelection).OrderBy(x => x.Revision).ToArrayAsync(Ct);
            Assert.Equal(2, revisions.Length); Assert.Equal(id, revisions[0].PhotoFileId); Assert.Null(revisions[1].PhotoFileId);
            Assert.Equal(2, await db.CommandReceipts.CountAsync(x => x.Action == "CrewPhotoSave", Ct));
            Assert.DoesNotContain(await db.AuditEvents.Where(x => x.EntityType == nameof(CrewPersonalProfile)).Select(x => x.ChangeSummary ?? "").ToArrayAsync(Ct), x => x.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
        } finally { DeletePhotoObject(root, id); }
    }

    [Fact]
    public async Task Photo_access_and_policy_revocation_precede_current_read_history_and_receipt_recovery() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client); var id = await CleanPhoto(client);
        try {
            var row = (await ReadPhoto(client))!; var command = PhotoCommand(row, id); var key = Guid.NewGuid().ToString();
            Assert.Equal(HttpStatusCode.OK, (await Send(client, PhotoPath, command, key, true)).StatusCode);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "crew.documents.read").ToArray() };
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + PhotoPath, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Send(client, PhotoPath, command, key, true)).StatusCode);
            PersonFilePermissions(host.Services);
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = [.. settings.Memberships[0].Permissions, "crew.history.read"] };
            host.Services.GetRequiredService<IConfiguration>()["CrewFilePolicy:Version"] = "synthetic-person-files-v2";
            var unavailable = (await ReadPhoto(client))!; Assert.Equal("Unavailable", unavailable.State); Assert.Null(unavailable.Selected);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, PhotoPath, command, key, true)).StatusCode);
            var history = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + PhotoPath + "/history", Ct);
            Assert.Empty(history.GetProperty("items").EnumerateArray());
        } finally { DeletePhotoObject(root, id); }
    }

    [Fact]
    public async Task Photo_profile_version_race_has_one_effect_and_stale_clear_cannot_overwrite_a_newer_revision() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1); using var client = Client(host);
        PersonFilePermissions(host.Services); await Token(client); var initial = (await ReadPhoto(client))!; var command = PhotoCommand(initial, null);
        var responses = await Task.WhenAll(Send(client, PhotoPath, command, update: true), Send(client, PhotoPath, command, update: true));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, PhotoPath, command, update: true)).StatusCode);
        await using var db = Db(); Assert.Single(await db.CrewPersonalProfileRevisions.Where(x => x.IsPhotoSelection).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.Action == "CrewPhotoSave").ToArrayAsync(Ct)); Directory.Delete(root, false);
    }

    [Fact]
    public async Task Database_photo_guard_rejects_wrong_person_and_revision_mutation_without_partial_audit() {
        var root = PersonFileRoot(); using var peer = new ScanPeer("stream: OK\0"); using var host = PersonFileHost(root, peer.Port);
        using var client = Client(host); PersonFilePermissions(host.Services); await Token(client); var id = await CleanPhoto(client);
        try {
            var another = (await (await Send(client, CrewUrl, NewCrew())).Content.ReadFromJsonAsync<CrewSavedDto>(Ct))!;
            var otherPath = $"{CrewUrl}/{another.Id}/photo";
            var other = (await client.GetFromJsonAsync<CrewPhotoDto>("/api/v1/" + otherPath, Ct))!;
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, otherPath, PhotoCommand(other, id), update: true)).StatusCode);
            await using var db = Db(); var count = await db.AuditEvents.CountAsync(Ct);
            // Adversarial SQL proves database ownership separately from API validation; product writes remain audited EF.
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE crew.\"PersonalProfiles\" SET \"PhotoFileId\" = {id}, \"PhotoVesselId\" = {SeedVessel.Id} WHERE \"PersonId\" <> {SeedVessel.Id}", Ct));
            Assert.Equal(count, await db.AuditEvents.CountAsync(Ct));
            Assert.Equal(HttpStatusCode.OK, (await Send(client, PhotoPath, PhotoCommand((await ReadPhoto(client))!, id), update: true)).StatusCode);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE crew.\"PersonalProfileRevisions\" SET \"PhotoFileId\" = NULL WHERE \"PersonId\" = {SeedVessel.Id} AND \"IsPhotoSelection\"", Ct));
        } finally { DeletePhotoObject(root, id); }
    }

    [Fact]
    public async Task Photo_policy_csrf_unknown_authority_and_cross_tenant_routes_fail_closed() {
        var root = PersonFileRoot(); using var host = PersonFileHost(root, 1, false); using var client = Client(host);
        PersonFilePermissions(host.Services); await Token(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/" + PhotoPath, Ct)).StatusCode);
        host.Services.GetRequiredService<IConfiguration>()["CrewFilePolicy:Version"] = "synthetic-person-files-v1";
        var row = (await ReadPhoto(client))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, PhotoPath, new { baseVersion = row.Version, fileId = (Guid?)null, row.PolicyVersion, row.Purpose, actorId = Guid.NewGuid() }, update: true)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, PhotoPath, PhotoCommand(row, null), update: true)).StatusCode);
        var foreign = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId).Id;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{foreign}/crew/{foreign}/photo", Ct)).StatusCode);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action == "CrewPhotoSave", Ct)); Directory.Delete(root, false);
    }
}
