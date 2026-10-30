using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Poseidon;
public static class EvidenceEndpoints {
 public static void AddEvidence(this WebApplicationBuilder builder) {
  var enabled=builder.Configuration.GetValue<bool>("EvidenceIntake:Enabled");var root=builder.Configuration["EvidenceIntake:Root"];
  if(enabled&&!builder.Environment.IsDevelopment()&&!builder.Environment.IsEnvironment("Testing"))throw new InvalidOperationException("Evidence intake requires explicit Development or Testing environment.");
  builder.Services.AddSingleton(new EvidenceSettings(enabled,root));
  if(!enabled)return;
  if(string.IsNullOrWhiteSpace(root))throw new InvalidOperationException("Existing private EvidenceIntake:Root required.");
  // The local object root must not be served by this app or the sibling frontend.
  var fullRoot=Path.GetFullPath(root);var sourceRoot=new DirectoryInfo(builder.Environment.ContentRootPath);
  for(var current=sourceRoot;current is not null;current=current.Parent)if(File.Exists(Path.Combine(current.FullName,"Poseidon.sln"))||Directory.Exists(Path.Combine(current.FullName,".git"))){sourceRoot=current;break;}
  bool Within(string candidate,string boundary)=>candidate==boundary||candidate.StartsWith(boundary.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal);
  if(Within(fullRoot,sourceRoot.FullName)||(!string.IsNullOrEmpty(builder.Environment.WebRootPath)&&Within(fullRoot,Path.GetFullPath(builder.Environment.WebRootPath))))throw new InvalidOperationException("Evidence root must be outside application and static source trees.");
  // Resolved and validated at startup before serving; container owns descriptor disposal.
  builder.Services.AddSingleton<EvidenceObjectStore>(_=>new EvidenceObjectStore(root));builder.Services.TryAddSingleton<IEvidenceFaults,NoEvidenceFaults>();
 }
 public static void MapEvidence(this WebApplication app) {
  if(!app.Services.GetRequiredService<EvidenceSettings>().Enabled)return;
  Map(app.MapGroup("/api/v1/evidence-intake"));
  if(!app.Services.GetRequiredService<BffSettings>().Enabled)return;
  var b=app.MapGroup("/bff/api/v1/evidence-intake").RequireAuthorization(new AuthorizeAttribute{AuthenticationSchemes=BffAuthentication.CookieScheme});
  b.AddEndpointFilter(async(context,next)=>{var c=context.HttpContext;if(!HttpMethods.IsGet(c.Request.Method))try{await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);}catch(AntiforgeryValidationException){throw new ApiFault(400,"csrf_invalid","Request verification failed.");}if(c.Request.Headers["X-Poseidon-Scope"].ToString()!=IdentityBoundary.ScopeStamp(Contracts.Scope(c.User)))throw new ApiFault(401,"session_scope_changed","Reload session.");return await next(context);});Map(b);
 }
 static string Key(HttpContext c){var k=c.Request.Headers["Idempotency-Key"].ToString();if(k=="")throw new ApiFault(428,"idempotency_required","Idempotency-Key required.");if(!Guid.TryParseExact(k,"D",out _))throw new ApiFault(400,"invalid_idempotency_key","UUID required.");return k;}
 static long Version(HttpContext c){var s=c.Request.Headers.IfMatch.ToString();if(s.Length<3||s[0]!='"'||s[^1]!='"'||!long.TryParse(s[1..^1],out var v)||v<1)throw new ApiFault(428,"version_required","Quoted version required.");return v;}
 static IResult Result(HttpContext c,EvidenceIntake item){c.Response.Headers.ETag=$"\"{item.Version}\"";return Results.Ok(item);}
 static async Task<JsonDocument> ReadMetadata(HttpContext c,CancellationToken ct){using var m=new MemoryStream();var buffer=new byte[4096];int n;while((n=await c.Request.Body.ReadAsync(buffer,ct))>0){if(m.Length+n>16384)throw new ApiFault(413,"metadata_too_large","Metadata limit is 16 KiB.");m.Write(buffer,0,n);}return JsonDocument.Parse(m.ToArray());}
 static void Map(RouteGroupBuilder g) {
  g.MapGet("/capabilities",async(HttpContext c,DraftStore store,CancellationToken ct)=>{var q=c.Request.Query;if(q.Count!=2||q.Any(p=>p.Value.Count!=1)||!q.ContainsKey("targetType")||!Guid.TryParseExact(q["targetId"],"D",out var id))throw new ApiFault(400,"invalid_query","Target required.");var canCapture=await store.EvidenceCapability(c.User,q["targetType"].ToString(),id,ct);return Results.Ok(new{enabled=true,canCapture,maxBytes=EvidenceContracts.MaxBytes});});
  g.MapGet("",async(HttpContext c,DraftStore store,CancellationToken ct)=>Results.Ok(await store.EvidenceList(c.User,EvidenceBrowse.Parse(c.Request.Query,Contracts.Scope(c.User)),ct)));
  g.MapGet("/{id:guid}/history",async(Guid id,HttpContext c,DraftStore store,CancellationToken ct)=>Results.Ok(await store.EvidenceHistory(c.User,id,c.Request.Query,ct)));
  g.MapGet("/{id:guid}",async(Guid id,HttpContext c,DraftStore store,CancellationToken ct)=>Result(c,await store.EvidenceGet(c.User,id,ct)));
  g.MapPost("",async(HttpContext c,DraftStore store,CancellationToken ct)=>{var key=Key(c);if(c.Request.ContentLength>16384)throw new ApiFault(413,"metadata_too_large","Metadata limit is 16 KiB.");using var m=new MemoryStream();var buffer=new byte[4096];int n;while((n=await c.Request.Body.ReadAsync(buffer,ct))>0){if(m.Length+n>16384)throw new ApiFault(413,"metadata_too_large","Metadata limit is 16 KiB.");m.Write(buffer,0,n);}using var json=JsonDocument.Parse(m.ToArray());return Result(c,await store.EvidenceReserve(c.User,json.RootElement,key,c.TraceIdentifier,ct));});
  g.MapPost("/{id:guid}/supersede",async(Guid id,HttpContext c,DraftStore store,CancellationToken ct)=>{using var json=await ReadMetadata(c,ct);var body=json.RootElement;if(body.ValueKind!=JsonValueKind.Object||body.EnumerateObject().Count()!=2||!body.TryGetProperty("metadata",out var metadata)||!body.TryGetProperty("reason",out var reason)||reason.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(reason.GetString())||reason.GetString()!.Trim().Length>500)throw EvidenceContracts.Invalid("reason");return Result(c,await store.EvidenceReserve(c.User,metadata,Key(c),c.TraceIdentifier,ct,id,Version(c),reason.GetString()!.Trim()));});
  g.MapPost("/{id:guid}/content",async(Guid id,HttpContext c,DraftStore store,EvidenceObjectStore objects,IEvidenceFaults faults,CancellationToken ct)=>{var size=c.Features.Get<IHttpMaxRequestBodySizeFeature>();if(size is {IsReadOnly:false})size.MaxRequestBodySize=EvidenceContracts.MaxBytes+1;return Result(c,await store.EvidenceUpload(c.User,id,Version(c),Key(c),c.Request.ContentType??"",c.Request.Body,objects,faults,c.TraceIdentifier,ct));});
 }
}
