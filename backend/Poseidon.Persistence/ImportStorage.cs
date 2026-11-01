using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// Private normalized declaration. Ownership never comes from CertificateMetadata.
public sealed class Credential : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid PersonId { get; set; }
    public string? CertificateTypeReference { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Issuer { get; set; }
    // Source numeric/precision TBD: lossless lexical numeric capture, never ISO code.
    public string? IssuingCountryNumeric { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? Limitations { get; set; }
    public bool? EndorsementRequired { get; set; }
    public string? EndorsementNumber { get; set; }
    public DateOnly? EndorsementExpiry { get; set; }
    public string? StcwReference { get; set; }
    public string? TankerRelevance { get; set; }
    public string? VesselTypeScope { get; set; }
    public Guid? LegacyDocumentReference { get; set; }
    public Guid? DocumentReference { get; set; }
    public string? RenewalLeadDaysNumeric { get; set; }
    public string? SourceSystem { get; set; }
    public string? Notes { get; set; }
    public string VerificationState { get; set; } = "Unverified";
    public string AlertState { get; set; } = "Unknown";
    public string SyncState { get; set; } = "Unknown";
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public CertificateMetadata Metadata() => new(new(CertificateTypeReference, CertificateNumber, Issuer, IssueDate, ExpiryDate, Limitations),
        IssuingCountryNumeric, EndorsementRequired, EndorsementNumber, EndorsementExpiry, StcwReference, TankerRelevance,
        VesselTypeScope, DocumentReference, RenewalLeadDaysNumeric, SourceSystem, Notes);
    public void Capture(CertificateMetadata m) {
        if (!CertificateMetadataPolicy.Valid(m)) throw new ArgumentException("invalid_certificate_metadata");
        CertificateTypeReference = m.Declaration.CertificateTypeReference; CertificateNumber = m.Declaration.CertificateNumber;
        Issuer = m.Declaration.Issuer; IssueDate = m.Declaration.IssueDate; ExpiryDate = m.Declaration.ExpiryDate;
        Limitations = m.Declaration.Limitations; IssuingCountryNumeric = m.IssuingCountryNumeric;
        EndorsementRequired = m.EndorsementRequired; EndorsementNumber = m.EndorsementNumber;
        EndorsementExpiry = m.EndorsementExpiry; StcwReference = m.StcwReference; TankerRelevance = m.TankerRelevance;
        VesselTypeScope = m.VesselTypeScope; DocumentReference = m.DocumentReference;
        RenewalLeadDaysNumeric = m.RenewalLeadDaysNumeric; SourceSystem = m.SourceSystem; Notes = m.Notes;
    }
    public override string ToString() => nameof(Credential);
}

// History deliberately records no old certificate numbers/PII/body values.
public sealed class CredentialRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
}

public sealed class ImportBatch : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid ActorId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string SessionHash { get; set; } = "";
    public string Category { get; set; } = "";
    public string TemplateVersion { get; set; } = "";
    public string TemplateFingerprint { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string SourceLocator { get; set; } = "18.01/Backlog/row70;15.01/paragraph226";
    // Closed, server-parsed table and bindings only. Never projected or logged.
    public string TableJson { get; set; } = "";
    public string BindingsJson { get; set; } = "[]";
    public long Revision { get; set; } = 1;
    public string State { get; set; } = "Uploaded";
    public int RowCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
    public Guid? CommittedPreviewId { get; set; }
    public string ReviewerState { get; set; } = "Unassigned";
    public uint Version { get; set; }
    public override string ToString() => nameof(ImportBatch);
}

public sealed class ImportPreview : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid VesselId { get; set; }
    public long BatchRevision { get; set; }
    public string TemplateFingerprint { get; set; } = "";
    public string BindingsJson { get; set; } = "[]";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int RejectedRows { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class ImportRowResult : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PreviewId { get; set; }
    public Guid VesselId { get; set; }
    public int RowNumber { get; set; }
    public string Outcome { get; set; } = "rejected";
    public string IssuesJson { get; set; } = "[]";
}

internal static class ImportStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<Credential>(b => {
            b.ToTable("Credentials", "crew", t => {
                t.HasCheckConstraint("CK_Credential_IssuingCountryNumeric", "\"IssuingCountryNumeric\" IS NULL OR \"IssuingCountryNumeric\" ~ '^(0|[1-9][0-9]*)(\\.[0-9]+)?$'");
                t.HasCheckConstraint("CK_Credential_RenewalLeadDaysNumeric", "\"RenewalLeadDaysNumeric\" IS NULL OR \"RenewalLeadDaysNumeric\" ~ '^(0|[1-9][0-9]*)(\\.[0-9]+)?$'");
            }); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId }).HasName("AK_Credentials_Scope");
            b.HasOne<CrewAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId, x.PersonId }).HasConstraintName("FK_Credentials_Assignments_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Document>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegacyDocumentReference, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_Credentials_Documents_Scope_Document").OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.IssuingCountryNumeric).HasMaxLength(CertificateMetadataPolicy.MaxNumericCharacters);
            b.Property(x => x.RenewalLeadDaysNumeric).HasMaxLength(CertificateMetadataPolicy.MaxNumericCharacters);
            b.HasIndex(x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId });
            b.Property(x => x.LegacyDocumentReference).HasColumnName("DocumentReference");
            b.Property(x => x.DocumentReference).HasColumnName("PersonFileReference");
            b.HasIndex(x => new { x.TenantId, x.LegacyDocumentReference, x.VesselId }).HasDatabaseName("IX_Credentials_TenantId_DocumentReference_VesselId");
            b.HasIndex(x => new { x.TenantId, x.DocumentReference, x.VesselId }).HasDatabaseName("IX_Credentials_TenantId_PersonFileReference_VesselId");
            b.HasOne<FileStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentReference, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_Credentials_PersonFile_Scope").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CredentialRevision>(b => {
            b.ToTable("CredentialRevisions", "crew"); b.HasKey(x => new { x.TenantId, x.CredentialId, x.Revision });
            b.HasOne<Credential>().WithMany().HasForeignKey(x => new { x.TenantId, x.CredentialId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_CredentialRevisions_Credentials_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CredentialId, x.VesselId });
        });
        model.Entity<ImportBatch>(b => {
            b.ToTable("ImportBatches", "integration"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId }).HasName("AK_ImportBatches_Scope");
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).HasConstraintName("FK_ImportBatches_Vessels_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.ActorId, x.CreatedAt }); b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.TenantId, x.VesselId });
        });
        model.Entity<ImportPreview>(b => {
            b.ToTable("ImportPreviews", "integration"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId }).HasName("AK_ImportPreviews_Scope");
            b.HasOne<ImportBatch>().WithMany().HasForeignKey(x => new { x.TenantId, x.BatchId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ImportPreviews_ImportBatches_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.BatchId, x.VesselId });
        });
        model.Entity<ImportRowResult>(b => {
            b.ToTable("ImportRowResults", "integration"); b.HasKey(x => new { x.TenantId, x.PreviewId, x.RowNumber });
            b.HasOne<ImportPreview>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PreviewId, x.VesselId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ImportRowResults_ImportPreviews_Scope").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PreviewId, x.VesselId });
        });
    }
}
