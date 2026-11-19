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
    // DEV-097/098: a reference becomes Known only with a registered owning reader
    // AND an exact canonical pin; otherwise the source remains unavailable.
    [Fact]
    public void Reference_is_Known_only_through_a_registered_owning_reader_and_a_canonical_pin() {
        var id = Guid.Parse("6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21");
        var pin = new DailyReferencePin("crew-assignment", id, 3).Token;
        Assert.Equal("crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@3", pin);
        bool Deck(string kind, string key) => kind == "Deck" && key == "officer_of_watch";
        Assert.True(DailyOperationsPolicy.TryNormalize("Deck", new Dictionary<string,DailyValue>{["officer_of_watch"]=new("Known",pin)}, out var fields, out _, Deck));
        Assert.Equal(pin, fields["officer_of_watch"].Value);
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck", new Dictionary<string,DailyValue>{["officer_of_watch"]=new("Known",pin)}, out _, out var error));
        Assert.Equal("lookup_source_unavailable", error);
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck", new Dictionary<string,DailyValue>{["evidence_ref"]=new("Known","evidence:"+id+"@1")}, out _, out error, Deck));
        Assert.Equal("lookup_source_unavailable", error);
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck", new Dictionary<string,DailyValue>{["officer_of_watch"]=new("Known","2nd Officer Smith")}, out _, out error, Deck));
        Assert.Equal("invalid_payload", error);
        Assert.True(DailyOperationsPolicy.TryNormalize("Deck", new Dictionary<string,DailyValue>{["officer_of_watch"]=new("Unknown",null)}, out fields, out _, Deck));
        Assert.Equal("Unknown", fields["officer_of_watch"].State);
        Assert.False(DailyOperationsPolicy.TryNormalize("Noon", new Dictionary<string,DailyValue>{["voyage_id"]=new("Known","voyage:"+id+"@1")}, out _, out error, Deck));
        Assert.Equal("lookup_source_unavailable", error);
    }
    [Theory]
    [InlineData("crew-assignment:6B1F8A8E-3F43-4D2A-9A3C-1D4F6C0B9E21@3")] [InlineData("crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@0")]
    [InlineData("crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@03")] [InlineData("crew-assignment:6b1f8a8e3f434d2a9a3c1d4f6c0b9e21@3")]
    [InlineData("Crew:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@3")] [InlineData("crew-:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@3")]
    [InlineData("crew-assignment:00000000-0000-0000-0000-000000000000@1")] [InlineData(" crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@3")]
    [InlineData("crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@-3")] [InlineData("crew-assignment:6b1f8a8e-3f43-4d2a-9a3c-1d4f6c0b9e21@1234567890123456789")]
    public void Reference_pin_accepts_only_the_exact_canonical_lexical_form(string text) =>
        Assert.False(DailyReferencePin.TryParse(text, out _));
    private static DateOnly D(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    // DEV-098 officer_of_watch: the assignment must cover the deck log's own date.
    [Theory]
    [InlineData("2026-10-01", "2026-11-01", "2026-10-20", true)]   // ended after log_date: back-dated log accepted
    [InlineData("2026-10-01", null, "2026-10-20", true)]           // open-ended
    [InlineData("2026-10-20", "2026-11-01", "2026-10-20", true)]   // starts on log_date (inclusive)
    [InlineData("2026-10-21", "2026-11-01", "2026-10-20", false)]  // started after log_date
    [InlineData("2026-10-01", "2026-10-15", "2026-10-20", false)]  // ended before log_date
    [InlineData("2026-10-01", "2026-10-20", "2026-10-20", false)]  // end is exclusive
    [InlineData(null, "2026-11-01", "2026-10-20", false)]          // unknown start never covers
    public void Officer_assignment_must_cover_the_log_date_not_the_save_date(string? from, string? to, string log, bool covers) =>
        Assert.Equal(covers, DailyOfficerAssignment.Covers(from == null ? null : D(from), to == null ? null : D(to), D(log)));
    [Fact]
    public void Officer_check_requires_a_known_exact_log_date() {
        Assert.True(DailyOfficerAssignment.TryLogDate(new Dictionary<string,DailyValue>{["log_date"]=new("Known","2026-10-20")}, out var date));
        Assert.Equal(new DateOnly(2026,10,20), date);
        foreach (var value in new DailyValue[]{ new("Unknown",null), new("Missing",null), new("Known","20.10.2026"), new("Known","2026-02-30"), new("Known","2026-10-20T00:00Z") })
            Assert.False(DailyOfficerAssignment.TryLogDate(new Dictionary<string,DailyValue>{["log_date"]=value}, out _));
        Assert.False(DailyOfficerAssignment.TryLogDate(null, out _));
    }
    [Fact]
    public void Narrative_handover_preserves_paragraphs_but_never_NUL_or_unknown_code_values() {
        Assert.True(DailyOperationsPolicy.TryNormalize("Deck",new Dictionary<string,DailyValue>{["handover_note"]=new("Known","FICTIONAL first\r\n\tsecond")},out var fields,out _));
        Assert.Contains("\n",fields["handover_note"].Value!);
        Assert.False(DailyOperationsPolicy.TryNormalize("Deck",new Dictionary<string,DailyValue>{["handover_note"]=new("Known","hidden\0data")},out _,out _));
        Assert.False(DailyOperationsPolicy.TryNormalize("Noon",new Dictionary<string,DailyValue>{["activity"]=new("Known","Ready")},out _,out _));
    }
}
