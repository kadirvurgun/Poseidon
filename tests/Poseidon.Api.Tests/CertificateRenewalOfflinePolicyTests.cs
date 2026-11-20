using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// S10 WP-C DEV-066: closed offline policy (no approved classification exists, so only the most restrictive class
// and a complete classification permit local persistence) and the online-only linked reference rule.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-066")]
public sealed class CertificateRenewalOfflinePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
    private static CertificateRenewalOfflinePolicy Policy(Guid tenant, Guid actor, Guid vessel) => new() {
        Version = "fictional-v1", SourceVersion = VesselCertificatePolicy.SourceVersion, StoragePolicyReference = "FICTIONAL storage",
        KeyPolicyReference = "FICTIONAL key", RetentionPolicyReference = "FICTIONAL retention", RetentionSeconds = 7200, LeaseSeconds = 600,
        NotBefore = Now.AddDays(-1), ExpiresAt = Now.AddDays(1), MaxEnrollments = 2, MaxRecords = 8, MaxEncryptedBytes = 262144, MaxDeviceRecords = 16, MaxDeviceEncryptedBytes = 1048576,
        Fields = CertificateRenewalOfflinePolicy.Parts.Select(p => new CertificateRenewalOfflineField(p, "FICTIONAL", "FICTIONAL", "RestrictedOperationalDraft")).ToArray(),
        Scopes = [new(tenant, actor, vessel)] };

    [Fact]
    public void Only_a_complete_most_restrictive_scoped_policy_allows_offline_drafts() {
        Guid tenant = Guid.NewGuid(), actor = Guid.NewGuid(), vessel = Guid.NewGuid();
        var policy = Policy(tenant, actor, vessel);
        Assert.True(policy.Allows(tenant, actor, vessel, Now));
        Assert.False(policy.Allows(tenant, Guid.NewGuid(), vessel, Now));
        Assert.False(policy.Allows(tenant, actor, vessel, Now.AddDays(2)));
        Assert.Null(new CertificateRenewalOfflineSettings().For(tenant, actor, vessel, Now));
        var fields = policy.Fields;
        policy.Fields = [.. fields.Take(4)]; Assert.False(policy.Configured);
        policy.Fields = [.. fields.Take(4), fields[4] with { PrivacyClass = "NonPersonalOperationalDraft" }]; Assert.False(policy.Configured);
        policy.Fields = fields; policy.SourceVersion = "other"; Assert.False(policy.Configured);
        policy.SourceVersion = VesselCertificatePolicy.SourceVersion; policy.RetentionPolicyReference = " "; Assert.False(policy.Configured);
        policy.RetentionPolicyReference = "FICTIONAL retention"; policy.RetentionSeconds = 60; Assert.False(policy.Configured);
    }

    [Fact]
    public void Fingerprint_is_stable_under_field_and_scope_order() {
        Guid tenant = Guid.NewGuid(), actor = Guid.NewGuid(), vessel = Guid.NewGuid();
        var a = Policy(tenant, actor, vessel); var b = Policy(tenant, actor, vessel); b.Fields = b.Fields.Reverse().ToArray();
        Assert.Equal(a.Fingerprint(), b.Fingerprint());
        b.LeaseSeconds = 601; Assert.NotEqual(a.Fingerprint(), b.Fingerprint());
    }

    [Fact]
    public void Offline_draft_may_change_metadata_but_never_replacement_or_document_links() {
        var server = new CertificateRenewalContent(PlannedLocation: "FICTIONAL", NewDocument: new(Guid.NewGuid(), 1, new string('A', 64)));
        Assert.True(CertificateRenewalOfflinePolicy.OfflineEditable(server with { PlannedLocation = "FICTIONAL changed", DueDate = new(2027, 1, 1) }, server));
        Assert.False(CertificateRenewalOfflinePolicy.OfflineEditable(server with { NewDocument = null }, server));
        Assert.False(CertificateRenewalOfflinePolicy.OfflineEditable(server with { NewCertificateId = Guid.NewGuid(), NewCertificateRevision = 1, NewCertificateVersion = 1 }, server));
        Assert.False(CertificateRenewalOfflinePolicy.OfflineEditable(server with { LeadDaysNumeric = "not-a-number" }, server));
    }
}
