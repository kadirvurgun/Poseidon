using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned appointment of one human as Master of one ship (60.01 p952: "gemide o karar icin gecerli onboard
// authority"). Never derived from a request field, IdP role name, crew list, tenant-wide manage or administrator.
public sealed record DepartureAuthority(Guid Id, Guid VesselId, Guid RoleId, string Duty, string AppointmentSource,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record DepartureAuthorityDto(Guid Id, string Duty);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DepartureSignoffCommand(DateOnly From, DateOnly To, string ExpectedAssessmentHash, [property: JsonRequired] long ExpectedSequence,
    string Decision, string Reason, Guid AuthorityId);
public sealed record DepartureDecisionDto(Guid Id, Guid VesselId, long Sequence, Guid? PreviousDecisionId, string Decision, string AssessedState, string AssessmentMode,
    string AssessmentHash, DateOnly WindowFrom, DateOnly WindowTo, string Reason, string Duty, Guid ActorId, DateTimeOffset RecordedAt, string Validity, bool Replayed = false);
public sealed record DepartureReadinessView(Guid VesselId, DateOnly From, DateOnly To, string Mode, string State, DepartureCategoryState[] Categories,
    string[] CriticalBlockers, string[] ReviewReasons, string[] Conditions, DepartureWhy[] Why, string AssessmentHash, DateTimeOffset AssessedAt,
    DateTimeOffset ValidUntil, DepartureDecisionDto[] Decisions, long Sequence, DepartureAuthorityDto[] Authorities, bool CanDecide, string[] AllowedDecisions,
    string DecisionAuthority = DepartureReadinessEngine.DecisionAuthority, string SourceLocator = DepartureReadinessEngine.SourceLocator);

internal static class DepartureAuthorities
{
    internal static bool CanRead(TrustedActor? a) => a != null && a.CanRead && a.Permissions.Contains("departure.readiness.read");
    // The permission is necessary, never sufficient: an exact current Master appointment is also required.
    internal static bool CanDecide(TrustedActor? a, Guid vessel) => CanRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("departure.decide") && a.CanEditVessel(vessel);

    // Exactly one membership with ActorKind exact ordinal "Human"; AI, automation, service, unspecified or ambiguous
    // principals fail closed even when an appointment is (mis)configured for them.
    internal static DepartureAuthority[] Resolve(FederationSettings settings, TrustedActor? actor, Guid vessel, DateTimeOffset now) {
        if (actor == null || !CanDecide(actor, vessel) || actor.RoleContext == null) return [];
        var members = settings.Memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (members.Length != 1 || !string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal)) return [];
        var grants = members[0].DepartureAuthorities ?? [];
        if (grants.Length > 256) return [];
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 && g.VesselId == vessel && vessel != Guid.Empty &&
            string.Equals(g.Duty, DepartureDecisionPolicy.MasterDuty, StringComparison.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            g.AppointmentSource is { Length: > 0 and <= 200 } source && source.Trim() == source && !source.Any(char.IsControl) &&
            g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
    internal static DepartureAuthorityDto[] Dto(DepartureAuthority[] grants) => grants.Select(g => new DepartureAuthorityDto(g.Id, g.Duty)).ToArray();
    internal static string Hash(DepartureAuthority grant, TrustedActor actor) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = actor.Id, tenantId = actor.TenantId, actorKind = "Human" }));
}

// DEV-058 aggregation. Every input is read through its owning module's gate at server time; an unreadable or
// absent source is UNKNOWN with an explicit reason. Nothing is scored or averaged.
internal static class DepartureReadinessAssessor
{
    private const string U = DepartureReadinessEngine.Unknown, F = DepartureReadinessEngine.Fail, P = DepartureReadinessEngine.Pass, C = DepartureReadinessEngine.Condition;
    private const int CertificateLimit = 200;
    private static string Code(string value) => DepartureReadinessEngine.Code(value);

