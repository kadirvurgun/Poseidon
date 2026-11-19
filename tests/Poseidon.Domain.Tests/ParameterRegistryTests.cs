using System.Text.Json;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// PLAN-1511-5401 / 54.01 Rev4. Pure domain rules: registry/contract parity, typed validation,
// CFG3 resolver precedence and effective dating, maker-checker. No database or host.
[Trait("Requirement", "PLAN-1511-5401")]
public sealed class ParameterRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Company = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid Vessel = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static ParameterDefinition Def(string key) => ParameterRegistry.Find(key)!;
    private static JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonDocument Contract() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "parameters.v1.json")));

    [Fact] // P5401-AC-01, P5401-AC-12
    public void Code_registry_equals_the_machine_readable_contract_and_the_354_inventory_is_tracked_one_by_one()
    {
        using var contract = Contract();
        var root = contract.RootElement;
        Assert.Equal("poseidon.parameters.v1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(ParameterRegistry.SourceSha256, root.GetProperty("source").GetProperty("sha256").GetString());
        Assert.Equal(ParameterRegistry.SourcePath, root.GetProperty("source").GetProperty("path").GetString());
        Assert.Equal(ParameterRegistry.SourceDefinitionCount, root.GetProperty("source").GetProperty("definitionCount").GetInt32());
        var expected = root.GetProperty("definitions").EnumerateArray().ToArray();
        Assert.Equal(expected.Length, ParameterRegistry.Definitions.Count);
        foreach (var (json, code) in expected.Zip(ParameterRegistry.Definitions))
        {
            Assert.Equal(json.GetProperty("key").GetString(), code.Key);
            Assert.Equal(json.GetProperty("definitionVersion").GetInt32(), code.DefinitionVersion);
            Assert.Equal(json.GetProperty("ownerDomain").GetString(), code.OwnerDomain);
            Assert.Equal(json.GetProperty("classification").GetString(), code.Classification);
            Assert.Equal(json.GetProperty("valueClass").GetString(), code.ValueClass);
            Assert.Equal(json.GetProperty("risk").ValueKind == JsonValueKind.Null ? null : json.GetProperty("risk").GetString(), code.Risk);
            var type = json.GetProperty("type");
            Assert.Equal(type.GetProperty("type").GetString(), code.Type.Type);
            Assert.Equal(type.GetProperty("nullable").GetBoolean(), code.Type.Nullable);
            Assert.Equal(type.GetProperty("minimum").ValueKind == JsonValueKind.Null ? null : type.GetProperty("minimum").GetInt64(), code.Type.Minimum);
            Assert.Equal(type.GetProperty("maximum").ValueKind == JsonValueKind.Null ? null : type.GetProperty("maximum").GetInt64(), code.Type.Maximum);
            Assert.Equal(type.GetProperty("unit").ValueKind == JsonValueKind.Null ? null : type.GetProperty("unit").GetString(), code.Type.Unit);
            Assert.Equal(json.GetProperty("allowedScopes").EnumerateArray().Select(x => x.GetString()), code.AllowedScopes);
            Assert.Equal(json.GetProperty("requiredSelectors").EnumerateArray().Select(x => x.GetString()), code.RequiredSelectors);
            Assert.Equal(json.GetProperty("fallback").GetString(), code.Fallback);
            var productDefault = json.GetProperty("approvedProductDefault");
            Assert.Equal(productDefault.ValueKind == JsonValueKind.Null ? null : productDefault.GetString(), code.ApprovedProductDefault);
            Assert.Equal(json.GetProperty("productDefaultDeploymentAccepted").GetBoolean(), code.ProductDefaultDeploymentAccepted);
            Assert.False(code.ProductDefaultDeploymentAccepted); // No deployment acceptance record exists.
            var alias = json.GetProperty("aliasTarget");
            Assert.Equal(alias.ValueKind == JsonValueKind.Null ? null : alias.GetString(), code.AliasTarget);
            Assert.Equal(json.GetProperty("sensitivity").GetString(), code.Sensitivity);
            Assert.Equal(json.GetProperty("sourceLocator").GetString(), code.SourceLocator);
            Assert.Equal(json.GetProperty("runtimeStatus").GetString(), code.RuntimeStatus);
        }
        var inventory = root.GetProperty("inventory").EnumerateArray().ToArray();
        Assert.Equal(354, inventory.Length);
        Assert.Equal(354, inventory.Select(x => x.GetProperty("key").GetString()).Distinct(StringComparer.Ordinal).Count());
        var implemented = inventory.Where(x => x.GetProperty("status").GetString() == "IMPLEMENTED_FOUNDATION").Select(x => x.GetProperty("key").GetString()).ToHashSet();
        Assert.True(implemented.SetEquals(ParameterRegistry.Definitions.Select(x => x.Key)));
        Assert.All(inventory, x => Assert.Contains(x.GetProperty("status").GetString(), new[] { "IMPLEMENTED_FOUNDATION", "NOT_IMPLEMENTED" }));
        Assert.All(inventory, x => Assert.StartsWith("paragraph:", x.GetProperty("sourceLocator").GetString()));
    }

    [Fact] // P5401-AC-02: DEV-020 freshness policies have no default and resolve UNCONFIGURED.
    public void Freshness_policies_have_no_default_and_never_resolve_to_a_fabricated_value()
    {
        foreach (var key in new[] { "ops.source.freshness_policy_ref", "regulatory.source.freshness_policy_ref" })
        {
            var d = Def(key);
            Assert.Null(d.ApprovedProductDefault); Assert.Equal("BLOCK", d.Fallback); Assert.Equal("POLICY", d.ValueClass);
            var selector = ParameterValuePolicy.Selectors(d, d.RequiredSelectors.ToDictionary(x => x, _ => "synthetic"))!.Value;
            var result = ParameterResolver.Resolve(d, [new("TENANT", Tenant), new("GLOBAL", null)], selector.Hash, [], Now, Now);
            Assert.Equal("UNCONFIGURED", result.Status); Assert.Null(result.ValueJson); Assert.False(result.FallbackUsed);
            Assert.False(result.PositiveUseAllowed); Assert.Contains("FALLBACK_BLOCK", result.ReasonCodes);
        }
    }

    [Fact] // P5401-AC-03
    public void Product_defaults_without_deployment_acceptance_remain_unconfigured()
    {
        var d = Def("rpt.export.rows");
        var result = ParameterResolver.Resolve(d, [new("TENANT", Tenant), new("GLOBAL", null)], ParameterValuePolicy.Hash("{}"), [], Now, Now);
        Assert.Equal("UNCONFIGURED", result.Status); Assert.Null(result.ValueJson);
        Assert.Contains("PRODUCT_DEFAULT_NOT_DEPLOYMENT_ACCEPTED", result.ReasonCodes);
        var accepted = ParameterResolver.Resolve(d with { ProductDefaultDeploymentAccepted = true }, [new("TENANT", Tenant), new("GLOBAL", null)], ParameterValuePolicy.Hash("{}"), [], Now, Now);
        Assert.Equal("RESOLVED", accepted.Status); Assert.Equal("10000", accepted.ValueJson); Assert.True(accepted.FallbackUsed); Assert.Equal("GLOBAL", accepted.SourceScope);
    }

    private static ParameterCandidate C(string scope, Guid id, string? value, DateTimeOffset from, DateTimeOffset? to = null, DateTimeOffset? recorded = null, long sequence = 2, Guid? root = null) =>
        new(Guid.NewGuid(), root ?? Guid.NewGuid(), sequence, scope, id, ParameterValuePolicy.Hash("{}"), value == null ? "INHERIT" : "VALUE", value, from, to, recorded ?? from, new string('a', 64));
    private static readonly ParameterScopeLevel[] VesselChain = [new("VESSEL", Vessel), new("COMPANY", Company), new("TENANT", Tenant), new("GLOBAL", null)];

    [Fact] // P5401-AC-04: most specific allowed scope wins and reports its source.
    public void Most_specific_scope_wins_and_reports_source_scope_and_version()
    {
        var d = Def("control.assurance.maxAgeSeconds");
        var company = C("COMPANY", Company, "120", Now.AddDays(-2));
        var vessel = C("VESSEL", Vessel, "30", Now.AddDays(-1), sequence: 4);
        var result = ParameterResolver.Resolve(d, VesselChain, ParameterValuePolicy.Hash("{}"), [company, vessel], Now, Now);
        Assert.Equal("RESOLVED", result.Status); Assert.Equal("30", result.ValueJson); Assert.Equal("VESSEL", result.SourceScope);
        Assert.Equal(Vessel, result.SourceScopeId); Assert.Equal(4, result.SourceVersion); Assert.Equal(vessel.RevisionId, result.SourceRevisionId);
        Assert.False(result.PositiveUseAllowed); // Domain PARAMETER without registered owner adapter.
    }

    [Fact] // P5401-AC-05: parent scope only through explicit INHERIT, never implicitly.
    public void Parent_scope_is_consulted_only_through_explicit_inherit()
    {
        var d = Def("control.assurance.maxAgeSeconds");
        var company = C("COMPANY", Company, "120", Now.AddDays(-2));
        var implicitResult = ParameterResolver.Resolve(d, VesselChain, ParameterValuePolicy.Hash("{}"), [company], Now, Now);
        Assert.Equal("UNCONFIGURED", implicitResult.Status); Assert.Null(implicitResult.ValueJson);
        Assert.Contains("NO_APPROVED_VALUE_AT_SCOPE", implicitResult.ReasonCodes);
        var inherit = C("VESSEL", Vessel, null, Now.AddDays(-1));
        var explicitResult = ParameterResolver.Resolve(d, VesselChain, ParameterValuePolicy.Hash("{}"), [company, inherit], Now, Now);
        Assert.Equal("RESOLVED", explicitResult.Status); Assert.Equal("120", explicitResult.ValueJson); Assert.Equal("COMPANY", explicitResult.SourceScope);
        Assert.Contains("EXPLICIT_INHERIT", explicitResult.ReasonCodes);
        // A tenant-only definition evaluated for a vessel starts at its only allowed scope.
        var tenantOnly = Def("regulatory.cache.max_age_seconds");
        var tenant = ParameterResolver.Resolve(tenantOnly, VesselChain, ParameterValuePolicy.Hash("{}"), [C("TENANT", Tenant, "45", Now.AddDays(-1))], Now, Now);
        Assert.Equal("45", tenant.ValueJson); Assert.Equal("TENANT", tenant.SourceScope);
        // A definition with no allowed scope in the chain is blocked, not guessed.
        var blocked = ParameterResolver.Resolve(Def("mobile.offline.idleLockSeconds"), [new("TENANT", Tenant), new("GLOBAL", null)], ParameterValuePolicy.Hash("{}"), [], Now, Now);
        Assert.Equal("BLOCKED", blocked.Status); Assert.Contains("SCOPE_NOT_ALLOWED", blocked.ReasonCodes);
    }

    [Fact] // P5401-AC-06: effective dating, append-only cutover, expiry and knownAt cut.
    public void Effective_dating_supersession_expiry_and_known_at_are_deterministic()
    {
        var d = Def("regulatory.cache.max_age_seconds");
        var chain = new ParameterScopeLevel[] { new("TENANT", Tenant), new("GLOBAL", null) };
        var hash = ParameterValuePolicy.Hash("{}");
        var root = Guid.NewGuid();
        var first = C("TENANT", Tenant, "50", Now.AddDays(-10), root: root, sequence: 2);
        var second = C("TENANT", Tenant, "40", Now.AddDays(-5), to: Now.AddDays(-1), root: root, sequence: 4);
        Assert.Equal("50", ParameterResolver.Resolve(d, chain, hash, [first, second], Now.AddDays(-7), Now).ValueJson);
        Assert.Equal("40", ParameterResolver.Resolve(d, chain, hash, [first, second], Now.AddDays(-3), Now).ValueJson);
        // After the selected revision expires the earlier one does not silently resume.
        var expired = ParameterResolver.Resolve(d, chain, hash, [first, second], Now, Now);
        Assert.Equal("UNCONFIGURED", expired.Status); Assert.Contains("SELECTED_REVISION_EXPIRED", expired.ReasonCodes);
        // Historical knowledge cut: a selection recorded later is invisible to an earlier knownAt (CFG3-T02).
        var late = C("TENANT", Tenant, "30", Now.AddDays(-9), recorded: Now.AddDays(-2), root: root, sequence: 6);
        Assert.Equal("50", ParameterResolver.Resolve(d, chain, hash, [first, late], Now.AddDays(-8), Now.AddDays(-3)).ValueJson);
        Assert.Equal("30", ParameterResolver.Resolve(d, chain, hash, [first, late], Now.AddDays(-8), Now).ValueJson);
        // Future selection is not visible before its boundary.
        var future = C("TENANT", Tenant, "20", Now.AddDays(1), root: root, sequence: 8);
        Assert.Equal("50", ParameterResolver.Resolve(d, chain, hash, [first, future], Now, Now).ValueJson);
    }

    [Fact] // P5401-AC-07
    public void Equal_precedence_selections_are_ambiguous_not_last_writer_wins()
    {
        var d = Def("regulatory.cache.max_age_seconds");
        var result = ParameterResolver.Resolve(d, [new("TENANT", Tenant)], ParameterValuePolicy.Hash("{}"),
            [C("TENANT", Tenant, "10", Now.AddDays(-1)), C("TENANT", Tenant, "20", Now.AddDays(-1))], Now, Now);
        Assert.Equal("AMBIGUOUS", result.Status); Assert.Null(result.ValueJson);
    }

    [Fact] // P5401-AC-08: owner fields, invariants, aliases and family references.
    public void Non_writable_classifications_reject_generic_writes_and_resolve_without_guessing()
    {
        Assert.Equal("owner_field_not_writable", ParameterValuePolicy.WriteRejection(Def("safety.deferral.max_count")));
        Assert.Equal("invariant_not_writable", ParameterValuePolicy.WriteRejection(Def("common.list.default_limit")));
        Assert.Equal("alias_read_only", ParameterValuePolicy.WriteRejection(Def("tug.history.export.max_rows")));
        Assert.Equal("family_not_writable", ParameterValuePolicy.WriteRejection(Def("eligibility.rule_version")));
        Assert.Null(ParameterValuePolicy.WriteRejection(Def("ops.source.freshness_policy_ref")));
        var (proposal, errors) = ParameterValuePolicy.Propose(Def("safety.deferral.max_count"), 1, "TENANT", new Dictionary<string, string> { ["actionType"] = "x" },
            "VALUE", J("3"), null, null, "Synthetic owner field write attempt", Now);
        Assert.Null(proposal); Assert.Equal("owner_field_not_writable", Assert.Single(errors).Code);
        var chain = new ParameterScopeLevel[] { new("TENANT", Tenant), new("GLOBAL", null) };
        Assert.Equal("BLOCKED", ParameterResolver.Resolve(Def("safety.deferral.max_count"), chain, "x", [], Now, Now).Status);
        var family = ParameterResolver.Resolve(Def("eligibility.rule_version"), chain, "x", [], Now, Now);
        Assert.Equal("UNKNOWN", family.Status); Assert.Null(family.ValueJson);
        var invariant = ParameterResolver.Resolve(Def("common.list.default_limit"), chain, "x", [], Now, Now);
        Assert.Equal("RESOLVED", invariant.Status); Assert.Equal("25", invariant.ValueJson); Assert.True(invariant.PositiveUseAllowed);
    }

    [Theory] // P5401-AC-09: closed typed validation (CFG3-T01).
    [InlineData("regulatory.cache.max_age_seconds", "\"30x\"", "type_mismatch")]
    [InlineData("regulatory.cache.max_age_seconds", "30.0", "type_mismatch")]
    [InlineData("regulatory.cache.max_age_seconds", "true", "type_mismatch")]
    [InlineData("regulatory.cache.max_age_seconds", "0", "out_of_range")]
    [InlineData("regulatory.cache.max_age_seconds", "61", "out_of_range")]
    [InlineData("regulatory.cache.max_age_seconds", "null", "type_mismatch")]
    [InlineData("regulatory.cache.max_age_seconds", "60", null)]
    [InlineData("ops.source.freshness_policy_ref", "null", "type_mismatch")]
    [InlineData("ops.source.freshness_policy_ref", "{\"ownerDomain\":\"60.03\",\"objectType\":\"FreshnessPolicy\",\"objectId\":\"10000000-0000-4000-8000-0000000000aa\",\"revisionId\":\"10000000-0000-4000-8000-0000000000ab\",\"contentHash\":\"" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\"}", "unresolved_reference")]
    [InlineData("ops.source.freshness_policy_ref", "{\"ownerDomain\":\"60.01\",\"objectType\":\"FreshnessPolicy\",\"objectId\":\"10000000-0000-4000-8000-0000000000aa\",\"revisionId\":\"10000000-0000-4000-8000-0000000000ab\",\"contentHash\":\"AA\"}", "type_mismatch")]
    [InlineData("ops.source.freshness_policy_ref", "{\"ownerDomain\":\"60.01\",\"objectType\":\"FreshnessPolicy\",\"objectId\":\"10000000-0000-4000-8000-0000000000aa\",\"revisionId\":\"10000000-0000-4000-8000-0000000000ab\",\"contentHash\":\"" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\",\"extra\":\"x\"}", "type_mismatch")]
    [InlineData("ops.source.freshness_policy_ref", "{\"ownerDomain\":\"60.01\",\"objectType\":\"FreshnessPolicy\",\"objectId\":\"10000000-0000-4000-8000-0000000000aa\",\"revisionId\":\"10000000-0000-4000-8000-0000000000ab\",\"contentHash\":\"" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" + "\"}", null)]
    public void Values_are_validated_against_the_closed_type_and_range(string key, string value, string? expected)
    {
        var (json, error) = ParameterValuePolicy.Value(Def(key), J(value));
        Assert.Equal(expected, error);
        Assert.Equal(expected == null, json != null);
    }

    [Fact] // P5401-AC-10
    public void Proposals_require_exact_selectors_allowed_scope_reason_and_no_backdating()
    {
        var d = Def("ops.source.freshness_policy_ref");
        var selectors = new Dictionary<string, string> { ["sourceOwner"] = "60.01", ["sourceKind"] = "AIS", ["purpose"] = "ops.release" };
        var value = J("{\"ownerDomain\":\"60.01\",\"objectType\":\"FreshnessPolicy\",\"objectId\":\"10000000-0000-4000-8000-0000000000aa\",\"revisionId\":\"10000000-0000-4000-8000-0000000000ab\",\"contentHash\":\"" + new string('b', 64) + "\"}");
        var (ok, errors) = ParameterValuePolicy.Propose(d, 1, "TENANT", selectors, "VALUE", value, null, null, "Synthetic proposal reason", Now);
        Assert.Empty(errors); Assert.NotNull(ok); Assert.Equal(Now, ok!.EffectiveFrom);
        Assert.Equal("{\"purpose\":\"ops.release\",\"sourceKind\":\"AIS\",\"sourceOwner\":\"60.01\"}", ok.SelectorJson);
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", new Dictionary<string, string> { ["sourceOwner"] = "60.01" }, "VALUE", value, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "selector_invalid");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", new Dictionary<string, string>(selectors) { ["extra"] = "x" }, "VALUE", value, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "selector_invalid");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "VESSEL", selectors, "VALUE", value, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "scope_not_allowed");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "GLOBAL", selectors, "VALUE", value, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "scope_not_allowed");
        Assert.Contains(ParameterValuePolicy.Propose(d, 2, "TENANT", selectors, "VALUE", value, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "definition_version_mismatch");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", selectors, "VALUE", value, Now.AddMinutes(-1), null, "Synthetic proposal reason", Now).Errors, e => e.Code == "effective_from_in_past");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", selectors, "VALUE", value, Now.AddDays(1), Now.AddDays(1), "Synthetic proposal reason", Now).Errors, e => e.Code == "outside_effective_dates");
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", selectors, "VALUE", value, null, null, "short", Now).Errors, e => e.Code == "invalid_reason");
        // INHERIT for a tenant-only definition has no parent to climb to.
        Assert.Contains(ParameterValuePolicy.Propose(d, 1, "TENANT", selectors, "INHERIT", null, null, null, "Synthetic proposal reason", Now).Errors, e => e.Code == "scope_not_allowed");
        Assert.Empty(ParameterValuePolicy.Propose(Def("control.assurance.maxAgeSeconds"), 1, "VESSEL", null, "INHERIT", null, null, null, "Synthetic proposal reason", Now).Errors);
    }

    [Fact] // P5401-AC-11: maker-checker.
    public void Maker_checker_rejects_self_approval_unknown_actors_and_synthetic_approval()
    {
        var author = Guid.NewGuid(); var checker = Guid.NewGuid();
        Assert.Equal("separation_of_duties", ParameterGovernance.DecisionViolation(author, author, "DeclaredUnverified", true));
        Assert.Equal("separation_of_duties", ParameterGovernance.DecisionViolation(author, author, "DeclaredUnverified", false));
        Assert.Equal("separation_of_duties", ParameterGovernance.DecisionViolation(Guid.Empty, checker, "DeclaredUnverified", true));
        Assert.Equal("separation_of_duties", ParameterGovernance.DecisionViolation(author, Guid.Empty, "DeclaredUnverified", true));
        Assert.Null(ParameterGovernance.DecisionViolation(author, checker, "DeclaredUnverified", true));
        Assert.Equal("synthetic_not_approvable", ParameterGovernance.DecisionViolation(Guid.Empty, checker, "SyntheticUnverified", true));
        Assert.Null(ParameterGovernance.DecisionViolation(Guid.Empty, checker, "SyntheticUnverified", false));
        Assert.Equal(Now, ParameterGovernance.SelectedFrom(Now.AddDays(-1), Now));
        Assert.Equal(Now.AddDays(1), ParameterGovernance.SelectedFrom(Now.AddDays(1), Now));
    }

    [Fact] // QA F1 (54.01 paragraph:81-85): an approved TECHNICAL value is reported but not ACTIVE.
    public void Approved_technical_value_is_resolved_but_never_allowed_for_positive_use_before_activation()
    {
        var d = Def("mobile.offline.idleLockSeconds");
        Assert.Equal("NOT_REQUIRED", d.OwnerVerification);
        var result = ParameterResolver.Resolve(d, [new("VESSEL", Vessel)], ParameterValuePolicy.Hash("{}"), [C("VESSEL", Vessel, "900", Now.AddMinutes(-1))], Now, Now);
        Assert.Equal("RESOLVED", result.Status); Assert.Equal("900", result.ValueJson);
        Assert.False(result.PositiveUseAllowed);
        Assert.Contains("APPROVED_NOT_ACTIVATED", result.ReasonCodes);
        Assert.DoesNotContain("OWNER_VERIFICATION_REQUIRED_FOR_POSITIVE_USE", result.ReasonCodes);
    }

    [Theory] // QA F3: decision and proposal reasons share one rule; control characters are rejected.
    [InlineData("Synthetic independent approval", true)]
    [InlineData("Synthetic line one\nline two", true)]
    [InlineData("Synthetic \u0000 approval", false)]
    [InlineData("Synthetic \u001b[31m approval", false)]
    [InlineData("too short", false)]
    public void Reason_rule_rejects_control_characters_and_short_text(string reason, bool valid) =>
        Assert.Equal(valid, ParameterValuePolicy.ValidReason(reason));
}
