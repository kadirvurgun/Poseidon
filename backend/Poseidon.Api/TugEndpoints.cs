using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TugSaveCommand(Guid ClientId, uint? BaseVersion, TugRequestInputs Inputs);
public sealed record TugRequestDto(Guid Id, Guid VesselId, string JobNumber, long Revision, uint Version,
    TugRequestInputs Inputs, string Provenance, DateTimeOffset SavedAt, string[] Missing, bool Replayed = false, string SourceVersion = TugRequestPolicy.SourceVersion);
public sealed record TugDeclaredComparison(Guid JobId, string Relation);
public sealed record TugBoardRow(TugRequestDto Request, string WindowState, string PlannedTimeSource,
    Guid[] AssignedTugs, string AssignmentCoverage, string? AvailableBollardPull, string Readiness,
    string Conflict, TugDeclaredComparison[] DeclaredComparisons, string ComparisonCoverage, string[] BlockingReasons, string NextAction, string DetailHref);

public static class TugEndpoints
{
    public static void MapTug(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1").RequireAuthorization("DirectoryRead");
        g.MapGet("/tug-command", Context).WithName("TugContext");
        g.MapGet("/tug-job-board", Board).WithName("TugBoard");
        g.MapGet("/tug-jobs/{id:guid}", Detail).WithName("TugDetail");
        // Raw bounded parser rejects duplicate JSON members as well as unknown keys.
        g.MapPost("/tug-jobs", Save).WithName("TugSave").Accepts<TugSaveCommand>("application/json").Produces<TugRequestDto>();
        g.MapPut("/tug-jobs/{id:guid}", Save).WithName("TugUpdate").Accepts<TugSaveCommand>("application/json").Produces<TugRequestDto>();
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Write(TrustedActor a, Guid vessel) => a.CanReadTug && a.HasWriteAttribution && a.Permissions.Contains("tug.write") &&
        (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));
    private static Task Observe(HttpContext h, string result, Guid? id = null) {
        h.Items["TugObserved"] = true;
        return h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, h.Request.Method == "GET" ? "TugRead" : "TugSave", result, resourceId: id);
    }
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await Observe(h, code); return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }
    private static IQueryable<Vessel> Tugs(PoseidonDbContext db, TrustedActor a) => a.Scope(db.Vessels).Where(v => v.Kind == VesselKind.Tug);
    private static Guid[] Owners(HttpContext h, Guid vessel) => h.RequestServices.GetRequiredService<FederationSettings>().Memberships
        .Where(m => m.TenantId == Actor(h).TenantId && m.ActorId != Guid.Empty && m.Permissions.Contains("tug.owner.eligible") &&
            m.Permissions.Contains("tug.read") && m.Permissions.Contains("mode.tug") && m.Permissions.Contains("directory.read") &&
            (m.TenantWideManage || m.TenantWideRead || (m.VesselGrants ?? []).Any(g => g.VesselId == vessel)) && ActorResolver.TrustedRoleContext(m.RoleReferences) != null)
        .SelectMany(m => m.RoleReferences!).Select(Guid.Parse).Distinct().Order().ToArray();
    private static async Task<(Guid Vessel, IResult? Failure)> Gate(HttpContext h, PoseidonDbContext db, bool write = false) {
        var a = Actor(h);
        if (!a.CanReadTug) return (default, await Fail(h, 403, "permission_denied"));
        var allowed = h.Request.Method == "GET" ? new[] { "vessel", "offset", "limit", "priority", "q" } : ["vessel"];
        if (h.Request.Query.Any(q => !allowed.Contains(q.Key) || q.Value.Count != 1) ||
            !Guid.TryParseExact(h.Request.Query["vessel"], "D", out var vessel) || vessel == Guid.Empty)
            return (default, await Fail(h, 400, "invalid_request"));
        if (!await Tugs(db, a).AnyAsync(v => v.Id == vessel, h.RequestAborted)) return (default, await Fail(h, 404, "resource_not_found"));
        if (write && !Write(a, vessel)) return (default, await Fail(h, 403, "permission_denied"));
        return (vessel, null);
    }
    private static TugRequestDto Dto(TugJob j) => new(j.Id, j.VesselId, j.JobNumber, j.Revision, j.Version,
        j.Inputs(), j.Provenance, j.SavedAt, TugRequestPolicy.Missing(j.Inputs()), SourceVersion: j.SourceVersion);
    private static TugRequestDto Dto(TugJobRevision r, uint version, bool replayed) {
        var inputs = JsonSerializer.Deserialize<TugRequestInputs>(r.ContentJson)!;
        return new(r.JobId, r.VesselId, r.JobNumber, r.Revision, version, inputs, r.Provenance, r.SavedAt, TugRequestPolicy.Missing(inputs), replayed, r.SourceVersion);
    }
    private static async Task<IResult> Context(HttpContext h, PoseidonDbContext db) {
        var a = Actor(h);
        if (!a.CanReadTug) return await Fail(h, 403, "permission_denied");
        if (!h.Request.Query.ContainsKey("vessel")) {
            if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_request");
            var vessels = await Tugs(db, a).AsNoTracking().OrderBy(v => v.Code).Select(v => new { v.Id, v.Code, v.Name }).ToArrayAsync(h.RequestAborted);
            await Observe(h, "Success"); return Results.Ok(new { vessels });
        }
        var (vessel, failure) = await Gate(h, db); if (failure != null) return failure;
        var v = await Tugs(db, a).AsNoTracking().SingleAsync(v => v.Id == vessel, h.RequestAborted);
        var bp = await db.TugProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == vessel, h.RequestAborted);
        var assets = a.Permissions.Contains("equipment.read") ? await db.Assets.AsNoTracking().Where(x => x.VesselId == vessel)
            .Select(x => new { x.Id, x.Level, x.EquipmentType, x.Revision, x.Provenance }).ToArrayAsync(h.RequestAborted) : null;
        await Observe(h, "Success", vessel);
        return Results.Ok(new { vessel = new { v.Id, v.Code, v.Name }, mode = "Tug", crossMode = a.Permissions.Contains("mode.cross"),
            roleReferences = a.RoleContext, canWrite = Write(a, vessel), owners = Owners(h, vessel),
            bollardPull = new { value = bp?.BollardPullTonnes.ToString(CultureInfo.InvariantCulture), version = bp?.Version,
                provenance = bp == null ? "Missing" : "LegacyMeasurementUnverified", source = "TugProfile", observedAt = (DateTimeOffset?)null, freshness = "Unknown" },
            tugType = "Unknown", equipment = assets, equipmentCoverage = assets == null ? "Unavailable" : "DraftDeclarationsOnly",
            readiness = "Unknown", reason = "VerifiedMeasurementCertificateCrewAndAssignmentAdaptersUnavailable", jobTypes = TugRequestPolicy.JobTypes, priorities = TugRequestPolicy.Priorities, requestSources = TugRequestPolicy.RequestSources });
    }
    private static async Task<IResult> Detail(Guid id, HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await Gate(h, db); if (failure != null) return failure;
        var row = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.VesselId == vessel, h.RequestAborted);
        if (row == null) return await Fail(h, 404, "resource_not_found");
        await Observe(h, "Success", id); return Results.Ok(Dto(row));
    }
    private static async Task<IResult> Board(HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await Gate(h, db); if (failure != null) return failure;
        int ReadInt(string key, int fallback) => h.Request.Query.ContainsKey(key) && int.TryParse(h.Request.Query[key], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : h.Request.Query.ContainsKey(key) ? -1 : fallback;
        var offset = ReadInt("offset", 0); var limit = ReadInt("limit", 25); var priority = h.Request.Query["priority"].ToString(); var q = h.Request.Query["q"].ToString();
        if (offset is < 0 or > 100000 || limit is < 1 or > 100 || q.Length > 100 || q.Any(char.IsControl) || priority != "" && !TugRequestPolicy.Priorities.Contains(priority))
            return await Fail(h, 400, "invalid_request");
        var jobs = await db.TugJobs.AsNoTracking().Where(j => j.VesselId == vessel && (priority == "" || j.Priority == priority) &&
            (q == "" || j.JobNumber.Contains(q) || j.AssistedVessel != null && j.AssistedVessel.Contains(q)))
            .OrderBy(j => j.RequestedStart).ThenBy(j => j.Id).Skip(offset).Take(limit + 1).ToArrayAsync(h.RequestAborted);
        var ids = jobs.Take(limit).Select(j => j.Id).ToArray();
        var authorizedTugs = Tugs(db, Actor(h)).Select(v => v.Id);
        var assignments = await db.TugAssignments.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.JobId) && authorizedTugs.Contains(x.TugId)).ToArrayAsync(h.RequestAborted);
        var rows = jobs.Take(limit).Select(j => {
            var actual = assignments.Where(x => x.JobId == j.Id && x.JobRevision == j.Revision).Select(x => x.TugId).Distinct().ToArray();
            var missing = TugRequestPolicy.Missing(j.Inputs());
            return new TugBoardRow(Dto(j), TugPlanningWindowPolicy.Assess(new(j.RequestedStart, j.RequestedEnd)).ToString(),
                "RequestedWindowOnly;NoApprovedPlan", actual, "UnavailableControlledAssignmentAdapter", null, "Unknown", "Unknown",
                jobs.Take(limit).Where(other => other.Id != j.Id).Select(other => new TugDeclaredComparison(other.Id, TugPlanningWindowPolicy.CompareDeclared(new(j.RequestedStart, j.RequestedEnd), new(other.RequestedStart, other.RequestedEnd)).ToString())).ToArray(), "VisiblePageEnteredDataOnly;NoBufferOrOperationalAssessment",
                [.. missing.Select(x => "Missing:" + x), "AssignmentCoverageUnavailable", "VerifiedBPCertificateCrewUnavailable", "OperationalConflictInputsUnavailable", "D-STATE-01"],
                missing.Length > 0 ? "CompleteDraft" : "AuthorizedPlanningReviewRequired", $"/app/tug-job-detail/{j.Id}?vessel={vessel}");
        }).ToArray();
        await Observe(h, "Success", vessel); return Results.Ok(new PageDto<TugBoardRow>(rows, offset, limit, jobs.Length > limit));
    }
    private static bool Unique(JsonElement e) => e.ValueKind switch {
        JsonValueKind.Object => e.EnumerateObject().Select(p => p.Name.ToUpperInvariant()).Distinct().Count() == e.EnumerateObject().Count() && e.EnumerateObject().All(p => Unique(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().All(Unique), _ => true
    };
    private static async Task<IResult> Save(HttpContext h, PoseidonDbContext db) {
        var (vessel, failure) = await Gate(h, db, true); if (failure != null) return failure;
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await Fail(h, 400, "csrf_invalid");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await Fail(h, 400, "invalid_idempotency_key");
        var key = parsedKey.ToString("D");
        if (!h.Request.HasJsonContentType() || h.Request.ContentLength > 16384) return await Fail(h, 400, "invalid_request");
        TugSaveCommand command;
        try {
            using var buffer = new MemoryStream(); var bytes = new byte[4096]; int count;
            while ((count = await h.Request.Body.ReadAsync(bytes, h.RequestAborted)) > 0) {
                if (buffer.Length + count > 16384) return await Fail(h, 400, "invalid_request"); buffer.Write(bytes, 0, count);
            }
            using var doc = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 5 });
            if (!Unique(doc.RootElement)) return await Fail(h, 400, "invalid_request");
            // Offset is explicit; never interpret a zone-free local datetime using the server clock zone.
            foreach (var p in doc.RootElement.GetProperty("inputs").EnumerateObject().Where(p => (p.Name is "requestedStart" or "requestedEnd") && p.Value.ValueKind != JsonValueKind.Null)) {
                if (p.Value.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(p.Value.GetString()!, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$")) return await Fail(h, 400, "validation_failed");
            }
            command = doc.RootElement.Deserialize<TugSaveCommand>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false }) ?? throw new JsonException();
        } catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { return await Fail(h, 400, "invalid_request"); }
        if (command.ClientId == Guid.Empty || command.Inputs == null || command.BaseVersion == 0 || TugRequestPolicy.Invalid(command.Inputs).Length != 0) return await Fail(h, 400, "validation_failed");
        var update = h.Request.Method == "PUT";
        if (update != (command.BaseVersion != null) || update && h.Request.RouteValues["id"]?.ToString() != command.ClientId.ToString()) return await Fail(h, 400, "validation_failed");
        // Authorize referenced owner on every attempt, including a replay after revocation.
        if (command.Inputs.RequestOwner is Guid owner && !Owners(h, vessel).Contains(owner)) return await Fail(h, 403, "permission_denied");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, command })));
        var a = Actor(h); var ct = h.RequestAborted;
        await Observe(h, "Attempted", command.ClientId);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == a.Id && r.Key == key, ct);
            if (receipt != null) {
                if (receipt.Action != "TugSave" || receipt.ResourceId != command.ClientId || receipt.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
                var revision = await db.TugJobRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.JobId == receipt.ResourceId && r.VesselId == vessel && r.Revision == receipt.RegisterRevision, ct);
                if (revision == null) return await Fail(h, 409, "source_revision_unavailable");
                await Observe(h, "Success", revision.JobId); return Results.Ok(Dto(revision, receipt.RegisterVersion!.Value, true));
            }
            var job = await db.TugJobs.SingleOrDefaultAsync(j => j.Id == command.ClientId, ct);
            if (job != null && job.VesselId != vessel) return await Fail(h, 404, "resource_not_found");
            if (job == null && update || job != null && (!update || job.Version != command.BaseVersion)) return await Fail(h, 409, "version_conflict");
            if (job == null) {
                job = new() { TenantId = a.TenantId, VesselId = vessel, Id = command.ClientId, JobNumber = "TUG-" + Guid.NewGuid().ToString("N") };
                db.TugJobs.Add(job);
            } else job.Revision = checked(job.Revision + 1);
            if (command.Inputs.RequestOwner is Guid role && !await db.TugOwners.AnyAsync(o => o.VesselId == vessel && o.Id == role, ct)) db.TugOwners.Add(new() { TenantId = a.TenantId, VesselId = vessel, Id = role });
            job.Apply(command.Inputs); job.Provenance = "UserDeclaredUnverified";
            var snapshot = job.Snapshot(); db.TugJobRevisions.Add(snapshot); await db.SaveChangesAsync(ct);
            db.CommandReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, Action = "TugSave", Fingerprint = fingerprint,
                ResourceId = job.Id, RegisterRevision = job.Revision, RegisterVersion = job.Version, CommittedAt = job.SavedAt });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Observe(h, "Success", job.Id);
            return Results.Ok(Dto(snapshot, job.Version, false));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict");
        }
    }
}
