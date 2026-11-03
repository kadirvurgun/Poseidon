using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S1 / DEV-016 / T-S1-004: actual committed audit observations from the owned database.
// Nothing here supplies command authority, modifies product data, or accepts a phase.
internal sealed class CriticalWriteCoverageCapture
{
    private const string Prefix = "POSEIDON_CRITICAL_WRITE_COVERAGE_";
    private const int RowLimit = 100000;
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private readonly string instance = Guid.NewGuid().ToString("N");
    private readonly Binding? binding = Binding.Read();
    private readonly ConcurrentDictionary<string, RequestObservation> requests = new(StringComparer.Ordinal);
    private static readonly Regex Identifier = new(@"\A[A-Z][A-Za-z0-9]{0,95}\z", RegexOptions.CultureInvariant);

    internal IStartupFilter StartupFilter() => new RequestFilter(this);

    private void Observe(HttpContext context)
    {
        if (binding == null) return;
        var command = S1SecurityContract.Command(context);
        if (command != null) requests.TryAdd(context.TraceIdentifier, new(command.EndpointName, context.Response.StatusCode is >= 200 and < 300));
    }

    private sealed class RequestFilter(CriticalWriteCoverageCapture capture) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, invoke) => {
                try { await invoke(context); }
                finally { capture.Observe(context); }
            });
            next(app);
        };
    }

    internal async Task WriteAsync(OwnedTestDatabase database, IReadOnlyList<Guid> tenants)
    {
        if (binding == null) return;
        var failures = new HashSet<string>(StringComparer.Ordinal);
        var groups = new Dictionary<string, Observation>(StringComparer.Ordinal);
        List<AuditEvent> rows = [];
        var entityTypes = new HashSet<string>(StringComparer.Ordinal);
        var unavailable = false;
        var excludedFixtureRows = 0;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (tenants.Count is < 1 or > 100 || tenants.Any(x => x == Guid.Empty) || tenants.Distinct().Count() != tenants.Count)
                throw new InvalidOperationException("coverage-owned-tenant-set-unavailable");
            foreach (var tenant in tenants)
            {
                // The fixture owns this database and supplies its synthetic tenant IDs. No
                // administrative import context, unfiltered query or external connection override.
                await using var db = new PoseidonDbContext(SeedImporter.Options(database.GetConnectionString()), tenant,
                    new ActorContext("coverage-observer", "System", "coverage-observer"));
                entityTypes.UnionWith(db.Model.GetEntityTypes().Select(x => x.ClrType.Name).Where(name => Identifier.IsMatch(name)));
                var current = await db.AuditEvents.AsNoTracking().Where(x => x.Source == "WebCommand")
                    .Take(RowLimit + 1).ToArrayAsync(deadline.Token);
                rows.AddRange(current);
                if (rows.Count > RowLimit) { failures.Add("AUDIT_ROW_LIMIT_EXCEEDED"); break; }
            }
        }
        catch
        {
            // A deliberate unavailable-table/connection test must not become an empty pass.
            unavailable = true; rows.Clear(); failures.Add("AUDIT_QUERY_UNAVAILABLE");
        }
        if (!unavailable)
        {
            var committedVersions = rows.Where(x => x.AfterVersion > 0 && requests.ContainsKey(x.CorrelationId))
                .Select(x => (x.CorrelationId, x.TenantId, x.ResourceId)).ToHashSet();
            var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var operation in S1SecurityContract.Operations.Where(x => x.Kind == "CriticalWrite"))
            {
                if (operation.Variants.Length == 0) reasons.Add(operation.OperationId, operation.ReasonCode!);
                else foreach (var variant in operation.Variants) reasons.Add(variant.OperationId, operation.ReasonCode!);
            }
            foreach (var row in rows)
            {
                // Trusted direct-DB fixtures exercise audit behavior, but their command labels
                // cannot close an HTTP operation gap. Only this host's actual requests qualify.
                if (!requests.TryGetValue(row.CorrelationId, out var request)) { excludedFixtureRows++; continue; }
                var violations = Validate(row, committedVersions, tenants, reasons, entityTypes, request.OperationId, out var operation);
                failures.UnionWith(violations);
                if (operation == null || !reasons.ContainsKey(operation)) continue;
                if (!groups.TryGetValue(operation, out var observed)) groups[operation] = observed = new();
                observed.Rows++;
                if (violations.Count == 0) observed.Valid++;
                if (violations.Count == 0 && request.SuccessStatus) observed.ValidSuccessStatusRows++;
                if (request.SuccessStatus) observed.SuccessStatusRows++; else observed.NonSuccessStatusRows++;
                if (entityTypes.Contains(row.EntityType)) observed.Entities.Add(row.EntityType);
            }
        }
        var evidence = new
        {
            schemaVersion = "poseidon.critical-write-observation.v1",
            runId = binding.RunId, contractSha256 = binding.ContractSha256,
            target = new { commit = binding.Commit, tree = binding.Tree }, scope = binding.Scope,
            instanceId = instance, fixture = "DirectoryWorkspaceTests",
            testMethod = StaticTestMethod(),
            status = unavailable ? "UNAVAILABLE" : "OBSERVED",
            rowCount = unavailable ? (int?)null : rows.Count,
            excludedDirectFixtureRows = unavailable ? (int?)null : excludedFixtureRows,
            observations = groups.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new {
                operationId = x.Key, observedRows = x.Value.Rows, validRows = x.Value.Valid,
                successfulHttpRows = x.Value.SuccessStatusRows, nonSuccessHttpRows = x.Value.NonSuccessStatusRows,
                validSuccessfulHttpRows = x.Value.ValidSuccessStatusRows,
                entityTypes = x.Value.Entities.Order(StringComparer.Ordinal).ToArray()
            }).ToArray(),
            violations = failures.Order(StringComparer.Ordinal).ToArray(),
            unavailableCause = unavailable ? "OWNED_AUDIT_QUERY_UNAVAILABLE" : null
        };
        binding.Write(instance, JsonSerializer.SerializeToUtf8Bytes(evidence));
    }

    private static string? StaticTestMethod()
    {
        // Read the framework's actual method metadata, never a display name/theory arguments.
        var name = (TestContext.Current.TestCase as Xunit.v3.ICoreTestCase)?.TestMethodName;
        return name != null && Regex.IsMatch(name, @"\A[A-Za-z_][A-Za-z0-9_]{0,199}\z", RegexOptions.CultureInvariant) ? name : null;
    }

    private HashSet<string> Validate(AuditEvent row, IReadOnlySet<(string Correlation, Guid Tenant, Guid? Resource)> committedVersions, IReadOnlyList<Guid> tenants,
        IReadOnlyDictionary<string, string> reasons, IReadOnlySet<string> entityTypes, string actualOperation, out string? operation)
    {
        var failures = new HashSet<string>(StringComparer.Ordinal); operation = null;
        if (!Guid.TryParseExact(row.ActorId, "D", out var actor) || actor == Guid.Empty || row.ActorType != "User") failures.Add("AUDIT_ACTOR_INVALID");
        var roles = row.RoleContext?.Split(',');
        if (roles == null || roles.Length is < 1 or > 100 || roles.Distinct(StringComparer.Ordinal).Count() != roles.Length ||
            roles.Any(x => !Guid.TryParseExact(x, "D", out var role) || role == Guid.Empty)) failures.Add("AUDIT_ROLE_INVALID");
        if (!tenants.Contains(row.TenantId) || row.TenantId == Guid.Empty) failures.Add("AUDIT_TENANT_INVALID");
        if (string.IsNullOrWhiteSpace(row.CorrelationId) || row.CorrelationId.Length > 128 || row.CorrelationId.Any(c => c < 33 || c > 126)) failures.Add("AUDIT_CORRELATION_INVALID");
        if (row.OccurredAt.Offset != TimeSpan.Zero || row.OccurredAt < started.AddSeconds(-2) || row.OccurredAt > DateTimeOffset.UtcNow.AddSeconds(2)) failures.Add("AUDIT_TIMESTAMP_INVALID");
        if (row.ResourceId is null || row.ResourceId == Guid.Empty) failures.Add("AUDIT_RESOURCE_INVALID");
        if (!entityTypes.Contains(row.EntityType)) failures.Add("AUDIT_ENTITY_INVALID");
        if (row.Action is not ("Added" or "Modified" or "Deleted")) failures.Add("AUDIT_ACTION_INVALID");
        if (row.ReasonStatus is not ("RecordedControlledCode" or "RecordedProtected")) failures.Add("AUDIT_REASON_INVALID");
        try
        {
            if (row.ChangeSummary is not { Length: > 0 and <= 2048 }) throw new JsonException();
            using var summary = JsonDocument.Parse(row.ChangeSummary, new() { MaxDepth = 4 });
            if (summary.RootElement.ValueKind != JsonValueKind.Array || summary.RootElement.GetArrayLength() is < 1 or > 20) throw new JsonException();
            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var entry in summary.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) throw new JsonException();
                var properties = entry.EnumerateObject().ToArray();
                if (properties.Length != 4 || properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != 4 ||
                    properties.Any(x => x.Name is not ("field" or "before" or "after" or "evidence"))) throw new JsonException();
                var field = entry.GetProperty("field").GetString();
                if (field == null || !fields.TryAdd(field, entry) || entry.GetProperty("evidence").ValueKind != JsonValueKind.String ||
                    entry.GetProperty("before").ValueKind is not (JsonValueKind.String or JsonValueKind.Null) ||
                    entry.GetProperty("after").ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException();
                if (entry.GetProperty("evidence").GetString() is not ("Allowlisted" or "ValuesWithheld" or "RevisionReference")) throw new JsonException();
                if ((entry.GetProperty("evidence").GetString() is "ValuesWithheld" or "RevisionReference") &&
                    (entry.GetProperty("before").ValueKind != JsonValueKind.Null || entry.GetProperty("after").ValueKind != JsonValueKind.Null)) throw new JsonException();
            }
            string? Context(string field)
            {
                if (!fields.TryGetValue(field, out var value) || value.GetProperty("evidence").GetString() != "Allowlisted" ||
                    value.GetProperty("before").ValueKind != JsonValueKind.Null || value.GetProperty("after").ValueKind != JsonValueKind.String) return null;
                return value.GetProperty("after").GetString();
            }
            operation = Context("CommandName");
            if (operation == null || !reasons.TryGetValue(operation, out var reason)) failures.Add("AUDIT_COMMAND_INVALID");
            else
            {
                if (Context("TechnicalReasonCode") != reason) failures.Add("AUDIT_REASON_INVALID");
                if (actualOperation != operation) failures.Add("HTTP_COMMAND_UNOBSERVED");
            }
            var scope = Context("ScopeKind");
            if (scope == "Vessel" ? row.VesselId is null || row.VesselId == Guid.Empty : scope is not ("Tenant" or "TenantPerson") || row.VesselId != null)
                failures.Add("AUDIT_SCOPE_INVALID");
            if (!BeforeAfter(row, committedVersions, fields)) failures.Add("AUDIT_BEFORE_AFTER_UNPROVEN");
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        { failures.Add("AUDIT_SUMMARY_INVALID"); }
        if (operation == null || !reasons.ContainsKey(operation)) failures.Add("AUDIT_COMMAND_INVALID");
        return failures;
    }

    internal static bool BeforeAfter(AuditEvent row, IReadOnlySet<(string Correlation, Guid Tenant, Guid? Resource)> committedVersions, IReadOnlyDictionary<string, JsonElement> fields)
    {
        var business = fields.Where(x => x.Key is not ("CommandName" or "TechnicalReasonCode" or "ScopeKind")).Select(x => x.Value).ToArray();
        if (business.Length == 0) return false; // Provenance cannot stand in for a business summary.
        if (row.Action == "Added" && (row.BeforeVersion != null || row.BeforeRevision != null)) return false;
        if (row.BeforeVersion == 0 || row.AfterVersion == 0 || row.BeforeRevision < 0 || row.AfterRevision < 0) return false;
        var receiptVersion = committedVersions.Contains((row.CorrelationId, row.TenantId, row.ResourceId));
        var revision = row.AfterRevision > 0 && (row.Action == "Added" || row.BeforeRevision is >= 0 && row.AfterRevision >= row.BeforeRevision);
        bool Closed(string field, string? value) => field switch {
            "State" => value is "Draft" or "Uploaded" or "MappingDraft" or "Previewed" or "Committed" or "Pending" or
                "Receiving" or "CleanTransport" or "Abandoned" or "SyntheticUnverified" or "Reviewed" or "Adopted" or "Withdrawn" or
                "DRAFT" or "IN_REVIEW" or "ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "IN_FORCE" or "SUPERSEDED",
            "Lifecycle" => value is "DRAFT" or "SUBMITTED" or "COMPLETED" or "Enrolled" or "KeyEnrolled" or "Renewed" or "Revoked" or "KeyLost" or "CleanupAllowed",
            "Binding" => value is "ServerOwnedContext" or "OwnerReference" or "ProtectedStorage" or "LegacyStorage" or
                "TenantPersonRecord" or "PositionReference" or "JobReference" or "CommittedResource",
            "Outcome" => value is "EvaluationRecorded" or "PreviewRecorded" or "valid" or "rejected" or "Committed" or "MaterialConflict",
            _ => false
        };
        var state = business.Any(x =>
            x.GetProperty("evidence").GetString() == "Allowlisted" && x.GetProperty("after").ValueKind == JsonValueKind.String &&
            Closed(x.GetProperty("field").GetString()!, x.GetProperty("after").GetString()) &&
            (row.Action == "Added" || x.GetProperty("before").ValueKind == JsonValueKind.String &&
                Closed(x.GetProperty("field").GetString()!, x.GetProperty("before").GetString())));
        var before = row.BeforeVersion > 0 || row.BeforeRevision is >= 0 || state;
        if (row.Action == "Modified" && !before) return false;
        return receiptVersion || revision || state;
    }

    private sealed class Observation
    {
        public int Rows { get; set; }
        public int Valid { get; set; }
        public int SuccessStatusRows { get; set; }
        public int NonSuccessStatusRows { get; set; }
        public int ValidSuccessStatusRows { get; set; }
        public HashSet<string> Entities { get; } = new(StringComparer.Ordinal);
    }

    private sealed record RequestObservation(string OperationId, bool SuccessStatus);

    private sealed record Binding(string Directory, string ContractSha256, string RunId, string Commit, string Tree, string Scope)
    {
        public static Binding? Read()
        {
            var directory = Environment.GetEnvironmentVariable(Prefix + "DIRECTORY");
            if (directory == null) return null;
            var contract = Environment.GetEnvironmentVariable(Prefix + "CONTRACT");
            var digest = Environment.GetEnvironmentVariable(Prefix + "CONTRACT_SHA256");
            var run = Environment.GetEnvironmentVariable(Prefix + "RUN_ID");
            var commit = Environment.GetEnvironmentVariable(Prefix + "TARGET_COMMIT");
            var tree = Environment.GetEnvironmentVariable(Prefix + "TARGET_TREE");
            var scope = Environment.GetEnvironmentVariable(Prefix + "SCOPE");
            if (!Path.IsPathFullyQualified(directory) || contract == null || !Path.IsPathFullyQualified(contract) ||
                digest == null || !Regex.IsMatch(digest, @"\A[0-9a-f]{64}\z") || run == null || !Regex.IsMatch(run, @"\A[0-9a-f]{32}\z") ||
                commit == null || !Regex.IsMatch(commit, @"\A[0-9a-f]{40}\z") || tree == null || !Regex.IsMatch(tree, @"\A[0-9a-f]{40}\z") || scope is not ("integration" or "all"))
                throw new InvalidOperationException("critical-write-coverage-binding-invalid");
            SafeDirectory(directory); SafeDirectory(Path.GetDirectoryName(contract)!);
            if ((File.GetAttributes(contract) & FileAttributes.ReparsePoint) != 0 || new FileInfo(contract).Length is < 1 or > 262144 ||
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(contract))) != digest)
                throw new InvalidOperationException("critical-write-coverage-contract-invalid");
            using var embedded = typeof(S1SecurityContract).Assembly.GetManifestResourceStream("Poseidon.S1SecurityScope");
            if (embedded == null || Convert.ToHexStringLower(SHA256.HashData(embedded)) != digest)
                throw new InvalidOperationException("critical-write-coverage-built-contract-mismatch");
            return new(directory, digest, run, commit, tree, scope);
        }

        private static void SafeDirectory(string path)
        {
            for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
                if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("critical-write-coverage-directory-invalid");
        }

        public void Write(string instance, byte[] bytes)
        {
            if (bytes.Length is < 1 or > 262144) throw new InvalidOperationException("critical-write-coverage-report-invalid");
            SafeDirectory(Directory);
            var temporary = Path.Combine(Directory, instance + ".tmp"); var destination = Path.Combine(Directory, instance + ".json");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes); stream.Flush(true); }
                SafeDirectory(Directory); File.Move(temporary, destination, overwrite: false);
            }
            catch { throw new InvalidOperationException("critical-write-coverage-write-unavailable"); }
        }
    }
}

