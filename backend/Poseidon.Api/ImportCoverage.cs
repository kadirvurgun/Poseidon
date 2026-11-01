using System.Text.Json;
namespace Poseidon.Api;
public sealed record ImportSourceCoverageDto(string Version, bool IsWritableSchema, ImportSourceFieldCoverage[] Fields);
public sealed record ImportSourceFieldCoverage(string Category, string Screen, string Field, string SourceType, string SourceOwnership,
    string SourceLocator, string Fr, string Tc, string? ImportField, string State, string RemainingGate);
internal static class ImportCoverage
{
    internal static ImportSourceCoverageDto Describe(string[] categories) {
        using var stream = typeof(ImportCoverage).Assembly.GetManifestResourceStream("Poseidon.ImportSourceCoverage")!;
        using var json = JsonDocument.Parse(stream);
        var fields = json.RootElement.GetProperty("fields").EnumerateArray().Select(f => {
            string Text(string name) => f.GetProperty(name).GetString()!;
            var screen = Text("screen"); var category = screen switch { "fleet-vessels" => "vessel", "equipment-hierarchy" => "equipment", "crew-certificates" => "certificate", _ => "crew" };
            return new ImportSourceFieldCoverage(category, screen, Text("field"), Text("source_type"), Text("system"), Text("locator"), Text("fr"), Text("tc"),
                f.GetProperty("template_field_candidate").GetString(), Text("coverage_state"), Text("coverage"));
        }).Where(f => categories.Contains(f.Category, StringComparer.Ordinal)).ToArray();
        return new("DEV-069-source-coverage-v2", false, fields);
    }
}
