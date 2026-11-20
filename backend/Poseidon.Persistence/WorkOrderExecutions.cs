using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-054 work order execution. One head per released WorkOrder carrying only the optimistic
// revision/state; each transition or evidence save appends one immutable revision pinned to the
// exact WorkOrder revision it executes. Verification/closure are human records inside the content.
public sealed class WorkOrderExecution : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public long Revision {get;set;}=1;
    public string State {get;set;}="RELEASED";
    public uint Version {get;set;}
}
public sealed class WorkOrderExecutionRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public long WorkOrderRevision {get;set;}
    public long WorkOrderVersion {get;set;}
    public string WorkOrderHash {get;set;}="";
    public string Transition {get;set;}="RELEASE";
    public string State {get;set;}="RELEASED";
    public string EvidenceGate {get;set;}="REVIEW_REQUIRED";
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class WorkOrderExecutionReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public Guid WorkOrderId {get;set;}
    public string Action {get;set;}="";
    public long Revision {get;set;}
    public long RecordVersion {get;set;}
    public string Fingerprint {get;set;}="";
    public string OutcomeJson {get;set;}="";
    public DateTimeOffset SavedAt {get;set;}
}
// DEV-057 work order offline namespace: separate from crew/tug/daily grants. No plaintext command
// snapshot, key material or credential is stored; protected values use server data protection.
public sealed class WorkOrderOfflineEnrollment : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid ActorId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public Guid DeviceId {get;set;}
    public Guid KeyId {get;set;}
    public string Challenge {get;set;}="";
    public string? PublicKey {get;set;}
    public string PolicyVersion {get;set;}="";
    public string PolicyDigest {get;set;}="";
    public string AuthorityDigest {get;set;}="";
    public string SourceVersion {get;set;}="";
    public uint BaseVersion {get;set;}
    public long BaseRevision {get;set;}
    public string ProtectedBase {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public DateTimeOffset RetainUntil {get;set;}
    public DateTimeOffset? RevokedAt {get;set;}
    public uint Version {get;set;}
}
public sealed class WorkOrderOfflineLifecycle : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid EnrollmentId {get;set;}
    public Guid ActorId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public string Action {get;set;}="";
    public DateTimeOffset RecordedAt {get;set;}
}
public sealed class WorkOrderOfflineOutcome : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public Guid CommandId {get;set;}
    public Guid EnrollmentId {get;set;}
    public Guid VesselId {get;set;}
    public Guid WorkOrderId {get;set;}
    public Guid? ParentCommandId {get;set;}
    public string Digest {get;set;}="";
    public string Result {get;set;}="";
    public uint BaseVersion {get;set;}
    public uint ServerVersion {get;set;}
    public long Revision {get;set;}
    public string OutcomeJson {get;set;}="";
    public string ProtectedContext {get;set;}="";
    public DateTimeOffset CapturedAt {get;set;}
    public DateTimeOffset ReceivedAt {get;set;}
}

