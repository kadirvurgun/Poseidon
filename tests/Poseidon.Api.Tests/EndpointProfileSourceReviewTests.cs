using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5803 QA follow-up: the deviation list is derived from a static source review of every
// endpoint, not only from the runtime probe (which cannot reach every error branch).
//
// Method (reliable, no handwritten handler map):
//  1. Scan backend/Poseidon.Api/**/*.cs for empty-body error results (Results/TypedResults NotFound(),
//     BadRequest(), Unauthorized(), Conflict(), UnprocessableEntity(), StatusCode(..), Forbid(..)). Challenge(..) is
//     excluded: the only use is the OIDC login redirect (302), and a 401 challenge is the authentication scheme's response.
//  2. Attribute each source line to the innermost compiled method using the portable PDB sequence
//     points (async/iterator MoveNext is folded into its kick-off method, lambdas are their own methods).
//  3. Build the IL call graph of Poseidon.Api (call/callvirt/newobj/ldftn/ldvirtftn) and walk it forward
//     from every mapped endpoint's handler MethodInfo (the actual host EndpointDataSource).
//  An endpoint must carry NON_JSON_ERROR_ENVELOPE exactly when a reachable method contains such a result.
//  Framework parameter binding (typed query/route values, required strings/headers, JSON/form bodies)
//  can reject before the handler with an empty 400 outside Development; such endpoints must carry
//  BINDING_REJECTION_BEFORE_HANDLER_WITHOUT_ENVELOPE exactly.
// Limits: calls through interfaces/virtual dispatch into other assemblies are not followed; an occurrence
// that no handler reaches fails the test instead of being silently ignored.
[Trait("Requirement", "PLAN-1511-5803")]
public sealed class EndpointProfileSourceReviewTests
{
    private const string NonJson = "NON_JSON_ERROR_ENVELOPE";
    private const string Binding = "BINDING_REJECTION_BEFORE_HANDLER_WITHOUT_ENVELOPE";

    internal static readonly Regex EmptyBodyError = new(
        @"\b(?:Typed)?Results\s*\.\s*(?:(?:NotFound|BadRequest|Unauthorized|Conflict|UnprocessableEntity|InternalServerError)\s*\(\s*\)|(?:StatusCode|Forbid)\s*\()",
        RegexOptions.Compiled);

    [Fact]
    public void Empty_body_detector_matches_the_known_shapes_only()
    {
        Assert.Matches(EmptyBodyError, "return Results.NotFound();");
        Assert.Matches(EmptyBodyError, "return TypedResults.BadRequest( );");
        Assert.Matches(EmptyBodyError, "return Results.StatusCode(403);");
        Assert.Matches(EmptyBodyError, "if (x) return Results.Forbid();");
        Assert.DoesNotMatch(EmptyBodyError, "return Results.NotFound(new { code = \"x\" });");
        Assert.DoesNotMatch(EmptyBodyError, "return Results.BadRequest(new { code = \"invalid\" });");
        Assert.DoesNotMatch(EmptyBodyError, "return Results.Problem(statusCode: 409, title: code);");
        Assert.DoesNotMatch(EmptyBodyError, "return Results.Challenge(properties, [scheme]);");
    }

