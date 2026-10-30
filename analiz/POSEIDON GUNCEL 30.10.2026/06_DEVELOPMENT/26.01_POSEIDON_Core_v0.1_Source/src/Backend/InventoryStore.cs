using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
namespace Poseidon;
public sealed partial class DraftStore
{
 const string InventoryAccess="d.tenant_id=$1 AND d.company_id=$2 AND d.kind='vessels' AND (d.owner_subject=$3 OR d.id=ANY($4))";
 static InventoryVessel ReadInventory(NpgsqlDataReader r) {
  var data=JsonDocument.Parse(r.GetString(3)).RootElement.Clone();
  return new(r.GetGuid(0),r.GetInt64(1),r.GetString(2),data,InventoryContracts.Completeness(data),null,null);
 }
 static async Task<InventoryVessel?> InventoryGet(NpgsqlConnection c,NpgsqlTransaction? tx,Session s,Guid id,CancellationToken ct,bool locked=false) {
  await using var cmd=Query(c,tx,"SELECT d.id,d.version,d.admin_state,d.data::text FROM drafts d WHERE "+InventoryAccess+" AND d.id=$5"+(locked?" FOR UPDATE":""),s.TenantId,s.CompanyId,s.Subject,s.VesselIds,id);
  await using var reader=await cmd.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct))return null;return ReadInventory(reader);
 }
 public async Task<InventoryVessel> InventoryGet(Session s,Guid id,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);var item=await InventoryGet(c,null,s,id,ct)??throw NotFound();
  await using var times=Query(c,null,"SELECT min(occurred_at) FILTER (WHERE action='DRAFT_CREATED'),max(occurred_at) FROM audit_events WHERE tenant_id=$1 AND company_id=$2 AND entity_id=$3 AND vessel_id=$3",s.TenantId,s.CompanyId,id);
  await using var r=await times.ExecuteReaderAsync(ct);await r.ReadAsync(ct);
  return item with {CreatedAt=r.IsDBNull(0)?null:r.GetFieldValue<DateTimeOffset>(0),ModifiedAt=r.IsDBNull(1)?null:r.GetFieldValue<DateTimeOffset>(1)};
 }
 public async Task<InventoryPage> InventoryList(Session s,InventoryBrowse query,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);var q=query.Base;
  var sql="SELECT d.id,d.version,d.admin_state,d.data::text FROM drafts d WHERE "+InventoryAccess+"""
   AND ($5::uuid IS NULL OR d.id>$5)
   AND ($6='' OR translate(coalesce(d.data->>'name',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
    OR translate(coalesce(d.data->>'imo_no',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
    OR translate(coalesce(d.data->>'official_no',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\')
   AND ($8='' OR d.data->>'flag' COLLATE "C"=$8)
   AND ($9='' OR d.data->>'vessel_type' COLLATE "C"=$9)
   AND ($10='' OR d.admin_state=$10) AND ($11='' OR d.data->>'asset_mode'=$11)
   ORDER BY d.id LIMIT $12
   """;
  await using var cmd=Query(c,null,sql,s.TenantId,s.CompanyId,s.Subject,s.VesselIds,q.After.HasValue?q.After.Value:DBNull.Value,q.Q,VesselBrowse.Like(q.Q),q.Flag,q.VesselType,query.AdminState,query.AssetMode,q.PageSize+1);cmd.Parameters[4].NpgsqlDbType=NpgsqlDbType.Uuid;
  await using var r=await cmd.ExecuteReaderAsync(ct);List<InventoryVessel> items=[];while(await r.ReadAsync(ct))items.Add(ReadInventory(r));
  var more=items.Count>q.PageSize;if(more)items.RemoveAt(items.Count-1);return new(items,more?q.CursorAfter(items[^1].Id):null,more);
 }
 sealed record InventoryReplay(string Permission,InventoryVessel Result);
 static ApiFault NotFound()=>new(404,"not_found","Vessel not found.");
 static string WritePermission(string action,InventoryVessel? current)=>action is "record" or "reopen" || action=="metadata"&&current?.AdminState=="RECORDED"?"inventory:manage":"draft:write";
 public async Task<InventoryVessel> InventoryWrite(ClaimsPrincipal user,Guid? id,string action,long? expected,JsonElement body,string key,string correlation,CancellationToken ct) {
  var s=Contracts.Scope(user);
  // Validate shape before acquiring DB resources. Authorization still checked on every replay.
  JsonElement? supplied=null;string? reason=null;
  if(action is "create" or "metadata")supplied=InventoryContracts.Validate(body,s);
  else if(action=="record")InventoryContracts.Object(body,[]);
  else if(action=="reopen") {InventoryContracts.Object(body,["reason"]);if(!body.TryGetProperty("reason",out var v)||v.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(v.GetString())||v.GetString()!.Trim().Length>500)throw InventoryContracts.Invalid("reason");reason=v.GetString()!.Trim();}
  else throw new InvalidOperationException("Unknown inventory command.");
  var canonical=JsonSerializer.Serialize(body.EnumerateObject().OrderBy(p=>p.Name).ToDictionary(p=>p.Name,p=>p.Value));
  var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"inventory:v1|{action}|{id}|{expected}|{canonical}")));
  await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))",$"{s.TenantId}|{s.Subject}|{key}"))await l.ExecuteNonQueryAsync(ct);
  InventoryVessel? before=id.HasValue?await InventoryGet(c,tx,s,id.Value,ct,true)??throw NotFound():null;
  var permission=WritePermission(action,before);InventoryContracts.Permission(user,permission);
  await using(var replayQuery=Query(c,tx,"SELECT fingerprint,response::text FROM idempotency WHERE tenant_id=$1 AND subject=$2 AND key=$3",s.TenantId,s.Subject,key)) {
   await using var r=await replayQuery.ExecuteReaderAsync(ct);
   if(await r.ReadAsync(ct)) {
    if(r.GetString(0)!=fingerprint)throw new ApiFault(409,"idempotency_conflict","Key was used with another request.");
    var replay=JsonSerializer.Deserialize<InventoryReplay>(r.GetString(1),Json)!;await r.DisposeAsync();
    InventoryContracts.Permission(user,replay.Permission);
    if(await InventoryGet(c,tx,s,replay.Result.Id,ct) is null)throw NotFound();
    return replay.Result;
   }
  }
  if(before is not null && before.Version!=expected)throw new ApiFault(412,"stale_version","Reload before changing the inventory record.");
  await using(var company=Query(c,tx,"SELECT 1 FROM companies WHERE tenant_id=$1 AND id=$2",s.TenantId,s.CompanyId))if(await company.ExecuteScalarAsync(ct) is null)throw new ApiFault(403,"company_scope","Company has not been provisioned.");
  if(action=="record"&&before?.AdminState!="DRAFT" || action=="reopen"&&before?.AdminState!="RECORDED")throw new ApiFault(409,"invalid_transition","This administrative transition is not allowed.");
  var data=supplied??before!.Data;var state=action=="record"?"RECORDED":action=="reopen"?"DRAFT":before?.AdminState??"DRAFT";
  // Legacy incomplete records may omit company_id; authorized company is authoritative.
  var normalized=data.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);
  normalized["company_id"]=JsonSerializer.SerializeToElement(s.CompanyId.ToString("D"));data=JsonSerializer.SerializeToElement(normalized);
  var completeness=InventoryContracts.Completeness(data);
  if(state=="RECORDED" && (completeness.MissingRequiredFields.Length>0||completeness.InvalidFields.Length>0))throw new ApiFault(422,"inventory_incomplete","Missing administrative fields: "+string.Join(", ",completeness.MissingRequiredFields));
  if(action=="metadata" && before?.AdminState=="RECORDED" && before.Data.GetProperty("asset_mode").GetString()!=data.GetProperty("asset_mode").GetString())throw new ApiFault(422,"reopen_required","Reopen before changing asset mode.");
  var result=new InventoryVessel(id??Guid.NewGuid(),(before?.Version??0)+1,state,data,completeness,null,null);
  await using(var mark=Query(c,tx,"SELECT set_config('poseidon.inventory_write','on',true)"))await mark.ExecuteNonQueryAsync(ct);
  if(before is null) {
   await using var insert=Query(c,tx,"INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data,admin_state,inventory_schema_version) VALUES($1,$2,$3,'vessels',$4,$5,$6::jsonb,$7,2)",s.TenantId,result.Id,s.CompanyId,s.Subject,result.Version,data.GetRawText(),state);await insert.ExecuteNonQueryAsync(ct);
  } else {
   await using var update=Query(c,tx,"UPDATE drafts SET data=$1::jsonb,version=$2,admin_state=$3,inventory_schema_version=2 WHERE tenant_id=$4 AND id=$5 AND version=$6",data.GetRawText(),result.Version,state,s.TenantId,result.Id,expected!.Value);
   if(await update.ExecuteNonQueryAsync(ct)!=1)throw new ApiFault(412,"stale_version","Concurrent update detected.");
  }
  JsonElement AuditData(InventoryVessel item) {var fields=item.Data.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);fields["adminState"]=JsonSerializer.SerializeToElement(item.AdminState);return JsonSerializer.SerializeToElement(fields);}
  var code=action switch {"create"=>"DRAFT_CREATED","record"=>"INVENTORY_RECORDED","reopen"=>"INVENTORY_REOPENED",_=>state=="RECORDED"?"INVENTORY_UPDATED":"DRAFT_UPDATED"};
  await using(var audit=Query(c,tx,"INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,before_data,after_data,version,correlation_id,restricted_metadata) VALUES($1,$2,$3,$4,$4,$5,$6,$7::jsonb,$8::jsonb,$9,$10,$11::jsonb)",Guid.NewGuid(),s.TenantId,s.CompanyId,result.Id,s.Subject,code,before is null?"null":AuditData(before).GetRawText(),AuditData(result).GetRawText(),result.Version,correlation,JsonSerializer.Serialize(new{reason})))await audit.ExecuteNonQueryAsync(ct);
  await using(var replay=Query(c,tx,"INSERT INTO idempotency VALUES($1,$2,$3,$4,$5::jsonb)",s.TenantId,s.Subject,key,fingerprint,JsonSerializer.Serialize(new InventoryReplay(permission,result),Json)))await replay.ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);return result;
 }
 public async Task<InventoryHistoryPage> InventoryHistory(Session s,Guid id,HistoryQuery q,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);
  if(await InventoryGet(c,null,s,id,ct) is null)throw NotFound();
  var sql="""
   SELECT a.id,a.occurred_at,a.version,a.action,a.before_data::text,a.after_data::text,a.actor
   FROM audit_events a JOIN drafts d ON d.tenant_id=a.tenant_id AND d.company_id=a.company_id AND d.id=a.entity_id AND d.id=a.vessel_id
   WHERE
   """+" "+InventoryAccess+" "+"""
   AND d.id=$5 AND ($6::bigint IS NULL OR (a.version,a.id)<($6,$7::uuid))
   ORDER BY a.version DESC,a.id DESC LIMIT $8
   """;
  await using var cmd=Query(c,null,sql,s.TenantId,s.CompanyId,s.Subject,s.VesselIds,id,q.BeforeVersion.HasValue?q.BeforeVersion.Value:DBNull.Value,q.BeforeId.HasValue?q.BeforeId.Value:DBNull.Value,q.Size+1);cmd.Parameters[5].NpgsqlDbType=NpgsqlDbType.Bigint;cmd.Parameters[6].NpgsqlDbType=NpgsqlDbType.Uuid;
  await using var r=await cmd.ExecuteReaderAsync(ct);List<InventoryHistoryItem> items=[];
  while(await r.ReadAsync(ct)) {
   JsonElement? before=r.IsDBNull(4)?null:JsonDocument.Parse(r.GetString(4)).RootElement.Clone();var after=JsonDocument.Parse(r.GetString(5)).RootElement.Clone();
   var action=r.GetString(3);if(!new[]{"DRAFT_CREATED","DRAFT_UPDATED","INVENTORY_RECORDED","INVENTORY_UPDATED","INVENTORY_REOPENED"}.Contains(action))action="HISTORICAL_METADATA_CHANGE";
   items.Add(new(r.GetGuid(0),r.GetFieldValue<DateTimeOffset>(1),r.GetInt64(2),action,InventoryContracts.Changes(before,after),r.GetString(6)==s.Subject?"SELF":"OTHER"));
  }
  var more=items.Count>q.Size;if(more)items.RemoveAt(items.Count-1);return new(items,more?q.After(items[^1]):null,more);
 }
}
