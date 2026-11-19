using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S6 WP-A DEV-044 / T-S6-002 (18.01 row45; 60.04 VC-012 p180-200, SoD p196/p252-253) and DEV-043
// superseded-history display. Real host, antiforgery, scanned fixture bytes and owned PostgreSQL;
// synthetic memberships only. Authored; execution DEFERRED (no Docker in this package).
public sealed partial class DirectoryWorkspaceTests
{
    private void RenewalActor(IServiceProvider services,Guid actor,params string[] extra) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{ActorId=actor,ActorKind="Human",Permissions=[..m.Permissions.Where(p=>!p.StartsWith("certificate.renewal.",StringComparison.Ordinal)),
            "certificate.renewal.write","certificate.renewal.owner",..extra]};
    }
    private async Task<CertificateRenewalDto> Transition(HttpClient client,CertificateRenewalDto renewal,string action,CertificateRenewalDecision? decision=null,HttpStatusCode expected=HttpStatusCode.OK,string? code=null) {
        using var response=await Send(client,RenewalPath(renewal.Id,"/transitions"),new CertificateRenewalTransitionCommand(renewal.Id,renewal.Version,renewal.Revision,action,decision??new()));
        if(code!=null){await AssertCode(response,expected,code);return renewal;}
        Assert.Equal(expected,response.StatusCode);return (await response.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!;
    }
    private async Task<CertificateRenewalDto> CurrentRenewal(HttpClient client,Guid id)=>(await client.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(id),Ct))!;

    [Fact]
    [Trait("Requirement","DEV-044")]
    public async Task Renewal_workflow_runs_source_states_with_human_sod_and_supersedes_only_on_completion() {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-renewal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);
            var author=host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;RenewalActor(host.Services,author);await Token(client);
            var original=await CertificateWithScannedFile(client);var replacement=await CertificateWithScannedFile(client);
            var role=Guid.Parse(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0]);
            var content=new CertificateRenewalContent(OwnerRoleId:role,DueDate:DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),RequiredSurvey:true,RequiredDocuments:false,RequiredActions:null);
            using var created=await Send(client,RenewalPath(),new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,content,"INITIAL_AUTHORING"));
            Assert.Equal(HttpStatusCode.OK,created.StatusCode);var renewal=await CurrentRenewal(client,(await created.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!.Id);
            Assert.Equal("DRAFT",renewal.State);Assert.Contains("OPEN",renewal.AllowedTransitions!);Assert.DoesNotContain("VERIFY",renewal.AllowedTransitions!);
            // One open renewal case per certificate.
            await AssertCode(await Send(client,RenewalPath(),new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(),"INITIAL_AUTHORING")),
                HttpStatusCode.Conflict,"renewal_already_open");

            renewal=await Transition(client,renewal,"OPEN");Assert.Equal("PREPARING",renewal.State);renewal=await CurrentRenewal(client,renewal.Id);
            // UNKNOWN required actions block validation; the draft stays editable until it is complete.
            await Transition(client,renewal,"VALIDATE",new(SurveyStatus:"COMPLETE",DocumentsStatus:"NOT_REQUIRED",ActionsStatus:"COMPLETE"),HttpStatusCode.UnprocessableEntity,"renewal_requirements_unknown");
            using(var edit=await Send(client,RenewalPath(renewal.Id),new CertificateRenewalCommand(renewal.Id,renewal.Version,original.Id,original.Revision,original.Version,
                content with{RequiredActions=false},"AUTHORING_CORRECTION"),update:true))Assert.Equal(HttpStatusCode.OK,edit.StatusCode);
            renewal=await CurrentRenewal(client,renewal.Id);Assert.Equal("PREPARING",renewal.State);
            renewal=await CurrentRenewal(client,(await Transition(client,renewal,"VALIDATE",new(SurveyStatus:"COMPLETE",DocumentsStatus:"NOT_REQUIRED",ActionsStatus:"NOT_REQUIRED"))).Id);
            Assert.Equal("READY_TO_APPLY",renewal.State);Assert.False(renewal.CanEdit);
            await AssertCode(await Send(client,RenewalPath(renewal.Id),new CertificateRenewalCommand(renewal.Id,renewal.Version,original.Id,original.Revision,original.Version,content,"AUTHORING_CORRECTION"),update:true),
                HttpStatusCode.Conflict,"renewal_state_locked");
            renewal=await CurrentRenewal(client,(await Transition(client,renewal,"RECORD_APPLICATION",new(ApplicationDate:DateOnly.FromDateTime(DateTime.UtcNow),AuthorityReference:"FICTIONAL-APP-001"))).Id);
            await Transition(client,renewal,"CANCEL",expected:HttpStatusCode.Conflict,code:"renewal_cancellation_receipt_required");
            renewal=await CurrentRenewal(client,(await Transition(client,renewal,"AWAIT_AUTHORITY")).Id);
            var candidate=new CertificateRenewalDecision(NewCertificateId:replacement.Id,NewCertificateRevision:replacement.Revision,NewCertificateVersion:replacement.Version,NewDocument:replacement.Content.Document);
            renewal=await CurrentRenewal(client,(await Transition(client,renewal,"RECORD_ISSUED_CANDIDATE",candidate)).Id);
            Assert.Equal("ISSUED_UNVERIFIED",renewal.State);Assert.Equal(replacement.Content.Metadata.Declaration.IssueDate,renewal.Content.NewIssueDate);
            // A candidate never supersedes the original by itself.
            var stillCurrent=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(original.Id),Ct))!;Assert.Null(stillCurrent.SupersededBy);Assert.Equal("DRAFT",stillCurrent.State);

            // Verification is human-only, separately permissioned and never by the candidate recorder/author.
            await Transition(client,renewal,"VERIFY",Match(),HttpStatusCode.Forbidden,"permission_denied");
            RenewalActor(host.Services,author,"certificate.renewal.verify");await Token(client);
            await Transition(client,renewal,"VERIFY",Match(),HttpStatusCode.Conflict,"separation_of_duties");
            var verifier=Guid.NewGuid();RenewalActor(host.Services,verifier,"certificate.renewal.verify");await Token(client);
            await Transition(client,renewal,"VERIFY",Match() with{DateMatch="UNKNOWN"},HttpStatusCode.UnprocessableEntity,"renewal_verification_not_matched");
            renewal=await CurrentRenewal(client,(await Transition(client,renewal,"VERIFY",Match())).Id);Assert.Equal("VERIFIED",renewal.State);
            Assert.DoesNotContain("HUMAN_VERIFICATION_REQUIRED",renewal.Blockers);Assert.Contains("SOURCE_QUALIFICATION_REQUIRED",renewal.Blockers);Assert.Equal("REVIEW_REQUIRED",renewal.Qualification);
            RenewalActor(host.Services,verifier,"certificate.renewal.verify","certificate.renewal.complete");await Token(client);
            await Transition(client,renewal,"COMPLETE",expected:HttpStatusCode.Conflict,code:"separation_of_duties");
            RenewalActor(host.Services,Guid.NewGuid(),"certificate.renewal.complete");await Token(client);
            using(var auto=await Send(client,RenewalPath(renewal.Id,"/transitions"),new CertificateRenewalTransitionCommand(renewal.Id,renewal.Version,renewal.Revision,"COMPLETE",new()))) {
                Assert.Equal(HttpStatusCode.OK,auto.StatusCode);
                var completed=(await auto.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!;Assert.Equal("COMPLETED",completed.State);
            }
            var superseded=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(original.Id),Ct))!;
            Assert.Equal("SUPERSEDED",superseded.State);Assert.False(superseded.CanEdit);Assert.Equal(replacement.Id,superseded.SupersededBy!.CertificateId);
            // The original's history is preserved unchanged and now read-only.
            var history=(await client.GetFromJsonAsync<CertificateHistoryDto>("/api/v1/"+CertificatePath(original.Id)+"/history",Ct))!;
            Assert.Equal(original.Hash,Assert.Single(history.Items).Hash);
            RenewalActor(host.Services,author);CertificatePermissions(host.Services);await Token(client);
            await AssertCode(await Send(client,CertificatePath(original.Id),new CertificateSaveCommand(original.Id,superseded.Version,original.Content,"AUTHORING_CORRECTION"),update:true),
                HttpStatusCode.Conflict,"certificate_already_superseded");
            await AssertCode(await Send(client,RenewalPath(),new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(),"INITIAL_AUTHORING")),
                HttpStatusCode.Conflict,"certificate_already_superseded");
            var renewalHistory=(await client.GetFromJsonAsync<RenewalPageDto>("/api/v1/"+RenewalPath(renewal.Id,"/history"),Ct))!;
            Assert.Equal(new[]{"COMPLETED","VERIFIED","ISSUED_UNVERIFIED","AWAITING_AUTHORITY","APPLICATION_RECORDED","READY_TO_APPLY","PREPARING","PREPARING","DRAFT"},renewalHistory.Items.Select(r=>r.State));
            await using var db=Db();
            var supersession=await db.CertificateSupersessions.SingleAsync(s=>s.CertificateId==original.Id,Ct);supersession.EffectiveDate=supersession.EffectiveDate.AddDays(1);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
        }finally{Directory.Delete(root,true);}
    }
    private static CertificateRenewalDecision Match()=>new(IssuerMatch:"MATCH",NumberMatch:"MATCH",SubjectMatch:"MATCH",DateMatch:"MATCH",ScopeMatch:"MATCH");

    [Fact]
    [Trait("Requirement","DEV-044")]
    public async Task Renewal_transitions_enforce_version_idempotency_and_reject_ai_or_stale_commands() {
        await using var host=Host();using var client=Client(host);CertificatePermissions(host.Services);
        var author=host.Services.GetRequiredService<FederationSettings>().Memberships[0].ActorId;RenewalActor(host.Services,author);await Token(client);
        var original=await CertificateForRenewal(client);
        using var created=await Send(client,RenewalPath(),new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(),"INITIAL_AUTHORING"));
        var renewal=await CurrentRenewal(client,(await created.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!.Id);
        // Missing owner/due/expiry stay incomplete: POL-DEFAULT-01 blocks submission instead of guessing.
        await Transition(client,renewal,"OPEN",expected:HttpStatusCode.UnprocessableEntity,code:"renewal_owner_required");
        var command=new CertificateRenewalTransitionCommand(renewal.Id,renewal.Version,renewal.Revision,"CANCEL",new());var key=Guid.NewGuid().ToString("D");
        using var cancelled=await Send(client,RenewalPath(renewal.Id,"/transitions"),command,key);Assert.Equal(HttpStatusCode.OK,cancelled.StatusCode);var bytes=await cancelled.Content.ReadAsStringAsync(Ct);
        using(var replay=await Send(client,RenewalPath(renewal.Id,"/transitions"),command,key))Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        await AssertCode(await Send(client,RenewalPath(renewal.Id,"/transitions"),command with{Action="OPEN"},key),HttpStatusCode.Conflict,"idempotency_key_reused");
        await AssertCode(await Send(client,RenewalPath(renewal.Id,"/transitions"),command),HttpStatusCode.Conflict,"version_conflict");
        var closed=await CurrentRenewal(client,renewal.Id);Assert.Equal("CANCELLED",closed.State);Assert.Empty(closed.AllowedTransitions!);
        await Transition(client,closed,"OPEN",expected:HttpStatusCode.Conflict,code:"terminal_review");
        // A cancelled case no longer blocks a new one for the same certificate.
        using(var again=await Send(client,RenewalPath(),new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(),"INITIAL_AUTHORING")))
            Assert.Equal(HttpStatusCode.OK,again.StatusCode);
        // Facts outside their owning transition and AI identities are refused.
        await AssertCode(await Send(client,RenewalPath(closed.Id,"/transitions"),command with{ExpectedVersion=closed.Version,ExpectedRevision=closed.Revision,Decision=Match()}),HttpStatusCode.BadRequest,"validation_failed");
        var settings=host.Services.GetRequiredService<FederationSettings>();settings.Memberships[0]=settings.Memberships[0] with{ActorKind="AI",Permissions=[..settings.Memberships[0].Permissions,"certificate.renewal.verify"]};await Token(client);
        await AssertCode(await Send(client,RenewalPath(closed.Id,"/transitions"),command with{Action="VERIFY",ExpectedVersion=closed.Version,ExpectedRevision=closed.Revision,Decision=Match()}),HttpStatusCode.Forbidden,"human_actor_required");
        await using var db=Db();Assert.Equal(2,await db.CertificateRenewalRevisions.CountAsync(r=>r.RenewalId==renewal.Id,Ct));
    }
}
