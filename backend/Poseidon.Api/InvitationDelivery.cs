using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// USER-ADMIN-03: invitation e-mail delivery (transactional outbox + background dispatcher). Operator configuration only;
// no HTTP input selects the transport, the sender, the link origin or the dispatcher identity.
public sealed class InvitationDeliverySettings
{
    public bool Enabled { get; set; }
    // Origin of the link in the e-mail (https). Never taken from the request Host header, which a caller controls.
    public string? PublicBaseUrl { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    // StartTls (default, required for any non-loopback host) or None (loopback development mail catcher only).
    public string Security { get; set; } = "StartTls";
    public string? From { get; set; }
    public string? UserName { get; set; }
    // Name of the environment/secret-store variable holding the SMTP password. A literal password is not accepted.
    public string? PasswordReference { get; set; }
    public int PollSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int LeaseSeconds { get; set; } = 120;
    public int SendTimeoutSeconds { get; set; } = 30;

    public bool Valid()
    {
        if (!Enabled || string.IsNullOrWhiteSpace(SmtpHost) || SmtpHost.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || SmtpPort is < 1 or > 65535 ||
            !InvitationDeliveryPolicy.ValidEmail(From) || PollSeconds is < 1 or > 3600 || BatchSize is < 1 or > 100 ||
            SendTimeoutSeconds is < 1 or > 120 || LeaseSeconds <= SendTimeoutSeconds + 5 || LeaseSeconds > 900) return false;
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps || origin.Query.Length > 0 ||
            origin.Fragment.Length > 0 || !string.IsNullOrEmpty(origin.UserInfo)) return false;
        if (Security == "None" ? !Loopback(SmtpHost) : Security != "StartTls") return false;
        return (UserName == null) == (PasswordReference == null) && (UserName == null || UserName is { Length: > 0 and <= 256 } && !UserName.Any(char.IsControl));
    }

    private static bool Loopback(string host) => host == "localhost" || IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);

    public string Link(string token) => PublicBaseUrl!.TrimEnd('/') + "/app/accept-invitation#token=" + Uri.EscapeDataString(token);
}

// What the dispatcher hands to the transport. Recipient and link are personal/secret: never logged, audited or persisted.
public sealed record InvitationMailMessage(string To, string Link, DateTimeOffset ExpiresAt, string? TenantName);

public interface IInvitationMailer
{
    bool Configured { get; }
    Task<InvitationSendOutcome> SendAsync(InvitationMailMessage message, CancellationToken ct);
}

// Default transport: SMTP (System.Net.Mail, STARTTLS). Implicit TLS (port 465) is not supported by this client; use the
// submission port with STARTTLS. Exceptions are classified into closed outcomes and never logged with their message
// (SMTP exceptions can carry the recipient address).
public sealed class SmtpInvitationMailer(InvitationDeliverySettings settings, ILogger<SmtpInvitationMailer> logger, Func<string, string?>? secrets = null) : IInvitationMailer
{
    private readonly Func<string, string?> secrets = secrets ?? Environment.GetEnvironmentVariable;
    public bool Configured => settings.Valid();

    public async Task<InvitationSendOutcome> SendAsync(InvitationMailMessage message, CancellationToken ct)
    {
        if (!Configured) return InvitationSendOutcome.NotConfigured;
        NetworkCredential? credential = null;
        if (settings.UserName != null)
        {
            string? secretValue;
            try { secretValue = OperatorSecretReferences.Resolve(settings.PasswordReference, secrets); }
            catch (InvalidOperationException) { secretValue = null; }
            if (secretValue == null)
            {
                logger.LogWarning("Invitation e-mail transport credential is unavailable.");
                return InvitationSendOutcome.NotConfigured;
            }
            credential = new NetworkCredential(settings.UserName, secretValue);
        }
        using var mail = new MailMessage(new MailAddress(settings.From!), new MailAddress(message.To))
        {
            Subject = "POSEIDON invitation / POSEIDON daveti",
            Body = Body(message),
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
        };
        using var client = new SmtpClient(settings.SmtpHost!, settings.SmtpPort)
        {
            EnableSsl = settings.Security == "StartTls",
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = credential,
            Timeout = settings.SendTimeoutSeconds * 1000,
        };
        try
        {
            await client.SendMailAsync(mail, ct);
            return InvitationSendOutcome.Accepted;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e)
        {
            var outcome = Classify(e);
            // Closed outcome and exception type only: no message, recipient, link or server reply.
            logger.LogWarning("Invitation e-mail attempt failed: {Outcome} ({ExceptionType}).", outcome, e.GetType().Name);
            return outcome;
        }
    }

    // 5xx replies (mailbox unavailable, syntax, policy rejection) are permanent; connection errors, timeouts and 4xx are transient.
    internal static InvitationSendOutcome Classify(Exception e) => e switch
    {
        SmtpFailedRecipientsException many => many.InnerExceptions.All(x => Permanent(x.StatusCode)) ? InvitationSendOutcome.PermanentFailure : InvitationSendOutcome.TransientFailure,
        SmtpException smtp when Permanent(smtp.StatusCode) => InvitationSendOutcome.PermanentFailure,
        FormatException => InvitationSendOutcome.PermanentFailure,
        _ => InvitationSendOutcome.TransientFailure
    };

