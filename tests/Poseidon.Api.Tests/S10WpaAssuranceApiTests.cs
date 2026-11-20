using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// S10 WP-A DEV-064 / T-S10-001 (18.01 row 65), DEV-065 / T-S10-002 (row 66), DEV-113 / T-ASS2-001 (row 114).
// Real host, antiforgery and owned PostgreSQL. Synthetic memberships and FICTIONAL content only: an importer and a
// different acceptor are two synthetic people; no inspection, finding or audit pack is a real record.
// AUTHORED; execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] AssurancePermissions = [AssurancePolicy.ReadPermission, AssurancePolicy.ImportPermission, AssurancePolicy.AcceptPermission,
        AssurancePolicy.EvidencePermission, AssurancePolicy.LinkPermission, AssurancePolicy.MappingPermission, SafetyEventPolicy.ReadPermission];
    private void AssuranceActor(IServiceProvider services, Guid actor)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = "Human",
            Permissions = [.. m.Permissions.Where(p => !p.StartsWith("assurance.", StringComparison.Ordinal) && p != SafetyEventPolicy.ReadPermission), .. AssurancePermissions, "file.read", "file.write"] };
    }
    private string AssurancePath(string suffix = "", Guid? vessel = null) => "industry-assurance" + suffix + "?vesselId=" + (vessel ?? SeedVessel.Id);
    private static readonly AssuranceColumnBinding[] SireBindings = [new("Question", "question_ref"), new("Type", "observation_type"), new("Text", "observation_text")];
    private static AssuranceTableInput SireTable(params string[][] rows) => new(["Question", "Type", "Text"], rows);
    private async Task<FileDto> AssurancePhoto(HttpClient c)
    {
        using var staged = await Send(c, $"vessels/{SeedVessel.Id}/files", new { name = "fictional-sire-photo.png", mime = "image/png", length = Png.Length, checksum = Convert.ToHexString(SHA256.HashData(Png)) });
        Assert.Equal(HttpStatusCode.OK, staged.StatusCode); var file = (await staged.Content.ReadFromJsonAsync<FileDto>(Ct))!;
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vessels/{SeedVessel.Id}/files/{file.Id}/bytes") { Content = new ByteArrayContent(Png) };
        request.Content.Headers.ContentType = new("image/png"); using var uploaded = await c.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); return (await uploaded.Content.ReadFromJsonAsync<FileDto>(Ct))!;
    }

    [Fact]
    [Trait("Requirement", "DEV-113")]
    public async Task Sire_import_is_tanker_only_versioned_staged_and_accepted_by_another_human_without_blind_overwrite_or_finding_closure()
    {
        await using var host = Host(); using var c = Client(host);
        var importer = Guid.NewGuid(); var acceptor = Guid.NewGuid();
        AssuranceActor(host.Services, importer); await Token(c);

        // Versioned mapping (tenant configuration): the same key gains version 2, version 1 stays pinned.
        var mapping = await RiskOk<AssuranceMappingDto>(await Send(c, "industry-assurance/mappings", new AssuranceMappingCreateCommand(Guid.NewGuid(), "sire2-synthetic", SireBindings)));
        Assert.Equal(1, mapping.Version);
        var mapping2 = await RiskOk<AssuranceMappingDto>(await Send(c, "industry-assurance/mappings", new AssuranceMappingCreateCommand(Guid.NewGuid(), "sire2-synthetic", SireBindings)));
        Assert.Equal(2, mapping2.Version); Assert.NotEqual(mapping.Hash, mapping2.Hash);
        await AssertCode(await Send(c, "industry-assurance/mappings", new AssuranceMappingCreateCommand(Guid.NewGuid(), "sire2-synthetic", [new("Q", "question_ref")])),
            (HttpStatusCode)422, "mapping_invalid");

        // Undeclared ship type: the inspection is stored as REVIEW_REQUIRED, never as applicable.
        var inspectionId = Guid.NewGuid();
        var stage = new AssuranceImportStageCommand(Guid.NewGuid(), inspectionId, mapping.Id, "FICTIONAL-SIRE-001", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            SireTable(["2.1.1", "NEGATIVE", "FICTIONAL hose certificate not available"], ["2.1.2", "NONE", ""]));
        var profile = await Profile(c);
        await SaveProfile(c, profile, profile.Content with { ShipType = new("Known", "FICTIONAL bulk carrier") });
        await AssertCode(await Send(c, AssurancePath("/imports"), stage), HttpStatusCode.Conflict, "not_applicable_vessel_type");
        profile = await Profile(c);
        await SaveProfile(c, profile, profile.Content with { ShipType = new("Known", "FICTIONAL oil tanker") });
        var key = Guid.NewGuid().ToString();
        var staged = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath("/imports"), stage, key));
        Assert.Equal(("TANKER", "APPLICABLE", "ImportedUnverified"), (staged.Classification, staged.Applicability, staged.Provenance));
        Assert.Empty(staged.Items); // nothing is applied before a human acceptance
        var pending = Assert.Single(staged.Imports);
        Assert.Equal(("PENDING_REVIEW", 2, true), (pending.State, pending.New, pending.ImportedByMe));
        Assert.Equal(inspectionId, (await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath("/imports"), stage, key))).Id); // idempotent replay
        await AssertCode(await Send(c, AssurancePath("/imports"), stage with { Id = Guid.NewGuid() }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertCode(await Send(c, AssurancePath("/imports"), stage with { Id = Guid.NewGuid() }), HttpStatusCode.Conflict, "import_pending_exists");
        await AssertCode(await Send(c, AssurancePath("/imports"), stage with { Id = Guid.NewGuid(), InspectionId = Guid.NewGuid(), ReferenceNo = "FICTIONAL-SIRE-BAD",
            Table = SireTable(["not-a-question", "NONE", ""]) }), (HttpStatusCode)422, "import_rows_invalid");

        // The importer cannot accept their own import.
        var decide = new AssuranceImportDecisionCommand(pending.Version, pending.DiffHash, "ACCEPT", [], [], "FICTIONAL checked against the synthetic report");
        await AssertCode(await Send(c, AssurancePath($"/imports/{pending.Id}/decision"), decide), HttpStatusCode.Forbidden, "separation_of_duties");
        AssuranceActor(host.Services, acceptor); await Token(c);
        var accepted = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath($"/imports/{pending.Id}/decision"), decide));
        Assert.Equal(["2.1.1", "2.1.2"], accepted.Items.Select(i => i.QuestionRef).ToArray());
        var finding = Assert.Single(accepted.Findings);
        Assert.Equal(("2.1.1", "OPEN", "UNVERIFIED", "UNLINKED"), (finding.QuestionRef, finding.State, finding.Verification, finding.ActionLinkState));

        // Re-import: a changed accepted value needs an explicit decision; an absent item is kept; the finding stays OPEN.
        AssuranceActor(host.Services, importer); await Token(c);
        var restaged = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath("/imports"), new AssuranceImportStageCommand(Guid.NewGuid(), inspectionId, mapping2.Id,
            Table: SireTable(["2.1.1", "NONE", ""]))));
        var second = restaged.Imports.Single(i => i.State == "PENDING_REVIEW");
        Assert.Equal((1, 1, 0), (second.Changed, second.Absent, second.New));
        AssuranceActor(host.Services, acceptor); await Token(c);
        await AssertCode(await Send(c, AssurancePath($"/imports/{second.Id}/decision"), new AssuranceImportDecisionCommand(second.Version, second.DiffHash, "ACCEPT", [], [],
            "FICTIONAL accept everything blindly")), (HttpStatusCode)422, "import_decision_incomplete");
        await AssertCode(await Send(c, AssurancePath($"/imports/{second.Id}/decision"), new AssuranceImportDecisionCommand(second.Version, new string('0', 64), "ACCEPT", [], ["2.1.1"],
            "FICTIONAL stale diff")), HttpStatusCode.Conflict, "diff_changed");
        var kept = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath($"/imports/{second.Id}/decision"), new AssuranceImportDecisionCommand(second.Version, second.DiffHash,
            "ACCEPT", [], ["2.1.1"], "FICTIONAL inspector report still shows the observation")));
        Assert.Equal(1, kept.Items.Single(i => i.QuestionRef == "2.1.1").Revision);
        Assert.Contains(kept.Items, i => i.QuestionRef == "2.1.2"); // absent from the import, never deleted
        Assert.Equal(("OPEN", "UNVERIFIED", true), (kept.Findings.Single().State, kept.Findings.Single().Verification, kept.Findings.Single().ObservationPresent));

        // Field photo evidence: VIDEO is refused (no scanned video type), a clean photo is recorded UNVERIFIED and an
        // offline replay with the same Idempotency-Key does not duplicate it.
        var photo = await AssurancePhoto(c);
        var capture = new AssuranceEvidenceCommand(Guid.NewGuid(), "2.1.1", new(photo.Id, photo.Revision!.Value, photo.Checksum!), "PHOTO", DateTimeOffset.UtcNow.AddMinutes(-30), "OFFLINE");
        await AssertCode(await Send(c, AssurancePath($"/inspections/{inspectionId}/evidence"), capture with { Id = Guid.NewGuid(), MediaKind = "VIDEO" }), (HttpStatusCode)422, "media_type_unavailable");
        await AssertCode(await Send(c, AssurancePath($"/inspections/{inspectionId}/evidence"), capture with { Id = Guid.NewGuid(), QuestionRef = "9.9.9" }), (HttpStatusCode)422, "linked_resource_unavailable");
        var commandId = Guid.NewGuid().ToString();
        var withEvidence = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath($"/inspections/{inspectionId}/evidence"), capture, commandId));
        var replay = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath($"/inspections/{inspectionId}/evidence"), capture, commandId));
        var evidence = Assert.Single(replay.Evidence);
        Assert.Equal(("OFFLINE", "UNVERIFIED", "PHOTO"), (evidence.Origin, evidence.Verification, evidence.MediaKind));
        Assert.Single(withEvidence.Evidence);

        // Finding -> action trace (S9 WP-B SafetyAction). The link does not close or verify the finding.
        var eventId = Guid.NewGuid(); var actionId = Guid.NewGuid();
        await using (var seedDb = Db())
        {
            var now = DateTimeOffset.UtcNow;
            seedDb.SafetyEvents.Add(new() { TenantId = SeedVessel.TenantId, VesselId = SeedVessel.Id, Id = eventId, EventNo = "SE-SYNTH-" + eventId.ToString("N")[..8].ToUpperInvariant(),
                ReportingMode = SafetyEventPolicy.Named, ReportedBy = Guid.NewGuid(), Category = "AUDIT_FINDING", ContentJson = "{}", Hash = new string('A', 64), CreatedAt = now, SavedAt = now,
                Provenance = "SyntheticUnverified" });
            await seedDb.SaveChangesAsync(Ct);
            seedDb.SafetyActions.Add(new() { TenantId = SeedVessel.TenantId, VesselId = SeedVessel.Id, Id = actionId, EventId = eventId, Kind = "CORRECTIVE", Title = "FICTIONAL action",
                Description = "FICTIONAL corrective action for a synthetic finding", OwnerId = acceptor, DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(10)), InvestigationRevision = 1,
                CreatedBy = acceptor, CreatedAt = now });
            await seedDb.SaveChangesAsync(Ct);
        }
        var current = replay.Findings.Single();
        await AssertCode(await Send(c, AssurancePath($"/findings/{current.Id}/action-link"), new AssuranceFindingLinkCommand(current.Version, Guid.NewGuid())),
            (HttpStatusCode)422, "linked_resource_unavailable");
        var linked = await RiskOk<AssuranceInspectionDto>(await Send(c, AssurancePath($"/findings/{current.Id}/action-link"), new AssuranceFindingLinkCommand(current.Version, actionId)));
        Assert.Equal(("LINKED", actionId, "OPEN", "UNVERIFIED"), (linked.Findings.Single().ActionLinkState, linked.Findings.Single().ActionId, linked.Findings.Single().State, linked.Findings.Single().Verification));

        await using var db = Db();
        Assert.Equal(1, await db.AssuranceEvidence.CountAsync(e => e.InspectionId == inspectionId, Ct));
        Assert.All(await db.AssuranceFindings.Where(f => f.InspectionId == inspectionId).ToArrayAsync(Ct), f => Assert.Equal(("OPEN", "UNVERIFIED"), (f.State, f.Verification)));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.EntityType == nameof(AssuranceImport) && a.ResourceId == inspectionId, Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "AssuranceCommand" && x.Result == "Success", Ct));
        // Observation text never enters the audit summary.
        Assert.DoesNotContain(await db.AuditEvents.ToArrayAsync(Ct), a => (a.ChangeSummary ?? "").Contains("FICTIONAL hose certificate"));
        // Tenant boundary: another tenant's vessel is indistinguishable from missing.
        var foreign = seed.Vessels.First(x => x.TenantId != seed.Tenants[0].Id);
        using (var hidden = await c.GetAsync("/api/v1/" + AssurancePath("", foreign.Id), Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "DEV-064")]
    public async Task Audit_pack_is_an_immutable_labelled_compilation_reauthorized_on_every_read_and_excludes_unappointed_items()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        var (ev, co) = await ReviewInputs(c);
        var (otherEv, otherCo) = await ReviewInputs(c);
        var settings = host.Services.GetRequiredService<FederationSettings>(); var now = DateTimeOffset.UtcNow;
        HumanTraceReader Reader(Guid e, Guid k) => new(Guid.NewGuid(), SeedVessel.Id, e, k, ReviewRole, ["Verification", "Decision"], [], now.AddHours(-1), now.AddHours(1));
        settings.Memberships[0] = settings.Memberships[0] with {
            ActorKind = "Human", Permissions = ["directory.read", "audit.read", "human.trace.read", "regulatory.draft.read", "control.read", "evidence.read", "file.read"],
            TenantWideManage = false, TenantWideRead = false, VesselGrants = [new(SeedVessel.Id)], HumanAuthorities = [],
            HumanTraceReaders = [Reader(ev.Id, co.Id), Reader(otherEv.Id, otherCo.Id)] };
        var path = $"audit-ready/packs?vesselId={SeedVessel.Id}";
        var key = Guid.NewGuid().ToString(); var create = new AuditPackCreateCommand(Guid.NewGuid());
        var pack = await RiskOk<AuditPackDto>(await Send(c, path, create, key));
        Assert.Equal((AuditPackPolicy.Label, 0, true), (pack.Label, pack.Excluded, pack.CreatedByMe));
        Assert.Equal(pack.ItemCount, pack.Included);
        Assert.Contains(pack.Items, i => i.Kind == "Control" && i.ResourceId == co.Id && i.Revision == co.Revision && i.State == "Current" && i.Integrity == "Match");
        Assert.Contains(pack.Items, i => i.Kind == "Evidence" && i.ResourceId == ev.Id && i.State == "Current");
        Assert.Contains(pack.Items, i => i.Kind == "Evidence" && i.ResourceId == otherEv.Id);
        Assert.Contains(pack.Items, i => i.Kind is "Requirement" or "Source");
        Assert.Equal(pack.Id, (await RiskOk<AuditPackDto>(await Send(c, path, create, key))).Id); // idempotent replay
        await AssertCode(await Send(c, path, new AuditPackCreateCommand(Guid.NewGuid()), key), HttpStatusCode.Conflict, "idempotency_key_reused");

        // The second appointment is withdrawn: the same immutable pack now only counts those items as Excluded.
        settings.Memberships[0] = settings.Memberships[0] with { HumanTraceReaders = [Reader(ev.Id, co.Id)] };
        using (var reread = await c.GetAsync($"/api/v1/audit-ready/packs/{pack.Id}?vesselId={SeedVessel.Id}", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
            var body = await reread.Content.ReadAsStringAsync(Ct); var view = JsonSerializer.Deserialize<AuditPackDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.True(view.Excluded > 0); Assert.Equal(pack.ItemCount, view.Included + view.Excluded); Assert.Equal(pack.ManifestHash, view.ManifestHash);
            Assert.DoesNotContain(otherEv.Id.ToString("D"), body); Assert.DoesNotContain(otherCo.Id.ToString("D"), body);
        }
        using (var export = await c.GetAsync($"/api/v1/audit-ready/packs/{pack.Id}/export?vesselId={SeedVessel.Id}", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            Assert.Equal("attachment", export.Content.Headers.ContentDisposition?.DispositionType ?? export.Headers.GetValues("Content-Disposition").Single().Split(';')[0]);
            var exported = await export.Content.ReadFromJsonAsync<AuditPackExportDto>(Ct);
            Assert.Equal(("poseidon.audit-pack-export.v1", AuditPackPolicy.Label), (exported!.Schema, exported.Label));
        }
        var list = (await c.GetFromJsonAsync<AuditPackListDto>($"/api/v1/{path}", Ct))!;
        Assert.Contains(list.Items, i => i.Id == pack.Id && i.ManifestHash == pack.ManifestHash);

        // Without any appointment a pack cannot be read or compiled; a non-human principal cannot compile.
        settings.Memberships[0] = settings.Memberships[0] with { HumanTraceReaders = [] };
        await AssertCode(await c.GetAsync($"/api/v1/audit-ready/packs/{pack.Id}?vesselId={SeedVessel.Id}", Ct), HttpStatusCode.Forbidden, "trace_reader_required");
        await AssertCode(await Send(c, path, new AuditPackCreateCommand(Guid.NewGuid())), (HttpStatusCode)422, "pack_scope_empty");
        settings.Memberships[0] = settings.Memberships[0] with { ActorKind = "AI", HumanTraceReaders = [Reader(ev.Id, co.Id)] };
        await AssertCode(await Send(c, path, new AuditPackCreateCommand(Guid.NewGuid())), HttpStatusCode.Forbidden, "human_authority_required");
        var foreign = seed.Vessels.First(x => x.TenantId != seed.Tenants[0].Id);
        using (var hidden = await c.GetAsync($"/api/v1/audit-ready/packs?vesselId={foreign.Id}", Ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        await using var db = Db();
        Assert.Equal(pack.ItemCount, await db.AuditPackItems.CountAsync(i => i.PackId == pack.Id, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.EntityType == nameof(AuditPackManifest) && a.ResourceId == pack.Id, Ct));
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "AuditPackRead" && x.Result == "Success" && x.ResourceId == pack.Id, Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-065")]
    public async Task Data_trust_shows_source_provenance_freshness_and_holds_readiness_at_review_required()
    {
        await using var host = Host(); using var c = Client(host); GraphPermissionsForTest(host.Services); await Token(c);
        using (var oversized = await c.GetAsync($"/api/v1/data-trust?vesselId={SeedVessel.Id}&limit=101", Ct)) Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        var (ev, co) = await ReviewInputs(c);
        var page = (await c.GetFromJsonAsync<DataTrustPageDto>($"/api/v1/data-trust?vesselId={SeedVessel.Id}&limit=100", Ct))!;
        Assert.Equal(("AVAILABLE", DataQualityPolicy.ReviewRequired, "READINESS_FLOOR_REVIEW_REQUIRED"), (page.State, page.Disposition, page.ReadinessEffect));
        var evidence = Assert.Single(page.Items, i => i.SubjectKind == "Evidence" && i.SubjectId == ev.Id);
        Assert.Equal(DataQualityPolicy.ReviewRequired, evidence.Disposition);
        Assert.Contains(evidence.Signals, s => s.Kind == "UNVERIFIED" && s.Material);
        if (evidence.FreshUntil == null) Assert.NotEqual("CURRENT", evidence.Freshness); // no declared window is never current
        Assert.Contains(page.Items, i => i.SubjectKind == "Control" && i.SubjectId == co.Id);
        Assert.True(page.Counts.Unverified >= 1);
        Assert.DoesNotContain(page.Items.Concat(page.ProfileFields), i => i.Signals.Any(s => s.Material) && i.Disposition != DataQualityPolicy.ReviewRequired);
        // Read authority is the control/evidence read set; a reader without it is refused, and the read is audited.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read"] };
        await AssertCode(await c.GetAsync($"/api/v1/data-trust?vesselId={SeedVessel.Id}", Ct), HttpStatusCode.Forbidden, "permission_denied");
        await using var db = Db();
        Assert.True(await db.Set<SecurityEvent>().AnyAsync(x => x.Action == "DataTrustRead" && x.Result == "Success" && x.ResourceId == SeedVessel.Id, Ct));
    }
}
