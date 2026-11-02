using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;
namespace Poseidon.Api;
public sealed class MaintenanceObservationSettings
{
    public MaintenanceObservationRule[] Sources { get; set; } = [];
    public MaintenanceObservationRule? Rule(Guid tenant, Guid vessel, Guid asset, string source) {
        var matches = Sources.Where(s => s.TenantId == tenant && s.VesselId == vessel && s.AssetId == asset && s.Source == source).Take(2).ToArray();
        var candidate = matches.Length == 1 ? matches[0] : null;
        return candidate != null && candidate.FreshnessSeconds > 0 && candidate.MaxScale is >= 0 and <= 28 && (candidate.Kind is "Meter" or "Condition") &&
            candidate.Source.Length is > 0 and <= 80 && candidate.Version.Length is > 0 and <= 80 && candidate.Unit.Length is > 0 and <= 40 ? candidate : null;
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenanceObservationCommand(Guid Id, string Source, string PolicyVersion, long SourceRevision, string Kind, string Unit, string? Value, [property: JsonConverter(typeof(ExplicitObservationInstantConverter))] DateTimeOffset? ObservedAt);
public static class MaintenanceObservationEndpoints
{
    public static void MapMaintenanceObservations(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("/api/v1/equipment-hierarchy/{vessel:guid}/{asset:guid}/observations").RequireAuthorization("DirectoryRead");
        group.MapGet("", List).WithName("MaintenanceObservationRead");
        group.MapPost("", Capture).WithName("MaintenanceObservationCapture");
        group.MapPost("/{id:guid}/revoke", Revoke).WithName("MaintenanceObservationRevoke");
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "MaintenanceObservation", code);
        return Results.Json(new { code }, statusCode: status);
    }
    private static async Task<bool> Allowed(Guid vessel, Guid asset, HttpContext h, PoseidonDbContext db, bool write) {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        return a.CanRead && a.Permissions.Contains("maintenance.read") && a.Permissions.Contains("equipment.read") &&
            (!write || a.HasWriteAttribution && a.Permissions.Contains("maintenance.write") && a.Permissions.Contains("maintenance.observation.write") &&
                (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit))) &&
            await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted) && await db.Assets.AnyAsync(x => x.Id == asset && x.VesselId == vessel, h.RequestAborted);
    }
    private static async Task<IResult> List(Guid vessel, Guid asset, HttpContext h, PoseidonDbContext db, MaintenanceObservationSettings settings) {
        if (!await Allowed(vessel, asset, h, db, false)) return await Fail(h, 404, "resource_not_found");
        var sources = settings.Sources.Where(s => s.TenantId == db.TenantId && s.VesselId == vessel && s.AssetId == asset)
            .Where(s => settings.Rule(db.TenantId, vessel, asset, s.Source) != null)
            .GroupBy(s => s.Source, StringComparer.Ordinal).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
        var rows = await db.MaintenanceObservations.AsNoTracking().Where(x => x.VesselId == vessel && x.AssetId == asset)
            .OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Id).Take(100).Select(x => new { x.Id, x.Source, x.PolicyVersion, x.SourceRevision, x.Kind, x.Unit, x.Value, x.ObservedAt, x.Provenance,
                revoked = db.MaintenanceObservationRevocations.Any(r => r.ObservationId == x.Id) }).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "MaintenanceObservationRead", "Success", resourceId: asset);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows) {
            var rule = settings.Rule(db.TenantId, vessel, asset, row.Source);
            var age = rule != null && rule.Version == row.PolicyVersion && !row.revoked ? TimeSpan.FromSeconds(rule.FreshnessSeconds) : (TimeSpan?)null;
            OperationalTelemetry.ObserveFreshness(ObservationArea.Technical, SourceFreshnessPolicy.Assess(row.ObservedAt, now, age));
        }
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        return Results.Ok(new { sources, items = rows, canWrite = actor.Permissions.Contains("maintenance.observation.write") && actor.Permissions.Contains("maintenance.write") && actor.HasWriteAttribution &&
            (actor.TenantWideManage || actor.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit)), state = sources.Length == 0 ? "PolicyUnavailable" : "ConfiguredDeclarationsOnly" });
    }
    private static async Task<IResult> Capture(Guid vessel, Guid asset, MaintenanceObservationCommand command, HttpContext h, PoseidonDbContext db, MaintenanceObservationSettings settings) {
        if (!await Allowed(vessel, asset, h, db, true)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        var rule = settings.Rule(db.TenantId, vessel, asset, command.Source);
        if (rule == null || rule.Version != command.PolicyVersion || rule.Kind != command.Kind || rule.Unit != command.Unit || rule.FreshnessSeconds <= 0 || rule.MaxScale is null or < 0 or > 28)
            return await Fail(h, 403, "observation_policy_denied");
        if (command.Id == Guid.Empty || command.SourceRevision <= 0 || command.ObservedAt > DateTimeOffset.UtcNow || command.Source.Length is 0 or > 80 ||
            command.PolicyVersion.Length is 0 or > 80 || command.Unit.Length is 0 or > 40 || command.Kind is not ("Meter" or "Condition") ||
            command.Kind == "Meter" && !CertificateMetadataPolicy.ExactNumeric(command.Value) || command.Kind == "Condition" && command.Value is not (null or "True" or "False")) return await Fail(h, 400, "validation_failed");
        var old = await db.MaintenanceObservations.SingleOrDefaultAsync(x => x.Id == command.Id, h.RequestAborted);
        if (old != null) {
            if (old.VesselId != vessel || old.AssetId != asset || old.Source != command.Source || old.PolicyVersion != command.PolicyVersion || old.SourceRevision != command.SourceRevision ||
                old.Kind != command.Kind || old.Unit != command.Unit || old.Value != command.Value || old.ObservedAt != command.ObservedAt) return await Fail(h, 409, "command_reuse_conflict");
            return Results.Ok(new { old.Id, replayed = true });
        }
        var row = new MaintenanceObservation { TenantId = db.TenantId, Id = command.Id, VesselId = vessel, AssetId = asset, Source = command.Source, PolicyVersion = command.PolicyVersion,
            SourceRevision = command.SourceRevision, Kind = command.Kind, Unit = command.Unit, Value = command.Value, ObservedAt = command.ObservedAt?.ToUniversalTime(), RecordedAt = DateTimeOffset.UtcNow };
        db.MaintenanceObservations.Add(row);
        try { await db.SaveChangesAsync(h.RequestAborted); }
        catch (DbUpdateException) { return await Fail(h, 409, "version_conflict"); }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "MaintenanceObservationCapture", "Success", resourceId: asset);
        return Results.Ok(new { row.Id, replayed = false });
    }
    private static async Task<IResult> Revoke(Guid vessel, Guid asset, Guid id, HttpContext h, PoseidonDbContext db) {
        if (!await Allowed(vessel, asset, h, db, true) || !await db.MaintenanceObservations.AnyAsync(x => x.Id == id && x.VesselId == vessel && x.AssetId == asset, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        if (!await db.MaintenanceObservationRevocations.AnyAsync(x => x.ObservationId == id, h.RequestAborted)) {
            db.MaintenanceObservationRevocations.Add(new() { TenantId = db.TenantId, ObservationId = id, RecordedAt = DateTimeOffset.UtcNow });
            try { await db.SaveChangesAsync(h.RequestAborted); } catch (DbUpdateException) { return await Fail(h, 409, "version_conflict"); }
        }
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "MaintenanceObservationRevoke", "Success", resourceId: asset); return Results.Ok(new { id, state = "Revoked" });
    }
    internal static async Task<MaintenanceObservationAssessment> Resolve(Guid vessel, Guid asset, MaintenanceInputs inputs, HttpContext h, PoseidonDbContext db, DateTimeOffset? serverNow = null) {
        var row = await db.MaintenanceObservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == inputs.ObservationId && x.VesselId == vessel && x.AssetId == asset, h.RequestAborted);
        var rule = row == null ? null : h.RequestServices.GetRequiredService<MaintenanceObservationSettings>().Rule(db.TenantId, vessel, asset, row.Source);
        var observation = row == null ? null : new MaintenanceObservationValue(row.Id, row.TenantId, row.VesselId, row.AssetId, row.Source, row.PolicyVersion, row.SourceRevision,
            row.Kind, row.Unit, row.Value, row.ObservedAt, await db.MaintenanceObservationRevocations.AnyAsync(x => x.ObservationId == row.Id, h.RequestAborted));
        return MaintenanceObservationPolicy.Assess(observation, rule, db.TenantId, vessel, asset, inputs, serverNow ?? DateTimeOffset.UtcNow);
    }
}
