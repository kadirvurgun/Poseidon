using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV097..099 / T-OPS2-001..003. Actual API+PostgreSQL test sources, execution deferred.
public sealed partial class DirectoryWorkspaceTests
{
    private static void DailyPermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>(); var member=settings.Memberships[0];
        settings.Memberships[0]=member with {Permissions=[..member.Permissions,"daily.noon.read","daily.deck.read","daily.engine.read",
            write?"daily.noon.write":"unused",write?"daily.deck.write":"unused",write?"daily.engine.write":"unused"]};
    }
    private string DailyPath(string route,Guid? id=null,Guid? vessel=null,string suffix="") =>
        "/api/v1/"+route+(id==null?"":"/"+id)+suffix+"?vesselId="+(vessel??SeedVessel.Id);
    private async Task<HttpResponseMessage> DailySend(HttpClient client,string route,DailyDraftCommand command,string key,bool update=false,Guid? vessel=null) {
        using var request=new HttpRequestMessage(update?HttpMethod.Put:HttpMethod.Post,DailyPath(route,update?command.Id:null,vessel));
        await Token(client); request.Headers.Add("Idempotency-Key",key); request.Content=JsonContent.Create(command);
        return await client.SendAsync(request,Ct);
    }
    [Theory]
    [InlineData("noon-reports","Noon","weather_summary")]
    [InlineData("deck-daily-logs","Deck","handover_note")]
    [InlineData("engine-daily-logs","Engine","alarms")]
    public async Task Daily_commands_have_exact_receipt_replay_immutable_MVCC_history_and_attributed_audit(string route,string kind,string field) {
        await using var host=Host(); using var client=Client(host); DailyPermissions(host.Services);
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(){[field]=new("Known","FICTIONAL initial\nobservation")},"INITIAL_AUTHORING");
        var key=Guid.NewGuid().ToString("D");
        var first=await DailySend(client,route,command,key); Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var bytes=await first.Content.ReadAsStringAsync(Ct); var saved=(await first.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        Assert.Equal(kind,saved.Kind); Assert.Equal("DRAFT",saved.State); Assert.Equal("UserDeclaredUnverified",saved.Provenance);
        Assert.Equal("PENDING",saved.ReviewState); Assert.Null(saved.ReviewedBy); Assert.True(saved.Version>0);
        var replay=await DailySend(client,route,command,key); Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        var changed=command with {Fields=new(){[field]=new("Known","FICTIONAL corrected")},ReasonCode="AUTHORING_CORRECTION",ExpectedVersion=saved.Version};
        Assert.Equal(HttpStatusCode.Conflict,(await DailySend(client,route,changed,key,true)).StatusCode);
        var updatedResponse=await DailySend(client,route,changed,Guid.NewGuid().ToString("D"),true);
        Assert.Equal(HttpStatusCode.OK,updatedResponse.StatusCode); var updated=(await updatedResponse.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        Assert.Equal(2,updated.Revision); Assert.NotEqual(saved.Version,updated.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await DailySend(client,route,changed,Guid.NewGuid().ToString("D"),true)).StatusCode);
        var history=(await client.GetFromJsonAsync<DailyHistoryDto>(DailyPath(route,saved.Id,suffix:"/history"),Ct))!;
        Assert.False(history.HasMore); Assert.Equal(2,history.Items.Length);
        var prior=history.Items.Single(r=>r.Revision==1); Assert.Equal(saved.Version,prior.Version); Assert.Equal(saved.Hash,prior.Hash);
        Assert.Equal(command.Fields[field],prior.Fields[field]);
        await using var db=Db(); Assert.Equal(2,await db.DailyOperationReceipts.CountAsync(r=>r.RecordId==saved.Id,Ct));
        var audit=await db.AuditEvents.Where(e=>e.ResourceId==saved.Id).ToArrayAsync(Ct); Assert.NotEmpty(audit);
        Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(audit));
        var revision=await db.DailyOperationRevisions.SingleAsync(r=>r.RecordId==saved.Id && r.Revision==1,Ct);
        revision.FieldsJson="{}"; await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Daily_read_write_caps_deny_missing_permission_and_foreign_vessel_then_revoke_receipt_recovery() {
        await using var host=Host(); using var client=Client(host); var route="noon-reports";
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(DailyPath(route),Ct)).StatusCode);
        DailyPermissions(host.Services);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(DailyPath(route,vessel:foreign.Id),Ct)).StatusCode);
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(){["position_lat"]=new("Unknown",null)},"INITIAL_AUTHORING");
        var key=Guid.NewGuid().ToString("D"); Assert.Equal(HttpStatusCode.OK,(await DailySend(client,route,command,key)).StatusCode);
        var caps=(await client.GetFromJsonAsync<DailyCapabilitiesDto>(DailyPath(route,suffix:"/capabilities"),Ct))!;
        Assert.True(caps.CanWrite); Assert.False(caps.CanReview); Assert.False(caps.CanSubmit); Assert.False(caps.Offline.Supported);
        Assert.Equal(DailyOperationsPolicy.SourceVersion,caps.SourceVersion); Assert.Equal(20,caps.Schema.Length);
        var settings=host.Services.GetRequiredService<FederationSettings>(); var member=settings.Memberships[0];
        settings.Memberships[0]=member with {Permissions=member.Permissions.Where(p=>p!="daily.noon.write").ToArray()};
        Assert.Equal(HttpStatusCode.Forbidden,(await DailySend(client,route,command,key)).StatusCode);
        await using var db=Db(); Assert.Equal(1,await db.DailyOperationRevisions.CountAsync(r=>r.RecordId==command.Id,Ct));
    }
    [Fact]
    public async Task Daily_drafts_reject_forged_review_references_and_duplicate_query_selectors_without_mutation() {
        await using var host=Host(); using var client=Client(host); DailyPermissions(host.Services);
        var command=new DailyDraftCommand(Guid.NewGuid(),0,new(){["master_review"]=new("Known","SUBMITTED")},"INITIAL_AUTHORING");
        Assert.Equal(HttpStatusCode.BadRequest,(await DailySend(client,"noon-reports",command,Guid.NewGuid().ToString("D"))).StatusCode);
        command=command with {Fields=new(){["voyage_id"]=new("Known",Guid.NewGuid().ToString("D"))}};
        Assert.Equal(HttpStatusCode.BadRequest,(await DailySend(client,"noon-reports",command,Guid.NewGuid().ToString("D"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(DailyPath("noon-reports")+"&vesselId="+SeedVessel.Id,Ct)).StatusCode);
        await using var db=Db(); Assert.False(await db.DailyOperations.AnyAsync(r=>r.Id==command.Id,Ct));
    }
}
