using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] DrydockContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockUpdateCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] DrydockContent Content);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockTransitionCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash, [property: JsonRequired] string Action, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrydockVerifyCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ContentHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason);

public sealed record DrydockPackageDto(Guid Id, Guid VesselId, long Revision, uint Version, string State, DrydockContent Content, string Hash, DateTimeOffset? ActualStart,
    DateTimeOffset? ActualFinish, Guid? CompletedBy, Guid? VerifiedBy, DateTimeOffset? VerifiedAt, string Provenance, DateTimeOffset SavedAt);
public sealed record DrydockTimelineDto(long Revision, string State, string ReasonCode, string? Note, Guid ActorId, DateTimeOffset SavedAt);
public sealed record DrydockSummaryDto(Guid Id, long Revision, string State, string? ProjectReference, string? Yard, DateOnly? PlannedStart, DateOnly? PlannedFinish, int Items);
public sealed record DrydockListView(Guid VesselId, DrydockSummaryDto[] Items, bool CanWrite, string[] Categories, string[] Priorities, string[] LinkKinds,
    string SourceVersion = DrydockPolicy.SourceVersion);
public sealed record DrydockView(DrydockPackageDto Package, DrydockReadiness Readiness, DrydockTimelineDto[] Timeline, string[] PlanBlockers, string[] CompleteBlockers,
    string[] AllowedActions, bool CanEdit, bool CanVerify, string[] UnreadableLinkKinds, string External = "SupplierBudgetExternalReferenceOnly;NoProcurementOrApproval",
    string SourceVersion = DrydockPolicy.SourceVersion);

