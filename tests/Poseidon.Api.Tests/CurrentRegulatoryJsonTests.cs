using System.Text.Json;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class CurrentRegulatoryJsonTests
{
    [Fact]
    public void Wire_escape_spelling_does_not_change_owning_provenance()
    {
        using var current = JsonDocument.Parse("{\"savedAt\":\"2026-11-02T12:00:00+00:00\",\"revision\":2}");
        using var captured = JsonDocument.Parse("{\"savedAt\":\"2026-11-02T12:00:00\\u002B00:00\",\"revision\":2}");
        Assert.NotEqual(current.RootElement.GetRawText(), captured.RootElement.GetRawText());
        Assert.True(CurrentRegulatoryContext.MatchesCapturedJson(current.RootElement, captured.RootElement));
    }

    [Theory]
    [InlineData("{\"revision\":3}", "{\"revision\":2}")]
    [InlineData("[2,1]", "[1,2]")]
    [InlineData("null", "false")]
    [InlineData("\"Unknown\"", "\"Known\"")]
    [InlineData("9007199254740992.0000000001", "9007199254740992.0000000002")]
    [InlineData("\"2026-11-02T12:00:01+00:00\"", "\"2026-11-02T12:00:00+00:00\"")]
    public void Material_capture_changes_remain_distinct(string current, string captured)
    {
        using var a = JsonDocument.Parse(current); using var b = JsonDocument.Parse(captured);
        Assert.False(CurrentRegulatoryContext.MatchesCapturedJson(a.RootElement, b.RootElement));
    }
}
