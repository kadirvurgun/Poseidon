using Npgsql;
using Xunit;
namespace Poseidon.Tests;
[Collection("PostgreSQL schema")]
public class CertificateMigrationTests {
 [PostgresFact] public async Task Migration003_preserves_inventory_and_legacy_certificate_identity_audit_replay_and_scope(){
  var schema="certificate_migration_"+Guid.NewGuid().ToString("N");await using var admin=new NpgsqlConnection(TestDatabase.Connection);await admin.OpenAsync();await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
  try {
   await using var source=NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(TestDatabase.Connection){SearchPath=schema}.ConnectionString);await using var c=await source.OpenConnectionAsync();
   foreach(var name in new[]{"001_drafts.sql","002_inventory.sql"}){using var stream=typeof(DraftStore).Assembly.GetManifestResourceStream("Poseidon.Backend.Migrations."+name)!;using var reader=new StreamReader(stream);await using var apply=new NpgsqlCommand(await reader.ReadToEndAsync(),c);await apply.ExecuteNonQueryAsync();}
   var scope=SyntheticIdentity.Fixtures["certificate-manager"];var vessel=Guid.NewGuid();var cert=Guid.NewGuid();var eventId=Guid.NewGuid();var key=Guid.NewGuid().ToString();
   await using(var seed=new NpgsqlCommand("""
    CREATE TABLE poseidon_migrations(version text PRIMARY KEY);INSERT INTO poseidon_migrations VALUES('001_drafts.sql'),('002_inventory.sql');
    INSERT INTO companies VALUES(@tenant,@company);
    INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data,admin_state,inventory_schema_version) VALUES(@tenant,@vessel,@company,'vessels',@actor,5,'{"name":"Old recorded tug","asset_mode":"TUG","flag":"TR","vessel_type":"Tug"}','RECORDED',2);
    INSERT INTO drafts(tenant_id,id,company_id,kind,vessel_id,owner_subject,version,data) VALUES(@tenant,@cert,@company,'certificates',@vessel,'other-certificate-creator',7,jsonb_build_object('vessel_id',@vessel::text,'type','Old certificate','issuer','Declared','expiry','2030-12-31'));
    INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,after_data,version,correlation_id) VALUES(@event,@tenant,@company,@cert,@vessel,'historical-actor','DRAFT_UPDATED','{"type":"Old certificate"}',7,'historical-correlation');
    INSERT INTO idempotency VALUES(@tenant,@actor,@key,'original-fingerprint','{"original":"response"}');
    """,c)){seed.Parameters.AddWithValue("tenant",scope.TenantId);seed.Parameters.AddWithValue("company",scope.CompanyId);seed.Parameters.AddWithValue("vessel",vessel);seed.Parameters.AddWithValue("cert",cert);seed.Parameters.AddWithValue("event",eventId);seed.Parameters.AddWithValue("actor",scope.Subject);seed.Parameters.AddWithValue("key",key);await seed.ExecuteNonQueryAsync();}
   var store=new DraftStore(source);await store.Migrate(default);await store.Migrate(default);
   var parent=await store.InventoryGet(scope,vessel,default);Assert.Equal("RECORDED",parent.AdminState);Assert.Equal(5,parent.Version);
   var record=await store.CertificateGet(scope,cert,default);Assert.Equal(cert,record.Id);Assert.Equal(vessel,record.ParentVessel.Id);Assert.Equal(7,record.Version);Assert.Equal("DRAFT",record.AdminState);Assert.Equal("2030-12-31",record.Data.GetProperty("expiry").GetString());
   Assert.Equal(cert,(await store.Get(scope,"certificates",cert,default))!.DraftId);Assert.Null(await store.Get(scope with{Subject="same-company-hidden"},"certificates",cert,default));
   await using var check=new NpgsqlCommand("SELECT (SELECT certificate_schema_version FROM drafts WHERE id=@cert),(SELECT actor FROM audit_events WHERE id=@event),(SELECT fingerprint FROM idempotency WHERE key=@key),(SELECT count(*) FROM poseidon_migrations)",c);check.Parameters.AddWithValue("cert",cert);check.Parameters.AddWithValue("event",eventId);check.Parameters.AddWithValue("key",key);await using var r=await check.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal(1,r.GetInt32(0));Assert.Equal("historical-actor",r.GetString(1));Assert.Equal("original-fingerprint",r.GetString(2));Assert.Equal(4,r.GetInt64(3));
  }finally{await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();}
 }
}
