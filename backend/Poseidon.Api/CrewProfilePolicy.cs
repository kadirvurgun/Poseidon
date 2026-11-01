using System.Security.Cryptography;
using System.Text;
using Poseidon.Domain;
namespace Poseidon.Api;

public sealed class CrewProfileFieldRule
{
    public CrewPersonalField Field { get; set; }
    public string Purpose { get; set; } = "";
    public string ReadCapability { get; set; } = "";
    public string WriteCapability { get; set; } = "";
    public string[]? ControlledValues { get; set; }
}
// Trusted operator configuration, never deserialized from an API command.
// Empty/malformed/duplicate/missing field policy denies access, including Development.
public sealed class CrewProfilePolicy
{
    public string Version { get; set; } = "";
    public string ImportPurpose { get; set; } = "crew-profile";
    public string ActiveKeyId { get; set; } = "";
    public Dictionary<string, string> KeyReferences { get; set; } = new();
    public CrewProfileFieldRule[] Fields { get; set; } = [];
    public bool StorageConfigured => Version.Length is > 0 and <= 80 && ActiveKeyId.Length is > 0 and <= 40 &&
        ActiveKeyId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') && KeyReferences.ContainsKey(ActiveKeyId);
    public bool Valid => StorageConfigured &&
        Fields.Length > 0 && Fields.All(f => Enum.IsDefined(f.Field) && f.Purpose.Length is > 0 and <= 80 &&
            f.ReadCapability.StartsWith("crew.profile.", StringComparison.Ordinal) && f.WriteCapability.StartsWith("crew.profile.", StringComparison.Ordinal) &&
            (!Controlled(f.Field) || f.ControlledValues is { Length: > 0 } values && values.All(v => CrewPersonalData.Valid(f.Field, v)))) &&
        Fields.Select(f => f.Field).Distinct().Count() == Fields.Length;
    public static bool Controlled(CrewPersonalField f) => f is CrewPersonalField.Nationality or CrewPersonalField.PreferredLanguage or CrewPersonalField.VesselTypeExperience;
    public CrewProfileFieldRule? Rule(CrewPersonalField field, string purpose, IReadOnlyCollection<string> permissions, bool write) => !Valid ? null :
        Fields.SingleOrDefault(f => f.Field == field && f.Purpose == purpose && permissions.Contains(f.ReadCapability) && (!write || permissions.Contains(f.WriteCapability)));
    public bool ValueAllowed(CrewPersonalField field, string? value) => CrewPersonalData.Valid(field, value) &&
        (value == null || !Controlled(field) || Fields.SingleOrDefault(x => x.Field == field)?.ControlledValues?.Contains(value, StringComparer.Ordinal) == true);
}

// Key references reuse the existing operator secret resolver. No generated/ephemeral or plaintext fallback.
// Ciphertext binds the tenant/person/field/key version. Old keys must remain enrolled to read old data.
public sealed class CrewProfileProtection(CrewProfilePolicy policy)
{
    private static byte[] Aad(Guid tenant, Guid person, string field, string key) =>
        Encoding.UTF8.GetBytes($"poseidon.crew.profile.v1|{tenant:D}|{person:D}|{field}|{key}");
    private byte[] Key(string id) {
        if (!policy.StorageConfigured || !policy.KeyReferences.TryGetValue(id, out var reference)) throw new CryptographicException("protected_storage_unavailable");
        try {
            var raw = OperatorSecretReferences.Resolve(reference, Environment.GetEnvironmentVariable);
            var bytes = raw == null ? [] : Convert.FromBase64String(raw);
            if (bytes.Length != 32) throw new CryptographicException("protected_storage_unavailable");
            return bytes;
        } catch (Exception e) when (e is FormatException or InvalidOperationException) { throw new CryptographicException("protected_storage_unavailable"); }
    }
    public bool Available {
        get { try { var key = Key(policy.ActiveKeyId); CryptographicOperations.ZeroMemory(key); return true; } catch (CryptographicException) { return false; } }
    }
    public string? Encrypt(Guid tenant, Guid person, CrewPersonalField field, string? value) => Seal(tenant, person, "profile/" + CrewPersonalData.Key(field), value);
    public string? Decrypt(Guid tenant, Guid person, CrewPersonalField field, string? value) => Open(tenant, person, "profile/" + CrewPersonalData.Key(field), value, 16384);
    public string EncryptImport(Guid tenant, Guid batch, string table) => "protected-v1:" + Seal(tenant, batch, "import/table", table);
    public string DecryptImport(Guid tenant, Guid batch, string table) => !table.StartsWith("protected-v1:", StringComparison.Ordinal)
        ? throw new CryptographicException("protected_storage_unavailable") : Open(tenant, batch, "import/table", table[13..], 16 * 1024 * 1024)!;
    private string? Seal(Guid tenant, Guid person, string field, string? value) {
        if (value == null) return null;
        var key = Key(policy.ActiveKeyId); var plain = Encoding.UTF8.GetBytes(value);
        try {
            var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
            using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag, Aad(tenant, person, field, policy.ActiveKeyId));
            return string.Join('.', policy.ActiveKeyId, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
        } finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    private string? Open(Guid tenant, Guid person, string field, string? value, int maximum) {
        if (value == null) return null;
        byte[]? key = null; byte[]? plain = null;
        try {
            var pieces = value.Split('.'); if (pieces.Length != 4) throw new CryptographicException();
            key = Key(pieces[0]); var nonce = Convert.FromBase64String(pieces[1]); var tag = Convert.FromBase64String(pieces[2]); var cipher = Convert.FromBase64String(pieces[3]);
            if (nonce.Length != 12 || tag.Length != 16 || cipher.Length > maximum) throw new CryptographicException();
            plain = new byte[cipher.Length]; using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain, Aad(tenant, person, field, pieces[0])); return Encoding.UTF8.GetString(plain);
        } catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException) { throw new CryptographicException("protected_storage_unavailable"); }
        finally { if (key != null) CryptographicOperations.ZeroMemory(key); if (plain != null) CryptographicOperations.ZeroMemory(plain); }
    }
}
