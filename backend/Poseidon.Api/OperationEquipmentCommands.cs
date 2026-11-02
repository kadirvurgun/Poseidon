using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class OperationEquipmentCommands
{
    internal static bool Read(TrustedActor a) => ControlEvidenceEndpoints.Read(a) && a.Permissions.Contains("human.review.read") &&
        a.Permissions.Contains("equipment.read");
    internal static bool OperationRead(TrustedActor a) => Read(a) && a.CanReadTug;
    internal static async Task<bool> VerificationAllowed(PoseidonDbContext db, FederationSettings settings, ControlEvidenceContent content, RequirementPin[] pins, Guid role, DateTimeOffset now, CancellationToken ct) {
        if (!OperationEquipmentInputs.Present(content)) return true;
        foreach (var pin in pins) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == pin.RuleId && r.Revision == pin.RuleRevision, ct);
            var q = rule == null ? null : await CurrentRegulatoryContext.Qualification(db, rule, ct);
            var p = q == null ? null : RegulatoryQualificationEndpoints.Content(q).OwningDocuments?.SingleOrDefault(p => p.DocumentKind == OperationEquipmentInputs.Kind(content));
            if (p == null || p.VerifierRole != role || q?.State != "IN_FORCE" || !TugOwnedCapacityConsumer.CurrentQualifier(settings, q, now, now)) return false;
        }
        return pins.Length > 0;
    }
    internal static async Task<bool> SubmissionAllowed(PoseidonDbContext db, FederationSettings settings, TrustedActor a, ControlEvidenceContent content, RequirementPin[] pins, CancellationToken ct) {
        if (!OperationEquipmentInputs.Present(content)) return true;
        var members = settings.Memberships.Where(m => m.TenantId == a.TenantId && m.ActorId == a.Id).ToArray();
        if (members.Length != 1 || members[0].ActorKind != "Human") return false;
        foreach (var pin in pins) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == pin.RuleId && r.Revision == pin.RuleRevision, ct);
            var q = rule == null ? null : await CurrentRegulatoryContext.Qualification(db, rule, ct);
            var policy = q == null ? null : RegulatoryQualificationEndpoints.Content(q).OwningDocuments?.SingleOrDefault(p => p.DocumentKind == OperationEquipmentInputs.Kind(content));
            if (policy == null || q?.State != "IN_FORCE" || !TugOwnedCapacityConsumer.CurrentQualifier(settings, q, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) || a.RoleContext?.Split(',').Contains(policy.AuthorRole.GetValueOrDefault().ToString("D")) != true || policy.AuthorRole != content.OwnerRole) return false;
        }
        return pins.Length > 0;
    }
    internal static IQueryable<ApplicabilityEvaluation> Evaluations(PoseidonDbContext db, TrustedActor a) {
        var visible = a.Scope(db.Vessels).Select(v => v.Id);
        return db.ApplicabilityEvaluations.AsNoTracking().Where(e =>
        (!e.IncludesOwningInputs || Read(a)) && (!e.IncludesOperationInputs || a.CanReadTug) &&
        !db.EvaluationOwningReferences.Any(pin => pin.EvaluationId == e.Id && db.OperationEquipmentBindings.Any(b => b.VesselId == pin.VesselId && b.Kind == pin.Kind && b.Id == pin.Id && b.Revision == pin.Revision &&
            (db.TugDraftPlanTugs.Any(t => t.VesselId == b.VesselId && t.JobId == b.JobId && t.Revision == b.PlanRevision && !visible.Contains(t.TugId)) ||
                db.TugAssignments.Any(t => t.VesselId == b.VesselId && t.JobId == b.JobId && !visible.Contains(t.TugId))))));
    }
    // Apply before paging/history/receipt on every graph surface, including linked controls.
    internal static IQueryable<ControlEvidenceRevision> Visible(PoseidonDbContext db, TrustedActor a, IQueryable<ControlEvidenceRevision> rows) {
        var visible = a.Scope(db.Vessels).Select(v => v.Id);
        return rows.Where(r =>
        !db.OperationEquipmentBindings.Any(b => b.VesselId == r.VesselId && b.Kind == r.Kind && b.Id == r.Id && b.Revision == r.Revision &&
            (db.TugDraftPlanTugs.Any(t => t.VesselId == b.VesselId && t.JobId == b.JobId && t.Revision == b.PlanRevision && !visible.Contains(t.TugId)) ||
                db.TugAssignments.Any(t => t.VesselId == b.VesselId && t.JobId == b.JobId && !visible.Contains(t.TugId)))) &&
        !db.OperationEquipmentBindings.Any(b => b.VesselId == r.VesselId && b.Kind == r.Kind && b.Id == r.Id && b.Revision == r.Revision &&
            (!Read(a) || (b.DocumentKind == "Operation" || b.DocumentKind == "ServiceAuthorization") && !a.CanReadTug)) &&
        !db.ControlEvidenceEdges.Any(edge => edge.VesselId == r.VesselId && edge.Kind == r.Kind && edge.Id == r.Id && edge.Revision == r.Revision &&
            db.OperationEquipmentBindings.Any(b => b.VesselId == edge.VesselId && b.Kind == edge.TargetKind && b.Id == edge.TargetId && b.Revision == edge.TargetRevision &&
                (!Read(a) || (b.DocumentKind == "Operation" || b.DocumentKind == "ServiceAuthorization") && !a.CanReadTug))));
    }
    internal static async Task<bool> Check(PoseidonDbContext db, TrustedActor a, Guid vessel, Guid? id, ControlEvidenceContent c, CancellationToken ct) {
        if (!OperationEquipmentInputs.Present(c)) return true;
        if (!Read(a) || !a.CanEditVessel(vessel) || !await a.Scope(db.Vessels).AnyAsync(v => v.Id == vessel, ct)) return false;
        var jobId = c.Operation?.JobId ?? c.ServiceAuthorization?.JobId;
        var revision = c.Operation?.JobRevision ?? c.ServiceAuthorization?.JobRevision;
        var version = c.Operation?.JobVersion ?? c.ServiceAuthorization?.JobVersion;
        var planRevision = c.Operation?.PlanRevision ?? c.ServiceAuthorization?.PlanRevision;
        var planVersion = c.Operation?.PlanVersion ?? c.ServiceAuthorization?.PlanVersion;
        if (c.Operation != null || c.ServiceAuthorization != null) {
            if (!a.CanReadTug) return false;
            if (jobId != null && !await db.TugJobs.AnyAsync(j => j.Id == jobId && j.VesselId == vessel && j.Revision == revision && j.Version == version, ct)) return false;
            if (planRevision != null) {
                var plan = await db.TugDraftPlans.AsNoTracking().SingleOrDefaultAsync(p => p.JobId == jobId && p.VesselId == vessel && p.Revision == planRevision && p.Version == planVersion && p.RequestRevision == revision, ct);
                if (plan == null) return false;
                var visible = a.Scope(db.Vessels).Select(v => v.Id);
                if (await db.TugDraftPlanTugs.AnyAsync(t => t.VesselId == vessel && t.JobId == jobId && t.Revision == planRevision && !visible.Contains(t.TugId), ct)) return false;
            }
        }
        if (c.Operation?.ServiceAuthorizationId is { } service) {
            if (service == id) return false;
            var r = await Visible(db, a, db.ControlEvidenceRevisions.AsNoTracking()).SingleOrDefaultAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == service && e.Revision == c.Operation.ServiceAuthorizationRevision &&
                db.ControlEvidenceHeads.Any(h => h.VesselId == vessel && h.Kind == e.Kind && h.Id == e.Id && h.Revision == e.Revision), ct);
            var s = r == null ? null : JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, RegulatoryQualificationEndpoints.Json)?.ServiceAuthorization;
            if (s == null || s.JobId != jobId || s.JobRevision != revision || s.JobVersion != version || s.PlanRevision != planRevision || s.PlanVersion != planVersion) return false;
        }
        if (c.WinchFitment?.AssetId is { } asset && !await db.Assets.AnyAsync(x => x.VesselId == vessel && x.Id == asset && x.Revision == c.WinchFitment.AssetRevision && x.Version == c.WinchFitment.AssetVersion, ct)) return false;
        return true;
    }
}
