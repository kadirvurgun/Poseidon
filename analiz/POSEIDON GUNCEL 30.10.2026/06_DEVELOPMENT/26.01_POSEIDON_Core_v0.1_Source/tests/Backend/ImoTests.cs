using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
[Collection("PostgreSQL schema")]
public class ImoTests {
 [Theory][InlineData("1234567",true)][InlineData("1234568",false)][InlineData("9319466",true)][InlineData("8814275",true)][InlineData("8814274",false)][InlineData("123",false)][InlineData("１２３４５６７",false)][InlineData("١٢٣٤٥٦٧",false)][InlineData("1234567\n",false)][InlineData(" 1234567",false)][InlineData("IMO1234567",false)][InlineData("",false)]
 public void Ship_check_digit_and_ascii_are_consistent_across_contracts(string value,bool valid) {
  Assert.Equal(valid,ImoNumber.HasCheckDigit(value));
  var data=new{imo_no=value};
  var legacy=JsonSerializer.SerializeToElement(new{saveMode="DRAFT",data});
  var admin=JsonSerializer.SerializeToElement(new{data});
  if(valid){Contracts.Validate(legacy,"vessels");InventoryContracts.Validate(admin,SyntheticIdentity.Fixtures["tenant-a"]);}
  else {Assert.Equal(422,Assert.Throws<ApiFault>(()=>Contracts.Validate(legacy,"vessels")).Status);Assert.Equal(422,Assert.Throws<ApiFault>(()=>InventoryContracts.Validate(admin,SyntheticIdentity.Fixtures["tenant-a"])).Status);}
 }
 [Fact] public void Absent_is_allowed_and_company_number_does_not_use_ship_algorithm() {
  Contracts.Validate(JsonSerializer.SerializeToElement(new{saveMode="DRAFT",data=new{}}),"vessels");
  InventoryContracts.Validate(JsonSerializer.SerializeToElement(new{data=new{company_imo="1234568"}}),SyntheticIdentity.Fixtures["tenant-a"]);
 }
 static async Task<HttpResponseMessage> Post(HttpClient c,string path,object body,long? version=null) {
  using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
  request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
  if(version.HasValue)request.Headers.Add("If-Match",$"\"{version}\"");
  return await c.SendAsync(request);
 }
 [PostgresFact] public Task Legacy_http_checksum_rejection_preserves_history()=>VerifyHttp(false);
 [PostgresFact] public Task Administrative_http_checksum_rejection_preserves_history()=>VerifyHttp(true);
 async Task VerifyHttp(bool admin) {
  await using var app=new AppFactory();using var client=app.CreateClient();
  var login=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture="inventory-manager"});
  var token=await login.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token.GetProperty("accessToken").GetString());
  var store=app.Services.GetRequiredService<DraftStore>();var scope=SyntheticIdentity.Fixtures["inventory-manager"];
  // Deliberately simulate a pre-validation historical row, not a runtime seed.
  var old=await store.Save(scope,"vessels",null,null,JsonSerializer.SerializeToElement(new{name="Historical synthetic",flag="TR",vessel_type="Tug",asset_mode="TUG",imo_no="1234568"}),Guid.NewGuid().ToString(),"historical-test",default);
  var path=admin?$"/api/v1/inventory/vessels/{old.DraftId}":$"/api/v1/vessels/{old.DraftId}";
  var read=await client.GetFromJsonAsync<JsonElement>(path);Assert.Equal("1234568",read.GetProperty("data").GetProperty("imo_no").GetString());
  if(admin)Assert.Contains(read.GetProperty("completeness").GetProperty("invalidFields").EnumerateArray(),x=>x.GetString()=="imo_no");
  async Task<long> AuditCount(){await using var c=new NpgsqlConnection(TestDatabase.Connection);await c.OpenAsync();await using var q=new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE entity_id=@id",c);q.Parameters.AddWithValue("id",old.DraftId);return (long)(await q.ExecuteScalarAsync())!;}
  var before=await AuditCount();
  object invalid=admin?new{data=new{imo_no="1234568",notes="Must not commit"}}:new{saveMode="DRAFT",data=new{imo_no="1234568",notes="Must not commit"}};
  var rejected=await Post(client,path+(admin?"/metadata":""),invalid,old.Version);
  Assert.Equal((HttpStatusCode)422,rejected.StatusCode);
  Assert.Equal("invalid_imo_checksum",(await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
  Assert.Equal(old.Version,(await client.GetFromJsonAsync<JsonElement>(path)).GetProperty("version").GetInt64());
  Assert.Equal(before,await AuditCount());
  if(admin){Assert.Equal((HttpStatusCode)422,(await Post(client,path+"/commands/record",new{},old.Version)).StatusCode);Assert.Equal(before,await AuditCount());}
  // Omit optional IMO explicitly in a replacement; no generated replacement number.
  object corrected=admin?new{data=new{name="Historical synthetic",flag="TR",vessel_type="Tug",asset_mode="TUG"}}:new{saveMode="DRAFT",data=new{name="Historical synthetic"}};
  var saved=await Post(client,path+(admin?"/metadata":""),corrected,old.Version);saved.EnsureSuccessStatusCode();
  var final=await client.GetFromJsonAsync<JsonElement>(path);
  Assert.Equal(old.Version+1,final.GetProperty("version").GetInt64());Assert.False(final.GetProperty("data").TryGetProperty("imo_no",out _));
  Assert.Equal(before+1,await AuditCount());
  if(admin) {
   var recorded=await Post(client,path+"/commands/record",new{},old.Version+1);recorded.EnsureSuccessStatusCode();
   var count=await AuditCount();
   var badEdit=await Post(client,path+"/metadata",new{data=new{name="Historical synthetic",flag="TR",vessel_type="Tug",asset_mode="TUG",imo_no="1234568"}},old.Version+2);
   Assert.Equal((HttpStatusCode)422,badEdit.StatusCode);
   Assert.Equal(old.Version+2,(await client.GetFromJsonAsync<JsonElement>(path)).GetProperty("version").GetInt64());
   Assert.Equal(count,await AuditCount());
  }
 }
}
