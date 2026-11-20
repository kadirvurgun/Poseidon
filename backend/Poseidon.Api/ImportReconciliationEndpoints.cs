using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned appointment of one human as the customer data owner for one vessel's migrated data (18.03 p24
// "Data owners assigned", p102 "Customer data owner signs reconciliation"). Never derived from a request field, an
// IdP role name, tenant-wide manage, membership administration or the import permissions.
public sealed record DataOwnerAuthority(Guid Id, Guid VesselId, Guid RoleId, string Duty, string AppointmentSource,
    DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record DataOwnerAuthorityDto(Guid Id, string Duty);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReconciliationReportCreateCommand([property: JsonRequired] Guid Id, [property: JsonRequired] Guid[] BatchIds, [property: JsonRequired] int SampleSize);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReconciliationSignOffCommand([property: JsonRequired] uint ExpectedVersion, [property: JsonRequired] string ReportHash,
    [property: JsonRequired] string Decision, [property: JsonRequired] string Reason, ReconciliationException[]? Exceptions,
    Guid[]? SampleVerified, [property: JsonRequired] Guid AuthorityId);

public sealed record ReconciliationSignOffDto(Guid Id, string Decision, string Reason, ReconciliationException[] Exceptions, Guid[] SampleVerified,
    string Duty, Guid SignedBy, DateTimeOffset SignedAt, string ReportHash, string Validity);
public sealed record ReconciliationReportDto(Guid Id, Guid VesselId, long Revision, uint Version, string State, string PolicyVersion, string CriticalFieldVersion,
    ReconciliationBatchFacts[] Batches, ReconciliationCounts Counts, MissingCriticalItem[] MissingCritical, bool MissingCriticalTruncated,
    string SampleSeed, ReconciliationTarget[] Sample, string Hash, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? SupersededAt, string Provenance);
public sealed record ReconciliationReportView(ReconciliationReportDto Report, ReconciliationSignOffDto? SignOff, bool Stale, string[] OpenItems,
    DataOwnerAuthorityDto[] Authorities, bool CanSignOff, string[] SignOffBlockers, string[] Decisions, string[] ExceptionKinds,
    string SourceLocator = ImportReconciliationPolicy.SourceLocator);
public sealed record ReconciliationReportSummaryDto(Guid Id, string State, long Revision, ReconciliationCounts Counts, DateTimeOffset CreatedAt,
    string? Decision, string SignOffValidity);
public sealed record ReconciliationCandidateBatchDto(Guid Id, string Category, string State, int RowCount, DateTimeOffset CreatedAt, DateTimeOffset? CommittedAt, string TemplateVersion);
public sealed record ReconciliationListView(Guid VesselId, ReconciliationReportSummaryDto[] Items, ReconciliationCandidateBatchDto[] Batches,
    bool CanCreate, DataOwnerAuthorityDto[] Authorities, IReadOnlyDictionary<string, string> TemplateVersions, int MaxBatches, int MaxSample,
    string SourceLocator = ImportReconciliationPolicy.SourceLocator);

internal static class DataOwnerAuthorities
{
    internal static DataOwnerAuthority[] Resolve(FederationSettings settings, TrustedActor? actor, Guid vessel, DateTimeOffset now) {
        if (actor == null || !actor.CanRead || !actor.HasWriteAttribution || actor.RoleContext == null || !actor.Permissions.Contains("import.reconciliation.sign")) return [];
        // Identity/membership administrators never sign data acceptance (platform administration is not data ownership).
        if (actor.Permissions.Contains("membership.manage")) return [];
        var members = settings.Memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (members.Length != 1 || !string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal)) return [];
        var grants = members[0].DataOwnerAuthorities ?? [];
        if (grants.Length > 256) return [];
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 && g.VesselId == vessel && vessel != Guid.Empty &&
            string.Equals(g.Duty, ImportReconciliationPolicy.DataOwnerDuty, StringComparison.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            g.AppointmentSource is { Length: > 0 and <= 200 } source && source.Trim() == source && !source.Any(char.IsControl) &&
            g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
    internal static DataOwnerAuthorityDto[] Dto(DataOwnerAuthority[] grants) => grants.Select(g => new DataOwnerAuthorityDto(g.Id, g.Duty)).ToArray();
    internal static string Hash(DataOwnerAuthority grant, TrustedActor actor) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = actor.Id, tenantId = actor.TenantId, actorKind = "Human" }));
}

