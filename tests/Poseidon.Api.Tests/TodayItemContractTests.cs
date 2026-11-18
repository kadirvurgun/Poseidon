using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-023 / T-S2-004 (61.03 paragraph 401-428, §33): every Today item names its owning source
// (module + record + revision + save time), its next *authorized* action and a deterministic
// composition view state. Actual host + owned PostgreSQL.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    [Trait("Requirement", "DEV-023")]
    public async Task Today_item_source_ref_matches_why_lineage_and_next_action_follows_the_owner_write_gate()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var due = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, due)).StatusCode);
        var profile = await Profile(client);
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.Equal("Partial", page.ViewState); // Operational/safety/permit/evidence owners are not connected.
        var card = Assert.Single(page.Cards.Items, c => c.Key == $"maintenance-plan:{due.Id:D}");
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(due.Id), Ct))!;
        Assert.Equal(new TodayCardSourceDto("PMS", "MaintenancePlan", due.Id, why.Source!.Revision, why.SavedAt), card.SourceRef);
        Assert.Equal("ReviewMaintenancePlan", card.NextAction); Assert.Equal(card.NextAction, why.NextAction);
        var draft = Assert.Single(page.Cards.Items, c => c.Key == "regulatory-draft");
        var draftWhy = (await client.GetFromJsonAsync<TodayWhyDto>(WhyPath, Ct))!;
        Assert.Equal(new TodayCardSourceDto("REGULATORY", "RegulatoryDraft", SeedVessel.Id, profile.Revision, draftWhy.SavedAt), draft.SourceRef);
        Assert.Equal(draftWhy.Source!.Revision, draft.SourceRef.Revision);

        // Read-only actor: the card still names its source, but the next action is View, never Review.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != "maintenance.write").ToArray() }).ToArray();
        var readOnly = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        var viewCard = Assert.Single(readOnly.Cards.Items, c => c.Key == card.Key);
        Assert.Equal("ViewMaintenancePlan", viewCard.NextAction); Assert.Equal(card.SourceRef, viewCard.SourceRef);
        Assert.Equal("ViewMaintenancePlan", (await client.GetFromJsonAsync<TodayWhyDto>(MaintenanceWhyPath(due.Id), Ct))!.NextAction);
        await using var db = Db();
        Assert.Single(await db.MaintenancePlanRevisions.Where(x => x.PlanId == due.Id).ToArrayAsync(Ct)); // Today never writes.
    }

    [Fact]
    [Trait("Requirement", "DEV-023")]
    public async Task Today_hidden_module_items_leave_no_source_ref_count_or_identifier_and_view_state_stays_partial()
    {
        using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var plan = CurrentCalendar();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, PlansUrl, plan)).StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var member = settings.Memberships[0];
        settings.Memberships = [member with { Permissions = member.Permissions.Where(p => p != "maintenance.read").ToArray() }];
        var json = await client.GetStringAsync(TodayPath, Ct);
        Assert.DoesNotContain(plan.Id.ToString("D"), json);
        Assert.DoesNotContain("\"PMS\"", json);
        var page = (await client.GetFromJsonAsync<TodayDto>(TodayPath, Ct))!;
        Assert.Equal("Unavailable", page.Coverage.Single(c => c.Source == "TechnicalMaintenance").Status);
        Assert.Equal("Partial", page.ViewState);
        Assert.All(page.Cards.Items, c => Assert.Equal("REGULATORY", c.SourceRef.Owner));
        using (var denied = await client.GetAsync(MaintenanceWhyPath(plan.Id), Ct)) {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.DoesNotContain(plan.Id.ToString("D"), await denied.Content.ReadAsStringAsync(Ct));
        }
        // Vessel grant removal: the selector is not authority; no partial snapshot or source ref is returned.
        settings.Memberships = [member with { TenantWideManage = false, TenantWideRead = false,
            VesselGrants = [new(seed.Vessels.First(x => x.TenantId == SeedVessel.TenantId && x.Id != SeedVessel.Id).Id)] }];
        using var outOfScope = await client.GetAsync(TodayPath, Ct);
        Assert.Equal(HttpStatusCode.NotFound, outOfScope.StatusCode);
        var body = await outOfScope.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain(plan.Id.ToString("D"), body); Assert.DoesNotContain("viewState", body);
    }

    [Fact]
    [Trait("Requirement", "DEV-023")]
    public async Task Today_tug_item_names_the_exact_job_revision_and_withdraws_review_without_plan_write()
    {
        using var host = PlanningHost(); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client);
        var page = (await client.GetFromJsonAsync<TodayDto>(TugTodayPath, Ct))!;
        var card = Assert.Single(page.Cards.Items, c => c.Key == $"tug-job:{job.Id:D}");
        var why = (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!;
        Assert.Equal(new TodayCardSourceDto("TUG", "TugJob", job.Id, job.Revision, why.SavedAt), card.SourceRef);
        Assert.Equal("ReviewTugJob", card.NextAction); Assert.Equal("Partial", page.ViewState);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships = settings.Memberships.Select(m => m with { Permissions = m.Permissions.Where(p => p != "tug.plan.write").ToArray() }).ToArray();
        var readOnly = (await client.GetFromJsonAsync<TodayDto>(TugTodayPath, Ct))!;
        Assert.Equal("ViewTugJob", Assert.Single(readOnly.Cards.Items, c => c.Key == card.Key).NextAction);
        Assert.Equal("ViewTugJob", (await client.GetFromJsonAsync<TodayWhyDto>(TugWhyPath(job.Id), Ct))!.NextAction);
    }

    [Fact]
    [Trait("Requirement", "DEV-023")]
    public async Task Today_absent_draft_source_ref_has_no_invented_revision_or_time()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        using var create = await Send(client, "vessels", new { companyId = SeedVessel.CompanyId, code = "TW-REF", name = "Synthetic Today source ref vessel", kind = "Ship" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var vessel = (await create.Content.ReadFromJsonAsync<VesselDto>(Ct))!;
        var today = (await client.GetFromJsonAsync<TodayDto>($"/api/v1/poseidon-today?vesselId={vessel.Id}", Ct))!;
        Assert.Equal(new TodayCardSourceDto("REGULATORY", "RegulatoryDraft", vessel.Id, null, null), Assert.Single(today.Cards.Items).SourceRef);
        Assert.Equal("Partial", today.ViewState);
    }
}
