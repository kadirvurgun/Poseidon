using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S11 WP-A DEV-069 (18.01 Backlog row 70) and DEV-070 / T-S11-002 (row 71; 18.03 p10, p49-66, p82, p84-102): real
// PostgreSQL + actual host. Synthetic identities, a FICTIONAL data owner appointment and FICTIONAL rows only; the
// sign-off recorded here is a test-database human-command record, never a seed. AUTHORED; execution DEFERRED to CI.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid DataOwnerGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000e7");
    private string Reconciliation(string suffix = "") => $"vessels/{SeedVessel.Id}/reconciliation-reports{suffix}";

    // Same host membership, another identity; the data owner additionally holds the exact appointment.
    private void ReconActor(IServiceProvider services, Guid actor, bool owner = false, string kind = "Human", params string[] extra)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = kind,
            Permissions = [.. m.Permissions.Where(p => p is not ("import.reconciliation.sign" or "membership.manage")), .. owner ? new[] { "import.reconciliation.sign" } : [], .. extra],
            DataOwnerAuthorities = owner ? [new(DataOwnerGrant, SeedVessel.Id, Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535"), "DataOwner",
                "FICTIONAL synthetic data owner appointment; not operational", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30))] : [] };
    }

    private async Task<(ImportBatchDto Batch, ImportPreviewPageDto Preview)> ImportEquipment(HttpClient c, bool commit, params Dictionary<string, string>[] rows) {
        var batch = await UploadImport(c, "equipment", rows); var preview = await PreviewImport(c, batch);
        if (commit) Assert.Equal(HttpStatusCode.OK, (await Send(c, Imports + $"/{batch.Id}/commit",
            new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint))).StatusCode);
        return (batch, preview);
    }
    private static Dictionary<string, string> EquipmentRow(Guid id, string? criticality = "STANDARD", string? name = "FICTIONAL pump") {
        var row = new Dictionary<string, string> { ["record_key"] = id.ToString(), ["level"] = "Equipment", ["code"] = "FICTIONAL-" + id.ToString("N")[..10] };
        if (criticality != null) row["criticality"] = criticality;
        if (name != null) row["name"] = name;
        return row;
    }
    private Task<HttpResponseMessage> SignOff(HttpClient c, ReconciliationReportView v, string decision, ReconciliationException[]? exceptions = null, Guid[]? verified = null,
        string? key = null, Guid? authority = null) =>
        Send(c, Reconciliation($"/{v.Report.Id}/sign-off"), new ReconciliationSignOffCommand(v.Report.Version, v.Report.Hash, decision,
            "FICTIONAL data owner reconciliation review", exceptions, verified ?? v.Report.Sample.Select(s => s.TargetId).ToArray(), authority ?? DataOwnerGrant), key);

    [Fact]
    [Trait("Requirement", "DEV-069")]
    [Trait("Requirement", "DEV-070")]
    public async Task Reconciliation_counts_match_previews_and_only_an_appointed_independent_owner_signs_until_a_new_import_supersedes() {
        using var host = ImportHost(); using var c = Client(host);
        Guid uploader = Guid.NewGuid(), author = Guid.NewGuid(), owner = Guid.NewGuid();
        ReconActor(host.Services, uploader); await Token(c);
        // Committed batch: two created assets, one without criticality and one without a name (both missing critical).
        Guid a1 = Guid.NewGuid(), a2 = Guid.NewGuid();
        var (committed, committedPreview) = await ImportEquipment(c, true, EquipmentRow(a1, criticality: null), EquipmentRow(a2, name: null));
        // Previewed, never committed: the duplicated key rejects every occurrence; the third row stays valid but uncommitted.
        var dup = Guid.NewGuid();
        var (rejected, rejectedPreview) = await ImportEquipment(c, false, EquipmentRow(dup), EquipmentRow(dup), EquipmentRow(Guid.NewGuid()));
        Assert.Equal((3, 2, 1), (rejectedPreview.TotalRows, rejectedPreview.RejectedRows, rejectedPreview.ValidRows));

        // DEV-069: insert-only row lineage of the committed batch, readable and bounded.
        var lineage = (await c.GetFromJsonAsync<ImportProvenancePageDto>("/api/v1/" + Imports + $"/{committed.Id}/provenance", Ct))!;
        Assert.Equal(2, lineage.Total); Assert.False(lineage.HasMore);
        Assert.Equal([(2, a1), (3, a2)], lineage.Items.Select(x => (x.RowNumber, x.TargetId)));
        Assert.All(lineage.Items, x => Assert.Equal(("Asset", "CREATE", 1L, "Revision", committed.SourceHash, committedPreview.PreviewId, uploader, committed.Template.Version),
            (x.TargetKind, x.Operation, x.TargetRevision, x.RevisionKind, x.SourceHash, x.PreviewId, x.ActorId, x.TemplateVersion)));
        var page = (await c.GetFromJsonAsync<ImportProvenancePageDto>("/api/v1/" + Imports + $"/{committed.Id}/provenance?limit=1&offset=1", Ct))!;
        Assert.Equal((1, true, false), (page.Items.Length, page.Items[0].TargetId == a2, page.HasMore));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/" + Imports + $"/{committed.Id}/provenance?limit=101", Ct)).StatusCode);
        Assert.Equal(0, (await c.GetFromJsonAsync<ImportProvenancePageDto>("/api/v1/" + Imports + $"/{rejected.Id}/provenance", Ct))!.Total);
        await using (var db = Db()) Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.EntityType == "ImportProvenance" && x.ResourceId == committed.Id, Ct));

        // DEV-070: a different identity authors the report; counts equal the previews row for row.
        ReconActor(host.Services, author); await Token(c);
        var list = (await c.GetFromJsonAsync<ReconciliationListView>("/api/v1/" + Reconciliation(), Ct))!;
        Assert.True(list.CanCreate); Assert.Empty(list.Authorities);
        Assert.Contains(list.Batches, b => b.Id == committed.Id); Assert.Equal(ImportTemplates.Versions["equipment"], list.TemplateVersions["equipment"]);
        var reportId = Guid.NewGuid();
        var created = await RiskOk<ReconciliationReportView>(await Send(c, Reconciliation(), new ReconciliationReportCreateCommand(reportId, [committed.Id, rejected.Id], 1)));
        var r = created.Report;
        Assert.Equal(("AWAITING_SIGN_OFF", ImportReconciliationPolicy.PolicyVersion), (r.State, r.PolicyVersion));
        Assert.Equal(new ReconciliationCounts(5, committedPreview.ValidRows, rejectedPreview.RejectedRows, 2, 1, 2), r.Counts);
        Assert.Equal(r.Counts.Source, committedPreview.TotalRows + rejectedPreview.TotalRows);
        Assert.Equal([(a1, "criticality", "MISSING"), (a2, "name", "MISSING")], r.MissingCritical.Select(m => (m.TargetId, m.Field, m.Reason)));
        var sampled = Assert.Single(r.Sample); Assert.Contains(sampled.TargetId, new[] { a1, a2 });
        Assert.Equal(64, r.Hash.Length); Assert.Null(created.SignOff); Assert.False(created.Stale);
        Assert.Contains("data_owner_authority_required", created.SignOffBlockers); Assert.Contains("separation_of_duties", created.SignOffBlockers);
        Assert.Equal(["MISSING_CRITICAL", "REJECTED_ROWS", "DUPLICATE_ROWS", "NOT_COMMITTED", "SAMPLE_UNVERIFIED"], created.OpenItems);

        // Unauthorized sign-offs: no sign permission (403), the author or the uploader even when appointed (403), a
        // platform/membership administrator (403) and a non-human principal (403). None writes anything.
        await AssertCode(await SignOff(c, created, "REJECTED"), HttpStatusCode.Forbidden, "permission_denied");
        ReconActor(host.Services, author, owner: true); await Token(c);
        await AssertCode(await SignOff(c, created, "REJECTED"), HttpStatusCode.Forbidden, "separation_of_duties");
        ReconActor(host.Services, uploader, owner: true); await Token(c);
        await AssertCode(await SignOff(c, created, "REJECTED"), HttpStatusCode.Forbidden, "separation_of_duties");
        ReconActor(host.Services, owner, owner: true, extra: "membership.manage"); await Token(c);
        await AssertCode(await SignOff(c, created, "REJECTED"), HttpStatusCode.Forbidden, "data_owner_authority_required");
        ReconActor(host.Services, owner, owner: true, kind: "AI"); await Token(c);
        await AssertCode(await SignOff(c, created, "REJECTED"), HttpStatusCode.Forbidden, "human_actor_required");
        ReconActor(host.Services, owner, owner: true); await Token(c);
        await AssertCode(await SignOff(c, created, "REJECTED", authority: Guid.NewGuid()), HttpStatusCode.Forbidden, "data_owner_authority_required");
        await using (var db = Db()) Assert.False(await db.ReconciliationSignOffs.AnyAsync(s => s.ReportId == reportId, Ct));

        // The appointed owner cannot accept open items silently; exceptions must disposition every open item.
        var ownerView = (await c.GetFromJsonAsync<ReconciliationReportView>("/api/v1/" + Reconciliation($"/{reportId}"), Ct))!;
        Assert.True(ownerView.CanSignOff, string.Join(",", ownerView.SignOffBlockers)); Assert.Equal(DataOwnerGrant, Assert.Single(ownerView.Authorities).Id);
        await AssertCode(await SignOff(c, ownerView, "ACCEPTED"), (HttpStatusCode)422, "reconciliation_open_items");
        ReconciliationException E(string kind) => new(kind, "FICTIONAL reference", "FICTIONAL owner disposition");
        await AssertCode(await SignOff(c, ownerView, "ACCEPTED_WITH_EXCEPTIONS", [E("MISSING_CRITICAL")]), (HttpStatusCode)422, "reconciliation_exceptions_required");
        var key = Guid.NewGuid().ToString();
        var exceptions = new[] { E("MISSING_CRITICAL"), E("REJECTED_ROWS"), E("DUPLICATE_ROWS"), E("NOT_COMMITTED") };
        var signed = await RiskOk<ReconciliationReportView>(await SignOff(c, ownerView, "ACCEPTED_WITH_EXCEPTIONS", exceptions, key: key));
        Assert.Equal(("SIGNED_OFF", 2L), (signed.Report.State, signed.Report.Revision));
        Assert.Equal(("ACCEPTED_WITH_EXCEPTIONS", "DataOwner", owner, "EFFECTIVE", r.Hash), (signed.SignOff!.Decision, signed.SignOff.Duty, signed.SignOff.SignedBy, signed.SignOff.Validity, signed.SignOff.ReportHash));
        // Idempotent replay re-reads the committed outcome; a second, stale sign-off is a conflict.
        Assert.Equal(signed.SignOff.Id, (await RiskOk<ReconciliationReportView>(await SignOff(c, ownerView, "ACCEPTED_WITH_EXCEPTIONS", exceptions, key: key))).SignOff!.Id);
        await AssertCode(await SignOff(c, ownerView, "REJECTED"), HttpStatusCode.Conflict, "version_conflict");
        await AssertCode(await SignOff(c, signed, "REJECTED"), HttpStatusCode.Conflict, "invalid_state");
        await using (var db = Db()) {
            Assert.Equal(1, await db.ReconciliationSignOffs.CountAsync(s => s.ReportId == reportId, Ct));
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.EntityType == "ReconciliationSignOff" && x.ResourceId == reportId, Ct));
            Assert.DoesNotContain("FICTIONAL owner disposition", string.Join('|', await db.AuditEvents.Where(x => x.ResourceId == reportId).Select(x => x.ChangeSummary).ToArrayAsync(Ct)));
        }

        // A new committed import supersedes the signed report: the sign-off is invalidated, never edited.
        ReconActor(host.Services, uploader); await Token(c);
        await ImportEquipment(c, true, EquipmentRow(Guid.NewGuid()));
        ReconActor(host.Services, owner, owner: true); await Token(c);
        var superseded = (await c.GetFromJsonAsync<ReconciliationReportView>("/api/v1/" + Reconciliation($"/{reportId}"), Ct))!;
        Assert.Equal(("SUPERSEDED", "INVALIDATED_SUPERSEDED", "ACCEPTED_WITH_EXCEPTIONS"), (superseded.Report.State, superseded.SignOff!.Validity, superseded.SignOff.Decision));
        Assert.NotNull(superseded.Report.SupersededAt); Assert.False(superseded.CanSignOff);
        await AssertCode(await SignOff(c, superseded, "REJECTED"), HttpStatusCode.Conflict, "reconciliation_superseded");
        var summary = Assert.Single((await c.GetFromJsonAsync<ReconciliationListView>("/api/v1/" + Reconciliation(), Ct))!.Items, i => i.Id == reportId);
        Assert.Equal(("SUPERSEDED", "INVALIDATED_SUPERSEDED"), (summary.State, summary.SignOffValidity));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public async Task Reconciliation_scope_stale_pins_and_concurrent_sign_off_fail_closed() {
        using var host = ImportHost(); using var c = Client(host);
        // The author uploads the pinned batch in the same session (authoring is not signing); the owner is distinct.
        Guid author = Guid.NewGuid(), owner = Guid.NewGuid();
        ReconActor(host.Services, author); await Token(c);
        var (pending, _) = await ImportEquipment(c, false, EquipmentRow(Guid.NewGuid()));
        // Cross-tenant vessel and report ids are 404; a foreign/unknown batch cannot be pinned.
        var foreign = seed.Vessels.First(v => v.TenantId != seed.Tenants[0].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/vessels/{foreign.Id}/reconciliation-reports", Ct)).StatusCode);
        await AssertCode(await Send(c, $"vessels/{foreign.Id}/reconciliation-reports", new ReconciliationReportCreateCommand(Guid.NewGuid(), [pending.Id], 1)),
            HttpStatusCode.NotFound, "resource_not_found");
        await AssertCode(await Send(c, Reconciliation(), new ReconciliationReportCreateCommand(Guid.NewGuid(), [Guid.NewGuid()], 1)),
            (HttpStatusCode)422, "reconciliation_batch_unavailable");
        await AssertCode(await Send(c, Reconciliation(), new ReconciliationReportCreateCommand(Guid.NewGuid(), [pending.Id, pending.Id], 1)), HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + Reconciliation($"/{Guid.NewGuid()}"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/vessels/{foreign.Id}/imports/{pending.Id}/provenance", Ct)).StatusCode);

        // A pinned batch that moves after the snapshot makes the report stale; the owner cannot sign it.
        var stale = await RiskOk<ReconciliationReportView>(await Send(c, Reconciliation(), new ReconciliationReportCreateCommand(Guid.NewGuid(), [pending.Id], 1)));
        Assert.Equal(new ReconciliationCounts(1, 0, 0, 0, 1, 0), stale.Report.Counts); Assert.Empty(stale.Report.Sample);
        Assert.Equal(["NOT_COMMITTED"], stale.OpenItems);
        var fresh = (await c.GetFromJsonAsync<ImportBatchDto>("/api/v1/" + Imports + $"/{pending.Id}", Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await Send(c, Imports + $"/{pending.Id}/mapping", new ImportMappingCommand(fresh.Version, fresh.Bindings), update: true)).StatusCode);
        ReconActor(host.Services, owner, owner: true); await Token(c);
        var view = (await c.GetFromJsonAsync<ReconciliationReportView>("/api/v1/" + Reconciliation($"/{stale.Report.Id}"), Ct))!;
        Assert.True(view.Stale); Assert.Contains("reconciliation_stale", view.SignOffBlockers);
        await AssertCode(await SignOff(c, view, "REJECTED"), HttpStatusCode.Conflict, "reconciliation_stale");

        // A newer report supersedes the current one; two concurrent sign-offs of the newer report record exactly one.
        ReconActor(host.Services, author); await Token(c);
        var current = await RiskOk<ReconciliationReportView>(await Send(c, Reconciliation(), new ReconciliationReportCreateCommand(Guid.NewGuid(), [pending.Id], 1)));
        await using (var db = Db()) {
            Assert.Equal("SUPERSEDED", (await db.ReconciliationReports.SingleAsync(x => x.Id == stale.Report.Id, Ct)).State);
            Assert.Equal(1, await db.ReconciliationReports.CountAsync(x => x.VesselId == SeedVessel.Id && x.SupersededAt == null, Ct));
        }
        ReconActor(host.Services, owner, owner: true); await Token(c);
        var both = await Task.WhenAll(SignOff(c, current, "REJECTED"), SignOff(c, current, "REJECTED"));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], both.Select(x => x.StatusCode).Order());
        foreach (var response in both) response.Dispose();
        await using (var db = Db()) Assert.Equal(1, await db.ReconciliationSignOffs.CountAsync(s => s.ReportId == current.Report.Id, Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-069")]
    public async Task Import_commit_runs_the_ordinary_domain_policy_and_a_rejected_batch_writes_no_provenance() {
        using var host = ImportHost(); using var c = Client(host); await Token(c);
        // Criticality outside the declared set is refused by the ordinary equipment policy, at preview and at commit.
        var id = Guid.NewGuid();
        var (batch, preview) = await ImportEquipment(c, false, EquipmentRow(id, criticality: "UNKNOWN"));
        Assert.Contains(Assert.Single(preview.Rows).Issues, i => i.Code == "domain_validation");
        await AssertCode(await Send(c, Imports + $"/{batch.Id}/commit", new ImportCommitCommand(preview.PreviewId, long.Parse(preview.Revision), preview.TemplateFingerprint)),
            HttpStatusCode.Conflict, "version_conflict");
        await using var db = Db();
        Assert.False(await db.Assets.AnyAsync(x => x.Id == id, Ct));
        Assert.False(await db.ImportProvenance.AnyAsync(x => x.BatchId == batch.Id, Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-070")]
    public async Task Reconciliation_fixture_is_opt_in_idempotent_fictional_and_seeds_no_sign_off() {
        var vessels = seed.Vessels.Where(v => v.TenantId == seed.Tenants[0].Id).ToArray();
        for (var i = 0; i < 2; i++) {
            await using var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
            await using var tx = await admin.Database.BeginTransactionAsync(Ct);
            await ImportReconciliationFixtures.AddMissingAsync(admin, vessels, Ct);
            await tx.CommitAsync(Ct);
        }
        await using var db = Db();
        var ids = vessels.Select(v => ImportReconciliationFixtures.ReportId(v.Id)).ToArray();
        var rows = await db.ReconciliationReports.Where(r => ids.Contains(r.Id)).ToArrayAsync(Ct);
        Assert.Equal(vessels.Length, rows.Length);
        Assert.All(rows, r => Assert.Equal(("SyntheticUnverified", "AWAITING_SIGN_OFF", 0, (Guid?)null), (r.Provenance, r.State, r.SourceCount, r.SignOffId)));
        Assert.False(await db.ReconciliationSignOffs.AnyAsync(s => ids.Contains(s.ReportId), Ct));
        Assert.False(await db.ImportProvenance.AnyAsync(Ct));
        // The DB CHECK refuses a synthetic report that claims a sign-off.
        var synthetic = rows[0];
        await Assert.ThrowsAnyAsync<DbUpdateException>(async () => {
            await using var tamper = Db();
            var r = await tamper.ReconciliationReports.SingleAsync(x => x.Id == synthetic.Id, Ct);
            r.State = "SIGNED_OFF"; r.SignOffId = Guid.NewGuid(); r.SignedBy = Guid.NewGuid(); r.Revision++;
            await tamper.SaveChangesAsync(Ct);
        });
    }
}
