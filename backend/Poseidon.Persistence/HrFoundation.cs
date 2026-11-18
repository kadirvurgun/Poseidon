using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Poseidon.Persistence;

// PLAN-1511-5201 / 52.01 Rev6 foundation. No wage/payroll/bank/monetary field exists on any HR entity.

// Append-only field/source revision for legal-entity core fields (who/when/source). Values are public business data.
public sealed class LegalEntityFieldRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid LegalEntityId { get; set; }
    public long Revision { get; set; }
    public required string Field { get; set; }
    public string? Value { get; set; }
    public required string SourceKind { get; set; }
    public string? SourceRef { get; set; }
    public required string ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// Append-only field/source revision for HR core fields of the single Person master. Closed codes only; no identity values.
public sealed class PersonFieldRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public required string Field { get; set; }
    public string? Value { get; set; }
    public required string SourceKind { get; set; }
    public string? SourceRef { get; set; }
    public required string ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

// Registration identifier: type + issuer + value. Immutable except retirement; a changed value is retire + add.
public sealed class LegalEntityIdentifier : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid LegalEntityId { get; set; }
    public required string Type { get; set; }
    public required string Issuer { get; set; }
    public required string Value { get; set; }
    public required string SourceKind { get; set; }
    public string? SourceRef { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetiredBy { get; set; }
    public uint Version { get; set; }
}

// Typed, effective-dated legal-entity relation (owner/operator/managers/employer). Declared and unverified.
public sealed class LegalEntityRole : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid LegalEntityId { get; set; }
    public required string Role { get; set; }
    public Guid? VesselId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public required string SourceKind { get; set; }
    public string? SourceRef { get; set; }
    public string VerificationState { get; set; } = "Unverified";
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? EndedBy { get; set; }
    public uint Version { get; set; }
}

