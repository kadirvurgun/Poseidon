using System.Globalization;
using System.Numerics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Poseidon;

public sealed record InventoryCompleteness(string[] MissingRequiredFields, string[] InvalidFields);
public sealed record InventoryVessel(Guid Id,long Version,string AdminState,JsonElement Data,InventoryCompleteness Completeness,DateTimeOffset? CreatedAt,DateTimeOffset? ModifiedAt);
public sealed record InventoryPage(IReadOnlyList<InventoryVessel> Items,string? NextCursor,bool HasMore);
public sealed record InventoryHistoryItem(Guid EventId,DateTimeOffset OccurredAt,long Version,string Action,string[] ChangedFields,string ActorRelation);
public sealed record InventoryHistoryPage(IReadOnlyList<InventoryHistoryItem> Items,string? NextCursor,bool HasMore);
public static class InventoryContracts
{
 public static readonly string[] Required=["company_id","name","flag","vessel_type","asset_mode"];
 public static readonly string[] Numbers=["gt","nt","dwt","loa","breadth","draft"];
 public static readonly string[] Fields=["company_id","name","flag","vessel_type","asset_mode","imo_no","official_no","port_registry","call_sign","mmsi","gt","nt","dwt","loa","breadth","draft","build_date","class_society","class_no","doc_company","company_imo","master_name","dpa_name","notes","evidence_ref"];
 public static InventoryCompleteness Completeness(JsonElement data) {
  var missing=Required.Where(k=>!data.TryGetProperty(k,out var v)||v.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(v.GetString())).ToArray();
  var company=data.TryGetProperty("company_id",out var companyValue)&&companyValue.ValueKind==JsonValueKind.String&&Guid.TryParse(companyValue.GetString(),out var parsed)?parsed:Guid.Empty;
  List<string> invalid=[];
  foreach(var p in data.EnumerateObject())try{Validate(JsonSerializer.SerializeToElement(new{data=new Dictionary<string,JsonElement>{{p.Name,p.Value}}}),new Session("validation",Guid.Empty,company,[],false));}catch(ApiFault){invalid.Add(Fields.Contains(p.Name)?p.Name:"unknown_field");}
  return new(missing,invalid.Distinct().ToArray());
 }
 public static JsonElement Validate(JsonElement body,Session s) {
  Object(body,["data"]);
  if(!body.TryGetProperty("data",out var data)||data.ValueKind!=JsonValueKind.Object)throw Invalid("data");
  Object(data,Fields);
  var result=new Dictionary<string,object>();
  foreach(var p in data.EnumerateObject()) {
   if(Numbers.Contains(p.Name)) {
    if(p.Value.ValueKind!=JsonValueKind.Number || !ExactNumber(p.Value,out var number))throw Invalid(p.Name);
    result[p.Name]=number;continue;
   }
   if(p.Value.ValueKind!=JsonValueKind.String)throw Invalid(p.Name);
   var value=p.Value.GetString()!.Trim();
   if(value.Length==0||value.Length>(p.Name=="notes"?4000:200))throw Invalid(p.Name);
   if(p.Name=="imo_no") ImoNumber.Validate(p.Value.GetString()!);
   if(p.Name=="company_imo" && !Regex.IsMatch(value,@"\A[0-9]{7}\z"))throw Invalid(p.Name);
   if(p.Name=="asset_mode" && value is not ("SHIP" or "TUG"))throw Invalid(p.Name);
   if(p.Name=="build_date" && !DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw Invalid(p.Name);
   if(p.Name=="company_id" && (!Guid.TryParseExact(value,"D",out var company)||company!=s.CompanyId))throw new ApiFault(403,"company_scope","Company is server controlled.");
   result[p.Name]=value;
  }
  result["company_id"]=s.CompanyId.ToString("D");
  return JsonSerializer.SerializeToElement(result);
 }
 // Exact decimal(12,3) technical bound, including exponent notation; never round input.
 public static bool ExactNumber(JsonElement element,out decimal value) {
  value=0;var raw=element.GetRawText();if(raw.Length>128)return false;
  var match=Regex.Match(raw,@"\A(?<sign>-?)(?<whole>\d+)(?:\.(?<fraction>\d+))?(?:[eE](?<exponent>[+-]?\d+))?\z");
  if(!match.Success)return false;
  var exponent=0;if(match.Groups["exponent"].Success && (!int.TryParse(match.Groups["exponent"].Value,out exponent)||Math.Abs((long)exponent)>100))return false;
  var digits=BigInteger.Parse(match.Groups["whole"].Value+match.Groups["fraction"].Value,CultureInfo.InvariantCulture);
  if(match.Groups["sign"].Value=="-" && digits!=0)return false;
  var scale=match.Groups["fraction"].Length-exponent;
  while(scale>3 && digits%10==0 && digits!=0){digits/=10;scale--;}
  if(digits==0)return true;
  if(scale>3)return false;
  var scaled=digits*BigInteger.Pow(10,3-scale);
  if(scaled>999999999999L)return false;
  value=(decimal)scaled/1000m;return true;
 }
 public static void Object(JsonElement body,string[] fields) {
  if(body.ValueKind!=JsonValueKind.Object)throw Invalid("body");
  var props=body.EnumerateObject().ToArray();
  if(props.Select(x=>x.Name).Distinct().Count()!=props.Length || props.Any(x=>!fields.Contains(x.Name)))throw Invalid("unknown_or_duplicate_field");
 }
 public static void Permission(ClaimsPrincipal user,string permission) {if(!user.HasClaim("permission",permission))throw new ApiFault(403,"action_forbidden","The current identity lacks this action capability.");}
 public static ApiFault Invalid(string field)=>new(422,"invalid_inventory_metadata",$"Invalid inventory field: {field}.");
 public static string[] Changes(JsonElement? before,JsonElement after)=>Fields.Concat(["adminState"]).Where(k=> {
  var old=before is {ValueKind:JsonValueKind.Object} b && b.TryGetProperty(k,out var v)?v.GetRawText():null;
  var next=after.ValueKind==JsonValueKind.Object&&after.TryGetProperty(k,out var n)?n.GetRawText():null;
  return old!=next;
 }).ToArray();
}
