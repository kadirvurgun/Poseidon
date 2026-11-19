using System.Text.RegularExpressions;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Persistence.Tests;

// PLAN-1511-5802: every ControlledDocument denial is written to the security event store before
// the HTTP problem is returned. A code missing from SecurityEventCodes.Results makes that append
// throw, so a 409 (e.g. active_use_open) turns into an audit-unavailable failure instead.
[Trait("Category", "Unit")]
[Trait("Requirement", "PLAN-1511-5802")]
public sealed partial class ControlledDocumentSecurityCodeTests
{
    private static readonly Guid Uploader = Guid.NewGuid(), Registrar = Guid.NewGuid(), Other = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void Every_policy_blocker_code_is_an_allowed_controlled_document_security_result()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? code) { if (code != null) codes.Add(code); }
        string?[] reasons = [null, "", "reason"];
        bool[] flags = [false, true];
        string[] intakes = [.. ControlledDocumentPolicy.IntakeStates, "X"];
        string[] publications = [.. ControlledDocumentPolicy.PublicationStates, "X"];
        string[] disposals = [.. ControlledDocumentPolicy.DisposalStates, "X"];
        DateOnly?[] dates = [null, Today.AddDays(-1), Today, Today.AddDays(1)];
        string?[] policies = [null, "bad policy", "POLICY-1"];
        Guid[] actors = [Uploader, Registrar, Other];
        Guid?[] requesters = [null, Other, Registrar];
        Add(ControlledDocumentPolicy.RegisterBlocker("NONE"));
        foreach (var disposal in disposals)
        {
            Add(ControlledDocumentPolicy.RegisterBlocker(disposal));
            foreach (var intake in intakes)
            {
                foreach (var clean in flags) Add(ControlledDocumentPolicy.ReleaseBlocker(intake, disposal, clean));
                foreach (var actor in actors)
                {
                    foreach (var publication in publications)
                    foreach (var current in new long?[] { null, 1, 3 })
                        Add(ControlledDocumentPolicy.PublishBlocker(intake, publication, disposal, actor, Uploader, Registrar, 2, current));
                    foreach (var method in new string?[] { null, "BAD", "MANUAL_SOURCE_COMPARISON" })
                    foreach (var result in new string?[] { null, "BAD", "VALID" })
                    foreach (var basis in reasons)
                        Add(ControlledDocumentPolicy.VerifyBlocker(intake, disposal, actor, Uploader, Registrar, method, result, basis));
                }
            }
            foreach (var policy in policies)
            foreach (var date in dates)
            {
                Add(ControlledDocumentPolicy.RetentionBlocker(disposal, policy, date, Today));
                foreach (var hold in flags)
                foreach (var active in flags)
                foreach (var reason in reasons)
                {
                    Add(ControlledDocumentPolicy.DisposalRequestBlocker(hold, policy, date, Today, disposal, reason, active));
                    foreach (var requester in requesters)
                        Add(ControlledDocumentPolicy.DisposalApproveBlocker(hold, policy, date, Today, disposal, requester, Registrar, reason, active));
                }
            }
        }
        foreach (var hold in flags)
        foreach (var reason in reasons)
        {
            Add(ControlledDocumentPolicy.PlaceHoldBlocker(hold, reason, reason));
            foreach (var placer in requesters) Add(ControlledDocumentPolicy.ReleaseHoldBlocker(hold, placer, Registrar, reason));
        }

        // The QA F2/F5 codes that regressed must be reachable through this matrix.
        Assert.Contains("active_use_open", codes);
        Assert.Contains("revision_older_than_effective", codes);
        Assert.All(codes, code => Assert.True(SecurityEventCodes.Allows("ControlledDocument", code), $"Unaudited ControlledDocument code: {code}"));
    }

    [Fact]
    public void Every_literal_denial_code_in_the_controlled_document_chain_and_endpoints_is_allowed()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Poseidon.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "backend/Poseidon.Persistence/ControlledDocumentChain.cs", "backend/Poseidon.Persistence/ControlledDocuments.cs",
                     "backend/Poseidon.Api/ControlledDocumentEndpoints.cs" })
        {
            var source = File.ReadAllText(Path.Combine(root.FullName, file));
            foreach (Match match in DenialLiteral().Matches(source)) codes.Add(match.Groups["code"].Value);
        }
        Assert.Contains("linked_resource_unavailable", codes);
        Assert.Contains("resource_not_found", codes);
        Assert.All(codes, code => Assert.True(SecurityEventCodes.Allows("ControlledDocument", code), $"Unaudited ControlledDocument code: {code}"));
    }

    [GeneratedRegex("""(?:ControlledDocumentException\(|Fail\(h, \d{3}, )"(?<code>[a-z_]+)"\)?""")]
    private static partial Regex DenialLiteral();
}
