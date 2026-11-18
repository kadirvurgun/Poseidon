using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CertificateAssertionAuthority(Guid Id,Guid VesselId,Guid CertificateId,Guid RoleId,string Duty,string SourceVersion,
    string AssignmentReference,DateTimeOffset ValidFrom,DateTimeOffset ValidUntil);
public sealed record CertificateVerificationDto(Guid Id,Guid CertificateId,long CertificateRevision,uint CertificateVersion,Guid FileId,long FileRevision,string Checksum,
    long Revision,uint Version,string State,Guid ActorId,Guid RoleId,Guid AuthorityId,string AuthorityHash,string AuthorityBindingHash,string SourceVersion,DateTimeOffset SavedAt,
    bool Current,string[] Blockers,string Qualification="RECORDED_UNASSESSED");
public sealed record CertificateAssertionOption(Guid Id,Guid RoleId,string Duty,string[] States);
public sealed record CertificateAssertionCapabilities(Guid CertificateId,long CertificateRevision,uint CertificateVersion,uint AssertionVersion,
    bool CanRecord,CertificateAssertionOption[] Authorities,string[] Blockers,string SourceVersion);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateAssertionCommand([property:JsonRequired] uint ExpectedVersion,[property:JsonRequired] long CertificateRevision,
    [property:JsonRequired] uint ExpectedAssertionVersion,[property:JsonRequired] Guid AuthorityId,[property:JsonRequired] string State,[property:JsonRequired] string Rationale);

