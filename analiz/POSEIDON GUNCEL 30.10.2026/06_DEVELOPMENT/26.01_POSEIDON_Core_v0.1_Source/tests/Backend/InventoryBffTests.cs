using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Poseidon.Tests;
public partial class BffTests {
 [PostgresFact] public async Task Inventory_cookie_routes_require_csrf_scope_and_current_independent_grant(){
  await using var app=new App(database:true);app.Grants.CanManageInventory=true;using var client=app.Client();await app.Services.GetRequiredService<DraftStore>().Migrate(default);await app.Services.GetRequiredService<DraftStore>().Seed(default);
  await client.GetAsync("/test/signin");var scope=(await client.GetFromJsonAsync<Session>("/bff/session"))!;client.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var body=new{data=new{name="Cookie admin",flag="TR",vessel_type="Tug",asset_mode="TUG"}};
  async Task<HttpResponseMessage> Post(string path,object value,bool csrf=true,long? version=null,string? key=null){using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(value)};request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());if(csrf)request.Headers.Add("X-CSRF-TOKEN",await Csrf(client));if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return await client.SendAsync(request);}
  const string path="/bff/inventory/vessels";
  Assert.Equal(HttpStatusCode.BadRequest,(await Post(path,body,false)).StatusCode);
  client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");client.DefaultRequestHeaders.Add("X-Poseidon-Scope","forged");Assert.Equal(HttpStatusCode.Unauthorized,(await Post(path,body)).StatusCode);
  client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");client.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var created=await Post(path,body);created.EnsureSuccessStatusCode();var json=await created.Content.ReadFromJsonAsync<JsonElement>();var id=json.GetProperty("id").GetGuid();var command=$"{path}/{id}/commands/record";var key=Guid.NewGuid().ToString();
  var recorded=await Post(command,new{},version:1,key:key);recorded.EnsureSuccessStatusCode();
  app.Grants.CanManageInventory=false;
  Assert.Equal(HttpStatusCode.Forbidden,(await Post(command,new{},version:1,key:key)).StatusCode);
  Assert.Equal(HttpStatusCode.Forbidden,(await Post($"{path}/{id}/metadata",body,version:2)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"{path}/{id}/history")).StatusCode);
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/v1/inventory/vessels/{id}")).StatusCode);
  app.Store.Expire();Assert.Equal(HttpStatusCode.Unauthorized,(await Post(command,new{},version:1,key:key)).StatusCode);
 }
}
