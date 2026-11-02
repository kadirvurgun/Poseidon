using Poseidon.Domain;
using Poseidon.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Poseidon.Api;
public sealed class CrewAssignmentPolicy
{
    public string Fingerprint() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this)));
    public DateOnly? ReferenceDate(DateTimeOffset now) {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(TimeZone)).DateTime); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return null; }
    }
    public string Version { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string[] EffectiveStatuses { get; set; } = [];
    public string[] PlannedStatuses { get; set; } = [];
    public string[] Provenances { get; set; } = [];
    public bool RequireSourceObservation { get; set; } = true;
    public bool Configured => Version.Length is > 0 and <= 80 && ReferenceDate(DateTimeOffset.UtcNow) != null && EffectiveStatuses.Length > 0 && Provenances.Length > 0 &&
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
        h.Response.Headers.CacheControl = "no-store";
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        if (!a.CanRead || !a.Permissions.Contains("crew.read") || !await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCurrentAssignmentRead", "permission_denied");
            return Results.NotFound();
        }
        var owner = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Id == assignment, h.RequestAborted);
        if (owner == null) {
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCurrentAssignmentRead", "resource_not_found");
            return Results.NotFound();
        }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "CrewCurrentAssignmentRead", "Success", resourceId: owner.PersonId);
        if (!policy.Configured) return Results.Ok(new { state = "PolicyUnavailable", policyVersion = (string?)null });
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { return Results.Ok(new { state = "PolicyUnavailable" }); }
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var visible = a.Scope(db.Vessels).Select(x => x.Id);
        var fingerprint = policy.Fingerprint();
        var rows = await db.CrewAssignments.AsNoTracking().Where(x => x.PersonId == owner.PersonId && visible.Contains(x.VesselId) &&
            x.AssignmentPolicyVersion == policy.Version && x.AssignmentPolicyFingerprint == fingerprint)
            .Select(x => new CrewAssignmentObservation(x.Id, x.VesselId, x.Status, x.Provenance, x.From, x.To, x.SourceObservedAt)).ToArrayAsync(h.RequestAborted);
        var selection = CrewEffectiveAssignment.Select(rows, date, now, policy.EffectiveStatuses, policy.Provenances, policy.RequireSourceObservation);
        if (selection.State == "Missing" && await db.CrewAssignments.AsNoTracking().AnyAsync(x => x.PersonId == owner.PersonId && visible.Contains(x.VesselId) &&
            x.Status != "Draft" && (x.AssignmentPolicyVersion == null || x.AssignmentPolicyVersion != policy.Version || x.AssignmentPolicyFingerprint == null || x.AssignmentPolicyFingerprint != fingerprint), h.RequestAborted))
            selection = new("PolicyStale", null, null);
        var position = selection.AssignmentId == null ? null : await (from c in db.CrewAssignments.AsNoTracking().Where(x => x.Id == selection.AssignmentId)
            join p in db.CrewPositions.AsNoTracking() on new { c.TenantId, Id = c.PositionId, c.VesselId } equals new { p.TenantId, Id = (Guid?)p.Id, p.VesselId }
            select p).SingleOrDefaultAsync(h.RequestAborted);
        if (position != null) {
            var declaration = await db.CrewAssignments.AsNoTracking().SingleAsync(x => x.Id == selection.AssignmentId, h.RequestAborted);
            var lookup = await CrewPositionPolicy.Current(h, db, vessel);
            if (!lookup.Allows(position) || declaration.PositionPolicyVersion != lookup.Version || declaration.PositionPolicyFingerprint != lookup.Fingerprint()) position = null;
        }
        return Results.Ok(new { selection.State, selection.AssignmentId, selection.VesselId, position = position == null ? null : new { position.Id, position.Rank, position.Capacity, position.Department, position.Provenance }, policyVersion = policy.Version, referenceDate = date, timeZone = zone.Id,
            authority = "UnverifiedDeclaration;NoQualificationOrSafetyDecision" });
    }
}
