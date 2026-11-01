using System.Security.Cryptography;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;
namespace Poseidon.Api.Tests;
public sealed class CrewProfileProtectionTests
{
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
    [Theory]
    [InlineData("1\n")][InlineData("01")][InlineData("1e3")][InlineData("-1")][InlineData(" 1")]
    public void Experience_rejects_coercion_and_noncanonical_values(string value) => Assert.False(CrewPersonalData.Valid(CrewPersonalField.SeaServiceMonths, value));
}
