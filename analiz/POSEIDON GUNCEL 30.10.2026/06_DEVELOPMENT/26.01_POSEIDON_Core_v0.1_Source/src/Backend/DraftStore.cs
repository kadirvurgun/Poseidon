using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
namespace Poseidon;
public sealed partial class DraftStore(NpgsqlDataSource source,TimeProvider? clock=null)
{
 static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
 public async Task Migrate(CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct); await using var tx=await c.BeginTransactionAsync(ct);
  await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(818264020)"))await l.ExecuteNonQueryAsync(ct);
  await using(var ledger=Query(c,tx,"CREATE TABLE IF NOT EXISTS poseidon_migrations(version text PRIMARY KEY)"))await ledger.ExecuteNonQueryAsync(ct);
  foreach(var file in new[]{"001_drafts.sql","002_inventory.sql","003_certificate_register.sql","004_evidence_intake.sql"}) {
   await using var check=Query(c,tx,"SELECT 1 FROM poseidon_migrations WHERE version=$1",file);
   if(await check.ExecuteScalarAsync(ct) is not null)continue;
   using var stream=typeof(DraftStore).Assembly.GetManifestResourceStream("Poseidon.Backend.Migrations."+file)!;using var reader=new StreamReader(stream);
   await using(var migration=Query(c,tx,await reader.ReadToEndAsync(ct)))await migration.ExecuteNonQueryAsync(ct);
   await using(var mark=Query(c,tx,"INSERT INTO poseidon_migrations VALUES($1)",file))await mark.ExecuteNonQueryAsync(ct);
  }
  await tx.CommitAsync(ct);
 }
 public async Task Seed(CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);
  foreach(var scope in SyntheticIdentity.Fixtures.Values) {
   await using var cmd=new NpgsqlCommand("INSERT INTO companies VALUES($1,$2) ON CONFLICT DO NOTHING",c);cmd.Parameters.AddWithValue(scope.TenantId);cmd.Parameters.AddWithValue(scope.CompanyId);await cmd.ExecuteNonQueryAsync(ct);
  }
 }
 static NpgsqlCommand Query(NpgsqlConnection c,NpgsqlTransaction? tx,string sql,params object[] values) {var cmd=new NpgsqlCommand(sql,c,tx);foreach(var value in values)cmd.Parameters.AddWithValue(value);return cmd;}
 static Draft Read(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetInt64(1),"DRAFT",false,JsonDocument.Parse(r.GetString(2)).RootElement.Clone());
 public async Task<Draft?> Get(Session scope,string kind,Guid id,CancellationToken ct) { await using var c=await source.OpenConnectionAsync(ct);return await Get(c,null,scope,kind,id,ct); }
 static async Task<Draft?> Get(NpgsqlConnection c,NpgsqlTransaction? tx,Session s,string kind,Guid id,CancellationToken ct,bool internalRead=false) {
  await using var cmd=Query(c,tx,"SELECT d.id,d.version,d.data::text,d.admin_state,d.inventory_schema_version FROM drafts d WHERE d.tenant_id=$1 AND d.company_id=$2 AND d.kind=$3 AND d.id=$4 AND ((d.kind='vessels' AND (d.owner_subject=$5 OR d.id=ANY($6))) OR (d.kind='certificates' AND EXISTS(SELECT 1 FROM drafts v WHERE v.tenant_id=d.tenant_id AND v.company_id=d.company_id AND v.id=d.vessel_id AND v.kind='vessels' AND (v.owner_subject=$5 OR v.id=ANY($6)))))",s.TenantId,s.CompanyId,kind,id,s.Subject,s.VesselIds);
  await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;
  if(!internalRead && r.GetString(3)!="DRAFT")throw kind=="vessels"?InventoryRequired():CertificateRequired();
  var result=Read(r);
  if(kind=="vessels" && !internalRead && r.GetInt32(4)==2)result=result with {Data=LegacyData(result.Data)};
  return result;
 }
 public async Task<List<Draft>> List(Session s,string kind,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);
  await using var cmd=Query(c,null,"SELECT d.id,d.version,d.data::text FROM drafts d WHERE d.tenant_id=$1 AND d.company_id=$2 AND d.kind=$3 AND d.admin_state='DRAFT' AND ((d.kind='vessels' AND (d.owner_subject=$4 OR d.id=ANY($5))) OR (d.kind='certificates' AND EXISTS(SELECT 1 FROM drafts v WHERE v.tenant_id=d.tenant_id AND v.company_id=d.company_id AND v.id=d.vessel_id AND v.kind='vessels' AND (v.owner_subject=$4 OR v.id=ANY($5))))) ORDER BY d.id LIMIT 500",s.TenantId,s.CompanyId,kind,s.Subject,s.VesselIds);
  await using var r=await cmd.ExecuteReaderAsync(ct);List<Draft> items=[];while(await r.ReadAsync(ct))items.Add(Read(r) with {Data=kind=="vessels"?LegacyData(Read(r).Data):Read(r).Data});return items;
 }
 public async Task<VesselPage> BrowseVessels(Session s,VesselBrowse query,CancellationToken ct) {
  await using var c=await source.OpenConnectionAsync(ct);
  const string sql = """
   SELECT d.id,d.version,d.data::text FROM drafts d
   WHERE d.tenant_id=$1 AND d.company_id=$2 AND d.kind='vessels' AND d.admin_state='DRAFT'
    AND (d.owner_subject=$3 OR d.id=ANY($4))
    AND ($5::uuid IS NULL OR d.id>$5)
    AND ($6='' OR translate(coalesce(d.data->>'name',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
     OR translate(coalesce(d.data->>'imo_no',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\'
     OR translate(coalesce(d.data->>'official_no',''),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz') COLLATE "C" LIKE $7 ESCAPE '\')
    AND ($8='' OR d.data->>'flag' COLLATE "C"=$8)
    AND ($9='' OR d.data->>'vessel_type' COLLATE "C"=$9)
   ORDER BY d.id LIMIT $10
   """;
  await using var cmd=Query(c,null,sql,s.TenantId,s.CompanyId,s.Subject,s.VesselIds,query.After.HasValue?query.After.Value:DBNull.Value,query.Q,VesselBrowse.Like(query.Q),query.Flag,query.VesselType,query.PageSize+1);
  cmd.Parameters[4].NpgsqlDbType=NpgsqlDbType.Uuid;
  await using var r=await cmd.ExecuteReaderAsync(ct);List<Draft> items=[];while(await r.ReadAsync(ct))items.Add(Read(r) with {Data=LegacyData(Read(r).Data)});
  var more=items.Count>query.PageSize;if(more)items.RemoveAt(items.Count-1);
  return new(items,more?query.CursorAfter(items[^1].DraftId):null,more);
 }
 public async Task<Draft> Save(Session s,string kind,Guid? id,long? expected,JsonElement data,string key,string correlation,CancellationToken ct) {
  if(data.TryGetProperty("company_id",out var company)&&Guid.Parse(company.GetString()!)!=s.CompanyId)throw new ApiFault(403,"company_scope","Company is outside authorized scope.");
  var canonical=JsonSerializer.Serialize(data.EnumerateObject().OrderBy(p=>p.Name).ToDictionary(p=>p.Name,p=>p.Value));
  var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{id}|{expected}|{canonical}")));
  await using var c=await source.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  // Transaction-level lock serializes identical keys before looking up replay state.
  await using(var l=Query(c,tx,"SELECT pg_advisory_xact_lock(hashtextextended($1,0))",$"{s.TenantId}|{s.Subject}|{key}"))await l.ExecuteNonQueryAsync(ct);
  if(id.HasValue) {if(kind=="vessels")await CheckLegacyWrite(c,tx,s,id.Value,ct);else await CheckLegacyCertificateWrite(c,tx,s,id.Value,ct);}
  await using(var q=Query(c,tx,"SELECT fingerprint,response::text FROM idempotency WHERE tenant_id=$1 AND subject=$2 AND key=$3",s.TenantId,s.Subject,key)) {
   await using var r=await q.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)) {if(r.GetString(0)!=fingerprint)throw new ApiFault(409,"idempotency_conflict","Key was used with a different request.");var replay=JsonSerializer.Deserialize<Draft>(r.GetString(1),Json)!;await r.DisposeAsync();if(await Get(c,tx,s,kind,replay.DraftId,ct) is null)throw new ApiFault(404,"not_found","Draft not found in current scope.");if(kind=="vessels")await CheckLegacyWrite(c,tx,s,replay.DraftId,ct);else await CheckLegacyCertificateWrite(c,tx,s,replay.DraftId,ct);return replay;}
  }
  await using(var companyQuery=Query(c,tx,"SELECT 1 FROM companies WHERE tenant_id=$1 AND id=$2",s.TenantId,s.CompanyId))if(await companyQuery.ExecuteScalarAsync(ct) is null)throw new ApiFault(403,"company_scope","Authorized company has not been provisioned.");
  Guid? vessel=null;
  if(kind=="certificates") {vessel=Guid.Parse(data.GetProperty("vessel_id").GetString()!);if(await Get(c,tx,s,"vessels",vessel.Value,ct,true) is null)throw new ApiFault(404,"vessel_not_found","Vessel not found in authorized scope.");}
  Draft? before=null;
  if(id.HasValue) {before=await Get(c,tx,s,kind,id.Value,ct);if(before is null)throw new ApiFault(404,"not_found","Draft not found.");if(before.Version!=expected)throw new ApiFault(412,"stale_version","Reload the draft before saving.");if(kind=="certificates"&&before.Data.GetProperty("vessel_id").GetString()!=data.GetProperty("vessel_id").GetString())throw new ApiFault(422,"immutable_vessel","Certificate vessel cannot change.");}
  var result=new Draft(id??Guid.NewGuid(),(before?.Version??0)+1,"DRAFT",false,data);
  if(before is null) {
   await using var cmd=Query(c,tx,"INSERT INTO drafts(tenant_id,id,company_id,kind,vessel_id,owner_subject,version,data) VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb)",s.TenantId,result.DraftId,s.CompanyId,kind,vessel.HasValue?vessel.Value:DBNull.Value,s.Subject,result.Version,data.GetRawText());cmd.Parameters[4].NpgsqlDbType=NpgsqlDbType.Uuid;await cmd.ExecuteNonQueryAsync(ct);
  } else {
   await using var cmd=Query(c,tx,"UPDATE drafts SET data=$1::jsonb,version=$2 WHERE tenant_id=$3 AND id=$4 AND version=$5",data.GetRawText(),result.Version,s.TenantId,result.DraftId,expected!.Value);if(await cmd.ExecuteNonQueryAsync(ct)!=1)throw new ApiFault(412,"stale_version","Concurrent update detected.");
  }
  await using(var a=Query(c,tx,"INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,before_data,after_data,version,correlation_id) VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb,$10,$11)",Guid.NewGuid(),s.TenantId,s.CompanyId,result.DraftId,vessel??result.DraftId,s.Subject,before is null?"DRAFT_CREATED":"DRAFT_UPDATED",before?.Data.GetRawText()??"null",data.GetRawText(),result.Version,correlation)) await a.ExecuteNonQueryAsync(ct);
  await using(var i=Query(c,tx,"INSERT INTO idempotency VALUES($1,$2,$3,$4,$5::jsonb)",s.TenantId,s.Subject,key,fingerprint,JsonSerializer.Serialize(result,Json)))await i.ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);return result;
 }
 static ApiFault InventoryRequired()=>new(409,"inventory_resource_required","Use the full inventory resource; legacy writes cannot replace extended or recorded metadata.");
 static JsonElement LegacyData(JsonElement data)=>JsonSerializer.SerializeToElement(data.EnumerateObject().Where(p=>new[]{"name","company_id","imo_no","official_no","flag","vessel_type","notes"}.Contains(p.Name)).ToDictionary(p=>p.Name,p=>p.Value));
 static async Task CheckLegacyWrite(NpgsqlConnection c,NpgsqlTransaction tx,Session s,Guid id,CancellationToken ct) {
  var row=await InventoryGet(c,tx,s,id,ct,true);if(row is null)throw new ApiFault(404,"not_found","Vessel not found.");
  await using var q=Query(c,tx,"SELECT inventory_schema_version FROM drafts WHERE tenant_id=$1 AND id=$2",s.TenantId,id);
  if(row.AdminState!="DRAFT" || (int)(await q.ExecuteScalarAsync(ct))! ==2)throw InventoryRequired();
 }

}
