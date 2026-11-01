using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompanyCreateCommand(string? Code, string? Name);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompanyUpdateCommand(string? Code, string? Name, uint? ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VesselCreateCommand(string? Code, string? Name, string? Kind, Guid? CompanyId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VesselUpdateCommand(string? Code, string? Name, uint? ExpectedVersion);

public static class DirectoryCommands
{
    public static void MapDirectoryCommands(this IEndpointRouteBuilder app)
    {
        Map(app.MapPost("/api/v1/companies", (HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Execute(http, "CompanyCreate", null)), "CreateCompany", true);
        Map(app.MapPut("/api/v1/companies/{id:guid}", (Guid id, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Execute(http, "CompanyUpdate", id)), "UpdateCompany", true);
        Map(app.MapPost("/api/v1/vessels", (HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Execute(http, "VesselCreate", null)), "CreateVessel", false);
        Map(app.MapPut("/api/v1/vessels/{id:guid}", (Guid id, HttpContext http, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromHeader(Name = "X-CSRF-TOKEN")] string? csrfToken) => Execute(http, "VesselUpdate", id)), "UpdateVessel", false);
    }

    private static void Map(RouteHandlerBuilder route, string name, bool company)
    {
        route.RequireAuthorization("DirectoryRead").WithName(name)
            .WithSummary(name)
            .WithDescription("Online directory command. Requires server-provisioned scope, antiforgery and a 16–80 character Idempotency-Key. Updates require expectedVersion; stale changes return 409. Replay returns the fresh authorized representation.")
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409).Produces(401).Produces(503);
        if (company) route.Produces<CompanyDto>(); else route.Produces<VesselDto>();
        // Explicit request metadata: handlers parse after authorization so malformed writes are audited.
        if (name == "CreateCompany") route.Accepts<CompanyCreateCommand>("application/json");
        else if (company) route.Accepts<CompanyUpdateCommand>("application/json");
        else if (name == "CreateVessel") route.Accepts<VesselCreateCommand>("application/json");
        else route.Accepts<VesselUpdateCommand>("application/json");
    }

    private static IResult Problem(HttpContext http, int status, string code, Dictionary<string, string[]>? errors = null) =>
        Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?>
        { ["code"] = code, ["errors"] = errors ?? [], ["correlationId"] = http.TraceIdentifier });

    private static async Task<IResult> Execute(HttpContext http, string action, Guid? id)
    {
        var actor = http.RequestServices.GetRequiredService<ActorResolver>().Resolve(http.User)!;
        var audit = http.RequestServices.GetRequiredService<SecurityAudit>();
        async Task<IResult> Fail(int status, string code, Dictionary<string, string[]>? fields = null)
        {
            await audit.WriteAsync(http, action, code, resourceId: id);
            if (code == "duplicate_code" && fields == null) fields = new() { ["code"] = ["duplicate_code"] };
            return Problem(http, status, code, fields);
        }
        var company = action.StartsWith("Company", StringComparison.Ordinal);
        var update = id.HasValue;
        // Metadata audit must persist before parsing or changing business data. Never label this Success.
        await audit.WriteAsync(http, action, "Attempted", resourceId: id);
        if ((!update || company) && !actor.CanManage) return await Fail(403, "permission_denied");
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { return await Fail(403, "csrf_invalid"); }
        if (!http.Request.HasJsonContentType()) return await Fail(400, "invalid_request");
        if (http.Request.ContentLength is > 4096) return await Fail(400, "invalid_request");
        string? code, name, kind = null; Guid? companyId = null; uint? version = null;
        try
        {
            // Bound even chunked requests; JSON has no mass assignment path to persistence entities.
            using var buffer = new MemoryStream();
            var bytes = new byte[1024]; int read;
            while ((read = await http.Request.Body.ReadAsync(bytes, http.RequestAborted)) > 0)
            {
                if (buffer.Length + read > 4096) return await Fail(400, "invalid_request");
                buffer.Write(bytes, 0, read);
            }
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 4 };
            if (company && !update)
            {
                var body = JsonSerializer.Deserialize<CompanyCreateCommand>(buffer.ToArray(), json) ?? throw new JsonException();
                (code, name) = (body.Code, body.Name);
            }
            else if (company)
            {
                var body = JsonSerializer.Deserialize<CompanyUpdateCommand>(buffer.ToArray(), json) ?? throw new JsonException();
                (code, name, version) = (body.Code, body.Name, body.ExpectedVersion);
            }
            else if (update)
            {
                var body = JsonSerializer.Deserialize<VesselUpdateCommand>(buffer.ToArray(), json) ?? throw new JsonException();
                (code, name, version) = (body.Code, body.Name, body.ExpectedVersion);
            }
            else
            {
                var body = JsonSerializer.Deserialize<VesselCreateCommand>(buffer.ToArray(), json) ?? throw new JsonException();
                (code, name, kind, companyId) = (body.Code, body.Name, body.Kind, body.CompanyId);
            }
        }
        catch (JsonException) { return await Fail(400, "invalid_request"); }
        var errors = Poseidon.Domain.DirectoryInputPolicy.Validate(code, name, version, update);
        if (!company && !update)
        {
            if (kind is not ("Ship" or "Tug")) errors["kind"] = ["explicit_ship_or_tug_required"];
            if (companyId is null || companyId == Guid.Empty) errors["companyId"] = ["required"];
        }
        if (errors.Count > 0) return await Fail(400, "validation_failed", errors);
        code = code!.Trim(); name = name!.Trim();
        var keys = http.Request.Headers["Idempotency-Key"];
        var key = keys.Count == 1 ? keys[0] : null;
        if (key is null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return await Fail(400, "invalid_idempotency_key");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { action, id, code, name, kind, companyId, version }))));
        var db = http.RequestServices.GetRequiredService<PoseidonDbContext>();
        Company? targetCompany = null; Vessel? targetVessel = null;
        // Fresh object authorization precedes receipt reads, including retries after revocation.
        if (update)
        {
            if (company) targetCompany = await db.Companies.SingleOrDefaultAsync(x => x.Id == id, http.RequestAborted);
            else targetVessel = await actor.Scope(db.Vessels).SingleOrDefaultAsync(x => x.Id == id, http.RequestAborted);
            if (targetCompany == null && targetVessel == null) return await Fail(404, "resource_not_found");
            if (!company && !actor.CanEditVessel(id!.Value)) return await Fail(403, "permission_denied");
        }
        if (!company && !update && !await db.Companies.AnyAsync(x => x.Id == companyId, http.RequestAborted))
            return await Fail(404, "resource_not_found");

        async Task<IResult?> Replay()
        {
            var receipt = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == actor.Id && x.Key == key, http.RequestAborted);
            if (receipt == null) return null;
            if (receipt.Action != action || receipt.Fingerprint != fingerprint) return await Fail(409, "idempotency_key_reused");
            http.Response.Headers["Idempotency-Replayed"] = "true";
            // Return the fresh authorized representation; receipts contain no business body/snapshot.
            if (company)
            {
                var row = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.ResourceId, http.RequestAborted);
                return row == null ? await Fail(404, "resource_not_found") : Results.Ok(new CompanyDto(row.Id, row.Code, row.Name, row.Version, actor.CanManage));
            }
            var vessel = await actor.Scope(db.Vessels.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == receipt.ResourceId, http.RequestAborted);
            return vessel == null ? await Fail(404, "resource_not_found") : Results.Ok(new VesselDto(vessel.Id, vessel.CompanyId, vessel.Code, vessel.Name, vessel.Kind.ToString(), vessel.Version, actor.CanEditVessel(vessel.Id)));
        }
        var replay = await Replay(); if (replay != null) return replay;
        if (update && (company ? targetCompany!.Version : targetVessel!.Version) != version) return await Fail(409, "version_conflict");
        await using var transaction = await db.Database.BeginTransactionAsync(http.RequestAborted);
        try
        {
            var resource = id ?? Guid.NewGuid();
            if (company)
            {
                if (targetCompany == null)
                {
                    targetCompany = new Company { Id = resource, TenantId = actor.TenantId, Code = code, Name = name };
                    db.Companies.Add(targetCompany);
                }
                else
                {
                    targetCompany.Code = code; targetCompany.Name = name;
                    db.Entry(targetCompany).Property(x => x.Version).OriginalValue = version!.Value;
                    // An accepted unchanged update is an audited touch with an xmin check.
                    db.Entry(targetCompany).Property(x => x.Name).IsModified = true;
                }
            }
            else
            {
                if (targetVessel == null)
                {
                    targetVessel = new Vessel { Id = resource, TenantId = actor.TenantId, CompanyId = companyId!.Value, Code = code, Name = name, Kind = kind == "Ship" ? VesselKind.Ship : VesselKind.Tug };
                    db.Vessels.Add(targetVessel);
                    // Regulatory measurements remain unknown; no zero-valued operational profiles are invented.
                }
                else
                {
                    targetVessel.Code = code; targetVessel.Name = name;
                    db.Entry(targetVessel).Property(x => x.Version).OriginalValue = version!.Value;
                    db.Entry(targetVessel).Property(x => x.Name).IsModified = true;
                }
            }
            await db.SaveChangesAsync(http.RequestAborted);
            db.CommandReceipts.Add(new CommandReceipt { TenantId = actor.TenantId, ActorId = actor.Id, Key = key, Action = action,
                Fingerprint = fingerprint, ResourceId = resource, CommittedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(http.RequestAborted);
            await transaction.CommitAsync(http.RequestAborted);
            // No independent HTTP Success before commit, and no fallible audit write after commit.
            return company ? Results.Ok(new CompanyDto(resource, code, name, targetCompany!.Version, actor.CanManage)) :
                Results.Ok(new VesselDto(resource, targetVessel!.CompanyId, code, name, targetVessel.Kind.ToString(), targetVessel.Version, actor.CanEditVessel(resource)));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(409, "version_conflict");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay() ?? await Fail(409, "duplicate_code");
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return await Fail(503, "service_unavailable");
        }
    }
}
