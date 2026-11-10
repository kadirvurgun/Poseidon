using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateSaveCommand([property:JsonRequired] Guid Id,[property:JsonRequired] uint ExpectedVersion,
    [property:JsonRequired] VesselCertificateContent Content,[property:JsonRequired] string ReasonCode);
internal sealed record CertificateApplyResult(CertificateDto? Record,int Status=200,string? Code=null,CertificateDto? Previous=null);

internal static class CertificateWriter
{
    // OCR confirmation and ordinary HTTP commands use this same attributed EF
    // apply in THEIR existing Serializable transaction. No commit, receipt,
    // actor creation, administrative context or inferred verification occurs.
    internal static async Task<CertificateApplyResult> Apply(PoseidonDbContext db,TrustedActor actor,Guid vessel,CertificateSaveCommand command,
        bool create,DateTimeOffset now,CancellationToken ct) {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("Certificate apply requires caller transaction ownership.");
        if(db.IsAdministrativeImport || db.TenantId!=actor.TenantId || !CertificateReader.Write(actor,vessel))return new(null,403,"permission_denied");
        if(command.Id==Guid.Empty || command.ReasonCode is not("INITIAL_AUTHORING" or "AUTHORING_CORRECTION") || !VesselCertificatePolicy.Valid(command.Content))return new(null,400,"validation_failed");
        if(!await actor.Scope(db.Vessels).AnyAsync(v=>v.Id==vessel,ct))return new(null,404,"resource_not_found");
        if(!await CertificateReader.FileAllowed(db,actor,vessel,command.Content.Document,ct))return new(null,403,"linked_resource_unavailable");
        var head=await db.VesselCertificates.SingleOrDefaultAsync(c=>c.Id==command.Id,ct);CertificateDto? previous=null;
        if(head!=null && head.VesselId!=vessel)return new(null,404,"resource_not_found");
        if(create && (head!=null || command.ExpectedVersion!=0))return new(null,409,"version_conflict");
        if(!create && head==null)return new(null,404,"resource_not_found");
        if(head!=null) {
            if(head.Version!=command.ExpectedVersion)return new(null,409,"version_conflict");
            var before=await CertificateReader.Visible(db,actor,vessel).SingleOrDefaultAsync(r=>r.CertificateId==head.Id,ct);
            if(before==null)return new(null,404,"resource_not_found");
            if(before.SourceVersion!=VesselCertificatePolicy.SourceVersion)return new(null,409,"source_contract_changed");
            previous=CertificateReader.Dto(before,actor,DateOnly.FromDateTime(now.UtcDateTime));head.Revision++;
        }else{head=new(){TenantId=actor.TenantId,VesselId=vessel,Id=command.Id};db.VesselCertificates.Add(head);}
        await db.SaveChangesAsync(ct);
        var json=JsonSerializer.Serialize(command.Content,CertificateReader.Json);
        var end=DateOnly.TryParseExact(command.Content.SurveyWindowEnd,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed:(DateOnly?)null;
        var row=new VesselCertificateRevision{TenantId=actor.TenantId,VesselId=vessel,CertificateId=head.Id,Revision=head.Revision,RecordVersion=head.Version,
            ContentJson=json,Hash=RegisterStorage.Hash(JsonSerializer.Serialize(new{vessel,head.Id,head.Revision,source=VesselCertificatePolicy.SourceVersion,json},CertificateReader.Json)),
            SourceVersion=VesselCertificatePolicy.SourceVersion,Provenance="UserDeclaredUnverified",FileId=command.Content.Document?.Id,FileRevision=command.Content.Document?.Revision,
            Checksum=command.Content.Document?.Checksum,ActorId=actor.Id,SavedAt=now,IssueDate=command.Content.Metadata.Declaration.IssueDate,ExpiryDate=command.Content.Metadata.Declaration.ExpiryDate,
            SurveyWindowStart=command.Content.SurveyWindowStart,SurveyWindowEnd=end,SurveyWindowDeclared=command.Content.SurveyWindowStart!=null || command.Content.SurveyWindowEnd!=null};
        db.VesselCertificateRevisions.Add(row);await db.SaveChangesAsync(ct);
        // A saved command outcome is an immutable snapshot. Only a subsequent
        // current GET may claim currentRevision/editability after receipt replay.
        return new(CertificateReader.Dto(row,actor,DateOnly.FromDateTime(now.UtcDateTime)) with{CurrentRevision=false,CanEdit=false},Previous:previous);
    }
}
