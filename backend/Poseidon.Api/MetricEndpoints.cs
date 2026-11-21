using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record MetricDefinitionDto(string Id, int Version, string OwnerModule, string Label, string UnitCode,
    string SourceProjectionId, string Aggregation, int MinCohort, string FreshnessRule, string[] RequiredPermissions,
    string[] AllowedActions, string[] Scopes, string Basis, string Source);
public sealed record MetricCatalogDto(string ContractVersion, MetricDefinitionDto[] Items);
// Scope coverage over visible source subjects: Expected = Read + Withheld +
// Unreadable + NotCollected (all zero except Expected when UNAVAILABLE).
// WithheldSources counts ONLY sources the actor's current access hides;
// UnreadableSources are stored sources that could not be decoded and
// NotCollectedSources subjects with no source declaration. Never a hidden-record count.
public sealed record MetricScopeDto(string Kind, Guid? VesselId, int ExpectedSources, int ReadSources, int WithheldSources,
    int UnreadableSources, int NotCollectedSources);
// 55.01 paragraph:834 / 61.03 paragraph:177 MetricTile alignment. Value is an
// invariant decimal string and is present ONLY when State is VALUE.
public sealed record MetricScalarDto(string MetricId, int DefinitionVersion, string OwnerModule, string State,
    string? Value, string UnitCode, string Quality, DateTimeOffset AsOf, string SourceProjectionId,
    DateTimeOffset? SourceWatermark, MetricScopeDto Scope, int UnknownCount, string[] Reasons, string Basis,
    string FreshnessRule, string Source);

// PLAN-1511-5501-SLICE-01: read-only metric catalog and scalar evaluation.
// The selector (metricId, vesselId) is never authority: tenant, vessel grant,
// ship/tug mode and source permissions are re-derived from the trusted actor.
public static class MetricEndpoints
{
    public const string ContractVersion = "metrics.v1";

