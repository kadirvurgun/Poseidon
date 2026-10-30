using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Poseidon.Tests;
// Ephemeral local signing material exercises actual JwtBearer validation without a live IdP.
// This does not establish deployment identity-provider/claim-governance readiness.
public class JwtValidationTests {
 sealed class JwtApp(RsaSecurityKey key):ConfiguredAppFactory {
  protected override string EnvironmentName=>"Production";
  protected override Dictionary<string,string?> Settings=>new(){["SyntheticIdentity:Enabled"]="false",["Database:Initialize"]="false",["ConnectionStrings:Poseidon"]="Host=unused;Database=unused;Username=unused",["Authentication:Authority"]="https://synthetic-issuer.invalid",["Authentication:Audience"]="poseidon-test"};
  protected override void ConfigureWebHost(IWebHostBuilder builder){
   builder.ConfigureTestServices(services=>services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,o=>{o.Configuration=new OpenIdConnectConfiguration{Issuer="https://synthetic-issuer.invalid"};o.Configuration.SigningKeys.Add(key);o.ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(o.Configuration);o.RefreshOnIssuerKeyNotFound=false;o.TokenValidationParameters.ClockSkew=TimeSpan.Zero;}));
  }
 }
 [Theory][InlineData("valid",200)][InlineData("issuer",401)][InlineData("audience",401)][InlineData("expired",401)][InlineData("signature",401)]
 public async Task Jwt_signature_issuer_audience_and_lifetime_are_enforced(string scenario,int expected){
  using var rsa=RSA.Create(2048);using var wrong=RSA.Create(2048);var key=new RsaSecurityKey(rsa){KeyId="synthetic-test-key"};await using var app=new JwtApp(key);using var client=app.CreateClient();
  var token=new JwtSecurityToken(scenario=="issuer"?"https://wrong.invalid":"https://synthetic-issuer.invalid",scenario=="audience"?"wrong":"poseidon-test",[new Claim("sub","synthetic-jwt-user"),new Claim("tenant_id","aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),new Claim("company_id","aaaaaaaa-aaaa-aaaa-aaaa-000000000001")],DateTime.UtcNow.AddHours(-3),scenario=="expired"?DateTime.UtcNow.AddHours(-2):DateTime.UtcNow.AddMinutes(5),new SigningCredentials(scenario=="signature"?new RsaSecurityKey(wrong){KeyId="synthetic-test-key"}:key,SecurityAlgorithms.RsaSha256));
  client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(token));Assert.Equal((HttpStatusCode)expected,(await client.GetAsync("/api/v1/session")).StatusCode);
 }
}
