using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record SecurityOperationVariant(string RouteValue, string OperationId);
public sealed record SecurityOperation(string OperationId, string Method, string RoutePattern, string Family,
    string Scope, string Kind, string? ReasonCode, string DenialAction, SecurityOperationVariant[] Variants, string[] TraceIds)
{
    public bool IsCritical => Kind == "CriticalWrite";
}

// This is an observation/coverage contract, never an authorization grant. The matched
// server RouteEndpoint supplies identity; caller paths, roles and reason text do not.
public static class S1SecurityContract
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    private static readonly FrozenSet<string> Reasons = new[] { "PREPARATION_COMMAND", "FILE_TRANSPORT", "OFFLINE_LIFECYCLE",
        "REGULATORY_WORKFLOW", "HUMAN_REVIEW_WORKFLOW", "SOURCE_QUALIFICATION" }.ToFrozenSet(StringComparer.Ordinal);
    public static IReadOnlyList<SecurityOperation> Operations { get; } = Load();
    private static readonly FrozenDictionary<(string Method, string Route), SecurityOperation> ByRoute =
        Operations.ToFrozenDictionary(x => (x.Method, Normalize(x.RoutePattern)));
    public static IReadOnlySet<string> CriticalOperationIds { get; } = Operations.Where(x => x.IsCritical)
        .SelectMany(x => x.Variants.Length == 0 ? new[] { x.OperationId } : x.Variants.Select(v => v.OperationId))
        .ToFrozenSet(StringComparer.Ordinal);

    private static string Normalize(string route) => route.TrimEnd('/');
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 80 } && char.IsAsciiLetter(value[0]) &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    private static IReadOnlyList<SecurityOperation> Load()
    {
        using var stream = typeof(S1SecurityContract).Assembly.GetManifestResourceStream("Poseidon.S1SecurityScope")
            ?? throw new InvalidOperationException("The current security scope contract is unavailable.");
        using var document = JsonDocument.Parse(stream, new() { MaxDepth = 16 });
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "poseidon.s1-current-security-scope.v1" ||
            root.GetProperty("baseline").GetString() != "poseidon-v46.3-20261030")
            throw new InvalidOperationException("The current security scope contract is invalid.");
        var values = root.GetProperty("operations").Deserialize<SecurityOperation[]>(Json)
            ?? throw new InvalidOperationException("The current security scope contract is invalid.");
        if (values.Length is 0 or > 512 || values.Any(x => !Identifier(x.OperationId) || !Identifier(x.Family) ||
            x.Method is not ("POST" or "PUT" or "PATCH" or "DELETE") ||
            !x.RoutePattern.StartsWith("/api/v1/", StringComparison.Ordinal) || x.RoutePattern.Length > 320 ||
            x.RoutePattern.Any(char.IsControl) || x.RoutePattern.Contains('?') || x.RoutePattern.Contains("..", StringComparison.Ordinal) ||
            x.Kind is not ("CriticalWrite" or "SecurityObservation" or "AlwaysDenied") ||
            x.Scope is not ("Vessel" or "Tenant" or "TenantPerson" or "CommandVessel" or "ResolvedVessel" or "Session") ||
            (x.IsCritical ? x.ReasonCode == null || !Reasons.Contains(x.ReasonCode) : x.ReasonCode != null) ||
            !SecurityEventCodes.Actions.Contains(x.DenialAction) || x.TraceIds.Length != 4 ||
            !x.TraceIds.Order(StringComparer.Ordinal).SequenceEqual(new[] { "T-S1-002", "T-S1-003", "T-S1-004", "T-S1-007" }) ||
            x.Variants.Length > 8 || x.Variants.Select(v => v.RouteValue).Distinct(StringComparer.Ordinal).Count() != x.Variants.Length ||
            x.Variants.Any(v => !Identifier(v.OperationId) ||
                v.RouteValue is not ("renew" or "key" or "revoke" or "lost" or "cleanup" or "replay")) ||
            (x.Variants.Length > 0 && (!x.IsCritical || !x.RoutePattern.EndsWith("/{action}", StringComparison.Ordinal)))))
            throw new InvalidOperationException("The current security scope contract is invalid.");
        var concrete = values.SelectMany(x => x.Variants.Length == 0 ? new[] { x.OperationId } : x.Variants.Select(v => v.OperationId)).ToArray();
        if (values.Select(x => (x.Method, Normalize(x.RoutePattern))).Distinct().Count() != values.Length ||
            values.Select(x => x.OperationId).Distinct(StringComparer.Ordinal).Count() != values.Length ||
            concrete.Distinct(StringComparer.Ordinal).Count() != concrete.Length ||
            values.Length != root.GetProperty("mutationRouteTemplates").GetInt32() ||
            values.Count(x => x.IsCritical) != root.GetProperty("criticalRouteTemplates").GetInt32() ||
            values.Where(x => x.IsCritical).Sum(x => Math.Max(1, x.Variants.Length)) != root.GetProperty("criticalOperations").GetInt32())
            throw new InvalidOperationException("The current security scope contract is invalid.");
        return Array.AsReadOnly(values);
    }

    public static SecurityOperation? Match(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint || endpoint.RoutePattern.RawText is not { } route)
            return null;
        var method = context.Request.Method.ToUpperInvariant();
        if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) != true)
            return null;
        return ByRoute.GetValueOrDefault((method, Normalize(route)));
    }

    public static AuditCommandContext? Command(HttpContext context)
    {
        var operation = Match(context);
        if (operation?.IsCritical != true) return null;
        var id = operation.OperationId;
        if (operation.Variants.Length > 0)
        {
            // Only exact supported actions become observations. An unknown action
            // cannot get an audited write context merely by matching the route.
            var value = context.Request.RouteValues["action"] as string;
            var variant = operation.Variants.SingleOrDefault(v => v.RouteValue == value);
            if (variant == null) return null;
            id = variant.OperationId;
        }
        return new(id, operation.ReasonCode!);
    }

    // Called by the real host phase test. Definitions alone never claim runtime
    // execution or acceptance; actual audit observations supply the second gate.
    public static IReadOnlyList<SecurityOperation> ValidateEndpointInventory(IEnumerable<Endpoint> endpoints)
    {
        var observed = new List<SecurityOperation>();
        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var route = endpoint.RoutePattern.RawText;
            if (route == null || !route.StartsWith("/api/v1/", StringComparison.Ordinal)) continue;
            foreach (var method in endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
            {
                if (method is not ("POST" or "PUT" or "PATCH" or "DELETE")) continue;
                if (!ByRoute.TryGetValue((method, Normalize(route)), out var operation) ||
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
                    throw new InvalidOperationException("An HTTP command has no classified authorization/audit contract.");
                observed.Add(operation);
            }
        }
        if (observed.Count != Operations.Count || observed.Select(x => x.OperationId).Distinct().Count() != observed.Count ||
            !observed.Select(x => x.OperationId).ToHashSet(StringComparer.Ordinal).SetEquals(Operations.Select(x => x.OperationId)))
            throw new InvalidOperationException("The HTTP command scope differs from its audit contract.");
        return observed.AsReadOnly();
    }
}
