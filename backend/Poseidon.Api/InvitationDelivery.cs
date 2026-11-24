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
    public const string ModeDisabled = "Disabled";
    public bool Enabled { get; set; }
    // INVITE-RATELIMIT-FOLLOWUPS N3: null (default) or "Disabled". "Disabled" is the operator's deliberate, fleet-wide
    // decision that no instance sends invitation e-mail: only then does the dispatcher finalize queued rows (of any
    // instance) as NOT_CONFIGURED. A merely missing or incomplete transport never finalizes rows another instance could send.
    public string? Mode { get; set; }
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

    public bool Valid() => Enabled && Problems().Count == 0;

    // INVITE-RATELIMIT-FOLLOWUPS N3: closed reason codes (never values) for an incomplete or unsafe transport configuration.
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(SmtpHost) || SmtpHost.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) problems.Add("smtp_host");
        if (SmtpPort is < 1 or > 65535) problems.Add("smtp_port");
        if (!InvitationDeliveryPolicy.ValidEmail(From)) problems.Add("from");
        if (PollSeconds is < 1 or > 3600) problems.Add("poll_seconds");
        if (BatchSize is < 1 or > 100) problems.Add("batch_size");
        if (SendTimeoutSeconds is < 1 or > 120) problems.Add("send_timeout_seconds");
        if (LeaseSeconds <= SendTimeoutSeconds + 5 || LeaseSeconds > 900) problems.Add("lease_seconds");
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps || origin.Query.Length > 0 ||
            origin.Fragment.Length > 0 || !string.IsNullOrEmpty(origin.UserInfo)) problems.Add("public_base_url");
        if (Security == "None" ? !Loopback(SmtpHost) : Security != "StartTls") problems.Add("security");
        if ((UserName == null) != (PasswordReference == null) || UserName != null && (UserName is not { Length: > 0 and <= 256 } || UserName.Any(char.IsControl)))
            problems.Add("credential");
        return problems;
    }

    // N3: true only when the operator set Mode=Disabled (and did not enable a transport). Only such an instance sweeps
    // due QUEUED rows to NOT_CONFIGURED (USER-ADMIN-03 M2 retention); every other instance without a valid transport
    // leaves them for an instance that can send (closed/expired invitations are still cancelled by any instance).
    public bool SweepsUnsent => Mode == ModeDisabled && !Enabled;

    // N3 startup validation. Enabled with an incomplete/unsafe configuration, or with a credential reference that does not
    // resolve, stops startup (fail fast, reason codes only): an instance meant to send never silently falls back. An
    // unknown Mode, or Mode=Disabled together with Enabled, is contradictory and stops startup too.
    internal void Validate(Func<string, string?> secrets)
    {
        if (Mode != null && Mode != ModeDisabled)
            throw new InvalidOperationException("Invitation e-mail delivery mode is invalid: mode.");
        if (Mode == ModeDisabled && Enabled)
            throw new InvalidOperationException("Invitation e-mail delivery is both enabled and disabled: mode.");
        if (!Enabled) return;
        var problems = Problems();
        if (problems.Count > 0)
            throw new InvalidOperationException("Invitation e-mail delivery is enabled but its configuration is invalid: " + string.Join(", ", problems) + ".");
        if (PasswordReference != null)
        {
            string? value;
            try { value = OperatorSecretReferences.Resolve(PasswordReference, secrets); }
            catch (InvalidOperationException) { value = null; }
            if (value == null) throw new InvalidOperationException("Invitation e-mail delivery is enabled but its credential reference is unavailable.");
        }
    }

    // Transport fields set while delivery is not enabled (a disabled instance among enabled peers, see Validate).
    internal bool HasTransportSettings => !Enabled && (SmtpHost != null || PublicBaseUrl != null || From != null || UserName != null || PasswordReference != null);

    // N3: a short digest of the non-secret transport configuration, logged once at startup so operators can verify that
    // every API instance runs the same configuration. The password is never read for it (only the reference name), and
    // the digest is not reversible to the host, sender or user name.
    public string ConfigurationDigest()
    {
        var canonical = string.Join('\n', Enabled ? "1" : "0", Mode ?? "", PublicBaseUrl ?? "", SmtpHost ?? "", SmtpPort.ToString(CultureInfo.InvariantCulture), Security,
            From ?? "", UserName ?? "", PasswordReference ?? "", PollSeconds.ToString(CultureInfo.InvariantCulture), BatchSize.ToString(CultureInfo.InvariantCulture),
            LeaseSeconds.ToString(CultureInfo.InvariantCulture), SendTimeoutSeconds.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("poseidon.invitation-delivery.config.v1\n" + canonical))).ToLowerInvariant()[..16];
    }

    private static bool Loopback(string? host) => host == "localhost" || IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);

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
        try
        {
            // QA L1: message construction is inside the try, so a malformed address is a classified outcome, never an
            // exception that escapes the dispatcher.
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

    // QA L4: the tenant name is administrator text; it is put on one line (control/format characters and line or paragraph
    // separators become a space, runs of whitespace collapse) and bounded, so it cannot add lines or fake paragraphs.
    internal static string OneLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = value.Select(c => char.IsControl(c) || char.IsWhiteSpace(c) || char.GetUnicodeCategory(c) is UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : c).ToArray();
        var text = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= 120 ? text : text[..120];
    }

    internal static string Body(InvitationMailMessage m)
    {
        var expires = m.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var name = OneLine(m.TenantName);
        var organisation = name.Length == 0 ? "" : $" ({name})";
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

public sealed record InvitationDispatchResult(string Status, int Sent, int Retried, int Failed, int Cancelled, int NotConfigured, int Errored = 0);

// Dispatcher. Identity: the explicit system actor "system:invitation-delivery" (ActorType System, Source InvitationDelivery);
// every state change is an audited SaveChanges write in the row's own tenant context. The only cross-tenant read is the due
// scan below: tenant ids of due rows, read with a context that never writes (same pattern as the login lookup).
public sealed class InvitationDeliveryDispatcher(IConfiguration configuration, IHostEnvironment environment, InvitationDeliverySettings settings,
    IInvitationMailer mailer, InvitationPayloadProtection protection, TimeProvider time, ILogger<InvitationDeliveryDispatcher> logger) : BackgroundService
{
    public const string ActorId = "system:invitation-delivery";
    // Tenants with due rows handled per run; the cursor rotates so no tenant starves behind lower tenant ids (QA L5).
    internal const int TenantsPerRun = 64;
    // Without a valid transport the dispatcher runs at a slower cadence: it cancels rows of closed/expired invitations and,
    // only under Mode=Disabled (N3), sweeps the other due rows to NOT_CONFIGURED (QA M2).
    internal static readonly TimeSpan UnconfiguredSweep = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim gate = new(1, 1);
    private Guid cursor = Guid.Empty;

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
            Guid[] due;
            await using (var scan = Open(Guid.Empty, Guid.NewGuid().ToString("N")))
            {
                if (scan == null) return new("Unavailable", 0, 0, 0, 0, 0);
                due = await scan.InvitationDeliveries.IgnoreQueryFilters().AsNoTracking()
                    .Where(d => d.State == "QUEUED" && d.NextAttemptAt <= now && (d.LeaseUntil == null || d.LeaseUntil < now))
                    .Select(d => d.TenantId).Distinct().OrderBy(t => t).ToArrayAsync(ct);
            }
            // QA L5: round-robin from the tenant after the last one handled.
            var tenants = due.Where(t => t.CompareTo(cursor) > 0).Concat(due.Where(t => t.CompareTo(cursor) <= 0)).Take(TenantsPerRun).ToArray();
            if (tenants.Length > 0) cursor = tenants[^1];
            int sent = 0, retried = 0, failed = 0, cancelled = 0, notConfigured = 0, errored = 0;
            foreach (var tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                // QA M1: one tenant's or one row's failure never stops the others; the error is logged by type only.
                try
                {
                    await using var db = Open(tenant, Guid.NewGuid().ToString("N"));
                    if (db == null) return new("Unavailable", sent, retried, failed, cancelled, notConfigured, errored);
                    var rows = await db.InvitationDeliveries.AsNoTracking()
                        .Where(d => d.State == "QUEUED" && d.NextAttemptAt <= now && (d.LeaseUntil == null || d.LeaseUntil < now))
                        .OrderBy(d => d.NextAttemptAt).ThenBy(d => d.Id).Select(d => d.Id).Take(settings.BatchSize is >= 1 and <= 100 ? settings.BatchSize : 20).ToArrayAsync(ct);
                    foreach (var id in rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        InvitationDeliveryState? state;
                        try { state = await DeliverAsync(db, id, ct); }
                        catch (Exception e) when (Isolated(e, ct))
                        {
                            db.ChangeTracker.Clear();
                            errored++;
                            logger.LogWarning("Invitation delivery {DeliveryId} could not be processed ({ExceptionType}).", id, e.GetType().Name);
                            continue;
                        }
                        switch (state)
                        {
                            case InvitationDeliveryState.Sent: sent++; break;
                            case InvitationDeliveryState.Queued: retried++; break;
                            case InvitationDeliveryState.Failed: failed++; break;
                            case InvitationDeliveryState.Cancelled: cancelled++; break;
                            case InvitationDeliveryState.NotConfigured: notConfigured++; break;
                        }
                    }
                }
                catch (Exception e) when (Isolated(e, ct))
                {
                    errored++;
                    logger.LogWarning("Invitation delivery for one tenant could not be processed ({ExceptionType}).", e.GetType().Name);
                }
            }
            return new(tenants.Length == 0 ? "Idle" : "Dispatched", sent, retried, failed, cancelled, notConfigured, errored);
        }
        finally { gate.Release(); }
    }

    // Everything except shutdown cancellation is isolated to its row/tenant.
    private static bool Isolated(Exception e, CancellationToken ct) => !(e is OperationCanceledException && ct.IsCancellationRequested);

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
        // N3: without a transport the row is finalized only under the deliberate fleet-wide Mode=Disabled; otherwise it
        // stays QUEUED (untouched, payload kept encrypted) for an instance that can send it.
        if (!mailer.Configured)
            return settings.SweepsUnsent
                ? await Finish(db, row, new(InvitationDeliveryState.NotConfigured, null, InvitationDeliveryPolicy.ErrorNotConfigured), now, ct)
                : null;
        if (protection.Unprotect(row.TenantId, row.Id, row.ProtectedPayload!) is not { } payload || !InvitationDeliveryPolicy.ValidEmail(payload.To) ||
            !MembershipInvitationPolicy.TryParse(payload.Token, out var tokenTenant, out var secret) || tokenTenant != row.TenantId ||
            MembershipInvitationPolicy.Hash(secret) != invitation.TokenHash)
            return await Finish(db, row, new(InvitationDeliveryState.Failed, null, InvitationDeliveryPolicy.ErrorPayloadUnavailable), now, ct);
        // QA M1: a row whose last allowed attempt was claimed but never finished (crash or lost final write) is terminal,
        // never claimed again past the ceiling.
        if (row.Attempts >= InvitationDeliveryPolicy.MaxAttempts)
            return await Finish(db, row, new(InvitationDeliveryState.Failed, null, InvitationDeliveryPolicy.ErrorExhausted), now, ct);
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
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                // Any transport fault (including a mailer that throws) is a transient attempt, never an escaped exception.
                outcome = InvitationSendOutcome.TransientFailure;
                if (e is not OperationCanceledException) logger.LogWarning("Invitation e-mail transport fault ({ExceptionType}).", e.GetType().Name);
            }
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
        // QA M2 / N3: the dispatcher always runs. With a valid transport it sends every PollSeconds. Without one it runs
        // every 5 minutes: rows of closed/expired invitations end CANCELLED (payload erased) on any instance; the other due
        // rows end NOT_CONFIGURED only when the operator set InvitationDelivery:Mode=Disabled (a deliberate fleet-wide
        // decision). A missing or incomplete configuration never finalizes rows a correctly configured peer could send.
        // Without a database connection it does nothing. No exception except shutdown leaves this loop (QA L1).
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogWarning("Invitation delivery dispatch is unavailable ({ExceptionType}).", e.GetType().Name);
            }
            var wait = mailer.Configured && settings.PollSeconds is >= 1 and <= 3600 ? TimeSpan.FromSeconds(settings.PollSeconds) : UnconfiguredSweep;
            try { await Task.Delay(wait, time, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}

// INVITE-RATELIMIT-FOLLOWUPS N3: operator configuration registration with fail-fast validation and a startup notice that
// names the transport state and the non-secret configuration digest, so operators can compare all API instances.
public static partial class InvitationDeliveryRuntime
{
    public static InvitationDeliverySettings AddInvitationDeliverySettings(this IServiceCollection services, IConfiguration configuration) =>
        services.AddInvitationDeliverySettings(configuration, Environment.GetEnvironmentVariable);

    internal static InvitationDeliverySettings AddInvitationDeliverySettings(this IServiceCollection services, IConfiguration configuration, Func<string, string?> secrets)
    {
        var settings = configuration.GetSection("InvitationDelivery").Get<InvitationDeliverySettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new();
        settings.Validate(secrets);
        services.AddSingleton(settings);
        services.AddHostedService<InvitationDeliveryConfigurationNotice>();
        return settings;
    }

    [LoggerMessage(EventId = 4301, Level = LogLevel.Information,
        Message = "Invitation e-mail transport {State}; configuration digest {Digest}. Every API instance must report the same digest.")]
    internal static partial void Transport(ILogger logger, string state, string digest);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Warning,
        Message = "Invitation e-mail transport settings are present but delivery is not enabled; this instance records new invitation e-mail as NOT_CONFIGURED.")]
    internal static partial void DisabledWithSettings(ILogger logger);

    [LoggerMessage(EventId = 4303, Level = LogLevel.Warning,
        Message = "Invitation e-mail transport is not configured on this instance and InvitationDelivery:Mode is not Disabled; queued invitation e-mail is left queued, not finalized. Set InvitationDelivery:Mode=Disabled on every instance to finalize it as NOT_CONFIGURED.")]
    internal static partial void UnconfiguredWithoutDecision(ILogger logger);

    [LoggerMessage(EventId = 4304, Level = LogLevel.Information,
        Message = "Invitation e-mail delivery is disabled by operator decision (Mode=Disabled); this instance finalizes queued invitation e-mail as NOT_CONFIGURED.")]
    internal static partial void DisabledByDecision(ILogger logger);
}

internal sealed class InvitationDeliveryConfigurationNotice(InvitationDeliverySettings settings, ILogger<InvitationDeliveryConfigurationNotice> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        InvitationDeliveryRuntime.Transport(logger, settings.Valid() ? "enabled" : settings.SweepsUnsent ? "disabled" : "not configured", settings.ConfigurationDigest());
        if (settings.HasTransportSettings) InvitationDeliveryRuntime.DisabledWithSettings(logger);
        if (settings.SweepsUnsent) InvitationDeliveryRuntime.DisabledByDecision(logger);
        else if (!settings.Valid()) InvitationDeliveryRuntime.UnconfiguredWithoutDecision(logger);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
