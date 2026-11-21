using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// USER-ADMIN-01. Fixed role bundles derived from 13.03 v46.3 "Rol Bazlı Kullanıcı El Kitabı" role sections
// (Master §2 paragraph:25-48, Tug Master §3 paragraph:50-56, DPA/HSEQ §4 paragraph:57-78, Chief Engineer §5
// paragraph:79-85, Crew/OOW §6 paragraph:86-91, Shore Ops/Tug Coordinator §7 paragraph:92-96, Regulatory/Assurance
// §8 paragraph:97-101, System/Tenant Admin §9 paragraph:102-106; least-privilege principle paragraph:11-12).
// A bundle is a fixed, code-versioned list of existing server permissions; it is a technical starting set, not an
// approved production role catalog. Decision-authority permissions (decide/verify/approve/attest/sign/verdict,
// human sign-offs, security suspension, purge approval) are never part of a bundle: they are assigned individually
// by an administrator who holds them, and appointment-gated actions additionally need the operator-provisioned
// appointment (TugOperations/Departure/... authorities), which this package never creates.
public sealed record RoleBundle(string Code, Guid RoleId, string Locator, string[] Permissions);

public static class MembershipRoleBundles
{
    private static Guid RoleId(string code) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("POSEIDON-ROLE-BUNDLE-v1:" + code)).AsSpan(0, 16));

    private static RoleBundle Bundle(string code, string locator, params string[] permissions) => new(code, RoleId(code), locator, permissions);

    public static readonly IReadOnlyList<RoleBundle> All =
    [
        Bundle("master", "13.03 §2 paragraph:25-48",
            "directory.read", "workspace.search", "notification.read", "notification.acknowledge", "document.acknowledge",
            "daily.noon.read", "daily.noon.write", "daily.noon.review", "daily.deck.read", "daily.deck.write", "daily.deck.review",
            "departure.readiness.read", "crew.read", "crew.eligibility.read", "crew.safe_manning.read", "crew.competence.read",
            "equipment.read", "maintenance.read", "workorder.read", "defect.read", "certificate.read",
            "hse.risk.read", "hse.risk.write", "hse.risk.acknowledge", "hse.safety_event.read", "hse.safety_event.report",
            "operations.port.read", "operations.port.write"),
        Bundle("tug-master", "13.03 §3 paragraph:50-56",
            "directory.read", "workspace.search", "mode.tug", "tug.read", "tug.write", "tug.plan.write",
            "notification.read", "notification.acknowledge", "document.acknowledge",
            "crew.read", "crew.eligibility.read", "crew.safe_manning.read", "crew.competence.read",
            "equipment.read", "defect.read", "hse.risk.read", "hse.risk.write", "hse.risk.acknowledge",
            "hse.safety_event.read", "hse.safety_event.report"),
        Bundle("dpa-hseq", "13.03 §4 paragraph:57-78",
            "directory.read", "workspace.search", "notification.read",
            "hse.risk.read", "hse.risk_template.read", "hse.risk_template.write", "hse.risk_template.suggest", "hse.high_risk_work.read",
            "hse.safety_event.read", "hse.safety_event.triage", "hse.safety_event.investigate", "hse.safety_event.review", "hse.safety_action.write",
            "assurance.inspection.read", "assurance.evidence.write", "assurance.finding.link", "control.read", "evidence.read"),
        Bundle("chief-engineer", "13.03 §5 paragraph:79-85",
            "directory.read", "workspace.search", "notification.read",
            "equipment.read", "equipment.write", "equipment.history.read", "maintenance.read", "maintenance.write", "maintenance.history.read",
            "maintenance.measurement.write", "maintenance.postponement.request", "maintenance.observation.write",
            "workorder.read", "workorder.write", "workorder.execute", "defect.read", "defect.write",
            "service.report.read", "service.report.write", "spare.read", "spare.write", "requisition.propose",
            "drydock.read", "drydock.write", "daily.engine.read", "daily.engine.write", "daily.engine.review", "certificate.read"),
        Bundle("crew-oow", "13.03 §6 paragraph:86-91",
            "directory.read", "notification.read", "notification.acknowledge", "document.acknowledge",
            "daily.deck.read", "daily.deck.write", "daily.engine.read", "daily.engine.write",
            "workorder.read", "workorder.execute", "maintenance.observation.write", "hse.risk.acknowledge", "hse.safety_event.report"),
        Bundle("shore-ops", "13.03 §7 paragraph:92-96",
            "directory.read", "workspace.search", "notification.read", "mode.tug", "mode.cross", "tug.read", "tug.write",
            "operations.downtime.read", "operations.downtime.write", "defect.read"),
        Bundle("regulatory-assurance", "13.03 §8 paragraph:97-101",
            "directory.read", "workspace.search", "regulatory.draft.read", "regulatory.draft.write", "regulatory.review.read",
            "applicability.read", "applicability.evaluate", "rules.write", "control.read", "control.write",
            "evidence.read", "evidence.write", "assurance.inspection.read", "certificate.read"),
        Bundle("tenant-admin", "13.03 §9 paragraph:102-106",
            "directory.read", "directory.write", "workspace.search", "membership.read", "membership.manage",
            "audit.read", "config.read", "config.draft", "system.health.read", "notification.read"),
        Bundle("read-only", "13.03 paragraph:11-12 (least privilege)",
            "directory.read", "workspace.search", "notification.read", "certificate.read", "equipment.read", "maintenance.read",
            "workorder.read", "defect.read", "daily.noon.read", "daily.deck.read", "daily.engine.read"),
    ];

    public static readonly string[] Codes = [.. All.Select(b => b.Code)];

    public static RoleBundle? Find(string? code) => All.FirstOrDefault(b => string.Equals(b.Code, code, StringComparison.Ordinal));

    // Permissions that record a human decision, verification, approval, attestation or a security/purge authority.
    public static bool IsDecisionAuthority(string permission) =>
        permission.StartsWith("human.", StringComparison.Ordinal) && permission != "human.review.read" && permission != "human.trace.read" ||
        permission.EndsWith(".decide", StringComparison.Ordinal) || permission.EndsWith(".verify", StringComparison.Ordinal) ||
        permission.EndsWith(".approve", StringComparison.Ordinal) || permission.EndsWith(".attest", StringComparison.Ordinal) ||
        permission.EndsWith(".sign", StringComparison.Ordinal) || permission.EndsWith(".verdict", StringComparison.Ordinal) ||
        permission.EndsWith(".effectiveness", StringComparison.Ordinal) || permission.EndsWith(".finalize", StringComparison.Ordinal) ||
        permission is "membership.security.suspend" or "certificate.verify.record" or "regulatory.qualify" or "regulatory.facts.attest" or
            "crew.catalog.govern" or "uat.attest" or "import.reconciliation.sign" or "record.purge.approve" or "workorder.close" or
            "defect.close" or "workorder.release" or "certificate.renewal.complete" or "document.publish" or "hse.risk_template.publish" or
            "hse.risk_method.publish" or "applicability.override.approve" or "applicability.override.revoke" or "rules.review" or "config.approve";

    // Module grouping for display: the segment before the first '.'.
    public static string Module(string permission) => permission.Split('.', 2)[0];
}

