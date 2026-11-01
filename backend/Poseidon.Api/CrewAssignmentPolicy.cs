using Poseidon.Domain;
using Poseidon.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Poseidon.Api;
public sealed class CrewAssignmentPolicy
{
    public string Version { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string[] EffectiveStatuses { get; set; } = [];
    public string[] PlannedStatuses { get; set; } = [];
    public string[] Provenances { get; set; } = [];
    public bool RequireSourceObservation { get; set; } = true;
    public bool Configured => Version.Length is > 0 and <= 80 && TimeZone.Length > 0 && EffectiveStatuses.Length > 0 && Provenances.Length > 0 &&
        EffectiveStatuses.Concat(PlannedStatuses).All(x => x.Length is > 0 and <= 32 && x != "Draft") &&
        EffectiveStatuses.Intersect(PlannedStatuses, StringComparer.Ordinal).Any() == false;
    public bool CanDeclare(string? status, string? version, TrustedActor a) => status == null || Configured && version == Version &&
        a.Permissions.Contains("crew.assignment.declare") && (status == "Draft" || EffectiveStatuses.Concat(PlannedStatuses).Contains(status, StringComparer.Ordinal));
}
public static class CrewAssignmentSelectionEndpoints
{
    public static void MapCrewSelection(this IEndpointRouteBuilder app) => app.MapGet("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/current-assignment", Read)
        .RequireAuthorization("DirectoryRead").WithName("CrewEffectiveAssignment");
    private static async Task<IResult> Read(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db, CrewAssignmentPolicy policy) {
        var owner = await CrewProfileEndpoints.Owner(vessel, assignment, h, db, false);
        if (owner == null) return Results.NotFound();
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCurrentAssignmentRead", "Success", resourceId: owner.PersonId);
        if (!policy.Configured) return Results.Ok(new { state = "PolicyUnavailable", policyVersion = (string?)null });
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return Results.Ok(new { state = "PolicyUnavailable" }); }
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!; var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var visible = a.Scope(db.Vessels).Select(x => x.Id);
        var rows = await db.CrewAssignments.AsNoTracking().Where(x => x.PersonId == owner.PersonId && visible.Contains(x.VesselId))
            .Select(x => new CrewAssignmentObservation(x.Id, x.VesselId, x.Status, x.Provenance, x.From, x.To, x.SourceObservedAt)).ToArrayAsync(h.RequestAborted);
        var selection = CrewEffectiveAssignment.Select(rows, date, now, policy.EffectiveStatuses, policy.Provenances, policy.RequireSourceObservation);
        var position = selection.AssignmentId == null ? null : await (from c in db.CrewAssignments.AsNoTracking().Where(x => x.Id == selection.AssignmentId)
            join p in db.CrewPositions.AsNoTracking() on new { c.TenantId, Id = c.PositionId, c.VesselId } equals new { p.TenantId, Id = (Guid?)p.Id, p.VesselId }
            select new { p.Id, p.Rank, p.Capacity, p.Department, p.Provenance }).SingleOrDefaultAsync(h.RequestAborted);
        return Results.Ok(new { selection.State, selection.AssignmentId, selection.VesselId, position, policyVersion = policy.Version, referenceDate = date, timeZone = zone.Id,
            authority = "UnverifiedDeclaration;NoQualificationOrSafetyDecision" });
    }
}
