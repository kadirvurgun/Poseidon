using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// DEV-105/DEV-106 optional administrative fixture. Per tenant one FICTIONAL template DRAFT
// (revision 1, never submitted/published) and per vessel one FICTIONAL incomplete assessment
// DRAFT with an unrated hazard. No risk method/matrix, rating, participant, approval,
// HSEQ publication, acknowledgement, suggestion review or receipt is seeded: every rating
// stays METHOD_UNCONFIGURED and every human gate stays open. Add-missing; operator edits kept.
public static class RiskFixtures
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.risk." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid TemplateId(Guid tenant) => Id("template", tenant);
    public static Guid AssessmentId(Guid vessel) => Id("assessment", vessel);
    private static string Hash(string text) => RegisterStorage.Hash("SyntheticUnverified:" + text);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic risk fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        foreach (var vessel in vessels)
        {
            if (!await db.Vessels.AnyAsync(v => v.TenantId == vessel.TenantId && v.Id == vessel.Id, ct)) throw new InvalidOperationException("Synthetic risk scope is unavailable.");
            var templateId = TemplateId(vessel.TenantId);
            if (!await db.RiskTemplates.AnyAsync(t => t.TenantId == vessel.TenantId && t.Id == templateId, ct))
            {
                var content = new RiskTemplateContent("FICTIONAL template - not approved", "MAINTENANCE", [], [],
                    [new(Id("template-hazard", vessel.TenantId), null, "FICTIONAL hazard placeholder; requires HSEQ authoring", ["FICTIONAL consequence placeholder"],
                        [new(Id("template-control", vessel.TenantId), "FICTIONAL control placeholder", "PROPOSED", null, false, [])], [])], [], null, null);
                if (!RiskPolicy.ValidTemplateDraft(content)) throw new InvalidOperationException("Synthetic risk template schema unavailable.");
                var json = JsonSerializer.Serialize(content, Json);
                db.RiskTemplates.Add(new() { TenantId = vessel.TenantId, Id = templateId, TemplateCode = "FICTIONAL-TPL-" + templateId.ToString("N")[..8].ToUpperInvariant(),
                    LatestRevision = 1, CreatedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
                db.RiskTemplateRevisions.Add(new() { TenantId = vessel.TenantId, TemplateId = templateId, Revision = 1, Status = RiskPolicy.Draft, ContentJson = json,
                    Hash = Hash(templateId + ":" + json), AuthorsJson = "[]", SavedAt = now, Provenance = "SyntheticUnverified" });
                await db.SaveChangesAsync(ct);
            }
            var id = AssessmentId(vessel.Id);
            if (await db.RiskAssessments.AnyAsync(r => r.TenantId == vessel.TenantId && r.Id == id, ct)) continue;
            var draft = new RiskAssessmentContent("FICTIONAL assessment - incomplete draft", null, null, null, null,
                [new(Id("hazard", vessel.Id), null, "FICTIONAL hazard placeholder", [], new(null, null, null), [], [], new(null, null, null))], null, [], []);
            if (!RiskPolicy.ValidDraft(draft)) throw new InvalidOperationException("Synthetic risk assessment schema unavailable.");
            var body = JsonSerializer.Serialize(draft, Json);
            var row = new RiskAssessment { TenantId = vessel.TenantId, VesselId = vessel.Id, Id = id, AssessmentNo = "FICTIONAL-RA-" + id.ToString("N")[..8].ToUpperInvariant(),
                Revision = 1, State = RiskPolicy.Draft, ContentJson = body, Hash = Hash(vessel.Id + ":" + body), SavedAt = now, Provenance = "SyntheticUnverified" };
            db.RiskAssessments.Add(row); await db.SaveChangesAsync(ct);
            db.RiskAssessmentRevisions.Add(new() { TenantId = row.TenantId, VesselId = row.VesselId, AssessmentId = id, Revision = 1, State = RiskPolicy.Draft,
                ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", ContentJson = body, Hash = row.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
