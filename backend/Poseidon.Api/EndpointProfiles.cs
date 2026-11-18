using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Poseidon.Api;

// PLAN-1511-5803 / 58.03 §1-§6, §11: exact endpoint transport profile registry.
// The registry describes the transport contract each mapped endpoint is bound to.
// It is never an authorization grant (58.03 paragraph:19, "profil kaydı yeni izin
// vermez"): authorization stays in the endpoint's own policy and handler checks.
// Current endpoints keep their native wire behavior; native profiles record that
// behavior and the explicit gap to the 58.03 target profile (paragraph:8, :257).

public sealed record EndpointIdempotencyContract(
    string Mode, bool Required, string Header, string KeyFormat, string DedupScope, bool Durable, string ResponseReplay,
    int ConflictStatus, string ConflictCode, string LostReplyRetry, string AfterResponseWindowRetry);

public sealed record EndpointConcurrencyContract(
    string Mode, bool CreateExempt, string MissingPrecondition, int StaleStatus, string StaleCode);

public sealed record EndpointErrorEnvelope(string MediaType, string[] RequiredFields, string CodeCase, string CorrelationHeader);

public sealed record EndpointProfile(
    string ProfileId, string Conformance, string TargetProfile, string Kind, string Authentication, string Csrf,
    string CachePolicy, EndpointIdempotencyContract? Idempotency, EndpointConcurrencyContract? Concurrency,
    EndpointErrorEnvelope ErrorEnvelope, string[] Gaps, string Summary);

public sealed record EndpointProfileEntry(
    string Method, string NormalizedPath, string? OperationId, string ProfileId, bool Anonymous,
    string[] AuthorizationPolicies, string[] RequestContentTypes, string? RequestSchema, string ConcurrencyEvidence,
    string? SecurityOperationId, string Owner, string[] Deviations);

public sealed record EndpointProfileExclusion(string NormalizedPath, string Reason);

public sealed record EndpointProfileRegistryDocument(
    string SchemaVersion, JsonElement Source, string DefinitionState, string Meaning,
    EndpointProfile[] Profiles, EndpointProfileExclusion[] Exclusions, EndpointProfileEntry[] Endpoints);

