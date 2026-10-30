using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
namespace Poseidon;
public static class InventoryEndpoints {
 public static void MapInventoryEndpoints(this WebApplication app) {
  Map(app.MapGroup("/api/v1/inventory/vessels"));
  if(!app.Services.GetRequiredService<BffSettings>().Enabled)return;
  var browser=app.MapGroup("/bff/inventory/vessels").RequireAuthorization(new AuthorizeAttribute{AuthenticationSchemes=BffAuthentication.CookieScheme});
  browser.AddEndpointFilter(async(context,next)=> {
   var c=context.HttpContext;
   if(!HttpMethods.IsGet(c.Request.Method))try {await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}catch(AntiforgeryValidationException){throw new ApiFault(400,"csrf_invalid","Request verification failed.");}
   if(c.Request.Headers["X-Poseidon-Scope"].ToString()!=IdentityBoundary.ScopeStamp(Contracts.Scope(c.User)))throw new ApiFault(401,"session_scope_changed","Reload the authenticated session.");
   return await next(context);
  });Map(browser);
 }
 static void Map(RouteGroupBuilder group) {
  group.MapGet("/capabilities",(HttpContext c)=>Results.Ok(new{canWriteDraft=c.User.HasClaim("permission","draft:write"),canManage=c.User.HasClaim("permission","inventory:manage")}));
  group.MapGet("",async(HttpContext c,DraftStore store,CancellationToken ct)=>{var scope=Contracts.Scope(c.User);return Results.Ok(await store.InventoryList(scope,InventoryBrowse.Parse(c.Request.Query,scope),ct));});
  group.MapGet("/{id:guid}",async(Guid id,HttpContext c,DraftStore store,CancellationToken ct)=>{var item=await store.InventoryGet(Contracts.Scope(c.User),id,ct);c.Response.Headers.ETag=$"\"{item.Version}\"";return Results.Ok(item);});
  group.MapGet("/{id:guid}/history",async(Guid id,HttpContext c,DraftStore store,CancellationToken ct)=>{var scope=Contracts.Scope(c.User);await store.InventoryGet(scope,id,ct);return Results.Ok(await store.InventoryHistory(scope,id,HistoryQuery.Parse(c.Request.Query,scope,id),ct));});
  group.MapPost("",async(JsonElement body,HttpContext c,DraftStore store,CancellationToken ct)=>await Write(null,"create",body,c,store,ct));
  foreach(var action in new[]{"metadata","record","reopen"}) {
   var suffix=action=="metadata"?"metadata":"commands/"+action;
   group.MapPost("/{id:guid}/"+suffix,async(Guid id,JsonElement? body,HttpContext c,DraftStore store,CancellationToken ct)=>await Write(id,action,body ?? (action=="record"?JsonSerializer.SerializeToElement(new{}):throw InventoryContracts.Invalid("body")),c,store,ct));
  }
 }
 static async Task<IResult> Write(Guid? id,string action,JsonElement body,HttpContext c,DraftStore store,CancellationToken ct) {
  var key=c.Request.Headers["Idempotency-Key"].ToString();
  if(string.IsNullOrEmpty(key))throw new ApiFault(428,"idempotency_required","Idempotency-Key is required.");
  if(!Guid.TryParseExact(key,"D",out _))throw new ApiFault(400,"invalid_idempotency_key","Idempotency-Key must be a UUID.");
  long? version=null;if(id.HasValue) {var raw=c.Request.Headers.IfMatch.ToString();if(raw.Length<3||raw[0]!='"'||raw[^1]!='"'||!long.TryParse(raw[1..^1],out var parsed)||parsed<1)throw new ApiFault(428,"version_required","Quoted positive If-Match required.");version=parsed;}
  var result=await store.InventoryWrite(c.User,id,action,version,body,key,c.TraceIdentifier,ct);c.Response.Headers.ETag=$"\"{result.Version}\"";return Results.Ok(result);
 }
}
