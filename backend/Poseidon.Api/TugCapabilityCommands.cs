using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class TugCapabilityCommands
{
    internal static async Task<bool> Check(PoseidonDbContext db, TrustedActor actor, Guid vessel, Guid? id, TugCapabilityStatement statement, bool guard, CancellationToken ct) {
        if (!actor.CanReadTug || !actor.CanEditVessel(vessel) || !actor.Permissions.Contains("equipment.read") ||
            !await actor.Scope(db.Vessels).AnyAsync(v => v.Id == vessel && v.Kind == VesselKind.Tug, ct)) return false;
        var profile = await db.TugProfiles.SingleOrDefaultAsync(p => p.VesselId == vessel, ct);
        if (profile == null || statement.ProfileVersion != null && profile.Version != statement.ProfileVersion) return false;
        if (statement.AssetId != null) {
            var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == statement.AssetId && a.VesselId == vessel && a.Revision == statement.AssetRevision, ct);
            if (asset == null || statement.AssetVersion != null && asset.Version != statement.AssetVersion) return false;

        }
        if (statement.ConditionEvidenceId != null) {
            if (statement.ConditionEvidenceId == id) return false;
            var head = await db.ControlEvidenceHeads.SingleOrDefaultAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == statement.ConditionEvidenceId &&
                e.Revision == statement.ConditionEvidenceRevision && e.Version == statement.ConditionEvidenceVersion, ct);
            if (head == null) return false;
            var condition = await db.ControlEvidenceRevisions.AsNoTracking().SingleAsync(e => e.VesselId == vessel && e.Kind == "Evidence" && e.Id == head.Id && e.Revision == head.Revision, ct);
            if (JsonSerializer.Deserialize<ControlEvidenceContent>(condition.ContentJson, RegulatoryQualificationEndpoints.Json)?.TugCapability?.Kind != "Condition" ||
                condition.FileId != null && !await db.FileStages.AnyAsync(s => s.Id == condition.FileId && s.VesselId == vessel && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport", ct)) return false;

        }
        // Profile xmin is a captured input, not a semantic scalar to rewrite. The
        // transaction's repeated read and dependent FK protect ownership; current
        // reads invalidate a changed profile, including an operator no-op.
        return true;
    }
}
