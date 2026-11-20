using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// DEV-101 SOF finalization / DEV-102 downtime lifecycle: one immutable row per
// attributed human command. It pins the exact record revision it produced and
// the server-provisioned appointment used. It is never a signature, external
// agreement, financial claim decision or safety release, and is never seeded.
public sealed class ShipOpsLifecycleEvent : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public string Module {get;set;}="";
    public Guid RecordId {get;set;}
    public long FromRevision {get;set;}
    public long ToRevision {get;set;}
    public string Action {get;set;}="";
    public string? FromState {get;set;}
    public string? ToState {get;set;}
    public Guid ActorId {get;set;}
    public Guid? RoleId {get;set;}
    public Guid? AuthorityId {get;set;}
    public string? Duty {get;set;}
    public string? AuthorityHash {get;set;}
    public string ReasonCode {get;set;}="";
    // Acknowledged unresolved references (SOF) or the evidence pin (downtime);
    // closed technical identifiers only, no free text.
    public string DetailJson {get;set;}="{}";
    public string SignatureState {get;set;}="SOF_SIGNATURE_NOT_RECORDED";
    public string RecordHash {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
internal static class ShipOpsLifecycleStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<ShipOpsLifecycleEvent>(b=> {
            b.ToTable("ShipOpsLifecycleEvents","operations",t=>t.HasCheckConstraint("CK_ShipOpsLifecycleEvent_State",
                "\"Module\" IN ('PortSof','Downtime') AND \"FromRevision\">0 AND \"ToRevision\"=\"FromRevision\"+1 AND \"SignatureState\"='SOF_SIGNATURE_NOT_RECORDED' AND "+
                "(\"Module\"<>'PortSof' OR \"Action\"='FINALIZE' AND \"AuthorityId\" IS NOT NULL AND \"Duty\" IN ('Master','ChiefOfficer','ChiefEngineer'))"));
            b.HasKey(r=>new{r.TenantId,r.Id});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.Module,r.RecordId,r.ToRevision}).IsUnique();
            b.Property(r=>r.Module).HasMaxLength(16);b.Property(r=>r.Action).HasMaxLength(32);b.Property(r=>r.FromState).HasMaxLength(16);b.Property(r=>r.ToState).HasMaxLength(16);
            b.Property(r=>r.Duty).HasMaxLength(32);b.Property(r=>r.AuthorityHash).HasMaxLength(64);b.Property(r=>r.ReasonCode).HasMaxLength(64);
            b.Property(r=>r.DetailJson).HasColumnType("text");b.Property(r=>r.SignatureState).HasMaxLength(32);b.Property(r=>r.RecordHash).HasMaxLength(64);
        });
    }
}
