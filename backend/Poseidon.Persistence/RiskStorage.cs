using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-105 risk assessment workspace / DEV-106 risk template library (50.01 Rev3 §3-6).
// Heads are mutable with xmin concurrency; revisions, human decisions, acknowledgements
// and command receipts are append-only. No row is a permit, an expert risk acceptance,
// a signature or an HSE release gate PASS. Seeds never add decisions or acknowledgements.
public sealed class RiskAssessment : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public string AssessmentNo {get;set;}="";
    public long Revision {get;set;}=1;
    public string State {get;set;}="DRAFT";
    public string ContentJson {get;set;}="{}";
    public string LinksJson {get;set;}="[]";
    public string? TemplateJson {get;set;}
    public Guid? MethodId {get;set;}
    public string Hash {get;set;}="";
    // Editors of the current review cycle (sorted GUID JSON); used for separation of duties.
    public string AuthorsJson {get;set;}="[]";
    public Guid? SubmittedBy {get;set;}
    public long? ApprovedRevision {get;set;}
    public Guid CreatedBy {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public uint Version {get;set;}
}
public sealed class RiskAssessmentRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid AssessmentId {get;set;}
    public long Revision {get;set;}
    public string State {get;set;}="DRAFT";
    public string ReasonCode {get;set;}="";
    public string ContentJson {get;set;}="{}";
    public string LinksJson {get;set;}="[]";
    public string? TemplateJson {get;set;}
    public Guid? MethodId {get;set;}
    public string Hash {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
}
// Human review record (APPROVE / RETURN). Reason text is withheld from audit summaries.
public sealed class RiskAssessmentDecision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid AssessmentId {get;set;}
    public long Revision {get;set;}
    public long ResultRevision {get;set;}
    public string Decision {get;set;}="";
    public string ContentHash {get;set;}="";
    public Guid ActorId {get;set;}
    public string Reason {get;set;}="";
    public DateTimeOffset DecidedAt {get;set;}
}
// One per person per approved revision (ACK or DISPUTE); recorded only by that person.
public sealed class RiskAssessmentAcknowledgement : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public Guid AssessmentId {get;set;}
    public long Revision {get;set;}
    public Guid ActorId {get;set;}
    public string Choice {get;set;}="";
    public string ContentHash {get;set;}="";
    public string? Reason {get;set;}
    public DateTimeOffset RecordedAt {get;set;}
}
public sealed class RiskTemplate : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public string TemplateCode {get;set;}="";
    public long LatestRevision {get;set;}=1;
    public Guid CreatedBy {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public uint Version {get;set;}
}
// One row per template revision. Content is mutable only before publication; a
// PUBLISHED row may only move to SUPERSEDED and is retained forever.
public sealed class RiskTemplateRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid TemplateId {get;set;}
    public long Revision {get;set;}
    public string Status {get;set;}="DRAFT";
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public DateTimeOffset? EffectiveFrom {get;set;}
    public string AuthorsJson {get;set;}="[]";
    public Guid? SubmittedBy {get;set;}
    public Guid? PublishedBy {get;set;}
    public DateTimeOffset? PublishedAt {get;set;}
    public long? SupersededByRevision {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public string Provenance {get;set;}="UserDeclaredUnverified";
    public uint Version {get;set;}
}
public sealed class RiskTemplateDecision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid TemplateId {get;set;}
    public long Revision {get;set;}
    public string Decision {get;set;}="";
    public string ContentHash {get;set;}="";
    public DateTimeOffset? EffectiveFrom {get;set;}
    public Guid ActorId {get;set;}
    public string Reason {get;set;}="";
    public DateTimeOffset DecidedAt {get;set;}
}
// Crew improvement suggestion. ACCEPTED never changes or publishes a template.
public sealed class RiskTemplateSuggestion : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public Guid TemplateId {get;set;}
    public long Revision {get;set;}
    public string Text {get;set;}="";
    public string Status {get;set;}="OPEN";
    public Guid ActorId {get;set;}
    public DateTimeOffset CreatedAt {get;set;}
    public Guid? ReviewedBy {get;set;}
    public string? ReviewReason {get;set;}
    public DateTimeOffset? ReviewedAt {get;set;}
    public uint Version {get;set;}
}
// Tenant risk matrix configuration (50.01 paragraph 29). Nothing is seeded: until a human
// publishes one, every rating is METHOD_UNCONFIGURED / NOT_ASSESSED.
public sealed class RiskMethod : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public string Code {get;set;}="";
    public long Revision {get;set;}=1;
    public string Status {get;set;}="DRAFT";
    public string ContentJson {get;set;}="{}";
    public string Hash {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public Guid? PublishedBy {get;set;}
    public DateTimeOffset? PublishedAt {get;set;}
    public string? PublishReason {get;set;}
    public uint Version {get;set;}
}
public sealed class RiskCommandReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public string Action {get;set;}="";
    public string Fingerprint {get;set;}="";
    public Guid? VesselId {get;set;}
    public Guid ResourceId {get;set;}
    public long Revision {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}

