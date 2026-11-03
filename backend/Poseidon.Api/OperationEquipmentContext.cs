using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class OperationEquipmentContext
{
    internal static async Task<TugReadinessFact> TugCurrent(PoseidonDbContext db, TrustedActor actor, FederationSettings settings, TugJob job, Guid tug, DateTimeOffset now, CancellationToken ct) {
        if (!OperationEquipmentCommands.OperationRead(actor)) return new(tug, "OperationContext", null, null, "Unavailable", "CurrentOwningInputAccessRequired");
        var rows = await db.OperationEquipmentBindings.AsNoTracking().Where(b => b.VesselId == job.VesselId && b.JobId == job.Id && b.DocumentKind == "Operation" &&
            db.ControlEvidenceHeads.Any(h => h.VesselId == b.VesselId && h.Kind == b.Kind && h.Id == b.Id && h.Revision == b.Revision)).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) return new(tug, "OperationContext", null, null, "Unknown", rows.Length == 0 ? "QualifiedOperationReferenceMissing" : "OperationReferenceAmbiguous");
        var r = rows[0];
        var refs = new OwningInputReferences(OperationId: r.Id);
        if (!await Authorized(db, actor, job.VesselId, refs, ct)) return new(tug, "OperationContext", null, null, "Unavailable", "CurrentOwningInputAccessRequired");
        var pins = await db.ControlEvidenceRequirements.AsNoTracking().Where(p => p.VesselId == job.VesselId && p.Kind == "Evidence" && p.Id == r.Id && p.Revision == r.Revision).ToArrayAsync(ct);
        foreach (var pin in pins) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pin.RuleId && x.Revision == pin.RuleRevision, ct);
            if (rule == null) continue;
            var q = await CurrentRegulatoryContext.Qualification(db, rule, ct); if (q?.State != "IN_FORCE") continue;
            var facts = new SortedDictionary<string, ContextFact>(StringComparer.Ordinal);
            foreach (var field in OperationEquipmentInputs.Fields) facts[field] = new(ApplicabilityEngine.Fields[field], "Unknown", JsonSerializer.SerializeToElement<object?>(null), "OwningReferenceUnqualified", ApplicabilityEngine.AdapterVersion);
            await Apply(db, settings, job.VesselId, q, refs, facts, now, ct);
            if (OperationEquipmentInputs.Fields.Take(4).All(f => facts[f].Knowledge == "Known")) return new(tug, "OperationContext", r.Id, r.Revision, "Current",
                "QualifiedOperationContext;MasterDecisionSeparate", facts["operation.profile"].ObservedAt, facts["operation.profile"].FreshUntil);
        }
        return new(tug, "OperationContext", r.Id, r.Revision, "Unknown", "CurrentQualifiedOperationAndServiceEvidenceRequired");
    }
    internal static async Task<bool> Authorized(PoseidonDbContext db, TrustedActor a, Guid vessel, OwningInputReferences? refs, CancellationToken ct, HashSet<Guid>? upstreamPath = null) {
        if (refs == null) return true;
        if (!await ComplianceInputContext.Authorized(db, a, vessel, refs, ct, upstreamPath)) return false;
        if (!OperationEquipmentCommands.Read(a) || refs.OperationId != null && !a.CanReadTug) return false;
        if (new[] { refs.OperationId, refs.CargoPlanId, refs.WinchFitmentId, refs.ClassPackId, refs.MsmdId }.Concat(refs.RoleAssessmentIds?.Select(x => (Guid?)x) ?? []).Any(x => x == Guid.Empty)) return false;
        foreach (var (id, kind) in new[] { (refs.OperationId, "Operation"), (refs.CargoPlanId, "CargoPlan"), (refs.WinchFitmentId, "WinchFitment"), (refs.ClassPackId, "ClassPack"), (refs.MsmdId, "Msmd") }.Concat((refs.RoleAssessmentIds ?? []).Select(x => ((Guid?)x, "CrewRoleAssessment"))))
            if (id != null && !await OperationEquipmentCommands.Visible(db, a, db.ControlEvidenceRevisions.AsNoTracking()).AnyAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == id &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision) &&
                db.OperationEquipmentBindings.Any(b => b.VesselId == vessel && b.Id == e.Id && b.Revision == e.Revision && b.DocumentKind == kind), ct)) return false;
        if (refs.OperationId != null) {
            var visibleVessels = a.Scope(db.Vessels).Select(v => v.Id);
            if (await db.OperationEquipmentBindings.AnyAsync(b => b.VesselId == vessel && b.Id == refs.OperationId &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == b.Kind && h.Id == b.Id && h.Revision == b.Revision) &&
                db.TugDraftPlanTugs.Any(t => t.VesselId == vessel && t.JobId == b.JobId && t.Revision == b.PlanRevision && !visibleVessels.Contains(t.TugId)), ct)) return false;
        }
        return true;
    }
    internal static async Task<object[]> Provenance(PoseidonDbContext db, Guid vessel, OwningInputReferences? refs, CancellationToken ct, Guid? evaluation = null) {
        if (refs == null) return [];
        var ids = new HashSet<(Guid Id, long? Revision)>(new[] { refs.OperationId, refs.CargoPlanId, refs.WinchFitmentId, refs.ClassPackId, refs.MsmdId }.Concat(refs.RoleAssessmentIds?.Select(x => (Guid?)x) ?? []).Where(x => x != null).Select(x => (x!.Value, (long?)null)));
        var result = new List<object>();
        for (var pass = 0; pass < 2; pass++) {
            var pending = ids.OrderBy(p => p.Id).ThenBy(p => p.Revision).ToArray(); ids.Clear();
            foreach (var pin in pending) {
                var row = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == pin.Id && (pin.Revision != null ? e.Revision == pin.Revision : db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision)), ct);
                var binding = await db.OperationEquipmentBindings.AsNoTracking().SingleAsync(b => b.VesselId == vessel && b.Id == pin.Id && b.Revision == row.Revision, ct);
                if (evaluation != null && !db.EvaluationOwningReferences.Local.Any(p => p.EvaluationId == evaluation && p.Id == row.Id)) db.EvaluationOwningReferences.Add(new() { TenantId = row.TenantId, VesselId = vessel, EvaluationId = evaluation.Value, Id = row.Id, Revision = row.Revision });
                if (binding.ServiceId != null && pass == 0) ids.Add((binding.ServiceId.Value, binding.ServiceRevision));
                var reviews = await db.HumanReviewEvents.AsNoTracking().Where(e => e.VesselId == vessel && e.Kind == "Verification" && e.EvidenceId == pin.Id && e.EvidenceRevision == row.Revision && db.HumanReviewHeads.Any(h => h.VesselId == vessel && h.Id == e.Id && h.Revision == e.Revision))
                    .OrderBy(e => e.Id).ThenBy(e => e.Revision).Select(e => new { e.Id, e.Revision, e.State, e.AuthorityHash, e.ContextHash, e.ValidUntil }).ToArrayAsync(ct);
                var documentHeadRevision = await db.ControlEvidenceHeads.Where(h => h.VesselId == vessel && h.Kind == "Evidence" && h.Id == row.Id).Select(h => h.Revision).SingleAsync(ct);
                var job = binding.JobId == null ? null : await db.TugJobs.AsNoTracking().Where(j => j.VesselId == vessel && j.Id == binding.JobId).Select(j => new { j.Revision, j.Version }).SingleOrDefaultAsync(ct);
                var plan = binding.JobId == null ? null : await db.TugDraftPlans.AsNoTracking().Where(p => p.VesselId == vessel && p.JobId == binding.JobId).Select(p => new { p.Revision, p.Version }).SingleOrDefaultAsync(ct);
                var asset = binding.AssetId == null ? null : await db.Assets.AsNoTracking().Where(a => a.VesselId == vessel && a.Id == binding.AssetId).Select(a => new { a.Revision, a.Version }).SingleOrDefaultAsync(ct);
                var assignments = await db.TugAssignments.AsNoTracking().Where(a => a.VesselId == vessel && a.JobId == binding.JobId).OrderBy(a => a.Id).Select(a => new { a.Id, a.TugId, a.JobRevision, a.DecisionReference, a.RecordedAt }).ToArrayAsync(ct);
                result.Add(new { row.Id, row.Revision, row.Hash, row.FileId, row.FileRevision, row.Checksum, binding.DocumentKind, documentHeadRevision, job, plan, asset, assignmentHash = RegisterStorage.Hash(JsonSerializer.Serialize(assignments)),
                    binding.JobId, binding.JobRevision, binding.PlanRevision, binding.AssetId, binding.AssetRevision, binding.ServiceId, binding.ServiceRevision, reviews });
            }
        }
        result.AddRange(await ComplianceInputContext.Provenance(db, vessel, refs, ct, evaluation));
        return result.ToArray();
    }
    internal static async Task Apply(PoseidonDbContext db, FederationSettings settings, Guid vessel, RegulatoryQualificationEvent qualification,
        OwningInputReferences? refs, SortedDictionary<string, ContextFact> facts, DateTimeOffset now, CancellationToken ct, HashSet<Guid>? ancestors = null) {
        if (refs == null) return;
        var policy = RegulatoryQualificationEndpoints.Content(qualification);
        Task<(ControlEvidenceRevision Row, ControlEvidenceContent Content, string Hash, DateTimeOffset Until)?> Document(Guid? id, string? kind) =>
            QualifiedOwningDocumentReader.Read(db, settings, vessel, id, kind, now, ct, ancestors);
        void Set(string field, object? value, string hash, DateTimeOffset observed, DateTimeOffset until) {
            var input = policy.Inputs?.SingleOrDefault(i => i.Field == field);
            if (value == null || input?.Basis != "HumanAttested" || !TugOwnedCapacityConsumer.CurrentQualifier(settings, qualification, now, now)) return;
            until = new[] { until, settings.Memberships.Single(m => m.TenantId == qualification.TenantId && m.ActorId == qualification.ActorId).RegulatoryAuthorities.Single(g => g.Id == qualification.AuthorityId).ValidUntil }.Min();
            if (input.MaxAgeHours != null) until = new[] { until, observed.AddHours(input.MaxAgeHours.Value) }.Min();
            if (until <= now) return;
            facts[field] = new(ApplicabilityEngine.Fields[field], "Known", JsonSerializer.SerializeToElement(value), "QualifiedOwningDocument", hash, observed, until);
        }
        var classPack = await Document(refs.ClassPackId, "ClassPack");
        if (classPack is { } cp && policy.ClassPack is { } pack && cp.Content.ClassPack!.PackReference == pack.PackReference && cp.Content.ClassPack.Edition == pack.Edition)
            Set("class.rulePack.IACS_M79", cp.Content.ClassPack.IacsM79, cp.Hash, cp.Content.ObservedAt!.Value, cp.Until);
        var msmd = await Document(refs.MsmdId, "Msmd");
        if (msmd is { } mm) Set("regulatory.msmdVerified", true, mm.Hash, mm.Content.ObservedAt!.Value, mm.Until);
        var sourceEvidence = await Document(refs.SourceEvidenceId, null);
        if (sourceEvidence is { } se) Set("evidence.sourceVerified", true, se.Hash, se.Content.ObservedAt!.Value, se.Until);
        if (policy.Eligibility is { } eligibility) {
            var criteria = await QualifiedRoleCriteriaReader.Read(db, settings, vessel, eligibility, refs, now, ct, ancestors);
            if (criteria != null && msmd is { } requiredMsmd &&
                await ComplianceInputContext.ManningCount(db, vessel, requiredMsmd.Content.Msmd!, criteria.Subject, now, ct) is { } countMet)
                Set("crew.requiredRoleEligible", criteria.Value && countMet,
                    RegisterStorage.Hash(JsonSerializer.Serialize(new { hashes = criteria.Hashes, requiredMsmd.Hash, eligibility })),
                    criteria.ObservedAt, new[] { criteria.Until, requiredMsmd.Until }.Min());
        }
        var ism = await ComplianceInputContext.Ism(db, settings, vessel, qualification, refs.Ism, now, ct, ancestors);
        if (ism is { } upstream) Set("regulatory.ismApplicable", upstream.Value, upstream.Hash, upstream.Observed, upstream.Until);
        var cargo = await Document(refs.CargoPlanId, "CargoPlan");
        if (cargo is { } cg) Set("cargo.packagedDangerousGoods", cg.Content.CargoPlan!.PackagedDangerousGoods, cg.Hash, cg.Content.ObservedAt!.Value, cg.Until);
        var winch = await Document(refs.WinchFitmentId, "WinchFitment");
        if (winch is { } wf) Set("equipment.towingWinchFitted", wf.Content.WinchFitment!.Fitted, wf.Hash, wf.Content.ObservedAt!.Value, wf.Until);
        var operation = await Document(refs.OperationId, "Operation");
        if (operation is not { } op) return;
        var reference = op.Content.Operation!;
        var service = await Document(reference.ServiceAuthorizationId, "ServiceAuthorization");
        if (service is not { } sa || sa.Row.Revision != reference.ServiceAuthorizationRevision) return;
        var auth = sa.Content.ServiceAuthorization!;
        if (auth.JobId != reference.JobId || auth.JobRevision != reference.JobRevision || auth.JobVersion != reference.JobVersion || auth.PlanRevision != reference.PlanRevision || auth.PlanVersion != reference.PlanVersion) return;
        var job = await db.TugJobs.AsNoTracking().SingleAsync(j => j.Id == reference.JobId && j.VesselId == vessel, ct);
        if (TugRequestPolicy.Missing(job.Inputs()).Length != 0) return;
        var plan = await db.TugDraftPlans.AsNoTracking().SingleAsync(p => p.JobId == job.Id && p.VesselId == vessel, ct);
        if (plan.PlannedStart == null || plan.PlannedEnd == null || plan.PlannedStart > now || plan.PlannedEnd <= now) return;
        var assignments = await db.TugAssignments.AsNoTracking().Where(a => a.VesselId == vessel && a.JobId == job.Id).OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.TugId, a.JobRevision, a.DecisionReference, a.RecordedAt }).ToArrayAsync(ct);
        var hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { operation = op.Hash, service = sa.Hash, job.Revision, job.Version, reference.PlanRevision, reference.PlanVersion, assignments }));
        var expiry = new[] { op.Until, sa.Until, plan.PlannedEnd.Value }.Min();
        var observed = new[] { op.Content.ObservedAt!.Value, sa.Content.ObservedAt!.Value }.Min();
        foreach (var (field, raw) in new[] { ("vessel.operatingArea", job.OperationArea), ("operation.jobType", job.JobType), ("operation.profile", reference.Profile), ("operation.serviceContext", auth.ServiceContext) })
            Set(field, OperationEquipmentInputs.Map(policy.LiteralBindings, field, raw), hash, observed, expiry);
    }
    internal static bool CurrentVerifier(FederationSettings settings, HumanReviewEvent e, string kind, DateTimeOffset now) {
        var members = settings.Memberships.Where(m => m.ActorId == e.ActorId && m.TenantId == e.TenantId).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        var m = members[0];
        if (m.Issuer != settings.Issuer || string.IsNullOrWhiteSpace(m.Subject) || settings.Memberships.Count(x => x.Issuer == m.Issuer && x.Subject == m.Subject) != 1 ||
            !new[] { "human.verify", "human.review.read", "directory.read", "directory.write", "control.read", "evidence.read", "file.read", "applicability.read", "regulatory.draft.read", "equipment.read" }.All(m.Permissions.Contains) ||
            !(m.TenantWideRead || m.TenantWideManage) || !(m.TenantWideManage || m.VesselGrants?.Any(g => g.VesselId == e.VesselId && g.CanEdit) == true) ||
            kind == "CrewRoleAssessment" && !ComplianceInputContext.CrewPermissions.All(m.Permissions.Contains) ||
            kind is "Operation" or "ServiceAuthorization" && (!m.Permissions.Contains("mode.tug") || !m.Permissions.Contains("tug.read")) ||
            ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(e.RoleId.ToString("D")) != true) return false;
        var grants = m.HumanAuthorities.Where(g => g.Id == e.AuthorityId).ToArray();
        if (grants.Length != 1) return false;
        var grant = grants[0];
        return grant.Capability == "human.verify" && grant.VesselId == e.VesselId && grant.EvidenceId == e.EvidenceId && grant.ControlId == e.ControlId && grant.OperationId == e.OperationId && grant.RoleId == e.RoleId &&
            grant.ValidFrom <= now && now < grant.ValidUntil && RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = e.ActorId, tenantId = e.TenantId, actorKind = "Human" })) == e.AuthorityHash;
    }
}
