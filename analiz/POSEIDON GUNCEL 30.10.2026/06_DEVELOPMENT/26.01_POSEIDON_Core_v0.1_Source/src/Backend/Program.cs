using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon;
var builder=WebApplication.CreateBuilder(args);
var uat=DevelopmentUat.Configure(builder);
var synthetic=builder.Configuration.GetValue<bool>("SyntheticIdentity:Enabled");
var migrate=builder.Configuration.GetValue<bool>("Database:Initialize");
if((synthetic||migrate)&&!builder.Environment.IsDevelopment())throw new InvalidOperationException("Synthetic identities and automatic schema initialization are Development-only.");
var connection=builder.Configuration.GetConnectionString("Poseidon")??throw new InvalidOperationException("ConnectionStrings:Poseidon is required; no in-memory fallback exists.");
builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));builder.Services.AddSingleton<DraftStore>();
if(uat.Enabled) builder.Services.AddAuthentication("UatDenied").AddScheme<AuthenticationSchemeOptions,DevelopmentDeniedHandler>("UatDenied",_=>{});
else if(synthetic) {builder.Services.AddSingleton<SyntheticIdentity>();builder.Services.AddAuthentication("Synthetic").AddScheme<AuthenticationSchemeOptions,SyntheticHandler>("Synthetic",_=>{});}
else {
 var authority=builder.Configuration["Authentication:Authority"]??throw new InvalidOperationException("OIDC Authentication:Authority required outside synthetic development.");
 var audience=builder.Configuration["Authentication:Audience"]??throw new InvalidOperationException("Authentication:Audience required.");
 IdentityBoundary.RequireHttps(authority);
 if(string.IsNullOrWhiteSpace(audience))throw new InvalidOperationException("Authentication:Audience required.");
 builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{o.Authority=authority;o.Audience=audience;o.RequireHttpsMetadata=true;o.MapInboundClaims=false;});
}
builder.Services.AddAuthorization(o=>{o.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();o.AddPolicy("draft-write",p=>p.RequireAuthenticatedUser().RequireClaim("permission","draft:write"));});
builder.Services.AddPoseidonBff(builder.Configuration, synthetic, uat);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=32768);
builder.AddEvidence();
var app=builder.Build();
uat.UseBoundary(app);
app.Use(async(context,next)=>{try {await next(context);}catch(ApiFault e){context.Response.StatusCode=e.Status;await context.Response.WriteAsJsonAsync(new {code=e.Code,message=e.Message});}catch(BadHttpRequestException e){context.Response.StatusCode=e.StatusCode;await context.Response.WriteAsJsonAsync(new {code="invalid_request",message="Request body is invalid or exceeds the size limit."});}catch(JsonException){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new {code="invalid_json",message="Invalid JSON."});}catch(NpgsqlException e){app.Logger.LogError(e,"Database operation failed ({Correlation})",context.TraceIdentifier);context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new {code="database_unavailable",message="Save was not confirmed. Retry with the same idempotency key."});}catch(Exception e){app.Logger.LogError(e,"Unhandled API error ({Correlation})",context.TraceIdentifier);if(!context.Response.HasStarted){context.Response.Clear();context.Response.StatusCode=500;await context.Response.WriteAsJsonAsync(new {code="internal_error",message="Request failed; no success is confirmed."});}}});
app.ValidatePoseidonBff();
app.UseAuthentication();app.UseAuthorization();
app.MapPoseidonBff();
app.MapGet("/health/ready",async(NpgsqlDataSource source,CancellationToken ct)=> {
 try {await using var connection=await source.OpenConnectionAsync(ct);await using var cmd=new NpgsqlCommand("SELECT current_setting('server_version_num')::int >= 170000 AND to_regclass('drafts') IS NOT NULL AND to_regclass('audit_events') IS NOT NULL AND to_regclass('companies') IS NOT NULL AND to_regclass('idempotency') IS NOT NULL AND EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='drafts' AND column_name='certificate_schema_version')",connection);var ready=(bool)(await cmd.ExecuteScalarAsync(ct))!;return Results.Json(new {status=ready?"ready":"not_ready",persistence="PostgreSQL"},statusCode:ready?200:503);}
 catch(NpgsqlException){return Results.Json(new {status="not_ready",persistence="PostgreSQL"},statusCode:503);}
}).AllowAnonymous();
if(synthetic)app.MapPost("/api/v1/dev/session",(JsonElement body,SyntheticIdentity identities)=> {
 if(body.ValueKind!=JsonValueKind.Object||!body.TryGetProperty("fixture",out var fixture)||fixture.ValueKind!=JsonValueKind.String||body.EnumerateObject().Count()!=1)throw new ApiFault(422,"invalid_fixture","A fixture name is required.");
 return Results.Ok(identities.Issue(fixture.GetString()!));
}).AllowAnonymous();
app.MapGet("/api/v1/session",(HttpContext c)=>Results.Ok(Contracts.Scope(c.User)));
app.MapDraftEndpoints();
app.MapInventoryEndpoints();
app.MapCertificateEndpoints();
app.MapEvidence();
if(migrate) {await app.Services.GetRequiredService<DraftStore>().Migrate(CancellationToken.None);if(synthetic || uat.Enabled)await app.Services.GetRequiredService<DraftStore>().Seed(CancellationToken.None);}
if(app.Services.GetRequiredService<EvidenceSettings>().Enabled)await app.Services.GetRequiredService<DraftStore>().EvidenceReconcile(app.Services.GetRequiredService<EvidenceObjectStore>(),CancellationToken.None);
uat.MapUi(app);
app.Run();
public partial class Program { }
