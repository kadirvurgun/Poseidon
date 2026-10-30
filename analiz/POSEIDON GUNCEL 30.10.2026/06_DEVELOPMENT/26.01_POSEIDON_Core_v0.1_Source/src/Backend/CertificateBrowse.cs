using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
namespace Poseidon;
public sealed record CertificateBrowse(string Q,Guid? VesselId,string AdminState,string DateBucket,int PageSize,Guid? After,DateOnly AsOfDate,string Binding) {
 private sealed record Cursor(int Format,Guid After,string AsOfDate,string Binding);
 public static CertificateBrowse Parse(IQueryCollection query,Session scope,DateOnly today) {
  var allowed=new[]{"q","vessel_id","adminState","dateBucket","pageSize","cursor"};
  if(query.Any(p=>!allowed.Contains(p.Key)||p.Value.Count!=1))throw Invalid();
  var raw=query["q"].ToString();if(raw.Length>200||raw.Any(char.IsControl)||raw.Trim().Length>100)throw Invalid();var q=VesselBrowse.Fold(raw.Trim());
  Guid? vessel=null;if(query.ContainsKey("vessel_id")){if(!Guid.TryParseExact(query["vessel_id"].ToString(),"D",out var id)||id==Guid.Empty)throw Invalid();vessel=id;}
  var state=query["adminState"].ToString();if(query.ContainsKey("adminState")&&state is not ("DRAFT" or "RECORDED"))throw Invalid();
  var bucket=query["dateBucket"].ToString();if(query.ContainsKey("dateBucket")&&!CertificateContracts.Buckets.Contains(bucket))throw Invalid();
  var size=50;if(query.ContainsKey("pageSize")&&(!int.TryParse(query["pageSize"],NumberStyles.None,CultureInfo.InvariantCulture,out size)||size<1||size>100))throw Invalid();
  Cursor? cursor=null;var asOf=today;
  if(query.ContainsKey("cursor")) {
   var encoded=query["cursor"].ToString();if(encoded.Length is 0 or >512)throw Invalid();
   try {cursor=JsonSerializer.Deserialize<Cursor>(WebEncoders.Base64UrlDecode(encoded));if(cursor is null||cursor.Format!=1||cursor.After==Guid.Empty||!DateOnly.TryParseExact(cursor.AsOfDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out asOf)||asOf>today)throw Invalid();}
   catch(Exception e)when(e is JsonException or FormatException or ArgumentException){throw Invalid();}
  }
  // Consistency binding only, never authority. Date context is held across midnight, not a data snapshot.
  var binding=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{domain="certificate-register:v1",q,vessel,state,bucket,size,asOf,scope.Subject,scope.TenantId,scope.CompanyId,grants=scope.VesselIds.Order().ToArray()}))));
  if(cursor is not null&&cursor.Binding!=binding)throw Invalid();
  return new(q,vessel,state,bucket,size,cursor?.After,asOf,binding);
 }
 public string AfterRecord(Guid id)=>WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Cursor(1,id,AsOfDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),Binding)));
 static ApiFault Invalid()=>new(400,"invalid_certificate_query","Invalid query or cursor. Restart pagination to change filters or date context.");
}
public sealed record CertificateHistoryQuery(HistoryQuery Query) {
 public static CertificateHistoryQuery Parse(IQueryCollection query,Session scope,Guid id)=>new(HistoryQuery.Parse(query,scope with {Subject=scope.Subject+"|certificate-history:v1"},id));
}
