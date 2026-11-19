using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S7 WP-D DEV-104 / T-CERT2-001 (18.01 row105; 60.04 Rev3 p135-140, p226, p245-246, p637-639) and
// DEV-112 / T-CERT2-002 (row113; p221-222). Real host, antiforgery and owned PostgreSQL; synthetic
// memberships only. Authored; execution DEFERRED (no Docker in this package).
public sealed partial class DirectoryWorkspaceTests
{
    private string SurveyPath(Guid? id=null,string suffix="",Guid? vessel=null)=>"pre-survey-checklists"+(id==null?"":"/"+id)+suffix+"?vesselId="+(vessel??SeedVessel.Id);
    private void SurveyActor(IServiceProvider services,Guid actor,string kind="Human",params string[] extra) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{ActorId=actor,ActorKind=kind,Permissions=[..m.Permissions.Where(p=>!p.StartsWith("certificate.survey.",StringComparison.Ordinal)),"certificate.survey.plan",..extra]};
    }
    private async Task<PreSurveyChecklistDto> CurrentChecklist(HttpClient client,Guid id)=>(await client.GetFromJsonAsync<PreSurveyChecklistDto>("/api/v1/"+SurveyPath(id),Ct))!;
    private static PreSurveyItemInput Input(PreSurveyItemDto i,Guid? owner=null,string? answer=null)=>new(i.ItemId,owner??i.OwnerRoleId,i.DueDate,answer??i.Answer,i.Evidence,i.Finding,i.Comment);

    [Fact]
    [Trait("Requirement","DEV-104")]
    public async Task Pre_survey_checklist_blocks_on_critical_items_and_records_independent_human_verification() {
        await using var host=Host();using var client=Client(host);CertificatePermissions(host.Services);
        var author=host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;SurveyActor(host.Services,author);await Token(client);
        var certificate=await CertificateForRenewal(client);
        var role=Guid.Parse(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0]);
        var create=new PreSurveyCreateCommand(Guid.NewGuid(),0,certificate.Id,certificate.Revision,certificate.Version,"STARTER-ANNUAL",new());var key=Guid.NewGuid().ToString("D");
        using var created=await Send(client,SurveyPath(),create,key);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var bytes=await created.Content.ReadAsStringAsync(Ct);
        using(var replay=await Send(client,SurveyPath(),create,key))Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        await AssertCode(await Send(client,SurveyPath(),create with{TemplateCode="STARTER-RENEWAL"},key),HttpStatusCode.Conflict,"idempotency_key_reused");
        await AssertCode(await Send(client,SurveyPath(),create with{Id=Guid.NewGuid(),TemplateCode="APPROVED-CLASS-CATALOG"}),HttpStatusCode.UnprocessableEntity,"template_unavailable");
        var checklist=await CurrentChecklist(client,create.Id);
        Assert.Equal("BLOCKED",checklist.Readiness.State);Assert.Equal("ADVISORY_NOT_SURVEY_APPROVAL",checklist.Qualification);Assert.Equal("STARTER_UNAPPROVED",checklist.TemplateStatus);
        Assert.Equal("UNKNOWN",checklist.Window.State);Assert.Equal("UNKNOWN",checklist.VoyageFit.State);Assert.Contains("WINDOW_UNKNOWN",checklist.VoyageFit.Reasons);
        Assert.Equal("UNKNOWN_NO_VOYAGE_MODULE",checklist.VoyageReference);Assert.All(checklist.Items,i=>Assert.Equal("PENDING",i.VerificationState));

        // Not-applicable needs template authority (60.04 p140 NA_NOT_AUTHORIZED).
        var na=checklist.Items.Select(i=>Input(i,role,i.Critical?"NOT_APPLICABLE":"READY")).ToArray();
        await AssertCode(await Send(client,SurveyPath(checklist.Id),new PreSurveyUpdateCommand(checklist.Id,checklist.Version,checklist.Revision,certificate.Revision,certificate.Version,new(),na),update:true),
            HttpStatusCode.UnprocessableEntity,"na_not_authorized");
        var answered=checklist.Items.Select(i=>Input(i,role,i.AllowNotApplicable?"NOT_APPLICABLE":"READY")).ToArray();
        using(var saved=await Send(client,SurveyPath(checklist.Id),new PreSurveyUpdateCommand(checklist.Id,checklist.Version,checklist.Revision,certificate.Revision,certificate.Version,new(),answered),update:true))
            Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        await AssertCode(await Send(client,SurveyPath(checklist.Id),new PreSurveyUpdateCommand(checklist.Id,checklist.Version,checklist.Revision,certificate.Revision,certificate.Version,new(),answered),update:true),
            HttpStatusCode.Conflict,"version_conflict");
        checklist=await CurrentChecklist(client,checklist.Id);
        // Answered but unverified critical items still block readiness.
        Assert.Equal("BLOCKED",checklist.Readiness.State);Assert.All(checklist.Items,i=>Assert.True(i.AnsweredByCurrentActor));

        var access=checklist.Items.Single(i=>i.ItemCode=="SURVEYOR_ACCESS_ARRANGED");var originals=checklist.Items.Single(i=>i.ItemCode=="CERTIFICATE_ORIGINALS_AVAILABLE");
        var decisions=new[]{new PreSurveyVerificationDecision(access.ItemId,access.ItemHash,"VERIFIED"),new PreSurveyVerificationDecision(originals.ItemId,originals.ItemHash,"VERIFIED")};
        var verify=new PreSurveyVerifyCommand(checklist.Id,checklist.Version,checklist.Revision,decisions);
        await AssertCode(await Send(client,SurveyPath(checklist.Id,"/verifications"),verify),HttpStatusCode.Forbidden,"permission_denied");
        SurveyActor(host.Services,author,"Human","certificate.survey.review");await Token(client);
        // The answerer cannot verify; every failing item is reported.
        using(var own=await Send(client,SurveyPath(checklist.Id,"/verifications"),verify)) {
            Assert.Equal(HttpStatusCode.UnprocessableEntity,own.StatusCode);
            using var body=JsonDocument.Parse(await own.Content.ReadAsStringAsync(Ct));Assert.Equal("verification_rejected",body.RootElement.GetProperty("code").GetString());
            Assert.Equal(2,body.RootElement.GetProperty("items").GetArrayLength());
        }
        SurveyActor(host.Services,Guid.NewGuid(),"AI","certificate.survey.review");await Token(client);
        await AssertCode(await Send(client,SurveyPath(checklist.Id,"/verifications"),verify),HttpStatusCode.Forbidden,"human_actor_required");
        SurveyActor(host.Services,Guid.NewGuid(),"Human","certificate.survey.review");await Token(client);
        // Evidence-required READY items cannot be verified without pinned evidence.
        using(var missing=await Send(client,SurveyPath(checklist.Id,"/verifications"),verify)) {
            Assert.Equal(HttpStatusCode.UnprocessableEntity,missing.StatusCode);
            Assert.Contains("evidence_required",await missing.Content.ReadAsStringAsync(Ct));
        }
        using(var recorded=await Send(client,SurveyPath(checklist.Id,"/verifications"),verify with{Decisions=[decisions[0]]}))Assert.Equal(HttpStatusCode.OK,recorded.StatusCode);
        checklist=await CurrentChecklist(client,checklist.Id);
        Assert.Equal("VERIFIED",checklist.Items.Single(i=>i.ItemCode=="SURVEYOR_ACCESS_ARRANGED").VerificationState);
        Assert.Equal("BLOCKED",checklist.Readiness.State);Assert.True(checklist.Readiness.CriticalUnresolved>0);
        var history=(await client.GetFromJsonAsync<PreSurveyPageDto>("/api/v1/"+SurveyPath(checklist.Id,"/history"),Ct))!;
        Assert.Equal(new[]{"VERIFY","UPDATE","CREATE"},history.Items.Select(r=>r.Transition));Assert.Single(history.Items,r=>r.CurrentRevision);

        await using var db=Db();
        var row=await db.PreSurveyChecklistRevisions.SingleAsync(r=>r.ChecklistId==checklist.Id && r.Revision==1,Ct);row.ContentJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement","DEV-112")]
    public async Task Survey_planner_lists_scoped_windows_with_unknown_fit_and_navigation_follows_certificate_read() {
        await using var host=Host();using var client=Client(host);CertificatePermissions(host.Services);
        SurveyActor(host.Services,host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId);await Token(client);
        var certificate=await CertificateForRenewal(client);
        using(var created=await Send(client,SurveyPath(),new PreSurveyCreateCommand(Guid.NewGuid(),0,certificate.Id,certificate.Revision,certificate.Version,"STARTER-RENEWAL",new())))
            Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var planner=(await client.GetFromJsonAsync<SurveyPlannerDto>("/api/v1/survey-planner?vesselId="+SeedVessel.Id,Ct))!;
        var entry=Assert.Single(planner.Items,r=>r.CertificateId==certificate.Id);
        Assert.Equal("UNKNOWN",entry.Window.State);Assert.Contains("WINDOW_NOT_DECLARED",entry.Window.Qualifiers);
        Assert.NotEqual("FIT",entry.Fit.State);Assert.Equal("EXTERNAL_NOT_SIMULATED",entry.Coordination);Assert.Equal("BLOCKED",entry.Checklist!.Readiness);
        Assert.DoesNotContain("FIT",planner.FitStates);
        using(var bad=await client.GetAsync("/api/v1/survey-planner?from=2027-01-01&to=2026-01-01",Ct))Assert.Equal(HttpStatusCode.BadRequest,bad.StatusCode);
        using(var foreign=await client.GetAsync("/api/v1/survey-planner?vesselId="+Guid.NewGuid(),Ct))Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        using(var hidden=await client.GetAsync("/api/v1/"+SurveyPath(vessel:Guid.NewGuid()),Ct))Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        var navigation=(await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace",Ct))!;
        Assert.Contains(navigation.Destinations,d=>d.Key=="pre-survey-checklist" && d.Href=="/app/pre-survey-checklist");
        Assert.Contains(navigation.Destinations,d=>d.Key=="survey-planner" && d.Href=="/app/survey-planner");
        CertificatePermissions(host.Services,write:false);var settings=host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0]=settings.Memberships[0] with{Permissions=settings.Memberships[0].Permissions.Where(p=>p!="certificate.read").ToArray()};await Token(client);
        navigation=(await client.GetFromJsonAsync<WorkspaceDto>("/api/v1/workspace",Ct))!;
        Assert.DoesNotContain(navigation.Destinations,d=>d.Key is "pre-survey-checklist" or "survey-planner");
        using(var denied=await client.GetAsync("/api/v1/survey-planner",Ct))Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Fact]
    [Trait("Requirement","DEV-104")]
    public async Task Pre_survey_fixture_is_idempotent_fictional_and_never_verified() {
        for(var i=0;i<2;i++) {
            await using var admin=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:presurvey-fixture","System","presurvey-fixture"),administrativeImport:true);
            await using var tx=await admin.Database.BeginTransactionAsync(Ct);
            await VesselCertificateFixtures.AddMissingAsync(admin,[SeedVessel],Ct);await PreSurveyChecklistFixtures.AddMissingAsync(admin,[SeedVessel],Ct);await tx.CommitAsync(Ct);
        }
        await using var db=Db();
        var id=PreSurveyChecklistFixtures.Id(SeedVessel.Id);
        var rows=await db.PreSurveyChecklistRevisions.Where(r=>r.ChecklistId==id).ToArrayAsync(Ct);var row=Assert.Single(rows);
        Assert.Equal("SyntheticUnverified",row.Provenance);Assert.Equal("BLOCKED",row.Readiness);
        var content=JsonSerializer.Deserialize<PreSurveyChecklistContent>(row.ContentJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.All(content.Items,i=>{Assert.Equal("OPEN",i.Answer);Assert.Equal("PENDING",i.Verification.State);Assert.Null(i.AnsweredBy);Assert.Empty(i.Evidence);});
        Assert.Null(content.Plan.PortCallId);Assert.False(await db.PreSurveyReceipts.AnyAsync(r=>r.ChecklistId==id,Ct));
    }
}
