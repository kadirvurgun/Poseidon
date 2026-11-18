using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CertificateSupersessionDto(Guid CertificateId,long CertificateRevision,Guid RenewalId,DateTimeOffset CompletedAt);
public sealed record CertificateDto(Guid Id,Guid VesselId,string Scope,Guid? AssignmentId,Guid? PersonId,long Revision,uint Version,
    VesselCertificateContent Content,string SourceVersion,string Provenance,DateTimeOffset? SavedAt,string? Hash,
    CertificateCalendarDto Calendar,string Qualification,string State,string[] Blockers,bool CanEdit,string Href,
    bool CurrentRevision,CertificateSupersessionDto? SupersededBy=null,CertificateVerificationDto? Verification=null);
public sealed record CertificateCountsDto(int Valid,int Due90,int Due60,int Due30,int Expired,int SurveyWindow,int ReviewRequired);
public sealed record CertificateCenterDto(CertificateDto[] Items,int Offset,int Limit,bool HasMore,string Scope,string Coverage,
    string CrewCoverage,CertificateCountsDto Counts,int? CriticalGapCount,string State,DateOnly ReferenceDate);
public sealed record CertificateCapabilitiesDto(Guid VesselId,bool CanReadVessel,bool CanWriteVessel,bool CanReadCrew,string? CrewPurpose,
    string SourceVersion,string[] Scopes,string TemporalSemantics,string[] AvailabilityOptions,string[] VesselMetadataMembers,bool CanVerify=false,bool CanCompleteRenewal=false);

