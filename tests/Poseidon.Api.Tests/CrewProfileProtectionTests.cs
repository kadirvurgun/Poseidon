using System.Security.Cryptography;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Api.Tests;
public sealed class CrewProfileProtectionTests
{
    [Fact]
    public void Existing_identity_ciphertext_cannot_be_rebound_to_a_person_tenant_profile_field_or_removed_key() {
        var reference = "POSEIDON_TEST_IDENTITY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(reference, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        try {
            var policy = new CrewProfilePolicy { Version = "synthetic", ActiveKeyId = "key1", KeyReferences = new() { ["key1"] = reference } };
            var protection = new CrewProfileProtection(policy); var tenant = Guid.NewGuid(); var person = Guid.NewGuid();
            var cipher = protection.EncryptIdentity(tenant, person, CrewIdentityField.EmployeeNo, "00001");
            Assert.Equal("00001", protection.DecryptIdentity(tenant, person, CrewIdentityField.EmployeeNo, cipher));
            Assert.Throws<CryptographicException>(() => protection.DecryptIdentity(Guid.NewGuid(), person, CrewIdentityField.EmployeeNo, cipher));
            Assert.Throws<CryptographicException>(() => protection.DecryptIdentity(tenant, Guid.NewGuid(), CrewIdentityField.EmployeeNo, cipher));
            Assert.Throws<CryptographicException>(() => protection.DecryptIdentity(tenant, person, CrewIdentityField.FullName, cipher));
            Assert.Throws<CryptographicException>(() => protection.Decrypt(tenant, person, CrewPersonalField.PassportNo, cipher));
            policy.KeyReferences.Clear();
            Assert.Throws<CryptographicException>(() => protection.DecryptIdentity(tenant, person, CrewIdentityField.EmployeeNo, cipher));
        } finally { Environment.SetEnvironmentVariable(reference, null); }
    }
    [Fact]
    public void Encryption_requires_explicit_external_key_and_binds_person_tenant_and_field() {
        var reference = "POSEIDON_TEST_PROFILE_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(reference, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        try {
            var policy = new CrewProfilePolicy { Version = "synthetic-v1", ActiveKeyId = "key1", KeyReferences = new() { ["key1"] = reference } };
            var protection = new CrewProfileProtection(policy); var tenant = Guid.NewGuid(); var person = Guid.NewGuid();
            var cipher = protection.Encrypt(tenant, person, CrewPersonalField.PassportNo, "SYNTHETIC");
            Assert.Equal("SYNTHETIC", protection.Decrypt(tenant, person, CrewPersonalField.PassportNo, cipher));
            Assert.NotEqual(cipher, protection.Encrypt(tenant, person, CrewPersonalField.PassportNo, "SYNTHETIC"));
            Assert.Throws<CryptographicException>(() => protection.Decrypt(Guid.NewGuid(), person, CrewPersonalField.PassportNo, cipher));
            Assert.Throws<CryptographicException>(() => protection.Decrypt(tenant, Guid.NewGuid(), CrewPersonalField.PassportNo, cipher));
            Assert.Throws<CryptographicException>(() => protection.Decrypt(tenant, person, CrewPersonalField.SeamanBookNo, cipher));
            Environment.SetEnvironmentVariable(reference, null); Assert.False(protection.Available);
            Assert.Throws<CryptographicException>(() => protection.Decrypt(tenant, person, CrewPersonalField.PassportNo, cipher));
        } finally { Environment.SetEnvironmentVariable(reference, null); }
    }
    [Fact]
    public void Missing_field_identifier_never_defaults_to_nationality_permission() {
        var policy = new CrewProfilePolicy { Version = "synthetic-v1", ActiveKeyId = "key1", KeyReferences = new() { ["key1"] = "POSEIDON_SYNTHETIC_KEY" },
            Fields = [new() { Purpose = "crew-profile", ReadCapability = "crew.profile.identity.read", WriteCapability = "crew.profile.identity.write", ControlledValues = ["SYNTHETIC"] }] };
        Assert.False(policy.Valid);
        Assert.Null(policy.Rule(CrewPersonalField.Nationality,"crew-profile",new[] { "crew.profile.identity.read", "crew.profile.identity.write" },true));
    }
    [Theory]
    [InlineData("1\n")][InlineData("01")][InlineData("1e3")][InlineData("-1")][InlineData(" 1")]
    public void Experience_rejects_coercion_and_noncanonical_values(string value) => Assert.False(CrewPersonalData.Valid(CrewPersonalField.SeaServiceMonths, value));
}
