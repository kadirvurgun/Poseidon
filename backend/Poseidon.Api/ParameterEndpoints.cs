using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ParameterTypeDto(string Type, bool Nullable, long? Minimum, long? Maximum, string? Unit);
public sealed record ParameterDefinitionDto(string Key, int DefinitionVersion, string OwnerDomain, string Classification, string ValueClass,
    string? Risk, ParameterTypeDto Type, string[] AllowedScopes, string[] RequiredSelectors, string Fallback, string? ApprovedProductDefault,
    bool ProductDefaultDeploymentAccepted, string? AliasTarget, bool Writable, string OwnerVerification, string Sensitivity,
    string SourceLocator, string RuntimeStatus);
public sealed record ParameterSourceDto(string Path, string Sha256);
public sealed record ParameterDefinitionListDto(ParameterDefinitionDto[] Items, int SourceDefinitionCount, int RegisteredCount, ParameterSourceDto Source);
public sealed record EffectiveParameterDto(string RequestedKey, string Key, string? AliasOf, int DefinitionVersion, string Mode,
    DateTimeOffset EffectiveAt, DateTimeOffset KnownAt, string ScopeKind, Guid ScopeId, string Status, string ValueClass, JsonElement? Value,
    string? ValueMode, Guid? SourceRevisionId, string? SourceScope, Guid? SourceScopeId, long? SourceVersion, DateTimeOffset? SelectedFrom,
    string? ContentHash, bool FallbackUsed, string OwnerVerification, bool PositiveUseAllowed, string[] ReasonCodes, string ApplicabilityHash);
public sealed record ParameterDecisionDto(Guid Id, string Outcome, Guid DeciderId, DateTimeOffset DecidedAt, DateTimeOffset? SelectedFrom, string Reason);
public sealed record ParameterRevisionDto(Guid Id, long Sequence, int DefinitionVersion, string ValueMode, JsonElement? Value,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string Reason, Guid AuthorId, DateTimeOffset AuthoredAt, string Provenance,
    string ContentHash, string State, ParameterDecisionDto? Decision);
public sealed record ParameterChangeDto(Guid Id, string Key, string ScopeKind, Guid ScopeId, JsonElement Applicability, long Revision,
    uint Version, Guid? PendingRevisionId, ParameterRevisionDto[] Revisions);