public static class EndpointProfiles
{
    public const string SchemaVersion = "poseidon.endpoint-profiles.v1";
    public const string OpenApiExtension = "x-poseidon-profile";
    private static readonly string[] CommandMethods = ["POST", "PUT", "PATCH", "DELETE"];
    // 58.03 paragraph:161 finite ProfileId set. Native ids are allowed only as the
    // real current profile of an existing pre-58.03 endpoint, never for new COMMON-1 uçlar.
    public static readonly FrozenSet<string> CanonicalProfileIds = new[] { "CORE-1", "ACCOUNT-2", "CFG3", "HR3", "IAM-3",
        "FLOW-1", "LEARN-1", "REPORT-1", "REPORT-2", "CONTROLLED_DOC-1", "COMMON-1" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> Kinds = new[] { "READ", "COMMAND", "SESSION_READ", "SESSION_COMMAND", "PUBLIC_READ" }
        .ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> Deviations = new[] { "CSRF_NOT_ENFORCED_BEFORE_EFFECT", "IDEMPOTENCY_KEY_NOT_REQUIRED",
        "IDEMPOTENCY_KEY_DERIVED_FROM_COMMAND_ID", "NON_JSON_ERROR_ENVELOPE",
        "BINDING_REJECTION_BEFORE_HANDLER_WITHOUT_ENVELOPE" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };

    private static readonly Lazy<EndpointProfileRegistryDocument> Registry = new(Load);
    public static EndpointProfileRegistryDocument Document => Registry.Value;
    private static readonly Lazy<FrozenDictionary<(string, string), EndpointProfileEntry>> ByRoute = new(() =>
        Document.Endpoints.ToFrozenDictionary(x => (x.Method, x.NormalizedPath)));
    public static string DocumentedPath(string normalizedPath) =>
        System.Text.RegularExpressions.Regex.Replace(normalizedPath, @"\{([^}:?=]+)[^}]*\}", "{$1}");
    // Ambiguous constraint-free templates are left without an extension; the OpenAPI test then fails.
    private static readonly Lazy<FrozenDictionary<(string, string), EndpointProfileEntry>> ByDocumentedPath = new(() =>
        Document.Endpoints.GroupBy(x => (x.Method, DocumentedPath(x.NormalizedPath))).Where(g => g.Count() == 1)
            .ToFrozenDictionary(g => g.Key, g => g.Single()));
    private static readonly Lazy<FrozenDictionary<string, EndpointProfile>> ById = new(() =>
        Document.Profiles.ToFrozenDictionary(x => x.ProfileId, StringComparer.Ordinal));

    public static string Normalize(string route) => route.Length > 1 ? route.TrimEnd('/') : route;
    public static bool IsCommandMethod(string method) => CommandMethods.Contains(method, StringComparer.Ordinal);

    public static EndpointProfile Profile(string profileId) => ById.Value[profileId];

    public static EndpointProfileEntry? Find(string method, string route) =>
        ByRoute.Value.GetValueOrDefault((method.ToUpperInvariant(), Normalize(route)));

    public static EndpointProfileEntry? Match(HttpContext context)
    {
        if (context.GetEndpoint() is not RouteEndpoint endpoint || endpoint.RoutePattern.RawText is not { } route) return null;
        var method = context.Request.Method.ToUpperInvariant();
        if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) != true) return null;
        return Find(method, route);
    }

    // Observed host facts. Curated fields (profileId, deviations) come only from the committed contract.
    public static IReadOnlyList<EndpointProfileEntry> Observe(IEnumerable<Endpoint> endpoints)
    {
        var observed = new List<EndpointProfileEntry>();
        foreach (var endpoint in endpoints.OfType<RouteEndpoint>())
        {
            var route = endpoint.RoutePattern.RawText;
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            if (route == null || methods == null || methods.Count == 0) continue;
            var path = Normalize(route.StartsWith('/') ? route : "/" + route);
            if (Document.Exclusions.Any(x => x.NormalizedPath == path)) continue;
            var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var anonymous = authorize.Count == 0 || endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null;
            var policies = authorize.Select(x => x.Policy ?? "(default)").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var accepts = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
            var media = accepts?.ContentTypes.Select(x => x.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
            var schema = accepts?.RequestType;
            var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            foreach (var method in methods.Select(x => x.ToUpperInvariant()).Distinct(StringComparer.Ordinal))
            {
                var security = IsCommandMethod(method) ? S1SecurityContract.Operations.SingleOrDefault(x => x.Method == method &&
                    S1Normalize(x.RoutePattern) == path)?.OperationId : null;
                observed.Add(new(method, path, name, "", anonymous, policies, media, schema == null ? null : SchemaName(schema),
                    ConcurrencyEvidence(schema), security, Owner(endpoint), []));
            }
        }
        return observed.OrderBy(x => x.NormalizedPath, StringComparer.Ordinal).ThenBy(x => x.Method, StringComparer.Ordinal).ToArray();
    }

    private static string S1Normalize(string route) => route.TrimEnd('/');

    // 58.03 paragraph:57 owner: the top-level type that declares the handler (closures unwrapped).
    private static string Owner(RouteEndpoint endpoint)
    {
        var type = endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType;
        while (type is { IsNested: true, DeclaringType: not null }) type = type.DeclaringType;
        return type?.Name ?? "(unknown)";
    }

    // Native idempotency mechanisms. Only the header modes require a client Idempotency-Key.
    public static readonly FrozenSet<string> IdempotencyModes = new[] { "HEADER_KEY_RECEIPT", "HEADER_EQUALS_BODY_COMMAND_ID",
        "BODY_COMMAND_ID_OUTCOME", "CLIENT_RESOURCE_ID", "STATE_IDEMPOTENT", "NO_BUSINESS_EFFECT_OBSERVATION", "NOT_APPLICABLE_ALWAYS_DENIED" }
        .ToFrozenSet(StringComparer.Ordinal);
    public static bool HeaderKeyRequired(EndpointProfile profile) =>
        profile.Idempotency is { Mode: "HEADER_KEY_RECEIPT" or "HEADER_EQUALS_BODY_COMMAND_ID" };

    private static string SchemaName(Type type) => type.IsArray ? SchemaName(type.GetElementType()!) + "[]" :
        type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] + "<" + string.Join(",", type.GetGenericArguments().Select(SchemaName)) + ">" : type.Name;

    // Transport-visible concurrency precondition carried by the request body. Handler-internal
    // xmin/revision CAS without a body precondition is reported as NONE_DECLARED (58.03 §4 gap).
    private static string ConcurrencyEvidence(Type? schema)
    {
        if (schema == null) return "NONE_DECLARED";
        var names = schema.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        if (names.Contains("ExpectedVersion", StringComparer.Ordinal)) return "BODY_EXPECTED_VERSION";
        if (names.Any(n => n.StartsWith("Expected", StringComparison.Ordinal) && n.EndsWith("Version", StringComparison.Ordinal)))
            return "BODY_EXPECTED_VERSION";
        return "NONE_DECLARED";
    }

    // Deployment gate (58.03 paragraph:59): every mapped endpoint has exactly one entry,
    // every entry is mapped, observed facts equal the committed contract and profile
    // semantics are consistent with the endpoint kind.
    public static IReadOnlyList<string> Validate(IEnumerable<Endpoint> endpoints)
    {
        var errors = new List<string>();
        var observed = Observe(endpoints);
        foreach (var group in observed.GroupBy(x => (x.Method, x.NormalizedPath)).Where(g => g.Count() > 1))
            errors.Add($"{group.Key.Method} {group.Key.NormalizedPath}: mapped more than once.");
        var observedKeys = observed.Select(x => (x.Method, x.NormalizedPath)).ToHashSet();
        foreach (var entry in Document.Endpoints.Where(x => !observedKeys.Contains((x.Method, x.NormalizedPath))))
            errors.Add($"{entry.Method} {entry.NormalizedPath}: registry entry has no mapped endpoint.");
        foreach (var actual in observed)
        {
            var entry = Find(actual.Method, actual.NormalizedPath);
            if (entry == null) { errors.Add($"{actual.Method} {actual.NormalizedPath}: mapped endpoint has no profile."); continue; }
            if (entry.OperationId != actual.OperationId || entry.Anonymous != actual.Anonymous ||
                !entry.AuthorizationPolicies.SequenceEqual(actual.AuthorizationPolicies, StringComparer.Ordinal) ||
                !entry.RequestContentTypes.SequenceEqual(actual.RequestContentTypes, StringComparer.Ordinal) ||
                entry.RequestSchema != actual.RequestSchema || entry.ConcurrencyEvidence != actual.ConcurrencyEvidence ||
                entry.SecurityOperationId != actual.SecurityOperationId || entry.Owner != actual.Owner)
                errors.Add($"{actual.Method} {actual.NormalizedPath}: observed host metadata differs from the registry entry.");
            if (!ById.Value.TryGetValue(entry.ProfileId, out var profile)) { errors.Add($"{actual.Method} {actual.NormalizedPath}: unknown profile."); continue; }
            var command = IsCommandMethod(actual.Method);
            if (command != (profile.Kind is "COMMAND" or "SESSION_COMMAND"))
                errors.Add($"{actual.Method} {actual.NormalizedPath}: method kind differs from profile {profile.ProfileId}.");
            if ((profile.Authentication == "ANONYMOUS") != actual.Anonymous)
                errors.Add($"{actual.Method} {actual.NormalizedPath}: authentication differs from profile {profile.ProfileId}.");
            if (command && actual.SecurityOperationId == null)
                errors.Add($"{actual.Method} {actual.NormalizedPath}: command has no S1 audit/security classification.");
            if (profile.Kind == "COMMAND" && (profile.Csrf != "REQUIRED" || profile.Idempotency == null || profile.CachePolicy != "no-store"))
                errors.Add($"{actual.Method} {actual.NormalizedPath}: command profile lacks CSRF, an idempotency contract or no-store.");
        }
        return errors;
    }

    private static EndpointProfileRegistryDocument Load()
    {
        using var stream = typeof(EndpointProfiles).Assembly.GetManifestResourceStream("Poseidon.EndpointProfiles")
            ?? throw new InvalidOperationException("The endpoint profile registry is unavailable.");
        var document = JsonSerializer.Deserialize<EndpointProfileRegistryDocument>(stream, Json)
            ?? throw new InvalidOperationException("The endpoint profile registry is invalid.");
        static bool Id(string? value) => value is { Length: > 0 and <= 40 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        var profileIds = document.Profiles.Select(x => x.ProfileId).ToArray();
        if (document.SchemaVersion != SchemaVersion || document.Profiles.Length is 0 or > 32 || document.Endpoints.Length is 0 or > 2048 ||
            profileIds.Distinct(StringComparer.Ordinal).Count() != profileIds.Length ||
            document.Profiles.Any(p => !Id(p.ProfileId) || !Kinds.Contains(p.Kind) || p.CachePolicy != "no-store" ||
                p.Authentication is not ("REQUIRED" or "ANONYMOUS") || p.Csrf is not ("REQUIRED" or "NOT_APPLICABLE") ||
                p.Conformance is not ("NATIVE_PRE_58_03" or "58_03") || !CanonicalProfileIds.Contains(p.TargetProfile) ||
                // A native profile cannot claim a canonical 58.03 id; a 58.03 profile must be one.
                (p.Conformance == "58_03") != CanonicalProfileIds.Contains(p.ProfileId) ||
                (p.Kind == "COMMAND" && (p.Idempotency == null || p.Csrf != "REQUIRED" || !IdempotencyModes.Contains(p.Idempotency.Mode) ||
                    HeaderKeyRequired(p) != p.Idempotency.Required || p.Idempotency.Header != "Idempotency-Key")) ||
                p.Kind is not ("COMMAND" or "SESSION_COMMAND") && (p.Idempotency != null || p.Concurrency != null || p.Csrf != "NOT_APPLICABLE") ||
                p.ErrorEnvelope.RequiredFields.Length == 0 || p.ErrorEnvelope.CorrelationHeader != "X-Correlation-ID") ||
            document.Endpoints.Any(e => !profileIds.Contains(e.ProfileId, StringComparer.Ordinal) ||
                !(e.NormalizedPath == "/health/live" || e.NormalizedPath.StartsWith("/api/v1/", StringComparison.Ordinal)) ||
                e.NormalizedPath.Length > 500 || e.NormalizedPath.Contains('?') || e.NormalizedPath.Contains('*') ||
                e.Method is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE") || e.Deviations.Any(d => !Deviations.Contains(d)) ||
                e.Deviations.Distinct(StringComparer.Ordinal).Count() != e.Deviations.Length) ||
            document.Endpoints.Select(x => (x.Method, x.NormalizedPath)).Distinct().Count() != document.Endpoints.Length)
            throw new InvalidOperationException("The endpoint profile registry is invalid.");
        return document;
    }

    // OpenAPI 3.1 operation extension (58.03 paragraph:278): every documented operation
    // carries the profileId of its single registry entry.
    public static void AddOpenApiProfiles(Microsoft.AspNetCore.OpenApi.OpenApiOptions options) =>
        options.AddOperationTransformer((operation, context, _) =>
        {
            var description = context.Description;
            // ApiDescription.RelativePath drops route constraints; match the constraint-free template.
            if (description.HttpMethod is { } method && description.RelativePath is { } relative &&
                ByDocumentedPath.Value.TryGetValue((method.ToUpperInvariant(), DocumentedPath(Normalize("/" + relative.TrimStart('/')))), out var entry))
            {
                operation.Extensions ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiExtension>();
                operation.Extensions[OpenApiExtension] = new Microsoft.OpenApi.JsonNodeExtension(
                    System.Text.Json.Nodes.JsonValue.Create(entry.ProfileId));
            }
            return Task.CompletedTask;
        });
}