// Pure guard unit fixtures, not persisted audits or observations of actual HTTP coverage.
public sealed class CriticalWriteBeforeAfterGuardTests
{
    private static AuditEvent Modified() => new()
    {
        TenantId = Guid.NewGuid(), ResourceId = Guid.NewGuid(), ActorId = "synthetic-unit-guard",
        ActorType = "User", EntityType = nameof(Company), EntityId = "synthetic-unit-resource",
        Action = "Modified", CorrelationId = "synthetic-unit-guard"
    };
    private static IReadOnlySet<(string Correlation, Guid Tenant, Guid? Resource)> Committed(AuditEvent row) =>
        new HashSet<(string, Guid, Guid?)> { (row.CorrelationId, row.TenantId, row.ResourceId) };
    private static IReadOnlyDictionary<string, JsonElement> Field(string field, string? before, string? after, string evidence = "Allowlisted") =>
        new Dictionary<string, JsonElement> { [field] = JsonSerializer.SerializeToElement(new { field, before, after, evidence }) };

    [Fact]
    public void Receipt_after_xmin_cannot_replace_missing_original_before_evidence_on_modified_rows()
    {
        var row = Modified();
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, Committed(row), Field("Name", null, null, "ValuesWithheld")));
    }

    [Theory]
    [InlineData(7, null, true)]
    [InlineData(null, 0L, true)]
    [InlineData(null, 2L, true)]
    [InlineData(null, null, false)]
    [InlineData(0, null, false)]
    public void Actual_original_xmin_or_honest_revision_is_required_separately_from_committed_after_xmin(int? beforeVersion, long? beforeRevision, bool expected)
    {
        var row = Modified(); row.BeforeVersion = beforeVersion is int version ? (uint)version : null; row.BeforeRevision = beforeRevision;
        Assert.Equal(expected, CriticalWriteCoverageCapture.BeforeAfter(row, Committed(row), Field("Name", null, null, "ValuesWithheld")));
    }

    [Fact]
    public void Before_xmin_without_after_lineage_or_effect_is_insufficient()
    {
        var row = Modified(); row.BeforeVersion = 7;
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), Field("Name", null, null, "ValuesWithheld")));
    }

    [Fact]
    public void Allowed_closed_state_pair_can_prove_both_sides_without_exposing_private_values()
    {
        var row = Modified();
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), Field("State", "Pending", "Receiving")));
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), Field("State", null, "Receiving")));
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), Field("Binding", "arbitrary-label", "another-label")));
    }

    [Fact]
    public void Added_rows_retain_honest_null_before_and_do_not_borrow_a_fabricated_before_version()
    {
        var row = Modified(); row.Action = "Added";
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, Committed(row), Field("Name", null, null, "ValuesWithheld")));
        row.BeforeVersion = 7;
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, Committed(row), Field("Name", null, null, "ValuesWithheld")));
    }

    [Fact]
    public void First_revision_edit_preserves_real_zero_before_revision()
    {
        var row = Modified(); row.BeforeRevision = 0; row.AfterRevision = 1;
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, new HashSet<(string, Guid, Guid?)>(), Field("ContentJson", null, null, "RevisionReference")));
    }

    [Fact]
    public void Receipt_from_another_correlation_or_scope_cannot_supply_after_xmin()
    {
        var row = Modified(); row.BeforeVersion = 7;
        var other = Modified();
        Assert.False(CriticalWriteCoverageCapture.BeforeAfter(row, Committed(other), Field("Name", null, null, "ValuesWithheld")));
    }
}