public sealed record VesselAccess(Guid VesselId, bool CanEdit);

public sealed record GrantRequest(string[]? Bundles, string[]? Permissions, bool TenantWideRead, bool TenantWideManage, VesselAccess[]? Vessels);

// The administrator's own CURRENT authority. A grant can never exceed it (least privilege, 13.03 paragraph:11-12/103).
public sealed record Grantor(Guid ActorId, IReadOnlyCollection<string> Permissions, bool TenantWideRead, bool TenantWideManage);

public sealed record GrantOutcome(Dictionary<string, string[]> Errors, string[] Bundles, string[] IndividualPermissions, string[] EffectivePermissions,
    VesselAccess[] Vessels, bool Administrator)
{
    public bool Valid => Errors.Count == 0;
}

public static class MembershipGrantPolicy
{
    public const int MaximumBundles = 9;
    public const int MaximumPermissions = 128;
    public const int MaximumVessels = 64;
    public const string SelfModification = "self_modification";
    public const string ExceedsGrantorAuthority = "exceeds_grantor_authority";
    public const string ConfigurationManaged = "configuration_managed";

    // Same predicate as the persisted/config administrator count (P5202-D04): tenant-wide manage, membership.manage,
    // directory.read and a trusted role context (here: at least one bundle).
    public static bool IsAdministrator(IReadOnlyCollection<string> effective, bool tenantWideManage, int bundleCount) =>
        tenantWideManage && bundleCount > 0 && effective.Contains("membership.manage") && effective.Contains("directory.read");

