using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

// S10 WP-A: DEV-064 / T-S10-001 (18.01 row 65), DEV-065 / T-S10-002 (row 66), DEV-113 / T-ASS2-001 (row 114).
// FICTIONAL values only. No case produces a READY state, a verified finding or an official audit record.
public sealed class S10WpaAssurancePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static DataQualityInput Evidence(SourceFreshness freshness = SourceFreshness.Current, string verification = "VERIFIED") =>
        new("Evidence", freshness, "UNKNOWN", [], [], verification, [], false);

    public static TheoryData<string, DataQualityInput, string[]> SignalCases => new()
    {
        { "clean verified evidence in its window", Evidence(), [] },
        { "missing provenance", Evidence() with { MissingFields = ["PROVENANCE_MISSING"] }, ["MISSING:PROVENANCE_MISSING"] },
        { "stale observation", Evidence(SourceFreshness.Stale), ["STALE:OBSERVATION_STALE"] },
        { "no freshness policy is never current", Evidence(SourceFreshness.Unavailable), ["STALE:FRESHNESS_POLICY_UNAVAILABLE"] },
        { "unknown observation time", Evidence(SourceFreshness.Unknown), ["STALE:OBSERVATION_TIME_UNKNOWN"] },
        { "future dated", Evidence(SourceFreshness.Future), ["STALE:OBSERVATION_IN_FUTURE"] },
        { "declared expiry", Evidence() with { DeclaredTimeState = "DECLARED_EXPIRED" }, ["STALE:DECLARED_VALIDITY_EXPIRED"] },
        { "superseded revision", Evidence() with { Superseded = true }, ["STALE:REVISION_SUPERSEDED"] },
        { "unverified", Evidence(verification: "UNVERIFIED"), ["UNVERIFIED:VERIFICATION_NOT_PERFORMED"] },
        { "expired verification", Evidence(verification: "VERIFICATION_EXPIRED"), ["UNVERIFIED:VERIFICATION_EXPIRED"] },
        { "conflicting sources", Evidence() with { ConflictingFields = ["SOURCES_DISAGREE"] }, ["CONFLICTING:SOURCES_DISAGREE"] },
        { "orphan evidence", Evidence() with { OrphanReasons = ["CONTROL_LINK_MISSING"] }, ["ORPHAN:CONTROL_LINK_MISSING"] },
        { "control with unknown criticality", new DataQualityInput("Control", SourceFreshness.Current, null, [], ["CRITICALITY_UNKNOWN"], "UNKNOWN", [], false), ["ORPHAN:CRITICALITY_UNKNOWN"] },
    };

    [Theory]
    [MemberData(nameof(SignalCases))]
    [Trait("Requirement", "DEV-065")]
    public void Every_material_signal_degrades_to_review_required_and_nothing_is_ever_ready(string name, DataQualityInput input, string[] expected)
    {
        var result = DataQualityPolicy.Assess(input);
        Assert.Equal(expected, result.Signals.Select(s => s.Kind + ":" + s.Reason).ToArray());
        Assert.Equal(expected.Length == 0 ? DataQualityPolicy.NoMaterialSignal : DataQualityPolicy.ReviewRequired, result.Disposition);
        Assert.DoesNotContain("READY", DataQualityPolicy.Dispositions);
        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    [Trait("Requirement", "DEV-065")]
    public void Readiness_adapter_floors_a_green_consumer_state_and_never_upgrades_a_worse_one()
    {
        DataQualitySignal[] material = [new("UNVERIFIED", "VERIFICATION_NOT_PERFORMED", true)];
        Assert.Equal("REVIEW_REQUIRED", DataQualityPolicy.Floor("READY", material));
        Assert.Equal("REVIEW_REQUIRED", DataQualityPolicy.Floor("PASS", material));
        Assert.Equal("NOT_READY", DataQualityPolicy.Floor("NOT_READY", material));
        Assert.Equal("READY", DataQualityPolicy.Floor("READY", []));
    }

    [Fact]
    [Trait("Requirement", "DEV-065")]
    public void Freshness_without_a_declared_window_is_unavailable_and_conflicts_need_two_sources_with_different_values()
    {
        Assert.Equal(SourceFreshness.Unavailable, DataQualityPolicy.Freshness(Now.AddDays(-1), null, Now));
        Assert.Equal(SourceFreshness.Unknown, DataQualityPolicy.Freshness(null, Now.AddDays(1), Now));
        Assert.Equal(SourceFreshness.Current, DataQualityPolicy.Freshness(Now.AddDays(-1), Now.AddDays(1), Now));
        Assert.Equal(SourceFreshness.Stale, DataQualityPolicy.Freshness(Now.AddDays(-10), Now.AddDays(-5), Now));
        Assert.Empty(DataQualityPolicy.Conflicts([("shipType", "DOCUMENT", "A"), ("shipType", "CLASS_RECORD", "A")]));
        Assert.Empty(DataQualityPolicy.Conflicts([("shipType", "DOCUMENT", "A"), ("shipType", "DOCUMENT", "B")]));
        Assert.Equal(["shipType"], DataQualityPolicy.Conflicts([("shipType", "DOCUMENT", "A"), ("shipType", "CLASS_RECORD", "B")]));
        Assert.Equal(["gt"], DataQualityPolicy.Conflicts([("gt", "DOCUMENT", "1"), ("gt", "OWNER_DECLARATION", null)]));
    }

    [Theory]
    [InlineData(false, "Known", "Oil/Chemical Tanker", "TANKER")]
    [InlineData(false, "Known", "LNG carrier", "TANKER")]
    [InlineData(false, "Known", "Liquefied gas carrier", "TANKER")]
    [InlineData(false, "Known", "Bulk carrier", "NOT_TANKER")]
    [InlineData(false, "Known", "LNG-fuelled container ship", "NOT_TANKER")]
    [InlineData(false, "Unknown", null, "UNKNOWN")]
    [InlineData(false, "Missing", null, "UNKNOWN")]
    [InlineData(true, "Known", "Oil tanker", "NOT_TANKER")]
    [Trait("Requirement", "DEV-113")]
    public void Sire_applies_to_tankers_only_and_an_undeclared_type_is_review_required_not_excluded(bool tug, string state, string? value, string expected)
    {
        var classification = AssurancePolicy.Classify(tug, state, value);
        Assert.Equal(expected, classification);
        Assert.Equal(expected switch { "TANKER" => "APPLICABLE", "NOT_TANKER" => "NOT_APPLICABLE", _ => "REVIEW_REQUIRED" }, AssurancePolicy.Applicability(classification));
    }

    private static readonly AssuranceColumnBinding[] Mapping = [new("Question", "question_ref"), new("Type", "observation_type"), new("Text", "observation_text"), new("Category", "observation_category")];

    [Fact]
    [Trait("Requirement", "DEV-113")]
    public void Mapping_is_closed_and_versioned_and_rows_are_parsed_without_inventing_values()
    {
        Assert.Empty(AssurancePolicy.MappingIssues(Mapping));
        Assert.Contains("MISSING_REQUIRED_MAPPING:observation_type", AssurancePolicy.MappingIssues([new("Q", "question_ref")]));
        Assert.Contains("UNKNOWN_TARGET", AssurancePolicy.MappingIssues([new("Q", "question_ref"), new("T", "observation_type"), new("X", "close_finding")]));
        Assert.Contains("DUPLICATE_TARGET", AssurancePolicy.MappingIssues([new("Q", "question_ref"), new("Q2", "question_ref"), new("T", "observation_type")]));
        Assert.NotEqual(AssurancePolicy.MappingHash(1, Mapping), AssurancePolicy.MappingHash(2, Mapping));
        var table = new ImportTable(["Question", "Type", "Text", "Category"], [
            ["2.1.1", "negative", "FICTIONAL hose certificate expired", ""],
            ["2.1.2", "NONE", "", ""],
            ["bad", "NONE", "", ""],
            ["2.1.3", "NEGATIVE", "", "HUMAN"],
            ["2.1.4", "", "", ""]]);
        var parsed = AssurancePolicy.Parse(Mapping, table);
        Assert.Equal(["2.1.1", "2.1.2"], parsed.Items.Select(i => i.QuestionRef).ToArray());
        Assert.Null(parsed.Items[0].ObservationCategory); // an unstated category stays absent, never defaulted
        Assert.Equal([(3, "QUESTION_REF_INVALID"), (4, "OBSERVATION_TEXT_MISSING"), (5, "OBSERVATION_TYPE_INVALID")], parsed.Issues.Select(i => (i.Row, i.Code)).ToArray());
        Assert.True(AssurancePolicy.IsFinding(parsed.Items[0])); Assert.False(AssurancePolicy.IsFinding(parsed.Items[1]));
        Assert.Equal("HEADER_MISSING:observation_text", AssurancePolicy.Parse(Mapping, new ImportTable(["Question", "Type", "Category"], [["2.1.1", "NONE", ""]])).Issues.Single().Code);
    }

    [Fact]
    [Trait("Requirement", "DEV-113")]
    public void Re_import_never_overwrites_blindly_every_changed_item_needs_an_explicit_decision_and_absent_items_are_kept()
    {
        var a = new AssuranceItemContent("2.1.1", null, "NEGATIVE", null, "FICTIONAL original", null);
        var b = new AssuranceItemContent("2.1.2", null, "NONE", null, null, null);
        var current = new Dictionary<string, (AssuranceItemContent, long)> { ["2.1.1"] = (a, 1), ["2.1.2"] = (b, 1), ["2.1.9"] = (b with { QuestionRef = "2.1.9" }, 3) };
        var incoming = new[] { a with { ObservationText = "FICTIONAL revised" }, b, new AssuranceItemContent("3.1.1", null, "POSITIVE", null, "FICTIONAL good", null) };
        var diff = AssurancePolicy.Diff(current, incoming);
        Assert.Equal([("2.1.1", "CHANGED"), ("2.1.2", "UNCHANGED"), ("2.1.9", "ABSENT"), ("3.1.1", "NEW")], diff.Select(d => (d.QuestionRef, d.Change)).ToArray());
        Assert.Equal(["observation_text"], diff[0].Fields); Assert.Equal(1, diff[0].CurrentRevision);
        Assert.Equal(["DECISION_MISSING:2.1.1"], AssurancePolicy.AcceptanceIssues(diff, [], []));
        Assert.Empty(AssurancePolicy.AcceptanceIssues(diff, [], ["2.1.1"]));
        Assert.Contains("NOT_A_CHANGED_ITEM:3.1.1", AssurancePolicy.AcceptanceIssues(diff, ["2.1.1", "3.1.1"], []));
        Assert.Contains("DUPLICATE_DECISION", AssurancePolicy.AcceptanceIssues(diff, ["2.1.1"], ["2.1.1"]));
        Assert.Equal(AssurancePolicy.DiffHash(diff, incoming), AssurancePolicy.DiffHash(AssurancePolicy.Diff(current, incoming), incoming));
        Assert.DoesNotContain("CLOSED", AssurancePolicy.FindingStates);
    }

    [Fact]
    [Trait("Requirement", "DEV-064")]
    public void Pack_manifest_is_order_independent_labelled_and_superseded_evidence_is_a_distinct_pinned_state()
    {
        Guid e = Guid.NewGuid(), c = Guid.NewGuid(), vessel = Guid.NewGuid();
        var items = new[] {
            new AuditPackEntry("Evidence", e, 2, "Current", new string('A', 64), e, c, Now),
            new AuditPackEntry("Evidence", e, 1, "Superseded", new string('B', 64), e, c, Now.AddDays(-1)),
            new AuditPackEntry("Requirement", Guid.NewGuid(), 3, "Unavailable", null, e, c, null),
            new AuditPackEntry("Decision", Guid.NewGuid(), 1, "Recorded", new string('C', 64), e, c, Now) };
        Assert.All(items, i => Assert.True(AuditPackPolicy.Valid(i)));
        Assert.Equal(AuditPackPolicy.ManifestHash(vessel, Now, items), AuditPackPolicy.ManifestHash(vessel, Now, items.Reverse()));
        Assert.NotEqual(AuditPackPolicy.ManifestHash(vessel, Now, items), AuditPackPolicy.ManifestHash(vessel, Now, items.Take(3)));
        Assert.False(AuditPackPolicy.Valid(items[1] with { Kind = "Control" }));     // only evidence is superseded in a pack
        Assert.False(AuditPackPolicy.Valid(items[2] with { Hash = new string('D', 64) })); // unavailable has no invented hash
        Assert.False(AuditPackPolicy.Valid(items[3] with { Kind = "Evidence" }));    // Recorded = human verification/decision only
        Assert.Equal("SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD", AuditPackPolicy.Label);
        Assert.Equal("Match", AuditPackPolicy.IntegrityOf("ab", "AB", true));
        Assert.Equal("Mismatch", AuditPackPolicy.IntegrityOf("ab", "CD", true));
        Assert.Equal("SourceUnavailable", AuditPackPolicy.IntegrityOf("ab", null, true));
    }
}
