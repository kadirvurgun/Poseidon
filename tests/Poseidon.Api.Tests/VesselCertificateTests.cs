using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV043/T-S6-001 owning API + PostgreSQL source. Execution deferred.
public sealed partial class DirectoryWorkspaceTests
{
    private static void CertificatePermissions(IServiceProvider services,bool write=true) {
        var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"certificate.read",write?"certificate.write":"unused"]};
    }
    private string CertificatePath(Guid? id=null,Guid? vessel=null)=>$"vessels/{vessel??SeedVessel.Id}/certificates"+(id==null?"":"/"+id);
    [Fact]
    public async Task Certificate_owning_save_has_exact_receipt_actual_MVCC_history_and_no_temporal_qualification_inference() {
        await using var host=Host();using var client=Client(host);CertificatePermissions(host.Services);await Token(client);
        var command=new CertificateSaveCommand(Guid.NewGuid(),0,new(new(new(Issuer:"FICTIONAL declared issuer",IssueDate:new(2020,1,1),ExpiryDate:new(2090,1,1))),DocumentName:"FICTIONAL certificate"),"INITIAL_AUTHORING");
        var key=Guid.NewGuid().ToString("D");using var created=await Send(client,CertificatePath(),command,key);Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var bytes=await created.Content.ReadAsStringAsync(Ct);var row=(await created.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
        Assert.False(row.CurrentRevision);Assert.False(row.CanEdit);Assert.Equal("REVIEW_REQUIRED",row.Qualification);Assert.Equal("DRAFT",row.State);Assert.Equal("VALID",row.Calendar.State);
        Assert.Null(row.SupersededBy);using var repeated=await Send(client,CertificatePath(),command,key);Assert.Equal(bytes,await repeated.Content.ReadAsStringAsync(Ct));
        var current=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(row.Id),Ct))!;Assert.True(current.CurrentRevision);Assert.True(current.CanEdit);
        var update=command with{ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION",Content=command.Content with{DocumentName="FICTIONAL corrected"}};
        using var changed=await Send(client,CertificatePath(row.Id),update,update:true);Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
        var next=(await changed.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;Assert.Equal(2,next.Revision);Assert.NotEqual(row.Version,next.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(client,CertificatePath(row.Id),update,update:true)).StatusCode);
        using var oldReplay=await Send(client,CertificatePath(),command,key);Assert.Equal(bytes,await oldReplay.Content.ReadAsStringAsync(Ct));
        var history=(await client.GetFromJsonAsync<CertificateHistoryDto>("/api/v1/"+CertificatePath(row.Id)+"/history",Ct))!;
        Assert.Equal(2,history.Items.Length);Assert.Equal(row.Version,history.Items.Single(r=>r.Revision==1).Version);Assert.False(history.Items.Single(r=>r.Revision==1).CurrentRevision);
        await using var db=Db();Assert.Equal(2,await db.CertificateCommandReceipts.CountAsync(r=>r.CertificateId==row.Id,Ct));
        Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(await db.AuditEvents.Where(a=>a.ResourceId==row.Id).ToArrayAsync(Ct)));
        var immutable=await db.VesselCertificateRevisions.SingleAsync(r=>r.CertificateId==row.Id && r.Revision==1,Ct);immutable.ContentJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Center_filters_calendar_before_paging_and_never_turns_missing_crew_or_critical_source_into_zero_gaps() {
        await using var host=Host();using var client=Client(host);CertificatePermissions(host.Services);await Token(client);
        foreach(var expiry in new[]{DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),new DateOnly(2001,1,1)}) {
            var command=new CertificateSaveCommand(Guid.NewGuid(),0,new(new(new(IssueDate:new(2000,1,1),ExpiryDate:expiry))),"INITIAL_AUTHORING");
            Assert.Equal(HttpStatusCode.OK,(await Send(client,CertificatePath(),command)).StatusCode);
        }
        var center=(await client.GetFromJsonAsync<CertificateCenterDto>($"/api/v1/certificate-command-center?vesselId={SeedVessel.Id}&scope=All&temporalState=EXPIRED",Ct))!;
        Assert.Single(center.Items);Assert.Equal(1,center.Counts.Expired);Assert.Equal(0,center.Counts.ReviewRequired);Assert.Equal("REVIEW_REQUIRED",center.Items[0].Qualification);Assert.Null(center.CriticalGapCount);
        Assert.Equal("AUTHORIZED_PAGE_ONLY",center.Coverage);Assert.Equal("UNAVAILABLE_POLICY_OR_AUTHORITY",center.CrewCoverage);Assert.Equal("REVIEW_REQUIRED",center.State);
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/v1/certificate-command-center?vesselId={foreign.Id}",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/v1/certificate-command-center?vesselId={SeedVessel.Id}&scope=Crew&purpose=unapproved",Ct)).StatusCode);
    }
    [Theory]
    [InlineData("head")][InlineData("role")]
    public async Task Certificate_current_response_withholds_a_snapshot_changed_during_success_audit(string change) {
        await using var host=Host();using var client=Client(host);using var writer=Client(host);CertificatePermissions(host.Services);await Token(client);await Token(writer);
        var command=new CertificateSaveCommand(Guid.NewGuid(),0,new(new(new()),DocumentName:"FICTIONAL private snapshot"),"INITIAL_AUTHORING");
        using var saved=await Send(writer,CertificatePath(),command);var row=(await saved.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
        var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
        await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
        await Sql($$"""
            CREATE FUNCTION audit.s6_pause_certificate_read() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
            CREATE TRIGGER s6_pause_certificate_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
              WHEN (NEW."Action"='CertificateRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_certificate_read();
            """);
        var pending=client.GetAsync("/api/v1/"+CertificatePath(row.Id),Ct);HttpResponseMessage? response=null;
        try {
            await WaitForComplianceAuditLock(key);
            if(change=="head")Assert.Equal(HttpStatusCode.OK,(await Send(writer,CertificatePath(row.Id),command with{ExpectedVersion=row.Version,ReasonCode="AUTHORING_CORRECTION"},update:true)).StatusCode);
            else {var settings=host.Services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];settings.Memberships[0]=m with{Permissions=m.Permissions.Where(p=>p!="certificate.read").ToArray()};}
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("FICTIONAL",await response.Content.ReadAsStringAsync(Ct));
        }finally {
            await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",key);await release.ExecuteNonQueryAsync(Ct);}
            response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
            await Sql("""DROP TRIGGER s6_pause_certificate_read ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_certificate_read();""");
        }
    }
    [Fact]
    public async Task Synthetic_certificate_fixture_adds_missing_incomplete_data_only_and_preserves_existing_revisions() {
        await using var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:certificate-fixture","System","certificate-fixture"),administrativeImport:true);
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await VesselCertificateFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
        var id=VesselCertificateFixtures.Id(SeedVessel.Id);var row=await db.VesselCertificates.SingleAsync(c=>c.Id==id,Ct);var version=row.Version;
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await VesselCertificateFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
        await db.Entry(row).ReloadAsync(Ct);Assert.Equal(version,row.Version);Assert.Equal(1,row.Revision);
        var revision=await db.VesselCertificateRevisions.SingleAsync(c=>c.CertificateId==id,Ct);Assert.Equal("SyntheticUnverified",revision.Provenance);Assert.Null(revision.FileId);Assert.Null(revision.ExpiryDate);
        Assert.Empty(await db.CertificateCommandReceipts.ToArrayAsync(Ct));
    }
}
