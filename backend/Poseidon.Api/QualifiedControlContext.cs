using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class QualifiedControlContext
{
    internal static async Task<(string[] Blockers, QualifiedControlPolicy[] Policies)> Resolve(PoseidonDbContext db, Guid vessel,
        RequirementPin[] pins, string kind, ControlEvidenceContent content, bool file, DateTimeOffset now, CancellationToken ct, FederationSettings settings)
    {
        var blockers = new HashSet<string>(); var policies = new List<QualifiedControlPolicy>();
        if ((content.Operation?.JobId ?? content.ServiceAuthorization?.JobId) is { } jobId) {
            var job = await db.TugJobs.AsNoTracking().SingleOrDefaultAsync(j => j.VesselId == vessel && j.Id == jobId, ct);
            if (job == null || TugRequestPolicy.Missing(job.Inputs()).Length != 0) blockers.Add("OWNING_JOB_INCOMPLETE");
        }
        if (content.TugCapability is { } capability) {
            if (!TugCapability.Complete(capability)) blockers.Add("TUG_CAPABILITY_INCOMPLETE");
            if (capability.TestAt > now) blockers.Add("TUG_CAPABILITY_TEST_FUTURE");
        }
        if (pins.Length == 0) blockers.Add("REQUIREMENT_LINK_MISSING");
        foreach (var pin in pins) {
            var rule = await db.RuleRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pin.RuleId && x.Revision == pin.RuleRevision, ct);
            if (rule == null || rule.SourceId != pin.SourceId || rule.SourceRevision != pin.SourceRevision || rule.RequirementId != pin.RequirementId || rule.RequirementRevision != pin.RequirementRevision ||
                !await db.RuleDrafts.AnyAsync(x => x.Id == rule.Id && x.Revision == rule.Revision, ct)) { blockers.Add("APPLICABILITY_LINK_MISSING"); continue; }
            var q = await CurrentRegulatoryContext.Qualification(db, rule, ct);
            if (q?.State != "IN_FORCE") { blockers.Add("SOURCE_REVIEW_REQUIRED"); continue; }
            if (OperationEquipmentInputs.Present(content) && !TugOwnedCapacityConsumer.CurrentQualifier(settings, q, now, now)) { blockers.Add("SOURCE_REVIEW_REQUIRED"); continue; }
            var policy = RegulatoryQualificationEndpoints.Content(q).Control;
            if (OperationEquipmentInputs.Present(content) && RegulatoryQualificationEndpoints.Content(q).OwningDocuments?.Any(p => p.DocumentKind == OperationEquipmentInputs.Kind(content) && p.AuthorRole == content.OwnerRole) != true) blockers.Add("OWNING_DOCUMENT_POLICY_REQUIRED");
            if (policy == null) { blockers.Add("CONTROL_POLICY_MISSING"); continue; }
            policies.Add(policy);
            if (kind == "Control" && (content.OwnerRole != policy.OwnerRole || content.Frequency != policy.Frequency || content.EvidencePolicy != policy.EvidencePolicy || content.VerificationPolicy != policy.VerificationPolicy)) blockers.Add("QUALIFIED_CONTROL_POLICY_MISMATCH");
            if (kind == "Evidence") {
                if (policy.RequiredVerificationMechanism != null) blockers.Add("EVIDENCE_MECHANISM_UNAVAILABLE");
                if (policy.RequireFile == true && !file) blockers.Add("CLEAN_FILE_MISSING");
                if (content.ObservedAt == null || content.ObservedAt > now || content.ValidFrom > now || content.ValidUntil <= now || content.FreshUntil <= now ||
                    policy.MaxAgeHours != null && (content.ObservedAt.Value.AddHours(policy.MaxAgeHours.Value) <= now || content.FreshUntil > content.ObservedAt.Value.AddHours(policy.MaxAgeHours.Value))) blockers.Add("QUALIFIED_FRESHNESS_REQUIRED");
                if (policy.RequireAcknowledgment == true && content.Acknowledgments?.Any(a => a.RuleId == rule.Id && a.QualificationHash == q.Hash && a.SourceHash == q.SourceHash) != true) blockers.Add("SOURCE_ACKNOWLEDGMENT_REQUIRED");
            }
        }
        // Owning-document verification cannot depend on the facts it will produce.
        // Its independently applicable source/control evaluation remains selectable
        // after a later consumer evaluation has captured this document.
        var owningDocument = OperationEquipmentInputs.Present(content);
        var ruleIds = pins.Where(p => p.RuleId != null).Select(p => p.RuleId!.Value).Distinct().ToArray();
        var evaluation = await db.ApplicabilityEvaluations.AsNoTracking().Where(x => x.VesselId == vessel && (!owningDocument || !x.IncludesOwningInputs) &&
            db.EvaluationQualifications.Any(q => q.EvaluationId == x.Id) && db.EvaluationRules.Count(r => r.EvaluationId == x.Id && ruleIds.Contains(r.RuleId)) == ruleIds.Length)
            .OrderByDescending(x => x.SavedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (evaluation == null) blockers.Add("AUTHORITATIVE_EVALUATION_UNAVAILABLE");
        else {
            foreach (var cause in await CurrentRegulatoryContext.Stale(db, evaluation.Id, vessel, now, ct, settings)) blockers.Add(cause);
            var result = JsonSerializer.Deserialize<EvaluationDto>(evaluation.ResultJson, RegulatoryQualificationEndpoints.Json)!;
            if (!result.Authoritative || result.Conflicts.Length > 0) blockers.Add("APPLICABILITY_REVIEW_REQUIRED");
            foreach (var pin in pins)
                if (!result.Results.Any(r => r.RuleId == pin.RuleId && r.Revision == pin.RuleRevision && r.Prediction.Authoritative && r.Prediction.Result == "APPLICABLE")) blockers.Add("APPLICABILITY_REVIEW_REQUIRED");
        }
        return (blockers.Order(StringComparer.Ordinal).ToArray(), policies.ToArray());
    }
    internal static async Task GuardInputs(PoseidonDbContext db, Guid vessel, RequirementPin[] pins, CancellationToken ct)
    {
        foreach (var pin in pins) {
            var q = await db.RegulatoryQualificationHeads.SingleAsync(x => x.RuleId == pin.RuleId, ct);
            db.Entry(q).Property(x => x.Revision).IsModified = true;
            var rule = await db.RuleDrafts.SingleAsync(x => x.Id == pin.RuleId, ct);
            db.Entry(rule).Property(x => x.Revision).IsModified = true;
            foreach (var (kind, id) in new[] { ("Source", pin.SourceId), ("Requirement", pin.RequirementId) }) {
                var input = await db.RegisterDrafts.SingleAsync(x => x.Kind == kind && x.Id == id, ct);
                db.Entry(input).Property(x => x.Revision).IsModified = true;
            }
        }
        var profile = await db.RegulatoryDrafts.SingleAsync(x => x.VesselId == vessel, ct);
        db.Entry(profile).Property(x => x.Revision).IsModified = true;
        // Serialize input changes without interpreting a no-op xmin as a changed
        // vessel fact; evaluations pin the separate semantic vessel context hash.
        var vesselHead = await db.Vessels.SingleAsync(x => x.Id == vessel, ct);
        db.Entry(vesselHead).Property(x => x.Name).IsModified = true;
    }
}
