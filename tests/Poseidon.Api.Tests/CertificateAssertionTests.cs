using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV043/T-S6-001: real owning HTTP/PG and scanned fixture bytes. The Human
// appointment is isolated test configuration, not a provisioned operational role.
public sealed partial class DirectoryWorkspaceTests
{
    private CertificateAssertionAuthority AssertionAppointment(IServiceProvider services,Guid certificate) {
        var settings=services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
        var grant=new CertificateAssertionAuthority(Guid.NewGuid(),SeedVessel.Id,certificate,Guid.Parse(member.RoleReferences![0]),"Auditor",
            VesselCertificatePolicy.SourceVersion,"SyntheticUnverified test assignment; no legal transition authority",DateTimeOffset.UtcNow.AddHours(-1),DateTimeOffset.UtcNow.AddHours(1));
        settings.Memberships[0]=member with{ActorKind="Human",Permissions=[..member.Permissions,"certificate.verify.record"],CertificateAssertionAuthorities=[grant]};return grant;
    }
    private async Task<CertificateDto> CertificateWithScannedFile(HttpClient client) {
        var stage=await StagePng(client);using var uploaded=await PutPng(client,stage.Id);Assert.Equal(HttpStatusCode.OK,uploaded.StatusCode);
        var file=(await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        var content=new VesselCertificateContent(new(new(Issuer:"FICTIONAL declared issuer",IssueDate:new(2020,1,1),ExpiryDate:new(2090,1,1)),DocumentReference:file.Id),
            DocumentName:"FICTIONAL assertion fixture",Document:new(file.Id,file.Revision!.Value,file.Checksum!));
        using var saved=await Send(client,CertificatePath(),new CertificateSaveCommand(Guid.NewGuid(),0,content,"INITIAL_AUTHORING"));
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);return(await saved.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
    }
    private string AssertionPath(Guid certificate)=>CertificatePath(certificate)+"/verification-assertions";
    [Fact]
    public async Task Assigned_Human_assertion_pins_exact_file_revision_and_records_without_legal_qualification_or_supersession() {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-assertion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);await Token(client);var certificate=await CertificateWithScannedFile(client);
            var grant=AssertionAppointment(host.Services,certificate.Id);var command=new CertificateAssertionCommand(certificate.Version,certificate.Revision,0,grant.Id,"Verified","FICTIONAL assigned assertion; legal transition remains unavailable");
            var key=Guid.NewGuid().ToString("D");using var recorded=await Send(client,AssertionPath(certificate.Id),command,key);Assert.Equal(HttpStatusCode.OK,recorded.StatusCode);
            var bytes=await recorded.Content.ReadAsStringAsync(Ct);var assertion=(await recorded.Content.ReadFromJsonAsync<CertificateVerificationDto>(Ct))!;
            Assert.False(assertion.Current);Assert.Equal("Verified",assertion.State);Assert.Equal("RECORDED_UNASSESSED",assertion.Qualification);
            Assert.Equal(certificate.Version,assertion.CertificateVersion);Assert.Equal(certificate.Content.Document!.Checksum,assertion.Checksum);
            var current=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(certificate.Id),Ct))!;
            Assert.True(current.Verification!.Current);Assert.Equal(assertion.Id,current.Verification.Id);Assert.Equal("REVIEW_REQUIRED",current.Qualification);Assert.Equal("DRAFT",current.State);
            Assert.Null(current.SupersededBy);Assert.Equal(certificate.Version,current.Version);
            using var replay=await Send(client,AssertionPath(certificate.Id),command,key);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
            Assert.Equal(HttpStatusCode.Conflict,(await Send(client,AssertionPath(certificate.Id),command with{State="Suspicious"},key)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await Send(client,AssertionPath(certificate.Id),command)).StatusCode);
            await using var db=Db();Assert.Single(await db.CertificateAssertionEvents.Where(e=>e.CertificateId==certificate.Id).ToArrayAsync(Ct));
            var receiptAudit=await db.AuditEvents.Where(e=>e.ResourceId==certificate.Id && e.ResourceKind=="CertificateReceipt").OrderByDescending(e=>e.OccurredAt).FirstAsync(Ct);
            Assert.Equal(assertion.Version,receiptAudit.AfterVersion);Assert.Equal(assertion.Revision,receiptAudit.AfterRevision);
            Assert.DoesNotContain("FICTIONAL",JsonSerializer.Serialize(await db.AuditEvents.Where(e=>e.ResourceId==certificate.Id).ToArrayAsync(Ct)));
            var immutable=await db.CertificateAssertionEvents.SingleAsync(e=>e.Id==assertion.Id,Ct);immutable.State="Suspicious";
            await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Assertion_appointment_identity_is_immutable_but_a_valid_extension_keeps_exact_receipt_recovery() {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-assertion-binding-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);await Token(client);var certificate=await CertificateWithScannedFile(client);var grant=AssertionAppointment(host.Services,certificate.Id);
            var settings=host.Services.GetRequiredService<FederationSettings>();var command=new CertificateAssertionCommand(certificate.Version,certificate.Revision,0,grant.Id,"Not Verified","FICTIONAL assignment binding test");var key=Guid.NewGuid().ToString("D");
            using var first=await Send(client,AssertionPath(certificate.Id),command,key);Assert.Equal(HttpStatusCode.OK,first.StatusCode);var bytes=await first.Content.ReadAsStringAsync(Ct);
            var member=settings.Memberships[0];settings.Memberships[0]=member with{CertificateAssertionAuthorities=[grant with{ValidUntil=grant.ValidUntil.AddHours(2)}]};
            var extended=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(certificate.Id),Ct))!;Assert.True(extended.Verification!.Current);
            using var recovered=await Send(client,AssertionPath(certificate.Id),command,key);Assert.Equal(bytes,await recovered.Content.ReadAsStringAsync(Ct));
            settings.Memberships[0]=member with{CertificateAssertionAuthorities=[grant with{AssignmentReference="Different synthetic assignment"}]};
            var replaced=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(certificate.Id),Ct))!;
            Assert.False(replaced.Verification!.Current);Assert.Equal(extended.Verification.AuthorityHash,replaced.Verification.AuthorityHash);Assert.Equal("Not Verified",replaced.Verification.State);
            settings.Memberships[0]=member with{CertificateAssertionAuthorities=[]};
            Assert.Equal(HttpStatusCode.Forbidden,(await Send(client,AssertionPath(certificate.Id),command,key)).StatusCode);
            var retained=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(certificate.Id),Ct))!;Assert.False(retained.Verification!.Current);
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("Unspecified")][InlineData("AI")][InlineData("wrong-role")][InlineData("foreign-certificate")][InlineData("source-unavailable")]
    public async Task Assertion_requires_exact_assigned_Human_and_never_uses_admin_or_client_role_fallback(string fault) {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-assertion-deny-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);await Token(client);var certificate=await CertificateWithScannedFile(client);var grant=AssertionAppointment(host.Services,certificate.Id);
            var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
            settings.Memberships[0]=member with{ActorKind=fault is "AI" or "Unspecified"?fault:"Human",CertificateAssertionAuthorities=[grant with{
                RoleId=fault=="wrong-role"?Guid.NewGuid():grant.RoleId,CertificateId=fault=="foreign-certificate"?Guid.NewGuid():grant.CertificateId,
                SourceVersion=fault=="source-unavailable"?"unqualified source":grant.SourceVersion}]};
            using var denied=await Send(client,AssertionPath(certificate.Id),new CertificateAssertionCommand(certificate.Version,certificate.Revision,0,grant.Id,"Verified","FICTIONAL denied assertion"));
            Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);await using var db=Db();Assert.Empty(await db.CertificateAssertionEvents.ToArrayAsync(Ct));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Assertion_current_read_rechecks_immutable_assignment_identity_after_its_success_audit(bool replaceAssignment) {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-assertion-read-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);await Token(client);var certificate=await CertificateWithScannedFile(client);var grant=AssertionAppointment(host.Services,certificate.Id);
            using var saved=await Send(client,AssertionPath(certificate.Id),new CertificateAssertionCommand(certificate.Version,certificate.Revision,0,grant.Id,"Verified","FICTIONAL read fence"));Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
            var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];
            var barrier=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
            await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",barrier);await acquire.ExecuteNonQueryAsync(Ct);}
            await Sql($$"""
                CREATE FUNCTION audit.s6_pause_assertion_read() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN PERFORM pg_advisory_xact_lock({{barrier}}::bigint);RETURN NEW;END $body$;
                CREATE TRIGGER s6_pause_assertion_read BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
                  WHEN (NEW."Action"='CertificateRead' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_assertion_read();
                """);
            var pending=client.GetAsync("/api/v1/"+CertificatePath(certificate.Id),Ct);HttpResponseMessage? response=null;
            try {
                await WaitForComplianceAuditLock(barrier);
                settings.Memberships[0]=member with{CertificateAssertionAuthorities=[replaceAssignment?grant with{AssignmentReference="Different synthetic appointment"}:grant with{ValidUntil=grant.ValidUntil.AddHours(1)}]};
                await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",barrier);await release.ExecuteNonQueryAsync(Ct);}
                response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(replaceAssignment?HttpStatusCode.Forbidden:HttpStatusCode.OK,response.StatusCode);
                if(replaceAssignment){var body=await response.Content.ReadAsStringAsync(Ct);Assert.DoesNotContain("authorityHash",body);Assert.DoesNotContain("FICTIONAL",body);}
                else Assert.True((await response.Content.ReadFromJsonAsync<CertificateDto>(Ct))!.Verification!.Current);
            }finally {
                await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",barrier);await release.ExecuteNonQueryAsync(Ct);}
                response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
                await Sql("""DROP TRIGGER s6_pause_assertion_read ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_assertion_read();""");
            }
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData("appointment")][InlineData("file-read")]
    public async Task Assertion_postcommit_withdrawal_during_success_audit_withholds_receipt_without_rolling_back_committed_event(string withdrawal) {
        var root=Path.Combine(Path.GetTempPath(),"poseidon-s6-assertion-fence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            using var scanner=new ScanPeer("stream: OK\0",repeat:true);await using var host=FileHost(root,scanner.Port);using var client=Client(host);
            FilePermissions(host.Services);CertificatePermissions(host.Services);await Token(client);var certificate=await CertificateWithScannedFile(client);var grant=AssertionAppointment(host.Services,certificate.Id);
            var settings=host.Services.GetRequiredService<FederationSettings>();var member=settings.Memberships[0];var key=Guid.NewGuid().ToString("D");
            var command=new CertificateAssertionCommand(certificate.Version,certificate.Revision,0,grant.Id,"Verified","FICTIONAL exact outcome recovery");
            var barrier=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
            await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",barrier);await acquire.ExecuteNonQueryAsync(Ct);}
            await Sql($$"""
                CREATE FUNCTION audit.s6_pause_assertion_outcome() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN PERFORM pg_advisory_xact_lock({{barrier}}::bigint);RETURN NEW;END $body$;
                CREATE TRIGGER s6_pause_assertion_outcome BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
                  WHEN (NEW."Action"='CertificateCommand' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_assertion_outcome();
                """);
            var pending=Send(client,AssertionPath(certificate.Id),command,key);HttpResponseMessage? response=null;
            try {
                await WaitForComplianceAuditLock(barrier);
                settings.Memberships[0]=withdrawal=="appointment"?member with{CertificateAssertionAuthorities=[]}:member with{Permissions=member.Permissions.Where(p=>p!="file.read").ToArray()};
                await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",barrier);await release.ExecuteNonQueryAsync(Ct);}
                response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
                var body=await response.Content.ReadAsStringAsync(Ct);Assert.Contains("command_outcome_withheld",body);Assert.DoesNotContain("authorityHash",body);Assert.DoesNotContain("FICTIONAL",body);
            }finally {
                await using(var release=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){release.Parameters.AddWithValue("key",barrier);await release.ExecuteNonQueryAsync(Ct);}
                response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();
                await Sql("""DROP TRIGGER s6_pause_assertion_outcome ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_assertion_outcome();""");
            }
            await using var db=Db();Assert.Single(await db.CertificateAssertionEvents.Where(e=>e.CertificateId==certificate.Id).ToArrayAsync(Ct));
            var receipt=await db.CertificateCommandReceipts.SingleAsync(r=>r.CertificateId==certificate.Id && r.Action=="CertificateAssertion",Ct);
            settings.Memberships[0]=member with{CertificateAssertionAuthorities=[grant with{ValidUntil=grant.ValidUntil.AddHours(1)}]};
            using var recovered=await Send(client,AssertionPath(certificate.Id),command,key);Assert.Equal(HttpStatusCode.OK,recovered.StatusCode);Assert.Equal(receipt.OutcomeJson,await recovered.Content.ReadAsStringAsync(Ct));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
