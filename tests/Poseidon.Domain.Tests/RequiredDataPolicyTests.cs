using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class RequiredDataPolicyTests
{
    private static RequiredDataPolicy Policy(DataKind kind, bool required = true)
        => new([new("field", kind, required)]);
    private static KeyValuePair<string, DraftValue>[] Input(DraftValue value) => [new("field", value)];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void BlankTextRemainsMissingAndBlocksRequiredGate(string? text)
    {
        var value = DraftValue.Text(text);
        Assert.Equal(DataState.Missing, value.State);
        Assert.Null(value.Value);
        Assert.Equal(new RequiredDataBlock("field", RequiredDataReason.MissingRequiredValue),
            Assert.Single(Policy(DataKind.Text).Assess(Input(value)).BlockingFields));
    }

    [Fact]
    public void KnownFalseZeroAndTextArePreserved()
    {
        foreach (var value in new[] { DraftValue.Boolean(false), DraftValue.Number(0), DraftValue.Text("value") })
        {
            Assert.Equal(DataState.Known, value.State);
            Assert.True(Policy(value.Kind).Assess(Input(value)).RequiredDataGateSatisfied);
        }
        Assert.Equal(false, DraftValue.Boolean(false).Value);
        Assert.Equal(0m, DraftValue.Number(0).Value);
    }

    [Theory]
    [InlineData(DataKind.Text)]
    [InlineData(DataKind.Boolean)]
    [InlineData(DataKind.Number)]
    public void UnknownNeverBecomesKnownOrMissing(DataKind kind)
    {
        var value = DraftValue.Unknown(kind);
        Assert.Null(value.Value);
        Assert.Equal(DataState.Unknown, value.State);
        Assert.Equal(RequiredDataReason.UnknownRequiredValue,
            Assert.Single(Policy(kind).Assess(Input(value)).BlockingFields).Reason);
    }

    [Fact]
    public void NullableInputsDoNotDefault()
    {
        foreach (var value in new[] { DraftValue.Boolean(null), DraftValue.Number(null) })
        {
            Assert.Equal(DataState.Missing, value.State);
            Assert.Null(value.Value);
            Assert.False(Policy(value.Kind).Assess(Input(value)).RequiredDataGateSatisfied);
        }
    }

    [Fact]
    public void AbsentRequiredBlocksButOptionalMissingAndUnknownDoNot()
    {
        Assert.False(Policy(DataKind.Text).Assess([]).RequiredDataGateSatisfied);
        var optional = Policy(DataKind.Text, false);
        Assert.True(optional.Assess([]).RequiredDataGateSatisfied);
        Assert.True(optional.Assess(Input(DraftValue.Missing(DataKind.Text))).RequiredDataGateSatisfied);
        Assert.True(optional.Assess(Input(DraftValue.Unknown(DataKind.Text))).RequiredDataGateSatisfied);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" field")]
    [InlineData(null)]
    public void AmbiguousKeysAreRejected(string? key)
        => Assert.Throws<ArgumentException>(() => new FieldDefinition(key!, DataKind.Text, true));

    [Fact]
    public void InvalidMetadataAndDuplicateDefinitionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FieldDefinition("f", (DataKind)99, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => DraftValue.Unknown((DataKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => DraftValue.Missing((DataKind)99));
        Assert.Throws<ArgumentException>(() => new RequiredDataPolicy([
            new("f", DataKind.Text, true), new("f", DataKind.Boolean, false)]));
        Assert.Throws<ArgumentNullException>(() => new RequiredDataPolicy([null!]));
    }

    [Fact]
    public void DuplicateUndefinedMismatchedAndNullValuesAreRejected()
    {
        var policy = Policy(DataKind.Text);
        Assert.Throws<ArgumentException>(() => policy.Assess([new("field", DraftValue.Text("a")), new("field", DraftValue.Text("b"))]));
        Assert.Throws<ArgumentException>(() => policy.Assess([new("other", DraftValue.Text("a"))]));
        Assert.Throws<ArgumentException>(() => policy.Assess(Input(DraftValue.Boolean(false))));
        Assert.Throws<ArgumentException>(() => policy.Assess([new(" ", DraftValue.Text("a"))]));
        Assert.Throws<ArgumentNullException>(() => policy.Assess([new("field", null!)]));
    }

    [Fact]
    public void DefinitionsAreSnapshottedResultsImmutableAndAssessmentRepeatable()
    {
        var definitions = new List<FieldDefinition> { new("z", DataKind.Text, true), new("a", DataKind.Text, true) };
        var policy = new RequiredDataPolicy(definitions);
        definitions.Clear();
        var inputs = new Dictionary<string, DraftValue> { ["z"] = DraftValue.Unknown(DataKind.Text) };
        var first = policy.Assess(inputs);
        Assert.Equal(new[] { "a", "z" }, first.BlockingFields.Select(b => b.FieldKey));
        Assert.Equal(first.BlockingFields, policy.Assess(inputs).BlockingFields);
        Assert.Equal(DataState.Unknown, inputs["z"].State);
        inputs.Clear();
        Assert.Equal(RequiredDataReason.UnknownRequiredValue, first.BlockingFields[1].Reason);
        Assert.Throws<NotSupportedException>(() => ((IList<RequiredDataBlock>)first.BlockingFields).Clear());
    }
}
