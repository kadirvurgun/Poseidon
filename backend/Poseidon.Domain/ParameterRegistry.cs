using System.Collections.Frozen;

namespace Poseidon.Domain;

// PLAN-1511-5401 / 54.01 Rev4 (SHA256 73fcde36…6c892). A bounded, source-copied subset of the
// 354 CFG3 definitions (paragraph:939, :1004). Every literal below is copied from the exact
// PARAMETER_DEFINITION record at the cited locator; nothing is inferred. The registry is a
// software contract, not a production registration: every source record says
// runtimeStatus NOT_REGISTERED_NOT_IMPLEMENTED and acceptedOwnerBinding null, so no owner
// validation/impact adapter exists and no approved product default has a deployment acceptance.
// contracts/parameters.v1.json must equal this list (ParameterRegistryContractTests).
public sealed record ParameterTypeSchema(string Type, bool Nullable, long? Minimum, long? Maximum, string? Unit);

public sealed record ParameterDefinition(
    string Key,
    int DefinitionVersion,
    string OwnerDomain,
    string Classification,
    string? Risk,
    ParameterTypeSchema Type,
    string[] AllowedScopes,
    string[] RequiredSelectors,
    string Fallback,
    string? ApprovedProductDefault,
    bool ProductDefaultDeploymentAccepted,
    string? AliasTarget,
    string Sensitivity,
    string SourceLocator,
    string RuntimeStatus)
{
    // Only these classifications accept a generic CFG3 value write (paragraph:943, :180-181).
    public bool Writable => Classification is ParameterClassifications.Parameter or ParameterClassifications.PolicyRef or ParameterClassifications.TechnicalParameter;

    // Policy references, domain parameters and technical values stay distinct in every response.
    public string ValueClass => Classification switch
    {
        ParameterClassifications.PolicyRef => "POLICY",
        ParameterClassifications.TechnicalParameter => "TECHNICAL",
        ParameterClassifications.Parameter => "PARAMETER",
        _ => Classification
    };

    // Owner validation/impact adapters are registered by the owning module, never by 54.01.
    // None is registered yet; technical parameters and product invariants need no domain owner proof.
    public string OwnerVerification => Classification is ParameterClassifications.TechnicalParameter or ParameterClassifications.Invariant
        ? "NOT_REQUIRED" : "OWNER_ADAPTER_NOT_REGISTERED";
}

public static class ParameterClassifications
{
    public const string Parameter = "PARAMETER";
    public const string PolicyRef = "POLICY_REF";
    public const string TechnicalParameter = "TECHNICAL_PARAMETER";
    public const string OwnerField = "OWNER_FIELD";
    public const string Invariant = "INVARIANT";
    public const string Alias = "ALIAS";
    public const string FamilyReference = "FAMILY_REFERENCE";
}

public static class ParameterRegistry
{
    public const string SourcePath = "analiz/POSEIDON GUNCEL 30.10.2026/son analizler 15.11.26/54.01_POSEIDON_Islev_Parametreleri_ve_Kontrollu_Politika_Sicili_Tam_Analiz_Rev4(1).docx";
    public const string SourceSha256 = "73fcde366550ff1eb23aaebb33c8d246f4173b03b5c416a1b74736078a36c892";
    // REGISTRATION_COMMON_definitionCount, paragraph:1004. Tracked one by one in the inventory document.
    public const int SourceDefinitionCount = 354;
    public const string Runtime = "FOUNDATION_REGISTERED_OWNER_ADAPTER_NOT_REGISTERED";
    // Source records carry no sensitivity classification; values are withheld from audit/logs regardless.
    public const string SensitivitySourceSilent = "NOT_DECLARED_IN_SOURCE_VALUES_WITHHELD";

    private static ParameterDefinition D(string key, string owner, string classification, string? risk, ParameterTypeSchema type,
        string[] scopes, string[] selectors, string fallback, string? productDefault, string locator, string? alias = null) =>
        new(key, 1, owner, classification, risk, type, scopes, selectors, fallback, productDefault, false, alias,
            SensitivitySourceSilent, locator, Runtime);

