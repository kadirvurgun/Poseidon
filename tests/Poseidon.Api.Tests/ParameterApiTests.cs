using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5401 / 54.01 CFG3. Actual host, real PostgreSQL, synthetic memberships only.
// Positive HTTP writes here are the critical-write audit observations of the three parameter commands.
public sealed partial class DirectoryWorkspaceTests
{
    private const string ParameterRole = "91c26eb5-cf16-4553-981d-1c275358c535";
    private static readonly string[] ConfigPermissions = ["config.read", "config.resolve", "config.draft", "config.approve", "config.reject"];
    private static readonly Dictionary<string, string> FreshnessSelectors = new() { ["sourceOwner"] = "60.01", ["sourceKind"] = "SYNTHETIC_AIS", ["purpose"] = "synthetic.release" };
    private static object PolicyRef(string owner = "60.01") => new { ownerDomain = owner, objectType = "SyntheticFreshnessPolicy",
        objectId = "10000000-0000-4000-8000-0000000000a1", revisionId = Guid.NewGuid().ToString("D"), contentHash = new string('c', 64) };

    private static void ParameterActor(WebApplicationFactory<Program> host, Guid actor, params string[] permissions)
    {
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var current = settings.Memberships[0];
        settings.Memberships[0] = current with
        {
            ActorId = actor,
            Permissions = [.. current.Permissions.Where(p => !p.StartsWith("config.", StringComparison.Ordinal)), .. permissions]
        };
    }

