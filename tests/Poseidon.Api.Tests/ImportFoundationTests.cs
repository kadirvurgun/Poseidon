using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string Imports => $"vessels/{SeedVessel.Id}/imports";
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ImportHost(bool write = true) => Host(
        crewRead: true, crewWrite: write, crewIdentity: true, technicalRead: true, technicalWrite: write,
        importRead: true, importWrite: write, certificateRead: true, certificateWrite: write);

    private static byte[] SourceWorkbook(string[] headers, params string[][] rows) {
        using var memory = new MemoryStream(); memory.Write(ImportWorkbook.Template(headers));
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) {
            zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete();
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var all = new[] { headers }.Concat(rows).Select((r, ri) => new XElement(ns + "row", new XAttribute("r", ri + 1), r.Select((c, ci) =>
                new XElement(ns + "c", new XAttribute("r", (ci < 26 ? ((char)('A' + ci)).ToString() : "A" + (char)('A' + ci - 26)) + (ri + 1)),
                    new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", c))))));
            using (var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()))
                writer.Write(new XDocument(new XElement(ns + "worksheet", new XElement(ns + "sheetData", all))).ToString());
            foreach (var entry in zip.Entries) entry.LastWriteTime = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        }
        return memory.ToArray();
    }
    private async Task<ImportBatchDto> UploadImport(HttpClient client, string category, params Dictionary<string, string>[] rows) {
        // Synthetic configured contract is explicit in the workbook, never inserted by product import.
        if (category == "crew") foreach (var row in rows) {
            row.TryAdd("position_policy_version", "synthetic-position-v1");
            row.TryAdd("identity_policy_version", "synthetic-identity-v1"); row.TryAdd("identity_purpose", "synthetic-crew-identity");
        }
        var templates = (await client.GetFromJsonAsync<ImportTemplateDto[]>("/api/v1/" + Imports + "/templates", Ct))!;
        var schema = templates.Single(t => t.Category == category); var headers = schema.Fields.Where(x => x.UserWritable && (x.RequiredInput || rows.Any(r => r.ContainsKey(x.Key)))).Select(x => x.Key).ToArray();
        using var content = new ByteArrayContent(SourceWorkbook(headers, rows.Select(row => headers.Select(f => row.GetValueOrDefault(f, "")).ToArray()).ToArray()));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var response = await client.PostAsync("/api/v1/" + Imports + "/uploads/" + category + (category == "certificate" ? "?assignment=" + SeedVessel.Id : ""), content, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var batch = (await response.Content.ReadFromJsonAsync<ImportBatchDto>(Ct))!;
        var mapped = await Send(client, Imports + $"/{batch.Id}/mapping", new ImportMappingCommand(batch.Version,
            headers.Select((f, i) => new ImportColumnBinding(i, f)).ToArray()), update: true);
        Assert.Equal(HttpStatusCode.OK, mapped.StatusCode); return (await mapped.Content.ReadFromJsonAsync<ImportBatchDto>(Ct))!;
    }
    private async Task<ImportPreviewPageDto> PreviewImport(HttpClient client, ImportBatchDto b) {
        var response = await Send(client, Imports + $"/{b.Id}/preview", new { }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportPreviewPageDto>(Ct))!;
    }
    private string ExactImport(ImportBatchDto b, ImportPreviewPageDto p, bool reject = false) =>
        "/api/v1/" + Imports + $"/{b.Id}/previews/{p.PreviewId}" + (reject ? "/rejects" : "") + $"?revision={p.Revision}&fingerprint={p.TemplateFingerprint}";

    [Theory]
    [InlineData("vessel")]
    [InlineData("crew")]
    [InlineData("certificate")]
    [InlineData("equipment")]
    public async Task Import_actual_four_categories_preview_without_domain_writes_and_commit_same_paths_with_durable_receipt(string category) {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var id = Guid.NewGuid(); var row = new Dictionary<string, string> { ["record_key"] = id.ToString(), ["code"] = "FICTIONAL-" + id.ToString("N"),
            ["name"] = "FICTIONAL import", ["full_name"] = "FICTIONAL PRIVATE IMPORT", ["level"] = "Equipment", ["issuing_country"] = "123456789012345678901234567890.00000000000001" };
        if (category == "vessel") { var v = (await client.GetFromJsonAsync<VesselDto>("/api/v1/vessels/" + SeedVessel.Id, Ct))!; id = v.Id; row["record_key"] = id.ToString(); row["base_version"] = v.Version.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        var batch = await UploadImport(client, category, row);
        int writesBefore;
        await using (var db = Db()) writesBefore = await db.AuditEvents.CountAsync(x => x.EntityType == "Credential" || x.EntityType == "CrewAssignment" || x.EntityType == "Asset" || x.EntityType == "Vessel", Ct);
        var p = await PreviewImport(client, batch); Assert.Equal(1, p.ValidRows); Assert.Equal(0, p.RejectedRows);
        await using (var db = Db()) Assert.Equal(writesBefore, await db.AuditEvents.CountAsync(x => x.EntityType == "Credential" || x.EntityType == "CrewAssignment" || x.EntityType == "Asset" || x.EntityType == "Vessel", Ct));
        var body = new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint); var key = Guid.NewGuid().ToString();
        var committed = await Send(client, Imports + $"/{batch.Id}/commit", body, key); Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var replay = await Send(client, Imports + $"/{batch.Id}/commit", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<ImportCommittedDto>(Ct))!.Replayed);
        await using var verify = Db();
        Assert.Equal(1, await verify.CommandReceipts.CountAsync(x => x.Action == "ImportCommit" && x.ResourceId == batch.Id, Ct));
        Assert.Equal("Committed", (await verify.ImportBatches.SingleAsync(x => x.Id == batch.Id, Ct)).State);
        Assert.DoesNotContain("FICTIONAL PRIVATE IMPORT", string.Join('|', await verify.AuditEvents.Select(x => x.ChangeSummary).ToArrayAsync(Ct)));
        if (category == "certificate") {
            var credential = await verify.Credentials.SingleAsync(x => x.Id == id, Ct);
            Assert.Equal(row["issuing_country"], credential.IssuingCountryNumeric); Assert.Equal("Unverified", credential.VerificationState);
            Assert.Equal("Draft", credential.Status); Assert.Equal("Unknown", credential.AlertState); Assert.Equal(SeedVessel.Id, credential.PersonId);
        }
    }
    [Fact]
    public async Task Import_duplicate_key_rejects_every_occurrence_and_never_commits_partial_success() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var id = Guid.NewGuid(); var row = new Dictionary<string, string> { ["record_key"] = id.ToString(), ["level"] = "Equipment", ["name"] = "FICTIONAL PRIVATE CELL" };
        var b = await UploadImport(client, "equipment", row, row); var p = await PreviewImport(client, b);
        Assert.Equal(2, p.RejectedRows); Assert.Equal(0, p.ValidRows); Assert.All(p.Rows, r => Assert.Contains(r.Issues, x => x.Code == "duplicate"));
        var commit = await Send(client, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint));
        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode); await using var db = Db(); Assert.False(await db.Assets.AnyAsync(x => x.Id == id, Ct));
    }
    [Fact]
    public async Task Import_reject_report_uses_safe_codes_exact_revision_context_and_current_permissions() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var b = await UploadImport(client, "equipment", new() { ["record_key"] = "PRIVATE MALFORMED KEY", ["level"] = "PRIVATE INVALID LEVEL" });
        var p = await PreviewImport(client, b); var report = await client.GetAsync(ExactImport(b, p, true), Ct);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode); var csv = await report.Content.ReadAsStringAsync(Ct);
        Assert.StartsWith("row_number,field_ordinal,code\r\n", csv); Assert.DoesNotContain("PRIVATE", csv); Assert.Contains("invalid_format", csv);
        Assert.Equal("no-store", report.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(ExactImport(b, p, true).Replace(p.TemplateFingerprint, "changed"), Ct)).StatusCode);
        using var revokedHost = Host(); using var revoked = Client(revokedHost);
        var denied = await revoked.GetAsync(ExactImport(b, p, true), Ct); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain("row_number", await denied.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Import_offset_pagination_and_aggregates_share_the_same_authorized_preview() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var rows = Enumerable.Range(0, 26).Select(_ => new Dictionary<string, string> { ["record_key"] = Guid.NewGuid().ToString(), ["level"] = "Equipment" }).ToArray();
        var b = await UploadImport(client, "equipment", rows); var p = await PreviewImport(client, b);
        Assert.Equal(26, p.TotalRows); Assert.Equal(26, p.ValidRows); Assert.Equal(25, p.Rows.Length); Assert.True(p.HasMore);
        var next = (await client.GetFromJsonAsync<ImportPreviewPageDto>(ExactImport(b, p) + "&offset=25", Ct))!;
        Assert.Single(next.Rows); Assert.Equal(27, next.Rows[0].RowNumber); Assert.Equal(26, next.TotalRows); Assert.False(next.HasMore);
    }
    [Fact]
    public async Task Import_mapping_changes_invalidate_old_preview_report_and_commit() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var b = await UploadImport(client, "equipment", new() { ["record_key"] = Guid.NewGuid().ToString(), ["level"] = "Equipment" });
        var p = await PreviewImport(client, b);
        var fresh = (await client.GetFromJsonAsync<ImportBatchDto>("/api/v1/" + Imports + $"/{b.Id}", Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{b.Id}/mapping", new ImportMappingCommand(fresh.Version, []), update: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(ExactImport(b, p, true), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint))).StatusCode);
    }
    [Fact]
    public async Task Import_current_domain_change_after_preview_rolls_back_entire_batch_and_has_no_receipt() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var id = Guid.NewGuid(); var b = await UploadImport(client, "equipment", new() { ["record_key"] = id.ToString(), ["level"] = "Equipment" });
        var p = await PreviewImport(client, b);
        await using (var db = Db()) { db.Assets.Add(new() { TenantId = db.TenantId, Id = id, VesselId = SeedVessel.Id, Level = "Equipment" }); await db.SaveChangesAsync(Ct); }
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint))).StatusCode);
        await using var verify = Db(); Assert.False(await verify.CommandReceipts.AnyAsync(x => x.Action == "ImportCommit" && x.ResourceId == b.Id, Ct));
        Assert.Equal("Previewed", (await verify.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct)).State);
    }
    [Fact]
    public async Task Import_foreign_vessel_scope_precedes_templates_counts_upload_and_history() {
        using var host = Host(manage: false, read: false, importRead: true, importWrite: true, technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var other = seed.Vessels.First(x => x.Id != SeedVessel.Id);
        foreach (var suffix in new[] { "", "/templates", "/" + Guid.NewGuid() }) {
            var response = await client.GetAsync($"/api/v1/vessels/{other.Id}/imports{suffix}", Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("items", body); Assert.DoesNotContain(other.Id.ToString(), body);
        }
    }
    [Fact]
    public async Task Import_malformed_or_oversized_bytes_are_bounded_and_do_not_echo_private_inputs() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        foreach (var bytes in new[] { System.Text.Encoding.UTF8.GetBytes("PRIVATE NON ZIP"), new byte[ImportWorkbook.MaxBytes + 1] }) {
            using var content = new ByteArrayContent(bytes); content.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            var response = await client.PostAsync("/api/v1/" + Imports + "/uploads/equipment", content, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync(Ct));
        }
    }
    [Fact]
    public async Task Certificate_ordinary_and_imported_metadata_share_exact_private_validation_and_owner_derivation() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var id = Guid.NewGuid(); var path = $"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/certificates";
        var metadata = new CertificateMetadata(new(CertificateNumber: "FICTIONAL PRIVATE", IssueDate: new(2027, 1, 1), ExpiryDate: new(2026, 11, 1)), IssuingCountryNumeric: "12.00000000000000000000000000000001");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, path, new CredentialSaveCommand(id, null, metadata))).StatusCode);
        var list = (await client.GetFromJsonAsync<CredentialDto[]>("/api/v1/" + path, Ct))!; var row = list.Single(x => x.Id == id);
        Assert.Equal("Unverified", row.VerificationState); Assert.Equal(DeclaredCredentialDateOrder.ExpiryBeforeIssue, row.Dates.Order);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, path, new CredentialSaveCommand(id, row.Version, metadata with { IssuingCountryNumeric = "TR" }))).StatusCode);
        var b = await UploadImport(client, "certificate", new() { ["record_key"] = Guid.NewGuid().ToString(), ["issuing_country"] = "TR" });
        Assert.Equal(1, (await PreviewImport(client, b)).RejectedRows);
    }

    [Fact]
    public async Task Import_unmapped_fields_preserve_operator_metadata_while_current_version_is_required() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        uint version;
        await using (var db = Db()) {
            var asset = await db.Assets.SingleAsync(x => x.Id == SeedVessel.Id, Ct);
            asset.Name = "FICTIONAL OPERATOR NAME"; asset.SerialNumber = "FICTIONAL OPERATOR SERIAL";
            await db.SaveChangesAsync(Ct); version = asset.Version;
        }
        var b = await UploadImport(client, "equipment", new() { ["record_key"] = SeedVessel.Id.ToString(), ["base_version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["level"] = "Equipment" });
        var keep = b.Bindings.Where(x => x.TargetField is "record_key" or "base_version" or "level").ToArray();
        var saved = await Send(client, Imports + $"/{b.Id}/mapping", new ImportMappingCommand(b.Version, keep), update: true);
        b = (await saved.Content.ReadFromJsonAsync<ImportBatchDto>(Ct))!;
        var p = await PreviewImport(client, b); Assert.Equal(0, p.RejectedRows);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint))).StatusCode);
        await using var verify = Db(); var edited = await verify.Assets.SingleAsync(x => x.Id == SeedVessel.Id, Ct);
        Assert.Equal("FICTIONAL OPERATOR NAME", edited.Name); Assert.Equal("FICTIONAL OPERATOR SERIAL", edited.SerialNumber);
    }

    [Fact]
    public async Task Concurrent_commit_retry_converges_on_one_durable_batch_and_row_receipt() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var id = Guid.NewGuid(); var b = await UploadImport(client, "equipment", new() { ["record_key"] = id.ToString(), ["level"] = "Equipment" });
        var p = await PreviewImport(client, b); var body = new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint); var key = Guid.NewGuid().ToString();
        var attempts = await Task.WhenAll(Send(client, Imports + $"/{b.Id}/commit", body, key), Send(client, Imports + $"/{b.Id}/commit", body, key));
        Assert.Contains(attempts, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(attempts, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var replay = await Send(client, Imports + $"/{b.Id}/commit", body, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<ImportCommittedDto>(Ct))!.Replayed);
        await using var db = Db(); Assert.Equal(1, await db.Assets.CountAsync(x => x.Id == id, Ct));
        Assert.Equal(1, await db.CommandReceipts.CountAsync(x => x.Action == "ImportCommit" && x.ResourceId == b.Id, Ct));
        Assert.Equal(1, await db.CommandReceipts.CountAsync(x => x.Action == "EquipmentSave" && x.ResourceId == id, Ct));
    }

    [Fact]
    public async Task Reconstructed_actual_host_replays_using_persisted_batch_receipt_and_current_authorization() {
        ImportBatchDto b; ImportPreviewPageDto p; string cookie; string csrf; var key = Guid.NewGuid().ToString();
        using (var host = ImportHost()) {
            using var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add("X-Directory-Test", "synthetic");
            var session = await client.GetAsync("/api/v1/auth/session", Ct); csrf = (await session.Content.ReadFromJsonAsync<SessionDto>(Ct))!.CsrfToken;
            cookie = string.Join("; ", session.Headers.GetValues("Set-Cookie").Select(x => x.Split(';')[0]));
            client.DefaultRequestHeaders.Add("Cookie", cookie); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
            b = await UploadImport(client, "equipment", new() { ["record_key"] = Guid.NewGuid().ToString(), ["level"] = "Equipment" }); p = await PreviewImport(client, b);
            Assert.Equal(HttpStatusCode.OK, (await Send(client, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint), key)).StatusCode);
        }
        using var restarted = ImportHost(); using var retry = restarted.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        retry.DefaultRequestHeaders.Add("X-Directory-Test", "synthetic"); retry.DefaultRequestHeaders.Add("Cookie", cookie); retry.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        var response = await Send(retry, Imports + $"/{b.Id}/commit", new ImportCommitCommand(p.PreviewId, long.Parse(p.Revision), p.TemplateFingerprint), key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True((await response.Content.ReadFromJsonAsync<ImportCommittedDto>(Ct))!.Replayed);
        // This reconstructs the real API host with the same PostgreSQL/key ring; it does not
        // claim an executed production process/database restore or a provider DR rehearsal.
    }

    [Fact]
    public async Task Identical_source_upload_retry_reuses_only_the_same_authorized_context_batch() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var row = new Dictionary<string, string> { ["record_key"] = Guid.NewGuid().ToString(), ["level"] = "Equipment" };
        var first = await UploadImport(client, "equipment", row); var retry = await UploadImport(client, "equipment", row);
        Assert.Equal(first.Id, retry.Id); Assert.Equal(first.SourceHash, retry.SourceHash);
        await using var db = Db(); Assert.Equal(1, await db.ImportBatches.CountAsync(x => x.SourceHash == first.SourceHash && x.VesselId == SeedVessel.Id, Ct));
    }
}
