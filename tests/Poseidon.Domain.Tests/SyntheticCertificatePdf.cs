using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Poseidon.Domain.Tests;

// DEV046 test helper: writes small, fictional PDFs from the golden fixture layout
// so the golden set is reproducible text rather than opaque binary files. It
// covers WinAnsi literal strings, Type0 Identity-H with/without ToUnicode, Flate,
// TJ kerning, cm scaling, object streams and image-only pages. Not a general PDF
// writer and not evidence about third-party generators.
public sealed record SyntheticPdfLine(double X, double Y, double Size, string Text);
public sealed record SyntheticPdfOptions(string Encoding = "winansi", bool Compress = true, bool ObjectStream = false, string TextMode = "tj", double Scale = 1, bool ImageOnly = false);

public static class SyntheticCertificatePdf
{
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static byte[] Build(IReadOnlyList<IReadOnlyList<SyntheticPdfLine>> pages, SyntheticPdfOptions options)
    {
        var codes = new Dictionary<char, int>();
        foreach (var ch in pages.SelectMany(p => p).SelectMany(l => l.Text).Distinct()) codes[ch] = codes.Count + 1;
        var objects = new List<(int Id, string Body, byte[]? Stream)>();
        var nextId = 1;
        int Reserve() => nextId++;
        var catalog = Reserve();
        var pagesId = Reserve();
        var font = Reserve();
        int? cmap = options.Encoding == "tounicode" ? Reserve() : null;
        var pageIds = pages.Select(_ => Reserve()).ToArray();
        var contentIds = pages.Select(_ => Reserve()).ToArray();
        objects.Add((catalog, $"<< /Type /Catalog /Pages {pagesId} 0 R >>", null));
        objects.Add((pagesId, $"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Length} >>", null));
        objects.Add((font, options.Encoding == "winansi"
            ? "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"
            : $"<< /Type /Font /Subtype /Type0 /BaseFont /SyntheticSans /Encoding /Identity-H{(cmap is { } c ? $" /ToUnicode {c} 0 R" : "")} >>", null));
        if (cmap is { } cmapId)
        {
            var map = new StringBuilder("/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Synthetic def\n1 begincodespacerange <0000> <FFFF> endcodespacerange\n");
            foreach (var chunk in codes.Chunk(100))
            {
                map.Append(CultureInfo.InvariantCulture, $"{chunk.Length} beginbfchar\n");
                foreach (var (ch, code) in chunk) map.Append(CultureInfo.InvariantCulture, $"<{code:X4}> <{(int)ch:X4}>\n");
                map.Append("endbfchar\n");
            }
            map.Append("endcmap CMapName currentdict /CMap defineresource pop end end\n");
            objects.Add((cmapId, "", Encoding.ASCII.GetBytes(map.ToString())));
        }
        for (var p = 0; p < pages.Count; p++)
        {
            objects.Add((pageIds[p], $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentIds[p]} 0 R >>", null));
            var content = new StringBuilder();
            if (options.ImageOnly) content.Append("q 468 0 0 600 72 96 cm /Im1 Do Q\n");
            if (options.Scale != 1) content.Append(CultureInfo.InvariantCulture, $"q {N(options.Scale)} 0 0 {N(options.Scale)} 0 0 cm\n");
            foreach (var line in pages[p])
            {
                var x = line.X / options.Scale;
                var y = line.Y / options.Scale;
                content.Append(CultureInfo.InvariantCulture, $"BT /F1 {N(line.Size)} Tf {N(x)} {N(y)} Td ");
                if (options.TextMode == "kern")
                    content.Append('[').Append(string.Join(" -260 ", line.Text.Split(' ').Select(word => Encode(word, options.Encoding, codes)))).Append("] TJ");
                else content.Append(Encode(line.Text, options.Encoding, codes)).Append(" Tj");
                content.Append(" ET\n");
            }
            if (options.Scale != 1) content.Append("Q\n");
            objects.Add((contentIds[p], "", Encoding.Latin1.GetBytes(content.ToString())));
        }
        return Write(objects, options);
    }

    private static string Encode(string text, string encoding, Dictionary<char, int> codes)
    {
        if (encoding != "winansi") return "<" + string.Concat(text.Select(ch => codes[ch].ToString("X4", CultureInfo.InvariantCulture))) + ">";
        var builder = new StringBuilder("(");
        foreach (var ch in text)
        {
            if (ch > 0xFF) throw new ArgumentException("winansi fixture text must be Latin-1");
            builder.Append(ch switch { '(' => "\\(", ')' => "\\)", '\\' => "\\\\", _ => ch.ToString() });
        }
        return builder.Append(')').ToString();
    }

    private static byte[] Write(List<(int Id, string Body, byte[]? Stream)> objects, SyntheticPdfOptions options)
    {
        using var output = new MemoryStream();
        void Ascii(string value) => output.Write(Encoding.Latin1.GetBytes(value));
        Ascii("%PDF-1.7\n%\xE2\xE3\xCF\xD3\n");
        var offsets = new SortedDictionary<int, long>();
        var packed = options.ObjectStream ? objects.Where(o => o.Stream == null).ToList() : [];
        var direct = objects.Where(o => !packed.Contains(o)).ToList();
        foreach (var (id, body, stream) in direct)
        {
            offsets[id] = output.Position;
            if (stream == null) { Ascii($"{id} 0 obj\n{body}\nendobj\n"); continue; }
            var data = options.Compress ? Deflate(stream) : stream;
            Ascii($"{id} 0 obj\n<< /Length {data.Length}{(options.Compress ? " /Filter /FlateDecode" : "")} >>\nstream\n");
            output.Write(data);
            Ascii("\nendstream\nendobj\n");
        }
        if (packed.Count > 0)
        {
            var header = new StringBuilder();
            var bodies = new StringBuilder();
            foreach (var (id, body, _) in packed)
            {
                header.Append(CultureInfo.InvariantCulture, $"{id} {bodies.Length} ");
                bodies.Append(body).Append('\n');
            }
            var first = header.Length;
            var data = Deflate(Encoding.Latin1.GetBytes(header.ToString() + bodies));
            var id2 = objects.Max(o => o.Id) + 1;
            offsets[id2] = output.Position;
            Ascii($"{id2} 0 obj\n<< /Type /ObjStm /N {packed.Count} /First {first} /Length {data.Length} /Filter /FlateDecode >>\nstream\n");
            output.Write(data);
            Ascii("\nendstream\nendobj\n");
        }
        // Classic xref for direct objects; packed objects are synthetic-only.
        var xref = output.Position;
        var size = offsets.Keys.Max() + 1;
        Ascii($"xref\n0 {size}\n0000000000 65535 f \n");
        for (var id = 1; id < size; id++) Ascii(offsets.TryGetValue(id, out var at) ? $"{at:D10} 00000 n \n" : "0000000000 65535 f \n");
        Ascii($"trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(data);
        return output.ToArray();
    }
}
