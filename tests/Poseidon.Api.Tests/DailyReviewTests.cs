using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    // Independent review: the reviewer is never an author of the log (DailyReviewPolicy.IndependentReviewer). The harness has one
    // configured membership, so a different person is simulated by giving that membership a separate synthetic reviewer actor id
    // (the precedent in ApplicabilityOverrideApiTests / CertificateRenewalWorkflowApiTests). Revisions are append-only in the database.
    private readonly Guid dailyReviewer=Guid.NewGuid();
    private DailyReviewAuthority DailyAppointment(IServiceProvider services,Guid record,string kind,string duty,string actorKind="Human",bool independent=true) {
        var settings=services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        if(independent)member=member with {ActorId=dailyReviewer};
        var grant=new DailyReviewAuthority(Guid.NewGuid(),SeedVessel.Id,record,kind,Guid.Parse(member.RoleReferences![0]),duty,
            DailyOperationsPolicy.SourceVersion,"FICTIONAL owned-test appointment",DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        settings.Memberships[0]=member with {ActorKind=actorKind,DailyReviewAuthorities=[grant],Permissions=[..member.Permissions,"daily."+kind.ToLowerInvariant()+".review"]};
        return grant;
    }
    private async Task<HttpResponseMessage> ReviewSend(HttpClient client,string route,Guid id,DailyReviewCommand command,string key) {
        await Token(client);using var request=new HttpRequestMessage(HttpMethod.Post,DailyPath(route,id,suffix:"/reviews")){Content=JsonContent.Create(command)};
        request.Headers.Add("Idempotency-Key",key);return await client.SendAsync(request,Ct);
    }
    [Theory]
    [InlineData("noon-reports","Noon","Master","DRAFT")]
    [InlineData("deck-daily-logs","Deck","ChiefOfficer","PENDING")]
    [InlineData("engine-daily-logs","Engine","ChiefEngineer","PENDING")]
    public async Task Exact_human_appointment_records_immutable_review_of_one_draft_revision_without_approval(string route,string kind,string duty,string sourceReview) {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");
        var created=await DailySend(client,route,draft,Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var before=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath(route,row.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.False(before.CanRecord);Assert.Empty(before.Authorities);
        var grant=DailyAppointment(host.Services,row.Id,kind,duty);
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath(route,row.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.True(cap.CanRecord);Assert.Equal(grant.Id,Assert.Single(cap.Authorities).Id);Assert.Equal(0u,cap.ReviewVersion);
        var command=new DailyReviewCommand(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL source revision reviewed, unresolved inputs retained");
        var key=Guid.NewGuid().ToString("D");var saved=await ReviewSend(client,route,row.Id,command,key);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var bytes=await saved.Content.ReadAsStringAsync(Ct);var review=(await saved.Content.ReadFromJsonAsync<DailyReviewDto>(Ct))!;
        Assert.Equal("RECORDED_UNASSESSED",review.State);Assert.Equal(row.Hash,review.RecordHash);Assert.Equal(row.Revision,review.RecordRevision);
        Assert.Equal(dailyReviewer,review.ActorId);Assert.Equal(grant.RoleId,review.RoleId);Assert.True(review.Version>0);
        Assert.Equal(bytes,await (await ReviewSend(client,route,row.Id,command,key)).Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict,(await ReviewSend(client,route,row.Id,command,Guid.NewGuid().ToString("D"))).StatusCode);
        var current=(await client.GetFromJsonAsync<DailyRecordDto>(DailyPath(route,row.Id),Ct))!;
        Assert.Equal("RECORDED_UNASSESSED",current.ReviewState);Assert.Equal(dailyReviewer,current.ReviewedBy);Assert.Equal(row.Version,current.Version);
        Assert.Equal(sourceReview,current.Fields[kind=="Engine"?"chief_engineer_review":"master_review"].Value);Assert.Equal("DRAFT",current.State);
        var edited=await DailySend(client,route,draft with {ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION"},Guid.NewGuid().ToString("D"),true);
        Assert.Equal(HttpStatusCode.OK,edited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await ReviewSend(client,route,row.Id,command with{ExpectedReviewVersion=review.Version},Guid.NewGuid().ToString("D"))).StatusCode);
        current=(await client.GetFromJsonAsync<DailyRecordDto>(DailyPath(route,row.Id),Ct))!;Assert.Equal("PENDING",current.ReviewState);Assert.Null(current.ReviewedBy);
        var history=(await client.GetFromJsonAsync<DailyHistoryDto>(DailyPath(route,row.Id,suffix:"/history"),Ct))!;
        Assert.Equal("RECORDED_UNASSESSED",history.Items.Single(r=>r.Revision==row.Revision).ReviewState);
        await using var db=Db();Assert.Equal(1,await db.DailyReviewEvents.CountAsync(r=>r.RecordId==row.Id,Ct));
        var immutable=await db.DailyReviewEvents.SingleAsync(r=>r.Id==review.Id,Ct);immutable.Rationale="rewrite";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Theory]
    [InlineData("Unspecified")][InlineData("Machine")][InlineData("AI")]
    public async Task Administrator_or_nonhuman_actor_never_inherits_human_review_from_daily_write(string actorKind) {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master",actorKind);
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath("noon-reports",row.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.False(cap.CanRecord);
        var response=await ReviewSend(client,"noon-reports",row.Id,new(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL"),Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);await using var db=Db();Assert.Empty(await db.DailyReviewEvents.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Current_qualified_appointment_renewal_can_recover_exact_historical_outcome_without_rewriting_its_original_authority() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        var command=new DailyReviewCommand(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL immutable historical review");
        var key=Guid.NewGuid().ToString("D");var recorded=await ReviewSend(client,"noon-reports",row.Id,command,key);Assert.Equal(HttpStatusCode.OK,recorded.StatusCode);
        var bytes=await recorded.Content.ReadAsStringAsync(Ct);var settings=host.Services.GetRequiredService<FederationSettings>();
        string originalHash;string originalSnapshot;
        await using(var before=Db()){var original=await before.DailyReviewEvents.SingleAsync(r=>r.RecordId==row.Id,Ct);originalHash=original.AuthorityHash;originalSnapshot=original.AuthorityJson;}
        settings.Memberships[0].DailyReviewAuthorities=[grant with {ValidUntil=grant.ValidUntil.AddDays(1),AppointmentSource="FICTIONAL legitimate current appointment renewal"}];
        var recovered=await ReviewSend(client,"noon-reports",row.Id,command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(bytes,await recovered.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.Conflict,(await ReviewSend(client,"noon-reports",row.Id,command with{Rationale="different body"},key)).StatusCode);
        settings.Memberships[0].DailyReviewAuthorities=[];Assert.Equal(HttpStatusCode.Forbidden,(await ReviewSend(client,"noon-reports",row.Id,command,key)).StatusCode);
        await using var db=Db();var retained=await db.DailyReviewEvents.SingleAsync(r=>r.RecordId==row.Id,Ct);
        Assert.Equal(originalHash,retained.AuthorityHash);Assert.Equal(originalSnapshot,retained.AuthorityJson);Assert.Equal(1,retained.Sequence);
    }
    [Fact]
    public async Task Review_history_retains_recorded_metadata_after_authority_expiry_without_renewing_its_permission() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        var command=new DailyReviewCommand(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL retained immutable metadata");
        var key=Guid.NewGuid().ToString("D");var recorded=await ReviewSend(client,"noon-reports",row.Id,command,key);Assert.Equal(HttpStatusCode.OK,recorded.StatusCode);
        var settings=host.Services.GetRequiredService<FederationSettings>();settings.Memberships[0].DailyReviewAuthorities=[grant with {ValidUntil=DateTimeOffset.UtcNow.AddMinutes(-1)}];
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath("noon-reports",row.Id,suffix:"/review-capabilities"),Ct))!;Assert.False(cap.CanRecord);
        var current=(await client.GetFromJsonAsync<DailyRecordDto>(DailyPath("noon-reports",row.Id),Ct))!;
        Assert.Equal("RECORDED_UNASSESSED",current.ReviewState);Assert.Equal(dailyReviewer,current.ReviewedBy);Assert.Equal("DRAFT",current.State);
        Assert.Equal(HttpStatusCode.Forbidden,(await ReviewSend(client,"noon-reports",row.Id,command,key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(DailyPath("noon-reports",row.Id,suffix:"/reviews"),Ct)).StatusCode);
        await using var db=Db();var entry=await db.DailyReviewEvents.SingleAsync(r=>r.RecordId==row.Id,Ct);
        Assert.Equal(RegisterStorage.Hash(entry.AuthorityJson),entry.AuthorityHash);
    }
    [Theory]
    [InlineData("role")][InlineData("record")][InlineData("vessel")][InlineData("source")][InlineData("duty")][InlineData("expired")]
    public async Task Human_review_appointment_is_exact_role_record_vessel_source_duty_and_time_scoped(string mismatch) {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        var wrong=mismatch switch {
            "role"=>grant with {RoleId=Guid.NewGuid()},"record"=>grant with {RecordId=Guid.NewGuid()},"vessel"=>grant with {VesselId=Guid.NewGuid()},
            "source"=>grant with {SourceVersion="unqualified-source"},"duty"=>grant with {Duty="Administrator"},_=>grant with {ValidUntil=DateTimeOffset.UtcNow.AddMinutes(-1)}
        };
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].DailyReviewAuthorities=[wrong];
        var response=await ReviewSend(client,"noon-reports",row.Id,new(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL"),Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);await using var db=Db();Assert.Empty(await db.DailyReviewEvents.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Review_appointment_revoked_during_database_read_rolls_back_head_event_and_receipt() {
        var barrier=new DailyRowsBarrier();await using var host=Host(interceptor:barrier);using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        barrier.Arm();var pending=ReviewSend(client,"noon-reports",row.Id,new(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL"),Guid.NewGuid().ToString("D"));HttpResponseMessage? response=null;
        try {
            await barrier.Wait(Ct);var settings=host.Services.GetRequiredService<FederationSettings>();settings.Memberships[0].DailyReviewAuthorities=[];
            barrier.Release();response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            await using var db=Db();Assert.Empty(await db.DailyReviewHeads.ToArrayAsync(Ct));Assert.Empty(await db.DailyReviewEvents.ToArrayAsync(Ct));Assert.Empty(await db.DailyReviewReceipts.ToArrayAsync(Ct));
        }finally{barrier.Release();response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();}
    }
    [Fact]
    public async Task Review_appointment_withdrawn_during_postcommit_audit_withholds_outcome_and_exact_retry_recovers_it_once() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");var created=await DailySend(client,"noon-reports",draft,Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        var command=new DailyReviewCommand(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL");var idempotency=Guid.NewGuid().ToString("D");
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s5_pause_daily_review_response() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint); RETURN NEW; END $body$;
            CREATE TRIGGER s5_pause_daily_review_response BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='DailyReviewCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s5_pause_daily_review_response();
            """);
        var pending=ReviewSend(client,"noon-reports",row.Id,command,idempotency);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);await using(var observer=Db())Assert.True(await observer.DailyReviewEvents.AnyAsync(r=>r.RecordId==row.Id,Ct));
            var settings=host.Services.GetRequiredService<FederationSettings>();settings.Memberships[0].DailyReviewAuthorities=[];
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.Contains("command_outcome_withheld",await response.Content.ReadAsStringAsync(Ct));
            settings.Memberships[0].DailyReviewAuthorities=[grant];var recovered=await ReviewSend(client,"noon-reports",row.Id,command,idempotency);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);
            await using var db=Db();Assert.Equal(1,await db.DailyReviewEvents.CountAsync(r=>r.RecordId==row.Id,Ct));
            var receipt=await db.DailyReviewReceipts.SingleAsync(r=>r.RecordId==row.Id,Ct);Assert.Equal(receipt.OutcomeJson,await recovered.Content.ReadAsStringAsync(Ct));
        }finally{
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s5_pause_daily_review_response ON audit."SecurityEvents";DROP FUNCTION audit.s5_pause_daily_review_response();""");
        }
    }
    // Guide finding 22.11.2026: independent review of ship daily logs (13.03 p35-37/86-89; 60.01 Rev3 p544-546; ISM 10.2/12;
    // tug precedent QA S9 N5). An exact appointment never lets an author of the reviewed revision record its review.
    [Theory]
    [InlineData("noon-reports","Noon","Master")]
    [InlineData("deck-daily-logs","Deck","ChiefOfficer")]
    [InlineData("engine-daily-logs","Engine","ChiefEngineer")]
    public async Task Author_of_the_log_is_never_offered_or_allowed_to_record_its_review(string route,string kind,string duty) {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var created=await DailySend(client,route,new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING"),Guid.NewGuid().ToString("D"));
        var row=(await created.Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;var grant=DailyAppointment(host.Services,row.Id,kind,duty,independent:false);
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath(route,row.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.False(cap.CanRecord);Assert.Empty(cap.Authorities);Assert.Equal([DailyReviewPolicy.ReviewerIsAuthor],cap.Blockers);
        var response=await ReviewSend(client,route,row.Id,new(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL self review attempt"),Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.Contains("separation_of_duties",await response.Content.ReadAsStringAsync(Ct));
        await using var db=Db();Assert.Empty(await db.DailyReviewEvents.Where(r=>r.RecordId==row.Id).ToArrayAsync(Ct));
        Assert.Empty(await db.DailyReviewHeads.Where(r=>r.RecordId==row.Id).ToArrayAsync(Ct));Assert.Empty(await db.DailyReviewReceipts.Where(r=>r.RecordId==row.Id).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Author_of_an_earlier_revision_cannot_review_a_later_revision_written_by_someone_else() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var draft=new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING");
        var first=(await (await DailySend(client,"deck-daily-logs",draft,Guid.NewGuid().ToString("D"))).Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        // A different person writes revision 2; the author of revision 1 then tries to review revision 2.
        var settings=host.Services.GetRequiredService<FederationSettings>();var author=settings.Memberships[0];
        settings.Memberships[0]=author with {ActorId=dailyReviewer};
        var second=(await (await DailySend(client,"deck-daily-logs",draft with{ExpectedVersion=first.Version,ReasonCode="AUTHORING_CORRECTION"},Guid.NewGuid().ToString("D"),true)).Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        Assert.Equal(first.Revision+1,second.Revision);settings.Memberships[0]=author;
        var grant=DailyAppointment(host.Services,second.Id,"Deck","Master",independent:false);
        var cap=(await client.GetFromJsonAsync<DailyReviewCapabilitiesDto>(DailyPath("deck-daily-logs",second.Id,suffix:"/review-capabilities"),Ct))!;
        Assert.False(cap.CanRecord);Assert.Contains(DailyReviewPolicy.ReviewerIsAuthor,cap.Blockers);
        var response=await ReviewSend(client,"deck-daily-logs",second.Id,new(second.Version,0,second.Revision,grant.Id,"RETURNED","RETURNED_FOR_CORRECTION","FICTIONAL co-author return attempt"),Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [Fact]
    public async Task Database_refuses_a_review_event_by_an_author_outside_the_api_path() {
        await using var host=Host();using var client=Client(host);DailyPermissions(host.Services);
        var row=(await (await DailySend(client,"noon-reports",new DailyDraftCommand(Guid.NewGuid(),0,new(),"INITIAL_AUTHORING"),Guid.NewGuid().ToString("D"))).Content.ReadFromJsonAsync<DailyRecordDto>(Ct))!;
        var grant=DailyAppointment(host.Services,row.Id,"Noon","Master");
        var saved=await ReviewSend(client,"noon-reports",row.Id,new(row.Version,0,row.Revision,grant.Id,"REVIEWED","REVIEW_RECORDED","FICTIONAL independent review"),Guid.NewGuid().ToString("D"));
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);var review=(await saved.Content.ReadFromJsonAsync<DailyReviewDto>(Ct))!;
        var columns="\"TenantId\",\"VesselId\",\"RecordId\",\"Kind\",\"Id\",\"RecordRevision\",\"Sequence\",\"RecordVersion\",\"ReviewVersion\",\"ActorId\",\"RoleId\",\"AuthorityId\",\"Duty\",\"Decision\",\"ReasonCode\",\"Rationale\",\"SourceVersion\",\"AuthorityHash\",\"AuthorityJson\",\"RecordHash\",\"SavedAt\"";
        var error=await Assert.ThrowsAsync<PostgresException>(()=>Sql($$"""
            INSERT INTO operations."DailyReviewEvents" ({{columns}})
            SELECT "TenantId","VesselId","RecordId","Kind",gen_random_uuid(),"RecordRevision","Sequence"+1,"RecordVersion","ReviewVersion",'{{actorId}}'::uuid,"RoleId","AuthorityId","Duty","Decision","ReasonCode","Rationale","SourceVersion","AuthorityHash","AuthorityJson","RecordHash","SavedAt"
            FROM operations."DailyReviewEvents" WHERE "Id"='{{review.Id}}';
            """));
        Assert.Equal("23514",error.SqlState);
        await using var db=Db();Assert.Equal(1,await db.DailyReviewEvents.CountAsync(r=>r.RecordId==row.Id,Ct));
    }
}
