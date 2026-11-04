using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// Authored real PostgreSQL interleavings. No sleeps choose a race window: the
// database statement or audit insert remains explicitly blocked until release.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class ComplianceRowsBarrier(string table = "RegulatoryDrafts") : DbCommandInterceptor
    {
        private readonly TaskCompletionSource read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public Task Wait(CancellationToken ct) => read.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        public void Release() => released.TrySetResult();
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("core.\"" + table + "\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                read.TrySetResult(); await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData("onboarding", "directory")]
    [InlineData("passport", "file")]
    [InlineData("applicability-review", "role")]
    [InlineData("control-plan", "scope")]
    [InlineData("passport", "tenant")]
    [InlineData("onboarding", "actor")]
    [InlineData("passport", "unchanged")]
    public async Task Compliance_response_withdraws_a_live_scope_changed_after_its_database_statement(string view, string change) {
        var barrier = new ComplianceRowsBarrier();
        await using var host = Host(interceptor: barrier); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var original = settings.Memberships[0];
        long writes; long receipts;
        await using (var db = Db()) { writes = await db.AuditEvents.LongCountAsync(Ct); receipts = await db.CommandReceipts.LongCountAsync(Ct); }
        barrier.Arm(); var pending = client.GetAsync(CompliancePath(view), Ct); HttpResponseMessage? response = null;
        try {
            await barrier.Wait(Ct);
            settings.Memberships[0] = change switch {
                "directory" => original with { Permissions = original.Permissions.Where(p => p != "directory.read").ToArray() },
                "file" => original with { Permissions = original.Permissions.Where(p => p != "file.read").ToArray() },
                "role" => original with { RoleReferences = [Guid.NewGuid().ToString("D")] },
                "scope" => original with { TenantWideRead = false, TenantWideManage = false, VesselGrants = [] },
                "tenant" => original with { TenantId = seed.Tenants[1].Id },
                "actor" => original with { ActorId = Guid.NewGuid() },
                _ => original with { Permissions = original.Permissions.Reverse().ToArray(), VesselGrants = original.VesselGrants!.Reverse().ToArray() }
            };
            barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(change == "unchanged" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
            if (change != "unchanged") {
                var body = await response.Content.ReadAsStringAsync(Ct);
                foreach (var hidden in new[] { "profileRevision", "fields", "inputs", "controls", "requirements", SeedVessel.Id.ToString() }) Assert.DoesNotContain(hidden, body);
            }
            await using var verify = Db(); Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
            Assert.Equal(receipts, await verify.CommandReceipts.LongCountAsync(Ct));
        }
        finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
    }

    [Fact]
    public async Task Onboarding_response_rechecks_a_new_private_profile_binding_after_its_old_snapshot() {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s5-response-binding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var writerHost = FileHost(root, scanner.Port); using var writer = Client(writerHost); FilePermissions(writerHost.Services); await Token(writer);
            var before = await Profile(writer); var stage = await StagePng(writer); using var uploaded = await PutPng(writer, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var barrier = new ComplianceRowsBarrier(); await using var readerHost = Host(interceptor: barrier); using var reader = Client(readerHost);
            barrier.Arm(); var pending = reader.GetAsync(CompliancePath("onboarding"), Ct); HttpResponseMessage? response = null;
            try {
                await barrier.Wait(Ct);
                await SaveProfile(writer, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!) });
                barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("fields", body); Assert.DoesNotContain("profileRevision", body);
                Assert.DoesNotContain(file.Id.ToString(), body);
            }
            finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private async Task WaitForComplianceAuditLock(long key) {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString()); await connection.OpenAsync(Ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(3));
        while (true) {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND NOT granted
                  AND database=(SELECT oid FROM pg_database WHERE datname=current_database()) AND classid=0 AND objid::bigint=@key)
                """, connection);
            command.Parameters.AddWithValue("key", key);
            if ((bool)(await command.ExecuteScalarAsync(deadline.Token))!) return;
            // Observe the explicit PostgreSQL wait condition, not a timing window.
            await Task.Delay(10, deadline.Token);
        }
    }
    private Task InstallComplianceAuditPause(long key) => Sql($$"""
        CREATE FUNCTION audit.s5_pause_compliance_read() RETURNS trigger LANGUAGE plpgsql AS $body$
        BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
        CREATE TRIGGER s5_pause_compliance_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
          WHEN (NEW."Action"='ComplianceWorkspaceRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s5_pause_compliance_read();
        """);
    private Task RemoveComplianceAuditPause() => Sql("""DROP TRIGGER s5_pause_compliance_read ON audit."SecurityEvents"; DROP FUNCTION audit.s5_pause_compliance_read();""");

    [Theory]
    [InlineData("onboarding")]
    [InlineData("passport")]
    public async Task Compliance_response_rechecks_permissions_after_the_success_audit_await(string view) {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var key = (long)Random.Shared.Next(1, int.MaxValue);
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", blocker)) { acquire.Parameters.AddWithValue("key", key); await acquire.ExecuteNonQueryAsync(Ct); }
        // Controlled DDL in this owned test database only; ordinary API business
        // writes still use the audited SaveChanges path and are never bypassed.
        await InstallComplianceAuditPause(key);
        var pending = client.GetAsync(CompliancePath(view), Ct); HttpResponseMessage? response = null;
        try {
            await WaitForComplianceAuditLock(key);
            var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
            settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "directory.read").ToArray() };
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("fields", body); Assert.DoesNotContain("inputs", body);
            await using var db = Db(); var events = await db.Set<SecurityEvent>().Where(e => e.Action == "ComplianceWorkspaceRead").ToArrayAsync(Ct);
            Assert.Contains(events, e => e.Result == "Success"); Assert.Contains(events, e => e.Result == "permission_denied" && e.ResourceId == null);
        }
        finally {
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose();
            await RemoveComplianceAuditPause();
        }
    }

    [Fact]
    public async Task Onboarding_rechecks_a_private_binding_committed_while_success_audit_is_waiting() {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s5-audit-binding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var writerHost = FileHost(root, scanner.Port); using var writer = Client(writerHost); FilePermissions(writerHost.Services); await Token(writer);
            var before = await Profile(writer); var stage = await StagePng(writer); using var uploaded = await PutPng(writer, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            await using var readerHost = Host(); using var reader = Client(readerHost);
            var key = (long)Random.Shared.Next(1, int.MaxValue);
            await using var blocker = new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
            await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", blocker)) { acquire.Parameters.AddWithValue("key", key); await acquire.ExecuteNonQueryAsync(Ct); }
            await InstallComplianceAuditPause(key);
            var pending = reader.GetAsync(CompliancePath("onboarding"), Ct); HttpResponseMessage? response = null;
            try {
                await WaitForComplianceAuditLock(key);
                await SaveProfile(writer, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!) });
                await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
                response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync(Ct); Assert.DoesNotContain("fields", body); Assert.DoesNotContain("profileRevision", body);
                Assert.DoesNotContain(file.Id.ToString(), body);
            }
            finally {
                await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
                response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); await RemoveComplianceAuditPause();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
