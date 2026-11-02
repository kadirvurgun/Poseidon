using System.IO.Compression;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class ImportWorkbookTests
{
    [Fact]
    public void Versioned_template_is_a_real_single_sheet_workbook_without_fake_rows() {
        var parsed = ImportWorkbook.Parse(ImportWorkbook.Template(["record_key", "issuing_country"]));
        Assert.Equal(new[] { "record_key", "issuing_country" }, parsed.Headers); Assert.Empty(parsed.Rows);
    }
    [Theory]
    [InlineData("<f>HYPERLINK(\"https://invalid.example\")</f><v>1</v>")]
    [InlineData("<f>1+1</f><v>2</v>")]
    public void Rejects_formula_even_with_a_cached_result_without_echoing_contents(string body) {
        var bytes = Rewrite($"<row r=\"2\"><c r=\"A2\">{body}</c></row>");
        var error = Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse(bytes));
        Assert.Equal("invalid_import_input", error.Message);
    }
    [Theory]
    [InlineData("<row r=\"3\"><c r=\"A3\"><v>private</v></c></row>")]
    [InlineData("<row r=\"2\"><c r=\"AO2\"><v>private</v></c></row>")]
    [InlineData("<row r=\"2\"><c r=\"A2\" t=\"e\"><v>private</v></c></row>")]
    [InlineData("<row r=\"2\"><c r=\"A2\" t=\"s\"><v>999</v></c></row>")]
    [InlineData("<row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"A2\"><v>2</v></c></row>")]
    public void Rejects_ambiguous_row_column_cell_and_shared_string_references(string xml) =>
        Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse(Rewrite(xml)));
    [Fact]
    public void Preserves_exact_decimal_and_iso_date_text_without_excel_date_coercion() {
        const string numeric = "123456789012345678901234567890.00000000000000000000000001";
        var table = ImportWorkbook.Parse(Rewrite($"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>{numeric}</t></is></c></row>"));
        Assert.Equal(numeric, table.Rows[0][0]); Assert.DoesNotContain(numeric, table.ToString());
    }
    [Fact]
    public void Rejects_actual_oversized_and_non_zip_inputs() {
        Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse(new byte[ImportWorkbook.MaxBytes + 1]));
        Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse([1, 2, 3]));
    }
    [Fact]
    public void Rejects_second_sheet_and_external_relationship() {
        foreach (var external in new[] { true, false }) {
            using var memory = new MemoryStream(); memory.Write(ImportWorkbook.Template(["key"]));
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) {
                using var writer = new StreamWriter(zip.CreateEntry(external ? "xl/unsafe.rels" : "xl/worksheets/sheet2.xml").Open());
                writer.Write(external ? "<Relationships><Relationship TargetMode=\"External\" Target=\"https://invalid.example\"/></Relationships>" : "<worksheet/>");
            }
            Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse(memory.ToArray()));
        }
    }
    [Fact]
    public void Rejects_macro_enabled_content_type_even_without_a_conventional_vba_filename() {
        using var memory = new MemoryStream(); memory.Write(ImportWorkbook.Template(["key"]));
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) {
            zip.GetEntry("[Content_Types].xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open());
            writer.Write("<Types><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.ms-excel.sheet.macroEnabled.main+xml\"/></Types>");
        }
        Assert.Throws<ImportInputException>(() => ImportWorkbook.Parse(memory.ToArray()));
    }
    private static byte[] Rewrite(string rows) {
        using var memory = new MemoryStream(); memory.Write(ImportWorkbook.Template(["key"]));
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) {
            zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>key</t></is></c></row>" + rows + "</sheetData></worksheet>");
        }
        return memory.ToArray();
    }
}
