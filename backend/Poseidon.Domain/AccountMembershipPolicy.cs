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
    public static MembershipDecision Decide(MembershipStatus current, MembershipTransition transition, bool isLastActiveAdministrator)
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
                return new(true, MembershipStatus.Active, null, false, "AdministrativeResume");
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

public sealed record DelegationRequest(Guid DelegatorMembershipId, Guid DelegateMembershipId, string[]? Permissions,
    DateTimeOffset? ValidFrom, DateTimeOffset? ValidUntil);

// 52.02 §6: source person, delegate, scope, validity window are mandatory; delegation
// can never be wider than the source authority; re-delegation is forbidden by default.
public static class DelegationPolicy
{
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(90); // Technical ceiling, not an approved policy value.
    public const int MaximumPermissions = 32;

    public static bool NonDelegable(string permission) =>
        permission.StartsWith("membership.", StringComparison.Ordinal) || permission.StartsWith("delegation.", StringComparison.Ordinal) ||
        permission is "audit.read" or "directory.read";

    public static Dictionary<string, string[]> Validate(DelegationRequest request, IReadOnlyCollection<string> delegatorPermissions, DateTimeOffset now)
    {
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
        return errors;
    }

    public static string Canonical(IEnumerable<string> permissions) => string.Join(',', permissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    // Effective only inside [ValidFrom, ValidUntil) while both memberships are active.
    // Expiry is computed from server time; a lapsed window never extends itself.
    public static string State(bool revoked, DateTimeOffset validFrom, DateTimeOffset validUntil, DateTimeOffset now, bool delegatorActive, bool delegateActive) =>
        revoked ? "Revoked" : now >= validUntil ? "Expired" : now < validFrom ? "Pending" :
        delegatorActive && delegateActive ? "Effective" : "Ineffective";
}