    [Fact]
    public async Task Every_source_visible_profile_violation_is_a_recorded_deviation()
    {
        await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Test"));
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        var isService = host.Services.GetRequiredService<IServiceProviderIsService>();
        var analysis = new HandlerGraph(typeof(EndpointProfiles).Assembly);
        var occurrences = SourceOccurrences();
        Assert.NotEmpty(occurrences);

        var attributed = occurrences.Select(o => (o.Location, Method: analysis.MethodAt(o.File, o.Line))).ToArray();
        var unattributed = attributed.Where(x => x.Method == null).Select(x => x.Location).ToArray();
        Assert.True(unattributed.Length == 0, "No compiled method for: " + string.Join(", ", unattributed));

        var reachedOccurrences = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();
        var nullability = new NullabilityInfoContext();
        foreach (var endpoint in endpoints)
        {
            var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            if (handler == null || methods == null || endpoint.RoutePattern.RawText is not { } raw) continue;
            var path = EndpointProfiles.Normalize(raw.StartsWith('/') ? raw : "/" + raw);
            if (EndpointProfiles.Document.Exclusions.Any(x => x.NormalizedPath == path)) continue;
            var reachable = analysis.Reachable(handler);
            var hits = attributed.Where(x => reachable.Contains(x.Method!.Value)).Select(x => x.Location).ToArray();
            foreach (var hit in hits) reachedOccurrences.Add(hit);
            var bindingRisk = handler.GetParameters().Where(p => CanRejectInBinding(p, endpoint, isService, nullability))
                .Select(p => p.Name).ToArray();
            foreach (var method in methods)
            {
                var entry = EndpointProfiles.Find(method, path);
                if (entry == null) { failures.Add($"{method} {path}: no registry entry."); continue; }
                if (hits.Length > 0 != entry.Deviations.Contains(NonJson))
                    failures.Add($"{method} {path}: {NonJson} expected={hits.Length > 0} [{string.Join(", ", hits)}]");
                if (bindingRisk.Length > 0 != entry.Deviations.Contains(Binding))
                    failures.Add($"{method} {path}: {Binding} expected={bindingRisk.Length > 0} [{string.Join(", ", bindingRisk)}]");
            }
        }
        var orphan = occurrences.Select(o => o.Location).Where(l => !reachedOccurrences.Contains(l)).ToArray();
        Assert.True(orphan.Length == 0, "Empty-body results no endpoint handler reaches (review manually): " + string.Join(", ", orphan));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private sealed record Occurrence(string File, int Line, string Location);

    private static Occurrence[] SourceOccurrences()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var api = Path.Combine(root.FullName, "backend", "Poseidon.Api");
        var sep = Path.DirectorySeparatorChar;
        var result = new List<Occurrence>();
        foreach (var file in Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{sep}obj{sep}") && !p.Contains($"{sep}bin{sep}")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = lines[i];
                var comment = code.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0 && !code[..comment].Contains('"')) code = code[..comment];
                if (EmptyBodyError.IsMatch(code))
                    result.Add(new(Path.GetFileName(file), i + 1, $"{Path.GetFileName(file)}:{i + 1}"));
            }
        }
        return result.ToArray();
    }

    // Mirrors minimal API parameter source inference (RequestDelegateFactory). Outside Development a
    // failed bind ends with an empty 400 before the handler runs (ThrowOnBadRequest is false).
    private static bool CanRejectInBinding(ParameterInfo parameter, RouteEndpoint endpoint, IServiceProviderIsService services,
        NullabilityInfoContext nullability)
    {
        var type = parameter.ParameterType;
        var attributes = parameter.GetCustomAttributes(true);
        if (attributes.Any(a => a is IFromServiceMetadata or FromKeyedServicesAttribute)) return false;
        if (type == typeof(HttpContext) || type == typeof(HttpRequest) || type == typeof(HttpResponse) || type == typeof(CancellationToken) ||
            type == typeof(ClaimsPrincipal) || type == typeof(Stream) || type == typeof(PipeReader)) return false;
        if (attributes.Any(a => a is IFromBodyMetadata or IFromFormMetadata) || type == typeof(IFormFile) ||
            type == typeof(IFormFileCollection) || type == typeof(IFormCollection)) return true;
        if (attributes.Any(a => a is AsParametersAttribute)) return true;
        var optional = parameter.HasDefaultValue || Nullable.GetUnderlyingType(type) != null ||
            (!type.IsValueType && nullability.Create(parameter).WriteState != NullabilityState.NotNull);
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var name = attributes.OfType<IFromRouteMetadata>().FirstOrDefault()?.Name ?? parameter.Name!;
        var explicitSource = attributes.Any(a => a is IFromHeaderMetadata or IFromQueryMetadata);
        var route = !explicitSource ? endpoint.RoutePattern.GetParameter(name) : null;
        if (route != null) return underlying != typeof(string) && route.ParameterPolicies.Count == 0;
        if (explicitSource || Simple(underlying))
            return underlying == typeof(string) || underlying == typeof(string[]) || underlying == typeof(StringValues) ? !optional : true;
        if (services.IsService(type)) return false;
        return true; // inferred JSON body
    }

    private static bool Simple(Type type) => type == typeof(string) || type == typeof(string[]) || type == typeof(StringValues) ||
        type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) ||
        type == typeof(Uri) || (type.IsArray && Simple(type.GetElementType()!)) ||
        type.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name is "TryParse" or "BindAsync");

    // PDB line ranges + IL call graph of one assembly. Nodes are method metadata tokens; state-machine
    // MoveNext methods are folded into their kick-off method.
    private sealed class HandlerGraph
    {
        private static readonly OpCode[] OneByte = new OpCode[256], TwoByte = new OpCode[256];
        private readonly Module module;
        private readonly List<(int Token, string File, int Start, int End)> ranges = [];
        private readonly Dictionary<int, int> kickoff = [];
        private readonly Dictionary<int, HashSet<int>> edges = [];

        static HandlerGraph()
        {
            foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var op = (OpCode)field.GetValue(null)!;
                var value = (ushort)op.Value;
                if (op.Size == 1) OneByte[value] = op; else TwoByte[value & 0xff] = op;
            }
        }

        public HandlerGraph(Assembly assembly)
        {
            module = assembly.ManifestModule;
            using (var stream = File.OpenRead(Path.ChangeExtension(assembly.Location, ".pdb")))
            using (var provider = MetadataReaderProvider.FromPortablePdbStream(stream))
            {
                var pdb = provider.GetMetadataReader();
                foreach (var handle in pdb.MethodDebugInformation)
                {
                    var info = pdb.GetMethodDebugInformation(handle);
                    if (info.SequencePointsBlob.IsNil) continue;
                    string? file = null; int start = int.MaxValue, end = 0;
                    foreach (var point in info.GetSequencePoints())
                    {
                        if (point.IsHidden) continue;
                        file ??= Path.GetFileName(pdb.GetString(pdb.GetDocument(point.Document).Name));
                        start = Math.Min(start, point.StartLine); end = Math.Max(end, point.EndLine);
                    }
                    if (file != null) ranges.Add((MetadataTokens.GetToken(handle.ToDefinitionHandle()), file, start, end));
                }
            }
            var all = assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                .Concat(t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                    BindingFlags.Static | BindingFlags.DeclaredOnly))).ToArray();
            foreach (var method in all)
                if (method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } moveNext)
                    kickoff[moveNext.MetadataToken] = method.MetadataToken;
            foreach (var method in all) Scan(method);
        }

        private int Node(int token) => kickoff.TryGetValue(token, out var owner) ? owner : token;

        public int? MethodAt(string file, int line)
        {
            var hit = ranges.Where(r => r.File == file && r.Start <= line && line <= r.End)
                .OrderBy(r => r.End - r.Start).Select(r => (int?)r.Token).FirstOrDefault();
            return hit is { } token ? Node(token) : null;
        }

        public HashSet<int> Reachable(MethodInfo handler)
        {
            var seen = new HashSet<int>();
            if (handler.Module != module) return seen;
            var queue = new Queue<int>([Node(handler.MetadataToken)]);
            while (queue.TryDequeue(out var node))
                if (seen.Add(node) && edges.TryGetValue(node, out var next))
                    foreach (var target in next) queue.Enqueue(target);
            return seen;
        }

        private void Scan(MethodBase method)
        {
            byte[]? il;
            try { il = method.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { return; }
            if (il == null) return;
            var from = Node(method.MetadataToken);
            var targets = edges.TryGetValue(from, out var existing) ? existing : edges[from] = [];
            var typeArgs = method.DeclaringType is { IsGenericType: true } d ? d.GetGenericArguments() : null;
            var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (var i = 0; i < il.Length;)
            {
                OpCode op;
                if (il[i] == 0xFE) { op = TwoByte[il[i + 1]]; i += 2; } else { op = OneByte[il[i]]; i += 1; }
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar: i += 1; break;
                    case OperandType.InlineVar: i += 2; break;
                    case OperandType.InlineI8 or OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                    case OperandType.InlineMethod:
                        var token = BitConverter.ToInt32(il, i); i += 4;
                        try
                        {
                            var target = module.ResolveMethod(token, typeArgs, methodArgs);
                            if (target?.Module == module) targets.Add(Node(target.MetadataToken));
                        }
                        catch (Exception) { /* external or unresolvable generic context: not part of this assembly */ }
                        break;
                    default: i += 4; break;
                }
            }
        }
    }
}

