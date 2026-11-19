using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

// S7 WP-C: DEV-105 (18.01 row 106, T-RISK2-001) and DEV-106 (row 107, T-RISK2-002); 50.01 Rev3 §3-6.
// Real host, antiforgery and owned PostgreSQL; synthetic memberships and FICTIONAL content only.
// AUTHORED; execution DEFERRED (no Docker in this package).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly string[] RiskPermissions = ["hse.risk.read", "hse.risk.write", "hse.risk.approve", "hse.risk.acknowledge", "hse.risk_template.read",
        "hse.risk_template.write", "hse.risk_template.publish", "hse.risk_template.suggest", "hse.risk_method.write", "hse.risk_method.publish"];
    private void RiskActor(IServiceProvider services, Guid actor, params string[] permissions)
    {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        settings.Memberships[0] = m with { ActorId = actor, Permissions = [.. m.Permissions.Where(p => !p.StartsWith("hse.", StringComparison.Ordinal)), .. permissions] };
    }
    private string RiskPath(string suffix = "", Guid? vessel = null) => "hse/risk-assessments" + suffix + "?vesselId=" + (vessel ?? SeedVessel.Id);
    private async Task<T> RiskOk<T>(HttpResponseMessage response)
    {
        using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<T>(Ct))!; }
    }
    private static RiskMethodContent RiskMatrix() => new("FICTIONAL-RM", "FICTIONAL tenant-configured method", [new("L1", "Rare"), new("L2", "Likely")],
        [new("S1", "Minor"), new("S2", "Major")], [new("L1", "S1", "R-A", 0), new("L1", "S2", "R-B", 1), new("L2", "S1", "R-B", 1), new("L2", "S2", "R-C", 2)],
        ["FICTIONAL-SMS-1"], "FICTIONAL configuration for test only");

    private async Task<RiskMethodDto> PublishedRiskMethod(HttpClient c, IServiceProvider services, Guid author, Guid publisher)
    {
        RiskActor(services, author, RiskPermissions); await Token(c);
        var draft = await RiskOk<RiskMethodDto>(await Send(c, "hse/risk-methods", new RiskMethodCreateCommand(Guid.NewGuid(), RiskMatrix())));
        Assert.Equal("DRAFT", draft.Status); Assert.False(draft.CanPublish);
        await AssertCode(await Send(c, $"hse/risk-methods/{draft.Id}/publish", new RiskMethodPublishCommand(draft.Version, draft.Hash, "FICTIONAL self publication attempt")), HttpStatusCode.Forbidden, "separation_of_duties");
        RiskActor(services, publisher, RiskPermissions); await Token(c);
        return await RiskOk<RiskMethodDto>(await Send(c, $"hse/risk-methods/{draft.Id}/publish", new RiskMethodPublishCommand(draft.Version, draft.Hash, "FICTIONAL HSEQ publication record")));
    }

    [Fact]
    [Trait("Requirement", "DEV-105")]
    public async Task Risk_assessment_lifecycle_keeps_human_approval_separate_immutable_and_acknowledged_per_person()
    {
        await using var host = Host(); using var c = Client(host);
        var author = Guid.NewGuid(); var approver = Guid.NewGuid(); var crew = Guid.NewGuid();
        RiskActor(host.Services, author, RiskPermissions); await Token(c);
        var caps = (await c.GetFromJsonAsync<RiskCapabilitiesDto>("/api/v1/" + RiskPath("/capabilities"), Ct))!;
        Assert.False(caps.MethodConfigured); Assert.Empty(caps.PublishedMethods);

        // Draft without a configured method: storable, every rating METHOD_UNCONFIGURED, submission blocked.
        var hazard = new RiskHazard(Guid.NewGuid(), "Rig staging", "FICTIONAL fall from height", ["Injury"], new("L2", "S2", "FICTIONAL rationale"),
            [new(Guid.NewGuid(), "FICTIONAL harness", "EXISTING", "Bosun", true, ["BAR-1"])], ["BAR-1"], new("L1", "S2", "FICTIONAL rationale"));
        var content = new RiskAssessmentContent("FICTIONAL mast work", "FICTIONAL port maintenance", "MAINTENANCE", "FICTIONAL scope", null, [hazard], true,
            [new(Guid.NewGuid(), "FICTIONAL wind above limit")], [new(author, "PREPARER"), new(crew, "PARTICIPANT")]);
        var key = Guid.NewGuid().ToString();
        var create = new RiskAssessmentCreateCommand(Guid.NewGuid(), "FICTIONAL-RA-" + Guid.NewGuid().ToString("N")[..6], content, []);
        var draft = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath(), create, key));
        Assert.Equal(("DRAFT", 1L), (draft.State, draft.Revision)); Assert.All(draft.Results, r => Assert.Equal("METHOD_UNCONFIGURED", r.Initial.State));
        Assert.Contains("risk_method_unconfigured", draft.Blockers); Assert.False(draft.CanSubmit);
        Assert.Equal(draft.Id, (await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath(), create, key))).Id);
        await AssertCode(await Send(c, RiskPath(), create with { AssessmentNo = "OTHER-NO" }, key), HttpStatusCode.Conflict, "idempotency_key_reused");
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}/submit"), new RiskAssessmentSubmitCommand(draft.Version, draft.Hash)), HttpStatusCode.UnprocessableEntity, "risk_record_incomplete");

        var method = await PublishedRiskMethod(c, host.Services, author, approver);
        RiskActor(host.Services, author, RiskPermissions); await Token(c);
        var rated = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}"), new RiskAssessmentUpdateCommand(draft.Version, content with { MethodId = method.Id }, []), update: true));
        Assert.Equal(("ASSESSED", "R-C"), (rated.Results[0].Initial.State, rated.Results[0].Initial.Result)); Assert.Empty(rated.Blockers);
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}"), new RiskAssessmentUpdateCommand(draft.Version, content, []), update: true), HttpStatusCode.Conflict, "version_conflict");
        var submitted = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}/submit"), new RiskAssessmentSubmitCommand(rated.Version, rated.Hash)));
        Assert.Equal("IN_REVIEW", submitted.State); Assert.False(submitted.CanReview);
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}/review"), new RiskAssessmentReviewCommand(submitted.Version, submitted.Hash, "APPROVE", "FICTIONAL self approval attempt")),
            HttpStatusCode.Forbidden, "separation_of_duties");
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}"), new RiskAssessmentUpdateCommand(submitted.Version, content, []), update: true), HttpStatusCode.Conflict, "invalid_state");

        RiskActor(host.Services, approver, RiskPermissions); await Token(c);
        var approved = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}/review"), new RiskAssessmentReviewCommand(submitted.Version, submitted.Hash, "APPROVE", "FICTIONAL human review record")));
        Assert.Equal(("APPROVED", approved.Revision), (approved.State, approved.ApprovedRevision!.Value)); Assert.Equal("PENDING", approved.AcknowledgementState);
        var decision = Assert.Single(approved.Decisions); Assert.Equal((approver, "APPROVE"), (decision.ActorId, decision.Decision));

        // Acknowledgement is per person on the approved revision; a non-participant cannot acknowledge for them.
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}/acknowledgements"), new RiskAssessmentAcknowledgeCommand(approved.Revision, approved.Hash, "ACK")), HttpStatusCode.Forbidden, "acknowledgment_role_required");
        RiskActor(host.Services, crew, "hse.risk.read", "hse.risk.acknowledge"); await Token(c);
        var acked = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}/acknowledgements"), new RiskAssessmentAcknowledgeCommand(approved.Revision, approved.Hash, "ACK")));
        Assert.Equal("PENDING", acked.AcknowledgementState);
        await AssertCode(await Send(c, RiskPath($"/{draft.Id}/acknowledgements"), new RiskAssessmentAcknowledgeCommand(approved.Revision, approved.Hash, "ACK")), HttpStatusCode.Conflict, "risk_acknowledgement_exists");
        RiskActor(host.Services, author, RiskPermissions); await Token(c);
        var complete = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}/acknowledgements"), new RiskAssessmentAcknowledgeCommand(approved.Revision, approved.Hash, "ACK")));
        Assert.Equal("COMPLETE", complete.AcknowledgementState);

        // Amendment opens a new draft revision; the approved snapshot stays readable and unchanged.
        var amended = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath($"/{draft.Id}/amend"), new RiskAssessmentAmendCommand(complete.Version, "FICTIONAL changed weather limits")));
        Assert.Equal("DRAFT", amended.State); Assert.Equal(approved.Revision, amended.ApprovedRevision); Assert.Equal("NOT_APPLICABLE", amended.AcknowledgementState);
        var snapshot = (await c.GetFromJsonAsync<RiskAssessmentRevisionDto>("/api/v1/" + RiskPath($"/{draft.Id}/revisions/{approved.Revision}"), Ct))!;
        Assert.Equal(("APPROVED", approved.Hash), (snapshot.State, snapshot.Hash));

        await using var db = Db();
        Assert.Equal(1, await db.RiskAssessmentDecisions.CountAsync(d => d.AssessmentId == draft.Id, Ct));
        Assert.Equal(2, await db.RiskAssessmentAcknowledgements.CountAsync(a => a.AssessmentId == draft.Id, Ct));
        var audit = await db.AuditEvents.Where(e => e.ResourceId == draft.Id).ToArrayAsync(Ct);
        Assert.DoesNotContain("FICTIONAL", JsonSerializer.Serialize(audit));
        Assert.Contains(audit, e => e.EntityType == nameof(RiskAssessmentDecision) && e.ChangeSummary!.Contains("RiskAssessmentReview") && e.ActorId == approver.ToString());
        Assert.Contains(audit, e => e.EntityType == nameof(RiskAssessmentAcknowledgement) && e.ActorId == crew.ToString() && e.VesselId == SeedVessel.Id);
        var history = await db.RiskAssessmentRevisions.FirstAsync(r => r.AssessmentId == draft.Id && r.State == "APPROVED", Ct);
        history.ContentJson = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-106")]
    public async Task Risk_template_publication_is_a_separate_HSEQ_record_supersedes_by_revision_and_applies_only_as_a_copy()
    {
        await using var host = Host(); using var c = Client(host);
        var author = Guid.NewGuid(); var hseq = Guid.NewGuid(); var crew = Guid.NewGuid();
        RiskActor(host.Services, author, RiskPermissions); await Token(c);
        var template = new RiskTemplateContent("FICTIONAL bunkering", "BUNKERING", [], ["FICTIONAL-SOPEP"],
            [new(Guid.NewGuid(), "Connect hose", "FICTIONAL spill", ["Pollution"], [new(Guid.NewGuid(), "FICTIONAL drip trays", "EXISTING", "Chief Officer", true, [])], [])],
            ["FICTIONAL-SMS-5"], "FICTIONAL HSEQ owner", null);
        var created = await RiskOk<RiskTemplateDetailDto>(await Send(c, "hse/risk-templates", new RiskTemplateCreateCommand(Guid.NewGuid(), "FICTIONAL-T-" + Guid.NewGuid().ToString("N")[..6], template)));
        var r1 = created.Revisions.Single(); Assert.Equal(("DRAFT", "NOT_EFFECTIVE"), (r1.Status, r1.EffectiveState));
        var inReview = (await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/submit", new RiskTemplateSubmitCommand(1, r1.Version, r1.Hash)))).Revisions.Single();
        await AssertCode(await Send(c, $"hse/risk-templates/{created.Id}/review", new RiskTemplateReviewCommand(1, inReview.Version, inReview.Hash, "PUBLISH", "FICTIONAL self publication", DateTimeOffset.UtcNow)),
            HttpStatusCode.Forbidden, "separation_of_duties");
        RiskActor(host.Services, hseq, RiskPermissions); await Token(c);
        var published = await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/review",
            new RiskTemplateReviewCommand(1, inReview.Version, inReview.Hash, "PUBLISH", "FICTIONAL HSEQ publication record", DateTimeOffset.UtcNow)));
        Assert.Equal(("PUBLISHED", "CURRENT", hseq), (published.Revisions[0].Status, published.Revisions[0].EffectiveState, published.Revisions[0].PublishedBy!.Value));

        // Crew suggestion is counted; acceptance never edits or publishes the template.
        RiskActor(host.Services, crew, "hse.risk_template.read", "hse.risk_template.suggest", "hse.risk.read"); await Token(c);
        var suggested = await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/suggestions", new RiskTemplateSuggestCommand(Guid.NewGuid(), 1, "FICTIONAL add a spill kit check")));
        Assert.Equal(1, suggested.SuggestionCount);

        // Application is a copy into a new draft with no rating or approval.
        var applicable = (await c.GetFromJsonAsync<RiskApplicableTemplatesDto>("/api/v1/hse/risk-templates/applicable?vesselId=" + SeedVessel.Id + "&workType=BUNKERING", Ct))!;
        Assert.Contains(applicable.Items, i => i.TemplateId == created.Id && i.Qualification == "TEMPLATE_COPY_REQUIRES_REVIEW");
        RiskActor(host.Services, author, RiskPermissions); await Token(c);
        var copy = await RiskOk<RiskAssessmentDto>(await Send(c, RiskPath(), new RiskAssessmentCreateCommand(Guid.NewGuid(), "FICTIONAL-RA-" + Guid.NewGuid().ToString("N")[..6], null, [], created.Id, 1)));
        Assert.Equal(("DRAFT", 1L), (copy.State, copy.Template!.Revision)); Assert.Null(copy.ApprovedRevision); Assert.Empty(copy.Decisions);
        Assert.Null(copy.Content.Hazards[0].Initial.Likelihood); Assert.Contains("FICTIONAL-SOPEP", copy.Content.Hazards[0].BarrierRefs);

        // Revision 2 publication supersedes revision 1, which is retained.
        var revised = await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/revise", new RiskTemplateReviseCommand(1, published.Version, "FICTIONAL incorporate suggestion")));
        var r2 = revised.Revisions.Single(r => r.Revision == 2);
        var r2Review = (await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/submit", new RiskTemplateSubmitCommand(2, r2.Version, r2.Hash)))).Revisions.Single(r => r.Revision == 2);
        RiskActor(host.Services, hseq, RiskPermissions); await Token(c);
        var final = await RiskOk<RiskTemplateDetailDto>(await Send(c, $"hse/risk-templates/{created.Id}/review",
            new RiskTemplateReviewCommand(2, r2Review.Version, r2Review.Hash, "PUBLISH", "FICTIONAL HSEQ publication record", DateTimeOffset.UtcNow)));
        Assert.Equal(("SUPERSEDED", 2L), (final.Revisions.Single(r => r.Revision == 1).Status, final.Revisions.Single(r => r.Revision == 1).SupersededByRevision!.Value));
        Assert.Equal(2, final.Decisions.Count(d => d.Decision == "PUBLISH" && d.ActorId == hseq));
    }

    [Fact]
    [Trait("Requirement", "DEV-105")]
    public async Task Risk_endpoints_enforce_permissions_tenant_vessel_scope_closed_queries_and_menu_projection()
    {
        await using var host = Host(); using var c = Client(host); await Token(c);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/" + RiskPath(), Ct)).StatusCode);
        var menu = await c.GetStringAsync("/api/v1/workspace", Ct); Assert.DoesNotContain("risk-assessment", menu);
        RiskActor(host.Services, actorId, RiskPermissions); await Token(c);
        menu = await c.GetStringAsync("/api/v1/workspace", Ct); Assert.Contains("/app/risk-assessment", menu); Assert.Contains("/app/risk-template-library", menu);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/" + RiskPath() + "&tenantId=" + SeedVessel.TenantId, Ct)).StatusCode);
        var foreign = seed.Vessels.First(v => v.TenantId != SeedVessel.TenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/" + RiskPath(vessel: foreign.Id), Ct)).StatusCode);
        var empty = new RiskAssessmentContent(null, null, null, null, null, [], null, [], []);
        await AssertCode(await Send(c, RiskPath(vessel: foreign.Id), new RiskAssessmentCreateCommand(Guid.NewGuid(), "X-1", empty, [])), HttpStatusCode.NotFound, "resource_not_found");
        await AssertCode(await Send(c, RiskPath(), new RiskAssessmentCreateCommand(Guid.NewGuid(), "X-2", empty, [new("WorkOrder", Guid.NewGuid())])), HttpStatusCode.BadRequest, "linked_resource_unavailable");
        using (var unknown = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + RiskPath()) { Content = JsonContent.Create(new { id = Guid.NewGuid(), assessmentNo = "X-3", content = empty, links = Array.Empty<object>(), state = "APPROVED" }) })
        { unknown.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); await AssertCode(await c.SendAsync(unknown, Ct), HttpStatusCode.BadRequest, "invalid_request"); }
        await AssertCode(await Send(c, RiskPath(), new RiskAssessmentCreateCommand(Guid.NewGuid(), "X-4", empty, []), key: ""), HttpStatusCode.BadRequest, "invalid_idempotency_key");
        await using var db = Db(); Assert.Empty(await db.RiskAssessments.Where(r => r.AssessmentNo.StartsWith("X-")).ToArrayAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "DEV-105")]
    public async Task Risk_fixture_is_idempotent_fictional_and_seeds_no_method_decision_publication_or_acknowledgement()
    {
        await using (var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        {
            await using var tx = await db.Database.BeginTransactionAsync(Ct); await RiskFixtures.AddMissingAsync(db, seed.Vessels, Ct); await RiskFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
            Assert.Equal(seed.Vessels.Length, await db.RiskAssessments.CountAsync(Ct));
            Assert.Equal(seed.Tenants.Length, await db.RiskTemplates.CountAsync(Ct));
            Assert.All(await db.RiskTemplateRevisions.ToArrayAsync(Ct), r => Assert.Equal(("DRAFT", (Guid?)null), (r.Status, r.PublishedBy)));
            Assert.All(await db.RiskAssessments.ToArrayAsync(Ct), r => Assert.Equal(("DRAFT", "SyntheticUnverified", (long?)null), (r.State, r.Provenance, r.ApprovedRevision)));
            Assert.Empty(await db.RiskMethods.ToArrayAsync(Ct)); Assert.Empty(await db.RiskAssessmentDecisions.ToArrayAsync(Ct));
            Assert.Empty(await db.RiskAssessmentAcknowledgements.ToArrayAsync(Ct)); Assert.Empty(await db.RiskTemplateDecisions.ToArrayAsync(Ct));
            Assert.Empty(await db.RiskCommandReceipts.ToArrayAsync(Ct));
        }
    }
}