internal static class WorkOrderExecutionStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<WorkOrderExecution>(b=> {
            b.ToTable("WorkOrderExecutions","technical",t=>t.HasCheckConstraint("CK_WorkOrderExecution_State","\"Revision\" > 0 AND \"State\" IN ('RELEASED','IN_PROGRESS','EVIDENCE_SUBMITTED','VERIFIED','CLOSED')"));
            b.HasKey(r=>new{r.TenantId,r.WorkOrderId});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId});b.Property(r=>r.Version).IsRowVersion();
            b.Property(r=>r.State).HasMaxLength(24);b.HasIndex(r=>new{r.TenantId,r.VesselId,r.State}).HasDatabaseName("IX_WorkOrderExecution_State");
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasPrincipalKey(w=>new{w.TenantId,w.VesselId,w.Id})
                .HasConstraintName("FK_WorkOrderExecution_WorkOrder").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorkOrderExecutionRevision>(b=> {
            b.ToTable("WorkOrderExecutionRevisions","technical",t=> {
                t.HasCheckConstraint("CK_WorkOrderExecutionRevision_Binding","\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"WorkOrderRevision\" > 0 AND \"WorkOrderVersion\" > 0");
                // A seeded/fixture row can never carry a human verification or closure.
                t.HasCheckConstraint("CK_WorkOrderExecutionRevision_State","\"Transition\" IN ('RELEASE','REQUIREMENTS','START','EVIDENCE','SUBMIT','VERIFY','RETURN','CLOSE') AND \"State\" IN ('RELEASED','IN_PROGRESS','EVIDENCE_SUBMITTED','VERIFIED','CLOSED') AND \"EvidenceGate\" IN ('REVIEW_REQUIRED','MISSING','PRESENT_PENDING_HUMAN_VERIFICATION','HUMAN_VERIFIED') AND (\"Provenance\" = 'UserDeclaredUnverified' OR (\"Provenance\" = 'SyntheticUnverified' AND \"State\" IN ('RELEASED','IN_PROGRESS') AND \"EvidenceGate\" <> 'HUMAN_VERIFIED'))");
            });
            b.HasKey(r=>new{r.TenantId,r.WorkOrderId,r.Revision});
            b.HasOne<WorkOrderExecution>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.WorkOrderId})
                .HasConstraintName("FK_WorkOrderExecutionRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkOrderRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.WorkOrderId,r.WorkOrderRevision,r.WorkOrderVersion})
                .HasPrincipalKey(w=>new{w.TenantId,w.WorkOrderId,w.Revision,w.RecordVersion}).HasConstraintName("FK_WorkOrderExecutionRevision_WorkOrder").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.WorkOrderHash).HasMaxLength(64);b.Property(r=>r.Transition).HasMaxLength(16);b.Property(r=>r.State).HasMaxLength(24);b.Property(r=>r.EvidenceGate).HasMaxLength(40);
            b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.SourceVersion).HasMaxLength(192);b.Property(r=>r.Provenance).HasMaxLength(32);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasDatabaseName("IX_WorkOrderExecutionRevision_Head");
        });
        model.Entity<WorkOrderExecutionReceipt>(b=> {
            b.ToTable("WorkOrderExecutionReceipts","technical",t=>t.HasCheckConstraint("CK_WorkOrderExecutionReceipt_Action","\"Action\" IN ('WorkOrderExecutionTransition','WorkOrderExecutionEvidence') AND \"Revision\" > 0 AND \"RecordVersion\" > 0"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<WorkOrderExecution>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.WorkOrderId})
                .HasConstraintName("FK_WorkOrderExecutionReceipt_Head").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(40);b.Property(r=>r.Fingerprint).HasMaxLength(64);b.Property(r=>r.OutcomeJson).HasColumnType("text");
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.WorkOrderId}).HasDatabaseName("IX_WorkOrderExecutionReceipt_Head");
        });
        model.Entity<WorkOrderOfflineEnrollment>(b=> {
            b.ToTable("WorkOrderEnrollments","offline",t=>t.HasCheckConstraint("CK_WorkOrderOfflineEnrollment_State","\"BaseVersion\" > 0 AND \"BaseRevision\" > 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\""));
            b.HasKey(e=>new{e.TenantId,e.Id});
            b.HasAlternateKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.WorkOrderId}).HasName("AK_WorkOrderOfflineEnrollment_Scope");
            b.HasOne<WorkOrderExecution>().WithMany().HasForeignKey(e=>new{e.TenantId,e.VesselId,e.WorkOrderId}).HasPrincipalKey(h=>new{h.TenantId,h.VesselId,h.WorkOrderId})
                .HasConstraintName("FK_WorkOrderOffline_Execution").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.VesselId,e.WorkOrderId,e.CreatedAt}).HasDatabaseName("IX_WorkOrderOfflineEnrollment_Owner");
            b.Property(e=>e.Challenge).HasMaxLength(64);b.Property(e=>e.PublicKey).HasMaxLength(256);b.Property(e=>e.PolicyVersion).HasMaxLength(80);
            b.Property(e=>e.PolicyDigest).HasMaxLength(64);b.Property(e=>e.AuthorityDigest).HasMaxLength(64);b.Property(e=>e.SourceVersion).HasMaxLength(192);b.Property(e=>e.Version).IsRowVersion();
        });
        model.Entity<WorkOrderOfflineLifecycle>(b=> {
            b.ToTable("WorkOrderLifecycle","offline",t=>t.HasCheckConstraint("CK_WorkOrderOfflineLifecycle_Action","\"Action\" IN ('Enrolled','Renewed','KeyEnrolled','Revoked','KeyLost','CleanupAllowed')"));
            b.HasKey(e=>new{e.TenantId,e.Id});b.Property(e=>e.Action).HasMaxLength(24);
            b.HasIndex(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.WorkOrderId}).HasDatabaseName("IX_WorkOrderOfflineLifecycle_Scope");
            b.HasOne<WorkOrderOfflineEnrollment>().WithMany().HasForeignKey(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.WorkOrderId})
                .HasPrincipalKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.WorkOrderId}).HasConstraintName("FK_WorkOrderOfflineLifecycle_Enrollment").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorkOrderOfflineOutcome>(b=> {
            b.ToTable("WorkOrderOutcomes","offline",t=>t.HasCheckConstraint("CK_WorkOrderOfflineOutcome_State","\"BaseVersion\" > 0 AND \"ServerVersion\" > 0 AND \"Revision\" > 0 AND \"Result\" IN ('Committed','MaterialConflict','ServerRetained') AND (\"Result\" <> 'ServerRetained' OR \"ParentCommandId\" IS NOT NULL)"));
            b.HasKey(e=>new{e.TenantId,e.ActorId,e.CommandId});
            b.HasAlternateKey(e=>new{e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasName("AK_WorkOrderOfflineOutcome_ParentScope");
            b.HasIndex(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.WorkOrderId}).HasDatabaseName("IX_WorkOrderOfflineOutcome_Scope");
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId}).HasDatabaseName("IX_WorkOrderOfflineOutcome_ParentScope");
            b.HasIndex(e=>new{e.TenantId,e.ActorId,e.ParentCommandId}).HasDatabaseName("UX_WorkOrderOfflineOutcome_Resolution").IsUnique().HasFilter("\"ParentCommandId\" IS NOT NULL");
            b.HasOne<WorkOrderOfflineEnrollment>().WithMany().HasForeignKey(e=>new{e.TenantId,e.EnrollmentId,e.ActorId,e.VesselId,e.WorkOrderId})
                .HasPrincipalKey(e=>new{e.TenantId,e.Id,e.ActorId,e.VesselId,e.WorkOrderId}).HasConstraintName("FK_WorkOrderOfflineOutcome_Enrollment").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WorkOrderOfflineOutcome>().WithMany().HasForeignKey(e=>new{e.TenantId,e.ActorId,e.ParentCommandId,e.EnrollmentId})
                .HasPrincipalKey(e=>new{e.TenantId,e.ActorId,e.CommandId,e.EnrollmentId}).HasConstraintName("FK_WorkOrderOfflineOutcome_Parent").OnDelete(DeleteBehavior.Restrict);
            b.Property(e=>e.Digest).HasMaxLength(64);b.Property(e=>e.Result).HasMaxLength(24);b.Property(e=>e.OutcomeJson).HasColumnType("text");
        });
    }
    private static bool HeadAdvance(EntityEntry e,params string[] allowed)=>e.State==EntityState.Deleted || e.State==EntityState.Modified &&
        (e.Properties.Any(p=>p.IsModified && p.Metadata.Name!="Revision" && !allowed.Contains(p.Metadata.Name)) ||
            (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue!+1);
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if(changes.Any(e=>e.Entity is WorkOrderExecutionRevision or WorkOrderExecutionReceipt or WorkOrderOfflineLifecycle or WorkOrderOfflineOutcome && e.State!=EntityState.Added))
            throw new InvalidOperationException("Work order execution history, receipts and offline lineage are immutable.");
        if(changes.Any(e=>e.Entity is WorkOrderExecution && HeadAdvance(e,"State")))
            throw new InvalidOperationException("Work order execution ownership is immutable; append one revision.");
        if(changes.Any(e=>e.Entity is WorkOrderOfflineEnrollment && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            (e.Properties.Any(p=>p.IsModified && p.Metadata.Name is not("ExpiresAt" or "RevokedAt" or "PublicKey")) || e.Property("RevokedAt").OriginalValue!=null ||
             (DateTimeOffset)e.Property("ExpiresAt").CurrentValue! < (DateTimeOffset)e.Property("ExpiresAt").OriginalValue! ||
             e.Property("PublicKey").OriginalValue!=null && !Equals(e.Property("PublicKey").OriginalValue,e.Property("PublicKey").CurrentValue)))))
            throw new InvalidOperationException("Work order offline ownership and key identity are immutable.");
        TechnicalSupplyStorage.Guard(changes,HeadAdvance);
    }
}
