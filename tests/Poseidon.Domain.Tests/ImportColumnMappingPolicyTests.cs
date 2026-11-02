using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-069 format-independent mapping only. No import/domain bypass or executed-result claim.
public sealed class ImportColumnMappingPolicyTests
{
    private static ImportTemplateDefinition Template(bool reuse = false) => new("synthetic-vessel-v1",
        [new("code", true, true), new("declared_name", false, true), new("tenant_id", false, false)], reuse);
    private static ImportColumnMappingResult Check(params ImportColumnBinding[] bindings) =>
        ImportColumnMappingPolicy.Validate(Template(), 3, bindings);

    [Fact]
    public void Explicit_mapping_has_canonical_target_order_and_preserves_its_template_version()
    {
        var result = Check(new ImportColumnBinding(2, "declared_name"), new(1, "code"));
        Assert.True(result.IsValid); Assert.Empty(result.Issues);
        Assert.Equal("synthetic-vessel-v1", result.Mapping!.TemplateVersion);
        Assert.Equal(new[] { new ValidatedImportColumn(1, 0, "code"), new ValidatedImportColumn(2, 1, "declared_name") }, result.Mapping.Columns);
    }

    [Fact]
    public void Reordering_equivalent_bindings_does_not_change_the_validated_plan()
    {
        var first = Check(new ImportColumnBinding(1, "code"), new(2, "declared_name"));
        var second = Check(new ImportColumnBinding(2, "declared_name"), new(1, "code"));
        Assert.Equal(first.Mapping!.Columns, second.Mapping!.Columns);
    }

