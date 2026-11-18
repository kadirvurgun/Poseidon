using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class FleetCatalogDraftPolicyTests
{
    private static FleetCatalogEntryDraft Entry() => new(Guid.Parse("10000000-0000-4000-8000-000000000001"), null, null, null, [], []);

    [Fact]
    public void Empty_and_incomplete_drafts_are_retained_without_implied_type_or_approval()
    {
        var empty = FleetCatalogDraftPolicy.Normalize(new(null, []));
        Assert.Null(empty.Title); Assert.Empty(empty.Entries);
        var entry = Assert.Single(FleetCatalogDraftPolicy.Normalize(new(" FICTIONAL ", [Entry()])).Entries);
        Assert.Null(entry.TypeCode); Assert.Null(entry.Mode); Assert.Empty(entry.RequiredInputPaths);
    }

    [Fact]
    public void Normalization_preserves_identifiers_codes_pointers_order_and_does_not_modify_the_input()
    {
        var original = Entry() with { TypeCode = " draft-code ", Label = " Cafe\u0301 ", Mode = "TUG",
            AllowedProfileCodes = [" P2 ", "P1"], RequiredInputPaths = ["/gt", "/a~1b", "/a~0b"] };
        var result = Assert.Single(FleetCatalogDraftPolicy.Normalize(new(" Draft ", [original])).Entries);
        Assert.Equal(original.Id, result.Id); Assert.Equal("draft-code", result.TypeCode); Assert.Equal("Café", result.Label);
        Assert.Equal(new[] { "P2", "P1" }, result.AllowedProfileCodes);
        Assert.Equal(new[] { "/gt", "/a~1b", "/a~0b" }, result.RequiredInputPaths);
        Assert.Equal(" draft-code ", original.TypeCode); Assert.Equal(" P2 ", original.AllowedProfileCodes[0]);
    }

    [Fact]
    public void Duplicate_members_and_normalized_codes_fail_instead_of_silent_deduplication()
    {
        var first = Entry() with { TypeCode = "TEST" };
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null, [first, first])));
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null,
            [first, first with { Id = Guid.NewGuid(), TypeCode = " TEST " }])));
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null,
            [first with { AllowedProfileCodes = ["ONE", " ONE "] }])));
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null,
            [first with { RequiredInputPaths = ["/gt", "/gt"] }])));
    }

    [Theory]
    [InlineData("gt")][InlineData("/broken~")][InlineData("/bad~2escape")][InlineData("/bad\npath")]
    public void Invalid_native_pointer_syntax_is_rejected_without_inventing_a_schema(string path) =>
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null, [Entry() with { RequiredInputPaths = [path] }])));

    [Fact]
    public void Exact_source_bounds_are_preserved_and_arrays_are_never_truncated()
    {
        var full = Entry() with { TypeCode = new('A', 64), Label = new('L', 200), Mode = "SHIP",
            AllowedProfileCodes = Enumerable.Range(0, 50).Select(i => $"P{i}").ToArray(),
            RequiredInputPaths = Enumerable.Range(0, 100).Select(i => $"/field{i}").ToArray() };
        Assert.Equal(100, Assert.Single(FleetCatalogDraftPolicy.Normalize(new(null, [full])).Entries).RequiredInputPaths.Length);
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null, [full with { AllowedProfileCodes = [.. full.AllowedProfileCodes, "extra"] }])));
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null, [full with { RequiredInputPaths = [.. full.RequiredInputPaths, "/extra"] }])));
        Assert.Throws<ArgumentException>(() => FleetCatalogDraftPolicy.Normalize(new(null, [full with { Mode = "Ready" }])));
    }

    [Fact]
    public void Unmapped_publication_or_authority_fields_cannot_enter_the_draft_contract()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FleetCatalogDraftContent>("{\"title\":null,\"entries\":[],\"approved\":true}", json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FleetCatalogEntryDraft>(
            "{\"id\":\"10000000-0000-4000-8000-000000000001\",\"typeCode\":null,\"label\":null,\"mode\":null,\"allowedProfileCodes\":[],\"requiredInputPaths\":[],\"state\":\"ACTIVE\"}", json));
    }
}
