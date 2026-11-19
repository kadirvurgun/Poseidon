using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-031 / T-S3-006 (18.01 Backlog row:32): "Authorized role + context + reason + decision
// lineage kaydedilir; AI veya generic admin final decision oluşturamaz."
// Defense-in-depth gate for every final Decision event (RECORDED or REJECTED). It is evaluated
// against the trusted server-provisioned membership, never a request field or IdP role claim.
// Tenant-wide read/manage, directory.write or the human.decide permission alone never substitute
// for an exact operator appointment; AI, automation, service and unspecified principals are denied
// even when an appointment is (mis)configured for them. The gate makes no safety decision itself.
public static class HumanDecisionAuthority
{
    public const string SourceLocator = "18.01/Backlog/row:32";
    public static readonly IReadOnlyList<string> RecordingRoles = ["Master", "Tug Master", "DPA"];

    public static bool IsFinal(string kind, string capability) => kind == "Decision" && capability == "human.decide";

    // Returns null only for an exactly appointed human; otherwise a fixed, non-sensitive cause.
    public static string? FinalDecisionDenial(IReadOnlyList<Membership> memberships, TrustedActor actor, HumanAuthority? grant,
        VesselKind vesselKind, string state, DateTimeOffset now)
    {
        var matches = memberships.Where(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).ToArray();
        if (matches.Length != 1) return "membership_unresolved";
        var member = matches[0];
        // Exact ordinal "Human": "AI", "Automation", "Service", "Unspecified", "human" all fail closed.
        if (!string.Equals(member.ActorKind, "Human", StringComparison.Ordinal)) return "non_human_principal";
        if (grant == null || grant.Id == Guid.Empty) return "human_authority_required";
        // The appointment must be one of this membership's own provisioned appointments, not an
        // inferred administrator, tenant-wide manager or permission-derived grant.
        if ((member.HumanAuthorities ?? []).Count(g => g == grant) != 1) return "human_authority_required";
        if (grant.Capability != "human.decide" || !actor.Permissions.Contains("human.decide")) return "decision_capability_required";
        if (grant.RoleId == Guid.Empty || actor.RoleContext?.Split(',').Contains(grant.RoleId.ToString("D")) != true) return "decision_role_context_required";
        if (!(grant.ValidFrom <= now && now < grant.ValidUntil)) return "human_authority_required";
        if (state == "RECORDED") {
            if (grant.Recording is not { Semantics: "HumanRecording" } policy || !RecordingRoles.Contains(policy.AuthorityRole)) return "decision_role_required";
            if (policy.AuthorityRole == "Master" && vesselKind != VesselKind.Ship || policy.AuthorityRole == "Tug Master" && vesselKind != VesselKind.Tug) return "decision_role_mode_mismatch";
        }
        return null;
    }
}
