using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-062 / T-S9-005 and DEV-063 / T-S9-006 (18.01 Backlog rows 63-64).
[Trait("Requirement", "DEV-062")]
public sealed class VoiceEventDraftPolicyTests
{
    private static readonly DateOnly Reference = new(2026, 11, 19);
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000000";
    private static VoiceDraftCandidateField? Field(VoiceDraftCandidate c, string key) => c.Fields.SingleOrDefault(f => f.Key == key);

    [Fact]
    public void Turkish_note_yields_traced_versioned_candidate_fields()
    {
        const string note = "Ramak kala olayı. Bugün saat 14:30 makine dairesinde yakıt hattından sızıntı görüldü. Pompayı durdurduk ve vardiya mühendisine haber verdik.";
        var c = VoiceEventDraftPolicy.Extract(note, Sha, Reference);
        Assert.Equal((VoiceEventDraftPolicy.Protocol, VoiceEventDraftPolicy.ExtractorVersion, VoiceEventDraftPolicy.RuleSetVersion, Sha),
            (c.Protocol, c.ExtractorVersion, c.RuleSetVersion, c.TranscriptSha256));
        Assert.Equal("NEAR_MISS", Field(c, "eventType")!.Value);
        Assert.Equal("Ramak kala", note[Field(c, "eventType")!.Spans[0].Start..Field(c, "eventType")!.Spans[0].End]);
        Assert.Equal("2026-11-19T14:30", Field(c, "occurredAt")!.Value);
        Assert.Equal("ENGINE_ROOM", Field(c, "location")!.Value);
        Assert.Equal(note, Field(c, "description")!.Value);
        Assert.Equal("Pompayı durdurduk ve vardiya mühendisine haber verdik.", Field(c, "immediateAction")!.Value);
        Assert.All(c.Fields, f => Assert.True(f.Confidence is > 0 and <= 1 && f.Rule.Length > 0 && VoiceEventDraftPolicy.Grounded(note, f), f.Key));
    }

    [Fact]
    public void Unstated_facts_are_never_proposed()
    {
        // No type, no date (time only), no place: only the verbatim description and hints.
        var c = VoiceEventDraftPolicy.Extract("Radar ekranı bir süre dondu, saat 03:10 civarıydı.", Sha, Reference);
        Assert.Null(Field(c, "eventType")); Assert.Null(Field(c, "occurredAt")); Assert.Null(Field(c, "location")); Assert.Null(Field(c, "immediateAction"));
        Assert.Contains(c.Hints, h => h.Code == "TIME_WITHOUT_DATE");
        var fields = VoiceEventDraftPolicy.FromCandidate(c);
        Assert.Equal(["eventType", "occurredAt", "location", "immediateAction"], VoiceEventDraftPolicy.Missing(fields));
    }

    [Fact]
    public void Negated_injury_is_not_an_incident_and_conflicting_explicit_classes_are_left_to_the_user()
    {
        Assert.Null(Field(VoiceEventDraftPolicy.Extract("Oil on the deck, no one was injured.", Sha, Reference), "eventType"));
        Assert.Null(Field(VoiceEventDraftPolicy.Extract("Yağ döküldü, kimse yaralanmadı.", Sha, Reference), "eventType"));
        Assert.Null(Field(VoiceEventDraftPolicy.Extract("Near miss or non-conformity, not sure.", Sha, Reference), "eventType"));
        Assert.Equal("INCIDENT", Field(VoiceEventDraftPolicy.Extract("Bir tayfa yaralandı.", Sha, Reference), "eventType")!.Value);
    }

    [Fact]
    public void Evidence_mention_is_a_hint_and_vessel_never_comes_from_text()
    {
        var c = VoiceEventDraftPolicy.Extract("Near miss on MV EXAMPLE main deck, photo taken.", Sha, Reference);
        Assert.Contains(c.Hints, h => h.Code == "EVIDENCE_MENTIONED");
        Assert.DoesNotContain(c.Fields, f => f.Key is "vessel" or "evidence");
    }

    [Fact]
    public void Several_places_lower_location_confidence()
    {
        var c = VoiceEventDraftPolicy.Extract("Leak seen in the pump room, then traced to the engine room.", Sha, Reference);
        var location = Field(c, "location")!;
        Assert.Equal(("PUMP_ROOM", 0.50m, "LOCATION_FIRST_OF_SEVERAL"), (location.Value, location.Confidence, location.Rule));
    }

    [Fact]
    public void Field_validation_and_corrections_record_the_user_outcome()
    {
        var c = VoiceEventDraftPolicy.Extract("Near miss on the main deck today at 09:15. We cordoned off the area.", Sha, Reference);
        var initial = VoiceEventDraftPolicy.FromCandidate(c);
        Assert.True(VoiceEventDraftPolicy.Valid(initial));
        Assert.False(VoiceEventDraftPolicy.Valid(initial with { EventType = "READY" }));
        Assert.False(VoiceEventDraftPolicy.Valid(initial with { OccurredAt = "2026-11-19 09:15" }));
        Assert.False(VoiceEventDraftPolicy.Valid(initial with { Location = "OTHER", LocationDetail = null }));
        var edited = initial with { Location = "FORECASTLE", LocationDetail = "port side" };
        Assert.Equal("USER_EDITED", VoiceEventDraftPolicy.Origin(c, edited, "location"));
        Assert.Equal("USER_ENTERED", VoiceEventDraftPolicy.Origin(c, edited, "locationDetail"));
        Assert.Equal("EXTRACTED", VoiceEventDraftPolicy.Origin(c, edited, "eventType"));
        var corrections = VoiceEventDraftPolicy.Corrections(c, edited).ToDictionary(x => x.Key, x => x.Outcome);
        Assert.Equal(("ACCEPTED", "CORRECTED", "ADDED", "ACCEPTED"), (corrections["eventType"], corrections["location"], corrections["locationDetail"], corrections["occurredAt"]));
    }

