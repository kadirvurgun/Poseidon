using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// PLAN-1511-5402 / 54.02 Rev4 A02-004, A03-002, A03-007, A03-008 (§23), §25.
// Additive exact-action foundation. It derives decisions from the CURRENT server-provisioned
// membership only and never from request bodies, IdP role labels or compatibility aliases.
// Every legacy derivation below is equal-or-narrower than the predicate it replaces;
// LegacyCompatibility rows are documentation/grouping, never a runtime grant (54.02 paragraph:658).

public enum ExactScopeKind { Tenant, Vessel }

// Source state of the exact key itself. SOURCE_REGISTERED: literal key exists in 54.02 §25,
// registry record is still SPECIFIED/DISABLED_UNTIL_VERIFIED for production (paragraph:821).
// LOCAL_PROVISIONAL: no exact owner key published yet; local key is not a canonical grant.
public enum ExactActionSourceState { SourceRegistered, LocalProvisional }

// Legacy predicate the exact action is derived from. All flags are AND conditions.
public sealed record LegacyDerivation(
    string[] RequiredPermissions,
    bool RequiresWriteAttribution,
    bool RequiresDirectoryReadScope,
    bool RequiresTenantWideRead,
    bool RequiresVesselEditGrant,
    string ReplacedPredicate);

public sealed record ExactActionDefinition(
    string Key,
    string Route,
    ExactScopeKind ScopeKind,
    string[] Purposes,
    ExactActionSourceState SourceState,
    string SourceLocator,
    LegacyDerivation Legacy);

public sealed record AccessGrant(Guid VesselId, bool CanEdit);

// Trusted subject projection of the current membership. Built server-side only.
public sealed record AccessSubject(
    Guid ActorId,
    Guid TenantId,
    Guid? ActiveCompanyId,
    IReadOnlyCollection<string> Permissions,
    bool TenantWideRead,
    bool TenantWideManage,
    IReadOnlyCollection<AccessGrant> VesselGrants,
    string? RoleContext)
{
    // Fingerprint of every authority input. A change (grant, revoke, scope, role reference,
    // active company) changes the epoch. This is an in-process source epoch, not the persisted
    // 54.02 authorizationEpoch counter (open item P5402-OPEN-02).
    public string Epoch()
    {
        var canonical = new StringBuilder()
            .Append(ActorId.ToString("D")).Append('|').Append(TenantId.ToString("D")).Append('|')
            .Append(ActiveCompanyId?.ToString("D") ?? "-").Append('|')
            .Append(string.Join(',', Permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))).Append('|')
            .Append(TenantWideRead ? '1' : '0').Append(TenantWideManage ? '1' : '0').Append('|')
            .Append(string.Join(',', VesselGrants.Select(g => g.VesselId.ToString("D") + (g.CanEdit ? ":e" : ":r")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))).Append('|')
            .Append(RoleContext ?? "-");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}

public sealed record ExactAccessRequest(string ActionKey, string Purpose, Guid TenantId, Guid? VesselId);

public sealed record ExactAccessDecision(bool Allowed, string ReasonCode, string ActionKey, string? Epoch)
{
    public static ExactAccessDecision Deny(string reason, string action, string? epoch = null) => new(false, reason, action, epoch);
}

// Commit-time outcome (54.02 A02-004 step 8, A03-007 step 8).
public sealed record ExactRevalidation(bool Allowed, string ReasonCode, bool EpochChanged);

public static class ExactAccessReasons
{
    public const string Allow = "ALLOW";
    public const string MembershipInactive = "ACCOUNT_OR_MEMBERSHIP_INACTIVE";
    public const string ActionNotEnabled = "ACTION_NOT_ENABLED";
    public const string ScopeMismatch = "SCOPE_MISMATCH";
    public const string PurposeNotPermitted = "PURPOSE_NOT_PERMITTED";
    public const string WriteAttributionRequired = "WRITE_ATTRIBUTION_REQUIRED";
    public const string NoGrant = "NO_GRANT";
    public const string SodViolation = "SOD_VIOLATION";
    public const string SodMakerUnknown = "SOD_MAKER_UNKNOWN";
    public const string AccessContextChanged = "ACCESS_CONTEXT_CHANGED";
}

public static class ExactActionCatalog
{
    private const string Doc = "54.02_POSEIDON_Rol_Grup_Menu_ve_Islem_Yetkileri_Tam_Analiz_Rev4";
    private static readonly string[] DirectoryEdit = ["directory.read", "directory.write"];
    private static readonly string[] RegulatoryWrite = ["directory.read", "regulatory.draft.read", "regulatory.draft.write"];

    // Same predicate as TrustedActor.CanEditVessel(id).
    private static readonly LegacyDerivation VesselEdit = new(DirectoryEdit, true, true, false, true,
        "TrustedActor.CanEditVessel(vessel)");
    // Same predicate as TrustedActor.CanWriteRegulatory.
    private static readonly LegacyDerivation RegulatoryRegisterWrite = new(RegulatoryWrite, true, true, true, false,
        "TrustedActor.CanWriteRegulatory");

