using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Poseidon;
public sealed record EvidencePage(IReadOnlyList<EvidenceIntake> Items,string? NextCursor,bool HasMore);
public sealed record EvidenceBrowse(string TargetType,Guid TargetId,int PageSize,Guid? After,string Binding) {
 public static EvidenceBrowse Parse(IQueryCollection query,Session s) {
  if(query.Keys.Any(k=>k is not("targetType" or "targetId" or "pageSize" or "cursor"))||query.Any(p=>p.Value.Count!=1))throw Bad();
  var type=query["targetType"].ToString();if(type is not("Vessel" or "Certificate")||!Guid.TryParseExact(query["targetId"],"D",out var target)||target==Guid.Empty)throw Bad();
  var count=50;if(query.ContainsKey("pageSize")&&(!int.TryParse(query["pageSize"],out count)||count<1||count>100))throw Bad();
  var binding=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{domain="evidence-list:v1",type,target,count,s.Subject,s.TenantId,s.CompanyId,vessels=s.VesselIds.OrderBy(v=>v)}))));
  Guid? after=null;if(query.TryGetValue("cursor",out var raw))try {if(raw.ToString().Length>512)throw Bad();var parts=Encoding.UTF8.GetString(Convert.FromBase64String(raw.ToString())).Split('|');if(parts.Length!=2||parts[1]!=binding||!Guid.TryParseExact(parts[0],"D",out var id)||id==Guid.Empty)throw Bad();after=id;}catch(Exception e)when(e is FormatException or ArgumentException){throw Bad();}
  return new(type,target,count,after,binding);
 }
 public string Cursor(Guid id)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(id.ToString("D")+"|"+Binding));
 static ApiFault Bad()=>new(400,"invalid_query","Invalid evidence query or cursor.");
}
