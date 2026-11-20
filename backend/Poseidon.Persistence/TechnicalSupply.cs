using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-110 critical spares and technical requisition proposals. Stock counts are human declarations
// (never ERP truth); approval records only an export reference decision, never a purchase order.
public sealed class CriticalSpare : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}=1;
    public uint Version {get;set;}
}
public sealed class CriticalSpareRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid SpareId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Transition {get;set;}="CREATE";
    public Guid? AssetId {get;set;}
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class TechnicalRequisition : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid SpareId {get;set;}
    public long Revision {get;set;}=1;
    public string State {get;set;}="PROPOSED";
    public uint Version {get;set;}
}
public sealed class TechnicalRequisitionRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid RequisitionId {get;set;}
    public Guid SpareId {get;set;}
    public long SpareRevision {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Transition {get;set;}="PROPOSE";
    public string State {get;set;}="PROPOSED";
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class TechnicalSupplyReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid ResourceId {get;set;}
    public string Action {get;set;}="";
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class TechnicalSupplyStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<CriticalSpare>(b=> {
            b.ToTable("CriticalSpares","technical",t=>t.HasCheckConstraint("CK_CriticalSpare_Revision","\"Revision\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).HasConstraintName("FK_CriticalSpare_Vessel").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CriticalSpareRevision>(b=> {
            b.ToTable("CriticalSpareRevisions","technical",t=>t.HasCheckConstraint("CK_CriticalSpareRevision_State","\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"Transition\" IN ('CREATE','UPDATE') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.SpareId,r.Revision});
            b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.SpareId,r.Revision}).HasName("AK_CriticalSpareRevision_Scope");
            b.HasOne<CriticalSpare>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.SpareId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.Id})
                .HasConstraintName("FK_CriticalSpareRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Asset>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssetId}).HasPrincipalKey(a=>new{a.TenantId,a.VesselId,a.Id})
                .HasConstraintName("FK_CriticalSpareRevision_Asset").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Transition).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.Provenance).HasMaxLength(32);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SpareId}).HasDatabaseName("IX_CriticalSpareRevision_Head");
        });
        model.Entity<TechnicalRequisition>(b=> {
            b.ToTable("TechnicalRequisitions","technical",t=>t.HasCheckConstraint("CK_TechnicalRequisition_State","\"Revision\" > 0 AND \"State\" IN ('PROPOSED','APPROVED_FOR_ERP_EXPORT','REJECTED')"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();b.Property(r=>r.State).HasMaxLength(32);
            // At most one open proposal per spare: a second low-stock trigger cannot duplicate demand.
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SpareId}).HasDatabaseName("UX_TechnicalRequisition_OpenProposal").IsUnique().HasFilter("\"State\" = 'PROPOSED'");
            b.HasOne<CriticalSpare>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.SpareId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.Id})
                .HasConstraintName("FK_TechnicalRequisition_Spare").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TechnicalRequisitionRevision>(b=> {
            // Approval/rejection rows can never be synthetic: fixtures only ever hold proposals.
            b.ToTable("TechnicalRequisitionRevisions","technical",t=>t.HasCheckConstraint("CK_TechnicalRequisitionRevision_State","\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"SpareRevision\" > 0 AND \"Transition\" IN ('PROPOSE','APPROVE','REJECT') AND \"State\" IN ('PROPOSED','APPROVED_FOR_ERP_EXPORT','REJECTED') AND (\"Provenance\" = 'UserDeclaredUnverified' OR (\"Provenance\" = 'SyntheticUnverified' AND \"State\" = 'PROPOSED'))"));
            b.HasKey(r=>new{r.TenantId,r.RequisitionId,r.Revision});
            b.HasOne<TechnicalRequisition>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.RequisitionId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.Id})
                .HasConstraintName("FK_TechnicalRequisitionRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CriticalSpareRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.SpareId,r.SpareRevision}).HasPrincipalKey(s=>new{s.TenantId,s.VesselId,s.SpareId,s.Revision})
                .HasConstraintName("FK_TechnicalRequisitionRevision_Spare").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Transition).HasMaxLength(16);b.Property(r=>r.State).HasMaxLength(32);b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.Provenance).HasMaxLength(32);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.RequisitionId}).HasDatabaseName("IX_TechnicalRequisitionRevision_Head");
        });
        model.Entity<TechnicalSupplyReceipt>(b=> {
            b.ToTable("TechnicalSupplyReceipts","technical",t=>t.HasCheckConstraint("CK_TechnicalSupplyReceipt_Action","\"Action\" IN ('CriticalSpareCreate','CriticalSpareUpdate','TechnicalRequisitionPropose','TechnicalRequisitionDecide') AND \"Revision\" > 0 AND \"RecordVersion\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).HasConstraintName("FK_TechnicalSupplyReceipt_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(40);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.ResourceId}).HasDatabaseName("IX_TechnicalSupplyReceipt_Resource");
        });
    }
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes,Func<EntityEntry,string[],bool> headAdvance) {
        if(changes.Any(e=>e.Entity is CriticalSpareRevision or TechnicalRequisitionRevision or TechnicalSupplyReceipt && e.State!=EntityState.Added))
            throw new InvalidOperationException("Critical spare and requisition history and receipts are immutable.");
        if(changes.Any(e=>e.Entity is CriticalSpare && headAdvance(e,[]) || e.Entity is TechnicalRequisition && headAdvance(e,["State"])))
            throw new InvalidOperationException("Critical spare and requisition ownership is immutable; append one revision.");
    }
}