// Employment: Person <-> legal employer. Records only; no wage, contract money or payroll.
public sealed class Employment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid LegalEntityId { get; set; }
    public required string EmployeeNo { get; set; }
    public required string EmploymentType { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string State { get; set; } = "Draft";
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

// Planned assignment (plan). Actual facts live in HrActualEvent; planned values never become actual values.
public sealed class HrAssignment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid EmploymentId { get; set; }
    public Guid PersonId { get; set; }
    public Guid LegalEntityId { get; set; }
    public required string Kind { get; set; }
    public Guid? VesselId { get; set; }
    public Guid? PositionId { get; set; }
    public DateTimeOffset PlannedFrom { get; set; }
    public DateTimeOffset PlannedTo { get; set; }
    public required string PlannedZoneId { get; set; }
    public string State { get; set; } = "Planned";
    public required string PlannedBy { get; set; }
    public DateTimeOffset PlannedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

// Actual (fiilî) fact: append-only. Disposition and quality are server classified; doubtful facts are retained.
public sealed class HrActualEvent : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid PersonId { get; set; }
    public required string Kind { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string OriginZoneId { get; set; }
    public required string Origin { get; set; }
    public required string SourceSystem { get; set; }
    public required string SourceEventId { get; set; }
    public required string ContentHash { get; set; }
    public required string Quality { get; set; }
    public required string Disposition { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

internal static class HrStorage
{
    internal const string Schema = "hr";

    internal static void ConfigureCompany(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Companies", t => {
            t.HasCheckConstraint("CK_Company_LegalEntityStatus", "\"LegalEntityStatus\" IN ('Draft','Active','Inactive')");
            t.HasCheckConstraint("CK_Company_LegalEntityPeriod", "\"EffectiveTo\" IS NULL OR (\"EffectiveFrom\" IS NOT NULL AND \"EffectiveTo\" > \"EffectiveFrom\")");
            t.HasCheckConstraint("CK_Company_LegalEntityActive", "\"LegalEntityStatus\" = 'Draft' OR (\"LegalName\" IS NOT NULL AND \"CountryCode\" IS NOT NULL AND \"HrTimeZone\" IS NOT NULL AND \"BaseLocale\" IS NOT NULL AND \"EffectiveFrom\" IS NOT NULL)");
        });
        b.Property(x => x.LegalName).HasMaxLength(300);
        b.Property(x => x.CountryCode).HasMaxLength(2);
        b.Property(x => x.HrTimeZone).HasMaxLength(64);
        b.Property(x => x.BaseLocale).HasMaxLength(5);
        b.Property(x => x.LegalEntityStatus).HasMaxLength(16).HasDefaultValue("Draft");
        b.Property(x => x.LegalEntityRevision).HasDefaultValue(0L);
    }

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<LegalEntityFieldRevision>(b => {
            b.ToTable("LegalEntityFieldRevisions", Schema, t => t.HasCheckConstraint("CK_LegalEntityFieldRevision", "\"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.LegalEntityId, x.Revision, x.Field });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Field).HasMaxLength(32); b.Property(x => x.Value).HasMaxLength(300);
            b.Property(x => x.SourceKind).HasMaxLength(32); b.Property(x => x.SourceRef).HasMaxLength(200);
            b.Property(x => x.ActorId).HasMaxLength(200);
        });
        model.Entity<PersonFieldRevision>(b => {
            b.ToTable("PersonFieldRevisions", Schema, t => t.HasCheckConstraint("CK_PersonFieldRevision", "\"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.PersonId, x.Revision, x.Field });
            b.HasOne<Person>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Field).HasMaxLength(32); b.Property(x => x.Value).HasMaxLength(64);
            b.Property(x => x.SourceKind).HasMaxLength(32); b.Property(x => x.SourceRef).HasMaxLength(200);
            b.Property(x => x.ActorId).HasMaxLength(200);
        });
        model.Entity<LegalEntityIdentifier>(b => {
            b.ToTable("LegalEntityIdentifiers", Schema, t => t.HasCheckConstraint("CK_LegalEntityIdentifier_Retired", "(\"RetiredAt\" IS NULL) = (\"RetiredBy\" IS NULL)"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId });
            // 52.01 paragraph:61: registration numbers identify one legal entity per tenant while current.
            b.HasIndex(x => new { x.TenantId, x.Type, x.Issuer, x.Value }).IsUnique().HasFilter("\"RetiredAt\" IS NULL");
            b.Property(x => x.Type).HasMaxLength(32); b.Property(x => x.Issuer).HasMaxLength(100); b.Property(x => x.Value).HasMaxLength(128);
            b.Property(x => x.SourceKind).HasMaxLength(32); b.Property(x => x.SourceRef).HasMaxLength(200);
            b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.RetiredBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<LegalEntityRole>(b => {
            b.ToTable("LegalEntityRoles", Schema, t => {
                t.HasCheckConstraint("CK_LegalEntityRole_Period", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                t.HasCheckConstraint("CK_LegalEntityRole_Kind", "\"Role\" IN ('RegisteredOwner','Operator','TechnicalManager','CrewManager','CommercialManager','DocCompany','Employer') AND ((\"Role\" = 'Employer') = (\"VesselId\" IS NULL)) AND \"VerificationState\" = 'Unverified'");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId });
            b.HasIndex(x => new { x.TenantId, x.VesselId });
            b.Property(x => x.Role).HasMaxLength(32); b.Property(x => x.VerificationState).HasMaxLength(16);
            b.Property(x => x.SourceKind).HasMaxLength(32); b.Property(x => x.SourceRef).HasMaxLength(200);
            b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.EndedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<Employment>(b => {
            b.ToTable("Employments", Schema, t => {
                t.HasCheckConstraint("CK_Employment_Period", "\"EndAt\" IS NULL OR \"EndAt\" > \"StartAt\"");
                t.HasCheckConstraint("CK_Employment_State", "\"State\" IN ('Draft','Active','Ended','Cancelled') AND \"EmploymentType\" IN ('Employee','Contractor','Agency') AND (\"State\" NOT IN ('Active','Ended') OR (\"ActivatedBy\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"ActivatedBy\" <> \"CreatedBy\")) AND (\"State\" <> 'Ended' OR \"EndAt\" IS NOT NULL)");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.PersonId, x.LegalEntityId });
            b.HasOne<Person>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PersonId });
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EmployeeNo });
            b.Property(x => x.EmployeeNo).HasMaxLength(64); b.Property(x => x.EmploymentType).HasMaxLength(16);
            b.Property(x => x.State).HasMaxLength(16); b.Property(x => x.CreatedBy).HasMaxLength(200); b.Property(x => x.ActivatedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<HrAssignment>(b => {
            b.ToTable("PlannedAssignments", Schema, t => {
                t.HasCheckConstraint("CK_HrAssignment_Period", "\"PlannedTo\" > \"PlannedFrom\"");
                t.HasCheckConstraint("CK_HrAssignment_Kind", "\"Kind\" IN ('Vessel','Shore') AND ((\"Kind\" = 'Vessel') = (\"VesselId\" IS NOT NULL)) AND (\"PositionId\" IS NULL OR \"VesselId\" IS NOT NULL)");
                t.HasCheckConstraint("CK_HrAssignment_State", "\"State\" IN ('Planned','Approved','Cancelled') AND (\"State\" = 'Planned' OR \"State\" = 'Cancelled' OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"PlannedBy\"))");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.PersonId });
            b.HasOne<Employment>().WithMany().HasForeignKey(x => new { x.TenantId, x.EmploymentId, x.PersonId, x.LegalEntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.PersonId, x.LegalEntityId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewPosition>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PersonId, x.PlannedFrom });
            b.HasIndex(x => new { x.TenantId, x.VesselId });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.State).HasMaxLength(16);
            b.Property(x => x.PlannedZoneId).HasMaxLength(64);
            b.Property(x => x.PlannedBy).HasMaxLength(200); b.Property(x => x.ApprovedBy).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<HrActualEvent>(b => {
            b.ToTable("ActualEvents", Schema, t => t.HasCheckConstraint("CK_HrActualEvent", "\"Kind\" IN ('CheckIn','CheckOut') AND \"Origin\" IN ('Self','SupervisorReported') AND \"Quality\" IN ('Valid','Suspect') AND \"Disposition\" IN ('Accepted','PendingReconciliation') AND (\"Quality\" = 'Valid' OR \"Disposition\" = 'PendingReconciliation')"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<HrAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.PersonId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            // Device/offline duplicate: same source event id returns the same receipt (52.01 paragraph:557).
            b.HasIndex(x => new { x.TenantId, x.SourceSystem, x.SourceEventId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.AssignmentId, x.OccurredAt });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.OriginZoneId).HasMaxLength(64);
            b.Property(x => x.Origin).HasMaxLength(24); b.Property(x => x.SourceSystem).HasMaxLength(64);
            b.Property(x => x.SourceEventId).HasMaxLength(128); b.Property(x => x.ContentHash).HasMaxLength(64);
            b.Property(x => x.Quality).HasMaxLength(16); b.Property(x => x.Disposition).HasMaxLength(24);
            b.Property(x => x.RecordedBy).HasMaxLength(200);
        });
    }
}