// DEV-069 lineage helpers used inside the import commit transaction (ordinary audited SaveChanges path only).
internal static class ImportLineage
{
    internal static string Kind(string category) => category switch { "vessel" => "Vessel", "crew" => "CrewAssignment", "certificate" => "Credential", _ => "Asset" };

    internal static Task<bool> Exists(PoseidonDbContext db, ImportBatch b, Guid vessel, Guid id, CancellationToken ct) => b.Category switch {
        "vessel" => Task.FromResult(true),
        "crew" => db.CrewAssignments.AsNoTracking().AnyAsync(x => x.Id == id && x.VesselId == vessel, ct),
        "certificate" => db.Credentials.AsNoTracking().AnyAsync(x => x.Id == id && x.VesselId == vessel && x.AssignmentId == b.AssignmentId, ct),
        _ => db.Assets.AsNoTracking().AnyAsync(x => x.Id == id && x.VesselId == vessel, ct)
    };

    private static async Task<(long Revision, string Kind)> Revision(PoseidonDbContext db, string category, Guid vessel, Guid id, CancellationToken ct) => category switch {
        "vessel" => ((long)await db.Vessels.AsNoTracking().Where(x => x.Id == vessel).Select(x => x.Version).SingleAsync(ct), "RowVersion"),
        "crew" => (await db.CrewAssignments.AsNoTracking().Where(x => x.Id == id && x.VesselId == vessel).Select(x => x.Revision).SingleAsync(ct), "Revision"),
        "certificate" => (await db.Credentials.AsNoTracking().Where(x => x.Id == id && x.VesselId == vessel).Select(x => x.Revision).SingleAsync(ct), "Revision"),
        _ => (await db.Assets.AsNoTracking().Where(x => x.Id == id && x.VesselId == vessel).Select(x => x.Revision).SingleAsync(ct), "Revision")
    };

    internal static async Task Record(PoseidonDbContext db, TrustedActor a, ImportBatch b, ImportPreview p, IEnumerable<(int Row, Guid Target, bool Existed)> rows,
        DateTimeOffset now, CancellationToken ct) {
        foreach (var (row, target, existed) in rows) {
            var (revision, kind) = await Revision(db, b.Category, b.VesselId, target, ct);
            db.ImportProvenance.Add(new() { TenantId = b.TenantId, VesselId = b.VesselId, BatchId = b.Id, PreviewId = p.Id, RowNumber = row, Category = b.Category,
                SourceHash = b.SourceHash, TemplateVersion = b.TemplateVersion, TemplateFingerprint = b.TemplateFingerprint, TargetKind = Kind(b.Category), TargetId = target,
                TargetRevision = revision, RevisionKind = kind, Operation = existed ? "UPDATE" : "CREATE", ActorId = a.Id, RecordedAt = now });
        }
    }

    internal static async Task Supersede(PoseidonDbContext db, Guid vessel, DateTimeOffset now, CancellationToken ct) {
        foreach (var r in await db.ReconciliationReports.Where(x => x.VesselId == vessel && x.SupersededAt == null).ToListAsync(ct)) {
            r.State = ImportReconciliationPolicy.Superseded; r.SupersededAt = now; r.Revision = checked(r.Revision + 1);
        }
    }
}

// DEV-070 /api/v1/vessels/{vessel}/reconciliation-reports. A report pins an explicit batch set and stores an immutable,
// hash-addressed snapshot of explainable counts, the missing-critical list and a deterministic sample. Creating one is a
// preparation act; the sign-off is a separate human record by an appointed data owner who neither authored the report
// nor uploaded a pinned batch. A newer committed import or report supersedes the report and invalidates its sign-off.
public static class ImportReconciliationEndpoints
{
    private const string ReadAction = "ImportReconciliationRead", CommandAction = "ImportReconciliationCommand";
    private const int MaxBody = 65536;

