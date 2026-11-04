using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// AUDIT-TRACE-01 / AT-01..08. Real host, PostgreSQL, existing synthetic seed; no fake historical events.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Audit_permission_is_explicit_default_deny_and_live_revocation_blocks_list_detail_and_revision()
    {
        await using var deniedHost = Host(); using var denied = Client(deniedHost);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/v1/audit", Ct)).StatusCode);
        using var anonymous = Client(deniedHost, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/audit", Ct)).StatusCode);
        await using var host = Host(auditRead: true); using var client = Client(host);
        var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit", Ct))!;
        var record = page.Items.First(x => x.RevisionHref != null);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "directory.write"] };
        foreach (var path in new[] { "audit", $"audit/{record.Id}", $"audit/{record.Id}/revision" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/" + path, Ct)).StatusCode);
    }

    [Fact]
    public async Task Audit_scope_precedes_counts_paging_details_receipts_and_source_lineage()
    {
        await using var host = Host(manage: false, read: false, auditRead: true);
        using var client = Client(host);
        await using var db = Db();
        var sameTenantOther = seed.Vessels.First(x => x.TenantId == SeedVessel.TenantId && x.Id != SeedVessel.Id);
        var otherTenant = seed.Vessels.First(x => x.TenantId != SeedVessel.TenantId);
        // This actor has audit access, but no file, tug-operation or catalog
        // capability. Newly seeded operational entities are not automatically
        // published by the audit workspace's explicit entity contract.
        string[] visibleTypes = [nameof(Vessel), nameof(VesselProfile), nameof(TugProfile), nameof(RegulatoryDraft), nameof(RegulatoryDraftRevision)];
        var allowed = await db.AuditEvents.Where(x => x.VesselId == SeedVessel.Id && visibleTypes.Contains(x.EntityType)).CountAsync(Ct);
        var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1", Ct))!;
        Assert.Equal(allowed, page.Total); Assert.All(page.Items, x => Assert.Equal(SeedVessel.Id, x.VesselId));
        var complete = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=100", Ct))!;
        Assert.Equal(allowed, complete.Items.Length);
        Assert.All(complete.Items, x => Assert.Contains(x.EntityType, visibleTypes));
        foreach (var vessel in new[] { sameTenantOther, otherTenant }) {
            var empty = (await client.GetFromJsonAsync<AuditPageDto>($"/api/v1/audit?vesselId={vessel.Id}", Ct))!;
            Assert.Empty(empty.Items); Assert.Equal(0, empty.Total); Assert.Null(empty.NextCursor);
            await using var scoped = Db(vessel.TenantId);
            var hidden = await scoped.AuditEvents.Where(x => x.VesselId == vessel.Id).ToArrayAsync(Ct);
            foreach (var row in hidden) {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/audit/{row.Id}", Ct)).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/audit/{row.Id}/revision", Ct)).StatusCode);
            }
        }
        foreach (var type in new[] { nameof(Company), nameof(Tenant), nameof(RegisterDraft), nameof(RegisterRevision) }) {
            var empty = (await client.GetFromJsonAsync<AuditPageDto>($"/api/v1/audit?entityType={type}", Ct))!;
            Assert.Equal(0, empty.Total); Assert.Empty(empty.Items);
        }
        var catalog = await db.AuditEvents.FirstAsync(x => x.EntityType == nameof(RegisterRevision), Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/audit/{catalog.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/audit/{catalog.Id}/revision", Ct)).StatusCode);
    }

    [Fact]
    public async Task Audit_keyset_has_no_duplicates_and_reads_do_not_recursively_create_business_history()
    {
        await using var host = Host(auditRead: true); using var client = Client(host);
        await using var db = Db(); var before = await db.AuditEvents.CountAsync(Ct);
        var seen = new HashSet<Guid>(); string? cursor = null; int? total = null;
        do {
            var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=2" + (cursor == null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), Ct))!;
            total ??= page.Total; Assert.Equal(total.Value, page.Total);
            foreach (var row in page.Items) Assert.True(seen.Add(row.Id));
            cursor = page.NextCursor;
        } while (cursor != null);
        Assert.Equal(total, seen.Count);
        Assert.Equal(before, await db.AuditEvents.CountAsync(Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "AuditQuery" && x.Result == "Success", Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/audit/security", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=1&limit=2")]
    [InlineData("actorId=external")]
    [InlineData("vesselId=00000000-0000-0000-0000-000000000000")]
    [InlineData("entityType=SecurityEvent")]
    [InlineData("cursor=broken")]
    [InlineData("action=Export")]
    public async Task Audit_invalid_filters_are_observed_and_never_export_raw_details(string query)
    {
        await using var host = Host(auditRead: true); using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/audit?" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_audit_query", (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("code").GetString());
        await using var db = Db();
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "AuditQuery" && x.Result == "Invalid", Ct));
    }

    [Fact]
    public async Task Audit_new_directory_write_records_trusted_scope_redacts_text_and_replay_has_one_effect()
    {
        await using var host = Host(auditRead: true); using var client = Client(host); await Token(client);
        var before = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        var body = new { code = before.Code, name = "Synthetic withheld value", expectedVersion = before.Version };
        var key = Guid.NewGuid().ToString();
        using var result = await Send(client, $"vessels/{SeedVessel.Id}", body, key, true);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var replay = await Send(client, $"vessels/{SeedVessel.Id}", body, key, true);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        await using var db = Db();
        var row = Assert.Single(await db.AuditEvents.Where(x => x.ActorId == actorId.ToString() && x.EntityType == nameof(Vessel)).ToArrayAsync(Ct));
        Assert.Equal(SeedVessel.Id, row.VesselId); Assert.Equal(SeedVessel.Id, row.ResourceId);
        Assert.Contains("audit.read", row.CapabilityContext); Assert.DoesNotContain("Synthetic withheld value", row.ChangeSummary);
        Assert.Contains("ValuesWithheld", row.ChangeSummary); Assert.Equal("RecordedControlledCode", row.ReasonStatus);
        using (var summary = System.Text.Json.JsonDocument.Parse(row.ChangeSummary!))
        {
            Assert.Contains(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == "CommandName" && x.GetProperty("after").GetString() == "UpdateVessel");
            Assert.Contains(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == "TechnicalReasonCode" && x.GetProperty("after").GetString() == "PREPARATION_COMMAND");
        }
        var detail = (await client.GetFromJsonAsync<AuditRecordDto>($"/api/v1/audit/{row.Id}", Ct))!;
        Assert.Equal(before.Version, detail.BeforeVersion);
        var current = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        Assert.Equal(current.Version, detail.AfterVersion);
        Assert.Equal("Recorded", detail.MetadataStatus); Assert.Equal(actorId.ToString(), detail.ActorReference);
        Assert.Equal(Assert.Single(result.Headers.GetValues("X-Correlation-ID")), detail.CorrelationId);
        Assert.Null(detail.RevisionHref); // Directory has no invented immutable business snapshot.
        Assert.DoesNotContain(key, await (await client.GetAsync("/api/v1/audit", Ct)).Content.ReadAsStringAsync(Ct));
        db.Entry(row).Property(x => x.ReasonStatus).CurrentValue = "tamper";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Audit_exact_revision_survives_later_edit_and_scope_revocation_hides_it()
    {
        await using var host = Host(manage: false, read: false, auditRead: true); using var client = Client(host); await Token(client);
        var before = await Profile(client);
        var saved = await SaveProfile(client, before, before.Content);
        await SaveProfile(client, saved, saved.Content);
        await using var db = Db();
        var row = await db.AuditEvents.SingleAsync(x => x.EntityType == nameof(RegulatoryDraft) && x.ActorId == actorId.ToString() && x.AfterRevision == saved.Revision, Ct);
        Assert.Equal(before.Revision, row.BeforeRevision); Assert.Equal(saved.Revision, row.AfterRevision);
        Assert.DoesNotContain("declaration", row.ChangeSummary!, StringComparison.OrdinalIgnoreCase);
        var evidence = (await client.GetFromJsonAsync<AuditEvidenceDto>($"/api/v1/audit/{row.Id}/revision", Ct))!.Profile!;
        Assert.Equal(saved.Revision, evidence.Revision); Assert.Equal(saved.SavedAt, evidence.SavedAt);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { VesselGrants = [new(Guid.NewGuid())] };
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/audit/{row.Id}/revision", Ct)).StatusCode);
        var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit", Ct))!;
        Assert.Equal(0, page.Total); Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Audit_cursor_excludes_later_commits_and_never_changes_scope_from_a_hidden_record()
    {
        await using var host = Host(auditRead: true); using var client = Client(host); await Token(client);
        var first = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1", Ct))!;
        Assert.NotNull(first.NextCursor);
        var before = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        using var write = await Send(client, $"vessels/{SeedVessel.Id}", new { code = before.Code, name = before.Name, expectedVersion = before.Version }, update: true);
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
        var next = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1&cursor=" + Uri.EscapeDataString(first.NextCursor!), Ct))!;
        Assert.Equal(first.Total, next.Total); Assert.NotEqual(first.Items[0].Id, next.Items[0].Id);
        await using var restrictedHost = Host(manage: false, read: false, auditRead: true); using var restricted = Client(restrictedHost);
        var oldScopeCursor = (await restricted.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1&cursor=" + Uri.EscapeDataString(first.NextCursor!), Ct))!;
        Assert.All(oldScopeCursor.Items, x => Assert.Equal(SeedVessel.Id, x.VesselId));
        var current = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?limit=1", Ct))!;
        Assert.Equal(first.Total + 1, current.Total); // receipt audit is internal, not an extra workspace record
    }

    [Fact]
    public async Task Audit_catalog_identifiers_need_separate_catalog_capability_and_resolve_only_exact_revisions()
    {
        await using var deniedHost = Host(auditRead: true); using var denied = Client(deniedHost);
        var hidden = (await denied.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?entityType=RegisterRevision", Ct))!;
        Assert.Equal(0, hidden.Total);
        await using var host = Host(auditRead: true, regulatoryRead: true); using var client = Client(host);
        var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit?entityType=RegisterRevision", Ct))!;
        Assert.NotEmpty(page.Items);
        foreach (var item in page.Items) {
            Assert.NotNull(item.RevisionHref);
            Assert.Equal(HttpStatusCode.NotFound, (await denied.GetAsync($"/api/v1/audit/{item.Id}", Ct)).StatusCode);
            using var response = await client.GetAsync(item.RevisionHref, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct);
            Assert.Equal(item.AfterRevision, content.GetProperty("register").GetProperty("revision").GetInt64());
        }
    }

    [Theory]
    [InlineData("companies?limit=0", "CompaniesRead")]
    [InlineData("vessels?kind=Untrusted", "VesselsRead")]
    public async Task Invalid_directory_reads_record_invalid_without_success(string path, string action)
    {
        await using var host = Host(); using var client = Client(host);
        using var response = await client.GetAsync("/api/v1/" + path, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        await using var db = Db();
        var observation = Assert.Single(await db.Set<SecurityEvent>().Where(x => x.Action == action && x.CorrelationId == correlation).ToArrayAsync(Ct));
        Assert.Equal("Invalid", observation.Result);
    }

    [Fact]
    public async Task Invalid_profile_history_is_not_recorded_as_success()
    {
        await using var host = Host(); using var client = Client(host);
        using var response = await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile/history?limit=0", Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        await using var db = Db();
        var observation = Assert.Single(await db.Set<SecurityEvent>().Where(x => x.Action == "ProfileHistory" && x.CorrelationId == correlation).ToArrayAsync(Ct));
        Assert.Equal("Invalid", observation.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_detail_and_revision_reject_request_supplied_authority_filters(bool revision)
    {
        await using var host = Host(auditRead: true); using var client = Client(host);
        var page = (await client.GetFromJsonAsync<AuditPageDto>("/api/v1/audit", Ct))!;
        var path = $"/api/v1/audit/{page.Items[0].Id}" + (revision ? "/revision" : "");
        using var response = await client.GetAsync(path + "?tenantId=" + seed.Tenants[1].Id, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        await using var db = Db();
        var action = revision ? "AuditRevision" : "AuditDetail";
        var observation = Assert.Single(await db.Set<SecurityEvent>().Where(x => x.CorrelationId == correlation && x.Action == action).ToArrayAsync(Ct));
        Assert.Equal("Invalid", observation.Result);
    }

    [Fact]
    public async Task Audit_read_fails_closed_when_required_observation_cannot_persist()
    {
        await using var host = Host(auditRead: true); using var client = Client(host);
        await Sql("CREATE FUNCTION audit.reject_audit_query() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Action\" = 'AuditQuery' THEN RAISE EXCEPTION 'synthetic failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_audit_query BEFORE INSERT ON audit.\"SecurityEvents\" FOR EACH ROW EXECUTE FUNCTION audit.reject_audit_query();");
        using var response = await client.GetAsync("/api/v1/audit", Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.DoesNotContain("synthetic failure", text); Assert.DoesNotContain("items", text);
    }
}
