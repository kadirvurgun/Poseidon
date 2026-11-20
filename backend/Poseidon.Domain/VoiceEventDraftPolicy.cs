using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV-062 / T-S9-005 (18.01 Backlog row 63): voice-to-event draft. A short Turkish/English note is
// turned into a structured SAFETY EVENT DRAFT CANDIDATE by a deterministic, in-process rule set; no
// external LLM, cloud speech or prompt is involved. Every proposed field carries its exact transcript
// span(s), a fixed rule confidence, the rule id and the extractor/rule-set version. A value that the
// note does not state is NOT proposed (no default type, no assumed date, no invented vessel/location);
// the vessel comes from the server scope, never from the text. The user edits and confirms; a
// confirmation is a human record that makes the candidate ready for a later hand-off to the owning
// safety event draft. There is no submission, closure or event creation here (no autonomous effect).
public sealed record VoiceDraftSpan(int Start, int End)
{ public override string ToString() => nameof(VoiceDraftSpan); }

public sealed record VoiceDraftCandidateField(string Key, string Value, VoiceDraftSpan[] Spans, decimal Confidence, string Rule)
{ public override string ToString() => nameof(VoiceDraftCandidateField); }

public sealed record VoiceDraftHint(string Code, VoiceDraftSpan Span)
{ public override string ToString() => nameof(VoiceDraftHint); }

public sealed record VoiceDraftCandidate(string Protocol, string ExtractorVersion, string RuleSetVersion, string TranscriptSha256,
    VoiceDraftCandidateField[] Fields, VoiceDraftHint[] Hints)
{ public override string ToString() => nameof(VoiceDraftCandidate); }

// Current user-owned field values. Origin: EXTRACTED (unchanged candidate), USER_EDITED (candidate changed),
// USER_ENTERED (no candidate existed). The candidate itself stays immutable on the draft.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record VoiceDraftFields(string? EventType, string? OccurredAt, string? Location, string? LocationDetail, string? Description, string? ImmediateAction)
{ public override string ToString() => nameof(VoiceDraftFields); }

public sealed record VoiceDraftFieldCorrection(string Key, string Outcome, bool Required)
{ public override string ToString() => nameof(VoiceDraftFieldCorrection); }

public static class VoiceEventDraftPolicy
{
    public const string Protocol = "POSEIDON_VOICE_EVENT_DRAFT_V1";
    public const string ExtractorVersion = "voice-rule-extractor/1.0.0";
    public const string RuleSetVersion = "voice-event-rules-tr-en/2026-11-20";
    public const string Draft = "DRAFT", Confirmed = "CONFIRMED", Rejected = "REJECTED";
    public static readonly string[] States = [Draft, Confirmed, Rejected];
    public static readonly string[] ReasonCodes = ["CREATE", "EDIT", "CONFIRM", "REJECT"];
    public static readonly string[] Origins = ["TYPED", "LOCAL_SPEECH"];
    // ISM Code 9.1: accidents, hazardous situations and non-conformities are reported; a near miss is a
    // hazardous occurrence that did not result in loss. No default: the note must state it or the user selects.
    public static readonly string[] EventTypes = ["NEAR_MISS", "INCIDENT", "HAZARDOUS_SITUATION", "NON_CONFORMITY"];
    public static readonly string[] Locations = ["ENGINE_ROOM", "BRIDGE", "MAIN_DECK", "FORECASTLE", "AFT_DECK", "CARGO_HOLD", "PUMP_ROOM",
        "GALLEY", "ACCOMMODATION", "GANGWAY", "MOORING_STATION", "STEERING_GEAR_ROOM", "TOWING_DECK", "OTHER"];
    public static readonly string[] RejectionReasons = ["TRANSCRIPT_WRONG", "FIELDS_WRONG", "NOT_AN_EVENT", "DUPLICATE", "OTHER"];
    public static readonly string[] FieldKeys = ["eventType", "occurredAt", "location", "locationDetail", "description", "immediateAction"];
    public static readonly string[] RequiredKeys = ["eventType", "occurredAt", "location", "description", "immediateAction"];
    public static readonly string[] CorrectionOutcomes = ["ACCEPTED", "CORRECTED", "ADDED", "NOT_PROVIDED"];
    public const int MaxTranscript = 4000, MaxText = 2000, MaxDetail = 200, MaxEvidence = 10, MaxNote = 500;
    public const string ReadPermission = "voice.draft.read", WritePermission = "voice.draft.write";

