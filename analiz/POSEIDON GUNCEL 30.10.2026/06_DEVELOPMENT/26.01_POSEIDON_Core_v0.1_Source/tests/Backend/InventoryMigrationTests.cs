using System.Text.Json;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
[Collection("PostgreSQL schema")]
public class InventoryMigrationTests {
 [PostgresFact] public async Task Old_schema_rows_audit_idempotency_and_links_survive_additive_migration(){
  var schema="inventory_migration_"+Guid.NewGuid().ToString("N");await using var admin=new NpgsqlConnection(TestDatabase.Connection);await admin.OpenAsync();
  await using(var create=new NpgsqlCommand($"CREATE SCHEMA {schema}",admin))await create.ExecuteNonQueryAsync();
  try {
   var cs=new NpgsqlConnectionStringBuilder(TestDatabase.Connection){SearchPath=schema};await using var source=NpgsqlDataSource.Create(cs.ConnectionString);await using var c=await source.OpenConnectionAsync();
   using var stream=typeof(DraftStore).Assembly.GetManifestResourceStream("Poseidon.Backend.Migrations.001_drafts.sql")!;using var reader=new StreamReader(stream);await using(var old=new NpgsqlCommand(await reader.ReadToEndAsync(),c))await old.ExecuteNonQueryAsync();
   var scope=SyntheticIdentity.Fixtures["tenant-a"];var vessel=Guid.NewGuid();var cert=Guid.NewGuid();var audit=Guid.NewGuid();var key=Guid.NewGuid().ToString();
   await using(var seed=new NpgsqlCommand("""
    INSERT INTO companies VALUES(@tenant,@company);
    INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data) VALUES(@tenant,@vessel,@company,'vessels',@actor,7,'{"name":"Legacy","flag":"TR","vessel_type":"Tug"}');
    INSERT INTO drafts(tenant_id,id,company_id,kind,vessel_id,owner_subject,version,data) VALUES(@tenant,@cert,@company,'certificates',@vessel,@actor,2,jsonb_build_object('vessel_id',@vessel::text));
    INSERT INTO audit_events(id,tenant_id,company_id,entity_id,vessel_id,actor,action,after_data,version,correlation_id) VALUES(@audit,@tenant,@company,@vessel,@vessel,@actor,'DRAFT_UPDATED','{"name":"Legacy"}',7,'old-correlation');
    INSERT INTO idempotency VALUES(@tenant,@actor,@key,'original-fingerprint','{"original":"response"}');
    """,c)) {seed.Parameters.AddWithValue("tenant",scope.TenantId);seed.Parameters.AddWithValue("company",scope.CompanyId);seed.Parameters.AddWithValue("vessel",vessel);seed.Parameters.AddWithValue("cert",cert);seed.Parameters.AddWithValue("audit",audit);seed.Parameters.AddWithValue("actor",scope.Subject);seed.Parameters.AddWithValue("key",key);await seed.ExecuteNonQueryAsync();}
   var store=new DraftStore(source);await store.Migrate(default);await store.Migrate(default);
   var migrated=await store.InventoryGet(scope,vessel,default);Assert.Null(migrated.CreatedAt);Assert.NotNull(migrated.ModifiedAt);Assert.Equal(7,migrated.Version);Assert.Equal("DRAFT",migrated.AdminState);Assert.Contains("asset_mode",migrated.Completeness.MissingRequiredFields);Assert.Equal("Legacy",migrated.Data.GetProperty("name").GetString());
   Assert.Equal(cert,(await store.Get(scope,"certificates",cert,default))!.DraftId);Assert.Null(await store.Get(scope with{Subject="unauthorized"},"vessels",vessel,default));
   await using var check=new NpgsqlCommand("SELECT (SELECT actor FROM audit_events WHERE id=@audit),(SELECT fingerprint FROM idempotency WHERE key=@key),(SELECT count(*) FROM poseidon_migrations),(SELECT inventory_schema_version FROM drafts WHERE id=@id)",c);check.Parameters.AddWithValue("audit",audit);check.Parameters.AddWithValue("key",key);check.Parameters.AddWithValue("id",vessel);await using var r=await check.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal(scope.Subject,r.GetString(0));Assert.Equal("original-fingerprint",r.GetString(1));Assert.Equal(4,r.GetInt64(2));Assert.Equal(1,r.GetInt32(3));
  } finally {await using var drop=new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE",admin);await drop.ExecuteNonQueryAsync();}
 }
}
