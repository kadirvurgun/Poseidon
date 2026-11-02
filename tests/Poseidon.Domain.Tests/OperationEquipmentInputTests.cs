using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class OperationEquipmentInputTests
{
    [Fact]
    public void Missing_cargo_and_fitment_are_not_false_and_draft_references_are_incomplete() {
        var cargo = new ControlEvidenceContent(CargoPlan: new());
        var fitment = new ControlEvidenceContent(WinchFitment: new());
        Assert.Null(cargo.CargoPlan!.PackagedDangerousGoods); Assert.Null(fitment.WinchFitment!.Fitted);
        foreach (var c in new ControlEvidenceContent[] { cargo, fitment, new(Operation: new()), new(ServiceAuthorization: new()) }) {
            ControlEvidencePolicy.Validate("Evidence", c, DateTimeOffset.UtcNow);
            Assert.False(OperationEquipmentInputs.Complete(c));
            Assert.Contains("OWNING_INPUT_INCOMPLETE", ControlEvidencePolicy.Blockers("Evidence", c, 0, false));
        }
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.Validate("Control", cargo));
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.Validate("Evidence", cargo with { WinchFitment = new() }));
    }
    [Fact]
    public void Literal_bindings_require_exact_source_mapping_and_never_alias_intake_labels() {
        QualifiedLiteralBinding[] bindings = [new("operation.jobType", "Harbour Towage", "HARBOUR_ASSIST", "FICTIONAL/qualified/source/only")];
        Assert.Null(OperationEquipmentInputs.Map(null, "operation.jobType", "Harbour Towage"));
        Assert.Null(OperationEquipmentInputs.Map(bindings, "operation.jobType", "harbour towage"));
        Assert.Equal("HARBOUR_ASSIST", OperationEquipmentInputs.Map(bindings, "operation.jobType", "Harbour Towage"));
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.ValidateBindings([bindings[0], bindings[0] with { RuleValue = "ESCORT" }]));
        foreach (var field in OperationEquipmentInputs.Fields) Assert.Throws<ArgumentException>(() => RegulatoryQualificationPolicy.Validate(new(Inputs: [new(field, "Declared")])));
    }
    [Theory]
    [InlineData("{\"packagedDangerousGoods\":false,\"planReference\":\"FICTIONAL\",\"verified\":true}")]
    [InlineData("{\"packagedDangerousGoods\":\"false\",\"planReference\":\"FICTIONAL\"}")]
    public void Caller_cannot_assert_verification_or_coerce_boolean(string json) => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CargoPlanReference>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    [Fact]
    public void Partial_versions_and_self_inconsistent_owner_pins_are_rejected() {
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.Validate("Evidence", new(Operation: new(JobId: Guid.NewGuid()))));
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.Validate("Evidence", new(WinchFitment: new(AssetId: Guid.NewGuid(), AssetRevision: 1))));
        Assert.Throws<ArgumentException>(() => OperationEquipmentInputs.Validate("Evidence", new(ServiceAuthorization: new(PlanRevision: 1, PlanVersion: 1))));
    }
    [Fact]
    public void Incomplete_source_policies_can_be_saved_as_drafts_but_cannot_enter_review() {
        var c = new QualificationContent(LiteralBindings: [new("operation.jobType", null, null, null)], OwningDocuments: [new("CargoPlan", null, null, null)]);
        RegulatoryQualificationPolicy.Validate(c);
        Assert.Contains("OWNING_DOCUMENT_POLICY_MISSING", RegulatoryQualificationPolicy.Missing(c));
        Assert.Contains("OWNING_LITERAL_BINDING_MISSING", RegulatoryQualificationPolicy.Missing(c));
        Assert.Equal("incomplete_qualification", RegulatoryQualificationPolicy.Transition("DRAFT", "IN_REVIEW", c, true, DateOnly.FromDateTime(DateTime.UtcNow), new("DRAFT", "2000-01-01")));
    }
}
