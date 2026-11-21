using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// S9 WP-B DEV-060 optional administrative fixture (docs/seed-data.md). Not wired into the default seed.
// Per seeded vessel: one FICTIONAL incomplete NEAR_MISS draft (no occurrence time, context, narrative or immediate
// action) reported NAMED by a deterministic synthetic actor id that has no membership. No submission, triage,
// severity, investigation, action, verification, effectiveness, closure, identity vault or receipt is seeded; DB
// checks reject a synthetic non-DRAFT row. Add-missing; operator edits are kept.
public static class SafetyEventFixtures
{
    private static Guid Id(string kind, Guid scope) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.safety-event." + kind + ".v1:" + scope.ToString("D"))).AsSpan(0, 16));
    public static Guid EventId(Guid vessel) => Id("event", vessel);
    public static Guid SyntheticReporter(Guid tenant) => Id("reporter", tenant);

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic safety event fixtures require an administrative audited transaction.");
        var now = db.Clock.GetUtcNow();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var v in vessels)
        {
            var id = EventId(v.Id);
            if (await db.SafetyEvents.AnyAsync(e => e.TenantId == v.TenantId && e.Id == id, ct)) continue;
            var fields = new SafetyEventDraftFields(Category: "NEAR_MISS", Summary: "FICTIONAL synthetic near-miss draft (incomplete)");
            if (!SafetyEventPolicy.ValidDraft(fields) || SafetyEventPolicy.Missing(fields).Length == 0) throw new InvalidOperationException("Synthetic safety event schema unavailable.");
            var e = new SafetyEvent { TenantId = v.TenantId, VesselId = v.Id, Id = id, EventNo = "SE-SYNTH-" + id.ToString("N")[..8].ToUpperInvariant(),
                ReportingMode = SafetyEventPolicy.Named, ReportedBy = SyntheticReporter(v.TenantId), Category = fields.Category,
                ContentJson = JsonSerializer.Serialize(fields, json), CreatedAt = now, SavedAt = now, Provenance = "SyntheticUnverified" };
            e.Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic-safety-event|" + e.Id.ToString("D") + "|" + e.ContentJson)));
            db.SafetyEvents.Add(e); await db.SaveChangesAsync(ct);
            db.SafetyEventRevisions.Add(new() { TenantId = e.TenantId, VesselId = e.VesselId, EventId = e.Id, Revision = 1, State = SafetyEventPolicy.Draft,
                ReasonCode = "SYNTHETIC_INCOMPLETE_DRAFT", ContentJson = e.ContentJson, Hash = e.Hash, SavedAt = now, Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