    internal static bool CanRead(TrustedActor? a) => a != null && a.CanRead && (a.Permissions.Contains("import.read") || a.Permissions.Contains("import.reconciliation.sign"));
    internal static bool CanCreate(TrustedActor a, Guid vessel) => a.CanRead && a.HasWriteAttribution && a.Permissions.Contains("import.read") && a.Permissions.Contains("import.write") &&
        (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));

    public static void MapImportReconciliation(this IEndpointRouteBuilder app) {
        var g = app.MapGroup("/api/v1/vessels/{vessel:guid}/reconciliation-reports").RequireAuthorization("DirectoryRead");
        g.MapGet("", List).WithName("ReconciliationReportList").Produces<ReconciliationListView>();
        g.MapGet("/{id:guid}", Detail).WithName("ReconciliationReportRead").Produces<ReconciliationReportView>();
        g.MapPost("", (Guid vessel, HttpContext h, PoseidonDbContext db) => Run<ReconciliationReportCreateCommand>(h, db, vessel, "ReconciliationReportCreate", null,
                (a, v) => CanCreate(a, v), c => c.Id, Create))
            .WithName("ReconciliationReportCreate").Accepts<ReconciliationReportCreateCommand>("application/json").Produces<ReconciliationReportView>();
        g.MapPost("/{id:guid}/sign-off", (Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) => Run<ReconciliationSignOffCommand>(h, db, vessel, "ReconciliationSignOff", id,
                (a, _) => a.Permissions.Contains("import.reconciliation.sign"), _ => id, SignOff))
            .WithName("ReconciliationSignOff").Accepts<ReconciliationSignOffCommand>("application/json").Produces<ReconciliationReportView>();
    }

    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static DateTimeOffset Now(HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static Task<bool> InScope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) => a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct);

    // Same closed envelope as the S8/S9 field commands, with its own receipt ledger and a route-bound vessel: exact
    // vessel scope, CSRF, one UUID Idempotency-Key (a replay re-reads the committed resource), bounded strict JSON, one
    // SERIALIZABLE transaction classified only through PostgresFailure, and attributed EF audit.
    private static async Task<IResult> Run<T>(HttpContext h, PoseidonDbContext db, Guid vessel, string operation, Guid? routeId,
        Func<TrustedActor, Guid, bool> allowed, Func<T, Guid?> resourceOf, Func<TowageContext<T>, Task<TowageOutcome>> apply) where T : class {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        if (h.Request.Query.Count > 0) return await TowageCommands.Fail(h, CommandAction, 400, "invalid_query", routeId);
        var actor = Actor(h);
        if (actor == null || !CanRead(actor) || !actor.HasWriteAttribution || db.TenantId != actor.TenantId) return await TowageCommands.Fail(h, CommandAction, 403, "permission_denied", routeId);
        if (!await InScope(db, actor, vessel, ct)) return await TowageCommands.Fail(h, CommandAction, 404, "resource_not_found", routeId);
        if (!allowed(actor, vessel)) return await TowageCommands.Fail(h, CommandAction, 403, "permission_denied", routeId);
        if (h.Request.Headers["X-CSRF-TOKEN"].Count != 1) return await TowageCommands.Fail(h, CommandAction, 400, "csrf_invalid", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await TowageCommands.Fail(h, CommandAction, 400, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || !Guid.TryParseExact(keys[0], "D", out var parsedKey) || parsedKey == Guid.Empty) return await TowageCommands.Fail(h, CommandAction, 400, "invalid_idempotency_key", routeId);
        var key = parsedKey.ToString("D");
        T command;
        try {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength > MaxBody) throw new JsonException();
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await h.Request.Body.ReadAsync(chunk, ct)) > 0) {
                if (buffer.Length + count > MaxBody) throw new JsonException();
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command = document.RootElement.Deserialize<T>(FieldOpsJson.Options) ?? throw new JsonException();
        } catch (Exception ex) when (ex is JsonException or InvalidDataException or InvalidOperationException or FormatException or NotSupportedException) {
            return await TowageCommands.Fail(h, CommandAction, 400, "invalid_request", routeId);
        }
        var resource = resourceOf(command) ?? routeId ?? Guid.Empty;
        if (resource == Guid.Empty) return await TowageCommands.Fail(h, CommandAction, 400, "invalid_request");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation, vessel, routeId, command }, FieldOpsJson.Options));
        async Task<IResult> Fresh(Guid id) {
            var live = Actor(h);
            if (live == null || !CanRead(live)) return await TowageCommands.Fail(h, CommandAction, 403, "permission_denied", id);
            db.ChangeTracker.Clear();
            var value = await ReadView(h, db, live, vessel, id, ct);
            if (value == null) return await TowageCommands.Fail(h, CommandAction, 503, "command_outcome_withheld", id);
            await TugReadinessHttp.Observe(h, CommandAction, "Success", id);
            return Results.Json(value, FieldOpsJson.Options);
        }
        await TugReadinessHttp.Observe(h, CommandAction, "Attempted", resource);
        Guid? committed = null; TowageOutcome? failure = null;
        await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)) {
            try {
                var receipt = await db.ImportReconciliationReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor.Id && r.Key == key, ct);
                if (receipt != null) {
                    await tx.RollbackAsync(CancellationToken.None);
                    if (receipt.Action != operation || receipt.Fingerprint != fingerprint || receipt.VesselId != vessel)
                        return await TowageCommands.Fail(h, CommandAction, 409, "idempotency_key_reused", resource);
                    return await Fresh(receipt.ResourceId);
                }
                var outcome = await apply(new(h, actor, vessel, routeId, command, Now(h), db, ct));
                if (outcome.Status != 200) { failure = outcome; await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
                else {
                    db.ImportReconciliationReceipts.Add(new() { TenantId = actor.TenantId, VesselId = vessel, ActorId = actor.Id, Key = key, Action = operation, Fingerprint = fingerprint,
                        ResourceId = outcome.ResourceId, Revision = Math.Max(1, outcome.Revision), CommittedAt = Now(h) });
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); committed = outcome.ResourceId;
                }
            } catch (DbUpdateConcurrencyException) {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); failure = TowageOutcome.Fail(409, "version_conflict");
            } catch (Exception ex) when (PostgresFailure.IsDatabase(ex)) {
                // Only serialization/deadlock (40001/40P01) and unique races (23505) are conflicts.
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                failure = PostgresFailure.IsConflict(ex) ? TowageOutcome.Fail(409, "version_conflict") : TowageOutcome.Fail(503, "service_unavailable");
            }
        }
        if (failure != null) return await TowageCommands.Fail(h, CommandAction, failure.Status, failure.Code!, resource, failure.Missing);
        try { return await Fresh(committed!.Value); }
        catch (Exception ex) when (ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)) {
            return Results.Json(new { code = "command_outcome_withheld", correlationId = h.TraceIdentifier }, statusCode: 503);
        }
    }

    private static bool Readable(HttpContext h, TrustedActor a, Guid vessel, string category) => ImportEndpoints.Category(a, vessel, category, false) &&
        (category != "crew" || h.RequestServices.GetRequiredService<CrewIdentityPolicy>().Allows(a, false));

    // Current explainable facts for an exact batch set; null when a batch is missing, foreign or unreadable for the actor.
    internal static async Task<(ReconciliationBatchFacts[] Batches, ReconciliationTarget[] Targets)?> Facts(HttpContext h, PoseidonDbContext db, TrustedActor a,
        Guid vessel, Guid[] ids, CancellationToken ct) {
        var batches = await db.ImportBatches.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.Category, x.Revision, x.State, x.SourceHash, x.TemplateFingerprint, x.RowCount, x.ActorId, x.CommittedPreviewId }).ToArrayAsync(ct);
        if (batches.Length != ids.Length || batches.Any(b => !Readable(h, a, vessel, b.Category))) return null;
        var facts = new List<ReconciliationBatchFacts>(); var targets = new List<ReconciliationTarget>();
        foreach (var b in batches.OrderBy(x => x.Id)) {
            var committed = b.State == "Committed" && b.CommittedPreviewId != null;
            var preview = committed
                ? await db.ImportPreviews.AsNoTracking().SingleOrDefaultAsync(p => p.Id == b.CommittedPreviewId && p.BatchId == b.Id, ct)
                : await db.ImportPreviews.AsNoTracking().Where(p => p.BatchId == b.Id && p.BatchRevision == b.Revision).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);
            var duplicates = preview == null ? 0 : await db.ImportRowResults.AsNoTracking()
                .CountAsync(r => r.PreviewId == preview.Id && r.Outcome == "rejected" && r.IssuesJson.Contains("\"code\":\"duplicate\""), ct);
            facts.Add(new(b.Id, b.Category, b.Revision, b.State, b.SourceHash, b.TemplateFingerprint, preview?.Id, b.RowCount,
                committed ? preview?.ValidRows ?? 0 : 0, committed ? 0 : preview?.RejectedRows ?? 0, duplicates, b.ActorId));
            if (committed)
                targets.AddRange(await db.ImportProvenance.AsNoTracking().Where(p => p.BatchId == b.Id).OrderBy(p => p.RowNumber)
                    .Select(p => new ReconciliationTarget(p.BatchId, p.RowNumber, p.TargetKind, p.TargetId)).ToArrayAsync(ct));
        }
        return (facts.ToArray(), targets.ToArray());
    }

    private static string? Day(DateOnly? d) => d?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // Current owning values of the critical fields; a target that is no longer present is listed as a missing record.
    private static async Task<MissingCriticalItem[]> Missing(PoseidonDbContext db, Guid vessel, ReconciliationBatchFacts[] batches, ReconciliationTarget[] targets, CancellationToken ct) {
        var items = new List<MissingCriticalItem>();
        var category = batches.ToDictionary(b => b.BatchId, b => b.Category);
        foreach (var group in targets.GroupBy(t => category[t.BatchId])) {
            var ids = group.Select(t => t.TargetId).Distinct().ToArray();
            var values = new Dictionary<Guid, Dictionary<string, string?>>();
            if (group.Key == "vessel")
                foreach (var v in await db.Vessels.AsNoTracking().Where(x => x.Id == vessel && ids.Contains(x.Id)).Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(ct))
                    values[v.Id] = new() { ["code"] = v.Code, ["name"] = v.Name };
            else if (group.Key == "crew")
                foreach (var c in await db.CrewAssignments.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.Id)).Select(x => new { x.Id, x.PositionId, x.From }).ToArrayAsync(ct))
                    values[c.Id] = new() { ["position_ref"] = c.PositionId?.ToString("D"), ["from"] = Day(c.From) };
            else if (group.Key == "certificate")
                foreach (var c in await db.Credentials.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.Id))
                    .Select(x => new { x.Id, x.CertificateTypeReference, x.Issuer, x.IssueDate, x.ExpiryDate, x.DocumentReference }).ToArrayAsync(ct))
                    values[c.Id] = new() { ["certificate_type"] = c.CertificateTypeReference, ["issuer"] = c.Issuer, ["issue_date"] = Day(c.IssueDate),
                        ["expiry_date"] = Day(c.ExpiryDate), ["document_ref"] = c.DocumentReference?.ToString("D") };
            else
                foreach (var e in await db.Assets.AsNoTracking().Where(x => x.VesselId == vessel && ids.Contains(x.Id)).Select(x => new { x.Id, x.Code, x.Name, x.Level, x.Criticality }).ToArrayAsync(ct))
                    values[e.Id] = new() { ["code"] = e.Code, ["name"] = e.Name, ["level"] = e.Level, ["criticality"] = e.Criticality };
            foreach (var t in group.OrderBy(t => t.BatchId).ThenBy(t => t.RowNumber))
                items.AddRange(values.TryGetValue(t.TargetId, out var v) ? ImportReconciliationPolicy.Missings(t, group.Key, v)
                    : [new MissingCriticalItem(t.BatchId, t.RowNumber, t.TargetKind, t.TargetId, "record", ImportReconciliationPolicy.Missing)]);
        }
        return items.ToArray();
    }

    private static ReconciliationBatchFacts[] Pins(ReconciliationReport r) => JsonSerializer.Deserialize<ReconciliationBatchFacts[]>(r.BatchesJson, FieldOpsJson.Options) ?? [];
    private static ReconciliationCounts Counts(ReconciliationReport r) => new(r.SourceCount, r.AcceptedCount, r.RejectedCount, r.DuplicateCount, r.NotCommittedCount, r.MissingCriticalCount);
    private static ReconciliationTarget[] SampleOf(ReconciliationReport r) => JsonSerializer.Deserialize<ReconciliationTarget[]>(r.SampleJson, FieldOpsJson.Options) ?? [];

    private static async Task<TowageOutcome> Create(TowageContext<ReconciliationReportCreateCommand> c) {
        var db = c.Db; var cmd = c.Command;
        if (cmd.Id == Guid.Empty || cmd.BatchIds == null || cmd.BatchIds.Length is 0 or > ImportReconciliationPolicy.MaxBatches || cmd.BatchIds.Any(b => b == Guid.Empty) ||
            cmd.BatchIds.Distinct().Count() != cmd.BatchIds.Length || cmd.SampleSize < 1 || cmd.SampleSize > ImportReconciliationPolicy.MaxSample)
            return TowageOutcome.Fail(400, "validation_failed");
        if (await db.ReconciliationReports.AnyAsync(r => r.Id == cmd.Id, c.Ct)) return TowageOutcome.Fail(409, "version_conflict");
        var facts = await Facts(c.Http, db, c.Actor, c.Vessel, cmd.BatchIds, c.Ct);
        if (facts == null) return TowageOutcome.Fail(422, "reconciliation_batch_unavailable");
        var (batches, targets) = facts.Value;
        var missing = await Missing(db, c.Vessel, batches, targets, c.Ct);
        var counts = ImportReconciliationPolicy.Count(batches, missing.Length);
        var seed = ImportReconciliationPolicy.Seed(cmd.Id);
        var sample = targets.Length == 0 ? [] : ImportReconciliationPolicy.Sample(seed, targets, Math.Min(cmd.SampleSize, targets.Length));
        var listed = missing.Take(ImportReconciliationPolicy.MaxMissingListed).ToArray();
        // A newer report supersedes the current one; saved first so the one-current-per-vessel index never collides.
        await ImportLineage.Supersede(db, c.Vessel, c.Now, c.Ct);
        await db.SaveChangesAsync(c.Ct);
        db.ReconciliationReports.Add(new() { TenantId = c.Actor.TenantId, VesselId = c.Vessel, Id = cmd.Id, BatchesJson = JsonSerializer.Serialize(batches, FieldOpsJson.Options),
            SourceCount = counts.Source, AcceptedCount = counts.Accepted, RejectedCount = counts.Rejected, DuplicateCount = counts.Duplicate, NotCommittedCount = counts.NotCommitted,
            MissingCriticalCount = counts.MissingCritical, MissingCriticalJson = JsonSerializer.Serialize(listed, FieldOpsJson.Options), SampleSeed = seed,
            SampleJson = JsonSerializer.Serialize(sample, FieldOpsJson.Options), Hash = ImportReconciliationPolicy.Hash(c.Vessel, cmd.Id, batches, counts, listed, sample, seed),
            CreatedBy = c.Actor.Id, CreatedAt = c.Now });
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(cmd.Id, 1);
    }

    // A report is stale when any pinned batch moved (new revision, preview, state or counts) after the snapshot.
    private static async Task<bool> Stale(HttpContext h, PoseidonDbContext db, TrustedActor a, ReconciliationReport r, CancellationToken ct) {
        var pins = Pins(r);
        var current = await Facts(h, db, a, r.VesselId, pins.Select(p => p.BatchId).ToArray(), ct);
        return current == null || !current.Value.Batches.OrderBy(b => b.BatchId).SequenceEqual(pins.OrderBy(b => b.BatchId));
    }

    private static async Task<TowageOutcome> SignOff(TowageContext<ReconciliationSignOffCommand> c) {
        var db = c.Db; var cmd = c.Command;
        if (!FieldOpsCommands.Human(c.Http, c.Actor)) return TowageOutcome.Fail(403, "human_actor_required");
        var grant = DataOwnerAuthorities.Resolve(c.Http.RequestServices.GetRequiredService<FederationSettings>(), c.Actor, c.Vessel, c.Now).SingleOrDefault(g => g.Id == cmd.AuthorityId);
        if (grant == null) return TowageOutcome.Fail(403, "data_owner_authority_required");
        var r = await db.ReconciliationReports.SingleOrDefaultAsync(x => x.Id == c.RouteId && x.VesselId == c.Vessel, c.Ct);
        if (r == null) return TowageOutcome.Fail(404, "resource_not_found");
        if (r.Version != cmd.ExpectedVersion || r.Hash != cmd.ReportHash) return TowageOutcome.Fail(409, "version_conflict");
        var pins = Pins(r); var sample = SampleOf(r).Select(t => t.TargetId).ToArray();
        var failure = ImportReconciliationPolicy.SignOffFailure(r.State, cmd.Decision, cmd.Reason, cmd.Exceptions, cmd.SampleVerified ?? [], sample, Counts(r),
            c.Actor.Id, r.CreatedBy, pins.Select(p => p.UploadedBy));
        if (failure != null) return TowageOutcome.Fail(failure switch {
            "invalid_state" or "reconciliation_superseded" => 409, "separation_of_duties" => 403, "reconciliation_open_items" or "reconciliation_exceptions_required" => 422, _ => 400 }, failure);
        if (await Stale(c.Http, db, c.Actor, r, c.Ct)) return TowageOutcome.Fail(409, "reconciliation_stale");
        var id = Guid.NewGuid();
        db.ReconciliationSignOffs.Add(new() { TenantId = r.TenantId, VesselId = r.VesselId, Id = id, ReportId = r.Id, ReportHash = r.Hash, ReportAuthor = r.CreatedBy,
            Decision = cmd.Decision, Reason = cmd.Reason, ExceptionsJson = JsonSerializer.Serialize(cmd.Exceptions ?? [], FieldOpsJson.Options),
            SampleVerifiedJson = JsonSerializer.Serialize((cmd.SampleVerified ?? []).Order().ToArray(), FieldOpsJson.Options), AuthorityId = grant.Id,
            AuthorityHash = DataOwnerAuthorities.Hash(grant, c.Actor), SignedBy = c.Actor.Id, SignedAt = c.Now });
        r.State = ImportReconciliationPolicy.SignedOff; r.SignOffId = id; r.SignedBy = c.Actor.Id; r.Revision = checked(r.Revision + 1);
        await db.SaveChangesAsync(c.Ct);
        return TowageOutcome.Ok(r.Id, r.Revision);
    }

    private static ReconciliationReportDto Dto(ReconciliationReport r) {
        var missing = JsonSerializer.Deserialize<MissingCriticalItem[]>(r.MissingCriticalJson, FieldOpsJson.Options) ?? [];
        return new(r.Id, r.VesselId, r.Revision, r.Version, r.State, r.PolicyVersion, ImportReconciliationPolicy.CriticalFieldVersion, Pins(r), Counts(r), missing,
            missing.Length < r.MissingCriticalCount, r.SampleSeed, SampleOf(r), r.Hash, r.CreatedBy, r.CreatedAt, r.SupersededAt, r.Provenance);
    }

    private static async Task<object?> ReadView(HttpContext h, PoseidonDbContext db, TrustedActor a, Guid vessel, Guid id, CancellationToken ct) {
        var r = await db.ReconciliationReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (r == null) return null;
        var signOff = await db.ReconciliationSignOffs.AsNoTracking().SingleOrDefaultAsync(x => x.ReportId == r.Id, ct);
        var stale = r.State == ImportReconciliationPolicy.AwaitingSignOff && await Stale(h, db, a, r, ct);
        var grants = DataOwnerAuthorities.Resolve(h.RequestServices.GetRequiredService<FederationSettings>(), a, vessel, Now(h));
        var dto = Dto(r);
        var blockers = new List<string>();
        if (r.State != ImportReconciliationPolicy.AwaitingSignOff) blockers.Add(r.State == ImportReconciliationPolicy.Superseded ? "reconciliation_superseded" : "invalid_state");
        if (stale) blockers.Add("reconciliation_stale");
        if (grants.Length == 0 || !FieldOpsCommands.Human(h, a)) blockers.Add("data_owner_authority_required");
        if (a.Id == r.CreatedBy || dto.Batches.Any(b => b.UploadedBy == a.Id)) blockers.Add("separation_of_duties");
        return new ReconciliationReportView(dto, signOff == null ? null : new(signOff.Id, signOff.Decision, signOff.Reason,
                JsonSerializer.Deserialize<ReconciliationException[]>(signOff.ExceptionsJson, FieldOpsJson.Options) ?? [],
                JsonSerializer.Deserialize<Guid[]>(signOff.SampleVerifiedJson, FieldOpsJson.Options) ?? [], signOff.Duty, signOff.SignedBy, signOff.SignedAt, signOff.ReportHash,
                ImportReconciliationPolicy.SignOffValidity(r.State)),
            stale, ImportReconciliationPolicy.OpenItems(dto.Counts, dto.Sample.Length, 0), DataOwnerAuthorities.Dto(grants), blockers.Count == 0, blockers.ToArray(),
            ImportReconciliationPolicy.Decisions, ImportReconciliationPolicy.ExceptionKinds);
    }

    private static async Task<IResult> List(Guid vessel, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = Actor(h);
        if (!CanRead(a)) return await TowageCommands.Fail(h, ReadAction, 403, "permission_denied");
        if (h.Request.Query.Count > 0) return await TowageCommands.Fail(h, ReadAction, 400, "invalid_query");
        if (!await InScope(db, a!, vessel, ct)) return await TowageCommands.Fail(h, ReadAction, 404, "resource_not_found");
        var reports = await db.ReconciliationReports.AsNoTracking().Where(r => r.VesselId == vessel).OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Take(50).ToArrayAsync(ct);
        var ids = reports.Select(r => r.Id).ToArray();
        var decisions = await db.ReconciliationSignOffs.AsNoTracking().Where(s => ids.Contains(s.ReportId)).Select(s => new { s.ReportId, s.Decision }).ToDictionaryAsync(s => s.ReportId, s => s.Decision, ct);
        var categories = ImportTemplates.Categories.Where(c => Readable(h, a!, vessel, c)).ToArray();
        var batches = await db.ImportBatches.AsNoTracking().Where(b => b.VesselId == vessel && categories.Contains(b.Category)).OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id).Take(100)
            .Select(b => new ReconciliationCandidateBatchDto(b.Id, b.Category, b.State, b.RowCount, b.CreatedAt, b.CommittedAt, b.TemplateVersion)).ToArrayAsync(ct);
        var grants = DataOwnerAuthorities.Resolve(h.RequestServices.GetRequiredService<FederationSettings>(), a, vessel, Now(h));
        await TugReadinessHttp.Observe(h, ReadAction, "Success", vessel);
        return Results.Json(new ReconciliationListView(vessel, reports.Select(r => new ReconciliationReportSummaryDto(r.Id, r.State, r.Revision, Counts(r), r.CreatedAt,
                decisions.GetValueOrDefault(r.Id), ImportReconciliationPolicy.SignOffValidity(r.State))).ToArray(), batches, CanCreate(a!, vessel), DataOwnerAuthorities.Dto(grants),
            ImportTemplates.Versions, ImportReconciliationPolicy.MaxBatches, ImportReconciliationPolicy.MaxSample), FieldOpsJson.Options);
    }

    private static async Task<IResult> Detail(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = Actor(h);
        if (!CanRead(a)) return await TowageCommands.Fail(h, ReadAction, 403, "permission_denied", id);
        if (h.Request.Query.Count > 0) return await TowageCommands.Fail(h, ReadAction, 400, "invalid_query", id);
        if (!await InScope(db, a!, vessel, ct)) return await TowageCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        var view = await ReadView(h, db, a!, vessel, id, ct);
        if (view == null) return await TowageCommands.Fail(h, ReadAction, 404, "resource_not_found", id);
        await TugReadinessHttp.Observe(h, ReadAction, "Success", id);
        return Results.Json(view, FieldOpsJson.Options);
    }
}
