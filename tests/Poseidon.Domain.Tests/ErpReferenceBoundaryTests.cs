using System.Text.Json.Nodes;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Domain.Tests;

public sealed class ErpReferenceBoundaryTests
{
    private static readonly Guid Tenant = Guid.Parse("e4b00001-0000-4000-8000-000000000001");
    private static readonly Guid Vessel = Guid.Parse("e4b00002-0000-4000-8000-000000000001");
    private static readonly Guid Reference = Guid.Parse("e4b00003-0000-4000-8000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static ErpReferenceContext Context() => new(Tenant, Vessel, Reference, "TechnicalRequisition", "SyntheticERP", "adapter-v1", Now, TimeSpan.FromHours(1));
    private static JsonObject Input() => new() {
        ["contractVersion"] = 1, ["messageId"] = "e4b00004-0000-4000-8000-000000000001", ["referenceId"] = Reference.ToString(),
        ["tenantId"] = Tenant.ToString(), ["vesselId"] = Vessel.ToString(), ["kind"] = "TechnicalRequisition", ["sourceSystem"] = "SyntheticERP",
        ["sourceVersion"] = "adapter-v1", ["sourceRevision"] = 2, ["externalReference"] = "SYNTHETIC-REQ-1", ["observedAt"] = Now.ToString("O")
    };
    private static ErpReferenceProjection Read(JsonObject value) {
        Assert.True(ErpReferenceBoundary.TryRead(value.ToJsonString(), Context(), out var result)); return Assert.IsType<ErpReferenceProjection>(result);
    }
    [Fact]
    public void References_retain_selected_external_context_without_turning_it_into_a_finance_operation() {
        var input = Input(); input["externalPurchaseOrderReference"] = "SYNTHETIC-PO-REF";
        input["externalVendorReference"] = "SYNTHETIC-VENDOR-REF"; input["externalCostContextReference"] = "SYNTHETIC-COST-REF";
        var value = Read(input); Assert.Equal(ErpReferenceFreshness.Current, value.Freshness);
        Assert.Equal("SYNTHETIC-PO-REF", value.Reference.ExternalPurchaseOrderReference);
        Assert.Equal("SYNTHETIC-COST-REF", value.Reference.ExternalCostContextReference); Assert.Null(value.Reference.ExternalStatus);
        input["kind"] = "CriticalSpareReference";
        Assert.True(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context() with { Kind = "CriticalSpareReference" }, out _));
    }
    [Theory]
    [InlineData("purchaseOrder")][InlineData("invoice")][InlineData("payment")][InlineData("payroll")]
    [InlineData("accountsPayable")][InlineData("accountsReceivable")][InlineData("generalLedger")]
    [InlineData("rfq")][InlineData("warehouseTransaction")][InlineData("charterParty")]
    [InlineData("amount")][InlineData("currency")][InlineData("bankAccount")][InlineData("actorId")][InlineData("permissions")]
    public void Closed_message_rejects_financial_mutations_and_caller_authority_fields(string field) {
        var input = Input(); input[field] = new JsonObject { ["command"] = "FICTIONAL-out-of-scope" };
        Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out var result)); Assert.Null(result);
    }
    [Theory]
    [InlineData("tenantId")][InlineData("vesselId")][InlineData("referenceId")][InlineData("kind")][InlineData("sourceSystem")][InlineData("sourceVersion")]
    public void Wire_binding_must_match_current_caller_supplied_authorized_context(string field) {
        var input = Input(); input[field] = field.EndsWith("Id", StringComparison.Ordinal) ? Guid.NewGuid().ToString() : "other-binding";
        Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out _));
    }
    [Fact]
    public void Duplicate_case_variant_nested_and_serialized_mutation_payloads_are_rejected() {
        var input = Input(); var json = input.ToJsonString().Insert(1, $"\"tenantId\":\"{Tenant}\",");
        Assert.False(ErpReferenceBoundary.TryRead(json, Context(), out _));
        input["TenantId"] = Tenant.ToString(); Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out _));
        input = Input(); input["externalReference"] = new JsonObject { ["payment"] = "FICTIONAL" };
        Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out _));
        input["externalReference"] = "{\"payment\":\"FICTIONAL\"}";
        Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out _));
    }
    [Theory]
    [InlineData("Paid")][InlineData("Approved")][InlineData("Delivered")][InlineData("Ready")]
    public void External_status_is_retained_as_opaque_source_text(string externalStatus) {
        var input = Input(); input["externalStatus"] = externalStatus;
        Assert.Equal(externalStatus, Read(input).Reference.ExternalStatus);
        // The projection is provenance/freshness only: no PO/payroll/fulfilment/readiness transition exists in this contract.
    }
    [Fact]
    public void Missing_null_stale_and_future_are_preserved_without_fabricated_current_external_state() {
        var input = Input(); Assert.Null(Read(input).Reference.ExternalStatus);
        input["externalStatus"] = null; Assert.Null(Read(input).Reference.ExternalStatus);
        input["observedAt"] = Now.AddHours(-2).ToString("O"); Assert.Equal(ErpReferenceFreshness.Stale, Read(input).Freshness);
        input["observedAt"] = Now.AddTicks(1).ToString("O"); Assert.Equal(ErpReferenceFreshness.Future, Read(input).Freshness);
        input["observedAt"] = "2026-11-01T15:00:00+03:00";
        var value = Read(input); Assert.Equal(ErpReferenceFreshness.Current, value.Freshness); Assert.Equal(TimeSpan.FromHours(3), value.Reference.ObservedAt.Offset);
        input["observedAt"] = "2026-11-01T12:00:00Z"; Assert.Equal(ErpReferenceFreshness.Current, Read(input).Freshness);
    }
    [Fact]
    public void Reconciliation_distinguishes_recovery_material_conflict_older_and_changed_source_binding() {
        var current = Read(Input()).Reference; var otherId = Guid.NewGuid();
        Assert.Equal(ErpReferenceReconciliation.Duplicate, ErpReferenceBoundary.Reconcile(current, current));
        Assert.Equal(ErpReferenceReconciliation.Duplicate, ErpReferenceBoundary.Reconcile(current, current with { MessageId = otherId }));
        Assert.Equal(ErpReferenceReconciliation.Conflict, ErpReferenceBoundary.Reconcile(current, current with { ExternalReference = "OTHER" }));
        Assert.Equal(ErpReferenceReconciliation.Conflict, ErpReferenceBoundary.Reconcile(current, current with { MessageId = otherId, ExternalReference = "OTHER" }));
        Assert.Equal(ErpReferenceReconciliation.Conflict, ErpReferenceBoundary.Reconcile(current, current with { SourceRevision = 3 }));
        Assert.Equal(ErpReferenceReconciliation.OlderReference, ErpReferenceBoundary.Reconcile(current, current with { MessageId = otherId, SourceRevision = 1 }));
        Assert.Equal(ErpReferenceReconciliation.NewerReference, ErpReferenceBoundary.Reconcile(current, current with { MessageId = otherId, SourceRevision = 3 }));
        Assert.Equal(ErpReferenceReconciliation.DifferentBinding, ErpReferenceBoundary.Reconcile(current, current with { TenantId = Guid.NewGuid() }));
        Assert.Equal(ErpReferenceReconciliation.DifferentBinding, ErpReferenceBoundary.Reconcile(current, current with { SourceVersion = "other-version" }));
    }
    [Fact]
    public void Json_schema_integer_equivalents_do_not_lose_revision_precision() {
        var json = Input().ToJsonString().Replace("\"contractVersion\":1", "\"contractVersion\":1.0", StringComparison.Ordinal)
            .Replace("\"sourceRevision\":2", "\"sourceRevision\":2e0", StringComparison.Ordinal);
        Assert.True(ErpReferenceBoundary.TryRead(json, Context(), out var result)); Assert.Equal(2, result!.Reference.SourceRevision);
        foreach (var token in new[] { "2.0000000000000000000000000000000000000001", "2e-100", "2e999999999999999999", "-2", "0e999999999999999999" }) {
            var precise = Input().ToJsonString().Replace("\"sourceRevision\":2", "\"sourceRevision\":" + token, StringComparison.Ordinal);
            Assert.False(ErpReferenceBoundary.TryRead(precise, Context(), out _));
        }
        var exact = Input().ToJsonString().Replace("\"sourceRevision\":2", "\"sourceRevision\":2.0000000000000000000000000000000000000000", StringComparison.Ordinal);
        Assert.True(ErpReferenceBoundary.TryRead(exact, Context(), out _));
        var fractional = Input(); fractional["sourceRevision"] = 2.5m;
        Assert.False(ErpReferenceBoundary.TryRead(fractional.ToJsonString(), Context(), out _));
        var safeLimit = Input(); safeLimit["sourceRevision"] = ErpReferenceBoundary.MaxSourceRevision;
        Assert.Equal(ErpReferenceBoundary.MaxSourceRevision, Read(safeLimit).Reference.SourceRevision);
    }
    [Theory]
    [InlineData("{]")][InlineData("[]")][InlineData("null")][InlineData("{}")]
    public void Invalid_json_shapes_have_no_projection(string json) => Assert.False(ErpReferenceBoundary.TryRead(json, Context(), out _));
    [Fact]
    public void Invalid_policy_versions_types_dates_limits_and_empty_ids_fail_closed() {
        Assert.False(ErpReferenceBoundary.TryRead(Input().ToJsonString(), Context() with { MaxAge = TimeSpan.Zero }, out _));
        Assert.False(ErpReferenceBoundary.TryRead(Input().ToJsonString(), Context() with { TenantId = Guid.Empty }, out _));
        Assert.False(ErpReferenceBoundary.TryRead(Input().ToJsonString(), Context() with { ReceivedAt = Now.ToOffset(TimeSpan.FromHours(3)) }, out _));
        foreach (var field in new[] { "contractVersion", "sourceRevision", "observedAt", "messageId" }) {
            var input = Input(); input[field] = "invalid"; Assert.False(ErpReferenceBoundary.TryRead(input.ToJsonString(), Context(), out _));
        }
        var tooLarge = Input(); tooLarge["externalReference"] = new string('a', ErpReferenceBoundary.MaxUtf8Bytes);
        Assert.False(ErpReferenceBoundary.TryRead(tooLarge.ToJsonString(), Context(), out _));
        var empty = Input(); empty["messageId"] = Guid.Empty.ToString(); Assert.False(ErpReferenceBoundary.TryRead(empty.ToJsonString(), Context(), out _));
        var unsafeRevision = Input(); unsafeRevision["sourceRevision"] = ErpReferenceBoundary.MaxSourceRevision + 1;
        Assert.False(ErpReferenceBoundary.TryRead(unsafeRevision.ToJsonString(), Context(), out _));
        var unknown = Input(); unknown["contractVersion"] = 2; Assert.False(ErpReferenceBoundary.TryRead(unknown.ToJsonString(), Context(), out _));
    }
}
