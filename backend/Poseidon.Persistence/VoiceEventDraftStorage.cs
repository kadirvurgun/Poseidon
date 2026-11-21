using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// DEV-062 / DEV-063 voice-to-event draft candidate. Raw audio is never stored (AudioRetention is
// pinned to NOT_RETAINED by a DB CHECK); the transcript is the user's own note and is never copied
// into audit. The candidate (spans, confidence, rule and extractor versions) is immutable once
// created; user edits change FieldsJson only. CONFIRMED / REJECTED are terminal human records.
public sealed class VoiceEventDraft : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid Id {get;set;}
    public long Revision {get;set;}
    public uint Version {get;set;}
    public string State {get;set;}="DRAFT";
    public string TranscriptOrigin {get;set;}="TYPED";
    public string Transcript {get;set;}="";
    public string TranscriptSha256 {get;set;}="";
    public DateOnly ReferenceDate {get;set;}
    public string? SpeechEngineVersion {get;set;}
    public string? SpeechModelVersion {get;set;}
    public decimal? SpeechConfidence {get;set;}
    public string ExtractorVersion {get;set;}="";
    public string RuleSetVersion {get;set;}="";
    public string CandidateJson {get;set;}="{}";
    public string FieldsJson {get;set;}="{}";
    public string EvidenceJson {get;set;}="[]";
    public string Hash {get;set;}="";
    public Guid CreatedBy {get;set;}
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public Guid? ConfirmedBy {get;set;}
    public DateTimeOffset? ConfirmedAt {get;set;}
    public string? CorrectionsJson {get;set;}
    public Guid? RejectedBy {get;set;}
    public DateTimeOffset? RejectedAt {get;set;}
    public string? RejectionReason {get;set;}
    public string? RejectionNote {get;set;}
    public string AudioRetention {get;set;}="NOT_RETAINED";
    public string Provenance {get;set;}="UserDeclaredUnverified";
    // S9 integration (DEV-062 x DEV-060): the NAMED safety event DRAFT created from this confirmed candidate, or the
    // closed reason why the hand-off is still pending. Set once, in the same audited confirmation transaction.
    public Guid? SafetyEventId {get;set;}
    public string? HandoffReason {get;set;}
}
public sealed class VoiceEventDraftRevision : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid VesselId {get;set;}
    public Guid DraftId {get;set;}
    public long Revision {get;set;}
    public string State {get;set;}="DRAFT";
    public string ReasonCode {get;set;}="";
    public string FieldsJson {get;set;}="{}";
    public string EvidenceJson {get;set;}="[]";
    public string Hash {get;set;}="";
    public Guid ActorId {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}
public sealed class VoiceEventDraftReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public string Action {get;set;}="";
    public string Fingerprint {get;set;}="";
    public Guid VesselId {get;set;}
    public Guid ResourceId {get;set;}
    public long Revision {get;set;}
    public DateTimeOffset SavedAt {get;set;}
}

