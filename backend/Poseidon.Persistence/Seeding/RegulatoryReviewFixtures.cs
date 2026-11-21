using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S11 WP-C DEV-073/093 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per synthetic vessel: one FICTIONAL OPEN review campaign (SHIP for ships, TUG for tugs) whose items are the DEV-008 candidate
// sources (UNVERIFIED kept), the tug limit parameters and the unowned ownership slots — every item REVIEW_REQUIRED with no
// reviewer, no verdict, no tracked issue and no closure. DB CHECKs reject synthetic verdicts and synthetic closure.
// Add-missing; a vessel that already has an active campaign for its scope is left untouched.
public static class RegulatoryReviewFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.regulatory-review." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid CampaignId(Guid vessel) => Id("campaign", vessel);
    public static readonly Guid SyntheticAuthor = Guid.Parse("5e5e0000-0000-4000-8000-00000000c073");

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic regulatory review fixtures require an administrative audited transaction.");
        var now = db.Clock.GetUtcNow();
        foreach (var v in vessels)
        {
            var scope = v.Kind == VesselKind.Tug ? RegulatoryReviewPolicy.Tug : RegulatoryReviewPolicy.Ship;
            var id = CampaignId(v.Id);
            if (await db.RegulatoryReviewCampaigns.AnyAsync(c => c.TenantId == v.TenantId && (c.Id == id || c.VesselId == v.Id && c.Scope == scope &&
                    (c.State == RegulatoryReviewPolicy.Open || c.State == RegulatoryReviewPolicy.InReview)), ct)) continue;
            var profile = await db.RegulatoryDrafts.Where(x => x.TenantId == v.TenantId && x.VesselId == v.Id).Select(x => (long?)x.Revision).FirstOrDefaultAsync(ct);
            var items = new List<(string Kind, string Subject, string? Code, string? Authority, bool Unverified)>();
            items.AddRange(RegulatoryReviewPolicy.Candidates.Where(c => c.ReviewScopes.Contains(scope)).Select(c => (RegulatoryReviewPolicy.SourceCandidate, c.Code, (string?)c.Code, (string?)c.Authority, true)));
            if (scope == RegulatoryReviewPolicy.Tug) items.AddRange(RegulatoryReviewPolicy.TugLimitParameters.Select(p => (RegulatoryReviewPolicy.TugLimit, p, (string?)null, (string?)null, false)));
            items.AddRange(RegulatoryReviewPolicy.OwnershipSlots(scope).Select(s => (RegulatoryReviewPolicy.OwnershipSlot, s, (string?)null, (string?)s, false)));
            var campaign = new RegulatoryReviewCampaign { TenantId = v.TenantId, Id = id, VesselId = v.Id, Scope = scope, ProfileRevision = profile, ItemCount = items.Count,
                PinHash = RegulatoryReviewPolicy.Hash("synthetic:" + id.ToString("D") + ":" + profile), CreatedBy = SyntheticAuthor, CreatedAt = now,
                PolicyVersion = RegulatoryReviewPolicy.Version, Provenance = "SyntheticUnverified" };
            db.RegulatoryReviewCampaigns.Add(campaign);
            var ordinal = 0;
            foreach (var i in items)
                db.RegulatoryReviewItems.Add(new() { TenantId = v.TenantId, Id = Id("item:" + (++ordinal).ToString(System.Globalization.CultureInfo.InvariantCulture), v.Id), CampaignId = id,
                    VesselId = v.Id, Ordinal = ordinal, Kind = i.Kind, SubjectKey = i.Subject, CandidateSourceCode = i.Code, CandidateAuthority = i.Authority,
                    CandidateUnverified = i.Unverified, CreatedBy = SyntheticAuthor, Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
