using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// PLAN-1511-5201 / 52.01 Rev6 foundation: LegalEntity (the existing Company master), the single Person master,
// Employment, planned Assignment and actual (fiilî) facts. No wage/payroll/bank/monetary field exists anywhere.
public sealed record HrFieldRevisionDto(long Revision, string Field, string? Value, string SourceKind, string? SourceRef, string ActorId, DateTimeOffset RecordedAt);
public sealed record HrLegalEntityIdentifierDto(Guid Id, string Type, string Issuer, string Value, string MaskedValue, string SourceKind, DateTimeOffset CreatedAt, DateTimeOffset? RetiredAt, uint Version);
public sealed record HrLegalEntityRoleDto(Guid Id, string Role, Guid? VesselId, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo, string VerificationState, string SourceKind, uint Version);
public sealed record HrLegalEntitySummaryDto(Guid Id, string Code, string Name, string? LegalName, string? CountryCode, string? HrTimeZone, string Status, uint Version);
public sealed record HrLegalEntityDto(Guid Id, string Code, string Name, string? LegalName, string? CountryCode, string? HrTimeZone, string? BaseLocale,
    string Status, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, long Revision, uint Version, string[] MissingForActivation,
    HrLegalEntityIdentifierDto[] Identifiers, HrLegalEntityRoleDto[] Roles, bool CanEdit);
public sealed record HrPersonDto(Guid Id, string PersonType, string MasterState, long Revision, uint Version, bool CanEdit, bool CanVerify);
public sealed record HrEmploymentDto(Guid Id, Guid PersonId, Guid LegalEntityId, string EmployeeNo, string EmploymentType, DateTimeOffset StartAt,
    DateTimeOffset? EndAt, string State, DateTimeOffset? ActivatedAt, long Revision, uint Version);
public sealed record HrAssignmentPlanDto(DateTimeOffset PlannedFrom, DateTimeOffset PlannedTo, string PlannedZoneId, string State, DateTimeOffset PlannedAt, DateTimeOffset? ApprovedAt);
public sealed record HrActualEventDto(Guid Id, string Kind, DateTimeOffset OccurredAt, string OriginZoneId, string Origin, string Quality, string Disposition, DateTimeOffset RecordedAt);
public sealed record HrActualSessionDto(DateTimeOffset ActualFrom, DateTimeOffset? ActualTo);
public sealed record HrAssignmentActualDto(string Status, DateTimeOffset? ActualFrom, DateTimeOffset? ActualTo, int PendingReconciliation, HrActualSessionDto[] Sessions, HrActualEventDto[] Events);
public sealed record HrAssignmentDto(Guid Id, Guid EmploymentId, Guid PersonId, Guid LegalEntityId, string Kind, Guid? VesselId, Guid? PositionId,
    HrAssignmentPlanDto Plan, HrAssignmentActualDto Actual, long Revision, uint Version);