public sealed record ParameterCommandDto(Guid ChangeId, Guid RevisionId, long Revision, uint Version, string State, DateTimeOffset? SelectedFrom, bool Replayed);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProposeParameterChange(uint? ExpectedVersion, string? Key, int? DefinitionVersion, string? ScopeKind, Guid? ScopeId,
    Dictionary<string, string>? Applicability, string? ValueMode, JsonElement? Value, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo,
    string? Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecideParameterChange(uint? ExpectedVersion, Guid? RevisionId, string? Reason);

// PLAN-1511-5401 / 54.01 CFG3 foundation. Native transport (NATIVE-READ-1 / NATIVE-COMMAND-1 of
// 58.03): cookie session + CSRF, header Idempotency-Key with a durable tenant receipt, body
// expectedVersion CAS (stale 409 version_conflict), no-store. Parameter publication never grants an
// operational permission, qualification, certificate validity or deletion authority (paragraph:4).
public static class ParameterEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 8, PropertyNameCaseInsensitive = false };
    private const int MaxBody = 16384;

    public static void MapParameters(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1").RequireAuthorization("DirectoryRead");
        g.MapGet("/parameter-definitions", (Func<HttpContext, Task<IResult>>)Definitions).WithName("ListParameterDefinitions").Produces<ParameterDefinitionListDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503)
            .WithDescription("config.read. Registered 54.01 CFG3 definition subset with source locators; the source declares 354 definitions. Registration is not owner adapter admission.");
        g.MapGet("/effective-parameters", Resolve).WithName("ResolveEffectiveParameter").Produces<EffectiveParameterDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503)
            .WithDescription("config.resolve. Query key, scopeKind TENANT|COMPANY|VESSEL, scopeId (COMPANY/VESSEL), applicability (base64url canonical JSON object), mode CURRENT|HISTORICAL, effectiveAt+knownAt (HISTORICAL only). Reports status RESOLVED|UNCONFIGURED|AMBIGUOUS|BLOCKED|UNKNOWN with source scope and version; never fabricates a value.");
        g.MapGet("/parameter-changes/{id:guid}", Change).WithName("GetParameterChange").Produces<ParameterChangeDto>()
            .Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503)
            .WithDescription("config.read. Immutable revision and decision lineage of one parameter root; version is the CAS token for decisions.");
        g.MapPost("/parameter-changes", (Delegate)((HttpContext http) => Propose(http))).CommandHeaders()
            .WithName("ProposeParameterChange").Accepts<ProposeParameterChange>("application/json").Produces<ParameterCommandDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).Produces(503)
            .WithDescription("config.draft. Proposes one typed value revision (IN_REVIEW). expectedVersion 0 creates the root. Owner fields, invariants, aliases and family references are rejected with 422. No approval, schedule or activation.");
        g.MapPost("/parameter-changes/{id:guid}/commands/approve", (Guid id, HttpContext http) => Decide(id, true, http)).CommandHeaders()
            .WithName("ApproveParameterChange").Accepts<DecideParameterChange>("application/json").Produces<ParameterCommandDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).Produces(503)
            .WithDescription("config.approve. Independent approver only (maker != checker). The selection is append-only and effective no earlier than the decision.");
        g.MapPost("/parameter-changes/{id:guid}/commands/reject", (Guid id, HttpContext http) => Decide(id, false, http)).CommandHeaders()
            .WithName("RejectParameterChange").Accepts<DecideParameterChange>("application/json").Produces<ParameterCommandDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).Produces(503)
            .WithDescription("config.reject. Independent reviewer only; REJECTED is terminal and never selectable.");
    }

    // Runtime-required command headers are validated in the handler (403 csrf_invalid / 400 invalid_idempotency_key)
    // and advertised as required in OpenAPI, as for the register draft commands.
    private static RouteHandlerBuilder CommandHeaders(this RouteHandlerBuilder builder) => builder.AddOpenApiOperationTransformer((operation, _, _) =>
    {
        operation.Parameters ??= [];
        foreach (var name in new[] { "Idempotency-Key", "X-CSRF-TOKEN" })
            operation.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter { Name = name, In = Microsoft.OpenApi.ParameterLocation.Header,
                Required = true, Schema = new Microsoft.OpenApi.OpenApiSchema { Type = Microsoft.OpenApi.JsonSchemaType.String } });
        return Task.CompletedTask;
    });

    private static TrustedActor? Actor(HttpContext http) => http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User);
    // Tenant configuration authority is explicit and tenant-wide; vessel grants or directory writes confer none.
    internal static bool Can(TrustedActor? a, string permission) => a != null && a.CanRead && (a.TenantWideRead || a.TenantWideManage) && a.Permissions.Contains(permission);
    internal static bool CanWrite(TrustedActor? a, string permission) => a != null && a.HasWriteAttribution && Can(a, permission);

    private static async Task<IResult> Fail(HttpContext http, string action, int status, string code, IEnumerable<ParameterFieldError>? fields = null, Guid? resource = null)
    {
        await http.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(http, action, code, resourceId: resource);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
        {
            ["code"] = code, ["correlationId"] = http.TraceIdentifier,
            ["fieldErrors"] = (fields ?? []).Select(x => new { path = x.Path, code = x.Code }).ToArray()
        });
    }

    private static ParameterDefinitionDto Dto(ParameterDefinition d) => new(d.Key, d.DefinitionVersion, d.OwnerDomain, d.Classification, d.ValueClass,
        d.Risk, new(d.Type.Type, d.Type.Nullable, d.Type.Minimum, d.Type.Maximum, d.Type.Unit), [.. d.AllowedScopes], [.. d.RequiredSelectors], d.Fallback,
        d.ApprovedProductDefault, d.ProductDefaultDeploymentAccepted, d.AliasTarget, d.Writable, d.OwnerVerification, d.Sensitivity, d.SourceLocator, d.RuntimeStatus);

    private static async Task<IResult> Definitions(HttpContext http)
    {
        var actor = Actor(http);
        if (!Can(actor, "config.read")) return await Fail(http, "ParameterRead", 403, "permission_denied");
        var query = http.Request.Query;
        if (query.Any(q => q.Key is not ("owner" or "classification" or "key") || q.Value.Count != 1 || string.IsNullOrEmpty(q.Value[0]) || q.Value[0]!.Length > 128))
            return await Fail(http, "ParameterRead", 400, "invalid_query");
        var items = ParameterRegistry.Definitions.Where(d =>
            (!query.ContainsKey("owner") || d.OwnerDomain == query["owner"][0]) &&
            (!query.ContainsKey("classification") || d.Classification == query["classification"][0]) &&
            (!query.ContainsKey("key") || d.Key == query["key"][0])).Select(Dto).ToArray();
        await http.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(http, "ParameterRead", "Success");
        return Results.Ok(new ParameterDefinitionListDto(items, ParameterRegistry.SourceDefinitionCount, ParameterRegistry.Definitions.Count,
            new(ParameterRegistry.SourcePath, ParameterRegistry.SourceSha256)));
    }

    private static JsonElement? Value(string? json) => json == null ? null : JsonDocument.Parse(json).RootElement.Clone();

    private static Dictionary<string, string>? Applicability(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return [];
        if (encoded.Length > 2732 || encoded.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return null;
        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            using var document = JsonDocument.Parse(Convert.FromBase64String(padded), new() { MaxDepth = 2 });
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any(p => p.Value.ValueKind != JsonValueKind.String)) return null;
            return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        }
        catch (Exception error) when (error is FormatException or JsonException) { return null; }
    }

    // Server-built scope chain. Company/vessel identities are resolved inside the actor's visible
    // tenant scope; an invisible or foreign identity is indistinguishable from a missing one (404).
    private static async Task<ParameterScopeLevel[]?> Chain(TrustedActor actor, PoseidonDbContext db, string scopeKind, Guid? scopeId, CancellationToken ct)
    {
        switch (scopeKind)
        {
            case "TENANT":
                return scopeId == null || scopeId == actor.TenantId ? [new("TENANT", actor.TenantId), new("GLOBAL", null)] : null;
            case "COMPANY":
                if (scopeId is not { } company || !await db.Companies.AsNoTracking().AnyAsync(c => c.Id == company, ct)) return null;
                if (!(actor.TenantWideRead || actor.TenantWideManage)) return null;
                return [new("COMPANY", company), new("TENANT", actor.TenantId), new("GLOBAL", null)];
            case "VESSEL":
                if (scopeId is not { } vesselId) return null;
                var vessel = await actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vesselId).Select(v => new { v.Id, v.CompanyId }).SingleOrDefaultAsync(ct);
                return vessel == null ? null : [new("VESSEL", vessel.Id), new("COMPANY", vessel.CompanyId), new("TENANT", actor.TenantId), new("GLOBAL", null)];
            default: return null;
        }
    }

    private static async Task<IResult> Resolve(HttpContext http, PoseidonDbContext db, TimeProvider clock)
    {
        var actor = Actor(http); var ct = http.RequestAborted;
        if (!Can(actor, "config.resolve")) return await Fail(http, "ParameterRead", 403, "permission_denied");
        var query = http.Request.Query;
        string[] allowed = ["key", "scopeKind", "scopeId", "applicability", "mode", "effectiveAt", "knownAt"];
        if (query.Any(q => !allowed.Contains(q.Key, StringComparer.Ordinal) || q.Value.Count != 1)) return await Fail(http, "ParameterRead", 400, "invalid_query");
        string? Q(string name) => query.TryGetValue(name, out var v) ? v[0] : null;
        var mode = Q("mode") ?? "CURRENT";
        var now = ParameterValuePolicy.Micro(clock.GetUtcNow());
        DateTimeOffset effectiveAt = now, knownAt = now;
        static bool Instant(string? value, out DateTimeOffset parsed) =>
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed) &&
            value!.EndsWith('Z');
        Guid? scopeId = null;
        if (Q("scopeId") is { } rawScope) { if (!Guid.TryParseExact(rawScope, "D", out var parsedScope)) return await Fail(http, "ParameterRead", 400, "invalid_query"); scopeId = parsedScope; }
        if (Q("key") is not { } requested || Q("scopeKind") is not { } scopeKind || mode is not ("CURRENT" or "HISTORICAL") ||
            mode == "CURRENT" && (Q("effectiveAt") != null || Q("knownAt") != null) ||
            mode == "HISTORICAL" && (!Instant(Q("effectiveAt"), out effectiveAt) || !Instant(Q("knownAt"), out knownAt) || knownAt > now))
            return await Fail(http, "ParameterRead", 400, "invalid_query");
        var selectors = Applicability(Q("applicability"));
        if (selectors == null) return await Fail(http, "ParameterRead", 400, "invalid_query");
        var requestedDefinition = ParameterRegistry.Find(requested);
        if (requestedDefinition == null) return await Fail(http, "ParameterRead", 404, "resource_not_found");
        // CFG3-T09: an alias read returns the canonical definition's value; it never stores its own.
        var definition = requestedDefinition.AliasTarget is { } target ? ParameterRegistry.Find(target)! : requestedDefinition;
        var chain = await Chain(actor!, db, scopeKind, scopeId, ct);
        if (chain == null) return await Fail(http, "ParameterRead", 404, "resource_not_found");
        var selector = ParameterValuePolicy.Selectors(definition, selectors);
        if (selector == null && definition.Writable) return await Fail(http, "ParameterRead", 400, "selector_invalid");
        var hash = selector?.Hash ?? ParameterValuePolicy.Hash("{}");
        var candidates = definition.Writable ? await ParameterWriter.CandidatesAsync(db, definition.Key, chain, hash, ct) : [];
        var result = ParameterResolver.Resolve(definition, chain, hash, candidates, effectiveAt, knownAt);
        var reasons = requestedDefinition == definition ? result.ReasonCodes : ["ALIAS_READ_CANONICAL", .. result.ReasonCodes];
        if (mode == "HISTORICAL") reasons = [.. reasons, "HISTORICAL_NOT_CURRENT_PERMISSION"];
        await http.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(http, "ParameterRead", "Success");
        return Results.Ok(new EffectiveParameterDto(requested, definition.Key, requestedDefinition == definition ? null : definition.Key,
            definition.DefinitionVersion, mode, effectiveAt, knownAt, chain[0].Kind, chain[0].Id!.Value, result.Status, result.ValueClass,
            Value(result.ValueJson), result.ValueMode, result.SourceRevisionId, result.SourceScope, result.SourceScopeId, result.SourceVersion,
            result.SelectedFrom, result.ContentHash, result.FallbackUsed, result.OwnerVerification,
            mode == "CURRENT" && result.PositiveUseAllowed, reasons, hash));
    }

    private static async Task<IResult> Change(Guid id, HttpContext http, PoseidonDbContext db)
    {
        var actor = Actor(http); var ct = http.RequestAborted;
        if (!Can(actor, "config.read")) return await Fail(http, "ParameterRead", 403, "permission_denied");
        var root = await db.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (root == null || root.ScopeKind == "VESSEL" && !await actor!.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == root.ScopeId, ct))
            return await Fail(http, "ParameterRead", 404, "resource_not_found", resource: id);
        var revisions = await db.ParameterRevisions.AsNoTracking().Where(x => x.RootId == id).OrderBy(x => x.Sequence).ToArrayAsync(ct);
        var decisions = await db.ParameterDecisions.AsNoTracking().Where(x => x.RootId == id).ToDictionaryAsync(x => x.RevisionId, ct);
        await http.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(http, "ParameterRead", "Success", resourceId: id);
        return Results.Ok(new ParameterChangeDto(root.Id, root.Key, root.ScopeKind, root.ScopeId, JsonDocument.Parse(root.SelectorJson).RootElement.Clone(),
            root.Revision, root.Version, root.PendingRevisionId, revisions.Select(r =>
            {
                var decision = decisions.GetValueOrDefault(r.Id);
                return new ParameterRevisionDto(r.Id, r.Sequence, r.DefinitionVersion, r.ValueMode, Value(r.ValueJson), r.EffectiveFrom, r.EffectiveTo,
                    r.Reason, r.AuthorId, r.AuthoredAt, r.Provenance, r.ContentHash, decision?.Outcome ?? (root.PendingRevisionId == r.Id ? "IN_REVIEW" : "UNDECIDED"),
                    decision == null ? null : new(decision.Id, decision.Outcome, decision.DeciderId, decision.DecidedAt, decision.SelectedFrom, decision.Reason));
            }).ToArray()));
    }

    private static async Task<(T? Command, string? Key, IResult? Failure)> Command<T>(HttpContext http, string permission) where T : class
    {
        var actor = Actor(http);
        if (!CanWrite(actor, permission)) return (null, null, await Fail(http, "ParameterCommand", 403, "permission_denied"));
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { return (null, null, await Fail(http, "ParameterCommand", 403, "csrf_invalid")); }
        if (http.Request.Query.Count != 0 || !http.Request.HasJsonContentType() || http.Request.ContentLength > MaxBody)
            return (null, null, await Fail(http, "ParameterCommand", 400, "invalid_request"));
        T? command;
        try
        {
            using var bytes = new MemoryStream();
            await SecureFilePolicy.CopyAsync(http.Request.Body, bytes, MaxBody, http.RequestAborted);
            using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 8 });
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(Json);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or NotSupportedException) { return (null, null, await Fail(http, "ParameterCommand", 400, "invalid_request")); }
        var keys = http.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return (null, null, await Fail(http, "ParameterCommand", 400, "invalid_idempotency_key"));
        return command == null ? (null, null, await Fail(http, "ParameterCommand", 400, "invalid_request")) : (command, key, null);
    }

    private static int Status(string code) => code switch
    {
        "resource_not_found" => 404,
        "permission_denied" => 403,
        "separation_of_duties" or "synthetic_not_approvable" or "outside_effective_dates" => 422,
        "service_unavailable" => 503,
        _ => 409
    };

    private static async Task<IResult> Outcome(HttpContext http, ParameterWriteResult result)
    {
        if (result.Code != null) return await Fail(http, "ParameterCommand", Status(result.Code), result.Code, resource: result.RootId == Guid.Empty ? null : result.RootId);
        if (result.Replayed) http.Response.Headers["Idempotency-Replayed"] = "true";
        await http.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(http, "ParameterCommand", "Success", resourceId: result.RootId);
        return Results.Ok(new ParameterCommandDto(result.RootId, result.RevisionId, result.Revision, result.Version, result.State, result.SelectedFrom, result.Replayed));
    }

    private static Func<Task<bool>> Revalidate(HttpContext http, TrustedActor actor, string permission) => () =>
    {
        var live = Actor(http);
        return Task.FromResult(live != null && live.Id == actor.Id && live.TenantId == actor.TenantId && CanWrite(live, permission));
    };

    private static async Task<IResult> Propose(HttpContext http)
    {
        var (command, key, failure) = await Command<ProposeParameterChange>(http, "config.draft");
        if (failure != null) return failure;
        var actor = Actor(http)!; var ct = http.RequestAborted;
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        var now = ParameterValuePolicy.Micro(http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        if (command!.ExpectedVersion == null || command.DefinitionVersion == null) return await Fail(http, "ParameterCommand", 400, "invalid_request");
        var definition = ParameterRegistry.Find(command.Key);
        if (definition == null) return await Fail(http, "ParameterCommand", 404, "resource_not_found");
        var (proposal, errors) = ParameterValuePolicy.Propose(definition, command.DefinitionVersion.Value, command.ScopeKind, command.Applicability,
            command.ValueMode, command.Value, command.EffectiveFrom, command.EffectiveTo, command.Reason, now);
        // An exact retry is judged at its original acceptance instant, so an elapsed effectiveFrom
        // cannot turn a lost-reply retry into a validation failure; a different payload still conflicts.
        if (proposal == null && await db.ParameterCommandReceipts.AsNoTracking().Where(x => x.Key == key && x.ActorId == actor.Id && x.Action == ParameterWriter.Propose)
                .Select(x => (DateTimeOffset?)x.CommittedAt).SingleOrDefaultAsync(ct) is { } accepted)
            (proposal, errors) = ParameterValuePolicy.Propose(definition, command.DefinitionVersion.Value, command.ScopeKind, command.Applicability,
                command.ValueMode, command.Value, command.EffectiveFrom, command.EffectiveTo, command.Reason, accepted);
        // 54.01 paragraph:96: a key already bound to another actor, route or payload is a 409
        // conflict, never a validation answer for the reused key's new content.
        if (proposal == null && await ParameterWriter.KeyBoundAsync(db, key!, ct)) return await Fail(http, "ParameterCommand", 409, "idempotency_key_reused");
        if (proposal == null) return await Fail(http, "ParameterCommand", 422, errors[0].Code, errors);
        var chain = await Chain(actor, db, proposal.ScopeKind, proposal.ScopeKind == "TENANT" ? command.ScopeId ?? actor.TenantId : command.ScopeId, ct);
        if (chain == null) return await Fail(http, "ParameterCommand", 404, "resource_not_found");
        var result = await ParameterWriter.ProposeAsync(db, actor.Id, key!, command.ExpectedVersion.Value, definition, proposal, chain[0].Id!.Value,
            now, Revalidate(http, actor, "config.draft"), ct);
        return await Outcome(http, result);
    }

    private static async Task<IResult> Decide(Guid id, bool approve, HttpContext http)
    {
        var permission = approve ? "config.approve" : "config.reject";
        var (command, key, failure) = await Command<DecideParameterChange>(http, permission);
        if (failure != null) return failure;
        var actor = Actor(http)!; var ct = http.RequestAborted;
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        if (command!.ExpectedVersion == null || command.RevisionId is not { } revision || command.Reason is not { } reason ||
            !ParameterValuePolicy.ValidReason(reason))
            return await ParameterWriter.KeyBoundAsync(db, key!, ct)
                ? await Fail(http, "ParameterCommand", 409, "idempotency_key_reused", resource: id)
                : await Fail(http, "ParameterCommand", 400, "invalid_request", resource: id);
        var scope = await db.ParameterRoots.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ScopeKind, x.ScopeId }).SingleOrDefaultAsync(ct);
        if (scope == null || scope.ScopeKind == "VESSEL" && !await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == scope.ScopeId, ct))
            return await Fail(http, "ParameterCommand", 404, "resource_not_found", resource: id);
        var now = ParameterValuePolicy.Micro(http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow());
        var result = await ParameterWriter.DecideAsync(db, actor.Id, key!, id, command.ExpectedVersion.Value, revision, approve, reason, now,
            Revalidate(http, actor, permission), ct);
        return await Outcome(http, result);
    }
}