    internal sealed record Assessment(DepartureOutcome Outcome, DepartureCheck[] Checks);

    internal static async Task<Assessment> Assess(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, DateOnly from, DateOnly to, DateTimeOffset now, CancellationToken ct) {
        var checks = new List<DepartureCheck>();
        checks.AddRange(await Crew(h, db, a, vessel, from, to, ct));
        checks.AddRange(await Technical(h, db, a, vessel, now, ct));
        checks.AddRange(await Statutory(h, db, a, vessel, to, ct));
        // No emergency preparedness / drill source module exists yet: always UNKNOWN, never assumed ready.
        checks.Add(new("emergency", U, "EMERGENCY_SOURCE_UNAVAILABLE"));
        var mode = DepartureReadinessEngine.Mode(from, to, DateOnly.FromDateTime(now.UtcDateTime));
        var all = checks.ToArray();
        return new(DepartureReadinessEngine.Evaluate(all, from, to, mode), all);
    }

    // DEV-050 shared crew engine (SOLAS V/14, STCW): vessel safe manning evaluation over the voyage window, and the
    // latest human Safe Manning decision only when it is bound to this exact evaluation hash and window.
    internal static async Task<DepartureCheck[]> Crew(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, DateOnly from, DateOnly to, CancellationToken ct) {
        if (!CrewEligibilityEvaluator.CanEvaluate(a)) return [new("crew", U, "CREW_ACCESS_REQUIRED")];
        SafeManningEvaluation e;
        try { e = await h.RequestServices.GetRequiredService<ICrewEligibilityEvaluator>().EvaluateVesselAsync(a, vessel, new(from, to), ct); }
        catch (InvalidDataException) { return [new("crew", U, "CREW_EVALUATION_UNAVAILABLE")]; }
        var latest = await db.CrewSafeManningDecisions.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.Sequence).FirstOrDefaultAsync(ct);
        var bound = latest != null && string.Equals(latest.EvaluationHash, e.Hash, StringComparison.OrdinalIgnoreCase) && latest.WindowFrom == from && latest.WindowTo == to ? latest : null;
        var result = new List<DepartureCheck>();
        var accepted = bound?.Decision == "MANNING_ACCEPTED" && e.State != CrewEligibilityStates.Eligible;
        if (bound != null && e.State != CrewEligibilityStates.Eligible)
            result.Add(bound.Decision switch {
                "MANNING_ACCEPTED" => new("crew", C, "SAFE_MANNING_ACCEPTED_BY_HUMAN_DECISION", "SafeManningDecision", bound.Id, bound.Sequence),
                "MANNING_NOT_ACCEPTED" => new("crew", F, "SAFE_MANNING_NOT_ACCEPTED_BY_HUMAN_DECISION", "SafeManningDecision", bound.Id, bound.Sequence),
                _ => new("crew", U, "SAFE_MANNING_DECISION_ON_HOLD", "SafeManningDecision", bound.Id, bound.Sequence)
            });
        result.Add(e.State switch {
            CrewEligibilityStates.Eligible => new("crew", P, "SAFE_MANNING_ELIGIBLE", "SafeManningEvaluation", e.MsmdId, e.MsmdRevision),
            CrewEligibilityStates.Attention => new("crew", C, "SAFE_MANNING_ATTENTION", "SafeManningEvaluation", e.MsmdId, e.MsmdRevision),
            CrewEligibilityStates.NotEligible => new("crew", accepted ? C : F, "SAFE_MANNING_NOT_ELIGIBLE", "SafeManningEvaluation", e.MsmdId, e.MsmdRevision),
            _ => new("crew", accepted ? C : U, "SAFE_MANNING_REVIEW_REQUIRED", "SafeManningEvaluation", e.MsmdId, e.MsmdRevision)
        });
        string? Map(string severity) => severity switch {
            CrewEligibilityStates.NotEligible => accepted ? C : F, CrewEligibilityStates.ReviewRequired => accepted ? C : U,
            CrewEligibilityStates.Attention => C, _ => null };
        foreach (var r in e.Reasons.Take(128))
            if (Map(r.Severity) is { } mapped) result.Add(new("crew", mapped, "SAFE_MANNING:" + Code(r.Code), "SafeManningReason", r.SourceId));
        foreach (var m in e.Members.Where(m => m.State != CrewEligibilityStates.Eligible).Take(256))
            if (Map(m.State) is { } mapped) result.Add(new("crew", mapped, "CREW_MEMBER_" + Code(m.State), "CrewAssignment", m.AssignmentId));
        return result.ToArray();
    }

    // DEV-055/056/109 technical input: the PMS command center sections (critical overdue jobs, open work orders,
    // open critical defects, critical spare gaps) and PMS postponement readiness impact. CRITICAL -> FAIL.
    internal static async Task<DepartureCheck[]> Technical(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, DateTimeOffset now, CancellationToken ct) {
        if (!PmsCommandCenterEndpoints.CanRead(a)) return [new("technical", U, "TECHNICAL_ACCESS_REQUIRED")];
        var (sections, plans) = await PmsCommandCenterEndpoints.Compose(h, db, a, vessel, now, ct);
        var result = new List<DepartureCheck>();
        foreach (var s in sections) {
            var key = Code(s.Key);
            if (s.State is PmsCommandCenterPolicy.Unknown or PmsCommandCenterPolicy.AccessRequired) { result.Add(new("technical", U, key + ":" + Code(s.Reason ?? "SOURCE_UNAVAILABLE"))); continue; }
            if (s.State == PmsCommandCenterPolicy.Quiet) result.Add(new("technical", P, key + ":NO_OPEN_EXCEPTIONS"));
            foreach (var item in s.Items.Take(256))
                result.Add(new("technical", item.Severity switch { "CRITICAL" => F, "ATTENTION" => C, _ => U },
                    key + ":" + Code(item.Why.FirstOrDefault()?.Code ?? "EXCEPTION"), item.Kind, item.SourceId, item.SourceRevision));
            if (s.Items.Length > 256) result.Add(new("technical", U, key + ":PROJECTION_LIMIT_EXCEEDED"));
            if (s.Freshness == PmsCommandCenterPolicy.Stale) result.Add(new("technical", U, key + ":SOURCE_STALE"));
        }
        foreach (var p in plans ?? []) {
            if (p.Row.Postponement is not { } pp) continue;
            var mapped = pp.ReadinessImpact switch {
                MaintenancePostponementPolicy.ReviewRequired => U,
                MaintenancePostponementPolicy.ConditionalDeferred or MaintenancePostponementPolicy.PendingDecision => C,
                _ => null };
            if (mapped != null) result.Add(new("technical", mapped, "PMS_POSTPONEMENT:" + Code(pp.ReadinessImpact), "MaintenancePostponement", pp.Id));
        }
        return result.ToArray();
    }

    // Statutory: the vessel certificate register (DEV-042..044) assessed through the voyage end date; validity counts
    // only with a current human verification assertion. The required-certificate inventory is not established by
    // any source, so completeness stays UNKNOWN (expert/applicability gate), never assumed.
    internal static async Task<DepartureCheck[]> Statutory(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, DateOnly to, CancellationToken ct) {
        if (!CertificateReader.Read(a)) return [new("statutory", U, "CERTIFICATE_ACCESS_REQUIRED")];
        var result = new List<DepartureCheck> { new("statutory", U, "CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED") };
        var rows = await CertificateReader.Visible(db, a, vessel).OrderBy(r => r.CertificateId).Take(CertificateLimit + 1).ToArrayAsync(ct);
        if (rows.Length > CertificateLimit) { result.Add(new("statutory", U, "CERTIFICATE_PROJECTION_LIMIT_EXCEEDED")); return result.ToArray(); }
        CertificateDto[] records;
        try { records = await CertificateReader.Superseded(db, vessel, await CertificateAssertions.Decorate(h, db, a, rows.Select(r => CertificateReader.Dto(r, a, to)).ToArray(), ct), ct); }
        catch (Exception x) when (x is InvalidDataException or JsonException) { result.Add(new("statutory", U, "CERTIFICATE_CONTENT_UNAVAILABLE")); return result.ToArray(); }
        var current = records.Where(r => r.State != "SUPERSEDED").ToArray();
        if (current.Length == 0) result.Add(new("statutory", U, "NO_VESSEL_CERTIFICATES_RECORDED"));
        foreach (var r in current) {
            var check = r.Calendar.State switch {
                "EXPIRED" => new DepartureCheck("statutory", F, "CERTIFICATE_EXPIRES_BEFORE_VOYAGE_END"),
                "REVIEW_REQUIRED" => new DepartureCheck("statutory", U, "CERTIFICATE_DATES_REVIEW_REQUIRED"),
                "DUE" => new DepartureCheck("statutory", C, "CERTIFICATE_DUE_WITHIN_90_DAYS"),
                "SURVEY_WINDOW" => new DepartureCheck("statutory", C, "CERTIFICATE_SURVEY_WINDOW_OPEN"),
                "VALID" => r.Verification switch {
                    { Current: true, State: "Verified" } => new DepartureCheck("statutory", P, "CERTIFICATE_VALID_VERIFIED"),
                    { Current: true, State: "Not Verified" or "Suspended / Revoked" } => new DepartureCheck("statutory", F, "CERTIFICATE_VERIFICATION_NEGATIVE"),
                    { Current: true } => new DepartureCheck("statutory", U, "CERTIFICATE_VERIFICATION_SUSPICIOUS"),
                    _ => new DepartureCheck("statutory", U, "CERTIFICATE_HUMAN_VERIFICATION_MISSING")
                },
                _ => new DepartureCheck("statutory", U, "CERTIFICATE_STATE_UNRECOGNIZED")
            };
            result.Add(check with { SourceKind = "VesselCertificate", SourceId = r.Id, SourceRevision = r.Revision });
        }
        return result.ToArray();
    }

    internal static string Snapshot(DepartureOutcome o) => JsonSerializer.Serialize(new {
        o.State, o.Mode, categories = o.Categories.Select(c => new { c.Category, c.Result }),
        blockers = o.CriticalBlockers.Take(256), review = o.ReviewReasons.Take(256), conditions = o.Conditions.Take(256) });
}

