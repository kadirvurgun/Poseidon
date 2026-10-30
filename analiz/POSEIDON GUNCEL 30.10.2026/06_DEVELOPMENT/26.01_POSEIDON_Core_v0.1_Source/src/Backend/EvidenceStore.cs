using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
namespace Poseidon;
public sealed partial class DraftStore {
 static ApiFault EvidenceNotFound()=>new(404,"not_found","Evidence intake not found.");
 async Task<Guid> EvidenceAuthorize(ClaimsPrincipal user,string type,Guid id,CancellationToken ct,bool write,NpgsqlConnection? connection=null,NpgsqlTransaction? transaction=null) {
  if(connection is null){await using var owned=await source.OpenConnectionAsync(ct);return await EvidenceAuthorize(user,type,id,ct,write,owned);}
  var scope=Contracts.Scope(user);Guid vessel;
  if(type=="Vessel"){if(await InventoryGet(connection,transaction,scope,id,ct)is null)throw EvidenceNotFound();vessel=id;}else if(type=="Certificate")vessel=(await CertificateGet(connection,transaction,scope,id,CertificateToday,ct)??throw EvidenceNotFound()).ParentVessel.Id;else throw EvidenceContracts.Invalid("targetType");
  if(write&&(!user.HasClaim("permission","evidence:capture")||!user.HasClaim("permission",type=="Vessel"?"inventory:manage":"certificate:manage")))throw new ApiFault(403,"evidence_permission","Evidence capture and target management permission required.");
  return vessel;
 }
 public async Task<bool> EvidenceCapability(ClaimsPrincipal user,string type,Guid id,CancellationToken ct){await EvidenceAuthorize(user,type,id,ct,false);return user.HasClaim("permission","evidence:capture")&&user.HasClaim("permission",type=="Vessel"?"inventory:manage":"certificate:manage");}
 static EvidenceIntake ReadEvidence(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetInt64(1),r.GetString(2),r.GetGuid(3),JsonDocument.Parse(r.GetString(4)).RootElement.Clone(),r.GetString(5),"UNAVAILABLE",r.GetString(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetInt64(8),PreviousId:r.IsDBNull(9)?null:r.GetGuid(9),SupersededBy:r.IsDBNull(10)?null:r.GetGuid(10),CreatedAt:r.GetFieldValue<DateTimeOffset>(11),ExpiresAt:r.GetFieldValue<DateTimeOffset>(11).AddMinutes(15),DetectedMime:r.IsDBNull(12)?null:r.GetString(12),TypeResult:r.GetString(13));
 const string EvidenceColumns="id,version,target_type,target_id,metadata::text,state,integrity_state,sha256,actual_bytes,previous_id,(SELECT child.id FROM evidence_intakes child WHERE child.tenant_id=evidence_intakes.tenant_id AND child.previous_id=evidence_intakes.id AND child.state='STORED_QUARANTINED') AS superseded_by,created_at,detected_mime,type_result";
 async Task<EvidenceIntake> EvidenceRead(NpgsqlConnection c,NpgsqlTransaction? tx,Session s,Guid id,CancellationToken ct,bool locked=false) {
  // Filter by the current parent-object predicate before materializing any intake metadata.
  // Missing and inaccessible IDs share one resource error; unrelated DB/validation failures propagate.
  const string access="""
   AND EXISTS (SELECT 1 FROM drafts target JOIN drafts vessel
    ON vessel.tenant_id=target.tenant_id AND vessel.company_id=target.company_id AND vessel.kind='vessels'
    AND vessel.id=CASE WHEN target.kind='vessels' THEN target.id ELSE target.vessel_id END
    WHERE target.tenant_id=evidence_intakes.tenant_id AND target.company_id=evidence_intakes.company_id
    AND target.id=evidence_intakes.target_id AND vessel.id=evidence_intakes.vessel_id
    AND ((evidence_intakes.target_type='Vessel' AND target.kind='vessels') OR (evidence_intakes.target_type='Certificate' AND target.kind='certificates'))
    AND (vessel.owner_subject=$4 OR vessel.id=ANY($5)))
   """;
  await using var q=Query(c,tx,"SELECT "+EvidenceColumns+" FROM evidence_intakes WHERE tenant_id=$1 AND company_id=$2 AND id=$3 "+access+(locked?" FOR UPDATE":""),s.TenantId,s.CompanyId,id,s.Subject,s.VesselIds);
  await using var r=await q.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw EvidenceNotFound();return ReadEvidence(r);
 }
 public async Task<EvidenceIntake> EvidenceGet(ClaimsPrincipal user,Guid id,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);var item=await EvidenceRead(c,null,Contracts.Scope(user),id,ct);await EvidenceAuthorize(user,item.TargetType,item.TargetId,ct,false,c);return item;
 }
 public async Task<EvidenceIntake> EvidenceReserve(ClaimsPrincipal user,JsonElement body,string key,string correlation,CancellationToken ct,Guid? previousId=null,long? previousVersion=null,string? reason=null) {
  body=EvidenceContracts.Validate(body);var type=body.GetProperty("targetType").GetString()!;var target=body.GetProperty("targetId").GetGuid();var vessel=await EvidenceAuthorize(user,type,target,ct,true);var s=Contracts.Scope(user);
  var canonical=JsonSerializer.Serialize(body.EnumerateObject().OrderBy(p=>p.Name).ToDictionary(p=>p.Name,p=>p.Value));var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("evidence-reserve:v1|"+previousId+"|"+previousVersion+"|"+reason+"|"+canonical)));
  await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))","evidence-quota|"+s.TenantId))await l.ExecuteNonQueryAsync(ct);
  await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))",$"{s.TenantId}|{s.Subject}|{key}"))await l.ExecuteNonQueryAsync(ct);
  await using(var q=Query(c,tx,"SELECT fingerprint,response::text FROM idempotency WHERE tenant_id=$1 AND subject=$2 AND key=$3",s.TenantId,s.Subject,key)) {
   await using var r=await q.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){if(r.GetString(0)!=fingerprint)throw new ApiFault(409,"idempotency_conflict","Different reservation used this key.");var id=JsonDocument.Parse(r.GetString(1)).RootElement.GetProperty("id").GetGuid();await r.DisposeAsync();var replay=await EvidenceRead(c,tx,s,id,ct);await EvidenceAuthorize(user,replay.TargetType,replay.TargetId,ct,true,c,tx);return replay;}
  }
  if(previousId.HasValue) {var previous=await EvidenceRead(c,tx,s,previousId.Value,ct,true);await EvidenceAuthorize(user,previous.TargetType,previous.TargetId,ct,true,c,tx);if(previous.TargetType!=type||previous.TargetId!=target)throw EvidenceContracts.Invalid("targetId");if(previous.Version!=previousVersion)throw new ApiFault(412,"stale_version","Reload previous intake.");if(previous.State!="STORED_QUARANTINED"||previous.SupersededBy.HasValue)throw new ApiFault(409,"invalid_transition","Only current stored intake can be superseded.");await using var pending=Query(c,tx,"SELECT 1 FROM evidence_intakes WHERE tenant_id=$1 AND previous_id=$2 AND state IN ('RESERVED','RECEIVING','STORED_QUARANTINED')",s.TenantId,previousId.Value);if(await pending.ExecuteScalarAsync(ct)is not null)throw new ApiFault(409,"supersession_pending","A replacement reservation already exists; recover it instead.");}
  var bytes=body.GetProperty("expectedBytes").GetInt64();
  await using(var q=Query(c,tx,"SELECT COALESCE(sum(expected_bytes),0)::bigint,count(*) FILTER(WHERE actor=$2 AND state IN ('RESERVED','RECEIVING')) FROM evidence_intakes WHERE tenant_id=$1",s.TenantId,s.Subject)) {await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(r.GetInt64(0)+bytes>104857600||r.GetInt64(1)>=10)throw new ApiFault(409,"evidence_quota","Quarantine reservation capacity reached; no automatic capacity release.");}
  var result=new EvidenceIntake(Guid.NewGuid(),1,type,target,body,"RESERVED","UNAVAILABLE","NOT_CHECKED",null,null,PreviousId:previousId);
  await using(var q=Query(c,tx,"INSERT INTO evidence_intakes(tenant_id,id,company_id,vessel_id,target_type,target_id,actor,metadata,expected_bytes,previous_id,restricted_reason) VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10,$11)",s.TenantId,result.Id,s.CompanyId,vessel,type,target,s.Subject,body.GetRawText(),bytes,previousId.HasValue?previousId.Value:DBNull.Value,reason is not null?reason:DBNull.Value)){q.Parameters[9].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid;q.Parameters[10].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text;await q.ExecuteNonQueryAsync(ct);}
  result=await EvidenceRead(c,tx,s,result.Id,ct);
  await EvidenceAudit(c,tx,s,result,vessel,"EVIDENCE_RESERVED",correlation,ct,reason);
  await using(var q=Query(c,tx,"INSERT INTO idempotency VALUES($1,$2,$3,$4,$5::jsonb)",s.TenantId,s.Subject,key,fingerprint,JsonSerializer.Serialize(new{id=result.Id},Json)))await q.ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);return result;
 }
 static async Task EvidenceAudit(NpgsqlConnection c,NpgsqlTransaction tx,Session s,EvidenceIntake item,Guid vessel,string action,string correlation,CancellationToken ct,string? reason=null) {
  await using var q=Query(c,tx,"INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,before_data,after_data,version,correlation_id,restricted_metadata) VALUES($1,$2,$3,$4,$5,$6,$7,'null'::jsonb,$8::jsonb,$9,$10,$11::jsonb)",Guid.NewGuid(),s.TenantId,s.CompanyId,item.Id,vessel,s.Subject,action,JsonSerializer.Serialize(new{state=item.State},Json),item.Version,correlation,JsonSerializer.Serialize(new{reason,requiredCapabilities=new[]{"evidence:capture",item.TargetType=="Vessel"?"inventory:manage":"certificate:manage"}},Json));await q.ExecuteNonQueryAsync(ct);
 }
 public async Task<EvidenceIntake> EvidenceUpload(ClaimsPrincipal user,Guid id,long version,string key,string mime,Stream input,EvidenceObjectStore objects,IEvidenceFaults faults,string correlation,CancellationToken ct) {
  var s=Contracts.Scope(user);await using var c=await source.OpenConnectionAsync(ct);EvidenceIntake item;Guid vessel;
  // Commit intent before touching bytes. Unconfirmed attempts remain retained and non-retransmittable.
  await using(var tx=await c.BeginTransactionAsync(ct)) {
   await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))",$"{s.TenantId}|{s.Subject}|{key}"))await l.ExecuteNonQueryAsync(ct);
   item=await EvidenceRead(c,tx,s,id,ct,true);vessel=await EvidenceAuthorize(user,item.TargetType,item.TargetId,ct,true,c,tx);
   await using(var expiry=Query(c,tx,"UPDATE evidence_intakes SET state='EXPIRED',version=version+1 WHERE tenant_id=$1 AND id=$2 AND state='RESERVED' AND created_at + interval '15 minutes' <= now()",s.TenantId,id))if(await expiry.ExecuteNonQueryAsync(ct)==1){await EvidenceAudit(c,tx,s,item with{State="EXPIRED",Version=item.Version+1},vessel,"EVIDENCE_EXPIRED",correlation,ct);await tx.CommitAsync(ct);throw new ApiFault(409,"reservation_expired","Reservation expired; capacity remains charged.");}
   if(item.State!="RESERVED")throw new ApiFault(409,"content_not_retryable","Content cannot be retransmitted; recover through GET.");
   if(item.Version!=version)throw new ApiFault(412,"stale_version","Reload intake metadata.");
   if(mime!=item.Metadata.GetProperty("declaredMime").GetString())throw new ApiFault(422,"media_type_mismatch","Content type differs from reservation.");
   await using(var q=Query(c,tx,"SELECT 1 FROM idempotency WHERE tenant_id=$1 AND subject=$2 AND key=$3",s.TenantId,s.Subject,key))if(await q.ExecuteScalarAsync(ct)is not null)throw new ApiFault(409,"idempotency_conflict","Content key already used; recover through GET.");
   await using(var q=Query(c,tx,"INSERT INTO idempotency VALUES($1,$2,$3,$4,$5::jsonb)",s.TenantId,s.Subject,key,"evidence-content:v1|"+id+"|"+version,JsonSerializer.Serialize(new{id},Json)))await q.ExecuteNonQueryAsync(ct);
   item=item with{State="RECEIVING",Version=item.Version+1};
   await using(var q=Query(c,tx,"UPDATE evidence_intakes SET state=$1,version=$2 WHERE tenant_id=$3 AND id=$4",item.State,item.Version,s.TenantId,id))await q.ExecuteNonQueryAsync(ct);
   await EvidenceAudit(c,tx,s,item,vessel,"EVIDENCE_UPLOAD_STARTED",correlation,ct);await tx.CommitAsync(ct);
  }
  faults.Checkpoint("before_file");
  var expected=item.Metadata.GetProperty("expectedBytes").GetInt64();using var memory=new MemoryStream();
  using(var file=objects.Create(id)) {
   var buffer=new byte[65536];int read;while((read=await input.ReadAsync(buffer,ct))>0) {if(memory.Length+read>expected)throw new ApiFault(422,"length_mismatch","Upload exceeds declared length; reservation retained.");await file.WriteAsync(buffer.AsMemory(0,read),ct);await memory.WriteAsync(buffer.AsMemory(0,read),ct);}
   file.Flush(true);
  }
  objects.FlushDirectory();faults.Checkpoint("after_file");var content=memory.ToArray();var hash=Convert.ToHexString(SHA256.HashData(content));var match=!item.Metadata.TryGetProperty("expectedSha256",out var declared)||string.Equals(hash,declared.GetString(),StringComparison.OrdinalIgnoreCase);
  var signature=EvidenceContracts.Signature(content,mime);
  var valid=content.LongLength==expected&&signature&&match;
  item=item with{Version=item.Version+1,State=valid?"STORED_QUARANTINED":"REJECTED",ActualBytes=content.LongLength,Sha256=hash,DetectedMime=signature?mime:null,TypeResult=signature?"MATCHED":"REJECTED",IntegrityStatus=!match?"MISMATCH":item.Metadata.TryGetProperty("expectedSha256",out _)?"MATCHED":"COMPUTED"};
  await using(var tx=await c.BeginTransactionAsync(ct)) {
   await EvidenceAuthorize(user,item.TargetType,item.TargetId,ct,true,c,tx);
   await using(var q=Query(c,tx,"UPDATE evidence_intakes SET state=$1,version=$2,actual_bytes=$3,sha256=$4,integrity_state=$5,object_id=CASE WHEN $1='STORED_QUARANTINED' THEN id ELSE NULL END,detected_mime=$8,type_result=$9 WHERE tenant_id=$6 AND id=$7 AND state='RECEIVING'",item.State,item.Version,content.LongLength,hash,item.IntegrityStatus,s.TenantId,id,item.DetectedMime is null?DBNull.Value:item.DetectedMime,item.TypeResult)){q.Parameters[7].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Text;if(await q.ExecuteNonQueryAsync(ct)!=1)throw new ApiFault(409,"upload_state_changed","Recover metadata.");}
   string? correctionReason=null;await using(var reasonQuery=Query(c,tx,"SELECT restricted_reason FROM evidence_intakes WHERE tenant_id=$1 AND id=$2",s.TenantId,id)){correctionReason=await reasonQuery.ExecuteScalarAsync(ct) as string;}
   await EvidenceAudit(c,tx,s,item,vessel,valid?"EVIDENCE_STORED":"EVIDENCE_REJECTED",correlation,ct,correctionReason);
   if(valid&&item.PreviousId.HasValue){var previous=await EvidenceRead(c,tx,s,item.PreviousId.Value,ct,true);await EvidenceAuthorize(user,previous.TargetType,previous.TargetId,ct,true,c,tx);await EvidenceAudit(c,tx,s,previous,vessel,"EVIDENCE_SUPERSEDED",correlation,ct,correctionReason);}faults.Checkpoint("before_commit");await tx.CommitAsync(ct);
  }
  faults.Checkpoint("after_commit");if(!valid)throw new ApiFault(422,"content_rejected","Content rejected and retained in quarantine; recover metadata through GET.");return item;
 }
}

