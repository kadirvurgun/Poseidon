using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-020 / T-S2-001: DEV020-AC-01..10 through the actual API host, antiforgery and PostgreSQL.
// Partial of DirectoryWorkspaceTests (shared fixture): select with --filter-trait "Requirement=DEV-020", not --filter-class.
public sealed partial class DirectoryWorkspaceTests
{
    private static string FieldsPath(Guid id) => $"/api/v1/vessels/{id}/regulatory-profile/fields";
    private static string SourcesPath(Guid id) => $"vessels/{id}/regulatory-profile/field-sources";
    private async Task<ProfileFieldsDto> ProfileFields(HttpClient client, Guid? id = null) =>
        (await client.GetFromJsonAsync<ProfileFieldsDto>(FieldsPath(id ?? SeedVessel.Id), Ct))!;
    private static object Declaration(string field, long valueRevision, long sequence, string kind = "DOCUMENT",
        string? reference = "Synthetic certificate reference", string? observedAt = "2026-01-01T00:00:00Z") => new
    {
        fieldPath = field, expectedValueRevision = valueRevision, expectedSequence = sequence, sourceKind = kind,
        reference = reference == null ? new TextInput("Unknown") : new TextInput("Known", reference),
        observedAt = observedAt == null ? new TextInput("Unknown") : new TextInput("Known", observedAt)
    };
    private async Task<RegulatoryDraftDto> SeedProfileValues(HttpClient client, RegulatoryDraftContent? content = null) =>
        await SaveProfile(client, await Profile(client), content ?? new RegulatoryDraftContent { Gt = new("Known", "100.50"), Flag = new("Unknown") });

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Profile_fields_show_owner_value_versions_and_explicit_unknown_freshness_reasons()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client);
        using var raw = await client.GetAsync(FieldsPath(SeedVessel.Id), Ct);
        Assert.Equal(HttpStatusCode.OK, raw.StatusCode);
        Assert.Contains("no-store", raw.Headers.CacheControl!.ToString());
        var fields = (await raw.Content.ReadFromJsonAsync<ProfileFieldsDto>(Ct))!;
        Assert.Equal(SeedVessel.CompanyId, fields.Owner.LegalEntityId);
        Assert.Equal(("LEGACY_COMPANY_BINDING", "NOT_ESTABLISHED"), (fields.Owner.Binding, fields.Owner.RegisteredOwnerState));
        Assert.Equal(profile.Revision, fields.ProfileRevision); Assert.Equal("UNCONFIGURED", fields.Policy.Status); Assert.True(fields.CanDeclare);
        Assert.Equal(VesselProfileFieldPolicy.Fields.Count, fields.Fields.Length);
        var gt = fields.Fields.Single(x => x.FieldPath == "gt");
        Assert.Equal(new FieldValueDto("Known", "100.5"), gt.Value); Assert.Equal("TONNAGE_INDEX", gt.Unit);
        Assert.Equal(profile.Revision, gt.ValueRevision); Assert.Equal(0, gt.LatestSequence); Assert.Null(gt.Source);
        Assert.Equal(("UNKNOWN", "SOURCE_NOT_DECLARED"), (gt.Freshness.State, gt.Freshness.ReasonCode)); Assert.Equal("NOT_VERIFIED", gt.VerificationState);
        Assert.Equal("VALUE_UNKNOWN", fields.Fields.Single(x => x.FieldPath == "flag").Freshness.ReasonCode);
        Assert.Equal("VALUE_NOT_RECORDED", fields.Fields.Single(x => x.FieldPath == "imo").Freshness.ReasonCode);
        Assert.Equal(("UNKNOWN", 0, 0), (fields.Summary.State, fields.Summary.Fresh, fields.Summary.Stale));
        Assert.DoesNotContain(fields.Fields, x => x.Freshness.State is "FRESH" or "STALE");
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Declared_source_is_audited_replayable_and_bound_to_the_exact_value_version()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client); var key = Guid.NewGuid().ToString();
        using var declared = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, reference: "  Synthetic certificate reference  "), key);
        Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        Assert.True(declared.Headers.CacheControl is { NoStore: true, Private: true });
        var outcome = (await declared.Content.ReadFromJsonAsync<ProfileFieldSourceOutcomeDto>(Ct))!;
        Assert.Equal((1L, profile.Revision, "DOCUMENT", "NOT_VERIFIED", "DeclaredUnverified"),
            (outcome.Source.Sequence, outcome.Source.ValueRevision, outcome.Source.SourceKind, outcome.Source.VerificationState, outcome.Source.Provenance));
        Assert.Equal(new TextInput("Known", "Synthetic certificate reference"), outcome.Source.Reference);
        Assert.Equal(actorId, outcome.Source.DeclaredBy); Assert.Equal(SeedVessel.CompanyId, outcome.Source.StewardCompanyId);
        var gt = (await ProfileFields(client)).Fields.Single(x => x.FieldPath == "gt");
        Assert.Equal(outcome.Source, gt.Source); Assert.Equal(1, gt.LatestSequence);
        Assert.Equal(("UNKNOWN", "FRESHNESS_POLICY_UNCONFIGURED"), (gt.Freshness.State, gt.Freshness.ReasonCode));
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), gt.Freshness.ObservedAt); Assert.NotNull(gt.Freshness.AgeSeconds);
        using var replay = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, reference: "  Synthetic certificate reference  "), key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.True(replay.Headers.CacheControl is { NoStore: true, Private: true });
        Assert.Equal(outcome, await replay.Content.ReadFromJsonAsync<ProfileFieldSourceOutcomeDto>(Ct));
        using var reused = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, "OTHER"), key);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode); Assert.Contains("idempotency_key_reused", await reused.Content.ReadAsStringAsync(Ct));
        using var stale = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, "OTHER"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Contains("version_conflict", await stale.Content.ReadAsStringAsync(Ct));
        await using var db = Db();
        Assert.Single(await db.VesselProfileFieldSources.ToArrayAsync(Ct)); Assert.Single(await db.VesselProfileFieldSourceReceipts.ToArrayAsync(Ct));
        var events = await db.AuditEvents.Where(x => x.EntityType == nameof(VesselProfileFieldSource) || x.EntityType == nameof(VesselProfileFieldSourceReceipt)).ToArrayAsync(Ct);
        Assert.Equal(2, events.Length); Assert.All(events, x => { Assert.Equal(actorId.ToString(), x.ActorId); Assert.Equal(SeedVessel.Id, x.VesselId); });
        Assert.Single(events.Select(x => x.CorrelationId).Distinct());
        Assert.All(events, x => Assert.DoesNotContain("Synthetic certificate reference", x.ChangeSummary));
        Assert.Contains(events, x => x.ChangeSummary!.Contains("DeclareProfileFieldSource"));
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Changing_a_value_supersedes_its_source_and_history_keeps_both_versions()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var first = await SeedProfileValues(client);
        using (var declared = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", first.Revision, 0)))
            Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        // An unrelated field edit keeps the gt value version and its source.
        var second = await SaveProfile(client, first, first.Content with { Flag = new("Known", "Synthetic flag") });
        Assert.Equal(first.Revision, (await ProfileFields(client)).Fields.Single(x => x.FieldPath == "gt").ValueRevision);
        var third = await SaveProfile(client, second, second.Content with { Gt = new("Known", "120") });
        var gt = (await ProfileFields(client)).Fields.Single(x => x.FieldPath == "gt");
        Assert.Equal(third.Revision, gt.ValueRevision); Assert.Null(gt.Source); Assert.Equal(first.Revision, gt.PriorValueSource!.ValueRevision);
        Assert.Equal("SOURCE_DECLARED_FOR_PRIOR_VALUE", gt.Freshness.ReasonCode);
        using (var old = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", first.Revision, 1)))
        { Assert.Equal(HttpStatusCode.Conflict, old.StatusCode); Assert.Contains("version_conflict", await old.Content.ReadAsStringAsync(Ct)); }
        using (var current = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", third.Revision, 1, "CLASS_RECORD")))
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var history = (await client.GetFromJsonAsync<PageDto<ProfileFieldVersionDto>>(FieldsPath(SeedVessel.Id) + "/gt/history", Ct))!;
        Assert.Equal([third.Revision, first.Revision, 1L], history.Items.Select(x => x.ValueRevision));
        Assert.Equal(["120", "100.5", null], history.Items.Select(x => x.Value.Value));
        Assert.Equal("CLASS_RECORD", Assert.Single(history.Items[0].Sources).SourceKind);
        Assert.Equal("DOCUMENT", Assert.Single(history.Items[1].Sources).SourceKind);
        Assert.Empty(history.Items[2].Sources); Assert.False(history.HasMore);
        using var unknownField = await client.GetAsync(FieldsPath(SeedVessel.Id) + "/declaration/history", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Declaration_rejects_missing_values_invalid_bodies_csrf_and_keys_without_effects()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client);
        using (var missing = await Send(client, SourcesPath(SeedVessel.Id), Declaration("imo", 1, 0)))
        { Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode); Assert.Contains("known_profile_value_required", await missing.Content.ReadAsStringAsync(Ct)); }
        using (var field = await Send(client, SourcesPath(SeedVessel.Id), Declaration("sourceReference", profile.Revision, 0)))
        { Assert.Equal(HttpStatusCode.BadRequest, field.StatusCode); Assert.Contains("unknown_field", await field.Content.ReadAsStringAsync(Ct)); }
        using (var future = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, observedAt: "2999-01-01T00:00:00Z")))
        { Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode); Assert.Contains("observed_at_in_future", await future.Content.ReadAsStringAsync(Ct)); }
        using (var extra = await Send(client, SourcesPath(SeedVessel.Id), new { fieldPath = "gt", expectedValueRevision = profile.Revision, expectedSequence = 0, sourceKind = "DOCUMENT",
            reference = new TextInput("Unknown"), observedAt = new TextInput("Unknown"), verified = true }))
            Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        using (var noKey = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0), ""))
        { Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode); Assert.Contains("invalid_idempotency_key", await noKey.Content.ReadAsStringAsync(Ct)); }
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using (var csrf = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0)))
            Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        await using var db = Db();
        Assert.Empty(await db.VesselProfileFieldSources.ToArrayAsync(Ct)); Assert.Empty(await db.VesselProfileFieldSourceReceipts.ToArrayAsync(Ct));
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Read_only_role_cannot_declare_and_unscoped_or_other_tenant_vessels_are_not_found()
    {
        RegulatoryDraftDto profile;
        await using (var owner = Host()) { using var c = Client(owner); await Token(c); profile = await SeedProfileValues(c); }
        await using var host = Host(edit: false); using var client = Client(host); await Token(client);
        var fields = await ProfileFields(client); Assert.False(fields.CanDeclare);
        using (var denied = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0)))
        { Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Contains("permission_denied", await denied.Content.ReadAsStringAsync(Ct)); }
        var foreign = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        using (var other = await client.GetAsync(FieldsPath(foreign.Id), Ct)) Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using (var otherHistory = await client.GetAsync(FieldsPath(foreign.Id) + "/gt/history", Ct)) Assert.Equal(HttpStatusCode.NotFound, otherHistory.StatusCode);
        // 59.01 p1257 hidden root: an actor who can read the directory, but only another vessel, gets exactly 404 (never 403).
        await using var otherScopeBase = Host();
        await using var otherScopeHost = otherScopeBase.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Authentication:Oidc:Memberships:0:TenantWideManage"] = "False", ["Authentication:Oidc:Memberships:0:TenantWideRead"] = "False",
                ["Authentication:Oidc:Memberships:0:VesselGrants:0:VesselId"] = Guid.NewGuid().ToString() })));
        using var otherScope = Client(otherScopeHost); await Token(otherScope);
        using (var hidden = await otherScope.GetAsync(FieldsPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var hiddenHistory = await otherScope.GetAsync(FieldsPath(SeedVessel.Id) + "/gt/history", Ct)) Assert.Equal(HttpStatusCode.NotFound, hiddenHistory.StatusCode);
        using (var write = await Send(otherScope, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0)))
            Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        // 59.01 p1257 missing action: an actor with no directory read scope at all fails the DirectoryRead policy (403) before
        // any vessel lookup, so the answer is identical for an existing and a random vessel id (no existence oracle).
        await using var unscopedHost = Host(scope: false); using var unscoped = Client(unscopedHost); await Token(unscoped);
        foreach (var vesselId in new[] { SeedVessel.Id, Guid.NewGuid() })
        {
            using (var denied = await unscoped.GetAsync(FieldsPath(vesselId), Ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using (var deniedWrite = await Send(unscoped, SourcesPath(vesselId), Declaration("gt", profile.Revision, 0)))
                Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        }
        using var anonymous = Client(host, false);
        using (var anon = await anonymous.GetAsync(FieldsPath(SeedVessel.Id), Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
        await using var db = Db(); Assert.Empty(await db.VesselProfileFieldSources.ToArrayAsync(Ct));
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Declaration_without_a_derivable_value_version_is_a_conflict_not_a_server_error()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client);
        // Test-only fault injection: point the head at a revision with no immutable revision row,
        // so no field value version can be derived (the append-only revision table stays untouched).
        await using (var db = Db())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE core.\"RegulatoryDrafts\" SET \"Revision\" = \"Revision\" + 1000 WHERE \"VesselId\" = {SeedVessel.Id}", Ct);
        var gt = (await ProfileFields(client)).Fields.Single(x => x.FieldPath == "gt");
        Assert.Null(gt.ValueRevision); Assert.Equal(("UNKNOWN", "FIELD_VERSION_UNAVAILABLE"), (gt.Freshness.State, gt.Freshness.ReasonCode));
        using var declared = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0));
        Assert.Equal(HttpStatusCode.Conflict, declared.StatusCode); Assert.Contains("profile_unavailable", await declared.Content.ReadAsStringAsync(Ct));
        Assert.True(declared.Headers.CacheControl is { NoStore: true, Private: true });
        await using var check = Db(); Assert.Empty(await check.VesselProfileFieldSources.ToArrayAsync(Ct)); Assert.Empty(await check.VesselProfileFieldSourceReceipts.ToArrayAsync(Ct));
    }

    [Fact, Trait("Requirement", "DEV-020")]
    public async Task Concurrent_declarations_with_the_same_fence_commit_exactly_one()
    {
        await using var host = Host(); using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client);
        var replies = await Task.WhenAll(Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0)),
            Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0, "OTHER")));
        try
        {
            Assert.Single(replies, x => x.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("version_conflict", await conflict.Content.ReadAsStringAsync(Ct));
            await using var db = Db();
            Assert.Single(await db.VesselProfileFieldSources.ToArrayAsync(Ct)); Assert.Single(await db.VesselProfileFieldSourceReceipts.ToArrayAsync(Ct));
            Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.EntityType == nameof(VesselProfileFieldSource) || x.EntityType == nameof(VesselProfileFieldSourceReceipt), Ct));
        }
        finally { foreach (var reply in replies) reply.Dispose(); }
    }

    // Test-only synthetic policy: proves the server computes FRESH/STALE only from a resolved policy.
    private sealed class SyntheticFreshnessPolicy(TimeSpan maxAge) : IProfileFreshnessPolicySource
    {
        public Task<FreshnessPolicyResolution> ResolveAsync(Guid tenant, Guid vessel, CancellationToken ct) => Task.FromResult(
            new FreshnessPolicyResolution("RESOLVED", "synthetic-test-policy", new Dictionary<string, TimeSpan> { ["gt"] = maxAge }));
    }

    [Theory, Trait("Requirement", "DEV-020")]
    [InlineData(36500, "FRESH", "WITHIN_POLICY_MAX_AGE")]
    [InlineData(1, "STALE", "POLICY_MAX_AGE_EXCEEDED")]
    public async Task Resolved_policy_computes_fresh_or_stale_but_summary_stays_non_positive(int days, string state, string reason)
    {
        await using var baseline = Host();
        await using var host = baseline.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IProfileFreshnessPolicySource>(new SyntheticFreshnessPolicy(TimeSpan.FromDays(days)))));
        using var client = Client(host); await Token(client);
        var profile = await SeedProfileValues(client);
        using (var declared = await Send(client, SourcesPath(SeedVessel.Id), Declaration("gt", profile.Revision, 0)))
            Assert.Equal(HttpStatusCode.OK, declared.StatusCode);
        var fields = await ProfileFields(client);
        Assert.Equal("RESOLVED", fields.Policy.Status);
        var gt = fields.Fields.Single(x => x.FieldPath == "gt");
        Assert.Equal((state, reason, "NOT_VERIFIED"), (gt.Freshness.State, gt.Freshness.ReasonCode, gt.VerificationState));
        Assert.Equal(state == "STALE" ? "STALE" : "UNKNOWN", fields.Summary.State);
    }
}
