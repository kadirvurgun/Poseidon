using System.Security.Cryptography;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class CrewLegacyImportProtectionTests
{
    [Theory]
    [InlineData("not-base64")]
    [InlineData("AQID")]
    public void Invalid_operator_key_material_is_unavailable_and_never_creates_plaintext_fallback(string invalid) {
        var reference = "POSEIDON_TEST_REJECTED_LEGACY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(reference, invalid);
        try {
            var protection = new CrewProfileProtection(new() { Version = "v1", ActiveKeyId = "key", KeyReferences = new() { ["key"] = reference } });
            Assert.False(protection.Available);
            Assert.Throws<CryptographicException>(() => protection.SealHistoricalImport(new() { TenantId = Guid.NewGuid(), Id = Guid.NewGuid() },
                new("FICTIONAL plaintext", "[]", "", "", "", null)));
        } finally { Environment.SetEnvironmentVariable(reference, null); }
    }
    [Fact]
    public void Historical_storage_authenticates_tenant_batch_vessel_field_purpose_policy_and_explicit_key_and_preserves_exact_content() {
        var reference = "POSEIDON_TEST_LEGACY_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Environment.SetEnvironmentVariable(reference, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        try {
            var policy = new CrewProfilePolicy { Version = "profile-v1", ActiveKeyId = "historic-key", KeyReferences = new() { ["historic-key"] = reference } };
            var protection = new CrewProfileProtection(policy);
            var batch = new ImportBatch { TenantId = Guid.NewGuid(), Id = Guid.NewGuid(), VesselId = Guid.NewGuid(),
                LegacyProtectionPolicyVersion = policy.Version, LegacyProtectionIdentityVersion = "identity-v1", LegacyProtectionPurpose = "current-storage-purpose" };
            var original = new HistoricalCrewImportSource("  UNSUPPORTED\r\n\u00e7\u0131\u015f  ", " [ {\"old\":null} ] ", "FICTIONAL HASH", "FICTIONAL filename", "FICTIONAL locator", null);
            batch.TableJson = protection.SealHistoricalImport(batch, original);
            Assert.Equal(original, protection.OpenHistoricalImport(batch));
            Assert.DoesNotContain(original.TableJson, batch.TableJson);
            void Denies(Action<ImportBatch> change, Action<ImportBatch> restore) { change(batch); Assert.Throws<CryptographicException>(() => protection.OpenHistoricalImport(batch)); restore(batch); }
            var tenant = batch.TenantId; Denies(b => b.TenantId = Guid.NewGuid(), b => b.TenantId = tenant);
            var id = batch.Id; Denies(b => b.Id = Guid.NewGuid(), b => b.Id = id);
            var vessel = batch.VesselId; Denies(b => b.VesselId = Guid.NewGuid(), b => b.VesselId = vessel);
            Denies(b => b.LegacyProtectionPurpose = "another-purpose", b => b.LegacyProtectionPurpose = "current-storage-purpose");
            Denies(b => b.LegacyProtectionPolicyVersion = "another-version", b => b.LegacyProtectionPolicyVersion = policy.Version);
            Denies(b => b.LegacyProtectionIdentityVersion = "another-version", b => b.LegacyProtectionIdentityVersion = "identity-v1");
            Assert.Throws<CryptographicException>(() => protection.DecryptImport(tenant, id, batch.TableJson));
            var cipher = batch.TableJson;
            batch.TableJson = LegacyCrewImport.Prefix + protection.EncryptImport(tenant, id, original.TableJson)[13..];
            Assert.Throws<CryptographicException>(() => protection.OpenHistoricalImport(batch)); // Ordinary table field cannot be rebound as history.
            batch.TableJson = cipher;
            policy.KeyReferences.Clear(); Assert.Throws<CryptographicException>(() => protection.OpenHistoricalImport(batch));
        } finally { Environment.SetEnvironmentVariable(reference, null); }
    }
}
