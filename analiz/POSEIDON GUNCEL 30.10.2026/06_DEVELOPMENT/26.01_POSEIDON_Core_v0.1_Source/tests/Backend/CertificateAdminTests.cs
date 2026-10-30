using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
public sealed class CertificateClock : TimeProvider {
 public DateTimeOffset Now = new(2026,10,27,23,59,0,TimeSpan.Zero);
 public override DateTimeOffset GetUtcNow()=>Now;
}
public sealed class CertificateApp : ConfiguredAppFactory {
 public CertificateClock Clock {get;}=new();
 protected override Dictionary<string,string?> Settings=>new(){["ConnectionStrings:Poseidon"]=TestDatabase.Connection,["SyntheticIdentity:Enabled"]="true",["Database:Initialize"]="true"};
 protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.ConfigureTestServices(services=>{services.RemoveAll<TimeProvider>();services.AddSingleton<TimeProvider>(Clock);});
}
[Collection("PostgreSQL schema")]
public class CertificateAdminTests {
 const string Route="/api/v1/certificate-register";
 static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
 static ClaimsPrincipal User(Session s,bool write=true,bool cert=false,bool inventory=false)=>new(new ClaimsIdentity(new[]{new Claim("sub",s.Subject),new Claim("synthetic","true"),new Claim("tenant_id",s.TenantId.ToString()),new Claim("company_id",s.CompanyId.ToString())}.Concat(s.VesselIds.Select(v=>new Claim("vessel_id",v.ToString()))).Concat(write?[new Claim("permission","draft:write")]:Array.Empty<Claim>()).Concat(cert?[new Claim("permission","certificate:manage")]:Array.Empty<Claim>()).Concat(inventory?[new Claim("permission","inventory:manage")]:Array.Empty<Claim>()),"Synthetic"));
 static async Task<HttpClient> Login(CertificateApp app,string fixture="certificate-manager") {var client=app.CreateClient();var response=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture});response.EnsureSuccessStatusCode();var body=await response.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",body.GetProperty("accessToken").GetString());return client;}
 static async Task<(Guid id,long version)> Parent(CertificateApp app,bool recorded=false) {
  var scope=SyntheticIdentity.Fixtures["certificate-manager"];var store=app.Services.GetRequiredService<DraftStore>();var user=User(scope,true,true,true);
  var parent=await store.InventoryWrite(user,null,"create",null,Json(new{data=new{name="Certificate synthetic tug",flag="TR",vessel_type="Tug",asset_mode="TUG"}}),Guid.NewGuid().ToString(),"cert-test",default);
  if(recorded)parent=await store.InventoryWrite(user,parent.Id,"record",parent.Version,Json(new{}),Guid.NewGuid().ToString(),"cert-test",default);
  return(parent.Id,parent.Version);
 }
 static Task<HttpResponseMessage> Write(HttpClient c,string suffix,object body,long? version=null,string? key=null) {
  var request=new HttpRequestMessage(HttpMethod.Post,Route+suffix){Content=JsonContent.Create(body)};request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return c.SendAsync(request);
 }
 static async Task<JsonElement> Ok(HttpResponseMessage response) {var text=await response.Content.ReadAsStringAsync();Assert.True(response.IsSuccessStatusCode,$"HTTP {(int)response.StatusCode}: {text}");return JsonDocument.Parse(text).RootElement.Clone();}
 static string Id(JsonElement item)=>item.GetProperty("id").GetString()!;
 static long Version(JsonElement item)=>item.GetProperty("version").GetInt64();
 static async Task<long> Events(Guid id){await using var c=new NpgsqlConnection(TestDatabase.Connection);await c.OpenAsync();await using var q=new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE entity_id=@id",c);q.Parameters.AddWithValue("id",id);return (long)(await q.ExecuteScalarAsync())!;}
 [PostgresFact] public async Task Complete_admin_journey_preserves_recorded_parent_and_never_asserts_validity(){
  await using var app=new CertificateApp();using var client=await Login(app);var parent=await Parent(app,true);
  var partial=await Ok(await Write(client,"",new{data=new{vessel_id=parent.id.ToString()}}));var id=Id(partial);Assert.Equal("DRAFT",partial.GetProperty("adminState").GetString());Assert.False(partial.GetProperty("data").TryGetProperty("expiry",out _));Assert.Equal(new[]{"type","issuer"},partial.GetProperty("completeness").GetProperty("missingRequiredFields").EnumerateArray().Select(x=>x.GetString()));
  Assert.Equal((HttpStatusCode)422,(await Write(client,$"/{id}/commands/record",new{},Version(partial))).StatusCode);
  var full=await Ok(await Write(client,$"/{id}/metadata",new{data=new{vessel_id=parent.id.ToString(),type="Declared certificate",issuer="Declared authority",certificate_no="SYNTHETIC-DUPLICATE"}},Version(partial)));
  var key=Guid.NewGuid().ToString();var recorded=await Ok(await Write(client,$"/{id}/commands/record",new{},Version(full),key));Assert.Equal(id,Id(recorded));Assert.Equal("RECORDED",recorded.GetProperty("adminState").GetString());Assert.Equal("NO_EXPIRY_DATE",recorded.GetProperty("dateTracking").GetProperty("bucket").GetString());Assert.Equal("NOT_PERFORMED",recorded.GetProperty("verificationAssessment").GetString());Assert.Equal("NOT_PERFORMED",recorded.GetProperty("operationalAssessment").GetString());Assert.Equal("RECORDED",recorded.GetProperty("parentVessel").GetProperty("adminState").GetString());
  var replay=await Ok(await Write(client,$"/{id}/commands/record",new{},Version(full),key));Assert.Equal(Version(recorded),Version(replay));Assert.Equal(3,await Events(Guid.Parse(id)));
  var correction=new{data=new{vessel_id=parent.id.ToString(),type="Declared certificate",issuer="Declared authority",issue="2027-01-01",expiry="2027-12-31",notes="PRIVATE CERT NOTES"}};
  Assert.Equal((HttpStatusCode)422,(await Write(client,$"/{id}/metadata",correction,Version(recorded))).StatusCode);
  var corrected=await Ok(await Write(client,$"/{id}/metadata",new{correction.data,reason="PRIVATE CORRECTION REASON"},Version(recorded)));Assert.True(corrected.GetProperty("dateTracking").GetProperty("issueDateInFuture").GetBoolean());
  var reopened=await Ok(await Write(client,$"/{id}/commands/reopen",new{reason="PRIVATE REOPEN"},Version(corrected)));var cleared=await Ok(await Write(client,$"/{id}/metadata",new{data=new{vessel_id=parent.id.ToString()}},Version(reopened)));Assert.Equal("DRAFT",cleared.GetProperty("adminState").GetString());Assert.Equal((HttpStatusCode)422,(await Write(client,$"/{id}/commands/record",new{},Version(cleared))).StatusCode);
  var history=await client.GetStringAsync($"{Route}/{id}/history");foreach(var secret in new[]{"PRIVATE","synthetic-certificate-manager","before_data","after_data","reason","correlation_id"})Assert.DoesNotContain(secret,history);
  using var parsed=JsonDocument.Parse(history);Assert.All(parsed.RootElement.GetProperty("items").EnumerateArray(),row=>Assert.Equal(new[]{"eventId","occurredAt","version","action","changedFields","actorRelation"},row.EnumerateObject().Select(x=>x.Name)));
  // Parent reopening does not change either a DRAFT or RECORDED certificate.
  var stable=await Ok(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),type="Stable",issuer="Declared",expiry="2030-01-01"}}));
  stable=await Ok(await Write(client,$"/{Id(stable)}/commands/record",new{},Version(stable)));
  var scope=SyntheticIdentity.Fixtures["certificate-manager"];var store=app.Services.GetRequiredService<DraftStore>();await store.InventoryWrite(User(scope,true,true,true),parent.id,"reopen",parent.version,Json(new{reason="Parent metadata correction"}),Guid.NewGuid().ToString(),"test",default);
  var stillRecorded=await Ok(await client.GetAsync($"{Route}/{Id(stable)}"));Assert.Equal("RECORDED",stillRecorded.GetProperty("adminState").GetString());Assert.Equal(Version(stable),Version(stillRecorded));
  var after=await Ok(await client.GetAsync($"{Route}/{id}"));Assert.Equal(Version(cleared),Version(after));Assert.Equal("DRAFT",after.GetProperty("parentVessel").GetProperty("adminState").GetString());
  var duplicate=await Ok(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),certificate_no="SYNTHETIC-DUPLICATE"}}));Assert.NotEqual(id,Id(duplicate));
 }
 [PostgresFact] public async Task Date_boundaries_midnight_context_and_time_passage_do_not_mutate_aggregate(){
  await using var app=new CertificateApp();using var client=await Login(app);var parent=await Parent(app);
  var cases=new (int? days,string bucket)[]{(null,"NO_EXPIRY_DATE"),(-1,"PAST_DATE"),(0,"TODAY"),(1,"WITHIN_30_DAYS"),(30,"WITHIN_30_DAYS"),(31,"DAYS_31_TO_60"),(60,"DAYS_31_TO_60"),(61,"DAYS_61_TO_90"),(90,"DAYS_61_TO_90"),(91,"AFTER_90_DAYS")};
  foreach(var test in cases){var data=new Dictionary<string,string>{{"vessel_id",parent.id.ToString()},{"type","Date fixture"}};if(test.days.HasValue)data["expiry"]=new DateOnly(2026,10,27).AddDays(test.days.Value).ToString("yyyy-MM-dd");var row=await Ok(await Write(client,"",new{data}));var tracking=row.GetProperty("dateTracking");Assert.Equal(test.bucket,tracking.GetProperty("bucket").GetString());Assert.Equal("2026-10-27",tracking.GetProperty("asOfDate").GetString());Assert.Equal("UTC_CALENDAR_DATE",tracking.GetProperty("timeBasis").GetString());if(test.days.HasValue)Assert.Equal(test.days.Value,tracking.GetProperty("daysToRecordedExpiry").GetInt32());else Assert.Equal(JsonValueKind.Null,tracking.GetProperty("daysToRecordedExpiry").ValueKind);}
  var series="Midnight-"+Guid.NewGuid();var ids=new List<string>();for(var i=0;i<2;i++)ids.Add(Id(await Ok(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),type=series,expiry="2026-10-28"}}))));
  var prefix=$"{Route}?q={series}&dateBucket=WITHIN_30_DAYS&pageSize=1";var first=await Ok(await client.GetAsync(prefix));var cursor=first.GetProperty("nextCursor").GetString()!;Assert.Equal("2026-10-27",first.GetProperty("asOfDate").GetString());
  app.Clock.Now=new(2026,10,28,0,1,0,TimeSpan.Zero);
  var second=await Ok(await client.GetAsync(prefix+"&cursor="+Uri.EscapeDataString(cursor)));Assert.Equal("2026-10-27",second.GetProperty("asOfDate").GetString());Assert.Single(second.GetProperty("items").EnumerateArray());Assert.NotEqual(Id(first.GetProperty("items")[0]),Id(second.GetProperty("items")[0]));
  var fresh=await Ok(await client.GetAsync(prefix));Assert.Equal("2026-10-28",fresh.GetProperty("asOfDate").GetString());Assert.Empty(fresh.GetProperty("items").EnumerateArray());
  foreach(var id in ids){var detail=await Ok(await client.GetAsync($"{Route}/{id}"));Assert.Equal("TODAY",detail.GetProperty("dateTracking").GetProperty("bucket").GetString());Assert.Equal(1,Version(detail));Assert.Equal(1,await Events(Guid.Parse(id)));}
 }
 [PostgresFact] public async Task Closed_fields_dates_immutability_and_preconditions(){
  await using var app=new CertificateApp();using var client=await Login(app);var parent=await Parent(app);var other=await Parent(app);
  foreach(var field in new[]{"adminState","verification_state","readiness_state","submissionAllowed","issuer_ref","evidence_ref"})Assert.Equal((HttpStatusCode)422,(await Write(client,"",new{data=new Dictionary<string,object>{{"vessel_id",parent.id.ToString()},{field,"FORGED"}}})).StatusCode);
  using(var duplicate=new HttpRequestMessage(HttpMethod.Post,Route){Content=new StringContent("{\"data\":{\"vessel_id\":\""+parent.id+"\",\"issuer\":\"one\",\"issuer\":\"two\"}}",System.Text.Encoding.UTF8,"application/json")}){duplicate.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.Equal((HttpStatusCode)422,(await client.SendAsync(duplicate)).StatusCode);}
  foreach(var date in new[]{"2027-02-29","2026-13-01","2026-10-27T00:00:00Z"," "})Assert.Equal((HttpStatusCode)422,(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),expiry=date}})).StatusCode);
  Assert.Equal((HttpStatusCode)422,(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),issue="2028-02-29",expiry="2028-02-28"}})).StatusCode);
  var item=await Ok(await Write(client,"",new{data=new{vessel_id=parent.id.ToString(),issue="2028-02-29",expiry="2028-02-29"}}));var id=Id(item);
  Assert.Equal((HttpStatusCode)422,(await Write(client,$"/{id}/metadata",new{data=new{vessel_id=other.id.ToString()}},Version(item))).StatusCode);
  Assert.Equal((HttpStatusCode)428,(await client.PostAsJsonAsync(Route,new{data=new{vessel_id=parent.id.ToString()}})).StatusCode);Assert.Equal((HttpStatusCode)428,(await Write(client,$"/{id}/metadata",new{data=new{vessel_id=parent.id.ToString()}})).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync($"/api/v1/certificates/{id}/submit",new{})).StatusCode);
 }
 [PostgresFact] public async Task Legacy_replay_and_read_are_blocked_after_promotion_and_metadata_extension(){
  await using var app=new CertificateApp();using var client=await Login(app);var parent=await Parent(app);var scope=SyntheticIdentity.Fixtures["certificate-manager"];var store=app.Services.GetRequiredService<DraftStore>();var key=Guid.NewGuid().ToString();var data=Json(new{vessel_id=parent.id.ToString(),type="Legacy cert",issuer="Issuer"});var old=await store.Save(scope,"certificates",null,null,data,key,"test",default);
  var item=await Ok(await client.GetAsync($"{Route}/{old.DraftId}"));item=await Ok(await Write(client,$"/{old.DraftId}/metadata",new{data},Version(item)));
  Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>store.Save(scope,"certificates",null,null,data,key,"test",default))).Status);
  item=await Ok(await Write(client,$"/{old.DraftId}/commands/record",new{},Version(item)));
  Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync($"/api/v1/certificates/{old.DraftId}")).StatusCode);
  Assert.Equal(409,(await Assert.ThrowsAsync<ApiFault>(()=>store.Save(scope,"certificates",old.DraftId,Version(item),data,Guid.NewGuid().ToString(),"test",default))).Status);
  await using var c=new NpgsqlConnection(TestDatabase.Connection);await c.OpenAsync();await using var sql=new NpgsqlCommand("UPDATE drafts SET data='{}',version=version+1 WHERE id=@id",c);sql.Parameters.AddWithValue("id",old.DraftId);await Assert.ThrowsAsync<PostgresException>(()=>sql.ExecuteNonQueryAsync());
 }
}
