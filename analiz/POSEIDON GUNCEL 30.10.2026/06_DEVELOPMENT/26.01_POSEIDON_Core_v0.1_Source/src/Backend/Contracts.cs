using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Poseidon;
public sealed record Session(string Subject, Guid TenantId, Guid CompanyId, Guid[] VesselIds, bool Synthetic);
public sealed record Draft(Guid DraftId, long Version, string PersistenceState, bool SubmissionAllowed, JsonElement Data);
public sealed class ApiFault(int status, string code, string message) : Exception(message) { public int Status => status; public string Code => code; }
public static partial class Contracts
{
 public static Session Scope(ClaimsPrincipal user) {
  var synthetic=user.Identity?.AuthenticationType=="Synthetic" && user.HasClaim("synthetic","true");
  if(user.Identity?.IsAuthenticated!=true || user.FindAll("sub").Count()!=1 || user.FindAll("tenant_id").Count()!=1 || user.FindAll("company_id").Count()!=1 ||
     !Guid.TryParse(user.FindFirstValue("tenant_id"),out var tenant)||!Guid.TryParse(user.FindFirstValue("company_id"),out var company)||tenant==Guid.Empty||company==Guid.Empty||string.IsNullOrWhiteSpace(user.FindFirstValue("sub")))
      throw new ApiFault(403,"scope_missing","Unique verified tenant, company and subject claims required.");
  var vessels=user.FindAll("vessel_id").Select(x=>Guid.TryParse(x.Value,out var id)?id:Guid.Empty).ToArray();
  if(vessels.Contains(Guid.Empty)||vessels.Distinct().Count()!=vessels.Length)throw new ApiFault(403,"scope_invalid","Invalid vessel grants.");
  var subject=user.FindFirstValue("sub")!;
  if(!synthetic) {
   if(user.FindAll("iss").Count()!=1 || string.IsNullOrWhiteSpace(user.FindFirstValue("iss")))throw new ApiFault(403,"scope_missing","Verified issuer required.");
   subject=IdentityBoundary.ActorKey(new(user.FindFirstValue("iss")!,subject));
  }
  return new(subject,tenant,company,vessels,synthetic || (user.Identity?.AuthenticationType==BffAuthentication.CookieScheme && user.HasClaim("development_uat","true")));
 }
 public static JsonElement Validate(JsonElement body,string kind) {
  if(body.ValueKind!=JsonValueKind.Object) throw new ApiFault(400,"invalid_body","Object required.");
  var outer=body.EnumerateObject().ToArray();
  if(outer.Select(x=>x.Name).Distinct().Count()!=outer.Length || outer.Any(x=>x.Name is not ("saveMode" or "data"))) throw new ApiFault(422,"unknown_field","Unknown or duplicate envelope field.");
  if(!body.TryGetProperty("saveMode",out var mode)||mode.ValueKind!=JsonValueKind.String||mode.GetString()!="DRAFT") throw new ApiFault(403,"submission_disabled","Only metadata DRAFT saves are permitted; completion is not verification.");
  if(!body.TryGetProperty("data",out var data)||data.ValueKind!=JsonValueKind.Object) throw new ApiFault(422,"invalid_data","data must be an object.");
  string[] allowed=kind=="vessels"?["name","company_id","imo_no","official_no","flag","vessel_type","notes"]:["vessel_id","type","certificate_no","issuer","issue","expiry","notes"];
  var fields=data.EnumerateObject().ToArray();
  if(fields.Select(x=>x.Name).Distinct().Count()!=fields.Length) throw new ApiFault(422,"duplicate_field","Duplicate field.");
  foreach(var p in fields) {
   if(!allowed.Contains(p.Name)) throw new ApiFault(422,"unknown_field",$"Field {p.Name} is unknown or server controlled.");
   if(p.Value.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(p.Value.GetString()) || p.Value.GetString()!.Length>(p.Name=="notes"?4000:200)) throw new ApiFault(422,"invalid_value",$"{p.Name}: provide a nonblank string within the field limit, or omit it.");
   var value=p.Value.GetString()!;
   if(p.Name.EndsWith("_id") && (!Guid.TryParseExact(value,"D",out var id)||id==Guid.Empty)) throw new ApiFault(422,"invalid_reference",$"{p.Name}: UUID required.");
   if(p.Name=="imo_no") ImoNumber.Validate(value);
   if((p.Name is "issue" or "expiry")&&!DateOnly.TryParseExact(value,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _)) throw new ApiFault(422,"invalid_date",$"{p.Name}: ISO calendar date required.");
  }
  if(kind=="certificates"&&!data.TryGetProperty("vessel_id",out _)) throw new ApiFault(422,"vessel_required","vessel_id is required to establish authorization scope.");
  if(data.TryGetProperty("issue",out var issue)&&data.TryGetProperty("expiry",out var expiry)&&string.CompareOrdinal(issue.GetString(),expiry.GetString())>0) throw new ApiFault(422,"invalid_date_range","expiry cannot precede issue.");
  return data.Clone();
 }
}
