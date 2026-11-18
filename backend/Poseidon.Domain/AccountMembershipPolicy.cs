namespace Poseidon.Domain;

// PLAN-1511-5202 / 52.02 ACC04-ACC05 + 54.02 Rev4 paragraph:807 (last-admin narrowing).
// Closed membership lifecycle. A persisted membership state can only narrow the
// operator-provisioned grant; it never creates access, roles or vessel scope.
public enum MembershipStatus { Active, Suspended, Revoked }

public enum MembershipTransition
{
    // Voluntary administrative changes are subject to the last-admin guard.
    Suspend, Resume, Revoke,
    // Forced security suspension (54.02 Rev4: security suspension is not blocked).
    // It is a separate exact action, never a client-supplied "forced" flag.
    SecuritySuspend
}

public sealed record MembershipDecision(bool Allowed, MembershipStatus Next, string? ErrorCode, bool RecoveryRequired, string ReasonCode);

public static class MembershipLifecyclePolicy
{
    public const string LastAdmin = "last_admin";
    public const string InvalidTransition = "invalid_transition";
    public const string SecuritySuspensionReason = "SecuritySuspension";

    // QA follow-up (PLAN-1511-5202): a forced security suspension can only be lifted by the
    // separate security authority; ordinary membership administration cannot undo it.
    public static bool RequiresSecurityAuthority(MembershipTransition transition, string? currentReason) =>
        transition is MembershipTransition.SecuritySuspend || transition is MembershipTransition.Resume && currentReason == SecuritySuspensionReason;

