using Poseidon.Api;

var builder = WebApplication.CreateBuilder(args);
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
    // Public diagnostics contain no tenant/user context and must not be cached.
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next(context);
});

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

// No database registration, automatic migration, demo login or business endpoints.
app.Run();

public partial class Program;
