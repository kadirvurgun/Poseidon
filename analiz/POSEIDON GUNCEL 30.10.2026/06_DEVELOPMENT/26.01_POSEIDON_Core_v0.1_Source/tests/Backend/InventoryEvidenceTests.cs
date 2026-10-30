using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
[Collection("PostgreSQL schema")]
public class InventoryEvidenceTests {
 [PostgresFact] public async Task New_inventory_pages_cover_more_than_500_with_all_filter_bindings_and_no_duplicates(){
  await using var source=NpgsqlDataSource.Create(TestDatabase.Connection);var store=new DraftStore(source);await store.Migrate(default);
  var scope=new Session("synthetic-page-test",Guid.NewGuid(),Guid.NewGuid(),[],true);await using var c=await source.OpenConnectionAsync();
  await using(var seed=new NpgsqlCommand("""
   INSERT INTO companies VALUES(@tenant,@company);
   INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data,admin_state) SELECT @tenant,gen_random_uuid(),@company,'vessels',@actor,1,jsonb_build_object('name','Bulk literal %_\ '||i,'flag','TR','vessel_type','tug','asset_mode','TUG'),'RECORDED' FROM generate_series(1,601) i;
   INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data) VALUES(@tenant,gen_random_uuid(),@company,'vessels','hidden',1,'{"name":"Bulk hidden"}');
   """,c)){seed.Parameters.AddWithValue("tenant",scope.TenantId);seed.Parameters.AddWithValue("company",scope.CompanyId);seed.Parameters.AddWithValue("actor",scope.Subject);await seed.ExecuteNonQueryAsync();}
  var values=new Dictionary<string,StringValues>{{"q","literal %_\\"},{"adminState","RECORDED"},{"asset_mode","TUG"},{"flag","TR"},{"vessel_type","tug"},{"pageSize","100"}};List<Guid> ids=[];string? firstCursor=null;
  do{var page=await store.InventoryList(scope,InventoryBrowse.Parse(new QueryCollection(values),scope),default);ids.AddRange(page.Items.Select(v=>v.Id));if(page.NextCursor is null)break;firstCursor??=page.NextCursor;values["cursor"]=page.NextCursor;}while(true);
  Assert.Equal(601,ids.Count);Assert.Equal(601,ids.Distinct().Count());values["cursor"]=firstCursor;values["asset_mode"]="SHIP";Assert.Throws<ApiFault>(()=>InventoryBrowse.Parse(new QueryCollection(values),scope));values.Remove("cursor");var ship=await store.InventoryList(scope,InventoryBrowse.Parse(new QueryCollection(values),scope),default);Assert.Empty(ship.Items);
  var hidden=await store.InventoryList(scope with{Subject="other-no-grant"},InventoryBrowse.Parse(new QueryCollection(),scope with{Subject="other-no-grant"}),default);Assert.Empty(hidden.Items);
 }
 [PostgresFact] public async Task Unknown_historical_fields_and_actions_are_redacted_and_actor_relation_is_exact(){
  await using var source=NpgsqlDataSource.Create(TestDatabase.Connection);var store=new DraftStore(source);await store.Migrate(default);await store.Seed(default);
  var identity=new ExternalIdentity("https://first.invalid","same-sub");var scope=SyntheticIdentity.Fixtures["tenant-a"] with{Subject=IdentityBoundary.ActorKey(identity),Synthetic=false};
  var draft=await store.Save(scope,"vessels",null,null,JsonSerializer.SerializeToElement(new{name="Historical"}),Guid.NewGuid().ToString(),"test",default);
  await using var c=await source.OpenConnectionAsync();await using(var insert=new NpgsqlCommand("INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,after_data,version,correlation_id,restricted_metadata) VALUES(@event,@tenant,@company,@id,@id,@actor,'UNKNOWN PRIVATE ACTION','{\"unknown_secret_key\":\"SECRET VALUE\",\"notes\":\"SECRET NOTES\",\"master_name\":\"SECRET CONTACT\"}',2,'SECRET CORRELATION','{\"reason\":\"SECRET REASON\"}')",c)){insert.Parameters.AddWithValue("event",Guid.NewGuid());insert.Parameters.AddWithValue("tenant",scope.TenantId);insert.Parameters.AddWithValue("company",scope.CompanyId);insert.Parameters.AddWithValue("id",draft.DraftId);insert.Parameters.AddWithValue("actor",scope.Subject);await insert.ExecuteNonQueryAsync();}
  var other=scope with{Subject=IdentityBoundary.ActorKey(new("https://second.invalid","same-sub")),VesselIds=[draft.DraftId]};
  var page=await store.InventoryHistory(other,draft.DraftId,HistoryQuery.Parse(new QueryCollection(),other,draft.DraftId),default);Assert.All(page.Items,x=>Assert.Equal("OTHER",x.ActorRelation));Assert.Equal("HISTORICAL_METADATA_CHANGE",page.Items[0].Action);var json=JsonSerializer.Serialize(page);foreach(var value in new[]{"SECRET","unknown_secret_key","PRIVATE ACTION","oidc:","https://","same-sub","reason"})Assert.DoesNotContain(value,json);
 }
 [PostgresFact] public async Task Inventory_audit_failure_rolls_back_state_version_metadata_and_idempotency(){
  await using var source=NpgsqlDataSource.Create(TestDatabase.Connection);var store=new DraftStore(source);await store.Migrate(default);await store.Seed(default);var scope=SyntheticIdentity.Fixtures["inventory-manager"];
  var user=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("sub",scope.Subject),new Claim("synthetic","true"),new Claim("tenant_id",scope.TenantId.ToString()),new Claim("company_id",scope.CompanyId.ToString()),new Claim("permission","draft:write"),new Claim("permission","inventory:manage")},"Synthetic"));
  var created=await store.InventoryWrite(user,null,"create",null,JsonSerializer.SerializeToElement(new{data=new{name="Atomic",flag="TR",vessel_type="Tug",asset_mode="TUG"}}),Guid.NewGuid().ToString(),"test",default);
  var trigger="inventory_fail_"+Guid.NewGuid().ToString("N");var key=Guid.NewGuid().ToString();await using var c=await source.OpenConnectionAsync();
  await using(var ddl=new NpgsqlCommand($"CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.entity_id='{created.Id}' AND NEW.action='INVENTORY_RECORDED' THEN RAISE EXCEPTION 'synthetic failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER {trigger} BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION {trigger}();",c))await ddl.ExecuteNonQueryAsync();
  try {await Assert.ThrowsAsync<PostgresException>(()=>store.InventoryWrite(user,created.Id,"record",1,JsonSerializer.SerializeToElement(new{}),key,"test",default));var current=await store.InventoryGet(scope,created.Id,default);Assert.Equal(1,current.Version);Assert.Equal("DRAFT",current.AdminState);await using var q=new NpgsqlCommand("SELECT (SELECT count(*) FROM audit_events WHERE entity_id=@id)+(SELECT count(*) FROM idempotency WHERE key=@key)",c);q.Parameters.AddWithValue("id",created.Id);q.Parameters.AddWithValue("key",key);Assert.Equal(1L,await q.ExecuteScalarAsync());}
  finally{await using var drop=new NpgsqlCommand($"DROP TRIGGER {trigger} ON audit_events;DROP FUNCTION {trigger}();",c);await drop.ExecuteNonQueryAsync();}
  Assert.Equal("RECORDED",(await store.InventoryWrite(user,created.Id,"record",1,JsonSerializer.SerializeToElement(new{}),key,"test",default)).AdminState);
 }
}