    public static string Wire(MembershipStatus status) => status switch
    {
        MembershipStatus.Active => "Active", MembershipStatus.Suspended => "Suspended", MembershipStatus.Revoked => "Revoked",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static MembershipStatus? Parse(string? value) => value switch
    {
        "Active" => MembershipStatus.Active, "Suspended" => MembershipStatus.Suspended, "Revoked" => MembershipStatus.Revoked, _ => null
    };

    // isLastActiveAdministrator: the target is currently an active tenant identity
    // administrator and no other active administrator remains. UNKNOWN is never passed
    // here as false; callers that cannot count administrators must fail closed.
    public static MembershipDecision Decide(MembershipStatus current, MembershipTransition transition, bool isLastActiveAdministrator, string? currentReason = null)
    {
        static MembershipDecision Deny(MembershipStatus current, string code) => new(false, current, code, false, "Rejected");
        switch (transition)
        {
            case MembershipTransition.Suspend:
                if (current != MembershipStatus.Active) return Deny(current, InvalidTransition);
                if (isLastActiveAdministrator) return Deny(current, LastAdmin);
                return new(true, MembershipStatus.Suspended, null, false, "AdministrativeSuspension");
            case MembershipTransition.Revoke:
                if (current == MembershipStatus.Revoked) return Deny(current, InvalidTransition);
                // A suspended member is already not an active administrator.
                if (current == MembershipStatus.Active && isLastActiveAdministrator) return Deny(current, LastAdmin);
                return new(true, MembershipStatus.Revoked, null, false, "AdministrativeRevocation");
            case MembershipTransition.Resume:
                // Revoked is terminal: 52.02 "otomatik yeniden açılmaz".
                if (current != MembershipStatus.Suspended) return Deny(current, InvalidTransition);
                // Resuming a security suspension closes the RECOVERY_REQUIRED case with its own reason.
                return new(true, MembershipStatus.Active, null, false, currentReason == SecuritySuspensionReason ? "SecurityResume" : "AdministrativeResume");
            case MembershipTransition.SecuritySuspend:
                if (current != MembershipStatus.Active) return Deny(current, InvalidTransition);
                // Fail-closed access removal; RECOVERY_REQUIRED instead of a hidden admin.
                return new(true, MembershipStatus.Suspended, null, isLastActiveAdministrator, "SecuritySuspension");
            default:
                throw new ArgumentOutOfRangeException(nameof(transition));
        }
    }

    public static bool IsLastActiveAdministrator(Guid target, IEnumerable<Guid> activeAdministrators)
    {
        var admins = activeAdministrators.Distinct().ToArray();
        return admins.Contains(target) && admins.Length == 1;
    }

    // Session admission: no persisted state means the operator bootstrap source still
    // governs (not yet imported). A persisted non-active state or a newer access
    // revision than the session stamp always denies; cached old sessions do not revive.
    public static bool AdmitsSession(MembershipStatus? persisted, long persistedAccessRevision, long sessionAccessRevision) =>
        persisted is null || persisted == MembershipStatus.Active && sessionAccessRevision >= persistedAccessRevision;
}

// 52.02 paragraph:102: source person, delegate, scope, permitted actions and reason are
// mandatory. Scope is an explicit company/vessel snapshot; tenant-wide scope is never delegated.
public sealed record DelegatedVessel(Guid VesselId, bool CanEdit);

public sealed record DelegationRequest(Guid DelegatorMembershipId, Guid DelegateMembershipId, string[]? Permissions,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil, string? ReasonCode = null, Guid? CompanyId = null, DelegatedVessel[]? VesselScope = null);

// The source's CURRENT effective authority (operator grant narrowed by the persisted snapshot).
public sealed record DelegationSource(IReadOnlyCollection<string> Permissions, Guid? CompanyId, IReadOnlyCollection<DelegatedVessel> Vessels);

// Request-time effective scope: snapshot intersected with the source's current authority.
public sealed record DelegatedScope(string[] Permissions, Guid? CompanyId, DelegatedVessel[] Vessels)
{
    public bool IsEmpty => Permissions.Length == 0 || CompanyId is null && Vessels.Length == 0;
}

// 52.02 §6: source person, delegate, scope, validity window and reason are mandatory; delegation
// can never be wider than the source authority; re-delegation is forbidden by default.
public static class DelegationPolicy
{
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(90); // Technical ceiling, not an approved policy value.
    public const int MaximumPermissions = 32;
    public const int MaximumVessels = 64;
    // Closed technical reason codes (no free text: privacy decision P5202). Not an approved
    // 54.01 catalog; a future catalog replaces this list through a reviewed migration.
    public static readonly string[] ReasonCodes = ["PlannedAbsence", "UnplannedAbsence", "WorkloadCover", "Handover"];
    // Rows created before the scope snapshot existed; they never become effective.
    public const string LegacyUnscoped = "LegacyUnscoped";

    public static bool NonDelegable(string permission) =>
        permission.StartsWith("membership.", StringComparison.Ordinal) || permission.StartsWith("delegation.", StringComparison.Ordinal) ||
        permission is "audit.read" or "directory.read";

    // Permission/window/identity rules only (no scope); kept for focused policy checks.
    public static Dictionary<string, string[]> Validate(DelegationRequest request, IReadOnlyCollection<string> delegatorPermissions, DateTimeOffset now) =>
        Validate(request, new DelegationSource(delegatorPermissions, null, []), now, scoped: false);

    // Full create validation: also reason and a scope that is a subset of the source's current scope.
    public static Dictionary<string, string[]> Validate(DelegationRequest request, DelegationSource source, DateTimeOffset now) =>
        Validate(request, source, now, scoped: true);

