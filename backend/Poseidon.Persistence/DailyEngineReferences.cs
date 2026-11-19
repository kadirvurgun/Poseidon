using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// DEV-099: append-only WO/defect link events for one engine daily log entry. Each
// event pins an exact owning revision; links never verify, close or approve the target.
public sealed class DailyEngineReferenceHead : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="Engine";
    public long Sequence {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class DailyEngineReferenceEvent : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="Engine";
    public Guid Id {get;set;}
    public long Sequence {get;set;}
    public long RecordRevision {get;set;}
    public long RecordVersion {get;set;}
    public string Operation {get;set;}="";
    public string TargetKind {get;set;}="";
    public Guid? WorkOrderId {get;set;}
    public long? WorkOrderRevision {get;set;}
    public Guid? DefectId {get;set;}
    public long? DefectRevision {get;set;}
    public long TargetVersion {get;set;}
    public string TargetHash {get;set;}="";
    public string TargetSourceVersion {get;set;}="";
    public string SourceVersion {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DailyEngineReferenceReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="Engine";
    public Guid EventId {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class DailyEngineReferenceStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<DailyEngineReferenceHead>(b=> {
            b.ToTable("DailyEngineReferenceHeads","operations",t=>t.HasCheckConstraint("CK_DailyEngineReferenceHead_State","\"Sequence\" > 0 AND \"Kind\" = 'Engine'"));
            b.HasKey(r=>new {r.TenantId,r.RecordId});b.HasAlternateKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId});
            b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<DailyOperation>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.Id}).HasConstraintName("FK_DailyEngineReferenceHead_Record").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DailyEngineReferenceEvent>(b=> {
            // Exactly one tenant/vessel-bound owning target per event.
            b.ToTable("DailyEngineReferenceEvents","operations",t=>t.HasCheckConstraint("CK_DailyEngineReferenceEvent_State",
                "\"Sequence\" > 0 AND \"RecordRevision\" > 0 AND \"RecordVersion\" > 0 AND \"TargetVersion\" > 0 AND \"Kind\" = 'Engine' AND " +
                "\"ActorId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Operation\" IN ('LINK','UNLINK') AND " +
                "((\"TargetKind\" = 'WorkOrder' AND \"WorkOrderId\" IS NOT NULL AND \"WorkOrderRevision\" > 0 AND \"DefectId\" IS NULL AND \"DefectRevision\" IS NULL) OR " +
                "(\"TargetKind\" = 'Defect' AND \"DefectId\" IS NOT NULL AND \"DefectRevision\" > 0 AND \"WorkOrderId\" IS NULL AND \"WorkOrderRevision\" IS NULL))"));
            b.HasKey(r=>new {r.TenantId,r.Id});b.HasAlternateKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.Id});
            b.HasIndex(r=>new {r.TenantId,r.RecordId,r.Sequence}).IsUnique();
            b.HasOne<DailyEngineReferenceHead>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId}).HasConstraintName("FK_DailyEngineReferenceEvent_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DailyOperationRevision>().WithMany().HasForeignKey(r=>new {r.TenantId,r.RecordId,r.RecordRevision})
                .HasPrincipalKey(r=>new {r.TenantId,r.RecordId,r.Revision}).HasConstraintName("FK_DailyEngineReferenceEvent_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.WorkOrderId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Id}).HasConstraintName("FK_DailyEngineReferenceEvent_WorkOrder").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkOrderRevision>().WithMany().HasForeignKey(r=>new {r.TenantId,r.WorkOrderId,r.WorkOrderRevision})
                .HasPrincipalKey(r=>new {r.TenantId,r.WorkOrderId,r.Revision}).HasConstraintName("FK_DailyEngineReferenceEvent_WorkOrderRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Defect>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.DefectId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Id}).HasConstraintName("FK_DailyEngineReferenceEvent_Defect").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DefectRevision>().WithMany().HasForeignKey(r=>new {r.TenantId,r.DefectId,r.DefectRevision})
                .HasPrincipalKey(r=>new {r.TenantId,r.DefectId,r.Revision}).HasConstraintName("FK_DailyEngineReferenceEvent_DefectRevision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Operation).HasMaxLength(8);b.Property(r=>r.TargetKind).HasMaxLength(16);
            b.Property(r=>r.TargetHash).HasMaxLength(64);b.Property(r=>r.TargetSourceVersion).HasMaxLength(192);b.Property(r=>r.SourceVersion).HasMaxLength(192);
        });
        model.Entity<DailyEngineReferenceReceipt>(b=> {
            b.ToTable("DailyEngineReferenceReceipts","operations");b.HasKey(r=>new {r.TenantId,r.ActorId,r.Key});
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasOne<DailyEngineReferenceEvent>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.EventId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.Id}).HasConstraintName("FK_DailyEngineReferenceReceipt_Event").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