// Reusable owning projection for the center/passport. It never creates a parallel
// crew credential table, validity assertion or inferred verification.
internal static class CertificateReader
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=8};
    internal static bool Read(TrustedActor? a)=>a?.CanRead==true && a.Permissions.Contains("certificate.read");
    internal static bool Write(TrustedActor? a,Guid vessel)=>Read(a) && a!.CanEditVessel(vessel) && a.Permissions.Contains("certificate.write");
    internal static string Rights(TrustedActor a)=>RegisterStorage.Hash(JsonSerializer.Serialize(new{a.Id,a.TenantId,a.RoleContext,a.TenantWideRead,a.TenantWideManage,
        permissions=a.Permissions.Order(StringComparer.Ordinal),vessels=a.VesselGrants.OrderBy(g=>g.VesselId).ThenBy(g=>g.CanEdit)},Json));
    internal static IQueryable<VesselCertificateRevision> Visible(PoseidonDbContext db,TrustedActor a,Guid vessel,bool currentOnly=true) {
        if(!Read(a))return db.VesselCertificateRevisions.Where(_=>false);
        var authorized=a.Scope(db.Vessels).Where(v=>v.Id==vessel);
        var files=a.Permissions.Contains("file.read");
        var visible=db.VesselCertificateRevisions.AsNoTracking().Where(r=>r.VesselId==vessel && authorized.Any() && (r.FileId==null || files &&
            db.StoredFiles.Any(f=>f.Id==r.FileId && f.VesselId==vessel && f.Revision==r.FileRevision && f.Checksum==r.Checksum &&
                db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"))));
        var heads=db.VesselCertificates.AsNoTracking().Where(c=>c.VesselId==vessel);
        if(currentOnly)return visible.Where(r=>heads.Any(c=>c.Id==r.CertificateId && c.Revision==r.Revision));
        // A newly protected current head must not leave a less restricted old
        // snapshot reachable from history during the same response composition.
        return visible.Where(r=>heads.Any(c=>c.Id==r.CertificateId && visible.Any(h=>h.CertificateId==c.Id && h.Revision==c.Revision)));
    }
    internal static bool CrewRead(TrustedActor a,Guid vessel,CrewFilePolicy p,string? purpose)=>Read(a) && CredentialEndpoints.Permitted(a,vessel,false) &&
        a.Permissions.Contains("file.read") && p.Allows(a,false) && p.Kinds.Contains("CertificateDocument") && purpose==p.Purpose;
    internal static IQueryable<Credential> CrewVisible(PoseidonDbContext db,TrustedActor a,Guid vessel,CrewFilePolicy p,string? purpose) {
        if(!CrewRead(a,vessel,p,purpose))return db.Credentials.Where(_=>false);
        var authorized=a.Scope(db.Vessels).Where(v=>v.Id==vessel);
        return db.Credentials.AsNoTracking().Where(c=>c.VesselId==vessel && authorized.Any() && db.CrewAssignments.Any(owner=>owner.Id==c.AssignmentId && owner.VesselId==vessel && owner.PersonId==c.PersonId) &&
            (c.DocumentReference==null || db.StoredFiles.Any(f=>f.Id==c.DocumentReference && f.VesselId==vessel && db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel &&
                s.PersonId==c.PersonId && s.PersonalKind=="CertificateDocument" && s.PersonalPolicyVersion==p.Version && s.PersonalPurpose==p.Purpose && s.PersonalSourceVersion==p.SourceVersion && s.State=="CleanTransport"))));
    }
    internal static VesselCertificateContent Content(VesselCertificateRevision row)=>JsonSerializer.Deserialize<VesselCertificateContent>(row.ContentJson,Json)??throw new InvalidDataException("certificate_content_unavailable");
    internal static CertificateDto Dto(VesselCertificateRevision row,TrustedActor a,DateOnly reference,bool current=true) {
        var content=Content(row);
        return new(row.CertificateId,row.VesselId,"Vessel",null,null,row.Revision,checked((uint)row.RecordVersion),content,row.SourceVersion,row.Provenance,row.SavedAt,row.Hash,
            VesselCertificatePolicy.Calendar(content.Metadata.Declaration,reference,content.SurveyWindowStart,content.SurveyWindowEnd),"REVIEW_REQUIRED","DRAFT",VesselCertificatePolicy.Blockers(content),
            current && Write(a,row.VesselId),$"/app/certificate-command-center/{row.VesselId:D}?id={row.CertificateId:D}",current);
    }
    internal static CertificateDto CrewDto(Credential row,DateOnly reference)=>new(row.Id,row.VesselId,"Crew",row.AssignmentId,row.PersonId,row.Revision,row.Version,
        new(row.Metadata()),VesselCertificatePolicy.SourceVersion,row.Provenance,null,null,VesselCertificatePolicy.Calendar(row.Metadata().Declaration,reference),
        "REVIEW_REQUIRED","DRAFT",["SOURCE_QUALIFICATION_REQUIRED","HUMAN_VERIFICATION_REQUIRED"],false,$"/app/crew-roster/{row.VesselId:D}/crew-profile/{row.AssignmentId:D}",true);
    internal static CertificateCountsDto Counts(CertificateDto[] rows)=>new(rows.Count(r=>r.Calendar.State=="VALID"),
        rows.Count(r=>r.Calendar.DaysToDeclaredExpiry is >=0 and <90),rows.Count(r=>r.Calendar.DaysToDeclaredExpiry is >=0 and <60),
        rows.Count(r=>r.Calendar.DaysToDeclaredExpiry is >=0 and <30),rows.Count(r=>r.Calendar.State=="EXPIRED"),
        rows.Count(r=>r.Calendar.State=="SURVEY_WINDOW"),rows.Count(r=>r.Calendar.State=="REVIEW_REQUIRED"));
    internal static IQueryable<VesselCertificateRevision> Temporal(IQueryable<VesselCertificateRevision> query,string? state,DateOnly date) {
        if(state==null)return query;
        Expression<Func<VesselCertificateRevision,bool>> unknown=r=>r.IssueDate==null || r.ExpiryDate==null || r.IssueDate>date || r.ExpiryDate<r.IssueDate ||
            r.SurveyWindowDeclared && (r.SurveyWindowStart==null || r.SurveyWindowEnd==null || r.SurveyWindowEnd<r.SurveyWindowStart);
        if(state=="REVIEW_REQUIRED")return query.Where(unknown);
        query=query.Where(r=>r.IssueDate!=null && r.ExpiryDate!=null && r.IssueDate<=date && r.ExpiryDate>=r.IssueDate &&
            (!r.SurveyWindowDeclared || r.SurveyWindowStart!=null && r.SurveyWindowEnd!=null && r.SurveyWindowEnd>=r.SurveyWindowStart));
        if(state=="EXPIRED")return query.Where(r=>r.ExpiryDate<date);
        query=query.Where(r=>r.ExpiryDate>=date);
        if(state=="SURVEY_WINDOW")return query.Where(r=>r.SurveyWindowDeclared && r.SurveyWindowStart<=date && r.SurveyWindowEnd>=date);
        query=query.Where(r=>!r.SurveyWindowDeclared || r.SurveyWindowStart>date || r.SurveyWindowEnd<date);
        return state=="DUE"?query.Where(r=>r.ExpiryDate<date.AddDays(90)):query.Where(r=>r.ExpiryDate>=date.AddDays(90));
    }
    internal static IQueryable<Credential> CrewTemporal(IQueryable<Credential> query,string? state,DateOnly date) {
        if(state==null)return query;
        if(state=="REVIEW_REQUIRED")return query.Where(r=>r.IssueDate==null || r.ExpiryDate==null || r.IssueDate>date || r.ExpiryDate<r.IssueDate);
        query=query.Where(r=>r.IssueDate!=null && r.ExpiryDate!=null && r.IssueDate<=date && r.ExpiryDate>=r.IssueDate);
        return state switch {"EXPIRED"=>query.Where(r=>r.ExpiryDate<date),"SURVEY_WINDOW"=>query.Where(_=>false),
            "DUE"=>query.Where(r=>r.ExpiryDate>=date && r.ExpiryDate<date.AddDays(90)),_=>query.Where(r=>r.ExpiryDate>=date.AddDays(90))};
    }
    internal static Task<bool> FileAllowed(PoseidonDbContext db,TrustedActor a,Guid vessel,CertificateFilePin? pin,CancellationToken ct)=>pin==null?Task.FromResult(true):
        a.Permissions.Contains("file.read")?db.StoredFiles.AsNoTracking().AnyAsync(f=>f.Id==pin.Id && f.VesselId==vessel && f.Revision==pin.Revision && f.Checksum==pin.Checksum &&
            db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"),ct):Task.FromResult(false);
    internal static IQueryable<VesselCertificateRevision> ReturnedVisible(PoseidonDbContext db,TrustedActor actor,Guid vessel,CertificateDto[] records) {
        var heads=db.VesselCertificates.Where(c=>c.VesselId==vessel);
        Expression<Func<VesselCertificateRevision,bool>> pairs=r=>false;
        foreach(var row in records.Where(r=>r.Scope=="Vessel")) {
            var assertion=row.Verification;
            Expression<Func<VesselCertificateRevision,bool>> pin=r=>r.CertificateId==row.Id && r.Revision==row.Revision && r.RecordVersion==row.Version && r.Hash==row.Hash &&
                (!row.CurrentRevision || heads.Any(c=>c.Id==r.CertificateId && c.Revision==r.Revision));
            if(assertion!=null)pin=And(pin,r=>db.CertificateAssertionEvents.Any(e=>e.Id==assertion.Id && e.VesselId==vessel && e.CertificateId==r.CertificateId &&
                    e.CertificateRevision==r.Revision && e.CertificateVersion==r.RecordVersion && e.Revision==assertion.Revision && e.RecordVersion==assertion.Version &&
                    e.FileId==assertion.FileId && e.FileRevision==assertion.FileRevision && e.Checksum==assertion.Checksum && e.State==assertion.State &&
                    e.AuthorityHash==assertion.AuthorityHash && e.AuthorityBindingHash==assertion.AuthorityBindingHash && e.ActorId==assertion.ActorId && e.RoleId==assertion.RoleId && e.AuthorityId==assertion.AuthorityId &&
                    (!assertion.Current || heads.Any(head=>head.Id==r.CertificateId && head.Revision==r.Revision) &&
                        db.CertificateAssertionHeads.Any(head=>head.CertificateId==r.CertificateId && head.VesselId==vessel && head.Revision==e.Revision && head.Version==assertion.Version))));
            pairs=Or(pairs,pin);
        }
        return Visible(db,actor,vessel,false).Where(pairs);
    }
    internal static async Task<bool> Current(HttpContext h,Guid vessel,string rights,CertificateDto[] records,bool write=false,
        string? purpose=null,CertificateFilePin[]? pendingFiles=null) {
        await using var scope=h.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();var resolver=scope.ServiceProvider.GetRequiredService<ActorResolver>();var a=resolver.Resolve(h.User);
        if(a==null || !Read(a) || Rights(a)!=rights || write && !Write(a,vessel))return false;
        PoseidonDbContext db;try{db=scope.ServiceProvider.GetRequiredService<PoseidonDbContext>();}catch(UnauthorizedAccessException){return false;}
        if(db.TenantId!=a.TenantId)return false;
        var crewPolicy=CrewFilePolicy.Current(h);var crewPolicyHash=RegisterStorage.Hash(JsonSerializer.Serialize(crewPolicy,Json));
        var visible=ReturnedVisible(db,a,vessel,records);var visibleCrew=CrewVisible(db,a,vessel,crewPolicy,purpose);
        var crewIds=records.Where(r=>r.Scope=="Crew").Select(r=>r.Id).Distinct().ToArray();
        // Exact immutable versions, not an id-only intersection. Each bounded
        // query is tenant/file/purpose scoped and the final statement observes
        // all returned references together with actual vessel visibility.
        Expression<Func<Credential,bool>> crewPairs=r=>false;
        foreach(var row in records.Where(r=>r.Scope=="Crew"))crewPairs=Or(crewPairs,r=>r.Id==row.Id && r.AssignmentId==row.AssignmentId && r.PersonId==row.PersonId && r.Revision==row.Revision && r.Version==row.Version);
        var returnedCrew=visibleCrew.Where(crewPairs);var expectedVessels=records.Count(r=>r.Scope=="Vessel");var expectedCrew=records.Count(r=>r.Scope=="Crew");
        var files=pendingFiles??[];var fileIds=files.Select(p=>p.Id).Distinct().ToArray();
        Expression<Func<StoredFile,bool>> filePairs=f=>false;
        foreach(var pin in files)filePairs=Or(filePairs,f=>f.Id==pin.Id && f.Revision==pin.Revision && f.Checksum==pin.Checksum);
        var allowedFiles=db.StoredFiles.Where(filePairs).Where(f=>f.VesselId==vessel && db.FileStages.Any(s=>s.Id==f.Id && s.VesselId==vessel && s.PersonId==null && s.DocumentId==null && s.State=="CleanTransport"));
        var ok=await a.Scope(db.Vessels.AsNoTracking()).Where(v=>v.Id==vessel).Select(v=>
            visible.Count()==expectedVessels && returnedCrew.Count()==expectedCrew &&
            (fileIds.Length==0 || a.Permissions.Contains("file.read") && allowedFiles.Select(f=>f.Id).Distinct().Count()==fileIds.Length)).SingleOrDefaultAsync(h.RequestAborted);
        var live=resolver.Resolve(h.User);
        return ok && live!=null && Read(live) && Rights(live)==rights && (!write || Write(live,vessel)) && CertificateAssertions.CurrentPolicies(h,records) &&
            (crewIds.Length==0 || crewPolicyHash==RegisterStorage.Hash(JsonSerializer.Serialize(CrewFilePolicy.Current(h),Json)) && CrewRead(live,vessel,CrewFilePolicy.Current(h),purpose));
    }
    private sealed class ReplaceParameter(ParameterExpression from,ParameterExpression to):ExpressionVisitor {
        protected override Expression VisitParameter(ParameterExpression node)=>node==from?to:base.VisitParameter(node);
    }
    private static Expression<Func<T,bool>> Or<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right) {
        var body=new ReplaceParameter(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!;
        return Expression.Lambda<Func<T,bool>>(Expression.OrElse(left.Body,body),left.Parameters);
    }
    private static Expression<Func<T,bool>> And<T>(Expression<Func<T,bool>> left,Expression<Func<T,bool>> right) {
        var body=new ReplaceParameter(right.Parameters[0],left.Parameters[0]).Visit(right.Body)!;
        return Expression.Lambda<Func<T,bool>>(Expression.AndAlso(left.Body,body),left.Parameters);
    }
}
