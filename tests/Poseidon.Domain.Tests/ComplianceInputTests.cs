using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class ComplianceInputTests
{
    [Theory]
    [InlineData(true, true, true)] [InlineData(true, false, false)] [InlineData(null, false, null)] [InlineData(null, true, null)]
    public void Only_complete_source_defined_criterion_set_can_derive_eligibility(bool? first, bool? second, bool? expected) =>
        Assert.Equal(expected, ComplianceInputs.Eligibility([first, second]));
    [Fact]
    public void Incomplete_records_save_as_drafts_and_mixed_purposes_are_rejected() {
        foreach (var c in new ControlEvidenceContent[] { new(ClassPack: new()), new(Msmd: new()), new(CrewRoleAssessment: new()) }) {
            ControlEvidencePolicy.Validate("Evidence", c, DateTimeOffset.UtcNow);
            Assert.Contains("OWNING_INPUT_INCOMPLETE", ControlEvidencePolicy.Blockers("Evidence", c, 0, false));
        }
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Evidence", new(ClassPack: new(), CargoPlan: new()), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Control", new(Msmd: new()), DateTimeOffset.UtcNow));
        Assert.Null(ComplianceInputs.Eligibility([]));
    }
    [Theory]
    [InlineData("class.rulePack.IACS_M79")] [InlineData("regulatory.ismApplicable")] [InlineData("regulatory.msmdVerified")] [InlineData("crew.requiredRoleEligible")] [InlineData("evidence.sourceVerified")]
    public void Declaration_does_not_qualify_any_compliance_input(string field) => Assert.Throws<ArgumentException>(() =>
        RegulatoryQualificationPolicy.Validate(new(Inputs: [new(field, "Declared")])));
    [Fact]
    public void Authority_injection_and_partial_subject_version_tuples_are_rejected() {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClassPackReference>("{\"verified\":true}"));
        Assert.Throws<ArgumentException>(() => ControlEvidencePolicy.Validate("Evidence", new(CrewRoleAssessment: new(AssignmentId: Guid.NewGuid())), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => RegulatoryQualificationPolicy.Validate(new(IsmDependency: new(Guid.NewGuid(), null, "source"))));
        var incomplete = new QualificationContent(Eligibility: new(Guid.NewGuid(), "source", []));
        RegulatoryQualificationPolicy.Validate(incomplete); Assert.Contains("ELIGIBILITY_POLICY_MISSING", RegulatoryQualificationPolicy.Missing(incomplete));
    }
}
