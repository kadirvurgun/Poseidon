using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// VESSEL-PROFILE-DRAFT-01 / DEV-020/T-S2-001, DEV-021/T-S2-002, POL-DEFAULT-01.
// Shares only the existing real PostgreSQL/API/authentication fixture, not production authentication.
public sealed partial class DirectoryWorkspaceTests
{
    private static string ProfilePath(Guid id) => $"vessels/{id}/regulatory-profile";
    private async Task<RegulatoryDraftDto> Profile(HttpClient client, Guid? id = null) =>
        (await client.GetFromJsonAsync<RegulatoryDraftDto>("/api/v1/" + ProfilePath(id ?? SeedVessel.Id), Ct))!;
    private async Task<RegulatoryDraftDto> SaveProfile(HttpClient client, RegulatoryDraftDto before, RegulatoryDraftContent content, string? key = null)
    {
        using var response = await Send(client, ProfilePath(before.Vessel.Id), new SaveRegulatoryDraft(before.Version, content), key, true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegulatoryDraftDto>(Ct))!;
    }

    [Fact]
    public async Task Profile_incomplete_draft_preserves_unknown_zero_observation_and_immutable_versions()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var observed = "2001-01-01T00:00:00Z";
        var content = new RegulatoryDraftContent { Imo = new("Known", "  "), Flag = new("Unknown"), Gt = new("Known", 0),
            SourceObservedAt = new("Known", observed), SourceReference = new("Known", "Synthetic declaration source"), Declaration = new("Unknown") };
        var after = await SaveProfile(client, before, content);
        Assert.Equal(before.TenantId, after.TenantId); Assert.Equal(SeedVessel.Id, after.Vessel.Id);
        Assert.Equal(SeedVessel.Kind.ToString(), after.Vessel.Kind); Assert.True(after.Vessel.CanEdit);
        Assert.NotEqual(before.Version, after.Version); Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal("DeclaredUnverified", after.Provenance); Assert.Equal("Unknown", after.Content.Flag.State);
        Assert.Equal("Missing", after.Content.Imo.State); Assert.Equal(0, after.Content.Gt.Value); Assert.Equal("Known", after.Content.Gt.State);
        Assert.Equal(DateTimeOffset.Parse(observed), DateTimeOffset.Parse(after.Content.SourceObservedAt.Value!));
        Assert.True(after.SavedAt > DateTimeOffset.Parse(observed));
        var history = (await client.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>("/api/v1/" + ProfilePath(SeedVessel.Id) + "/history", Ct))!;
        Assert.Equal(2, history.Items.Length); Assert.Equal(after.Content, history.Items[0].Content);
        Assert.Equal(before.Content, history.Items[1].Content); Assert.Equal("Synthetic", history.Items[1].Provenance);
        await using var db = Db();
        Assert.Equal(seed.VesselProfiles.Single(x => x.VesselId == SeedVessel.Id).GrossTonnage,
            (await db.VesselProfiles.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct)).GrossTonnage);
        var receipt = Assert.Single(await db.CommandReceipts.ToArrayAsync(Ct)); Assert.Equal(SeedVessel.Id, receipt.ResourceId);
        // Correlate actual draft and receipt audit, with no business body in audit metadata.
        var events = await db.AuditEvents.Where(x => x.ActorId == actorId.ToString()).ToArrayAsync(Ct);
        Assert.Equal(3, events.Length); Assert.Single(events.Select(x => x.CorrelationId).Distinct());
        Assert.Contains(events, x => x.EntityType == nameof(RegulatoryDraft));
        Assert.Contains(events, x => x.EntityType == nameof(RegulatoryDraftRevision));
        Assert.Contains(events, x => x.EntityType == nameof(CommandReceipt));
    }

    [Theory]
    [InlineData("Ship")]
    [InlineData("Tug")]
    public async Task New_vessel_has_absent_draft_then_allows_explicit_incomplete_creation(string kind)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        using var create = await Send(client, "vessels", new { code = "PROFILE-NEW", name = "Synthetic new vessel", kind, companyId = SeedVessel.CompanyId });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var vessel = (await create.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var before = await Profile(client, vessel.Id); Assert.Equal(0u, before.Version); Assert.Equal(0, before.Revision); Assert.Null(before.SavedAt);
        Assert.Equal("Missing", before.Provenance); Assert.Equal("Missing", before.Content.Gt.State);
        var after = await SaveProfile(client, before, new());
        Assert.True(after.Version > 0); Assert.Equal(1, after.Revision); Assert.Equal(kind, after.Vessel.Kind);
    }

    [Fact]
    public async Task Profile_noop_save_touches_xmin_and_stale_save_has_no_effect_but_receipt_retry_recovers()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client); var key = Guid.NewGuid().ToString();
        var after = await SaveProfile(client, before, before.Content, key);
        Assert.NotEqual(before.Version, after.Version);
        using var stale = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, before.Content), update: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var replay = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, before.Content), key, true);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.Equal(after, await replay.Content.ReadFromJsonAsync<RegulatoryDraftDto>(Ct));
        await using var db = Db(); Assert.Single(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Equal(2, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
    }

    [Fact]
    public async Task Profile_noop_race_after_business_select_rolls_back_history_audit_and_receipt()
    {
        var barrier = new DirectorySelectBarrier("RegulatoryDrafts");
        await using var host = Host(interceptor: barrier); using var client = Client(host); await Token(client);
        var before = await Profile(client); var losingKey = Guid.NewGuid().ToString();
        barrier.Arm();
        var pending = Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, before.Content), losingKey, true);
        HttpResponseMessage? loser = null;
        try
        {
            await barrier.WaitUntilReadAsync(Ct);
            var winner = await SaveProfile(client, before, before.Content with { Flag = new("Unknown") });
            barrier.Release(); loser = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode); Assert.Contains("version_conflict", await loser.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            Assert.DoesNotContain(await db.CommandReceipts.ToArrayAsync(Ct), x => x.Key == losingKey);
            var events = await db.AuditEvents.Where(x => x.ActorId == actorId.ToString()).ToArrayAsync(Ct);
            Assert.Equal(3, events.Length);
            Assert.DoesNotContain(events, x => x.CorrelationId == Assert.Single(loser.Headers.GetValues("X-Correlation-ID")));
            Assert.Equal(2, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
            Assert.Equal(winner.Version, (await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct)).Version);
        }
        finally { barrier.Release(); loser ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); loser.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_profile_commands_same_key_have_one_effect_and_detect_payload_reuse(bool different)
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client); var key = Guid.NewGuid().ToString();
        var first = new SaveRegulatoryDraft(before.Version, before.Content with { Flag = new("Known", "Declared A") });
        var second = different ? new SaveRegulatoryDraft(before.Version, before.Content with { Flag = new("Known", "Declared B") }) : first;
        var replies = await Task.WhenAll(Send(client, ProfilePath(SeedVessel.Id), first, key, true), Send(client, ProfilePath(SeedVessel.Id), second, key, true));
        try
        {
            if (different)
            {
                Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK);
                var conflict = Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
                Assert.Contains("idempotency_key_reused", await conflict.Content.ReadAsStringAsync(Ct));
            }
            else Assert.All(replies, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
            await using var db = Db(); Assert.Single(await db.CommandReceipts.ToArrayAsync(Ct));
            Assert.Equal(2, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
            Assert.Equal(3, await db.AuditEvents.CountAsync(x => x.ActorId == actorId.ToString(), Ct));
        }
        finally { foreach (var response in replies) response.Dispose(); }
    }

    [Fact]
    public async Task Profile_scope_role_csrf_and_body_spoofing_are_denied_including_history_and_replay()
    {
        await using var host = Host(manage: false, read: false); using var client = Client(host); await Token(client);
        var before = await Profile(client); var key = Guid.NewGuid().ToString(); var body = new SaveRegulatoryDraft(before.Version, new());
        using var saved = await Send(client, ProfilePath(SeedVessel.Id), body, key, true); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        foreach (var other in seed.Vessels.Where(x => x.Id != SeedVessel.Id))
        {
            using var get = await client.GetAsync("/api/v1/" + ProfilePath(other.Id), Ct); Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            using var history = await client.GetAsync("/api/v1/" + ProfilePath(other.Id) + "/history", Ct); Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
            using var write = await Send(client, ProfilePath(other.Id), body, key, true); Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        }
        foreach (var spoof in new object[] {
            new { expectedVersion = before.Version, content = new { flag = new { state = "Unknown" }, vesselId = Guid.NewGuid() } },
            new { expectedVersion = before.Version, content = new { }, tenantId = Guid.NewGuid() },
            new { expectedVersion = before.Version, content = new { }, canEdit = true },
            new { expectedVersion = before.Version, content = new { flag = new { state = "Known", value = "X", verified = true } } }
        })
        {
            using var denied = await Send(client, ProfilePath(SeedVessel.Id), spoof, update: true); Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [new(SeedVessel.Id, false)] }];
        using var revoked = await Send(client, ProfilePath(SeedVessel.Id), body, key, true); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        Assert.False((await Profile(client)).Vessel.CanEdit);
        settings.Memberships = [settings.Memberships[0] with { VesselGrants = [] }];
        using var noScope = await client.GetAsync("/api/v1/" + ProfilePath(SeedVessel.Id) + "/history", Ct); Assert.Equal(HttpStatusCode.Forbidden, noScope.StatusCode);
        await using var db = Db(); Assert.Single(await db.CommandReceipts.ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Profile_anonymous_viewer_and_missing_csrf_commands_leave_no_receipts()
    {
        await using var host = Host(); using var anonymous = Client(host, false); using var client = Client(host);
        using var get = await anonymous.GetAsync("/api/v1/" + ProfilePath(SeedVessel.Id), Ct); Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var write = await Send(anonymous, ProfilePath(SeedVessel.Id), new { expectedVersion = 0, content = new { } }, update: true); Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);
        var before = await Profile(client);
        using var csrf = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, new()), update: true); Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        await using var viewerHost = Host(edit: false); using var viewer = Client(viewerHost); await Token(viewer);
        using var denied = await Send(viewer, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, new()), update: true); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var db = Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), x => x.Action == "ProfileSave" && x.Result == "csrf_invalid");
    }

    [Fact]
    public async Task Profile_invalid_values_and_keys_are_safe_and_do_not_create_history()
    {
        await using var host = Host(); using var client = Client(host); await Token(client); var before = await Profile(client);
        foreach (var bad in new[] {
            new RegulatoryDraftContent { Flag = new("Verified", "A") }, new() { Gt = new("Known", -1) },
            new() { Imo = new("Known", "bad") }, new() { SourceObservedAt = new("Known", "2020-01-01") },
            new() { Declaration = new("Known", new string('x', 501)) }, new() { Gt = null! }
        })
        {
            using var denied = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, bad), update: true);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode); Assert.DoesNotContain("stack", await denied.Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
        }
        using var key = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, new()), "", true); Assert.Equal(HttpStatusCode.BadRequest, key.StatusCode);
        using var noVersion = await Send(client, ProfilePath(SeedVessel.Id), new { content = new { } }, update: true); Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        await using var db = Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Single(await db.RegulatoryDraftRevisions.Where(x => x.VesselId == SeedVessel.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Profile_first_save_race_and_restart_retry_preserve_one_durable_effect()
    {
        Guid vesselId; var key = Guid.NewGuid().ToString(); var body = new SaveRegulatoryDraft(0, new() { Flag = new("Unknown") });
        await using (var host = Host())
        {
            using var client = Client(host); await Token(client);
            using var create = await Send(client, "vessels", new { code = "DRAFT-RACE", name = "Synthetic", kind = "Tug", companyId = SeedVessel.CompanyId });
            vesselId = (await create.Content.ReadFromJsonAsync<VesselDto>(Ct))!.Id;
            var replies = await Task.WhenAll(Send(client, ProfilePath(vesselId), body, key, true), Send(client, ProfilePath(vesselId), body, key, true));
            try { Assert.All(replies, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode)); }
            finally { foreach (var response in replies) response.Dispose(); }
        }
        await using (var host = Host())
        {
            using var client = Client(host); await Token(client);
            using var replay = await Send(client, ProfilePath(vesselId), body, key, true);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal(1, (await replay.Content.ReadFromJsonAsync<RegulatoryDraftDto>(Ct))!.Revision);
        }
        await using var db = Db();
        Assert.Single(await db.RegulatoryDraftRevisions.Where(x => x.VesselId == vesselId).ToArrayAsync(Ct));
        Assert.Single(await db.CommandReceipts.Where(x => x.Key == key).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Profile_compound_foreign_key_rejects_cross_tenant_draft_and_rolls_back_generated_audit()
    {
        await using (var db = Db())
        {
            db.RegulatoryDrafts.Add(new() { TenantId = SeedVessel.TenantId, VesselId = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId).Id,
                Revision = 1, SavedAt = DateTimeOffset.UtcNow, Provenance = "Unknown", ContentJson = RegulatoryDraftStorage.Serialize(new()) });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }
        await using var verify = Db();
        Assert.Equal(seed.Vessels.Count(x => x.TenantId == SeedVessel.TenantId), await verify.RegulatoryDrafts.CountAsync(Ct));
        Assert.Empty(await verify.AuditEvents.Where(x => x.ActorId == "synthetic-inspector").ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Profile_receipt_failure_rolls_back_draft_version_history_and_business_audit()
    {
        await using var host = Host(); using var client = Client(host); await Token(client); var before = await Profile(client);
        await Sql("CREATE TRIGGER profile_test_reject_receipt BEFORE INSERT ON core.\"CommandReceipts\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        using var denied = await Send(client, ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(before.Version, new()), update: true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
        await using var db = Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
        Assert.Equal(before.Version, (await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == SeedVessel.Id, Ct)).Version);
        Assert.Single(await db.RegulatoryDraftRevisions.Where(x => x.VesselId == SeedVessel.Id).ToArrayAsync(Ct));
        Assert.Empty(await db.AuditEvents.Where(x => x.ActorId == actorId.ToString()).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Profile_history_is_append_only_and_seed_reimport_preserves_operator_values_and_freshness()
    {
        await using var host = Host(); using var client = Client(host); await Token(client); var before = await Profile(client);
        Assert.Equal("Synthetic", before.Provenance); Assert.Equal("Missing", before.Content.Gt.State); Assert.Equal("Unknown", before.Content.SourceObservedAt.State);
        var after = await SaveProfile(client, before, new() { Flag = new("Known", "Synthetic operator declaration"), SourceObservedAt = new("Unknown") });
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true))
            await SeedImporter.ImportAsync(admin, SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json")), Ct);
        Assert.Equal(after, await Profile(client));
        await using var db = Db(); var revision = await db.RegulatoryDraftRevisions.FirstAsync(Ct);
        revision.Provenance = "Verified";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct)); db.ChangeTracker.Clear();
        foreach (var sql in new[] { "DELETE FROM core.\"RegulatoryDraftRevisions\"", "UPDATE core.\"RegulatoryDraftRevisions\" SET \"Provenance\"='Verified'", "TRUNCATE core.\"RegulatoryDraftRevisions\"" })
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));
        Assert.Equal(2, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
        using var page = await client.GetAsync("/api/v1/" + ProfilePath(SeedVessel.Id) + "/history?limit=1", Ct);
        var result = (await page.Content.ReadFromJsonAsync<PageDto<RegulatoryRevisionDto>>(Ct))!; Assert.Single(result.Items); Assert.True(result.HasMore);
    }
}
