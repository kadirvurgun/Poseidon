using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class DailyOperationsPolicyTests
{
    [Theory]
    [InlineData("Noon",20,18)] [InlineData("Deck",18,16)] [InlineData("Engine",20,18)]
    public void Missing_required_inputs_are_retained_as_incomplete_drafts(string kind,int total,int mutable) {
        Assert.True(DailyOperationsPolicy.TryNormalize(kind,new Dictionary<string,DailyValue>(),out var fields,out _));
        Assert.Equal(mutable,fields.Count); Assert.All(fields,f=>Assert.Equal("Missing",f.Value.State));
        var read=DailyOperationsPolicy.ReadFields(kind,Guid.NewGuid(),JsonSerializer.Serialize(fields));
        Assert.Equal(total,read.Count); Assert.Contains("UNVERIFIED_DECLARATION",DailyOperationsPolicy.Blockers(kind,read));
        Assert.DoesNotContain(read.Values,v=>v.Value is "REVIEWED" or "SUBMITTED");
    }
    [Fact]
    public void Exact_signed_decimal_and_unknown_are_neither_rounded_nor_coerced_to_zero() {
        var input=new Dictionary<string,DailyValue> { ["position_lat"]=new("Known","-01.123456789012345678901234567890"), ["position_lon"]=new("Unknown",null) };
        Assert.True(DailyOperationsPolicy.TryNormalize("Noon",input,out var fields,out _));
        Assert.Equal(input["position_lat"],fields["position_lat"]); Assert.Equal("Unknown",fields["position_lon"].State); Assert.Null(fields["position_lon"].Value);
    }
    [Theory]
    [InlineData("1e3")] [InlineData("NaN")] [InlineData("+1")] [InlineData("1,2")] [InlineData(".5")] [InlineData("1.")] [InlineData("-")]
    public void Numeric_transport_rejects_non_decimal_syntax(string text) => Assert.False(DailyOperationsPolicy.TryNormalize("Engine",
        new Dictionary<string,DailyValue>{["fuel_consumption"]=new("Known",text)},out _,out _));
    [Fact]
    public void Source_references_and_server_review_fields_cannot_be_invented_by_a_client() {
        Assert.False(DailyOperationsPolicy.TryNormalize("Noon",new Dictionary<string,DailyValue>{["voyage_id"]=new("Known",Guid.NewGuid().ToString())},out _,out var error));
        Assert.Equal("lookup_source_unavailable",error);
        Assert.False(DailyOperationsPolicy.TryNormalize("Noon",new Dictionary<string,DailyValue>{["master_review"]=new("Known","SUBMITTED")},out _,out _));
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck",new Dictionary<string,DailyValue>{["vessel_id"]=new("Known",Guid.NewGuid().ToString())},out _,out _));
    }
    [Fact]
    public void Narrative_handover_preserves_paragraphs_but_never_NUL_or_unknown_code_values() {
        Assert.True(DailyOperationsPolicy.TryNormalize("Deck",new Dictionary<string,DailyValue>{["handover_note"]=new("Known","FICTIONAL first\r\n\tsecond")},out var fields,out _));
        Assert.Contains("\n",fields["handover_note"].Value!);
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck",new Dictionary<string,DailyValue>{["handover_note"]=new("Known","hidden\0data")},out _,out _));
        Assert.False(DailyOperationsPolicy.TryNormalize("Noon",new Dictionary<string,DailyValue>{["activity"]=new("Known","Ready")},out _,out _));
    }
}