// DEV-111 /api/v1/drydock-packages. A drydock package plans the drydock scope against existing owning records
// (work orders, defects, certificates and pre-survey checklists) through exact revision pins; it never changes
// those records. Readiness is linkage completeness (unlinked/overdue/stale/unavailable), not a survey result.
// Completion needs evidence references; verification is a different person's human record.
public static class DrydockEndpoints
{
    private const string Read = "DrydockRead", Command = "DrydockCommand";
    internal static bool CanRead(TrustedActor? a) => a?.CanRead == true && a.Permissions.Contains("drydock.read");
    internal static bool CanWrite(TrustedActor a, Guid vessel) => CanRead(a) && a.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains("drydock.write");
    internal static bool CanVerify(TrustedActor a) => CanRead(a) && a.HasWriteAttribution && a.Permissions.Contains("drydock.verify");
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db, TrustedActor a) => a.Scope(db.Vessels);

    public static void MapDrydock(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/drydock-packages").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("DrydockList").Produces<DrydockListView>();
        g.MapGet("/{id:guid}", Detail).WithName("DrydockRead").Produces<DrydockView>();
        g.MapPost("", (HttpContext h, PoseidonDbContext db) => Run<DrydockCreateCommand>(h, db, "DrydockCreate", null, CanWrite, c => c.Id, Create))
            .WithName("DrydockCreate").Accepts<DrydockCreateCommand>("application/json").Produces<DrydockView>();
        g.MapPut("/{id:guid}", (Guid id, HttpContext h, PoseidonDbContext db) => Run<DrydockUpdateCommand>(h, db, "DrydockUpdate", id, CanWrite, _ => id, Update))
            .WithName("DrydockUpdate").Accepts<DrydockUpdateCommand>("application/json").Produces<DrydockView>();
        g.MapPost("/{id:guid}/transitions", (Guid id, HttpContext h, PoseidonDbContext db) => Run<DrydockTransitionCommand>(h, db, "DrydockTransition", id, CanWrite, _ => id, Transition))
            .WithName("DrydockTransition").Accepts<DrydockTransitionCommand>("application/json").Produces<DrydockView>().Produces<RiskFailure>(422);
        g.MapPost("/{id:guid}/verification", (Guid id, HttpContext h, PoseidonDbContext db) => Run<DrydockVerifyCommand>(h, db, "DrydockVerify", id, (a, _) => CanVerify(a), _ => id, Verify))
            .WithName("DrydockVerify").Accepts<DrydockVerifyCommand>("application/json").Produces<DrydockView>();
    }

    private static Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, string operation, Guid? id, Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resource,
        Func<TowageContext<T>, Task<TowageOutcome>> apply) where T : class =>
        FieldOpsCommands.Run(h, db, operation, Command, id, a => CanRead(a), Vessels, allowed, resource, apply, ReadView);

    private static DrydockContent Content(string json) => JsonSerializer.Deserialize<DrydockContent>(json, FieldOpsJson.Options) ?? throw new InvalidDataException("drydock_unavailable");
    private static string Json(DrydockContent c) => JsonSerializer.Serialize(c, FieldOpsJson.Options);
    private static DrydockPackageDto Dto(DrydockPackage p) => new(p.Id, p.VesselId, p.Revision, p.Version, p.State, Content(p.ContentJson), p.Hash, p.ActualStart, p.ActualFinish,
        p.CompletedBy, p.VerifiedBy, p.VerifiedAt, p.Provenance, p.SavedAt);
    private static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    private static string? Known(string fieldsJson, string key) {
        using var doc = JsonDocument.Parse(fieldsJson);
        foreach (var p in doc.RootElement.EnumerateObject().Where(p => p.Name == key && p.Value.ValueKind == JsonValueKind.Object)) {
            string? Get(string name) => p.Value.EnumerateObject().Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) && x.Value.ValueKind == JsonValueKind.String)
                .Select(x => x.Value.GetString()).FirstOrDefault();
            if (Get("state") == "Known") return Get("value");
        }
        return null;
    }
    private static DateOnly? Date(string? text) => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    // Current owning facts for the linked targets, read through each owner's own read gate. A target the reader
    // cannot see is simply absent (UNAVAILABLE); the reader's missing permissions are listed separately.
    private static async Task<(Dictionary<(string, Guid), DrydockTargetFact> Facts, string[] Unreadable)> Targets(PoseidonDbContext db, TrustedActor a, Guid vessel,
        DrydockContent c, DateOnly today, CancellationToken ct) {
        var facts = new Dictionary<(string, Guid), DrydockTargetFact>(); var unreadable = new List<string>();
        Guid[] Ids(string kind) => c.Items.SelectMany(i => i.Links).Where(l => l.Kind == kind).Select(l => l.TargetId).Distinct().ToArray();
        var wo = Ids("WORK_ORDER");
        if (wo.Length > 0) {
            if (!WorkOrderEndpoints.CanRead(a)) unreadable.Add("WORK_ORDER");
            else foreach (var w in await db.WorkOrders.AsNoTracking().Where(x => x.VesselId == vessel && wo.Contains(x.Id)).ToArrayAsync(ct))
                facts[("WORK_ORDER", w.Id)] = new("WORK_ORDER", w.Id, true, w.Revision, Date(Known(w.FieldsJson, "due_date")), Known(w.FieldsJson, "actual_finish") != null, "WorkOrderDuePassed");
        }
        var defects = Ids("DEFECT");
        if (defects.Length > 0) {
            if (!DefectEndpoints.CanRead(a)) unreadable.Add("DEFECT");
            else {
                var closed = await db.DefectLifecycles.AsNoTracking().Where(x => x.VesselId == vessel && defects.Contains(x.DefectId) && x.Stage == DefectLifecyclePolicy.Closed)
                    .Select(x => x.DefectId).ToArrayAsync(ct);
                foreach (var d in await db.Defects.AsNoTracking().Where(x => x.VesselId == vessel && defects.Contains(x.Id)).Select(x => new { x.Id, x.Revision }).ToArrayAsync(ct))
                    facts[("DEFECT", d.Id)] = new("DEFECT", d.Id, true, d.Revision, null, closed.Contains(d.Id));
            }
        }
        var certificates = Ids("CERTIFICATE");
        if (certificates.Length > 0) {
            if (!CertificateReader.Read(a)) unreadable.Add("CERTIFICATE");
            else foreach (var r in await CertificateReader.Visible(db, a, vessel).Where(x => certificates.Contains(x.CertificateId)).ToArrayAsync(ct)) {
                var window = SurveyPlannerPolicy.Window(today, r.SurveyWindowDeclared, r.SurveyWindowStart, r.SurveyWindowEnd);
                facts[("CERTIFICATE", r.CertificateId)] = window.State == "OVERDUE"
                    ? new("CERTIFICATE", r.CertificateId, true, r.Revision, r.SurveyWindowEnd, false, "SurveyWindowPassed")
                    : new("CERTIFICATE", r.CertificateId, true, r.Revision, r.ExpiryDate, false, "CertificateExpired");
            }
        }
        var checklists = Ids("SURVEY_CHECKLIST");
        if (checklists.Length > 0) {
            if (!PreSurveyChecklistEndpoints.Read(a)) unreadable.Add("SURVEY_CHECKLIST");
            else foreach (var r in await PreSurveyChecklistEndpoints.Visible(db, a, vessel).Where(x => checklists.Contains(x.ChecklistId)).Select(x => new { x.ChecklistId, x.Revision }).ToArrayAsync(ct))
                facts[("SURVEY_CHECKLIST", r.ChecklistId)] = new("SURVEY_CHECKLIST", r.ChecklistId, true, r.Revision, null, false);
        }
        return (facts, [.. unreadable]);
    }

    // Saving pins only current, visible targets (and an equipment link only to this vessel's equipment).
    private static async Task<TowageOutcome?> ValidLinks(PoseidonDbContext db, TrustedActor a, Guid vessel, DrydockContent c, DateTimeOffset now, CancellationToken ct) {
        if (!DrydockPolicy.ValidDraft(c)) return TowageOutcome.Fail(400, "validation_failed");
        var (facts, unreadable) = await Targets(db, a, vessel, c, Today(now), ct);
        foreach (var l in c.Items.SelectMany(i => i.Links)) {
            if (unreadable.Contains(l.Kind) || !facts.TryGetValue((l.Kind, l.TargetId), out var f)) return TowageOutcome.Fail(422, "linked_resource_unavailable");
            if (f.CurrentRevision != l.PinnedRevision) return TowageOutcome.Fail(409, "version_conflict");
        }
        var equipment = c.Items.Where(i => i.EquipmentId != null).Select(i => i.EquipmentId!.Value).Distinct().ToArray();
        // QA S9 N9: the existence check is itself an equipment read; without equipment.read the link is unavailable.
        if (equipment.Length > 0 && (!a.Permissions.Contains("equipment.read") ||
            await db.Assets.CountAsync(e => e.VesselId == vessel && equipment.Contains(e.Id), ct) != equipment.Length))
            return TowageOutcome.Fail(422, "linked_resource_unavailable");
        return null;
    }

    private static void Stamp(DrydockPackage p, Guid actor, DateTimeOffset now) {
        p.ActorId = actor; p.SavedAt = now; p.Hash = DrydockPolicy.Hash(p.VesselId, p.Id, p.Revision, p.State, p.ContentJson);
    }
    private static void Append(PoseidonDbContext db, DrydockPackage p, string reason, string? note = null) => db.DrydockPackageRevisions.Add(new() { TenantId = p.TenantId,
        VesselId = p.VesselId, PackageId = p.Id, Revision = p.Revision, State = p.State, ReasonCode = reason, ContentJson = p.ContentJson, Hash = p.Hash, Note = note,
        ActorId = p.ActorId, SavedAt = p.SavedAt, Provenance = p.Provenance });

    private static async Task<TowageOutcome> Create(TowageContext<DrydockCreateCommand> c) {
        var db = c.Db;
        if (c.Command.Id == Guid.Empty) return TowageOutcome.Fail(400, "validation_failed");
        if (await ValidLinks(db, c.Actor, c.Vessel, c.Command.Content, c.Now, c.Ct) is { } failure) return failure;
        if (await db.DrydockPackages.AnyAsync(p => p.Id == c.Command.Id, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        var p = new DrydockPackage { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = c.Command.Id, ContentJson = Json(c.Command.Content) };
        Stamp(p, c.Actor.Id, c.Now); db.DrydockPackages.Add(p); Append(db, p, "CREATE");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> Update(TowageContext<DrydockUpdateCommand> c) {
        var db = c.Db;
        var p = await db.DrydockPackages.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != c.Command.ExpectedVersion) return TowageOutcome.Fail(409, "version_conflict");
        if (p.State is DrydockPolicy.Verified or DrydockPolicy.Cancelled or DrydockPolicy.Completed) return TowageOutcome.Fail(409, "invalid_state");
        if (await ValidLinks(db, c.Actor, c.Vessel, c.Command.Content, c.Now, c.Ct) is { } failure) return failure;
        p.Revision = checked(p.Revision + 1); p.ContentJson = Json(c.Command.Content);
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, "EDIT");
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> Transition(TowageContext<DrydockTransitionCommand> c) {
        var db = c.Db; var cmd = c.Command;
        if (cmd.Note != null && !TowageExecutionPolicy.Long(cmd.Note, 1, 1000)) return TowageOutcome.Fail(400, "validation_failed");
        var p = await db.DrydockPackages.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != cmd.ExpectedVersion || p.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var (to, failure) = DrydockPolicy.Transition(p.State, cmd.Action);
        if (failure != null) return TowageOutcome.Fail(failure == "invalid_state" ? 409 : 400, failure);
        if (cmd.Action == "CANCEL" && cmd.Note == null) return TowageOutcome.Fail(400, "invalid_reason");
        var content = Content(p.ContentJson); var today = Today(c.Now);
        var (facts, _) = await Targets(db, c.Actor, c.Vessel, content, today, c.Ct);
        var blockers = DrydockPolicy.Blockers(cmd.Action, content, DrydockPolicy.Readiness(content, facts, today));
        if (blockers.Length > 0) return TowageOutcome.Fail(422, "drydock_record_incomplete", blockers);
        p.Revision = checked(p.Revision + 1); p.State = to!;
        if (cmd.Action == "START") p.ActualStart = c.Now;
        if (cmd.Action == "COMPLETE") { p.ActualFinish = c.Now; p.CompletedBy = c.Actor.Id; }
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, cmd.Action, cmd.Note);
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<TowageOutcome> Verify(TowageContext<DrydockVerifyCommand> c) {
        var db = c.Db; var cmd = c.Command;
        if (!FieldOpsCommands.Human(c.Http, c.Actor)) return TowageOutcome.Fail(403, "human_actor_required");
        var p = await db.DrydockPackages.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (p == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (p.Version != cmd.ExpectedVersion || p.Hash != cmd.ContentHash) return TowageOutcome.Fail(409, "version_conflict");
        var failure = DrydockPolicy.VerificationFailure(p.State, cmd.Decision, c.Actor.Id, p.CompletedBy, cmd.Reason);
        if (failure != null) return TowageOutcome.Fail(failure switch { "invalid_state" => 409, "separation_of_duties" => 403, _ => 400 }, failure);
        p.Revision = checked(p.Revision + 1);
        if (cmd.Decision == "VERIFY") { p.State = DrydockPolicy.Verified; p.VerifiedBy = c.Actor.Id; p.VerifiedAt = c.Now; }
        else { p.State = DrydockPolicy.InProgress; p.ActualFinish = null; p.CompletedBy = null; }
        Stamp(p, c.Actor.Id, c.Now); Append(db, p, cmd.Decision == "VERIFY" ? "VERIFY" : "REJECT", cmd.Reason);
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(p.Id, p.Revision);
    }

    private static async Task<object?> ReadView(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var p = await db.DrydockPackages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (p == null) return null;
        var content = Content(p.ContentJson); var today = Today(TugReadinessHttp.Now(h));
        var (facts, unreadable) = await Targets(db, a, vessel, content, today, ct);
        var readiness = DrydockPolicy.Readiness(content, facts, today);
        var timeline = await db.DrydockPackageRevisions.AsNoTracking().Where(r => r.PackageId == p.Id).OrderBy(r => r.Revision).Take(200)
            .Select(r => new DrydockTimelineDto(r.Revision, r.State, r.ReasonCode, r.Note, r.ActorId, r.SavedAt)).ToArrayAsync(ct);
        var write = CanWrite(a, vessel);
        string[] actions = !write ? [] : p.State switch {
            DrydockPolicy.Draft => ["PLAN", "CANCEL"], DrydockPolicy.Planned => ["START", "REPLAN", "CANCEL"], DrydockPolicy.InProgress => ["COMPLETE"], _ => [] };
        return new DrydockView(Dto(p), readiness, timeline, DrydockPolicy.Blockers("PLAN", content, readiness), DrydockPolicy.Blockers("COMPLETE", content, readiness), actions,
            write && p.State is DrydockPolicy.Draft or DrydockPolicy.Planned or DrydockPolicy.InProgress,
            CanVerify(a) && p.State == DrydockPolicy.Completed && p.CompletedBy != a.Id && FieldOpsCommands.Human(h, a), unreadable);
    }

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (a == null || !CanRead(a)) return await TowageCommands.Fail(h, Read, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        if (!await Vessels(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found");
        var rows = await db.DrydockPackages.AsNoTracking().Where(p => p.VesselId == vessel).OrderByDescending(p => p.SavedAt).Take(50).ToArrayAsync(ct);
        await TugReadinessHttp.Observe(h, Read, "Success", vessel);
        return Results.Json(new DrydockListView(vessel, rows.Select(p => { var c = Content(p.ContentJson);
            return new DrydockSummaryDto(p.Id, p.Revision, p.State, c.ProjectReference, c.Yard, c.PlannedStart, c.PlannedFinish, c.Items.Length); }).ToArray(),
            CanWrite(a, vessel), DrydockPolicy.Categories, DrydockPolicy.Priorities, DrydockPolicy.LinkKinds), FieldOpsJson.Options);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (a == null || !CanRead(a)) return await TowageCommands.Fail(h, Read, 403, "permission_denied", id);
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TowageCommands.Fail(h, Read, 400, "invalid_request", id);
        if (!await Vessels(db, a).AnyAsync(v => v.Id == q["vessel"], h.RequestAborted)) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        var view = await ReadView(h, db, a, q["vessel"], id, h.RequestAborted);
        if (view == null) return await TowageCommands.Fail(h, Read, 404, "resource_not_found", id);
        await TugReadinessHttp.Observe(h, Read, "Success", id);
        return Results.Json(view, FieldOpsJson.Options);
    }
}