    public const string VesselRegulatoryProfileDraftSave = "vessel.regulatory_profile.draft.save";
    public const string VesselDirectoryUpdate = "vessel.directory.update";
    public const string RegulatorySourceCreate = "regulatory.source.create";
    public const string RegulatorySourceEdit = "regulatory.source.edit";
    public const string RegulatoryRequirementCreate = "regulatory.requirement.create";
    public const string RegulatoryRequirementEdit = "regulatory.requirement.edit";

    public const string PurposeRegulatoryProfileDraft = "vessel.regulatory_profile.draft";
    public const string PurposeDirectoryMaintenance = "vessel.directory.maintenance";
    public const string PurposeRegisterDraft = "regulatory.register.draft";

    public static IReadOnlyDictionary<string, ExactActionDefinition> Actions { get; } = new ExactActionDefinition[]
    {
        new(VesselRegulatoryProfileDraftSave, "PUT /api/v1/vessels/{id}/regulatory-profile", ExactScopeKind.Vessel,
            [PurposeRegulatoryProfileDraft], ExactActionSourceState.LocalProvisional,
            Doc + " paragraph:821 (no published owner key; 59.01/60.x owner registry pending)", VesselEdit),
        new(VesselDirectoryUpdate, "PUT /api/v1/vessels/{id}", ExactScopeKind.Vessel,
            [PurposeDirectoryMaintenance], ExactActionSourceState.LocalProvisional,
            // 59.01 publishes the fleet write literal `fleet.vessel.edit` (59.01 paragraph:589 "DraftVesselDto;
            // fleet.vessel.edit") and 54.02 registers it on PUT /api/v1/fleet/vessels/{id}/draft (54.02
            // paragraph:15755, PROPOSED_DISABLED_DISTINCT_SELECTOR_KEY / DISABLED_UNTIL_VERIFIED, owner 59.01).
            // That registry record is not activated and its route differs, so the local key stays provisional
            // until the 59.01 owner registry exists. Mapping target: fleet.vessel.edit (MIGRATION_REVIEW).
            Doc + " paragraph:15755 + 59.01 paragraph:589 (mapping target fleet.vessel.edit; owner registry 59.01 DISABLED_UNTIL_VERIFIED)", VesselEdit),
        new(RegulatorySourceCreate, "POST /api/v1/source-drafts", ExactScopeKind.Tenant,
            [PurposeRegisterDraft], ExactActionSourceState.SourceRegistered, Doc + " paragraph:4081", RegulatoryRegisterWrite),
        new(RegulatorySourceEdit, "PUT /api/v1/source-drafts/{id}", ExactScopeKind.Tenant,
            [PurposeRegisterDraft], ExactActionSourceState.SourceRegistered, Doc + " paragraph:4087", RegulatoryRegisterWrite),
        new(RegulatoryRequirementCreate, "POST /api/v1/requirement-drafts", ExactScopeKind.Tenant,
            [PurposeRegisterDraft], ExactActionSourceState.SourceRegistered, Doc + " paragraph:4114", RegulatoryRegisterWrite),
        new(RegulatoryRequirementEdit, "PUT /api/v1/requirement-drafts/{id}", ExactScopeKind.Tenant,
            [PurposeRegisterDraft], ExactActionSourceState.LocalProvisional,
            Doc + " paragraph:4114 (only create published; edit key is a source gap)", RegulatoryRegisterWrite),
    }.ToDictionary(x => x.Key, StringComparer.Ordinal);

    // Legacy permission → exact action compatibility (54.02 paragraph:658, 809, 813).
    // Status MAPPED_EQUAL: exact action derives from the same predicate, never wider.
    // MIGRATION_REVIEW_REQUIRED: no exact action is enabled from this legacy key.
    public sealed record LegacyCompatibility(string LegacyPermission, string[] ExactActionKeys, string Status, string Note);
    public static IReadOnlyList<LegacyCompatibility> Compatibility { get; } =
    [
        new("directory.write", [VesselRegulatoryProfileDraftSave, VesselDirectoryUpdate], "MAPPED_EQUAL",
            "Vessel-scoped only with CanEdit grant or TenantWideManage; company create/update stay on CanManage (unmigrated). " +
            "vessel.directory.update is local; MIGRATION_REVIEW mapping target fleet.vessel.edit (59.01 p:589, 54.02 p:15755)."),
        // CanManage writes stay on their legacy predicate. Published 59.01 candidates (not enabled here):
        // fleet.vessel.create (59.01 p:586, 54.02 p:15753), fleet.company.edit (59.01 p:616, 792),
        // fleet.vessel.retire/reactivate/merge (59.01 p:670, 804). Registry state DISABLED_UNTIL_VERIFIED.
        new("directory.write+CanManage", [], "MIGRATION_REVIEW_REQUIRED",
            "Company create/update and vessel create; candidates fleet.vessel.create, fleet.company.edit, fleet.vessel.retire/reactivate/merge (59.01 owner registry pending)."),
        new("regulatory.draft.write", [RegulatorySourceCreate, RegulatorySourceEdit, RegulatoryRequirementCreate, RegulatoryRequirementEdit], "MAPPED_EQUAL",
            "Tenant-wide read scope + regulatory.draft.read required; never approve/publish/legal_review."),
        new("rules.write", [], "MIGRATION_REVIEW_REQUIRED", "Rule authoring unmigrated; no rule.validate/approve derived."),
        new("rules.review", [], "MIGRATION_REVIEW_REQUIRED", "Review is a separate checker action; not derivable from write."),
        new("notification.manage", [], "MIGRATION_REVIEW_REQUIRED", "Broad manage key; not a runtime exact action."),
        new("human.verify", [], "MIGRATION_REVIEW_REQUIRED", "Verifier action keeps existing HumanAuthority grant + SoD helper."),
    ];

