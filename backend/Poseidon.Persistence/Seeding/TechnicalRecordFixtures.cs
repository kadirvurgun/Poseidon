using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// DEV-103 optional administrative fixture (NOT part of the default seed). Per vessel one FICTIONAL
// incomplete service & engineer report DRAFT with no equipment/work-order/defect/evidence link. No
// submission, verification, defect lifecycle record, closure or receipt is seeded; every human gate
// stays open and DB CHECKs refuse a synthetic verification. Add-missing; operator edits are kept.
public static class TechnicalRecordFixtures
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static Guid ServiceReportId(Guid vessel) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.service-report.v1:" + vessel.ToString("D"))).AsSpan(0, 16));

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic technical record fixtures require an administrative audited transaction.");
        var now = db.Clock.GetUtcNow();
        foreach (var vessel in vessels)
        {
            if (!await db.Vessels.AnyAsync(v => v.TenantId == vessel.TenantId && v.Id == vessel.Id, ct)) throw new InvalidOperationException("Synthetic technical record scope is unavailable.");
            var id = ServiceReportId(vessel.Id);
            if (await db.ServiceReports.AnyAsync(r => r.TenantId == vessel.TenantId && r.Id == id, ct)) continue;
            var content = new ServiceReportContent("FICTIONAL provider - not a real company", null, null, "FICTIONAL scope placeholder", null, [], [], null, null, null);
            if (!ServiceReportPolicy.Valid(content)) throw new InvalidOperationException("Synthetic service report schema unavailable.");
            var body = JsonSerializer.Serialize(content, Json);
            var row = new ServiceReport { TenantId = vessel.TenantId, VesselId = vessel.Id, Id = id, ReportNo = "FICTIONAL-SR-" + id.ToString("N")[..8].ToUpperInvariant(),
                Revision = 1, State = ServiceReportPolicy.Draft, ContentJson = body, Hash = RegisterStorage.Hash("SyntheticUnverified:" + vessel.Id + ":" + body),
                AuthorsJson = "[]", SavedAt = now, Provenance = "SyntheticUnverified" };
            db.ServiceReports.Add(row); await db.SaveChangesAsync(ct);
            db.ServiceReportRevisions.Add(new() { TenantId = row.TenantId, VesselId = row.VesselId, ReportId = id, Revision = 1, State = ServiceReportPolicy.Draft,
                ReasonCode = "CREATE", ContentJson = body, Hash = row.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
