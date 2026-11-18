using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S2-QA-001 / DEV-020/T-S2-001 / DEV-024/T-S2-005.
// Actual PostgreSQL statement interleaving and HTTP commands; execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    private string ProfileHistoryPath(Guid vessel, int offset = 0, int limit = 1) =>
        $"/api/v1/{ProfilePath(vessel)}/history?offset={offset}&limit={limit}";

    private sealed class ProfileHistorySnapshotBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public Task WaitForStatement(CancellationToken ct) => read.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        public void Release() => released.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            // Covers both the vulnerable separate Any query and the fixed single
            // snapshot query. The database has executed, but EF has not read it.
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) &&
                command.CommandText.Contains("core.\"RegulatoryDraftRevisions\"", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1) {
                read.TrySetResult();
                await released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    [Fact]
    public async Task Profile_history_does_not_read_a_concurrently_appended_first_file_bound_revision_after_an_unrestricted_check()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s2-history-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var writerHost = FileHost(root, scanner.Port); using var writer = Client(writerHost);
            FilePermissions(writerHost.Services); await Token(writer);
            var before = await Profile(writer);
            var stage = await StagePng(writer); using var uploaded = await PutPng(writer, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var barrier = new ProfileHistorySnapshotBarrier();
            await using var readerHost = Host(interceptor: barrier); using var reader = Client(readerHost);
            barrier.Arm();
            var pending = reader.GetAsync(ProfileHistoryPath(SeedVessel.Id), Ct);
            HttpResponseMessage? result = null;
            try {
                await barrier.WaitForStatement(Ct);
                var appended = await SaveProfile(writer, before, before.Content with {
                    ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!),
                    SourceReference = new("Known", "FICTIONAL-RESTRICTED-CONCURRENT-HISTORY"),
                    SourceObservedAt = new("Known", "2001-01-01T00:00:00Z")
                });
                long writes; long receipts;
                await using (var db = Db()) {
                    writes = await db.AuditEvents.LongCountAsync(Ct);
                    receipts = await db.CommandReceipts.LongCountAsync(Ct);
                }
                barrier.Release(); result = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
                Assert.True(result.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden);
                var json = await result.Content.ReadAsStringAsync(Ct);
                Assert.DoesNotContain("FICTIONAL-RESTRICTED-CONCURRENT-HISTORY", json);
                Assert.DoesNotContain(file.Id.ToString("D"), json);
                Assert.DoesNotContain(file.Checksum!, json);
                Assert.DoesNotContain("2001-01-01", json);
                if (result.StatusCode == HttpStatusCode.OK) {
                    // A response started before the append may serve its exact
                    // earlier statement snapshot, never a mixture of snapshots.
                    var page = (await result.Content.ReadFromJsonAsync<PageDto<RegulatoryRevisionDto>>(Ct))!;
                    Assert.Equal(before.Revision, Assert.Single(page.Items).Revision);
                    Assert.Equal(before.Content, page.Items[0].Content); Assert.False(page.HasMore);
                }
                using var after = await reader.GetAsync(ProfileHistoryPath(SeedVessel.Id), Ct);
                Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
                var authorized = (await writer.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>(ProfileHistoryPath(SeedVessel.Id, limit: 25), Ct))!;
                Assert.Equal(appended.Content, authorized.Items.Single(r => r.Revision == appended.Revision).Content);
                Assert.Equal(before.Content, authorized.Items.Single(r => r.Revision == before.Revision).Content);
                await using var verify = Db();
                Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
                Assert.Equal(receipts, await verify.CommandReceipts.LongCountAsync(Ct));
                Assert.Equal(2, await verify.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
            }
            finally {
                barrier.Release(); result ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); result.Dispose();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Profile_history_denies_the_whole_history_for_a_restricted_revision_outside_the_requested_page_and_preserves_source_scope()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s2-history-pages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var writerHost = FileHost(root, scanner.Port); using var writer = Client(writerHost);
            FilePermissions(writerHost.Services); await Token(writer);
            var original = await Profile(writer);
            var stage = await StagePng(writer); using var uploaded = await PutPng(writer, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var restricted = await SaveProfile(writer, original, original.Content with {
                ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!),
                SourceReference = new("Known", "FICTIONAL-RESTRICTED-OLDER-HISTORY")
            });
            var current = await SaveProfile(writer, restricted, restricted.Content with {
                ConstructionRecord = null, SourceReference = new("Known", "FICTIONAL-CURRENT-UNBOUND")
            });
            await using var readerHost = Host(); using var reader = Client(readerHost);
            Assert.Equal(current, await Profile(reader));
            foreach (var offset in new[] { 0, 1, 2, 100000 }) {
                using var denied = await reader.GetAsync(ProfileHistoryPath(SeedVessel.Id, offset), Ct);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                var body = await denied.Content.ReadAsStringAsync(Ct);
                Assert.DoesNotContain("items", body); Assert.DoesNotContain("hasMore", body);
                Assert.DoesNotContain(file.Id.ToString("D"), body); Assert.DoesNotContain("FICTIONAL-", body);
            }
            // A restricted revision in this source must not block another vessel's
            // unrelated history, and forged selectors never grant cross-scope access.
            var other = seed.Vessels.First(x => x.TenantId == SeedVessel.TenantId && x.Id != SeedVessel.Id);
            var otherBefore = await Profile(reader, other.Id);
            var visibleOther = (await reader.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>(ProfileHistoryPath(other.Id), Ct))!;
            Assert.Equal(otherBefore.Content, Assert.Single(visibleOther.Items).Content);
            await using var scopedHost = Host(manage: false, read: false); using var scoped = Client(scopedHost);
            scoped.DefaultRequestHeaders.Add("X-Tenant-Id", seed.Tenants[1].Id.ToString());
            foreach (var hidden in seed.Vessels.Where(x => x.Id != SeedVessel.Id)) {
                using var denied = await scoped.GetAsync(ProfileHistoryPath(hidden.Id), Ct);
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.DoesNotContain("FICTIONAL-", await denied.Content.ReadAsStringAsync(Ct));
            }
            var full = (await writer.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>(ProfileHistoryPath(SeedVessel.Id, limit: 25), Ct))!;
            Assert.Equal(new[] { current.Revision, restricted.Revision, original.Revision }, full.Items.Select(r => r.Revision));
            Assert.Equal(restricted.Content, full.Items.Single(r => r.Revision == restricted.Revision).Content);
            Assert.Equal(original.Content, full.Items.Single(r => r.Revision == original.Revision).Content);
            await using var verify = Db();
            var immutable = await verify.RegulatoryDraftRevisions.SingleAsync(x => x.VesselId == SeedVessel.Id && x.Revision == restricted.Revision, Ct);
            immutable.ContentJson = "{}";
            await Assert.ThrowsAsync<InvalidOperationException>(() => verify.SaveChangesAsync(Ct));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