    private static ParameterTypeSchema Int(long min, long max, string unit) => new("INTEGER", false, min, max, unit);
    private static readonly ParameterTypeSchema RevisionRef = new("REVISION_REF", true, null, null, null);

    public static IReadOnlyList<ParameterDefinition> Definitions { get; } =
    [
        // DEV-020 freshness decision: UUID no default; positive release closed while unconfigured (paragraph:656-657, :2313).
        D("ops.source.freshness_policy_ref", "60.01", ParameterClassifications.PolicyRef, "SAFETY", RevisionRef,
            ["TENANT"], ["sourceOwner", "sourceKind", "purpose"], "BLOCK", null, "paragraph:2313"),
        D("regulatory.source.freshness_policy_ref", "60.03", ParameterClassifications.PolicyRef, "SAFETY", RevisionRef,
            ["TENANT"], ["sourceKind", "purpose"], "BLOCK", null, "paragraph:2523"),
        D("pms.measurement.freshness_minutes", "59.04", ParameterClassifications.Parameter, "SAFETY", Int(1, 10080, "MINUTE"),
            ["TENANT"], ["equipmentTypeId", "measurementPointId"], "APPROVED_DEFAULT", "60", "paragraph:2211"),
        D("rpt.freshness.online_min", "55.01", ParameterClassifications.Parameter, "HIGH", Int(1, 60, "MINUTE"),
            ["GLOBAL", "TENANT"], ["datasetId"], "APPROVED_DEFAULT", "15", "paragraph:1565"),
        D("rpt.export.rows", "55.01", ParameterClassifications.Parameter, "HIGH", Int(1, 100000, "COUNT"),
            ["GLOBAL", "TENANT"], [], "APPROVED_DEFAULT", "10000", "paragraph:1120"),
        D("regulatory.cache.max_age_seconds", "60.03", ParameterClassifications.Parameter, "SAFETY", Int(1, 60, "SECOND"),
            ["TENANT"], [], "APPROVED_DEFAULT", "60", "paragraph:2496"),
        D("control.assurance.maxAgeSeconds", "54.01 technical", ParameterClassifications.Parameter, "SAFETY", new("INTEGER", false, 1, 300, null),
            ["COMPANY", "VESSEL"], [], "APPROVED_DEFAULT", "60", "paragraph:3602"),
        D("mobile.offline.idleLockSeconds", "63.03", ParameterClassifications.TechnicalParameter, "HIGH", new("INTEGER", false, 60, 900, null),
            ["VESSEL"], [], "APPROVED_DEFAULT", "300", "paragraph:4315"),
        // Native owner field: changes only inside the owner revision lifecycle (CFG3-T15, paragraph:221).
        D("safety.deferral.max_count", "59.02", ParameterClassifications.OwnerField, null, Int(0, 20, "COUNT"),
            ["TENANT"], ["actionType"], "BLOCK", null, "paragraph:2051"),
        D("common.list.default_limit", "58.03", ParameterClassifications.Invariant, null, Int(25, 25, "COUNT"),
            ["GLOBAL"], [], "APPROVED_DEFAULT", "25", "paragraph:1912"),
        // Explicit consumer binding of the canonical export quota, stores no separate value (paragraph:39).
        D("tug.history.export.max_rows", "60.02", ParameterClassifications.Alias, null, new("INTEGER", false, null, null, "COUNT"),
            ["TENANT"], [], "BLOCK", null, "paragraph:2479", alias: "rpt.export.rows"),
        // Legacy family reference: without owner+purpose context the answer is UNKNOWN (CFG3-T25, paragraph:241).
        D("eligibility.rule_version", "54.01", ParameterClassifications.FamilyReference, null, new("FAMILY", false, null, null, null),
            ["TENANT"], ["ownerDomain", "purpose", "vesselId", "positionId"], "BLOCK", null, "paragraph:3195"),
    ];

    private static readonly FrozenDictionary<string, ParameterDefinition> ByKey = Definitions.ToFrozenDictionary(x => x.Key, StringComparer.Ordinal);
    public static ParameterDefinition? Find(string? key) => key != null && ByKey.TryGetValue(key, out var value) ? value : null;
}
