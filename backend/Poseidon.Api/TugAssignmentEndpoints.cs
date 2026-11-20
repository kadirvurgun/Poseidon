using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugAssignmentProposeCommand(Guid ClientId, Guid JobId, uint ExpectedJobVersion, Guid TugId,
    string? PlannedStart, string? PlannedEnd, string? Role);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugAssignmentRespondCommand(uint ExpectedVersion, string ScopeHash, string Decision, Guid AuthorityId, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugAssignmentWithdrawCommand(uint ExpectedVersion, string Reason);

public sealed record TugAssignmentResponseDto(Guid Id, string Decision, string? Reason, string Duty, DateTimeOffset RecordedAt);
public sealed record TugAssignmentDto(Guid Id, Guid VesselId, Guid JobId, long JobRevision, Guid TugId, DateTimeOffset PlannedStart, DateTimeOffset PlannedEnd,
    string Role, string State, string MasterAck, string ScopeHash, long Revision, uint Version, DateTimeOffset ProposedAt, DateTimeOffset? CancelledAt,
    string? CancelReason, bool Replayed = false, string SourceVersion = TugAssignmentWorkflow.SourceVersion);
public sealed record TugAssignmentItem(TugAssignmentDto Assignment, TugAssignmentResponseDto? Response, TugConflictReason[] Conflicts,
    string ConflictState, bool CanRespond, bool CanWithdraw, TugAuthorityDto[] Authorities, bool RequestChanged);
public sealed record TugAssignmentList(Guid JobId, long JobRevision, uint JobVersion, string JobState, bool CanPropose,
    TugAssignmentItem[] Items, TugOption[] Tugs, string[] Roles, string Boundary = "ShoreProposes;TugMasterAcknowledges;NoDispatchOrRelease");
public sealed record TugOption(Guid Id, string Code);

// DEV-083 / T-TUG-004: /api/v1/tug-assignments (18.01 Backlog row 84; 60.02 paragraphs 108-128).
public static class TugAssignmentEndpoints
{
    private const string Read = "TugAssignmentRead", Command = "TugAssignmentCommand";
    private static readonly Regex Instant = new(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant);

    public static void MapTugAssignments(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/tug-assignments").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("TugAssignmentList").Produces<TugAssignmentList>();
        g.MapPost("", Propose).WithName("TugAssignmentPropose").Accepts<TugAssignmentProposeCommand>("application/json").Produces<TugAssignmentDto>();
        g.MapPost("/{id:guid}/respond", Respond).WithName("TugAssignmentRespond").Accepts<TugAssignmentRespondCommand>("application/json").Produces<TugAssignmentDto>();
        g.MapPost("/{id:guid}/withdraw", Withdraw).WithName("TugAssignmentWithdraw").Accepts<TugAssignmentWithdrawCommand>("application/json").Produces<TugAssignmentDto>();
    }

    internal static TugAssignmentDto Dto(TugAssignmentProposal p, bool replayed = false) => new(p.Id, p.VesselId, p.JobId, p.JobRevision, p.TugId, p.PlannedStart, p.PlannedEnd,
        p.Role, p.State, p.MasterAck, p.ScopeHash, p.Revision, p.Version, p.ProposedAt, p.CancelledAt, p.CancelReason, replayed);

    private static async Task<IResult> List(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Read, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel", "job"], [], out var q)) return await TugReadinessHttp.Fail(h, Read, 400, "invalid_request");
        var ct = h.RequestAborted; var vessel = q["vessel"];
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TugReadinessHttp.Fail(h, Read, 404, "resource_not_found");
        var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == q["job"] && j.VesselId == vessel, ct);
        if (job == null) return await TugReadinessHttp.Fail(h, Read, 404, "resource_not_found", q["job"]);
        var visible = TugReadinessHttp.Tugs(db, a).Select(v => v.Id);
        // Scope before bounding: a proposal for a tug this actor cannot see is not returned or counted.
        var rows = await db.TugAssignmentProposals.AsNoTracking().Where(p => p.VesselId == vessel && p.JobId == job.Id && visible.Contains(p.TugId))
            .OrderByDescending(p => p.ProposedAt).ThenBy(p => p.Id).Take(TugReadinessAssessor.Limit).ToArrayAsync(ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var responses = await db.TugAssignmentResponses.AsNoTracking().Where(r => r.VesselId == vessel && ids.Contains(r.AssignmentId)).ToArrayAsync(ct);
        var now = TugReadinessHttp.Now(h);
        var items = new List<TugAssignmentItem>();
        foreach (var p in rows) {
            var conflicts = p.State == TugAssignmentWorkflow.Proposed ? await TugReadinessAssessor.Conflicts(h, db, a, p, now, ct) : [];
            var grants = TugOperationsAuthorities.Resolve(h, a, p.TugId, TugOperationsAuthorities.MasterDuties);
            var response = responses.SingleOrDefault(r => r.AssignmentId == p.Id);
            items.Add(new(Dto(p), response == null ? null : new(response.Id, response.Decision, response.Reason, response.Duty, response.RecordedAt), conflicts,
                conflicts.Any(c => c.Result == TugReadinessEngine.Fail) ? "BlockingConflict" : conflicts.Length > 0 ? "HumanReviewRequired" : "NoneObserved;InputsMayBeIncomplete",
                grants.Length > 0 && TugReadinessHttp.Write(a, p.TugId) && p.State == TugAssignmentWorkflow.Proposed && p.MasterAck == TugAssignmentWorkflow.Pending && p.ProposedBy != a.Id && p.JobRevision == job.Revision,
                TugEndpoints.PlanWrite(a, vessel) && p.State == TugAssignmentWorkflow.Proposed, TugOperationsAuthorities.Dto(grants), p.JobRevision != job.Revision));
        }
        var tugs = await TugReadinessHttp.Tugs(db, a).AsNoTracking().OrderBy(v => v.Code).Take(TugReadinessAssessor.Limit).Select(v => new TugOption(v.Id, v.Code)).ToArrayAsync(ct);
        await TugReadinessHttp.Observe(h, Read, "Success", job.Id);
        return Results.Ok(new TugAssignmentList(job.Id, job.Revision, job.Version, job.Status,
            TugEndpoints.PlanWrite(a, vessel) && job.Status == TugJobLifecycle.Submitted, items.ToArray(), tugs, TugAssignmentWorkflow.Roles));
    }

    private static bool TryInstant(string? text, out DateTimeOffset? value) {
        value = null;
        if (text == null) return true;
        if (!Instant.IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
        value = TugRequestPolicy.StoragePrecision(parsed.ToUniversalTime()); return true;
    }

    private static async Task<IResult> Propose(HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Command, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, Command, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found");
        // Shore operations proposes: current tug.plan.write and editable request scope.
        if (!TugEndpoints.PlanWrite(a, vessel)) return await TugReadinessHttp.Fail(h, Command, 403, "permission_denied");
        var (command, key, failure) = await TugReadinessHttp.Read<TugAssignmentProposeCommand>(h, Command);
        if (failure != null) return failure;
        if (command!.ClientId == Guid.Empty || command.JobId == Guid.Empty || command.TugId == Guid.Empty || command.ExpectedJobVersion == 0 ||
            !TryInstant(command.PlannedStart, out var start) || !TryInstant(command.PlannedEnd, out var end))
            return await TugReadinessHttp.Fail(h, Command, 400, "validation_failed");
        if (TugAssignmentWorkflow.InvalidProposal(start, end, command.Role) != null) return await TugReadinessHttp.Fail(h, Command, 400, "validation_failed");
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == command.TugId, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found");
        var fingerprint = TugReadinessHttp.Fingerprint(new { vessel, command });
        await TugReadinessHttp.Observe(h, Command, "Attempted", command.ClientId);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugAssignmentPropose" || receipt.ResourceId != command.ClientId || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, Command, 409, "idempotency_key_reused", command.ClientId);
                var saved = await db.TugAssignmentProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == receipt.ResourceId && p.VesselId == vessel, ct);
                if (saved == null) return await TugReadinessHttp.Fail(h, Command, 409, "source_revision_unavailable", command.ClientId);
                await TugReadinessHttp.Observe(h, Command, "Success", saved.Id);
                return Results.Ok(Dto(saved, true) with { Revision = receipt.Revision, Version = receipt.Version });
            }
            if (await db.TugAssignmentProposals.AnyAsync(p => p.Id == command.ClientId, ct)) return await TugReadinessHttp.Fail(h, Command, 409, "version_conflict", command.ClientId);
            var job = await db.TugJobs.SingleOrDefaultAsync(j => j.Id == command.JobId && j.VesselId == vessel, ct);
            if (job == null) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found", command.JobId);
            if (job.Version != command.ExpectedJobVersion) return await TugReadinessHttp.Fail(h, Command, 409, "version_conflict", job.Id);
            // 60.02 paragraph 340: assignment proposals belong to a SUBMITTED request; drafts and cancelled requests are refused.
            if (job.Status != TugJobLifecycle.Submitted) return await TugReadinessHttp.Fail(h, Command, 409, "invalid_state", job.Id);
            if (await db.TugAssignmentProposals.AnyAsync(p => p.VesselId == vessel && p.JobId == job.Id && p.TugId == command.TugId && p.State == TugAssignmentWorkflow.Proposed, ct))
                return await TugReadinessHttp.Fail(h, Command, 409, "tug_assignment_active_exists", job.Id);
            var now = TugReadinessHttp.Now(h);
            var proposal = new TugAssignmentProposal { TenantId = a.TenantId, VesselId = vessel, Id = command.ClientId, JobId = job.Id, JobRevision = job.Revision,
                TugId = command.TugId, PlannedStart = start!.Value, PlannedEnd = end!.Value, Role = command.Role!, ProposedBy = a.Id, ProposedAt = now,
                ScopeHash = TugAssignmentWorkflow.ScopeHash(new(job.Id, job.Revision, command.TugId, start.Value, end.Value, command.Role!)) };
            db.TugAssignmentProposals.Add(proposal); await db.SaveChangesAsync(ct);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = "TugAssignmentPropose", Fingerprint = fingerprint,
                ResourceId = proposal.Id, Revision = proposal.Revision, Version = proposal.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, Command, "Success", proposal.Id);
            return Results.Ok(Dto(proposal));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, Command, e, command.ClientId);
        }
    }

    // The Tug Master's own human acknowledgement. Authority is the server-provisioned appointment for the
    // assigned tug; the proposer can never acknowledge their own proposal; a changed request is stale.
    private static async Task<IResult> Respond(Guid id, HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Command, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, Command, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found");
        var (command, key, failure) = await TugReadinessHttp.Read<TugAssignmentRespondCommand>(h, Command);
        if (failure != null) return failure;
        if (command!.ExpectedVersion == 0 || command.ScopeHash is not { Length: 64 } || command.AuthorityId == Guid.Empty ||
            TugAssignmentWorkflow.Evaluate(TugAssignmentWorkflow.Proposed, TugAssignmentWorkflow.Pending, TugAssignmentCommand.Respond, command.Decision, command.Reason).Failure != null)
            return await TugReadinessHttp.Fail(h, Command, 400, "validation_failed", id);
        var fingerprint = TugReadinessHttp.Fingerprint(new { vessel, id, command });
        await TugReadinessHttp.Observe(h, Command, "Attempted", id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var p = await db.TugAssignmentProposals.SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
            if (p == null || !await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == p.TugId, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found", id);
            var grants = TugOperationsAuthorities.Resolve(h, a, p.TugId, TugOperationsAuthorities.MasterDuties);
            var grant = grants.SingleOrDefault(g => g.Id == command.AuthorityId);
            if (grant == null || !TugReadinessHttp.Write(a, p.TugId)) return await TugReadinessHttp.Fail(h, Command, 403, "tug_authority_required", id);
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugAssignmentRespond" || receipt.ResourceId != id || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, Command, 409, "idempotency_key_reused", id);
                await TugReadinessHttp.Observe(h, Command, "Success", id);
                return Results.Ok(Dto(p, true));
            }
            if (p.Version != command.ExpectedVersion) return await TugReadinessHttp.Fail(h, Command, 409, "version_conflict", id);
            if (p.ProposedBy == a.Id) return await TugReadinessHttp.Fail(h, Command, 403, "separation_of_duties", id);
            var job = await db.TugJobs.AsNoTracking().SingleAsync(j => j.Id == p.JobId && j.VesselId == vessel, ct);
            if (p.ScopeHash != command.ScopeHash || job.Revision != p.JobRevision || job.Status != TugJobLifecycle.Submitted)
                return await TugReadinessHttp.Fail(h, Command, 409, "tug_assignment_scope_stale", id);
            var transition = TugAssignmentWorkflow.Evaluate(p.State, p.MasterAck, TugAssignmentCommand.Respond, command.Decision, command.Reason);
            if (transition.ToState == null) return await TugReadinessHttp.Fail(h, Command, transition.Failure == "validation_failed" ? 400 : 409, transition.Failure!, id);
            var now = TugReadinessHttp.Now(h);
            p.MasterAck = transition.ToAck!; p.Revision = checked(p.Revision + 1);
            db.TugAssignmentResponses.Add(new() { TenantId = a.TenantId, VesselId = vessel, Id = Guid.NewGuid(), AssignmentId = p.Id, TugId = p.TugId, Decision = command.Decision,
                ScopeHash = p.ScopeHash, Reason = command.Reason, ActorId = a.Id, AuthorityId = grant.Id, Duty = grant.Duty, RecordedAt = now });
            await db.SaveChangesAsync(ct);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = "TugAssignmentRespond", Fingerprint = fingerprint,
                ResourceId = p.Id, Revision = p.Revision, Version = p.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, Command, "Success", p.Id);
            return Results.Ok(Dto(p));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, Command, e, id);
        }
    }

    private static async Task<IResult> Withdraw(Guid id, HttpContext h, PoseidonDbContext db) {
        var a = TugReadinessHttp.Actor(h);
        if (!a.CanReadTug) return await TugReadinessHttp.Fail(h, Command, 403, "permission_denied");
        if (!TugReadinessHttp.Query(h, ["vessel"], [], out var q)) return await TugReadinessHttp.Fail(h, Command, 400, "invalid_request");
        var vessel = q["vessel"]; var ct = h.RequestAborted;
        if (!await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == vessel, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found");
        if (!TugEndpoints.PlanWrite(a, vessel)) return await TugReadinessHttp.Fail(h, Command, 403, "permission_denied");
        var (command, key, failure) = await TugReadinessHttp.Read<TugAssignmentWithdrawCommand>(h, Command);
        if (failure != null) return failure;
        if (command!.ExpectedVersion == 0 || !TugAssignmentWorkflow.ValidReason(command.Reason)) return await TugReadinessHttp.Fail(h, Command, 400, "validation_failed", id);
        var fingerprint = TugReadinessHttp.Fingerprint(new { vessel, id, command });
        await TugReadinessHttp.Observe(h, Command, "Attempted", id);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var p = await db.TugAssignmentProposals.SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
            if (p == null || !await TugReadinessHttp.Tugs(db, a).AnyAsync(v => v.Id == p.TugId, ct)) return await TugReadinessHttp.Fail(h, Command, 404, "resource_not_found", id);
            var receipt = await TugReadinessHttp.Receipt(db, a, key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugAssignmentWithdraw" || receipt.ResourceId != id || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, Command, 409, "idempotency_key_reused", id);
                await TugReadinessHttp.Observe(h, Command, "Success", id);
                return Results.Ok(Dto(p, true));
            }
            if (p.Version != command.ExpectedVersion) return await TugReadinessHttp.Fail(h, Command, 409, "version_conflict", id);
            var transition = TugAssignmentWorkflow.Evaluate(p.State, p.MasterAck, TugAssignmentCommand.Withdraw, reason: command.Reason);
            if (transition.ToState == null) return await TugReadinessHttp.Fail(h, Command, transition.Failure == "validation_failed" ? 400 : 409, transition.Failure!, id);
            var now = TugReadinessHttp.Now(h);
            // The Master's acknowledgement record is kept unchanged; only the proposal is cancelled.
            p.State = transition.ToState; p.CancelledAt = now; p.CancelledBy = a.Id; p.CancelReason = command.Reason; p.Revision = checked(p.Revision + 1);
            await db.SaveChangesAsync(ct);
            db.TugReadinessReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = "TugAssignmentWithdraw", Fingerprint = fingerprint,
                ResourceId = p.Id, Revision = p.Revision, Version = p.Version, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, Command, "Success", p.Id);
            return Results.Ok(Dto(p));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, Command, e, id);
        }
    }
}
