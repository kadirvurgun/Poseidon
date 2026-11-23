using Microsoft.AspNetCore.DataProtection;

namespace Poseidon.Api;

// INVITE-RATELIMIT-FOLLOWUPS L2: explicit Data Protection key persistence (operator configuration, section
// "DataProtection"). Without KeyDirectory the framework default applies (keys per machine/user profile, or in memory in
// a container), so a payload protected on one instance or before a restart may be unreadable on another
// (invitation e-mail fails closed as FAILED/payload_unavailable). With KeyDirectory every instance reads and writes one
// key ring under one application name, so protected payloads, antiforgery tokens and protected cookies survive restarts
// and work across instances. The directory is operator infrastructure: a volume shared by all API instances, writable
// only by the API service account and covered by the deployment backup. Keys at rest are protected only by the
// directory's access control and the volume's encryption; nothing here reads or logs key material.
public sealed class DataProtectionSettings
{
    public const string SharedApplicationName = "Poseidon";
    // Absolute path of the shared key ring directory. Unset = framework default (not persisted for this purpose).
    public string? KeyDirectory { get; set; }

    public bool Persisted => KeyDirectory != null;

    internal static DataProtectionSettings From(IConfiguration configuration) =>
        configuration.GetSection("DataProtection").Get<DataProtectionSettings>(o => o.ErrorOnUnknownConfiguration = true) ?? new();

    // A relative, blank or control-character path is refused (reason code only, never the configured value).
    internal void Validate()
    {
        if (KeyDirectory == null) return;
        if (string.IsNullOrWhiteSpace(KeyDirectory) || KeyDirectory.Any(char.IsControl) || !Path.IsPathFullyQualified(KeyDirectory))
            throw new InvalidOperationException("Data Protection configuration is invalid: key_directory.");
    }
}

public static class DataProtectionRuntime
{
    public static DataProtectionSettings AddPoseidonDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = DataProtectionSettings.From(configuration);
        settings.Validate();
        var builder = services.AddDataProtection();
        if (settings.Persisted)
            builder.SetApplicationName(DataProtectionSettings.SharedApplicationName).PersistKeysToFileSystem(new DirectoryInfo(settings.KeyDirectory!));
        services.AddSingleton(settings);
        return settings;
    }

    // L2 fail fast: invitation e-mail keeps the recipient and token protected in the outbox until the dispatcher (on any
    // instance, possibly after a restart) sends it. Enabling it outside Development/Test without a persisted key ring would
    // turn every restart or instance switch into lost invitations, so startup stops instead.
    internal static void RequirePersistedKeysForInvitationEmail(InvitationDeliverySettings delivery, DataProtectionSettings protection, IHostEnvironment environment)
    {
        if (delivery.Enabled && !protection.Persisted && !environment.IsDevelopment() && !environment.IsEnvironment("Test"))
            throw new InvalidOperationException("Invitation e-mail delivery is enabled but the Data Protection key ring is not persisted: DataProtection:KeyDirectory.");
    }
}
