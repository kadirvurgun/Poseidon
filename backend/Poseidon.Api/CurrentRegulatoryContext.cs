using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class CurrentRegulatoryContext
{
    // Recheck a recorded profile attestation's real, current server appointment.
    // Historical author provenance is retained even after its grant is withdrawn.
    internal static bool AttestationCurrent(FederationSettings settings, RegulatoryFactAttestation e, DateTimeOffset now)
    {
        if (e.ValidUntil <= now || e.SavedAt > now) return false;
        var members = settings.Memberships.Where(m => m.ActorId == e.ActorId && m.TenantId == e.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        var m = members[0];
        if (m.Issuer != settings.Issuer || string.IsNullOrWhiteSpace(m.Subject) || settings.Memberships.Count(x => x.Issuer == m.Issuer && x.Subject == m.Subject) != 1) return false;
        if (JsonSerializer.Deserialize<string[]>(e.FieldsJson)?.Contains("vessel.contractForConstructionDate") == true && !m.Permissions.Contains("file.read")) return false;
        if (!new[] { "directory.read", "directory.write", "regulatory.draft.read", "regulatory.facts.attest" }.All(m.Permissions.Contains) ||
            !(m.TenantWideRead || m.TenantWideManage) || !(m.TenantWideManage || m.VesselGrants?.Any(g => g.VesselId == e.VesselId && g.CanEdit) == true) ||
            ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(e.RoleId.ToString("D")) != true) return false;
        var grants = m.RegulatoryAuthorities.Where(g => g.Id == e.AuthorityId).ToArray();
        if (grants.Length != 1) return false;
        var grant = grants[0];
        return grant.RuleId == e.RuleId && grant.VesselId == e.VesselId && grant.RoleId == e.RoleId && grant.Actions.Contains("attest") &&
            grant.ValidFrom <= now && now < grant.ValidUntil &&
            RegisterStorage.Hash(JsonSerializer.Serialize(new { g = grant, Id = e.ActorId, TenantId = e.TenantId }, RegulatoryQualificationEndpoints.Json)) == e.AuthorityHash;
    }
    internal static string VesselHash(Vessel v) => RegisterStorage.Hash(JsonSerializer.Serialize(new { v.Id, v.Kind }));
    internal static async Task<RegulatoryQualificationEvent?> Qualification(PoseidonDbContext db, RuleRevision rule, CancellationToken ct)
    {
        var q = await db.RegulatoryQualificationEvents.AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == rule.Id && x.RuleRevision == rule.Revision &&
            db.RegulatoryQualificationHeads.Any(h => h.RuleId == x.RuleId && h.Revision == x.Revision), ct);
        if (q == null || q.State is "DRAFT" or "IN_REVIEW" || RegulatoryQualificationPolicy.Missing(RegulatoryQualificationEndpoints.Content(q)).Length != 0 ||
            JsonSerializer.Deserialize<RegulatorySourceContract>(q.ContractJson, RegulatoryQualificationEndpoints.Json)?.RequiredMechanism != null) return null;
        try { RegulatoryQualificationPolicy.Validate(RegulatoryQualificationEndpoints.Content(q)); }
        catch (ArgumentException) { return null; }
        foreach (var (kind, id, revision) in new[] { ("Source", rule.SourceId, rule.SourceRevision), ("Requirement", rule.RequirementId, rule.RequirementRevision) })
            if (!await db.RegisterDrafts.AnyAsync(x => x.Kind == kind && x.Id == id && x.Revision == revision, ct)) return null;
        return q;
    }
    internal static async Task<(SortedDictionary<string, ContextFact> Facts, RegulatoryFactAttestation? Attestation)> Facts(PoseidonDbContext db,
        RegulatoryQualificationEvent q, Guid vessel, long? profile, SortedDictionary<string, ContextFact> declared, DateTimeOffset now, CancellationToken ct, FederationSettings settings)
    {
        var facts = new SortedDictionary<string, ContextFact>(declared, StringComparer.Ordinal);
        var policy = RegulatoryQualificationEndpoints.Content(q);
        var attestation = await db.RegulatoryFactAttestations.AsNoTracking().Where(x => x.RuleId == q.RuleId && x.QualificationRevision == q.Revision && x.VesselId == vessel && x.ProfileRevision == profile && x.SavedAt <= now)
            .OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        var attested = attestation == null ? [] : JsonSerializer.Deserialize<string[]>(attestation.FieldsJson)!;
        foreach (var input in policy.Inputs ?? []) {
            var f = facts[input.Field];
            if (input.Basis == "ProviderVerified") { facts[input.Field] = f with { Knowledge = f.Knowledge == "Known" ? "Unverified" : f.Knowledge, Provenance = "RequiredProviderUnavailable" }; continue; }
            if (f.Knowledge != "Unverified") continue;
            if (input.Basis == "Declared" && input.MaxAgeHours != null && f.ObservedAt == null) facts[input.Field] = f with { Knowledge = "Unverified", Provenance = "FreshnessObservationMissing" };
            else if (input.Basis == "Declared" && RegulatoryQualificationPolicy.ProfileFields.Contains(input.Field) && !RegulatoryQualificationPolicy.VerifiedProfileFields.Contains(input.Field))
                facts[input.Field] = f with { Knowledge = "Known", FreshUntil = input.MaxAgeHours == null ? null : f.ObservedAt?.AddHours(input.MaxAgeHours.Value) };
            else if (input.Basis == "HumanAttested" && attestation != null && AttestationCurrent(settings, attestation, now) && attested.Contains(input.Field))
                facts[input.Field] = f with { Knowledge = "Known", Provenance = "RecordedHumanAttestation", Revision = attestation.Hash,
                    ObservedAt = attestation.SavedAt, FreshUntil = input.MaxAgeHours == null ? attestation.ValidUntil : new[] { attestation.ValidUntil, attestation.SavedAt.AddHours(input.MaxAgeHours.Value) }.Min() };

        }
        facts["source.lifecycle"] = new(RuleFieldType.Text, "Known", JsonSerializer.SerializeToElement(q.State), "RecordedSourceQualification", q.Hash, q.SavedAt);
        return (facts, attestation);
    }
    internal static async Task<string[]> Stale(PoseidonDbContext db, Guid id, Guid vessel, DateTimeOffset now, CancellationToken ct, FederationSettings settings, HashSet<Guid>? ancestors = null)
    {
        var path = ancestors == null ? new HashSet<Guid>() : new HashSet<Guid>(ancestors);
        if (path.Count >= 16 || !path.Add(id)) return ["CIRCULAR_INPUT_LINEAGE"];
        var evaluation = await db.ApplicabilityEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.VesselId == vessel, ct);
        if (evaluation == null) return ["EVALUATION_UNAVAILABLE"];
        var pins = await db.EvaluationQualifications.AsNoTracking().Where(x => x.EvaluationId == id).ToArrayAsync(ct);
        if (pins.Length == 0) return ["QUALIFICATION_LINEAGE_UNAVAILABLE"];
        var causes = new HashSet<string>();
        var inputProfile = await db.RegulatoryDraftRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == vessel && x.Revision == evaluation.ProfileRevision, ct);
        if (inputProfile?.ConstructionFileId != null && !await db.StoredFiles.AnyAsync(f => f.VesselId == vessel && f.Id == inputProfile.ConstructionFileId && f.Revision == inputProfile.ConstructionFileRevision && f.Checksum == inputProfile.ConstructionFileChecksum &&
            db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"), ct)) causes.Add("CONSTRUCTION_RECORD_CHANGED");
        if (!await db.RegulatoryDrafts.AnyAsync(x => x.VesselId == vessel && x.Revision == evaluation.ProfileRevision, ct)) causes.Add("PROFILE_CHANGED");
        using var snapshot = JsonDocument.Parse(evaluation.SnapshotJson);
        if (snapshot.RootElement.TryGetProperty("owningInputs", out var owning) && owning.ValueKind == JsonValueKind.Object) {
            var refs = owning.Deserialize<OwningInputReferences>(RegulatoryQualificationEndpoints.Json)!;
            if (snapshot.RootElement.TryGetProperty("owningProvenance", out var provenance)) {
                // Changed heads/reviews also invalidate retained Unknown observations.
                var currentIds = ComplianceInputContext.DocumentIds(refs);
                var available = await db.ControlEvidenceHeads.CountAsync(b => b.VesselId == vessel && b.Kind == "Evidence" && currentIds.Contains(b.Id), ct);
                if (available != currentIds.Length) causes.Add("OWNING_INPUT_CHANGED");
                else if (JsonSerializer.SerializeToElement(await OperationEquipmentContext.Provenance(db, vessel, refs, ct), RegulatoryQualificationEndpoints.Json).GetRawText() != provenance.GetRawText()) causes.Add("OWNING_INPUT_CHANGED");
            }
            foreach (var capture in snapshot.RootElement.GetProperty("qualifications").EnumerateArray()) {
                var q = await db.RegulatoryQualificationEvents.AsNoTracking().SingleAsync(x => x.RuleId == capture.GetProperty("ruleId").GetGuid() && x.Revision == capture.GetProperty("revision").GetInt64(), ct);
                var original = capture.GetProperty("facts").Deserialize<SortedDictionary<string, ContextFact>>(RegulatoryQualificationEndpoints.Json)!;
                var current = new SortedDictionary<string, ContextFact>(original, StringComparer.Ordinal);
                foreach (var field in OperationEquipmentInputs.Fields.Concat(ComplianceInputs.Fields)) current[field] = new(ApplicabilityEngine.Fields[field], "Unknown", JsonSerializer.SerializeToElement<object?>(null), "OwningReferenceUnqualified", ApplicabilityEngine.AdapterVersion);
                await OperationEquipmentContext.Apply(db, settings, vessel, q, refs, current, now, ct, path);
                if (OperationEquipmentInputs.Fields.Concat(ComplianceInputs.Fields).Any(f => original[f].Knowledge != current[f].Knowledge || original[f].Value.GetRawText() != current[f].Value.GetRawText() || original[f].Revision != current[f].Revision)) causes.Add("OWNING_INPUT_CHANGED");
            }
        }
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
            if (pin.AttestationId != null) {
                var attestation = await db.RegulatoryFactAttestations.AsNoTracking().SingleAsync(x => x.Id == pin.AttestationId, ct);
                if (!AttestationCurrent(settings, attestation, now)) causes.Add("ATTESTATION_AUTHORITY_CHANGED");
            }
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
