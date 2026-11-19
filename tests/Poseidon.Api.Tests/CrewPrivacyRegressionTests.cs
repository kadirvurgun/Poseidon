using System.Reflection;
using System.Text.RegularExpressions;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S7 WP-A DEV-051 / T-S7-005 (18.01 Backlog row 52): protected crew fields never reach
// generic search, export or metric surfaces, and the new crew compliance contracts carry
// identifiers and codes only. Source/reflection guards (no database); the real-host
// regression with canary values is CrewComplianceTests (Integration, PG).
[Trait("Requirement", "DEV-051")]
public sealed class CrewPrivacyRegressionTests
{
    private static readonly Regex Protected = new(@"\b(Persons|CrewPersonalProfiles|CrewIdentityStorage|CrewProfileProtection|ProtectedFullName|ProtectedEmployeeNo|FullName|EmployeeNo|Nationality|CertificateNumber|CrewPersonalProfile)\b", RegexOptions.Compiled);

    private static string Source(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        return File.ReadAllText(Path.Combine(root!.FullName, "backend", "Poseidon.Api", file));
    }

    [Theory]
    [InlineData("WorkspaceEndpoints.cs")]
    [InlineData("WorkspaceLensEndpoints.cs")]
    [InlineData("MetricEndpoints.cs")]
    [InlineData("AuditReadyEndpoints.cs")]
    [InlineData("AuditHistoryEndpoints.cs")]
    [InlineData("CrewEligibilityEvaluator.cs")]
    public void Generic_search_export_metric_and_eligibility_sources_never_touch_protected_crew_fields(string file)
    {
        var hits = Protected.Matches(Source(file)).Select(m => m.Value).Distinct().ToArray();
        Assert.True(hits.Length == 0, $"{file} references protected crew members: {string.Join(", ", hits)}");
    }

    private static readonly string[] Pii = ["FullName", "EmployeeNo", "Name", "Nationality", "CertificateNumber", "Issuer", "Email", "Phone",
        "ContactPhone", "DateOfBirth", "BirthDate", "Address", "Notes", "Limitations", "Rationale", "PersonId"];

    public static TheoryData<Type> Contracts() => new([typeof(CrewEligibilityResult), typeof(CrewEligibilityReason), typeof(CrewRequirementGap),
        typeof(SafeManningEvaluation), typeof(ManningRoleCoverage), typeof(SafeManningView), typeof(SafeManningDecisionDto), typeof(CrewCertificateRegisterRow),
        typeof(CrewCertificateRegister), typeof(CrewCredentialAssertionDto), typeof(CrewCredentialVerificationView), typeof(CrewCompetenceRecordDto),
        typeof(CrewCompetenceSignOffDto), typeof(CrewCompetenceView), typeof(CrewCompetenceMatrix), typeof(CrewCompetenceRequirementDto), typeof(CrewAuthorityDto),
        typeof(CrewCommandOutcome), typeof(SafeManningDecisionPage)]);

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Crew_compliance_contracts_carry_no_protected_personal_fields(Type type)
    {
        var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
        Assert.Empty(names.Intersect(Pii, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_audited_crew_compliance_code_is_registered()
    {
        foreach (var action in new[] { "CredentialRead", "CredentialVerificationRead", "CredentialVerificationCommand", "CrewCompetenceRead", "CrewCompetenceCommand", "SafeManningRead", "SafeManningCommand" })
            Assert.Contains(action, SecurityEventCodes.Actions);
        foreach (var result in new[] { "Attempted", "Success", "permission_denied", "resource_not_found", "invalid_query", "csrf_invalid", "invalid_idempotency_key",
                     "invalid_request", "validation_failed", "idempotency_key_reused", "version_conflict", "service_unavailable", "human_authority_required",
                     "separation_of_duties", "credential_document_required", "competence_requirement_unavailable", "evaluation_stale",
                     "evaluation_window_invalid", "linked_resource_unavailable" })
            Assert.Contains(result, SecurityEventCodes.Results);
    }

    [Fact]
    public async Task Default_work_rest_source_is_unknown_so_no_evaluation_can_be_eligible()
    {
        var fact = await new UnavailableCrewWorkRestSource().ReadAsync(Guid.NewGuid(), Guid.NewGuid(), new(new(2026, 12, 1), new(2026, 12, 1)), TestContext.Current.CancellationToken);
        Assert.Equal(CrewWorkRestStates.Unknown, fact.State);
    }

    [Fact]
    public void Crew_compliance_sources_classify_serializable_failures_and_record_no_system_decision()
    {
        var endpoints = Source("CrewComplianceEndpoints.cs");
        Assert.Contains("PostgresFailure.", endpoints, StringComparison.Ordinal);
        // The decision row is written only from the authorized human command path.
        Assert.Single(Regex.Matches(endpoints, @"CrewSafeManningDecisions\.Add\("));
        Assert.DoesNotContain("CrewSafeManningDecisions.Add", Source("CrewEligibilityEvaluator.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("QualifiedEligible", Source("CrewEligibilityObservation.cs"), StringComparison.Ordinal);
    }
}