    private static Dictionary<string, string[]> Validate(DelegationRequest request, DelegationSource source, DateTimeOffset now, bool scoped)
    {
        var delegatorPermissions = source.Permissions;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.DelegatorMembershipId == Guid.Empty) errors["delegatorMembershipId"] = ["required"];
        if (request.DelegateMembershipId == Guid.Empty) errors["delegateMembershipId"] = ["required"];
        else if (request.DelegateMembershipId == request.DelegatorMembershipId) errors["delegateMembershipId"] = ["self_delegation"];
        var permissions = request.Permissions;
        if (permissions is not { Length: > 0 } || permissions.Length > MaximumPermissions || permissions.Any(p => p is not { Length: > 0 and <= 64 }))
            errors["permissions"] = ["required"];
        else if (permissions.Distinct(StringComparer.Ordinal).Count() != permissions.Length) errors["permissions"] = ["duplicate"];
        else if (permissions.Any(NonDelegable)) errors["permissions"] = ["not_delegable"];
        else if (permissions.Any(p => !delegatorPermissions.Contains(p))) errors["permissions"] = ["exceeds_source_authority"];
        if (request.ValidFrom is null) errors["validFrom"] = ["required"];
        if (request.ValidUntil is null) errors["validUntil"] = ["required"];
        if (request.ValidFrom is { } from && request.ValidUntil is { } until)
        {
            if (until <= from) errors["validUntil"] = ["must_follow_validFrom"];
            else if (until - from > MaximumWindow) errors["validUntil"] = ["window_too_long"];
            else if (until <= now) errors["validUntil"] = ["already_expired"];
            if (from < now - TimeSpan.FromMinutes(5)) errors["validFrom"] = ["backdated"];
        }
        if (!scoped) return errors;
        if (request.ReasonCode is null) errors["reasonCode"] = ["required"];
        else if (!ReasonCodes.Contains(request.ReasonCode, StringComparer.Ordinal)) errors["reasonCode"] = ["unknown_reason"];
        var vessels = request.VesselScope ?? [];
        if (request.CompanyId == Guid.Empty) errors["companyId"] = ["required"];
        else if (request.CompanyId is { } company && company != source.CompanyId) errors["companyId"] = ["exceeds_source_scope"];
        if (vessels.Length > MaximumVessels || vessels.Any(v => v is null || v.VesselId == Guid.Empty)) errors["vesselScope"] = ["required"];
        else if (vessels.Select(v => v.VesselId).Distinct().Count() != vessels.Length) errors["vesselScope"] = ["duplicate"];
        else if (vessels.Any(v => !source.Vessels.Any(s => s.VesselId == v.VesselId && (s.CanEdit || !v.CanEdit))))
            errors["vesselScope"] = ["exceeds_source_scope"];
        else if (request.CompanyId is null && vessels.Length == 0) errors["scope"] = ["required"];
        return errors;
    }

    // Request-time narrowing: the snapshot never outlives the source's current authority.
    public static DelegatedScope Effective(IEnumerable<string> delegatedPermissions, Guid? snapshotCompany, IEnumerable<DelegatedVessel> snapshotVessels, DelegationSource current)
    {
        var permissions = delegatedPermissions.Where(p => current.Permissions.Contains(p) && !NonDelegable(p)).Distinct(StringComparer.Ordinal).ToArray();
        var company = snapshotCompany is { } c && c == current.CompanyId ? c : (Guid?)null;
        var vessels = snapshotVessels
            .Select(v => current.Vessels.FirstOrDefault(s => s.VesselId == v.VesselId) is { } s ? new DelegatedVessel(v.VesselId, v.CanEdit && s.CanEdit) : null)
            .OfType<DelegatedVessel>().DistinctBy(v => v.VesselId).ToArray();
        return new(permissions, company, vessels);
    }

    public static string Canonical(IEnumerable<string> permissions) => string.Join(',', permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    // Effective only inside [ValidFrom, ValidUntil) while both memberships are active.
    // Expiry is computed from server time; a lapsed window never extends itself.
    public static string State(bool revoked, DateTimeOffset validFrom, DateTimeOffset validUntil, DateTimeOffset now, bool delegatorActive, bool delegateActive) =>
        revoked ? "Revoked" : now >= validUntil ? "Expired" : now < validFrom ? "Pending" :
        delegatorActive && delegateActive ? "Effective" : "Ineffective";
}
