namespace Poseidon.Domain;
public sealed record MaintenanceObservationValue(Guid Id, Guid TenantId, Guid VesselId, Guid AssetId, string Source,
    string PolicyVersion, long SourceRevision, string Kind, string Unit, string? Value, DateTimeOffset? ObservedAt, bool Revoked);
public sealed record MaintenanceObservationRule(Guid TenantId, Guid VesselId, Guid AssetId, string Source, string Version,
    string Kind, string Unit, int FreshnessSeconds, int? MaxScale);
public sealed record MaintenanceObservationAssessment(string State, string PolicyVersion, MaintenanceInputs Inputs)
{
    public bool Ready => State == "ConfiguredDeclaredObservation";
}
public static class MaintenanceObservationPolicy
{
    public static MaintenanceObservationAssessment Assess(MaintenanceObservationValue? observation, MaintenanceObservationRule? rule,
        Guid tenant, Guid vessel, Guid asset, MaintenanceInputs input, DateTimeOffset serverNow) {
        MaintenanceObservationAssessment Fail(string state) => new(state, rule?.Version ?? "Unavailable", input);
        if (observation == null) return Fail("ObservationMissing");
        if (observation.TenantId != tenant || observation.VesselId != vessel || observation.AssetId != asset) return Fail("ObservationScopeMismatch");
        if (observation.Revoked) return Fail("ObservationRevoked");
        if (rule == null || rule.TenantId != tenant || rule.VesselId != vessel || rule.AssetId != asset || rule.Source != observation.Source ||
            rule.FreshnessSeconds <= 0 || rule.MaxScale is null or < 0 or > 28 || string.IsNullOrWhiteSpace(rule.Version)) return Fail("ObservationPolicyUnavailable");
        if (rule.Version != observation.PolicyVersion) return Fail("ObservationPolicyChanged");
        if (observation.Kind != input.TriggerType || observation.Kind != rule.Kind || observation.Unit != rule.Unit || input.IntervalUnit != rule.Unit) return Fail("ObservationUnitOrKindMismatch");
        if (observation.ObservedAt == null || observation.Value == null) return Fail("ObservationMissing");
        if (observation.ObservedAt > serverNow) return Fail("ObservationFuture");
        if ((serverNow - observation.ObservedAt.Value).TotalSeconds > rule.FreshnessSeconds) return Fail("ObservationStale");
        if (observation.Kind == "Meter" && (!CertificateMetadataPolicy.ExactNumeric(observation.Value) || !MaintenanceCalculation.Number(observation.Value, out _) ||
            observation.Value.Contains('.') && observation.Value.Length - observation.Value.IndexOf('.') - 1 > rule.MaxScale.Value)) return Fail("ObservationPrecisionUnsupported");
        if (observation.Kind == "Condition" && observation.Value is not ("True" or "False")) return Fail("ObservationConditionUnknown");
        return new("ConfiguredDeclaredObservation", rule.Version, input with { CurrentMeter = observation.Kind == "Meter" ? observation.Value : null,
            Condition = observation.Kind == "Condition" ? observation.Value : null, ObservedAt = observation.ObservedAt,
            ObservationRevision = $"{observation.Id:D}/{observation.SourceRevision}", MeterSource = observation.Source,
            Provenance = "ConfiguredSourceDeclaredUnverified" });
    }
}
