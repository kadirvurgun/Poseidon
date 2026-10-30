using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon;
using Xunit;
namespace Poseidon.Tests;
[Collection("PostgreSQL schema")]
public class InventoryAdminTests {
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 static JsonElement Body(object value)=>JsonSerializer.SerializeToElement(value);
 static ClaimsPrincipal User(Session s,bool writer=true,bool manager=false)=>new(new ClaimsIdentity(new[]{new Claim("sub",s.Subject),new Claim("synthetic","true"),new Claim("tenant_id",s.TenantId.ToString()),new Claim("company_id",s.CompanyId.ToString())}.Concat(s.VesselIds.Select(id=>new Claim("vessel_id",id.ToString()))).Concat(writer?[new Claim("permission","draft:write")]:Array.Empty<Claim>()).Concat(manager?[new Claim("permission","inventory:manage")]:Array.Empty<Claim>()),"Synthetic"));
 static object Complete(string mode="TUG")=>new {data=new{name="Synthetic admin tug",flag="Declared TR",vessel_type="Declared tug",asset_mode=mode}};
 static Task<InventoryVessel> Write(DraftStore store,ClaimsPrincipal user,string action,object body,InventoryVessel? record=null,string? key=null)=>store.InventoryWrite(user,record?.Id,action,record?.Version,Body(body),key??Guid.NewGuid().ToString(),"synthetic-test",default);
 static async Task Fault(int status,string code,Func<Task> call){var ex=await Assert.ThrowsAsync<ApiFault>(call);Assert.Equal(status,ex.Status);Assert.Equal(code,ex.Code);}
 static async Task<HttpClient> Login(AppFactory app,string fixture) {var client=app.CreateClient();var response=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture});response.EnsureSuccessStatusCode();var body=await response.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",body.GetProperty("accessToken").GetString());return client;}
 [Theory]
 [InlineData("0",true)][InlineData("999999999.999",true)][InlineData("1.234",true)][InlineData("1.2340",true)][InlineData("1234e-3",true)][InlineData("1.2341",false)][InlineData("-1",false)][InlineData("1000000000",false)][InlineData("1e100",false)][InlineData("0.0000000000000000000000000000001",false)]
 public void Numeric_precision_never_rounds(string input,bool valid){using var doc=JsonDocument.Parse(input);Assert.Equal(valid,InventoryContracts.ExactNumber(doc.RootElement,out _));}
 [Theory]
 [InlineData("{\"data\":{\"asset_mode\":\"OTHER\"}}")][InlineData("{\"data\":{\"status\":\"READY\"}}")][InlineData("{\"data\":{\"master_name\":true}}")][InlineData("{\"data\":{\"build_date\":\"2026-02-30\"}}")][InlineData("{\"data\":{\"company_imo\":\"123\"}}")][InlineData("{\"data\":{\"name\":\"x\",\"name\":\"y\"}}")]
 public void Closed_typed_metadata(string json){using var doc=JsonDocument.Parse(json);Assert.Throws<ApiFault>(()=>InventoryContracts.Validate(doc.RootElement,SyntheticIdentity.Fixtures["tenant-a"]));}
 [PostgresFact] public async Task Complete_journey_preserves_identity_certificates_history_and_legacy_guards(){
  await using var app=new AppFactory();using var client=await Login(app,"inventory-manager");var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["inventory-manager"];var user=User(scope,true,true);
  var empty=await Write(store,user,"create",new{data=new{}});Assert.Equal("DRAFT",empty.AdminState);Assert.False(empty.Data.TryGetProperty("asset_mode",out _));Assert.False(empty.Data.TryGetProperty("imo_no",out _));Assert.Equal(new[]{"name","flag","vessel_type","asset_mode"},empty.Completeness.MissingRequiredFields);
  await Fault(422,"inventory_incomplete",()=>Write(store,user,"record",new{},empty));
  var draft=await Write(store,user,"metadata",Complete(),empty);
  var key=Guid.NewGuid().ToString();var recorded=await Write(store,user,"record",new{},draft,key);Assert.Equal(empty.Id,recorded.Id);Assert.Equal("RECORDED",recorded.AdminState);Assert.False(recorded.Data.TryGetProperty("imo_no",out _));Assert.Equal(JsonSerializer.Serialize(recorded,Json),JsonSerializer.Serialize(await Write(store,user,"record",new{},draft,key),Json));
  await Fault(409,"invalid_transition",()=>Write(store,user,"record",new{},recorded));
  await Fault(422,"reopen_required",()=>Write(store,user,"metadata",Complete("SHIP"),recorded));
  await Fault(422,"inventory_incomplete",()=>Write(store,user,"metadata",new{data=new{name="Only name"}},recorded));
  var certificate=await store.Save(scope,"certificates",null,null,Body(new{vessel_id=recorded.Id.ToString()}),Guid.NewGuid().ToString(),"test",default);Assert.Equal(recorded.Id.ToString(),certificate.Data.GetProperty("vessel_id").GetString());
  Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync($"/api/v1/vessels/{recorded.Id}")).StatusCode);
  var managedKey=Guid.NewGuid().ToString();var updated=await Write(store,user,"metadata",new{data=new{name="Corrected",flag="TR",vessel_type="Tug",asset_mode="TUG",notes="PRIVATE NOTES",master_name="PRIVATE CONTACT",dpa_name="PRIVATE DPA",gt=0,nt=1.234m,evidence_ref="Reference only"}},recorded,managedKey);
  var reopened=await Write(store,user,"reopen",new{reason="PRIVATE REOPEN REASON"},updated);Assert.Equal("DRAFT",reopened.AdminState);
  await Fault(403,"action_forbidden",()=>Write(store,User(scope),"metadata",new{data=new{name="Corrected",flag="TR",vessel_type="Tug",asset_mode="TUG",notes="PRIVATE NOTES",master_name="PRIVATE CONTACT",dpa_name="PRIVATE DPA",gt=0,nt=1.234m,evidence_ref="Reference only"}},recorded,managedKey));
  var legacy=await store.Get(scope,"vessels",reopened.Id,default);Assert.NotNull(legacy);Assert.False(legacy.Data.TryGetProperty("gt",out _));Assert.False(legacy.Data.TryGetProperty("master_name",out _));Assert.All(legacy.Data.EnumerateObject(),p=>Assert.Equal(JsonValueKind.String,p.Value.ValueKind));
  await Fault(409,"inventory_resource_required",()=>store.Save(scope,"vessels",reopened.Id,reopened.Version,Body(new{name="Erase"}),Guid.NewGuid().ToString(),"test",default));
  var final=await Write(store,user,"record",new{},reopened);Assert.Equal(recorded.Id,final.Id);
  var history=await store.InventoryHistory(scope,final.Id,HistoryQuery.Parse(new QueryCollection(),scope,final.Id),default);Assert.Equal(6,history.Items.Count);Assert.All(history.Items,h=>Assert.Equal("SELF",h.ActorRelation));
  var http=await client.GetStringAsync($"/api/v1/inventory/vessels/{final.Id}/history");foreach(var secret in new[]{"PRIVATE","synthetic-inventory-manager","before_data","after_data","restricted_metadata","correlation","actor\"","issuer","companyId"})Assert.DoesNotContain(secret,http);
  var body=JsonDocument.Parse(http).RootElement;Assert.All(body.GetProperty("items").EnumerateArray(),item=>Assert.Equal(new[]{"eventId","occurredAt","version","action","changedFields","actorRelation"},item.EnumerateObject().Select(p=>p.Name)));
  await using var c=new NpgsqlConnection(TestDatabase.Connection);await c.OpenAsync();await using var old=new NpgsqlCommand("UPDATE drafts SET data='{}',version=version+1 WHERE id=@id",c);old.Parameters.AddWithValue("id",final.Id);await Assert.ThrowsAsync<PostgresException>(()=>old.ExecuteNonQueryAsync());Assert.Equal(final.Version,(await store.InventoryGet(scope,final.Id,default)).Version);
 }
 [PostgresFact] public async Task Scope_capability_replay_and_history_cursors_are_independent(){
  await using var app=new AppFactory();using var client=await Login(app,"inventory-manager");var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["inventory-manager"];var manager=User(scope,true,true);var draft=await Write(store,manager,"create",Complete());
  await Fault(403,"action_forbidden",()=>Write(store,User(scope),"record",new{},draft));
  var key=Guid.NewGuid().ToString();var recorded=await Write(store,manager,"record",new{},draft,key);
  await Fault(403,"action_forbidden",()=>Write(store,User(scope),"record",new{},draft,key));
  var reader=scope with{Subject="synthetic-reader",VesselIds=[recorded.Id]};Assert.Equal(recorded.Id,(await store.InventoryGet(reader,recorded.Id,default)).Id);
  await Fault(403,"action_forbidden",()=>Write(store,User(reader,false),"reopen",new{reason="no"},recorded));
  var hidden=reader with{VesselIds=[]};await Fault(404,"not_found",()=>store.InventoryGet(hidden,recorded.Id,default));await Fault(404,"not_found",()=>store.InventoryHistory(hidden,recorded.Id,HistoryQuery.Parse(new QueryCollection(),hidden,recorded.Id),default));
  await Fault(404,"not_found",()=>store.InventoryGet(scope with{CompanyId=Guid.NewGuid()},recorded.Id,default));await Fault(404,"not_found",()=>store.InventoryGet(scope with{TenantId=Guid.NewGuid()},recorded.Id,default));
  var query=new QueryCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{{"pageSize","1"}});var page=await store.InventoryHistory(reader,recorded.Id,HistoryQuery.Parse(query,reader,recorded.Id),default);Assert.True(page.HasMore);Assert.Equal("OTHER",page.Items[0].ActorRelation);
  var next=new QueryCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{{"pageSize","1"},{"cursor",page.NextCursor}});Assert.Throws<ApiFault>(()=>HistoryQuery.Parse(next,reader,Guid.NewGuid()));Assert.Throws<ApiFault>(()=>HistoryQuery.Parse(next,hidden,recorded.Id));
  var rest=await store.InventoryHistory(reader,recorded.Id,HistoryQuery.Parse(next,reader,recorded.Id),default);Assert.Single(rest.Items);Assert.NotEqual(page.Items[0].EventId,rest.Items[0].EventId);
  var recordQuery=new QueryCollection(new Dictionary<string,Microsoft.Extensions.Primitives.StringValues>{{"adminState","RECORDED"},{"asset_mode","TUG"}});var browse=await store.InventoryList(reader,InventoryBrowse.Parse(recordQuery,reader),default);Assert.Single(browse.Items);Assert.Equal(recorded.Id,browse.Items[0].Id);
 }
 [PostgresFact] public async Task Legacy_create_replay_cannot_return_false_draft_after_inventory_edit(){
  await using var app=new AppFactory();using var client=await Login(app,"tenant-a");var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["tenant-a"];var key=Guid.NewGuid().ToString();var data=Body(new{name="Legacy"});var old=await store.Save(scope,"vessels",null,null,data,key,"test",default);
  var current=await store.InventoryGet(scope,old.DraftId,default);var extended=await Write(store,User(scope,true,true),"metadata",Complete(),current);
  await Fault(409,"inventory_resource_required",()=>store.Save(scope,"vessels",null,null,data,key,"test",default));
  await Write(store,User(scope,true,true),"record",new{},extended);
  await Fault(409,"inventory_resource_required",()=>store.Save(scope,"vessels",null,null,data,key,"test",default));
 }
 [PostgresFact] public async Task Race_serializes_version_permission_state_and_exactly_once_events(){
  await using var app=new AppFactory();using var client=await Login(app,"inventory-manager");var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["inventory-manager"];var manager=User(scope,true,true);var draft=await Write(store,manager,"create",Complete());var key=Guid.NewGuid().ToString();
  var replies=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Write(store,manager,"record",new{},draft,key)));Assert.All(replies,x=>Assert.Equal(2,x.Version));
  await Fault(409,"idempotency_conflict",()=>Write(store,manager,"reopen",new{reason="changed request"},replies[0],key));
  var current=replies[0];async Task<int> Attempt(Func<Task> f){try{await f();return 200;}catch(ApiFault e){return e.Status;}}
  var results=await Task.WhenAll(Attempt(()=>Write(store,manager,"reopen",new{reason="correct"},current)),Attempt(()=>Write(store,manager,"metadata",Complete(),current)));Assert.Single(results,x=>x==200);Assert.Single(results,x=>x==412);
  var history=await store.InventoryHistory(scope,draft.Id,HistoryQuery.Parse(new QueryCollection(),scope,draft.Id),default);Assert.Equal(3,history.Items.Count);
 }
 [PostgresFact] public async Task Http_preconditions_and_closed_commands(){
  await using var app=new AppFactory();using var client=await Login(app,"inventory-manager");
  Assert.Equal((HttpStatusCode)428,(await client.PostAsJsonAsync("/api/v1/inventory/vessels",new{data=new{}})).StatusCode);
  using var create=new HttpRequestMessage(HttpMethod.Post,"/api/v1/inventory/vessels"){Content=JsonContent.Create(Complete())};create.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());var response=await client.SendAsync(create);response.EnsureSuccessStatusCode();var vessel=await response.Content.ReadFromJsonAsync<InventoryVessel>(Json);Assert.NotNull(vessel);
  using var record=new HttpRequestMessage(HttpMethod.Post,$"/api/v1/inventory/vessels/{vessel.Id}/commands/record"){Content=JsonContent.Create(new{})};record.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.Equal((HttpStatusCode)428,(await client.SendAsync(record)).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/inventory/vessels?adminState=READY")).StatusCode);
 }
}
