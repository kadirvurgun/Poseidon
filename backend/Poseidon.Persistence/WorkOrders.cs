using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Owning technical work order draft declarations. No closure, service verification,
// effectiveness assessment or safety decision is represented by these rows.
public sealed class WorkOrder : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
    public string FieldsJson {get;set;}="{}";
    public Guid? PlanId {get;set;}
    public long? PlanRevision {get;set;}
    public long? PlanVersion {get;set;}
    public string? PlanHash {get;set;}
    public string? PlanSourceVersion {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class WorkOrderRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string FieldsJson {get;set;}="{}";
    public Guid? PlanId {get;set;}
    public long? PlanRevision {get;set;}
    public long? PlanVersion {get;set;}
    public string? PlanHash {get;set;}
    public string? PlanSourceVersion {get;set;}
    public string SourceVersion {get;set;}="";
    public string Hash {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class WorkOrderReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid WorkOrderId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class WorkOrderStorage
{
    internal const string PlanConstraint="((\"PlanId\" IS NULL AND \"PlanRevision\" IS NULL AND \"PlanVersion\" IS NULL AND \"PlanHash\" IS NULL AND \"PlanSourceVersion\" IS NULL) OR (\"PlanId\" IS NOT NULL AND \"PlanRevision\" IS NOT NULL AND \"PlanRevision\">0 AND \"PlanVersion\" IS NOT NULL AND \"PlanVersion\">0 AND \"PlanHash\" IS NOT NULL AND \"PlanSourceVersion\" IS NOT NULL))";
    internal static void Configure(ModelBuilder model) {
        model.Entity<WorkOrder>(b=> {
            b.ToTable("WorkOrders","technical",t=>t.HasCheckConstraint("CK_WorkOrder_Draft","\"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND "+PlanConstraint));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<MaintenancePlanRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.PlanId,r.PlanRevision})
                .HasConstraintName("FK_WorkOrder_PlanRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.PlanHash).HasMaxLength(64);b.Property(r=>r.PlanSourceVersion).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<WorkOrderRevision>(b=> {
            b.ToTable("WorkOrderRevisions","technical",t=>t.HasCheckConstraint("CK_WorkOrderRevision_Draft","\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND "+PlanConstraint));
            b.HasKey(r=>new{r.TenantId,r.WorkOrderId,r.Revision});
            b.HasAlternateKey(r=>new{r.TenantId,r.WorkOrderId,r.Revision,r.RecordVersion}).HasName("AK_WorkOrderRevision_Version");
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_WorkOrderRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<MaintenancePlanRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.PlanId,r.PlanRevision})
                .HasConstraintName("FK_WorkOrderRevision_PlanRevision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.SourceVersion).HasMaxLength(192);
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.PlanHash).HasMaxLength(64);b.Property(r=>r.PlanSourceVersion).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<WorkOrderReceipt>(b=> {
            b.ToTable("WorkOrderReceipts","technical",t=>t.HasCheckConstraint("CK_WorkOrderReceipt_Version","\"Revision\">0 AND \"RecordVersion\">0"));b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_WorkOrderReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkOrderRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.WorkOrderId,r.Revision,r.RecordVersion}).HasPrincipalKey(r=>new{r.TenantId,r.WorkOrderId,r.Revision,r.RecordVersion})
                .HasConstraintName("FK_WorkOrderReceipt_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
        });
    }
}
