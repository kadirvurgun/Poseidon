using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private string TugTodayPath => $"/api/v1/poseidon-today?vesselId={Tug.Id:D}";
    private string TugWhyPath(Guid job) => $"/api/v1/why-engine?vesselId={Tug.Id:D}&cardKey=tug-job:{job:D}";

    [Fact]
    public async Task Today_tug_available_empty_source_is_missing_and_never_manufactures_a_job()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var create = await Send(client, "vessels", new { companyId = Tug.CompanyId, code = "TT-EMPTY", name = "FICTIONAL empty tug", kind = "Tug" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode); var vessel = (await create.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var page = (await client.GetFromJsonAsync<TodayDto>($"/api/v1/poseidon-today?vesselId={vessel.Id}", Ct))!;
        Assert.Equal("Missing", page.Coverage.Single(c => c.Source == "TugPlanning").Status);
        Assert.DoesNotContain(page.Cards.Items, c => c.Source == "TugJob");
    }

    [Fact]
    public async Task Today_tug_current_owner_is_a_scoped_role_reference_and_is_withdrawn_after_eligibility_revocation()
    {
        using var host = Host(tugRead: true, tugWrite: true, tugOwner: true); using var client = Client(host); await Token(client);
        var owner = CurrentCalendar().OwnerRoleId!.Value;
        var command = Draft() with { Inputs = Draft().Inputs with { RequestOwner = owner } };
        var created = await Send(client, TugUrl, command); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var before = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(command.ClientId), Ct))!;
        Assert.Equal(owner.ToString("D"), before.Owner); Assert.Equal(owner, before.Tug!.CurrentOwnerRoleId);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != "tug.owner.eligible").ToArray() }).ToArray();
        var after = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(command.ClientId), Ct))!;
        Assert.Null(after.Owner); Assert.Null(after.Tug!.CurrentOwnerRoleId);
        var page = (await client.GetFromJsonAsync<TodayDto>(TugTodayPath, Ct))!;
        Assert.Null(page.Cards.Items.Single(c => c.Key == $"tug-job:{command.ClientId:D}").Owner);
        await using var db = Db(); Assert.Single(await db.TugJobRevisions.Where(r => r.JobId == command.ClientId).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Today_tug_mixed_feed_pages_every_visible_job_once_in_priority_and_ordinal_key_order()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var created = await Send(client, "vessels", new { companyId = Tug.CompanyId, code = "TT-PAGES", name = "FICTIONAL paged tug", kind = "Tug" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); var vessel = (await created.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var ids = new List<Guid>();
        for (var i = 0; i < 26; i++) {
            var command = Draft(); ids.Add(command.ClientId);
            Assert.Equal(HttpStatusCode.OK, (await Send(client, $"tug-jobs?vessel={vessel.Id}", command)).StatusCode);
        }
        var url = $"/api/v1/poseidon-today?vesselId={vessel.Id}&limit=25";
        var first = (await client.GetFromJsonAsync<TodayDto>(url + "&offset=0", Ct))!;
        var second = (await client.GetFromJsonAsync<TodayDto>(url + "&offset=25", Ct))!;
        Assert.Equal(25, first.Cards.Items.Length); Assert.True(first.Cards.HasMore);
        Assert.Equal(2, second.Cards.Items.Length); Assert.False(second.Cards.HasMore);
        var combined = first.Cards.Items.Concat(second.Cards.Items).ToArray();
        var expected = ids.Select(id => $"tug-job:{id:D}").Order(StringComparer.Ordinal).Append("regulatory-draft").ToArray();
        Assert.Equal(expected, combined.Select(c => c.Key).ToArray());
        Assert.Equal(expected.Length, combined.Select(c => c.Key).Distinct().Count());
        var repeated = (await client.GetFromJsonAsync<TodayDto>(url + "&offset=0", Ct))!;
        Assert.Equal(first.Cards.Items.Select(c => c.Key), repeated.Cards.Items.Select(c => c.Key));
    }

    [Fact]
    public async Task Today_tug_reads_actual_job_and_missing_plan_without_writing_business_state()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client);
        long before;
        await using (var db = Db()) before = await db.CommandReceipts.LongCountAsync(Ct);
        var page = (await client.GetFromJsonAsync<TodayDto>(TugTodayPath, Ct))!;
        Assert.Equal("Available", page.Coverage.Single(c => c.Source == "TugPlanning").Status);
        var card = Assert.Single(page.Cards.Items, c => c.Key == $"tug-job:{job.Id:D}");
        Assert.Equal("TugPlanMissing", card.Reason); Assert.Equal("Review", card.Priority); Assert.Null(card.Owner);
        Assert.Equal($"/app/tug-job-detail/{job.Id:D}?vessel={Tug.Id:D}", card.Href);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        Assert.NotNull(why.Tug); Assert.Null(why.Tug.PlanRevision); Assert.Null(why.Tug.Evidence);
        Assert.Null(why.Inputs); Assert.Null(why.Maintenance); Assert.Empty(why.Tug.AssignedTugs);
        Assert.Equal(job.Revision, why.Tug.RequestRevision); Assert.Equal("TW-TUG-01", why.Rule);
        Assert.Equal(new TodayWhySourceDto("TugJob", job.Id, job.Revision), why.Source);
        Assert.Equal("Unverified", why.Freshness); Assert.Null(why.SourceObservedAt);
        Assert.Contains("MasterDecisionRequired", why.Blockers);
        var json = await client.GetStringAsync(TugWhyPath(job.Id), Ct);
        Assert.DoesNotContain(job.Inputs.AssistedVessel!, json); Assert.DoesNotContain("jobNumber", json);
        await using var verify = Db();
        Assert.Equal(before, await verify.CommandReceipts.LongCountAsync(Ct));
        Assert.Empty(await verify.TugDraftPlanRevisions.Where(r => r.JobId == job.Id).ToArrayAsync(Ct));
        Assert.Contains(await verify.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "TodayWhyRead" && e.Result == "Success" && e.ResourceId == Tug.Id);
    }

    [Theory]
    [InlineData("tug.read")]
    [InlineData("mode.tug")]
    public async Task Today_tug_rechecks_current_module_and_mode_before_counts_and_why(string permission)
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(TugWhyPath(job.Id), Ct)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != permission).ToArray() }).ToArray();
        var feed = await client.GetAsync(TugTodayPath, Ct);
        if (permission == "mode.tug") {
            Assert.Equal(HttpStatusCode.NotFound, feed.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(TugWhyPath(job.Id), Ct)).StatusCode);
        } else {
            Assert.Equal(HttpStatusCode.OK, feed.StatusCode);
            var page = (await feed.Content.ReadFromJsonAsync<TodayDto>(Ct))!;
            Assert.Equal("Unavailable", page.Coverage.Single(c => c.Source == "TugPlanning").Status);
            Assert.DoesNotContain(page.Cards.Items, c => c.Source == "TugJob");
            var denied = await client.GetAsync(TugWhyPath(job.Id), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain(job.Id.ToString(), await denied.Content.ReadAsStringAsync(Ct));
        }
    }

    [Fact]
    public async Task Today_tug_uses_current_request_and_exact_plan_lineage_after_request_edit()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client);
        var plan = await ChangePlan(client, job, null, new("Window", DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddHours(3)));
        var changed = await Send(client, $"tug-jobs/{job.Id}?vessel={Tug.Id}",
            new TugSaveCommand(job.Id, job.Version, job.Inputs with { Notes = "SYNTHETIC changed request" }), update: true);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        Assert.Equal("TugRequestChanged", why.Reason); Assert.NotNull(why.Tug);
        Assert.Equal(2, why.Tug.RequestRevision); Assert.Equal(plan.Revision, why.Tug.PlanRevision);
        Assert.Equal(new TodayWhySourceDto("TugJob", job.Id, why.Tug.RequestRevision), why.Source);
        Assert.Equal(1, why.Tug.PlannedRequestRevision); Assert.Equal("ReviewRequired", why.Tug.Evidence!.Readiness);
        Assert.DoesNotContain("SYNTHETIC changed request", await client.GetStringAsync(TugWhyPath(job.Id), Ct));
        await using var db = Db(); Assert.Single(await db.TugDraftPlanRevisions.Where(r => r.JobId == job.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Today_tug_hides_the_entire_job_when_a_current_plan_links_an_inaccessible_tug()
    {
        var other = Guid.NewGuid();
        await using (var db = Db()) {
            db.Vessels.Add(new() { TenantId = Tug.TenantId, Id = other, CompanyId = Tug.CompanyId,
                Code = "TT-HIDDEN", Name = "FICTIONAL other tug", Kind = VesselKind.Tug });
            await db.SaveChangesAsync(Ct);
        }
        var policy = SyntheticPlanning(Tug.Id, other); TugRequestDto job;
        using (var host = PlanningHost(policy)) {
            using var client = Client(host); await Token(client); job = await PlanningJob(client);
            await ChangePlan(client, job, null, new("Assign", TugId: other));
        }
        using var scoped = PlanningHost(policy, scoped: true); using var restricted = Client(scoped);
        var page = (await restricted.GetFromJsonAsync<TodayDto>(TugTodayPath, Ct))!;
        Assert.DoesNotContain(page.Cards.Items, c => c.Key == $"tug-job:{job.Id:D}");
        Assert.Equal(HttpStatusCode.NotFound, (await restricted.GetAsync(TugWhyPath(job.Id), Ct)).StatusCode);
        var json = await restricted.GetStringAsync(TugTodayPath, Ct);
        Assert.DoesNotContain(other.ToString(), json); Assert.DoesNotContain(job.Id.ToString(), json);
    }

    [Fact]
    public async Task Today_tug_recomputes_conflicts_against_visible_plans_outside_the_current_page()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var first = await PlanningJob(client); var second = await PlanningJob(client);
        var start = DateTimeOffset.UtcNow.AddHours(4);
        foreach (var job in new[] { first, second }) {
            var window = await ChangePlan(client, job, null, new("Window", start, start.AddHours(1)));
            await ChangePlan(client, job, window, new("Assign", TugId: Tug.Id));
        }
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(first.Id), Ct))!;
        Assert.Equal("TugConflictReviewRequired", why.Reason); Assert.NotNull(why.Tug);
        Assert.Contains(why.Tug.Evidence!.Conflicts, c => c.JobId == second.Id && c.Relation == "Intersecting");
        var page = (await client.GetFromJsonAsync<TodayDto>(TugTodayPath + "&limit=1", Ct))!;
        Assert.True(page.Cards.HasMore); Assert.Equal("Attention", Assert.Single(page.Cards.Items).Priority);
    }

    [Fact]
    public async Task Today_tug_and_board_reassess_current_equipment_and_owner_instead_of_using_saved_maintenance_due()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var command = CurrentCalendar() with { AssetId = Tug.Id, JobId = Tug.Id };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"maintenance-plan/{Tug.Id}", command)).StatusCode);
        var job = await PlanningJob(client);
        await ChangePlan(client, job, null, new("Assign", TugId: Tug.Id));
        var before = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        var fact = Assert.Single(before.Tug!.Evidence!.ReadinessFacts!, f => f.Id == command.Id);
        Assert.Equal("Blocked", fact.State); Assert.Equal("CalendarBoundary", fact.Why);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != "maintenance.owner.eligible").ToArray() }).ToArray();
        var after = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        var current = Assert.Single(after.Tug!.Evidence!.ReadinessFacts!, f => f.Id == command.Id);
        Assert.Equal("Unknown", current.State); Assert.Equal("MaintenanceOwnerUnavailable", current.Why);
        var board = (await client.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}", Ct))!;
        var boardFact = Assert.Single(board.Items.Single(r => r.Request.Id == job.Id).PlanningEvidence!.ReadinessFacts!, f => f.Id == command.Id);
        Assert.Equal(current, boardFact);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>($"/api/v1/maintenance-plan/{Tug.Id}/{command.Id}/history", Ct))!;
        Assert.Equal("NotDue", Assert.Single(history).Due.State);
    }

    [Fact]
    public async Task Today_tug_projects_calendar_due_at_the_declared_job_end_without_rewriting_current_or_historical_plan()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var command = CurrentCalendar() with { AssetId = Tug.Id, JobId = Tug.Id, ReferenceAt = PostgresInstant(DateTimeOffset.UtcNow),
            Inputs = CurrentCalendar().Inputs with { LastDoneDate = DateOnly.FromDateTime(DateTime.UtcNow), IntervalValue = "2" } };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"maintenance-plan/{Tug.Id}", command)).StatusCode);
        var job = await PlanningJob(client); var start = DateTimeOffset.UtcNow.AddDays(3);
        var plan = await ChangePlan(client, job, null, new("Window", start, start.AddHours(1)));
        await ChangePlan(client, job, plan, new("Assign", TugId: Tug.Id));
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        Assert.Equal("Blocked", Assert.Single(why.Tug!.Evidence!.ReadinessFacts!, f => f.Id == command.Id).State);
        var current = (await client.GetFromJsonAsync<MaintenancePage>($"/api/v1/maintenance-plan/{Tug.Id}", Ct))!;
        Assert.Equal("NotDue", current.Items.Single(p => p.Id == command.Id).Due.State);
        await using var db = Db(); Assert.Single(await db.MaintenancePlanRevisions.Where(p => p.PlanId == command.Id).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Today_tug_withdraws_a_saved_due_result_when_its_owning_equipment_revision_changes()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var command = CurrentCalendar() with { AssetId = Tug.Id, JobId = Tug.Id };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"maintenance-plan/{Tug.Id}", command)).StatusCode);
        var job = await PlanningJob(client); await ChangePlan(client, job, null, new("Assign", TugId: Tug.Id));
        var assets = (await client.GetFromJsonAsync<EquipmentSnapshot>($"/api/v1/equipment-hierarchy/{Tug.Id}", Ct))!;
        var asset = assets.Items.Single(a => a.Id == Tug.Id);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"equipment-hierarchy/{Tug.Id}",
            NewEquipment() with { Id = asset.Id, BaseVersion = asset.Version, ParentId = asset.ParentId })).StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        var fact = Assert.Single(why.Tug!.Evidence!.ReadinessFacts!, f => f.Id == command.Id);
        Assert.Equal("Unknown", fact.State); Assert.Equal("EquipmentRevisionChanged", fact.Why);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Today_tug_rechecks_observation_revocation_and_freshness_through_the_job_end(bool futureWindow)
    {
        using var host = PlanningHost().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MaintenanceObservations:Sources:0:TenantId"] = Tug.TenantId.ToString(), ["MaintenanceObservations:Sources:0:VesselId"] = Tug.Id.ToString(),
            ["MaintenanceObservations:Sources:0:AssetId"] = Tug.Id.ToString(), ["MaintenanceObservations:Sources:0:Source"] = "synthetic-meter",
            ["MaintenanceObservations:Sources:0:Version"] = "synthetic-v1", ["MaintenanceObservations:Sources:0:Kind"] = "Meter",
            ["MaintenanceObservations:Sources:0:Unit"] = "hours", ["MaintenanceObservations:Sources:0:FreshnessSeconds"] = "3600",
            ["MaintenanceObservations:Sources:0:MaxScale"] = "4"
        })));
        using var client = Client(host); await Token(client);
        var reading = Reading("25"); var observationPath = $"equipment-hierarchy/{Tug.Id}/{Tug.Id}/observations";
        Assert.Equal(HttpStatusCode.OK, (await Send(client, observationPath, reading)).StatusCode);
        var command = new MaintenanceCommand(Guid.NewGuid(), null, Tug.Id, Tug.Id, CurrentCalendar().OwnerRoleId,
            new(TriggerType: "Meter", IntervalValue: "10", IntervalUnit: "hours", LastDoneMeter: "10", TimeZone: "UTC",
                ObservationId: reading.Id, Provenance: "SyntheticUnverified", Criticality: "DeclaredCritical"), PostgresInstant(DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.OK, (await Send(client, $"maintenance-plan/{Tug.Id}", command)).StatusCode);
        var job = await PlanningJob(client); var plan = await ChangePlan(client, job, null, new("Assign", TugId: Tug.Id));
        if (futureWindow) {
            var start = DateTimeOffset.UtcNow.AddHours(2);
            await ChangePlan(client, job, plan, new("Window", start, start.AddHours(1)));
        } else Assert.Equal(HttpStatusCode.OK, (await Send(client, observationPath + $"/{reading.Id}/revoke", new { })).StatusCode);
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        var fact = Assert.Single(why.Tug!.Evidence!.ReadinessFacts!, f => f.Id == command.Id);
        Assert.Equal("Unknown", fact.State); Assert.Equal(futureWindow ? "ObservationStale" : "ObservationRevoked", fact.Why);
        var history = (await client.GetFromJsonAsync<MaintenanceHistoryRow[]>($"/api/v1/maintenance-plan/{Tug.Id}/{command.Id}/history", Ct))!;
        Assert.Equal("Due", Assert.Single(history).Due.State); Assert.Equal("20", Assert.Single(history).Due.NextDueMeter);
    }

    [Fact]
    public async Task Today_tug_rejects_cross_context_keys_and_fails_closed_when_its_read_audit_fails()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client); var job = await PlanningJob(client);
        var otherTenant = seed.Vessels.First(v => v.TenantId != Tug.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/why-engine?vesselId={otherTenant.Id}&cardKey=tug-job:{job.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(TugWhyPath(Guid.NewGuid()), Ct)).StatusCode);
        foreach (var key in new[] { "tug-job:bad", "tug-job:00000000-0000-0000-0000-000000000000", "tug-job:../../secret" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/why-engine?vesselId={Tug.Id}&cardKey={Uri.EscapeDataString(key)}", Ct)).StatusCode);
        await Sql("""CREATE TRIGGER today_tug_reject BEFORE INSERT ON audit."SecurityEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
        foreach (var path in new[] { TugTodayPath, TugWhyPath(job.Id) }) {
            var failed = await client.GetAsync(path, Ct); Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.DoesNotContain(job.Id.ToString(), await failed.Content.ReadAsStringAsync(Ct));
        }
    }
}
