using System.Text.Json;

namespace Poseidon.Persistence.Seeding;

// DEV-040 optional administrative fixture (not wired into normal startup/seed import). Per vessel it adds one
// FICTIONAL REQUIRE_ADDITIONAL_CONTROL proposal that the synthetic author already WITHDREW, so it is terminal
// (REVOKED) and can never be approved or become ACTIVE. It pins a synthetic, non-existent evaluation and the
// tenant's synthetic incomplete rule draft, so its read-time validity is never CURRENT. No approval, approver,
// authority grant, OVERRIDE_AUTHORITY policy, legal interpretation, evidence or receipt is seeded. Add-missing:
// an existing row (or operator history) is never touched; a second run adds nothing. The fixture saves twice
// because each override state change must carry its exact append-only event (PoseidonDbContext guard).
public static class ApplicabilityOverrideFixtures
{
    public const string Rationale = "FICTIONAL synthetic proposal withdrawn by its synthetic author; no authority, approval or legal interpretation.";
    public static Guid Id(Guid vessel) => new(System.Security.Cryptography.SHA256.HashData(vessel.ToByteArray().Concat("APPLICABILITY-OVERRIDE-WITHDRAWN-v1"u8.ToArray()).ToArray()).AsSpan(0, 16));
    private static Guid Evaluation(Guid vessel) => new(System.Security.Cryptography.SHA256.HashData(vessel.ToByteArray().Concat("SYNTHETIC-NONEXISTENT-EVALUATION-v1"u8.ToArray()).ToArray()).AsSpan(0, 16));
    public static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Vessel> vessels, CancellationToken ct)
    {
        if (!db.IsAdministrativeImport) throw new UnauthorizedAccessException();
        var added = new List<ApplicabilityOverride>();
        var now = DateTimeOffset.UtcNow;
        foreach (var vessel in vessels) {
            var id = Id(vessel.Id);
            if (await db.ApplicabilityOverrides.FindAsync([vessel.TenantId, vessel.Id, id], ct) != null) continue;
            var row = new ApplicabilityOverride {
                TenantId = vessel.TenantId, VesselId = vessel.Id, Id = id, EvaluationId = Evaluation(vessel.Id), RuleId = ApplicabilityFixtures.Id(vessel.TenantId), RuleRevision = 1,
                BaseResult = "REVIEW_REQUIRED", InputHash = new string('0', 64), RuleSetHash = new string('0', 64), Purpose = "COMPLIANCE_REVIEW",
                Action = "REQUIRE_ADDITIONAL_CONTROL", ReasonCode = "ADDITIONAL_PRECAUTION", Rationale = Rationale, EvidenceIdsJson = JsonSerializer.Serialize(Array.Empty<Guid>()),
                ValidFrom = now, ExpiresAt = now.AddDays(1), ReviewAt = now.AddHours(12), AuthorId = Guid.Empty, AuthorRoleContext = "SystemSyntheticFixture",
                State = "IN_REVIEW", Sequence = 1, CreatedAt = now };
            db.ApplicabilityOverrides.Add(row);
            db.ApplicabilityOverrideEvents.Add(new() { TenantId = vessel.TenantId, VesselId = vessel.Id, OverrideId = id, Sequence = 1, FromState = null, ToState = "IN_REVIEW",
                EventCode = "PROPOSE", Rationale = Rationale, ActorId = Guid.Empty, RoleContext = "SystemSyntheticFixture", SavedAt = now });
            added.Add(row);
        }
        if (added.Count == 0) return;
        await db.SaveChangesAsync(ct);
        foreach (var row in added) {
            row.State = "REVOKED"; row.Sequence = 2;
            db.ApplicabilityOverrideEvents.Add(new() { TenantId = row.TenantId, VesselId = row.VesselId, OverrideId = row.Id, Sequence = 2, FromState = "IN_REVIEW", ToState = "REVOKED",
                EventCode = "WITHDRAW", Rationale = Rationale, ActorId = Guid.Empty, RoleContext = "SystemSyntheticFixture", SavedAt = now });
        }
        await db.SaveChangesAsync(ct);
    }
}