public sealed record HrListDto<T>(T[] Items, bool Truncated);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrLegalEntityUpdateCommand(uint? ExpectedVersion, string? LegalName, string? CountryCode, string? HrTimeZone, string? BaseLocale,
    DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, string? SourceKind, string? SourceRef);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrStatusCommand(uint? ExpectedVersion, string? TargetState, string? Reason, DateTimeOffset? EndAt = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrIdentifierAddCommand(string? Type, string? Issuer, string? Value, string? SourceKind, string? SourceRef);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrRoleAddCommand(string? Role, Guid? VesselId, DateTimeOffset? EffectiveFrom, DateTimeOffset? EffectiveTo, string? SourceKind, string? SourceRef);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrRetireCommand(uint? ExpectedVersion, string? Reason, DateTimeOffset? EffectiveTo = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrPersonUpdateCommand(uint? ExpectedVersion, string? PersonType, string? SourceKind, string? SourceRef);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrEmploymentCreateCommand(Guid? PersonId, Guid? LegalEntityId, string? EmployeeNo, string? EmploymentType, DateTimeOffset? StartAt, DateTimeOffset? EndAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrAssignmentCreateCommand(Guid? EmploymentId, string? Kind, Guid? VesselId, Guid? PositionId, DateTimeOffset? PlannedFrom, DateTimeOffset? PlannedTo, string? PlannedZoneId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HrActualEventCommand(string? Kind, DateTimeOffset? OccurredAt, string? OriginZoneId, string? Origin, string? SourceSystem, string? SourceEventId);

public static class HrEndpoints
{
    private const string ReadAction = "HrRead", CommandAction = "HrCommand";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };

    internal sealed record Ctx(HttpContext Http, TrustedActor Actor, PoseidonDbContext Db, DateTimeOffset Now, CancellationToken Ct)
    {
        public string ActorId => Actor.Id.ToString();
    }
    internal sealed record Outcome(Guid? ResourceId, int Status = 200, string? Code = null, Dictionary<string, string[]>? Errors = null)
    {
        public static Outcome Ok(Guid id) => new(id);
        public static Outcome Fail(int status, string code, Dictionary<string, string[]>? errors = null) => new(null, status, code, errors);
    }

    // HR is tenant/company data: vessel-only grants confer nothing. Company-scoped HR grants follow 52.02/54.02.
    internal static bool Allowed(TrustedActor? a, string permission) =>
        a != null && a.CanRead && (a.TenantWideRead || a.TenantWideManage) && a.Permissions.Contains(permission);

    public static void MapHr(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/hr").RequireAuthorization("DirectoryRead");
        g.MapGet("/legal-entities", (Delegate)((HttpContext h) => ListLegalEntities(h))).WithName("HrListLegalEntities")
            .WithSummary("List legal entities (the single company master)").Produces<HrListDto<HrLegalEntitySummaryDto>>().ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503);
        g.MapGet("/legal-entities/{id:guid}", (Guid id, HttpContext h) => GetLegalEntity(id, h)).WithName("HrGetLegalEntity")
            .Produces<HrLegalEntityDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        g.MapGet("/legal-entities/{id:guid}/field-revisions", (Guid id, HttpContext h) => LegalEntityRevisions(id, h)).WithName("HrGetLegalEntityFieldRevisions")
            .Produces<HrListDto<HrFieldRevisionDto>>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        g.MapGet("/people/{id:guid}", (Guid id, HttpContext h) => GetPerson(id, h)).WithName("HrGetPerson")
            .Produces<HrPersonDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        g.MapGet("/people/{id:guid}/field-revisions", (Guid id, HttpContext h) => PersonRevisions(id, h)).WithName("HrGetPersonFieldRevisions")
            .Produces<HrListDto<HrFieldRevisionDto>>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        g.MapGet("/employments", (HttpContext h, string? personId, string? legalEntityId) => ListEmployments(h)).WithName("HrListEmployments")
            .Produces<HrListDto<HrEmploymentDto>>().ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503);
        g.MapGet("/employments/{id:guid}", (Guid id, HttpContext h) => GetEmployment(id, h)).WithName("HrGetEmployment")
            .Produces<HrEmploymentDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);
        g.MapGet("/assignments", (HttpContext h, string? personId, string? employmentId, string? vesselId) => ListAssignments(h)).WithName("HrListAssignments")
            .Produces<HrListDto<HrAssignmentDto>>().ProducesProblem(400).Produces(401).ProducesProblem(403).Produces(503);
        g.MapGet("/assignments/{id:guid}", (Guid id, HttpContext h) => GetAssignment(id, h)).WithName("HrGetAssignment")
            .Produces<HrAssignmentDto>().Produces(401).ProducesProblem(403).ProducesProblem(404).Produces(503);

        Command<HrLegalEntityUpdateCommand, HrLegalEntityDto>(g.MapPut("/legal-entities/{id:guid}", (Guid id, HttpContext h) =>
            Run<HrLegalEntityUpdateCommand>(h, "HrLegalEntityUpdate", "hr.legal_entity.write", id, null, UpdateLegalEntity, LegalEntityRepresentation)), "HrUpdateLegalEntity");
        Command<HrStatusCommand, HrLegalEntityDto>(g.MapPost("/legal-entities/{id:guid}/status", (Guid id, HttpContext h) =>
            Run<HrStatusCommand>(h, "HrLegalEntityStatus", "hr.legal_entity.write", id, null, LegalEntityStatus, LegalEntityRepresentation)), "HrChangeLegalEntityStatus");
        Command<HrIdentifierAddCommand, HrLegalEntityIdentifierDto>(g.MapPost("/legal-entities/{id:guid}/identifiers", (Guid id, HttpContext h) =>
            Run<HrIdentifierAddCommand>(h, "HrIdentifierAdd", "hr.legal_entity.write", id, null, AddIdentifier, IdentifierRepresentation)), "HrAddLegalEntityIdentifier");
        Command<HrRetireCommand, HrLegalEntityIdentifierDto>(g.MapPost("/legal-entities/{id:guid}/identifiers/{identifierId:guid}/retire", (Guid id, Guid identifierId, HttpContext h) =>
            Run<HrRetireCommand>(h, "HrIdentifierRetire", "hr.legal_entity.write", id, identifierId, RetireIdentifier, IdentifierRepresentation)), "HrRetireLegalEntityIdentifier");
        Command<HrRoleAddCommand, HrLegalEntityRoleDto>(g.MapPost("/legal-entities/{id:guid}/roles", (Guid id, HttpContext h) =>
            Run<HrRoleAddCommand>(h, "HrRoleAdd", "hr.legal_entity.write", id, null, AddRole, RoleRepresentation)), "HrAddLegalEntityRole");
        Command<HrRetireCommand, HrLegalEntityRoleDto>(g.MapPost("/legal-entities/{id:guid}/roles/{roleId:guid}/end", (Guid id, Guid roleId, HttpContext h) =>
            Run<HrRetireCommand>(h, "HrRoleEnd", "hr.legal_entity.write", id, roleId, EndRole, RoleRepresentation)), "HrEndLegalEntityRole");
        Command<HrPersonUpdateCommand, HrPersonDto>(g.MapPut("/people/{id:guid}", (Guid id, HttpContext h) =>
            Run<HrPersonUpdateCommand>(h, "HrPersonUpdate", "hr.person.write", id, null, UpdatePerson, PersonRepresentation)), "HrUpdatePerson");
        Command<HrStatusCommand, HrPersonDto>(g.MapPost("/people/{id:guid}/verify", (Guid id, HttpContext h) =>
            Run<HrStatusCommand>(h, "HrPersonVerify", "hr.person.verify", id, null, VerifyPerson, PersonRepresentation)), "HrVerifyPerson");
        Command<HrEmploymentCreateCommand, HrEmploymentDto>(g.MapPost("/employments", (Delegate)((HttpContext h) =>
            Run<HrEmploymentCreateCommand>(h, "HrEmploymentCreate", "hr.employment.write", null, null, CreateEmployment, EmploymentRepresentation))), "HrCreateEmployment");
        Command<HrStatusCommand, HrEmploymentDto>(g.MapPost("/employments/{id:guid}/status", (Guid id, HttpContext h) =>
            Run<HrStatusCommand>(h, "HrEmploymentStatus", "hr.employment.write", id, null, EmploymentStatus, EmploymentRepresentation)), "HrChangeEmploymentStatus");
        Command<HrAssignmentCreateCommand, HrAssignmentDto>(g.MapPost("/assignments", (Delegate)((HttpContext h) =>
            Run<HrAssignmentCreateCommand>(h, "HrAssignmentCreate", "hr.assignment.write", null, null, CreateAssignment, AssignmentRepresentation))), "HrCreateAssignment");
        Command<HrStatusCommand, HrAssignmentDto>(g.MapPost("/assignments/{id:guid}/status", (Guid id, HttpContext h) =>
            Run<HrStatusCommand>(h, "HrAssignmentStatus", "hr.assignment.write", id, null, AssignmentStatus, AssignmentRepresentation)), "HrChangeAssignmentStatus");
        Command<HrActualEventCommand, HrAssignmentDto>(g.MapPost("/assignments/{id:guid}/actual-events", (Guid id, HttpContext h) =>
            Run<HrActualEventCommand>(h, "HrActualEventRecord", "hr.actual.record", id, null, RecordActual, ActualRepresentation)), "HrRecordActualEvent");
    }

    private static void Command<TCommand, TResult>(RouteHandlerBuilder route, string name) where TCommand : notnull where TResult : notnull => route.WithName(name)
        .WithDescription("Audited HR command. Requires tenant-wide HR permission, a trusted role, antiforgery and a 16-80 character Idempotency-Key. " +
            "Mutations of existing records require expectedVersion (409 on conflict). Replay returns the fresh authorized representation.")
        .Accepts<TCommand>("application/json")
        .AddOpenApiOperationTransformer((operation, _, _) => {
            operation.Parameters ??= [];
            foreach (var header in new[] { "Idempotency-Key", "X-CSRF-TOKEN" })
                operation.Parameters.Add(new Microsoft.OpenApi.OpenApiParameter { Name = header, In = Microsoft.OpenApi.ParameterLocation.Header,
                    Required = true, Schema = new Microsoft.OpenApi.OpenApiSchema { Type = Microsoft.OpenApi.JsonSchemaType.String } });
            return Task.CompletedTask;
        })
        .Produces<TResult>().ProducesProblem(400).Produces(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).Produces(503);

    private static IResult Problem(HttpContext h, int status, string code, Dictionary<string, string[]>? errors = null) =>
        Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
            { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = h.TraceIdentifier });

    private static async Task<IResult> Fail(HttpContext h, string action, int status, string code, Guid? resource = null, Dictionary<string, string[]>? errors = null)
    {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, action, code, resourceId: resource);
        return Problem(h, status, code, errors);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static bool UniqueMembers(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().All(UniqueMembers) :
        value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == value.EnumerateObject().Count() &&
            value.EnumerateObject().All(p => UniqueMembers(p.Value));

    // Shared audited command pipeline: permission -> antiforgery -> key -> bounded closed JSON -> SERIALIZABLE
    // transaction -> receipt replay -> domain change -> audited SaveChanges -> receipt -> commit.
    private static async Task<IResult> Run<TCommand>(HttpContext h, string action, string permission, Guid? routeId, Guid? childId,
        Func<Ctx, Guid?, Guid?, TCommand, Task<Outcome>> execute, Func<Ctx, Guid, Task<object?>> represent) where TCommand : class
    {
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        if (actor == null || !actor.HasWriteAttribution || !Allowed(actor, permission)) return await Fail(h, CommandAction, 403, "permission_denied", routeId);
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, CommandAction, 403, "csrf_invalid", routeId); }
        var keys = h.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key is null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return await Fail(h, CommandAction, 400, "invalid_idempotency_key", routeId);
        if (h.Request.Query.Count != 0 || !h.Request.HasJsonContentType()) return await Fail(h, CommandAction, 400, "invalid_request", routeId);
        TCommand command;
        try
        {
            using var buffer = new MemoryStream();
            await SecureFilePolicy.CopyAsync(h.Request.Body, buffer, 8192, h.RequestAborted);
            using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !UniqueMembers(document.RootElement)) throw new JsonException();
            command = document.RootElement.Deserialize<TCommand>(Json) ?? throw new JsonException();
        }
        catch (Exception error) when (error is JsonException or InvalidDataException) { return await Fail(h, CommandAction, 400, "invalid_request", routeId); }
        var fingerprint = Hash(JsonSerializer.Serialize(new { action, routeId, childId, command }, Json));
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var ctx = new Ctx(h, actor, db, h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), h.RequestAborted);

        async Task<IResult?> Replay()
        {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, ctx.Ct);
            if (receipt == null) return null;
            if (receipt.Action != action || receipt.Fingerprint != fingerprint) return await Fail(h, CommandAction, 409, "idempotency_key_reused", routeId);
            var fresh = await represent(ctx, receipt.ResourceId);
            if (fresh == null) return await Fail(h, CommandAction, 404, "resource_not_found", routeId);
            h.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Ok(fresh);
        }

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ctx.Ct);
            var replay = await Replay();
            if (replay != null) { await tx.RollbackAsync(CancellationToken.None); return replay; }
            var outcome = await execute(ctx, routeId, childId, command);
            if (outcome.ResourceId is not { } resource)
            {
                await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
                return await Fail(h, CommandAction, outcome.Status, outcome.Code!, routeId, outcome.Errors);
            }
            await db.SaveChangesAsync(ctx.Ct);
            db.CommandReceipts.Add(new CommandReceipt { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = action,
                Fingerprint = fingerprint, ResourceId = resource, CommittedAt = ctx.Now });
            await db.SaveChangesAsync(ctx.Ct);
            await tx.CommitAsync(ctx.Ct);
            return Results.Ok(await represent(ctx, resource));
        }
        catch (Exception error) when (error is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(error))
        {
            db.ChangeTracker.Clear();
            var state = PostgresFailure.Provider(error)?.SqlState;
            if (error is not DbUpdateConcurrencyException && state is not ("23505" or "23P01") && !PostgresFailure.IsSerialization(error))
                return await Fail(h, CommandAction, 503, "service_unavailable", routeId);
            // A concurrent identical command may have committed first: replay it instead of reporting a conflict.
            try { if (await Replay() is { } replayed) return replayed; }
            catch (Exception replayError) when (PostgresFailure.IsDatabase(replayError)) { return await Fail(h, CommandAction, 503, "service_unavailable", routeId); }
            var code = state switch
            {
                "23P01" => action switch
                {
                    "HrRoleAdd" => "hr_role_overlap", "HrEmploymentCreate" => "hr_employee_no_overlap",
                    "HrAssignmentStatus" => "hr_assignment_overlap", _ => "version_conflict"
                },
                "23505" => action switch { "HrIdentifierAdd" => "hr_identifier_duplicate", "HrActualEventRecord" => "hr_event_conflict", _ => "version_conflict" },
                _ => "version_conflict"
            };
            return await Fail(h, CommandAction, 409, code, routeId);
        }
    }

    private static Dictionary<string, string[]> Errors(params (string Field, string Code)[] errors) =>
        errors.ToDictionary(x => x.Field, x => new[] { x.Code }, StringComparer.Ordinal);

    private static bool Cas(Ctx c, object entity, uint? expected)
    {
        if (expected is not { } version) return false;
        var entry = c.Db.Entry(entity);
        if ((uint)entry.Property("Version").CurrentValue! != version) return false;
        entry.Property("Version").OriginalValue = version;
        return true;
    }

    // ---------- Legal entity ----------
    private static async Task<Outcome> UpdateLegalEntity(Ctx c, Guid? id, Guid? _, HrLegalEntityUpdateCommand cmd)
    {
        var entity = await c.Db.Companies.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (entity == null) return Outcome.Fail(404, "resource_not_found");
        var legalName = cmd.LegalName?.Trim();
        var errors = HrLegalEntityPolicy.Validate(legalName, cmd.CountryCode, cmd.HrTimeZone, cmd.BaseLocale, cmd.EffectiveFrom, cmd.EffectiveTo);
        if (!HrSourcePolicy.ValidSourceKind(cmd.SourceKind)) errors["sourceKind"] = ["closed_source_kind_required"];
        if (!HrSourcePolicy.ValidSourceRef(cmd.SourceRef)) errors["sourceRef"] = ["text_1_200"];
        if (cmd.ExpectedVersion is null) errors["expectedVersion"] = ["required"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        if (!Cas(c, entity, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (entity.LegalEntityStatus != "Draft")
        {
            var missing = HrLegalEntityPolicy.MissingForActivation(legalName, cmd.CountryCode, cmd.HrTimeZone, cmd.BaseLocale, cmd.EffectiveFrom);
            if (missing.Length > 0) return Outcome.Fail(422, "hr_reference_incomplete", missing.ToDictionary(x => x, _ => new[] { "required_while_active" }));
        }
        string? Instant(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var changes = new (string Field, string? Before, string? After)[] {
            ("LegalName", entity.LegalName, legalName), ("CountryCode", entity.CountryCode, cmd.CountryCode),
            ("HrTimeZone", entity.HrTimeZone, cmd.HrTimeZone), ("BaseLocale", entity.BaseLocale, cmd.BaseLocale),
            ("EffectiveFrom", Instant(entity.EffectiveFrom), Instant(cmd.EffectiveFrom)), ("EffectiveTo", Instant(entity.EffectiveTo), Instant(cmd.EffectiveTo))
        }.Where(x => x.Before != x.After).ToArray();
        // An unchanged update is still an attributed xmin touch, but adds no field revision.
        if (changes.Length > 0)
        {
            entity.LegalEntityRevision = checked(entity.LegalEntityRevision + 1);
            foreach (var change in changes)
                c.Db.LegalEntityFieldRevisions.Add(new() { TenantId = c.Actor.TenantId, LegalEntityId = entity.Id, Revision = entity.LegalEntityRevision,
                    Field = change.Field, Value = change.After, SourceKind = cmd.SourceKind!, SourceRef = cmd.SourceRef, ActorId = c.ActorId, RecordedAt = c.Now });
        }
        (entity.LegalName, entity.CountryCode, entity.HrTimeZone, entity.BaseLocale, entity.EffectiveFrom, entity.EffectiveTo) =
            (legalName, cmd.CountryCode, cmd.HrTimeZone, cmd.BaseLocale, cmd.EffectiveFrom, cmd.EffectiveTo);
        c.Db.Entry(entity).Property(x => x.LegalEntityRevision).IsModified = true;
        return Outcome.Ok(entity.Id);
    }

    private static async Task<Outcome> LegalEntityStatus(Ctx c, Guid? id, Guid? _, HrStatusCommand cmd)
    {
        var entity = await c.Db.Companies.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (entity == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.TargetState is null || !HrLegalEntityPolicy.Statuses.Contains(cmd.TargetState) || cmd.ExpectedVersion is null || cmd.EndAt != null ||
            !HrSourcePolicy.ValidReason(cmd.Reason, true))
            return Outcome.Fail(400, "validation_failed", Errors(("targetState", "closed_state_expected_version_and_reason_required")));
        if (!Cas(c, entity, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (!HrLegalEntityPolicy.CanTransition(entity.LegalEntityStatus, cmd.TargetState)) return Outcome.Fail(409, "invalid_transition");
        if (cmd.TargetState == "Active")
        {
            var missing = HrLegalEntityPolicy.MissingForActivation(entity.LegalName, entity.CountryCode, entity.HrTimeZone, entity.BaseLocale, entity.EffectiveFrom);
            if (missing.Length > 0) return Outcome.Fail(422, "hr_reference_incomplete", missing.ToDictionary(x => x, _ => new[] { "required" }));
        }
        entity.LegalEntityRevision = checked(entity.LegalEntityRevision + 1);
        c.Db.LegalEntityFieldRevisions.Add(new() { TenantId = c.Actor.TenantId, LegalEntityId = entity.Id, Revision = entity.LegalEntityRevision,
            Field = "LegalEntityStatus", Value = cmd.TargetState, SourceKind = "UserDeclared", ActorId = c.ActorId, RecordedAt = c.Now });
        entity.LegalEntityStatus = cmd.TargetState;
        return Outcome.Ok(entity.Id);
    }

    private static async Task<Outcome> AddIdentifier(Ctx c, Guid? id, Guid? _, HrIdentifierAddCommand cmd)
    {
        if (!await c.Db.Companies.AnyAsync(x => x.Id == id, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        var errors = HrIdentifierPolicy.Validate(cmd.Type, cmd.Issuer, cmd.Value);
        if (!HrSourcePolicy.ValidSourceKind(cmd.SourceKind)) errors["sourceKind"] = ["closed_source_kind_required"];
        if (!HrSourcePolicy.ValidSourceRef(cmd.SourceRef)) errors["sourceRef"] = ["text_1_200"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        var (issuer, value) = (HrIdentifierPolicy.NormalizeIssuer(cmd.Issuer!), HrIdentifierPolicy.Normalize(cmd.Value!));
        if (await c.Db.LegalEntityIdentifiers.AnyAsync(x => x.Type == cmd.Type && x.Issuer == issuer && x.Value == value && x.RetiredAt == null, c.Ct))
            return Outcome.Fail(409, "hr_identifier_duplicate");
        var row = new LegalEntityIdentifier { TenantId = c.Actor.TenantId, Id = Guid.NewGuid(), LegalEntityId = id!.Value, Type = cmd.Type!, Issuer = issuer,
            Value = value, SourceKind = cmd.SourceKind!, SourceRef = cmd.SourceRef, CreatedBy = c.ActorId, CreatedAt = c.Now };
        c.Db.LegalEntityIdentifiers.Add(row);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> RetireIdentifier(Ctx c, Guid? id, Guid? identifierId, HrRetireCommand cmd)
    {
        var row = await c.Db.LegalEntityIdentifiers.SingleOrDefaultAsync(x => x.Id == identifierId && x.LegalEntityId == id, c.Ct);
        if (row == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.ExpectedVersion is null || cmd.EffectiveTo != null || !HrSourcePolicy.ValidReason(cmd.Reason, true))
            return Outcome.Fail(400, "validation_failed", Errors(("reason", "expected_version_and_reason_required")));
        if (!Cas(c, row, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (row.RetiredAt != null) return Outcome.Fail(409, "invalid_transition");
        (row.RetiredAt, row.RetiredBy) = (c.Now, c.ActorId);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> AddRole(Ctx c, Guid? id, Guid? _, HrRoleAddCommand cmd)
    {
        if (!await c.Db.Companies.AnyAsync(x => x.Id == id, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        var errors = HrRolePolicy.Validate(cmd.Role, cmd.VesselId, cmd.EffectiveFrom, cmd.EffectiveTo);
        if (!HrSourcePolicy.ValidSourceKind(cmd.SourceKind)) errors["sourceKind"] = ["closed_source_kind_required"];
        if (!HrSourcePolicy.ValidSourceRef(cmd.SourceRef)) errors["sourceRef"] = ["text_1_200"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        // The vessel must be inside the actor's server-resolved vessel scope; otherwise it does not exist for them.
        if (cmd.VesselId is { } vessel && !await c.Actor.Scope(c.Db.Vessels).AnyAsync(x => x.Id == vessel, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        var period = new HrPeriod(cmd.EffectiveFrom!.Value, cmd.EffectiveTo);
        var current = await c.Db.LegalEntityRoles.AsNoTracking().Where(x => x.Role == cmd.Role && (x.VesselId == cmd.VesselId || x.LegalEntityId == id))
            .Select(x => new { x.Id, x.LegalEntityId, x.Role, x.VesselId, x.EffectiveFrom, x.EffectiveTo }).ToArrayAsync(c.Ct);
        if (HrRolePolicy.Conflict(current.Select(x => new HrRolePolicy.Existing(x.Id, x.LegalEntityId, x.Role, x.VesselId, new HrPeriod(x.EffectiveFrom, x.EffectiveTo))),
            id!.Value, cmd.Role!, cmd.VesselId, period) != null) return Outcome.Fail(409, "hr_role_overlap");
        var row = new LegalEntityRole { TenantId = c.Actor.TenantId, Id = Guid.NewGuid(), LegalEntityId = id.Value, Role = cmd.Role!, VesselId = cmd.VesselId,
            EffectiveFrom = period.From, EffectiveTo = period.To, SourceKind = cmd.SourceKind!, SourceRef = cmd.SourceRef, CreatedBy = c.ActorId, CreatedAt = c.Now };
        c.Db.LegalEntityRoles.Add(row);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> EndRole(Ctx c, Guid? id, Guid? roleId, HrRetireCommand cmd)
    {
        var row = await c.Db.LegalEntityRoles.SingleOrDefaultAsync(x => x.Id == roleId && x.LegalEntityId == id, c.Ct);
        if (row == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.ExpectedVersion is null || cmd.EffectiveTo is null || !HrSourcePolicy.ValidReason(cmd.Reason, true))
            return Outcome.Fail(400, "validation_failed", Errors(("effectiveTo", "expected_version_effective_to_and_reason_required")));
        if (!Cas(c, row, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (row.EffectiveTo != null) return Outcome.Fail(409, "invalid_transition");
        if (cmd.EffectiveTo <= row.EffectiveFrom) return Outcome.Fail(400, "validation_failed", Errors(("effectiveTo", "must_follow_effective_from")));
        (row.EffectiveTo, row.EndedBy) = (cmd.EffectiveTo, c.ActorId);
        return Outcome.Ok(row.Id);
    }

    // ---------- Person master ----------
    private static async Task<Outcome> UpdatePerson(Ctx c, Guid? id, Guid? _, HrPersonUpdateCommand cmd)
    {
        var person = await c.Db.Persons.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (person == null) return Outcome.Fail(404, "resource_not_found");
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!HrPersonPolicy.ValidType(cmd.PersonType)) errors["personType"] = ["closed_person_type_required"];
        if (!HrSourcePolicy.ValidSourceKind(cmd.SourceKind)) errors["sourceKind"] = ["closed_source_kind_required"];
        if (!HrSourcePolicy.ValidSourceRef(cmd.SourceRef)) errors["sourceRef"] = ["text_1_200"];
        if (cmd.ExpectedVersion is null) errors["expectedVersion"] = ["required"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        if (!Cas(c, person, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (person.PersonType != cmd.PersonType)
        {
            person.MasterRevision = checked(person.MasterRevision + 1);
            Revision(c, person, "PersonType", cmd.PersonType, cmd.SourceKind!, cmd.SourceRef);
            // A changed core field invalidates a prior verification; it must be verified again independently.
            if (person.MasterState == "Verified") { person.MasterState = "Draft"; Revision(c, person, "MasterState", "Draft", cmd.SourceKind!, cmd.SourceRef); }
            person.PersonType = cmd.PersonType!;
        }
        c.Db.Entry(person).Property(x => x.MasterRevision).IsModified = true;
        return Outcome.Ok(person.Id);
    }

    private static void Revision(Ctx c, Person person, string field, string? value, string sourceKind, string? sourceRef) =>
        c.Db.PersonFieldRevisions.Add(new() { TenantId = c.Actor.TenantId, PersonId = person.Id, Revision = person.MasterRevision, Field = field,
            Value = value, SourceKind = sourceKind, SourceRef = sourceRef, ActorId = c.ActorId, RecordedAt = c.Now });

    private static async Task<Outcome> VerifyPerson(Ctx c, Guid? id, Guid? _, HrStatusCommand cmd)
    {
        var person = await c.Db.Persons.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (person == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.ExpectedVersion is null || cmd.TargetState is not (null or "Verified") || cmd.EndAt != null || !HrSourcePolicy.ValidReason(cmd.Reason, true))
            return Outcome.Fail(400, "validation_failed", Errors(("reason", "expected_version_and_reason_required")));
        if (!Cas(c, person, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        var lastActor = await c.Db.PersonFieldRevisions.AsNoTracking().Where(x => x.PersonId == person.Id && x.Field == "PersonType")
            .OrderByDescending(x => x.Revision).Select(x => x.ActorId).FirstOrDefaultAsync(c.Ct);
        var rejected = HrPersonPolicy.VerifyRejection(person.MasterState, person.PersonType, lastActor, c.ActorId);
        if (rejected != null) return Outcome.Fail(rejected == "hr_reference_incomplete" ? 422 : rejected == "separation_of_duties" ? 403 : 409, rejected);
        person.MasterRevision = checked(person.MasterRevision + 1);
        person.MasterState = "Verified";
        Revision(c, person, "MasterState", "Verified", "UserDeclared", null);
        return Outcome.Ok(person.Id);
    }

    // ---------- Employment ----------
    private static async Task<Outcome> CreateEmployment(Ctx c, Guid? _, Guid? __, HrEmploymentCreateCommand cmd)
    {
        var errors = HrEmploymentPolicy.Validate(cmd.EmployeeNo, cmd.EmploymentType, cmd.StartAt, cmd.EndAt);
        if (cmd.PersonId is null || cmd.PersonId == Guid.Empty) errors["personId"] = ["required"];
        if (cmd.LegalEntityId is null || cmd.LegalEntityId == Guid.Empty) errors["legalEntityId"] = ["required"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        if (!await c.Db.Persons.AnyAsync(x => x.Id == cmd.PersonId, c.Ct) || !await c.Db.Companies.AnyAsync(x => x.Id == cmd.LegalEntityId, c.Ct))
            return Outcome.Fail(404, "resource_not_found");
        var employeeNo = cmd.EmployeeNo!.Trim();
        var period = new HrPeriod(cmd.StartAt!.Value, cmd.EndAt);
        var sameNumber = await c.Db.Employments.AsNoTracking().Where(x => x.LegalEntityId == cmd.LegalEntityId && x.EmployeeNo == employeeNo && x.State != "Cancelled")
            .Select(x => new { x.StartAt, x.EndAt }).ToArrayAsync(c.Ct);
        if (sameNumber.Any(x => new HrPeriod(x.StartAt, x.EndAt).Overlaps(period))) return Outcome.Fail(409, "hr_employee_no_overlap");
        var row = new Employment { TenantId = c.Actor.TenantId, Id = Guid.NewGuid(), PersonId = cmd.PersonId!.Value, LegalEntityId = cmd.LegalEntityId!.Value,
            EmployeeNo = employeeNo, EmploymentType = cmd.EmploymentType!, StartAt = period.From, EndAt = period.To, CreatedBy = c.ActorId, CreatedAt = c.Now };
        c.Db.Employments.Add(row);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> EmploymentStatus(Ctx c, Guid? id, Guid? _, HrStatusCommand cmd)
    {
        var row = await c.Db.Employments.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (row == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.ExpectedVersion is null || cmd.TargetState is null || !HrEmploymentPolicy.States.Contains(cmd.TargetState) || !HrSourcePolicy.ValidReason(cmd.Reason, true) ||
            cmd.EndAt != null && cmd.TargetState != "Ended")
            return Outcome.Fail(400, "validation_failed", Errors(("targetState", "closed_state_expected_version_and_reason_required")));
        if (!Cas(c, row, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (!HrEmploymentPolicy.CanTransition(row.State, cmd.TargetState)) return Outcome.Fail(409, "invalid_transition");
        if (cmd.TargetState == "Active")
        {
            if (!c.Actor.Permissions.Contains("hr.employment.approve")) return Outcome.Fail(403, "permission_denied");
            var person = await c.Db.Persons.AsNoTracking().SingleAsync(x => x.Id == row.PersonId, c.Ct);
            var entity = await c.Db.Companies.AsNoTracking().SingleAsync(x => x.Id == row.LegalEntityId, c.Ct);
            var employer = await c.Db.LegalEntityRoles.AsNoTracking().Where(x => x.LegalEntityId == row.LegalEntityId && x.Role == "Employer")
                .Select(x => new { x.EffectiveFrom, x.EffectiveTo }).ToArrayAsync(c.Ct);
            var rejected = HrEmploymentPolicy.ActivationRejection(person.MasterState, entity.LegalEntityStatus,
                entity.EffectiveFrom is { } from ? new HrPeriod(from, entity.EffectiveTo) : null, employer.Select(x => new HrPeriod(x.EffectiveFrom, x.EffectiveTo)),
                new HrPeriod(row.StartAt, row.EndAt), row.CreatedBy, c.ActorId);
            if (rejected != null) return Outcome.Fail(rejected == "separation_of_duties" ? 403 : 409, rejected);
            (row.ActivatedBy, row.ActivatedAt) = (c.ActorId, c.Now);
        }
        if (cmd.TargetState == "Ended")
        {
            var end = cmd.EndAt ?? row.EndAt;
            if (end is null || end <= row.StartAt || end > c.Now) return Outcome.Fail(400, "validation_failed", Errors(("endAt", "past_end_after_start_required")));
            // Ending employment never fabricates a physical sign-off; an approved plan beyond the end must be resolved first.
            if (await c.Db.HrAssignments.AnyAsync(x => x.EmploymentId == row.Id && x.State == "Approved" && x.PlannedTo > end, c.Ct))
                return Outcome.Fail(409, "hr_active_assignment_at_exit");
            row.EndAt = end;
        }
        row.State = cmd.TargetState;
        row.Revision = checked(row.Revision + 1);
        return Outcome.Ok(row.Id);
    }

    // ---------- Planned assignment and actual facts ----------
    private static async Task<Outcome> CreateAssignment(Ctx c, Guid? _, Guid? __, HrAssignmentCreateCommand cmd)
    {
        var errors = HrAssignmentPolicy.Validate(cmd.Kind, cmd.VesselId, cmd.PositionId, cmd.PlannedFrom, cmd.PlannedTo, cmd.PlannedZoneId);
        if (cmd.EmploymentId is null || cmd.EmploymentId == Guid.Empty) errors["employmentId"] = ["required"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        var employment = await c.Db.Employments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == cmd.EmploymentId, c.Ct);
        if (employment == null) return Outcome.Fail(404, "resource_not_found");
        if (cmd.VesselId is { } vessel && !await c.Actor.Scope(c.Db.Vessels).AnyAsync(x => x.Id == vessel, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        if (cmd.PositionId is { } position && !await c.Db.CrewPositions.AnyAsync(x => x.Id == position && x.VesselId == cmd.VesselId, c.Ct))
            return Outcome.Fail(404, "resource_not_found");
        if (employment.State is "Ended" or "Cancelled") return Outcome.Fail(409, "hr_employment_not_active");
        var plan = new HrPeriod(cmd.PlannedFrom!.Value, cmd.PlannedTo);
        if (!new HrPeriod(employment.StartAt, employment.EndAt).Covers(plan)) return Outcome.Fail(422, "hr_period_outside_employment");
        var row = new HrAssignment { TenantId = c.Actor.TenantId, Id = Guid.NewGuid(), EmploymentId = employment.Id, PersonId = employment.PersonId,
            LegalEntityId = employment.LegalEntityId, Kind = cmd.Kind!, VesselId = cmd.VesselId, PositionId = cmd.PositionId, PlannedFrom = plan.From,
            PlannedTo = plan.To!.Value, PlannedZoneId = cmd.PlannedZoneId!, PlannedBy = c.ActorId, PlannedAt = c.Now };
        c.Db.HrAssignments.Add(row);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> AssignmentStatus(Ctx c, Guid? id, Guid? _, HrStatusCommand cmd)
    {
        var row = await c.Db.HrAssignments.SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (row == null || row.VesselId is { } scoped && !await c.Actor.Scope(c.Db.Vessels).AnyAsync(x => x.Id == scoped, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        if (cmd.ExpectedVersion is null || cmd.TargetState is not ("Approved" or "Cancelled") || cmd.EndAt != null || !HrSourcePolicy.ValidReason(cmd.Reason, true))
            return Outcome.Fail(400, "validation_failed", Errors(("targetState", "approved_or_cancelled_expected_version_and_reason_required")));
        if (!Cas(c, row, cmd.ExpectedVersion)) return Outcome.Fail(409, "version_conflict");
        if (!HrAssignmentPolicy.CanTransition(row.State, cmd.TargetState)) return Outcome.Fail(409, "invalid_transition");
        if (cmd.TargetState == "Approved")
        {
            if (!c.Actor.Permissions.Contains("hr.assignment.approve")) return Outcome.Fail(403, "permission_denied");
            var employment = await c.Db.Employments.AsNoTracking().SingleAsync(x => x.Id == row.EmploymentId, c.Ct);
            var operators = row.VesselId is { } vessel ? await c.Db.LegalEntityRoles.AsNoTracking()
                .Where(x => x.LegalEntityId == row.LegalEntityId && x.VesselId == vessel && x.Role == "Operator")
                .Select(x => new { x.EffectiveFrom, x.EffectiveTo }).ToArrayAsync(c.Ct) : [];
            var approved = await c.Db.HrAssignments.AsNoTracking().Where(x => x.PersonId == row.PersonId && x.Id != row.Id && x.State == "Approved")
                .Select(x => new { x.PlannedFrom, x.PlannedTo }).ToArrayAsync(c.Ct);
            var rejected = HrAssignmentPolicy.ApprovalRejection(employment.State, new HrPeriod(employment.StartAt, employment.EndAt),
                new HrPeriod(row.PlannedFrom, row.PlannedTo), row.Kind == "Vessel", operators.Select(x => new HrPeriod(x.EffectiveFrom, x.EffectiveTo)),
                approved.Select(x => new HrPeriod(x.PlannedFrom, x.PlannedTo)), row.PlannedBy, c.ActorId);
            if (rejected != null) return Outcome.Fail(rejected == "separation_of_duties" ? 403 : 409, rejected);
            (row.ApprovedBy, row.ApprovedAt) = (c.ActorId, c.Now);
        }
        // A plan that already has an accepted actual check-in is history, not a cancellable intention.
        if (cmd.TargetState == "Cancelled" && await c.Db.HrActualEvents.AnyAsync(x => x.AssignmentId == row.Id && x.Disposition == "Accepted", c.Ct))
            return Outcome.Fail(409, "hr_actual_exists");
        row.State = cmd.TargetState;
        row.Revision = checked(row.Revision + 1);
        return Outcome.Ok(row.Id);
    }

    private static async Task<Outcome> RecordActual(Ctx c, Guid? id, Guid? _, HrActualEventCommand cmd)
    {
        var row = await c.Db.HrAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, c.Ct);
        if (row == null || row.VesselId is { } scoped && !await c.Actor.Scope(c.Db.Vessels).AnyAsync(x => x.Id == scoped, c.Ct)) return Outcome.Fail(404, "resource_not_found");
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (cmd.Kind is null || !HrActualTimeline.Kinds.Contains(cmd.Kind)) errors["kind"] = ["closed_kind_required"];
        if (cmd.OccurredAt is null || cmd.OccurredAt.Value.Millisecond != 0 || cmd.OccurredAt.Value.Microsecond != 0) errors["occurredAt"] = ["whole_second_instant_required"];
        if (cmd.OriginZoneId is null || !HrLegalEntityPolicy.ValidTimeZone(cmd.OriginZoneId)) errors["originZoneId"] = ["iana_zone_required"];
        // Self facts need an account-person binding (52.02); until then only supervisor-reported facts are accepted.
        if (cmd.Origin != "SupervisorReported") errors["origin"] = ["supervisor_reported_required"];
        if (!HrSourcePolicy.ValidText(cmd.SourceSystem, 64)) errors["sourceSystem"] = ["text_1_64_required"];
        if (!HrSourcePolicy.ValidText(cmd.SourceEventId, 128)) errors["sourceEventId"] = ["text_1_128_required"];
        if (errors.Count > 0) return Outcome.Fail(400, "validation_failed", errors);
        var hash = Hash(JsonSerializer.Serialize(new { assignment = row.Id, cmd.Kind, occurredAt = cmd.OccurredAt!.Value.ToUniversalTime(), cmd.OriginZoneId, cmd.Origin }, Json));
        var duplicate = await c.Db.HrActualEvents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceSystem == cmd.SourceSystem && x.SourceEventId == cmd.SourceEventId, c.Ct);
        if (duplicate != null) return duplicate.ContentHash == hash ? Outcome.Ok(duplicate.Id) : Outcome.Fail(409, "hr_event_conflict");
        var existing = await c.Db.HrActualEvents.AsNoTracking().Where(x => x.AssignmentId == row.Id)
            .Select(x => new HrActualTimeline.Fact(x.Id, x.Kind, x.OccurredAt, x.Quality, x.Disposition)).ToArrayAsync(c.Ct);
        var (quality, disposition) = HrActualTimeline.Classify(existing, cmd.Kind!, cmd.OccurredAt.Value, row.State == "Approved", c.Now);
        var fact = new HrActualEvent { TenantId = c.Actor.TenantId, Id = Guid.NewGuid(), AssignmentId = row.Id, PersonId = row.PersonId, Kind = cmd.Kind!,
            OccurredAt = cmd.OccurredAt.Value, OriginZoneId = cmd.OriginZoneId!, Origin = cmd.Origin!, SourceSystem = cmd.SourceSystem!.Trim(),
            SourceEventId = cmd.SourceEventId!.Trim(), ContentHash = hash, Quality = quality, Disposition = disposition, RecordedBy = c.ActorId, RecordedAt = c.Now };
        c.Db.HrActualEvents.Add(fact);
        return Outcome.Ok(fact.Id);
    }

    // ---------- Representations (fresh, tenant-filtered reads) ----------
    private static HrLegalEntityIdentifierDto IdentifierDto(LegalEntityIdentifier x) =>
        new(x.Id, x.Type, x.Issuer, x.Value, HrIdentifierPolicy.Mask(x.Value), x.SourceKind, x.CreatedAt, x.RetiredAt, x.Version);
    private static HrLegalEntityRoleDto RoleDto(LegalEntityRole x) => new(x.Id, x.Role, x.VesselId, x.EffectiveFrom, x.EffectiveTo, x.VerificationState, x.SourceKind, x.Version);

    private static async Task<object?> LegalEntityRepresentation(Ctx c, Guid id) => await LegalEntity(c.Db, c.Actor, id, c.Ct);
    private static async Task<HrLegalEntityDto?> LegalEntity(PoseidonDbContext db, TrustedActor a, Guid id, CancellationToken ct)
    {
        var e = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (e == null) return null;
        var identifiers = await db.LegalEntityIdentifiers.AsNoTracking().Where(x => x.LegalEntityId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var roles = await db.LegalEntityRoles.AsNoTracking().Where(x => x.LegalEntityId == id).OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).ToArrayAsync(ct);
        // Vessel relations outside the reader's vessel scope are not disclosed.
        var visible = (await a.Scope(db.Vessels.AsNoTracking()).Select(x => x.Id).ToArrayAsync(ct)).ToHashSet();
        return new(e.Id, e.Code, e.Name, e.LegalName, e.CountryCode, e.HrTimeZone, e.BaseLocale, e.LegalEntityStatus, e.EffectiveFrom, e.EffectiveTo,
            e.LegalEntityRevision, e.Version, HrLegalEntityPolicy.MissingForActivation(e.LegalName, e.CountryCode, e.HrTimeZone, e.BaseLocale, e.EffectiveFrom),
            identifiers.Select(IdentifierDto).ToArray(), roles.Where(r => r.VesselId is not { } v || visible.Contains(v)).Select(RoleDto).ToArray(),
            a.HasWriteAttribution && Allowed(a, "hr.legal_entity.write"));
    }
    private static async Task<object?> IdentifierRepresentation(Ctx c, Guid id) =>
        await c.Db.LegalEntityIdentifiers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, c.Ct) is { } row ? IdentifierDto(row) : null;
    private static async Task<object?> RoleRepresentation(Ctx c, Guid id) =>
        await c.Db.LegalEntityRoles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, c.Ct) is { } row ? RoleDto(row) : null;
    private static async Task<object?> PersonRepresentation(Ctx c, Guid id) => await Person(c.Db, c.Actor, id, c.Ct);
    private static async Task<HrPersonDto?> Person(PoseidonDbContext db, TrustedActor a, Guid id, CancellationToken ct) =>
        await db.Persons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } p
            ? new(p.Id, p.PersonType, p.MasterState, p.MasterRevision, p.Version, a.HasWriteAttribution && Allowed(a, "hr.person.write"),
                a.HasWriteAttribution && Allowed(a, "hr.person.verify")) : null;
    private static HrEmploymentDto EmploymentDto(Employment x) => new(x.Id, x.PersonId, x.LegalEntityId, x.EmployeeNo, x.EmploymentType, x.StartAt, x.EndAt, x.State, x.ActivatedAt, x.Revision, x.Version);
    private static async Task<object?> EmploymentRepresentation(Ctx c, Guid id) =>
        await c.Db.Employments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, c.Ct) is { } row ? EmploymentDto(row) : null;
    private static async Task<object?> AssignmentRepresentation(Ctx c, Guid id) => await Assignment(c.Db, c.Actor, id, c.Ct);
    private static async Task<object?> ActualRepresentation(Ctx c, Guid eventId) =>
        await c.Db.HrActualEvents.AsNoTracking().Where(x => x.Id == eventId).Select(x => (Guid?)x.AssignmentId).SingleOrDefaultAsync(c.Ct) is { } assignment
            ? await Assignment(c.Db, c.Actor, assignment, c.Ct) : null;

    private static async Task<HrAssignmentDto?> Assignment(PoseidonDbContext db, TrustedActor a, Guid id, CancellationToken ct)
    {
        var row = await db.HrAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null || row.VesselId is { } vessel && !await a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct)) return null;
        return (await Assignments(db, [row], ct))[0];
    }
    private static async Task<HrAssignmentDto[]> Assignments(PoseidonDbContext db, HrAssignment[] rows, CancellationToken ct)
    {
        var ids = rows.Select(x => x.Id).ToArray();
        var events = await db.HrActualEvents.AsNoTracking().Where(x => ids.Contains(x.AssignmentId)).OrderBy(x => x.OccurredAt).ThenBy(x => x.RecordedAt).ToArrayAsync(ct);
        return rows.Select(row =>
        {
            var own = events.Where(x => x.AssignmentId == row.Id).ToArray();
            var actual = HrActualTimeline.Derive(own.Select(x => new HrActualTimeline.Fact(x.Id, x.Kind, x.OccurredAt, x.Quality, x.Disposition)));
            return new HrAssignmentDto(row.Id, row.EmploymentId, row.PersonId, row.LegalEntityId, row.Kind, row.VesselId, row.PositionId,
                new(row.PlannedFrom, row.PlannedTo, row.PlannedZoneId, row.State, row.PlannedAt, row.ApprovedAt),
                new(actual.Status, actual.ActualFrom, actual.ActualTo, actual.PendingReconciliation, actual.Sessions.Select(s => new HrActualSessionDto(s.ActualFrom, s.ActualTo)).ToArray(),
                    own.Select(x => new HrActualEventDto(x.Id, x.Kind, x.OccurredAt, x.OriginZoneId, x.Origin, x.Quality, x.Disposition, x.RecordedAt)).ToArray()),
                row.Revision, row.Version);
        }).ToArray();
    }

    // ---------- Reads ----------
    private static async Task<(TrustedActor? Actor, IResult? Denied)> Reader(HttpContext h, string permission, Guid? resource = null)
    {
        var a = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
        return Allowed(a, permission) ? (a, null) : (null, await Fail(h, ReadAction, 403, "permission_denied", resource));
    }
    private static async Task<IResult> Found(HttpContext h, object? value, Guid id)
    {
        if (value == null) return await Fail(h, ReadAction, 404, "resource_not_found", id);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, ReadAction, "Success", resourceId: id);
        return Results.Ok(value);
    }
    private static bool Filters(HttpContext h, string[] allowed, out Dictionary<string, Guid> values)
    {
        values = new(StringComparer.Ordinal);
        foreach (var (key, value) in h.Request.Query)
        {
            if (!allowed.Contains(key, StringComparer.Ordinal) || value.Count != 1 || !Guid.TryParseExact(value[0], "D", out var id) || id == Guid.Empty) return false;
            values[key] = id;
        }
        return true;
    }

    private static async Task<IResult> ListLegalEntities(HttpContext h)
    {
        var (a, denied) = await Reader(h, "hr.legal_entity.read"); if (denied != null) return denied;
        if (h.Request.Query.Count != 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var rows = await db.Companies.AsNoTracking().OrderBy(x => x.Code).Take(201).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, ReadAction, "Success");
        return Results.Ok(new HrListDto<HrLegalEntitySummaryDto>(rows.Take(200).Select(e => new HrLegalEntitySummaryDto(e.Id, e.Code, e.Name, e.LegalName,
            e.CountryCode, e.HrTimeZone, e.LegalEntityStatus, e.Version)).ToArray(), rows.Length > 200));
    }
    private static async Task<IResult> GetLegalEntity(Guid id, HttpContext h)
    {
        var (a, denied) = await Reader(h, "hr.legal_entity.read", id); if (denied != null) return denied;
        return await Found(h, await LegalEntity(h.RequestServices.GetRequiredService<PoseidonDbContext>(), a!, id, h.RequestAborted), id);
    }
    private static async Task<IResult> LegalEntityRevisions(Guid id, HttpContext h)
    {
        var (_, denied) = await Reader(h, "hr.legal_entity.read", id); if (denied != null) return denied;
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        if (!await db.Companies.AnyAsync(x => x.Id == id, h.RequestAborted)) return await Fail(h, ReadAction, 404, "resource_not_found", id);
        var rows = await db.LegalEntityFieldRevisions.AsNoTracking().Where(x => x.LegalEntityId == id).OrderByDescending(x => x.Revision).ThenBy(x => x.Field).Take(501).ToArrayAsync(h.RequestAborted);
        return await Found(h, new HrListDto<HrFieldRevisionDto>(rows.Take(500).Select(x => new HrFieldRevisionDto(x.Revision, x.Field, x.Value, x.SourceKind, x.SourceRef, x.ActorId, x.RecordedAt)).ToArray(), rows.Length > 500), id);
    }
    private static async Task<IResult> GetPerson(Guid id, HttpContext h)
    {
        var (a, denied) = await Reader(h, "hr.person.read", id); if (denied != null) return denied;
        return await Found(h, await Person(h.RequestServices.GetRequiredService<PoseidonDbContext>(), a!, id, h.RequestAborted), id);
    }
    private static async Task<IResult> PersonRevisions(Guid id, HttpContext h)
    {
        var (_, denied) = await Reader(h, "hr.person.read", id); if (denied != null) return denied;
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        if (!await db.Persons.AnyAsync(x => x.Id == id, h.RequestAborted)) return await Fail(h, ReadAction, 404, "resource_not_found", id);
        var rows = await db.PersonFieldRevisions.AsNoTracking().Where(x => x.PersonId == id).OrderByDescending(x => x.Revision).ThenBy(x => x.Field).Take(501).ToArrayAsync(h.RequestAborted);
        return await Found(h, new HrListDto<HrFieldRevisionDto>(rows.Take(500).Select(x => new HrFieldRevisionDto(x.Revision, x.Field, x.Value, x.SourceKind, x.SourceRef, x.ActorId, x.RecordedAt)).ToArray(), rows.Length > 500), id);
    }
    private static async Task<IResult> ListEmployments(HttpContext h)
    {
        var (_, denied) = await Reader(h, "hr.employment.read"); if (denied != null) return denied;
        if (!Filters(h, ["personId", "legalEntityId"], out var f) || f.Count == 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var query = db.Employments.AsNoTracking();
        if (f.TryGetValue("personId", out var person)) query = query.Where(x => x.PersonId == person);
        if (f.TryGetValue("legalEntityId", out var entity)) query = query.Where(x => x.LegalEntityId == entity);
        var rows = await query.OrderBy(x => x.StartAt).ThenBy(x => x.Id).Take(201).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, ReadAction, "Success");
        return Results.Ok(new HrListDto<HrEmploymentDto>(rows.Take(200).Select(EmploymentDto).ToArray(), rows.Length > 200));
    }
    private static async Task<IResult> GetEmployment(Guid id, HttpContext h)
    {
        var (_, denied) = await Reader(h, "hr.employment.read", id); if (denied != null) return denied;
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        return await Found(h, await db.Employments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, h.RequestAborted) is { } row ? EmploymentDto(row) : null, id);
    }
    private static async Task<IResult> ListAssignments(HttpContext h)
    {
        var (a, denied) = await Reader(h, "hr.assignment.read"); if (denied != null) return denied;
        if (!Filters(h, ["personId", "employmentId", "vesselId"], out var f) || f.Count == 0) return await Fail(h, ReadAction, 400, "invalid_query");
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        var visible = await a!.Scope(db.Vessels.AsNoTracking()).Select(x => x.Id).ToArrayAsync(h.RequestAborted);
        var query = db.HrAssignments.AsNoTracking().Where(x => x.VesselId == null || visible.Contains(x.VesselId.Value));
        if (f.TryGetValue("personId", out var person)) query = query.Where(x => x.PersonId == person);
        if (f.TryGetValue("employmentId", out var employment)) query = query.Where(x => x.EmploymentId == employment);
        if (f.TryGetValue("vesselId", out var vessel)) query = query.Where(x => x.VesselId == vessel);
        var rows = await query.OrderBy(x => x.PlannedFrom).ThenBy(x => x.Id).Take(201).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, ReadAction, "Success");
        return Results.Ok(new HrListDto<HrAssignmentDto>(await Assignments(db, rows.Take(200).ToArray(), h.RequestAborted), rows.Length > 200));
    }
    private static async Task<IResult> GetAssignment(Guid id, HttpContext h)
    {
        var (a, denied) = await Reader(h, "hr.assignment.read", id); if (denied != null) return denied;
        return await Found(h, await Assignment(h.RequestServices.GetRequiredService<PoseidonDbContext>(), a!, id, h.RequestAborted), id);
    }
}
