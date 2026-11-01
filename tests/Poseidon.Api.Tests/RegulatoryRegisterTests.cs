using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// REG-SOURCE-DRAFT-01 / DEV-026 preparation. Real PostgreSQL and API; AUTHORED, NOT RUN.
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<RegisterDto> CreateRegister(HttpClient client, string path = "source-drafts", RegisterContent? content = null, Guid? source = null, long? revision = null)
    {
        using var response = await Send(client, path, new RegisterCommand(0, content ?? new(), source, revision));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterDto>(Ct))!;
    }
    private async Task<RegisterDto> UpdateRegister(HttpClient client, RegisterDto before, string path, RegisterContent? content = null, string? key = null)
    {
        using var response = await Send(client, $"{path}/{before.Current.Id}", new RegisterCommand(before.Version, content ?? before.Current.Content, before.Current.SourceId, before.Current.SourceRevision), key, true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterDto>(Ct))!;
    }
    [Fact]
    public async Task Register_audit_and_receipt_failures_leave_no_draft_revision_or_business_audit()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        foreach (var table in new[] { "core.\"CommandReceipts\"", "audit.\"AuditEvents\"" }) {
            await Sql($"CREATE TRIGGER reject_register_insert BEFORE INSERT ON {table} FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
            var key = Guid.NewGuid().ToString();
            try {
                using var failure = await Send(client, "source-drafts", new RegisterCommand(0, new() { Title = new("Known", "FAILED-FICTIONAL-IMPORT") }), key);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode); Assert.DoesNotContain("Postgres", await failure.Content.ReadAsStringAsync(Ct));
                await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
                Assert.False(await db.RegisterDrafts.AnyAsync(x => x.ContentJson.Contains("FAILED-FICTIONAL-IMPORT"), Ct));
                Assert.False(await db.RegisterRevisions.AnyAsync(x => x.ContentJson.Contains("FAILED-FICTIONAL-IMPORT"), Ct));
                Assert.False(await db.AuditEvents.AnyAsync(x => x.ActorId == actorId.ToString(), Ct));
            } finally { await Sql($"DROP TRIGGER reject_register_insert ON {table};"); }
        }
    }
    [Fact]
    public async Task Register_required_read_and_denial_audit_failure_is_fail_closed()
    {
        await using var host = Host(regulatoryRead: true); using var client = Client(host); await Token(client);
        await Sql("CREATE TRIGGER reject_register_security BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        try {
            using var read = await client.GetAsync("/api/v1/source-drafts", Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
            await using var deniedHost = Host(); using var denied = Client(deniedHost);
            using var denial = await denied.GetAsync("/api/v1/source-drafts", Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, denial.StatusCode);
        } finally { await Sql("DROP TRIGGER reject_register_security ON audit.\"SecurityEvents\";"); }
    }
    [Fact]
    public async Task Register_incomplete_create_history_and_exact_source_lineage_survive_later_source_edits()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        var source = await CreateRegister(client, content: new() { Title = new("Known", "Fictional source A"), Declaration = new("Unknown"), ObservedAt = new("Known", "2001-01-01T00:00:00Z") });
        var requirement = await CreateRegister(client, "requirement-drafts", new() { Instrument = new("Unknown") }, source.Current.Id, source.Current.Revision);
        var changed = await UpdateRegister(client, source, "source-drafts", source.Current.Content with { Title = new("Known", "Fictional source B") });
        Assert.NotEqual(source.Version, changed.Version); Assert.Equal(2, changed.Current.Revision);
        var read = (await client.GetFromJsonAsync<RegisterDto>($"/api/v1/requirement-drafts/{requirement.Current.Id}", Ct))!;
        Assert.Equal(1, read.Current.SourceRevision); Assert.Equal("DRAFT", read.Current.Lifecycle);
        Assert.Equal("Unknown", read.Current.Content.Instrument.State);
        var history = (await client.GetFromJsonAsync<PageDto<RegisterRevisionDto>>($"/api/v1/source-drafts/{source.Current.Id}/history", Ct))!;
        Assert.Equal(new long[] { 2, 1 }, history.Items.Select(x => x.Revision));
        Assert.Equal("Fictional source A", history.Items[1].Content.Title.Value);
        Assert.NotEqual(history.Items[0].Hash, history.Items[1].Hash);
        Assert.True(source.Current.SavedAt > DateTimeOffset.Parse(source.Current.Content.ObservedAt.Value!));
        Assert.All(history.Items, x => Assert.Equal("DeclaredUnverified", x.Provenance));
    }
    [Fact]
    public async Task Register_noop_touches_xmin_replay_is_idempotent_changed_payload_conflicts_and_permission_is_rechecked()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        var before = await CreateRegister(client); var key = Guid.NewGuid().ToString();
        var after = await UpdateRegister(client, before, "source-drafts", key: key);
        Assert.NotEqual(before.Version, after.Version); Assert.Equal(2, after.Current.Revision);
        var body = new RegisterCommand(before.Version, before.Current.Content);
        using var replay = await Send(client, $"source-drafts/{before.Current.Id}", body, key, true);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        using var reuse = await Send(client, $"source-drafts/{before.Current.Id}", body with { Content = new() { Title = new("Unknown") } }, key, true);
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
        await using var deniedHost = Host(regulatoryRead: true); using var denied = Client(deniedHost); await Token(denied);
        using var revoked = await Send(denied, $"source-drafts/{before.Current.Id}", body, key, true);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        await using var db = Db(); Assert.Equal(2, await db.RegisterRevisions.CountAsync(x => x.Id == before.Current.Id, Ct));
        Assert.Equal(2, await db.CommandReceipts.CountAsync(Ct));
    }
    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, false)]
    public async Task Register_denies_generic_directory_write_write_only_permission_and_vessel_only_membership(bool regRead, bool regWrite, bool manage, bool read)
    {
        await using var host = Host(manage: manage, read: read, regulatoryRead: regRead, regulatoryWrite: regWrite, search: true);
        using var client = Client(host); await Token(client);
        using var list = await client.GetAsync("/api/v1/source-drafts", Ct); Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        using var save = await Send(client, "source-drafts", new RegisterCommand(0, new())); Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
        var workspace = (await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace", Ct))!;
        Assert.DoesNotContain(workspace.Destinations, x => x.Key is "sources" or "requirements"); Assert.False(workspace.CanWriteRegulatory);
        var search = (await client.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=sources", Ct))!;
        Assert.DoesNotContain(search.Items, x => x.Href.Contains("sources"));
    }
    [Fact]
    public async Task Register_read_only_csrf_anonymous_unknown_members_and_cross_tenant_lineage_are_denied()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host);
        using var csrf = await Send(client, "source-drafts", new RegisterCommand(0, new())); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        using var anonymous = Client(host, false); using var unauthenticated = await anonymous.GetAsync("/api/v1/source-drafts", Ct); Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        await Token(client);
        var other = RegulatoryRegisterFixtures.SourceId(seed.Tenants[1].Id);
        using var detail = await client.GetAsync($"/api/v1/source-drafts/{other}", Ct); Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        using var history = await client.GetAsync($"/api/v1/source-drafts/{other}/history", Ct); Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        using var link = await Send(client, "requirement-drafts", new RegisterCommand(0, new(), other, 1)); Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);
        using var spoof = await Send(client, "source-drafts", new { expectedVersion = 0, content = new RegisterContent(), tenantId = seed.Tenants[1].Id, role = "RegulatoryAdmin" });
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        await using var readHost = Host(regulatoryRead: true); using var reader = Client(readHost); await Token(reader);
        var rows = (await reader.GetFromJsonAsync<PageDto<RegisterDto>>("/api/v1/source-drafts", Ct))!; Assert.All(rows.Items, x => Assert.False(x.CanEdit));
        using var write = await Send(reader, "source-drafts", new RegisterCommand(0, new())); Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }
    [Fact]
    public async Task Register_source_reference_is_not_fetched_and_search_is_tenant_scoped()
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        var declared = await CreateRegister(client, content: new() { Title = new("Known", "UNIQUE-FICTIONAL"), Locator = new("Known", "http://127.0.0.1:1/never-fetch") });
        var page = (await client.GetFromJsonAsync<PageDto<RegisterDto>>("/api/v1/source-drafts?q=UNIQUE-FICTIONAL", Ct))!;
        Assert.Equal(declared.Current.Id, Assert.Single(page.Items).Current.Id);
        using var invalid = await client.GetAsync("/api/v1/source-drafts?limit=101", Ct); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using var db = Db(); var events = await db.Set<SecurityEvent>().Where(x => x.Action.StartsWith("Register")).ToArrayAsync(Ct);
        Assert.Contains(events, x => x.Action == "RegisterList" && x.Result == "Success");
        Assert.All(events, x => Assert.DoesNotContain("UNIQUE-FICTIONAL", x.Result));
    }
    [Fact]
    public async Task Register_noop_race_rolls_back_business_audit_history_and_receipt()
    {
        var barrier = new DirectorySelectBarrier("RegisterDrafts");
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true, interceptor: barrier); using var client = Client(host); await Token(client);
        var before = await CreateRegister(client); var key = Guid.NewGuid().ToString();
        barrier.Arm(); var pending = Send(client, $"source-drafts/{before.Current.Id}", new RegisterCommand(before.Version, before.Current.Content), key, true);
        HttpResponseMessage? loser = null;
        try {
            await barrier.WaitUntilReadAsync(Ct); var winner = await UpdateRegister(client, before, "source-drafts");
            barrier.Release(); loser = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
            await using var db = Db(); Assert.False(await db.CommandReceipts.AnyAsync(x => x.Key == key, Ct));
            var correlation = Assert.Single(loser.Headers.GetValues("X-Correlation-ID"));
            Assert.False(await db.AuditEvents.AnyAsync(x => x.CorrelationId == correlation, Ct));
            Assert.Equal(2, await db.RegisterRevisions.CountAsync(x => x.Id == before.Current.Id, Ct));
            Assert.Equal(winner.Version, (await db.RegisterDrafts.SingleAsync(x => x.Id == before.Current.Id, Ct)).Version);
        } finally { barrier.Release(); loser ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); loser.Dispose(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Register_concurrent_create_same_key_has_one_effect_and_changed_payload_conflicts(bool changed)
    {
        await using var host = Host(regulatoryRead: true, regulatoryWrite: true); using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString(); var first = new RegisterCommand(0, new() { Title = new("Unknown") });
        var second = changed ? first with { Content = new() { Title = new("Known", "Different declared title") } } : first;
        var replies = await Task.WhenAll(Send(client, "source-drafts", first, key), Send(client, "source-drafts", second, key));
        try {
            Assert.Equal(changed ? 1 : 2, replies.Count(r => r.StatusCode == HttpStatusCode.OK));
            if (changed) Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            await using var db = Db(); var receipt = await db.CommandReceipts.SingleAsync(x => x.Key == key, Ct);
            Assert.Equal(1, await db.RegisterRevisions.CountAsync(x => x.Id == receipt.ResourceId, Ct));
            var audits = await db.AuditEvents.Where(x => x.ActorId == actorId.ToString()).ToArrayAsync(Ct); Assert.Equal(3, audits.Length);
        } finally { foreach (var reply in replies) reply.Dispose(); }
    }
}
