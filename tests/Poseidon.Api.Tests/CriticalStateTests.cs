using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-024 / T-S2-005: DEV024-AC-01..08 through the actual API host and PostgreSQL.
// Partial of DirectoryWorkspaceTests (shared fixture): select with --filter-trait "Requirement=DEV-024", not --filter-class.
public sealed partial class DirectoryWorkspaceTests
{
    private static string CriticalPath(Guid id, string? query = null) => $"/api/v1/vessels/{id}/critical-states{query}";
    private async Task<CriticalStatesDto> CriticalStates(HttpClient client, Guid? id = null, string? query = null)
    {
        using var response = await client.GetAsync(CriticalPath(id ?? SeedVessel.Id, query), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is { NoStore: true, Private: true });
        return (await response.Content.ReadFromJsonAsync<CriticalStatesDto>(Ct))!;
    }
    private static readonly string[] PositiveStates = ["READY", "VERIFIED", "COMPLIANT", "FRESH", "PASS"];

    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Authorized_reader_gets_source_revision_reasons_freshness_and_typed_blockers_never_ready()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var saved = await SaveProfile(client, before, before.Content with { Flag = new("Unknown"), Gt = new("Known", "100.5") });
        var dto = await CriticalStates(client);
        Assert.Equal((SeedVessel.Id, "VISIBLE_ONLY"), (dto.VesselId, dto.Scope));
        // No applicability review authority: the component is absent, not redacted or counted.
        Assert.Equal([CriticalStatePolicy.Onboarding, CriticalStatePolicy.ProfileFreshness], dto.States.Select(s => s.Key));
        var onboarding = dto.States[0];
        Assert.Equal(("BLOCKED", "FULL", "NOT_VERIFIED"), (onboarding.State, onboarding.Disclosure, onboarding.Verification));
        Assert.Equal(new CriticalStateSource("RegulatoryDraft", SeedVessel.Id, saved.Revision, saved.SavedAt, saved.Provenance), onboarding.Source);
        Assert.Contains("REQUIRED_DATA_UNKNOWN", onboarding.ReasonCodes!); Assert.Contains("VERIFICATION_NOT_ESTABLISHED", onboarding.ReasonCodes!);
        Assert.Contains(new CriticalStateBlocker("profile.flag", "REQUIRED_DATA_UNKNOWN", "POLICY_OR_EVIDENCE"), onboarding.Blockers!);
        Assert.Contains(new CriticalStateBlocker("profile.imo", "REQUIRED_DATA_MISSING", "REQUIRED_DECLARATION"), onboarding.Blockers!);
        Assert.Equal(("UNKNOWN", "FIELDS_UNKNOWN"), (onboarding.Freshness!.State, onboarding.Freshness.ReasonCode));
        Assert.Equal($"/app/fleet/{SeedVessel.Id:D}/onboarding", onboarding.Href);
        var freshness = dto.States[1];
        Assert.Equal(("UNKNOWN", "FULL"), (freshness.State, freshness.Disclosure));
        Assert.Equal(saved.Revision, freshness.Source!.Revision);
        Assert.Contains(new CriticalStateBlocker("profile.gt", "SOURCE_NOT_DECLARED", "SOURCE_DECLARATION"), freshness.Blockers!);
        Assert.Contains(new CriticalStateBlocker("profile.flag", "VALUE_UNKNOWN", "POLICY_OR_EVIDENCE"), freshness.Blockers!);
        Assert.Equal(VesselProfileFieldPolicy.Fields.Count, freshness.Blockers!.Length);
        // Same server computation as the DEV-020 owner endpoint.
        var fields = await ProfileFields(client);
        Assert.Equal((fields.Summary.State, fields.Summary.ReasonCode), (freshness.Freshness!.State, freshness.Freshness.ReasonCode));
        Assert.All(dto.States, s => Assert.DoesNotContain(s.State, PositiveStates));
        var single = await CriticalStates(client, query: "?key=vessel.profile.freshness");
        Assert.Equal(CriticalStatePolicy.ProfileFreshness, Assert.Single(single.States).Key);
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "CriticalStateRead" && e.Result == "Success" && e.ResourceId == SeedVessel.Id);
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Query_is_closed_and_read_writes_no_business_audit_or_receipt()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        long writes, receipts;
        await using (var db = Db()) { writes = await db.AuditEvents.LongCountAsync(Ct); receipts = await db.CommandReceipts.LongCountAsync(Ct); }
        foreach (var query in new[] { "?key=vessel.ready", "?key=vessel.onboarding&key=vessel.onboarding", "?vesselId=" + SeedVessel.Id, "?key=" })
        {
            using var invalid = await client.GetAsync(CriticalPath(SeedVessel.Id, query), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        await CriticalStates(client);
        await using var verify = Db();
        Assert.Equal(writes, await verify.AuditEvents.LongCountAsync(Ct)); Assert.Equal(receipts, await verify.CommandReceipts.LongCountAsync(Ct));
        Assert.Contains(await verify.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "CriticalStateRead" && e.Result == "Invalid");
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Applicability_explanation_is_unavailable_then_qualified_then_stale_after_profile_change()
    {
        await using var host = Host(); using var client = Client(host); GraphPermissionsForTest(host.Services); await Token(client);
        var none = (await CriticalStates(client, query: "?key=vessel.compliance.applicability")).States.Single();
        Assert.Equal(("UNAVAILABLE", "FULL"), (none.State, none.Disclosure)); Assert.Null(none.Source);
        Assert.Contains(new CriticalStateBlocker("applicability", "QUALIFIED_EVALUATION_UNAVAILABLE", "QUALIFIED_EVALUATION"), none.Blockers!);
        var (_, _, evaluation) = await AdmittedByCommands(client, host.Services);
        var recorded = (await CriticalStates(client, query: "?key=vessel.compliance.applicability")).States.Single();
        Assert.Equal(new CriticalStateSource("ApplicabilityEvaluation", evaluation.Id, 1, recorded.Source!.RecordedAt, "QualifiedEvaluation"), recorded.Source);
        Assert.Contains(recorded.State, new[] { "QUALIFIED_CURRENT_UNVERIFIED", "REVIEW_REQUIRED" });
        Assert.Equal("NOT_VERIFIED", recorded.Verification);
        var profile = await Profile(client); await SaveProfile(client, profile, profile.Content with { Flag = new("Unknown") });
        var stale = (await CriticalStates(client, query: "?key=vessel.compliance.applicability")).States.Single();
        Assert.Equal("STALE", stale.State); Assert.Equal(evaluation.Id, stale.Source!.Id);
        Assert.Equal(("STALE", "PROFILE_CHANGED"), (stale.Freshness!.State, stale.Freshness.ReasonCode));
        Assert.Contains(new CriticalStateBlocker("applicability", "PROFILE_CHANGED", "RE_EVALUATION"), stale.Blockers!);
    }

    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Reader_without_owning_detail_authority_sees_state_existence_only_redacted_server_side()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-critical-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); await using var host = FileHost(root, scanner.Port); using var client = Client(host);
            FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client); using var accepted = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var file = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var before = await Profile(client);
            var saved = await SaveProfile(client, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!), Flag = new("Unknown") });
            var full = await CriticalStates(client);
            Assert.All(full.States, s => Assert.Equal("FULL", s.Disclosure));
            Assert.Equal(saved.Revision, full.States[0].Source!.Revision);
            // Withdraw only the file read capability: the construction-record gate now withholds the owning profile.
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "directory.write"] };
            using (var onboarding = await client.GetAsync(CompliancePath("onboarding"), Ct)) Assert.Equal(HttpStatusCode.Forbidden, onboarding.StatusCode);
            using (var fields = await client.GetAsync(FieldsPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.Forbidden, fields.StatusCode);
            using var raw = await client.GetAsync(CriticalPath(SeedVessel.Id), Ct);
            Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
            var json = await raw.Content.ReadAsStringAsync(Ct);
            using var document = JsonDocument.Parse(json);
            var states = document.RootElement.GetProperty("states").EnumerateArray().ToArray();
            Assert.Equal([CriticalStatePolicy.Onboarding, CriticalStatePolicy.ProfileFreshness], states.Select(s => s.GetProperty("key").GetString()));
            foreach (var state in states)
            {
                // Withheld properties are omitted (54.02 p153), never sent as null.
                Assert.Equal(["key", "subjectId", "state", "disclosure", "asOf"], state.EnumerateObject().Select(p => p.Name));
                Assert.Equal("STATE_ONLY", state.GetProperty("disclosure").GetString());
                Assert.Contains(state.GetProperty("state").GetString(), CriticalStatePolicy.RedactedStates);
            }
            Assert.Equal("REVIEW_REQUIRED", states[0].GetProperty("state").GetString());
            foreach (var secret in new[] { "RegulatoryDraft", "REQUIRED_DATA", "VALUE_UNKNOWN", "profile.flag", "blockers", "reasonCodes", "\"freshness\"", "\"source\"", "observedAt", "/app/fleet", file.Id.ToString() })
                Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    // QA F1: each embedded sub-object follows its own owner's gate. A construction record bound
    // then removed leaves the current draft readable by the onboarding owner, but the DEV-020
    // owner still withholds field freshness (any bound revision). The onboarding entry stays
    // FULL without its embedded freshness; profile freshness is STATE_ONLY.
    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Onboarding_omits_embedded_dev020_freshness_when_only_a_prior_revision_is_construction_bound()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-critical-prior-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true); await using var host = FileHost(root, scanner.Port); using var client = Client(host);
            FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client); using var accepted = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var file = (await accepted.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var before = await Profile(client);
            var bound = await SaveProfile(client, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!), Flag = new("Unknown") });
            var removed = await SaveProfile(client, bound, bound.Content with { ConstructionRecord = null });
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "directory.write"] };
            // Owners: onboarding readable (current draft unbound), DEV-020 fields withheld (prior revision bound).
            using (var owner = await client.GetAsync(CompliancePath("onboarding"), Ct)) Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
            using (var fields = await client.GetAsync(FieldsPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.Forbidden, fields.StatusCode);
            using var raw = await client.GetAsync(CriticalPath(SeedVessel.Id), Ct);
            Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
            var json = await raw.Content.ReadAsStringAsync(Ct);
            using var document = JsonDocument.Parse(json);
            var states = document.RootElement.GetProperty("states").EnumerateArray().ToArray();
            Assert.Equal([CriticalStatePolicy.Onboarding, CriticalStatePolicy.ProfileFreshness], states.Select(s => s.GetProperty("key").GetString()));
            Assert.Equal("FULL", states[0].GetProperty("disclosure").GetString());
            Assert.Equal(removed.Revision, states[0].GetProperty("source").GetProperty("revision").GetInt64());
            Assert.False(states[0].TryGetProperty("freshness", out _));
            Assert.Equal(["key", "subjectId", "state", "disclosure", "asOf", "verification", "source", "reasonCodes", "blockers", "href"],
                states[0].EnumerateObject().Select(p => p.Name));
            Assert.Equal(["key", "subjectId", "state", "disclosure", "asOf"], states[1].EnumerateObject().Select(p => p.Name));
            Assert.Equal(("STATE_ONLY", "REVIEW_REQUIRED"), (states[1].GetProperty("disclosure").GetString(), states[1].GetProperty("state").GetString()));
            foreach (var secret in new[] { "FIELDS_", "SOURCE_NOT_DECLARED", "VALUE_UNKNOWN", "observedAt", "checkedAt", file.Id.ToString() })
                Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
            var single = await CriticalStates(client, query: "?key=vessel.onboarding");
            Assert.Null(Assert.Single(single.States).Freshness);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    // QA F2: the final fence re-reads the construction-file gates outside the composition
    // snapshot. A binding committed while the read is paused after its first profile statement
    // narrows the response to what the owners would now grant (both STATE_ONLY).
    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Critical_states_recheck_a_construction_binding_committed_during_composition()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-critical-fence-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var scanner = new ScanPeer("stream: OK\0", repeat: true);
            await using var writerHost = FileHost(root, scanner.Port); using var writer = Client(writerHost); FilePermissions(writerHost.Services); await Token(writer);
            var before = await Profile(writer); var stage = await StagePng(writer); using var uploaded = await PutPng(writer, stage.Id);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); var file = (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
            var barrier = new ComplianceRowsBarrier(); await using var readerHost = Host(interceptor: barrier); using var reader = Client(readerHost); await Token(reader);
            Assert.All((await CriticalStates(reader)).States, s => Assert.Equal("FULL", s.Disclosure));
            barrier.Arm(); var pending = reader.GetAsync(CriticalPath(SeedVessel.Id), Ct); HttpResponseMessage? response = null;
            try
            {
                await barrier.Wait(Ct);
                await SaveProfile(writer, before, before.Content with { ConstructionRecord = new(file.Id, file.Revision!.Value, file.Checksum!) });
                barrier.Release(); response = await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync(Ct);
                using var document = JsonDocument.Parse(json);
                var states = document.RootElement.GetProperty("states").EnumerateArray().ToArray();
                Assert.Equal([CriticalStatePolicy.Onboarding, CriticalStatePolicy.ProfileFreshness], states.Select(s => s.GetProperty("key").GetString()));
                Assert.All(states, s => Assert.Equal(["key", "subjectId", "state", "disclosure", "asOf"], s.EnumerateObject().Select(p => p.Name)));
                Assert.All(states, s => Assert.Equal("STATE_ONLY", s.GetProperty("disclosure").GetString()));
                foreach (var secret in new[] { "RegulatoryDraft", "reasonCodes", "blockers", "\"freshness\"", "/app/fleet", file.Id.ToString() })
                    Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
            }
            finally { barrier.Release(); response ??= await pending.WaitAsync(TimeSpan.FromSeconds(30), Ct); response.Dispose(); }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    [Fact, Trait("Requirement", "DEV-024")]
    public async Task Other_tenant_and_unscoped_vessels_are_not_found_and_unscoped_readers_are_denied_without_an_oracle()
    {
        await using var host = Host(edit: false); using var client = Client(host); await Token(client);
        var foreign = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        using (var other = await client.GetAsync(CriticalPath(foreign.Id), Ct)) Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using (var random = await client.GetAsync(CriticalPath(Guid.NewGuid()), Ct)) Assert.Equal(HttpStatusCode.NotFound, random.StatusCode);
        // A read-only actor still gets full explanations for its scoped vessel (reading is not editing).
        Assert.All((await CriticalStates(client)).States, s => Assert.Equal("FULL", s.Disclosure));
        await using var otherScopeBase = Host();
        await using var otherScopeHost = otherScopeBase.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Authentication:Oidc:Memberships:0:TenantWideManage"] = "False", ["Authentication:Oidc:Memberships:0:TenantWideRead"] = "False",
                ["Authentication:Oidc:Memberships:0:VesselGrants:0:VesselId"] = Guid.NewGuid().ToString() })));
        using var otherScope = Client(otherScopeHost); await Token(otherScope);
        using (var hidden = await otherScope.GetAsync(CriticalPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await using var unscopedHost = Host(scope: false); using var unscoped = Client(unscopedHost); await Token(unscoped);
        foreach (var vesselId in new[] { SeedVessel.Id, Guid.NewGuid() })
            using (var denied = await unscoped.GetAsync(CriticalPath(vesselId), Ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var anonymous = Client(host, false);
        using (var anon = await anonymous.GetAsync(CriticalPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
        await using var db = Db();
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "CriticalStateRead" && e.Result == "NotFound");
        Assert.Contains(await db.Set<SecurityEvent>().ToArrayAsync(Ct), e => e.Action == "CriticalStateRead" && e.Result == "Denied");
    }
}
