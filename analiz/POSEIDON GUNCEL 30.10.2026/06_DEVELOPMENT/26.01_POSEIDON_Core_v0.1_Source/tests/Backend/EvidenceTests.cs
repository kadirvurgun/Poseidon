using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Npgsql;
namespace Poseidon.Tests;
public sealed class EvidenceApp : ConfiguredAppFactory {
 public string Root { get; }=Directory.CreateTempSubdirectory("poseidon-evidence-test-").FullName;
 readonly string schema="evidence_test_"+Guid.NewGuid().ToString("N");
 public EvidenceApp(){using var c=new NpgsqlConnection(TestDatabase.Connection);c.Open();using var q=new NpgsqlCommand($"CREATE SCHEMA {schema}",c);q.ExecuteNonQuery();}
 public string Connection=>new NpgsqlConnectionStringBuilder(TestDatabase.Connection){SearchPath=schema}.ConnectionString;
 public override async ValueTask DisposeAsync(){await base.DisposeAsync();await using var c=new NpgsqlConnection(TestDatabase.Connection);await c.OpenAsync();await using var q=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",c);await q.ExecuteNonQueryAsync();}
 public IEvidenceFaults? Faults;
 protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.ConfigureServices(services=>{if(Faults is not null)services.AddSingleton(Faults);});
 public string? RootOverride;
 public bool Enabled=true;
 public string Environment="Development";
 protected override string EnvironmentName=>Environment;
 protected override Dictionary<string,string?> Settings=>new(){["ConnectionStrings:Poseidon"]=Connection,["SyntheticIdentity:Enabled"]=Environment=="Development"?"true":"false",["Database:Initialize"]=Environment=="Development"?"true":"false",["Authentication:Authority"]="https://issuer.invalid",["Authentication:Audience"]="test-api",["EvidenceIntake:Enabled"]=Enabled.ToString(),["EvidenceIntake:Root"]=RootOverride??Root};
}
[Collection("PostgreSQL schema")]
public class EvidenceTests {
 sealed class NonSeek(byte[] bytes) : MemoryStream(bytes){public override bool CanSeek=>false;}
 const string Route="/api/v1/evidence-intake";
 static readonly byte[] Pdf=Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");
 static readonly byte[] Png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aL1cAAAAASUVORK5CYII=");
 static readonly byte[] Jpeg=Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==");
 static async Task<HttpClient> Login(EvidenceApp app,string fixture="evidence-manager") {var c=app.CreateClient();var r=await c.PostAsJsonAsync("/api/v1/dev/session",new{fixture});r.EnsureSuccessStatusCode();var j=await r.Content.ReadFromJsonAsync<JsonElement>();c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",j.GetProperty("accessToken").GetString());return c;}
 static async Task<JsonElement> Ok(HttpResponseMessage r) {var text=await r.Content.ReadAsStringAsync();Assert.True(r.IsSuccessStatusCode,$"{(int)r.StatusCode} {text}");return JsonDocument.Parse(text).RootElement.Clone();}
 static Task<HttpResponseMessage> Post(HttpClient c,string suffix,object body,string? key=null,long? version=null){var r=new HttpRequestMessage(HttpMethod.Post,Route+suffix){Content=JsonContent.Create(body)};r.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());if(version.HasValue)r.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return c.SendAsync(r);}
 static async Task<Guid> Parent(HttpClient c){var r=new HttpRequestMessage(HttpMethod.Post,"/api/v1/inventory/vessels"){Content=JsonContent.Create(new{data=new{name="Evidence synthetic target"}})};r.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());return (await Ok(await c.SendAsync(r))).GetProperty("id").GetGuid();}
 static object Metadata(Guid target,byte[] bytes,string filename="synthetic.pdf",string mime="application/pdf",string? sha=null){var result=new Dictionary<string,object>{{"targetType","Vessel"},{"targetId",target},{"filename",filename},{"declaredMime",mime},{"expectedBytes",bytes.Length}};if(sha is not null)result["expectedSha256"]=sha;return result;}
 static Task<HttpResponseMessage> Upload(HttpClient c,Guid id,byte[] bytes,long version=1,string? key=null,string mime="application/pdf",bool length=true){var r=new HttpRequestMessage(HttpMethod.Post,$"{Route}/{id}/content"){Content=length?new ByteArrayContent(bytes):new StreamContent(new NonSeek(bytes))};r.Content.Headers.ContentType=new MediaTypeHeaderValue(mime);r.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());r.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return c.SendAsync(r);}
 [PostgresFact] public async Task Quarantined_bytes_hash_scope_history_and_all_content_routes_blocked(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);
  foreach(var sample in new[]{(Pdf,"synthetic.pdf","application/pdf"),(Png,"synthetic.png","image/png"),(Jpeg,"synthetic.jpg","image/jpeg")}) {
   var reservation=await Ok(await Post(c,"",Metadata(target,sample.Item1,sample.Item2,sample.Item3,Convert.ToHexString(SHA256.HashData(sample.Item1)))));var id=reservation.GetProperty("id").GetGuid();
   var stored=await Ok(await Upload(c,id,sample.Item1,mime:sample.Item3));Assert.Equal("STORED_QUARANTINED",stored.GetProperty("state").GetString());Assert.False(stored.GetProperty("canDownload").GetBoolean());Assert.Equal("UNAVAILABLE",stored.GetProperty("scanState").GetString());Assert.Equal("NOT_PERFORMED",stored.GetProperty("verificationAssessment").GetString());Assert.Equal("NOT_PERFORMED",stored.GetProperty("operationalAssessment").GetString());
   Assert.Equal(Convert.ToHexString(SHA256.HashData(sample.Item1)).ToLowerInvariant(),stored.GetProperty("sha256").GetString()!.ToLowerInvariant());Assert.Equal(sample.Item1.Length,stored.GetProperty("actualBytes").GetInt64());
   var raw=stored.GetRawText();Assert.DoesNotContain(app.Root,raw);Assert.DoesNotContain("storageRef",raw);Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(sample.Item1));
   foreach(var suffix in new[]{"content","download","preview","thumbnail","export","ocr","scan","release"}) {
    using var get=new HttpRequestMessage(HttpMethod.Get,$"{Route}/{id}/{suffix}");get.Headers.Range=new RangeHeaderValue(0,1);var denied=await c.SendAsync(get);Assert.False(denied.IsSuccessStatusCode);Assert.NotEqual(sample.Item1,await denied.Content.ReadAsByteArrayAsync());
   }
   Assert.False((await c.SendAsync(new HttpRequestMessage(HttpMethod.Head,$"{Route}/{id}/content"))).IsSuccessStatusCode);
   var history=await Ok(await c.GetAsync($"{Route}/{id}/history"));foreach(var field in new[]{"filename","synthetic.pdf","synthetic.png","synthetic.jpg","storage","source","actor\"","before_data","after_data"})Assert.DoesNotContain(field,history.GetRawText());
   using var hidden=await Login(app,"tenant-b");Assert.Equal(HttpStatusCode.NotFound,(await hidden.GetAsync($"{Route}/{id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await hidden.GetAsync($"{Route}/{id}/history")).StatusCode);
  }
 }
 [PostgresFact] public async Task Closed_metadata_rejects_path_control_duplicate_and_state_spoofing(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);
  foreach(var name in new[]{"../x.pdf","/tmp/x.pdf","C:\\x.pdf","\\\\server\\x.pdf","x%2f.pdf","x%5c.pdf","x\0.pdf","x\r\n.pdf","x\u202E.pdf","x.html.pdf","x.svg",""})Assert.False((await Post(c,"",Metadata(target,Pdf,name))).IsSuccessStatusCode);
  foreach(var field in new[]{"tenant_id","company_id","uploader","storage_ref","state","scanState","verificationAssessment","effective"}){var body=JsonSerializer.SerializeToElement(Metadata(target,Pdf)).EnumerateObject().Where(p=>p.Value.ValueKind!=JsonValueKind.Null).ToDictionary(p=>p.Name,p=>p.Value);body[field]=JsonSerializer.SerializeToElement("CLEAN");Assert.False((await Post(c,"",body)).IsSuccessStatusCode);}
  var json=JsonSerializer.Serialize(Metadata(target,Pdf));using var duplicate=new HttpRequestMessage(HttpMethod.Post,Route){Content=new StringContent(json[..^1]+",\"targetType\":\"Vessel\"}",Encoding.UTF8,"application/json")};duplicate.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.False((await c.SendAsync(duplicate)).IsSuccessStatusCode);
  using var reader=await Login(app,"tenant-a");var readerTarget=await Parent(reader);Assert.Equal(HttpStatusCode.Forbidden,(await Post(reader,"",Metadata(readerTarget,Pdf))).StatusCode);
 }
 [PostgresFact] public async Task Stream_spoofing_hash_size_and_equal_size_replay_fail_closed(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);
  foreach(var sample in new[]{(Encoding.ASCII.GetBytes("<html>benign text</html>"),"synthetic.pdf","application/pdf",(string?)null),(Png,"synthetic.jpg","image/jpeg",(string?)null),(Pdf,"synthetic.pdf","application/pdf",new string('0',64))}){
   var row=await Ok(await Post(c,"",Metadata(target,sample.Item1,sample.Item2,sample.Item3,sample.Item4)));var response=await Upload(c,row.GetProperty("id").GetGuid(),sample.Item1,mime:sample.Item3);Assert.False(response.IsSuccessStatusCode);var state=await Ok(await c.GetAsync($"{Route}/{row.GetProperty("id").GetGuid()}"));Assert.NotEqual("STORED_QUARANTINED",state.GetProperty("state").GetString());
  }
  var body=Metadata(target,Pdf);var key=Guid.NewGuid().ToString();var reserve=await Ok(await Post(c,"",body,key));var repeated=await Ok(await Post(c,"",body,key));Assert.Equal(reserve.GetProperty("id").GetGuid(),repeated.GetProperty("id").GetGuid());
  var id=reserve.GetProperty("id").GetGuid();var uploadKey=Guid.NewGuid().ToString();var committed=await Ok(await Upload(c,id,Pdf,key:uploadKey));var changed=Pdf.ToArray();changed[20]=(byte)'X';Assert.Equal(HttpStatusCode.Conflict,(await Upload(c,id,changed,key:uploadKey)).StatusCode);
  var recovered=await Ok(await c.GetAsync($"{Route}/{id}"));Assert.Equal(committed.GetProperty("sha256").GetString(),recovered.GetProperty("sha256").GetString());Assert.Equal(committed.GetProperty("version").GetInt64(),recovered.GetProperty("version").GetInt64());
  var shortRow=await Ok(await Post(c,"",Metadata(target,Pdf)));Assert.False((await Upload(c,shortRow.GetProperty("id").GetGuid(),Pdf[..^1],length:false)).IsSuccessStatusCode);
 }
 [PostgresFact] public async Task Disabled_or_unsupported_environment_never_activates_intake(){
  await using var off=new EvidenceApp{Enabled=false};using var c=await Login(off);Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync(Route)).StatusCode);
  await using var production=new EvidenceApp{Environment="Production"};Assert.Throws<InvalidOperationException>(()=>production.CreateClient());
 }
 sealed class Fault(string point,bool cancel=false) : IEvidenceFaults {public void Checkpoint(string stage){if(stage==point){if(cancel)throw new OperationCanceledException("Synthetic cancellation");throw new IOException("Synthetic storage failure");}}}
 [PostgresFact] public async Task File_and_database_fault_boundaries_never_remove_committed_objects_or_retransmit(){
  foreach(var point in new[]{"before_file","after_file","before_commit","after_commit"}) {
   await using var app=new EvidenceApp{Faults=new Fault(point)};using var c=await Login(app);var target=await Parent(c);var reservation=await Ok(await Post(c,"",Metadata(target,Pdf)));var id=reservation.GetProperty("id").GetGuid();
   var failed=await Upload(c,id,Pdf);Assert.False(failed.IsSuccessStatusCode);var recovered=await Ok(await c.GetAsync($"{Route}/{id}"));Assert.Equal(point=="after_commit"?"STORED_QUARANTINED":"RECEIVING",recovered.GetProperty("state").GetString());
   Assert.Equal(HttpStatusCode.Conflict,(await Upload(c,id,Pdf)).StatusCode);
   if(point!="before_file")Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(Pdf));
   await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();await using var quota=new NpgsqlCommand("SELECT sum(expected_bytes)::bigint FROM evidence_intakes",db);Assert.Equal((long)Pdf.Length,await quota.ExecuteScalarAsync());
  }
  await using var canceled=new EvidenceApp{Faults=new Fault("after_commit",true)};using var client=await Login(canceled);var parent=await Parent(client);var pending=await Ok(await Post(client,"",Metadata(parent,Pdf)));var upload=pending.GetProperty("id").GetGuid();Assert.False((await Upload(client,upload,Pdf)).IsSuccessStatusCode);Assert.Equal("STORED_QUARANTINED",(await Ok(await client.GetAsync($"{Route}/{upload}"))).GetProperty("state").GetString());Assert.Contains(Directory.GetFiles(canceled.Root),f=>File.ReadAllBytes(f).SequenceEqual(Pdf));
 }
 [PostgresFact] public async Task Actor_slots_are_atomic_and_replay_does_not_charge_twice(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var body=Metadata(target,Pdf);var results=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>Post(c,"",body)));Assert.Equal(10,results.Count(x=>x.IsSuccessStatusCode));Assert.Equal(2,results.Count(x=>x.StatusCode==HttpStatusCode.Conflict));
  await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();await using var q=new NpgsqlCommand("SELECT count(*),sum(expected_bytes)::bigint FROM evidence_intakes",db);await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal(10,r.GetInt64(0));Assert.Equal(Pdf.Length*10,r.GetInt64(1));
 }
 [Fact] public void Linux_descriptor_store_rejects_symlink_roots_and_exclusive_name_replacement(){
  var root=Directory.CreateTempSubdirectory("poseidon-object-test-").FullName;var link=root+"-link";Directory.CreateSymbolicLink(link,root);Assert.ThrowsAny<Exception>(()=>new EvidenceObjectStore(link));
  using var store=new EvidenceObjectStore(root);var id=Guid.NewGuid();using(var file=store.Create(id)){file.Write(Pdf);file.Flush(true);}Assert.Throws<IOException>(()=>store.Create(id));
  var outside=Path.Combine(root,"outside.txt");File.WriteAllText(outside,"benign preserved fixture");var malicious=Guid.NewGuid();File.CreateSymbolicLink(Path.Combine(root,malicious.ToString("N")+".quarantine"),outside);Assert.Throws<IOException>(()=>store.Create(malicious));Assert.Equal("benign preserved fixture",File.ReadAllText(outside));
 }

 static ClaimsPrincipal Principal(Session s,bool capture=true,bool inventory=true,bool certificate=true)=>new(new ClaimsIdentity(new[]{new Claim("sub",s.Subject),new Claim("synthetic","true"),new Claim("tenant_id",s.TenantId.ToString()),new Claim("company_id",s.CompanyId.ToString())}.Concat(s.VesselIds.Select(v=>new Claim("vessel_id",v.ToString()))).Concat(capture?[new Claim("permission","evidence:capture")]:Array.Empty<Claim>()).Concat(inventory?[new Claim("permission","inventory:manage")]:Array.Empty<Claim>()).Concat(certificate?[new Claim("permission","certificate:manage")]:Array.Empty<Claim>()),"Synthetic"));
 [PostgresFact] public async Task Independent_permissions_current_target_scope_and_replay_are_enforced(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["evidence-manager"];var body=JsonSerializer.SerializeToElement(Metadata(target,Pdf));var key=Guid.NewGuid().ToString();
  var row=await store.EvidenceReserve(Principal(scope),body,key,"test",default);
  foreach(var denied in new[]{Principal(scope,capture:false),Principal(scope,inventory:false)}) {
   Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>store.EvidenceReserve(denied,body,key,"test",default))).Status);
   Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>store.EvidenceUpload(denied,row.Id,row.Version,Guid.NewGuid().ToString(),"application/pdf",new MemoryStream(Pdf),app.Services.GetRequiredService<EvidenceObjectStore>(),new NoEvidenceFaults(),"test",default))).Status);
  }
  var reader=scope with{Subject="reader",VesselIds=[target]};Assert.Equal(row.Id,(await store.EvidenceGet(Principal(reader,false,false,false),row.Id,default)).Id);
  foreach(var hidden in new[]{reader with{VesselIds=[]},reader with{CompanyId=Guid.NewGuid()},reader with{TenantId=Guid.NewGuid()}})Assert.Equal(404,(await Assert.ThrowsAsync<ApiFault>(()=>store.EvidenceGet(Principal(hidden),row.Id,default))).Status);
  var certRequest=new HttpRequestMessage(HttpMethod.Post,"/api/v1/certificate-register"){Content=JsonContent.Create(new{data=new{vessel_id=target.ToString()}})};certRequest.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());var cert=(await Ok(await c.SendAsync(certRequest))).GetProperty("id").GetGuid();var certBody=JsonSerializer.SerializeToElement(new{targetType="Certificate",targetId=cert,filename="synthetic.pdf",declaredMime="application/pdf",expectedBytes=Pdf.Length});
  Assert.Equal(403,(await Assert.ThrowsAsync<ApiFault>(()=>store.EvidenceReserve(Principal(scope,certificate:false),certBody,Guid.NewGuid().ToString(),"test",default))).Status);
  Assert.Equal("Certificate",(await store.EvidenceReserve(Principal(scope,inventory:false),certBody,Guid.NewGuid().ToString(),"test",default)).TargetType);
 }

 [PostgresFact] public async Task Supersede_preserves_old_bytes_link_history_and_original_authority(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var first=await Ok(await Post(c,"",Metadata(target,Pdf)));var old=await Ok(await Upload(c,first.GetProperty("id").GetGuid(),Pdf));var oldId=old.GetProperty("id").GetGuid();var version=old.GetProperty("version").GetInt64();var key=Guid.NewGuid().ToString();var body=new{metadata=Metadata(target,Png,"replacement.png","image/png"),reason="PRIVATE correction reason"};
  Assert.Equal((HttpStatusCode)428,(await Post(c,$"/{oldId}/supersede",body)).StatusCode);
  Assert.Equal(HttpStatusCode.PreconditionFailed,(await Post(c,$"/{oldId}/supersede",body,version:version-1)).StatusCode);
  var next=await Ok(await Post(c,$"/{oldId}/supersede",body,key,version));var child=next.GetProperty("id").GetGuid();Assert.Equal(oldId,next.GetProperty("previousId").GetGuid());
  Assert.Equal(child,(await Ok(await Post(c,$"/{oldId}/supersede",body,key,version))).GetProperty("id").GetGuid());
  Assert.Equal(HttpStatusCode.Conflict,(await Post(c,$"/{oldId}/supersede",body,version:version)).StatusCode);
  await Ok(await Upload(c,child,Png,mime:"image/png"));var parent=await Ok(await c.GetAsync($"{Route}/{oldId}"));Assert.Equal(child,parent.GetProperty("supersededBy").GetGuid());Assert.Equal(old.GetProperty("sha256").GetString(),parent.GetProperty("sha256").GetString());
  Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(Pdf));Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(Png));
  var history=await Ok(await c.GetAsync($"{Route}/{oldId}/history"));Assert.Contains("EVIDENCE_SUPERSEDED",history.GetRawText());Assert.DoesNotContain("PRIVATE",history.GetRawText());Assert.DoesNotContain("replacement.png",history.GetRawText());
  await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();foreach(var sql in new[]{"DELETE FROM evidence_intakes WHERE id=@id","UPDATE evidence_intakes SET sha256='forged' WHERE id=@id"}){await using var q=new NpgsqlCommand(sql,db);q.Parameters.AddWithValue("id",oldId);await Assert.ThrowsAsync<PostgresException>(()=>q.ExecuteNonQueryAsync());}
 }
 [PostgresFact] public async Task Exact_byte_caps_unknown_length_and_tenant_quota_are_bounded(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var bytes=Enumerable.Repeat((byte)' ',(int)EvidenceContracts.MaxBytes).ToArray();Encoding.ASCII.GetBytes("%PDF-1.4\n").CopyTo(bytes,0);Encoding.ASCII.GetBytes("\n%%EOF").CopyTo(bytes,bytes.Length-6);
  var exact=await Ok(await Post(c,"",Metadata(target,bytes)));await Ok(await Upload(c,exact.GetProperty("id").GetGuid(),bytes));
  Assert.Equal((HttpStatusCode)422,(await Post(c,"",new{targetType="Vessel",targetId=target,filename="over.pdf",declaredMime="application/pdf",expectedBytes=EvidenceContracts.MaxBytes+1})).StatusCode);
  Assert.Equal((HttpStatusCode)422,(await Post(c,"",new{targetType="Vessel",targetId=target,filename="zero.pdf",declaredMime="application/pdf",expectedBytes=0})).StatusCode);
  var small=await Ok(await Post(c,"",Metadata(target,Pdf)));Assert.Equal((HttpStatusCode)422,(await Upload(c,small.GetProperty("id").GetGuid(),Pdf.Concat(new byte[]{32}).ToArray(),length:false)).StatusCode);
  var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["evidence-manager"];var metadata=JsonSerializer.SerializeToElement(new{targetType="Vessel",targetId=target,filename="quota.pdf",declaredMime="application/pdf",expectedBytes=EvidenceContracts.MaxBytes});
  for(var n=0;n<18;n++)await store.EvidenceReserve(Principal(scope with{Subject="quota-"+n,VesselIds=[target]}),metadata,Guid.NewGuid().ToString(),"test",default);
  var remaining=104857600-EvidenceContracts.MaxBytes*19-Pdf.Length;
  await store.EvidenceReserve(Principal(scope with{Subject="quota-last",VesselIds=[target]}),JsonSerializer.SerializeToElement(new{targetType="Vessel",targetId=target,filename="last.pdf",declaredMime="application/pdf",expectedBytes=remaining}),Guid.NewGuid().ToString(),"test",default);
  Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>store.EvidenceReserve(Principal(scope with{Subject="quota-over",VesselIds=[target]}),JsonSerializer.SerializeToElement(Metadata(target,Pdf)),Guid.NewGuid().ToString(),"test",default))).Status);
 }
 [PostgresFact] public async Task Stored_pointer_audit_failure_rolls_back_but_preserves_charged_file(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var row=await Ok(await Post(c,"",Metadata(target,Pdf)));var id=row.GetProperty("id").GetGuid();await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();
  await using(var ddl=new NpgsqlCommand("CREATE FUNCTION fail_evidence_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action='EVIDENCE_STORED' THEN RAISE EXCEPTION 'Synthetic audit failure'; END IF; RETURN NEW; END $$;CREATE TRIGGER fail_evidence_audit BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION fail_evidence_audit();",db))await ddl.ExecuteNonQueryAsync();
  Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Upload(c,id,Pdf)).StatusCode);Assert.Equal("RECEIVING",(await Ok(await c.GetAsync($"{Route}/{id}"))).GetProperty("state").GetString());Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(Pdf));
  await using var q=new NpgsqlCommand("SELECT object_id,(SELECT count(*) FROM audit_events WHERE entity_id=@id AND action='EVIDENCE_STORED') FROM evidence_intakes WHERE id=@id",db);q.Parameters.AddWithValue("id",id);await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.True(r.IsDBNull(0));Assert.Equal(0,r.GetInt64(1));
 }

 [PostgresFact] public async Task Reconciliation_and_local_copy_restore_preserve_hash_scope_history_and_reject_orphans(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var reserve=await Ok(await Post(c,"",Metadata(target,Pdf)));var id=reserve.GetProperty("id").GetGuid();var receipt=await Ok(await Upload(c,id,Pdf));var store=app.Services.GetRequiredService<DraftStore>();var objects=app.Services.GetRequiredService<EvidenceObjectStore>();await store.EvidenceReconcile(objects,default);
  await using var restored=new EvidenceApp();using var initialized=await Login(restored);await using var db=new NpgsqlConnection(restored.Connection);await db.OpenAsync();var from=new NpgsqlConnectionStringBuilder(app.Connection).SearchPath!;
  foreach(var table in new[]{"companies","drafts","audit_events","idempotency","evidence_intakes"}){await using var copy=new NpgsqlCommand($"INSERT INTO {table} SELECT * FROM {from}.{table} ON CONFLICT DO NOTHING",db);await copy.ExecuteNonQueryAsync();}
  foreach(var file in Directory.GetFiles(app.Root))File.Copy(file,Path.Combine(restored.Root,Path.GetFileName(file)));
  var restoredStore=restored.Services.GetRequiredService<DraftStore>();await restoredStore.EvidenceReconcile(restored.Services.GetRequiredService<EvidenceObjectStore>(),default);var scope=SyntheticIdentity.Fixtures["evidence-manager"];var recovered=await restoredStore.EvidenceGet(Principal(scope),id,default);Assert.Equal(receipt.GetProperty("sha256").GetString(),recovered.Sha256);Assert.Equal(receipt.GetProperty("version").GetInt64(),recovered.Version);Assert.False(recovered.CanDownload);
  Assert.Equal(404,(await Assert.ThrowsAsync<ApiFault>(()=>restoredStore.EvidenceGet(Principal(scope with{Subject="ungranted",VesselIds=[]}),id,default))).Status);
  var history=await Ok(await initialized.GetAsync($"{Route}/{id}/history"));Assert.Equal(3,history.GetProperty("items").GetArrayLength());
  var empty=Directory.CreateTempSubdirectory("poseidon-empty-restore-").FullName;using(var missing=new EvidenceObjectStore(empty))await Assert.ThrowsAsync<InvalidOperationException>(()=>restoredStore.EvidenceReconcile(missing,default));
  using(var extra=restored.Services.GetRequiredService<EvidenceObjectStore>().Create(Guid.NewGuid())){extra.Write(Pdf);extra.Flush(true);}await Assert.ThrowsAsync<InvalidOperationException>(()=>restoredStore.EvidenceReconcile(restored.Services.GetRequiredService<EvidenceObjectStore>(),default));
  // Original remains valid: no cleanup or mutation of its accepted object took place.
  await store.EvidenceReconcile(objects,default);Assert.Contains(Directory.GetFiles(app.Root),f=>File.ReadAllBytes(f).SequenceEqual(Pdf));
 }
 [PostgresFact] public async Task Failed_replacement_is_retained_but_does_not_permanently_block_a_new_correction(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var original=await Ok(await Post(c,"",Metadata(target,Pdf)));var id=original.GetProperty("id").GetGuid();var stored=await Ok(await Upload(c,id,Pdf));var body=new{metadata=Metadata(target,Pdf),reason="Synthetic correction"};
  var failed=await Ok(await Post(c,$"/{id}/supersede",body,version:stored.GetProperty("version").GetInt64()));Assert.Equal((HttpStatusCode)422,(await Upload(c,failed.GetProperty("id").GetGuid(),new byte[Pdf.Length])).StatusCode);
  var next=await Ok(await Post(c,$"/{id}/supersede",body,version:stored.GetProperty("version").GetInt64()));await Ok(await Upload(c,next.GetProperty("id").GetGuid(),Pdf));Assert.Equal("REJECTED",(await Ok(await c.GetAsync($"{Route}/{failed.GetProperty("id").GetGuid()}"))).GetProperty("state").GetString());
 }

 [PostgresFact] public async Task Query_binding_expiry_metadata_limits_and_safe_history_field_shape(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var other=await Parent(c);
  var items=new List<Guid>();for(var i=0;i<3;i++){var item=await Ok(await Post(c,"",Metadata(target,Pdf)));items.Add(item.GetProperty("id").GetGuid());Assert.Equal(TimeSpan.FromMinutes(15),item.GetProperty("expiresAt").GetDateTimeOffset()-item.GetProperty("createdAt").GetDateTimeOffset());}
  var query=$"?targetType=Vessel&targetId={target}&pageSize=1";var page=await Ok(await c.GetAsync(Route+query));var cursor=page.GetProperty("nextCursor").GetString()!;Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync(Route+$"?targetType=Vessel&targetId={other}&pageSize=1&cursor="+Uri.EscapeDataString(cursor))).StatusCode);
  var found=new HashSet<Guid>();while(true){found.Add(page.GetProperty("items")[0].GetProperty("id").GetGuid());if(!page.GetProperty("hasMore").GetBoolean())break;page=await Ok(await c.GetAsync(Route+query+"&cursor="+Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!)));}Assert.Equal(3,found.Count);
  foreach(var invalid in new[]{"&pageSize=101","&unknown=x","&targetType=Certificate","&cursor="+new string('x',513)})Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync(Route+$"?targetType=Vessel&targetId={target}"+invalid)).StatusCode);
  var history=await Ok(await c.GetAsync($"{Route}/{items[0]}/history"));Assert.Equal(new[]{"action","actorRelation","changedFields","eventId","occurredAt","version"},history.GetProperty("items")[0].EnumerateObject().Select(p=>p.Name).OrderBy(x=>x));
  var large=JsonSerializer.Serialize(Metadata(target,Pdf));using var request=new HttpRequestMessage(HttpMethod.Post,Route){Content=new StringContent(large+new string(' ',16385),Encoding.UTF8,"application/json")};request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await c.SendAsync(request)).StatusCode);
  await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();var expired=Guid.NewGuid();var scope=SyntheticIdentity.Fixtures["evidence-manager"];
  await using(var insert=new NpgsqlCommand("INSERT INTO evidence_intakes(tenant_id,id,company_id,vessel_id,target_type,target_id,actor,metadata,expected_bytes,created_at) VALUES(@tenant,@id,@company,@target,'Vessel',@target,@actor,@metadata::jsonb,@bytes,now()-interval '16 minutes')",db)){insert.Parameters.AddWithValue("tenant",scope.TenantId);insert.Parameters.AddWithValue("id",expired);insert.Parameters.AddWithValue("company",scope.CompanyId);insert.Parameters.AddWithValue("target",target);insert.Parameters.AddWithValue("actor",scope.Subject);insert.Parameters.AddWithValue("metadata",JsonSerializer.Serialize(Metadata(target,Pdf)));insert.Parameters.AddWithValue("bytes",Pdf.Length);await insert.ExecuteNonQueryAsync();}
  Assert.Equal(HttpStatusCode.Conflict,(await Upload(c,expired,Pdf)).StatusCode);Assert.Equal("EXPIRED",(await Ok(await c.GetAsync($"{Route}/{expired}"))).GetProperty("state").GetString());Assert.Empty(Directory.GetFiles(app.Root));
 }
 [Theory][InlineData("2026-10-27Z")][InlineData("2026-10-27T12:00:00+00:00")][InlineData("2026-02-30T12:00:00Z")]
 public void Capture_timestamp_requires_actual_ISO_UTC_timestamp(string date){var body=JsonSerializer.SerializeToElement(new{targetType="Vessel",targetId=Guid.NewGuid(),filename="test.pdf",declaredMime="application/pdf",expectedBytes=1,capturedAt=date});Assert.Throws<ApiFault>(()=>EvidenceContracts.Validate(body));}

 [System.Runtime.InteropServices.DllImport("libc",SetLastError=true)]static extern int mkfifo(string path,uint mode);
 [Fact] public void Matching_name_FIFO_is_rejected_without_blocking_inspection(){var root=Directory.CreateTempSubdirectory("poseidon-fifo-fixture-").FullName;Assert.Equal(0,mkfifo(Path.Combine(root,Guid.NewGuid().ToString("N")+".quarantine"),0x180));using var store=new EvidenceObjectStore(root);Assert.Throws<InvalidOperationException>(()=>store.Inspect());}
 [PostgresFact] public void Evidence_root_cannot_be_inside_application_source_or_static_tree(){using var app=new EvidenceApp{RootOverride=Path.GetDirectoryName(typeof(Program).Assembly.Location)};Assert.Throws<InvalidOperationException>(()=>app.CreateClient());}

 [PostgresFact] public async Task Concurrent_uploads_and_cross_action_same_key_finish_without_double_commit(){
  await using var app=new EvidenceApp();using var c=await Login(app);var target=await Parent(c);var reservation=await Ok(await Post(c,"",Metadata(target,Pdf)));var id=reservation.GetProperty("id").GetGuid();var key=Guid.NewGuid().ToString();
  var results=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Upload(c,id,Pdf,key:key))).WaitAsync(TimeSpan.FromSeconds(10));Assert.Single(results,x=>x.IsSuccessStatusCode);Assert.Equal(3,results.Count(x=>x.StatusCode==HttpStatusCode.Conflict));Assert.Single(Directory.GetFiles(app.Root));
  var second=await Ok(await Post(c,"",Metadata(target,Pdf)));var secondId=second.GetProperty("id").GetGuid();var shared=Guid.NewGuid().ToString();var race=await Task.WhenAll(Upload(c,secondId,Pdf,key:shared),Post(c,$"/{secondId}/supersede",new{metadata=Metadata(target,Pdf),reason="Synthetic race"},shared,1)).WaitAsync(TimeSpan.FromSeconds(10));Assert.True(race[0].IsSuccessStatusCode);Assert.True(race[1].StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed);
  await using var db=new NpgsqlConnection(app.Connection);await db.OpenAsync();await using var q=new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE entity_id=@id AND action='EVIDENCE_STORED'",db);q.Parameters.AddWithValue("id",id);Assert.Equal(1L,await q.ExecuteScalarAsync());
 }

 [PostgresFact] public async Task Missing_and_same_tenant_hidden_intakes_have_identical_detail_history_and_content_errors(){
  await using var app=new EvidenceApp();using var owner=await Login(app);using var hidden=await Login(app,"tenant-a");var target=await Parent(owner);
  var vesselIntake=await Ok(await Post(owner,"",Metadata(target,Pdf)));var certRequest=new HttpRequestMessage(HttpMethod.Post,"/api/v1/certificate-register"){Content=JsonContent.Create(new{data=new{vessel_id=target.ToString()}})};certRequest.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());var cert=(await Ok(await owner.SendAsync(certRequest))).GetProperty("id").GetGuid();
  var certificateIntake=await Ok(await Post(owner,"",new{targetType="Certificate",targetId=cert,filename="private-target.pdf",declaredMime="application/pdf",expectedBytes=Pdf.Length}));
  // Both tokens belong to the same tenant and company. The second actor lacks owner/explicit vessel access.
  var ownerScope=await owner.GetFromJsonAsync<Session>("/api/v1/session");var hiddenScope=await hidden.GetFromJsonAsync<Session>("/api/v1/session");Assert.Equal(ownerScope!.TenantId,hiddenScope!.TenantId);Assert.Equal(ownerScope.CompanyId,hiddenScope.CompanyId);Assert.NotEqual(ownerScope.Subject,hiddenScope.Subject);
  foreach(var action in new[]{"detail","history","content"}) {
   async Task<(HttpStatusCode Status,string Code,string Body)> Attempt(Guid id){using var response=action=="content"?await Upload(hidden,id,Pdf):await hidden.GetAsync($"{Route}/{id}"+(action=="history"?"/history":""));var body=await response.Content.ReadAsStringAsync();return(response.StatusCode,JsonDocument.Parse(body).RootElement.GetProperty("code").GetString()!,body);}
   var missing=await Attempt(Guid.NewGuid());Assert.Equal(HttpStatusCode.NotFound,missing.Status);Assert.Equal("not_found",missing.Code);Assert.Equal("{\"code\":\"not_found\",\"message\":\"Evidence intake not found.\"}",missing.Body);
   Assert.Equal(missing,await Attempt(vesselIntake.GetProperty("id").GetGuid()));Assert.Equal(missing,await Attempt(certificateIntake.GetProperty("id").GetGuid()));
  }
  // Denials must not start streams, write audit events, change versions or weaken capture permission checks.
  Assert.Empty(Directory.GetFiles(app.Root));Assert.Equal("RESERVED",(await Ok(await owner.GetAsync($"{Route}/{vesselIntake.GetProperty("id").GetGuid()}"))).GetProperty("state").GetString());
  var readableTarget=await Parent(hidden);Assert.Equal(HttpStatusCode.Forbidden,(await Post(hidden,"",Metadata(readableTarget,Pdf))).StatusCode);
 }

}
