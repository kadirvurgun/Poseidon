using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// INVITE-RATELIMIT-FOLLOWUPS: N4 recipient fingerprint, N3 invitation transport configuration validation, and the
// invitation-issue / sign-in limiter wiring. No host, database or mail server.
[Trait("Category", "Unit")]
[Trait("Requirement", "INVITE-RATELIMIT-FOLLOWUPS")]
public sealed class InviteRateLimitFollowupUnitTests
{
    private const string Address = "invitee@example.invalid";
    // Generated per run: no credential-shaped literal in the repository.
    private static readonly string ServerSecret = "synthetic-" + Guid.NewGuid().ToString("N");

    // ---- N4 -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_recipient_fingerprint_is_keyed_normalized_and_never_contains_the_address()
    {
        var fingerprint = new InvitationAddressFingerprint(ServerSecret);
        Assert.True(fingerprint.Persistent);
        var value = fingerprint.For(Address)!;
        Assert.Matches("^a1:[0-9a-f]{64}$", value);
        Assert.DoesNotContain("invitee", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example", value, StringComparison.OrdinalIgnoreCase);
        // Same mailbox, other spelling: same fingerprint (replay). Another mailbox: another fingerprint (409).
        Assert.Equal(value, fingerprint.For("INVITEE@Example.Invalid"));
        Assert.NotEqual(value, fingerprint.For("invitee2@example.invalid"));
        Assert.NotEqual(value, fingerprint.For("invitee@example2.invalid"));
        Assert.Null(fingerprint.For(null));
        // Stable across restarts/instances with the same operator secret; keyed (not an unkeyed, dictionary-guessable hash).
        Assert.Equal(value, new InvitationAddressFingerprint(ServerSecret).For(Address));
        Assert.NotEqual(value, new InvitationAddressFingerprint(ServerSecret + "-rotated").For(Address));
        Assert.NotEqual(value[3..], Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Address))).ToLowerInvariant());
        // Invalid input never compares equal to a valid address.
        Assert.NotEqual(value, fingerprint.For(" " + Address));
    }

    [Fact]
    public void Without_a_server_secret_the_fingerprint_is_per_process_and_marked_so()
    {
        var one = new InvitationAddressFingerprint(null);
        var two = new InvitationAddressFingerprint("");
        Assert.False(one.Persistent);
        Assert.Matches("^p1:[0-9a-f]{64}$", one.For(Address)!);
        Assert.Equal(one.For(Address), one.For(Address.ToUpperInvariant()));
        Assert.NotEqual(one.For(Address), two.For(Address)); // fail closed after a restart: 409, never a second invitation
    }

    // ---- N3 -------------------------------------------------------------------------------------------------------

    private static InvitationDeliverySettings Valid() => new()
    {
        Enabled = true, PublicBaseUrl = "https://poseidon.example.invalid", SmtpHost = "127.0.0.1", SmtpPort = 1, Security = "None",
        From = "noreply@example.invalid", SendTimeoutSeconds = 2, LeaseSeconds = 30,
    };

    private static ServiceProvider RegisterDelivery(Dictionary<string, string?> values, Dictionary<string, string?>? secrets = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        services.AddInvitationDeliverySettings(configuration, name => secrets != null && secrets.TryGetValue(name, out var v) ? v : null);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void An_enabled_transport_with_an_incomplete_or_unsafe_configuration_stops_startup_with_reason_codes_only()
    {
        Assert.Empty(Valid().Problems());
        var cases = new (Action<InvitationDeliverySettings> Change, string Code)[]
        {
            (s => s.SmtpHost = null, "smtp_host"),
            (s => s.SmtpHost = "mail.example.invalid", "security"), // plaintext SMTP only to a loopback mail catcher
            (s => s.SmtpPort = 0, "smtp_port"),
            (s => s.From = "Name <noreply@example.invalid>", "from"),
            (s => s.PublicBaseUrl = "http://poseidon.example.invalid", "public_base_url"),
            (s => s.PublicBaseUrl = null, "public_base_url"),
            (s => s.UserName = "smtp-user", "credential"),
            (s => s.PasswordReference = "SMTP_PASSWORD", "credential"),
            (s => s.LeaseSeconds = 5, "lease_seconds"),
            (s => s.PollSeconds = 0, "poll_seconds"),
            (s => s.BatchSize = 101, "batch_size"),
            (s => s.SendTimeoutSeconds = 121, "send_timeout_seconds"),
            (s => s.Security = "Ssl", "security"),
        };
        foreach (var (change, code) in cases)
        {
            var settings = Valid(); change(settings);
            Assert.Contains(code, settings.Problems());
            Assert.False(settings.Valid());
            var error = Assert.Throws<InvalidOperationException>(() => settings.Validate(_ => null));
            Assert.Contains(code, error.Message);
            // No configured value is echoed (host, sender, origin, user name).
            Assert.DoesNotContain("example.invalid", error.Message);
            Assert.DoesNotContain("smtp-user", error.Message);
        }
    }

    [Fact]
    public void An_enabled_transport_whose_credential_reference_does_not_resolve_stops_startup()
    {
        var settings = Valid(); settings.SmtpHost = "mail.example.invalid"; settings.Security = "StartTls";
        settings.UserName = "smtp-user"; settings.PasswordReference = "POSEIDON_SMTP_SECRET";
        var error = Assert.Throws<InvalidOperationException>(() => settings.Validate(_ => null));
        Assert.DoesNotContain("POSEIDON_SMTP_SECRET", error.Message);
        settings.Validate(name => name == "POSEIDON_SMTP_SECRET" ? ServerSecret : null);
        Assert.True(settings.Valid());
    }

    [Fact]
    public void Absent_or_disabled_delivery_stays_not_configured_and_registration_validates_the_section()
    {
        using (var provider = RegisterDelivery([]))
            Assert.False(provider.GetRequiredService<InvitationDeliverySettings>().Valid());
        // Disabled with leftover transport settings: allowed (copy-link flow), reported by the startup warning.
        using (var provider = RegisterDelivery(new() { ["InvitationDelivery:Enabled"] = "false", ["InvitationDelivery:SmtpHost"] = "mail.example.invalid" }))
        {
            var settings = provider.GetRequiredService<InvitationDeliverySettings>();
            Assert.False(settings.Valid());
            Assert.True(settings.HasTransportSettings);
            Assert.Contains(provider.GetServices<IHostedService>(), s => s.GetType().Name == "InvitationDeliveryConfigurationNotice");
        }
        Assert.Throws<InvalidOperationException>(() => RegisterDelivery(new() { ["InvitationDelivery:Enabled"] = "true" }));
        Assert.Throws<InvalidOperationException>(() => RegisterDelivery(new() { ["InvitationDelivery:Unknown"] = "x" }));
        using (var provider = RegisterDelivery(new()
        {
            ["InvitationDelivery:Enabled"] = "true", ["InvitationDelivery:PublicBaseUrl"] = "https://poseidon.example.invalid",
            ["InvitationDelivery:SmtpHost"] = "127.0.0.1", ["InvitationDelivery:Security"] = "None", ["InvitationDelivery:From"] = "noreply@example.invalid",
        }))
            Assert.True(provider.GetRequiredService<InvitationDeliverySettings>().Valid());
    }

    [Fact]
    public void Only_the_explicit_disabled_mode_sweeps_unsent_rows_and_contradictory_modes_stop_startup()
    {
        // Missing or incomplete configuration: not configured, but never a sweep of rows a configured peer could send.
        Assert.False(new InvitationDeliverySettings().SweepsUnsent);
        var leftover = new InvitationDeliverySettings { SmtpHost = "mail.example.invalid" };
        Assert.False(leftover.SweepsUnsent);
        leftover.Validate(_ => null);
        // The deliberate fleet-wide decision.
        var disabled = new InvitationDeliverySettings { Mode = InvitationDeliverySettings.ModeDisabled };
        disabled.Validate(_ => null);
        Assert.True(disabled.SweepsUnsent);
        Assert.False(disabled.Valid());
        Assert.NotEqual(new InvitationDeliverySettings().ConfigurationDigest(), disabled.ConfigurationDigest());
        using (var provider = RegisterDelivery(new() { ["InvitationDelivery:Mode"] = "Disabled" }))
            Assert.True(provider.GetRequiredService<InvitationDeliverySettings>().SweepsUnsent);
        // Disabled together with an enabled transport, or an unknown mode, is contradictory.
        var both = Valid(); both.Mode = InvitationDeliverySettings.ModeDisabled;
        Assert.False(both.SweepsUnsent);
        Assert.Contains("mode", Assert.Throws<InvalidOperationException>(() => both.Validate(_ => null)).Message);
        Assert.Throws<InvalidOperationException>(() => RegisterDelivery(new() { ["InvitationDelivery:Mode"] = "disabled" }));
        Assert.Throws<InvalidOperationException>(() => RegisterDelivery(new() { ["InvitationDelivery:Mode"] = "Smtp" }));
    }

    [Fact]
    public async Task The_startup_notice_warns_when_unconfigured_without_the_disabled_decision()
    {
        var ct = TestContext.Current.CancellationToken;
        async Task<string[]> Notice(InvitationDeliverySettings settings)
        {
            var sink = new RecordingLoggerProvider();
            using var factory = LoggerFactory.Create(b => b.AddProvider(sink));
            var notice = new InvitationDeliveryConfigurationNotice(settings, factory.CreateLogger<InvitationDeliveryConfigurationNotice>());
            await notice.StartAsync(ct);
            return sink.Messages.ToArray();
        }
        Assert.Contains(await Notice(new InvitationDeliverySettings()), m => m.Contains("left queued, not finalized", StringComparison.Ordinal));
        var disabled = await Notice(new InvitationDeliverySettings { Mode = InvitationDeliverySettings.ModeDisabled });
        Assert.Contains(disabled, m => m.Contains("disabled by operator decision", StringComparison.Ordinal));
        Assert.DoesNotContain(disabled, m => m.Contains("left queued", StringComparison.Ordinal));
        var enabled = await Notice(Valid());
        Assert.DoesNotContain(enabled, m => m.Contains("left queued", StringComparison.Ordinal) || m.Contains("operator decision", StringComparison.Ordinal));
        // No configured value reaches the log.
        Assert.DoesNotContain(enabled, m => m.Contains("example.invalid", StringComparison.Ordinal) || m.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Recorder(Messages);
        public void Dispose() { }
        private sealed class Recorder(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception));
        }
    }

    // ---- L2 Data Protection key ring --------------------------------------------------------------------------------

    private static DataProtectionSettings Protection(Dictionary<string, string?> values, ServiceCollection? services = null) =>
        (services ?? new ServiceCollection()).AddPoseidonDataProtection(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

    [Fact]
    public void A_configured_key_directory_persists_one_shared_key_ring_and_invalid_paths_stop_startup()
    {
        Assert.False(Protection([]).Persisted);
        var directory = Path.Combine(Path.GetTempPath(), "poseidon-dp-" + Guid.NewGuid().ToString("N"));
        try
        {
            ServiceProvider Instance()
            {
                var services = new ServiceCollection();
                Assert.True(Protection(new() { ["DataProtection:KeyDirectory"] = directory }, services).Persisted);
                return services.BuildServiceProvider();
            }
            using var one = Instance();
            Assert.IsType<FileSystemXmlRepository>(one.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
            Assert.Equal(DataProtectionSettings.SharedApplicationName, one.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
            // Two instances sharing the directory read each other's protected payloads.
            using var two = Instance();
            var sealedValue = one.GetRequiredService<IDataProtectionProvider>().CreateProtector("test.v1").Protect("synthetic");
            Assert.Equal("synthetic", two.GetRequiredService<IDataProtectionProvider>().CreateProtector("test.v1").Unprotect(sealedValue));
            Assert.NotEmpty(Directory.GetFiles(directory, "*.xml"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        foreach (var invalid in new[] { "relative/keys", " ", Path.Combine(Path.GetTempPath(), "keys\u0001") })
            Assert.Contains("key_directory", Assert.Throws<InvalidOperationException>(() => Protection(new() { ["DataProtection:KeyDirectory"] = invalid })).Message);
        Assert.Throws<InvalidOperationException>(() => Protection(new() { ["DataProtection:Unknown"] = "x" }));
    }

    [Theory]
    [InlineData("Production", true, false, true)]
    [InlineData("Staging", true, false, true)]
    [InlineData("Production", true, true, false)]
    [InlineData("Production", false, false, false)]
    [InlineData("Development", true, false, false)]
    [InlineData("Test", true, false, false)]
    public void Invitation_email_outside_development_requires_the_persisted_key_ring(string environment, bool enabled, bool persisted, bool fails)
    {
        var delivery = enabled ? Valid() : new InvitationDeliverySettings();
        var protection = new DataProtectionSettings { KeyDirectory = persisted ? Path.GetTempPath() : null };
        void Check() => DataProtectionRuntime.RequirePersistedKeysForInvitationEmail(delivery, protection, new Environment(environment));
        if (fails) Assert.Contains("DataProtection:KeyDirectory", Assert.Throws<InvalidOperationException>(Check).Message);
        else Check();
    }

    [Fact]
    public void The_configuration_digest_is_equal_only_for_equal_transport_configurations_and_reveals_no_value()
    {
        var digest = Valid().ConfigurationDigest();
        Assert.Matches("^[0-9a-f]{16}$", digest);
        Assert.Equal(digest, Valid().ConfigurationDigest());
        var changes = new Action<InvitationDeliverySettings>[]
        {
            s => s.Enabled = false, s => s.PublicBaseUrl = "https://other.example.invalid", s => s.SmtpHost = "localhost", s => s.SmtpPort = 2,
            s => s.From = "other@example.invalid", s => s.UserName = "u", s => s.PasswordReference = "OTHER_REF", s => s.PollSeconds = 16,
            s => s.BatchSize = 21, s => s.LeaseSeconds = 31, s => s.SendTimeoutSeconds = 3, s => s.Security = "StartTls",
        };
        foreach (var change in changes) { var s = Valid(); change(s); Assert.NotEqual(digest, s.ConfigurationDigest()); }
    }

    // ---- Rate limit wiring ----------------------------------------------------------------------------------------

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Poseidon.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ServiceProvider RegisterLimits(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSharedRateLimiting(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new Environment("Test"), _ => null);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Invitation_issue_and_sign_in_limiters_are_registered_with_closed_policies()
    {
        using var provider = RegisterLimits([]);
        Assert.NotNull(provider.GetRequiredService<InvitationIssueRateLimiter>());
        Assert.Equal(("invitation.issue", 60, TimeSpan.FromMinutes(15)),
            (InvitationIssueRateLimiter.Policy.Name, InvitationIssueRateLimiter.Policy.PermitLimit, InvitationIssueRateLimiter.Policy.Window));
        var login = provider.GetRequiredService<LoginRateLimiter>().Policy;
        Assert.Equal(("auth.login", LoginRateLimiter.DefaultPermitLimit, TimeSpan.FromMinutes(5)), (login.Name, login.PermitLimit, login.Window));
        using var configured = RegisterLimits(new() { ["RateLimiting:LoginPermitLimit"] = "500" });
        Assert.Equal(500, configured.GetRequiredService<LoginRateLimiter>().Policy.PermitLimit);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("100001")]
    public void An_out_of_range_sign_in_limit_stops_startup(string value) =>
        Assert.Throws<InvalidOperationException>(() => RegisterLimits(new() { ["RateLimiting:LoginPermitLimit"] = value }));

    [Fact]
    public async Task The_issue_budget_is_per_tenant_and_administrator_and_refuses_once_audibly_then_quietly()
    {
        using var provider = RegisterLimits([]);
        var limiter = provider.GetRequiredService<InvitationIssueRateLimiter>();
        var ct = TestContext.Current.CancellationToken;
        Guid tenant = Guid.NewGuid(), admin = Guid.NewGuid();
        for (var i = 0; i < InvitationIssueRateLimiter.IssuesPerWindow; i++) Assert.True((await limiter.AcquireAsync(tenant, admin, ct)).Allowed);
        var first = await limiter.AcquireAsync(tenant, admin, ct);
        Assert.Equal(RateLimitOutcome.Limited, first.Outcome);
        Assert.InRange(int.Parse(first.RetryAfterHeader, System.Globalization.CultureInfo.InvariantCulture), 1, 900);
        Assert.Equal(RateLimitOutcome.LimitedRepeat, (await limiter.AcquireAsync(tenant, admin, ct)).Outcome);
        Assert.True((await limiter.AcquireAsync(tenant, Guid.NewGuid(), ct)).Allowed); // another administrator
        Assert.True((await limiter.AcquireAsync(Guid.NewGuid(), admin, ct)).Allowed); // the same id in another tenant
    }

    [Theory]
    [InlineData("203.0.113.7", "v4:203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "v4:203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "v6:2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "v6:2001:db8:1:2::/64")]
    public void Sign_in_partitions_by_ipv4_address_or_ipv6_subscriber_prefix(string address, string partition) =>
        Assert.Equal(partition, LoginRateLimiter.Network(IPAddress.Parse(address)));

    [Fact]
    public async Task Sign_in_starts_are_counted_per_client_network()
    {
        using var provider = RegisterLimits(new() { ["RateLimiting:LoginPermitLimit"] = "10" });
        var limiter = provider.GetRequiredService<LoginRateLimiter>();
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal("none", LoginRateLimiter.Network(null));
        for (var i = 0; i < 10; i++) Assert.True((await limiter.AcquireAsync(IPAddress.Parse($"2001:db8:9:9::{i + 1:x}"), ct)).Allowed);
        // The same /64 shares the budget; another network does not.
        Assert.Equal(RateLimitOutcome.Limited, (await limiter.AcquireAsync(IPAddress.Parse("2001:db8:9:9::ff"), ct)).Outcome);
        Assert.True((await limiter.AcquireAsync(IPAddress.Parse("2001:db8:9:a::1"), ct)).Allowed);
        Assert.True((await limiter.AcquireAsync(IPAddress.Parse("198.51.100.1"), ct)).Allowed);
    }

    // The exported contract (contracts/openapi.v1.json, regenerated from the Release host) advertises the 429.
    [Theory]
    [InlineData("/api/v1/memberships", "post")]
    [InlineData("/api/v1/memberships/{id}/invitation", "post")]
    [InlineData("/api/v1/auth/login", "get")]
    public void The_contract_documents_the_429_on_the_limited_operations(string path, string method)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Poseidon.slnx"))) dir = dir.Parent;
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "contracts", "openapi.v1.json")));
        Assert.True(contract.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").TryGetProperty("429", out _));
    }
}
