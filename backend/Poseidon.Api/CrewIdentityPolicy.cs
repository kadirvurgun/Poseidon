using System.Security.Cryptography;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed class CrewIdentityFieldRule
{
    public CrewIdentityField? Field { get; set; }
    public string Purpose { get; set; } = "";
    public string ReadCapability { get; set; } = "";
    public string WriteCapability { get; set; } = "";
}

// Operator configuration. No default key, implicit lawful purpose or natural identity rule.
public sealed class CrewIdentityPolicy
{
    public string Version { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string EmployeeNumberMode { get; set; } = "";
    public Guid[] TenantIds { get; set; } = [];
    public CrewIdentityFieldRule[] Fields { get; set; } = [];
    public bool Configured => Version.Length is > 0 and <= 80 && Purpose.Length is > 0 and <= 80 &&
        SourceReference.Length is > 0 and <= 512 && TenantIds.Length > 0 && !TenantIds.Contains(Guid.Empty) &&
        (EmployeeNumberMode is "MutableDeclaration" or "ImmutableAfterCapture") &&
        Fields.Length == 2 && Fields.All(f => f.Field is { } configuredField && Enum.IsDefined(configuredField) && f.Purpose == Purpose &&
            f.ReadCapability == "crew.profile.identity.read" && f.WriteCapability == "crew.profile.identity.write") &&
        Fields.Select(f => f.Field).Distinct().Count() == 2;
    public bool Allows(TrustedActor a, bool write) => Configured && TenantIds.Contains(a.TenantId) &&
        a.Permissions.Contains("crew.identity.read") && a.Permissions.Contains("crew.profile.identity.read") &&
        (!write || a.HasWriteAttribution && a.Permissions.Contains("crew.identity.write") && a.Permissions.Contains("crew.profile.identity.write"));
}

internal static class CrewIdentityStorage
{
    internal static (string? Name, string? Employee) Open(Person person, CrewProfileProtection protection) {
        if (person.IdentityStorageState != "ProtectedV1") throw new CryptographicException("protected_storage_unavailable");
        return (protection.DecryptIdentity(person.TenantId, person.Id, CrewIdentityField.FullName, person.ProtectedFullName),
            protection.DecryptIdentity(person.TenantId, person.Id, CrewIdentityField.EmployeeNo, person.ProtectedEmployeeNo));
    }
    internal static void Capture(Person person, string? name, string? employee, CrewProfileProtection protection) {
        person.ProtectedFullName = protection.EncryptIdentity(person.TenantId, person.Id, CrewIdentityField.FullName, name);
        person.ProtectedEmployeeNo = protection.EncryptIdentity(person.TenantId, person.Id, CrewIdentityField.EmployeeNo, employee);
        person.FullName = null; person.EmployeeNo = null; person.IdentityStorageState = "ProtectedV1";
        person.IdentityRevision = checked(person.IdentityRevision + 1);
    }
}
