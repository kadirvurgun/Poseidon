using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

public sealed class TugDraftPlan : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public long RequestRevision { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string SourceVersion { get; set; } = TugDraftPlanning.SourceVersion;
    public DateTimeOffset? PlannedStart { get; set; }
    public DateTimeOffset? PlannedEnd { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}
public sealed class TugDraftPlanRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public long Revision { get; set; }
    public long RequestRevision { get; set; }
    public string PolicyVersion { get; set; } = "";
    public string SourceVersion { get; set; } = TugDraftPlanning.SourceVersion;
    public string Operation { get; set; } = "Window";
    public string ContentJson { get; set; } = "";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public DateTimeOffset SavedAt { get; set; }
    public TugPlanInputs Inputs() => JsonSerializer.Deserialize<TugPlanInputs>(ContentJson)!;
}
// Revision-local references. Removal/reassignment appends a new set, preserving old facts.
public sealed class TugDraftPlanTug : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public long Revision { get; set; }
    public Guid TugId { get; set; }
}
public static class TugDraftPlanStorage
{
    public static void Configure(ModelBuilder model) {
        model.Entity<TugDraftPlan>(b => {
            b.ToTable("DraftPlans", "tug", t => t.HasCheckConstraint("CK_DraftPlan_Window", "\"PlannedStart\" IS NULL OR \"PlannedEnd\" IS NULL OR \"PlannedEnd\" >= \"PlannedStart\""));
            b.HasKey(x => new { x.TenantId, x.VesselId, x.JobId }); b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.PolicyVersion).HasMaxLength(80); b.Property(x => x.SourceVersion).HasMaxLength(80);
            b.HasOne<TugJobRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, x.RequestRevision }).HasConstraintName("FK_DraftPlans_JobRevisions").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugDraftPlanRevision>(b => {
            b.ToTable("DraftPlanRevisions", "tug"); b.HasKey(x => new { x.TenantId, x.VesselId, x.JobId, x.Revision });
            b.Property(x => x.ContentJson).HasColumnType("jsonb"); b.Property(x => x.PolicyVersion).HasMaxLength(80);
            b.Property(x => x.SourceVersion).HasMaxLength(80); b.Property(x => x.Operation).HasMaxLength(16); b.Property(x => x.Provenance).HasMaxLength(40);
            b.HasOne<TugDraftPlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId }).HasConstraintName("FK_DraftPlanRevisions_Plan").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugJobRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, x.RequestRevision }).HasConstraintName("FK_DraftPlanRevisions_Request").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugDraftPlanTug>(b => {
            b.ToTable("DraftPlanTugs", "tug"); b.HasKey(x => new { x.TenantId, x.VesselId, x.JobId, x.Revision, x.TugId });
            b.HasOne<TugDraftPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, x.Revision }).HasConstraintName("FK_DraftPlanTugs_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.TugId }).HasConstraintName("FK_DraftPlanTugs_Tug").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
