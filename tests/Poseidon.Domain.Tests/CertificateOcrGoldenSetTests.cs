using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV045/T-S6-003 + DEV046/T-S6-004. Runs the actual in-process text-layer
// engine over the fictional synthetic golden set and scores it with the
// production scorer. The regression thresholds in the fixture are the measured
// baseline of this engine on this set (see docs/phases/S6-WPB-20261119.md); a
// drop fails CI. Synthetic results are not operational accuracy.
public sealed class CertificateOcrGoldenSetTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    internal sealed record GoldenRun(CertificateGoldenReport Report, Dictionary<string, CertificateRawExtraction?> Raw, Dictionary<string, string?> Capability, JsonElement Fixture);

    internal static GoldenRun Run()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "certificate-extraction.golden-pdf-v1.json");
        var fixture = JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        var examples = new List<CertificateGoldenExample>();
        var predictions = new List<CertificateGoldenPrediction>();
        var raws = new Dictionary<string, CertificateRawExtraction?>();
        var capability = new Dictionary<string, string?>();
        foreach (var document in fixture.GetProperty("documents").EnumerateArray())
        {
            var id = document.GetProperty("id").GetString()!;
            var mime = document.GetProperty("mime").GetString()!;
            var bytes = mime == "application/pdf" ? SyntheticCertificatePdf.Build(
                document.GetProperty("pages").EnumerateArray().Select(page => (IReadOnlyList<SyntheticPdfLine>)page.EnumerateArray().Select(line => new SyntheticPdfLine(
                    line.GetProperty("x").GetDouble(), line.GetProperty("y").GetDouble(), line.GetProperty("size").GetDouble(), line.GetProperty("text").GetString()!)).ToList()).ToList(),
                new SyntheticPdfOptions(document.GetProperty("encoding").GetString()!, document.GetProperty("compress").GetBoolean(), document.GetProperty("objectStream").GetBoolean(),
                    document.GetProperty("textMode").GetString()!, document.GetProperty("scale").GetDouble(), document.GetProperty("imageOnly").GetBoolean()))
                : [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]; // fictional PNG signature only
            var checksum = Convert.ToHexString(SHA256.HashData(bytes));
            examples.Add(new CertificateGoldenExample(id, "SYNTHETIC_MASKED", checksum, document.GetProperty("truth").Deserialize<Dictionary<string, string?>>()!,
                document.GetProperty("criticalFields").Deserialize<string[]>()!));
            CertificateRawExtraction raw;
            try
            {
                raw = CertificateTextLayerEngine.Extract(bytes, mime, checksum, TestContext.Current.CancellationToken);
                raws[id] = raw;
                capability[id] = null;
            }
            catch (CertificateTextLayerUnavailableException error)
            {
                // Unavailable = zero proposals; recall counts the missed truths honestly.
                raw = new CertificateRawExtraction(CertificateExtractionPolicy.Protocol, CertificateTextLayerEngine.EngineVersion, CertificateTextLayerEngine.RuleSetVersion, checksum, []);
                raws[id] = null;
                capability[id] = error.Reason;
            }
            predictions.Add(new CertificateGoldenPrediction(id, JsonSerializer.Serialize(raw, Web)));
        }
        var report = CertificateGoldenEvaluation.Evaluate(fixture.GetProperty("datasetVersion").GetString()!, CertificateTextLayerEngine.EngineVersion, CertificateTextLayerEngine.RuleSetVersion, examples, predictions);
        return new GoldenRun(report, raws, capability, fixture);
    }

    [Fact]
    public void Golden_set_meets_measured_regression_thresholds_with_zero_critical_hallucination_and_zero_authority_bypass()
    {
        var run = Run();
        var report = run.Report;
        var summary = JsonSerializer.Serialize(new
        {
            report.DatasetVersion, report.ModelVersion, report.PromptVersion, report.State, documents = report.Cases.Length,
            report.Precision, report.Recall, report.ExactMatchRate, report.FieldAccuracy, report.CorrectionRate, report.CriticalUnsupportedFacts, report.AuthorityBypasses,
            unavailable = run.Capability.Where(c => c.Value != null).ToDictionary(c => c.Key, c => c.Value),
            vesselMatch = VesselMatches(run), personMatch = PersonMatches(run),
            fields = report.Fields.Select(f => new { f.Key, f.Precision, f.Recall, exactMatch = f.Accuracy, f.Proposed, f.TruthPresent, f.CorrectProposed }),
            cases = report.Cases.Select(c => new { c.Id, c.ExactMatch, c.CorrectFields, c.ScoredFields, c.CriticalUnsupportedFacts }),
        }, new JsonSerializerOptions(Web) { WriteIndented = true });
        // Field metrics and case ids only: no document text or values.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "certificate-ocr-golden-report.json"), summary);
        TestContext.Current.TestOutputHelper?.WriteLine(summary);

        Assert.Equal("SYNTHETIC_SOFTWARE_EVALUATION_ONLY", report.State);
        Assert.Equal(CertificateTextLayerEngine.EngineVersion, report.ModelVersion);
        Assert.Equal(CertificateTextLayerEngine.RuleSetVersion, report.PromptVersion);
        var thresholds = run.Fixture.GetProperty("regressionThresholds");
        Assert.True(report.Precision >= thresholds.GetProperty("microPrecision").GetDecimal(), $"precision regressed: {report.Precision}");
        Assert.True(report.Recall >= thresholds.GetProperty("microRecall").GetDecimal(), $"recall regressed: {report.Recall}");
        Assert.True(report.ExactMatchRate >= thresholds.GetProperty("exactMatchRate").GetDecimal(), $"exact match regressed: {report.ExactMatchRate}");
        Assert.True(report.CriticalUnsupportedFacts <= thresholds.GetProperty("criticalUnsupportedFactsMax").GetInt32());
        Assert.True(report.AuthorityBypasses <= thresholds.GetProperty("authorityBypassesMax").GetInt32());
        var person = PersonMatches(run);
        Assert.True(person.FalseMatches <= thresholds.GetProperty("personFalseMatchesMax").GetInt32(), "person_reference false match");
        Assert.True(person.Recall >= thresholds.GetProperty("personMatchRecall").GetDecimal(), $"person match recall regressed: {person.Recall}");
        var vessel = VesselMatches(run);
        Assert.True(vessel.FalseMatches <= thresholds.GetProperty("vesselFalseMatchesMax").GetInt32(), "vessel_reference false match");
        Assert.True(vessel.Recall >= thresholds.GetProperty("vesselMatchRecall").GetDecimal(), $"vessel match recall regressed: {vessel.Recall}");
        if (thresholds.TryGetProperty("fieldRecall", out var fieldRecall))
            foreach (var field in fieldRecall.EnumerateObject())
                Assert.True(report.Fields.Single(f => f.Key == field.Name).Recall >= field.Value.GetDecimal(), $"{field.Name} recall regressed");
    }

    internal sealed record VesselMatchSummary(int Expected, int Correct, int FalseMatches, int Missed, decimal? Recall);

    // Runs the production projection with each document's fictional scoped vessel
    // profile; a match is only a suggestion reason, never verification.
    internal static VesselMatchSummary PersonMatches(GoldenRun run) => Matches(run, "personProfile", "expectPersonMatch", "person_reference",
        (value, profile) => CertificatePersonMatch.Match(value, new CertificatePersonIdentifiers(profile.GetProperty("name").GetString(), profile.GetProperty("employeeNo").GetString())));
    internal static VesselMatchSummary VesselMatches(GoldenRun run) => Matches(run, "vesselProfile", "expectVesselMatch", "vessel_reference",
        (value, profile) => CertificateVesselMatch.Match(value, new CertificateVesselIdentifiers(profile.GetProperty("name").GetString(), profile.GetProperty("code").GetString(),
            profile.GetProperty("imo").ValueKind == JsonValueKind.Null ? null : profile.GetProperty("imo").GetString())));
    private static VesselMatchSummary Matches(GoldenRun run, string profileKey, string expectKey, string field, Func<string?, JsonElement, string?> match)
    {
        int expected = 0, correct = 0, falseMatches = 0, missed = 0;
        foreach (var document in run.Fixture.GetProperty("documents").EnumerateArray())
        {
            var id = document.GetProperty("id").GetString()!;
            var profile = document.GetProperty(profileKey);
            var want = document.GetProperty(expectKey).ValueKind == JsonValueKind.Null ? null : document.GetProperty(expectKey).GetString();
            var raw = run.Raw[id];
            var got = raw == null ? null : match(raw.Fields.GetValueOrDefault(field)?.Value, profile);
            if (want != null) expected++;
            if (want != null && got == want) correct++;
            else if (want == null && got != null) falseMatches++;
            else if (want != null) missed++;
        }
        return new(expected, correct, falseMatches, missed, expected == 0 ? null : (decimal)correct / expected);
    }

    [Fact]
    public void Golden_set_capability_gaps_ambiguity_and_page_locators_match_the_fixture_expectations()
    {
        var run = Run();
        foreach (var document in run.Fixture.GetProperty("documents").EnumerateArray())
        {
            var id = document.GetProperty("id").GetString()!;
            var expected = document.GetProperty("expectCapability").ValueKind == JsonValueKind.Null ? null : document.GetProperty("expectCapability").GetString();
            Assert.Equal(expected, run.Capability[id]);
            if (expected != null) { Assert.Null(run.Raw[id]); continue; }
            var raw = Assert.IsType<CertificateRawExtraction>(run.Raw[id]);
            Assert.True(CertificateExtractionPolicy.ValidRaw(raw, raw.InputSha256, CertificateTextLayerEngine.EngineVersion, CertificateTextLayerEngine.RuleSetVersion));
            foreach (var field in raw.Fields)
            {
                Assert.NotNull(field.Value.Page);
                Assert.True(Assert.IsType<CertificateSourceRegion>(field.Value.Region).Valid());
                Assert.InRange(field.Value.Confidence!.Value, 0m, 1m);
            }
            foreach (var ambiguous in document.GetProperty("expectAmbiguous").EnumerateArray())
            {
                var candidate = raw.Fields[ambiguous.GetString()!];
                Assert.True(candidate.Ambiguous);
                Assert.Equal(CertificateTextLayerEngine.ScoreAmbiguous, candidate.Confidence);
            }
            foreach (var page in document.GetProperty("expectPage").EnumerateObject()) Assert.Equal(page.Value.GetInt32(), raw.Fields[page.Name].Page);
        }
        Assert.Empty(run.Raw["adversarial-instructions"]!.Fields);
    }

    [Fact]
    public void Engine_candidates_project_to_review_required_with_region_and_ambiguity_and_never_verify()
    {
        var run = Run();
        var raw = run.Raw["ambiguous-slash-date"]!;
        var candidates = CertificateExtractionPolicy.Project(raw, raw.InputSha256, raw.ModelVersion, raw.PromptVersion, 0.3m, new CertificateVesselIdentifiers("Synthetic Epsilon", "SYN-EPSILON"), null);
        Assert.All(candidates, c => Assert.Equal("REVIEW_REQUIRED", c.State));
        Assert.All(candidates, c => Assert.Contains("PROVIDER_CANDIDATE_NOT_VERIFICATION", c.Reasons));
        var expiry = candidates.Single(c => c.Key == "expiry_date");
        // Even under a permissive threshold, ambiguity forces low-confidence review.
        Assert.True(expiry.Ambiguous); Assert.True(expiry.LowConfidence); Assert.Contains("AMBIGUOUS_CANDIDATE_REVIEW", expiry.Reasons);
        Assert.NotNull(expiry.Region); Assert.DoesNotContain("SOURCE_REGION_UNAVAILABLE", expiry.Reasons);
        var vessel = candidates.Single(c => c.Key == "vessel_reference");
        Assert.Equal("REVIEW_REQUIRED", vessel.State); Assert.Contains("SCOPED_REFERENCE_MATCH_SUGGESTED", vessel.Reasons); Assert.Contains("SCOPED_REFERENCE_MATCHED_BY_NAME", vessel.Reasons);
        Assert.DoesNotContain("SCOPED_REFERENCE_UNMATCHED", vessel.Reasons);
        var foreign = CertificateExtractionPolicy.Project(raw, raw.InputSha256, raw.ModelVersion, raw.PromptVersion, 0.3m, new CertificateVesselIdentifiers("Synthetic Other", "SYN-OTHER"), null);
        Assert.Contains("SCOPED_REFERENCE_UNMATCHED", foreign.Single(c => c.Key == "vessel_reference").Reasons);
        var unscoped = CertificateExtractionPolicy.Project(raw, raw.InputSha256, raw.ModelVersion, raw.PromptVersion, 0.3m, null, null);
        Assert.Contains("SCOPED_REFERENCE_UNMATCHED", unscoped.Single(c => c.Key == "vessel_reference").Reasons);
        Assert.Contains("NOT_EXTRACTED", candidates.Single(c => c.Key == "issuer").Reasons);
    }

    [Fact]
    public void Text_layer_reader_rejects_non_pdf_encrypted_and_truncated_input_as_explicit_unavailable()
    {
        Assert.Equal(CertificateTextLayerUnavailableException.ImageOcrUnavailable,
            Assert.Throws<CertificateTextLayerUnavailableException>(() => CertificateTextLayerEngine.Extract([0xFF, 0xD8, 0xFF], "image/jpeg", new string('A', 64), TestContext.Current.CancellationToken)).Reason);
        Assert.Equal(CertificateTextLayerUnavailableException.PdfUnsupported,
            Assert.Throws<CertificateTextLayerUnavailableException>(() => CertificatePdfTextLayer.Read(Encoding.ASCII.GetBytes("NOT A PDF AT ALL, SYNTHETIC"), TestContext.Current.CancellationToken)).Reason);
        var pdf = SyntheticCertificatePdf.Build([[new(72, 700, 11, "Certificate No: SYN-ENC-1")]], new SyntheticPdfOptions());
        var encrypted = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(pdf).Replace("/Root 1 0 R", "/Root 1 0 R /Encrypt 99 0 R", StringComparison.Ordinal));
        Assert.Equal(CertificateTextLayerUnavailableException.PdfUnsupported, Assert.Throws<CertificateTextLayerUnavailableException>(() => CertificatePdfTextLayer.Read(encrypted, TestContext.Current.CancellationToken)).Reason);
        Assert.Throws<CertificateTextLayerUnavailableException>(() => CertificatePdfTextLayer.Read(pdf[..40], TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2031-02-03", "2031-02-03", false, true)]
    [InlineData("03.02.2031", "2031-02-03", false, true)]
    [InlineData("13/02/2031", "2031-02-13", false, true)]
    [InlineData("02/13/2031", "2031-02-13", false, true)]
    [InlineData("03/02/2031", "2031-02-03", true, true)]
    [InlineData("3 Feb 2031", "2031-02-03", false, true)]
    [InlineData("February 3, 2031", "2031-02-03", false, true)]
    [InlineData("31.02.2031", "31.02.2031", false, false)]
    [InlineData("next year", "next year", false, false)]
    public void Date_normalization_reports_ambiguity_and_never_invents_a_date(string printed, string value, bool ambiguous, bool parsed)
    {
        Assert.Equal((value, ambiguous, parsed), CertificateTextLayerEngine.NormalizeDate(printed));
    }

    [Theory]
    [InlineData("MV SYNTHETIC ALPHA", "Synthetic Alpha", "SYN-A", null, "NAME")]
    [InlineData("m/v  synthetic-alpha.", "Synthetic Alpha", "SYN-A", null, "NAME")]
    [InlineData("MV SENTETİK ZETA", "Sentetik Zeta", "SYN-Z", null, "NAME")]
    [InlineData("syn a", "Synthetic Alpha", "SYN-A", null, "CODE")]
    [InlineData("IMO 9074729", "Synthetic Alpha", "SYN-A", "9074729", "IMO")]
    [InlineData("9074729", "Synthetic Alpha", "SYN-A", "9074729", "IMO")]
    [InlineData("IMO 9074720", "Synthetic Alpha", "SYN-A", "9074729", null)]
    [InlineData("MV SYNTHETIC ALPHA II", "Synthetic Alpha", "SYN-A", null, null)]
    [InlineData("SYNTHETIC", "Synthetic Alpha", "SYN-A", null, null)]
    [InlineData("", "Synthetic Alpha", "SYN-A", null, null)]
    [InlineData("MV", "Synthetic Alpha", "SYN-A", null, null)]
    public void Vessel_reference_matches_only_exact_normalised_identifiers(string extracted, string? name, string? code, string? imo, string? expected)
    {
        Assert.Equal(expected, CertificateVesselMatch.Match(extracted, new CertificateVesselIdentifiers(name, code, imo)));
    }

    [Theory]
    [InlineData("SYNTHETIC SEAFARER ONE", "Synthetic Seafarer One", "SEA-1", "NAME")]
    [InlineData("synthetic seafarer, one.", "Synthetic Seafarer One", "SEA-1", "NAME")]
    [InlineData("SENTETİK DENİZCİ BİR", "Sentetik Denizci Bir", "SEA-1", "NAME")]
    [InlineData("sea 0042", "Synthetic Seafarer One", "SEA-0042", "EMPLOYEE_NO")]
    [InlineData("SYNTHETIC SEAFARER", "Synthetic Seafarer One", "SEA-1", null)]
    [InlineData("SYNTHETIC SEAFARER ONE", null, null, null)]
    [InlineData("SYNTHETIC", "Synthetic", "SEA-1", null)]
    [InlineData("MV SYNTHETIC ONE", "Synthetic One", "SEA-1", null)]
    public void Person_reference_matches_only_exact_normalised_crew_identity(string extracted, string? name, string? employee, string? expected)
    {
        Assert.Equal(expected, CertificatePersonMatch.Match(extracted, new CertificatePersonIdentifiers(name, employee)));
        Assert.Null(CertificatePersonMatch.Match(extracted, null));
    }

    [Fact]
    public void Person_match_is_only_a_review_suggestion_and_unreadable_identity_stays_unmatched()
    {
        var raw = Run().Raw["basic-labelled"]!;
        Project(raw, new("Synthetic Seafarer One", "SEA-0001"), out var matched);
        Assert.Equal("REVIEW_REQUIRED", matched.State); Assert.Contains("SCOPED_REFERENCE_MATCHED_BY_NAME", matched.Reasons); Assert.Contains("PROVIDER_CANDIDATE_NOT_VERIFICATION", matched.Reasons);
        Project(raw, null, out var unreadable);
        Assert.Contains("SCOPED_REFERENCE_UNMATCHED", unreadable.Reasons); Assert.DoesNotContain("SCOPED_REFERENCE_MATCH_SUGGESTED", unreadable.Reasons);
        static void Project(CertificateRawExtraction raw, CertificatePersonIdentifiers? person, out CertificateCandidate candidate) =>
            candidate = CertificateExtractionPolicy.Project(raw, raw.InputSha256, raw.ModelVersion, raw.PromptVersion, 0.8m, null, person).Single(c => c.Key == "person_reference");
    }
}
