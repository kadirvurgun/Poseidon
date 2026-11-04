using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class DailyReviewHead : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="";
    public long Sequence {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class DailyReviewEvent : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="";
    public Guid Id {get;set;}
    public long RecordRevision {get;set;}
    public long Sequence {get;set;}
    public long RecordVersion {get;set;}
    public long ReviewVersion {get;set;}
    public Guid ActorId {get;set;}
    public Guid RoleId {get;set;}
    public Guid AuthorityId {get;set;}
    public string Duty {get;set;}="";
    public string Decision {get;set;}="";
    public string ReasonCode {get;set;}="";
    public string Rationale {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string AuthorityHash {get;set;}="";
    public string AuthorityJson {get;set;}="{}";
    public string RecordHash {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class DailyReviewReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid RecordId {get;set;}
    public string Kind {get;set;}="";
    public Guid EventId {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class DailyReviewStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<DailyReviewHead>(b=> {
            b.ToTable("DailyReviewHeads","operations",t=>t.HasCheckConstraint("CK_DailyReviewHead_Sequence","\"Sequence\" > 0"));
            b.HasKey(r=>new {r.TenantId,r.RecordId});b.HasAlternateKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId});
            b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<DailyOperation>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.Id}).HasConstraintName("FK_DailyReviewHead_Record").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DailyReviewEvent>(b=> {
            b.ToTable("DailyReviewEvents","operations",t=>t.HasCheckConstraint("CK_DailyReviewEvent_State",
                "\"Sequence\" > 0 AND \"RecordVersion\" > 0 AND \"ReviewVersion\" > 0 AND \"ActorId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND ((\"Kind\" = 'Noon' AND \"Decision\" = 'REVIEWED' AND \"Duty\" = 'Master') OR (\"Kind\" = 'Deck' AND \"Decision\" IN ('REVIEWED','RETURNED') AND \"Duty\" IN ('Master','ChiefOfficer')) OR (\"Kind\" = 'Engine' AND \"Decision\" IN ('REVIEWED','RETURNED') AND \"Duty\" = 'ChiefEngineer'))"));
            b.HasKey(r=>new {r.TenantId,r.Id});b.HasAlternateKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.Id});
            b.HasIndex(r=>new {r.TenantId,r.RecordId,r.Sequence}).IsUnique();
            b.HasOne<DailyReviewHead>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId}).HasConstraintName("FK_DailyReviewEvent_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DailyOperationRevision>().WithMany().HasForeignKey(r=>new {r.TenantId,r.RecordId,r.RecordRevision})
                .HasPrincipalKey(r=>new {r.TenantId,r.RecordId,r.Revision}).HasConstraintName("FK_DailyReviewEvent_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Duty).HasMaxLength(24);b.Property(r=>r.Decision).HasMaxLength(16);
            b.Property(r=>r.ReasonCode).HasMaxLength(32);b.Property(r=>r.Rationale).HasMaxLength(2000);b.Property(r=>r.SourceVersion).HasMaxLength(160);
            b.Property(r=>r.AuthorityHash).HasMaxLength(64);b.Property(r=>r.RecordHash).HasMaxLength(64);
            b.Property(r=>r.AuthorityJson).HasColumnType("text");
        });
        model.Entity<DailyReviewReceipt>(b=> {
            b.ToTable("DailyReviewReceipts","operations");b.HasKey(r=>new {r.TenantId,r.ActorId,r.Key});
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Kind).HasMaxLength(12);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasOne<DailyReviewEvent>().WithMany().HasForeignKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.EventId})
                .HasPrincipalKey(r=>new {r.TenantId,r.VesselId,r.Kind,r.RecordId,r.Id}).HasConstraintName("FK_DailyReviewReceipt_Event").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
