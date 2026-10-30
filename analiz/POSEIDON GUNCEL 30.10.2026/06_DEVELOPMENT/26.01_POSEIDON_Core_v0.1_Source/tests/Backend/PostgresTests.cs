using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;
namespace Poseidon.Tests;
public sealed class PostgresFactAttribute:FactAttribute {public PostgresFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSEIDON_TEST_POSTGRES")))Skip="Real PostgreSQL required: set POSEIDON_TEST_POSTGRES to a dedicated disposable PostgreSQL 17+ database. No fallback provider.";}}
public sealed class AppFactory:ConfiguredAppFactory {
 protected override Dictionary<string,string?> Settings=>new(){["ConnectionStrings:Poseidon"]=TestDatabase.Connection,["SyntheticIdentity:Enabled"]="true",["Database:Initialize"]="true"};
}
// Serialize fixture DDL; the explicit concurrent-client transaction tests still run in parallel.
[Collection("PostgreSQL schema")]
public class PostgresTests {
 static async Task<HttpClient> Login(AppFactory app,string fixture) {var client=app.CreateClient();var r=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture});r.EnsureSuccessStatusCode();var data=await r.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.GetProperty("accessToken").GetString());return client;}
 static async Task<HttpResponseMessage> Save(HttpClient c,string route,object data,string? key=null,long? version=null){using var request=new HttpRequestMessage(HttpMethod.Post,route){Content=JsonContent.Create(new{saveMode="DRAFT",data})};request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");return await c.SendAsync(request);}
 [PostgresFact] public async Task Isolation_replay_conflict_concurrency_and_atomic_audit(){
  await using var app=new AppFactory();using var a=await Login(app,"tenant-a");using var b=await Login(app,"tenant-b");using var restricted=await Login(app,"tenant-a-restricted");
  using var otherCompany=await Login(app,"tenant-a-other-company");using var readOnly=await Login(app,"tenant-a-readonly");
  Assert.Equal(HttpStatusCode.Forbidden,(await Save(readOnly,"/api/v1/vessels",new{name="No write permission"})).StatusCode);
  var key=Guid.NewGuid().ToString();var payload=new{name="Synthetic integration vessel"};var create=await Save(a,"/api/v1/vessels",payload,key);Assert.Equal(HttpStatusCode.OK,create.StatusCode);var draft=await create.Content.ReadFromJsonAsync<JsonElement>();var id=draft.GetProperty("draftId").GetGuid();
  var replay=await Save(a,"/api/v1/vessels",payload,key);Assert.Equal(await create.Content.ReadAsStringAsync(),await replay.Content.ReadAsStringAsync());
  Assert.Equal(HttpStatusCode.Conflict,(await Save(a,"/api/v1/vessels",new{name="different"},key)).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await otherCompany.GetAsync($"/api/v1/vessels/{id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Save(otherCompany,"/api/v1/vessels",payload,key)).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await b.GetAsync($"/api/v1/vessels/{id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await restricted.GetAsync($"/api/v1/vessels/{id}")).StatusCode);
  Assert.Equal(HttpStatusCode.NotFound,(await Save(b,"/api/v1/certificates",new{vessel_id=id.ToString()})).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await Save(restricted,"/api/v1/certificates",new{vessel_id=id.ToString()})).StatusCode);
  var certificateResponse=await Save(a,"/api/v1/certificates",new{vessel_id=id.ToString(),certificate_no="SYNTHETIC-ONLY"});Assert.Equal(HttpStatusCode.OK,certificateResponse.StatusCode);var certificate=await certificateResponse.Content.ReadFromJsonAsync<JsonElement>();var certificateId=certificate.GetProperty("draftId").GetGuid();
  Assert.Equal(HttpStatusCode.NotFound,(await b.GetAsync($"/api/v1/certificates/{certificateId}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await restricted.GetAsync($"/api/v1/certificates/{certificateId}")).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await Save(a,$"/api/v1/certificates/{certificateId}",new{vessel_id=id.ToString(),certificate_no="REVISED-SYNTHETIC"},version:1)).StatusCode);
  Assert.Equal(HttpStatusCode.PreconditionFailed,(await Save(a,$"/api/v1/certificates/{certificateId}",new{vessel_id=id.ToString()},version:1)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await Save(a,$"/api/v1/vessels/{id}",new{name="Revised synthetic"},version:1)).StatusCode);
  Assert.Equal(HttpStatusCode.PreconditionFailed,(await Save(a,$"/api/v1/vessels/{id}",payload,version:1)).StatusCode);
  using var connection=new NpgsqlConnection(TestDatabase.Connection);await connection.OpenAsync();
  using var audit=new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE entity_id=@id",connection);audit.Parameters.AddWithValue("id",id);Assert.Equal(2L,await audit.ExecuteScalarAsync());
  using var mutate=new NpgsqlCommand("UPDATE audit_events SET action=action WHERE entity_id=@id",connection);mutate.Parameters.AddWithValue("id",id);await Assert.ThrowsAsync<PostgresException>(()=>mutate.ExecuteNonQueryAsync());
 }
 [PostgresFact] public async Task Audit_failure_rolls_back_draft_and_idempotency(){
  await using var app=new AppFactory();using var client=await Login(app,"tenant-a");var marker="audit-failure-"+Guid.NewGuid();var trigger="test_failure_"+Guid.NewGuid().ToString("N");
  await using var connection=new NpgsqlConnection(TestDatabase.Connection);await connection.OpenAsync();
  var ddl=$"CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.after_data->>'notes'='{marker}' THEN RAISE EXCEPTION 'synthetic audit failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER {trigger} BEFORE INSERT ON audit_events FOR EACH ROW EXECUTE FUNCTION {trigger}();";
  await using(var cmd=new NpgsqlCommand(ddl,connection))await cmd.ExecuteNonQueryAsync();
  var key=Guid.NewGuid().ToString();
  try {
   Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Save(client,"/api/v1/vessels",new{notes=marker},key)).StatusCode);
   await using var check=new NpgsqlCommand("SELECT (SELECT count(*) FROM drafts WHERE data->>'notes'=@marker)+(SELECT count(*) FROM idempotency WHERE key=@key)",connection);check.Parameters.AddWithValue("marker",marker);check.Parameters.AddWithValue("key",key);Assert.Equal(0L,await check.ExecuteScalarAsync());
  }finally{await using var drop=new NpgsqlCommand($"DROP TRIGGER {trigger} ON audit_events;DROP FUNCTION {trigger}();",connection);await drop.ExecuteNonQueryAsync();}
  Assert.Equal(HttpStatusCode.OK,(await Save(client,"/api/v1/vessels",new{notes=marker},key)).StatusCode);
 }
 [PostgresFact] public async Task Parallel_replay_and_competing_versions_are_serialized(){
  await using var app=new AppFactory();using var client=await Login(app,"tenant-a");var key=Guid.NewGuid().ToString();
  var responses=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Save(client,"/api/v1/vessels",new{name="Parallel synthetic"},key)));
  Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
  var drafts=await Task.WhenAll(responses.Select(async r=>await r.Content.ReadFromJsonAsync<JsonElement>()));var id=drafts[0].GetProperty("draftId").GetGuid();Assert.All(drafts,d=>Assert.Equal(id,d.GetProperty("draftId").GetGuid()));
  var updates=await Task.WhenAll(Save(client,$"/api/v1/vessels/{id}",new{name="Version two A"},version:1),Save(client,$"/api/v1/vessels/{id}",new{name="Version two B"},version:1));
  Assert.Single(updates,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(updates,r=>r.StatusCode==HttpStatusCode.PreconditionFailed);
  await using var connection=new NpgsqlConnection(TestDatabase.Connection);await connection.OpenAsync();await using var query=new NpgsqlCommand("SELECT count(*) FROM audit_events WHERE entity_id=@id",connection);query.Parameters.AddWithValue("id",id);Assert.Equal(2L,await query.ExecuteScalarAsync());
 }

}