    [Fact]
    public void Readonly_context_is_not_client_writable_even_when_it_is_in_the_template()
    {
        var result = Check(new ImportColumnBinding(0, "code"), new(1, "tenant_id"));
        Assert.False(result.IsValid); Assert.Null(result.Mapping);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.ReadOnlyTarget);
    }

    [Fact]
    public void Unknown_target_and_its_private_value_are_never_echoed_in_a_diagnostic()
    {
        const string untrusted = "PRIVATE_CUSTOMER_MARKER";
        var result = Check(new ImportColumnBinding(0, "code"), new(1, untrusted));
        Assert.Null(result.Mapping);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.UnknownTarget);
        Assert.DoesNotContain(untrusted, JsonSerializer.Serialize(result));
        Assert.DoesNotContain(untrusted, result.ToString());
    }

    [Fact]
    public void Unknown_case_alias_cannot_silently_map_to_a_whitelisted_target()
    {
        var result = Check(new ImportColumnBinding(0, "CODE"));
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.UnknownTarget);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.MissingRequiredMapping);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Source_column_must_exist_in_the_actual_provider_projection(int column)
    {
        var result = Check(new ImportColumnBinding(column, "code"));
        Assert.Null(result.Mapping);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.InvalidSourceColumn);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.MissingRequiredMapping);
    }

    [Fact]
    public void Duplicate_targets_never_choose_first_or_last_writer()
    {
        var result = Check(new ImportColumnBinding(0, "code"), new(1, "code"));
        Assert.Null(result.Mapping);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.DuplicateTarget);
    }

    [Fact]
    public void Reusing_a_source_column_requires_an_explicit_trusted_template_policy()
    {
        var denied = Check(new ImportColumnBinding(0, "code"), new(0, "declared_name"));
        Assert.False(denied.IsValid);
        Assert.Contains(denied.Issues, issue => issue.Code == ImportMappingIssueCode.DuplicateSourceColumn);
        var allowed = ImportColumnMappingPolicy.Validate(Template(reuse: true), 1,
            [new(0, "code"), new(0, "declared_name")]);
        Assert.True(allowed.IsValid);
        Assert.Equal(2, allowed.Mapping!.Columns.Count);
    }

    [Fact]
    public void Missing_required_mapping_is_distinct_from_optional_unmapped_columns()
    {
        var optionalMissing = Check(new ImportColumnBinding(0, "code"));
        Assert.True(optionalMissing.IsValid);
        var requiredMissing = Check(new ImportColumnBinding(0, "declared_name"));
        Assert.False(requiredMissing.IsValid);
        Assert.Contains(requiredMissing.Issues, issue =>
            issue.Code == ImportMappingIssueCode.MissingRequiredMapping && issue.TemplateFieldOrdinal == 0);
    }

    [Fact]
    public void A_system_derived_field_cannot_be_misconfigured_as_required_client_input()
    {
        var template = new ImportTemplateDefinition("synthetic-v1", [new("tenant_id", true, false)], false);
        var result = ImportColumnMappingPolicy.Validate(template, 1, []);
        Assert.Equal(ImportMappingIssueCode.InvalidTemplate, Assert.Single(result.Issues).Code);
        Assert.Null(result.Mapping);
    }

    [Fact]
    public void Duplicate_template_keys_are_a_schema_error_not_a_request_override()
    {
        var template = new ImportTemplateDefinition("synthetic-v1", [new("code", true, true), new("code", false, true)], false);
        var result = ImportColumnMappingPolicy.Validate(template, 1, [new(0, "code")]);
        Assert.Equal(ImportMappingIssueCode.InvalidTemplate, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Invalid_template_version_and_empty_or_controlled_keys_fail_without_data_echo()
    {
        ImportTemplateDefinition[] invalid = [new("", [new("code", true, true)], false),
            new("v1", [], false), new("v1", [new("", true, true)], false),
            new("v1", [new("private\rheader", true, true)], false)];
        foreach (var template in invalid)
        {
            var result = ImportColumnMappingPolicy.Validate(template, 1, []);
            Assert.Equal(ImportMappingIssueCode.InvalidTemplate, Assert.Single(result.Issues).Code);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(result));
        }
    }

    [Fact]
    public void Validated_plan_is_not_mutated_by_changes_to_request_bindings()
    {
        var bindings = new List<ImportColumnBinding> { new(0, "code") };
        var result = ImportColumnMappingPolicy.Validate(Template(), 3, bindings);
        bindings.Clear(); bindings.Add(new(2, "tenant_id"));
        Assert.Equal(new ValidatedImportColumn(0, 0, "code"), Assert.Single(result.Mapping!.Columns));
        Assert.Throws<NotSupportedException>(() => ((IList<ValidatedImportColumn>)result.Mapping.Columns).Clear());
    }

    [Fact]
    public void Null_binding_is_a_safe_request_error_and_cannot_fill_a_required_field()
    {
        var result = Check(new ImportColumnBinding[] { null! });
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.InvalidBinding);
        Assert.Contains(result.Issues, issue => issue.Code == ImportMappingIssueCode.MissingRequiredMapping);
        Assert.Null(result.Mapping);
    }

    [Fact]
    public void No_column_allocation_is_derived_from_an_untrusted_large_provider_count()
    {
        var result = ImportColumnMappingPolicy.Validate(Template(), int.MaxValue, [new(int.MaxValue - 1, "code")]);
        Assert.True(result.IsValid);
        Assert.Single(result.Mapping!.Columns);
        Assert.Throws<ArgumentOutOfRangeException>(() => ImportColumnMappingPolicy.Validate(Template(), -1, []));
    }

    [Fact]
    public void Later_template_list_mutation_cannot_retarget_an_existing_plan()
    {
        var fields = new List<ImportTemplateField> { new("code", true, true) };
        var definition = new ImportTemplateDefinition("synthetic-v1", fields, false);
        var first = ImportColumnMappingPolicy.Validate(definition, 1, [new(0, "code")]).Mapping!;
        fields[0] = new("other_field", true, true);
        var changed = ImportColumnMappingPolicy.Validate(definition, 1, [new(0, "other_field")]).Mapping!;
        Assert.Equal("code", Assert.Single(first.Columns).TargetFieldKey);
        Assert.Equal(first.TemplateVersion, changed.TemplateVersion);
        Assert.NotEqual(first.TemplateFingerprint, changed.TemplateFingerprint);
    }

    [Fact]
    public void Reuse_and_required_write_policy_changes_are_bound_into_template_fingerprint()
    {
        var first = ImportColumnMappingPolicy.Validate(Template(), 1, [new(0, "code")]).Mapping!;
        var reused = ImportColumnMappingPolicy.Validate(Template(reuse: true), 1, [new(0, "code")]).Mapping!;
        var changedFields = Template().Fields.Select(field => field.Key == "code" ? field with { RequiredInput = false } : field).ToArray();
        var optional = ImportColumnMappingPolicy.Validate(new("synthetic-vessel-v1", changedFields, false), 1, [new(0, "code")]).Mapping!;
        Assert.NotEqual(first.TemplateFingerprint, reused.TemplateFingerprint);
        Assert.NotEqual(first.TemplateFingerprint, optional.TemplateFingerprint);
    }
}
