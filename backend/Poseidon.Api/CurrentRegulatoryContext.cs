using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class CurrentRegulatoryContext
{
    internal static string VesselHash(Vessel v) => RegisterStorage.Hash(JsonSerializer.Serialize(new { v.Id, v.Kind }));
    internal static async Task<RegulatoryQualificationEvent?> Qualification(PoseidonDbContext db, RuleRevision rule, CancellationToken ct)
    {
        var q = await db.RegulatoryQualificationEvents.AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == rule.Id && x.RuleRevision == rule.Revision &&
            db.RegulatoryQualificationHeads.Any(h => h.RuleId == x.RuleId && h.Revision == x.Revision), ct);
        if (q == null || q.State is "DRAFT" or "IN_REVIEW" || RegulatoryQualificationPolicy.Missing(RegulatoryQualificationEndpoints.Content(q)).Length != 0 ||
            JsonSerializer.Deserialize<RegulatorySourceContract>(q.ContractJson, RegulatoryQualificationEndpoints.Json)?.RequiredMechanism != null) return null;
        foreach (var (kind, id, revision) in new[] { ("Source", rule.SourceId, rule.SourceRevision), ("Requirement", rule.RequirementId, rule.RequirementRevision) })
            if (!await db.RegisterDrafts.AnyAsync(x => x.Kind == kind && x.Id == id && x.Revision == revision, ct)) return null;
        return q;
    }
    internal static async Task<(SortedDictionary<string, ContextFact> Facts, RegulatoryFactAttestation? Attestation)> Facts(PoseidonDbContext db,
        RegulatoryQualificationEvent q, Guid vessel, long? profile, SortedDictionary<string, ContextFact> declared, DateTimeOffset now, CancellationToken ct)
    {
        var facts = new SortedDictionary<string, ContextFact>(declared, StringComparer.Ordinal);
        var policy = RegulatoryQualificationEndpoints.Content(q);
        var attestation = await db.RegulatoryFactAttestations.AsNoTracking().Where(x => x.RuleId == q.RuleId && x.QualificationRevision == q.Revision && x.VesselId == vessel && x.ProfileRevision == profile && x.SavedAt <= now)
            .OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        var attested = attestation == null ? [] : JsonSerializer.Deserialize<string[]>(attestation.FieldsJson)!;
        foreach (var input in policy.Inputs ?? []) {
            var f = facts[input.Field];
            if (input.Basis == "ProviderVerified") { facts[input.Field] = f with { Provenance = "RequiredProviderUnavailable" }; continue; }
            if (f.Knowledge != "Unverified") continue;
            if (input.Basis == "Declared" && input.MaxAgeHours != null && f.ObservedAt == null) facts[input.Field] = f with { Knowledge = "Unverified", Provenance = "FreshnessObservationMissing" };
            else if (input.Basis == "Declared" && RegulatoryQualificationPolicy.ProfileFields.Contains(input.Field))
                facts[input.Field] = f with { Knowledge = "Known", FreshUntil = input.MaxAgeHours == null ? null : f.ObservedAt?.AddHours(input.MaxAgeHours.Value) };
            else if (input.Basis == "HumanAttested" && attestation != null && attested.Contains(input.Field))
                facts[input.Field] = f with { Knowledge = "Known", Provenance = "RecordedHumanAttestation", Revision = attestation.Hash,
                    ObservedAt = attestation.SavedAt, FreshUntil = input.MaxAgeHours == null ? attestation.ValidUntil : new[] { attestation.ValidUntil, attestation.SavedAt.AddHours(input.MaxAgeHours.Value) }.Min() };

        }
        facts["source.lifecycle"] = new(RuleFieldType.Text, "Known", JsonSerializer.SerializeToElement(q.State), "RecordedSourceQualification", q.Hash, q.SavedAt);
        return (facts, attestation);
    }
    internal static async Task<string[]> Stale(PoseidonDbContext db, Guid id, Guid vessel, DateTimeOffset now, CancellationToken ct)
    {
        var evaluation = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (evaluation == null) return ["EVALUATION_UNAVAILABLE"];
        var pins = await db.EvaluationQualifications.AsNoTracking().Where(x => x.EvaluationId == id).ToArrayAsync(ct);
        if (pins.Length == 0) return ["QUALIFICATION_LINEAGE_UNAVAILABLE"];
        var causes = new HashSet<string>();
        if (!await db.RegulatoryDrafts.AnyAsync(x => x.VesselId == vessel && x.Revision == evaluation.ProfileRevision, ct)) causes.Add("PROFILE_CHANGED");
        using var snapshot = JsonDocument.Parse(evaluation.SnapshotJson);
        var captured = snapshot.RootElement.GetProperty("vesselVersion").GetUInt32();
        var currentVessel = await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == vessel, ct);
        if (snapshot.RootElement.TryGetProperty("vesselContextHash", out var vesselHash) ? vesselHash.GetString() != VesselHash(currentVessel) : currentVessel.Version != captured) causes.Add("VESSEL_CHANGED");
        foreach (var pin in pins) {
            var rule = await db.EvaluationRules.AsNoTracking().SingleAsync(x => x.EvaluationId == id && x.RuleId == pin.RuleId, ct);
            if (!await db.RuleDrafts.AnyAsync(x => x.Id == rule.RuleId && x.Revision == rule.RuleRevision, ct)) causes.Add("RULE_CHANGED");
            if (!await db.RegulatoryQualificationHeads.AnyAsync(x => x.RuleId == pin.RuleId && x.Revision == pin.QualificationRevision, ct)) causes.Add("QUALIFICATION_CHANGED");
            var revision = await db.RuleRevisions.AsNoTracking().SingleAsync(x => x.Id == rule.RuleId && x.Revision == rule.RuleRevision, ct);
            if (await Qualification(db, revision, ct) == null) causes.Add("SOURCE_REVIEW_REQUIRED");
            var q = await db.RegulatoryQualificationEvents.AsNoTracking().SingleAsync(x => x.RuleId == pin.RuleId && x.Revision == pin.QualificationRevision, ct);
            var parsed = ApplicabilityEngine.Parse(revision.RuleJson!);
            if (q.State != "IN_FORCE" || !ApplicabilityEngine.Date(parsed.Lifecycle.EffectiveFrom, out var from) || DateOnly.FromDateTime(now.UtcDateTime) < from ||
                parsed.Lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(parsed.Lifecycle.EffectiveTo, out var to) && DateOnly.FromDateTime(now.UtcDateTime) > to) causes.Add("SOURCE_NOT_CURRENT");
            if (pin.AttestationId != null && await db.RegulatoryFactAttestations.AnyAsync(x => x.Id == pin.AttestationId && x.ValidUntil <= now, ct)) causes.Add("ATTESTATION_EXPIRED");
        }
        if (snapshot.RootElement.TryGetProperty("qualifications", out var qualifications))
            foreach (var q in qualifications.EnumerateArray())
                foreach (var field in q.GetProperty("facts").EnumerateObject()) {
                    var until = field.Value.GetProperty("freshUntil");
                    if (until.ValueKind == JsonValueKind.String && until.GetDateTimeOffset() <= now) causes.Add("INPUT_STALE");
                }
        return causes.Order(StringComparer.Ordinal).ToArray();
    }
}
