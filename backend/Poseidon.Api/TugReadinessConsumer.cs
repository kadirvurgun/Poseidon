using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class TugReadinessConsumer
{
    internal static async Task<TugReadinessFact[]> Read(PoseidonDbContext db, TrustedActor actor, Guid tug, TugPlanInputs plan, DateTimeOffset now, CancellationToken ct, CrewFilePolicy? files = null) {
        var result = new List<TugReadinessFact>();
        if (!await actor.Scope(db.Vessels).AnyAsync(v => v.Id == tug && v.Kind == VesselKind.Tug, ct)) return [];
        // No names, medical fields, certificate numbers or hidden counts in this projection.
        if (!CredentialEndpoints.Permitted(actor, tug, false)) result.Add(new(tug, "Crew", null, null, "Unavailable", "CurrentCrewCertificateAccessRequired"));
        else {
            var date = DateOnly.FromDateTime((plan.PlannedEnd ?? now).UtcDateTime);
            var from = DateOnly.FromDateTime((plan.PlannedStart ?? now).UtcDateTime);
            var assignments = await db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == tug && (a.From == null || a.From <= date) && (a.To == null || a.To >= from)).ToArrayAsync(ct);
            if (assignments.Length == 0) result.Add(new(tug, "Crew", null, null, "Unknown", "CrewCoverageMissing"));
            foreach (var a in assignments) {
                result.Add(new(tug, "CrewAssignment", a.Id, a.Revision, a.ContractEndDate < date || a.To < date ? "Blocked" : "Unknown",
                    a.ContractEndDate < date || a.To < date ? "DeclaredAssignmentDoesNotCoverWindow" : "ManningFitnessRestCompetenceQualificationRequired", a.SourceObservedAt));
                var canReadDocument = files?.Allows(actor, false) == true && actor.Permissions.Contains("file.read") && files.Kinds.Contains("CertificateDocument");
                var version = files?.Version; var purpose = files?.Purpose;
                // Apply the SAME owning person/file/policy restriction as the certificate endpoint before counts/projection.
                var credentials = await db.Credentials.AsNoTracking().Where(c => c.VesselId == tug && c.AssignmentId == a.Id && c.PersonId == a.PersonId &&
                    (c.DocumentReference == null || canReadDocument && db.FileStages.Any(f => f.Id == c.DocumentReference && f.VesselId == tug && f.PersonId == a.PersonId &&
                        f.PersonalKind == "CertificateDocument" && f.PersonalPolicyVersion == version && f.PersonalPurpose == purpose && f.State == "CleanTransport" &&
                        db.StoredFiles.Any(s => s.Id == f.Id && s.VesselId == tug)))).ToArrayAsync(ct);
                if (credentials.Length == 0) result.Add(new(tug, "CrewCertificate", null, null, "Unknown", "CertificateCoverageMissing"));
                foreach (var c in credentials) result.Add(new(tug, "CrewCertificate", c.Id, c.Revision,
                    c.ExpiryDate < date || c.EndorsementRequired == true && c.EndorsementExpiry < date ? "Blocked" : "Unknown",
                    c.ExpiryDate < date || c.EndorsementRequired == true && c.EndorsementExpiry < date ? "DeclaredCertificateExpiresBeforeWindowEnd" : "CredentialQualificationRequired"));
            }
        }
        if (!actor.Permissions.Contains("equipment.read") || !actor.Permissions.Contains("maintenance.read")) result.Add(new(tug, "EquipmentPms", null, null, "Unavailable", "CurrentEquipmentMaintenanceAccessRequired"));
        else {
            var assets = await db.Assets.AsNoTracking().Where(a => a.VesselId == tug).ToArrayAsync(ct);
            if (assets.Length == 0) result.Add(new(tug, "Equipment", null, null, "Unknown", "EquipmentCoverageMissing"));
            foreach (var asset in assets) {
                result.Add(new(tug, "Equipment", asset.Id, asset.Revision, "Unknown", "EquipmentConditionQualificationRequired"));
                var plans = await db.MaintenancePlans.AsNoTracking().Where(p => p.VesselId == tug && p.AssetId == asset.Id).ToArrayAsync(ct);
                foreach (var p in plans) {
                    var inputs = JsonSerializer.Deserialize<MaintenanceInputs>(p.ContentJson)!;
                    var due = MaintenanceCalculation.Evaluate(inputs, plan.PlannedEnd ?? now);
                    result.Add(new(tug, "MaintenancePlan", p.Id, p.Revision, due.State is "Overdue" or "Due" ? "Blocked" : "Unknown", due.Reason,
                        inputs.ObservedAt));
                }
            }
        }
        return result.ToArray();
    }
}
