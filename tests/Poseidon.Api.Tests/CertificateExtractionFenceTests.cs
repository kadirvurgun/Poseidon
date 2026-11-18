using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// QA-SRC-002 / DEV045: change the real owning aggregate after Current has
// captured it but before its awaited stage query. No fabricated business result.
public sealed partial class DirectoryWorkspaceTests
{
    private sealed class ExtractionStageBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int remaining;
        internal void Arm(int stageRead)=>Volatile.Write(ref remaining,stageRead);
        internal Task Wait(CancellationToken ct)=>entered.Task.WaitAsync(TimeSpan.FromSeconds(30),ct);
        internal void Release()=>released.TrySetResult();
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,
            InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default) {
            // Only standalone stage materialization, not a composed EXISTS fence.
            if(command.CommandText.StartsWith("SELECT",StringComparison.Ordinal) && command.CommandText.Contains("FROM files.\"FileStages\" AS",StringComparison.Ordinal) &&
                !command.CommandText.Contains("EXISTS",StringComparison.Ordinal) && Volatile.Read(ref remaining)>0 && Interlocked.Decrement(ref remaining)==0) {
                entered.TrySetResult();await released.Task.WaitAsync(TimeSpan.FromSeconds(30),cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public async Task Extraction_final_composed_fence_withholds_an_owning_head_changed_during_awaited_transport_read(bool crew,bool afterProvider) {
        var root=ExtractionRoot();var secret=ExtractionSecret();using var scanner=new ScanPeer("stream: OK\0");var policy=ExtractionPolicy(secret);using var peer=new ExtractionPeer{Policy=policy};
        var barrier=new ExtractionStageBarrier();
        try {
            await using var host=FileHost(root,scanner.Port,barrier).WithWebHostBuilder(b=>{
                b.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {
                    ["CrewFilePolicy:Version"]="synthetic-person-files-v1",["CrewFilePolicy:Purpose"]="synthetic-person-file-test",
                    ["CrewFilePolicy:SourceVersion"]="poseidon-v46.3-20261030",["CrewFilePolicy:Kinds:0"]="CertificateDocument"
                }));
                b.ConfigureServices(s=>{s.AddSingleton(policy);s.AddSingleton(sp=>new CertificateExtractionProvider(peer,sp.GetRequiredService<IHostEnvironment>()));});
            });
            using var client=Client(host);ExtractionPermissions(host.Services,crew);await Token(client);
            Guid id;uint version;string path;Func<Task<HttpResponseMessage>> advance;
            if(!crew) {
                var original=await ExtractionSource(client);id=original.Id;version=original.Version;path=ExtractionPath(original);
                advance=()=>Send(client,CertificatePath(id),new CertificateSaveCommand(id,version,
                    original.Content with{Metadata=original.Content.Metadata with{Declaration=original.Content.Metadata.Declaration with{CertificateNumber="MASKED-ADVANCED"}}},"AUTHORING_CORRECTION"),update:true);
            }else {
                using var staged=await Send(client,PersonalVersion("CertificateDocument"),PersonalPng("CertificateDocument"));Assert.Equal(HttpStatusCode.OK,staged.StatusCode);
                var file=(await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;using var bytes=await PutPersonalPng(client,PersonalVersion("CertificateDocument"),file.Id);Assert.Equal(HttpStatusCode.OK,bytes.StatusCode);
                id=Guid.NewGuid();var metadata=new CertificateMetadata(new(CertificateNumber:"MASKED-CREW-ORIGINAL"),DocumentReference:file.Id);
                var owning=$"vessels/{SeedVessel.Id}/crew/{SeedVessel.Id}/certificates";
                using var created=await Send(client,owning,new CredentialSaveCommand(id,null,metadata));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
                await using var db=Db();version=(await db.Credentials.AsNoTracking().SingleAsync(r=>r.Id==id,Ct)).Version;
                path=$"{owning}/{id}/extractions?purpose=synthetic-person-file-test";
                advance=()=>Send(client,owning,new CredentialSaveCommand(id,version,metadata with{Declaration=metadata.Declaration with{CertificateNumber="MASKED-CREW-ADVANCED"}}));
            }
            // Initial Read=1; preprovider Current.Read=2; postprovider=3.
            barrier.Arm(afterProvider?3:2);var pending=Send(client,path,new CertificateExtractCommand(version));HttpResponseMessage? response=null;
            try {
                await barrier.Wait(Ct);using var changed=await advance();Assert.Equal(HttpStatusCode.OK,changed.StatusCode);
                await using(var verify=Db()) {
                    if(crew){var current=await verify.Credentials.AsNoTracking().SingleAsync(r=>r.Id==id,Ct);Assert.Equal(2,current.Revision);Assert.NotEqual(version,current.Version);}
                    else{var current=await verify.VesselCertificates.AsNoTracking().SingleAsync(r=>r.Id==id,Ct);Assert.Equal(2,current.Revision);Assert.NotEqual(version,current.Version);}
                }
                barrier.Release();response=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
                var body=await response.Content.ReadAsStringAsync(Ct);Assert.DoesNotContain("MASKED",body);Assert.DoesNotContain("reviewContext",body);Assert.DoesNotContain("candidates",body);
                Assert.Equal(afterProvider?1:0,peer.Requests);
            }finally{barrier.Release();response??=await pending.WaitAsync(TimeSpan.FromSeconds(30),Ct);response.Dispose();}
        }finally{barrier.Release();Environment.SetEnvironmentVariable(secret,null);RemoveExtractionFiles(root);}
    }
}