    public static void MapMetrics(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/metrics/definitions", (Delegate)Catalog).RequireAuthorization("DirectoryRead")
            .WithName("ListMetricDefinitions").Produces<MetricCatalogDto>().Produces(400).Produces(401).Produces(403).Produces(503)
            .WithSummary("55.01 metric registry entries the actor may view. Definition only; no formula designer or export.");
        // Query values are declared for OpenAPI only; the handler parses the raw
        // query exactly (single values, no unknown keys).
        app.MapGet("/api/v1/metrics/scalar", (HttpContext h, PoseidonDbContext db, [FromQuery] string? metricId,
                [FromQuery] string? vesselId) => Scalar(h, db)).RequireAuthorization("DirectoryRead")
            .WithName("GetMetricScalar").Produces<MetricScalarDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503)
            .WithSummary("Evaluates one registered scalar over records the actor can currently read. SUPPRESSED, UNKNOWN and UNAVAILABLE carry no value; zero is never substituted for a missing source.");
    }

    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);

    private static string AccessHash(TrustedActor actor) => RegisterStorage.Hash(JsonSerializer.Serialize(new {
        actor.Id, actor.TenantId, actor.TenantWideRead, actor.TenantWideManage,
        permissions = actor.Permissions.Order(StringComparer.Ordinal),
        vessels = actor.VesselGrants.OrderBy(g => g.VesselId).ThenBy(g => g.CanEdit)
    }));

    private static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? resource = null)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, code, resourceId: resource);
        return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }

    internal static MetricDefinitionDto Dto(MetricDefinition d) => new(d.Id, d.Version, d.OwnerModule, d.Label, d.UnitCode,
        d.SourceProjectionId, d.Aggregation, d.MinCohort, d.FreshnessRule, d.RequiredPermissions, d.AllowedActions, d.Scopes, d.Basis, d.Source);

    private static async Task<IResult> Catalog(HttpContext h)
    {
        const string action = "MetricCatalogRead";
        h.Response.Headers.CacheControl = "no-store";
        if (h.Request.Query.Count != 0) return await Fail(h, action, 400, "invalid_metric_query");
        var actor = Actor(h);
        if (actor?.CanRead != true) return await Fail(h, action, 403, "permission_denied");
        // Unauthorized definitions are omitted, not listed as locked entries.
        var items = MetricRegistry.All.Where(d => MetricRegistry.Permits(d, actor.Permissions)).Select(Dto).ToArray();
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Success");
        return Results.Ok(new MetricCatalogDto(ContractVersion, items));
    }

    private static async Task<IResult> Scalar(HttpContext h, PoseidonDbContext db)
    {
        const string action = "MetricScalarRead";
        h.Response.Headers.CacheControl = "no-store";
        var query = h.Request.Query;
        if (query.Any(x => x.Key is not ("metricId" or "vesselId") || x.Value.Count != 1) || !query.ContainsKey("metricId"))
            return await Fail(h, action, 400, "invalid_metric_query");
        Guid? vesselId = null;
        if (query.ContainsKey("vesselId"))
        {
            if (!Guid.TryParseExact(query["vesselId"], "D", out var parsed) || parsed == Guid.Empty)
                return await Fail(h, action, 400, "invalid_metric_query");
            vesselId = parsed;
        }
        var definition = MetricRegistry.Find(query["metricId"].ToString());
        if (definition == null) return await Fail(h, action, 404, "metric_not_found");
        var actor = Actor(h);
        if (actor?.CanRead != true || !MetricRegistry.Permits(definition, actor.Permissions))
            return await Fail(h, action, 403, "permission_denied");
        if (!definition.Scopes.Contains(vesselId == null ? MetricRegistry.ScopeTenantVisible : MetricRegistry.ScopeVessel))
            return await Fail(h, action, 400, "invalid_metric_scope");
        var access = AccessHash(actor);
        var ct = h.RequestAborted;
        MetricScalarDto dto;
        // One read-only snapshot for scope and every source row of this scalar.
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct))
        {
            var asOf = h.UtcNow();
            var vessels = actor.Scope(db.Vessels.AsNoTracking());
            if (vesselId is { } selected)
            {
                if (!await vessels.AnyAsync(v => v.Id == selected, ct))
                {
                    await tx.RollbackAsync(ct);
                    return await Fail(h, action, 404, "resource_not_found");
                }
                vessels = vessels.Where(v => v.Id == selected);
            }
            var (observation, watermark) = definition.Id switch
            {
                MetricRegistry.CrewDeclaredPersons => (await CrewPersons(db, vessels, ct), (DateTimeOffset?)null),
                _ => await Onboarding(definition, actor, db, vessels, ct)
            };
            var outcome = MetricScalarPolicy.Evaluate(definition, observation);
            await tx.RollbackAsync(ct);
            dto = new(definition.Id, definition.Version, definition.OwnerModule, outcome.State,
                outcome.Value?.ToString(CultureInfo.InvariantCulture), definition.UnitCode, outcome.Quality, asOf,
                definition.SourceProjectionId, outcome.State == MetricStates.Value ? watermark : null,
                Scope(vesselId, observation),
                outcome.UnknownCount, outcome.Reasons, definition.Basis, definition.FreshnessRule, definition.Source);
        }
        // Current access is rechecked after evaluation: a grant or permission
        // withdrawn meanwhile denies the response instead of returning a stale scope.
        var live = Actor(h);
        if (live == null || AccessHash(live) != access) return await Fail(h, action, 403, "permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, "Success", resourceId: vesselId);
        live = Actor(h);
        if (live == null || AccessHash(live) != access) return await Fail(h, action, 403, "permission_denied");
        return Results.Ok(dto);
    }

    private static MetricScopeDto Scope(Guid? vesselId, MetricObservation o)
    {
        var kind = vesselId == null ? MetricRegistry.ScopeTenantVisible : MetricRegistry.ScopeVessel;
        return o.UnavailableReason == null
            ? new(kind, vesselId, o.ExpectedSources, o.ReadSources, o.WithheldSources, o.UnreadableSources, o.NotCollectedSources)
            : new(kind, vesselId, o.ExpectedSources, 0, 0, 0, 0);
    }

    // projection.compliance-onboarding.v1: the same ComplianceOnboardingPolicy
    // the onboarding/passport owner uses, applied per visible vessel. A profile
    // bound to a construction file is withheld without file.read (same rule as
    // RegulatoryDraftEndpoints.ReadCurrent); it is UNKNOWN, never "complete".
    private static async Task<(MetricObservation, DateTimeOffset?)> Onboarding(MetricDefinition definition, TrustedActor actor,
        PoseidonDbContext db, IQueryable<Vessel> vessels, CancellationToken ct)
    {
        var files = actor.Permissions.Contains("file.read");
        var rows = await vessels.OrderBy(v => v.Id).Take(MetricRegistry.MaxSourceVessels + 1).Select(v => new {
            v.Id, v.Code, v.Name,
            Draft = db.RegulatoryDrafts.Where(d => d.VesselId == v.Id).Select(d => new {
                Readable = files || d.ConstructionFileId == null,
                Content = files || d.ConstructionFileId == null ? d.ContentJson : null,
                SavedAt = files || d.ConstructionFileId == null ? (DateTimeOffset?)d.SavedAt : null
            }).FirstOrDefault()
        }).AsNoTracking().ToArrayAsync(ct);
        if (rows.Length > MetricRegistry.MaxSourceVessels)
            return (new(rows.Length, 0, null, 0, null, "QUERY_LIMIT_EXCEEDED"), null);
        var unknownFields = definition.Id == MetricRegistry.OnboardingRequiredFieldsUnknown;
        int read = 0, unreadable = 0, notCollected = 0; long count = 0; DateTimeOffset? watermark = null;
        foreach (var row in rows)
        {
            if (row.Draft is { Readable: false }) continue; // withheld by current access
            RegulatoryDraftContent? content = null;
            if (row.Draft == null)
            {
                // No declaration: the unknown-field source was never collected
                // (55.01 paragraph:263/652), never zero unknown fields. A missing
                // profile still means the vessel itself requires review.
                if (unknownFields) { notCollected++; continue; }
            }
            else
            {
                try { content = RegulatoryDraftStorage.Deserialize(row.Draft.Content!); }
                catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or InvalidDataException or FormatException)
                { unreadable++; continue; }
                if (watermark == null || row.Draft.SavedAt > watermark) watermark = row.Draft.SavedAt;
            }
            var assessment = ComplianceOnboardingPolicy.Assess(row.Id, row.Code, row.Name, content);
            read++;
            count += unknownFields
                ? assessment.Fields.Count(f => f.State == "Unknown")
                : assessment.State == "REVIEW_REQUIRED" ? 1 : 0;
        }
        // Withheld, undecodable and not-collected profiles are unread sources with
        // separate causes: the value covers only read sources and is PARTIAL (or
        // UNKNOWN when nothing was read), never a full-fleet total or a zero.
        return (new(rows.Length, read, count, 0, UnreadableSources: unreadable, NotCollectedSources: notCollected), watermark);
    }

    // projection.crew-assignment-declarations.v1 (definition v2): distinct persons
    // with any declared assignment on visible vessels, regardless of assignment
    // status or dates. Declaration only: no current-crew, presence, eligibility or
    // verification claim. Per-vessel components feed complementary and overlap
    // small-group suppression.
    private static async Task<MetricObservation> CrewPersons(PoseidonDbContext db, IQueryable<Vessel> vessels, CancellationToken ct)
    {
        var ids = await vessels.OrderBy(v => v.Id).Take(MetricRegistry.MaxSourceVessels + 1).Select(v => v.Id).ToArrayAsync(ct);
        if (ids.Length > MetricRegistry.MaxSourceVessels) return new(ids.Length, 0, null, 0, null, "QUERY_LIMIT_EXCEEDED");
        var pairs = await db.CrewAssignments.AsNoTracking().Where(a => ids.Contains(a.VesselId))
            .Select(a => new { a.VesselId, a.PersonId }).Distinct().ToArrayAsync(ct);
        var components = pairs.GroupBy(p => p.VesselId).Select(g => (long)g.Count()).ToArray();
        return new(ids.Length, ids.Length, pairs.Select(p => p.PersonId).Distinct().LongCount(), 0, components);
    }
}
