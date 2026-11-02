using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// FSC-DOC-01..05 actual host/PostgreSQL. No mock storage/scanner is used by these metadata tests.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Document_drafts_allow_missing_title_append_immutable_revisions_and_replay_original_snapshot()
    {
        await using var host = Host(); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var path = $"vessels/{SeedVessel.Id}/documents"; var key = Guid.NewGuid().ToString();
        using var created = await Send(client, path, new { title = (string?)null }, key);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct))!;
        Assert.Null(draft.Title); Assert.Equal("DraftUnverified", draft.Status);
        client.DefaultRequestHeaders.IfMatch.ParseAdd($"\"{draft.Version}\"");
        using var revised = await Send(client, $"{path}/{draft.Id}/versions", new { title = "FICTIONAL draft" });
        Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        var second = (await revised.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct))!;
        Assert.Equal(2, second.Revision); Assert.NotEqual(draft.Version, second.Version);
        client.DefaultRequestHeaders.IfMatch.Clear();
        using var replay = await Send(client, path, new { title = (string?)null }, key);
        Assert.Equal(draft, await replay.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct));
        await using var db = Db();
        var immutable = await db.DocumentVersions.SingleAsync(x => x.DocumentId == draft.Id && x.Revision == 1, Ct);
        immutable.Title = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.DocumentVersions.CountAsync(x => x.DocumentId == draft.Id, Ct));
        Assert.Empty(await db.StoredFiles.Where(x => x.Id == draft.Id).ToArrayAsync(Ct));
        Assert.All(await db.AuditEvents.Where(x => x.ResourceId == draft.Id && x.EntityType == nameof(DocumentVersion)).ToArrayAsync(Ct), x => Assert.Equal(SeedVessel.Id, x.VesselId));
    }

    [Fact]
    public async Task Document_count_detail_revision_and_replay_deny_wrong_vessel_tenant_and_revoked_roles()
    {
        await using var host = Host(); using var client = Client(host); FilePermissions(host.Services); await Token(client);
        var path = $"vessels/{SeedVessel.Id}/documents"; var key = Guid.NewGuid().ToString();
        using var response = await Send(client, path, new { title = "FICTIONAL owner draft" }, key);
        var draft = (await response.Content.ReadFromJsonAsync<DocumentDraftDto>(Ct))!;
        var other = seed.Vessels.First(x => x.Id != SeedVessel.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{other.Id}/documents/{draft.Id}/versions/1", Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { RoleReferences = [] };
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, path, new { title = "FICTIONAL owner draft" }, key)).StatusCode);
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read"] };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/{path}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Document_attachments_cannot_be_staged_or_replayed_into_a_different_owner_revision()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-owner-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            await using var host = FileHost(root, 1); using var client = Client(host); FilePermissions(host.Services); await Token(client);
            var prefix = $"vessels/{SeedVessel.Id}/documents/{SeedVessel.Id}/versions";
            var key = Guid.NewGuid().ToString();
            var command = new { name = "synthetic.png", mime = "image/png", length = Png.Length, checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png)) };
            using var response = await Send(client, $"{prefix}/1/files", command, key);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var staged = (await response.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{staged.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/{prefix}/2/files/{staged.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Send(client, $"vessels/{SeedVessel.Id}/files", command, key)).StatusCode);
            await using var db = Db(); var stage = await db.FileStages.SingleAsync(x => x.Id == staged.Id, Ct);
            Assert.Equal(SeedVessel.Id, stage.DocumentId); Assert.Equal(1, stage.DocumentRevision);
        } finally { Directory.Delete(root, false); }
    }
}
