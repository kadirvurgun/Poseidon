using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class CrewPositionRule
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PositionId { get; set; }
    public string Code { get; set; } = "";
    public string? Rank { get; set; }
    public string? Capacity { get; set; }
    public string? Department { get; set; }
    public string Provenance { get; set; } = "";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool Effective(DateOnly date) => (From == null || From <= date) && (To == null || date < To);
    public bool Matches(CrewPosition p) => TenantId == p.TenantId && VesselId == p.VesselId && PositionId == p.Id &&
        Code == p.Code && Rank == p.Rank && Capacity == p.Capacity && Department == p.Department && Provenance == p.Provenance;
}

// Explicit scoped versioned tuple contracts, not independent guessed rank/department lists.
// Missing classifications stay null in permitted incomplete position declarations.
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class CrewPositionPolicy
{
    public string Fingerprint() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this)));
    public string Version { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string PeriodSemantics { get; set; } = "";
    public CrewPositionRule[] Positions { get; set; } = [];
    public bool Configured => Version?.Length is > 0 and <= 80 && SourceReference?.Length is > 0 and <= 512 && Positions != null &&
        PeriodSemantics == "StartInclusiveEndExclusive" && ReferenceDate() != null &&
        Positions.All(p => p != null && p.TenantId != Guid.Empty && p.VesselId != Guid.Empty && p.PositionId != Guid.Empty &&
            p.Code?.Length is > 0 and <= 64 && p.Provenance?.Length is > 0 and <= 32 &&
            (p.Rank == null || p.Rank.Length <= 100) && (p.Capacity == null || p.Capacity.Length <= 100) &&
            (p.Department == null || p.Department.Length <= 100) && (p.To == null || p.From != null && p.To > p.From)) &&
        Positions.Select(p => (p.TenantId, p.VesselId, p.PositionId)).Distinct().Count() == Positions.Length;
    public static Task<CrewPositionPolicy> Current(HttpContext h, PoseidonDbContext db, Guid vessel) => CrewCatalogEndpoints.Resolve(db,
        h.RequestServices.GetRequiredService<FederationSettings>(), h.RequestServices.GetRequiredService<CrewPositionPolicy>(), vessel,
        h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), h.RequestAborted);
    public DateOnly? ReferenceDate() {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return null; }
    }
    public bool Allows(CrewPosition p, DateOnly? date = null) => Configured && Positions.Any(r => r.Matches(p) && r.Effective(date ?? ReferenceDate()!.Value));
    public bool AllowsReference(Guid tenant, Guid vessel, Guid? position, string? version, IEnumerable<CrewPosition> stored) => position == null ||
        Configured && version == Version && stored.Any(p => p.TenantId == tenant && p.VesselId == vessel && p.Id == position && Allows(p));
}

public static class CrewPositionPolicyEndpoints
{
    public static void MapCrewPositionPolicy(this IEndpointRouteBuilder app) => app.MapGet("/api/v1/vessels/{vessel:guid}/crew/position-policy", Read).RequireAuthorization("DirectoryRead").WithName("CrewPositionPolicy");
    private static async Task<IResult> Read(Guid vessel, HttpContext h, PoseidonDbContext db, CrewPositionPolicy policy) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!a.CanRead || !a.Permissions.Contains("crew.read")) return Results.Forbid();
        if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) return Results.NotFound();
        policy = await CrewPositionPolicy.Current(h, db, vessel);
        var rows = await db.CrewPositions.AsNoTracking().Where(x => x.VesselId == vessel).ToArrayAsync(h.RequestAborted);
        var options = rows.Where(x => policy.Allows(x)).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Rank, x.Capacity, x.Department, x.Provenance }).ToArray();
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewPositions", "Success", resourceId: vessel);
        return Results.Ok(new { state = policy.Configured ? "ConfiguredUnverified" : "Unavailable", version = policy.Configured ? policy.Version : null,
            referenceDate = policy.Configured ? policy.ReferenceDate() : null, timeZone = policy.Configured ? policy.TimeZone : null, items = options });
    }
}
