using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;
using Poseidon.Domain;

namespace Poseidon.Api;

// Actual operator provision, scoped to source, vessel, human and local role. No admin fallback.
public sealed record CrewCatalogAuthority(Guid Id, Guid VesselId, Guid RoleId, string SourceReference,
    string[] Actions, DateTimeOffset From, DateTimeOffset Until);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewCatalogCommand([property: JsonRequired] uint ExpectedVersion, Guid AuthorityId,
    string State, string Reason, CrewPositionPolicy? Content = null);

public static class CrewCatalogEndpoints
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 8 };
    private static string Hash(object value) => RegisterStorage.Hash(JsonSerializer.Serialize(value, Json));
    private static string GrantHash(Membership m, CrewCatalogAuthority g) => Hash(new { m.ActorId, m.TenantId, m.Issuer, m.Subject, m.ActorKind, g });
    private static CrewCatalogAuthority? Grant(FederationSettings settings, TrustedActor a, Guid vessel, string action, DateTimeOffset now)
    {
        if (!a.CanRead || !a.HasWriteAttribution || !a.Permissions.Contains("crew.catalog.govern") || !a.Permissions.Contains("crew.read") ||
            !a.Permissions.Contains("crew.write") || !(a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit))) return null;
        var memberships = settings.Memberships.Where(m => m.TenantId == a.TenantId && m.ActorId == a.Id).ToArray();
        if (memberships.Length != 1 || memberships[0].ActorKind != "Human") return null;
        var member = memberships[0];
        if (member.Issuer != settings.Issuer || string.IsNullOrWhiteSpace(member.Subject) ||
            settings.Memberships.Count(m => m.Issuer == member.Issuer && m.Subject == member.Subject) != 1) return null;
        var grants = memberships[0].CrewCatalogAuthorities;
        var candidates = grants.Where(g => g.Id != Guid.Empty && grants.Count(x => x.Id == g.Id) == 1 && g.VesselId == vessel &&
            g.RoleId != Guid.Empty && a.RoleContext!.Split(',').Contains(g.RoleId.ToString("D")) && g.Actions != null && g.Actions.Contains(action) &&
            !string.IsNullOrWhiteSpace(g.SourceReference) && g.From <= now && now < g.Until).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static bool CurrentAuthority(FederationSettings settings, CrewCatalogEvent e, DateTimeOffset now, VesselKind kind)
    {
        var memberships = settings.Memberships.Where(m => m.TenantId == e.TenantId && m.ActorId == e.ActorId).ToArray();
        if (memberships.Length != 1) return false;
        var membership = memberships[0];
        var a = new TrustedActor(membership.ActorId, membership.TenantId, membership.Permissions, membership.TenantWideRead, membership.TenantWideManage, membership.VesselGrants ?? []) {
            RoleContext = ActorResolver.TrustedRoleContext(membership.RoleReferences)
        };
        if (!a.CanAccessKind(kind)) return false;
        var action = e.State switch { "Draft" => "draft", "Reviewed" => "review", "Adopted" => "adopt", "Withdrawn" => "withdraw", _ => "" };
        var grant = Grant(settings, a, e.VesselId, action, now);
        return grant?.Id == e.AuthorityId && grant.RoleId == e.RoleId && GrantHash(membership, grant) == e.AuthorityHash;
    }
    internal static async Task<CrewPositionPolicy> Resolve(PoseidonDbContext db, FederationSettings settings, CrewPositionPolicy fallback,
        Guid vessel, DateTimeOffset now, CancellationToken ct)
    {
        var head = await db.CrewCatalogHeads.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel, ct);
        if (head == null) return fallback; // Existing explicitly enrolled configured catalogs remain supported.
        if (head.ActiveRevision == null) return await db.CrewCatalogEvents.AnyAsync(x => x.VesselId == vessel && (x.State == "Adopted" || x.State == "Withdrawn"), ct)
            ? new() : fallback; // A proposal does not replace the active config; withdrawal never resurrects it.
        var row = await db.CrewCatalogEvents.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Revision == head.ActiveRevision, ct);
        var kind = await db.Vessels.Where(v => v.Id == vessel).Select(v => v.Kind).SingleAsync(ct);
        var chain = await db.CrewCatalogEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.Revision >= row.Revision - 2 && x.Revision <= row.Revision)
            .OrderBy(x => x.Revision).ToArrayAsync(ct);
        if (row.State != "Adopted" || chain.Length != 3 || chain[0].State != "Draft" || chain[1].State != "Reviewed" ||
            chain[0].ActorId == chain[1].ActorId || chain[0].ActorId == row.ActorId ||
            chain[1].PriorHash != chain[0].Hash || row.PriorHash != chain[1].Hash || chain.Any(e => !CurrentAuthority(settings, e, now, kind))) return new();
        var policy = JsonSerializer.Deserialize<CrewPositionPolicy>(row.ContentJson, Json)!;
        return policy.Configured ? policy : new();
    }
    public static void MapCrewCatalog(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/vessels/{vessel:guid}/crew/position-catalog").RequireAuthorization("DirectoryRead");
        g.MapGet("", Read).WithName("CrewCatalogRead");
        g.MapPost("/events", Save).Accepts<CrewCatalogCommand>("application/json").WithName("CrewCatalogSave");
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCatalog", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<IResult> Read(Guid vessel, HttpContext h, PoseidonDbContext db, FederationSettings settings) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!a.Permissions.Contains("crew.read") || !a.Permissions.Contains("crew.history.read")) return await Fail(h, 403, "permission_denied");
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var before = long.MaxValue;
        if (h.Request.Query.Any(q => q.Key != "before" || q.Value.Count != 1) || h.Request.Query.ContainsKey("before") &&
            (!long.TryParse(h.Request.Query["before"], out before) || before <= 0)) return await Fail(h, 400, "invalid_query");
        var head = await db.CrewCatalogHeads.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel, h.RequestAborted);
        var rows = await db.CrewCatalogEvents.AsNoTracking().Where(x => x.VesselId == vessel && x.Revision < before).OrderByDescending(x => x.Revision).Take(26).ToArrayAsync(h.RequestAborted);
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCatalogRead", "Success", resourceId: vessel);
        h.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { version = head?.Version ?? 0, activeRevision = head?.ActiveRevision, hasMore = rows.Length > 25,
            items = rows.Take(25).Select(e => new { e.Revision, e.State, e.PolicyVersion, e.ActorId, e.RoleId, e.Reason, e.SavedAt,
                content = JsonSerializer.Deserialize<CrewPositionPolicy>(e.ContentJson, Json), e.Hash, e.PriorHash }),
            actions = new[] { "draft", "review", "adopt", "withdraw" }.Select(action => new { action, authorityId = Grant(settings, a, vessel, action, now)?.Id }).Where(x => x.authorityId != null) });
    }
    private static bool Unique(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().All(Unique) :
        value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count() &&
            value.EnumerateObject().All(p => Unique(p.Value));
    private static async Task<IResult> Save(Guid vessel, HttpContext h, PoseidonDbContext db, FederationSettings settings, IAntiforgery csrf) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!; var ct = h.RequestAborted;
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (!new[] { "draft", "review", "adopt", "withdraw" }.Any(candidateAction => Grant(settings, a, vessel, candidateAction, now) != null))
            return await Fail(h, 403, "human_catalog_authority_required");
        var vesselRow = await a.Scope(db.Vessels).AsNoTracking().SingleOrDefaultAsync(x => x.Id == vessel, ct);
        if (vesselRow == null) return await Fail(h, 404, "resource_not_found");
        try { await csrf.ValidateRequestAsync(h); } catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        CrewCatalogCommand command;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var bytes = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, bytes, 131072, ct);
            using var document = JsonDocument.Parse(bytes.ToArray(), new() { MaxDepth = 8 });
            if (!Unique(document.RootElement)) throw new JsonException();
            command = document.RootElement.Deserialize<CrewCatalogCommand>(Json) ?? throw new JsonException();
        } catch (Exception parseFailure) when (parseFailure is JsonException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var action = command.State switch { "Draft" => "draft", "Reviewed" => "review", "Adopted" => "adopt", "Withdrawn" => "withdraw", _ => "" };
        var grant = Grant(settings, a, vessel, action, now);
        if (grant == null || grant.Id != command.AuthorityId) return await Fail(h, 403, "human_catalog_authority_required");
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out _) || h.Request.Query.Count != 0 ||
            string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 1000) return await Fail(h, 400, "invalid_request");
        var fingerprint = Hash(new { vessel, command }); var key = keys[0]!;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
        if (receipt != null) {
            if (receipt.Action != "CrewCatalogSave" || receipt.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
            var recorded = await db.CrewCatalogEvents.AsNoTracking().SingleAsync(e => e.VesselId == vessel && e.Revision == receipt.RegisterRevision, ct);
            if (!CurrentAuthority(settings, recorded, now, vesselRow.Kind)) return await Fail(h, 403, "catalog_authority_changed");
            return Results.Ok(new { revision = receipt.RegisterRevision, version = receipt.RegisterVersion, replayed = true });
        }
        var head = await db.CrewCatalogHeads.SingleOrDefaultAsync(x => x.VesselId == vessel, ct);
        if (command.ExpectedVersion != (head?.Version ?? 0)) return await Fail(h, 409, "version_conflict");
        var prior = head == null ? null : await db.CrewCatalogEvents.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Revision == head.Revision, ct);
        var active = command.State == "Withdrawn" && head?.ActiveRevision != null
            ? await db.CrewCatalogEvents.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Revision == head.ActiveRevision, ct) : null;
        var policy = command.State == "Draft" ? command.Content : (active ?? prior) is { } basis ? JsonSerializer.Deserialize<CrewPositionPolicy>(basis.ContentJson, Json) : null;
        if (command.State != "Draft" && command.Content != null || policy == null || policy.Version == null || policy.Version.Length > 80 ||
            policy.SourceReference != grant.SourceReference || policy.Positions == null || policy.Positions.Length > 200 ||
            policy.TimeZone == null || policy.TimeZone.Length > 128 || policy.PeriodSemantics == null || policy.PeriodSemantics.Length > 80 ||
            policy.Positions.Any(p => p == null || p.TenantId != a.TenantId || p.VesselId != vessel || p.PositionId == Guid.Empty ||
                p.Code == null || p.Code.Length > 64 || p.Provenance == null || p.Provenance.Length > 32 || p.Rank?.Length > 100 ||
                p.Capacity?.Length > 100 || p.Department?.Length > 100) || policy.Positions.Select(p => p.PositionId).Distinct().Count() != policy.Positions.Length)
            return await Fail(h, 400, "invalid_catalog");
        var rejected = CrewCatalogGovernance.Reject(prior?.State, command.State, policy.Configured && policy.Positions.Length > 0,
            command.State != "Reviewed" || prior?.ActorId != a.Id, head?.ActiveRevision != null);
        if (rejected != null) return await Fail(h, 409, rejected);
        if (command.State == "Reviewed" && !CurrentAuthority(settings, prior!, now, vesselRow.Kind)) return await Fail(h, 403, "catalog_authority_changed");
        if (command.State == "Adopted") {
            // Adoption may be by the independent reviewer, never the proposal's author.
            var draft = await db.CrewCatalogEvents.AsNoTracking().SingleAsync(x => x.VesselId == vessel && x.Revision == prior!.Revision - 1, ct);
            if (draft.ActorId == a.Id) return await Fail(h, 403, "independent_catalog_authority_required");
            if (!CurrentAuthority(settings, draft, now, vesselRow.Kind) || !CurrentAuthority(settings, prior!, now, vesselRow.Kind)) return await Fail(h, 403, "catalog_authority_changed");
            if (await db.CrewCatalogEvents.AnyAsync(x => x.VesselId == vessel && x.State == "Adopted" && x.PolicyVersion == policy.Version, ct)) return await Fail(h, 409, "catalog_version_reused");
            foreach (var p in policy.Positions) {
                var stored = await db.CrewPositions.SingleOrDefaultAsync(x => x.Id == p.PositionId, ct);
                if (stored != null && !p.Matches(stored)) return await Fail(h, 409, "immutable_position_conflict");
                if (stored == null) db.CrewPositions.Add(new() { TenantId = a.TenantId, VesselId = vessel, Id = p.PositionId, Code = p.Code,
                    Rank = p.Rank, Capacity = p.Capacity, Department = p.Department, Provenance = p.Provenance });
            }
        }
        if (head == null) { head = new() { TenantId = a.TenantId, VesselId = vessel }; db.CrewCatalogHeads.Add(head); }
        head.Revision = checked(head.Revision + 1);
        if (command.State == "Adopted") head.ActiveRevision = head.Revision;
        if (command.State == "Withdrawn") head.ActiveRevision = null;
        var membership = settings.Memberships.Single(candidate => candidate.TenantId == a.TenantId && candidate.ActorId == a.Id);
        var content = JsonSerializer.Serialize(policy, Json);
        var catalogEvent = new CrewCatalogEvent { TenantId = a.TenantId, VesselId = vessel, Revision = head.Revision, State = command.State,
            ContentJson = content, PolicyVersion = policy.Version, PriorHash = prior?.Hash, Hash = Hash(new { vessel, head.Revision, command.State, content, prior = prior?.Hash, actorId = a.Id, authorityId = grant.Id }),
            ActorId = a.Id, RoleId = grant.RoleId, AuthorityId = grant.Id, AuthorityHash = GrantHash(membership, grant), Reason = command.Reason, SavedAt = now };
        db.CrewCatalogEvents.Add(catalogEvent);
        try {
            await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "CrewCatalogSave", Fingerprint = fingerprint,
                ResourceId = vessel, RegisterRevision = head.Revision, RegisterVersion = head.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.Ok(new { revision = head.Revision, version = head.Version, replayed = false });
        } catch (DbUpdateException) { await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict"); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict"); }
    }
}
