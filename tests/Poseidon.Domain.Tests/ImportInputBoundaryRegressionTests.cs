using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// Integrate after the real Import checkpoint supplies CertificateMetadataPolicy
// and ImportWorkbook. Authored boundary regressions, NOT an executed red/green run.
public sealed class ImportInputBoundaryRegressionTests
{
    [Theory]
    [InlineData("1\n")]
    [InlineData("12.300\n")]
    [InlineData("1\r\n")]
    [InlineData("1\t")]
    [InlineData("1\0")]
    [InlineData("١")]
    public void Numeric_declarations_reject_whitespace_control_and_non_ASCII_digit_suffixes(string value)
    {
        // Regex $ may match immediately before a final LF. Exact lexical inputs
        // require an absolute end boundary, not Trim() or partial regex matching.
        Assert.False(CertificateMetadataPolicy.ExactNumeric(value));
        Assert.False(CertificateMetadataPolicy.Valid(new(new(), IssuingCountryNumeric: value)));
        Assert.False(CertificateMetadataPolicy.Valid(new(new(), RenewalLeadDaysNumeric: value)));
    }

    [Fact]
    public void Downloaded_template_assigns_text_format_to_editable_columns_before_entry()
    {
        // Excel General may coerce dates, long identifiers and exact decimals.
        // This checks the emitted workbook contract, not a live Excel application.
        string[] fields = ["record_key", "certificate_no", "issue_date", "issuing_country"];
        using var stream = new MemoryStream(ImportWorkbook.Template(fields));
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XDocument Read(string path) {
            var entry = zip.GetEntry(path); Assert.NotNull(entry);
            using var input = entry.Open(); return XDocument.Load(input);
        }
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        var workbookRels = Read("xl/_rels/workbook.xml.rels");
        var stylesRelation = Assert.Single(workbookRels.Root!.Elements(relationships + "Relationship")
            .Where(x => ((string?)x.Attribute("Type"))?.EndsWith("/styles", StringComparison.Ordinal) == true));
        Assert.NotEqual("External", (string?)stylesRelation.Attribute("TargetMode"));
        var target = (string?)stylesRelation.Attribute("Target"); Assert.False(string.IsNullOrWhiteSpace(target));
        var stylesPath = new Uri(new Uri("https://fixture.invalid/xl/workbook.xml"), target!).AbsolutePath.TrimStart('/');
        var styles = Read(stylesPath);
        var formats = styles.Root!.Element(spreadsheet + "cellXfs"); Assert.NotNull(formats);
        var sheet = Read("xl/worksheets/sheet1.xml");
        var columns = sheet.Root!.Element(spreadsheet + "cols"); Assert.NotNull(columns);
        for (var position = 1; position <= fields.Length; position++) {
            var column = Assert.Single(columns.Elements(spreadsheet + "col").Where(x =>
                int.Parse(x.Attribute("min")!.Value, CultureInfo.InvariantCulture) <= position &&
                int.Parse(x.Attribute("max")!.Value, CultureInfo.InvariantCulture) >= position));
            var style = int.Parse(column.Attribute("style")!.Value, CultureInfo.InvariantCulture);
            var numberFormat = formats.Elements(spreadsheet + "xf").ElementAt(style).Attribute("numFmtId")!.Value;
            var text = numberFormat == "49" || styles.Descendants(spreadsheet + "numFmt").Any(x =>
                (string?)x.Attribute("numFmtId") == numberFormat && (string?)x.Attribute("formatCode") == "@");
            Assert.True(text, "Editable import columns must preserve text declarations before spreadsheet coercion.");
        }
        Assert.Equal(fields, ImportWorkbook.Parse(ImportWorkbook.Template(fields)).Headers);
    }
}
