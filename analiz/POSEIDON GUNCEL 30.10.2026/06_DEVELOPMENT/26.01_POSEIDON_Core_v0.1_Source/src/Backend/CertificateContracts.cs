using System.Globalization;
using System.Text.Json;
namespace Poseidon;

public sealed record CertificateParent(Guid Id,string? Name,string? ImoNo,string? Flag,string AdminState);
public sealed record CertificateDateTracking(DateOnly AsOfDate,string TimeBasis,int? DaysToRecordedExpiry,string Bucket,bool IssueDateInFuture);
public sealed record CertificateRecord(Guid Id,long Version,string AdminState,JsonElement Data,InventoryCompleteness Completeness,CertificateDateTracking DateTracking,string VerificationAssessment,string OperationalAssessment,CertificateParent ParentVessel);
public sealed record CertificatePage(IReadOnlyList<CertificateRecord> Items,string? NextCursor,bool HasMore,DateOnly AsOfDate,string TimeBasis);
public static class CertificateContracts {
 public const string TimeBasis="UTC_CALENDAR_DATE";
 public static readonly string[] Fields=["vessel_id","type","certificate_no","issuer","issue","expiry","notes"];
 public static readonly string[] Required=["vessel_id","type","issuer"];
 public static readonly string[] Buckets=["NO_EXPIRY_DATE","PAST_DATE","TODAY","WITHIN_30_DAYS","DAYS_31_TO_60","DAYS_61_TO_90","AFTER_90_DAYS"];
 public static DateOnly Today(TimeProvider clock)=>DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
 public static JsonElement Validate(JsonElement body) {
  Object(body,["data","reason"]);
  if(!body.TryGetProperty("data",out var data))throw Invalid("data");
  // Reuse the exact existing typed/date/immutable-reference draft syntax; only the envelope changes.
  return Contracts.Validate(JsonSerializer.SerializeToElement(new{saveMode="DRAFT",data}),"certificates");
 }
 public static void Object(JsonElement body,string[] fields) {
  if(body.ValueKind!=JsonValueKind.Object)throw Invalid("body");
  var entries=body.EnumerateObject().ToArray();
  if(entries.Select(p=>p.Name).Distinct().Count()!=entries.Length || entries.Any(p=>!fields.Contains(p.Name)))throw Invalid("unknown_or_duplicate_field");
 }
 public static string? Reason(JsonElement body,bool required) {
  if(!body.TryGetProperty("reason",out var reason)) {if(required)throw Invalid("reason");return null;}
  if(reason.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString()) || reason.GetString()!.Trim().Length>500)throw Invalid("reason");
  return reason.GetString()!.Trim();
 }
 public static InventoryCompleteness Completeness(JsonElement data) {
  var missing=Required.Where(k=>!data.TryGetProperty(k,out var value)||value.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(value.GetString())).ToArray();
  List<string> invalid=[];
  foreach(var p in data.EnumerateObject()) {
   if(!Fields.Contains(p.Name)){invalid.Add("unknown_field");continue;}
   if(p.Value.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(p.Value.GetString()) || p.Value.GetString()!.Length>(p.Name=="notes"?4000:200)){invalid.Add(p.Name);continue;}
   if(p.Name=="vessel_id" && (!Guid.TryParseExact(p.Value.GetString(),"D",out var id)||id==Guid.Empty))invalid.Add(p.Name);
   if(p.Name is "issue" or "expiry" && !DateOnly.TryParseExact(p.Value.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))invalid.Add(p.Name);
  }
  if(Date(data,"issue") is {} issue && Date(data,"expiry") is {} expiry && expiry<issue)invalid.Add("expiry");
  return new(missing,invalid.Distinct().ToArray());
 }
 public static DateOnly? Date(JsonElement data,string key)=>data.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String&&DateOnly.TryParseExact(value.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day)?day:null;
 public static CertificateDateTracking Tracking(JsonElement data,DateOnly asOf) {
  int? days=Date(data,"expiry") is {} expiry?expiry.DayNumber-asOf.DayNumber:null;
  var bucket=days switch {null=>"NO_EXPIRY_DATE",<0=>"PAST_DATE",0=>"TODAY",<=30=>"WITHIN_30_DAYS",<=60=>"DAYS_31_TO_60",<=90=>"DAYS_61_TO_90",_=>"AFTER_90_DAYS"};
  return new(asOf,TimeBasis,days,bucket,Date(data,"issue") is {} issue&&issue>asOf);
 }
 public static string[] Changes(JsonElement? before,JsonElement after)=>Fields.Concat(["adminState"]).Where(key=>{
  var old=before is {ValueKind:JsonValueKind.Object} b && b.TryGetProperty(key,out var v)?v.GetRawText():null;
  var next=after.ValueKind==JsonValueKind.Object&&after.TryGetProperty(key,out var n)?n.GetRawText():null;
  return old!=next;
 }).ToArray();
 public static ApiFault Invalid(string field)=>new(422,"invalid_certificate_metadata",$"Invalid certificate field: {field}.");
}
