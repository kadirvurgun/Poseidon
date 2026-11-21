using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record NavigationDto(string Key, string Href);
public sealed record WorkspaceDto(NavigationDto[] Destinations, bool CanSearch, bool CanWriteRegulatory = false);
public sealed record SearchItemDto(string Key, string Href, string Label, string Kind);

// NAV-01/02: projections contain implemented routes only, with the same scope as their endpoints.
public static class WorkspaceEndpoints
{
    public static NavigationDto[] Destinations(TrustedActor actor)
    {
        if (!actor.CanRead) return [];
        NavigationDto[] destinations = actor.CanReadAudit ? [.. BaseDestinations(actor), new NavigationDto("audit", "/app/audit")] : BaseDestinations(actor);
        // DEV-032: audit-ready trace only for an audit reader with the human trace capability.
        if (AuditReadyEndpoints.CanRead(actor)) destinations = [.. destinations, new("audit-ready", "/app/audit-ready")];
        if (SystemHealthEndpoints.Permitted(actor)) destinations = [.. destinations, new("system-health", "/app/system-health")];
        if (NotificationEndpoints.CanRead(actor)) destinations = [.. destinations, new("notification-escalation", "/app/notification-escalation")];
        if (SyncConflictCenterEndpoints.CanOpen(actor)) destinations = [.. destinations, new("offline-conflict-center", "/app/offline-conflict-center")];
        if (DefectAssetContextReader.CanRead(actor)) destinations = [.. destinations, new("defects", "/app/defects")];
        if (WorkOrderEndpoints.CanRead(actor)) destinations = [.. destinations, new("work-order-center", "/app/work-order-center")];
        // DEV-056/DEV-103: the same server predicates as their endpoints.
        if (PmsCommandCenterEndpoints.CanRead(actor)) destinations = [.. destinations, new("pms-command-center", "/app/pms-command-center")];
        // S9 DEV-058/059: the same server predicate as the departure readiness endpoint.
        if (DepartureAuthorities.CanRead(actor)) destinations = [.. destinations, new("departure-signoff", "/app/departure-signoff")];
        // S11 DEV-073/093: the same server predicate as the regulatory review endpoints.
        if (RegulatoryReviewAccess.CanRead(actor)) destinations = [.. destinations, new("regulatory-review", "/app/regulatory-review")];
        if (ServiceReportEndpoints.CanRead(actor)) destinations = [.. destinations, new("service-engineer-report", "/app/service-engineer-report")];
        // S9 WP-C DEV-062 / DEV-114: the same server predicates as their endpoints.
        if (VoiceEventDraftEndpoints.CanRead(actor)) destinations = [.. destinations, new("voice-event-draft", "/app/voice-event-draft")];
        if (ControlledDocumentEndpoints.CanReadDocuments(actor)) destinations = [.. destinations, new("document-compare", "/app/document-compare")];
        // S11 WP-D DEV-074/094/075: the same server predicates as the UAT and pilot gate endpoints.
        if (PilotAccess.UatRead(actor)) destinations = [.. destinations, new("role-uat", "/app/role-uat")];
        if (PilotAccess.GateRead(actor)) destinations = [.. destinations, new("pilot-gate", "/app/pilot-gate")];
        if (PilotProgramAccess.Read(actor)) destinations = [.. destinations, new("pilot-program", "/app/pilot-program")];
        if (OperationalDowntimeEndpoints.CanRead(actor)) destinations = [.. destinations, new("operational-downtime", "/app/operational-downtime")];
        // DEV-105/DEV-106: the same server predicates as the risk endpoints.
        if (RiskAssessmentEndpoints.CanRead(actor)) destinations = [.. destinations, new("risk-assessment", "/app/risk-assessment")];
        if (RiskTemplateEndpoints.CanRead(actor)) destinations = [.. destinations, new("risk-template-library", "/app/risk-template-library")];
        // DEV-110: same server read authority as the critical spare endpoints (spare.read).
        if (CriticalSpareEndpoints.CanRead(actor)) destinations = [.. destinations, new("critical-spares", "/app/critical-spares")];
        // S8 WP-D DEV-107: same server predicate as GET /api/v1/hse/high-risk-work.
        if (HighRiskWorkEndpoints.CanRead(actor)) destinations = [.. destinations, new("high-risk-work", "/app/high-risk-work")];
        // S9 WP-B DEV-060/061: same server predicate as GET /api/v1/safety-events (read or report permission).
        if (SafetyEventAccess.Any(actor)) destinations = [.. destinations, new("safety-events", "/app/safety-events")];
        // S9 WP-D DEV-111: same server predicate as /api/v1/drydock-packages.
        if (DrydockEndpoints.CanRead(actor)) destinations = [.. destinations, new("drydock-planning", "/app/drydock-planning")];
        // S10 WP-A DEV-065 / DEV-113: same server predicates as GET /api/v1/data-trust and GET /api/v1/industry-assurance.
        if (DataTrustSignalSource.CanRead(actor)) destinations = [.. destinations, new("data-trust-center", "/app/data-trust-center")];
        if (AssuranceAccess.Reader(actor)) destinations = [.. destinations, new("industry-assurance", "/app/industry-assurance")];
        if (CertificateReader.Read(actor)) destinations = [.. destinations,
            new("certificate-command-center", "/app/certificate-command-center"), new("renewal-workflow", "/app/renewal-workflow"),
            // DEV-104/DEV-112: same server read authority as their endpoints (certificate.read).
            new("pre-survey-checklist", "/app/pre-survey-checklist"), new("survey-planner", "/app/survey-planner")];
        if (actor.CanReadTug) destinations = [.. destinations, new("tug-command", "/app/tug-command"), new("tug-job-board", "/app/tug-job-board"), new("tug-job-intake", "/app/tug-job-intake"),
            // S7 WP-B DEV-083..086: assignment, readiness gate, tow equipment and crew readiness screens.
            new("tug-assignment", "/app/tug-assignment"), new("tug-readiness", "/app/tug-readiness"), new("tug-equipment-readiness", "/app/tug-equipment-readiness"), new("tug-crew-readiness", "/app/tug-crew-readiness"),
            // S8 WP-D DEV-087/088: towage plan and towage risk & briefing screens.
            new("towage-plan", "/app/towage-plan"), new("towage-risk-briefing", "/app/towage-risk-briefing"),
            // S9 WP-D DEV-089/090: towage execution console and tug daily log (same CanReadTug predicate as the endpoints).
            new("towage-execution", "/app/towage-execution"), new("tug-daily-log", "/app/tug-daily-log"),
            // S10 WP-B DEV-091/092/119: operation history, offline operations and demo journey (same CanReadTug predicate).
            new("tug-operation-history", "/app/tug-operation-history"), new("tug-operations-offline", "/app/tug-operations-offline"), new("tug-demo-journey", "/app/tug-demo-journey")];
        if (actor.CanSeeShip) {
            if (ShipOperationsEndpoints.CanRead(actor,"Fuel")) destinations = [.. destinations,new("fuel-bunker-log","/app/fuel-bunker-log")];
            if (ShipOperationsEndpoints.CanRead(actor,"Port")) destinations = [.. destinations,new("port-activity-sof","/app/port-activity-sof")];
            if (DailyOperationsEndpoints.CanRead(actor,"Noon")) destinations = [.. destinations,new("noon-report","/app/noon-report")];
            if (DailyOperationsEndpoints.CanRead(actor,"Deck")) destinations = [.. destinations,new("deck-daily-log","/app/deck-daily-log")];
            if (DailyOperationsEndpoints.CanRead(actor,"Engine")) destinations = [.. destinations,new("engine-daily-log","/app/engine-daily-log")];
        }
        return actor.CanReadRegulatory ? [.. destinations, new("sources", "/app/sources"), new("requirements", "/app/requirements")] : destinations;
    }
    private static NavigationDto[] BaseDestinations(TrustedActor actor) =>
        actor.TenantWideRead || actor.TenantWideManage
            ? [new("today", "/app/today"), new("companies", "/app/companies"), new("fleet", "/app/fleet")]
            : [new("today", "/app/today"), new("fleet", "/app/fleet")];