    // Editing one's own membership (escalation or lockout) is never allowed; another administrator must do it.
    public static bool IsSelf(Guid grantor, Guid targetAccount) => grantor == targetAccount;

    public static GrantOutcome Validate(GrantRequest request, Grantor grantor, IReadOnlySet<string> knownPermissions)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var bundles = request.Bundles ?? [];
        if (bundles.Length == 0) errors["bundles"] = ["required"];
        else if (bundles.Length > MaximumBundles || bundles.Any(b => b is not { Length: > 0 and <= 40 })) errors["bundles"] = ["invalid"];
        else if (bundles.Distinct(StringComparer.Ordinal).Count() != bundles.Length) errors["bundles"] = ["duplicate"];
        else if (bundles.Any(b => MembershipRoleBundles.Find(b) == null)) errors["bundles"] = ["unknown_bundle"];
        var individual = request.Permissions ?? [];
        if (individual.Length > MaximumPermissions || individual.Any(p => p is not { Length: > 0 and <= 64 })) errors["permissions"] = ["invalid"];
        else if (individual.Distinct(StringComparer.Ordinal).Count() != individual.Length) errors["permissions"] = ["duplicate"];
        else if (individual.Any(p => !knownPermissions.Contains(p))) errors["permissions"] = ["unknown_permission"];
        var vessels = request.Vessels ?? [];
        if (vessels.Length > MaximumVessels || vessels.Any(v => v is null || v.VesselId == Guid.Empty)) errors["vesselGrants"] = ["invalid"];
        else if (vessels.Select(v => v.VesselId).Distinct().Count() != vessels.Length) errors["vesselGrants"] = ["duplicate"];
        if (!request.TenantWideRead && !request.TenantWideManage && vessels.Length == 0 && !errors.ContainsKey("vesselGrants")) errors["scope"] = ["required"];
        var canonicalBundles = errors.ContainsKey("bundles") ? [] : bundles.Order(StringComparer.Ordinal).ToArray();
        var canonicalIndividual = errors.ContainsKey("permissions") ? [] : individual.Order(StringComparer.Ordinal).ToArray();
        var effective = canonicalBundles.SelectMany(b => MembershipRoleBundles.Find(b)!.Permissions).Concat(canonicalIndividual)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        // Least privilege: never grant what the administrator does not hold, including tenant-wide scope.
        if (!errors.ContainsKey("bundles") && canonicalBundles.Any(b => MembershipRoleBundles.Find(b)!.Permissions.Any(p => !grantor.Permissions.Contains(p))))
            errors["bundles"] = [ExceedsGrantorAuthority];
        if (!errors.ContainsKey("permissions") && canonicalIndividual.Any(p => !grantor.Permissions.Contains(p)))
            errors["permissions"] = [ExceedsGrantorAuthority];
        if (request.TenantWideManage && !grantor.TenantWideManage) errors["tenantWideManage"] = [ExceedsGrantorAuthority];
        if (request.TenantWideRead && !(grantor.TenantWideRead || grantor.TenantWideManage)) errors["tenantWideRead"] = [ExceedsGrantorAuthority];
        if (!effective.Contains("directory.read") && errors.Count == 0) errors["permissions"] = ["directory_read_required"];
        var canonicalVessels = errors.ContainsKey("vesselGrants") ? [] : vessels.OrderBy(v => v.VesselId).ToArray();
        return new(errors, canonicalBundles, canonicalIndividual, effective, canonicalVessels,
            IsAdministrator(effective, request.TenantWideManage, canonicalBundles.Length));
    }

    public static string Canonical(IEnumerable<string> values) => string.Join(',', values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    public static string[] Split(string? csv) => string.IsNullOrEmpty(csv) ? [] : csv.Split(',');
}
