using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DefectLifecycleRecordDto(Guid Id, long Sequence, string Kind, string StageAfter, long DefectRevision, string? Criticality,
    string? MeasureKind, bool Temporary, DateTimeOffset? ExpiresAt, Guid? MeasureId, string? Outcome, string Text, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record DefectLifecycleActionsDto(bool Start, bool Assess, bool Safeguard, bool Lift, bool Action, bool Verify, bool Effectiveness, bool Close);
public sealed record DefectLifecycleDto(Guid DefectId, Guid VesselId, bool Started, long Revision, string Stage, string? AssessedCriticality,
    string? DeclaredCriticality, bool Critical, long DefectRevision, DefectLifecycleRecordDto[] Records, DefectMeasureView[] Measures,
    DefectLifecycleAlert[] Alerts, string[] CloseBlockers, Guid[] ActionOwners, Guid? ClosedBy, DateTimeOffset? ClosedAt, DateTimeOffset AssessedAt,
    DefectLifecycleActionsDto Can);

// DEV-055 / T-S8-004. Lifecycle records are human records attached to an owning defect declaration;
// the declaration endpoints (DefectEndpoints) stay unchanged. Verification, effectiveness and
// closure (return to service) require their own server permission and an actor who recorded no
// corrective action in this lifecycle. Temporary-measure expiry is evaluated with the server clock
// on every read; an expired measure is a blocking alert and blocks closure.
public static class DefectLifecycleEndpoints
{
    private const string ReadAction = "DefectLifecycleRead", CommandAction = "DefectLifecycleCommand";
    internal static bool CanRead(TrustedActor? a) => DefectEndpoints.CanRead(a);
    private static string Permission(string kind) => kind switch
    {
        "VERIFY" => DefectLifecyclePolicy.VerifyPermission,
        "EFFECTIVENESS" => DefectLifecyclePolicy.EffectivenessPermission,
        "CLOSE" => DefectLifecyclePolicy.ClosePermission,
        _ => "defect.write"
    };
    internal static bool CanRecord(TrustedActor? a, Guid vessel, string kind) => CanRead(a) && a!.HasWriteAttribution && a.CanEditVessel(vessel) &&
        a.Permissions.Contains(Permission(kind));
    // Verification, effectiveness and closure (return to service) are human records.
    internal static bool HumanOnly(string kind) => kind is "VERIFY" or "EFFECTIVENESS" or "CLOSE";
    internal static bool CanRecord(HttpContext h, TrustedActor? a, Guid vessel, string kind) => CanRecord(a, vessel, kind) &&
        (!HumanOnly(kind) || CertificateRenewalEndpoints.Human(h, a!));

    public static void MapDefectLifecycle(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/defects/{id:guid}/lifecycle").RequireAuthorization("DirectoryRead");
        g.MapGet("", (Guid id, HttpContext h, PoseidonDbContext db) => Detail(id, h, db)).WithName("DefectLifecycleRead").Produces<DefectLifecycleDto>();
        Command(g, "/start", "START", "DefectLifecycleStart");
        Command(g, "/assess", "ASSESS", "DefectLifecycleAssess");
        Command(g, "/safeguards", "SAFEGUARD", "DefectLifecycleSafeguard");
        g.MapPost("/safeguards/{measureId:guid}/lift", (Guid id, Guid measureId, HttpContext h, PoseidonDbContext db) =>
                TechnicalRecordCommands.Run<DefectLifecycleCommand>(h, db, "DefectLifecycleLift", CommandAction, id, measureId,
                    (a, v) => CanRecord(a, v, "LIFT"), _ => id, c => Apply("LIFT", c), Fresh(h)))
            .WithName("DefectLifecycleLift").Accepts<DefectLifecycleCommand>("application/json").Produces<DefectLifecycleDto>().Produces<TechnicalRecordFailure>(422);
        Command(g, "/actions", "ACTION", "DefectLifecycleAction");
        Command(g, "/verify", "VERIFY", "DefectLifecycleVerify");
        Command(g, "/effectiveness", "EFFECTIVENESS", "DefectLifecycleEffectiveness");
        Command(g, "/close", "CLOSE", "DefectLifecycleClose");
    }

    private static void Command(RouteGroupBuilder g, string path, string kind, string operation) =>
        g.MapPost(path, (Guid id, HttpContext h, PoseidonDbContext db) => TechnicalRecordCommands.Run<DefectLifecycleCommand>(h, db, operation, CommandAction, id, null,
                (a, v) => CanRecord(a, v, kind), _ => id, c => Apply(kind, c), Fresh(h), HumanOnly(kind)))
            .WithName(operation).Accepts<DefectLifecycleCommand>("application/json").Produces<DefectLifecycleDto>().Produces<TechnicalRecordFailure>(422);

    private static string? DeclaredCriticality(string fieldsJson)
    {
        try { return DefectPolicy.ReadFields(fieldsJson).TryGetValue("criticality", out var f) && f.State == "Known" ? f.Value : null; }
        catch (InvalidDataException) { return null; }
    }
    private static DefectLifecycleEntry Entry(DefectLifecycleRecord r) => new(r.Id, r.Sequence, r.Kind, r.StageAfter, r.DefectRevision, r.Criticality,
        r.MeasureKind, r.Temporary, r.ExpiresAt, r.MeasureId, r.Outcome, r.ActorId, r.RecordedAt);

    private static async Task<TechnicalOutcome> Apply(string kind, TechnicalContext<DefectLifecycleCommand> c)
    {
        var db = c.Db; var cmd = c.Command; var id = c.RouteId!.Value;
        if (!DefectLifecyclePolicy.ValidShape(kind, cmd, c.Now, out var expiresAt)) return TechnicalOutcome.Fail(400, "invalid_request");
        var defect = await db.Defects.AsNoTracking().SingleOrDefaultAsync(d => d.VesselId == c.Vessel && d.Id == id, c.Ct);
        if (defect == null) return TechnicalOutcome.Fail(404, "resource_not_found");
        var head = await db.DefectLifecycles.SingleOrDefaultAsync(l => l.VesselId == c.Vessel && l.DefectId == id, c.Ct);
        if (kind == "START" && head == null && cmd.DefectRevision != defect.Revision) return TechnicalOutcome.Fail(409, "defect_declaration_changed");
        if (head != null && kind != "START" && head.Revision != cmd.ExpectedRevision) return TechnicalOutcome.Fail(409, "version_conflict");
        var records = head == null ? [] : await db.DefectLifecycleRecords.AsNoTracking().Where(r => r.VesselId == c.Vessel && r.DefectId == id)
            .OrderBy(r => r.Sequence).ToArrayAsync(c.Ct);
        var evaluation = DefectLifecyclePolicy.Evaluate(records.Select(Entry).ToArray(), DeclaredCriticality(defect.FieldsJson), defect.Revision, c.Now);
        var decision = DefectLifecyclePolicy.Decide(kind, evaluation, head != null, c.Actor.Id, c.Actor.Permissions, cmd, c.SubId);
        if (decision.Status != 200) return TechnicalOutcome.Fail(decision.Status, decision.Code!, decision.Missing);
        if (head == null)
        {
            head = new DefectLifecycle { TenantId = c.Actor.TenantId, VesselId = c.Vessel, DefectId = id, Revision = 1 };
            db.DefectLifecycles.Add(head);
        }
        else head.Revision++;
        head.Stage = decision.StageAfter; head.ActorId = c.Actor.Id; head.SavedAt = c.Now;
        if (kind == "ASSESS") head.AssessedCriticality = cmd.Criticality;
        if (kind == "CLOSE") { head.ClosedBy = c.Actor.Id; head.ClosedAt = c.Now; }
        await db.SaveChangesAsync(c.Ct);
        db.DefectLifecycleRecords.Add(new()
        {
            TenantId = head.TenantId, VesselId = c.Vessel, Id = Guid.NewGuid(), DefectId = id, Sequence = head.Revision, Kind = kind, StageAfter = head.Stage,
            DefectRevision = defect.Revision, Criticality = kind == "ASSESS" ? cmd.Criticality : null, MeasureKind = kind == "SAFEGUARD" ? cmd.MeasureKind : null,
            Temporary = kind == "SAFEGUARD" && cmd.Temporary == true, ExpiresAt = expiresAt, MeasureId = kind == "LIFT" ? c.SubId : null,
            Outcome = kind == "CLOSE" ? DefectLifecyclePolicy.ClosureOutcome : cmd.Outcome, Text = cmd.Text ?? "", ActorId = c.Actor.Id, RecordedAt = c.Now
        });
        await db.SaveChangesAsync(c.Ct);
        return TechnicalOutcome.Ok(id, head.Revision);
    }

    private static Func<PoseidonDbContext, TrustedActor, Guid, Guid, DateTimeOffset, CancellationToken, Task<object?>> Fresh(HttpContext h) =>
        async (db, actor, vessel, id, now, ct) => await Describe(db, actor, vessel, id, now, CertificateRenewalEndpoints.Human(h, actor), ct);

    internal static async Task<DefectLifecycleDto?> Describe(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid id, DateTimeOffset now, bool human, CancellationToken ct)
    {
        var defect = await db.Defects.AsNoTracking().SingleOrDefaultAsync(d => d.VesselId == vessel && d.Id == id, ct);
        if (defect == null) return null;
        var head = await db.DefectLifecycles.AsNoTracking().SingleOrDefaultAsync(l => l.VesselId == vessel && l.DefectId == id, ct);
        var records = head == null ? [] : await db.DefectLifecycleRecords.AsNoTracking().Where(r => r.VesselId == vessel && r.DefectId == id)
            .OrderBy(r => r.Sequence).ToArrayAsync(ct);
        var declared = DeclaredCriticality(defect.FieldsJson);
        var e = DefectLifecyclePolicy.Evaluate(records.Select(Entry).ToArray(), declared, defect.Revision, now);
        bool May(string kind, Guid? measure = null) => CanRecord(actor, vessel, kind) && (human || !HumanOnly(kind)) && (kind == "START" ? head == null :
            head != null && DefectLifecyclePolicy.Decide(kind, e, true, actor.Id, actor.Permissions,
                new(head.Revision, Outcome: kind == "VERIFY" ? "VERIFIED" : kind == "EFFECTIVENESS" ? "EFFECTIVE" : null), measure).Status == 200);
        var liftable = e.Measures.FirstOrDefault(m => m.State is "ACTIVE" or "EXPIRED")?.Id;
        var can = new DefectLifecycleActionsDto(May("START"), May("ASSESS"), May("SAFEGUARD"), liftable != null && May("LIFT", liftable), May("ACTION"),
            May("VERIFY"), May("EFFECTIVENESS"), May("CLOSE"));
        return new(id, vessel, head != null, head?.Revision ?? 0, head?.Stage ?? DefectLifecyclePolicy.Detect, head?.AssessedCriticality, declared, e.Critical,
            defect.Revision, records.Select(r => new DefectLifecycleRecordDto(r.Id, r.Sequence, r.Kind, r.StageAfter, r.DefectRevision, r.Criticality, r.MeasureKind,
                r.Temporary, r.ExpiresAt, r.MeasureId, r.Outcome, r.Text, r.ActorId, r.RecordedAt)).ToArray(),
            e.Measures, e.Alerts, head == null ? ["LIFECYCLE_NOT_STARTED", .. e.CloseBlockers] : e.CloseBlockers, e.ActionOwners, head?.ClosedBy, head?.ClosedAt, e.AssessedAt, can);
    }

    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db)
    {
        var (actor, vessel, failure) = await TechnicalRecordCommands.ReadGate(h, db, ReadAction, CanRead); if (failure != null) return failure;
        var dto = await Describe(db, actor!, vessel, id, TechnicalRecordCommands.Now(h), CertificateRenewalEndpoints.Human(h, actor!), h.RequestAborted);
        if (dto == null) return await TechnicalRecordCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        await TechnicalRecordCommands.Audit(h, ReadAction, "Success", id);
        return Results.Json(dto, TechnicalRecordCommands.Json);
    }
}

// Read-only adapter for other modules (S7 tug readiness, DEV-056 command center): a defect counts
// as closed only through the human CLOSE record, and only for the exact declaration revision that
// was closed. A later declaration edit makes it open again for consumers.
internal static class DefectLifecycleReader
{
    internal static async Task<Dictionary<Guid, long>> ClosedRevisions(PoseidonDbContext db, Guid vessel, IReadOnlyCollection<Guid> defects, CancellationToken ct) =>
        defects.Count == 0 ? [] : (await db.DefectLifecycleRecords.AsNoTracking().Where(r => r.VesselId == vessel && defects.Contains(r.DefectId) && r.Kind == "CLOSE" &&
            db.DefectLifecycles.Any(l => l.VesselId == vessel && l.DefectId == r.DefectId && l.Stage == DefectLifecyclePolicy.Closed))
            .Select(r => new { r.DefectId, r.DefectRevision }).ToArrayAsync(ct)).ToDictionary(r => r.DefectId, r => r.DefectRevision);
}
