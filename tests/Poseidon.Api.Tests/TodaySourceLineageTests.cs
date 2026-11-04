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

// DEV-020/T-S2-001, DEV-023/T-S2-004, DEV-024/T-S2-005.
// Actual host + owned PostgreSQL and ordinary audited commands. Execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Vessel_master_context_is_versioned_and_why_names_the_exact_current_profile_revision()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var declared = before.Content with {
            Imo = new("Known", "1234567"), Flag = new("Known", "FICTIONAL-FLAG"),
            ClassSociety = new("Known", "FICTIONAL-CLASS"), ClassNumber = new("Known", "FICTIONAL-001"),
            ShipType = new("Known", "FICTIONAL-SHIP-TYPE"), Gt = new("Known", "1000.00000000000000000001"),
            BuildDate = new("Known", "2000-01-02"), TradingArea = new("Unknown"),
            CargoContext = new("Known", "FICTIONAL-CARGO-CONTEXT"), SourceReference = new("Known", "FICTIONAL-private-declaration"),
            SourceObservedAt = new("Known", "2001-01-01T00:00:00Z"), Declaration = new("Unknown")
        };
        var saved = await SaveProfile(client, before, declared);
        Assert.Equal("Unknown", saved.Content.TradingArea.State);
        Assert.Null(saved.Content.TradingArea.Value);
        Assert.Equal(declared.Gt.Value, saved.Content.Gt.Value);
        var first = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
        Assert.Equal(new TodayWhySourceDto("RegulatoryDraft", SeedVessel.Id, saved.Revision), first.Source);
        Assert.Equal(saved.SavedAt, first.SavedAt);
        Assert.Equal(DateTimeOffset.Parse(declared.SourceObservedAt.Value!), DateTimeOffset.Parse(first.SourceObservedAt!));
        Assert.Equal("Unverified", first.Freshness);
        Assert.DoesNotContain(declared.SourceReference.Value!, await client.GetStringAsync(WhyPath, Ct));

        var changed = await SaveProfile(client, saved, saved.Content with { TradingArea = new("Known", "FICTIONAL-TRADE-AREA") });
        var current = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
        Assert.Equal(new TodayWhySourceDto("RegulatoryDraft", SeedVessel.Id, changed.Revision), current.Source);
        Assert.Equal(saved.Revision + 1, current.Source!.Revision);
        var history = (await client.GetFromJsonAsync<PageDto<RegulatoryRevisionDto>>($"/api/v1/{ProfilePath(SeedVessel.Id)}/history", Ct))!;
        Assert.Equal(saved.Content, history.Items.Single(x => x.Revision == saved.Revision).Content);
        Assert.Equal(before.Content, history.Items.Single(x => x.Revision == before.Revision).Content);

        // Add-missing synthetic import must not replace this operator version or
        // manufacture freshness/verification for declared vessel context.
        await using (var importer = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true))
            await SeedImporter.ImportAsync(importer, seed, Ct);
        Assert.Equal(changed, await Profile(client));
        await using var db = Db();
        Assert.Equal(2, await db.CommandReceipts.CountAsync(Ct));
        Assert.Equal(3, await db.RegulatoryDraftRevisions.CountAsync(x => x.VesselId == SeedVessel.Id, Ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Today_and_why_apply_the_owning_profile_file_gate_before_any_source_detail(bool populated)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-s2-why-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var host = FileHost(root, scanner.Port); using var client = Client(host);
            FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client); using var accepted = await PutPng(client, stage.Id);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var file = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var before = await Profile(client);
            var content = before.Content with {
                ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!),
                Imo = populated ? new("Known", "1234567") : new("Missing"),
                SourceReference = populated ? new("Known", "FICTIONAL-withheld-source") : new("Missing"),
                SourceObservedAt = populated ? new("Known", "2001-01-01T00:00:00Z") : new("Unknown")
            };
            var saved = await SaveProfile(client, before, content);
            var allowed = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
            Assert.Equal(saved.Revision, allowed.Source!.Revision);
            long writes; long receipts;
            await using (var db = Db()) {
                writes = await db.AuditEvents.LongCountAsync(Ct);
                receipts = await db.CommandReceipts.LongCountAsync(Ct);
            }
            var settings = host.Services.GetRequiredService<FederationSettings>();
            var member = settings.Memberships[0];
            settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != "file.read").ToArray() };
            using var owningDenied = await client.GetAsync($"/api/v1/{ProfilePath(SeedVessel.Id)}", Ct);
            Assert.Equal(HttpStatusCode.Forbidden, owningDenied.StatusCode);
            using var whyDenied = await client.GetAsync(WhyPath, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, whyDenied.StatusCode);
            var deniedJson = await whyDenied.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(file.Id.ToString("D"), deniedJson);
            Assert.DoesNotContain("sourceObservedAt", deniedJson);
            Assert.DoesNotContain("revision", deniedJson);
            foreach (var offset in new[] { 0, 1 }) {
                var feed = (await client.GetFromJsonAsync<TodayDto>(TodayPath + $"&offset={offset}&limit=1", Ct))!;
                Assert.Equal("Unavailable", feed.Coverage.Single(x => x.Source == "RegulatoryDraft").Status);
                Assert.Empty(feed.Cards.Items); Assert.False(feed.Cards.HasMore);
            }
            var feedJson = await client.GetStringAsync(TodayPath, Ct);
            Assert.DoesNotContain(file.Id.ToString("D"), feedJson);
            Assert.DoesNotContain("FICTIONAL-withheld-source", feedJson);
            Assert.DoesNotContain("2001-01-01", feedJson);
            settings.Memberships[0] = member;
            var restored = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
            Assert.Equal(allowed.Source, restored.Source);
            await using var verify = Db();
            Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct));
            Assert.Equal(receipts, await verify.CommandReceipts.LongCountAsync(Ct));
            Assert.Contains(await verify.Set<SecurityEvent>().ToArrayAsync(Ct), e =>
                e.Action == "TodayWhyRead" && e.Result == "Denied" && e.ResourceId == null && e.ActorId == actorId.ToString());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Maintenance_why_retains_the_actual_selected_observation_time_after_revocation()
    {
        using var host = ObservationHost(); using var client = Client(host); await Token(client);
        var reading = Reading();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, ObservationPath, reading)).StatusCode);
        var command = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, SeedVessel.Id, null,
            new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", TimeZone: "UTC",
                ObservationId: reading.Id, ObservedAt: DateTimeOffset.Parse("2000-01-01T00:00:00Z")), DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, ObservationPath + $"/{reading.Id}/revoke", new { })).StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(command.Id), Ct))!;
        Assert.Equal(PostgresInstant(reading.ObservedAt!.Value), DateTimeOffset.Parse(why.SourceObservedAt!));
        Assert.Equal("ObservationRevoked", why.Maintenance!.ObservationState);
        Assert.Equal("ObservationRevoked", why.Maintenance.Calculation.Reason);
        Assert.Equal(new TodayWhySourceDto("MaintenancePlan", command.Id, why.Maintenance.PlanRevision), why.Source);
        Assert.Equal("Unverified", why.Freshness);
    }

    [Fact]
    public async Task Maintenance_why_does_not_substitute_a_client_inline_time_for_a_selected_observation_with_unknown_time()
    {
        using var host = ObservationHost(); using var client = Client(host); await Token(client);
        var reading = Reading() with { ObservedAt = null };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, ObservationPath, reading)).StatusCode);
        var command = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, SeedVessel.Id, null,
            new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", TimeZone: "UTC",
                ObservationId: reading.Id, ObservedAt: DateTimeOffset.Parse("2000-01-01T00:00:00Z")), DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, command)).StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(command.Id), Ct))!;
        Assert.Null(why.SourceObservedAt);
        Assert.Equal("ObservationMissing", why.Maintenance!.ObservationState);
        Assert.Equal("ObservationMissing", why.Maintenance.Calculation.Reason);
        Assert.Equal("Unverified", why.Freshness);
        Assert.DoesNotContain("2000-01-01", await client.GetStringAsync(MaintenanceWhyPath(command.Id), Ct));
    }
}