    private static string Applicability(Dictionary<string, string> selectors) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selectors))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<JsonElement> Effective(HttpClient client, string key, string scopeKind, Guid? scopeId = null, Dictionary<string, string>? selectors = null,
        string? extra = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var url = $"/api/v1/effective-parameters?key={key}&scopeKind={scopeKind}" + (scopeId is { } id ? $"&scopeId={id:D}" : "") +
            (selectors is { Count: > 0 } ? "&applicability=" + Applicability(selectors) : "") + extra;
        using var response = await client.GetAsync(url, Ct);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<string> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    private async Task AssertParameterCommandAudit(HttpResponseMessage response, string operation, string reason, Guid root, Guid actor)
    {
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        await using var db = Db();
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.CorrelationId == correlation && x.ResourceId == root).ToArrayAsync(Ct);
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal("WebCommand", row.Source); Assert.Equal(actor.ToString(), row.ActorId); Assert.Null(row.VesselId);
            Assert.Equal(seed.Tenants[0].Id, row.TenantId); Assert.Equal(ParameterRole, row.RoleContext);
            Assert.Equal("RecordedControlledCode", row.ReasonStatus);
            using var summary = JsonDocument.Parse(row.ChangeSummary!);
            string? Field(string name) => Assert.Single(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == name).GetProperty("after").GetString();
            Assert.Equal(operation, Field("CommandName")); Assert.Equal(reason, Field("TechnicalReasonCode")); Assert.Equal("Tenant", Field("ScopeKind"));
            Assert.DoesNotContain("SyntheticFreshnessPolicy", row.ChangeSummary); Assert.DoesNotContain("synthetic.release", row.ChangeSummary);
        });
    }

    [Fact] // P5401-AC-21..25
    [Trait("Requirement", "PLAN-1511-5401")]
    public async Task Parameter_policy_is_unconfigured_until_independently_approved_and_resolves_with_source_and_version()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var author = actorId; var approver = Guid.NewGuid();
        ParameterActor(host, author, ConfigPermissions);
        using (var definitions = await client.GetAsync("/api/v1/parameter-definitions", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, definitions.StatusCode);
            var body = await definitions.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(354, body.GetProperty("sourceDefinitionCount").GetInt32());
            Assert.Equal(ParameterRegistry.Definitions.Count, body.GetProperty("items").GetArrayLength());
            var freshness = Assert.Single(body.GetProperty("items").EnumerateArray(), x => x.GetProperty("key").GetString() == "ops.source.freshness_policy_ref");
            Assert.Equal(JsonValueKind.Null, freshness.GetProperty("approvedProductDefault").ValueKind);
            Assert.Equal("POLICY", freshness.GetProperty("valueClass").GetString());
        }
        var before = await Effective(client, "ops.source.freshness_policy_ref", "TENANT", selectors: FreshnessSelectors);
        Assert.Equal("UNCONFIGURED", before.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("value").ValueKind);
        Assert.False(before.GetProperty("positiveUseAllowed").GetBoolean());

        var proposal = new { expectedVersion = 0u, key = "ops.source.freshness_policy_ref", definitionVersion = 1, scopeKind = "TENANT",
            applicability = FreshnessSelectors, valueMode = "VALUE", value = PolicyRef(), reason = "Synthetic freshness policy proposal" };
        var key = Guid.NewGuid().ToString();
        using var proposed = await Send(client, "parameter-changes", proposal, key);
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        var command = await proposed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var root = command.GetProperty("changeId").GetGuid(); var revision = command.GetProperty("revisionId").GetGuid();
        Assert.Equal("IN_REVIEW", command.GetProperty("state").GetString());
        await AssertParameterCommandAudit(proposed, "ProposeParameterChange", "PREPARATION_COMMAND", root, author);
        using (var replay = await Send(client, "parameter-changes", proposal, key))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal(revision, (await replay.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("revisionId").GetGuid());
        }
        using (var reused = await Send(client, "parameter-changes", proposal with { reason = "Synthetic different payload reason" }, key))
        { Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode); Assert.Equal("idempotency_key_reused", await Code(reused)); }
        // QA F2 (paragraph:96): a reused key with an invalid payload is still a key conflict, not a validation answer.
        using (var reusedInvalid = await Send(client, "parameter-changes", proposal with { reason = "short" }, key))
        { Assert.Equal(HttpStatusCode.Conflict, reusedInvalid.StatusCode); Assert.Equal("idempotency_key_reused", await Code(reusedInvalid)); }
        // Pending proposal is never selectable.
        Assert.Equal("UNCONFIGURED", (await Effective(client, "ops.source.freshness_policy_ref", "TENANT", selectors: FreshnessSelectors)).GetProperty("status").GetString());

        var version = command.GetProperty("version").GetUInt32();
        using (var self = await Send(client, $"parameter-changes/{root}/commands/approve", new { expectedVersion = version, revisionId = revision, reason = "Synthetic self approval attempt" }))
        { Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode); Assert.Equal("separation_of_duties", await Code(self)); }

        ParameterActor(host, approver, ConfigPermissions);
        // QA F3: a decision reason with control characters is rejected like a proposal reason.
        using (var control = await Send(client, $"parameter-changes/{root}/commands/approve", new { expectedVersion = version, revisionId = revision, reason = "Synthetic \u001b[31m approval" }))
        { Assert.Equal(HttpStatusCode.BadRequest, control.StatusCode); Assert.Equal("invalid_request", await Code(control)); }
        using (var stale = await Send(client, $"parameter-changes/{root}/commands/approve", new { expectedVersion = version + 1, revisionId = revision, reason = "Synthetic stale approval attempt" }))
        { Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("version_conflict", await Code(stale)); }
        using var approved = await Send(client, $"parameter-changes/{root}/commands/approve", new { expectedVersion = version, revisionId = revision, reason = "Synthetic independent approval" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approval = await approved.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("APPROVED", approval.GetProperty("state").GetString());
        await AssertParameterCommandAudit(approved, "ApproveParameterChange", "HUMAN_REVIEW_WORKFLOW", root, approver);

        var resolved = await Effective(client, "ops.source.freshness_policy_ref", "TENANT", selectors: FreshnessSelectors);
        Assert.Equal("RESOLVED", resolved.GetProperty("status").GetString());
        Assert.Equal("60.01", resolved.GetProperty("value").GetProperty("ownerDomain").GetString());
        Assert.Equal("TENANT", resolved.GetProperty("sourceScope").GetString());
        Assert.Equal(revision, resolved.GetProperty("sourceRevisionId").GetGuid());
        Assert.Equal("OWNER_ADAPTER_NOT_REGISTERED", resolved.GetProperty("ownerVerification").GetString());
        Assert.False(resolved.GetProperty("positiveUseAllowed").GetBoolean());
        // A different exact selector is a different policy and stays unconfigured.
        Assert.Equal("UNCONFIGURED", (await Effective(client, "ops.source.freshness_policy_ref", "TENANT",
            selectors: new(FreshnessSelectors) { ["purpose"] = "synthetic.other" })).GetProperty("status").GetString());
        // HISTORICAL with knowledge before the approval does not see it.
        var past = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var historical = await Effective(client, "ops.source.freshness_policy_ref", "TENANT", selectors: FreshnessSelectors,
            extra: $"&mode=HISTORICAL&effectiveAt={past}&knownAt={past}");
        Assert.Equal("UNCONFIGURED", historical.GetProperty("status").GetString());

        // The approver now authors a replacement; the original author rejects it independently.
        using var second = await Send(client, "parameter-changes", proposal with { expectedVersion = approval.GetProperty("version").GetUInt32(), value = PolicyRef() });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var replacement = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        ParameterActor(host, author, ConfigPermissions);
        using var rejected = await Send(client, $"parameter-changes/{root}/commands/reject",
            new { expectedVersion = replacement.GetProperty("version").GetUInt32(), revisionId = replacement.GetProperty("revisionId").GetGuid(), reason = "Synthetic independent rejection" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("REJECTED", (await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("state").GetString());
        await AssertParameterCommandAudit(rejected, "RejectParameterChange", "HUMAN_REVIEW_WORKFLOW", root, author);
        Assert.Equal(revision, (await Effective(client, "ops.source.freshness_policy_ref", "TENANT", selectors: FreshnessSelectors)).GetProperty("sourceRevisionId").GetGuid());

        using var lineage = await client.GetAsync($"/api/v1/parameter-changes/{root}", Ct);
        Assert.Equal(HttpStatusCode.OK, lineage.StatusCode);
        var states = (await lineage.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("revisions").EnumerateArray().Select(x => x.GetProperty("state").GetString()).ToArray();
        Assert.Equal(new[] { "APPROVED", "REJECTED" }, states);
    }

    [Fact] // P5401-AC-26: most specific scope, explicit INHERIT, alias read and product default without acceptance.
    [Trait("Requirement", "PLAN-1511-5401")]
    public async Task Vessel_scope_overrides_company_only_by_value_and_inherits_only_explicitly()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var author = actorId; var approver = Guid.NewGuid();
        async Task<JsonElement> ProposeAndApprove(object body)
        {
            ParameterActor(host, author, ConfigPermissions);
            using var proposed = await Send(client, "parameter-changes", body); Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
            var command = await proposed.Content.ReadFromJsonAsync<JsonElement>(Ct);
            ParameterActor(host, approver, ConfigPermissions);
            using var approved = await Send(client, $"parameter-changes/{command.GetProperty("changeId").GetGuid()}/commands/approve",
                new { expectedVersion = command.GetProperty("version").GetUInt32(), revisionId = command.GetProperty("revisionId").GetGuid(), reason = "Synthetic independent approval" });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            return await approved.Content.ReadFromJsonAsync<JsonElement>(Ct);
        }
        const string key = "control.assurance.maxAgeSeconds";
        ParameterActor(host, author, ConfigPermissions);
        Assert.Equal("UNCONFIGURED", (await Effective(client, key, "VESSEL", SeedVessel.Id)).GetProperty("status").GetString());
        await ProposeAndApprove(new { expectedVersion = 0, key, definitionVersion = 1, scopeKind = "COMPANY", scopeId = SeedVessel.CompanyId, valueMode = "VALUE", value = 120, reason = "Synthetic company value" });
        // No implicit fallback from vessel to company.
        var implicitRead = await Effective(client, key, "VESSEL", SeedVessel.Id);
        Assert.Equal("UNCONFIGURED", implicitRead.GetProperty("status").GetString());
        var inherit = await ProposeAndApprove(new { expectedVersion = 0, key, definitionVersion = 1, scopeKind = "VESSEL", scopeId = SeedVessel.Id, valueMode = "INHERIT", reason = "Synthetic explicit inherit" });
        var inherited = await Effective(client, key, "VESSEL", SeedVessel.Id);
        Assert.Equal("RESOLVED", inherited.GetProperty("status").GetString()); Assert.Equal(120, inherited.GetProperty("value").GetInt32());
        Assert.Equal("COMPANY", inherited.GetProperty("sourceScope").GetString());
        await ProposeAndApprove(new { expectedVersion = inherit.GetProperty("version").GetUInt32(), key, definitionVersion = 1, scopeKind = "VESSEL", scopeId = SeedVessel.Id, valueMode = "VALUE", value = 30, reason = "Synthetic vessel override" });
        var overridden = await Effective(client, key, "VESSEL", SeedVessel.Id);
        Assert.Equal(30, overridden.GetProperty("value").GetInt32()); Assert.Equal("VESSEL", overridden.GetProperty("sourceScope").GetString());
        Assert.Equal(SeedVessel.Id, overridden.GetProperty("sourceScopeId").GetGuid());
        Assert.Equal(120, (await Effective(client, key, "COMPANY", SeedVessel.CompanyId)).GetProperty("value").GetInt32());
        // CFG3-T09: alias reads the canonical definition; the product default is not deployment-accepted.
        var alias = await Effective(client, "tug.history.export.max_rows", "TENANT");
        Assert.Equal("rpt.export.rows", alias.GetProperty("key").GetString()); Assert.Equal("rpt.export.rows", alias.GetProperty("aliasOf").GetString());
        Assert.Equal("UNCONFIGURED", alias.GetProperty("status").GetString());
        Assert.Contains("PRODUCT_DEFAULT_NOT_DEPLOYMENT_ACCEPTED", alias.GetProperty("reasonCodes").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("UNKNOWN", (await Effective(client, "eligibility.rule_version", "TENANT")).GetProperty("status").GetString());
    }

    [Fact] // P5401-AC-27..30
    [Trait("Requirement", "PLAN-1511-5401")]
    public async Task Parameter_commands_deny_without_authority_csrf_key_or_tenant_scope_and_reject_owner_fields_without_effects()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        ParameterActor(host, actorId);
        using (var denied = await client.GetAsync("/api/v1/parameter-definitions", Ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Effective(client, "rpt.export.rows", "TENANT", expected: HttpStatusCode.Forbidden);
        var valid = new { expectedVersion = 0, key = "regulatory.cache.max_age_seconds", definitionVersion = 1, scopeKind = "TENANT", valueMode = "VALUE", value = 30, reason = "Synthetic denied proposal" };
        using (var noRight = await Send(client, "parameter-changes", valid)) { Assert.Equal(HttpStatusCode.Forbidden, noRight.StatusCode); Assert.Equal("permission_denied", await Code(noRight)); }
        // Read/resolve rights never imply draft or decision rights.
        ParameterActor(host, actorId, "config.read", "config.resolve");
        using (var readOnly = await Send(client, "parameter-changes", valid)) Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        ParameterActor(host, actorId, ConfigPermissions);
        using (var anonymous = Client(host, false))
        using (var unauthenticated = await Send(anonymous, "parameter-changes", valid)) Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using (var noCsrf = Client(host))
        using (var csrf = await Send(noCsrf, "parameter-changes", valid)) { Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode); Assert.Equal("csrf_invalid", await Code(csrf)); }
        using (var noKey = await Send(client, "parameter-changes", valid, "")) { Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode); Assert.Equal("invalid_idempotency_key", await Code(noKey)); }
        using (var unknownField = await Send(client, "parameter-changes", new { expectedVersion = 0, key = "rpt.export.rows", definitionVersion = 1, scopeKind = "TENANT", valueMode = "VALUE", value = 10, reason = "Synthetic unknown field", tenantId = Guid.NewGuid() }))
            Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        foreach (var (key, code) in new[] { ("safety.deferral.max_count", "owner_field_not_writable"), ("common.list.default_limit", "invariant_not_writable"),
            ("tug.history.export.max_rows", "alias_read_only"), ("eligibility.rule_version", "family_not_writable") })
        {
            using var rejected = await Send(client, "parameter-changes", new { expectedVersion = 0, key, definitionVersion = 1, scopeKind = "TENANT", valueMode = "VALUE", value = 3, reason = "Synthetic owner field attempt" });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode); Assert.Equal(code, await Code(rejected));
        }
        using (var typed = await Send(client, "parameter-changes", new { valid.expectedVersion, valid.key, valid.definitionVersion, valid.scopeKind, valid.valueMode, value = "30x", valid.reason }))
        { Assert.Equal(HttpStatusCode.UnprocessableEntity, typed.StatusCode); Assert.Equal("type_mismatch", await Code(typed)); }
        using (var range = await Send(client, "parameter-changes", valid with { value = 61 }))
        { Assert.Equal(HttpStatusCode.UnprocessableEntity, range.StatusCode); Assert.Equal("out_of_range", await Code(range)); }
        using (var foreignOwner = await Send(client, "parameter-changes", new { expectedVersion = 0, key = "ops.source.freshness_policy_ref", definitionVersion = 1, scopeKind = "TENANT",
            applicability = FreshnessSelectors, valueMode = "VALUE", value = PolicyRef("59.04"), reason = "Synthetic foreign owner reference" }))
        { Assert.Equal(HttpStatusCode.UnprocessableEntity, foreignOwner.StatusCode); Assert.Equal("unresolved_reference", await Code(foreignOwner)); }
        // Tenant isolation: another tenant's scope identities and roots are not found.
        var foreignTenant = seed.Tenants[1].Id;
        var foreignVessel = seed.Vessels.First(v => v.TenantId == foreignTenant);
        using (var crossVessel = await Send(client, "parameter-changes", new { expectedVersion = 0, key = "mobile.offline.idleLockSeconds", definitionVersion = 1, scopeKind = "VESSEL", scopeId = foreignVessel.Id, valueMode = "VALUE", value = 300, reason = "Synthetic cross tenant vessel" }))
            Assert.Equal(HttpStatusCode.NotFound, crossVessel.StatusCode);
        using (var crossTenant = await Send(client, "parameter-changes", new { valid.expectedVersion, valid.key, valid.definitionVersion, valid.scopeKind, scopeId = foreignTenant, valid.valueMode, valid.value, valid.reason }))
            Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        await Effective(client, "control.assurance.maxAgeSeconds", "COMPANY", foreignVessel.CompanyId, expected: HttpStatusCode.NotFound);
        await Effective(client, "unknown.parameter", "TENANT", expected: HttpStatusCode.NotFound);
        await Effective(client, "rpt.export.rows", "TENANT", extra: "&tenantId=" + foreignTenant, expected: HttpStatusCode.BadRequest);
        Guid foreignRoot;
        await using (var other = Db(foreignTenant))
        {
            var definition = ParameterRegistry.Find("regulatory.cache.max_age_seconds")!;
            var (proposal, _) = ParameterValuePolicy.Propose(definition, 1, "TENANT", null, "VALUE", JsonDocument.Parse("20").RootElement, null, null, "Synthetic foreign tenant value", DateTimeOffset.UtcNow.AddSeconds(-1));
            var existing = await other.ParameterRoots.AsNoTracking().SingleOrDefaultAsync(x => x.Key == definition.Key, Ct);
            var written = await ParameterWriter.ProposeAsync(other, Guid.NewGuid(), "foreign-" + Guid.NewGuid().ToString("N"), existing?.Version ?? 0, definition, proposal!, foreignTenant,
                ParameterValuePolicy.Micro(DateTimeOffset.UtcNow), () => Task.FromResult(true), Ct);
            foreignRoot = written.RootId == Guid.Empty ? existing!.Id : written.RootId;
        }
        using (var hidden = await client.GetAsync($"/api/v1/parameter-changes/{foreignRoot}", Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var hiddenDecision = await Send(client, $"parameter-changes/{foreignRoot}/commands/approve", new { expectedVersion = 1, revisionId = Guid.NewGuid(), reason = "Synthetic cross tenant approval" }))
            Assert.Equal(HttpStatusCode.NotFound, hiddenDecision.StatusCode);
        await using (var db = Db())
        {
            Assert.False(await db.ParameterRoots.AnyAsync(x => x.Key == "regulatory.cache.max_age_seconds" || x.Key == "safety.deferral.max_count" || x.Key == "mobile.offline.idleLockSeconds", Ct));
            Assert.False(await db.ParameterCommandReceipts.AnyAsync(x => x.ActorId == actorId && x.Action == ParameterWriter.Propose && x.RootId == foreignRoot, Ct));
        }
    }
}
