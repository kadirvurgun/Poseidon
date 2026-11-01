using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// Actual host, real owned PostgreSQL and real secure-file transport. Authored only.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] GraphPermissions = ["directory.read", "directory.write", "regulatory.draft.read", "regulatory.draft.write", "applicability.read", "control.read", "control.write", "control.owner.eligible", "evidence.read", "evidence.write", "file.read", "file.write"];
    private void GraphPermissionsForTest(IServiceProvider services) {
        var settings = services.GetRequiredService<FederationSettings>();
        settings.Issuer ??= settings.Memberships[0].Issuer; // Test-only provisioned fixture, no real auth setup.
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = GraphPermissions };
    }
    private ControlEvidenceCommand GraphCommand(uint version = 0, ControlEvidenceContent? content = null) => new(version, content ?? new(Title: "FICTIONAL control"), [], [], null, version == 0 ? "INITIAL_AUTHORING" : "AUTHORING_CORRECTION");
    private string GraphRoute(string route, Guid? vessel = null) => $"{route}?vesselId={vessel ?? SeedVessel.Id}";
    private async Task<ControlEvidenceDto> GraphSave(HttpClient c, string route, ControlEvidenceCommand command, string? key = null, bool update = false) {
        using var response = await Send(c, GraphRoute(route), command, key, update); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
    }
    [Fact]
    public async Task Graph_capabilities_default_deny_and_missing_scope_hides_all_lists_reports_and_owners() {
        await using var host = Host(regulatoryRead: true); using var c = Client(host); await Token(c);
        foreach (var route in new[] { "controls", "evidence", "controls/owners", "controls/orphans" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/" + GraphRoute(route), Ct)).StatusCode);
        GraphPermissionsForTest(host.Services);
        var otherTenantVessel = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        foreach (var route in new[] { "controls", "evidence", "controls/owners", "controls/orphans" })
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + GraphRoute(route, otherTenantVessel.Id), Ct)).StatusCode);
    }
    [Fact]
    public async Task Draft_noop_appends_checks_xmin_and_exact_original_receipt_survives_later_edits() {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var first = await GraphSave(c, "controls", GraphCommand()); var key = Guid.NewGuid().ToString(); var command = GraphCommand(first.Version, first.Content);
        var second = await GraphSave(c, $"controls/{first.Id}", command, key, true); Assert.Equal(2, second.Revision); Assert.NotEqual(first.Version, second.Version);
        var third = await GraphSave(c, $"controls/{first.Id}", GraphCommand(second.Version, second.Content), update: true); Assert.Equal(3, third.Revision);
        var replay = await GraphSave(c, $"controls/{first.Id}", command, key, true); Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(replay));
        using var stale = await Send(c, GraphRoute($"controls/{first.Id}"), command, update: true); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var reused = await Send(c, GraphRoute($"controls/{first.Id}"), command with { Content = new(Title: "different") }, key, true); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); settings.Memberships[0] = settings.Memberships[0] with { Permissions = GraphPermissions.Where(p => p != "file.read").ToArray() };
        using var revoked = await Send(c, GraphRoute($"controls/{first.Id}"), command, key, true); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }
    [Fact]
    public async Task Critical_orphans_are_draft_findings_and_unavailable_is_never_all_clear() {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var critical = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL orphan", Critical: true)));
        var unknown = await GraphSave(c, "controls", GraphCommand(content: new(Title: "FICTIONAL unknown", Critical: null)));
        var report = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls/orphans"), Ct);
        Assert.False(report.GetProperty("available").GetBoolean()); Assert.Equal("AUTHORITATIVE_APPLICABILITY_UNAVAILABLE", report.GetProperty("reason").GetString());
        var ids = report.GetProperty("draftCriticalOrphans").EnumerateArray().Select(x => x.GetGuid()).ToArray(); Assert.Contains(critical.Id, ids); Assert.DoesNotContain(unknown.Id, ids);
        using var submit = await Send(c, GraphRoute($"controls/{critical.Id}/submit"), new { }); Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Equal("UNKNOWN", critical.Completion); Assert.Equal("UNVERIFIED", critical.Verification); Assert.Equal("UNKNOWN", critical.Effectiveness);
    }
    [Fact]
    public async Task Requirements_pin_exact_source_and_rule_revision_and_graph_is_many_to_many() {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var source = RegulatoryRegisterFixtures.SourceId(seed.Tenants[0].Id); var requirement = RegulatoryRegisterFixtures.RequirementId(seed.Tenants[0].Id);
        var pin = new RequirementPin(requirement, 1, source, 1);
        var secondRequirement = await CreateRegister(c, "requirement-drafts", new(), source, 1);
        var pin2 = new RequirementPin(secondRequirement.Current.Id, secondRequirement.Current.Revision, source, 1);
        var a = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin] });
        var b = await GraphSave(c, "controls", GraphCommand() with { Requirements = [pin2] });
        var evidence = await GraphSave(c, "evidence", GraphCommand(content: new(Title: "FICTIONAL incomplete evidence")) with { Requirements = [pin, pin2], Links = [new("Control", a.Id, 1, a.Version), new("Control", b.Id, 1, b.Version)] });
        Assert.Equal(2, evidence.Links.Length); Assert.False(evidence.EvidenceExists); Assert.Contains("CLEAN_FILE_MISSING", evidence.Blockers);
        var relink = await GraphSave(c, $"evidence/{evidence.Id}", GraphCommand(evidence.Version, evidence.Content) with { Requirements = [pin], Links = [new("Control", b.Id, 1, b.Version)] }, update: true);
        var history = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"evidence/{evidence.Id}/versions"), Ct);
        var old = history.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("revision").GetInt64() == 1); Assert.Equal(2, old.GetProperty("links").GetArrayLength()); Assert.Equal(2, old.GetProperty("requirements").GetArrayLength()); Assert.Equal("SUPERSEDED", old.GetProperty("timeState").GetString()); Assert.Single(relink.Links);
        await GraphSave(c, $"controls/{b.Id}", GraphCommand(b.Version, b.Content), update: true);
        var replayHistory = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute($"evidence/{evidence.Id}/versions") + "&asOf=" + Uri.EscapeDataString(evidence.AsOf.ToString("O")), Ct);
        var historical = Assert.Single(replayHistory.GetProperty("items").EnumerateArray());
        Assert.Equal(1, historical.GetProperty("revision").GetInt64()); Assert.Equal("UNKNOWN", historical.GetProperty("timeState").GetString()); Assert.Equal(2, historical.GetProperty("requirements").GetArrayLength());
        Assert.Equal(b.Version, historical.GetProperty("links").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == b.Id).GetProperty("expectedVersion").GetUInt32());
        using var wrong = await Send(c, GraphRoute("controls"), GraphCommand() with { Requirements = [pin with { SourceId = Guid.NewGuid() }] }); Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var wrongRule = await Send(c, GraphRoute("controls"), GraphCommand() with { Requirements = [pin with { RuleId = ApplicabilityFixtures.Id(seed.Tenants[0].Id), RuleRevision = 1 }] }); Assert.Equal(HttpStatusCode.BadRequest, wrongRule.StatusCode);
    }
    [Fact]
    public async Task Missing_synthetic_and_cross_vessel_file_references_and_unknown_owners_are_rejected() {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        foreach (var id in new[] { Guid.NewGuid(), SeedVessel.Id, seed.Vessels.First(v => v.Id != SeedVessel.Id).Id }) {
            using var response = await Send(c, GraphRoute("evidence"), GraphCommand() with { File = new(id, 1, new string('A', 64)) }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var owner = await Send(c, GraphRoute("controls"), GraphCommand(content: new(OwnerId: Guid.NewGuid(), OwnerRole: Guid.NewGuid()))); Assert.Equal(HttpStatusCode.BadRequest, owner.StatusCode);
        var controls = await GraphSave(c, "controls", GraphCommand());
        using var wrongVersion = await Send(c, GraphRoute("evidence"), GraphCommand() with { Links = [new("Control", controls.Id, controls.Revision, controls.Version + 1)] }); Assert.Equal(HttpStatusCode.Conflict, wrongVersion.StatusCode);
    }
    [Fact]
    public async Task Actual_file_bytes_are_required_immutable_and_corruption_blocks_reference_retries() {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-control-evidence-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var peer = new ScanPeer("stream: OK\0"); await using var host = FileHost(root, peer.Port); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
            var staged = await StagePng(c); using var upload = await PutPng(c, staged.Id); Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            var uploaded = (await upload.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            using var access = await c.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{uploaded.Id}/access", null, Ct); Assert.Equal(HttpStatusCode.OK, access.StatusCode);
            var capability = (await access.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetString();
            using var wrongDownload = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vessels/{seed.Vessels.First(v => v.Id != SeedVessel.Id).Id}/files/{uploaded.Id}/bytes");
            wrongDownload.Headers.Add("X-File-Capability", capability); using var wrongScope = await c.SendAsync(wrongDownload, Ct); Assert.Equal(HttpStatusCode.NotFound, wrongScope.StatusCode);
            var command = GraphCommand(content: new(Title: "FICTIONAL transport evidence", Provenance: "Synthetic protocol test", ObservedAt: DateTimeOffset.UtcNow.AddDays(-3), ValidUntil: DateTimeOffset.UtcNow.AddDays(-1))) with { File = new(uploaded.Id, uploaded.Revision!.Value, uploaded.Checksum!) };
            var key = Guid.NewGuid().ToString(); var evidence = await GraphSave(c, "evidence", command, key);
            Assert.True(evidence.EvidenceExists); Assert.Equal("UNVERIFIED", evidence.Verification); Assert.Equal("DECLARED_EXPIRED", evidence.TimeState);
            using var remove = await Send(c, GraphRoute($"evidence/{evidence.Id}"), GraphCommand(evidence.Version, evidence.Content), update: true); Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
            var path = Path.Combine(root, seed.Tenants[0].Id.ToString("N") + "-" + uploaded.Id.ToString("N") + ".object"); await File.WriteAllBytesAsync(path, [0], Ct);
            using var corrupt = await Send(c, GraphRoute("evidence"), command, key); Assert.Equal(HttpStatusCode.ServiceUnavailable, corrupt.StatusCode);
            File.Delete(path); using var missing = await Send(c, GraphRoute("evidence"), command); Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
            await using var db = Db(); Assert.Single(await db.ControlEvidenceRevisions.Where(x => x.Id == evidence.Id).ToArrayAsync(Ct)); Assert.Single(await db.ControlEvidenceReceipts.Where(x => x.ResourceId == evidence.Id).ToArrayAsync(Ct));
        } finally { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root, false); }
    }
    [Fact]
    public async Task Malicious_quarantine_cannot_become_evidence_even_with_matching_expected_checksum() {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-control-quarantine-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            using var peer = new ScanPeer("stream: FOUND\0"); await using var host = FileHost(root, peer.Port); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
            var pending = await StagePng(c);
            using var beforeUpload = await Send(c, GraphRoute("evidence"), GraphCommand() with { File = new(pending.Id, 1, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png))) }); Assert.Equal(HttpStatusCode.BadRequest, beforeUpload.StatusCode);
            using var upload = await PutPng(c, pending.Id); Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
            using var reference = await Send(c, GraphRoute("evidence"), GraphCommand() with { File = new(pending.Id, 1, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png))) }); Assert.Equal(HttpStatusCode.BadRequest, reference.StatusCode);
            await using var db = Db(); Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == pending.Id, Ct)); Assert.False(await db.ControlEvidenceRevisions.AnyAsync(x => x.FileId == pending.Id, Ct));
        } finally { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root, false); }
    }
    [Theory]
    [InlineData("actorContext")]
    [InlineData("verified")]
    [InlineData("effective")]
    [InlineData("tenantId")]
    public async Task Caller_authority_members_are_rejected_without_business_effect(string member) {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(GraphCommand(), new JsonSerializerOptions(JsonSerializerDefaults.Web)))!; node[member] = true;
        await using var before = Db(); var count = await before.ControlEvidenceHeads.CountAsync(Ct);
        using var response = await Send(c, GraphRoute("controls"), node); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var after = Db(); Assert.Equal(count, await after.ControlEvidenceHeads.CountAsync(Ct));
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"expectedVersion\":0,\"expectedVersion\":1}")]
    public async Task Invalid_root_and_duplicate_members_are_bad_requests_without_receipts(string body) {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var key = Guid.NewGuid().ToString(); using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + GraphRoute("controls")) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key); using var response = await c.SendAsync(request, Ct); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = Db(); Assert.False(await db.ControlEvidenceReceipts.AnyAsync(r => r.Key == key, Ct));
    }
    [Fact]
    public async Task Duplicate_race_has_one_effect_and_a_new_key_stale_noop_conflicts() {
        await using var host = Host(); using var a = Client(host); using var b = Client(host); GraphPermissionsForTest(host.Services); await Token(a); await Token(b);
        var first = await GraphSave(a, "controls", GraphCommand()); var key = Guid.NewGuid().ToString(); var command = GraphCommand(first.Version, first.Content);
        var results = await Task.WhenAll(Send(a, GraphRoute($"controls/{first.Id}"), command, key, true), Send(b, GraphRoute($"controls/{first.Id}"), command, key, true));
        foreach (var response in results) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        await using var db = Db(); Assert.Equal(2, await db.ControlEvidenceRevisions.CountAsync(x => x.Id == first.Id, Ct)); Assert.Single(await db.ControlEvidenceReceipts.Where(x => x.ResourceId == first.Id && x.Key == key).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Receipt_storage_failure_rolls_back_business_revision_and_attributed_write_audit() {
        var fault = new RejectGraphReceipt(); await using var host = Host(interceptor: fault); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var first = await GraphSave(c, "controls", GraphCommand());
        await using var before = Db(); var audits = await before.AuditEvents.CountAsync(Ct); var key = Guid.NewGuid().ToString(); fault.Armed = true;
        using var failed = await Send(c, GraphRoute($"controls/{first.Id}"), GraphCommand(first.Version, first.Content), key, true); Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        await using var check = Db(); Assert.Equal(first.Revision, (await check.ControlEvidenceHeads.SingleAsync(x => x.Id == first.Id, Ct)).Revision);
        Assert.Single(await check.ControlEvidenceRevisions.Where(x => x.Id == first.Id).ToArrayAsync(Ct)); Assert.False(await check.ControlEvidenceReceipts.AnyAsync(x => x.Key == key, Ct)); Assert.Equal(audits, await check.AuditEvents.CountAsync(Ct));
    }
    private sealed class RejectGraphReceipt : DbCommandInterceptor {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO core.\"ControlEvidenceReceipts\"", StringComparison.Ordinal)) throw new Npgsql.NpgsqlException("Synthetic receipt failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Owner_is_actual_provisioned_member_role_and_retry_rechecks_live_eligibility() {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
        var role = Guid.Parse(member.RoleReferences!.Single());
        var owners = await c.GetFromJsonAsync<JsonElement>("/api/v1/" + GraphRoute("controls/owners"), Ct);
        Assert.Contains(owners.GetProperty("items").EnumerateArray(), o => o.GetProperty("actorId").GetGuid() == member.ActorId && o.GetProperty("roleId").GetGuid() == role);
        var key = Guid.NewGuid().ToString(); var command = GraphCommand(content: new(Title: "FICTIONAL owner binding", OwnerId: member.ActorId, OwnerRole: role));
        var saved = await GraphSave(c, "controls", command, key); Assert.Equal(member.ActorId, saved.Content.OwnerId);
        settings.Memberships[0] = member with { Permissions = GraphPermissions.Where(p => p != "control.owner.eligible").ToArray() };
        using var replay = await Send(c, GraphRoute("controls"), command, key); Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        await using var db = Db(); Assert.Single(await db.ControlEvidenceRevisions.Where(r => r.Id == saved.Id).ToArrayAsync(Ct));
    }
}
