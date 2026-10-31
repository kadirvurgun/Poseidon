using Poseidon.Api;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

var builder = WebApplication.CreateBuilder(args);
// Framework request/auth/EF diagnostics can contain callback URLs or database details.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.None);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Services.AddFoundationIdentity(builder.Configuration);
builder.Services.AddScoped<SecurityAudit>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
    var actor = sp.GetRequiredService<ActorResolver>().Resolve(http.User) ?? throw new UnauthorizedAccessException();
    var connection = builder.Configuration.GetConnectionString("Poseidon") ?? builder.Configuration["POSEIDON_DATABASE"];
    if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
    return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options,
        actor.TenantId, new ActorContext(actor.Id.ToString(), "User", http.TraceIdentifier));
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Servers = [];
    return Task.CompletedTask;
}));

var app = builder.Build();
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.TraceIdentifier = Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    // Public diagnostics contain no tenant/user context and must not be cached.
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    try
    {
        await next(context);
        if (context.Response.StatusCode is 401 or 403)
            await context.RequestServices.GetRequiredService<SecurityAudit>().TryWriteAsync(context, "AccessDenied",
                context.Response.StatusCode == 401 ? "Unauthenticated" : "Denied");
    }
    catch (Exception exception) when (exception is AuditUnavailableException or Npgsql.NpgsqlException)
    {
        if (context.Response.HasStarted) throw;
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { code = "service_unavailable", correlationId = context.TraceIdentifier });
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapFoundation();

app.MapGet("/health/live", () => Results.Text("alive"))
    .WithName("GetLiveness")
    .WithSummary("Process liveness only; does not indicate database or operational readiness.");

app.MapGet("/api/v1/system/status", () => TypedResults.Ok(new SystemStatus("poseidon", "available")))
    .WithName("GetSystemStatus")
    .WithSummary("Public technical connectivity check. No business data or safety approval.");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Migration and synthetic import remain separate operator commands.
app.Run();

public partial class Program;