    private static bool Permanent(SmtpStatusCode code) => (int)code is >= 500 and < 600;

    internal static string Body(InvitationMailMessage m)
    {
        var expires = m.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var organisation = string.IsNullOrWhiteSpace(m.TenantName) ? "" : $" ({m.TenantName})";
        return $"You have been invited to POSEIDON{organisation}.\r\n" +
            $"Open the link below, sign in with your organisation account and accept the invitation. The link works once and expires on {expires}.\r\n\r\n" +
            $"{m.Link}\r\n\r\n" +
            "If you did not expect this invitation, ignore this message.\r\n\r\n" +
            $"POSEIDON'a davet edildiniz{organisation}. Bağlantıyı açın, kurum hesabınızla oturum açın ve daveti kabul edin. " +
            $"Bağlantı bir kez kullanılabilir ve {expires} tarihinde sona erer. Bu daveti beklemiyorsanız bu iletiyi dikkate almayın.\r\n";
    }
}

// The outbox payload: recipient + token protected with ASP.NET Core Data Protection, bound to tenant and delivery id
// (a ciphertext copied to another row or tenant does not decrypt). If the key ring is lost (non-persisted keys) the row
// fails closed as payload_unavailable; the administrator reissues the invitation.
public sealed class InvitationPayloadProtection(IDataProtectionProvider provider)
{
    private sealed record Payload(string To, string Token);
    private IDataProtector For(Guid tenant, Guid delivery) =>
        provider.CreateProtector("Poseidon.InvitationDelivery.Payload.v1", tenant.ToString("N"), delivery.ToString("N"));

    public string Protect(Guid tenant, Guid delivery, string to, string token) => For(tenant, delivery).Protect(JsonSerializer.Serialize(new Payload(to, token)));

    public (string To, string Token)? Unprotect(Guid tenant, Guid delivery, string payload)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Payload>(For(tenant, delivery).Unprotect(payload));
            return value is { To.Length: > 0, Token.Length: > 0 } ? (value.To, value.Token) : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return null; }
    }
}

public sealed record InvitationDispatchResult(string Status, int Sent, int Retried, int Failed, int Cancelled, int NotConfigured);