// PLAN-1511-5803 QA follow-up: registry loader guards and fail-closed host start.
[Trait("Requirement", "PLAN-1511-5803")]
public sealed class EndpointProfileRegistryGuardTests
{
    private static string Committed()
    {
        using var stream = typeof(EndpointProfiles).Assembly.GetManifestResourceStream("Poseidon.EndpointProfiles")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static EndpointProfileRegistryDocument Parse(string json) =>
        EndpointProfiles.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Committed_registry_parses()
    {
        var document = Parse(Committed());
        Assert.All(document.Exclusions, x => Assert.True(EndpointProfiles.AllowedExclusion(x.NormalizedPath), x.NormalizedPath));
    }

    [Theory]
    [InlineData("/api/v1/companies")]
    [InlineData("/api/v1/{*path}")]
    [InlineData("/API/v1/x")]
    [InlineData("/health/live")]
    [InlineData("/")]
    [InlineData("/app/../api/v1/x")]
    public void Exclusions_outside_known_non_api_surfaces_are_rejected(string path)
    {
        var json = Committed().Replace("\"normalizedPath\": \"/agents\"", "\"normalizedPath\": \"" + path + "\"", StringComparison.Ordinal);
        Assert.NotEqual(Committed(), json);
        var error = Assert.Throws<InvalidOperationException>(() => Parse(json));
        Assert.Contains("exclusion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reserved_profile_ids_are_rejected_for_profiles_and_endpoints()
    {
        var profile = Committed().Replace("\"profileId\": \"NATIVE-PUBLIC-1\",\n      \"conformance\"",
            "\"profileId\": \"UNASSIGNED\",\n      \"conformance\"", StringComparison.Ordinal);
        Assert.NotEqual(Committed(), profile);
        Assert.Contains("reserved", Assert.Throws<InvalidOperationException>(() => Parse(profile)).Message, StringComparison.Ordinal);

        var endpoint = Regex.Replace(Committed(), "(\"normalizedPath\": \"/api/v1/system/status\", \"operationId\": \"GetSystemStatus\", \"profileId\": )\"[^\"]+\"",
            "$1\"UNASSIGNED\"");
        Assert.NotEqual(Committed(), endpoint);
        Assert.Contains("reserved", Assert.Throws<InvalidOperationException>(() => Parse(endpoint)).Message, StringComparison.Ordinal);
    }

    // An unprofiled endpoint mapped by any part of the pipeline stops the host from starting.
    [Fact]
    public void Host_with_an_unprofiled_endpoint_fails_to_start()
    {
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Test")
            .ConfigureServices(s => s.AddTransient<IStartupFilter, UnprofiledEndpoint>()));
        var error = Assert.ThrowsAny<Exception>(() => host.Services.GetRequiredService<EndpointDataSource>());
        var messages = new List<string>();
        for (var e = error; e != null; e = e.InnerException) messages.Add(e.Message);
        Assert.Contains(messages, m => m.Contains("/api/v1/unprofiled-startup-probe: mapped endpoint has no profile", StringComparison.Ordinal));
    }

    private sealed class UnprofiledEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            app.UseEndpoints(e => e.MapGet("/api/v1/unprofiled-startup-probe", () => "unprofiled").RequireAuthorization("DirectoryRead"));
            next(app);
        };
    }
}
