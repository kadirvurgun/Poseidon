using System.Globalization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV045/T-S6-003. Deterministic, in-process candidate recognizer over a digital
// PDF text layer. Label rules only: document text is data and can never add a
// field, change authority or mark anything verified. Confidence values are fixed
// rule scores (see Score*), NOT calibrated probabilities; every output remains a
// REVIEW_REQUIRED candidate after CertificateExtractionPolicy.Project.
public static class CertificateTextLayerEngine
{
    public const string EngineVersion = "poseidon-pdf-text-layer-1.0.0";
    public const string RuleSetVersion = "poseidon-certificate-label-rules-1.0.0";
    public const decimal ScoreSameLine = 0.92m, ScoreNextLine = 0.78m, ScoreDateNormalized = 0.85m, ScoreDateUnparsed = 0.50m, ScoreAmbiguous = 0.40m;

    private static readonly (string Field, string[] Labels)[] Rules =
    [
        ("certificate_type", ["Type of Certificate", "Certificate Type", "Certificate Title", "Sertifika Türü", "Sertifika Turu", "Belge Türü"]),
        ("certificate_no", ["Certificate Number", "Certificate No.", "Certificate No", "Cert. No.", "Cert No", "Sertifika Numarası", "Sertifika No"]),
        ("issuer", ["Issuing Authority", "Issued by", "Issuer", "Düzenleyen Kurum", "Düzenleyen"]),
        ("issue_date", ["Date of Issue", "Issue Date", "Issued on", "Düzenleme Tarihi", "Veriliş Tarihi"]),
        ("expiry_date", ["Date of Expiry", "Expiry Date", "Expiration Date", "Valid until", "Son Geçerlilik Tarihi", "Geçerlilik Tarihi"]),
        ("vessel_reference", ["Name of Ship", "Name of Vessel", "Vessel Name", "Ship Name", "Gemi Adı"]),
        ("person_reference", ["Name of Holder", "Holder Name", "Seafarer Name", "Holder", "Sahibi"]),
    ];
    private static readonly Regex LabelPattern = new(
        @"(?<![\p{L}\p{N}])(" + string.Join("|", Rules.SelectMany(r => r.Labels).OrderByDescending(l => l.Length).Select(Regex.Escape)) + @")(?![\p{L}\p{N}])\s*[:：]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly string[] Months = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    public sealed record Hit(string Field, string Value, int Page, CertificateSourceRegion Region, bool NextLine)
    {
        public override string ToString() => nameof(Hit);
    }

    /// <summary>Extract candidates from bytes. Non-PDF input is an explicit IMAGE_OCR_UNAVAILABLE capability gap.</summary>
    public static CertificateRawExtraction Extract(byte[] bytes, string mime, string checksum, CancellationToken ct = default)
    {
        if (mime != "application/pdf") throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.ImageOcrUnavailable);
        var lines = CertificatePdfTextLayer.Read(bytes, ct);
        return Recognize(lines, checksum);
    }

