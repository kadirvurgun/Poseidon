using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record FieldValueDto(string State, string? Value);
public sealed record FieldSourceDto(long Sequence, long ValueRevision, string SourceKind, TextInput Reference, TextInput ObservedAt,
    Guid DeclaredBy, Guid StewardCompanyId, DateTimeOffset RecordedAt, string Provenance, string VerificationState);
public sealed record ProfileFieldDto(string FieldPath, string Kind, string? Unit, FieldValueDto Value, long? ValueRevision, long LatestSequence,
    FieldSourceDto? Source, FieldSourceDto? PriorValueSource, FieldFreshness Freshness, string VerificationState);
public sealed record ProfileOwnerDto(Guid LegalEntityId, string Binding, string RegisteredOwnerState);
public sealed record FreshnessPolicyDto(string Status, string? PolicyRef);
public sealed record ProfileFieldsDto(Guid TenantId, Guid VesselId, ProfileOwnerDto Owner, long ProfileRevision, DateTimeOffset? ProfileSavedAt,
    DateTimeOffset CheckedAt, FreshnessPolicyDto Policy, FreshnessSummary Summary, ProfileFieldDto[] Fields, bool CanDeclare);
public sealed record ProfileFieldVersionDto(long ValueRevision, DateTimeOffset RecordedAt, string ProfileProvenance, FieldValueDto Value,
    FieldSourceDto[] Sources, bool MoreSources);
public sealed record ProfileFieldSourceOutcomeDto(Guid TenantId, Guid VesselId, string FieldPath, FieldSourceDto Source);

// 54.01 ops.source.freshness_policy_ref has no default: until an approved, governed policy
// registration exists the only production answer is UNCONFIGURED (freshness UNKNOWN).
public interface IProfileFreshnessPolicySource
{
    Task<FreshnessPolicyResolution> ResolveAsync(Guid tenant, Guid vessel, CancellationToken ct);
}
public sealed class UnconfiguredProfileFreshnessPolicy : IProfileFreshnessPolicySource
{
    public Task<FreshnessPolicyResolution> ResolveAsync(Guid tenant, Guid vessel, CancellationToken ct) => Task.FromResult(FreshnessPolicyResolution.Unconfigured);
}