    private static readonly string[] SearchQueryKeys = ["q", "offset", "limit"];
    internal static bool SearchQueryClosed(IQueryCollection query) =>
        query.All(p => SearchQueryKeys.Contains(p.Key, StringComparer.OrdinalIgnoreCase) && p.Value.Count == 1);

    public static void MapWorkspace(this IEndpointRouteBuilder app)
    {
        app.MapWorkspaceContext();
        app.MapWorkspaceLenses();
        app.MapGet("/api/v1/workspace", async (HttpContext http, ActorResolver actors, SecurityAudit audit) =>
        {
            var actor = actors.Resolve(http.User)!;
            http.Response.Headers.CacheControl = "no-store";
            // DEV-022: the menu projection has no selector; a client tenant/role/vessel hint is rejected.
            if (http.Request.Query.Count != 0)
            {
                await audit.WriteAsync(http, "WorkspaceRead", "Invalid");
                return Results.BadRequest(new { code = "invalid_request" });
            }
            await audit.WriteAsync(http, "WorkspaceRead", "Success");
            return Results.Ok(new WorkspaceDto(Destinations(actor), actor.CanSearch, actor.CanWriteRegulatory));
        }).RequireAuthorization("DirectoryRead").WithName("GetWorkspace").Produces<WorkspaceDto>().Produces(400).Produces(401).Produces(403).Produces(503);

        app.MapGet("/api/v1/workspace/search", async (HttpContext http, ActorResolver actors, PoseidonDbContext db, SecurityAudit audit, string? q, int? offset, int? limit) =>
        {
            var actor = actors.Resolve(http.User)!;
            if (!actor.CanSearch)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Denied");
                return Results.StatusCode(403);
            }
            // DEV-022: closed query union (65.03 paragraph 170; 54.02 IAM_READ_V1 paragraph 13347 "unknown filter 400"). A tenant, vessel,
            // role, mode or permission selector is never an authority input and is rejected, not ignored.
            if (!SearchQueryClosed(http.Request.Query) || q is { Length: > 100 } || q?.Any(char.IsControl) == true || offset is < 0 or > 100000 || limit is < 1 or > 25)
            {
                await audit.WriteAsync(http, "WorkspaceSearch", "Invalid");
                return Results.BadRequest(new { code = "invalid_search" });
            }
            var search = q?.Trim() ?? "";
            var folded = search.ToLowerInvariant();
            var skip = offset ?? 0;
            var take = limit ?? 25;
            // Stable ordinal destination order, followed by Code/Id vessel order. No total count.
            // Destinations are matched in memory AFTER the scope projection (Destinations(actor)), by route
            // key or TR/EN display label with Turkish-aware folding; a hidden destination's label never
            // participates, so it is indistinguishable from no match.
            var needle = WorkspaceDestinationLabels.Fold(search);
            var destinations = Destinations(actor).Where(x => search == "" || WorkspaceDestinationLabels.Matches(x.Key, needle))
                .Select(x => new SearchItemDto(x.Key, x.Href, x.Key, "Destination")).ToArray();
            var vesselSkip = Math.Max(0, skip - destinations.Length);
            // Scope is applied before matching and pagination, so hidden vessels never influence
            // matches, ordering, hasMore or the page boundary. Matching is case-insensitive via SQL
            // lower(); known limitation: lower() follows the database collation/ctype, so Turkish
            // dotted/dotless I and diacritics are not folded for vessel code/name (destinations are).
            var rows = await actor.Scope(db.Vessels.AsNoTracking())
                .Where(x => folded == "" || x.Code.ToLower().Contains(folded) || x.Name.ToLower().Contains(folded))
                .OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(vesselSkip).Take(take + 1)
                .Select(x => new { x.Id, x.Code, x.Name, x.Kind }).ToArrayAsync(http.RequestAborted);
            var candidates = new List<SearchItemDto>();
            // Keep the destination/vessel boundary explicit, including pages inside either partition.
            for (var index = Math.Min(skip, destinations.Length); index < destinations.Length; index++)
                candidates.Add(destinations[index]);
            foreach (var row in rows)
                candidates.Add(new(row.Id.ToString(), $"/app/fleet/{row.Id}", row.Code + " · " + row.Name, row.Kind.ToString()));
            await audit.WriteAsync(http, "WorkspaceSearch", "Success");
            return Results.Ok(new PageDto<SearchItemDto>(candidates.GetRange(0, Math.Min(take, candidates.Count)).ToArray(), skip, take, candidates.Count > take));
        }).RequireAuthorization("DirectoryRead").WithName("SearchWorkspace").Produces<PageDto<SearchItemDto>>().Produces(400).Produces(401).Produces(403).Produces(503);
    }
}
