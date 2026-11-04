using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DailyRecordDto(Guid Id, Guid VesselId, string Kind, long Revision, uint Version,
    SortedDictionary<string,DailyValue> Fields, string State, string Provenance, DateTimeOffset SavedAt,
    string Hash, string ReviewState, long? ReviewedRevision, Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string[] Blockers);
public sealed record DailyOfflineDto(bool Supported, string State, string Operation);
public sealed record DailyDestinationDto(string Key, string Href);
public sealed record DailyCapabilitiesDto(Guid VesselId, string Kind, bool CanRead, bool CanWrite, bool CanReview,
    bool CanSubmit, DailyFieldDefinition[] Schema, string SourceVersion, DailyOfflineDto Offline, DailyDestinationDto[] Destinations);
public sealed record DailyPageDto(DailyRecordDto[] Items, int Offset, int Limit, bool HasMore,
    bool CanWrite, bool CanReview, bool CanSubmit, DailyOfflineDto Offline);
public sealed record DailyHistoryDto(DailyRecordDto[] Items, int Offset, int Limit, bool HasMore);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DailyDraftCommand([property:JsonRequired] Guid Id, [property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] Dictionary<string,DailyValue> Fields, [property:JsonRequired] string ReasonCode);

// These routes own declarations, never ship readiness, signatures or safety approval.
public static class DailyOperationsEndpoints
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 6, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly (string Kind,string Route,string Destination)[] Families = [
        ("Noon","noon-reports","noon-report"), ("Deck","deck-daily-logs","deck-daily-log"), ("Engine","engine-daily-logs","engine-daily-log")];
    public static void MapDailyOperations(this IEndpointRouteBuilder app) {
        foreach (var family in Families) {
            var kind = family.Kind;
            var group = app.MapGroup("/api/v1/" + family.Route).RequireAuthorization("DirectoryRead");
            group.MapGet("", (HttpContext h, PoseidonDbContext db) => Read(kind,null,false,false,h,db)).WithName(kind + "DailyList").Produces<DailyPageDto>();
            group.MapGet("/capabilities", (HttpContext h, PoseidonDbContext db) => Read(kind,null,false,true,h,db)).WithName(kind + "DailyCapabilities").Produces<DailyCapabilitiesDto>();
            group.MapGet("/{id:guid}", (Guid id,HttpContext h,PoseidonDbContext db) => Read(kind,id,false,false,h,db)).WithName(kind + "DailyRead").Produces<DailyRecordDto>();
            group.MapGet("/{id:guid}/history", (Guid id,HttpContext h,PoseidonDbContext db) => Read(kind,id,true,false,h,db)).WithName(kind + "DailyHistory").Produces<DailyHistoryDto>();
            group.MapPost("", (HttpContext h,PoseidonDbContext db) => Command(kind,null,h,db)).WithName(kind + "DailyCreate").Accepts<DailyDraftCommand>("application/json").Produces<DailyRecordDto>();
            group.MapPut("/{id:guid}", (Guid id,HttpContext h,PoseidonDbContext db) => Command(kind,id,h,db)).WithName(kind + "DailyUpdate").Accepts<DailyDraftCommand>("application/json").Produces<DailyRecordDto>();
        }
    }
    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static string Permission(string kind,string action) => "daily." + kind.ToLowerInvariant() + "." + action;
    internal static bool CanRead(TrustedActor? a,string kind) => a?.CanRead == true && a.Permissions.Contains(Permission(kind,"read"));
    internal static bool CanWrite(TrustedActor? a,string kind,Guid vessel) => CanRead(a,kind) && a!.HasWriteAttribution && a.CanEditVessel(vessel) && a.Permissions.Contains(Permission(kind,"write"));
    internal static string Rights(TrustedActor a) => RegisterStorage.Hash(JsonSerializer.Serialize(new {
        a.Id,a.TenantId,a.RoleContext,a.TenantWideRead,a.TenantWideManage,
        permissions=a.Permissions.Order(StringComparer.Ordinal), vessels=a.VesselGrants.OrderBy(v=>v.VesselId).ThenBy(v=>v.CanEdit)
    },Json));
    private static IQueryable<Vessel> Vessels(PoseidonDbContext db,TrustedActor actor,Guid vessel) =>
        actor.Scope(db.Vessels.AsNoTracking()).Where(v => v.Id == vessel && (v.Kind == VesselKind.Ship || actor.Permissions.Contains("mode.cross")));
    internal static async Task<bool> Current(HttpContext h,string kind,Guid vessel,string rights,bool write = false) {
        // Re-open outside a transaction's old snapshot. No private reference is
        // accepted as Known in this version, so vessel+rights is the owning boundary.
        await using var scope = h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ActorResolver>(); var a = resolver.Resolve(h.User);
        if (!CanRead(a,kind) || Rights(a!) != rights || write && !CanWrite(a,kind,vessel)) return false;
        var found = await Vessels(scope.ServiceProvider.GetRequiredService<PoseidonDbContext>(),a!,vessel).AnyAsync(h.RequestAborted);
        var after = resolver.Resolve(h.User);
        return found && CanRead(after,kind) && Rights(after!) == rights && (!write || CanWrite(after,kind,vessel));
    }
    private static bool Query(HttpContext h,bool paging,out Guid vessel,out int offset) {
        offset=0; vessel=Guid.Empty;
        return h.Request.Query.All(q=>q.Value.Count == 1 && (q.Key=="vesselId" || paging && q.Key=="offset")) &&
            Guid.TryParseExact(h.Request.Query["vesselId"],"D",out vessel) && vessel!=Guid.Empty &&
            (!h.Request.Query.ContainsKey("offset") || int.TryParse(h.Request.Query["offset"],NumberStyles.None,CultureInfo.InvariantCulture,out offset) && offset<=100000);
    }
    private static Task Audit(HttpContext h,string result,Guid? id = null) => h.RequestServices.GetRequiredService<SecurityAudit>()
        .WriteAsync(h,h.Request.Method == "GET" ? "DailyOperationsRead" : "DailyOperationsCommand",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code) {
        await Audit(h,code); return Results.Json(new {code},statusCode:status);
    }
    private static DailyOfflineDto Offline(string kind) => new(false,"UNAVAILABLE_POLICY","daily-operations." + kind.ToLowerInvariant() + "-draft.v1");
    internal static DailyRecordDto Dto(DailyOperation row) => Dto(row.Id,row.VesselId,row.Kind,row.Revision,row.Version,row.FieldsJson,row.Provenance,row.SavedAt,row.Hash);
    private static DailyRecordDto Dto(DailyOperationRevision row) => Dto(row.RecordId,row.VesselId,row.Kind,row.Revision,checked((uint)row.RecordVersion),row.FieldsJson,row.Provenance,row.SavedAt,row.Hash);
    private static DailyRecordDto Dto(Guid id,Guid vessel,string kind,long revision,uint version,string json,string provenance,DateTimeOffset saved,string hash) {
        var fields=DailyOperationsPolicy.ReadFields(kind,vessel,json);
        return new(id,vessel,kind,revision,version,fields,"DRAFT",provenance,saved,hash,"PENDING",null,null,null,DailyOperationsPolicy.Blockers(kind,fields));
    }
    private static async Task<IResult> Read(string kind,Guid? id,bool history,bool capabilities,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";
        if (!Query(h,history || id==null && !capabilities,out var vessel,out var offset)) return await Fail(h,400,"invalid_query");
        var a=Actor(h); if (!CanRead(a,kind)) return await Fail(h,403,"permission_denied");
        var rights=Rights(a!);
        if (!await Vessels(db,a!,vessel).AnyAsync(h.RequestAborted)) return await Fail(h,404,"resource_not_found");
        object result;
        if (capabilities) {
            var destinations=Families.Where(f=>CanRead(a,f.Kind)).Select(f=>new DailyDestinationDto(f.Destination,$"/app/{f.Destination}/{vessel:D}")).ToArray();
            result=new DailyCapabilitiesDto(vessel,kind,true,CanWrite(a,kind,vessel),false,false,DailyOperationsPolicy.Schema(kind),DailyOperationsPolicy.SourceVersion,Offline(kind),destinations);
        } else if (history) {
            if (!await db.DailyOperations.AnyAsync(r=>r.VesselId==vessel && r.Kind==kind && r.Id==id,h.RequestAborted)) return await Fail(h,404,"resource_not_found");
            var rows=await db.DailyOperationRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind && r.RecordId==id)
                .OrderByDescending(r=>r.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
            result=new DailyHistoryDto(rows.Take(25).Select(Dto).ToArray(),offset,25,rows.Length>25);
        } else {
            var query=db.DailyOperations.AsNoTracking().Where(r=>r.VesselId==vessel && r.Kind==kind);
            if (id!=null) {
                var row=await query.SingleOrDefaultAsync(r=>r.Id==id,h.RequestAborted);
                if(row==null) return await Fail(h,404,"resource_not_found"); result=Dto(row);
            } else {
                var rows=await query.OrderByDescending(r=>r.SavedAt).ThenBy(r=>r.Id).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
                result=new DailyPageDto(rows.Take(25).Select(Dto).ToArray(),offset,25,rows.Length>25,CanWrite(a,kind,vessel),false,false,Offline(kind));
            }
        }
        if (!await Current(h,kind,vessel,rights)) return await Fail(h,403,"permission_denied");
        await Audit(h,"Success",id??vessel);
        if (!await Current(h,kind,vessel,rights)) return await Fail(h,403,"permission_denied");
        return Results.Ok(result);
    }
    private static async Task<IResult> Command(string kind,Guid? routeId,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store"; var ct=h.RequestAborted;
        var a=Actor(h);
        if (!CanRead(a,kind) || !a!.HasWriteAttribution || !a.Permissions.Contains(Permission(kind,"write"))) return await Fail(h,403,"permission_denied");
        if (!Query(h,false,out var vessel,out _)) return await Fail(h,400,"invalid_query");
        if (!CanWrite(a,kind,vessel)) return await Fail(h,403,"permission_denied");
        var rights=Rights(a!);
        if (!await Vessels(db,a!,vessel).AnyAsync(ct)) return await Fail(h,404,"resource_not_found");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h,403,"csrf_invalid"); }
        var keys=h.Request.Headers["Idempotency-Key"]; var key=keys.Count==1?keys[0]:null;
        if (key==null || key.Length is <16 or >80 || key.Any(c=>!char.IsAsciiLetterOrDigit(c) && c!='-')) return await Fail(h,400,"invalid_idempotency_key");
        DailyDraftCommand command; SortedDictionary<string,DailyValue> normalized;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var buffer=new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,65536,ct);
            using var document=JsonDocument.Parse(buffer.ToArray(),new() {MaxDepth=6}); ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
            command=document.RootElement.Deserialize<DailyDraftCommand>(Json)??throw new JsonException();
            if(command.Id==Guid.Empty || routeId!=null && routeId!=command.Id || routeId==null && command.ExpectedVersion!=0 ||
                command.ReasonCode is not ("INITIAL_AUTHORING" or "AUTHORING_CORRECTION")) throw new JsonException();
            if (!DailyOperationsPolicy.TryNormalize(kind,command.Fields,out normalized,out var error))
                return await Fail(h,400,error=="lookup_source_unavailable"?"linked_resource_unavailable":"invalid_request");
        } catch (Exception e) when (e is JsonException or InvalidDataException) { return await Fail(h,400,"invalid_request"); }
        var fieldsJson=JsonSerializer.Serialize(normalized);
        var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new {kind,vessel,routeId,command.Id,command.ExpectedVersion,command.ReasonCode,fieldsJson},Json));
        async Task<IResult?> Replay() {
            if(!await Current(h,kind,vessel,rights,true)) return await Fail(h,403,"permission_denied");
            var receipt=await db.DailyOperationReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==a!.Id && r.Key==key,ct);
            if(receipt==null) return null;
            if(receipt.Fingerprint!=fingerprint || receipt.Kind!=kind || receipt.VesselId!=vessel || receipt.RecordId!=command.Id)
                return await Fail(h,409,"idempotency_key_reused");
            await Audit(h,"Success",command.Id);
            if(!await Current(h,kind,vessel,rights,true)) return await Fail(h,503,"command_outcome_withheld");
            return Results.Content(receipt.OutcomeJson,"application/json");
        }
        var replay=await Replay(); if(replay!=null)return replay;
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx=null;
        string? committedOutcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var now=h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var applied=await DailyOperationsWriter.Apply(db,a!.Id,vessel,kind,command,routeId==null,now,ct);
            if(applied.Record==null) return await Fail(h,applied.Status,applied.Code!);
            var outcome=JsonSerializer.Serialize(applied.Record,Json);
            db.DailyOperationReceipts.Add(new(){TenantId=a.TenantId,VesselId=vessel,ActorId=a.Id,Key=key,RecordId=command.Id,Kind=kind,Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=now});
            await db.SaveChangesAsync(ct);
            if(!await Current(h,kind,vessel,rights,true)) return await Fail(h,403,"permission_denied");
            await tx.CommitAsync(ct);
            committedOutcome=outcome;
        } catch(DbUpdateConcurrencyException) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            return await Replay()??await Fail(h,409,"version_conflict");
        } catch(Exception e) when(PostgresFailure.IsDatabase(e)) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var conflict=PostgresFailure.IsSerialization(e)||PostgresFailure.Provider(e)?.SqlState=="23505";
            return await Replay()??await Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");
        } finally { if(tx!=null)await tx.DisposeAsync(); }
        // Business data+receipt have committed. A later audit/fence failure must
        // retain the client's exact command identity as an unknown outcome;
        // never tell it that a definitive pre-commit rejection happened.
        await Audit(h,"Success",command.Id);
        if(!await Current(h,kind,vessel,rights,true)) return await Fail(h,503,"command_outcome_withheld");
        return Results.Content(committedOutcome!,"application/json");
    }
}
