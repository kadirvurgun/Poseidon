using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// AUTHORED / NOT RUN: actual API, audited EF, owned real PostgreSQL, no InMemory substitute.
public sealed partial class DirectoryWorkspaceTests
{
    private WebApplicationFactory<Program> LegacyImportHost(DbCommandInterceptor? interceptor = null) =>
        ProfileHost(interceptor: interceptor).WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:38"] = "import.legacy.convert",
            ["Authentication:Oidc:Memberships:0:Permissions:44"] = "crew.profile.medical.read",
            ["Authentication:Oidc:Memberships:0:Permissions:45"] = "crew.profile.medical.write",
            [$"CrewProfilePolicy:Fields:{(int)CrewPersonalField.MedicalExpiry}:ReadCapability"] = "crew.profile.medical.read",
            [$"CrewProfilePolicy:Fields:{(int)CrewPersonalField.MedicalExpiry}:WriteCapability"] = "crew.profile.medical.write"
        })));
    private string LegacyImportPath(Guid id) => Imports + $"/{id}/legacy-protection";
    private async Task<ImportBatch> HistoricalBatch(string table = " {\"headers\":[\"FICTIONAL PRIVATE HEADER\"],\"rows\":[[\"FICTIONAL PRIVATE CELL\"]],\"unknownOldField\":\"preserve me\"} ", bool committed = false) {
        await using var db = Db();
        var b = new ImportBatch { TenantId = seed.Tenants[0].Id, VesselId = SeedVessel.Id, Id = Guid.NewGuid(), ActorId = Guid.NewGuid(),
            SessionHash = "expired-historical-session", Category = "crew", TemplateVersion = "legacy-unsupported-v0", TemplateFingerprint = "historical-template",
            TableJson = table, BindingsJson = " [{\"sourceIndex\":0,\"targetField\":\"FICTIONAL UNKNOWN TARGET\"}] ", SourceHash = "FICTIONAL-HISTORICAL-SOURCE-HASH",
            SourceName = "FICTIONAL PRIVATE FILENAME.xlsx", SourceLocator = "FICTIONAL PRIVATE SOURCE LOCATOR", RowCount = 17, Revision = 4,
            State = committed ? "Committed" : "MappingDraft", CreatedAt = DateTimeOffset.UtcNow.AddDays(-10), CommittedAt = committed ? DateTimeOffset.UtcNow.AddDays(-9) : null };
        db.ImportBatches.Add(b); await db.SaveChangesAsync(Ct);
        // Compare conversion against the actual historical database baseline,
        // including PostgreSQL's microsecond timestamp representation.
        await db.Entry(b).ReloadAsync(Ct); return b;
    }
    private static LegacyCrewImportCommand Protect(ImportBatch b) => new(b.Version, LegacyCrewImport.Reason);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Historical_plaintext_conversion_preserves_exact_source_and_metadata_without_making_unsupported_schema_writable(bool committed, bool opaque) {
        var b = await HistoricalBatch(opaque ? "UNSUPPORTED HISTORICAL PAYLOAD\r\n  FICTIONAL PRIVATE CELL  " : " {\"obsolete\":true,\"rows\":[[\"FICTIONAL PRIVATE CELL\"]]} ", committed);
        var expected = new HistoricalCrewImportSource(b.TableJson, b.BindingsJson, b.SourceHash, b.SourceName, b.SourceLocator, b.CrewIdentityPolicyVersion);
        using var host = LegacyImportHost(); using var client = Client(host); await Token(client);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/" + LegacyImportPath(b.Id), Ct);
        Assert.Equal("LegacyUnavailable", status.GetProperty("storageState").GetString()); Assert.False(status.TryGetProperty("rowCount", out _));
        var key = Guid.NewGuid().ToString(); var response = await Send(client, LegacyImportPath(b.Id), Protect(b), key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Ct); Assert.Contains("ProtectedHistoricalV1", text); Assert.Contains("Unavailable", text); Assert.Contains("Unknown", text);
        await using var verify = Db(); var saved = await verify.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct);
        Assert.Equal(expected, host.Services.GetRequiredService<CrewProfileProtection>().OpenHistoricalImport(saved));
        Assert.StartsWith(LegacyCrewImport.Prefix + "synthetic-key-v1.", saved.TableJson);
        Assert.Equal("[]", saved.BindingsJson); Assert.Equal("", saved.SourceHash); Assert.Equal("name-withheld.xlsx", saved.SourceName); Assert.Equal("protected-history", saved.SourceLocator);
        Assert.Null(saved.CrewIdentityPolicyVersion); Assert.Equal(b.ActorId, saved.ActorId); Assert.Equal(b.SessionHash, saved.SessionHash);
        Assert.Equal(b.State, saved.State); Assert.Equal(b.Revision, saved.Revision); Assert.Equal(b.RowCount, saved.RowCount);
        Assert.Equal(b.TemplateVersion, saved.TemplateVersion); Assert.Equal(b.TemplateFingerprint, saved.TemplateFingerprint); Assert.Equal(b.CommittedAt, saved.CommittedAt);
        var receipt = await verify.CommandReceipts.SingleAsync(x => x.Key == key, Ct); Assert.Equal(saved.Version, receipt.RegisterVersion);
        var audit = await verify.AuditEvents.SingleAsync(x => x.EntityType == nameof(ImportBatch) && x.Action == "Modified" && x.ResourceId == b.Id, Ct);
        Assert.Equal(actorId.ToString(), audit.ActorId); Assert.Equal(SeedVessel.Id, audit.VesselId); Assert.Equal(b.Version, audit.BeforeVersion);
        var receiptAudit = await verify.AuditEvents.SingleAsync(x => x.EntityType == nameof(CommandReceipt) && x.ResourceId == b.Id, Ct);
        Assert.Equal(saved.Version, receiptAudit.AfterVersion);
        foreach (var secret in new[] { "FICTIONAL PRIVATE CELL", b.SourceHash, b.SourceName, b.SourceLocator, saved.TableJson }) {
            Assert.DoesNotContain(secret, text); Assert.DoesNotContain(secret, audit.ChangeSummary ?? ""); Assert.DoesNotContain(secret, receiptAudit.ChangeSummary ?? "");
        }
        foreach (var suffix in new[] { "", "/mapping", "/preview", "/commit" }) {
            var result = suffix == "" ? await client.GetAsync("/api/v1/" + Imports + $"/{b.Id}", Ct) :
                await Send(client, Imports + $"/{b.Id}" + suffix, suffix == "/mapping" ? (object)new ImportMappingCommand(saved.Version, []) : new { }, update: suffix == "/mapping");
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }
        Assert.DoesNotContain(b.Id.ToString(), await client.GetStringAsync("/api/v1/" + Imports, Ct));
        await using (var reseed = new PoseidonDbContext(Poseidon.Persistence.Seeding.SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true))
            await Poseidon.Persistence.Seeding.SeedImporter.ImportAsync(reseed, seed, Ct);
        await verify.Entry(saved).ReloadAsync(Ct); Assert.Equal(expected, host.Services.GetRequiredService<CrewProfileProtection>().OpenHistoricalImport(saved));
        Assert.Equal(1, await verify.CommandReceipts.CountAsync(x => x.ResourceId == b.Id, Ct));
    }

    [Fact]
    public async Task Legacy_conversion_exact_retry_after_host_restart_is_pinned_and_changed_attempt_or_stale_base_has_no_receipt() {
        var b = await HistoricalBatch(); var key = Guid.NewGuid().ToString(); uint version;
        using (var host = LegacyImportHost()) { using var client = Client(host); await Token(client);
            var r = await Send(client, LegacyImportPath(b.Id), Protect(b), key); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            version = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("version").GetUInt32(); }
        using var restarted = LegacyImportHost(); using var again = Client(restarted); await Token(again);
        var replay = await Send(again, LegacyImportPath(b.Id), Protect(b), key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<JsonElement>(Ct); Assert.True(body.GetProperty("replayed").GetBoolean()); Assert.Equal(version, body.GetProperty("version").GetUInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(again, LegacyImportPath(b.Id), Protect(b) with { BaseVersion = b.Version + 1 }, key)).StatusCode);
        var rejected = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.Conflict, (await Send(again, LegacyImportPath(b.Id), Protect(b), rejected)).StatusCode);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == rejected, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.EntityType == nameof(ImportBatch) && x.ResourceId == b.Id && x.Action == "Modified", Ct));
    }

    [Fact]
    public async Task Original_cookie_current_membership_and_field_policy_precede_exact_conversion_recovery() {
        var b = await HistoricalBatch(); var untouched = await HistoricalBatch(); var key = Guid.NewGuid().ToString();
        using var host = LegacyImportHost(); using var client = ImportSessionClient(host); await ImportSession(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var old = settings.Memberships[0];
        foreach (var capability in new[] { "import.legacy.convert", "crew.profile.contact.read", "crew.identity.write" }) {
            settings.Memberships[0] = old with { Permissions = old.Permissions.Where(x => x != capability).ToArray() };
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/" + LegacyImportPath(b.Id), Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, LegacyImportPath(untouched.Id), Protect(untouched))).StatusCode);
        }
        settings.Memberships[0] = old;
        var profile = host.Services.GetRequiredService<CrewProfilePolicy>(); profile.Fields[0].Purpose = "revoked-purpose";
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
        profile.Fields[0].Purpose = profile.ImportPurpose; profile.Version = "synthetic-changed-v2";
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/" + LegacyImportPath(b.Id), Ct)).StatusCode);
        await using var db = Db(); Assert.Null((await db.ImportBatches.SingleAsync(x => x.Id == untouched.Id, Ct)).LegacyProtectedAt);
        Assert.Equal(1, await db.CommandReceipts.CountAsync(x => x.ResourceId == b.Id || x.ResourceId == untouched.Id, Ct));
    }

    [Fact]
    public async Task Missing_invalid_and_removed_key_deny_without_plaintext_or_receipt_side_effects() {
        var b = await HistoricalBatch(); using var host = LegacyImportHost(); using var client = Client(host); await Token(client);
        var policy = host.Services.GetRequiredService<CrewProfilePolicy>(); var reference = policy.KeyReferences[policy.ActiveKeyId];
        foreach (var invalid in new[] { "not-an-operator-reference", "POSEIDON_UNENROLLED_LEGACY_KEY" }) {
            policy.KeyReferences[policy.ActiveKeyId] = invalid; var key = Guid.NewGuid().ToString();
            var r = await Send(client, LegacyImportPath(b.Id), Protect(b), key); Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
            Assert.DoesNotContain("FICTIONAL", await r.Content.ReadAsStringAsync(Ct));
            await using var verify = Db(); var row = await verify.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct);
            Assert.Equal(b.TableJson, row.TableJson); Assert.Null(row.LegacyProtectedAt); Assert.False(await verify.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
        }
        policy.KeyReferences[policy.ActiveKeyId] = reference;
        var attempt = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.OK, (await Send(client, LegacyImportPath(b.Id), Protect(b), attempt)).StatusCode);
        policy.ActiveKeyId = "synthetic-new-key"; policy.KeyReferences[policy.ActiveKeyId] = reference;
        policy.KeyReferences.Remove("synthetic-key-v1"); // Active key resolves, historical envelope key is no longer enrolled.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(client, LegacyImportPath(b.Id), Protect(b), attempt)).StatusCode);
        await using var db = Db(); Assert.StartsWith(LegacyCrewImport.Prefix, (await db.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct)).TableJson);
        Assert.Equal(1, await db.CommandReceipts.CountAsync(x => x.ResourceId == b.Id, Ct));
    }

    [Fact]
    public async Task Unknown_foreign_and_caller_authority_or_plaintext_inputs_are_closed_without_counts() {
        var b = await HistoricalBatch(); using var host = LegacyImportHost(); using var client = Client(host); await Token(client);
        foreach (var path in new[] { LegacyImportPath(Guid.NewGuid()), $"vessels/{seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id).Id}/imports/{b.Id}/legacy-protection" }) {
            var r = await Send(client, path, Protect(b)); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
            Assert.DoesNotContain("rowCount", await r.Content.ReadAsStringAsync(Ct)); Assert.DoesNotContain("FICTIONAL", await r.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, LegacyImportPath(b.Id), new { baseVersion = b.Version, reason = LegacyCrewImport.Reason, tenantId = seed.Tenants[0].Id, tableJson = "FICTIONAL forbidden input" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, LegacyImportPath(b.Id), Protect(b) with { Reason = "FICTIONAL arbitrary contents" })).StatusCode);
        await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.ResourceId == b.Id, Ct)); Assert.Equal(b.TableJson, (await db.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct)).TableJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Competing_conversion_attempts_are_atomic_and_exact_retry_recovers_one_durable_result(bool sameKey) {
        var b = await HistoricalBatch(); var barrier = new LegacyBatchReadBarrier(); using var host = LegacyImportHost(barrier); using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString(); var other = sameKey ? key : Guid.NewGuid().ToString();
        var race = await Task.WhenAll(Send(client, LegacyImportPath(b.Id), Protect(b), key), Send(client, LegacyImportPath(b.Id), Protect(b), other));
        Assert.Contains(race, r => r.StatusCode == HttpStatusCode.OK); Assert.All(race, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        if (!sameKey) { Assert.Single(race, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(race, r => r.StatusCode == HttpStatusCode.Conflict); }
        var winner = race[0].StatusCode == HttpStatusCode.OK ? key : other;
        Assert.Equal(HttpStatusCode.OK, (await Send(client, LegacyImportPath(b.Id), Protect(b), winner)).StatusCode);
        await using var db = Db(); Assert.Equal(1, await db.CommandReceipts.CountAsync(x => x.ResourceId == b.Id, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.EntityType == nameof(ImportBatch) && x.ResourceId == b.Id && x.Action == "Modified", Ct));
        Assert.Equal(b.TableJson, host.Services.GetRequiredService<CrewProfileProtection>().OpenHistoricalImport(await db.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct)).TableJson);
    }

    private sealed class LegacyBatchReadBarrier : DbCommandInterceptor {
        private readonly TaskCompletionSource bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("FROM integration.\"ImportBatches\"", StringComparison.Ordinal)) {
                var n = Interlocked.Increment(ref reads);
                if (n == 2) bothRead.TrySetResult();
                if (n <= 2) await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class RejectLegacyReceipt : DbCommandInterceptor {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("INSERT INTO core.\"CommandReceipts\"", StringComparison.Ordinal))
                throw new DbUpdateException("synthetic_receipt_storage_failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task Receipt_failure_rolls_back_encryption_plaintext_clear_and_business_audit_then_same_attempt_succeeds() {
        var b = await HistoricalBatch(); var failure = new RejectLegacyReceipt(); using var host = LegacyImportHost(failure); using var client = Client(host); await Token(client);
        int auditBefore; await using (var before = Db()) auditBefore = await before.AuditEvents.CountAsync(Ct);
        failure.Armed = true; var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
        await using (var db = Db()) {
            var old = await db.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct);
            Assert.Equal(b.TableJson, old.TableJson); Assert.Equal(b.SourceHash, old.SourceHash); Assert.Equal(b.BindingsJson, old.BindingsJson);
            Assert.Null(old.LegacyProtectedAt); Assert.Equal(b.Version, old.Version); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
            Assert.Equal(auditBefore, await db.AuditEvents.CountAsync(Ct));
        }
        failure.Armed = false; Assert.Equal(HttpStatusCode.OK, (await Send(client, LegacyImportPath(b.Id), Protect(b), key)).StatusCode);
    }

    [Fact]
    public async Task Converted_source_cannot_be_replaced_downgraded_remapped_or_deleted_through_ordinary_SaveChanges() {
        var b = await HistoricalBatch(); using var host = LegacyImportHost(); using var client = Client(host); await Token(client);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, LegacyImportPath(b.Id), Protect(b))).StatusCode);
        foreach (var mutation in new Action<ImportBatch>[] { x => x.TableJson = b.TableJson, x => x.LegacyProtectedAt = null, x => x.BindingsJson = b.BindingsJson, x => x.State = "Uploaded" }) {
            await using var db = Db(); var row = await db.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct);
            mutation(row);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        await using var deleting = Db(); deleting.Remove(await deleting.ImportBatches.SingleAsync(x => x.Id == b.Id, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => deleting.SaveChangesAsync(Ct));
        await using var boundary = Db();
        // Intentionally rejected DB probes verify a bypass cannot downgrade/remap the converted row.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => boundary.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE integration.\"ImportBatches\" SET \"TableJson\" = {b.TableJson} WHERE \"TenantId\" = {boundary.TenantId} AND \"Id\" = {b.Id}", Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => boundary.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE integration.\"ImportBatches\" SET \"BindingsJson\" = '[1]' WHERE \"TenantId\" = {boundary.TenantId} AND \"Id\" = {b.Id}", Ct));
    }

    [Fact]
    public async Task Configured_lookup_replacement_withdraws_current_projection_and_preserves_saved_assignment_reference_and_history() {
        using var host = ImportHost(); using var client = Client(host); await Token(client);
        var policy = host.Services.GetRequiredService<CrewPositionPolicy>();
        var command = NewCrew() with { PositionId = SeedVessel.Id, PositionPolicyVersion = policy.Version };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, CrewUrl, command)).StatusCode);
        policy.Version = "synthetic-position-v2"; policy.Positions[0].Rank = "SYNTHETIC REPLACEMENT RANK";
        // Same ID cannot relabel an old persisted reference, even when proposed configuration changes.
        var replacement = new CrewPositionCommand(SeedVessel.Id, policy.Positions[0].Code, policy.Positions[0].Rank,
            policy.Positions[0].Capacity, policy.Positions[0].Department, policy.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, CrewUrl + "/positions", replacement)).StatusCode);
        var profile = (await client.GetFromJsonAsync<CrewProfile>($"/api/v1/{CrewUrl}/{command.Id}", Ct))!;
        Assert.Equal(SeedVessel.Id, profile.Assignment.PositionId); Assert.Null(profile.Assignment.Rank); Assert.Null(profile.Assignment.Department);
        Assert.DoesNotContain("SYNTHETIC REPLACEMENT RANK", await client.GetStringAsync($"/api/v1/{CrewUrl}/{command.Id}/history", Ct));
        await using var db = Db(); var snapshot = await db.CrewAssignmentRevisions.SingleAsync(x => x.AssignmentId == command.Id, Ct);
        Assert.Equal(SeedVessel.Id, snapshot.PositionId);
        var position = await db.CrewPositions.SingleAsync(x => x.Id == SeedVessel.Id, Ct); position.Rank = "SYNTHETIC REPLACEMENT RANK";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }
}
