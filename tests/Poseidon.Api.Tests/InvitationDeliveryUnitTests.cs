using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// USER-ADMIN-03 / UA3-AC-02, UA3-AC-05, UA3-AC-06: transport configuration, SMTP outcome classification, the protected
// outbox payload and no recipient/token/link/secret in logs. No host, database or mail server (an unreachable loopback
// port stands in for a down server).
[Trait("Category", "Unit")]
[Trait("Requirement", "USER-ADMIN-03")]
public sealed class InvitationDeliveryUnitTests
{
    private const string Recipient = "invitee@example.invalid";
    private const string Token = "pi1.55555555555545558555555555555555.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    // Generated per run: no credential-shaped literal in the repository.
    private static readonly string SmtpSecret = "synthetic-" + Guid.NewGuid().ToString("N");

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception) + (exception == null ? "" : " | " + exception));
    }

    private static InvitationDeliverySettings Valid() => new()
    {
        Enabled = true, PublicBaseUrl = "https://poseidon.example.invalid", SmtpHost = "127.0.0.1", SmtpPort = 1, Security = "None",
        From = "noreply@example.invalid", SendTimeoutSeconds = 2, LeaseSeconds = 30,
    };

    [Fact]
    public void The_transport_is_configured_only_by_a_complete_safe_operator_configuration()
    {
        Assert.True(Valid().Valid());
        Assert.False(new InvitationDeliverySettings().Valid()); // absent configuration = NOT_CONFIGURED
        var cases = new Action<InvitationDeliverySettings>[]
        {
            s => s.Enabled = false,
            s => s.PublicBaseUrl = "http://poseidon.example.invalid", // the link origin must be https
            s => s.PublicBaseUrl = "https://poseidon.example.invalid/?x=1",
            s => s.PublicBaseUrl = "https://user:pw@poseidon.example.invalid",
            s => s.PublicBaseUrl = null,
            s => s.SmtpHost = "mail.example.invalid", // plaintext SMTP only to a loopback mail catcher
            s => s.Security = "Ssl",
            s => s.From = "Name <noreply@example.invalid>",
            s => s.SmtpPort = 0,
            s => s.UserName = "smtp-user", // a user name needs a secret reference
            s => s.PasswordReference = "SMTP_PASSWORD",
            s => s.LeaseSeconds = 5,
        };
        foreach (var change in cases) { var s = Valid(); change(s); Assert.False(s.Valid()); }
        var tls = Valid(); tls.SmtpHost = "mail.example.invalid"; tls.Security = "StartTls"; tls.UserName = "smtp-user"; tls.PasswordReference = "SMTP_PASSWORD";
        Assert.True(tls.Valid());
    }

    [Fact]
    public void The_link_uses_the_configured_origin_and_keeps_the_token_in_the_fragment()
    {
        var link = Valid().Link(Token);
        Assert.Equal("https://poseidon.example.invalid/app/accept-invitation#token=" + Uri.EscapeDataString(Token), link);
        Assert.Equal("", new Uri(link).Query);
    }

    [Fact]
    public void Smtp_replies_are_classified_into_closed_outcomes()
    {
        Assert.Equal(InvitationSendOutcome.PermanentFailure, SmtpInvitationMailer.Classify(new SmtpException(SmtpStatusCode.MailboxUnavailable)));
        Assert.Equal(InvitationSendOutcome.TransientFailure, SmtpInvitationMailer.Classify(new SmtpException(SmtpStatusCode.MailboxBusy)));
        Assert.Equal(InvitationSendOutcome.TransientFailure, SmtpInvitationMailer.Classify(new SmtpException(SmtpStatusCode.GeneralFailure)));
        Assert.Equal(InvitationSendOutcome.TransientFailure, SmtpInvitationMailer.Classify(new IOException("connection reset")));
        Assert.Equal(InvitationSendOutcome.PermanentFailure, SmtpInvitationMailer.Classify(new SmtpFailedRecipientsException("rejected",
            [new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, Recipient)])));
    }

    [Fact]
    public async Task An_unreachable_server_is_a_transient_failure_and_nothing_personal_or_secret_is_logged()
    {
        var logger = new CapturingLogger<SmtpInvitationMailer>();
        var settings = Valid(); settings.UserName = "smtp-user"; settings.PasswordReference = "POSEIDON_TEST_SMTP_PASSWORD";
        var mailer = new SmtpInvitationMailer(settings, logger, name => name == "POSEIDON_TEST_SMTP_PASSWORD" ? SmtpSecret : null);
        var outcome = await mailer.SendAsync(new InvitationMailMessage(Recipient, settings.Link(Token), DateTimeOffset.UnixEpoch.AddDays(1), "FICTIONAL tenant"),
            TestContext.Current.CancellationToken);
        Assert.Equal(InvitationSendOutcome.TransientFailure, outcome);
        Assert.NotEmpty(logger.Lines);
        Assert.All(logger.Lines, line =>
        {
            Assert.DoesNotContain(Recipient, line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("invitee", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Token, line);
            Assert.DoesNotContain("AAAAAAAAAAAA", line);
            Assert.DoesNotContain("accept-invitation", line);
            Assert.DoesNotContain(SmtpSecret, line);
        });
    }

    [Fact]
    public async Task A_missing_credential_or_configuration_is_not_configured_never_sent()
    {
        var logger = new CapturingLogger<SmtpInvitationMailer>();
        var settings = Valid(); settings.UserName = "smtp-user"; settings.PasswordReference = "POSEIDON_TEST_SMTP_ABSENT";
        var mailer = new SmtpInvitationMailer(settings, logger, _ => null);
        var message = new InvitationMailMessage(Recipient, settings.Link(Token), DateTimeOffset.UnixEpoch, null);
        Assert.Equal(InvitationSendOutcome.NotConfigured, await mailer.SendAsync(message, TestContext.Current.CancellationToken));
        Assert.All(logger.Lines, line => Assert.DoesNotContain("POSEIDON_TEST_SMTP_ABSENT", line));
        Assert.False(new SmtpInvitationMailer(new InvitationDeliverySettings(), logger).Configured);
        Assert.Equal(InvitationSendOutcome.NotConfigured,
            await new SmtpInvitationMailer(new InvitationDeliverySettings(), logger).SendAsync(message, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_mail_body_carries_the_link_and_expiry_but_never_the_recipient()
    {
        var body = SmtpInvitationMailer.Body(new InvitationMailMessage(Recipient, Valid().Link(Token), new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero), "FICTIONAL tenant"));
        Assert.Contains(Valid().Link(Token), body);
        Assert.Contains("2026-11-23 08:00 UTC", body);
        Assert.DoesNotContain(Recipient, body);
    }

    [Fact]
    public void The_outbox_payload_is_protected_and_bound_to_its_tenant_and_row()
    {
        var protection = new InvitationPayloadProtection(new EphemeralDataProtectionProvider());
        Guid tenant = Guid.NewGuid(), delivery = Guid.NewGuid();
        var payload = protection.Protect(tenant, delivery, Recipient, Token);
        Assert.DoesNotContain(Recipient, payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Token, payload);
        Assert.True(payload.Length <= Poseidon.Persistence.InvitationDeliveryStorage.PayloadMaxLength);
        Assert.Equal((Recipient, Token), protection.Unprotect(tenant, delivery, payload));
        Assert.Null(protection.Unprotect(Guid.NewGuid(), delivery, payload)); // copied to another tenant
        Assert.Null(protection.Unprotect(tenant, Guid.NewGuid(), payload));   // copied to another row
        Assert.Null(protection.Unprotect(tenant, delivery, payload[..^4] + "AAAA"));
        // A lost key ring (another provider) fails closed instead of throwing.
        Assert.Null(new InvitationPayloadProtection(new EphemeralDataProtectionProvider()).Unprotect(tenant, delivery, payload));
    }
}
