using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed class Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        // The generic host completes application configuration before invoking Startup.
        // OIDC scheme registration and membership resolution must share that final configuration.
        services.AddRouting();
        services.AddFoundationIdentity(configuration, hostEnvironment);
        services.AddScoped<SecurityAudit>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, DirectoryAuthorizationResultHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped(sp =>
        {
            var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            var actor = sp.GetRequiredService<ActorResolver>().Resolve(http.User) ?? throw new UnauthorizedAccessException();
            var connection = configuration.GetConnectionString("Poseidon") ?? configuration["POSEIDON_DATABASE"];
            if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
            return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options,
                actor.TenantId, new ActorContext(actor.Id.ToString(), "User", http.TraceIdentifier));
        });
        services.AddProblemDetails();
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Servers = [];
            return Task.CompletedTask;
        }));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
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
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        var labAssets = configuration["DevelopmentLab:WebRoot"];
        if (!string.IsNullOrEmpty(labAssets))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Lab assets require Development.");
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(labAssets) });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(labAssets) });
        }
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapFoundation();
            endpoints.MapRegulatoryDrafts();
            endpoints.MapGet("/health/live", () => Results.Text("alive"))
                .WithName("GetLiveness")
                .WithSummary("Process liveness only; does not indicate database or operational readiness.");

            endpoints.MapGet("/api/v1/system/status", () => TypedResults.Ok(new SystemStatus("poseidon", "available")))
                .WithName("GetSystemStatus")
                .WithSummary("Public technical connectivity check. No business data or safety approval.");

            if (environment.IsDevelopment()) endpoints.MapOpenApi();
            if (environment.IsDevelopment() && !string.IsNullOrEmpty(labAssets))
            {
                endpoints.MapFallbackToFile("/app/{*path:nonfile}", "index.html", new StaticFileOptions
                { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(labAssets) });
                endpoints.MapFallbackToFile("/agents", "index.html", new StaticFileOptions
                { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(labAssets) });
            }
        });
        // Migration and synthetic import remain separate operator commands.
    }
}
