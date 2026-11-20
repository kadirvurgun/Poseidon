using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugReadinessDecisionCommand(Guid AssignmentId, string ExpectedAssessmentHash, string Decision, string Reason, Guid AuthorityId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowEquipmentSaveCommand(Guid ClientId, uint? BaseVersion, TowEquipmentInputs Inputs);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowEquipmentVerifyCommand(uint ExpectedVersion, long ExpectedRevision, string Outcome, Guid AuthorityId, string? Reason = null);

public sealed record TugReadinessDecisionDto(Guid Id, Guid AssignmentId, long AssignmentRevision, string Decision, string AssessedState, string AssessmentHash,
    string Reason, string Duty, DateTimeOffset RecordedAt, bool Replayed = false);
public sealed record TugReadinessView(TugAssignmentDto Assignment, string State, TugReadinessCategoryState[] Categories, string[] CriticalBlockers,
    string[] ReviewReasons, string[] Conditions, string AssessmentHash, string? RequiredBollardPull, string? AvailableBollardPull, string? BollardPullMargin,
    TugReadinessCheck[] Checks, TugConflictReason[] Conflicts, TugReadinessDecisionDto[] Decisions, TugAuthorityDto[] Authorities, bool CanDecide,
    DateTimeOffset AssessedAt, string DecisionAuthority = TugReadinessEngine.DecisionAuthority, string SourceLocator = TugReadinessEngine.SourceLocator);
public sealed record TowEquipmentView(TowEquipmentAssessment Assessment, bool CanWrite, bool CanVerify, TugAuthorityDto[] Authorities,
    string[] Kinds, string[] Conditions, string SourceLocator = TowEquipmentPolicy.SourceLocator);
public sealed record TowEquipmentDto(Guid Id, Guid VesselId, long Revision, uint Version, TowEquipmentInputs Inputs, string Provenance, DateTimeOffset SavedAt, bool Replayed = false);

// DEV-084 /api/v1/tug-readiness, DEV-085 /api/v1/tug-equipment-readiness + /api/v1/tug-tow-equipment,
// DEV-086 /api/v1/tug-crew-readiness. Reads are advisory projections; the only human acts are the
// Master/Tug Master GO/NO_GO record and the Chief Engineer verification record.
public static class TugReadinessEndpoints
{
    private const string Read = "TugReadinessRead", Decide = "TugReadinessDecision", EquipmentRead = "TugEquipmentRead",
        EquipmentCommand = "TugEquipmentCommand", CrewRead = "TugCrewReadinessRead";

    public static void MapTugReadiness(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1").RequireAuthorization("DirectoryRead");
        g.MapGet("/tug-readiness", Readiness).WithName("TugReadinessRead").Produces<TugReadinessView>();
        g.MapPost("/tug-readiness/decisions", Decision).WithName("TugReadinessDecide").Accepts<TugReadinessDecisionCommand>("application/json").Produces<TugReadinessDecisionDto>().Produces(422);
        g.MapGet("/tug-crew-readiness", Crew).WithName("TugCrewReadinessRead").Produces<TugCrewReadinessView>();
        g.MapGet("/tug-equipment-readiness", Equipment).WithName("TugEquipmentReadinessRead").Produces<TowEquipmentView>();
        g.MapPost("/tug-tow-equipment", SaveEquipment).WithName("TugTowEquipmentSave").Accepts<TowEquipmentSaveCommand>("application/json").Produces<TowEquipmentDto>();
        g.MapPut("/tug-tow-equipment/{id:guid}", SaveEquipment).WithName("TugTowEquipmentUpdate").Accepts<TowEquipmentSaveCommand>("application/json").Produces<TowEquipmentDto>();
        g.MapPost("/tug-tow-equipment/{id:guid}/verifications", Verify).WithName("TugTowEquipmentVerify").Accepts<TowEquipmentVerifyCommand>("application/json").Produces<TowEquipmentVerificationDto>();
    }

    private static TugReadinessDecisionDto Dto(TugReadinessDecision d, bool replayed = false) => new(d.Id, d.AssignmentId, d.AssignmentRevision, d.Decision, d.AssessedState,
        d.AssessmentHash, d.Reason, d.Duty, d.RecordedAt, replayed);

    // Resolves an assignment the actor may see through both the request context and the assigned tug.
    private static async Task<(TugAssignmentProposal? P, TugJob? Job, IResult? Failure)> Assignment(HttpContext h, PoseidonDbContext db, TrustedActor a, string action, Guid vessel, Guid id, CancellationToken ct) {
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return (null, null, await TugReadinessHttp.Fail(h, action, 404, "resource_not_found"));
        var p = await db.TugAssignmentProposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (p == null || !await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == p.TugId, ct)) return (null, null, await TugReadinessHttp.Fail(h, action, 404, "resource_not_found", id));
        var job = await db.TugJobs.AsNoTracking().SingleAsync(j => j.Id == p.JobId && j.VesselId == vessel, ct);
        return (p, job, null);
    }

    private static async Task<IResult> Readiness(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Read, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "assignment"], [], out var q)) return await TugReadinessHttp.Fail(h, Read, 400, "invalid_request");
        var ct = h.RequestAborted;
        var (p, job, failure) = await Assignment(h, db, a, Read, q["vessel"], q["assignment"], ct);
        if (failure != null) return failure;
        var now = TugReadinessHttp.Now(h);
        var assessment = await TugReadinessAssessor.Assess(h, db, a, p!, job!, now, ct);
        var decisions = await db.TugReadinessDecisions.AsNoTracking().Where(d => d.VesselId == p!.VesselId && d.AssignmentId == p.Id)
            .OrderByDescending(d => d.RecordedAt).Take(32).ToArrayAsync(ct);
        var grants = TugOperationsAuthorities.Resolve(h, a, p!.TugId, TugOperationsAuthorities.MasterDuties);
        var o = assessment.Outcome;
        await TugReadinessHttp.Observe(h, Read, "Success", p.Id);
        return Results.Ok(new TugReadinessView(TugAssignmentEndpoints.Dto(p), o.State, o.Categories, o.CriticalBlockers, o.ReviewReasons, o.Conditions, o.Hash,
            job!.RequiredBollardPull?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), assessment.AvailableBollardPull, assessment.BollardPullMargin,
            assessment.Checks, assessment.Conflicts, decisions.Select(d => Dto(d)).ToArray(), TugOperationsAuthorities.Dto(grants),
            grants.Length > 0 && TugReadinessHttp.Write(a, p.TugId) && p.State == TugAssignmentWorkflow.Proposed, now));
    }

    // Master/Tug Master GO/NO_GO human record bound to the exact assessment the decider saw. The server
    // re-evaluates; a changed assessment is stale; GO is refused unless READY/CONDITIONAL; NO_GO is always
    // recordable with a reason. The record changes no assignment/job state (release is later work).
    private static async Task<IResult> Decision(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Decide, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, Decide, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        var (command, key, failure) = await TugReadinessHttp.Read<TugReadinessDecisionCommand>(h, Decide);
        if (failure != null) return failure;
        if (command!.AssignmentId == Guid.Empty || command.AuthorityId == Guid.Empty || command.ExpectedAssessmentHash is not { Length: 64 } ||
            !TugReadinessDecisionPolicy.Decisions.Contains(command.Decision) || !TugReadinessDecisionPolicy.ValidReason(command.Reason))
            return await TugReadinessHttp.Fail(h, Decide, 400, "validation_failed", command.AssignmentId);
        var (current, job, missing) = await Assignment(h, db, a, Decide, vessel, command.AssignmentId, ct);
        if (missing != null) return missing;
        var grant = TugOperationsAuthorities.Resolve(h, a, current!.TugId, TugOperationsAuthorities.MasterDuties).SingleOrDefault(g => g.Id == command.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(a, current.TugId)) return await TugReadinessHttp.Fail(h, Decide, 403, "tug_authority_required", current.Id);
        var fingerprint = TugReadinessHttp.Fingerprint(new { vessel, command });
        await TugReadinessHttp.Observe(h, Decide, "Attempted", current.Id);
        var now = TugReadinessHttp.Now(h);
        // Assessment reads run before the write transaction; the hash binds the decision to their result.
        var assessment = await TugReadinessAssessor.Assess(h, db, a, current, job!, now, ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugReadinessDecide" || receipt.ResourceId != current.Id || receipt.Fingerprint != fingerprint || receipt.RecordId == null)
                    return await TugReadinessHttp.Fail(h, Decide, 409, "idempotency_key_reused", current.Id);
                var saved = await db.TugReadinessDecisions.AsNoTracking().SingleAsync(d => d.Id == receipt.RecordId && d.VesselId == vessel, ct);
                await TugReadinessHttp.Observe(h, Decide, "Success", current.Id);
                return Results.Ok(Dto(saved, true));
            }
            var p = await db.TugAssignmentProposals.AsNoTracking().SingleAsync(x => x.Id == current.Id && x.VesselId == vessel, ct);
            if (p.Version != current.Version || p.State != TugAssignmentWorkflow.Proposed) return await TugReadinessHttp.Fail(h, Decide, 409, "version_conflict", p.Id);
            if (assessment.Outcome.Hash != command.ExpectedAssessmentHash) return await TugReadinessHttp.Fail(h, Decide, 409, "assessment_stale", p.Id);
            var refusal = TugReadinessDecisionPolicy.Refusal(command.Decision, assessment.Outcome.State, command.Reason);
            if (refusal != null) return await TugReadinessHttp.Fail(h, Decide, refusal == "tug_go_not_permitted" ? 422 : 400, refusal, p.Id);
            var decision = new TugReadinessDecision { TenantId = a.TenantId, VesselId = vessel, Id = Guid.NewGuid(), AssignmentId = p.Id, AssignmentRevision = p.Revision,
                TugId = p.TugId, Decision = command.Decision, AssessedState = assessment.Outcome.State, AssessmentHash = assessment.Outcome.Hash, Reason = command.Reason,
                ActorId = a.Id, AuthorityId = grant.Id, Duty = grant.Duty, RecordedAt = now };
            db.TugReadinessDecisions.Add(decision);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = "TugReadinessDecide", Fingerprint = fingerprint,
                ResourceId = p.Id, RecordId = decision.Id, Revision = p.Revision, Version = p.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, Decide, "Success", p.Id);
            return Results.Ok(Dto(decision));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, Decide, e, current.Id);
        }
    }

    private static async Task<IResult> Crew(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, CrewRead, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "assignment"], [], out var q)) return await TugReadinessHttp.Fail(h, CrewRead, 400, "invalid_request");
        var ct = h.RequestAborted;
        var (p, _, failure) = await Assignment(h, db, a, CrewRead, q["vessel"], q["assignment"], ct);
        if (failure != null) return failure;
        var view = await TugReadinessAssessor.Crew(h, db, a, p!, TugReadinessHttp.Now(h), ct);
        await TugReadinessHttp.Observe(h, CrewRead, "Success", p!.Id);
        return Results.Ok(view);
    }

    private static bool EquipmentWrite(TrustedActor a, Guid tug) => TugReadinessHttp.Write(a, tug) && a.Permissions.Contains("equipment.write");

    private static async Task<IResult> Equipment(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, EquipmentRead, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, EquipmentRead, 400, "invalid_request");
        var tug = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == tug, ct)) return await TugReadinessHttp.Fail(h, EquipmentRead, 404, "resource_not_found");
        var assessment = await TugReadinessAssessor.Equipment(h, db, a, tug, TugReadinessHttp.Now(h), ct);
        var grants = TugOperationsAuthorities.Resolve(h, a, tug, TugOperationsAuthorities.EngineDuties);
        await TugReadinessHttp.Observe(h, EquipmentRead, "Success", tug);
        return Results.Ok(new TowEquipmentView(assessment, EquipmentWrite(a, tug), grants.Length > 0 && TugReadinessHttp.Write(a, tug), TugOperationsAuthorities.Dto(grants),
            TowEquipmentPolicy.Kinds, TowEquipmentPolicy.Conditions));
    }

    private static TowEquipmentDto ItemDto(TowEquipmentRevision r, uint version, bool replayed) => new(r.ItemId, r.VesselId, r.Revision, version, r.Inputs(), r.Provenance, r.SavedAt, replayed);

    // Declarations only (UserDeclaredUnverified). Saving never verifies; any change needs a new verification.
    private static async Task<IResult> SaveEquipment(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, EquipmentCommand, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 400, "invalid_request");
        var tug = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == tug, ct)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 404, "resource_not_found");
        if (!EquipmentWrite(a, tug)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 403, "permission_denied");
        var (command, key, failure) = await TugReadinessHttp.Read<TowEquipmentSaveCommand>(h, EquipmentCommand);
        if (failure != null) return failure;
        var update = h.Request.Method == "PUT";
        if (command!.ClientId == Guid.Empty || command.Inputs == null || command.BaseVersion == 0 || update != (command.BaseVersion != null) ||
            update && h.Request.RouteValues["id"]?.ToString() != command.ClientId.ToString() || TowEquipmentPolicy.Invalid(command.Inputs).Length != 0)
            return await TugReadinessHttp.Fail(h, EquipmentCommand, 400, "validation_failed", command.ClientId);
        var action = update ? "TugTowEquipmentUpdate" : "TugTowEquipmentSave";
        var fingerprint = TugReadinessHttp.Fingerprint(new { tug, action, command });
        await TugReadinessHttp.Observe(h, EquipmentCommand, "Attempted", command.ClientId);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != action || receipt.ResourceId != command.ClientId || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, EquipmentCommand, 409, "idempotency_key_reused", command.ClientId);
                var revision = await db.TowEquipmentRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.ItemId == receipt.ResourceId && r.VesselId == tug && r.Revision == receipt.Revision, ct);
                if (revision == null) return await TugReadinessHttp.Fail(h, EquipmentCommand, 409, "source_revision_unavailable", command.ClientId);
                await TugReadinessHttp.Observe(h, EquipmentCommand, "Success", command.ClientId);
                return Results.Ok(ItemDto(revision, receipt.Version, true));
            }
            var item = await db.TowEquipmentItems.SingleOrDefaultAsync(x => x.Id == command.ClientId, ct);
            if (item != null && item.VesselId != tug) return await TugReadinessHttp.Fail(h, EquipmentCommand, 404, "resource_not_found", command.ClientId);
            if (item == null && update || item != null && (!update || item.Version != command.BaseVersion)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 409, "version_conflict", command.ClientId);
            if (item != null && item.Kind != command.Inputs.Kind) return await TugReadinessHttp.Fail(h, EquipmentCommand, 400, "validation_failed", command.ClientId);
            // Links must point at this tug's own asset/certificate; a foreign or hidden reference is refused.
            if (command.Inputs.AssetId is Guid asset && !await db.Assets.AnyAsync(x => x.Id == asset && x.VesselId == tug, ct) ||
                command.Inputs.CertificateId is Guid certificate && !await db.VesselCertificates.AnyAsync(x => x.Id == certificate && x.VesselId == tug, ct))
                return await TugReadinessHttp.Fail(h, EquipmentCommand, 422, "linked_resource_unavailable", command.ClientId);
            var now = TugReadinessHttp.Now(h);
            if (item == null) { item = new() { TenantId = a.TenantId, VesselId = tug, Id = command.ClientId, Kind = command.Inputs.Kind }; db.TowEquipmentItems.Add(item); }
            else item.Revision = checked(item.Revision + 1);
            item.Apply(command.Inputs); item.Provenance = "UserDeclaredUnverified"; item.SavedAt = now; item.ActorId = a.Id;
            var snapshot = item.Snapshot(); db.TowEquipmentRevisions.Add(snapshot);
            await db.SaveChangesAsync(ct);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = tug, ActorId = a.Id, Key = key, Action = action, Fingerprint = fingerprint,
                ResourceId = item.Id, Revision = item.Revision, Version = item.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, EquipmentCommand, "Success", item.Id);
            return Results.Ok(ItemDto(snapshot, item.Version, false));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, EquipmentCommand, e, command.ClientId);
        }
    }

    // Chief Engineer verification of one exact item revision; never by the person who declared it.
    private static async Task<IResult> Verify(Guid id, HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, EquipmentCommand, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 400, "invalid_request");
        var tug = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == tug, ct)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 404, "resource_not_found");
        var (command, key, failure) = await TugReadinessHttp.Read<TowEquipmentVerifyCommand>(h, EquipmentCommand);
        if (failure != null) return failure;
        if (command!.ExpectedVersion == 0 || command.ExpectedRevision < 1 || command.AuthorityId == Guid.Empty || !TowEquipmentPolicy.Verifications.Contains(command.Outcome) ||
            (command.Outcome == TowEquipmentPolicy.NotVerified ? !TugAssignmentWorkflow.ValidReason(command.Reason) : command.Reason != null && !TugAssignmentWorkflow.ValidReason(command.Reason)))
            return await TugReadinessHttp.Fail(h, EquipmentCommand, 400, "validation_failed", id);
        var grant = TugOperationsAuthorities.Resolve(h, a, tug, TugOperationsAuthorities.EngineDuties).SingleOrDefault(g => g.Id == command.AuthorityId);
        if (grant == null || !TugReadinessHttp.Write(a, tug)) return await TugReadinessHttp.Fail(h, EquipmentCommand, 403, "tug_authority_required", id);
        var fingerprint = TugReadinessHttp.Fingerprint(new { tug, id, command });
        await TugReadinessHttp.Observe(h, EquipmentCommand, "Attempted", id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var item = await db.TowEquipmentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == tug, ct);
            if (item == null) return await TugReadinessHttp.Fail(h, EquipmentCommand, 404, "resource_not_found", id);
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugTowEquipmentVerify" || receipt.ResourceId != id || receipt.Fingerprint != fingerprint || receipt.RecordId == null)
                    return await TugReadinessHttp.Fail(h, EquipmentCommand, 409, "idempotency_key_reused", id);
                var saved = await db.TowEquipmentVerifications.AsNoTracking().SingleAsync(v => v.Id == receipt.RecordId && v.VesselId == tug, ct);
                await TugReadinessHttp.Observe(h, EquipmentCommand, "Success", id);
                return Results.Ok(new TowEquipmentVerificationDto(saved.Id, saved.ItemRevision, saved.Outcome, saved.Reason, saved.Duty, saved.RecordedAt));
            }
            if (item.Version != command.ExpectedVersion || item.Revision != command.ExpectedRevision) return await TugReadinessHttp.Fail(h, EquipmentCommand, 409, "version_conflict", id);
            if (item.ActorId == a.Id) return await TugReadinessHttp.Fail(h, EquipmentCommand, 403, "separation_of_duties", id);
            var now = TugReadinessHttp.Now(h);
            var verification = new TowEquipmentVerification { TenantId = a.TenantId, VesselId = tug, Id = Guid.NewGuid(), ItemId = item.Id, ItemRevision = item.Revision,
                Outcome = command.Outcome, Reason = command.Reason, ActorId = a.Id, AuthorityId = grant.Id, Duty = grant.Duty, RecordedAt = now };
            db.TowEquipmentVerifications.Add(verification);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = tug, ActorId = a.Id, Key = key, Action = "TugTowEquipmentVerify", Fingerprint = fingerprint,
                ResourceId = item.Id, RecordId = verification.Id, Revision = item.Revision, Version = item.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, EquipmentCommand, "Success", item.Id);
            return Results.Ok(new TowEquipmentVerificationDto(verification.Id, verification.ItemRevision, verification.Outcome, verification.Reason, verification.Duty, verification.RecordedAt));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, EquipmentCommand, e, id);
        }
    }
}