internal static class VoiceEventDraftStorage
{
    internal static readonly string[] Operations = ["VoiceDraftCreate", "VoiceDraftUpdate", "VoiceDraftConfirm", "VoiceDraftReject", "VoiceDraftLinkSafetyEvent"];
    private const string Provenance="\"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')";
    internal static void Configure(ModelBuilder model) {
        model.Entity<VoiceEventDraft>(b=> {
            b.ToTable("VoiceEventDrafts","intelligence",t=>t.HasCheckConstraint("CK_VoiceEventDraft_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"TranscriptOrigin\" IN ('TYPED','LOCAL_SPEECH') AND "+Provenance+" AND "+
                "\"AudioRetention\"='NOT_RETAINED' AND length(\"Transcript\") BETWEEN 1 AND 4000 AND \"TranscriptSha256\" ~ '^[0-9a-f]{64}$' AND "+
                "((\"TranscriptOrigin\"='LOCAL_SPEECH')=(\"SpeechEngineVersion\" IS NOT NULL AND \"SpeechModelVersion\" IS NOT NULL)) AND "+
                "(\"SpeechConfidence\" IS NULL OR \"SpeechConfidence\" BETWEEN 0 AND 1) AND "+
                "((\"State\"='CONFIRMED')=(\"ConfirmedBy\" IS NOT NULL AND \"ConfirmedAt\" IS NOT NULL AND \"CorrectionsJson\" IS NOT NULL)) AND "+
                "((\"State\"='REJECTED')=(\"RejectedBy\" IS NOT NULL AND \"RejectedAt\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL)) AND "+
                "(\"RejectionReason\" IS NULL OR \"RejectionReason\" IN ('TRANSCRIPT_WRONG','FIELDS_WRONG','NOT_AN_EVENT','DUPLICATE','OTHER')) AND "+
                "(\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ConfirmedBy\" IS NULL)"));
            b.HasKey(r=>new{r.TenantId,r.Id});b.HasAlternateKey(r=>new{r.TenantId,r.VesselId,r.Id});b.Property(r=>r.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(r=>new{r.TenantId,r.VesselId,r.SavedAt,r.Id});b.HasIndex(r=>new{r.TenantId,r.VesselId,r.State});
            b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.TranscriptOrigin).HasMaxLength(16);b.Property(r=>r.Transcript).HasColumnType("text");
            b.Property(r=>r.TranscriptSha256).HasMaxLength(64);b.Property(r=>r.SpeechEngineVersion).HasMaxLength(128);b.Property(r=>r.SpeechModelVersion).HasMaxLength(128);
            b.Property(r=>r.SpeechConfidence).HasPrecision(5,4);
            b.Property(r=>r.ExtractorVersion).HasMaxLength(64);b.Property(r=>r.RuleSetVersion).HasMaxLength(64);b.Property(r=>r.CandidateJson).HasColumnType("text");
            b.Property(r=>r.FieldsJson).HasColumnType("text");b.Property(r=>r.EvidenceJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
            b.Property(r=>r.CorrectionsJson).HasColumnType("text");b.Property(r=>r.RejectionReason).HasMaxLength(24);b.Property(r=>r.RejectionNote).HasMaxLength(500);
            b.Property(r=>r.AudioRetention).HasMaxLength(16);b.Property(r=>r.Provenance).HasMaxLength(32);
            b.Property(r=>r.HandoffReason).HasMaxLength(64);
            b.ToTable(t=>t.HasCheckConstraint("CK_VoiceEventDraft_SafetyEventHandoff",
                "(\"SafetyEventId\" IS NULL OR \"State\"='CONFIRMED' AND \"HandoffReason\" IS NULL) AND "+
                "(\"HandoffReason\" IS NULL OR \"State\"='CONFIRMED' AND \"HandoffReason\" IN ('SAFETY_EVENT_REPORT_PERMISSION_REQUIRED'))"));
            b.HasIndex(r=>new{r.TenantId,r.SafetyEventId}).IsUnique().HasFilter("\"SafetyEventId\" IS NOT NULL");
            b.HasOne<SafetyEvent>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.SafetyEventId}).HasPrincipalKey(e=>new{e.TenantId,e.VesselId,e.Id})
                .HasConstraintName("FK_VoiceEventDraft_SafetyEvent").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<VoiceEventDraftRevision>(b=> {
            b.ToTable("VoiceEventDraftRevisions","intelligence",t=>t.HasCheckConstraint("CK_VoiceEventDraftRevision_State",
                "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"ReasonCode\" IN ('CREATE','EDIT','CONFIRM','REJECT','LINK')"));
            b.HasKey(r=>new{r.TenantId,r.DraftId,r.Revision});
            b.HasOne<VoiceEventDraft>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId,r.DraftId}).HasPrincipalKey(r=>new{r.TenantId,r.VesselId,r.Id})
                .HasConstraintName("FK_VoiceEventDraftRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.State).HasMaxLength(16);b.Property(r=>r.ReasonCode).HasMaxLength(16);b.Property(r=>r.FieldsJson).HasColumnType("text");
            b.Property(r=>r.EvidenceJson).HasColumnType("text");b.Property(r=>r.Hash).HasMaxLength(64);
        });
        model.Entity<VoiceEventDraftReceipt>(b=> {
            b.ToTable("VoiceEventDraftReceipts","intelligence",t=>t.HasCheckConstraint("CK_VoiceEventDraftReceipt_Revision","\"Revision\">0"));
            b.HasKey(r=>new{r.TenantId,r.ActorId,r.Key});
            b.HasOne<Vessel>().WithMany().HasForeignKey(r=>new{r.TenantId,r.VesselId}).OnDelete(DeleteBehavior.Restrict);
            b.Property(r=>r.Key).HasMaxLength(80);b.Property(r=>r.Action).HasMaxLength(48);b.Property(r=>r.Fingerprint).HasMaxLength(64);
        });
    }

    private static bool Changed(EntityEntry e,params string[] allowed)=>e.Properties.Any(p=>p.IsModified && !allowed.Contains(p.Metadata.Name));
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes) {
        if(changes.Any(e=>e.Entity is VoiceEventDraftRevision or VoiceEventDraftReceipt && e.State!=EntityState.Added))
            throw new InvalidOperationException("Voice draft history and command outcomes are immutable.");
        if(changes.Any(e=>e.Entity is VoiceEventDraft d && (e.State==EntityState.Deleted || e.State==EntityState.Added && (d.State!="DRAFT" || d.Revision!=1) ||
            e.State==EntityState.Modified && ((string)e.Property("State").OriginalValue! != "DRAFT" && !LateLink(e,d) ||
                Changed(e,"Revision","State","FieldsJson","EvidenceJson","Hash","ActorId","SavedAt","ConfirmedBy","ConfirmedAt","CorrectionsJson","RejectedBy","RejectedAt","RejectionReason","RejectionNote","SafetyEventId","HandoffReason") ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue!+1))))
            throw new InvalidOperationException("A voice draft starts as DRAFT, its transcript and candidate are immutable and confirmed/rejected drafts are terminal.");
    }
    // QA S9 N7: the only change to a CONFIRMED draft is the one-time late hand-off of a pending candidate to its event DRAFT.
    private static bool LateLink(EntityEntry e, VoiceEventDraft d) => (string)e.Property("State").OriginalValue! == "CONFIRMED" && d.State == "CONFIRMED" &&
        e.Property("SafetyEventId").OriginalValue == null && d.SafetyEventId != null &&
        (string?)e.Property("HandoffReason").OriginalValue == "SAFETY_EVENT_REPORT_PERMISSION_REQUIRED" && d.HandoffReason == null &&
        !Changed(e,"Revision","Hash","ActorId","SavedAt","SafetyEventId","HandoffReason");
}
