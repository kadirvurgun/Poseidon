using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// Recorded owning documents. Null is an incomplete draft; no authority is asserted
// by capture. Source qualification and independent verification are separate commands.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OperationReference(Guid? JobId = null, long? JobRevision = null, uint? JobVersion = null,
    long? PlanRevision = null, uint? PlanVersion = null, string? Profile = null,
    Guid? ServiceAuthorizationId = null, long? ServiceAuthorizationRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServiceAuthorizationReference(Guid? JobId = null, long? JobRevision = null, uint? JobVersion = null,
    long? PlanRevision = null, uint? PlanVersion = null, string? ServiceContext = null, string? IssuerReference = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CargoPlanReference(string? PlanReference = null, bool? PackagedDangerousGoods = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WinchFitmentReference(Guid? AssetId = null, long? AssetRevision = null, uint? AssetVersion = null,
    bool? Fitted = null, string? RegisterReference = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OwningInputReferences(Guid? OperationId = null, Guid? CargoPlanId = null, Guid? WinchFitmentId = null,
    Guid? ClassPackId = null, Guid? MsmdId = null, Guid[]? RoleAssessmentIds = null,
    Guid? SourceEvidenceId = null, EvaluationInputReference? Ism = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedLiteralBinding(string Field, string? OwnerValue, string? RuleValue, string? SourceLocator);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedOwningDocumentPolicy(string DocumentKind, Guid? AuthorRole, Guid? VerifierRole, string? SourceLocator);

public static class OperationEquipmentInputs
{
    public static readonly string[] Fields = ["vessel.operatingArea", "operation.jobType", "operation.profile", "operation.serviceContext", "cargo.packagedDangerousGoods", "equipment.towingWinchFitted"];
    public static bool Present(ControlEvidenceContent c) => c.Operation != null || c.ServiceAuthorization != null || c.CargoPlan != null || c.WinchFitment != null || ComplianceInputs.Present(c);
    public static string Kind(ControlEvidenceContent c) => c.Operation != null ? "Operation" : c.ServiceAuthorization != null ? "ServiceAuthorization" : c.CargoPlan != null ? "CargoPlan" : c.WinchFitment != null ? "WinchFitment" : ComplianceInputs.Kind(c);
    public static bool Text(string? s) => s == null || s.Length is > 0 and <= 2000 && s.Trim() == s && !s.Any(char.IsControl);
    public static void Validate(string kind, ControlEvidenceContent c) {
        if (!Present(c)) return;
        if (kind != "Evidence" || new object?[] { c.Operation, c.ServiceAuthorization, c.CargoPlan, c.WinchFitment, c.ClassPack, c.Msmd, c.CrewRoleAssessment, c.TugCapability }.Count(x => x != null) != 1)
            throw new ArgumentException("invalid_owning_document_kind");
        ComplianceInputs.Validate(c);
        var o = c.Operation; var s = c.ServiceAuthorization; var w = c.WinchFitment;
        var job = o?.JobId ?? s?.JobId; var revision = o?.JobRevision ?? s?.JobRevision; var version = o?.JobVersion ?? s?.JobVersion;
        var plan = o?.PlanRevision ?? s?.PlanRevision; var planVersion = o?.PlanVersion ?? s?.PlanVersion;
        if (job == Guid.Empty || revision is <= 0 || version == 0 || plan is <= 0 || planVersion == 0 ||
            (job == null) != (revision == null) || (job == null) != (version == null) || (plan == null) != (planVersion == null) || plan != null && job == null ||
            o?.ServiceAuthorizationId == Guid.Empty || o?.ServiceAuthorizationRevision is <= 0 || (o?.ServiceAuthorizationId == null) != (o?.ServiceAuthorizationRevision == null) ||
            w?.AssetId == Guid.Empty || w?.AssetRevision is <= 0 || w?.AssetVersion == 0 || (w?.AssetId == null) != (w?.AssetRevision == null) || (w?.AssetId == null) != (w?.AssetVersion == null) ||
            new[] { o?.Profile, s?.ServiceContext, s?.IssuerReference, c.CargoPlan?.PlanReference, w?.RegisterReference }.Any(x => !Text(x)))
            throw new ArgumentException("invalid_owning_reference");
    }
    public static bool Complete(ControlEvidenceContent c) => c.Operation is { JobId: not null, PlanRevision: not null, Profile: not null, ServiceAuthorizationId: not null } ||
        c.ServiceAuthorization is { JobId: not null, PlanRevision: not null, ServiceContext: not null, IssuerReference: not null } ||
        c.CargoPlan is { PlanReference: not null, PackagedDangerousGoods: not null } ||
        c.WinchFitment is { AssetId: not null, Fitted: not null, RegisterReference: not null } || ComplianceInputs.Complete(c);
    public static void ValidateBindings(QualifiedLiteralBinding[]? bindings) {
        if (bindings == null) return;
        if (bindings.Length > 256 || bindings.Any(b => b == null || !Fields.Take(4).Contains(b.Field) ||
            new[] { b.OwnerValue, b.RuleValue, b.SourceLocator }.Any(s => !Text(s))) ||
            bindings.Where(b => b.OwnerValue != null).Select(b => (b.Field, b.OwnerValue)).Distinct().Count() != bindings.Count(b => b.OwnerValue != null))
            throw new ArgumentException("invalid_qualified_literal_binding");
    }
    // Even identical literals need a reviewed source binding; never silently alias intake labels.
    public static string? Map(QualifiedLiteralBinding[]? bindings, string field, string? value) =>
        value == null ? null : bindings?.SingleOrDefault(b => b.Field == field && b.OwnerValue == value)?.RuleValue;
}
