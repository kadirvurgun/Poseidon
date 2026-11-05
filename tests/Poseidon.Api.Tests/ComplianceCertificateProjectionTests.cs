using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-042/043 owning projection and real PostgreSQL interleavings. Authored;
// execution DEFERRED until the user's actual larger PR bundle gate.
public sealed partial class DirectoryWorkspaceTests
{
    private async Task<CertificateDto> PassportCertificateSave(HttpClient client, string number, CertificateDto? previous = null) {
        var id = previous?.Id ?? Guid.NewGuid();
        var content = new VesselCertificateContent(new CertificateMetadata(new CredentialDeclaration(
            CertificateNumber: number, Issuer: "FICTIONAL DECLARATION", IssueDate: new(2000, 1, 1), ExpiryDate: new(2099, 1, 1))));
        var command = new CertificateSaveCommand(id, previous?.Version ?? 0, content,
            previous == null ? "INITIAL_AUTHORING" : "AUTHORING_CORRECTION");
        using var response = await Send(client, $"vessels/{SeedVessel.Id}/certificates" + (previous == null ? "" : $"/{id}"), command, update: previous != null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
    }

    [Fact]
    public async Task Passport_certificate_projection_uses_owning_current_revision_without_qualification_or_read_side_writes() {
        await using var host = Host(); using var client = Client(host);
        GraphPermissionsForTest(host.Services); CertificatePermissions(host.Services); await Token(client);
        var first = await PassportCertificateSave(client, "SYNTHETIC ORIGINAL");
        var current = await PassportCertificateSave(client, "SYNTHETIC CORRECTED", first);
        long writes; await using (var before = Db()) writes = await before.AuditEvents.LongCountAsync(Ct);
        var passport = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        Assert.Equal("AUTHORIZED_PAGE_ONLY", passport.CertificateCoverage); Assert.Equal(passport.CertificateCoverage, passport.Certificates.Coverage);
        var row = Assert.Single(passport.Certificates.Items, row => row.Id == first.Id);
        Assert.Equal(current.Revision, row.Revision); Assert.Equal(current.Version, row.Version); Assert.Equal(current.Hash, row.Hash);
        Assert.Equal("SYNTHETIC CORRECTED", row.Number); Assert.Equal("REVIEW_REQUIRED", row.Qualification);
        Assert.Equal("REVIEW_REQUIRED", passport.State); Assert.Null(row.Document); Assert.Null(passport.Plan.OrphanCriticalCount);
        Assert.Contains("CERTIFICATE_QUALIFICATION_REQUIRED", passport.Blockers);
        Assert.Contains("CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED", passport.Blockers);
        Assert.Equal($"/app/certificate-command-center/{SeedVessel.Id:D}?id={first.Id:D}", row.Href);
        await using var after = Db(); Assert.Equal(writes, await after.AuditEvents.LongCountAsync(Ct));
        Assert.Equal(first.Hash, (await after.VesselCertificateRevisions.SingleAsync(r => r.CertificateId == first.Id && r.Revision == first.Revision, Ct)).Hash);
    }

    [Fact]
    public async Task Passport_certificate_page_has_its_own_bounded_has_more_and_never_infers_inventory_completeness() {
        await using var host = Host(); using var client = Client(host);
        GraphPermissionsForTest(host.Services); CertificatePermissions(host.Services); await Token(client);
        var created = new HashSet<Guid>();
        for (var index = 0; index < 26; index++) created.Add((await PassportCertificateSave(client, $"FICTIONAL PAGE {index}")).Id);
        var first = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport"), Ct))!;
        var next = (await client.GetFromJsonAsync<CompliancePassportDto>(CompliancePath("passport") + "?offset=25", Ct))!;
        Assert.Equal(25, first.Certificates.Items.Length); Assert.True(first.Certificates.HasMore); Assert.Equal(0, first.Certificates.Offset);
        Assert.Equal(25, next.Certificates.Offset); Assert.False(next.Certificates.HasMore);
        var returned = first.Certificates.Items.Concat(next.Certificates.Items).ToArray();
        Assert.Equal(26, returned.Count(row => created.Contains(row.Id))); Assert.Equal(returned.Length, returned.Select(row => row.Id).Distinct().Count());
        Assert.All(returned, row => Assert.Equal("REVIEW_REQUIRED", row.Qualification));
        Assert.Null(next.Plan.OrphanCriticalCount); Assert.Contains("CERTIFICATE_INVENTORY_COMPLETENESS_UNESTABLISHED", next.Blockers);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("head")]
    public async Task Passport_certificate_response_withdraws_changed_rights_or_head_after_its_parent_success_audit(string change) {
        await using var host = Host(); using var client = Client(host);
        GraphPermissionsForTest(host.Services); CertificatePermissions(host.Services); await Token(client);
        var certificate = await PassportCertificateSave(client, "FICTIONAL WITHHELD NUMBER");
        var key = (long)Random.Shared.Next(1, int.MaxValue);
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString()); await blocker.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", blocker)) { acquire.Parameters.AddWithValue("key", key); await acquire.ExecuteNonQueryAsync(Ct); }
        // Owned test-only DDL synchronizes the real parent audit await. Business
        // head changes below still go through the actual audited owning API.
        await InstallComplianceAuditPause(key);
        var pending = client.GetAsync(CompliancePath("passport"), Ct); HttpResponseMessage? response = null;
        try {
            await WaitForComplianceAuditLock(key);
            if (change == "head") await PassportCertificateSave(client, "FICTIONAL NEW HEAD", certificate);
            else {
                var settings = host.Services.GetRequiredService<FederationSettings>(); var member = settings.Memberships[0];
                settings.Memberships[0] = member with { Permissions = member.Permissions.Where(permission => permission != "certificate.read").ToArray() };
            }
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("FICTIONAL WITHHELD NUMBER", body); Assert.DoesNotContain(certificate.Id.ToString(), body);
        }
        finally {
            await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", blocker)) { release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(Ct); }
            response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); await RemoveComplianceAuditPause();
        }
    }
}
