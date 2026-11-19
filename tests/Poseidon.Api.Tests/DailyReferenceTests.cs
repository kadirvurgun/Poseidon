using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-097 / T-OPS2-001 and DEV-098 / T-OPS2-002 (S5 WP-B): owning reference
// lookup, downstream reviewed projection and review-authority negatives.
// Actual HTTP + PostgreSQL sources; execution DEFERRED (no Docker this package).
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<ShipOpsRecordDto> PortCall(HttpClient client) {
        using var saved=await ShipOpsSend(client,"Port",new(Guid.NewGuid(),0,new(){["remarks"]=new("Known","FICTIONAL port call")},"INITIAL_AUTHORING"));
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);return (await saved.Content.ReadFromJsonAsync<ShipOpsRecordDto>(Ct))!;
    }

    [Fact]
    public async Task Noon_port_references_pin_an_exact_owning_port_call_revision_and_report_supersession() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);ShipOpsPermissions(host.Services);
        var caps=(await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath("noon-reports",suffix:"/capabilities"),Ct))!;
        Assert.False(caps.Schema.Single(f=>f.Key=="from_port").ReferenceUnavailable);Assert.True(caps.Schema.Single(f=>f.Key=="voyage_id").ReferenceUnavailable);
        var call=await PortCall(client);
        var options=(await client.GetFromJsonAsync<DailyReferenceOptionsDto>(DailyPath("noon-reports",suffix:"/reference-options")+"&field=from_port",Ct))!;
        Assert.True(options.Available);var option=Assert.Single(options.Items,o=>o.Id==call.Id);Assert.Equal(call.Revision,option.Revision);
        var noon=new DailyDraftCommand(Guid.NewGuid(),0,new(){["from_port"]=new("Known",option.Token)},"INITIAL_AUTHORING");
        using var created=await DailySend(client,"noon-reports",noon,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;Assert.Equal(option.Token,row.Fields["from_port"].Value);
        var refs=(await client.GetFromJsonAsync<DailyReferencesDto>(DailyPath("noon-reports",row.Id,suffix:"/references"),Ct))!;
        var from=refs.Items.Single(r=>r.Field=="from_port");Assert.Equal("Current",from.State);Assert.Equal(call.Id,from.Id);
        Assert.Equal("Missing",refs.Items.Single(r=>r.Field=="voyage_id").State);
        // The owning SOF advances: the saved pin stays exact and becomes Superseded.
        using var advanced=await ShipOpsSend(client,"Port",new(call.Id,call.Version,new(){["remarks"]=new("Known","FICTIONAL corrected")},"AUTHORING_CORRECTION"),update:true);
        Assert.Equal(HttpStatusCode.OK,advanced.StatusCode);
        refs=(await client.GetFromJsonAsync<DailyReferencesDto>(DailyPath("noon-reports",row.Id,suffix:"/references"),Ct))!;
        from=refs.Items.Single(r=>r.Field=="from_port");Assert.Equal("Superseded",from.State);Assert.Equal(call.Revision,from.Revision);Assert.Equal(call.Revision+1,from.CurrentRevision);
        // An unrelated later edit keeps the earlier pin; pinning the stale revision anew is denied.
        var edit=noon with{ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION",Fields=new(){["from_port"]=row.Fields["from_port"],["weather_summary"]=new("Known","FICTIONAL")}};
        Assert.Equal(HttpStatusCode.OK,(await DailySend(client,"noon-reports",edit,Guid.NewGuid().ToString("D"),true)).StatusCode);
        var stale=new DailyDraftCommand(Guid.NewGuid(),0,new(){["to_port"]=new("Known",option.Token)},"INITIAL_AUTHORING");
        using var denied=await DailySend(client,"noon-reports",stale,Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.BadRequest,denied.StatusCode);Assert.Contains("linked_resource_unavailable",await denied.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.False(await db.DailyOperations.AnyAsync(r=>r.Id==stale.Id,Ct));
        // The pin is copied as identity only: no SOF content is duplicated into the daily record.
        Assert.DoesNotContain("FICTIONAL port call",(await db.DailyOperations.SingleAsync(r=>r.Id==row.Id,Ct)).FieldsJson);
    }

    [Theory]
    [InlineData("random")][InlineData("foreign")][InlineData("unreadable")][InlineData("voyage")][InlineData("free-text")]
    public async Task Unresolvable_or_unreadable_references_stay_unknown_and_write_nothing(string scenario) {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);ShipOpsPermissions(host.Services);
        var call=await PortCall(client);var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        if(scenario=="unreadable") {
            var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
            settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="operations.port.read").ToArray()};
        }
        var (field,value)=scenario switch {
            "random"=>("from_port",new DailyReferencePin("port-call",Guid.NewGuid(),1).Token),
            "foreign"=>("from_port",new DailyReferencePin("port-call",ShipOperationsFixtures.Id(foreign.Id,"Port"),1).Token),
            "voyage"=>("voyage_id",new DailyReferencePin("voyage",Guid.NewGuid(),1).Token),
            "free-text"=>("to_port","FICTIONAL Port Name"),
            _=>("from_port",new DailyReferencePin("port-call",call.Id,call.Revision).Token)};
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(){[field]=new("Known",value)},"INITIAL_AUTHORING");
        using var response=await DailySend(client,"noon-reports",command,Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        if(scenario!="free-text")Assert.Contains("linked_resource_unavailable",await response.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.False(await db.DailyOperations.AnyAsync(r=>r.Id==command.Id,Ct));
        Assert.Equal(HttpStatusCode.OK,(await DailySend(client,"noon-reports",command with{Fields=new(){[field]=new("Unknown",null)}},Guid.NewGuid().ToString("D"))).StatusCode);
    }

    [Fact]
    public async Task Deck_officer_of_watch_requires_crew_read_and_a_policy_current_assignment_revision() {
        await using var host=Host(crewRead:true,crewWrite:true).WithWebHostBuilder(b=>b.ConfigureTestServices(s=>s.AddSingleton(new CrewAssignmentPolicy {
            Version="FICTIONAL-oow-assignment-v1",TimeZone="UTC",EffectiveStatuses=["FICTIONAL-onboard"],PlannedStatuses=["FICTIONAL-planned"],Provenances=["UserDeclaredUnverified"]})));
        using var client=Client(host);DailyPermissions(host.Services);
        var settings=host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0]=settings.Memberships[0] with{Permissions=[..settings.Memberships[0].Permissions,"crew.read","crew.write","crew.assignment.declare","crew.identity.write","crew.identity.read"]};await Token(client);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        var declaration=NewCrew() with{From=today,To=today.AddDays(2),PositionId=SeedVessel.Id,PositionPolicyVersion="synthetic-position-v1",DeclaredStatus="FICTIONAL-onboard",
            AssignmentPolicyVersion="FICTIONAL-oow-assignment-v1",SourceObservedAt=DateTimeOffset.UtcNow.AddSeconds(-1)};
        using var saved=await Send(client,CrewUrl,declaration);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        long revision;await using(var db=Db())revision=(await db.CrewAssignments.SingleAsync(a=>a.Id==declaration.Id,Ct)).Revision;
        var options=(await client.GetFromJsonAsync<DailyReferenceOptionsDto>(DailyPath("deck-daily-logs",suffix:"/reference-options")+"&field=officer_of_watch",Ct))!;
        var option=Assert.Single(options.Items,o=>o.Id==declaration.Id);Assert.Equal(revision,option.Revision);
        Assert.DoesNotContain("FICTIONAL",option.Label);// operator position label only, never a person identity
        var deck=new DailyDraftCommand(Guid.NewGuid(),0,new(){["officer_of_watch"]=new("Known",option.Token)},"INITIAL_AUTHORING");
        using var created=await DailySend(client,"deck-daily-logs",deck,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var refs=(await client.GetFromJsonAsync<DailyReferencesDto>(DailyPath("deck-daily-logs",row.Id,suffix:"/references"),Ct))!;
        Assert.Equal("Current",refs.Items.Single(r=>r.Field=="officer_of_watch").State);
        // Without crew.read the pin is neither listed, resolvable, nor writable.
        settings.Memberships[0]=settings.Memberships[0] with{Permissions=settings.Memberships[0].Permissions.Where(p=>p!="crew.read").ToArray()};
        options=(await client.GetFromJsonAsync<DailyReferenceOptionsDto>(DailyPath("deck-daily-logs",suffix:"/reference-options")+"&field=officer_of_watch",Ct))!;
        Assert.False(options.Available);Assert.Empty(options.Items);
        refs=(await client.GetFromJsonAsync<DailyReferencesDto>(DailyPath("deck-daily-logs",row.Id,suffix:"/references"),Ct))!;
        var hidden=refs.Items.Single(r=>r.Field=="officer_of_watch");Assert.Equal("NotVisible",hidden.State);Assert.Null(hidden.Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await DailySend(client,"deck-daily-logs",deck with{Id=Guid.NewGuid()},Guid.NewGuid().ToString("D"))).StatusCode);
    }

    [Fact]
    public async Task Seeded_unconfigured_assignment_policy_never_makes_an_officer_reference_current() {
        await using var host=Host(crewRead:true);using var client=Client(host);DailyPermissions(host.Services);
        long revision;await using(var db=Db())revision=(await db.CrewAssignments.SingleAsync(a=>a.Id==SeedVessel.Id,Ct)).Revision;
        var deck=new DailyDraftCommand(Guid.NewGuid(),0,new(){["officer_of_watch"]=new("Known",new DailyReferencePin("crew-assignment",SeedVessel.Id,revision).Token)},"INITIAL_AUTHORING");
        using var response=await DailySend(client,"deck-daily-logs",deck,Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.Contains("linked_resource_unavailable",await response.Content.ReadAsStringAsync(Ct));
        var options=(await client.GetFromJsonAsync<DailyReferenceOptionsDto>(DailyPath("deck-daily-logs",suffix:"/reference-options")+"&field=officer_of_watch",Ct))!;
        Assert.DoesNotContain(options.Items,o=>o.Id==SeedVessel.Id);
    }

    [Fact]
    public async Task Reference_options_require_current_write_and_a_mutable_reference_field() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services,write:false);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DailyPath("noon-reports",suffix:"/reference-options")+"&field=from_port",Ct)).StatusCode);
        DailyPermissions(host.Services);
        foreach(var field in new[]{"vessel_id","master_review","activity","unknown_key"})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(DailyPath("noon-reports",suffix:"/reference-options")+"&field="+field,Ct)).StatusCode);
        var voyage=(await client.GetFromJsonAsync<DailyReferenceOptionsDto>(DailyPath("noon-reports",suffix:"/reference-options")+"&field=voyage_id",Ct))!;
        Assert.False(voyage.Available);Assert.Empty(voyage.Items);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        Assert.Contains((await client.GetAsync(DailyPath("noon-reports",vessel:foreign.Id,suffix:"/reference-options")+"&field=from_port",Ct)).StatusCode,
            new[]{HttpStatusCode.Forbidden,HttpStatusCode.NotFound});
    }

    [Fact]
    public async Task Reviewed_noon_summary_exposes_only_master_reviewed_exact_revisions_and_marks_unreviewed_data() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var fields=new Dictionary<string,DailyValue>{["fuel_summary"]=new("Known","FICTIONAL 12.5 VLSFO declared"),["distance_run_nm"]=new("Known","240.0"),["avg_speed_kn"]=new("Unknown",null)};
        var reviewedDraft=new DailyDraftCommand(Guid.NewGuid(),0,fields,"INITIAL_AUTHORING");var plainDraft=reviewedDraft with{Id=Guid.NewGuid()};
        var reviewedRow=(await (await DailySend(client,"noon-reports",reviewedDraft,Guid.NewGuid().ToString("D"))).Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        Assert.Equal(HttpStatusCode.OK,(await DailySend(client,"noon-reports",plainDraft,Guid.NewGuid().ToString("D"))).StatusCode);
        var path="/api/v1/noon-reports/reviewed-summary?vesselId="+SeedVessel.Id;
        var before=(await client.GetFromJsonAsync<NoonSummaryPageDto>(path,Ct))!;
        Assert.All(before.Items,i=>{Assert.Equal("UNREVIEWED",i.State);Assert.False(i.Consumable);Assert.Empty(i.Fields);Assert.Contains("NOT_FOR_DOWNSTREAM_REUSE",i.Qualifications);});
        Assert.Equal("INPUTS_ONLY_NO_SOURCE_DEFINED_FORMULA",before.EmissionsBasis);
        var grant=DailyAppointment(host.Services,reviewedRow.Id,"Noon","Master");
        Assert.Equal(HttpStatusCode.OK,(await ReviewSend(client,"noon-reports",reviewedRow.Id,new(reviewedRow.Version,0,reviewedRow.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL reviewed"),Guid.NewGuid().ToString("D"))).StatusCode);
        var after=(await client.GetFromJsonAsync<NoonSummaryPageDto>(path,Ct))!;
        var item=after.Items.Single(i=>i.RecordId==reviewedRow.Id);
        Assert.Equal("MASTER_REVIEW_RECORDED",item.State);Assert.True(item.Consumable);Assert.True(item.ReviewedIsCurrent);
        Assert.Equal(reviewedRow.Hash,item.ReviewedPin!.Hash);Assert.Equal(reviewedRow.Revision,item.ReviewedPin.Revision);
        Assert.Equal("FICTIONAL 12.5 VLSFO declared",item.Fields.Single(f=>f.Key=="fuel_summary").Value.Value);
        Assert.Equal("Unknown",item.Fields.Single(f=>f.Key=="avg_speed_kn").Value.State);// never coerced to zero
        Assert.Equal("nm",item.Fields.Single(f=>f.Key=="distance_run_nm").Unit);
        Assert.Contains("REVIEW_RECORDING_IS_NOT_VERIFICATION",item.Qualifications);Assert.Contains("NO_COMPUTED_EMISSIONS",item.Qualifications);
        Assert.Equal("UNREVIEWED",after.Items.Single(i=>i.RecordId==plainDraft.Id).State);
        // A later unreviewed draft keeps the reviewed revision but marks it non-current.
        Assert.Equal(HttpStatusCode.OK,(await DailySend(client,"noon-reports",reviewedDraft with{ExpectedVersion=reviewedRow.Version,ReasonCode="AUTHORING_CORRECTION",
            Fields=new(fields){["fuel_summary"]=new("Known","FICTIONAL changed after review")}},Guid.NewGuid().ToString("D"),true)).StatusCode);
        item=(await client.GetFromJsonAsync<NoonSummaryPageDto>(path,Ct))!.Items.Single(i=>i.RecordId==reviewedRow.Id);
        Assert.False(item.ReviewedIsCurrent);Assert.Equal(reviewedRow.Revision,item.ReviewedPin!.Revision);Assert.Contains("LATER_UNREVIEWED_REVISION_EXISTS",item.Qualifications);
        Assert.Equal("FICTIONAL 12.5 VLSFO declared",item.Fields.Single(f=>f.Key=="fuel_summary").Value.Value);
        var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="daily.noon.read").ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(path,Ct)).StatusCode);
        await using var db=Db();Assert.DoesNotContain("FICTIONAL",System.Text.Json.JsonSerializer.Serialize(await db.AuditEvents.Where(e=>e.ResourceId==SeedVessel.Id).ToArrayAsync(Ct)));
    }

    [Fact]
    public async Task Review_permission_without_a_server_appointment_or_with_a_wrong_duty_records_nothing() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");
        var row=(await (await DailySend(client,"deck-daily-logs",draft,Guid.NewGuid().ToString("D"))).Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"daily.deck.review"],DailyReviewAuthorities=[]};
        var command=new DailyReviewCommand(row.Version,0,row.Revision,Guid.NewGuid(),"REVIEWED","REVIEW_RECORDED","FICTIONAL");
        using(var none=await ReviewSend(client,"deck-daily-logs",row.Id,command,Guid.NewGuid().ToString("D"))){
            Assert.Equal(HttpStatusCode.Forbidden,none.StatusCode);Assert.Contains("human_authority_required",await none.Content.ReadAsStringAsync(Ct));}
        var wrong=DailyAppointment(host.Services,row.Id,"Deck","ChiefEngineer");
        Assert.Equal(HttpStatusCode.Forbidden,(await ReviewSend(client,"deck-daily-logs",row.Id,command with{AuthorityId=wrong.Id},Guid.NewGuid().ToString("D"))).StatusCode);
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath("deck-daily-logs",row.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.False(cap.CanRecord);Assert.Contains("HUMAN_REVIEW_AUTHORITY_UNAVAILABLE",cap.Blockers);
        await using var db=Db();Assert.Empty(await db.DailyReviewEvents.ToArrayAsync(Ct));Assert.Empty(await db.DailyReviewReceipts.ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Synthetic_daily_fixtures_seed_no_review_appointment_or_reference() {
        await using var host=Host();
        await using var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("synthetic-fixture","System","daily-fixture"),administrativeImport:true);
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)) {await DailyOperationsFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
        Assert.Empty(await db.DailyReviewEvents.ToArrayAsync(Ct));Assert.Empty(await db.DailyReviewHeads.ToArrayAsync(Ct));
        var rows=await db.DailyOperations.Where(r=>r.VesselId==SeedVessel.Id).ToArrayAsync(Ct);
        Assert.All(rows,r=>{Assert.Equal("SyntheticUnverified",r.Provenance);Assert.DoesNotContain("\"Known\"",r.FieldsJson);});
        Assert.All(host.Services.GetRequiredService<FederationSettings>().Memberships,m=>Assert.Empty(m.DailyReviewAuthorities??[]));
    }
}
