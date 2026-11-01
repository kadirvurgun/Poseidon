using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentCommand(Guid Id, uint? BaseVersion, Guid? ParentId, string? Code, string? Name,
    string Level, string? SystemFamily, string? EquipmentType, string? Criticality, string? Maker,
    string? Model, string? SerialNumber, string? Location, DateOnly? CommissionDate, Guid? ResponsibleRoleId, string? Notes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenanceCommand(Guid Id, uint? BaseVersion, Guid AssetId, Guid? JobId,
    Guid? OwnerRoleId, MaintenanceInputs Inputs, DateTimeOffset ReferenceAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MaintenanceJobCommand(Guid Id, string Code, string Title);
public sealed record TechnicalSaved(Guid Id, long Revision, uint Version, bool Replayed);
public sealed record EquipmentSnapshot(Asset[] Items, bool Complete, bool CanWrite, bool CanReadHistory);
public sealed record EquipmentHistoryRow(long Revision, DateTimeOffset SavedAt, Asset Asset);
public sealed record MaintenanceRow(Guid Id, Guid AssetId, Guid? JobId, Guid? OwnerRoleId, uint Version, long Revision,
    string Status, string Provenance, MaintenanceInputs Inputs, MaintenanceDue Due, DateTimeOffset ReferenceAt, DateTimeOffset SavedAt, long AssetRevision);
public sealed record MaintenancePage(MaintenanceRow[] Items, bool CanWrite, bool CanReadHistory);
public sealed record MaintenanceJobRow(Guid Id, string Code, string Title, string Provenance);
public sealed record TechnicalReferences(MaintenanceJobRow[] Jobs, Guid[] Owners, string Policy);
public sealed record MaintenanceHistoryRow(long Revision, Guid AssetId, long AssetRevision, Guid? JobId, Guid? OwnerRoleId,
    DateTimeOffset ReferenceAt, DateTimeOffset SavedAt, string Provenance, MaintenanceInputs Inputs, MaintenanceDue Due);

public static class TechnicalEndpoints
{
    public static void MapTechnical(this IEndpointRouteBuilder app)
    {
        // Current screen contract and older normalized-resource names share handlers/storage.
        // Root aliases take explicit ?vessel=UUID; tenant/actor always come from the server.
        app.MapGet("/api/v1/equipment-hierarchy", Equipment).RequireAuthorization("DirectoryRead").WithName("EquipmentCanonicalRead").Produces<EquipmentSnapshot>();
        app.MapPost("/api/v1/equipment-hierarchy", SaveEquipment).RequireAuthorization("DirectoryRead").WithName("EquipmentCanonicalSave").Produces<TechnicalSaved>();
        app.MapGet("/api/v1/assets", Equipment).RequireAuthorization("DirectoryRead").WithName("EquipmentAssetsRead").Produces<EquipmentSnapshot>();
        app.MapPost("/api/v1/assets", SaveEquipment).RequireAuthorization("DirectoryRead").WithName("EquipmentAssetsSave").Produces<TechnicalSaved>();
        app.MapGet("/api/v1/maintenance-plan", Plans).RequireAuthorization("DirectoryRead").WithName("MaintenanceCanonicalRead").Produces<MaintenancePage>();
        app.MapPost("/api/v1/maintenance-plan", SavePlan).RequireAuthorization("DirectoryRead").WithName("MaintenanceCanonicalSave").Produces<TechnicalSaved>();
        app.MapGet("/api/v1/maintenance-plans", Plans).RequireAuthorization("DirectoryRead").WithName("MaintenanceAliasRead").Produces<MaintenancePage>();
        app.MapPost("/api/v1/maintenance-plans", SavePlan).RequireAuthorization("DirectoryRead").WithName("MaintenanceAliasSave").Produces<TechnicalSaved>();
        var assets = app.MapGroup("/api/v1/equipment-hierarchy/{vessel:guid}").RequireAuthorization("DirectoryRead");
        assets.MapGet("", Equipment).WithName("EquipmentRead").Produces<EquipmentSnapshot>();
        assets.MapPost("", SaveEquipment).WithName("EquipmentSave").Produces<TechnicalSaved>();
        assets.MapGet("/{id:guid}/history", EquipmentHistory).WithName("EquipmentHistory").Produces<EquipmentHistoryRow[]>();
        assets.MapGet("/owners", EquipmentOwners).WithName("EquipmentOwners");
        var plans = app.MapGroup("/api/v1/maintenance-plan/{vessel:guid}").RequireAuthorization("DirectoryRead");
        plans.MapGet("", Plans).WithName("MaintenanceRead").Produces<MaintenancePage>();
        plans.MapPost("", SavePlan).WithName("MaintenanceSave").Produces<TechnicalSaved>();
        plans.MapGet("/references", References).WithName("MaintenanceReferences").Produces<TechnicalReferences>();
        plans.MapPost("/jobs", AddJob).WithName("MaintenanceJobSave");
        plans.MapGet("/{id:guid}/history", PlanHistory).WithName("MaintenanceHistory").Produces<MaintenanceHistoryRow[]>();
        plans.MapPost("/{id:guid}/postpone", RejectPostponement).WithName("MaintenancePostpone");
    }
    private static TrustedActor Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
    private static bool Read(TrustedActor a, string module) => a.CanRead && a.Permissions.Contains(module + ".read");
    private static bool Write(TrustedActor a, Guid vessel, string module) => Read(a, module) && a.HasWriteAttribution &&
        a.Permissions.Contains(module + ".write") && (a.TenantWideManage || a.VesselGrants.Any(g => g.VesselId == vessel && g.CanEdit));
    private static Task<bool> Scope(PoseidonDbContext db, TrustedActor a, Guid vessel, CancellationToken ct) =>
        a.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, ct);
    private static string Action(HttpContext h) => h.Request.Path.Value?.Contains("/imports/", StringComparison.Ordinal) == true || h.Request.Path.StartsWithSegments("/api/v1/equipment-hierarchy") || h.Request.Path.StartsWithSegments("/api/v1/assets")
        ? h.Request.Method == "GET" ? "EquipmentRead" : "EquipmentSave"
        : h.Request.Method == "GET" ? "MaintenanceRead" : "MaintenanceSave";
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Action(h), code);
        h.Items["TechnicalActionObserved"] = true;
        return Results.Json(new { code, correlationId = h.TraceIdentifier }, statusCode: status);
    }
    private static Task Observe(HttpContext h, Guid vessel, string result = "Success") =>
        h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, Action(h), result, resourceId: vessel);
    private static async Task<IResult?> Gate(HttpContext h, PoseidonDbContext db, Guid vessel, string module, bool write = false) {
        var a = Actor(h);
        if (!Read(a, module) || write && !Write(a, vessel, module)) return await Fail(h, 403, "permission_denied");
        if (!await Scope(db, a, vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        if (write) {
            try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
            catch (AntiforgeryValidationException) { return await Fail(h, 400, "csrf_invalid"); }
        }
        return null;
    }
    private static async Task<IResult> Equipment(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "equipment"); if (failure != null) return failure;
        // Complete snapshot in one statement. Never return a paged forest that silently loses ancestors.
        var rows = await db.Assets.AsNoTracking().Where(x => x.VesselId == vessel).OrderBy(x => x.Code).ThenBy(x => x.Id).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel);
        return Results.Ok(new EquipmentSnapshot(rows, true, Write(Actor(h), vessel, "equipment"), Actor(h).Permissions.Contains("equipment.history.read")));
    }
    private static async Task<IResult> EquipmentHistory(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "equipment"); if (failure != null) return failure;
        if (!Actor(h).Permissions.Contains("equipment.history.read")) return await Fail(h, 403, "permission_denied");
        if (!await db.Assets.AnyAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var rows = await db.AssetRevisions.AsNoTracking().Where(x => x.AssetId == id && x.VesselId == vessel).OrderByDescending(x => x.Revision).Take(100).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel); return Results.Ok(rows.Select(x => new EquipmentHistoryRow(x.Revision, x.SavedAt, JsonSerializer.Deserialize<Asset>(x.ContentJson)!)).ToArray());
    }
    // Eligible role references are computed from server provisioning for this tenant/vessel each time,
    // including replay; no names, subject IDs or credential data leave the server.
    internal static Guid[] EligibleOwners(HttpContext h, Guid vessel) {
        var a = Actor(h);
        return h.RequestServices.GetRequiredService<FederationSettings>().Memberships
            .Where(m => m.TenantId == a.TenantId && m.ActorId != Guid.Empty && m.Permissions.Contains("maintenance.owner.eligible") &&
                m.Permissions.Contains("directory.read") && (m.TenantWideManage || m.TenantWideRead || (m.VesselGrants ?? []).Any(g => g.VesselId == vessel)))
            .Where(m => ActorResolver.TrustedRoleContext(m.RoleReferences) != null)
            .SelectMany(m => m.RoleReferences!).Select(Guid.Parse).Distinct().Order().ToArray();
    }
    private static async Task EnsureOwner(PoseidonDbContext db, Guid vessel, Guid? role, CancellationToken ct) {
        if (role == null || await db.TechnicalOwners.AnyAsync(x => x.VesselId == vessel && x.Id == role, ct)) return;
        db.TechnicalOwners.Add(new() { TenantId = db.TenantId, VesselId = vessel, Id = role.Value });
    }
    private static async Task<IResult> EquipmentOwners(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "equipment"); if (failure != null) return failure;
        await Observe(h, vessel); return Results.Ok(EligibleOwners(h, vessel));
    }
    private static bool Key(HttpContext h, out string key) {
        key = h.Request.Headers["Idempotency-Key"].ToString(); return Guid.TryParseExact(key, "D", out _);
    }
    private static string Fingerprint(Guid vessel, object command) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { vessel, command })));
    private static async Task<IResult?> Replay(PoseidonDbContext db, HttpContext h, Guid vessel, string key, string fingerprint, string action, Guid id) {
        var a = Actor(h);
        var r = await db.CommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, h.RequestAborted);
        if (r == null) return null;
        if (r.Action != action || r.Fingerprint != fingerprint || r.ResourceId != id) return await Fail(h, 409, "idempotency_key_reused");
        var exists = action == "EquipmentSave" ? await db.Assets.AnyAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted)
            : await db.MaintenancePlans.AnyAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted);
        if (!exists) return await Fail(h, 404, "resource_not_found");
        await Observe(h, vessel); return Results.Ok(new TechnicalSaved(id, r.RegisterRevision ?? 0, r.RegisterVersion ?? 0, true));
    }
    private static async Task Receipt(PoseidonDbContext db, HttpContext h, string key, string fingerprint, string action, Guid id, long revision, uint version) {
        db.CommandReceipts.Add(new() { TenantId = db.TenantId, ActorId = Actor(h).Id, Key = key, Fingerprint = fingerprint,
            Action = action, ResourceId = id, RegisterRevision = revision, RegisterVersion = version, CommittedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(h.RequestAborted);
    }
    private static bool Texts(params string?[] values) => values.All(x => x == null || x.Length <= 2000 && !x.Any(char.IsControl));
    internal static bool ValidEquipmentInput(EquipmentCommand command) => command.Id != Guid.Empty && command.ParentId != Guid.Empty &&
        command.Level is "System" or "Equipment" or "Component" && Texts(command.Code, command.Name, command.SystemFamily, command.EquipmentType,
            command.Criticality, command.Maker, command.Model, command.SerialNumber, command.Location, command.Notes);
    private static Task<IResult> SaveEquipment(Guid vessel, EquipmentCommand command, HttpContext h, PoseidonDbContext db) => ApplyImport(vessel, command, h, db, false);
    internal static async Task<IResult> ApplyImport(Guid vessel, EquipmentCommand command, HttpContext h, PoseidonDbContext db, bool imported) {
        var failure = await Gate(h, db, vessel, "equipment", true); if (failure != null) return failure;
        if (!ValidEquipmentInput(command)) return await Fail(h, 400, "validation_failed");
        if (command.ResponsibleRoleId != null && !EligibleOwners(h, vessel).Contains(command.ResponsibleRoleId.Value)) return await Fail(h, 403, "permission_denied");
        if (!Key(h, out var key)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Fingerprint(vessel, command); var ct = h.RequestAborted;
        await Observe(h, vessel, "Attempted");
        // SSI reads the entire authorized vessel predicate before any edge is changed. Concurrent
        // disjoint A->B / B->A changes cannot both commit; client retries must reload after 409.
        await using var tx = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        try {
            var replay = await Replay(db, h, vessel, key, fingerprint, "EquipmentSave", command.Id); if (replay != null) return replay;
            var rows = await db.Assets.Where(x => x.VesselId == vessel).ToListAsync(ct);
            var asset = rows.SingleOrDefault(x => x.Id == command.Id);
            if (asset == null && command.BaseVersion != null || asset != null && asset.Version != command.BaseVersion) return await Fail(h, 409, "version_conflict");
            if (asset == null) { asset = new() { TenantId = db.TenantId, VesselId = vessel, Id = command.Id }; rows.Add(asset); db.Assets.Add(asset); }
            var nodes = rows.Select(x => new EquipmentHierarchyNode(x.TenantId, x.VesselId, x.Id, x.ParentId)).ToArray();
            if (EquipmentHierarchyPolicy.ValidateReparent(db.TenantId, vessel, nodes, command.Id, command.ParentId) != EquipmentHierarchyValidation.Valid)
                return await Fail(h, 400, "validation_failed");
            await EnsureOwner(db, vessel, command.ResponsibleRoleId, ct);
            if (command.BaseVersion != null) asset.Revision = checked(asset.Revision + 1);
            asset.ParentId = command.ParentId; asset.Code = command.Code; asset.Name = command.Name; asset.Level = command.Level;
            asset.SystemFamily = command.SystemFamily; asset.EquipmentType = command.EquipmentType; asset.Criticality = command.Criticality;
            asset.Maker = command.Maker; asset.Model = command.Model; asset.SerialNumber = command.SerialNumber; asset.Location = command.Location;
            asset.CommissionDate = command.CommissionDate; asset.ResponsibleRoleId = command.ResponsibleRoleId; asset.Notes = command.Notes;
            asset.Provenance = imported ? "ImportedDeclaredUnverified" : asset.Provenance;
            db.AssetRevisions.Add(asset.Snapshot()); await db.SaveChangesAsync(ct);
            await Receipt(db, h, key, fingerprint, "EquipmentSave", asset.Id, asset.Revision, asset.Version);
            if (tx != null) await tx.CommitAsync(ct); return Results.Ok(new TechnicalSaved(asset.Id, asset.Revision, asset.Version, false));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            if (tx != null) await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict");
        }
    }
    private static async Task<IResult> Plans(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance"); if (failure != null) return failure;
        var rows = await (from p in db.MaintenancePlans.AsNoTracking().Where(x => x.VesselId == vessel)
            join r in db.MaintenancePlanRevisions.AsNoTracking() on new { p.TenantId, p.VesselId, PlanId = p.Id, p.Revision } equals new { r.TenantId, r.VesselId, r.PlanId, r.Revision }
            orderby p.Id select new { p.Id, p.AssetId, p.JobId, p.OwnerRoleId, p.Version, p.Revision, p.Status, p.Provenance,
                r.ContentJson, r.CalculationJson, r.ReferenceAt, r.SavedAt, r.AssetRevision }).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel);
        return Results.Ok(new MaintenancePage(rows.Select(p => new MaintenanceRow(p.Id, p.AssetId, p.JobId, p.OwnerRoleId, p.Version, p.Revision, p.Status, p.Provenance,
            JsonSerializer.Deserialize<MaintenanceInputs>(p.ContentJson)!, JsonSerializer.Deserialize<MaintenanceDue>(p.CalculationJson)!, p.ReferenceAt, p.SavedAt, p.AssetRevision)).ToArray(),
            Write(Actor(h), vessel, "maintenance"), Actor(h).Permissions.Contains("maintenance.history.read")));
    }
    private static async Task<IResult> References(Guid vessel, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance"); if (failure != null) return failure;
        var jobs = await db.MaintenanceJobs.AsNoTracking().Where(x => x.VesselId == vessel).Select(x => new MaintenanceJobRow(x.Id, x.Code, x.Title, x.Provenance)).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel); return Results.Ok(new TechnicalReferences(jobs, EligibleOwners(h, vessel), "DraftDeclarationsOnly;PostponementPolicyUnresolved"));
    }
    private static async Task<IResult> AddJob(Guid vessel, MaintenanceJobCommand command, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance", true); if (failure != null) return failure;
        if (command.Id == Guid.Empty || string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Title) || !Texts(command.Code, command.Title)) return await Fail(h, 400, "validation_failed");
        var old = await db.MaintenanceJobs.SingleOrDefaultAsync(x => x.Id == command.Id && x.VesselId == vessel, h.RequestAborted);
        if (old != null) {
            if (old.Code != command.Code || old.Title != command.Title) return await Fail(h, 409, "version_conflict");
            await Observe(h, vessel); return Results.Ok(new { old.Id });
        }
        await Observe(h, vessel, "Attempted");
        db.MaintenanceJobs.Add(new() { TenantId = db.TenantId, VesselId = vessel, Id = command.Id, Code = command.Code, Title = command.Title });
        try { await db.SaveChangesAsync(h.RequestAborted); return Results.Ok(new { command.Id }); }
        catch (DbUpdateException) { return await Fail(h, 409, "version_conflict"); }
    }
    private static async Task<IResult> PlanHistory(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance"); if (failure != null) return failure;
        if (!Actor(h).Permissions.Contains("maintenance.history.read")) return await Fail(h, 403, "permission_denied");
        if (!await db.MaintenancePlans.AnyAsync(x => x.VesselId == vessel && x.Id == id, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var rows = await db.MaintenancePlanRevisions.AsNoTracking().Where(x => x.VesselId == vessel && x.PlanId == id).OrderByDescending(x => x.Revision).Take(100).ToArrayAsync(h.RequestAborted);
        await Observe(h, vessel); return Results.Ok(rows.Select(x => new MaintenanceHistoryRow(x.Revision, x.AssetId, x.AssetRevision, x.JobId, x.OwnerRoleId, x.ReferenceAt, x.SavedAt, x.Provenance,
            JsonSerializer.Deserialize<MaintenanceInputs>(x.ContentJson)!, JsonSerializer.Deserialize<MaintenanceDue>(x.CalculationJson)!)).ToArray());
    }
    private static async Task<IResult> SavePlan(Guid vessel, MaintenanceCommand command, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance", true); if (failure != null) return failure;
        if (command.Id == Guid.Empty || command.Inputs == null || command.ReferenceAt == default || command.ReferenceAt > DateTimeOffset.UtcNow ||
            command.Inputs.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)).Any(p => !Texts((string?)p.GetValue(command.Inputs))))
            return await Fail(h, 400, "validation_failed");
        foreach (var number in new[] { command.Inputs.IntervalValue, command.Inputs.LastDoneMeter, command.Inputs.CurrentMeter })
            if (number != null && !MaintenanceCalculation.Number(number, out _)) return await Fail(h, 400, "validation_failed");
        if (command.Inputs.IntervalValue != null && MaintenanceCalculation.Number(command.Inputs.IntervalValue, out var interval) && interval <= 0)
            return await Fail(h, 400, "validation_failed");
        if (command.OwnerRoleId != null && !EligibleOwners(h, vessel).Contains(command.OwnerRoleId.Value)) return await Fail(h, 403, "permission_denied");
        if (!Key(h, out var key)) return await Fail(h, 400, "invalid_idempotency_key");
        var fingerprint = Fingerprint(vessel, command); var ct = h.RequestAborted;
        await Observe(h, vessel, "Attempted");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try {
            var replay = await Replay(db, h, vessel, key, fingerprint, "MaintenanceSave", command.Id); if (replay != null) return replay;
            var asset = await db.Assets.SingleOrDefaultAsync(x => x.Id == command.AssetId && x.VesselId == vessel, ct);
            if (asset == null || command.JobId != null && !await db.MaintenanceJobs.AnyAsync(x => x.VesselId == vessel && x.Id == command.JobId, ct)) return await Fail(h, 404, "resource_not_found");
            var plan = await db.MaintenancePlans.SingleOrDefaultAsync(x => x.Id == command.Id && x.VesselId == vessel, ct);
            if (plan == null && command.BaseVersion != null || plan != null && plan.Version != command.BaseVersion) return await Fail(h, 409, "version_conflict");
            await EnsureOwner(db, vessel, command.OwnerRoleId, ct);
            if (plan == null) { plan = new() { TenantId = db.TenantId, Id = command.Id, VesselId = vessel, AssetId = asset.Id }; db.MaintenancePlans.Add(plan); }
            else plan.Revision = checked(plan.Revision + 1);
            plan.AssetId = asset.Id; plan.JobId = command.JobId; plan.OwnerRoleId = command.OwnerRoleId;
            plan.ContentJson = JsonSerializer.Serialize(command.Inputs);
            db.MaintenancePlanRevisions.Add(plan.Snapshot(command.ReferenceAt.ToUniversalTime(), asset.Revision));
            await db.SaveChangesAsync(ct); await Receipt(db, h, key, fingerprint, "MaintenanceSave", plan.Id, plan.Revision, plan.Version);
            await tx.CommitAsync(ct); return Results.Ok(new TechnicalSaved(plan.Id, plan.Revision, plan.Version, false));
        } catch (Exception e) when (e is DbUpdateException || e is Npgsql.PostgresException { SqlState: "40001" or "40P01" }) {
            await tx.RollbackAsync(ct); return await Fail(h, 409, "version_conflict");
        }
    }
    private static async Task<IResult> RejectPostponement(Guid vessel, Guid id, HttpContext h, PoseidonDbContext db) {
        var failure = await Gate(h, db, vessel, "maintenance", true); if (failure != null) return failure;
        if (!await db.MaintenancePlans.AnyAsync(x => x.Id == id && x.VesselId == vessel, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        // DEV-109 source requires authority/risk/evidence/max-extension policy. None is supplied by
        // an arbitrary request reason or draft approval_rule. No postponed deadline can be activated.
        return await Fail(h, 422, "validation_failed");
    }
}
