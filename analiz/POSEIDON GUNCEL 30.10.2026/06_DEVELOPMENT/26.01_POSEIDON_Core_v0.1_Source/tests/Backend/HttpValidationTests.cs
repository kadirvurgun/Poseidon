using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Poseidon.Tests;
public class HttpValidationTests {
 sealed class ValidationApp:ConfiguredAppFactory { }
 [Fact] public async Task Malformed_http_json_returns_sanitized_client_error_without_database(){
  await using var app=new ValidationApp();using var client=app.CreateClient();var login=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture="tenant-a"});var session=await login.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
  using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/vessels"){Content=new StringContent("{broken",Encoding.UTF8,"application/json")};request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());var result=await client.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,result.StatusCode);var text=await result.Content.ReadAsStringAsync();Assert.DoesNotContain("System.Text.Json",text);Assert.DoesNotContain("StackTrace",text);
 }
 [Fact] public async Task Default_policy_rejects_anonymous_and_forged_headers_without_database(){await using var app=new ValidationApp();using var client=app.CreateClient();client.DefaultRequestHeaders.Add("X-Tenant-Id","aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/vessels")).StatusCode);}
 [Fact] public async Task Missing_write_permission_is_rejected_before_database(){
  await using var app=new ValidationApp();using var client=app.CreateClient();var login=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture="tenant-a-readonly"});var session=await login.Content.ReadFromJsonAsync<JsonElement>();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
  Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/v1/vessels",new{saveMode="DRAFT",data=new{name="Synthetic readonly"}})).StatusCode);
 }

}
