using Poseidon.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// A deliberately closed projection. Never serialize an entity, command, identity claim or ContentJson.
internal sealed record AuditMetadata(Guid? VesselId, Guid? ResourceId, string? Kind,
    long? BeforeRevision, long? AfterRevision, string Summary)
{
    internal static AuditMetadata From(EntityEntry entry)
    {
        Guid? vessel = entry.Entity switch {
            Credential c => c.VesselId, CredentialRevision c => c.VesselId, ImportBatch b => b.VesselId, ImportPreview p => p.VesselId, ImportRowResult r => r.VesselId,
            TugJob j => j.VesselId, TugJobRevision j => j.VesselId, TugOwner o => o.VesselId, TugAssignment a => a.VesselId,
            MaintenanceObservation o => o.VesselId,
            Asset a => a.VesselId, AssetRevision r => r.VesselId, MaintenancePlan p => p.VesselId,
            MaintenancePlanRevision r => r.VesselId, MaintenanceJob j => j.VesselId, TechnicalOwner o => o.VesselId,
            CrewAssignment c => c.VesselId, CrewAssignmentRevision c => c.VesselId, CrewPosition c => c.VesselId,
            Document d => d.VesselId, DocumentVersion d => d.VesselId,
            FileStage f => f.VesselId, StoredFile f => f.VesselId,
            Vessel v => v.Id, VesselProfile p => p.VesselId, TugProfile p => p.VesselId,
            RegulatoryDraft d => d.VesselId, RegulatoryDraftRevision r => r.VesselId,
            CommandReceipt r when r.Action is "ProfileSave" or "VesselCreate" or "VesselUpdate" => r.ResourceId,
            _ => null
        };
        Guid? resource = entry.Entity switch {
            Credential c => c.Id, CredentialRevision c => c.CredentialId, ImportBatch b => b.Id, ImportPreview p => p.BatchId, ImportRowResult r => r.PreviewId,
            TugJob j => j.Id, TugJobRevision j => j.JobId, TugOwner o => o.Id, TugAssignment a => a.JobId,
            MaintenanceObservation o => o.Id, MaintenanceObservationRevocation r => r.ObservationId,
            Asset a => a.Id, AssetRevision r => r.AssetId, MaintenancePlan p => p.Id,
            MaintenancePlanRevision r => r.PlanId, MaintenanceJob j => j.Id, TechnicalOwner o => o.Id,
            CrewPersonalProfile p => p.PersonId, CrewPersonalProfileRevision p => p.PersonId,
            CrewIdentityRevision r => r.PersonId,
            Person p => p.Id, CrewPosition c => c.Id, CrewAssignment c => c.Id, CrewAssignmentRevision c => c.AssignmentId,
            Document d => d.Id, DocumentVersion d => d.DocumentId,
            FileStage f => f.Id, StoredFile f => f.Id,
            Tenant t => t.Id, Company c => c.Id, Vessel v => v.Id,
            RegisterDraft d => d.Id, RegisterRevision r => r.Id, CommandReceipt r => r.ResourceId,
            _ => vessel
        };
        var kind = entry.Entity switch { RegisterDraft d => d.Kind, RegisterRevision r => r.Kind, _ => null };
        var revisionProperty = entry.Entity is Person ? "IdentityRevision" : "Revision";
        var revision = entry.Metadata.FindProperty(revisionProperty) == null ? null : entry.Property(revisionProperty);
        long? before = revision != null && entry.State != EntityState.Added ? (long?)revision.OriginalValue : null;
        long? after = revision != null && entry.State != EntityState.Deleted ? (long?)revision.CurrentValue : null;
        var fields = new List<object>();
        foreach (var property in entry.Properties.OrderBy(p => p.Metadata.Name, StringComparer.Ordinal))
        {
            var name = property.Metadata.Name;
            // Text values, bodies, fingerprints, receipt keys and personal details are never copied.
            if (!AuditValuePolicy.Includes(name)) continue;
            if (entry.State == EntityState.Modified && !property.IsModified) continue;
            string? Value(object? value) => AuditValuePolicy.Value(name, value);
            fields.Add(new { field = name, before = entry.State == EntityState.Added ? null : Value(property.OriginalValue),
                after = entry.State == EntityState.Deleted ? null : Value(property.CurrentValue),
                evidence = AuditValuePolicy.Evidence(name) });
        }
        return new(vessel, resource, kind, before, after, JsonSerializer.Serialize(fields));
    }
}
