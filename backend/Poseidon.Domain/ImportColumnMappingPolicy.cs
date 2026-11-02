using System.Security.Cryptography;
using System.Text.Json;

namespace Poseidon.Domain;

// The API loads this descriptor from its trusted versioned template registry;
// it must never bind a request body to a template/actor/tenant descriptor.
public sealed record ImportTemplateField(string Key, bool RequiredInput, bool UserWritable);
public sealed record ImportTemplateDefinition(
    string Version, IReadOnlyList<ImportTemplateField> Fields, bool AllowSourceColumnReuse);
public sealed record ImportColumnBinding(int SourceColumn, string TargetField);

public enum ImportMappingIssueCode
{
    InvalidTemplate,
    InvalidBinding,
    InvalidSourceColumn,
    UnknownTarget,
    ReadOnlyTarget,
    DuplicateTarget,
    DuplicateSourceColumn,
    MissingRequiredMapping
}

// Ordinals refer only to this authorized template/request, never another tenant's data.
// No raw header, unknown target string, cell data or private key appears in a diagnostic.
public readonly record struct ImportMappingIssue(
    ImportMappingIssueCode Code, int? BindingOrdinal = null, int? TemplateFieldOrdinal = null);
public readonly record struct ValidatedImportColumn(int SourceColumn, int TemplateFieldOrdinal, string TargetFieldKey);

public sealed class ValidatedImportMapping
{
    internal ValidatedImportMapping(string version, string fingerprint, IReadOnlyList<ValidatedImportColumn> columns)
    {
        TemplateVersion = version;
        TemplateFingerprint = fingerprint;
        Columns = columns;
    }
    public string TemplateVersion { get; }
    public string TemplateFingerprint { get; }
    public IReadOnlyList<ValidatedImportColumn> Columns { get; }
    public override string ToString() => nameof(ValidatedImportMapping);
}

public sealed class ImportColumnMappingResult
{
    internal ImportColumnMappingResult(ValidatedImportMapping? mapping, IEnumerable<ImportMappingIssue> issues)
    {
        Mapping = mapping;
        Issues = Array.AsReadOnly(issues.ToArray());
    }
    public bool IsValid => Mapping is not null;
    public ValidatedImportMapping? Mapping { get; }
    public IReadOnlyList<ImportMappingIssue> Issues { get; }
    public override string ToString() => nameof(ImportColumnMappingResult);
}

// DEV-069 / T-S11-001 (18.01 Backlog row70): explicit versioned mapping and
// validation, never a direct data-domain write. Spreadsheet/CSV/other providers
// supply bounded physical column ordinals; format detection is not guessed here.
public static class ImportColumnMappingPolicy
{
    public static ImportColumnMappingResult Validate(
        ImportTemplateDefinition template, int sourceColumnCount,
        IReadOnlyList<ImportColumnBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceColumnCount);
        if (string.IsNullOrWhiteSpace(template.Version) || template.Fields is null || template.Fields.Count == 0)
            return InvalidTemplate();

        var snapshot = template.Fields.ToArray();
        var fields = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < snapshot.Length; i++)
        {
            var field = snapshot[i];
            if (field is null || string.IsNullOrWhiteSpace(field.Key) || field.Key.Any(char.IsControl) ||
                field.RequiredInput && !field.UserWritable || !fields.TryAdd(field.Key, i))
                return InvalidTemplate();
        }

        var issues = new List<ImportMappingIssue>();
        var targets = new HashSet<int>();
        var sources = new HashSet<int>();
        var validTargets = new HashSet<int>();
        var columns = new List<ValidatedImportColumn>();
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding is null || binding.TargetField is null)
            {
                issues.Add(new(ImportMappingIssueCode.InvalidBinding, i));
                continue;
            }
            var valid = true;
            if (binding.SourceColumn < 0 || binding.SourceColumn >= sourceColumnCount)
            {
                issues.Add(new(ImportMappingIssueCode.InvalidSourceColumn, i));
                valid = false;
            }
            if (!fields.TryGetValue(binding.TargetField, out var fieldOrdinal))
            {
                issues.Add(new(ImportMappingIssueCode.UnknownTarget, i));
                continue;
            }
            if (!snapshot[fieldOrdinal].UserWritable)
            {
                issues.Add(new(ImportMappingIssueCode.ReadOnlyTarget, i, fieldOrdinal));
                valid = false;
            }
            if (!targets.Add(fieldOrdinal))
            {
                issues.Add(new(ImportMappingIssueCode.DuplicateTarget, i, fieldOrdinal));
                valid = false;
            }
            if (binding.SourceColumn >= 0 && binding.SourceColumn < sourceColumnCount &&
                !sources.Add(binding.SourceColumn) && !template.AllowSourceColumnReuse)
            {
                issues.Add(new(ImportMappingIssueCode.DuplicateSourceColumn, i, fieldOrdinal));
                valid = false;
            }
            if (valid)
            {
                validTargets.Add(fieldOrdinal);
                columns.Add(new(binding.SourceColumn, fieldOrdinal, snapshot[fieldOrdinal].Key));
            }
        }
        for (var i = 0; i < snapshot.Length; i++)
            if (snapshot[i].RequiredInput && !validTargets.Contains(i))
                issues.Add(new(ImportMappingIssueCode.MissingRequiredMapping, TemplateFieldOrdinal: i));
        if (issues.Count > 0)
            return new(null, issues);

        // Canonical target order is independent of request order. Copy everything before
        // publishing a plan: later mutation of a caller's binding list cannot alter it.
        columns.Sort((first, second) => first.TemplateFieldOrdinal.CompareTo(second.TemplateFieldOrdinal));
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            template.Version, Fields = snapshot, template.AllowSourceColumnReuse
        })));
        var plan = new ValidatedImportMapping(template.Version, fingerprint, Array.AsReadOnly(columns.ToArray()));
        return new(plan, []);
    }

    private static ImportColumnMappingResult InvalidTemplate() =>
        new(null, [new(ImportMappingIssueCode.InvalidTemplate)]);
}