// DEV-020 / T-S2-001. Field value versions, declared provenance and computed freshness of the
// single controlled vessel profile. Reads and the declaration command share the profile's
// vessel scope, construction-record withholding gate and Profile audit actions.
public static class VesselProfileFieldEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 6, PropertyNameCaseInsensitive = false };
    public static void MapVesselProfileFields(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/vessels/{id:guid}/regulatory-profile").RequireAuthorization("DirectoryRead");
        group.MapGet("/fields", Fields).WithName("GetProfileFields").Produces<ProfileFieldsDto>()
            .Produces(401).Produces(403).Produces(404).Produces(503)
            .WithDescription("Per-field value version, declared provenance and computed freshness. Declared sources are never verification; without an approved freshness policy every field is UNKNOWN with a reason.");
        group.MapGet("/fields/{field}/history", History).WithName("GetProfileFieldHistory").Produces<PageDto<ProfileFieldVersionDto>>()
            .Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);
        group.MapPost("/field-sources", (Guid id, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Declare(id, http))
            .WithName("DeclareProfileFieldSource").Accepts<DeclareProfileFieldSource>("application/json").Produces<ProfileFieldSourceOutcomeDto>()
            .ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).Produces(503)
            .WithDescription("Appends one declared (unverified) source for the exact current field value version. Trusted vessel edit grant, CSRF and Idempotency-Key required; expectedValueRevision and expectedSequence are compare-and-set fences.");
    }

    private static TrustedActor? Actor(HttpContext http) => http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User);

    // Same rule as the profile history: any restricted construction-record revision withholds all.
    private static async Task<bool> Readable(TrustedActor actor, Guid vessel, PoseidonDbContext db, CancellationToken ct) =>
        actor.Permissions.Contains("file.read") ||
        !await db.RegulatoryDraftRevisions.AnyAsync(r => r.VesselId == vessel && r.ConstructionFileId != null, ct);

    private static FieldSourceDto Dto(VesselProfileFieldSource s) => new(s.Sequence, s.ValueRevision, s.SourceKind,
        new(s.ReferenceState, s.Reference), new(s.ObservedAtState, s.ObservedAt?.ToUniversalTime().ToString("O")),
        s.DeclaredBy, s.StewardCompanyId, s.RecordedAt, s.Provenance, "NOT_VERIFIED");

    private static FieldValueDto Value(string kind, string state, string? value) =>
        new(state, state != "Known" ? null : kind == "number" && InvariantDecimal.TryNormalize(value, out var exact) ? exact : value);

    private static async Task<FreshnessPolicyResolution> Policy(HttpContext http, Guid tenant, Guid vessel)
    {
        try { return await http.RequestServices.GetRequiredService<IProfileFreshnessPolicySource>().ResolveAsync(tenant, vessel, http.RequestAborted); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return FreshnessPolicyResolution.Unavailable; }
    }

    private static async Task<IResult> Fields(Guid id, HttpContext http, PoseidonDbContext db, SecurityAudit audit, TimeProvider clock)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var actor = Actor(http)!; var ct = http.RequestAborted;
        ProfileFieldsDto dto;
        try
        {
            // One snapshot for head, derived versions and declarations.
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (vessel == null) { await audit.WriteAsync(http, "ProfileRead", "NotFound", resourceId: id); return Results.NotFound(); }
            if (!await Readable(actor, id, db, ct)) { await audit.WriteAsync(http, "ProfileRead", "permission_denied", resourceId: id); return Results.StatusCode(403); }
            var head = await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == id, ct);
            var content = head == null ? new RegulatoryDraftContent() : RegulatoryDraftStorage.Deserialize(head.ContentJson);
            var revisions = head == null ? [] : await VesselProfileFieldStorage.CurrentValueRevisions(db, actor.TenantId, id, head.Revision, [.. VesselProfileFieldPolicy.Paths], ct);
            var latest = await db.VesselProfileFieldSources.AsNoTracking().Where(s => s.VesselId == id && s.Sequence ==
                db.VesselProfileFieldSources.Where(x => x.VesselId == id && x.FieldPath == s.FieldPath).Max(x => x.Sequence)).ToArrayAsync(ct);
            await tx.CommitAsync(ct);
            var policy = await Policy(http, actor.TenantId, id);
            var now = clock.GetUtcNow();
            var fields = VesselProfileFieldPolicy.Fields.Select(f =>
            {
                var (state, value) = VesselProfileFieldPolicy.Value(content, f.Path);
                long? revision = revisions.TryGetValue(f.Path, out var r) ? r : null;
                var source = latest.SingleOrDefault(s => s.FieldPath == f.Path);
                var applies = source != null && revision == source.ValueRevision && source.ValueState == state &&
                    source.ValueHash == VesselProfileFieldPolicy.ValueHash(state, value);
                var facts = applies ? new ProfileFieldSourceFacts(source!.SourceKind, source.ReferenceState, source.Reference, source.ObservedAtState, source.ObservedAt) : null;
                var freshness = VesselProfileFieldPolicy.Evaluate(f.Path, state, revision, facts, source != null && !applies, policy, now);
                return new ProfileFieldDto(f.Path, f.Kind, f.Unit, Value(f.Kind, state, value), revision, source?.Sequence ?? 0,
                    applies ? Dto(source!) : null, source != null && !applies ? Dto(source) : null, freshness, "NOT_VERIFIED");
            }).ToArray();
            dto = new(actor.TenantId, id, new(vessel.CompanyId, "LEGACY_COMPANY_BINDING", "NOT_ESTABLISHED"), head?.Revision ?? 0, head?.SavedAt, now,
                new(policy.Status, policy.PolicyRef), VesselProfileFieldPolicy.Summarize([.. fields.Select(x => x.Freshness)], now), fields, actor.CanEditVessel(id));
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex)) { await audit.WriteAsync(http, "ProfileRead", "service_unavailable", resourceId: id); return Results.StatusCode(503); }
        await audit.WriteAsync(http, "ProfileRead", "Success", resourceId: id);
        return Results.Ok(dto);
    }

    private static async Task<IResult> History(Guid id, string field, HttpContext http, PoseidonDbContext db, SecurityAudit audit, int? offset, int? limit)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var actor = Actor(http)!; var ct = http.RequestAborted;
        var definition = VesselProfileFieldPolicy.Fields.SingleOrDefault(x => x.Path == field);
        PageDto<ProfileFieldVersionDto> page;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            if (!await actor.Scope(db.Vessels.AsNoTracking()).AnyAsync(x => x.Id == id, ct))
            { await audit.WriteAsync(http, "ProfileHistory", "NotFound", resourceId: id); return Results.NotFound(); }
            if (!await Readable(actor, id, db, ct)) { await audit.WriteAsync(http, "ProfileHistory", "permission_denied", resourceId: id); return Results.StatusCode(403); }
            if (definition == null || offset is < 0 or > 100000 || limit is not (null or 25))
            { await audit.WriteAsync(http, "ProfileHistory", "Invalid", resourceId: id); return Results.BadRequest(); }
            var skip = offset ?? 0;
            var rows = await VesselProfileFieldStorage.ValueVersions(db, actor.TenantId, id, field, skip, 26, ct);
            var shown = rows.Take(25).ToArray();
            var keys = shown.Select(x => x.Revision).ToArray();
            var sources = await db.VesselProfileFieldSources.AsNoTracking().Where(s => s.VesselId == id && s.FieldPath == field && keys.Contains(s.ValueRevision))
                .OrderByDescending(s => s.Sequence).ToArrayAsync(ct);
            await tx.CommitAsync(ct);
            page = new(shown.Select(x =>
            {
                var bound = sources.Where(s => s.ValueRevision == x.Revision).ToArray();
                return new ProfileFieldVersionDto(x.Revision, x.SavedAt, x.Provenance, Value(definition.Kind, x.State, x.Value),
                    [.. bound.Take(20).Select(Dto)], bound.Length > 20);
            }).ToArray(), skip, 25, rows.Length > 25);
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex)) { await audit.WriteAsync(http, "ProfileHistory", "service_unavailable", resourceId: id); return Results.StatusCode(503); }
        await audit.WriteAsync(http, "ProfileHistory", "Success", resourceId: id);
        return Results.Ok(page);
    }

    private static async Task<IResult> Declare(Guid id, HttpContext http)
    {
        var actor = Actor(http)!;
        var audit = http.RequestServices.GetRequiredService<SecurityAudit>();
        var ct = http.RequestAborted;
        async Task<IResult> Fail(int status, string code, Dictionary<string, string[]>? errors = null)
        {
            await audit.WriteAsync(http, "ProfileSave", code, resourceId: id);
            return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
                { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = http.TraceIdentifier });
        }
        await audit.WriteAsync(http, "ProfileSave", "Attempted", resourceId: id);
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        // Scope and edit authorization are reevaluated before every receipt lookup, including replay.
        var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (vessel == null) return await Fail(404, "resource_not_found");
        if (!actor.CanEditVessel(id)) return await Fail(403, "permission_denied");
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { return await Fail(403, "csrf_invalid"); }
        if (!http.Request.HasJsonContentType() || http.Request.ContentLength > 8192) return await Fail(400, "invalid_request");
        DeclareProfileFieldSource input;
        try
        {
            using var buffer = new MemoryStream();
            var bytes = new byte[4096]; int count;
            while ((count = await http.Request.Body.ReadAsync(bytes, ct)) > 0)
            {
                if (buffer.Length + count > 8192) return await Fail(400, "invalid_request");
                buffer.Write(bytes, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 6 });
            ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            input = document.RootElement.Deserialize<DeclareProfileFieldSource>(Json) ?? throw new JsonException();
        }
        catch (JsonException) { return await Fail(400, "invalid_request"); }
        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        var (command, errors) = VesselProfileFieldPolicy.Normalize(input, clock.GetUtcNow());
        if (errors.Count > 0) return await Fail(400, "validation_failed", errors);
        var keys = http.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return await Fail(400, "invalid_idempotency_key");
        var path = command.FieldPath!;
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"ProfileFieldSource/{id}/{JsonSerializer.Serialize(command, Json)}"))).ToLowerInvariant();
        async Task<bool> Access()
        {
            var live = Actor(http);
            return live != null && live.Id == actor.Id && live.TenantId == actor.TenantId && live.CanEditVessel(id) &&
                await live.Scope(db.Vessels.AsNoTracking()).AnyAsync(v => v.Id == id, ct) && await Readable(live, id, db, ct);
        }
        async Task<IResult?> Replay()
        {
            if (!await Access()) return await Fail(403, "permission_denied");
            var receipt = await db.VesselProfileFieldSourceReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, ct);
            if (receipt == null) return null;
            if (receipt.VesselId != id || receipt.FieldPath != path || receipt.Fingerprint != fingerprint) return await Fail(409, "idempotency_key_reused");
            http.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(receipt.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var head = await db.RegulatoryDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == id, ct);
            if (head == null) return await Fail(409, "profile_unavailable");
            var (state, value) = VesselProfileFieldPolicy.Value(RegulatoryDraftStorage.Deserialize(head.ContentJson), path);
            // A source attests a recorded value; a Missing value has nothing to attest.
            if (state == "Missing") return await Fail(422, "known_profile_value_required");
            var revision = (await VesselProfileFieldStorage.CurrentValueRevisions(db, actor.TenantId, id, head.Revision, [path], ct))[path];
            var latest = await db.VesselProfileFieldSources.AsNoTracking().Where(s => s.VesselId == id && s.FieldPath == path).MaxAsync(s => (long?)s.Sequence, ct) ?? 0;
            if (revision != command.ExpectedValueRevision || latest != command.ExpectedSequence)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return await Replay() ?? await Fail(409, "version_conflict");
            }
            var row = new VesselProfileFieldSource
            {
                TenantId = actor.TenantId, VesselId = id, FieldPath = path, Sequence = latest + 1, ValueRevision = revision,
                ValueState = state, ValueHash = VesselProfileFieldPolicy.ValueHash(state, value), SourceKind = command.SourceKind!,
                ReferenceState = command.Reference!.State, Reference = command.Reference.State == "Known" ? command.Reference.Value : null,
                ObservedAtState = command.ObservedAt!.State,
                ObservedAt = command.ObservedAt.State == "Known" ? DateTimeOffset.Parse(command.ObservedAt.Value!, System.Globalization.CultureInfo.InvariantCulture) : null,
                DeclaredBy = actor.Id, StewardCompanyId = vessel.CompanyId,
                RecordedAt = VesselProfileFieldPolicy.Truncate(clock.GetUtcNow()), Provenance = "DeclaredUnverified"
            };
            db.VesselProfileFieldSources.Add(row);
            await db.SaveChangesAsync(ct);
            var outcome = JsonSerializer.Serialize(new ProfileFieldSourceOutcomeDto(actor.TenantId, id, path, Dto(row)), Json);
            db.VesselProfileFieldSourceReceipts.Add(new() { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, VesselId = id, FieldPath = path,
                Sequence = row.Sequence, Fingerprint = fingerprint, OutcomeJson = outcome, SavedAt = row.RecordedAt });
            await db.SaveChangesAsync(ct);
            if (!await Access()) return await Fail(403, "permission_denied");
            await transaction.CommitAsync(ct);
            return Results.Content(outcome, "application/json");
        }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var conflict = PostgresFailure.IsSerialization(ex) || PostgresFailure.Provider(ex)?.SqlState == "23505";
            return await Replay() ?? await Fail(conflict ? 409 : 503, conflict ? "version_conflict" : "service_unavailable");
        }
    }
}