    [Fact]
    public void Invalid_transcripts_are_rejected()
    {
        Assert.False(VoiceEventDraftPolicy.ValidTranscript(" "));
        Assert.False(VoiceEventDraftPolicy.ValidTranscript(new string('a', VoiceEventDraftPolicy.MaxTranscript + 1)));
        Assert.False(VoiceEventDraftPolicy.ValidTranscript("bad\u0007bell"));
        Assert.Throws<ArgumentException>(() => VoiceEventDraftPolicy.Extract("", Sha, Reference));
    }
}

// DEV-063: the masked synthetic golden set must never contain a critical fabricated fact.
[Trait("Requirement", "DEV-063")]
public sealed class VoiceDraftGoldenSetTests
{
    private static (VoiceGoldenReport Report, JsonElement Fixture) Run()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "voice-event-draft.golden-v1.json"))).RootElement.Clone();
        var examples = fixture.GetProperty("examples").EnumerateArray().Select(e => new VoiceGoldenExample(e.GetProperty("id").GetString()!, e.GetProperty("masking").GetString()!,
            e.GetProperty("language").GetString()!, e.GetProperty("transcript").GetString()!, DateOnly.Parse(e.GetProperty("referenceDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            e.GetProperty("truth").Deserialize<Dictionary<string, string?>>()!, e.GetProperty("criticalFields").Deserialize<string[]>()!, e.GetProperty("reviewDecision").GetString()!,
            e.GetProperty("rejectionReason").ValueKind == JsonValueKind.Null ? null : e.GetProperty("rejectionReason").GetString())).ToArray();
        return (VoiceDraftGoldenEvaluation.Evaluate(fixture.GetProperty("datasetVersion").GetString()!, examples), fixture);
    }

    [Fact]
    public void Golden_set_has_zero_critical_fabricated_facts_and_meets_the_recorded_baseline()
    {
        var (report, fixture) = Run();
        var t = fixture.GetProperty("thresholds");
        Assert.Equal(("SYNTHETIC_MASKED", "SYNTHETIC_SOFTWARE_EVALUATION_ONLY"), (report.CorpusKind, report.State));
        var failing = string.Join("; ", report.Cases.Where(c => c.Fabricated > 0 || !c.UsableWithoutCorrection).Select(c => c.Id));
        Assert.True(report.CriticalFabricatedFacts == t.GetProperty("criticalFabricatedFacts").GetInt32(), "critical fabricated: " + failing);
        Assert.True(report.FabricatedFacts <= t.GetProperty("fabricatedFacts").GetInt32(), "fabricated: " + failing);
        Assert.True(report.FieldCompleteness >= t.GetProperty("minFieldCompleteness").GetDecimal(), $"completeness {report.FieldCompleteness}: {failing}");
        Assert.True(report.CorrectionRate <= t.GetProperty("maxCorrectionRate").GetDecimal(), $"correction {report.CorrectionRate}: {failing}");
        Assert.True(report.Usability >= t.GetProperty("minUsability").GetDecimal(), $"usability {report.Usability}: {failing}");
    }

    [Fact]
    public void Synthetic_reviewer_rejections_are_counted_by_reason()
    {
        var (report, _) = Run();
        Assert.Equal(3, report.Rejections);
        Assert.Equal(new Dictionary<string, int> { ["DUPLICATE"] = 1, ["FIELDS_WRONG"] = 1, ["NOT_AN_EVENT"] = 1 }, report.RejectionsByReason);
    }

    [Fact]
    public void Unmasked_or_malformed_examples_are_refused()
    {
        var truth = VoiceDraftGoldenEvaluation.ScoredKeys.ToDictionary(k => k, _ => (string?)null);
        Assert.Throws<InvalidDataException>(() => VoiceDraftGoldenEvaluation.Evaluate("v1", [new("X", "REAL", "tr", "not", new(2026, 11, 19), truth, [], "CONFIRM", null)]));
        Assert.Throws<InvalidDataException>(() => VoiceDraftGoldenEvaluation.Evaluate("v1", [new("X", "SYNTHETIC_MASKED", "tr", "not", new(2026, 11, 19), truth, [], "REJECT", null)]));
    }

    [Fact]
    public void Operational_observation_reports_correction_and_rejection_without_invented_rates()
    {
        var empty = VoiceDraftGoldenEvaluation.Observe([], [], 0);
        Assert.Null(empty.FieldCompleteness); Assert.Null(empty.CorrectionRate); Assert.Null(empty.RejectionRate);
        VoiceDraftFieldCorrection[] one = [new("eventType", "ACCEPTED", true), new("location", "CORRECTED", true), new("occurredAt", "ADDED", true)];
        var o = VoiceDraftGoldenEvaluation.Observe([one], ["NOT_AN_EVENT"], 2);
        Assert.Equal((1, 1, 2), (o.Confirmed, o.Rejected, o.Open));
        Assert.Equal(2m / 3, o.FieldCompleteness); Assert.Equal(0.5m, o.CorrectionRate); Assert.Equal(0m, o.Usability); Assert.Equal(0.5m, o.RejectionRate);
    }
}
