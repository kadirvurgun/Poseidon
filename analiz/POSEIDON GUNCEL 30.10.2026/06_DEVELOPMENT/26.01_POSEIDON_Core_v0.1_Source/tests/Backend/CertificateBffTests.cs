using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Poseidon.Tests;
public partial class BffTests {
 [PostgresFact] public async Task Certificate_BFF_requires_cookie_scope_csrf_and_independent_grant_on_replay(){
  await using var app=new App(database:true);app.Grants.CanManageCertificates=true;using var client=app.Client();await app.Services.GetRequiredService<DraftStore>().Migrate(default);await app.Services.GetRequiredService<DraftStore>().Seed(default);await client.GetAsync("/test/signin");
  var scope=(await client.GetFromJsonAsync<Session>("/bff/session"))!;client.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var parentResponse=await BrowserSave(client,"/bff/api/v1/vessels",new{name="BFF certificate parent"});parentResponse.EnsureSuccessStatusCode();var parent=(await parentResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("draftId").GetString();
  const string path="/bff/api/v1/certificate-register";var body=new{data=new{vessel_id=parent,type="Synthetic",issuer="Declared"}};
  async Task<HttpResponseMessage> Post(string suffix,object value,bool csrf=true,long? version=null,string? key=null){using var request=new HttpRequestMessage(HttpMethod.Post,path+suffix){Content=JsonContent.Create(value)};request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());if(csrf)request.Headers.Add("X-CSRF-TOKEN",await Csrf(client));if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return await client.SendAsync(request);}
  Assert.Equal(HttpStatusCode.BadRequest,(await Post("",body,false)).StatusCode);client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");client.DefaultRequestHeaders.Add("X-Poseidon-Scope","forged");Assert.Equal(HttpStatusCode.Unauthorized,(await Post("",body)).StatusCode);client.DefaultRequestHeaders.Remove("X-Poseidon-Scope");client.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var created=await Post("",body);created.EnsureSuccessStatusCode();var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();var key=Guid.NewGuid().ToString();var recorded=await Post($"/{id}/commands/record",new{},version:1,key:key);recorded.EnsureSuccessStatusCode();app.Grants.CanManageCertificates=false;app.Grants.CanManageInventory=true;
  Assert.Equal(HttpStatusCode.Forbidden,(await Post($"/{id}/commands/record",new{},version:1,key:key)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await Post($"/{id}/metadata",new{body.data,reason="Not authorized by inventory permission"},version:2)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"{path}/{id}/history")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/v1/certificate-register/{id}")).StatusCode);app.Store.Expire();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"{path}/{id}")).StatusCode);
 }
}
