using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// PLAN-1511-5802 / 58.02 shared custody owner (C04). The controlled identity, immutable
// revision content, append-only custody chain, independent verification, legal hold,
// retention and disposal live here once. Domain records (Credential, VesselCertificate,
// ControlEvidence) keep their business meaning and reference this owner through per-domain
// link tables; they never own custody/hold/disposal state themselves.
public sealed class ControlledDocument : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public string Classification { get; set; } = "INTERNAL";
    public long? CurrentRevision { get; set; }
    public long LatestRevision { get; set; }
    public long Sequence { get; set; }
    public bool HoldActive { get; set; }
    public string? HoldAuthorityRef { get; set; }
    public Guid? HoldPlacedBy { get; set; }
    public DateTimeOffset? HoldPlacedAt { get; set; }
    // QA F1: the releaser of the last hold, kept until the next placement; DB check + trigger
    // enforce releaser <> placer and match the HOLD_RELEASED custody event actor.
    public Guid? HoldReleasedBy { get; set; }
    public DateTimeOffset? HoldReleasedAt { get; set; }
    public string? RetentionPolicyRef { get; set; }
    public DateOnly? RetainUntil { get; set; }
    public string DisposalState { get; set; } = "NONE";
    public Guid? DisposalRequestedBy { get; set; }
    public DateTimeOffset? DisposalRequestedAt { get; set; }
    public Guid? DisposalApprovedBy { get; set; }
    public DateTimeOffset? DisposalApprovedAt { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Provenance { get; set; } = "DeclaredUnverified";
    public uint Version { get; set; }
}

