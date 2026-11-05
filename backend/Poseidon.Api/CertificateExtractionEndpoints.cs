using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateExtractCommand([property:JsonRequired] uint ExpectedVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateExtractConfirm([property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] string ReviewContext,
    [property:JsonRequired] CertificateConfirmedFact[] Facts,[property:JsonRequired] bool VesselContextConfirmed);
public sealed record CertificateExtractionDto(Guid CertificateId,Guid VesselId,string Scope,uint Version,string State,CertificateCandidate[] Candidates,
    string ModelVersion,string PromptVersion,string PolicyVersion,string SourceVersion,decimal LowConfidenceThreshold,string? ReviewContext,
    DateTimeOffset ExpiresAt,bool CanConfirm,string[] Blockers) { public override string ToString()=>nameof(CertificateExtractionDto); }
internal sealed record CertificateExtractionReview(CertificateExtractionBinding Binding,string PolicyHash,string ModelVersion,string PromptVersion,
    DateTimeOffset ExpiresAt,CertificateCandidate[] Candidates) { public override string ToString()=>nameof(CertificateExtractionReview); }

public static class CertificateExtractionEndpoints
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=10,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public static void MapCertificateExtraction(this IEndpointRouteBuilder app) {
        app.MapPost("/api/v1/vessels/{vessel:guid}/certificates/{id:guid}/extractions",(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db)=>Extract(vessel,id,null,h,db))
            .RequireAuthorization("DirectoryRead").WithName("CertificateExtract").Accepts<CertificateExtractCommand>("application/json").Produces<CertificateExtractionDto>();
        app.MapPost("/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/certificates/{id:guid}/extractions",(Guid vessel,Guid assignment,Guid id,HttpContext h,PoseidonDbContext db)=>Extract(vessel,id,assignment,h,db))
            .RequireAuthorization("DirectoryRead").WithName("CredentialExtract").Accepts<CertificateExtractCommand>("application/json").Produces<CertificateExtractionDto>();
        app.MapPost("/api/v1/vessels/{vessel:guid}/certificates/{id:guid}/extractions/confirm",Confirm).RequireAuthorization("DirectoryRead")
            .WithName("CertificateExtractionConfirm").Accepts<CertificateExtractConfirm>("application/json").Produces<CertificateDto>();
    }
    private static TrustedActor? Actor(HttpContext h)=>CertificateExtractionAccess.Actor(h);
    private static DateTimeOffset Now(HttpContext h)=>h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
    private static CertificateExtractionSettings Settings(HttpContext h)=>h.RequestServices.GetRequiredService<CertificateExtractionSettings>();
    private static bool Allowed(HttpContext h,CertificateExtractionSettings policy,Guid vessel,string scope)=>Actor(h) is {} a &&
        policy.Allows(h.RequestServices.GetRequiredService<IHostEnvironment>(),a.TenantId,a.Id,vessel,scope);
    private static bool PolicyCurrent(HttpContext h,CertificateExtractionSettings policy,Guid vessel,string scope)=>Allowed(h,Settings(h),vessel,scope) && Settings(h).Fingerprint()==policy.Fingerprint();
    private static IDataProtector Protector(HttpContext h)=>h.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("Poseidon.CertificateExtraction.Review.v1");
    private static Task Audit(HttpContext h,string result,Guid id)=>h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateExtraction",result,resourceId:id);
    private static async Task<IResult> Fail(HttpContext h,int status,string code,Guid id){await Audit(h,code,id);return Results.Json(new{code},statusCode:status);}
    private static async Task<bool> Csrf(HttpContext h) {
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);return true;}catch(AntiforgeryValidationException){return false;}
    }
    private static async Task<T> Body<T>(HttpContext h,int maximum) {
        if(!h.Request.HasJsonContentType())throw new JsonException();using var bounded=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,bounded,maximum,h.RequestAborted);
        using var document=JsonDocument.Parse(bounded.ToArray(),new(){MaxDepth=10});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);
        return document.RootElement.Deserialize<T>(Json)??throw new JsonException();
    }
    private static async Task<IResult> Extract(Guid vessel,Guid id,Guid? assignment,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var a=Actor(h);
        if(!CertificateExtractionAccess.Permission(a))return await Fail(h,403,"permission_denied",id);
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid",id);
        var scope=assignment==null?"Vessel":"Crew";var purpose=assignment==null?null:h.Request.Query["purpose"].ToString();
        if(h.Request.Query.Any(q=>q.Value.Count!=1 || q.Key!="purpose" || assignment==null))return await Fail(h,400,"invalid_query",id);
        CertificateExtractCommand command;try{command=await Body<CertificateExtractCommand>(h,2048);}catch(Exception e)when(e is JsonException or InvalidDataException){return await Fail(h,400,"invalid_request",id);}
        var policy=Settings(h);if(!Allowed(h,policy,vessel,scope))return await Fail(h,503,"source_contract_changed",id);policy=policy.Snapshot();
        var source=await CertificateExtractionAccess.Read(db,h,vessel,id,assignment,command.ExpectedVersion,purpose,ct);
        if(source==null)return await Fail(h,404,"resource_unavailable",id);
        var storage=h.RequestServices.GetRequiredService<SecureFileSettings>().Snapshot();
        if(!storage.Ready(h.RequestServices.GetRequiredService<IHostEnvironment>()) || source.File.Length<=0 || source.File.Length>Math.Min(policy.MaxBytes,storage.MaxBytes))return await Fail(h,503,"file_settings_unavailable",id);
        byte[] bytes;
        try {
            var objects=new FileObjects(storage,new LocalFileObjects(storage),h.RequestServices.GetRequiredService<IHostEnvironment>());
            await using var stream=await objects.OpenAsync(source.File,ct);using var bounded=new MemoryStream();
            var observed=await SecureFilePolicy.CopyAsync(stream,bounded,Math.Min(policy.MaxBytes,storage.MaxBytes),ct);
            if(observed.Length!=source.File.Length || observed.Checksum!=source.File.Checksum)return await Fail(h,503,"file_integrity_unavailable",id);
            bytes=bounded.ToArray();
        }catch(Exception e)when(e is IOException or InvalidDataException or HttpRequestException){return await Fail(h,503,"file_integrity_unavailable",id);}
        // Re-read outside the old request snapshot BEFORE sending private bytes
        // and again after the provider/audit awaits. No caller path/URL is sent.
        if(!PolicyCurrent(h,policy,vessel,scope) || !await CertificateExtractionAccess.Current(h,source.Binding,purpose)){CryptographicOperations.ZeroMemory(bytes);return await Fail(h,403,"permission_denied",id);}
        CertificateCandidate[] candidates;
        try {
            var raw=await h.RequestServices.GetRequiredService<CertificateExtractionProvider>().Extract(policy,bytes,source.File.Mime,source.File.Checksum,ct);
            candidates=CertificateExtractionPolicy.Project(raw,source.File.Checksum,policy.ModelVersion!,policy.PromptVersion!,policy.LowConfidenceThreshold!.Value,vessel.ToString("D"),source.Binding.PersonId?.ToString("D"));
        }catch(CertificateExtractionUnavailableException){return await Fail(h,503,"source_contract_changed",id);}
        finally{CryptographicOperations.ZeroMemory(bytes);}
        if(!PolicyCurrent(h,policy,vessel,scope) || !await CertificateExtractionAccess.Current(h,source.Binding,purpose))return await Fail(h,403,"permission_denied",id);
        await Audit(h,"Success",id);
        if(!PolicyCurrent(h,policy,vessel,scope) || !await CertificateExtractionAccess.Current(h,source.Binding,purpose))return await Fail(h,403,"permission_denied",id);
        var expires=Now(h).AddSeconds(policy.ReviewSeconds);var live=Actor(h)!;var canConfirm=assignment==null && CertificateReader.Write(live,vessel) && CertificateExtractionAccess.Human(h,live);
        var review=canConfirm?Protector(h).Protect(JsonSerializer.Serialize(new CertificateExtractionReview(source.Binding,policy.Fingerprint(),policy.ModelVersion!,policy.PromptVersion!,expires,candidates),Json)):null;
        if(review?.Length>65536)return await Fail(h,503,"request_limit",id);
        // Safe version metadata only. Neither model response, prompt, document
        // text, values, names, source IDs nor bearer credentials enter logs.
        h.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CertificateExtraction").LogInformation("Certificate candidate protocol completed using model version {ModelVersion} and prompt version {PromptVersion}.",policy.ModelVersion,policy.PromptVersion);
        return Results.Ok(new CertificateExtractionDto(id,vessel,scope,command.ExpectedVersion,"REVIEW_REQUIRED",candidates,policy.ModelVersion!,policy.PromptVersion!,policy.PolicyVersion!,policy.SourceVersion!,policy.LowConfidenceThreshold.Value,
            review,expires,canConfirm,assignment==null?["USER_FACT_CONFIRMATION_REQUIRED","SOURCE_QUALIFICATION_REQUIRED"]:["CREW_CONFIRMATION_WORKFLOW_UNAVAILABLE","SOURCE_QUALIFICATION_REQUIRED"]));
    }
    private static async Task<IResult> Confirm(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var ct=h.RequestAborted;var actor=Actor(h);
        if(!CertificateExtractionAccess.Permission(actor) || !CertificateReader.Write(actor,vessel) || !CertificateExtractionAccess.Human(h,actor!))return await Fail(h,403,"permission_denied",id);
        if(h.Request.Query.Count!=0)return await Fail(h,400,"invalid_query",id);
        if(!await Csrf(h))return await Fail(h,403,"csrf_invalid",id);
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;
        if(key==null || key.Length is <16 or >80 || key.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))return await Fail(h,400,"invalid_idempotency_key",id);
        CertificateExtractConfirm command;CertificateExtractionReview review;
        try {
            command=await Body<CertificateExtractConfirm>(h,98304);
            if(command.ReviewContext is not {Length:>0 and <=65536} || command.Facts==null || !command.VesselContextConfirmed)throw new JsonException();
            review=JsonSerializer.Deserialize<CertificateExtractionReview>(Protector(h).Unprotect(command.ReviewContext),Json)??throw new JsonException();
        }catch(Exception e)when(e is JsonException or InvalidDataException or CryptographicException){return await Fail(h,400,"invalid_request",id);}
        var policy=Settings(h);var binding=review.Binding;
        if(!Allowed(h,policy,vessel,"Vessel") || binding.Scope!="Vessel" || binding.VesselId!=vessel || binding.CertificateId!=id || binding.ActorId!=actor!.Id || binding.TenantId!=actor.TenantId ||
            command.ExpectedVersion!=binding.CertificateVersion || review.PolicyHash!=policy.Fingerprint() || review.ModelVersion!=policy.ModelVersion || review.PromptVersion!=policy.PromptVersion)return await Fail(h,403,"source_contract_changed",id);
        policy=policy.Snapshot();var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{operation="CertificateExtractionConfirm",vessel,id,command},Json));
        async Task<IResult?> Replay() {
            if(!PolicyCurrent(h,policy,vessel,"Vessel") || !await CertificateExtractionAccess.Current(h,binding,null,false,true))return await Fail(h,403,"permission_denied",id);
            var receipt=await db.CertificateCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,ct);
            if(receipt==null)return null;
            if(receipt.Action!="CertificateSave" || receipt.CertificateId!=id || receipt.VesselId!=vessel || receipt.Fingerprint!=fingerprint)return await Fail(h,409,"idempotency_key_reused",id);
            var saved=JsonSerializer.Deserialize<CertificateDto>(receipt.OutcomeJson,CertificateReader.Json);if(saved==null)return await Fail(h,409,"historical_outcome_unavailable",id);
            if(!await CertificateReader.Current(h,vessel,binding.Rights,[saved],true))return await Fail(h,403,"permission_denied",id);
            await Audit(h,"Success",id);
            if(!PolicyCurrent(h,policy,vessel,"Vessel") || !await CertificateExtractionAccess.Current(h,binding,null,false,true) || !await CertificateReader.Current(h,vessel,binding.Rights,[saved],true))return await Fail(h,503,"command_outcome_withheld",id);
            return Results.Content(receipt.OutcomeJson,"application/json");
        }
        var replay=await Replay();if(replay!=null)return replay;
        if(review.ExpiresAt<=Now(h) || !await CertificateExtractionAccess.Current(h,binding,null,true,true))return await Fail(h,409,"version_conflict",id);
        IDbContextTransaction? tx=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var source=await CertificateExtractionAccess.Read(db,h,vessel,id,null,command.ExpectedVersion,null,ct);
            if(source==null || source.Binding!=binding)return await Fail(h,409,"version_conflict",id);
            if(!CertificateExtractionPolicy.TryMerge(source.Record.Content.Metadata,command.Facts,out var metadata))return await Fail(h,400,"validation_failed",id);
            var content=source.Record.Content with{Metadata=metadata};
            var applied=await CertificateWriter.Apply(db,actor,vessel,new(id,command.ExpectedVersion,content,"AUTHORING_CORRECTION"),false,Now(h),ct);
            if(applied.Record==null)return await Fail(h,applied.Status,applied.Code??"validation_failed",id);
            var saved=applied.Record with{CanEdit=false,CurrentRevision=false};var outcome=JsonSerializer.Serialize(saved,CertificateReader.Json);
            db.CertificateCommandReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,ResourceId=id,CertificateId=id,Action="CertificateSave",Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=Now(h)});
            await db.SaveChangesAsync(ct);
            // Fresh readers must check the still-committed predecessor before
            // commit; the new immutable snapshot is not visible outside this tx.
            if(!PolicyCurrent(h,policy,vessel,"Vessel") || !await CertificateExtractionAccess.Current(h,binding,null,true,true))return await Fail(h,403,"permission_denied",id);
            await tx.CommitAsync(ct);await Audit(h,"Success",id);
            if(!PolicyCurrent(h,policy,vessel,"Vessel") || !await CertificateExtractionAccess.Current(h,binding,null,false,true) || !await CertificateReader.Current(h,vessel,binding.Rights,[saved],true))return await Fail(h,503,"command_outcome_withheld",id);
            return Results.Content(outcome,"application/json");
        }catch(Exception e)when(e is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(e)) {
            if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();
            var recovered=await Replay();if(recovered!=null)return recovered;
            return await Fail(h,e is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(e) || PostgresFailure.Provider(e)?.SqlState=="23505"?409:503,"version_conflict",id);
        }finally{if(tx!=null)await tx.DisposeAsync();}
    }
}
