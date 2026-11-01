using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class CertificateMetadataTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("0", true)]
    [InlineData("12.00000000000000000000000000000000000001", true)]
    [InlineData("99999999999999999999999999999999999999999", true)]
    [InlineData("-1", false)]
    [InlineData("TR", false)]
    [InlineData(" 1", false)]
    [InlineData("1,2", false)]
    [InlineData("1e2", false)]
    [InlineData("01", false)]
    public void Source_numeric_is_not_an_ISO_catalog_and_never_loses_digits(string? numeric, bool valid) {
        var metadata = new CertificateMetadata(new(), IssuingCountryNumeric: numeric);
        Assert.Equal(valid, CertificateMetadataPolicy.Valid(metadata));
        Assert.Equal(numeric, JsonSerializer.Deserialize<CertificateMetadata>(JsonSerializer.Serialize(metadata))!.IssuingCountryNumeric);
    }
    [Fact]
    public void Incomplete_endorsement_unknown_is_preserved_and_formatter_withholds_all_private_fields() {
        var m = new CertificateMetadata(new(CertificateNumber: "FICTIONAL-PRIVATE", Issuer: "FICTIONAL"), Notes: "PRIVATE");
        Assert.True(CertificateMetadataPolicy.Valid(m)); Assert.Null(m.EndorsementRequired);
        Assert.Equal(nameof(CertificateMetadata), m.ToString());
    }
    [Theory]
    [InlineData("TenantId")]
    [InlineData("PersonId")]
    [InlineData("VerifiedBy")]
    [InlineData("VerificationState")]
    public void Strict_capture_cannot_supply_owner_or_verifier(string property) => Assert.Throws<JsonException>(() =>
        JsonSerializer.Deserialize<CertificateMetadata>("{\"Declaration\":{},\"" + property + "\":\"forged\"}"));
    [Fact]
    public void Reversed_future_and_expiry_day_are_calendar_facts_without_eligibility_transition() {
        var m = new CertificateMetadata(new(IssueDate: new(2027, 1, 1), ExpiryDate: new(2026, 11, 1)));
        Assert.True(CertificateMetadataPolicy.Valid(m));
        var facts = CredentialDeclarationDates.Evaluate(m.Declaration, new(2026, 11, 1));
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, facts.Expiry); Assert.Equal(DeclaredCredentialDateOrder.ExpiryBeforeIssue, facts.Order);
    }
}
