using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-069: use the real host/PostgreSQL and the SAME captured session across
// permission changes. A new cookie would only test session isolation.
public sealed partial class DirectoryWorkspaceTests
{
    private static HttpClient ImportSessionClient(WebApplicationFactory<Program> host, string? cookie = null, string? csrf = null)
    {
        var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Directory-Test", "synthetic");
        if (cookie != null) client.DefaultRequestHeaders.Add("Cookie", cookie);
        if (csrf != null) client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        return client;
    }

    private async Task<(string Cookie, string Csrf)> ImportSession(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/session", Ct);
        var csrf = (await response.Content.ReadFromJsonAsync<SessionDto>(Ct))!.CsrfToken;
        var cookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0]));
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        return (cookie, csrf);
    }

    [Theory]
    [InlineData("vessel", false)]
    [InlineData("vessel", true)]
    [InlineData("crew", false)]
    [InlineData("crew", true)]
    [InlineData("certificate", false)]
    [InlineData("certificate", true)]
    [InlineData("equipment", false)]
    [InlineData("equipment", true)]
    public async Task Import_category_write_revocation_blocks_new_commit_and_durable_receipt_replay(string category, bool committed)
    {
        using var allowedHost = ImportHost(); using var allowed = ImportSessionClient(allowedHost);
        var session = await ImportSession(allowed);
        var id = Guid.NewGuid();
        var row = new Dictionary<string, string> { ["record_key"] = id.ToString(), ["name"] = "FICTIONAL PERMISSION BOUNDARY",
            ["full_name"] = "FICTIONAL PERSON BOUNDARY", ["code"] = "FICTIONAL-" + id.ToString("N"), ["level"] = "Equipment" };
        if (category == "vessel") {
            var vessel = (await allowed.GetFromJsonAsync<VesselDto>("/api/v1/vessels/" + SeedVessel.Id, Ct))!;
            id = vessel.Id; row["record_key"] = id.ToString();
            row["base_version"] = vessel.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var batch = await UploadImport(allowed, category, row); var preview = await PreviewImport(allowed, batch);
        Assert.Equal(0, preview.RejectedRows);
        var body = new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint);
        var key = Guid.NewGuid().ToString();
        if (committed) Assert.Equal(HttpStatusCode.OK, (await Send(allowed, Imports + $"/{batch.Id}/commit", body, key)).StatusCode);
        long revision; uint version; int receipts;
        await using (var db = Db()) {
            var stored = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct); revision = stored.Revision; version = stored.Version;
            receipts = await db.CommandReceipts.CountAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct);
        }
        using var deniedHost = Host(edit: category != "vessel", importRead: true, importWrite: true,
            crewRead: true, crewWrite: category != "crew", crewIdentity: true,
            technicalRead: true, technicalWrite: category != "equipment",
            certificateRead: true, certificateWrite: category != "certificate");
        using var denied = ImportSessionClient(deniedHost, session.Cookie, session.Csrf);
        // Prove read permission and the original session still work; only write was revoked.
        Assert.Equal(HttpStatusCode.OK, (await denied.GetAsync("/api/v1/" + Imports + $"/{batch.Id}", Ct)).StatusCode);
        var response = await Send(denied, Imports + $"/{batch.Id}/commit", body, key);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("replayed", await response.Content.ReadAsStringAsync(Ct));
        await using var verify = Db(); var current = await verify.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct);
        Assert.Equal(revision, current.Revision); Assert.Equal(version, current.Version);
        Assert.Equal(committed ? "Committed" : "Previewed", current.State);
        Assert.Equal(receipts, await verify.CommandReceipts.CountAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct));
        if (!committed && category == "equipment") Assert.False(await verify.Assets.AnyAsync(x => x.Id == id, Ct));
        if (!committed && category == "crew") Assert.False(await verify.CrewAssignments.AnyAsync(x => x.Id == id, Ct));
        if (!committed && category == "certificate") Assert.False(await verify.Credentials.AnyAsync(x => x.Id == id, Ct));
        if (!committed && category == "vessel") Assert.NotEqual(row["name"], (await verify.Vessels.SingleAsync(x => x.Id == id, Ct)).Name);
    }

    [Theory]
    [InlineData("crew")]
    [InlineData("certificate")]
    [InlineData("equipment")]
    public async Task Import_category_read_revocation_hides_existing_batch_preview_report_history_and_template(string category)
    {
        using var allowedHost = ImportHost(); using var allowed = ImportSessionClient(allowedHost);
        var session = await ImportSession(allowed);
        var batch = await UploadImport(allowed, category, new() { ["record_key"] = "FICTIONAL INVALID KEY" });
        var preview = await PreviewImport(allowed, batch); Assert.Equal(1, preview.RejectedRows);
        using var deniedHost = Host(importRead: true, importWrite: true, crewIdentity: true,
            crewRead: category != "crew", crewWrite: category != "crew", technicalRead: category != "equipment",
            technicalWrite: category != "equipment", certificateRead: category != "certificate", certificateWrite: category != "certificate");
        using var denied = ImportSessionClient(deniedHost, session.Cookie, session.Csrf);
        var templates = (await denied.GetFromJsonAsync<ImportTemplateDto[]>("/api/v1/" + Imports + "/templates", Ct))!;
        Assert.DoesNotContain(templates, x => x.Category == category);
        foreach (var path in new[] { "/api/v1/" + Imports + $"/{batch.Id}", ExactImport(batch, preview),
            ExactImport(batch, preview, true), "/api/v1/" + Imports + $"/templates/{category}/download" }) {
            using var response = await denied.GetAsync(path, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(batch.SourceHash, content); Assert.DoesNotContain("FICTIONAL", content);
            Assert.DoesNotContain("row_number", content);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        var history = await denied.GetStringAsync("/api/v1/" + Imports, Ct);
        Assert.DoesNotContain(batch.Id.ToString(), history); Assert.DoesNotContain(batch.SourceHash, history);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Import_missing_or_invalid_csrf_cannot_change_mapping_preview_or_commit(bool invalid)
    {
        using var host = ImportHost(); using var client = ImportSessionClient(host); await ImportSession(client);
        var id = Guid.NewGuid(); var batch = await UploadImport(client, "equipment", new() { ["record_key"] = id.ToString(), ["level"] = "Equipment" });
        var preview = await PreviewImport(client, batch);
        var fresh = (await client.GetFromJsonAsync<ImportBatchDto>("/api/v1/" + Imports + $"/{batch.Id}", Ct))!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        if (invalid) client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "FICTIONAL INVALID CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Imports + $"/{batch.Id}/mapping", new ImportMappingCommand(fresh.Version, []), update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Imports + $"/{batch.Id}/preview", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        await using var verify = Db(); var stored = await verify.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct);
        Assert.Equal(fresh.Revision, stored.Revision); Assert.Equal(fresh.Version, stored.Version); Assert.Equal("Previewed", stored.State);
        Assert.False(await verify.Assets.AnyAsync(x => x.Id == id, Ct));
        Assert.False(await verify.CommandReceipts.AnyAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct));
    }
}
