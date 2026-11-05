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
        // Numeric wire values remain numbers; do not advertise implicit string coercion.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling =
            System.Text.Json.Serialization.JsonNumberHandling.Strict);
        services.AddDataProtection();
        services.AddCertificateExtraction(configuration);
        services.AddSingleton(configuration.GetSection("TugPlanning").Get<TugPlanningPolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(configuration.GetSection("Notifications").Get<NotificationSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddScoped<NotificationEngine>();
        services.AddSingleton(configuration.GetSection("DailyOffline").Get<DailyOfflineSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("NotificationScheduler").Get<NotificationSchedulerSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton<NotificationServiceContexts>();
        services.AddSingleton<NotificationScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationScheduler>());
        services.AddSingleton(configuration.GetSection("OfflineApplication").Get<OfflineApplicationPolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("CrewPositionPolicy").Get<CrewPositionPolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("CrewIdentityPolicy").Get<CrewIdentityPolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("CrewProfilePolicy").Get<CrewProfilePolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton<CrewProfileProtection>();
        services.AddSingleton(configuration.GetSection("MaintenanceObservations").Get<MaintenanceObservationSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("CrewAssignmentPolicy").Get<CrewAssignmentPolicy>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton(configuration.GetSection("SecureFiles").Get<SecureFileSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new());
        services.AddSingleton<LocalFileObjects>();
        services.AddSingleton<FileObjects>();
        services.AddScoped<IQualifiedInputRuntime, QualifiedInputRuntime>();
        services.AddSingleton<ClamdScanner>();
        services.AddSingleton<FileCapabilities>();
        services.AddFoundationIdentity(configuration, hostEnvironment);
        services.AddScoped<SecurityAudit>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, DirectoryAuthorizationResultHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped(sp =>
        {
            var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!;
            var actor = sp.GetRequiredService<ActorResolver>().Resolve(http.User) ?? throw new UnauthorizedAccessException();
            var connection = OperatorSecretReferences.Database(configuration, hostEnvironment);
            if (string.IsNullOrWhiteSpace(connection)) throw new AuditUnavailableException();
            return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options,
                actor.TenantId, new ActorContext(actor.Id.ToString(), "User", http.TraceIdentifier, actor.AuditCapabilities, "WebCommand", actor.RoleContext,
                    S1SecurityContract.Command(http)), qualifiedInputs: sp.GetRequiredService<IQualifiedInputRuntime>());
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
        app.UseMiddleware<ApiRequestTelemetry>();
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
            catch (BadHttpRequestException exception) when (exception.StatusCode == 400)
            {
                if (context.Response.HasStarted) throw;
                try {
                    await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context, "RequestValidation", "invalid_request");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { code = "invalid_request", correlationId = context.TraceIdentifier });
                } catch (AuditUnavailableException) {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsJsonAsync(new { code = "service_unavailable", correlationId = context.TraceIdentifier });
                }
            }
            catch (Exception exception) when (exception is AuditUnavailableException or Npgsql.NpgsqlException or System.Security.Cryptography.CryptographicException)
            {
                if (context.Response.HasStarted) throw;
                context.Response.StatusCode = 503;
                await context.Response.WriteAsJsonAsync(new { code = "service_unavailable", correlationId = context.TraceIdentifier });
            }
        });
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            var observationActor = context.RequestServices.GetRequiredService<ActorResolver>().Resolve(context.User);
            if (observationActor != null) context.Items[OperationalHealthObservations.TenantItem] = observationActor.TenantId;
            var operation = S1SecurityContract.Match(context);
            if (operation?.IsCritical == true)
            {
                var actor = context.RequestServices.GetRequiredService<ActorResolver>().Resolve(context.User);
                if (actor?.HasWriteAttribution != true)
                {
                    await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                        operation.DenialAction, actor == null ? "Unauthenticated" : "Denied");
                    var code = actor == null ? "authentication_required" : "permission_denied";
                    await Results.Problem(statusCode: actor == null ? 401 : 403, title: code,
                        extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        app.Use(async (context, next) => {
            await next(context);
            // Invalid JSON/binding can end before the command handler. Record fixed metadata only.
            var name = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName;
            if (context.Response.StatusCode == 400 && !context.Items.ContainsKey("TechnicalActionObserved") && name != null &&
                (name.StartsWith("Equipment", StringComparison.Ordinal) || name.StartsWith("Maintenance", StringComparison.Ordinal)))
                await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                    name.StartsWith("Equipment", StringComparison.Ordinal) ? "EquipmentSave" : "MaintenanceSave", "validation_failed");
        });
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
            endpoints.MapRegulatoryRegister();
            endpoints.MapLifecycleApplicability();
            endpoints.MapRegulatoryQualifications();
            endpoints.MapControlEvidence();
            endpoints.MapComplianceWorkspaces();
            endpoints.MapHumanReviews();
            endpoints.MapToday();
            endpoints.MapSystemHealth();
            endpoints.MapNotifications();
            endpoints.MapDailyOperations();
            endpoints.MapCertificates();
            endpoints.MapCertificateRenewals();
            endpoints.MapCertificateExtraction();
            endpoints.MapCertificateAssertions();
            endpoints.MapShipOperations();
            endpoints.MapDailyOperationOrigins();
            endpoints.MapDailyOffline();
            endpoints.MapDailyReviews();
            endpoints.MapAuditHistory();
            endpoints.MapSecureFiles();
            endpoints.MapDocuments();
            endpoints.MapCrew();
            endpoints.MapOfflineApplication();
            endpoints.MapCrewPersonalProfiles();
            endpoints.MapCrewSelection();
            endpoints.MapCredentials();
            endpoints.MapImports();
            endpoints.MapLegacyCrewImports();
            endpoints.MapTechnical();
            endpoints.MapMaintenanceObservations();
            endpoints.MapTug();
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
