using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
namespace Poseidon;
public sealed partial class DraftStore {
 const string CertificateFrom="drafts d JOIN drafts v ON v.tenant_id=d.tenant_id AND v.company_id=d.company_id AND v.id=d.vessel_id AND v.kind='vessels'";
 const string CertificateAccess="d.tenant_id=$1 AND d.company_id=$2 AND d.kind='certificates' AND (v.owner_subject=$3 OR v.id=ANY($4))";
 const string CertificateColumns="d.id,d.version,d.admin_state,d.data::text,v.id,v.data->>'name',v.data->>'imo_no',v.data->>'flag',v.admin_state";
 public DateOnly CertificateToday=>CertificateContracts.Today(clock??TimeProvider.System);
 static CertificateRecord ReadCertificate(NpgsqlDataReader r,DateOnly asOf) {
  var data=JsonDocument.Parse(r.GetString(3)).RootElement.Clone();
  var parent=new CertificateParent(r.GetGuid(4),r.IsDBNull(5)?null:r.GetString(5),r.IsDBNull(6)?null:r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.GetString(8));
  return new(r.GetGuid(0),r.GetInt64(1),r.GetString(2),data,CertificateContracts.Completeness(data),CertificateContracts.Tracking(data,asOf),"NOT_PERFORMED","NOT_PERFORMED",parent);
 }
 static async Task<CertificateRecord?> CertificateGet(NpgsqlConnection c,NpgsqlTransaction? tx,Session s,Guid id,DateOnly asOf,CancellationToken ct,bool locked=false) {
  await using var cmd=Query(c,tx,"SELECT "+CertificateColumns+" FROM "+CertificateFrom+" WHERE "+CertificateAccess+" AND d.id=$5"+(locked?" FOR UPDATE OF d":""),s.TenantId,s.CompanyId,s.Subject,s.VesselIds,id);
  await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?ReadCertificate(r,asOf):null;
 }
 static ApiFault CertificateNotFound()=>new(404,"not_found","Certificate not found.");
 public async Task<CertificateRecord> CertificateGet(Session s,Guid id,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);return await CertificateGet(c,null,s,id,CertificateToday,ct)??throw CertificateNotFound();
 }
 public async Task<CertificatePage> CertificateList(Session s,CertificateBrowse q,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);
  // Date comparisons are DateOnly/PostgreSQL date, never browser-local timestamps.
  const string bucketSql="""
   CASE WHEN d.data->>'expiry' IS NULL THEN 'NO_EXPIRY_DATE'
    WHEN (d.data->>'expiry')::date < $10::date THEN 'PAST_DATE'
    WHEN (d.data->>'expiry')::date = $10::date THEN 'TODAY'
    WHEN (d.data->>'expiry')::date - $10::date <= 30 THEN 'WITHIN_30_DAYS'
    WHEN (d.data->>'expiry')::date - $10::date <= 60 THEN 'DAYS_31_TO_60'
    WHEN (d.data->>'expiry')::date - $10::date <= 90 THEN 'DAYS_61_TO_90'
    ELSE 'AFTER_90_DAYS' END
   """;
  var sql="SELECT "+CertificateColumns+" FROM "+CertificateFrom+" WHERE "+CertificateAccess+" "+"""
   AND ($5::uuid IS NULL OR d.id>$5)
   AND ($6='' OR translate(coalesce(d.data->>'type',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
    OR translate(coalesce(d.data->>'issuer',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
    OR translate(coalesce(d.data->>'certificate_no',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\')
   AND ($8::uuid IS NULL OR d.vessel_id=$8)
   AND ($9='' OR d.admin_state=$9)
   """+" AND ($11='' OR ("+bucketSql+")=$11) ORDER BY d.id LIMIT $12";
  await using var cmd=Query(c,null,sql,s.TenantId,s.CompanyId,s.Subject,s.VesselIds,q.After.HasValue?q.After.Value:DBNull.Value,q.Q,VesselBrowse.Like(q.Q),q.VesselId.HasValue?q.VesselId.Value:DBNull.Value,q.AdminState,q.AsOfDate,q.DateBucket,q.PageSize+1);cmd.Parameters[4].NpgsqlDbType=NpgsqlDbType.Uuid;cmd.Parameters[7].NpgsqlDbType=NpgsqlDbType.Uuid;
  await using var r=await cmd.ExecuteReaderAsync(ct);List<CertificateRecord> items=[];while(await r.ReadAsync(ct))items.Add(ReadCertificate(r,q.AsOfDate));
  var more=items.Count>q.PageSize;if(more)items.RemoveAt(items.Count-1);return new(items,more?q.AfterRecord(items[^1].Id):null,more,q.AsOfDate,CertificateContracts.TimeBasis);
 }
 sealed record CertificateReplay(string Permission,CertificateRecord Result);
 static string CertificatePermission(string action,CertificateRecord? current)=>action is "record" or "reopen" || action=="metadata"&&current?.AdminState=="RECORDED"?"certificate:manage":"draft:write";
 public async Task<CertificateRecord> CertificateWrite(ClaimsPrincipal user,Guid? id,string action,long? expected,JsonElement body,string key,string correlation,CancellationToken ct) {
  var scope=Contracts.Scope(user);JsonElement? supplied=null;string? reason=null;
  if(action is "create" or "metadata") {if(action=="create")CertificateContracts.Object(body,["data"]);supplied=CertificateContracts.Validate(body);reason=CertificateContracts.Reason(body,false);}
  else if(action=="record")CertificateContracts.Object(body,[]);
  else if(action=="reopen"){CertificateContracts.Object(body,["reason"]);reason=CertificateContracts.Reason(body,true);}
  else throw new InvalidOperationException("Unknown certificate action.");
  var canonical=JsonSerializer.Serialize(body.EnumerateObject().OrderBy(p=>p.Name).ToDictionary(p=>p.Name,p=>p.Value));
  var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"certificate-register:v1|{action}|{id}|{expected}|{canonical}")));
  var today=CertificateToday;
  await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await using(var keyLock=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))",$"{scope.TenantId}|{scope.Subject}|{key}"))await keyLock.ExecuteNonQueryAsync(ct);
  var before=id.HasValue?await CertificateGet(c,tx,scope,id.Value,today,ct,true)??throw CertificateNotFound():null;
  var permission=CertificatePermission(action,before);InventoryContracts.Permission(user,permission);
  await using(var replayQuery=Query(c,tx,"SELECT fingerprint,response::text FROM idempotency WHERE tenant_id=$1 AND subject=$2 AND key=$3",scope.TenantId,scope.Subject,key)) {
   await using var r=await replayQuery.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)) {
    if(r.GetString(0)!=fingerprint)throw new ApiFault(409,"idempotency_conflict","Key was used with another request.");
    var replay=JsonSerializer.Deserialize<CertificateReplay>(r.GetString(1),Json)!;await r.DisposeAsync();
    InventoryContracts.Permission(user,replay.Permission);
    if(await CertificateGet(c,tx,scope,replay.Result.Id,today,ct) is null)throw CertificateNotFound();
    // Replay is the exact original administrative response. UI re-reads current dates/parent afterward.
    return replay.Result;
   }
  }
  if(before is not null&&before.Version!=expected)throw new ApiFault(412,"stale_version","Reload before changing certificate metadata.");
  if(action=="record"&&before?.AdminState!="DRAFT" || action=="reopen"&&before?.AdminState!="RECORDED")throw new ApiFault(409,"invalid_transition","This administrative transition is not allowed.");
  if(action=="metadata"&&before?.AdminState=="RECORDED")reason=CertificateContracts.Reason(body,true);
  var data=supplied??before!.Data;var parentId=Guid.Parse(data.GetProperty("vessel_id").GetString()!);
  if(before is not null&&parentId!=before.ParentVessel.Id)throw new ApiFault(422,"immutable_vessel","Certificate vessel cannot change.");
  var parent=await InventoryGet(c,tx,scope,parentId,ct)??throw new ApiFault(404,"vessel_not_found","Vessel not found in authorized scope.");
  var state=action=="record"?"RECORDED":action=="reopen"?"DRAFT":before?.AdminState??"DRAFT";
  var completeness=CertificateContracts.Completeness(data);
  if(state=="RECORDED"&&(completeness.MissingRequiredFields.Length>0||completeness.InvalidFields.Length>0))throw new ApiFault(422,"certificate_incomplete","Missing administrative fields: "+string.Join(", ",completeness.MissingRequiredFields));
  string? ParentText(string name)=>parent.Data.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
  var result=new CertificateRecord(id??Guid.NewGuid(),(before?.Version??0)+1,state,data,completeness,CertificateContracts.Tracking(data,today),"NOT_PERFORMED","NOT_PERFORMED",new(parent.Id,ParentText("name"),ParentText("imo_no"),ParentText("flag"),parent.AdminState));
  await using(var marker=Query(c,tx,"SELECT set_config('poseidon.certificate_write','on',true)"))await marker.ExecuteNonQueryAsync(ct);
  if(before is null) {
   await using var insert=Query(c,tx,"INSERT INTO drafts(tenant_id,id,company_id,kind,vessel_id,owner_subject,version,data,admin_state,certificate_schema_version) VALUES($1,$2,$3,'certificates',$4,$5,$6,$7::jsonb,$8,2)",scope.TenantId,result.Id,scope.CompanyId,parentId,scope.Subject,result.Version,data.GetRawText(),state);await insert.ExecuteNonQueryAsync(ct);
  } else {
   await using var update=Query(c,tx,"UPDATE drafts SET data=$1::jsonb,version=$2,admin_state=$3,certificate_schema_version=2 WHERE tenant_id=$4 AND id=$5 AND version=$6",data.GetRawText(),result.Version,state,scope.TenantId,result.Id,expected!.Value);if(await update.ExecuteNonQueryAsync(ct)!=1)throw new ApiFault(412,"stale_version","Concurrent update detected.");
  }
  JsonElement AuditData(CertificateRecord item){var values=item.Data.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);values["adminState"]=JsonSerializer.SerializeToElement(item.AdminState);return JsonSerializer.SerializeToElement(values);}
  var code=action switch {"create"=>"DRAFT_CREATED","record"=>"CERTIFICATE_RECORDED","reopen"=>"CERTIFICATE_REOPENED",_=>state=="RECORDED"?"CERTIFICATE_METADATA_UPDATED":"DRAFT_UPDATED"};
  await using(var audit=Query(c,tx,"INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,before_data,after_data,version,correlation_id,restricted_metadata) VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb,$10,$11,$12::jsonb)",Guid.NewGuid(),scope.TenantId,scope.CompanyId,result.Id,parentId,scope.Subject,code,before is null?"null":AuditData(before).GetRawText(),AuditData(result).GetRawText(),result.Version,correlation,JsonSerializer.Serialize(new{reason})))await audit.ExecuteNonQueryAsync(ct);
  await using(var replay=Query(c,tx,"INSERT INTO idempotency VALUES($1,$2,$3,$4,$5::jsonb)",scope.TenantId,scope.Subject,key,fingerprint,JsonSerializer.Serialize(new CertificateReplay(permission,result),Json)))await replay.ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);return result;
 }
 public async Task<InventoryHistoryPage> CertificateHistory(Session scope,Guid id,CertificateHistoryQuery query,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);if(await CertificateGet(c,null,scope,id,CertificateToday,ct) is null)throw CertificateNotFound();var q=query.Query;
  var sql="SELECT a.id,a.occurred_at,a.version,a.action,a.before_data::text,a.after_data::text,a.actor FROM "+CertificateFrom+" JOIN audit_events a ON a.tenant_id=d.tenant_id AND a.company_id=d.company_id AND a.entity_id=d.id AND a.vessel_id=d.vessel_id WHERE "+CertificateAccess+" AND d.id=$5 AND ($6::bigint IS NULL OR (a.version,a.id)<($6,$7::uuid)) ORDER BY a.version DESC,a.id DESC LIMIT $8";
  await using var cmd=Query(c,null,sql,scope.TenantId,scope.CompanyId,scope.Subject,scope.VesselIds,id,q.BeforeVersion.HasValue?q.BeforeVersion.Value:DBNull.Value,q.BeforeId.HasValue?q.BeforeId.Value:DBNull.Value,q.Size+1);cmd.Parameters[5].NpgsqlDbType=NpgsqlDbType.Bigint;cmd.Parameters[6].NpgsqlDbType=NpgsqlDbType.Uuid;
  await using var r=await cmd.ExecuteReaderAsync(ct);List<InventoryHistoryItem> items=[];
  while(await r.ReadAsync(ct)) {
   JsonElement? before=r.IsDBNull(4)?null:JsonDocument.Parse(r.GetString(4)).RootElement.Clone();var after=JsonDocument.Parse(r.GetString(5)).RootElement.Clone();var action=r.GetString(3);
   if(!new[]{"DRAFT_CREATED","DRAFT_UPDATED","CERTIFICATE_RECORDED","CERTIFICATE_METADATA_UPDATED","CERTIFICATE_REOPENED"}.Contains(action))action="HISTORICAL_METADATA_CHANGE";
   items.Add(new(r.GetGuid(0),r.GetFieldValue<DateTimeOffset>(1),r.GetInt64(2),action,CertificateContracts.Changes(before,after),r.GetString(6)==scope.Subject?"SELF":"OTHER"));
  }
  var more=items.Count>q.Size;if(more)items.RemoveAt(items.Count-1);return new(items,more?q.After(items[^1]):null,more);
 }
 static ApiFault CertificateRequired()=>new(409,"certificate_resource_required","Use the certificate register; legacy writes cannot replace extended or recorded metadata.");
 static async Task CheckLegacyCertificateWrite(NpgsqlConnection c,NpgsqlTransaction tx,Session scope,Guid id,CancellationToken ct) {
  var row=await CertificateGet(c,tx,scope,id,DateOnly.MinValue,ct,true)??throw CertificateNotFound();
  await using var q=Query(c,tx,"SELECT certificate_schema_version FROM drafts WHERE tenant_id=$1 AND id=$2",scope.TenantId,id);
  if(row.AdminState!="DRAFT"||(int)(await q.ExecuteScalarAsync(ct))! ==2)throw CertificateRequired();
 }
}
