using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Poseidon;
using Xunit;
namespace Poseidon.Tests;
public class ValidationTests {
 static JsonElement Body(string data,string mode="DRAFT")=>JsonDocument.Parse("{\"saveMode\":\""+mode+"\",\"data\":"+data+"}").RootElement;
 [Fact] public void Missing_metadata_is_allowed_but_defaults_are_not_invented(){var data=Contracts.Validate(Body("{}"),"vessels");Assert.Empty(data.EnumerateObject());}
 [Fact] public void Supplied_values_are_preserved(){var data=Contracts.Validate(Body("{\"name\":\"  Synthetic vessel  \",\"imo_no\":\"1234567\"}"),"vessels");Assert.Equal("  Synthetic vessel  ",data.GetProperty("name").GetString());}
 [Theory][InlineData("{\"tenant_id\":\"forged\"}")][InlineData("{\"verified\":true}")][InlineData("{\"imo_no\":\"abc\"}")][InlineData("{\"imo_no\":\"1234567\\n\"}")][InlineData("{\"name\":null}")][InlineData("{\"name\":\"\"}")][InlineData("{\"name\":\"one\",\"name\":\"two\"}")]
 public void Unknown_forged_invalid_fields_rejected(string data)=>Assert.Throws<ApiFault>(()=>Contracts.Validate(Body(data),"vessels"));
 [Theory][InlineData("COMPLETE")][InlineData("SUBMIT")][InlineData("VERIFIED")]
 public void Domain_transitions_blocked(string mode)=>Assert.Equal(403,Assert.Throws<ApiFault>(()=>Contracts.Validate(Body("{}",mode),"vessels")).Status);
 [Fact] public void Invalid_calendar_date_rejected()=>Assert.Throws<ApiFault>(()=>Contracts.Validate(Body("{\"vessel_id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"issue\":\"2026-02-30\"}"),"certificates"));
 [Fact] public void Certificate_requires_scope_not_full_domain_completion()=>Assert.Throws<ApiFault>(()=>Contracts.Validate(Body("{}"),"certificates"));
 [Fact] public void Canonical_certificate_fields_preserved(){var data=Contracts.Validate(Body("{\"vessel_id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"type\":\"Synthetic metadata\",\"issue\":\"2026-01-01\",\"expiry\":\"2027-01-01\"}"),"certificates");Assert.Equal("Synthetic metadata",data.GetProperty("type").GetString());Assert.Equal("2026-01-01",data.GetProperty("issue").GetString());}
 [Theory][InlineData("certificate_type")][InlineData("issue_date")][InlineData("expiry_date")]
 public void Noncanonical_certificate_aliases_are_rejected(string field)=>Assert.Equal("unknown_field",Assert.Throws<ApiFault>(()=>Contracts.Validate(Body("{\"vessel_id\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\""+field+"\":\"unpublished alias\"}"),"certificates")).Code);

}
public class ProductionGuardTests {
 sealed class ProductionApp:ConfiguredAppFactory {protected override string EnvironmentName=>"Production";}
 [Fact] public void Synthetic_identity_fails_closed_in_production(){using var app=new ProductionApp();var exception=Assert.Throws<InvalidOperationException>(()=>app.CreateClient());Assert.Contains("Development-only",exception.Message);}
}
