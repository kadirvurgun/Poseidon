using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
namespace Poseidon;
public sealed record EvidenceSettings(bool Enabled,string? Root);
public sealed record EvidenceIntake(Guid Id,long Version,string TargetType,Guid TargetId,JsonElement Metadata,string State,string ScanState,string IntegrityStatus,string? Sha256,long? ActualBytes,bool CanDownload=false,string EvidenceType="ATTACHMENT",string VerificationAssessment="NOT_PERFORMED",string OperationalAssessment="NOT_PERFORMED",Guid? PreviousId=null,Guid? SupersededBy=null,DateTimeOffset CreatedAt=default,DateTimeOffset ExpiresAt=default,string? DetectedMime=null,string TypeResult="NOT_CHECKED") {
 public string BlockReason=>State switch {"RESERVED"=>"UPLOAD_NOT_COMPLETED","RECEIVING"=>"UPLOAD_OUTCOME_UNCONFIRMED","REJECTED"=>"CONTENT_REJECTED","EXPIRED"=>"UPLOAD_SESSION_EXPIRED","ABORTED"=>"UPLOAD_ABORTED",_=>"QUARANTINED_SCANNER_UNAVAILABLE"};
}
public static class EvidenceContracts {
 public const long MaxBytes=5242880;
 public static ApiFault Invalid(string field)=>new(422,"invalid_evidence_metadata","Invalid evidence field: "+field);
 public static JsonElement Validate(JsonElement body) {
  if(body.ValueKind!=JsonValueKind.Object||Encoding.UTF8.GetByteCount(body.GetRawText())>16384)throw Invalid("body");
  var allowed=new HashSet<string>{"targetType","targetId","filename","declaredMime","expectedBytes","expectedSha256","title","source","description","capturedAt"};var seen=new HashSet<string>();
  foreach(var p in body.EnumerateObject())if(!allowed.Contains(p.Name)||!seen.Add(p.Name))throw Invalid(p.Name);
  string Text(string key,int max,bool required=true) {if(!body.TryGetProperty(key,out var v)){if(required)throw Invalid(key);return "";}if(v.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(v.GetString())||v.GetString()!.Length>max)throw Invalid(key);return v.GetString()!;}
  if(Text("targetType",20) is not ("Vessel" or "Certificate"))throw Invalid("targetType");
  if(!Guid.TryParseExact(Text("targetId",36),"D",out var id)||id==Guid.Empty)throw Invalid("targetId");
  var filename=Text("filename",128);if(filename!=filename.Normalize(NormalizationForm.FormC)||filename.Any(c=>char.IsControl(c)||c is '\u061c' or '\u200e' or '\u200f' or '/' or '\\' or ':' or '%' || c is >= '\u202a' and <= '\u202e' or >= '\u2066' and <= '\u2069')||filename.StartsWith('.')||filename.Count(c=>c=='.')!=1)throw Invalid("filename");
  var mime=Text("declaredMime",100);var extension=Path.GetExtension(filename).ToLowerInvariant();if(!((mime=="application/pdf"&&extension==".pdf")||(mime=="image/png"&&extension==".png")||(mime=="image/jpeg"&&extension is ".jpg" or ".jpeg")))throw Invalid("declaredMime");
  if(!body.TryGetProperty("expectedBytes",out var bytes)||bytes.ValueKind!=JsonValueKind.Number||!bytes.TryGetInt64(out var n)||n<1||n>MaxBytes)throw Invalid("expectedBytes");
  var hash=Text("expectedSha256",64,false);if(hash!=""&&!Regex.IsMatch(hash,"^[a-fA-F0-9]{64}$"))throw Invalid("expectedSha256");
  Text("title",200,false);Text("source",200,false);Text("description",2000,false);
  var date=Text("capturedAt",40,false);if(date!=""&&(!Regex.IsMatch(date,@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$")||!DateTimeOffset.TryParse(date,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _)))throw Invalid("capturedAt");
  return body.Clone();
 }
 public static bool Signature(byte[] bytes,string mime) {
  if(mime=="application/pdf") {var s=Encoding.Latin1.GetString(bytes);return s.StartsWith("%PDF-",StringComparison.Ordinal)&&s.TrimEnd().EndsWith("%%EOF",StringComparison.Ordinal)&&!s.Contains("/Encrypt",StringComparison.Ordinal);}
  if(mime=="image/png")return bytes.Length>=20&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&bytes.AsSpan(bytes.Length-12,12).SequenceEqual(new byte[]{0,0,0,0,73,69,78,68,174,66,96,130});
  return mime=="image/jpeg"&&bytes.Length>=4&&bytes[0]==255&&bytes[1]==216&&bytes[^2]==255&&bytes[^1]==217;
 }
}