// Dispatcher. Identity: the explicit system actor "system:invitation-delivery" (ActorType System, Source InvitationDelivery);
// every state change is an audited SaveChanges write in the row's own tenant context. The only cross-tenant read is the due
// scan below: tenant ids of due rows, read with a context that never writes (same pattern as the login lookup).
public sealed class InvitationDeliveryDispatcher(IConfiguration configuration, IHostEnvironment environment, InvitationDeliverySettings settings,
    IInvitationMailer mailer, InvitationPayloadProtection protection, TimeProvider time, ILogger<InvitationDeliveryDispatcher> logger) : BackgroundService
{
    public const string ActorId = "system:invitation-delivery";
    private readonly SemaphoreSlim gate = new(1, 1);

    private PoseidonDbContext? Open(Guid tenant, string correlation)
    {
        var connection = OperatorSecretReferences.Database(configuration, environment);
        if (string.IsNullOrWhiteSpace(connection)) return null;
        return new PoseidonDbContext(new DbContextOptionsBuilder<PoseidonDbContext>().UseNpgsql(connection).Options, tenant,
            new ActorContext(ActorId, "System", correlation, Source: "InvitationDelivery"), timeProvider: time);
    }

    public async Task<InvitationDispatchResult> RunOnceAsync(CancellationToken ct)
    {
        if (!await gate.WaitAsync(0, ct)) return new("Busy", 0, 0, 0, 0, 0);
        try
        {
            var now = time.GetUtcNow();
            Guid[] tenants;
            await using (var scan = Open(Guid.Empty, Guid.NewGuid().ToString("N")))
            {
                if (scan == null) return new("Unavailable", 0, 0, 0, 0, 0);
                tenants = await scan.InvitationDeliveries.IgnoreQueryFilters().AsNoTracking()
                    .Where(d => d.State == "QUEUED" && d.NextAttemptAt <= now && (d.LeaseUntil == null || d.LeaseUntil < now))
                    .Select(d => d.TenantId).Distinct().OrderBy(t => t).Take(64).ToArrayAsync(ct);
            }
            int sent = 0, retried = 0, failed = 0, cancelled = 0, notConfigured = 0;
            foreach (var tenant in tenants)
            {
                await using var db = Open(tenant, Guid.NewGuid().ToString("N"));
                if (db == null) return new("Unavailable", sent, retried, failed, cancelled, notConfigured);
                var due = await db.InvitationDeliveries.AsNoTracking()
                    .Where(d => d.State == "QUEUED" && d.NextAttemptAt <= now && (d.LeaseUntil == null || d.LeaseUntil < now))
                    .OrderBy(d => d.NextAttemptAt).ThenBy(d => d.Id).Select(d => d.Id).Take(settings.BatchSize is >= 1 and <= 100 ? settings.BatchSize : 20).ToArrayAsync(ct);
                foreach (var id in due)
                {
                    ct.ThrowIfCancellationRequested();
                    switch (await DeliverAsync(db, id, ct))
                    {
                        case InvitationDeliveryState.Sent: sent++; break;
                        case InvitationDeliveryState.Queued: retried++; break;
                        case InvitationDeliveryState.Failed: failed++; break;
                        case InvitationDeliveryState.Cancelled: cancelled++; break;
                        case InvitationDeliveryState.NotConfigured: notConfigured++; break;
                    }
                }
            }
            return new(tenants.Length == 0 ? "Idle" : "Dispatched", sent, retried, failed, cancelled, notConfigured);
        }
        finally { gate.Release(); }
    }

    // One row: claim (xmin-checked lease), check the invitation, send, record the outcome. Returns the resulting state, or
    // null when another instance holds the row.
    internal async Task<InvitationDeliveryState?> DeliverAsync(PoseidonDbContext db, Guid id, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var now = time.GetUtcNow();
        var row = await db.InvitationDeliveries.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (row == null || row.State != "QUEUED" || row.NextAttemptAt > now || row.LeaseUntil >= now) return null;
        var invitation = await db.MembershipInvitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == row.InvitationId, ct);
        // A closed or expired invitation is never sent; its payload is erased.
        if (invitation == null || invitation.Status != "Pending")
            return await Finish(db, row, new(InvitationDeliveryState.Cancelled, null, InvitationDeliveryPolicy.ErrorInvitationClosed), now, ct);
        if (now >= invitation.ExpiresAt)
            return await Finish(db, row, new(InvitationDeliveryState.Cancelled, null, InvitationDeliveryPolicy.ErrorInvitationExpired), now, ct);
        if (!mailer.Configured)
            return await Finish(db, row, new(InvitationDeliveryState.NotConfigured, null, InvitationDeliveryPolicy.ErrorNotConfigured), now, ct);
        if (protection.Unprotect(row.TenantId, row.Id, row.ProtectedPayload!) is not { } payload || !InvitationDeliveryPolicy.ValidEmail(payload.To) ||
            !MembershipInvitationPolicy.TryParse(payload.Token, out var tokenTenant, out var secret) || tokenTenant != row.TenantId ||
            MembershipInvitationPolicy.Hash(secret) != invitation.TokenHash)
            return await Finish(db, row, new(InvitationDeliveryState.Failed, null, InvitationDeliveryPolicy.ErrorPayloadUnavailable), now, ct);
        // Claim: count the attempt and take a lease before sending (a crash after an accepted send can resend once
        // after the lease: at-least-once, documented).
        var attempt = row.Attempts + 1;
        row.Attempts = attempt;
        row.LeaseUntil = now + TimeSpan.FromSeconds(settings.LeaseSeconds);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return null; }
        var tenantName = await db.Tenants.AsNoTracking().Where(t => t.Id == row.TenantId).Select(t => t.Name).SingleOrDefaultAsync(ct);
        InvitationSendOutcome outcome;
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(settings.SendTimeoutSeconds + 5));
            try { outcome = await mailer.SendAsync(new InvitationMailMessage(payload.To, settings.Link(payload.Token), invitation.ExpiresAt, tenantName), deadline.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { outcome = InvitationSendOutcome.TransientFailure; }
        }
        var step = InvitationDeliveryPolicy.After(attempt, outcome, time.GetUtcNow());
        // A retry that would fall after the invitation expires is pointless: stop as expired instead.
        if (step.State == InvitationDeliveryState.Queued && step.NextAttemptAt >= invitation.ExpiresAt)
            step = new(InvitationDeliveryState.Failed, null, InvitationDeliveryPolicy.ErrorExhausted);
        return await Finish(db, row, step, time.GetUtcNow(), ct);
    }

    private async Task<InvitationDeliveryState?> Finish(PoseidonDbContext db, InvitationDelivery row, InvitationDeliveryStep step, DateTimeOffset now, CancellationToken ct)
    {
        row.State = InvitationDeliveryPolicy.Wire(step.State);
        row.LastErrorCode = step.ErrorCode;
        row.LeaseUntil = null;
        if (step.State == InvitationDeliveryState.Queued) row.NextAttemptAt = step.NextAttemptAt;
        else { row.ProtectedPayload = null; row.CompletedAt = now; }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return null; }
        // Closed state and code only; the delivery id is an internal reference, not personal data.
        logger.LogInformation("Invitation delivery {DeliveryId} is {State}{Code}.", row.Id, row.State, step.ErrorCode is { } c ? " (" + c + ")" : "");
        return step.State;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Without an explicit valid transport configuration the dispatcher performs no DB access; invitations created
        // meanwhile are recorded NOT_CONFIGURED at creation and the copy-link flow stays available.
        if (!settings.Valid()) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) when (PostgresFailure.IsDatabase(e) || e is InvalidOperationException or AuditUnavailableException)
            {
                logger.LogWarning("Invitation delivery dispatch is unavailable ({ExceptionType}).", e.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), time, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
