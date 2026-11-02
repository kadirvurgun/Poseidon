using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record TugOwnedCapacity(Guid TugId, string State, string? Tonnes, string Why, Guid? EvidenceId = null,
    long? EvidenceRevision = null, Guid? ConditionId = null, long? ConditionRevision = null,
    Guid? RuleId = null, long? QualificationRevision = null, string? QualificationHash = null,
    DateTimeOffset? ValidUntil = null, DateTimeOffset? ObservedAt = null);

internal static class TugOwnedCapacityConsumer
{
    // Rights are checked before querying even a count of protected evidence.
    internal static async Task<TugOwnedCapacity> Read(PoseidonDbContext db, TrustedActor actor, FederationSettings settings,
        Guid tug, TugJob job, TugPlanInputs plan, DateTimeOffset now, CancellationToken ct) {
        TugOwnedCapacity Missing(string state, string why) => new(tug, state, null, why);
        if (!actor.CanReadTug || !ControlEvidenceEndpoints.Read(actor) || !actor.Permissions.Contains("human.review.read") ||
            !actor.Permissions.Contains("equipment.read") || !await actor.Scope(db.Vessels).AnyAsync(v => v.Id == tug && v.Kind == VesselKind.Tug, ct))
            return Missing("Unavailable", "CurrentOwningEvidenceAccessRequired");
        if (plan.PlannedStart == null || plan.PlannedEnd == null) return Missing("Unknown", "PlanWindowMissing");
        // Current heads only; historical verified revisions never become a fallback.
        var rows = await db.ControlEvidenceRevisions.AsNoTracking().Where(e => e.VesselId == tug && e.Kind == "Evidence" &&
            db.ControlEvidenceHeads.Any(h => h.VesselId == tug && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision) &&
            (e.FileId == null || db.StoredFiles.Any(f => f.VesselId == tug && f.Id == e.FileId && f.Revision == e.FileRevision && f.Checksum == e.Checksum) &&
            db.FileStages.Any(s => s.Id == e.FileId && s.VesselId == tug && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport")))
            .ToArrayAsync(ct);
        var tests = rows.Where(r => r.FileId != null && (r.Lifecycle is "SUBMITTED" or "COMPLETED") && Content(r).TugCapability?.Kind == "BollardPullTest").ToArray();
        if (tests.Length != 1) return Missing(tests.Length == 0 ? "Missing" : "ReviewRequired", "CurrentBollardPullEvidenceMustBeUnambiguous");
        var test = tests[0]; var statement = Content(test).TugCapability!;
        TugOwnedCapacity Reject(string state, string why) => new(tug, state, null, why, test.Id, test.Revision, ObservedAt: statement.TestAt);
        var binding = await db.TugCapabilityBindings.AsNoTracking().SingleOrDefaultAsync(b => b.VesselId == tug && b.Id == test.Id && b.Revision == test.Revision, ct);
        if (binding == null || binding.AssetId != statement.AssetId || binding.AssetRevision != statement.AssetRevision || binding.ProfileVersion != statement.ProfileVersion ||
            binding.ConditionId != statement.ConditionEvidenceId || binding.ConditionRevision != statement.ConditionEvidenceRevision || binding.ConditionVersion != statement.ConditionEvidenceVersion)
            return Reject("Unknown", "OwningCapabilityBindingRequired");
        var conditions = rows.Where(r => Content(r).TugCapability?.Kind == "Condition" && r.Id == binding.ConditionId && r.Revision == binding.ConditionRevision).ToArray();
        if (conditions.Length != 1) return Reject("Unknown", "ExactCurrentVerifiedConditionRequired");
        var condition = conditions[0];
        if (!await db.ControlEvidenceHeads.AnyAsync(h => h.VesselId == tug && h.Kind == "Evidence" && h.Id == condition.Id && h.Revision == condition.Revision && h.Version == binding.ConditionVersion, ct))
            return Reject("ChangedInput", "ConditionVersionChanged");
        var profile = await db.TugProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.VesselId == tug, ct);
        if (profile == null || profile.Version != statement.ProfileVersion || !await db.Assets.AnyAsync(a => a.VesselId == tug && a.Id == statement.AssetId && a.Revision == statement.AssetRevision && a.Version == statement.AssetVersion, ct))
            return Reject("ChangedInput", "ProfileOrEquipmentChanged");
        var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == tug && x.Kind == "Evidence" && x.Id == test.Id && x.Revision == test.Revision)
            .Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArrayAsync(ct);
        var qualified = await QualifiedControlContext.Resolve(db, tug, pins, "Evidence", Content(test), true, now, ct, settings);
        if (qualified.Blockers.Length != 0) return Reject("SourceReviewRequired", "CurrentQualifiedEvidencePolicyRequired");
        var formulae = new List<(TugCalculationPolicy Policy, RegulatoryQualificationEvent Source)>();
        foreach (var pin in pins) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleAsync(r => r.Id == pin.RuleId && r.Revision == pin.RuleRevision, ct);
            var q = await CurrentRegulatoryContext.Qualification(db, rule, ct);
            if (q?.State == "IN_FORCE" && RegulatoryQualificationEndpoints.Content(q).TugCalculation is { } formula) formulae.Add((formula, q));
        }
        if (formulae.Count != 1) return Reject("SourceReviewRequired", "ExactQualifiedFormulaRequired");
        var (policy, qualification) = formulae[0];
        if (!CurrentQualifier(settings, qualification, now, plan.PlannedEnd.Value)) return Reject("SourceReviewRequired", "CurrentSourceQualificationAuthorityRequired");
        var ruleRevision = await db.RuleRevisions.AsNoTracking().SingleAsync(r => r.Id == qualification.RuleId && r.Revision == qualification.RuleRevision, ct);
        var lifecycle = ApplicabilityEngine.Parse(ruleRevision.RuleJson!).Lifecycle;
        var expires = new List<DateTimeOffset>();
        var sourceMember = settings.Memberships.Single(m => m.ActorId == qualification.ActorId && m.TenantId == qualification.TenantId);
        expires.Add(sourceMember.RegulatoryAuthorities.Single(g => g.Id == qualification.AuthorityId).ValidUntil);
        if (lifecycle.EffectiveTo != null && ApplicabilityEngine.Date(lifecycle.EffectiveTo, out var effectiveTo)) {
            if (DateOnly.FromDateTime(plan.PlannedEnd.Value.UtcDateTime) > effectiveTo) return Reject("SourceReviewRequired", "FormulaSourceDoesNotCoverPlanWindow");
        }
        foreach (var evidence in new[] { test, condition }) {
            var content = Content(evidence);
            if (evidence.Lifecycle is not ("SUBMITTED" or "COMPLETED") || content.ObservedAt == null || content.ObservedAt > now || content.ValidFrom > now ||
                content.ValidUntil == null || content.ValidUntil <= plan.PlannedEnd || content.FreshUntil == null || content.FreshUntil <= plan.PlannedEnd)
                return Reject("Expired", "EvidenceDoesNotCoverPlanWindow");
            var verification = await db.HumanReviewEvents.AsNoTracking().Where(v => v.VesselId == tug && v.Kind == "Verification" && v.EvidenceId == evidence.Id && v.EvidenceRevision == evidence.Revision &&
                db.HumanReviewHeads.Any(h => h.VesselId == tug && h.Id == v.Id && h.Revision == v.Revision)).OrderByDescending(v => v.SavedAt).ThenBy(v => v.Id).FirstOrDefaultAsync(ct);
            if (verification?.State != "VERIFIED" || verification.ValidUntil <= plan.PlannedEnd || verification.ValidUntil == null ||
                !CurrentVerifier(settings, verification, now, plan.PlannedEnd.Value) || verification.EvaluationId == null ||
                (await CurrentRegulatoryContext.Stale(db, verification.EvaluationId.Value, tug, now, ct, settings)).Length != 0 ||
                !await db.ControlEvidenceHeads.AnyAsync(h => h.VesselId == tug && h.Kind == "Control" && h.Id == verification.ControlId && h.Revision == verification.ControlRevision, ct))
                return Reject("Unverified", "CurrentIndependentVerificationRequired");
            var ep = await db.ControlEvidenceRequirements.AsNoTracking().Where(x => x.VesselId == tug && x.Kind == "Evidence" && x.Id == evidence.Id && x.Revision == evidence.Revision)
                .Select(x => new RequirementPin(x.RequirementId, x.RequirementRevision, x.SourceId, x.SourceRevision, x.RuleId, x.RuleRevision)).ToArrayAsync(ct);
            if ((await QualifiedControlContext.Resolve(db, tug, ep, "Evidence", content, evidence.FileId != null, now, ct, settings)).Blockers.Length != 0)
                return Reject("SourceReviewRequired", "ConditionSourcePolicyChanged");
            expires.Add(verification.ValidUntil.Value);
            expires.Add(settings.Memberships.Single(m => m.ActorId == verification.ActorId && m.TenantId == verification.TenantId)
                .HumanAuthorities.Single(g => g.Id == verification.AuthorityId).ValidUntil);
        }
        var result = TugCapability.Calculate(statement, Content(condition).TugCapability!, policy, job.JobType, job.OperationArea, plan.PlannedStart.Value, plan.PlannedEnd.Value, now);
        return new(tug, result.State, result.Tonnes, result.Why, test.Id, test.Revision, condition.Id, condition.Revision,
            qualification.RuleId, qualification.Revision, qualification.Hash,
            expires.Concat(new[] { Content(test).ValidUntil!.Value, Content(test).FreshUntil!.Value, Content(condition).ValidUntil!.Value, Content(condition).FreshUntil!.Value, statement.ReviewAt ?? now }).Min(), statement.TestAt);
    }
    private static ControlEvidenceContent Content(ControlEvidenceRevision r) => JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, RegulatoryQualificationEndpoints.Json)!;
    private static bool CurrentVerifier(FederationSettings settings, HumanReviewEvent e, DateTimeOffset now, DateTimeOffset through) {
        var members = settings.Memberships.Where(m => m.ActorId == e.ActorId && m.TenantId == e.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        var m = members[0];
        if (!new[] { "human.verify", "human.review.read", "directory.read", "control.read", "evidence.read", "file.read", "applicability.read", "regulatory.draft.read" }.All(m.Permissions.Contains) || !(m.TenantWideRead || m.TenantWideManage) ||
            !m.Permissions.Contains("mode.tug") || !m.Permissions.Contains("tug.read") || !m.Permissions.Contains("equipment.read") || !m.Permissions.Contains("directory.write") ||
            !(m.TenantWideManage || m.VesselGrants?.Any(g => g.VesselId == e.VesselId && g.CanEdit) == true) ||
            ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(e.RoleId.ToString("D")) != true) return false;
        var grants = m.HumanAuthorities.Where(g => g.Id == e.AuthorityId).ToArray();
        if (grants.Length != 1) return false;
        var grant = grants[0];
        return grant.Capability == "human.verify" && grant.RoleId == e.RoleId && grant.VesselId == e.VesselId && grant.EvidenceId == e.EvidenceId && grant.ControlId == e.ControlId &&
            grant.OperationId == e.OperationId && grant.ValidFrom <= now && now < grant.ValidUntil && through < grant.ValidUntil &&
            RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = e.ActorId, tenantId = e.TenantId, actorKind = "Human" })) == e.AuthorityHash;
    }
    private static bool CurrentQualifier(FederationSettings settings, RegulatoryQualificationEvent e, DateTimeOffset now, DateTimeOffset through) {
        var members = settings.Memberships.Where(m => m.ActorId == e.ActorId && m.TenantId == e.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        var m = members[0];
        if (!new[] { "regulatory.qualify", "directory.read", "regulatory.draft.read" }.All(m.Permissions.Contains) || !(m.TenantWideRead || m.TenantWideManage) ||
            ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(e.RoleId.ToString("D")) != true) return false;
        var grants = m.RegulatoryAuthorities.Where(g => g.Id == e.AuthorityId && g.RuleId == e.RuleId && g.RoleId == e.RoleId && g.Actions.Contains("qualify") && g.ValidFrom <= now && now < g.ValidUntil && through < g.ValidUntil).ToArray();
        return grants.Length == 1 && RegisterStorage.Hash(JsonSerializer.Serialize(new { g = grants[0], Id = e.ActorId, TenantId = e.TenantId }, RegulatoryQualificationEndpoints.Json)) == e.AuthorityHash;
    }
}