// DEV-058 GET /api/v1/departure-readiness and DEV-059 POST /api/v1/departure-signoff (15.01 paragraphs 1203-1213).
// The readiness read is advisory; the sign-off is the Master's human record bound to the assessment hash. A Master
// sign-off is not an external commitment or official departure clearance (60.01 p220) and changes no other state.
public static class DepartureReadinessEndpoints
{
    private const string ReadAction = "DepartureReadinessRead", CommandAction = "DepartureSignoffCommand", ReceiptAction = "DepartureSignoffRecord";

    public static void MapDepartureReadiness(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1").RequireAuthorization("DirectoryRead");
        g.MapGet("/departure-readiness", Readiness).WithName("DepartureReadinessRead").Produces<DepartureReadinessView>();
        g.MapPost("/departure-signoff", Signoff).WithName("DepartureSignoffRecord").Accepts<DepartureSignoffCommand>("application/json")
            .Produces<DepartureDecisionDto>().Produces(422);
    }

    private static DepartureDecisionDto Dto(DepartureDecision d, string validity, bool replayed = false) => new(d.Id, d.VesselId, d.Sequence, d.PreviousDecisionId, d.Decision,
        d.AssessedState, d.AssessmentMode, d.AssessmentHash, d.WindowFrom, d.WindowTo, d.Reason, d.Duty, d.ActorId, d.RecordedAt, validity, replayed);

