using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// DEV045/T-S6-003. In-process, dependency-free reader for the embedded text
// layer of digital PDFs. It is NOT image OCR: scanned/image-only documents,
// encrypted files and unsupported encodings are explicit unavailable outcomes,
// never empty "successful" extractions. Bounded by bytes, pages, objects,
// inflated stream size and emitted lines; nothing here logs or persists text.
public sealed class CertificateTextLayerUnavailableException(string reason) : Exception("certificate_text_layer_unavailable")
{
    public const string ImageOcrUnavailable = "IMAGE_OCR_UNAVAILABLE";
    public const string TextLayerUnavailable = "TEXT_LAYER_UNAVAILABLE";
    public const string PdfUnsupported = "PDF_UNSUPPORTED";
    public string Reason { get; } = reason;
}

/// <summary>Normalized (0..1) source region with a top-left origin on the stated page.</summary>
public sealed record CertificateSourceRegion(decimal X, decimal Y, decimal Width, decimal Height)
{
    public bool Valid() => X is >= 0 and <= 1 && Y is >= 0 and <= 1 && Width is >= 0 and <= 1 && Height is >= 0 and <= 1 && X + Width <= 1.0001m && Y + Height <= 1.0001m;
}

public sealed record CertificateTextLine(int Page, string Text, CertificateSourceRegion Region)
{
    public override string ToString() => nameof(CertificateTextLine);
}