internal static class RiskStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<RiskAssessment>(b=> {
            b.ToTable("RiskAssessments","hse",t=>t.HasCheckConstraint("CK_RiskAssessment_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND "+
                "(\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ApprovedRevision\" IS NULL)"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.AssessmentNo}).IsUnique();b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});
            b.Property(r=>r.AssessmentNo).HasMaxLength(50);b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");
            b.Property(r=>r.LinksJson).HasColumnType("text");b.Property(r=>r.TemplateJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.AuthorsJson).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<RiskAssessmentRevision>(b=> {
            b.ToTable("RiskAssessmentRevisions","hse",t=>t.HasCheckConstraint("CK_RiskAssessmentRevision_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"ReasonCode\" IN ('CREATE','TEMPLATE_COPY','EDIT','SUBMIT','APPROVE','RETURN','AMEND','SYNTHETIC_INCOMPLETE_DRAFT')"));
            b.HasKey(r=>new{r.TenantId,r.AssessmentId,r.Revision});
            b.HasOne<RiskAssessment>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.AssessmentId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_RiskAssessmentRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.ReasonCode).HasMaxLength(32);b.Property(r=>r.ContentJson).HasColumnType("text");
            b.Property(r=>r.LinksJson).HasColumnType("text");b.Property(r=>r.TemplateJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<RiskAssessmentDecision>(b=> {
            b.ToTable("RiskAssessmentDecisions","hse",t=>t.HasCheckConstraint("CK_RiskAssessmentDecision_Kind",
                "\"Decision\" IN ('APPROVE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.AssessmentId,r.Revision}).IsUnique();
            b.HasOne<RiskAssessmentRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.AssessmentId,r.Revision}).HasPrincipalKey(r=>new{r.TenantId,r.AssessmentId,r.Revision})
                .HasConstraintName("FK_RiskAssessmentDecision_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Decision).HasMaxLength(16);b.Property(r=>r.ContentHash).HasMaxLength(64);b.Property(r=>r.Reason).HasMaxLength(1000);
        });
        model.Entity<RiskAssessmentAcknowledgement>(b=> {
            b.ToTable("RiskAssessmentAcknowledgements","hse",t=>t.HasCheckConstraint("CK_RiskAssessmentAcknowledgement_Choice",
                "\"Choice\" IN ('ACK','DISPUTE') AND (\"Choice\"<>'DISPUTE' OR length(\"Reason\")>=10)"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.AssessmentId,r.Revision,r.ActorId}).IsUnique();
            b.HasOne<RiskAssessmentRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.AssessmentId,r.Revision}).HasPrincipalKey(r=>new{r.TenantId,r.AssessmentId,r.Revision})
                .HasConstraintName("FK_RiskAssessmentAcknowledgement_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Choice).HasMaxLength(16);b.Property(r=>r.ContentHash).HasMaxLength(64);b.Property(r=>r.Reason).HasMaxLength(1000);
        });
        model.Entity<RiskTemplate>(b=> {
            b.ToTable("RiskTemplates","hse",t=>t.HasCheckConstraint("CK_RiskTemplate_Revision","\"LatestRevision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.TemplateCode}).IsUnique();b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(r=>r.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.TemplateCode).HasMaxLength(50);b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<RiskTemplateRevision>(b=> {
            b.ToTable("RiskTemplateRevisions","hse",t=>t.HasCheckConstraint("CK_RiskTemplateRevision_Status",
                "\"Revision\">0 AND \"Status\" IN ('DRAFT','IN_REVIEW','RETURNED','PUBLISHED','SUPERSEDED') AND "+
                "(\"Status\" NOT IN ('PUBLISHED','SUPERSEDED') OR \"PublishedBy\" IS NOT NULL AND \"EffectiveFrom\" IS NOT NULL) AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"Status\"='DRAFT' AND \"PublishedBy\" IS NULL)"));
            b.HasKey(r=>new{r.TenantId,r.TemplateId,r.Revision});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<RiskTemplate>().WithMany().HasForeignKey(r=>new{r.TenantId,r.TemplateId}).HasConstraintName("FK_RiskTemplateRevision_Template").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Status).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.AuthorsJson).HasColumnType("text");b.Property(r=>r.Provenance).HasMaxLength(32);
        });
        model.Entity<RiskTemplateDecision>(b=> {
            b.ToTable("RiskTemplateDecisions","hse",t=>t.HasCheckConstraint("CK_RiskTemplateDecision_Kind",
                "\"Decision\" IN ('PUBLISH','RETURN') AND length(\"Reason\")>=10 AND (\"Decision\"<>'PUBLISH' OR \"EffectiveFrom\" IS NOT NULL)"));
            b.HasKey(r=>new{r.TenantId,r.Id});
            b.HasOne<RiskTemplateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.TemplateId,r.Revision}).HasConstraintName("FK_RiskTemplateDecision_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.TemplateId,r.Revision,r.DecidedAt});
            b.Property(r=>r.Decision).HasMaxLength(16);b.Property(r=>r.ContentHash).HasMaxLength(64);b.Property(r=>r.Reason).HasMaxLength(1000);
        });
        model.Entity<RiskTemplateSuggestion>(b=> {
            b.ToTable("RiskTemplateSuggestions","hse",t=>t.HasCheckConstraint("CK_RiskTemplateSuggestion_Status",
                "\"Status\" IN ('OPEN','ACCEPTED','REJECTED') AND (\"Status\"='OPEN' OR \"ReviewedBy\" IS NOT NULL AND length(\"ReviewReason\")>=10)"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<RiskTemplateRevision>().WithMany().HasForeignKey(r=>new{r.TenantId,r.TemplateId,r.Revision}).HasConstraintName("FK_RiskTemplateSuggestion_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.TemplateId,r.Status});
            b.Property(r=>r.Text).HasMaxLength(4000);b.Property(r=>r.Status).HasMaxLength(16);b.Property(r=>r.ReviewReason).HasMaxLength(1000);
        });
        model.Entity<RiskMethod>(b=> {
            b.ToTable("RiskMethods","hse",t=>t.HasCheckConstraint("CK_RiskMethod_Status",
                "\"Revision\">0 AND \"Status\" IN ('DRAFT','PUBLISHED') AND (\"Status\"<>'PUBLISHED' OR \"PublishedBy\" IS NOT NULL AND \"PublishedBy\"<>\"ActorId\")"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasIndex(r=>new{r.TenantId,r.Code,r.Revision}).IsUnique();b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(r=>r.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Code).HasMaxLength(40);b.Property(r=>r.Status).HasMaxLength(16);b.Property(r=>r.ContentJson).HasColumnType("text");
            b.Property(r=>r.Hash).HasMaxLength(64);b.Property(r=>r.PublishReason).HasMaxLength(1000);
        });
        model.Entity<RiskCommandReceipt>(b=> {
            b.ToTable("RiskCommandReceipts","hse",t=>t.HasCheckConstraint("CK_RiskCommandReceipt_Revision","\"Revision\">0"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(48);b.Property(r=>r.Fingerprint).HasMaxLength(64);
        });
    }

    private static bool Changed(EntityEntry e,params string[] allowed)=>e.Properties.Any(p=>p.IsModified && !allowed.Contains(p.Metadata.Name));
    // Called by PoseidonDbContext before audit projection; rejects history rewrites.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if(changes.Any(e=>e.Entity is RiskAssessmentRevision or RiskAssessmentDecision or RiskAssessmentAcknowledgement or RiskTemplateDecision or RiskCommandReceipt && e.State!=EntityState.Added))
            throw new InvalidOperationException("Risk history, decisions, acknowledgements and outcomes are immutable.");
        if(changes.Any(e=>e.Entity is RiskAssessment && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            (Changed(e,"Revision","State","ContentJson","LinksJson","TemplateJson","MethodId","Hash","AuthorsJson","SubmittedBy","ApprovedRevision","ActorId","SavedAt") ||
             (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue!+1))))
            throw new InvalidOperationException("Risk assessment ownership is immutable; append one revision.");
        if(changes.Any(e=>e.Entity is RiskTemplate && (e.State==EntityState.Deleted || e.State==EntityState.Modified && Changed(e,"LatestRevision"))))
            throw new InvalidOperationException("Risk template identity is immutable.");
        if(changes.Any(e=>e.Entity is RiskTemplateRevision && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            ((string)e.Property("Status").OriginalValue! is "PUBLISHED" ? Changed(e,"Status","SupersededByRevision") || (string)e.Property("Status").CurrentValue! != "SUPERSEDED" :
             (string)e.Property("Status").OriginalValue! is "SUPERSEDED" || Changed(e,"Status","ContentJson","Hash","EffectiveFrom","AuthorsJson","SubmittedBy","PublishedBy","PublishedAt","ActorId","SavedAt")))))
            throw new InvalidOperationException("Published risk template revisions are immutable and retained.");
        if(changes.Any(e=>e.Entity is RiskTemplateSuggestion && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            ((string)e.Property("Status").OriginalValue! != "OPEN" || Changed(e,"Status","ReviewedBy","ReviewReason","ReviewedAt")))))
            throw new InvalidOperationException("Reviewed risk template suggestions are immutable.");
        if(changes.Any(e=>e.Entity is RiskMethod && (e.State==EntityState.Deleted || e.State==EntityState.Modified &&
            ((string)e.Property("Status").OriginalValue! != "DRAFT" || Changed(e,"Status","PublishedBy","PublishedAt","PublishReason")))))
            throw new InvalidOperationException("Published risk methods are immutable.");
    }
}