    // ---- text folding: per-character so spans index the original transcript ----
    private static char Fold(char c) => c switch
    {
        'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'I' or 'İ' or 'i' => 'i', 'ö' or 'Ö' => 'o', 'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u',
        'â' or 'Â' => 'a', 'î' or 'Î' => 'i', 'û' or 'Û' => 'u', '’' or '\'' => '\'', _ => char.ToLowerInvariant(c)
    };
    public static string Folded(string text)
    {
        var chars = new char[text.Length];
        for (var i = 0; i < text.Length; i++) chars[i] = Fold(text[i]);
        return new string(chars);
    }

    public static bool ValidTranscript(string? value) => value != null && value.Trim().Length > 0 && value.Length <= MaxTranscript &&
        value.IsNormalized(NormalizationForm.FormC) && !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    public static bool ValidText(string? value, int max) => value == null || value.Trim().Length > 0 && value.Length <= max &&
        !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'));
    public static bool TryOccurredAt(string? value, out DateTime local) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    public static bool Valid(VoiceDraftFields? f) => f != null &&
        (f.EventType == null || EventTypes.Contains(f.EventType, StringComparer.Ordinal)) &&
        (f.OccurredAt == null || TryOccurredAt(f.OccurredAt, out _)) &&
        (f.Location == null || Locations.Contains(f.Location, StringComparer.Ordinal)) &&
        ValidText(f.LocationDetail, MaxDetail) && ValidText(f.Description, MaxText) && ValidText(f.ImmediateAction, MaxText) &&
        // OTHER needs the user's own words; a code alone would hide where it happened.
        (f.Location != "OTHER" || f.LocationDetail != null);