public static class CertificateAssertions
{
    private static readonly string[] States=["Verified","Not Verified","Suspicious","Suspended / Revoked"];
    private static readonly JsonSerializerOptions Input=new(CertificateReader.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    internal static CertificateAssertionAuthority[] Authorities(HttpContext h,TrustedActor actor,Guid vessel,Guid certificate) {
        if(!CertificateReader.Write(actor,vessel) || !actor.Permissions.Contains("certificate.verify.record"))return [];
        var matches=h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m=>m.TenantId==actor.TenantId && m.ActorId==actor.Id).Take(2).ToArray();
        if(matches.Length!=1 || matches[0].ActorKind!="Human")return [];
        var grants=matches[0].CertificateAssertionAuthorities??[];if(grants.Length>256)return [];
        var now=CertificateEndpoints.Now(h);
        return grants.Where(g=>g!=null && g.Id!=Guid.Empty && grants.Count(other=>other!=null && other.Id==g.Id)==1 && g.VesselId==vessel && g.CertificateId==certificate &&
            g.RoleId!=Guid.Empty && actor.RoleContext?.Split(',').Contains(g.RoleId.ToString("D"))==true && g.Duty=="Auditor" && g.SourceVersion==VesselCertificatePolicy.SourceVersion &&
            g.AssignmentReference is{Length:>0 and <=200} && !string.IsNullOrWhiteSpace(g.AssignmentReference) && !g.AssignmentReference.Any(char.IsControl) &&
            g.ValidUntil>g.ValidFrom && g.ValidFrom<=now && now<g.ValidUntil).ToArray();
    }
    private static string Snapshot(CertificateAssertionAuthority g,TrustedActor actor)=>JsonSerializer.Serialize(new{grant=g,actor.Id,actor.TenantId,actorKind="Human",semantics="RecordedUnassessedAssertion"},CertificateReader.Json);
    private static string Hash(CertificateAssertionAuthority g,TrustedActor actor)=>RegisterStorage.Hash(Snapshot(g,actor));
    private static string BindingHash(CertificateAssertionAuthority g,Guid actor,Guid tenant)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{
        actor,tenant,g.Id,g.VesselId,g.CertificateId,g.RoleId,g.Duty,g.SourceVersion,g.AssignmentReference},CertificateReader.Json));
    internal static bool AssertionAuthorityCurrent(HttpContext h,CertificateVerificationDto verification,Guid vessel) {
        var members=h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m=>m.ActorId==verification.ActorId && m.TenantId==CertificateEndpoints.Actor(h)?.TenantId).Take(2).ToArray();
        if(members.Length!=1 || members[0].ActorKind!="Human")return false;
        var member=members[0];var roles=ActorResolver.TrustedRoleContext(member.RoleReferences);var now=CertificateEndpoints.Now(h);
        var configured=member.CertificateAssertionAuthorities??[];if(configured.Length>256)return false;
        var grants=configured.Where(g=>g!=null && g.Id==verification.AuthorityId).Take(2).ToArray();
        // Current policy/appointment is independent from immutable recorded
        // provenance. Only the validity interval may change without changing the
        // appointment binding; replacing its source/assignment is not a renewal.
        var required=new[]{"directory.read","directory.write","certificate.read","certificate.write","certificate.verify.record"};
        return grants.Length==1 && required.All(p=>(member.Permissions??[]).Contains(p)) && roles?.Split(',').Contains(verification.RoleId.ToString("D"))==true &&
            (member.TenantWideManage || member.VesselGrants?.Any(v=>v.VesselId==vessel && v.CanEdit)==true) && grants[0] is{} g &&
            g.VesselId==vessel && g.CertificateId==verification.CertificateId && g.RoleId==verification.RoleId && g.Duty=="Auditor" && g.SourceVersion==verification.SourceVersion &&
            g.AssignmentReference is{Length:>0 and <=200} && !string.IsNullOrWhiteSpace(g.AssignmentReference) && !g.AssignmentReference.Any(char.IsControl) &&
            BindingHash(g,member.ActorId,member.TenantId)==verification.AuthorityBindingHash && g.ValidUntil>g.ValidFrom && g.ValidFrom<=now && now<g.ValidUntil;
    }
    internal static bool CurrentPolicies(HttpContext h,CertificateDto[] records)=>records.All(r=>r.Verification is not{Current:true} verification || AssertionAuthorityCurrent(h,verification,r.VesselId));
    internal static async Task<CertificateDto[]> Decorate(HttpContext h,PoseidonDbContext db,TrustedActor actor,CertificateDto[] records,CancellationToken ct) {
        var ids=records.Where(r=>r.Scope=="Vessel").Select(r=>r.Id).Distinct().ToArray();if(ids.Length==0)return records;
        var revisions=records.Where(r=>r.Scope=="Vessel").Select(r=>r.Revision).Distinct().ToArray();
        var assertions=await db.CertificateAssertionEvents.AsNoTracking().Where(e=>ids.Contains(e.CertificateId) && revisions.Contains(e.CertificateRevision)).GroupBy(e=>new{e.CertificateId,e.CertificateRevision})
            .Select(g=>g.OrderByDescending(e=>e.Revision).First()).ToArrayAsync(ct);
        var heads=await db.CertificateAssertionHeads.AsNoTracking().Where(e=>ids.Contains(e.CertificateId)).ToArrayAsync(ct);
        return records.Select(row=> {
            var e=assertions.SingleOrDefault(e=>e.CertificateId==row.Id && e.CertificateRevision==row.Revision && e.CertificateVersion==row.Version);
            if(e==null || row.Scope!="Vessel" || row.Content.Document is not{} file || e.FileId!=file.Id || e.FileRevision!=file.Revision || e.Checksum!=file.Checksum)return row;
            var dto=Dto(e,false);var current=row.CurrentRevision && heads.Any(head=>head.CertificateId==row.Id && head.Revision==e.Revision) && AssertionAuthorityCurrent(h,dto,row.VesselId);
            return row with{Verification=dto with{Current=current,Blockers=current?["LEGAL_TRANSITION_SOURCE_UNAVAILABLE"]:["RECORDED_ASSERTION_NOT_CURRENT","LEGAL_TRANSITION_SOURCE_UNAVAILABLE"]}};
        }).ToArray();
    }
    private static CertificateVerificationDto Dto(CertificateAssertionEvent e,bool current)=>new(e.Id,e.CertificateId,e.CertificateRevision,checked((uint)e.CertificateVersion),e.FileId,e.FileRevision,e.Checksum,
        e.Revision,checked((uint)e.RecordVersion),e.State,e.ActorId,e.RoleId,e.AuthorityId,e.AuthorityHash,e.AuthorityBindingHash,e.SourceVersion,e.SavedAt,current,["LEGAL_TRANSITION_SOURCE_UNAVAILABLE"]);
    public static void MapCertificateAssertions(this IEndpointRouteBuilder app) {
        var group=app.MapGroup("/api/v1/vessels/{vessel:guid}/certificates/{id:guid}").RequireAuthorization("DirectoryRead");
        group.MapGet("/verification-capabilities",Capabilities).WithName("CertificateAssertionCapabilities").Produces<CertificateAssertionCapabilities>();
        group.MapGet("/verification-assertions",History).WithName("CertificateAssertions");
        group.MapPost("/verification-assertions",Save).WithName("CertificateAssertionRecord").Accepts<CertificateAssertionCommand>("application/json").Produces<CertificateVerificationDto>();
    }
    private static async Task<IResult> Capabilities(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(h.Request.Query.Count!=0)return await CertificateEndpoints.Fail(h,400,"invalid_query");
        var actor=CertificateEndpoints.Actor(h);if(actor==null || !CertificateReader.Read(actor))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        var source=await CertificateReader.Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.CertificateId==id,h.RequestAborted);if(source==null)return await CertificateEndpoints.Fail(h,404,"resource_not_found");
        var authorities=Authorities(h,actor,vessel,id);var grantHash=RegisterStorage.Hash(JsonSerializer.Serialize(authorities.OrderBy(g=>g.Id),CertificateReader.Json));
        var head=await db.CertificateAssertionHeads.AsNoTracking().SingleOrDefaultAsync(e=>e.CertificateId==id && e.VesselId==vessel,h.RequestAborted);
        var result=new CertificateAssertionCapabilities(id,source.Revision,checked((uint)source.RecordVersion),head?.Version??0,authorities.Length>0 && source.FileId!=null,
            authorities.Select(g=>new CertificateAssertionOption(g.Id,g.RoleId,g.Duty,States)).ToArray(),["RECORDED_ASSERTION_ONLY","LEGAL_TRANSITION_SOURCE_UNAVAILABLE"],VesselCertificatePolicy.SourceVersion);
        var rights=CertificateReader.Rights(actor);var record=CertificateReader.Dto(source,actor,DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime));
        async Task<bool> Current()=>await CertificateReader.Current(h,vessel,rights,[record]) && CertificateEndpoints.Actor(h) is{} live &&
            RegisterStorage.Hash(JsonSerializer.Serialize(Authorities(h,live,vessel,id).OrderBy(g=>g.Id),CertificateReader.Json))==grantHash;
        if(!await Current())return await CertificateEndpoints.Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:id);
        if(!await Current())return await CertificateEndpoints.Fail(h,403,"permission_denied");return Results.Ok(result);
    }
    private static async Task<IResult> History(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";if(!CertificateEndpoints.Offset(h,new HashSet<string>{"offset"},out var offset))return await CertificateEndpoints.Fail(h,400,"invalid_query");
        var actor=CertificateEndpoints.Actor(h);if(actor==null || !CertificateReader.Read(actor))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        var certificates=CertificateReader.Visible(db,actor,vessel,false);
        var events=await db.CertificateAssertionEvents.AsNoTracking().Where(e=>e.CertificateId==id && e.VesselId==vessel && certificates.Any(c=>c.CertificateId==id && c.Revision==e.CertificateRevision && c.RecordVersion==e.CertificateVersion))
            .OrderByDescending(e=>e.Revision).Skip(offset).Take(26).ToArrayAsync(h.RequestAborted);
        var revisions=events.Take(25).Select(e=>e.CertificateRevision).Distinct().ToArray();
        var certRows=await certificates.Where(c=>c.CertificateId==id && revisions.Contains(c.Revision)).ToArrayAsync(h.RequestAborted);
        var records=certRows.Select(c=>CertificateReader.Dto(c,actor,DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime),false)).ToArray();
        var rights=CertificateReader.Rights(actor);
        if(!await CertificateReader.Current(h,vessel,rights,records))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateRead","Success",resourceId:id);
        if(!await CertificateReader.Current(h,vessel,rights,records))return await CertificateEndpoints.Fail(h,403,"permission_denied");
        return Results.Ok(new{items=events.Take(25).Select(e=>Dto(e,false)),offset,limit=25,hasMore=events.Length>25});
    }
    private static async Task<IResult> Save(Guid vessel,Guid id,HttpContext h,PoseidonDbContext db) {
        h.Response.Headers.CacheControl="no-store";var actor=CertificateEndpoints.Actor(h);
        if(actor==null || !CertificateReader.Write(actor,vessel) || !actor.Permissions.Contains("certificate.verify.record"))return await CertificateEndpoints.Fail(h,403,"human_authority_required");
        if(h.Request.Query.Count!=0)return await CertificateEndpoints.Fail(h,400,"invalid_query");
        try{await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h);}catch(AntiforgeryValidationException){return await CertificateEndpoints.Fail(h,403,"csrf_invalid");}
        var keys=h.Request.Headers["Idempotency-Key"];var key=keys.Count==1?keys[0]:null;if(key==null || !Guid.TryParseExact(key,"D",out _))return await CertificateEndpoints.Fail(h,400,"invalid_idempotency_key");
        CertificateAssertionCommand command;
        try {
            if(!h.Request.HasJsonContentType())throw new JsonException();using var buffer=new MemoryStream();await SecureFilePolicy.CopyAsync(h.Request.Body,buffer,16384,h.RequestAborted);
            using var document=JsonDocument.Parse(buffer.ToArray(),new(){MaxDepth=4});ApplicabilityEngine.RejectDuplicateKeys(document.RootElement);command=document.RootElement.Deserialize<CertificateAssertionCommand>(Input)??throw new JsonException();
            if(command.CertificateRevision<=0 || command.ExpectedVersion==0 || !States.Contains(command.State) || command.Rationale is not{Length:>0 and <=2000} ||
                string.IsNullOrWhiteSpace(command.Rationale) || command.Rationale.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))throw new JsonException();
        }catch(Exception ex)when(ex is JsonException or InvalidDataException){return await CertificateEndpoints.Fail(h,400,"invalid_review_request");}
        var grant=Authorities(h,actor,vessel,id).SingleOrDefault(g=>g.Id==command.AuthorityId);if(grant==null)return await CertificateEndpoints.Fail(h,403,"human_authority_required");
        var rights=CertificateReader.Rights(actor);var grantHash=Hash(grant,actor);var fingerprint=RegisterStorage.Hash(JsonSerializer.Serialize(new{operation="CertificateAssertion",vessel,id,command},CertificateReader.Json));
        bool CurrentAuthority()=>CertificateEndpoints.Actor(h) is{} live && Authorities(h,live,vessel,id).Any(g=>g.Id==grant.Id && Hash(g,live)==grantHash);
        async Task<IResult> Outcome(CertificateVerificationDto saved,string json) {
            try {
                var version=await CertificateReader.Visible(db,actor,vessel,false).SingleOrDefaultAsync(c=>c.CertificateId==id && c.Revision==saved.CertificateRevision && c.RecordVersion==saved.CertificateVersion,h.RequestAborted);
                if(version==null)return await CertificateEndpoints.Fail(h,503,"command_outcome_withheld");
                var record=CertificateReader.Dto(version,actor,DateOnly.FromDateTime(CertificateEndpoints.Now(h).UtcDateTime),false) with{Verification=saved};
                await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Success",resourceId:id);
                if(!await CertificateReader.Current(h,vessel,rights,[record],true) || !CurrentAuthority())return await CertificateEndpoints.Fail(h,503,"command_outcome_withheld");
                return Results.Content(json,"application/json");
            }catch(Exception ex)when(ex is AuditUnavailableException || PostgresFailure.IsDatabase(ex)){return Results.Json(new{code="command_outcome_withheld"},statusCode:503);}
        }
        async Task<IResult?> Replay() {
            if(!await CertificateReader.Current(h,vessel,rights,[],true) || !CurrentAuthority())return await CertificateEndpoints.Fail(h,403,"human_authority_required");
            var receipt=await db.CertificateCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r=>r.ActorId==actor.Id && r.Key==key,h.RequestAborted);if(receipt==null)return null;
            if(receipt.Action!="CertificateAssertion" || receipt.ResourceId!=id || receipt.VesselId!=vessel || receipt.Fingerprint!=fingerprint)return await CertificateEndpoints.Fail(h,409,"idempotency_key_reused");
            return await Outcome(JsonSerializer.Deserialize<CertificateVerificationDto>(receipt.OutcomeJson,CertificateReader.Json)??throw new InvalidDataException("assertion_receipt_unavailable"),receipt.OutcomeJson);
        }
        var replay=await Replay();if(replay!=null)return replay;IDbContextTransaction? tx=null;CertificateVerificationDto? savedRecord=null;string? outcome=null;
        try {
            tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,h.RequestAborted);
            var source=await CertificateReader.Visible(db,actor,vessel).SingleOrDefaultAsync(c=>c.CertificateId==id && c.Revision==command.CertificateRevision && c.RecordVersion==command.ExpectedVersion,h.RequestAborted);
            if(source==null)return await CertificateEndpoints.Fail(h,409,"source_revision_changed");
            if(source.FileId==null)return await CertificateEndpoints.Fail(h,403,"clean_file_unavailable");
            var file=await db.StoredFiles.SingleOrDefaultAsync(f=>f.Id==source.FileId && f.VesselId==vessel && f.Revision==source.FileRevision && f.Checksum==source.Checksum,h.RequestAborted);
            if(file==null || db.QualifiedInputs==null || !await db.QualifiedInputs.FileIntact(file,h.RequestAborted))return await CertificateEndpoints.Fail(h,403,"file_integrity_unavailable");
            var head=await db.CertificateAssertionHeads.SingleOrDefaultAsync(c=>c.CertificateId==id && c.VesselId==vessel,h.RequestAborted);
            if((head?.Version??0)!=command.ExpectedAssertionVersion)return await CertificateEndpoints.Fail(h,409,"version_conflict");
            if(head==null){head=new(){TenantId=actor.TenantId,VesselId=vessel,CertificateId=id};db.CertificateAssertionHeads.Add(head);}else head.Revision++;
            await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h,"CertificateCommand","Attempted",resourceId:id);await db.SaveChangesAsync(h.RequestAborted);
            var entry=new CertificateAssertionEvent{TenantId=actor.TenantId,VesselId=vessel,CertificateId=id,Id=Guid.NewGuid(),Revision=head.Revision,RecordVersion=head.Version,
                CertificateRevision=source.Revision,CertificateVersion=source.RecordVersion,FileId=file.Id,FileRevision=file.Revision,Checksum=file.Checksum,State=command.State,
                ActorId=actor.Id,RoleId=grant.RoleId,AuthorityId=grant.Id,AuthorityHash=grantHash,AuthorityBindingHash=BindingHash(grant,actor.Id,actor.TenantId),AuthorityJson=Snapshot(grant,actor),SourceVersion=VesselCertificatePolicy.SourceVersion,Rationale=command.Rationale,SavedAt=CertificateEndpoints.Now(h)};
            db.CertificateAssertionEvents.Add(entry);savedRecord=Dto(entry,false);outcome=JsonSerializer.Serialize(savedRecord,CertificateReader.Json);
            db.CertificateCommandReceipts.Add(new(){TenantId=actor.TenantId,VesselId=vessel,ActorId=actor.Id,Key=key,CertificateId=id,ResourceId=id,Action="CertificateAssertion",Fingerprint=fingerprint,OutcomeJson=outcome,SavedAt=entry.SavedAt});
            await db.SaveChangesAsync(h.RequestAborted);var record=CertificateReader.Dto(source,actor,DateOnly.FromDateTime(entry.SavedAt.UtcDateTime));
            if(!await CertificateReader.Current(h,vessel,rights,[record],true) || !CurrentAuthority())return await CertificateEndpoints.Fail(h,403,"human_authority_required");
            await tx.CommitAsync(h.RequestAborted);
        }catch(DbUpdateConcurrencyException){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();return await Replay()??await CertificateEndpoints.Fail(h,409,"version_conflict");}
        catch(Exception ex)when(PostgresFailure.IsDatabase(ex)){if(tx!=null)await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();var conflict=PostgresFailure.IsSerialization(ex)||PostgresFailure.Provider(ex)?.SqlState=="23505";return await Replay()??await CertificateEndpoints.Fail(h,conflict?409:503,conflict?"version_conflict":"service_unavailable");}
        finally{if(tx!=null)await tx.DisposeAsync();}
        return await Outcome(savedRecord!,outcome!);
    }
}
