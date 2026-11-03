using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Poseidon.Api;

// Denied mutations must be durably recorded before any denial response is delivered.
public sealed class DirectoryAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        // Cover every classified mutation route, including security-only commands
        // and aliases. A durable denied observation must precede its response.
        var operation = S1SecurityContract.Match(context);
        if (!result.Succeeded && operation != null)
        {
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                operation.DenialAction, result.Challenged ? "Unauthenticated" : "Denied");
            var code = result.Challenged ? "authentication_required" : "permission_denied";
            await Results.Problem(statusCode: result.Challenged ? 401 : 403, title: code,
                extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        var endpointName = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName;
        if (!result.Succeeded && endpointName != null && (endpointName.Contains("SourceDraft", StringComparison.Ordinal) || endpointName.Contains("RequirementDraft", StringComparison.Ordinal)))
        {
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                context.Request.Method is "POST" or "PUT" ? "RegisterSave" : "RegisterRead", result.Challenged ? "Unauthenticated" : "Denied");
            var code = result.Challenged ? "authentication_required" : "permission_denied";
            await Results.Problem(statusCode: result.Challenged ? 401 : 403, title: code,
                extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        if (!result.Succeeded && endpointName != null && (endpointName.StartsWith("Equipment", StringComparison.Ordinal) || endpointName.StartsWith("Maintenance", StringComparison.Ordinal))) {
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                endpointName.StartsWith("Equipment", StringComparison.Ordinal) ? context.Request.Method == "GET" ? "EquipmentRead" : "EquipmentSave" : context.Request.Method == "GET" ? "MaintenanceRead" : "MaintenanceSave",
                result.Challenged ? "Unauthenticated" : "Denied");
        }
        if (!result.Succeeded && endpointName?.StartsWith("Tug", StringComparison.Ordinal) == true)
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context,
                context.Request.Method == "GET" ? "TugRead" : "TugSave", result.Challenged ? "Unauthenticated" : "Denied");
        var todayAction = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName switch
        {
            "GetRegulatoryDraft" => "ProfileRead",
            "GetRegulatoryHistory" => "ProfileHistory",
            "ListCompanies" or "GetCompany" => "CompaniesRead",
            "ListVessels" or "GetVessel" => "VesselsRead",
            "GetWorkspace" => "WorkspaceRead",
            "SearchWorkspace" => "WorkspaceSearch",
            "GetAuditHistory" => "AuditQuery",
            "GetAuditRecord" => "AuditDetail",
            "GetAuditRevision" => "AuditRevision",
            "ListVesselFiles" => "FileList",
            "GetVesselFile" => "FileDetail",
            "DownloadVesselFile" => "FileDownload",
            "CrewList" => "CrewRead",
            "CrewProfile" => "CrewProfile",
            "CrewHistory" => "CrewHistory",
            "CrewPositions" => "CrewPositions",
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
            await context.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(context, endpointName is "CrewSave" or "CrewPersonSave" or "CrewPositionSave" ? "CrewSave" : endpointName == "SaveRegulatoryDraft" ? "ProfileSave" : "DirectoryWrite", result.Challenged ? "Unauthenticated" : "Denied");
            var code = result.Challenged ? "authentication_required" : "permission_denied";
            await Results.Problem(statusCode: result.Challenged ? 401 : 403, title: code,
                extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
            return;
        }
        await fallback.HandleAsync(next, context, policy, result);
    }
}
