using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Xunit;
namespace Poseidon.Api.Tests;
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Tug_audit_revision_drills_into_exact_saved_facts_without_free_text_and_rechecks_original_session_rights() {
        using var host=Host(tugRead:true,tugWrite:true,auditRead:true);using var client=ImportSessionClient(host);var session=await ImportSession(client);
        var command=Draft() with { Inputs=Draft().Inputs with { Notes="SYNTHETIC PRIVATE AUDIT NOTE", CustomerRef="SYNTHETIC PRIVATE CUSTOMER" } };
        var created=await Send(client,TugUrl,command);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var first=(await created.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        var next=command with {BaseVersion=first.Version,Inputs=command.Inputs with {RequiredBollardPull="46.02"}};
        Assert.Equal(HttpStatusCode.OK,(await Send(client,$"tug-jobs/{command.ClientId}?vessel={Tug.Id}",next,update:true)).StatusCode);
        Guid auditId;
        await using(var db=Db()) auditId=await db.AuditEvents.Where(x=>x.EntityType=="TugJobRevision"&&x.ResourceId==command.ClientId&&x.AfterRevision==1).Select(x=>x.Id).SingleAsync(Ct);
        var record=(await client.GetFromJsonAsync<AuditRecordDto>($"/api/v1/audit/{auditId}",Ct))!;
        Assert.Equal($"/api/v1/audit/{auditId}/revision",record.RevisionHref);
        var response=await client.GetAsync(record.RevisionHref,Ct);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var evidence=(await response.Content.ReadFromJsonAsync<AuditEvidenceDto>(Ct))!;Assert.Equal(1,evidence.Tug!.Revision);Assert.Equal("45.01",evidence.Tug.RequiredBollardPull);
        var json=await response.Content.ReadAsStringAsync(Ct);Assert.DoesNotContain("SYNTHETIC PRIVATE",json);Assert.DoesNotContain("READY",json);
        using var deniedHost=Host(tugRead:false,auditRead:true);using var denied=ImportSessionClient(deniedHost,session.Cookie,session.Csrf);
        Assert.Equal(HttpStatusCode.NotFound,(await denied.GetAsync(record.RevisionHref,Ct)).StatusCode);
    }
}
