using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
namespace Poseidon.Tests;
public class ConfiguredAppFactory:WebApplicationFactory<Program> {
 protected virtual string EnvironmentName=>"Development";
 protected virtual Dictionary<string,string?> Settings=>new(){["SyntheticIdentity:Enabled"]="true",["Database:Initialize"]="false",["ConnectionStrings:Poseidon"]="Host=unused;Database=unused;Username=unused"};
 protected override IHost CreateHost(IHostBuilder builder){builder.UseEnvironment(EnvironmentName);builder.ConfigureHostConfiguration(c=>c.AddInMemoryCollection(Settings));if(!Settings.TryGetValue("Bff:Enabled",out var bff)||bff!="true")builder.ConfigureServices(s=>s.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider()));return base.CreateHost(builder);}
}
