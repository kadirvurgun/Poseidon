using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public class CrewMedicalExpiryTests
{
    [Theory]
    [InlineData("2031-02-03", true)]
    [InlineData("2031-02-29", false)]
    [InlineData("2031-2-3", false)]
    [InlineData("2031-02-03T00:00:00Z", false)]
    [InlineData(" 2031-02-03", false)]
    [InlineData("2031-02-03\n", false)]
    [InlineData(null, true)]
    public void Medical_expiry_is_an_exact_nullable_date_declaration(string? text, bool expected) =>
        Assert.Equal(expected, CrewPersonalData.Valid(CrewPersonalField.MedicalExpiry, text));

    [Fact]
    public void Added_field_preserves_existing_wire_ids_and_has_no_decision_state()
    {
        Assert.Equal(14, (int)CrewPersonalField.Email);
        Assert.Equal(15, (int)CrewPersonalField.MedicalExpiry);
        var data = new CrewPersonalData().With(CrewPersonalField.MedicalExpiry, "2031-02-03");
        Assert.Equal("medical_expiry", CrewPersonalData.Key(CrewPersonalField.MedicalExpiry));
        Assert.Equal("2031-02-03", data.Lexical(CrewPersonalField.MedicalExpiry));
        Assert.Null(data.With(CrewPersonalField.MedicalExpiry, null).MedicalExpiry);
    }
}