    public static CertificateRawExtraction Recognize(IReadOnlyList<CertificateTextLine> lines, string checksum)
    {
        var hits = Hits(lines);
        var fields = new Dictionary<string, CertificateRawCandidate>(StringComparer.Ordinal);
        foreach (var group in hits.GroupBy(h => h.Field))
        {
            var normalized = group.Select(h => (Hit: h, Date: IsDate(h.Field) ? NormalizeDate(h.Value) : (Value: h.Value, Ambiguous: false, Parsed: true))).ToList();
            var distinct = normalized.Select(n => n.Date.Value.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count();
            var first = normalized[0];
            var ambiguous = distinct > 1 || first.Date.Ambiguous;
            decimal confidence = first.Hit.NextLine ? ScoreNextLine : ScoreSameLine;
            if (IsDate(first.Hit.Field))
            {
                if (!first.Date.Parsed) confidence = Math.Min(confidence, ScoreDateUnparsed);
                else if (first.Date.Value != first.Hit.Value) confidence = Math.Min(confidence, ScoreDateNormalized);
            }
            if (ambiguous) confidence = Math.Min(confidence, ScoreAmbiguous);
            var value = first.Date.Value.Length > 512 ? first.Date.Value[..512] : first.Date.Value;
            fields[group.Key] = new CertificateRawCandidate(value, confidence, first.Hit.Page, first.Hit.Region, ambiguous);
        }
        return new CertificateRawExtraction(CertificateExtractionPolicy.Protocol, EngineVersion, RuleSetVersion, checksum, fields);
    }

    public static List<Hit> Hits(IReadOnlyList<CertificateTextLine> lines)
    {
        var hits = new List<Hit>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            // Single-word labels ("Holder", "Issuer") need an explicit colon so prose
            // such as "the holder of this certificate" is not read as a field.
            var labels = LabelPattern.Matches(line.Text).Where(m => m.Groups[1].Value.Contains(' ') || m.Value.TrimEnd().EndsWith(':') || m.Value.TrimEnd().EndsWith('：')).ToList();
            for (var k = 0; k < labels.Count; k++)
            {
                var label = labels[k];
                var field = FieldOf(label.Groups[1].Value);
                if (field == null) continue;
                var end = k + 1 < labels.Count ? labels[k + 1].Index : line.Text.Length;
                var value = Clean(line.Text[(label.Index + label.Length)..end]);
                var nextLine = false;
                var region = line.Region;
                var page = line.Page;
                if (value.Length == 0 && k + 1 == labels.Count && index + 1 < lines.Count && lines[index + 1].Page == line.Page && !LabelPattern.Matches(lines[index + 1].Text).Any(m => m.Groups[1].Value.Contains(' ') || m.Value.TrimEnd().EndsWith(':')))
                {
                    value = Clean(lines[index + 1].Text);
                    region = lines[index + 1].Region;
                    nextLine = true;
                }
                if (value.Length is 0 or > 200 || !value.Any(char.IsLetterOrDigit)) continue;
                hits.Add(new Hit(field, value, page, region, nextLine));
            }
        }
        return hits;
    }

    private static string? FieldOf(string label) => Rules.FirstOrDefault(r => r.Labels.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase))).Field;
    private static bool IsDate(string field) => field is "issue_date" or "expiry_date";
    private static string Clean(string value) => value.Trim().TrimStart(':', '：', '-', '–').TrimEnd(';', ',', '.', '|').Trim();

    /// <summary>Normalize a printed date to ISO yyyy-MM-dd. Day/month-order uncertainty is reported, never guessed silently.</summary>
    public static (string Value, bool Ambiguous, bool Parsed) NormalizeDate(string value)
    {
        var text = value.Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso)) return (iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false, true);
        var numeric = Regex.Match(text, @"^(\d{1,2})([./-])(\d{1,2})\2(\d{4})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (numeric.Success)
        {
            var a = int.Parse(numeric.Groups[1].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(numeric.Groups[3].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(numeric.Groups[4].Value, CultureInfo.InvariantCulture);
            // Dotted dates follow the day-first convention; slash dates are only
            // unambiguous when one component exceeds 12.
            var ambiguous = numeric.Groups[2].Value == "/" && a <= 12 && b <= 12 && a != b;
            var (day, month) = numeric.Groups[2].Value == "/" && a <= 12 && b > 12 ? (b, a) : (a, b);
            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(Math.Clamp(year, 1, 9999), month) && year is >= 1900 and <= 2200)
                return (new DateOnly(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ambiguous, true);
            return (text, false, false);
        }
        var named = Regex.Match(text, @"^(\d{1,2})(?:st|nd|rd|th)?\s+([A-Za-z]{3,9})\.?,?\s+(\d{4})$|^([A-Za-z]{3,9})\.?\s+(\d{1,2})(?:st|nd|rd|th)?,?\s+(\d{4})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (named.Success)
        {
            var dayText = named.Groups[1].Success ? named.Groups[1].Value : named.Groups[5].Value;
            var monthText = (named.Groups[2].Success ? named.Groups[2].Value : named.Groups[4].Value).ToLowerInvariant();
            var yearText = named.Groups[3].Success ? named.Groups[3].Value : named.Groups[6].Value;
            var month = Array.FindIndex(Months, m => m == monthText || monthText.Length == 3 && m.StartsWith(monthText, StringComparison.Ordinal)) + 1;
            var day = int.Parse(dayText, CultureInfo.InvariantCulture);
            var year = int.Parse(yearText, CultureInfo.InvariantCulture);
            if (month >= 1 && year is >= 1900 and <= 2200 && day >= 1 && day <= DateTime.DaysInMonth(year, month)) return (new DateOnly(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), false, true);
        }
        return (text, false, false);
    }
}
