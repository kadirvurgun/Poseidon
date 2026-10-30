using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
namespace Poseidon;
public sealed record InventoryBrowse(VesselBrowse Base,string AdminState,string AssetMode) {
 public static InventoryBrowse Parse(IQueryCollection query,Session scope) {
  string Filter(string key,string[] allowed) {
   if(!query.TryGetValue(key,out var v))return "";
   if(v.Count!=1||!allowed.Contains(v.ToString()))throw new ApiFault(400,"invalid_inventory_query","Invalid inventory filter.");return v.ToString();
  }
  var state=Filter("adminState",["DRAFT","RECORDED"]);var mode=Filter("asset_mode",["SHIP","TUG"]);
  var values=query.Where(x=>x.Key is not ("adminState" or "asset_mode")).ToDictionary(x=>x.Key,x=>x.Value);
  // Domain-separate collection cursors and bind all extra filters without weakening object access.
  var bound=scope with {Subject=scope.Subject+"|inventory:v1|"+state+"|"+mode};
  return new(VesselBrowse.Parse(new QueryCollection(values),bound),state,mode);
 }
}
public sealed record HistoryQuery(int Size,long? BeforeVersion,Guid? BeforeId,string Binding) {
 private sealed record Cursor(int Format,long Version,Guid Id,string Binding);
 public static HistoryQuery Parse(IQueryCollection query,Session scope,Guid id) {
  if(query.Any(p=>p.Key is not ("pageSize" or "cursor")||p.Value.Count!=1))throw Invalid();
  int size=20;if(query.ContainsKey("pageSize")&&(!int.TryParse(query["pageSize"],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out size)||size<1||size>100))throw Invalid();
  var binding=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {domain="inventory-history:v1",scope.Subject,scope.CompanyId,scope.TenantId,grants=scope.VesselIds.Order().ToArray(),id,size}))));
  long? version=null;Guid? eventId=null;
  if(query.ContainsKey("cursor")) {
   var text=query["cursor"].ToString();if(text.Length is 0 or >512)throw Invalid();
   try {var c=JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(text));if(c is null||c.Format!=1||c.Version<1||c.Id==Guid.Empty||c.Binding!=binding)throw Invalid();version=c.Version;eventId=c.Id;}
   catch(Exception e)when(e is JsonException or FormatException or ArgumentException){throw Invalid();}
  }
  return new(size,version,eventId,binding);
 }
 public string After(InventoryHistoryItem item)=>WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Cursor(1,item.Version,item.EventId,Binding)));
 static ApiFault Invalid()=>new(400,"invalid_history_query","Invalid history query or cursor.");
}
