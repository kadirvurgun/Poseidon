using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// USER-ADMIN-02 (closes USER-ADMIN-01 open item N1, cross-tenant identity squatting). An administrator-created
// membership is an INVITATION: it grants nothing and reserves no IdP identity until the identity holder, signed in
// through the trusted IdP, accepts it with a single-use, high-entropy token. Only a hash of the token's secret is stored.
public enum InvitationStatus { Pending, Accepted, Revoked, Superseded }

// Internal (audit-only) refusal reasons. The caller always receives one neutral public code, so a refusal never
// reveals whether a token exists, expired, was used, belongs to another identity or whether the identity is bound.
public enum InvitationRefusal { None, Closed, Expired, SubjectMismatch, IssuerMismatch, MembershipNotInvited }

public sealed record InvitationFacts(InvitationStatus Status, DateTimeOffset ExpiresAt, string? ExpectedSubject, string AccountIssuer,
    MembershipStatus MembershipStatus);

public static class MembershipInvitationPolicy
{
    // 54.01 account.invite_hours (paragraph:10025: Tenant, integer, default 24, range 1-168) consumed by 52.02 paragraph:277
    // (expired or used token refused). The registered tenant override is not wired yet (USER-ADMIN-02 open item), so the
    // source default applies to every tenant; it is the source's default, not an approved tenant value.
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    public const string NeutralRefusal = "invitation_unavailable";
    public const string Prefix = "pi1";
    public const int SecretBytes = 32;
    // pi1.<tenant:N 32 hex>.<secret base64url 43 chars>
    public const int TokenLength = 3 + 1 + 32 + 1 + 43;

    public static string Wire(InvitationStatus status) => status switch
    {
        InvitationStatus.Pending => "Pending", InvitationStatus.Accepted => "Accepted", InvitationStatus.Revoked => "Revoked",
        InvitationStatus.Superseded => "Superseded", _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static InvitationStatus? Parse(string? value) => value switch
    {
        "Pending" => InvitationStatus.Pending, "Accepted" => InvitationStatus.Accepted, "Revoked" => InvitationStatus.Revoked,
        "Superseded" => InvitationStatus.Superseded, _ => null
    };

    // Display state for administrators: a pending invitation past its expiry is Expired (derived from server time,
    // never stored, so no background job is needed and an expired link can never be accepted).
    public static string State(InvitationStatus status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == InvitationStatus.Pending && now >= expiresAt ? "Expired" : Wire(status);

    public static string NewSecret()
    {
        Span<byte> bytes = stackalloc byte[SecretBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Format(Guid tenant, string secret) => $"{Prefix}.{tenant:N}.{secret}";

    // Strict closed format; anything else is simply "unavailable". The tenant part selects the tenant-scoped
    // database context (no cross-tenant query); it is not a secret and grants nothing by itself.
    public static bool TryParse(string? token, out Guid tenant, out string secret)
    {
        tenant = Guid.Empty; secret = "";
        if (token is not { Length: TokenLength } || token[3] != '.' || token[36] != '.' || !token.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var tenantPart = token.AsSpan(4, 32);
        var secretPart = token[37..];
        if (!tenantPart.ToString().All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            !Guid.TryParseExact(tenantPart, "N", out tenant) || tenant == Guid.Empty) { tenant = Guid.Empty; return false; }
        if (!secretPart.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) { tenant = Guid.Empty; return false; }
        secret = secretPart;
        return true;
    }

    // SHA-256 of the secret (hex). The secret has 256 bits of entropy, so an unsalted fast hash is sufficient: the
    // stored value cannot be reversed or guessed, and lookups stay an indexed equality match.
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));

    public static bool ValidExpectedSubject(string? subject) =>
        subject is { Length: > 0 and <= 255 } && subject.Trim() == subject && !subject.Any(char.IsControl);

    // Acceptance rule: the invitation is pending and unexpired, the membership still awaits acceptance, the signed-in
    // identity comes from the invitation's issuer and, when the administrator recorded an expected subject, matches it
    // exactly (ordinal). The identity-already-bound rule is enforced by the database (unique accepted binding).
    public static InvitationRefusal Decide(InvitationFacts facts, string issuer, string subject, DateTimeOffset now)
    {
        if (facts.Status != InvitationStatus.Pending) return InvitationRefusal.Closed;
        if (now >= facts.ExpiresAt) return InvitationRefusal.Expired;
        if (facts.MembershipStatus != MembershipStatus.Invited) return InvitationRefusal.MembershipNotInvited;
        if (!string.Equals(facts.AccountIssuer, issuer, StringComparison.Ordinal)) return InvitationRefusal.IssuerMismatch;
        if (facts.ExpectedSubject != null && !string.Equals(facts.ExpectedSubject, subject, StringComparison.Ordinal)) return InvitationRefusal.SubjectMismatch;
        return InvitationRefusal.None;
    }

    // Closed audit result code per refusal (registered in SecurityEventCodes); never returned to the caller.
    public static string AuditCode(InvitationRefusal refusal) => refusal switch
    {
        InvitationRefusal.Closed => "invitation_closed", InvitationRefusal.Expired => "invitation_expired",
        InvitationRefusal.SubjectMismatch => "invitation_subject_mismatch", InvitationRefusal.IssuerMismatch => "invitation_subject_mismatch",
        InvitationRefusal.MembershipNotInvited => "invitation_closed", _ => "Success"
    };
}
