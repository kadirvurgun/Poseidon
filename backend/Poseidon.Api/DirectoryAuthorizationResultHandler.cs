using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Poseidon.Api;

// Denied mutations must be durably recorded before any denial response is delivered.
public sealed class DirectoryAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        var todayAction = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName switch
        {
            "GetToday" => "TodayRead",
            "GetTodayWhy" => "TodayWhyRead",
            _ => null
        };
        if (!result.Succeeded && todayAction != null)
        {
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                todayAction, result.Challenged ? "Unauthenticated" : "Denied");
        }
        if (!result.Succeeded && (context.Request.Method is "POST" or "PUT") &&
            (context.Request.Path.StartsWithSegments("/api/v1/companies") || context.Request.Path.StartsWithSegments("/api/v1/vessels")))
        {
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context, "DirectoryWrite", result.Challenged ? "Unauthenticated" : "Denied");
            var code = result.Challenged ? "authentication_required" : "permission_denied";
            await Results.Problem(statusCode: result.Challenged ? 401 : 403, title: code,
                extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        await fallback.HandleAsync(next, context, policy, result);
    }
}
