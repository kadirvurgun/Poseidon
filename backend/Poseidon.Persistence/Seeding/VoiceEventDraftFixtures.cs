using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// DEV-062 optional administrative fixture (NOT part of the default seed). Per vessel one FICTIONAL
// typed voice-event draft whose candidate comes from the real deterministic extractor. No speech
// provenance, confirmation, rejection, correction record, evidence pin, receipt or safety event is
// seeded; DB CHECKs refuse a synthetic non-DRAFT draft. Add-missing; operator edits are kept.
public static class VoiceEventDraftFixtures
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string Transcript = "FICTIONAL synthetic note: near miss on the main deck, a loose line was seen. No real vessel, person or event.";
    public static Guid DraftId(Guid vessel) => new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.voice-draft.v1:" + vessel.ToString("D"))).AsSpan(0, 16));

    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct = default)
    {
        if (!db.IsAdministrativeImport || db.Database.CurrentTransaction == null)
            throw new UnauthorizedAccessException("Synthetic voice draft fixtures require an administrative audited transaction.");
        var now = DateTimeOffset.UtcNow;
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Transcript))).ToLowerInvariant();
        foreach (var vessel in vessels)
        {
            if (!await db.Vessels.AnyAsync(v => v.TenantId == vessel.TenantId && v.Id == vessel.Id, ct)) throw new InvalidOperationException("Synthetic voice draft scope is unavailable.");
            var id = DraftId(vessel.Id);
            if (await db.VoiceEventDrafts.AnyAsync(d => d.TenantId == vessel.TenantId && d.Id == id, ct)) continue;
            var candidate = VoiceEventDraftPolicy.Extract(Transcript, sha, new DateOnly(2026, 11, 20));
            var fields = JsonSerializer.Serialize(VoiceEventDraftPolicy.FromCandidate(candidate), Json);
            var hash = RegisterStorage.Hash("SyntheticUnverified:" + vessel.Id + ":" + fields);
            db.VoiceEventDrafts.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, Id = id, Revision = 1, State = VoiceEventDraftPolicy.Draft, TranscriptOrigin = "TYPED",
                Transcript = Transcript, TranscriptSha256 = sha, ReferenceDate = new DateOnly(2026, 11, 20), ExtractorVersion = VoiceEventDraftPolicy.ExtractorVersion,
                RuleSetVersion = VoiceEventDraftPolicy.RuleSetVersion, CandidateJson = JsonSerializer.Serialize(candidate, Json), FieldsJson = fields, Hash = hash, SavedAt = now,
                Provenance = "SyntheticUnverified" });
            await db.SaveChangesAsync(ct);
            db.VoiceEventDraftRevisions.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, DraftId = id, Revision = 1, State = VoiceEventDraftPolicy.Draft, ReasonCode = "CREATE",
                FieldsJson = fields, Hash = hash, SavedAt = now });
            await db.SaveChangesAsync(ct);
        }
        // Caller commits.
    }
}