    public static string? Value(VoiceDraftFields f, string key) => key switch
    {
        "eventType" => f.EventType, "occurredAt" => f.OccurredAt, "location" => f.Location, "locationDetail" => f.LocationDetail,
        "description" => f.Description, "immediateAction" => f.ImmediateAction, _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    // POL-DEFAULT-01: a draft may be incomplete; confirmation is blocked with the missing list.
    public static string[] Missing(VoiceDraftFields f) => RequiredKeys.Where(k => string.IsNullOrWhiteSpace(Value(f, k))).ToArray();

    public static VoiceDraftFields FromCandidate(VoiceDraftCandidate c)
    {
        string? V(string key) => c.Fields.SingleOrDefault(x => x.Key == key)?.Value;
        return new(V("eventType"), V("occurredAt"), V("location"), null, V("description"), V("immediateAction"));
    }

    public static string Origin(VoiceDraftCandidate c, VoiceDraftFields f, string key)
    {
        var candidate = c.Fields.SingleOrDefault(x => x.Key == key)?.Value; var value = Value(f, key);
        return value == null ? "NONE" : candidate == null ? "USER_ENTERED" : string.Equals(candidate, value, StringComparison.Ordinal) ? "EXTRACTED" : "USER_EDITED";
    }

    // DEV-063: the confirmation records, per field, whether the extracted candidate was accepted,
    // corrected, added by the user or not provided. This is the measured correction signal.
    public static VoiceDraftFieldCorrection[] Corrections(VoiceDraftCandidate c, VoiceDraftFields f) => FieldKeys.Select(k =>
    {
        var candidate = c.Fields.SingleOrDefault(x => x.Key == k)?.Value; var value = Value(f, k);
        var outcome = candidate == null ? value == null ? "NOT_PROVIDED" : "ADDED" : string.Equals(candidate, value, StringComparison.Ordinal) ? "ACCEPTED" : "CORRECTED";
        return new VoiceDraftFieldCorrection(k, outcome, RequiredKeys.Contains(k));
    }).ToArray();

    // ---- deterministic rule set ----
    private sealed record Term(string Value, string Folded, decimal Confidence, string Rule);
    private static Term T(string value, string phrase, decimal confidence, string rule) => new(value, Folded(phrase), confidence, rule);

    // Explicit classification phrases outrank consequence words. Folded (ASCII) forms.
    private static readonly Term[] TypeTerms =
    [
        T("NEAR_MISS", "ramak kala", 0.90m, "TYPE_EXPLICIT"), T("NEAR_MISS", "near miss", 0.90m, "TYPE_EXPLICIT"), T("NEAR_MISS", "near-miss", 0.90m, "TYPE_EXPLICIT"),
        T("NON_CONFORMITY", "uygunsuzluk", 0.90m, "TYPE_EXPLICIT"), T("NON_CONFORMITY", "non-conformity", 0.90m, "TYPE_EXPLICIT"), T("NON_CONFORMITY", "nonconformity", 0.90m, "TYPE_EXPLICIT"),
        T("NON_CONFORMITY", "non conformity", 0.90m, "TYPE_EXPLICIT"),
        T("HAZARDOUS_SITUATION", "tehlikeli durum", 0.85m, "TYPE_EXPLICIT"), T("HAZARDOUS_SITUATION", "hazardous situation", 0.85m, "TYPE_EXPLICIT"),
        T("HAZARDOUS_SITUATION", "hazardous condition", 0.85m, "TYPE_EXPLICIT"), T("HAZARDOUS_SITUATION", "unsafe condition", 0.80m, "TYPE_EXPLICIT"),
        T("INCIDENT", "kaza", 0.75m, "TYPE_CONSEQUENCE"), T("INCIDENT", "accident", 0.75m, "TYPE_CONSEQUENCE"), T("INCIDENT", "incident", 0.75m, "TYPE_CONSEQUENCE"),
        T("INCIDENT", "yaralandi", 0.70m, "TYPE_CONSEQUENCE"), T("INCIDENT", "injured", 0.70m, "TYPE_CONSEQUENCE"), T("INCIDENT", "yangin cikti", 0.70m, "TYPE_CONSEQUENCE"),
        T("INCIDENT", "fire broke out", 0.70m, "TYPE_CONSEQUENCE"),
    ];
    // Longest phrase wins at a position; stems are matched as word prefixes ("makine dairesinde").
    private static readonly Term[] LocationTerms =
    [
        T("ENGINE_ROOM", "makine dairesi", 0.85m, "LOCATION_LEXICON"), T("ENGINE_ROOM", "engine room", 0.85m, "LOCATION_LEXICON"),
        T("BRIDGE", "kopru ustu", 0.85m, "LOCATION_LEXICON"), T("BRIDGE", "kopruustu", 0.85m, "LOCATION_LEXICON"), T("BRIDGE", "navigation bridge", 0.85m, "LOCATION_LEXICON"),
        T("BRIDGE", "bridge", 0.75m, "LOCATION_LEXICON"),
        T("MAIN_DECK", "ana guverte", 0.85m, "LOCATION_LEXICON"), T("MAIN_DECK", "main deck", 0.85m, "LOCATION_LEXICON"),
        T("FORECASTLE", "bas kasara", 0.85m, "LOCATION_LEXICON"), T("FORECASTLE", "forecastle", 0.85m, "LOCATION_LEXICON"), T("FORECASTLE", "pruva", 0.75m, "LOCATION_LEXICON"),
        T("AFT_DECK", "kic guverte", 0.85m, "LOCATION_LEXICON"), T("AFT_DECK", "poop deck", 0.85m, "LOCATION_LEXICON"), T("AFT_DECK", "aft deck", 0.85m, "LOCATION_LEXICON"),
        T("CARGO_HOLD", "ambar", 0.80m, "LOCATION_LEXICON"), T("CARGO_HOLD", "cargo hold", 0.85m, "LOCATION_LEXICON"),
        T("PUMP_ROOM", "pompa dairesi", 0.85m, "LOCATION_LEXICON"), T("PUMP_ROOM", "pump room", 0.85m, "LOCATION_LEXICON"),
        T("GALLEY", "mutfak", 0.80m, "LOCATION_LEXICON"), T("GALLEY", "kuzine", 0.80m, "LOCATION_LEXICON"), T("GALLEY", "galley", 0.85m, "LOCATION_LEXICON"),
        T("ACCOMMODATION", "yasam mahalli", 0.80m, "LOCATION_LEXICON"), T("ACCOMMODATION", "accommodation", 0.75m, "LOCATION_LEXICON"),
        T("GANGWAY", "borda iskelesi", 0.85m, "LOCATION_LEXICON"), T("GANGWAY", "iskele merdiveni", 0.85m, "LOCATION_LEXICON"), T("GANGWAY", "gangway", 0.85m, "LOCATION_LEXICON"),
        T("GANGWAY", "accommodation ladder", 0.85m, "LOCATION_LEXICON"),
        T("MOORING_STATION", "manevra istasyonu", 0.80m, "LOCATION_LEXICON"), T("MOORING_STATION", "mooring station", 0.85m, "LOCATION_LEXICON"),
        T("STEERING_GEAR_ROOM", "dumen dairesi", 0.85m, "LOCATION_LEXICON"), T("STEERING_GEAR_ROOM", "steering gear room", 0.85m, "LOCATION_LEXICON"),
        T("TOWING_DECK", "cekme guvertesi", 0.85m, "LOCATION_LEXICON"), T("TOWING_DECK", "towing deck", 0.85m, "LOCATION_LEXICON"),
    ];
    private static readonly string[] ActionStems = ["durdur", "kapat", "izole", "haber ver", "bildir", "temizle", "sondur", "tahliye", "ilk yardim", "kordon", "sabitle",
        "stopped", "shut", "isolat", "informed", "notified", "reported to", "cleaned", "extinguish", "evacuat", "first aid", "cordon", "secured", "barricad"];
    private static readonly string[] EvidenceStems = ["fotograf", "foto ", "resim", "video", "photo", "picture"];
    private static readonly string[] Negations = ["no one", "nobody", "not ", "without", "kimse", "hic kimse"];

    private static bool Boundary(string folded, int index) => index <= 0 || index >= folded.Length || !char.IsLetterOrDigit(folded[index]);
    private static IEnumerable<int> Find(string folded, string term, bool prefix)
    {
        for (var i = folded.IndexOf(term, StringComparison.Ordinal); i >= 0; i = folded.IndexOf(term, i + 1, StringComparison.Ordinal))
            if (Boundary(folded, i - 1) && (prefix || Boundary(folded, i + term.Length))) yield return i;
    }

    private static readonly Regex Clock = new(@"(?<![\d:.])(?:saat\s*|at\s*)?([01]?\d|2[0-3])[:.]([0-5]\d)(?![\d:]|\.\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Compact = new(@"(?:saat|at)\s+([01]\d|2[0-3])([0-5]\d)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private static readonly Regex Date = new(@"(?<!\d)(0?[1-9]|[12]\d|3[01])[./](0?[1-9]|1[0-2])[./](20\d\d)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private static readonly (string Folded, int Days)[] Relative = [("bugun", 0), ("bu sabah", 0), ("bu aksam", 0), ("today", 0), ("this morning", 0), ("this evening", 0), ("tonight", 0),
        ("dun", -1), ("dun gece", -1), ("yesterday", -1), ("last night", -1)];

    public static VoiceDraftCandidate Extract(string transcript, string transcriptSha256, DateOnly referenceDate)
    {
        if (!ValidTranscript(transcript)) throw new ArgumentException("voice_transcript_invalid", nameof(transcript));
        var folded = Folded(transcript);
        var fields = new List<VoiceDraftCandidateField>();
        var hints = new List<VoiceDraftHint>();

        // Event type: explicit phrases first; consequence words only when not negated nearby.
        VoiceDraftCandidateField? type = null;
        foreach (var term in TypeTerms)
        {
            foreach (var at in Find(folded, term.Folded, term.Rule == "TYPE_CONSEQUENCE"))
            {
                if (term.Rule == "TYPE_CONSEQUENCE")
                {
                    var window = folded[Math.Max(0, at - 24)..at];
                    if (Negations.Any(n => window.Contains(n, StringComparison.Ordinal))) continue;
                    if (term.Folded == "yaralandi" && at + term.Folded.Length < folded.Length && char.IsLetter(folded[at + term.Folded.Length])) continue;
                }
                var candidate = new VoiceDraftCandidateField("eventType", term.Value, [new(at, at + term.Folded.Length)], term.Confidence, term.Rule);
                if (type == null || term.Rule == "TYPE_EXPLICIT" && type.Rule != "TYPE_EXPLICIT") type = candidate;
                break;
            }
            if (type?.Rule == "TYPE_EXPLICIT") break;
        }
        // Two different explicit classes in one note are ambiguous: propose nothing (user decides).
        if (type != null && type.Rule == "TYPE_EXPLICIT" && TypeTerms.Where(t => t.Rule == "TYPE_EXPLICIT" && t.Value != type.Value).Any(t => Find(folded, t.Folded, false).Any()))
            type = null;
        if (type != null) fields.Add(type);

        // Occurred at: a stated date (explicit or relative to the note's reference date) AND a stated clock time.
        var clock = Clock.Match(folded) is { Success: true } m1 ? m1 : Compact.Match(folded) is { Success: true } m2 ? m2 : null;
        DateOnly? day = null; VoiceDraftSpan? daySpan = null; var dayRule = "";
        if (Date.Match(folded) is { Success: true } d && DateOnly.TryParseExact($"{d.Groups[3].Value}-{int.Parse(d.Groups[2].Value, CultureInfo.InvariantCulture):00}-{int.Parse(d.Groups[1].Value, CultureInfo.InvariantCulture):00}",
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var explicitDay))
        { day = explicitDay; daySpan = new(d.Index, d.Index + d.Length); dayRule = "DATE_EXPLICIT"; }
        else
        {
            foreach (var (word, days) in Relative.OrderByDescending(r => r.Folded.Length))
                if (Find(folded, word, false).FirstOrDefault(-1) is var at && at >= 0)
                { day = referenceDate.AddDays(days); daySpan = new(at, at + word.Length); dayRule = "DATE_RELATIVE_TO_NOTE"; break; }
        }
        if (clock != null && day != null)
        {
            var hour = int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture); var minute = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
            var value = new DateTime(day.Value, new TimeOnly(hour, minute)).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            VoiceDraftSpan[] spans = [daySpan!, new(clock.Index, clock.Index + clock.Length)];
            fields.Add(new("occurredAt", value, [.. spans.OrderBy(s => s.Start)], dayRule == "DATE_EXPLICIT" ? 0.90m : 0.80m, dayRule + "+CLOCK"));
        }
        else if (clock != null) hints.Add(new("TIME_WITHOUT_DATE", new(clock.Index, clock.Index + clock.Length)));
        else if (daySpan != null) hints.Add(new("DATE_WITHOUT_TIME", daySpan));

        // Location: lexicon, longest phrase at the earliest position; several distinct places lower confidence.
        var hits = LocationTerms.SelectMany(t => Find(folded, t.Folded, true).Select(at => (Term: t, At: at)))
            .OrderBy(h => h.At).ThenByDescending(h => h.Term.Folded.Length).ToList();
        var places = new List<(Term Term, int At)>();
        foreach (var hit in hits)
            if (!places.Any(p => hit.At < p.At + p.Term.Folded.Length && p.At < hit.At + hit.Term.Folded.Length)) places.Add(hit);
        if (places.Count > 0)
        {
            var first = places[0]; var distinct = places.Select(p => p.Term.Value).Distinct().Count();
            fields.Add(new("location", first.Term.Value, [new(first.At, first.At + first.Term.Folded.Length)],
                distinct == 1 ? first.Term.Confidence : 0.50m, distinct == 1 ? first.Term.Rule : "LOCATION_FIRST_OF_SEVERAL"));
        }

        // Description: the note itself, verbatim (never paraphrased).
        var start = transcript.Length - transcript.TrimStart().Length; var end = transcript.TrimEnd().Length;
        fields.Add(new("description", transcript[start..end], [new(start, end)], 1.00m, "VERBATIM_TRANSCRIPT"));

        // Immediate action: verbatim sentences that contain an action verb stem.
        var sentences = new List<VoiceDraftSpan>(); var s = 0;
        for (var i = 0; i <= transcript.Length; i++)
            if (i == transcript.Length || transcript[i] is '.' or '!' or '?' or '\n' or ';')
            {
                var a = s; var b = i;
                while (a < b && char.IsWhiteSpace(transcript[a])) a++;
                while (b > a && char.IsWhiteSpace(transcript[b - 1])) b--;
                if (b > a) sentences.Add(new(a, b));
                s = i + 1;
            }
        var actions = sentences.Where(x => ActionStems.Any(stem => Find(folded[x.Start..x.End], stem, true).Any())).ToArray();
        if (actions.Length > 0)
            fields.Add(new("immediateAction", string.Join(" ", actions.Select(x => transcript[x.Start..x.End].TrimEnd(',') + ".")).Replace("..", ".", StringComparison.Ordinal),
                actions, 0.70m, "ACTION_VERB_SENTENCE"));

        // Evidence: a mention is a hint only. No evidence reference is ever fabricated from text.
        foreach (var stem in EvidenceStems)
            if (Find(folded, stem, true).FirstOrDefault(-1) is var at && at >= 0) { hints.Add(new("EVIDENCE_MENTIONED", new(at, at + stem.TrimEnd().Length))); break; }

        return new(Protocol, ExtractorVersion, RuleSetVersion, transcriptSha256, [.. fields], [.. hints]);
    }

    // Grounding check used by the evaluator and by tests: every proposed span lies in the transcript and,
    // for coded values, the span text is a lexicon phrase for exactly that code.
    public static bool Grounded(string transcript, VoiceDraftCandidateField field)
    {
        if (field.Spans.Length == 0 || field.Spans.Any(s => s.Start < 0 || s.End > transcript.Length || s.End <= s.Start)) return false;
        var folded = Folded(transcript);
        string Text(VoiceDraftSpan s) => folded[s.Start..s.End];
        return field.Key switch
        {
            "eventType" => TypeTerms.Any(t => t.Value == field.Value && Text(field.Spans[0]) == t.Folded),
            "location" => LocationTerms.Any(t => t.Value == field.Value && Text(field.Spans[0]) == t.Folded),
            "description" => transcript[field.Spans[0].Start..field.Spans[0].End] == field.Value,
            "immediateAction" => field.Spans.All(s => field.Value.Contains(transcript[s.Start..s.End].TrimEnd(','), StringComparison.Ordinal)),
            "occurredAt" => field.Spans.Length == 2,
            _ => false
        };
    }
}
