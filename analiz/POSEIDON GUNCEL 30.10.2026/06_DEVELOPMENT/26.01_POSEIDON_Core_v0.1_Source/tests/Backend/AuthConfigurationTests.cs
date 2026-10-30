using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Poseidon.Tests;
public class AuthConfigurationTests {
 sealed class OidcApp:ConfiguredAppFactory {
  protected override string EnvironmentName=>"Production";
  protected override Dictionary<string,string?> Settings=>new(){["SyntheticIdentity:Enabled"]="false",["Database:Initialize"]="false",["ConnectionStrings:Poseidon"]="Host=unused;Database=unused;Username=unused",["Authentication:Authority"]="https://unconfigured.invalid",["Authentication:Audience"]="poseidon-test"};
 }
 [Fact] public async Task Production_uses_bearer_and_anonymous_requests_fail_without_contacting_Idp(){
  await using var app=new OidcApp();using var client=app.CreateClient();var schemes=app.Services.GetRequiredService<IAuthenticationSchemeProvider>();Assert.Equal("Bearer",(await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/vessels")).StatusCode);
  Assert.DoesNotContain(await schemes.GetAllSchemesAsync(),s=>s.Name=="Synthetic");
 }
}
