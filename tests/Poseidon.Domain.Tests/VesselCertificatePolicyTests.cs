using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class VesselCertificatePolicyTests
{
    private static readonly DateOnly Reference=new(2030,1,1);
    [Fact]
    public void Incomplete_draft_is_allowed_but_never_a_qualified_or_ready_certificate() {
        var content=new VesselCertificateContent(new(new()));Assert.True(VesselCertificatePolicy.Valid(content));
        Assert.Equal("REVIEW_REQUIRED",VesselCertificatePolicy.Calendar(content.Metadata.Declaration,Reference).State);
        Assert.Contains("HUMAN_VERIFICATION_REQUIRED",VesselCertificatePolicy.Blockers(content));
    }
    [Theory]
    [InlineData(-1,"EXPIRED")][InlineData(0,"DUE")][InlineData(89,"DUE")][InlineData(90,"VALID")]
    public void Calendar_categories_disclose_declared_UTC_boundaries_without_claiming_legal_validity(int days,string state) {
        var result=VesselCertificatePolicy.Calendar(new(IssueDate:Reference.AddDays(-10),ExpiryDate:Reference.AddDays(days)),Reference);
        Assert.Equal(state,result.State);Assert.Equal(days,result.DaysToDeclaredExpiry);Assert.Equal("DeclaredUtcCalendarNotLegalValidity",result.Semantics);
    }
    [Fact]
    public void Future_or_reversed_dates_and_unresolved_text_window_do_not_become_valid() {
        Assert.Equal("REVIEW_REQUIRED",VesselCertificatePolicy.Calendar(new(IssueDate:Reference.AddDays(1),ExpiryDate:Reference.AddYears(1)),Reference).State);
        Assert.Equal("REVIEW_REQUIRED",VesselCertificatePolicy.Calendar(new(IssueDate:Reference,ExpiryDate:Reference.AddDays(-1)),Reference).State);
        Assert.Equal("REVIEW_REQUIRED",VesselCertificatePolicy.Calendar(new(IssueDate:Reference,ExpiryDate:Reference.AddYears(1)),Reference,Reference,"source window unresolved").State);
        Assert.Equal("SURVEY_WINDOW",VesselCertificatePolicy.Calendar(new(IssueDate:Reference,ExpiryDate:Reference.AddYears(1)),Reference,Reference,"2030-01-10").State);
    }
    [Fact]
    public void Vessel_common_fields_cannot_smuggle_person_credentials_or_mismatched_file_identity() {
        Assert.False(VesselCertificatePolicy.Valid(new(new(new(),StcwReference:"unresolved private crew reference"))));
        Assert.False(VesselCertificatePolicy.Valid(new(new(new(),DocumentReference:Guid.NewGuid()))));
        Assert.False(VesselCertificatePolicy.Valid(new(new(new()),Document:new(Guid.NewGuid(),1,new string('A',64)))));
    }
}
