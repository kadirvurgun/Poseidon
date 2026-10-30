using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace Poseidon;
public sealed class SyntheticIdentity
{
 public static readonly Dictionary<string,Session> Fixtures=new() {
  ["tenant-a"]=new("synthetic-a",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true),
  ["inventory-manager"]=new("synthetic-inventory-manager",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true),
  ["evidence-manager"]=new("synthetic-evidence-manager",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true),
  ["certificate-manager"]=new("synthetic-certificate-manager",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true),
  ["tenant-b"]=new("synthetic-b",Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000001"),[],true),
  ["tenant-a-other-company"]=new("synthetic-a",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000002"),[],true),
  ["tenant-a-readonly"]=new("synthetic-a-readonly",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true),
  ["tenant-a-restricted"]=new("synthetic-a-restricted",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"),[],true)
 };
 readonly ConcurrentDictionary<string,(Session session,DateTimeOffset expires)> tokens=new();
 public object Issue(string fixture) {if(!Fixtures.TryGetValue(fixture,out var s))throw new ApiFault(422,"unknown_fixture","Use a configured synthetic fixture.");var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));tokens[token]=(s,DateTimeOffset.UtcNow.AddHours(1));return new {accessToken=token,session=s};}
 public Session? Find(string token)=>tokens.TryGetValue(token,out var value)&&value.expires>DateTimeOffset.UtcNow?value.session:null;
}
public sealed class SyntheticHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder,SyntheticIdentity identities):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
 protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
  var header=Request.Headers.Authorization.ToString();var s=header.StartsWith("Bearer ",StringComparison.Ordinal)?identities.Find(header[7..]):null;
  if(s is null)return Task.FromResult(AuthenticateResult.NoResult());
  Claim[] claims=[new("sub",s.Subject),new("tenant_id",s.TenantId.ToString()),new("company_id",s.CompanyId.ToString()),new("synthetic","true"),..(s.Subject=="synthetic-a-readonly"?Array.Empty<Claim>():new[]{new Claim("permission","draft:write")}),..(s.Subject is "synthetic-inventory-manager" or "synthetic-evidence-manager"?new[]{new Claim("permission","inventory:manage")}:Array.Empty<Claim>()),..(s.Subject is "synthetic-certificate-manager" or "synthetic-evidence-manager"?new[]{new Claim("permission","certificate:manage")}:Array.Empty<Claim>()),..(s.Subject=="synthetic-evidence-manager"?new[]{new Claim("permission","evidence:capture")}:Array.Empty<Claim>()),..s.VesselIds.Select(id=>new Claim("vessel_id",id.ToString()))];
  return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name)));
 }
}
