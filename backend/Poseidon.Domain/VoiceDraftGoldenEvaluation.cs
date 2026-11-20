namespace Poseidon.Domain;

// DEV-063 / T-S9-006 (18.01 Backlog row 64): voice draft evaluation. Scorer software over a MASKED,
// SYNTHETIC set of realistic maritime notes; it is not an operational corpus or a measured field
// accuracy. Measures field completeness, correction rate, usability and fabricated facts. A
// "fabricated" fact is a proposal for a field the note does not state, or a proposal whose span is
// not grounded in the transcript. Critical fabricated facts must be zero (threshold test). Synthetic
// reviewer rejections are counted by reason so the rejection signal is part of the evaluation.
public sealed record VoiceGoldenExample(string Id, string Masking, string Language, string Transcript, DateOnly ReferenceDate,
    Dictionary<string, string?> Truth, string[] CriticalFields, string ReviewDecision, string? RejectionReason);

public sealed record VoiceGoldenFieldScore(string Key, int TruthPresent, int Proposed, int Correct, int Corrections, int Fabricated, int CriticalFabricated,
    decimal? Completeness, decimal? CorrectionRate);

public sealed record VoiceGoldenCaseScore(string Id, int RequiredPresent, int RequiredProposedCorrect, int Proposed, int Corrections, int Fabricated, int CriticalFabricated,
    bool UsableWithoutCorrection, string ReviewDecision, string? RejectionReason);

public sealed record VoiceGoldenReport(string DatasetVersion, string ExtractorVersion, string RuleSetVersion, string CorpusKind, string State,
    VoiceGoldenCaseScore[] Cases, VoiceGoldenFieldScore[] Fields, decimal? FieldCompleteness, decimal? CorrectionRate, decimal? Usability,
    int FabricatedFacts, int CriticalFabricatedFacts, int Rejections, Dictionary<string, int> RejectionsByReason);

public static class VoiceDraftGoldenEvaluation
{
    // Fields that are scored. Description is verbatim by construction and locationDetail is never extracted.
    public static readonly string[] ScoredKeys = ["eventType", "occurredAt", "location", "immediateAction"];

    private static decimal? Rate(int numerator, int denominator) => denominator == 0 ? null : (decimal)numerator / denominator;

    public static VoiceGoldenReport Evaluate(string datasetVersion, IReadOnlyList<VoiceGoldenExample> examples)
    {
        if (string.IsNullOrWhiteSpace(datasetVersion) || datasetVersion.Length > 80 || examples.Count is < 1 or > 256 ||
            examples.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() != examples.Count ||
            examples.Any(e => e.Masking != "SYNTHETIC_MASKED" || e.Language is not ("tr" or "en") || !VoiceEventDraftPolicy.ValidTranscript(e.Transcript) || e.Truth == null ||
                !e.Truth.Keys.Order(StringComparer.Ordinal).SequenceEqual(ScoredKeys.Order(StringComparer.Ordinal)) || e.CriticalFields.Any(f => !ScoredKeys.Contains(f)) ||
                e.ReviewDecision is not ("CONFIRM" or "REJECT") || (e.ReviewDecision == "REJECT") != (e.RejectionReason != null) ||
                e.RejectionReason != null && !VoiceEventDraftPolicy.RejectionReasons.Contains(e.RejectionReason)))
            throw new InvalidDataException("voice_golden_dataset_contract_unavailable");
        var rows = new List<(string Key, bool Present, bool Proposed, bool Correct, bool Fabricated, bool Critical)>();
        var cases = new List<VoiceGoldenCaseScore>();
        foreach (var e in examples)
        {
            var candidate = VoiceEventDraftPolicy.Extract(e.Transcript, new string('0', 64), e.ReferenceDate);
            var caseRows = ScoredKeys.Select(key =>
            {
                var proposal = candidate.Fields.SingleOrDefault(f => f.Key == key); var truth = e.Truth[key];
                var correct = proposal != null && string.Equals(proposal.Value, truth, StringComparison.Ordinal);
                var fabricated = proposal != null && (truth == null || !VoiceEventDraftPolicy.Grounded(e.Transcript, proposal));
                return (Key: key, Present: truth != null, Proposed: proposal != null, Correct: correct, Fabricated: fabricated, Critical: e.CriticalFields.Contains(key));
            }).ToArray();
            rows.AddRange(caseRows);
            var required = caseRows.Where(r => r.Present).ToArray();
            cases.Add(new(e.Id, required.Length, required.Count(r => r.Correct), caseRows.Count(r => r.Proposed), caseRows.Count(r => r.Proposed && !r.Correct),
                caseRows.Count(r => r.Fabricated), caseRows.Count(r => r.Fabricated && r.Critical),
                caseRows.All(r => r.Present ? r.Correct : !r.Proposed), e.ReviewDecision, e.RejectionReason));
        }
        var fields = ScoredKeys.Select(key =>
        {
            var f = rows.Where(r => r.Key == key).ToArray();
            var present = f.Count(r => r.Present); var proposed = f.Count(r => r.Proposed); var correct = f.Count(r => r.Correct);
            return new VoiceGoldenFieldScore(key, present, proposed, correct, proposed - correct, f.Count(r => r.Fabricated), f.Count(r => r.Fabricated && r.Critical),
                Rate(f.Count(r => r.Present && r.Correct), present), Rate(proposed - correct, proposed));
        }).ToArray();
        var rejections = examples.Where(e => e.ReviewDecision == "REJECT").GroupBy(e => e.RejectionReason!).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count());
        return new(datasetVersion, VoiceEventDraftPolicy.ExtractorVersion, VoiceEventDraftPolicy.RuleSetVersion, "SYNTHETIC_MASKED", "SYNTHETIC_SOFTWARE_EVALUATION_ONLY",
            [.. cases], fields, Rate(rows.Count(r => r.Present && r.Correct), rows.Count(r => r.Present)),
            Rate(rows.Count(r => r.Proposed && !r.Correct), rows.Count(r => r.Proposed)), Rate(cases.Count(c => c.UsableWithoutCorrection), cases.Count),
            rows.Count(r => r.Fabricated), rows.Count(r => r.Fabricated && r.Critical), rejections.Values.Sum(), rejections);
    }

    // Operational observation over persisted confirmations/rejections (not an accuracy claim).
    public sealed record Observation(int Confirmed, int Rejected, int Open, decimal? FieldCompleteness, decimal? CorrectionRate, decimal? Usability, decimal? RejectionRate,
        Dictionary<string, int> RejectionsByReason);

    public static Observation Observe(IReadOnlyList<VoiceDraftFieldCorrection[]> confirmed, IReadOnlyList<string> rejectionReasons, int open)
    {
        var required = confirmed.SelectMany(c => c.Where(x => x.Required)).ToArray();
        var proposed = confirmed.SelectMany(c => c.Where(x => x.Outcome is "ACCEPTED" or "CORRECTED")).ToArray();
        return new(confirmed.Count, rejectionReasons.Count, open,
            Rate(required.Count(x => x.Outcome is "ACCEPTED" or "CORRECTED"), required.Length),
            Rate(proposed.Count(x => x.Outcome == "CORRECTED"), proposed.Length),
            Rate(confirmed.Count(c => c.All(x => x.Outcome is "ACCEPTED" or "NOT_PROVIDED")), confirmed.Count),
            Rate(rejectionReasons.Count, confirmed.Count + rejectionReasons.Count),
            rejectionReasons.GroupBy(r => r).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()));
    }
}
