namespace Poseidon.Domain;

// USER-ADMIN-03. Invitation e-mail delivery through a transactional outbox (52.02 Rev3 paragraph:342: the token hash is
// stored and delivery is tracked separately). A delivery row is written in the same audited transaction as the invitation;
// a background dispatcher sends it. Delivery is never assumed: only a mail server's acceptance moves a row to Sent, and
// an unconfigured mail transport is recorded as NotConfigured (the administrator still has the one-time copy link).
public enum InvitationDeliveryState { Queued, Sent, Failed, NotConfigured, Cancelled }

// Outcome of one send attempt reported by the mail transport. Accepted means the SMTP server accepted the message for
// relay; it is not proof that the recipient read it.
public enum InvitationSendOutcome { Accepted, TransientFailure, PermanentFailure, NotConfigured }

public sealed record InvitationDeliveryStep(InvitationDeliveryState State, DateTimeOffset? NextAttemptAt, string? ErrorCode);

public static class InvitationDeliveryPolicy
{
    // Technical retry guard, not an approved service level: 5 attempts with exponential backoff (1, 2, 4, 8 minutes).
    public const int MaxAttempts = 5;
    public static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);
    public const int EmailMaxLength = 254;

    // Closed error codes stored on the row and in audit summaries. Never a server reply, address or exception text.
    public const string ErrorTransient = "smtp_transient";
    public const string ErrorRejected = "smtp_rejected";
    public const string ErrorExhausted = "smtp_retries_exhausted";
    public const string ErrorNotConfigured = "not_configured";
    public const string ErrorPayloadUnavailable = "payload_unavailable";
    public const string ErrorInvitationClosed = "invitation_closed";
    public const string ErrorInvitationExpired = "invitation_expired";
    public static readonly string[] ErrorCodes = [ErrorTransient, ErrorRejected, ErrorExhausted, ErrorNotConfigured, ErrorPayloadUnavailable,
        ErrorInvitationClosed, ErrorInvitationExpired];
    public static readonly string[] States = ["QUEUED", "SENT", "FAILED", "NOT_CONFIGURED", "CANCELLED"];

    public static string Wire(InvitationDeliveryState state) => state switch
    {
        InvitationDeliveryState.Queued => "QUEUED", InvitationDeliveryState.Sent => "SENT", InvitationDeliveryState.Failed => "FAILED",
        InvitationDeliveryState.NotConfigured => "NOT_CONFIGURED", InvitationDeliveryState.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    public static InvitationDeliveryState? Parse(string? value) => value switch
    {
        "QUEUED" => InvitationDeliveryState.Queued, "SENT" => InvitationDeliveryState.Sent, "FAILED" => InvitationDeliveryState.Failed,
        "NOT_CONFIGURED" => InvitationDeliveryState.NotConfigured, "CANCELLED" => InvitationDeliveryState.Cancelled, _ => null
    };

    public static bool Terminal(InvitationDeliveryState state) => state != InvitationDeliveryState.Queued;

    // Only Queued moves, and only once to a terminal state or back to Queued for a retry; terminal rows never change.
    public static bool CanTransition(InvitationDeliveryState from, InvitationDeliveryState to) => from == InvitationDeliveryState.Queued;

    // Backoff after the given number of failed attempts (1 -> 1 min, 2 -> 2 min, ...), capped.
    public static TimeSpan Backoff(int failedAttempts)
    {
        if (failedAttempts < 1) throw new ArgumentOutOfRangeException(nameof(failedAttempts));
        var minutes = FirstBackoff.TotalMinutes * Math.Pow(2, Math.Min(failedAttempts - 1, 16));
        return minutes >= MaxBackoff.TotalMinutes ? MaxBackoff : TimeSpan.FromMinutes(minutes);
    }

    // Next state after attempt number `attempt` (1-based) ended with `outcome`.
    public static InvitationDeliveryStep After(int attempt, InvitationSendOutcome outcome, DateTimeOffset now)
    {
        if (attempt is < 1 or > MaxAttempts) throw new ArgumentOutOfRangeException(nameof(attempt));
        return outcome switch
        {
            InvitationSendOutcome.Accepted => new(InvitationDeliveryState.Sent, null, null),
            InvitationSendOutcome.NotConfigured => new(InvitationDeliveryState.NotConfigured, null, ErrorNotConfigured),
            InvitationSendOutcome.PermanentFailure => new(InvitationDeliveryState.Failed, null, ErrorRejected),
            InvitationSendOutcome.TransientFailure when attempt >= MaxAttempts => new(InvitationDeliveryState.Failed, null, ErrorExhausted),
            InvitationSendOutcome.TransientFailure => new(InvitationDeliveryState.Queued, now + Backoff(attempt), ErrorTransient),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }

    // A deliberately narrow address check (no display names, comments, quoted local parts or IP literals). It refuses
    // anything that could inject an SMTP/MIME header (CR, LF, control characters, commas, angle brackets).
    public static bool ValidEmail(string? value)
    {
        if (value is not { Length: >= 3 and <= EmailMaxLength } || value.Trim() != value) return false;
        var at = value.IndexOf('@');
        if (at < 1 || at != value.LastIndexOf('@') || at > 64 || at == value.Length - 1) return false;
        if (value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '<' or '>' or ',' or ';' or '"' or '(' or ')' or '[' or ']' or '\\' || c > '~'))
            return false;
        var local = value[..at];
        var domain = value[(at + 1)..];
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal)) return false;
        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(l => l.Length is >= 1 and <= 63 && !l.StartsWith('-') && !l.EndsWith('-') &&
            l.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) && !labels[^1].All(char.IsAsciiDigit);
    }

    // INVITE-RATELIMIT-FOLLOWUPS N4: the comparison form of a valid address for idempotency fingerprints. ValidEmail admits
    // ASCII only, so invariant lower-casing is exact. The whole address is folded: domains are case-insensitive (RFC 5321
    // 2.4) and, although a local part may be case-sensitive, mail systems treat it case-insensitively in practice, so two
    // spellings of one mailbox are the same request (replay) while any other address is a different one (409). An invalid
    // address has no comparison form (the request is refused with 400 before a fingerprint is used).
    public static string? NormalizedEmail(string? value) => ValidEmail(value) ? value!.ToLowerInvariant() : null;
}

// 54.01 Rev4 paragraph:10025 `account.invite_hours`: Tenant scope, integer, default 24, range 1-168 (7 days). The tenant
// value is an attributed administrator setting; a tenant without one uses the source default. It applies to invitations
// issued after the change; existing invitations keep their expiry (expiry is immutable, USER-ADMIN-02).
public static class InvitationLifetimePolicy
{
    public const int MinHours = 1;
    public const int MaxHours = 168;
    public const int DefaultHours = 24;

    public static bool ValidHours(int hours) => hours is >= MinHours and <= MaxHours;

    public static TimeSpan For(int? tenantHours)
    {
        if (tenantHours is null) return TimeSpan.FromHours(DefaultHours);
        if (!ValidHours(tenantHours.Value)) throw new ArgumentOutOfRangeException(nameof(tenantHours));
        return TimeSpan.FromHours(tenantHours.Value);
    }
}
