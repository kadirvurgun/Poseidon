using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-104 critical-write gate: checklist revisions/heads/receipts prove before/after through the
// unchanged CriticalWriteCoverageCapture.BeforeAfter rule; item content is withheld from audit.
// Every literal security result written by the new endpoints is a registered code.
[Trait("Requirement", "DEV-104")]
public sealed partial class PreSurveyAuditContractTests
{
    private static readonly Guid Tenant = Guid.NewGuid(), Vessel = Guid.NewGuid();
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];
    private const string PrivateFinding = "FICTIONAL-FINDING-TEXT-42";

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>(); options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command = "PreSurveyChecklistUpdate", string reason = "PREPARATION_COMMAND")
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
            EntityId = "synthetic", CorrelationId = "synthetic-presurvey-correlation", ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }
    private static PreSurveyChecklistRevision Revision(string transition, string readiness) => new()
    {
        TenantId = Tenant, VesselId = Vessel, ChecklistId = Guid.NewGuid(), CertificateId = Guid.NewGuid(), CertificateRevision = 1, CertificateVersion = 9, Revision = 4, RecordVersion = 12,
        TemplateCode = "STARTER-ANNUAL", ContentJson = $$"""{"finding":"{{PrivateFinding}}"}""", Hash = new string('A', 64), SourceVersion = "synthetic",
        Transition = transition, Readiness = readiness, ActorId = Guid.NewGuid(), SavedAt = DateTimeOffset.UnixEpoch
    };

    [Fact]
    public void Revision_records_closed_codes_and_withholds_item_content()
    {
        using var db = Db();
        var entry = db.Entry(Revision("VERIFY", "BLOCKED")); entry.State = EntityState.Added;
        var (row, fields) = Row(entry, "PreSurveyChecklistVerify", "HUMAN_REVIEW_WORKFLOW");
        Assert.Equal((Vessel, (long?)null, (long?)4), (row.VesselId!.Value, row.BeforeRevision, row.AfterRevision));
        Assert.Equal("VERIFY", fields["Transition"].GetProperty("after").GetString());
        Assert.Equal("BLOCKED", fields["Readiness"].GetProperty("after").GetString());
        Assert.Equal("ValuesWithheld", fields["Content"].GetProperty("evidence").GetString());
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain(PrivateFinding, row.ChangeSummary);
    }

    [Fact]
    public void Head_increment_and_receipt_are_bound_and_unknown_codes_are_rejected()
    {
        using var db = Db();
        var head = new PreSurveyChecklist { TenantId = Tenant, VesselId = Vessel, Id = Guid.NewGuid(), CertificateId = Guid.NewGuid(), Revision = 2, Version = 21 };
        var entry = db.Attach(head); head.Revision = 3; entry.DetectChanges();
        var (row, fields) = Row(entry);
        Assert.Equal((21u, 2L, 3L), (row.BeforeVersion!.Value, row.BeforeRevision!.Value, row.AfterRevision!.Value));
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        var receipt = db.Entry(new PreSurveyReceipt { TenantId = Tenant, VesselId = Vessel, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString("D"), ChecklistId = head.Id,
            Action = "PreSurveySave", Fingerprint = new string('C', 64), OutcomeJson = "{}", SavedAt = DateTimeOffset.UnixEpoch }); receipt.State = EntityState.Added;
        var (_, receiptFields) = Row(receipt);
        Assert.Equal("CommittedResource", receiptFields["Binding"].GetProperty("after").GetString());
        var ready = db.Entry(Revision("UPDATE", "READY")); ready.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(ready)).InnerException);
        var auto = db.Entry(Revision("AUTO_VERIFY", "BLOCKED")); auto.State = EntityState.Added;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Row(auto)).InnerException);
    }

    [Fact]
    public void Every_security_result_written_by_the_survey_endpoints_is_registered()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Poseidon.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var pairs = new List<(string Action, string Code)>();
        foreach (var name in new[] { "PreSurveyChecklistEndpoints.cs", "SurveyPlannerEndpoints.cs" })
        {
            var text = File.ReadAllText(Path.Combine(root.FullName, "backend", "Poseidon.Api", name));
            foreach (Match m in Helper().Matches(text))
                pairs.Add((m.Groups["helper"].Value switch { "ReadFail" => "PreSurveyRead", "CommandFail" => "PreSurveyCommand", _ => "SurveyPlannerRead" }, m.Groups["code"].Value));
            foreach (Match m in Direct().Matches(text)) pairs.Add((m.Groups["action"].Value, m.Groups["code"].Value));
        }
        // Codes produced through the domain policy and the conditional database branch.
        foreach (var code in new[] { "validation_failed", "checklist_items_mismatch", "na_not_authorized", "version_conflict", "service_unavailable", "Attempted", "Success" })
            pairs.Add(("PreSurveyCommand", code));
        Assert.True(pairs.Count > 30, $"Scanner matched only {pairs.Count} pairs.");
        Assert.Empty(pairs.Where(p => !SecurityEventCodes.Allows(p.Action, p.Code)).Select(p => p.Action + "/" + p.Code));
    }

    [GeneratedRegex(""""(?<helper>ReadFail|CommandFail|PlannerFail)\(h,\d+,"(?<code>\w+)"\)"""")]
    private static partial Regex Helper();
    [GeneratedRegex(""""(?:Fail|WriteAsync)\(h,"(?<action>\w+)",(?:\d+,)?"(?<code>\w+)"""")]
    private static partial Regex Direct();
}