public sealed partial class DraftStore {
 public async Task<EvidencePage> EvidenceList(ClaimsPrincipal user,EvidenceBrowse query,CancellationToken ct) {
  var s=Contracts.Scope(user);await EvidenceAuthorize(user,query.TargetType,query.TargetId,ct,false);
  await using var c=await source.OpenConnectionAsync(ct);await using var q=Query(c,null,"SELECT "+EvidenceColumns+" FROM evidence_intakes WHERE tenant_id=$1 AND company_id=$2 AND target_type=$3 AND target_id=$4 AND ($5::uuid IS NULL OR id>$5) ORDER BY id LIMIT $6",s.TenantId,s.CompanyId,query.TargetType,query.TargetId,query.After.HasValue?query.After.Value:DBNull.Value,query.PageSize+1);q.Parameters[4].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid;
  await using var r=await q.ExecuteReaderAsync(ct);List<EvidenceIntake> items=[];while(await r.ReadAsync(ct))items.Add(ReadEvidence(r));var more=items.Count>query.PageSize;if(more)items.RemoveAt(items.Count-1);return new(items,more?query.Cursor(items[^1].Id):null,more);
 }
 public async Task<InventoryHistoryPage> EvidenceHistory(ClaimsPrincipal user,Guid id,IQueryCollection query,CancellationToken ct) {
  var item=await EvidenceGet(user,id,ct);var s=Contracts.Scope(user);var page=HistoryQuery.Parse(query,s with{Subject=s.Subject+"|evidence-history:v1"},id);
  var vessel=await EvidenceAuthorize(user,item.TargetType,item.TargetId,ct,false);
  await using var c=await source.OpenConnectionAsync(ct);
  await using var q=Query(c,null,"SELECT id,occurred_at,version,action,actor FROM audit_events WHERE tenant_id=$1 AND company_id=$2 AND entity_id=$3 AND vessel_id=$4 AND ($5::bigint IS NULL OR version<$5 OR (version=$5 AND id<$6)) ORDER BY version DESC,id DESC LIMIT $7",s.TenantId,s.CompanyId,id,vessel,page.BeforeVersion.HasValue?page.BeforeVersion.Value:DBNull.Value,page.BeforeId.HasValue?page.BeforeId.Value:DBNull.Value,page.Size+1);q.Parameters[4].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Bigint;q.Parameters[5].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid;
  await using var r=await q.ExecuteReaderAsync(ct);List<InventoryHistoryItem> rows=[];while(await r.ReadAsync(ct))rows.Add(new(r.GetGuid(0),r.GetFieldValue<DateTimeOffset>(1),r.GetInt64(2),r.GetString(3) is "EVIDENCE_RESERVED" or "EVIDENCE_UPLOAD_STARTED" or "EVIDENCE_STORED" or "EVIDENCE_REJECTED" or "EVIDENCE_EXPIRED" or "EVIDENCE_SUPERSEDED"?r.GetString(3):"HISTORICAL_METADATA_CHANGE",r.GetString(3)=="EVIDENCE_SUPERSEDED"?["supersededBy"]:["state"],r.GetString(4)==s.Subject?"SELF":"OTHER"));
  var more=rows.Count>page.Size;if(more)rows.RemoveAt(rows.Count-1);return new(rows,more?page.After(rows[^1]):null,more);
 }
}

public sealed partial class DraftStore {
 public async Task EvidenceReconcile(EvidenceObjectStore objects,CancellationToken ct) {
  var files=objects.Inspect();await using var c=await source.OpenConnectionAsync(ct);
  await using var q=Query(c,null,"SELECT id,state,expected_bytes,actual_bytes,sha256 FROM evidence_intakes");await using var r=await q.ExecuteReaderAsync(ct);
  while(await r.ReadAsync(ct)) {
   var id=r.GetGuid(0);var state=r.GetString(1);if(files.Remove(id,out var file)) {
    if(state is "RESERVED" or "EXPIRED"||file.Length>r.GetInt64(2))throw new InvalidOperationException("Quarantine accounting reconciliation failed.");
    if(state=="STORED_QUARANTINED"&&(r.IsDBNull(3)||r.IsDBNull(4)||file.Length!=r.GetInt64(3)||file.Hash!=r.GetString(4)))throw new InvalidOperationException("Stored quarantine integrity reconciliation failed.");
   }else if(state=="STORED_QUARANTINED")throw new InvalidOperationException("Stored quarantine object unavailable.");
  }
  if(files.Count!=0)throw new InvalidOperationException("Evidence root contains unaccounted retained objects.");
 }
}