// Content columns are immutable (EF guard + DB trigger). Only the separate intake and
// publication dimensions advance, forward only. No verification column exists here:
// verification is a separate append-only human record.
public sealed class ControlledDocumentRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public Guid VesselId { get; set; }
    public Guid FileId { get; set; }
    public required string ContentSha256 { get; set; }
    public long ContentLength { get; set; }
    public required string Mime { get; set; }
    public Guid UploadedBy { get; set; }
    public Guid RegisteredBy { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public string IntakeState { get; set; } = "QUARANTINED";
    public string? ObjectGeneration { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string PublicationState { get; set; } = "UNPUBLISHED";
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

// Append-only custody chain. OccurredAt is when the fact happened (e.g. the secure-file
// scan); RecordedAt/RecordedBy are when/who appended it. SubjectActorId names the actor
// of a pipeline fact (the uploader whose transport was scanned) when it differs.
public sealed class ControlledDocumentCustodyEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public long Sequence { get; set; }
    public Guid VesselId { get; set; }
    public long? Revision { get; set; }
    public required string EventType { get; set; }
    public required string RecordedBy { get; set; }
    public Guid? SubjectActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Source { get; set; } = "COMMAND";
    public string? Basis { get; set; }
}

public sealed class ControlledDocumentVerification : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public long Revision { get; set; }
    public Guid VesselId { get; set; }
    public Guid VerifierId { get; set; }
    public required string Method { get; set; }
    public required string Result { get; set; }
    public required string Basis { get; set; }
    public required string ContentSha256 { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}

public sealed class ControlledDocumentReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public Guid VesselId { get; set; }
    public Guid DocumentId { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public required string OutcomeJson { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

// C04 per-domain adapters: real composite FKs to each existing owner, append-only.
public sealed class ControlledDocumentCertificateLink : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid CertificateId { get; set; }
    public Guid VesselId { get; set; }
    public Guid LinkedBy { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class ControlledDocumentEvidenceLink : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid EvidenceId { get; set; }
    public Guid VesselId { get; set; }
    public string EvidenceKind { get; set; } = "Evidence";
    public Guid LinkedBy { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class ControlledDocumentCredentialLink : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid VesselId { get; set; }
    public Guid LinkedBy { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}

public static class ControlledDocumentStorage
{
    private static string In(params string[] values) => string.Join(',', values.Select(v => "'" + v + "'"));

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<ControlledDocument>(b => {
            b.ToTable("ControlledDocuments", "files", t => {
                t.HasCheckConstraint("CK_ControlledDocuments_State",
                    $"\"Classification\" IN ({In(ControlledDocumentPolicy.Classifications)}) AND \"DisposalState\" IN ({In(ControlledDocumentPolicy.DisposalStates)}) " +
                    "AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND \"Sequence\" > 0 AND \"LatestRevision\" >= 0 " +
                    "AND (\"CurrentRevision\" IS NULL OR (\"CurrentRevision\" > 0 AND \"CurrentRevision\" <= \"LatestRevision\"))");
                t.HasCheckConstraint("CK_ControlledDocuments_Hold",
                    "NOT \"HoldActive\" OR (\"HoldPlacedBy\" IS NOT NULL AND \"HoldPlacedAt\" IS NOT NULL AND \"HoldAuthorityRef\" IS NOT NULL)");
                t.HasCheckConstraint("CK_ControlledDocuments_HoldRelease",
                    "(\"HoldReleasedBy\" IS NULL) = (\"HoldReleasedAt\" IS NULL) AND (\"HoldReleasedBy\" IS NULL OR (NOT \"HoldActive\" " +
                    "AND \"HoldPlacedBy\" IS NOT NULL AND \"HoldReleasedBy\" <> \"HoldPlacedBy\"))");
                t.HasCheckConstraint("CK_ControlledDocuments_Disposal",
                    "((\"DisposalState\" = 'NONE') = (\"DisposalRequestedBy\" IS NULL AND \"DisposalRequestedAt\" IS NULL)) " +
                    "AND ((\"DisposalState\" = 'APPROVED') = (\"DisposalApprovedBy\" IS NOT NULL AND \"DisposalApprovedAt\" IS NOT NULL)) " +
                    "AND (\"DisposalApprovedBy\" IS NULL OR \"DisposalApprovedBy\" <> \"DisposalRequestedBy\") " +
                    "AND (\"DisposalState\" = 'NONE' OR (\"RetentionPolicyRef\" IS NOT NULL AND \"RetainUntil\" IS NOT NULL))");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId }).HasName("AK_ControlledDocuments_Scope");
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasDatabaseName("IX_ControlledDocuments_Code");
            b.HasIndex(x => new { x.TenantId, x.VesselId }).HasDatabaseName("IX_ControlledDocuments_Vessel");
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_ControlledDocuments_Vessel").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Classification).HasMaxLength(16); b.Property(x => x.HoldAuthorityRef).HasMaxLength(200);
            b.Property(x => x.RetentionPolicyRef).HasMaxLength(64); b.Property(x => x.DisposalState).HasMaxLength(16);
            b.Property(x => x.CreatedBy).HasMaxLength(300); b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<ControlledDocumentRevision>(b => {
            b.ToTable("ControlledDocumentRevisions", "files", t => t.HasCheckConstraint("CK_ControlledDocumentRevisions",
                $"\"Revision\" > 0 AND \"ContentLength\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{{64}}$' " +
                $"AND \"IntakeState\" IN ({In(ControlledDocumentPolicy.IntakeStates)}) AND \"PublicationState\" IN ({In(ControlledDocumentPolicy.PublicationStates)}) " +
                "AND ((\"IntakeState\" = 'RELEASED') = (\"ReleasedAt\" IS NOT NULL)) " +
                "AND ((\"PublicationState\" = 'UNPUBLISHED') = (\"PublishedBy\" IS NULL AND \"PublishedAt\" IS NULL)) " +
                "AND (\"PublicationState\" = 'UNPUBLISHED' OR \"IntakeState\" = 'RELEASED') " +
                "AND (\"PublishedBy\" IS NULL OR (\"PublishedBy\" <> \"UploadedBy\" AND \"PublishedBy\" <> \"RegisteredBy\"))"));
            b.HasKey(x => new { x.TenantId, x.DocumentId, x.Revision });
            b.HasIndex(x => new { x.TenantId, x.FileId }).IsUnique().HasDatabaseName("IX_ControlledDocumentRevisions_File");
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentRevisions_Document");
            b.HasIndex(x => new { x.TenantId, x.FileId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentRevisions_Stage");
            // QA F4: at most one EFFECTIVE revision per document.
            b.HasIndex(x => new { x.TenantId, x.DocumentId }).IsUnique().HasFilter("\"PublicationState\" = 'EFFECTIVE'")
                .HasDatabaseName("IX_ControlledDocumentRevisions_Effective");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentRevisions_Document").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FileStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.FileId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentRevisions_FileStage").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ContentSha256).HasMaxLength(64); b.Property(x => x.Mime).HasMaxLength(32);
            b.Property(x => x.IntakeState).HasMaxLength(16); b.Property(x => x.PublicationState).HasMaxLength(16);
            b.Property(x => x.ObjectGeneration).HasMaxLength(1024);
        });
        model.Entity<ControlledDocumentCustodyEvent>(b => {
            b.ToTable("ControlledDocumentCustodyEvents", "files", t => t.HasCheckConstraint("CK_ControlledDocumentCustodyEvents",
                $"\"Sequence\" > 0 AND \"EventType\" IN ({In(ControlledDocumentPolicy.EventTypes)}) " +
                "AND \"Source\" IN ('COMMAND','SECURE_FILE_PIPELINE','SYNTHETIC_SEED') AND length(\"RecordedBy\") > 0 " +
                "AND (\"Revision\" IS NULL OR \"Revision\" > 0) AND (\"Source\" <> 'SECURE_FILE_PIPELINE' OR \"SubjectActorId\" IS NOT NULL)"));
            b.HasKey(x => new { x.TenantId, x.DocumentId, x.Sequence });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentCustodyEvents_Document");
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.Revision }).HasDatabaseName("IX_ControlledDocumentCustodyEvents_Revision");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentCustodyEvents_Document").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlledDocumentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.Revision })
                .HasConstraintName("FK_ControlledDocumentCustodyEvents_Revision").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.EventType).HasMaxLength(32); b.Property(x => x.RecordedBy).HasMaxLength(300);
            b.Property(x => x.Source).HasMaxLength(32); b.Property(x => x.Basis).HasMaxLength(1000);
        });
        model.Entity<ControlledDocumentVerification>(b => {
            b.ToTable("ControlledDocumentVerifications", "files", t => t.HasCheckConstraint("CK_ControlledDocumentVerifications",
                $"\"Method\" IN ({In(ControlledDocumentPolicy.VerificationMethods)}) AND \"Result\" IN ({In(ControlledDocumentPolicy.VerificationResults)}) " +
                "AND length(btrim(\"Basis\")) > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$'"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.Revision }).HasDatabaseName("IX_ControlledDocumentVerifications_Revision");
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentVerifications_Document");
            b.HasOne<ControlledDocumentRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.Revision })
                .HasConstraintName("FK_ControlledDocumentVerifications_Revision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentVerifications_Document").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Method).HasMaxLength(32); b.Property(x => x.Result).HasMaxLength(16);
            b.Property(x => x.Basis).HasMaxLength(1000); b.Property(x => x.ContentSha256).HasMaxLength(64);
        });
        model.Entity<ControlledDocumentReceipt>(b => {
            b.ToTable("ControlledDocumentReceipts", "files");
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentReceipts_Document");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentReceipts_Document").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Action).HasMaxLength(48); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
        model.Entity<ControlledDocumentCertificateLink>(b => {
            b.ToTable("ControlledDocumentCertificateLinks", "files");
            b.HasKey(x => new { x.TenantId, x.DocumentId, x.CertificateId });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentCertificateLinks_Document");
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.CertificateId }).HasDatabaseName("IX_ControlledDocumentCertificateLinks_Certificate");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentCertificateLinks_Document").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<VesselCertificate>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.CertificateId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).HasConstraintName("FK_ControlledDocumentCertificateLinks_Certificate").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ControlledDocumentEvidenceLink>(b => {
            b.ToTable("ControlledDocumentEvidenceLinks", "files", t => t.HasCheckConstraint("CK_ControlledDocumentEvidenceLinks", "\"EvidenceKind\" = 'Evidence'"));
            b.HasKey(x => new { x.TenantId, x.DocumentId, x.EvidenceId });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentEvidenceLinks_Document");
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.EvidenceKind, x.EvidenceId }).HasDatabaseName("IX_ControlledDocumentEvidenceLinks_Evidence");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentEvidenceLinks_Document").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ControlEvidenceHead>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.EvidenceKind, x.EvidenceId })
                .HasConstraintName("FK_ControlledDocumentEvidenceLinks_Evidence").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.EvidenceKind).HasMaxLength(16);
        });
        model.Entity<ControlledDocumentCredentialLink>(b => {
            b.ToTable("ControlledDocumentCredentialLinks", "files");
            b.HasKey(x => new { x.TenantId, x.DocumentId, x.CredentialId });
            b.HasIndex(x => new { x.TenantId, x.DocumentId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentCredentialLinks_Document");
            b.HasIndex(x => new { x.TenantId, x.CredentialId, x.VesselId }).HasDatabaseName("IX_ControlledDocumentCredentialLinks_Credential");
            b.HasOne<ControlledDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentCredentialLinks_Document").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Credential>().WithMany().HasForeignKey(x => new { x.TenantId, x.CredentialId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ControlledDocumentCredentialLinks_Credential").OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static readonly string[] RevisionStateColumns = [nameof(ControlledDocumentRevision.IntakeState), nameof(ControlledDocumentRevision.ObjectGeneration),
        nameof(ControlledDocumentRevision.ReleasedAt), nameof(ControlledDocumentRevision.PublicationState), nameof(ControlledDocumentRevision.PublishedBy),
        nameof(ControlledDocumentRevision.PublishedAt)];
    private static readonly string[] DocumentImmutableColumns = [nameof(ControlledDocument.TenantId), nameof(ControlledDocument.Id), nameof(ControlledDocument.VesselId),
        nameof(ControlledDocument.Code), nameof(ControlledDocument.Title), nameof(ControlledDocument.Classification), nameof(ControlledDocument.CreatedBy),
        nameof(ControlledDocument.CreatedAt), nameof(ControlledDocument.Provenance)];

    // Application-side mirror of the DB triggers; both must hold.
    internal static void Guard(PoseidonDbContext db, IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is ControlledDocumentCustodyEvent or ControlledDocumentVerification or ControlledDocumentReceipt or
            ControlledDocumentCertificateLink or ControlledDocumentEvidenceLink or ControlledDocumentCredentialLink && e.State != EntityState.Added))
            throw new InvalidOperationException("Controlled document custody, verification, receipt and link history is append-only.");
        static Guid? Owner(object entity) => entity switch
        {
            ControlledDocumentRevision r => r.DocumentId, ControlledDocumentVerification v => v.DocumentId, ControlledDocumentCertificateLink l => l.DocumentId,
            ControlledDocumentEvidenceLink l => l.DocumentId, ControlledDocumentCredentialLink l => l.DocumentId, _ => null
        };
        foreach (var entry in changes.Where(e => Owner(e.Entity) != null))
            if (!changes.Any(x => x.State is EntityState.Added or EntityState.Modified && x.Entity is ControlledDocument d &&
                d.TenantId == ((ITenantEntity)entry.Entity).TenantId && d.Id == Owner(entry.Entity)))
                throw new InvalidOperationException("Controlled document revision, verification and link changes require their custody-appending document change.");
        foreach (var entry in changes.Where(e => e.Entity is ControlledDocumentRevision))
        {
            var r = (ControlledDocumentRevision)entry.Entity;
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Controlled document revisions cannot be removed.");
            if (entry.State == EntityState.Added)
            {
                if (r.IntakeState != "QUARANTINED" || r.PublicationState != "UNPUBLISHED" || r.ObjectGeneration != null || r.ReleasedAt != null || r.PublishedBy != null)
                    throw new InvalidOperationException("A registered revision starts quarantined and unpublished; an upload is never released, published or verified.");
                continue;
            }
            if (entry.Properties.Any(p => p.IsModified && !RevisionStateColumns.Contains(p.Metadata.Name)))
                throw new InvalidOperationException("Controlled document revision content is immutable.");
            var intake = (string)entry.Property(nameof(r.IntakeState)).OriginalValue!;
            var publication = (string)entry.Property(nameof(r.PublicationState)).OriginalValue!;
            if (intake != r.IntakeState && !(intake == "QUARANTINED" && r.IntakeState == "RELEASED") ||
                publication != r.PublicationState && !(publication == "UNPUBLISHED" && r.PublicationState == "EFFECTIVE" || publication == "EFFECTIVE" && r.PublicationState == "SUPERSEDED") ||
                entry.Property(nameof(r.ObjectGeneration)).OriginalValue is string generation && generation != r.ObjectGeneration)
                throw new InvalidOperationException("Controlled document revision states only advance forward.");
        }
        foreach (var entry in changes.Where(e => e.Entity is ControlledDocument))
        {
            var d = (ControlledDocument)entry.Entity;
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Controlled documents cannot be removed; disposal is a recorded state.");
            var before = entry.State == EntityState.Added ? 0 : (long)entry.Property(nameof(d.Sequence)).OriginalValue!;
            if (entry.State == EntityState.Modified)
            {
                if (entry.Properties.Any(p => p.IsModified && DocumentImmutableColumns.Contains(p.Metadata.Name)))
                    throw new InvalidOperationException("Controlled document identity is immutable.");
                var disposal = (string)entry.Property(nameof(d.DisposalState)).OriginalValue!;
                // NONE -> REQUESTED -> APPROVED; REQUESTED -> NONE only as a recorded invalidation (58.02 §100).
                var transition = disposal == d.DisposalState || disposal == "NONE" && d.DisposalState == "REQUESTED" ||
                    disposal == "REQUESTED" && (d.DisposalState == "APPROVED" || d.DisposalState == "NONE" && changes.Any(x => x.State == EntityState.Added &&
                        x.Entity is ControlledDocumentCustodyEvent c && c.TenantId == d.TenantId && c.DocumentId == d.Id && c.EventType == "DISPOSAL_REQUEST_INVALIDATED"));
                if (!transition || disposal != d.DisposalState && d.DisposalState != "NONE" && d.HoldActive)
                    throw new InvalidOperationException("Disposal only advances and a legal hold blocks disposal.");
                var wasHeld = (bool)entry.Property(nameof(d.HoldActive)).OriginalValue!;
                if (wasHeld && !d.HoldActive && (d.HoldReleasedBy == null || d.HoldReleasedBy == d.HoldPlacedBy))
                    throw new InvalidOperationException("A legal hold is released by an actor other than its placer.");
            }
            if (d.Sequence <= before || d.Id == Guid.Empty) throw new InvalidOperationException("Every controlled document change appends custody.");
            for (var sequence = before + 1; sequence <= d.Sequence; sequence++)
                if (!changes.Any(x => x.State == EntityState.Added && x.Entity is ControlledDocumentCustodyEvent c && c.TenantId == d.TenantId && c.DocumentId == d.Id && c.Sequence == sequence))
                    throw new InvalidOperationException("Controlled document custody sequence has a gap.");
        }
        foreach (var entry in changes.Where(e => e.Entity is ControlledDocumentCustodyEvent))
        {
            var c = (ControlledDocumentCustodyEvent)entry.Entity;
            if (c.RecordedBy != db.AuditActorId || !ControlledDocumentPolicy.EventTypes.Contains(c.EventType) ||
                c.Source == "SYNTHETIC_SEED" != db.IsAdministrativeImport || c.Source == "SYNTHETIC_SEED" && c.EventType != "CREATED" ||
                !changes.Any(x => x.State is EntityState.Added or EntityState.Modified && x.Entity is ControlledDocument d && d.TenantId == c.TenantId && d.Id == c.DocumentId && d.Sequence >= c.Sequence))
                throw new InvalidOperationException("Custody events are attributed to the auditing actor and its document change.");
        }
        foreach (var entry in changes.Where(e => e.Entity is ControlledDocument && e.State == EntityState.Added))
        {
            var d = (ControlledDocument)entry.Entity;
            if (d.Provenance == "SyntheticUnverified" != db.IsAdministrativeImport || d.LatestRevision != 0 || d.CurrentRevision != null || d.DisposalState != "NONE" || d.HoldActive)
                throw new InvalidOperationException("A controlled document starts empty, unheld and undisposed.");
        }
    }
}