    // Segments that a legacy broad write/manage key must never produce (54.02 paragraph:813).
    public static readonly string[] CheckerSegments = ["approve", "verify", "release", "publish", "legal_review", "activate", "attest"];
}

public static class ExactActionResolver
{
    public static ExactAccessDecision Evaluate(AccessSubject? subject, ExactAccessRequest request)
    {
        var action = request.ActionKey;
        if (subject == null || subject.ActorId == Guid.Empty || subject.TenantId == Guid.Empty)
            return ExactAccessDecision.Deny(ExactAccessReasons.MembershipInactive, action);
        var epoch = subject.Epoch();
        if (!ExactActionCatalog.Actions.TryGetValue(action, out var definition))
            return ExactAccessDecision.Deny(ExactAccessReasons.ActionNotEnabled, action, epoch);
        if (request.TenantId != subject.TenantId ||
            definition.ScopeKind == ExactScopeKind.Vessel && (request.VesselId == null || request.VesselId == Guid.Empty) ||
            definition.ScopeKind == ExactScopeKind.Tenant && request.VesselId != null)
            return ExactAccessDecision.Deny(ExactAccessReasons.ScopeMismatch, action, epoch);
        if (!definition.Purposes.Contains(request.Purpose, StringComparer.Ordinal))
            return ExactAccessDecision.Deny(ExactAccessReasons.PurposeNotPermitted, action, epoch);
        var legacy = definition.Legacy;
        if (legacy.RequiresWriteAttribution && subject.RoleContext == null)
            return ExactAccessDecision.Deny(ExactAccessReasons.WriteAttributionRequired, action, epoch);
        if (legacy.RequiredPermissions.Any(p => !subject.Permissions.Contains(p)))
            return ExactAccessDecision.Deny(ExactAccessReasons.NoGrant, action, epoch);
        var wide = subject.TenantWideRead || subject.TenantWideManage;
        if (legacy.RequiresDirectoryReadScope && !wide && subject.VesselGrants.Count == 0)
            return ExactAccessDecision.Deny(ExactAccessReasons.NoGrant, action, epoch);
        if (legacy.RequiresTenantWideRead && !wide)
            return ExactAccessDecision.Deny(ExactAccessReasons.NoGrant, action, epoch);
        if (legacy.RequiresVesselEditGrant && !subject.TenantWideManage &&
            !subject.VesselGrants.Any(g => g.VesselId == request.VesselId && g.CanEdit))
            return ExactAccessDecision.Deny(ExactAccessReasons.NoGrant, action, epoch);
        return new(true, ExactAccessReasons.Allow, action, epoch);
    }

    // Re-resolve the CURRENT subject before commit. An epoch change is recomputed, never trusted
    // from the earlier allow; access loss denies. Identity/tenant drift is a context change.
    public static ExactRevalidation Revalidate(ExactAccessDecision prior, AccessSubject? current, ExactAccessRequest request, Guid expectedActor)
    {
        if (!prior.Allowed) return new(false, prior.ReasonCode, false);
        if (current == null || current.ActorId != expectedActor || current.TenantId != request.TenantId)
            return new(false, ExactAccessReasons.AccessContextChanged, true);
        var now = Evaluate(current, request);
        return new(now.Allowed, now.ReasonCode, now.Epoch != prior.Epoch);
    }
}

// Dynamic SoD (54.02 A02-006 paragraph:182, A02-005 paragraph:158): on the SAME record the
// checker must be a known real actor different from every maker. Unknown makers fail closed.
public static class SeparationOfDuties
{
    public static string? Evaluate(Guid checker, params Guid?[] makers)
    {
        if (checker == Guid.Empty || makers.Length == 0 || makers.Any(m => m is null || m == Guid.Empty))
            return ExactAccessReasons.SodMakerUnknown;
        return makers.Any(m => m == checker) ? ExactAccessReasons.SodViolation : null;
    }
    public static bool Independent(Guid checker, params Guid?[] makers) => Evaluate(checker, makers) == null;
}