    private static Task<bool> Ship(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        a.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == vessel && v.Kind == VesselKind.Ship, ct);

    private static bool Date(HttpContext h, string key, out DateOnly value) {
        value = default;
        return h.Request.Query.TryGetValue(key, out var raw) && raw.Count == 1 &&
            DateOnly.TryParseExact(raw[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static async Task<IResult> Readiness(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!DepartureAuthorities.CanRead(a)) return await TugReadinessHttp.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Any(q => q.Key is not ("vessel" or "from" or "to") || q.Value.Count != 1) ||
            !Guid.TryParseExact(h.Request.Query["vessel"], "D", out var vessel) || vessel == Guid.Empty || !Date(h, "from", out var from) || !Date(h, "to", out var to))
            return await TugReadinessHttp.Fail(h, ReadAction, 400, "invalid_query");
        var now = TugReadinessHttp.Now(h);
        if (!DepartureReadinessEngine.ValidWindow(from, to, DateOnly.FromDateTime(now.UtcDateTime))) return await TugReadinessHttp.Fail(h, ReadAction, 400, "departure_window_invalid");
        if (!await Ship(db, a, vessel, ct)) return await TugReadinessHttp.Fail(h, ReadAction, 404, "resource_not_found");
        var assessment = await DepartureReadinessAssessor.Assess(h, db, a, vessel, from, to, now, ct);
        var o = assessment.Outcome;
        var decisions = await db.DepartureDecisions.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.Sequence).Take(32).ToArrayAsync(ct);
        var grants = DepartureAuthorities.Resolve(h.RequestServices.GetRequiredService<FederationSettings>(), a, vessel, now);
        var allowed = DepartureDecisionPolicy.Decisions.Where(d => DepartureDecisionPolicy.Refusal(d, o.State, o.Mode, "0123456789") == null).ToArray();
        await TugReadinessHttp.Observe(h, ReadAction, "Success", vessel);
        return Results.Ok(new DepartureReadinessView(vessel, from, to, o.Mode, o.State, o.Categories, o.CriticalBlockers, o.ReviewReasons, o.Conditions, o.Why,
            o.Hash, now, now.AddSeconds(60),
            decisions.Select(d => Dto(d, DepartureDecisionPolicy.Validity(d.AssessmentHash, d.WindowFrom, d.WindowTo, o.Hash, from, to))).ToArray(),
            decisions.FirstOrDefault()?.Sequence ?? 0, DepartureAuthorities.Dto(grants), grants.Length > 0, allowed));
    }

