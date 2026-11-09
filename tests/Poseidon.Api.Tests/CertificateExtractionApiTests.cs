using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV045/T-S6-003: real owning HTTP, accepted synthetic file bytes, current
// PostgreSQL migrations and audited writer. Peer proves protocol only, not OCR
// quality, deployed malware scanning or an approved processing arrangement.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class ExtractionPeer:HttpMessageHandler,IHttpClientFactory {
        internal int Requests;internal Func<CancellationToken,Task>? BeforeReply;
        internal CertificateExtractionSettings Policy=null!;
        public HttpClient CreateClient(string name)=>new(this,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            Interlocked.Increment(ref Requests);
            var parts=Assert.IsType<MultipartFormDataContent>(request.Content);
            var document=parts.Single(p=>p.Headers.ContentDisposition?.Name?.Trim('"')=="document");
            Assert.Equal(Png,await document.ReadAsByteArrayAsync(ct));
            if(BeforeReply!=null)await BeforeReply(ct);
            var raw=new CertificateRawExtraction(CertificateExtractionPolicy.Protocol,Policy.ModelVersion!,Policy.PromptVersion!,Convert.ToHexString(SHA256.HashData(Png)),
                new(){["certificate_no"]=new("MASKED-EXTRACTED-123",0.91m,1),["issuer"]=new("MASKED-UNCONFIRMED-ISSUER",0.2m,null)});
            return new(HttpStatusCode.OK){Content=JsonContent.Create(raw)};
        }
    }
    private CertificateExtractionSettings ExtractionPolicy(string secret)=>new(){Enabled=true,Endpoint="https://synthetic-extraction.invalid/v1/extract",AllowedHost="synthetic-extraction.invalid",SecretReference=secret,
        PolicyVersion="synthetic-unapproved-policy-v1",ProcessingReference="FICTIONAL masked protocol fixture only",StorageReference="FICTIONAL no external activation",Region="FICTIONAL fixture region",
        PrivacyReference="FICTIONAL synthetic data",RetentionReference="FICTIONAL fixture lifetime",ConfidenceReference="FICTIONAL review threshold",PersonalProcessingReference="FICTIONAL masked crew fixture only",
        SourceVersion=CertificateExtractionPolicy.SourceVersion,ModelVersion="synthetic-model-v1",PromptVersion="synthetic-prompt-v1",LowConfidenceThreshold=0.8m,TimeoutSeconds=30,ReviewSeconds=60,MaxBytes=4096,MaxResponseBytes=4096,
        Scopes=[new(SeedVessel.TenantId,actorId,SeedVessel.Id,"Vessel"),new(SeedVessel.TenantId,actorId,SeedVessel.Id,"Crew")]};
    private WebApplicationFactory<Program> ExtractionHost(string root,int scanner,CertificateExtractionSettings policy,ExtractionPeer peer,bool crew=false) =>
        (crew?PersonFileHost(root,scanner):FileHost(root,scanner)).WithWebHostBuilder(b=>b.ConfigureServices(s=>{
            s.AddSingleton(policy);s.AddSingleton(sp=>new CertificateExtractionProvider(peer,sp.GetRequiredService<IHostEnvironment>()));
        }));
    private void ExtractionPermissions(IServiceProvider services,bool crew=false) {
        if(crew)PersonFilePermissions(services);else FilePermissions(services);
        CertificatePermissions(services);var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{Permissions=[..m.Permissions,"certificate.extract"],ActorKind="Human"};
    }
    private async Task<CertificateDto> ExtractionSource(HttpClient client) {
        var staged=await StagePng(client);using var accepted=await PutPng(client,staged.Id);Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);
        var file=(await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;var fileRevision=Assert.IsType<long>(file.Revision);var fileChecksum=Assert.IsType<string>(file.Checksum);
        var metadata=new CertificateMetadata(new(CertificateTypeReference:"MASKED-TYPE",CertificateNumber:"MASKED-OLD",Issuer:"MASKED-ORIGINAL-ISSUER"),DocumentReference:file.Id,Notes:"MASKED-PRESERVED-NOTE");
        var body=new CertificateSaveCommand(Guid.NewGuid(),0,new(metadata,DocumentName:"SYNTHETIC accepted PNG",Document:new(file.Id,fileRevision,fileChecksum)),"INITIAL_AUTHORING");
        using var saved=await Send(client,CertificatePath(),body);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);return (await saved.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
    }
    private string ExtractionPath(CertificateDto row)=>CertificatePath(row.Id)+"/extractions";
    private async Task<CertificateExtractionDto> ExtractSource(HttpClient client,CertificateDto row) {
        using var response=await Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CertificateExtractionDto>(Ct))!;
    }
    private static CertificateExtractConfirm ConfirmFacts(CertificateExtractionDto preview)=>new(preview.Version,preview.ReviewContext!,[new("certificate_no","MASKED-CORRECTED-456",true)],true);
    private static string ExtractionSecret() {var name="POSEIDON_TEST_EXTRACTION_"+Guid.NewGuid().ToString("N").ToUpperInvariant();Environment.SetEnvironmentVariable(name,"fictional-protocol-secret-only");return name;}
    private static string ExtractionRoot() {var root=Path.Combine(Path.GetTempPath(),"poseidon-extraction-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
    private static void RemoveExtractionFiles(string root) {foreach(var file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root,false);}

    [Fact]
    public async Task Extraction_is_nonmutating_and_only_explicit_selected_facts_save_through_current_owning_writer_with_exact_receipt() {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);
            var original=await ExtractionSource(client);await using var db=Db();var audits=await db.AuditEvents.CountAsync(Ct);
            var preview=await ExtractSource(client,original);Assert.True(preview.CanConfirm);Assert.NotNull(preview.ReviewContext);Assert.Equal("REVIEW_REQUIRED",preview.State);
            Assert.All(preview.Candidates,f=>Assert.Equal("REVIEW_REQUIRED",f.State));Assert.True(preview.Candidates.Single(f=>f.Key=="issuer").LowConfidence);
            Assert.Contains("SCOPED_REFERENCE_UNMATCHED",preview.Candidates.Single(f=>f.Key=="vessel_reference").Reasons);
            Assert.Equal(audits,await db.AuditEvents.CountAsync(Ct));Assert.Equal(1,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==original.Id,Ct));
            Assert.Equal(1,await db.CertificateCommandReceipts.CountAsync(r=>r.CertificateId==original.Id,Ct));
            using var uncheckedFact=await Send(client,ExtractionPath(original)+"/confirm",ConfirmFacts(preview) with{Facts=[new("certificate_no","MASKED-CORRECTED-456",false)]});Assert.Equal(HttpStatusCode.BadRequest,uncheckedFact.StatusCode);
            var command=ConfirmFacts(preview);var key=Guid.NewGuid().ToString("D");using var applied=await Send(client,ExtractionPath(original)+"/confirm",command,key);Assert.Equal(HttpStatusCode.OK,applied.StatusCode);
            var bytes=await applied.Content.ReadAsStringAsync(Ct);var saved=(await applied.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
            Assert.Equal(2,saved.Revision);Assert.False(saved.CurrentRevision);Assert.False(saved.CanEdit);Assert.Equal("REVIEW_REQUIRED",saved.Qualification);
            Assert.Equal("MASKED-CORRECTED-456",saved.Content.Metadata.Declaration.CertificateNumber);Assert.Equal("MASKED-ORIGINAL-ISSUER",saved.Content.Metadata.Declaration.Issuer);
            Assert.Equal(original.Content.Metadata.Notes,saved.Content.Metadata.Notes);Assert.Equal(original.Content.Document,saved.Content.Document);
            using var duplicate=await Send(client,ExtractionPath(original)+"/confirm",command,key);Assert.Equal(HttpStatusCode.OK,duplicate.StatusCode);Assert.Equal(bytes,await duplicate.Content.ReadAsStringAsync(Ct));
            Assert.Equal(2,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==original.Id,Ct));Assert.Equal(2,await db.CertificateCommandReceipts.CountAsync(r=>r.CertificateId==original.Id,Ct));
            var businessAudit=JsonSerializer.Serialize(await db.AuditEvents.Where(e=>e.ResourceId==original.Id).ToArrayAsync(Ct));Assert.DoesNotContain("MASKED",businessAudit);
            Assert.Equal(1,peer.Requests);
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Theory]
    [InlineData("disabled")][InlineData("privacy")][InlineData("scope")][InlineData("permission")][InlineData("bytes")]
    public async Task Default_policy_source_scope_permission_and_actual_byte_integrity_fail_before_external_processing(string boundary) {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);var row=await ExtractionSource(client);
            if(boundary=="disabled")p.Enabled=false;if(boundary=="privacy")p.PrivacyReference=null;if(boundary=="scope")p.Scopes=[];
            if(boundary=="permission"){var f=host.Services.GetRequiredService<FederationSettings>();f.Memberships[0]=f.Memberships[0] with{Permissions=f.Memberships[0].Permissions.Where(x=>x!="file.read").ToArray()};}
            if(boundary=="bytes")await File.WriteAllBytesAsync(Path.Combine(root,SeedVessel.TenantId.ToString("N")+"-"+row.Content.Document!.Id.ToString("N")+".object"),[1,2,3],Ct);
            using var response=await Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable);
            Assert.Equal(0,peer.Requests);Assert.DoesNotContain("MASKED",await response.Content.ReadAsStringAsync(Ct));
            await using var db=Db();Assert.Equal(1,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==row.Id,Ct));
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Theory]
    [InlineData("permission")][InlineData("file")][InlineData("processing")]
    public async Task Current_source_and_authority_are_reloaded_after_provider_await_and_no_private_candidate_or_token_escapes(string boundary) {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        peer.BeforeReply=async ct=>{entered.TrySetResult();await release.Task.WaitAsync(ct);};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);var row=await ExtractionSource(client);
            var request=Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));await entered.Task.WaitAsync(TimeSpan.FromSeconds(15),Ct);
            if(boundary=="permission"){var f=host.Services.GetRequiredService<FederationSettings>();f.Memberships[0]=f.Memberships[0] with{Permissions=f.Memberships[0].Permissions.Where(x=>x!="certificate.extract").ToArray()};}
            if(boundary=="file"){await using var db=Db();var stage=await db.FileStages.SingleAsync(s=>s.Id==row.Content.Document!.Id,Ct);stage.State="Pending";await db.SaveChangesAsync(Ct);}
            if(boundary=="processing")p.PrivacyReference=null;
            release.TrySetResult();using var response=await request.WaitAsync(TimeSpan.FromSeconds(15),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
            var bytes=await response.Content.ReadAsStringAsync(Ct);Assert.DoesNotContain("MASKED",bytes);Assert.DoesNotContain("reviewContext",bytes);Assert.DoesNotContain(row.Content.Document!.Id.ToString(),bytes);
            using var next=await Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));Assert.False(next.IsSuccessStatusCode);Assert.Equal(1,peer.Requests);
        }finally{release.TrySetResult();Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Fact]
    public async Task Fresh_fence_after_security_audit_withholds_candidates_and_next_request_denies_same_stale_claims() {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);var row=await ExtractionSource(client);
            var key=(long)Random.Shared.Next(1,int.MaxValue);await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
            await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
            await Sql($$"""
                CREATE FUNCTION audit.s6_pause_extraction() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
                CREATE TRIGGER s6_pause_extraction BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
                  WHEN (NEW."Action"='CertificateExtraction' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_extraction();
                """);
            var pending=Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));HttpResponseMessage? response=null;
            try {
                await WaitForComplianceAuditLock(key);var f=host.Services.GetRequiredService<FederationSettings>();f.Memberships[0]=f.Memberships[0] with{Permissions=f.Memberships[0].Permissions.Where(x=>x!="file.read").ToArray()};
                await using(var unlock=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){unlock.Parameters.AddWithValue("key",key);await unlock.ExecuteNonQueryAsync(Ct);}
                response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.DoesNotContain("MASKED",await response.Content.ReadAsStringAsync(Ct));
                using var next=await Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));Assert.Equal(HttpStatusCode.Forbidden,next.StatusCode);Assert.Equal(1,peer.Requests);
            }finally {
                await using(var unlock=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){unlock.Parameters.AddWithValue("key",key);await unlock.ExecuteNonQueryAsync(Ct);}
                response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER s6_pause_extraction ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_extraction();""");
            }
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Fact]
    public async Task Unconfirmed_scope_authority_fields_stale_version_and_foreign_vessel_never_mutate_an_owning_certificate() {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);var row=await ExtractionSource(client);var preview=await ExtractSource(client,row);
            foreach(var command in new[]{ConfirmFacts(preview) with{VesselContextConfirmed=false},ConfirmFacts(preview) with{Facts=[new("verified","true",true)]},ConfirmFacts(preview) with{Facts=[new("vessel_reference",Guid.NewGuid().ToString(),true)]}}) {
                using var response=await Send(client,ExtractionPath(row)+"/confirm",command);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            }
            using var stale=await Send(client,ExtractionPath(row)+"/confirm",ConfirmFacts(preview) with{ExpectedVersion=row.Version+1});Assert.False(stale.IsSuccessStatusCode);
            var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);using var cross=await Send(client,CertificatePath(row.Id,foreign.Id)+"/extractions/confirm",ConfirmFacts(preview));Assert.False(cross.IsSuccessStatusCode);
            var f=host.Services.GetRequiredService<FederationSettings>();f.Memberships[0].ActorKind="System";
            using var nonhuman=await Send(client,ExtractionPath(row)+"/confirm",ConfirmFacts(preview));Assert.Equal(HttpStatusCode.Forbidden,nonhuman.StatusCode);
            await using var db=Db();Assert.Equal(1,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==row.Id,Ct));Assert.Equal(1,await db.CertificateCommandReceipts.CountAsync(r=>r.CertificateId==row.Id,Ct));
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Fact]
    public async Task Committed_confirmation_withheld_after_audit_recovers_only_exact_receipt_after_original_authorization_returns() {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);var row=await ExtractionSource(client);
            var preview=await ExtractSource(client,row);var command=ConfirmFacts(preview);var commandKey=Guid.NewGuid().ToString("D");var key=(long)Random.Shared.Next(1,int.MaxValue);
            await using var blocker=new NpgsqlConnection(postgres.GetConnectionString());await blocker.OpenAsync(Ct);
            await using(var acquire=new NpgsqlCommand("SELECT pg_advisory_lock(@key)",blocker)){acquire.Parameters.AddWithValue("key",key);await acquire.ExecuteNonQueryAsync(Ct);}
            await Sql($$"""
                CREATE FUNCTION audit.s6_pause_extraction_confirm() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN PERFORM pg_advisory_xact_lock({{key}}::bigint);RETURN NEW;END $body$;
                CREATE TRIGGER s6_pause_extraction_confirm BEFORE INSERT ON audit."SecurityEvents" FOR EACH ROW
                  WHEN (NEW."Action"='CertificateExtraction' AND NEW."Result"='Success') EXECUTE FUNCTION audit.s6_pause_extraction_confirm();
                """);
            var pending=Send(client,ExtractionPath(row)+"/confirm",command,commandKey);HttpResponseMessage? response=null;
            try {
                await WaitForComplianceAuditLock(key);var f=host.Services.GetRequiredService<FederationSettings>();var original=f.Memberships[0];
                f.Memberships[0]=original with{Permissions=original.Permissions.Where(x=>x!="file.read").ToArray()};
                await using(var unlock=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){unlock.Parameters.AddWithValue("key",key);await unlock.ExecuteNonQueryAsync(Ct);}
                response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
                Assert.Contains("command_outcome_withheld",await response.Content.ReadAsStringAsync(Ct));Assert.DoesNotContain("MASKED",await response.Content.ReadAsStringAsync(Ct));
                await using var db=Db();Assert.Equal(2,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==row.Id,Ct));
                var stored=await db.CertificateCommandReceipts.AsNoTracking().SingleAsync(r=>r.CertificateId==row.Id && r.Key==commandKey,Ct);
                using var denied=await Send(client,ExtractionPath(row)+"/confirm",command,commandKey);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
                f.Memberships[0]=original;using var retry=await Send(client,ExtractionPath(row)+"/confirm",command,commandKey);
                Assert.Equal(HttpStatusCode.OK,retry.StatusCode);Assert.Equal(stored.OutcomeJson,await retry.Content.ReadAsStringAsync(Ct));
                Assert.Equal(2,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==row.Id,Ct));Assert.Equal(1,peer.Requests);
            }finally {
                await using(var unlock=new NpgsqlCommand("SELECT pg_advisory_unlock(@key)",blocker)){unlock.Parameters.AddWithValue("key",key);await unlock.ExecuteNonQueryAsync(Ct);}
                response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();await Sql("""DROP TRIGGER s6_pause_extraction_confirm ON audit."SecurityEvents";DROP FUNCTION audit.s6_pause_extraction_confirm();""");
            }
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }

    [Fact]
    public async Task Crew_source_requires_current_person_file_purpose_and_stays_review_only_without_a_crew_owning_confirm_seam() {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var p=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer,true);using var client=Client(host);ExtractionPermissions(host.Services,true);await Token(client);
            using var staged=await Send(client,PersonalVersion("CertificateDocument"),PersonalPng("CertificateDocument"));Assert.Equal(HttpStatusCode.OK,staged.StatusCode);
            var file=(await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;using var bytes=await PutPersonalPng(client,PersonalVersion("CertificateDocument"),file.Id);Assert.Equal(HttpStatusCode.OK,bytes.StatusCode);
            var id=Guid.NewGuid();using var saved=await Send(client,$"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/certificates",new CredentialSaveCommand(id,null,new(new(CertificateNumber:"MASKED-CREW-ONLY"),DocumentReference:file.Id)));
            Assert.Equal(HttpStatusCode.OK,saved.StatusCode);await using var db=Db();var credential=await db.Credentials.AsNoTracking().SingleAsync(r=>r.Id==id,Ct);
            var path=$"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/certificates/{id}/extractions";
            using var wrongPurpose=await Send(client,path+"?purpose=unapproved",new CertificateExtractCommand(credential.Version));Assert.Equal(HttpStatusCode.NotFound,wrongPurpose.StatusCode);Assert.Equal(0,peer.Requests);
            using var extracted=await Send(client,path+"?purpose=synthetic-person-file-test",new CertificateExtractCommand(credential.Version));Assert.Equal(HttpStatusCode.OK,extracted.StatusCode);
            var preview=(await extracted.Content.ReadFromJsonAsync<CertificateExtractionDto>(Ct))!;Assert.Equal("Crew",preview.Scope);Assert.False(preview.CanConfirm);Assert.Null(preview.ReviewContext);
            Assert.Contains("CREW_CONFIRMATION_WORKFLOW_UNAVAILABLE",preview.Blockers);Assert.Equal(credential.Version,(await db.Credentials.AsNoTracking().SingleAsync(r=>r.Id==id,Ct)).Version);
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            peer.BeforeReply=async ct=>{entered.TrySetResult();await release.Task.WaitAsync(ct);};
            var pending=Send(client,path+"?purpose=synthetic-person-file-test",new CertificateExtractCommand(credential.Version));
            try {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(15),Ct);host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["CrewFilePolicy:Purpose"]="withdrawn-purpose";release.TrySetResult();
                using var denied=await pending.WaitAsync(TimeSpan.FromSeconds(15),Ct);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);Assert.DoesNotContain("MASKED",await denied.Content.ReadAsStringAsync(Ct));
            }finally{release.TrySetResult();}
        }finally{Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }
}
