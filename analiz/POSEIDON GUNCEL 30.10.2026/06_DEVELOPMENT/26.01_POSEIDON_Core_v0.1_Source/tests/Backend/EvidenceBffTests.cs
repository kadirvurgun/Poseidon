using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
public partial class BffTests {
 [PostgresFact] public async Task Evidence_BFF_upload_requires_csrf_scope_current_grants_and_no_cookie_bearer_crossover(){
  await using var data=new EvidenceApp();await using(var source=NpgsqlDataSource.Create(data.Connection)){var store=new DraftStore(source);await store.Migrate(default);await store.Seed(default);}
  await using var app=new App(database:true,evidenceRoot:data.Root,evidenceDatabase:data.Connection);app.Grants.CanManageInventory=true;app.Grants.CanManageCertificates=true;app.Grants.CanCaptureEvidence=true;using var c=app.Client();await c.GetAsync("/test/signin");var scope=(await c.GetFromJsonAsync<Session>("/bff/session"))!;c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var parent=(await (await BrowserSave(c,"/bff/api/v1/vessels",new{name="Evidence BFF target"})).Content.ReadFromJsonAsync<Draft>())!;var bytes=Encoding.ASCII.GetBytes("%PDF-1.4\nsynthetic\n%%EOF");const string route="/bff/api/v1/evidence-intake";
  async Task<HttpResponseMessage> Post(string suffix,object body,string? key=null){var request=new HttpRequestMessage(HttpMethod.Post,route+suffix){Content=JsonContent.Create(body)};request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());request.Headers.Add("X-CSRF-TOKEN",await Csrf(c));return await c.SendAsync(request);}
  var body=new{targetType="Vessel",targetId=parent.DraftId,filename="synthetic.pdf",declaredMime="application/pdf",expectedBytes=bytes.Length};var reserve=await Post("",body);reserve.EnsureSuccessStatusCode();var id=(await reserve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  async Task<HttpResponseMessage> Upload(bool csrf=true,Guid? intake=null){var request=new HttpRequestMessage(HttpMethod.Post,$"{route}/{intake??id}/content"){Content=new ByteArrayContent(bytes)};request.Content.Headers.ContentType=new("application/pdf");request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());request.Headers.TryAddWithoutValidation("If-Match","\"1\"");if(csrf)request.Headers.Add("X-CSRF-TOKEN",await Csrf(c));return await c.SendAsync(request);}
  Assert.Equal(HttpStatusCode.BadRequest,(await Upload(false)).StatusCode);app.Grants.CanCaptureEvidence=false;Assert.Equal(HttpStatusCode.Forbidden,(await Upload()).StatusCode);app.Grants.CanCaptureEvidence=true;
  c.DefaultRequestHeaders.Remove("X-Poseidon-Scope");c.DefaultRequestHeaders.Add("X-Poseidon-Scope","old-account");Assert.Equal(HttpStatusCode.Unauthorized,(await Upload()).StatusCode);c.DefaultRequestHeaders.Remove("X-Poseidon-Scope");c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(scope));
  var accepted=await Upload();accepted.EnsureSuccessStatusCode();Assert.Equal("STORED_QUARANTINED",(await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync($"/api/v1/evidence-intake/{id}")).StatusCode);
  var certificate=(await (await BrowserSave(c,"/bff/api/v1/certificates",new{vessel_id=parent.DraftId.ToString()})).Content.ReadFromJsonAsync<Draft>())!;var certReserve=await Post("",new{targetType="Certificate",targetId=certificate.DraftId,filename="synthetic.pdf",declaredMime="application/pdf",expectedBytes=bytes.Length});certReserve.EnsureSuccessStatusCode();var certIntake=(await certReserve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  var original=await c.GetStringAsync($"{route}/{id}");await c.GetAsync("/test/signin?subject=replacement-account");var replacement=(await c.GetFromJsonAsync<Session>("/bff/session"))!;Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync($"{route}/{id}")).StatusCode);c.DefaultRequestHeaders.Remove("X-Poseidon-Scope");c.DefaultRequestHeaders.Add("X-Poseidon-Scope",IdentityBoundary.ScopeStamp(replacement));Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"{route}/{id}")).StatusCode);
  foreach(var action in new[]{"detail","history","content"}) {
   async Task<(HttpStatusCode Status,string Code,string Body)> Attempt(Guid target){using var response=action=="content"?await Upload(intake:target):await c.GetAsync($"{route}/{target}"+(action=="history"?"/history":""));var text=await response.Content.ReadAsStringAsync();return(response.StatusCode,JsonDocument.Parse(text).RootElement.GetProperty("code").GetString()!,text);}
   var missing=await Attempt(Guid.NewGuid());Assert.Equal(HttpStatusCode.NotFound,missing.Status);Assert.Equal("not_found",missing.Code);Assert.Equal(missing,await Attempt(id));Assert.Equal(missing,await Attempt(certIntake));
  }
  app.Store.Expire();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync($"{route}/{id}")).StatusCode);Assert.DoesNotContain(data.Root,original);
 }
}