    // The server re-evaluates the assessment; a changed hash is STALE (409); RELEASE needs CURRENT READY (422);
    // HOLD/DENY are always recordable with a reason. Lineage: Sequence = previous + 1 under SERIALIZABLE.
    private static async Task<IResult> Signoff(HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store";
        var a = TugReadinessHttp.Actor(h); var ct = h.RequestAborted;
        if (!DepartureAuthorities.CanRead(a)) return await TugReadinessHttp.Fail(h, CommandAction, 403, "permission_denied");
        if (h.Request.Query.Any(q => q.Key != "vessel" || q.Value.Count != 1) || !Guid.TryParseExact(h.Request.Query["vessel"], "D", out var vessel) || vessel == Guid.Empty)
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "invalid_request");
        var (command, key, failure) = await TugReadinessHttp.Read<DepartureSignoffCommand>(h, CommandAction);
        if (failure != null) return failure;
        if (command!.AuthorityId == Guid.Empty || command.ExpectedAssessmentHash is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigitUpper) ||
            command.ExpectedSequence < 0 || !DepartureDecisionPolicy.Decisions.Contains(command.Decision, StringComparer.Ordinal) || !DepartureDecisionPolicy.ValidReason(command.Reason))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "validation_failed", vessel);
        var now = TugReadinessHttp.Now(h);
        if (!DepartureReadinessEngine.ValidWindow(command.From, command.To, DateOnly.FromDateTime(now.UtcDateTime)))
            return await TugReadinessHttp.Fail(h, CommandAction, 400, "departure_window_invalid", vessel);
        if (!await Ship(db, a, vessel, ct)) return await TugReadinessHttp.Fail(h, CommandAction, 404, "resource_not_found");
        var grant = DepartureAuthorities.Resolve(h.RequestServices.GetRequiredService<FederationSettings>(), a, vessel, now).SingleOrDefault(g => g.Id == command.AuthorityId);
        if (grant == null) return await TugReadinessHttp.Fail(h, CommandAction, 403, "departure_authority_required", vessel);
        var fingerprint = TugReadinessHttp.Fingerprint(new { vessel, command });
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted", vessel);
        // Assessment reads run before the write transaction; the hash binds the decision to their exact result.
        var assessment = await DepartureReadinessAssessor.Assess(h, db, a, vessel, command.From, command.To, now, ct);
        var o = assessment.Outcome;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await db.DepartureReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != ReceiptAction || receipt.VesselId != vessel || receipt.Fingerprint != fingerprint)
                    return await TugReadinessHttp.Fail(h, CommandAction, 409, "idempotency_key_reused", vessel);
                var saved = await db.DepartureDecisions.AsNoTracking().SingleAsync(d => d.Id == receipt.RecordId && d.VesselId == vessel, ct);
                await TugReadinessHttp.Observe(h, CommandAction, "Success", vessel);
                return Results.Ok(Dto(saved, DepartureDecisionPolicy.Validity(saved.AssessmentHash, saved.WindowFrom, saved.WindowTo, o.Hash, command.From, command.To), true));
            }
            var previous = await db.DepartureDecisions.AsNoTracking().Where(d => d.VesselId == vessel).OrderByDescending(d => d.Sequence)
                .Select(d => new { d.Id, d.Sequence }).FirstOrDefaultAsync(ct);
            if ((previous?.Sequence ?? 0) != command.ExpectedSequence) return await TugReadinessHttp.Fail(h, CommandAction, 409, "decision_sequence_conflict", vessel);
            if (!string.Equals(o.Hash, command.ExpectedAssessmentHash, StringComparison.Ordinal)) return await TugReadinessHttp.Fail(h, CommandAction, 409, "assessment_stale", vessel);
            var refusal = DepartureDecisionPolicy.Refusal(command.Decision, o.State, o.Mode, command.Reason);
            if (refusal != null) return await TugReadinessHttp.Fail(h, CommandAction, refusal == "validation_failed" ? 400 : 422, refusal, vessel);
            var decision = new DepartureDecision { TenantId = a.TenantId, VesselId = vessel, Id = Guid.NewGuid(), Sequence = (previous?.Sequence ?? 0) + 1,
                PreviousDecisionId = previous?.Id, Decision = command.Decision, AssessedState = o.State, AssessmentMode = o.Mode, AssessmentHash = o.Hash,
                WindowFrom = command.From, WindowTo = command.To, SnapshotJson = DepartureReadinessAssessor.Snapshot(o), Reason = command.Reason,
                ActorId = a.Id, AuthorityId = grant.Id, RoleId = grant.RoleId, Duty = grant.Duty, AuthorityHash = DepartureAuthorities.Hash(grant, a), RecordedAt = now };
            db.DepartureDecisions.Add(decision);
            db.DepartureReceipts.Add(new() { TenantId = a.TenantId, VesselId = vessel, ActorId = a.Id, Key = key, Action = ReceiptAction, Fingerprint = fingerprint,
                RecordId = decision.Id, Sequence = decision.Sequence, CommittedAt = now });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            await TugReadinessHttp.Observe(h, CommandAction, "Success", vessel);
            return Results.Ok(Dto(decision, DepartureDecisionPolicy.CurrentValidity));
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return await TugReadinessHttp.Database(h, db, tx, CommandAction, e, vessel);
        }
    }
}
