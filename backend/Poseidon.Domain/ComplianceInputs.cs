using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// Captured source records, never class adoption, issuer authentication or fitness decisions.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassPackReference(string? PackReference = null, string? Edition = null, string? SurveyReference = null, bool? IacsM79 = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MsmdReference(string? DocumentReference = null, string? IssuerReference = null, MsmdRoleRequirement[]? Roles = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MsmdRoleRequirement(Guid PositionId, int Count, string SourceLocator);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewRoleAssessmentReference(Guid? AssignmentId = null, long? AssignmentRevision = null, uint? AssignmentVersion = null,
    Guid? PositionId = null, uint? PositionVersion = null, DateOnly? From = null, DateOnly? To = null,
    string? Criterion = null, string? RecordReference = null, bool? Satisfied = null, CredentialInputReference? Credential = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CredentialInputReference(Guid Id, long Revision, uint Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedClassPack(string? PackReference, string? Edition, string? SourceLocator);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedRoleCriterion(string? Code, Guid? RuleId, long? RuleRevision, string? SourceLocator, bool? RequireCredential = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedEligibilityPolicy(Guid? PositionId, string? SourceLocator, QualifiedRoleCriterion[]? Criteria);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualifiedIsmDependency(Guid? RuleId, long? RuleRevision, string? SourceLocator);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvaluationInputReference(Guid EvaluationId, Guid RuleId, long RuleRevision);

public static class ComplianceInputs
{
    public static readonly string[] Fields = ["class.rulePack.IACS_M79", "regulatory.ismApplicable", "regulatory.msmdVerified", "crew.requiredRoleEligible", "evidence.sourceVerified"];
    public static readonly string[] Kinds = ["ClassPack", "Msmd", "CrewRoleAssessment"];
    public static bool Present(ControlEvidenceContent c) => c.ClassPack != null || c.Msmd != null || c.CrewRoleAssessment != null;
    public static string Kind(ControlEvidenceContent c) => c.ClassPack != null ? "ClassPack" : c.Msmd != null ? "Msmd" : "CrewRoleAssessment";
    public static void Validate(ControlEvidenceContent c) {
        if (c.ClassPack is { } p && new[] { p.PackReference, p.Edition, p.SurveyReference }.Any(x => !OperationEquipmentInputs.Text(x)) ||
            c.Msmd is { } m && new[] { m.DocumentReference, m.IssuerReference }.Any(x => !OperationEquipmentInputs.Text(x))) throw new ArgumentException("invalid_compliance_record");
        if (c.Msmd?.Roles is { } roles && (roles.Length > 128 || roles.Any(r => r == null || r.PositionId == Guid.Empty || r.Count is < 1 or > 1000 || !OperationEquipmentInputs.Text(r.SourceLocator) || r.SourceLocator == null) || roles.Select(r => r.PositionId).Distinct().Count() != roles.Length)) throw new ArgumentException("invalid_msmd_roles");
        if (c.CrewRoleAssessment is not { } a) return;
        if (a.Credential is { } credential && (credential.Id == Guid.Empty || credential.Revision <= 0 || credential.Version == 0)) throw new ArgumentException("invalid_credential_reference");
        if (a.AssignmentId == Guid.Empty || a.AssignmentRevision is <= 0 || a.AssignmentVersion == 0 || a.PositionId == Guid.Empty || a.PositionVersion == 0 ||
            (a.AssignmentId == null) != (a.AssignmentRevision == null) || (a.AssignmentId == null) != (a.AssignmentVersion == null) ||
            (a.PositionId == null) != (a.PositionVersion == null) || (a.From == null) != (a.To == null) || a.To <= a.From ||
            !OperationEquipmentInputs.Text(a.Criterion) || !OperationEquipmentInputs.Text(a.RecordReference)) throw new ArgumentException("invalid_role_assessment");
    }
    public static bool Complete(ControlEvidenceContent c) => c.ClassPack is { PackReference: not null, Edition: not null, SurveyReference: not null, IacsM79: not null } ||
        c.Msmd is { DocumentReference: not null, IssuerReference: not null, Roles: { Length: > 0 } } ||
        c.CrewRoleAssessment is { AssignmentId: not null, PositionId: not null, From: not null, To: not null, Criterion: not null, RecordReference: not null, Satisfied: not null };
    // Literal stateEffect from the controlled 09.04 sample/09.02 GOLD-004.
    // Keep applicability separate from readiness; missing MSMD never means N/A.
    public static bool ManningReview(ApplicabilityRule rule, IReadOnlyDictionary<string, ContextFact> facts) =>
        rule.Then.TryGetProperty("stateEffect", out var effect) && effect.GetString() == "VERIFIED_MSMD_REQUIRED" &&
        (!facts.TryGetValue("regulatory.msmdVerified", out var msmd) || msmd.Knowledge != "Known" || msmd.Value.ValueKind != System.Text.Json.JsonValueKind.True);
    // Only the source-qualified criterion set defines the conjunction. No built-in
    // clinical thresholds, rest hours, issuer trust or normative codebook.
    public static bool? Eligibility(IEnumerable<bool?> values) {
        var all = values.ToArray();
        return all.Length == 0 || all.Any(x => x == null) ? null : all.All(x => x == true);
    }
}