public static class CertificatePdfTextLayer
{
    public const int MaxPages = 50, MaxObjects = 20000, MaxLines = 4000, MaxRunsPerPage = 8000;
    public const int MaxInflatedBytes = 8 * 1024 * 1024;
    private static readonly Regex ObjectHeader = new(@"(\d{1,7})\s+(\d{1,5})\s+obj\b", RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(2));
    private static readonly Regex Reference = new(@"(\d{1,7})\s+\d{1,5}\s+R\b", RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private sealed class PdfObject(string dictionary, byte[]? stream)
    {
        public string Dictionary { get; } = dictionary;
        public byte[]? Stream { get; } = stream;
    }
    private sealed record Font(Dictionary<string, string>? Map, int CodeBytes, bool Undecodable);
    private sealed record Run(string Text, double X, double Y, double Size, double Width);
    private readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);
        public Matrix Times(Matrix m) => new(A * m.A + B * m.C, A * m.B + B * m.D, C * m.A + D * m.C, C * m.B + D * m.D, E * m.A + F * m.C + m.E, E * m.B + F * m.D + m.F);
    }

    public static IReadOnlyList<CertificateTextLine> Read(byte[] bytes, CancellationToken ct = default)
    {
        if (bytes is not { Length: > 8 }) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
        // Latin1 is a lossless 1:1 byte view; offsets match the byte array.
        var raw = Encoding.Latin1.GetString(bytes);
        if (raw.IndexOf("%PDF-", 0, Math.Min(raw.Length, 1024), StringComparison.Ordinal) < 0) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
        if (raw.Contains("/Encrypt", StringComparison.Ordinal)) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
        var budget = new int[] { MaxInflatedBytes };
        var objects = Objects(raw, bytes, budget, ct);
        var pages = Pages(objects);
        if (pages.Count is 0 or > MaxPages) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
        var lines = new List<CertificateTextLine>();
        for (var index = 0; index < pages.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var page = pages[index];
            var box = MediaBox(objects, page);
            var fonts = Fonts(objects, page);
            var runs = new List<Run>();
            foreach (var content in Contents(objects, page, budget)) Interpret(content, fonts, runs, ct);
            foreach (var line in Lines(runs, index + 1, box))
            {
                if (lines.Count >= MaxLines) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
                lines.Add(line);
            }
        }
        if (lines.Sum(l => l.Text.Count(char.IsLetterOrDigit)) < 8) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.TextLayerUnavailable);
        return lines;
    }

    private static Dictionary<int, PdfObject> Objects(string raw, byte[] bytes, int[] budget, CancellationToken ct)
    {
        var objects = new Dictionary<int, PdfObject>();
        var streams = new List<PdfObject>();
        var position = 0;
        while (position < raw.Length)
        {
            ct.ThrowIfCancellationRequested();
            var match = ObjectHeader.Match(raw, position);
            if (!match.Success) break;
            if (objects.Count > MaxObjects) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var cursor = SkipWhitespace(raw, match.Index + match.Length);
            string dictionary;
            byte[]? stream = null;
            if (cursor + 1 < raw.Length && raw[cursor] == '<' && raw[cursor + 1] == '<')
            {
                var end = DictionaryEnd(raw, cursor);
                if (end < 0) break;
                dictionary = raw[cursor..end];
                var after = SkipWhitespace(raw, end);
                if (string.CompareOrdinal(raw, after, "stream", 0, 6) == 0)
                {
                    var start = after + 6;
                    if (start < raw.Length && raw[start] == '\r') start++;
                    if (start < raw.Length && raw[start] == '\n') start++;
                    var length = DirectInt(dictionary, "/Length");
                    var stop = length is > 0 && start + length.Value <= raw.Length && raw.IndexOf("endstream", start + length.Value, Math.Min(64, raw.Length - start - length.Value), StringComparison.Ordinal) >= 0
                        ? start + length.Value : raw.IndexOf("endstream", start, StringComparison.Ordinal);
                    if (stop < 0) break;
                    var data = bytes.AsSpan(start, stop - start).ToArray();
                    stream = Decode(dictionary, data, budget);
                    position = stop + 9;
                }
                else position = end;
            }
            else
            {
                var stop = raw.IndexOf("endobj", cursor, StringComparison.Ordinal);
                if (stop < 0) break;
                dictionary = raw[cursor..stop];
                position = stop + 6;
            }
            // Incremental updates: a later body definition supersedes an earlier one.
            var item = new PdfObject(dictionary, stream);
            objects[number] = item;
            if (stream != null) streams.Add(item);
        }
        // Compressed object streams (PDF 1.5+). Direct definitions take precedence.
        foreach (var container in streams.Where(s => Name(s.Dictionary, "/Type") == "ObjStm").ToArray())
        {
            var count = DirectInt(container.Dictionary, "/N");
            var first = DirectInt(container.Dictionary, "/First");
            if (count is not > 0 || first is not >= 0 || container.Stream!.Length < first) continue;
            var text = Encoding.Latin1.GetString(container.Stream!);
            var header = text[..first.Value].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (header.Length < count * 2) continue;
            for (var i = 0; i < count; i++)
            {
                if (!int.TryParse(header[2 * i], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                    !int.TryParse(header[2 * i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)) break;
                var start = first.Value + offset;
                var stop = i + 1 < count && int.TryParse(header[2 * i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out var next) ? first.Value + next : text.Length;
                if (start < 0 || start > text.Length || stop < start || stop > text.Length) break;
                if (!objects.ContainsKey(number)) objects[number] = new PdfObject(text[start..stop], null);
                if (objects.Count > MaxObjects) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
            }
        }
        return objects;
    }

    private static byte[]? Decode(string dictionary, byte[] data, int[] budget)
    {
        var filter = Regex.Match(dictionary, @"/Filter\s*(\[\s*)?/(\w+)(\s*\])?", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!filter.Success) return data;
        if (filter.Groups[2].Value != "FlateDecode" || Regex.IsMatch(dictionary, @"/Filter\s*\[\s*/\w+\s*/\w+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) return null;
        try
        {
            using var input = new MemoryStream(data);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[16384];
            int read;
            while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
            {
                budget[0] -= read;
                if (budget[0] < 0) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        catch (InvalidDataException) { return null; }
    }

    private static List<int> Pages(Dictionary<int, PdfObject> objects)
    {
        var pages = new List<int>();
        var roots = objects.Where(o => Name(o.Value.Dictionary, "/Type") == "Pages" && !o.Value.Dictionary.Contains("/Parent", StringComparison.Ordinal)).Select(o => o.Key).Order().ToArray();
        var visited = new HashSet<int>();
        void Walk(int id, int depth)
        {
            if (depth > 32 || !visited.Add(id) || !objects.TryGetValue(id, out var node) || pages.Count > MaxPages) return;
            var type = Name(node.Dictionary, "/Type");
            if (type == "Page") { pages.Add(id); return; }
            if (type != "Pages") return;
            var kids = Array(node.Dictionary, "/Kids");
            if (kids == null) return;
            foreach (Match kid in Reference.Matches(kids)) Walk(int.Parse(kid.Groups[1].Value, CultureInfo.InvariantCulture), depth + 1);
        }
        foreach (var root in roots) Walk(root, 0);
        if (pages.Count == 0) pages.AddRange(objects.Where(o => Name(o.Value.Dictionary, "/Type") == "Page").Select(o => o.Key).Order());
        return pages;
    }

    private static string? Inherited(Dictionary<int, PdfObject> objects, int page, string key)
    {
        var current = page;
        for (var depth = 0; depth < 32 && objects.TryGetValue(current, out var node); depth++)
        {
            var at = KeyIndex(node.Dictionary, key);
            if (at >= 0) return ValueAt(node.Dictionary, at + key.Length);
            var parent = RefAfter(node.Dictionary, "/Parent");
            if (parent == null) return null;
            current = parent.Value;
        }
        return null;
    }

    private static (double X, double Y, double W, double H) MediaBox(Dictionary<int, PdfObject> objects, int page)
    {
        var value = Inherited(objects, page, "/MediaBox");
        if (value != null && Reference.Match(value) is { Success: true } r && !value.TrimStart().StartsWith('[') && objects.TryGetValue(int.Parse(r.Groups[1].Value, CultureInfo.InvariantCulture), out var target)) value = target.Dictionary;
        var numbers = value == null ? [] : Regex.Matches(value, @"-?\d+(\.\d+)?", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
        if (numbers.Length >= 4 && numbers[2] > numbers[0] && numbers[3] > numbers[1]) return (numbers[0], numbers[1], numbers[2] - numbers[0], numbers[3] - numbers[1]);
        return (0, 0, 612, 792);
    }

    private static Dictionary<string, Font> Fonts(Dictionary<int, PdfObject> objects, int page)
    {
        var fonts = new Dictionary<string, Font>(StringComparer.Ordinal);
        var resources = Inherited(objects, page, "/Resources");
        if (resources == null) return fonts;
        resources = Resolve(objects, resources);
        var fontValue = KeyIndex(resources, "/Font") is var at and >= 0 ? ValueAt(resources, at + 5) : null;
        if (fontValue == null) return fonts;
        fontValue = Resolve(objects, fontValue);
        foreach (Match entry in Regex.Matches(fontValue, @"/([^\s/<>\[\]()]+)\s+(\d{1,7})\s+\d{1,5}\s+R", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            if (!objects.TryGetValue(int.Parse(entry.Groups[2].Value, CultureInfo.InvariantCulture), out var font)) continue;
            var identity = Regex.IsMatch(font.Dictionary, @"/Encoding\s*/Identity-[HV]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var map = RefAfter(font.Dictionary, "/ToUnicode") is { } cmap && objects.TryGetValue(cmap, out var stream) && stream.Stream != null ? CMap(stream.Stream) : null;
            var codeBytes = map is { Count: > 0 } ? map.Keys.Max(k => k.Length) / 2 : identity ? 2 : 1;
            fonts[entry.Groups[1].Value] = new Font(map, Math.Clamp(codeBytes, 1, 4), identity && map is not { Count: > 0 });
        }
        return fonts;
    }

    private static Dictionary<string, string>? CMap(byte[] data)
    {
        var text = Encoding.Latin1.GetString(data);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match block in Regex.Matches(text, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            foreach (Match pair in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]*)>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                map[pair.Groups[1].Value.ToUpperInvariant()] = Utf16(pair.Groups[2].Value);
        foreach (Match block in Regex.Matches(text, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            foreach (Match range in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(<([0-9A-Fa-f]*)>|\[([^\]]*)\])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                var width = range.Groups[1].Value.Length;
                var low = Convert.ToInt64(range.Groups[1].Value, 16);
                var high = Convert.ToInt64(range.Groups[2].Value, 16);
                if (high < low || high - low > 65535) continue;
                if (range.Groups[4].Success)
                {
                    var start = range.Groups[4].Value;
                    if (start.Length < 4) continue;
                    var prefix = Utf16(start[..^4]);
                    var last = Convert.ToInt32(start[^4..], 16);
                    for (var code = low; code <= high; code++) map[code.ToString("X" + width, CultureInfo.InvariantCulture)] = prefix + (char)(last + (int)(code - low));
                }
                else
                {
                    var targets = Regex.Matches(range.Groups[5].Value, @"<([0-9A-Fa-f]*)>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    for (var i = 0; i < targets.Count && low + i <= high; i++) map[(low + i).ToString("X" + width, CultureInfo.InvariantCulture)] = Utf16(targets[i].Groups[1].Value);
                }
                if (map.Count > 70000) return null;
            }
        return map;
    }

    private static string Utf16(string hex)
    {
        if (hex.Length % 4 != 0) return "";
        var builder = new StringBuilder();
        for (var i = 0; i + 4 <= hex.Length; i += 4) builder.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));
        return builder.ToString();
    }

    private static IEnumerable<byte[]> Contents(Dictionary<int, PdfObject> objects, int page, int[] budget)
    {
        var node = objects[page].Dictionary;
        var at = KeyIndex(node, "/Contents");
        if (at < 0) yield break;
        var value = ValueAt(node, at + 9) ?? "";
        if (!value.TrimStart().StartsWith('[') && Reference.Match(value) is { Success: true } single &&
            objects.TryGetValue(int.Parse(single.Groups[1].Value, CultureInfo.InvariantCulture), out var direct) && direct.Stream == null && direct.Dictionary.TrimStart().StartsWith('['))
            value = direct.Dictionary; // indirect array of content streams
        foreach (Match reference in Reference.Matches(value))
            if (objects.TryGetValue(int.Parse(reference.Groups[1].Value, CultureInfo.InvariantCulture), out var content) && content.Stream != null) yield return content.Stream;
    }

    private static void Interpret(byte[] content, Dictionary<string, Font> fonts, List<Run> runs, CancellationToken ct)
    {
        var operands = new List<object>();
        var graphics = new Stack<Matrix>();
        var ctm = Matrix.Identity;
        Matrix tm = Matrix.Identity, tlm = Matrix.Identity;
        double size = 12, leading = 0;
        Font? font = null;
        var i = 0;
        var steps = 0;
        void Show(byte[] text, double tx)
        {
            if (runs.Count >= MaxRunsPerPage) throw new CertificateTextLayerUnavailableException(CertificateTextLayerUnavailableException.PdfUnsupported);
            var decoded = DecodeText(text, font);
            var glyphs = font?.CodeBytes is > 1 ? text.Length / font.CodeBytes : text.Length;
            var width = glyphs * size * 0.5 + tx;
            var m = tm.Times(ctm);
            var scale = Math.Sqrt(m.C * m.C + m.D * m.D);
            if (decoded.Length > 0) runs.Add(new Run(decoded, m.E, m.F, size * (scale > 0 ? scale : 1), width * Math.Sqrt(m.A * m.A + m.B * m.B)));
            tm = new Matrix(1, 0, 0, 1, width, 0).Times(tm);
        }
        double Number(int back) => operands.Count >= back && operands[^back] is double d ? d : 0;
        while (i < content.Length)
        {
            if (++steps % 4096 == 0) ct.ThrowIfCancellationRequested();
            var c = content[i];
            if (c is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\f' or 0) { i++; continue; }
            if (c == (byte)'%') { while (i < content.Length && content[i] is not ((byte)'\n' or (byte)'\r')) i++; continue; }
            if (c == (byte)'(') { operands.Add(Literal(content, ref i)); continue; }
            if (c == (byte)'<' && i + 1 < content.Length && content[i + 1] == (byte)'<') { i += 2; continue; }
            if (c == (byte)'>' && i + 1 < content.Length && content[i + 1] == (byte)'>') { i += 2; continue; }
            if (c == (byte)'<') { operands.Add(Hex(content, ref i)); continue; }
            if (c == (byte)'[') { operands.Add("["); i++; continue; }
            if (c == (byte)']') { operands.Add("]"); i++; continue; }
            if (c is (byte)'{' or (byte)'}' or (byte)')' or (byte)'>') { i++; continue; }
            var start = i;
            if (c == (byte)'/') i++;
            while (i < content.Length && content[i] is not ((byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\f' or 0 or (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'/' or (byte)'%' or (byte)'{' or (byte)'}')) i++;
            var token = Encoding.Latin1.GetString(content, start, i - start);
            if (token.Length == 0) { i++; continue; }
            if (token[0] == '/') { operands.Add(token); continue; }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) { operands.Add(number); continue; }
            switch (token)
            {
                case "q": if (graphics.Count < 64) graphics.Push(ctm); break;
                case "Q": if (graphics.Count > 0) ctm = graphics.Pop(); break;
                case "cm": if (operands.Count >= 6) ctm = new Matrix(Number(6), Number(5), Number(4), Number(3), Number(2), Number(1)).Times(ctm); break;
                case "BT": tm = tlm = Matrix.Identity; break;
                case "Tf": size = Math.Abs(Number(1)) is > 0 and < 2000 and var s ? s : size; font = operands.Count >= 2 && operands[^2] is string name && name.StartsWith('/') ? fonts.GetValueOrDefault(name[1..]) : null; break;
                case "TL": leading = Number(1); break;
                case "Td": tlm = new Matrix(1, 0, 0, 1, Number(2), Number(1)).Times(tlm); tm = tlm; break;
                case "TD": leading = -Number(1); tlm = new Matrix(1, 0, 0, 1, Number(2), Number(1)).Times(tlm); tm = tlm; break;
                case "Tm": if (operands.Count >= 6) { tlm = new Matrix(Number(6), Number(5), Number(4), Number(3), Number(2), Number(1)); tm = tlm; } break;
                case "T*": tlm = new Matrix(1, 0, 0, 1, 0, -leading).Times(tlm); tm = tlm; break;
                case "Tj": if (operands.Count > 0 && operands[^1] is byte[] tj) Show(tj, 0); break;
                case "'": tlm = new Matrix(1, 0, 0, 1, 0, -leading).Times(tlm); tm = tlm; if (operands.Count > 0 && operands[^1] is byte[] q1) Show(q1, 0); break;
                case "\"": tlm = new Matrix(1, 0, 0, 1, 0, -leading).Times(tlm); tm = tlm; if (operands.Count > 0 && operands[^1] is byte[] q2) Show(q2, 0); break;
                case "TJ":
                    {
                        var open = operands.LastIndexOf("[");
                        if (open < 0) break;
                        using var joined = new MemoryStream();
                        var spaces = new List<int>();
                        double kern = 0;
                        foreach (var part in operands.Skip(open + 1))
                        {
                            if (part is byte[] chunk) { joined.Write(chunk); }
                            else if (part is double adjust) { kern -= adjust / 1000 * size; if (adjust < -180) spaces.Add((int)joined.Length); }
                        }
                        var bytes = joined.ToArray();
                        if (spaces.Count == 0) Show(bytes, kern);
                        else
                        {
                            // Large negative kerning is a word gap: emit an explicit space.
                            var code = font?.CodeBytes ?? 1;
                            var builder = new List<byte>();
                            var cursor = 0;
                            foreach (var at in spaces.Where(s => s > 0 && s < bytes.Length && s % code == 0))
                            {
                                builder.AddRange(bytes[cursor..at]);
                                if (code == 1) builder.Add((byte)' ');
                                cursor = at;
                            }
                            builder.AddRange(bytes[cursor..]);
                            Show(builder.ToArray(), kern);
                        }
                        break;
                    }
                case "BI":
                    {
                        // Inline image data is binary; skip to the EI terminator.
                        var id = IndexOf(content, " ID", i);
                        var end = id < 0 ? -1 : IndexOf(content, "EI", id + 3);
                        while (end > 0 && !(IsWhite(content, end - 1) && (end + 2 >= content.Length || IsWhite(content, end + 2)))) end = IndexOf(content, "EI", end + 2);
                        i = end < 0 ? content.Length : end + 2;
                        break;
                    }
            }
            operands.Clear();
        }
    }

    private static bool IsWhite(byte[] data, int at) => at >= 0 && at < data.Length && data[at] is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\f' or 0;
    private static int IndexOf(byte[] data, string needle, int from) => from >= data.Length ? -1 : data.AsSpan(from).IndexOf(Encoding.Latin1.GetBytes(needle)) is var at and >= 0 ? at + from : -1;

    private static byte[] Literal(byte[] content, ref int i)
    {
        var output = new List<byte>();
        var depth = 0;
        i++;
        while (i < content.Length)
        {
            var c = content[i++];
            if (c == (byte)'\\' && i < content.Length)
            {
                var e = content[i++];
                switch (e)
                {
                    case (byte)'n': output.Add(10); break;
                    case (byte)'r': output.Add(13); break;
                    case (byte)'t': output.Add(9); break;
                    case (byte)'b': output.Add(8); break;
                    case (byte)'f': output.Add(12); break;
                    case (byte)'\r': if (i < content.Length && content[i] == (byte)'\n') i++; break;
                    case (byte)'\n': break;
                    case >= (byte)'0' and <= (byte)'7':
                        {
                            var value = e - '0';
                            for (var k = 0; k < 2 && i < content.Length && content[i] is >= (byte)'0' and <= (byte)'7'; k++) value = value * 8 + content[i++] - '0';
                            output.Add((byte)value);
                            break;
                        }
                    default: output.Add(e); break;
                }
                continue;
            }
            if (c == (byte)'(') depth++;
            else if (c == (byte)')') { if (depth == 0) break; depth--; }
            output.Add(c);
        }
        return output.ToArray();
    }

    private static byte[] Hex(byte[] content, ref int i)
    {
        var digits = new StringBuilder();
        i++;
        while (i < content.Length && content[i] != (byte)'>')
        {
            if (Uri.IsHexDigit((char)content[i])) digits.Append((char)content[i]);
            i++;
        }
        i++;
        if (digits.Length % 2 == 1) digits.Append('0');
        return Convert.FromHexString(digits.ToString());
    }

    private static string DecodeText(byte[] text, Font? font)
    {
        if (font?.Undecodable == true) return "";
        var builder = new StringBuilder(text.Length);
        if (font?.Map is { Count: > 0 } map)
        {
            var width = font.CodeBytes;
            for (var i = 0; i + width <= text.Length; i += width)
            {
                var key = Convert.ToHexString(text, i, width);
                if (map.TryGetValue(key, out var mapped)) builder.Append(mapped);
                else if (width == 1 && text[i] == (byte)' ') builder.Append(' ');
            }
        }
        else
        {
            foreach (var b in text) builder.Append(b switch
            {
                0x80 => '€', 0x91 => '‘', 0x92 => '’', 0x93 => '“', 0x94 => '”', 0x96 => '–', 0x97 => '—',
                < 0x20 or (>= 0x7F and < 0xA0) => '�',
                _ => (char)b,
            });
        }
        var result = builder.ToString();
        return result.Any(ch => ch == '�' || char.IsControl(ch)) ? new string(result.Where(ch => ch != '�' && !char.IsControl(ch)).ToArray()) : result;
    }

    private static IEnumerable<CertificateTextLine> Lines(List<Run> runs, int page, (double X, double Y, double W, double H) box)
    {
        var ordered = runs.Where(r => r.Text.Trim().Length > 0).OrderByDescending(r => r.Y).ThenBy(r => r.X).ToList();
        var groups = new List<List<Run>>();
        foreach (var run in ordered)
        {
            var group = groups.LastOrDefault();
            if (group != null && Math.Abs(group[0].Y - run.Y) <= Math.Max(1.0, Math.Min(group[0].Size, run.Size) * 0.5)) group.Add(run);
            else groups.Add([run]);
        }
        foreach (var group in groups)
        {
            var sorted = group.OrderBy(r => r.X).ToList();
            var builder = new StringBuilder();
            double? previousEnd = null;
            foreach (var run in sorted)
            {
                if (previousEnd is { } end && run.X - end > run.Size * 0.2 && builder.Length > 0 && builder[^1] != ' ' && run.Text[0] != ' ') builder.Append(' ');
                builder.Append(run.Text);
                previousEnd = Math.Max(previousEnd ?? double.MinValue, run.X + run.Width);
            }
            var text = Regex.Replace(builder.ToString(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Trim();
            if (text.Length == 0) continue;
            var left = sorted.Min(r => r.X);
            var right = sorted.Max(r => r.X + r.Width);
            var size = sorted.Max(r => r.Size);
            var baseline = sorted.Average(r => r.Y);
            static decimal Clamp(double value) => (decimal)Math.Round(Math.Clamp(value, 0, 1), 4);
            var x = Clamp((left - box.X) / box.W);
            var y = Clamp((box.Y + box.H - (baseline + size)) / box.H);
            var width = Math.Min(Clamp((right - left) / box.W), 1 - x);
            var height = Math.Min(Clamp(size * 1.2 / box.H), 1 - y);
            yield return new CertificateTextLine(page, text.Length > 2000 ? text[..2000] : text, new CertificateSourceRegion(x, y, width, height));
        }
    }

    // ---- minimal dictionary helpers (bounded, regex-timeout protected) ----
    private static int SkipWhitespace(string raw, int at)
    {
        while (at < raw.Length && raw[at] is ' ' or '\n' or '\r' or '\t' or '\f' or '\0') at++;
        return at;
    }

    private static int DictionaryEnd(string raw, int start)
    {
        var depth = 0;
        for (var i = start; i < raw.Length - 1; i++)
        {
            var c = raw[i];
            if (c == '(')
            {
                var nesting = 0;
                for (i++; i < raw.Length; i++)
                {
                    if (raw[i] == '\\') { i++; continue; }
                    if (raw[i] == '(') nesting++;
                    else if (raw[i] == ')') { if (nesting == 0) break; nesting--; }
                }
                continue;
            }
            if (c == '<' && raw[i + 1] == '<') { depth++; i++; continue; }
            if (c == '>' && raw[i + 1] == '>') { depth--; i++; if (depth == 0) return i + 1; continue; }
            if (c == '<') { var close = raw.IndexOf('>', i); if (close < 0) return -1; i = close; }
        }
        return -1;
    }

    /// <summary>Index of a top-level key in a dictionary (depth 1 only).</summary>
    private static int KeyIndex(string dictionary, string key)
    {
        var depth = 0;
        for (var i = 0; i < dictionary.Length; i++)
        {
            var c = dictionary[i];
            if (c == '(') { var nest = 0; for (i++; i < dictionary.Length; i++) { if (dictionary[i] == '\\') { i++; continue; } if (dictionary[i] == '(') nest++; else if (dictionary[i] == ')') { if (nest == 0) break; nest--; } } continue; }
            if (c == '<' && i + 1 < dictionary.Length && dictionary[i + 1] == '<') { depth++; i++; continue; }
            if (c == '>' && i + 1 < dictionary.Length && dictionary[i + 1] == '>') { depth--; i++; continue; }
            if (c == '/' && depth <= 1 && string.CompareOrdinal(dictionary, i, key, 0, key.Length) == 0 &&
                (i + key.Length >= dictionary.Length || !char.IsLetterOrDigit(dictionary[i + key.Length]) && dictionary[i + key.Length] != '-')) return i;
        }
        return -1;
    }

    private static string? ValueAt(string dictionary, int at)
    {
        at = SkipWhitespace(dictionary, at);
        if (at >= dictionary.Length) return null;
        if (dictionary[at] == '<' && at + 1 < dictionary.Length && dictionary[at + 1] == '<')
        {
            var end = DictionaryEnd(dictionary, at);
            return end < 0 ? null : dictionary[at..end];
        }
        if (dictionary[at] == '[')
        {
            var depth = 0;
            for (var i = at; i < dictionary.Length; i++)
            {
                if (dictionary[i] == '[') depth++;
                else if (dictionary[i] == ']' && --depth == 0) return dictionary[at..(i + 1)];
            }
            return null;
        }
        var reference = Regex.Match(dictionary[at..], @"^\d{1,7}\s+\d{1,5}\s+R\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (reference.Success) return reference.Value;
        var token = Regex.Match(dictionary[at..], @"^/?[^\s/<>\[\]()]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return token.Success ? token.Value : null;
    }

    private static string Resolve(Dictionary<int, PdfObject> objects, string value)
    {
        var trimmed = value.Trim();
        var match = Regex.Match(trimmed, @"^(\d{1,7})\s+\d{1,5}\s+R$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success && objects.TryGetValue(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), out var target) ? target.Dictionary : value;
    }

    private static string? Name(string dictionary, string key)
    {
        var at = KeyIndex(dictionary, key);
        var value = at < 0 ? null : ValueAt(dictionary, at + key.Length);
        return value is { Length: > 1 } && value[0] == '/' ? value[1..] : null;
    }

    private static string? Array(string dictionary, string key)
    {
        var at = KeyIndex(dictionary, key);
        return at < 0 ? null : ValueAt(dictionary, at + key.Length);
    }

    private static int? RefAfter(string dictionary, string key)
    {
        var at = KeyIndex(dictionary, key);
        var value = at < 0 ? null : ValueAt(dictionary, at + key.Length);
        return value != null && Regex.Match(value, @"^(\d{1,7})\s+\d{1,5}\s+R$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static int? DirectInt(string dictionary, string key)
    {
        var at = KeyIndex(dictionary, key);
        var value = at < 0 ? null : ValueAt(dictionary, at + key.Length);
        return value != null && !value.EndsWith('R') && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}
