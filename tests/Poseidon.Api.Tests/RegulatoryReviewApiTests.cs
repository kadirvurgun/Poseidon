using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;
namespace Poseidon.Api.Tests;

// S11 WP-C DEV-073 / T-S11-005 (18.01 Backlog row 74) and DEV-093 / T-TUG-014 (row 94); 19.01 paragraphs 124-129:
// real PostgreSQL + actual host. Synthetic identities and a FICTIONAL reviewer appointment/qualification reference only.
// The recorded verdicts are test-database human acts by a synthetic reviewer, never a seed and never an SME sign-off.
// AUTHORED; execution DEFERRED to CI (no local Docker).
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid ReviewerGrant = Guid.Parse("5a7b0000-0000-4000-8000-0000000000c7");
    private Vessel ReviewShip => seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Kind == VesselKind.Ship);
    private readonly Dictionary<FederationSettings, RegulatoryReviewerAuthority[]> reviewGrants = new(ReferenceEqualityComparer.Instance);

    private WebApplicationFactory<Program> ReviewHost() {
        var config = new Dictionary<string, string?> {
            ["Authentication:Oidc:Memberships:0:Permissions:90"] = "regulatory.review.read",
            ["Authentication:Oidc:Memberships:0:Permissions:91"] = "regulatory.review.manage",
            ["Authentication:Oidc:Memberships:0:Permissions:92"] = "regulatory.review.verdict",
            ["Authentication:Oidc:Memberships:0:ActorKind"] = "Human",
        };
        const string prefix = "Authentication:Oidc:Memberships:0:RegulatoryReviewerAuthorities:0:";
        config[prefix + "Id"] = ReviewerGrant.ToString(); config[prefix + "RoleId"] = SyntheticRole;
        config[prefix + "Scopes:0"] = "SHIP"; config[prefix + "Scopes:1"] = "TUG";
        config[prefix + "SourceAuthorities:0"] = "STATUTORY"; config[prefix + "SourceAuthorities:1"] = "FLAG";
        config[prefix + "SourceAuthorities:2"] = "CLASS"; config[prefix + "SourceAuthorities:3"] = "MANUFACTURER";
        config[prefix + "QualificationRef"] = "FICTIONAL-SME-QUALIFICATION-REF; synthetic test appointment, not an operational qualification";
        config[prefix + "QualificationState"] = "VERIFIED";
        config[prefix + "ValidFrom"] = "2026-01-01T00:00:00Z"; config[prefix + "ValidUntil"] = "2099-01-01T00:00:00Z";
        return Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(config)));
    }

    // One fixture membership models several people: the author (actorId), a reviewer, an unappointed admin or a non-human.
    private void ReviewActor(IServiceProvider services, Guid actor, string kind = "Human", bool appointed = true, string? qualification = null) {
        var settings = services.GetRequiredService<FederationSettings>(); var m = settings.Memberships[0];
        if (!reviewGrants.TryGetValue(settings, out var grants)) reviewGrants[settings] = grants = m.RegulatoryReviewerAuthorities;
        settings.Memberships[0] = m with { ActorId = actor, ActorKind = kind,
            RegulatoryReviewerAuthorities = appointed ? grants.Select(g => qualification == null ? g : g with { QualificationState = qualification }).ToArray() : [] };
    }

    private async Task<T> ReviewOk<T>(HttpResponseMessage response) {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
    private async Task ReviewRefused(HttpResponseMessage response, HttpStatusCode status, string code) {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode} {body}");
        Assert.Contains($"\"code\":\"{code}\"", body);
    }
    private static RegulatoryReviewVerdictCommand ReviewVerdict(RegulatoryReviewItemDto item, string verdict = "CONFIRMED", string? authority = "STATUTORY",
        string owner = "REGULATORY_LEAD", string? reference = null, string? edition = null, string? issue = null, string note = "FICTIONAL synthetic reviewer note for the pilot profile") =>
        new(item.Version, ReviewerGrant, verdict, authority, owner, reference, edition, issue, note);

    [Fact]
    [Trait("Requirement", "DEV-073")]
    public async Task Ship_review_starts_review_required_and_only_a_qualified_independent_human_reviewer_confirms_against_a_source_and_version() {
        using var host = ReviewHost(); using var c = Client(host); await Token(c);
        var ship = ReviewShip.Id;
        // The author (Regulatory Lead) opens the campaign: every item is REVIEW_REQUIRED, nothing is confirmed by the system.
        var key = Guid.NewGuid().ToString();
        var created = await ReviewOk<RegulatoryReviewDetailDto>(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(ship, "SHIP"), key));
        var campaign = created.Campaign;
        Assert.Equal(("SHIP", "OPEN", 1L, (Guid?)null, actorId), (campaign.Scope, campaign.State, campaign.ProfileRevision, campaign.EvaluationId, campaign.CreatedBy));
        Assert.Equal(8, created.Items.Length); // 6 DEV-008 candidates (ISM, SOLAS, STCW, MLC, MARPOL, flag) + PORT_LOCAL + COMPANY_SMS slots
        Assert.All(created.Items, i => Assert.Equal(("REVIEW_REQUIRED", "REVIEW_REQUIRED", true, 0, (Guid?)null), (i.Verdict, i.EffectiveVerdict, i.Open, i.VerdictCount, i.ReviewerId)));
        Assert.All(created.Items.Where(i => i.Kind == "SOURCE_CANDIDATE"), i => Assert.Contains("CANDIDATE_UNVERIFIED", i.Blockers));
        Assert.Contains("APPLICABILITY_EVALUATION_UNAVAILABLE", campaign.Blockers);
        Assert.Equal((8, 8, 0), (campaign.OpenItems, campaign.UntrackedOpenItems, campaign.ConfirmedItems));
        Assert.True(created.CanManage); Assert.False(created.CanReview); // the author never reviews
        Assert.Contains("REVIEW_NEVER_ACTIVATES_RULES", created.Notices);
        var replay = await ReviewOk<RegulatoryReviewDetailDto>(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(ship, "SHIP"), key));
        Assert.True(replay.Replayed); Assert.Equal(campaign.Id, replay.Campaign.Id);
        await ReviewRefused(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(ship, "SHIP")), HttpStatusCode.Conflict, "review_campaign_open");
        await ReviewRefused(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(ship, "TUG")), HttpStatusCode.Conflict, "review_scope_mismatch");
        await ReviewRefused(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(ship, "PLATFORM")), HttpStatusCode.BadRequest, "validation_failed");

        var ism = created.Items.Single(i => i.SubjectKey == "SRC-ISM");
        var verdictPath = (RegulatoryReviewItemDto i) => $"regulatory-reviews/{campaign.Id}/items/{i.Id}/verdicts";
        // Separation of duties: the campaign author holds the appointment but cannot record any verdict.
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, reference: "IMO ISM Code", edition: "FICTIONAL edition")), HttpStatusCode.Forbidden, "separation_of_duties");

        var reviewer = Guid.NewGuid(); ReviewActor(host.Services, reviewer);
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism)), HttpStatusCode.Conflict, "source_reference_required");
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, owner: "UNASSIGNED", reference: "IMO ISM Code", edition: "FICTIONAL edition")), HttpStatusCode.Conflict, "source_owner_required");
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, authority: "PORT_LOCAL", reference: "IMO ISM Code", edition: "FICTIONAL edition")), HttpStatusCode.Forbidden, "reviewer_authority_required");
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, note: "Release system is type approved, FICTIONAL")), HttpStatusCode.BadRequest, "unsupported_approval_claim");
        var confirmKey = Guid.NewGuid().ToString();
        var confirmed = await ReviewOk<RegulatoryReviewItemDto>(await Send(c, verdictPath(ism), ReviewVerdict(ism, reference: "IMO ISM Code (FICTIONAL locator)", edition: "FICTIONAL edition 2018"), confirmKey));
        Assert.Equal(("CONFIRMED", false, 1, (Guid?)reviewer, "STATUTORY_LAW"), (confirmed.Verdict, confirmed.Open, confirmed.VerdictCount, confirmed.ReviewerId, confirmed.AuthorityCategory));
        Assert.True((await ReviewOk<RegulatoryReviewItemDto>(await Send(c, verdictPath(ism), ReviewVerdict(ism, reference: "IMO ISM Code (FICTIONAL locator)", edition: "FICTIONAL edition 2018"), confirmKey))).Replayed);
        // The stale client version is refused; a different body under the same key is refused.
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, "AMBIGUOUS", issue: "REG-1001")), HttpStatusCode.Conflict, "version_conflict");
        await ReviewRefused(await Send(c, verdictPath(ism), ReviewVerdict(ism, "AMBIGUOUS", issue: "REG-1001"), confirmKey), HttpStatusCode.Conflict, "idempotency_key_reused");

        // An UNVERIFIED qualification may track an open item but never confirms (409); AMBIGUOUS needs a tracked issue.
        var solas = created.Items.Single(i => i.SubjectKey == "SRC-SOLAS");
        ReviewActor(host.Services, reviewer, qualification: "UNVERIFIED");
        await ReviewRefused(await Send(c, verdictPath(solas), ReviewVerdict(solas, reference: "SOLAS (FICTIONAL)", edition: "FICTIONAL")), HttpStatusCode.Conflict, "reviewer_qualification_unverified");
        await ReviewRefused(await Send(c, verdictPath(solas), ReviewVerdict(solas, "AMBIGUOUS")), HttpStatusCode.Conflict, "tracked_issue_required");
        var ambiguous = await ReviewOk<RegulatoryReviewItemDto>(await Send(c, verdictPath(solas), ReviewVerdict(solas, "AMBIGUOUS", issue: "REG-1002")));
        Assert.Equal(("AMBIGUOUS", true, "REG-1002"), (ambiguous.Verdict, ambiguous.Open, ambiguous.TrackedIssueId));

        // A platform administrator without an appointment, and an AI principal with one, are refused.
        ReviewActor(host.Services, Guid.NewGuid(), appointed: false);
        var stcw = created.Items.Single(i => i.SubjectKey == "SRC-STCW");
        await ReviewRefused(await Send(c, verdictPath(stcw), ReviewVerdict(stcw, "REVIEW_REQUIRED", issue: "REG-1003")), HttpStatusCode.Forbidden, "reviewer_authority_required");
        ReviewActor(host.Services, reviewer, kind: "AI");
        await ReviewRefused(await Send(c, verdictPath(stcw), ReviewVerdict(stcw, "REVIEW_REQUIRED", issue: "REG-1003")), HttpStatusCode.Forbidden, "reviewer_authority_required");

        // Closing: CLOSED is refused while items are open; every open item must be a tracked issue first.
        ReviewActor(host.Services, actorId);
        var current = await c.GetFromJsonAsync<RegulatoryReviewDetailDto>($"/api/v1/regulatory-reviews/{campaign.Id}", Ct);
        Assert.Equal(("IN_REVIEW", 7, 6, 1), (current!.Campaign.State, current.Campaign.OpenItems, current.Campaign.UntrackedOpenItems, current.Campaign.ConfirmedItems));
        var closePath = $"regulatory-reviews/{campaign.Id}/close";
        await ReviewRefused(await Send(c, closePath, new RegulatoryReviewCloseCommand(current.Campaign.Version, "CLOSED", "FICTIONAL pilot review window ended")), HttpStatusCode.Conflict, "open_items_remain");
        await ReviewRefused(await Send(c, closePath, new RegulatoryReviewCloseCommand(current.Campaign.Version, "CLOSED_WITH_OPEN_ITEMS", "FICTIONAL pilot review window ended")), HttpStatusCode.Conflict, "open_item_untracked");
        ReviewActor(host.Services, reviewer);
        var issue = 2000;
        foreach (var open in current.Items.Where(i => i.Open && i.TrackedIssueId == null))
            await ReviewOk<RegulatoryReviewItemDto>(await Send(c, verdictPath(open), ReviewVerdict(open, "REVIEW_REQUIRED", authority: null, issue: $"REG-{++issue}")));
        ReviewActor(host.Services, actorId);
        var tracked = (await c.GetFromJsonAsync<RegulatoryReviewDetailDto>($"/api/v1/regulatory-reviews/{campaign.Id}", Ct))!;
        Assert.Equal((7, 0), (tracked.Campaign.OpenItems, tracked.Campaign.UntrackedOpenItems));
        var closed = await ReviewOk<RegulatoryReviewDetailDto>(await Send(c, closePath, new RegulatoryReviewCloseCommand(tracked.Campaign.Version, "CLOSED_WITH_OPEN_ITEMS", "FICTIONAL pilot review window ended")));
        Assert.Equal(("CLOSED_WITH_OPEN_ITEMS", (int?)7, (Guid?)actorId), (closed.Campaign.State, closed.Campaign.OpenItemsAtClose, closed.Campaign.ClosedBy));
        Assert.False(closed.CanManage); Assert.False(closed.CanReview);
        ReviewActor(host.Services, reviewer);
        var late = closed.Items.Single(i => i.SubjectKey == "SRC-MLC");
        await ReviewRefused(await Send(c, verdictPath(late), ReviewVerdict(late, "AMBIGUOUS", issue: "REG-9")), HttpStatusCode.Conflict, "campaign_closed");

        // Append-only lineage: 1 confirmation + 1 ambiguity + 6 tracked REVIEW_REQUIRED; no rule was activated.
        await using (var db = Db()) {
            var verdicts = await db.RegulatoryReviewVerdicts.Where(v => v.CampaignId == campaign.Id).ToArrayAsync(Ct);
            Assert.Equal(8, verdicts.Length);
            Assert.All(verdicts, v => Assert.Equal((reviewer, ReviewerGrant), (v.ReviewerId, v.AuthorityId)));
            var confirmation = Assert.Single(verdicts, v => v.Verdict == "CONFIRMED");
            Assert.Equal(("VERIFIED", "FICTIONAL edition 2018"), (confirmation.QualificationState, confirmation.SourceEdition));
            Assert.Equal("UNVERIFIED", Assert.Single(verdicts, v => v.Verdict == "AMBIGUOUS").QualificationState);
            Assert.Equal(10, await db.RegulatoryReviewReceipts.CountAsync(r => r.CampaignId == campaign.Id, Ct)); // create + 8 verdicts + close
        }
        // A pinned profile revision change makes the campaign STALE on the read side.
        await using (var admin = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true))
        await using (var tx = await admin.Database.BeginTransactionAsync(Ct)) {
            var draft = await admin.RegulatoryDrafts.SingleAsync(d => d.TenantId == ReviewShip.TenantId && d.VesselId == ship, Ct);
            draft.Revision++; draft.SavedAt = DateTimeOffset.UtcNow; admin.RegulatoryDraftRevisions.Add(draft.Snapshot());
            await admin.SaveChangesAsync(Ct); await tx.CommitAsync(Ct);
        }
        ReviewActor(host.Services, actorId);
        var stale = (await c.GetFromJsonAsync<RegulatoryReviewDetailDto>($"/api/v1/regulatory-reviews/{campaign.Id}", Ct))!;
        Assert.True(stale.Campaign.ProfileStale); Assert.Contains("VESSEL_PROFILE_CHANGED", stale.Campaign.Blockers);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/v1/regulatory-reviews?vessel={ship}&scope=SHIP", Ct)).StatusCode);
    }

    [Fact]
    [Trait("Requirement", "DEV-093")]
    public async Task Tug_review_never_presents_an_unsourced_limit_as_a_legal_default_and_tags_guidance_apart_from_law() {
        using var host = ReviewHost(); using var c = Client(host); await Token(c);
        var created = await ReviewOk<RegulatoryReviewDetailDto>(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(Tug.Id, "TUG")));
        // ISM + flag + 3 class candidates, 6 towage limits, PORT_LOCAL/COMPANY_SMS/MANUFACTURER/INDUSTRY_GUIDANCE slots.
        Assert.Equal(15, created.Items.Length);
        var limits = created.Items.Where(i => i.Kind == "TUG_LIMIT").ToArray();
        Assert.Equal(TowagePlanPolicy.LimitParameters.Order(StringComparer.Ordinal), limits.Select(i => i.SubjectKey).Order(StringComparer.Ordinal));
        Assert.All(limits, i => { Assert.Contains("LIMIT_SOURCE_MISSING", i.Blockers); Assert.Equal("REVIEW_REQUIRED", i.EffectiveVerdict); Assert.Null(i.SourceReference); });
        Assert.Equal("GUIDANCE_NOT_LAW", created.Items.Single(i => i.SubjectKey == "MANUFACTURER").AuthorityCategory);
        Assert.Equal("CLASS_RULE", created.Items.Single(i => i.SubjectKey == "SRC-IACS-M79").AuthorityCategory);
        Assert.Contains("NO_UNSOURCED_LIMIT_IS_A_LEGAL_DEFAULT", created.Notices); Assert.Contains("NO_CLASS_OR_TYPE_APPROVAL_CLAIM", created.Notices);
        Assert.Contains(created.OwnerMatrix, r => r.Authority == "UNASSIGNED" && r.Items == 6 && r.Open == 6);

        ReviewActor(host.Services, Guid.NewGuid());
        var wind = limits.Single(i => i.SubjectKey == "WIND_SPEED");
        var path = $"regulatory-reviews/{created.Campaign.Id}/items/{wind.Id}/verdicts";
        await ReviewRefused(await Send(c, path, ReviewVerdict(wind, authority: "MANUFACTURER")), HttpStatusCode.Conflict, "source_reference_required");
        await ReviewRefused(await Send(c, path, ReviewVerdict(wind, authority: "MANUFACTURER", reference: "FICTIONAL winch manual", edition: "Rev A", note: "Winch class approved, FICTIONAL")),
            HttpStatusCode.BadRequest, "unsupported_approval_claim");
        var sourced = await ReviewOk<RegulatoryReviewItemDto>(await Send(c, path, ReviewVerdict(wind, authority: "MANUFACTURER", owner: "REGULATORY_SME",
            reference: "FICTIONAL manufacturer operating manual section 4", edition: "FICTIONAL Rev A")));
        Assert.Equal(("CONFIRMED", "GUIDANCE_NOT_LAW", "MANUFACTURER"), (sourced.Verdict, sourced.AuthorityCategory, sourced.SourceAuthority));
        Assert.DoesNotContain("LIMIT_SOURCE_MISSING", sourced.Blockers);

        // Tenant boundary: an unknown vessel/campaign is 404 and the other tenant's context never sees the campaign or its items.
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/regulatory-reviews?vessel={Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/regulatory-reviews/{Guid.NewGuid()}", Ct)).StatusCode);
        await ReviewRefused(await Send(c, "regulatory-reviews", new RegulatoryReviewCreateCommand(Guid.NewGuid(), "SHIP")), HttpStatusCode.NotFound, "resource_not_found");
        await using (var other = Db(seed.Tenants[1].Id)) {
            Assert.False(await other.RegulatoryReviewCampaigns.AnyAsync(x => x.Id == created.Campaign.Id, Ct));
            Assert.False(await other.RegulatoryReviewItems.AnyAsync(x => x.CampaignId == created.Campaign.Id, Ct));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/regulatory-reviews?vessel={Tug.Id}&extra=1", Ct)).StatusCode);
        using (var plain = Host()) using (var p = Client(plain)) { await Token(p); Assert.Equal(HttpStatusCode.Forbidden, (await p.GetAsync($"/api/v1/regulatory-reviews?vessel={Tug.Id}", Ct)).StatusCode); }

        // Opt-in FICTIONAL fixture: idempotent, OPEN only, every item REVIEW_REQUIRED without reviewer; the tug keeps its campaign.
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await using (var tx = await db.Database.BeginTransactionAsync(Ct)) {
            await RegulatoryReviewFixtures.AddMissingAsync(db, seed.Vessels, Ct); await RegulatoryReviewFixtures.AddMissingAsync(db, seed.Vessels, Ct); await tx.CommitAsync(Ct);
        }
        var ids = seed.Vessels.Where(v => v.Id != Tug.Id).Select(v => RegulatoryReviewFixtures.CampaignId(v.Id)).ToArray();
        var synthetic = await db.RegulatoryReviewCampaigns.Where(x => ids.Contains(x.Id)).ToArrayAsync(Ct);
        Assert.Equal(ids.Length, synthetic.Length);
        Assert.All(synthetic, x => Assert.Equal(("OPEN", "SyntheticUnverified", (Guid?)null), (x.State, x.Provenance, x.ClosedBy)));
        Assert.False(await db.RegulatoryReviewCampaigns.AnyAsync(x => x.Id == RegulatoryReviewFixtures.CampaignId(Tug.Id), Ct));
        Assert.All(await db.RegulatoryReviewItems.Where(i => ids.Contains(i.CampaignId)).ToArrayAsync(Ct),
            i => Assert.Equal(("REVIEW_REQUIRED", 0, (Guid?)null, (string?)null), (i.Verdict, i.VerdictCount, i.ReviewerId, i.TrackedIssueId)));
        Assert.False(await db.RegulatoryReviewVerdicts.AnyAsync(v => ids.Contains(v.CampaignId), Ct));
    }
}
