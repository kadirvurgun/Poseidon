using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Poseidon.Domain;

public sealed record ImportTable(string[] Headers, string[][] Rows)
{
    public override string ToString() => nameof(ImportTable);
}
public sealed class ImportInputException : Exception
{
    public ImportInputException() : base("invalid_import_input") { }
}

// Source screen excel-migration: deliberately bounded single-sheet XLSX transport.
// Limits are engineering resource limits, not invented migration acceptance rules.
// No CSV, XLS, macro, formula, URL, external relationship or executable mapping.
public static class ImportWorkbook
{
    public const int MaxBytes = 2 * 1024 * 1024, MaxExpandedBytes = 8 * 1024 * 1024;
    public const int MaxRows = 500, MaxColumns = 40, MaxCellCharacters = 4096;
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static ImportTable Parse(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaxBytes) throw new ImportInputException();
        try {
            using var stream = new MemoryStream(bytes, false);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            if (zip.Entries.Count > 32 || zip.Entries.Sum(x => x.Length) > MaxExpandedBytes ||
                zip.Entries.Select(x => x.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count ||
                zip.Entries.Any(x => x.FullName.Contains("..", StringComparison.Ordinal) || x.FullName.StartsWith('/') ||
                    x.FullName.Contains('\\') || x.FullName.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                    x.FullName.Contains("external", StringComparison.OrdinalIgnoreCase) ||
                    !x.FullName.EndsWith(".xml", StringComparison.Ordinal) && !x.FullName.EndsWith(".rels", StringComparison.Ordinal))) throw new ImportInputException();
            XDocument Read(ZipArchiveEntry entry) {
                using var input = entry.Open();
                using var xml = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes });
                return XDocument.Load(xml);
            }
            var contentTypes = zip.GetEntry("[Content_Types].xml");
            if (contentTypes == null || zip.GetEntry("xl/workbook.xml") == null) throw new ImportInputException();
            static bool ExecutableType(string? type) => type != null && new[] { "macro", "vba", "oleobject", "activex" }
                .Any(term => type.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (Read(contentTypes).Descendants().Any(x => ExecutableType((string?)x.Attribute("ContentType")))) throw new ImportInputException();
            foreach (var entry in zip.Entries.Where(x => x.FullName.EndsWith(".rels", StringComparison.Ordinal))) {
                var rels = Read(entry);
                if (rels.Descendants().Any(x => (string?)x.Attribute("TargetMode") == "External" ||
                    ExecutableType((string?)x.Attribute("Type")) ||
                    ((string?)x.Attribute("Target"))?.Contains(":", StringComparison.Ordinal) == true)) throw new ImportInputException();
            }
            var sheets = zip.Entries.Where(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal) &&
                x.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray();
            if (sheets.Length != 1) throw new ImportInputException();
            var sharedEntry = zip.GetEntry("xl/sharedStrings.xml");
            var shared = sharedEntry == null ? Array.Empty<string>() : Read(sharedEntry).Root!.Elements(Ns + "si")
                .Select(x => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
            var sheet = Read(sheets[0]);
            if (sheet.Descendants().Any(x => x.Name.LocalName is "f" or "hyperlink" or "oleObject" or "mergeCell")) throw new ImportInputException();
            if (shared.Length > (MaxRows + 1) * MaxColumns) throw new ImportInputException();
            var output = new List<string[]>(); var priorRow = 0; var totalCharacters = 0;
            foreach (var row in sheet.Descendants(Ns + "row")) {
                if (!int.TryParse((string?)row.Attribute("r"), out var rowIndex) || rowIndex != priorRow + 1 || rowIndex > MaxRows + 1)
                    throw new ImportInputException();
                priorRow = rowIndex;
                var cells = new SortedDictionary<int, string>();
                foreach (var cell in row.Elements(Ns + "c")) {
                    var reference = (string?)cell.Attribute("r") ?? ""; var col = 0; var i = 0;
                    while (i < reference.Length && reference[i] is >= 'A' and <= 'Z') { col = checked(col * 26 + reference[i] - 'A' + 1); i++; }
                    if (col is < 1 or > MaxColumns || reference[i..] != rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new ImportInputException();
                    var type = (string?)cell.Attribute("t");
                    var value = cell.Element(Ns + "v")?.Value ?? "";
                    if (type == "s") {
                        if (!int.TryParse(value, out var si) || si < 0 || si >= shared.Length) throw new ImportInputException();
                        value = shared[si];
                    } else if (type == "inlineStr") value = string.Concat(cell.Descendants(Ns + "t").Select(x => x.Value));
                    else if (type is not (null or "n" or "str" or "b" or "d")) throw new ImportInputException();
                    totalCharacters = checked(totalCharacters + value.Length);
                    if (totalCharacters > MaxExpandedBytes) throw new ImportInputException();
                    // No Excel serial date guessing; ISO dates must be supplied explicitly.
                    if (value.Length > MaxCellCharacters || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') ||
                        !cells.TryAdd(col - 1, value)) throw new ImportInputException();
                }
                var width = cells.Count == 0 ? 0 : cells.Keys.Max() + 1;
                output.Add(Enumerable.Range(0, width).Select(c => cells.GetValueOrDefault(c, "")).ToArray());
            }
            if (output.Count == 0 || output[0].Length == 0) throw new ImportInputException();
            var headers = output[0];
            if (output.Skip(1).Any(r => r.Length > headers.Length)) throw new ImportInputException();
            return new(headers, output.Skip(1).Select(r => Enumerable.Range(0, headers.Length).Select(c => c < r.Length ? r[c] : "").ToArray()).ToArray());
        } catch (Exception e) when (e is InvalidDataException or XmlException or OverflowException or ArgumentException) { throw new ImportInputException(); }
    }

    public static byte[] Template(IReadOnlyList<string> fields)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true)) {
            void Write(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml); }
            Write("[Content_Types].xml", """<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Write("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Write("xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Import" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Write("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            // Column styles apply before entry, including currently empty rows. Built-in 49 is Text.
            Write("xl/styles.xml", """<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
            var columns = new XElement(Ns + "cols", fields.Select((_, i) => new XElement(Ns + "col",
                new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("style", 1))));
            var row = new XElement(Ns + "row", new XAttribute("r", 1), fields.Select((f, i) => new XElement(Ns + "c",
                new XAttribute("r", Column(i) + "1"), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", f)))));
            Write("xl/worksheets/sheet1.xml", new XDocument(new XElement(Ns + "worksheet", columns, new XElement(Ns + "sheetData", row))).ToString());
        }
        return output.ToArray();
    }
    private static string Column(int index) => index < 26 ? ((char)('A' + index)).ToString() : "A" + (char)('A' + index - 26);
}
