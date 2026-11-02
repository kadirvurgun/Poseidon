using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class Person : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string? FullName { get; set; }
    public string? EmployeeNo { get; set; }
    // Legacy plaintext is retained only until an explicit attributable conversion succeeds.
    public string? ProtectedFullName { get; set; }
    public string? ProtectedEmployeeNo { get; set; }
    public string IdentityStorageState { get; set; } = "LegacyUnavailable";
    public long IdentityRevision { get; set; }
    public string EmploymentState { get; set; } = "Unknown";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public DateTimeOffset? SourceObservedAt { get; set; }
    public uint Version { get; set; }
}

// Metadata-only identity history. No private values, ciphertext or private value digests.
public sealed class CrewIdentityRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public required string PolicyVersion { get; set; }
    public required string Purpose { get; set; }
    public required string Operation { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

// Operator-managed tenant-local position references, never a legal qualification catalog.
public sealed class CrewPosition : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public required string Code { get; set; }
    public string? Rank { get; set; }
    public string? Capacity { get; set; }
    public string? Department { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public uint Version { get; set; }
}

public sealed class CrewAssignment : ITenantEntity
{
    public string? AssignmentPolicyVersion { get; set; }
    public string? AssignmentPolicyFingerprint { get; set; }
    public string? PositionPolicyVersion { get; set; }
    public string? PositionPolicyFingerprint { get; set; }
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? PositionId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public string? WatchGroup { get; set; }
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public DateTimeOffset? SourceObservedAt { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public CrewAssignmentRevision Snapshot() => new() {
        TenantId = TenantId, AssignmentId = Id, VesselId = VesselId, PersonId = PersonId,
        PositionId = PositionId, From = From, To = To, ContractEndDate = ContractEndDate,
        Revision = Revision, Status = Status, Provenance = Provenance, SourceObservedAt = SourceObservedAt, SavedAt = DateTimeOffset.UtcNow,
        AssignmentPolicyVersion = AssignmentPolicyVersion, AssignmentPolicyFingerprint = AssignmentPolicyFingerprint,
        PositionPolicyVersion = PositionPolicyVersion, PositionPolicyFingerprint = PositionPolicyFingerprint
    };
}

// No identity/contact/body snapshot. Historical watch text is deliberately unavailable.
public sealed class CrewAssignmentRevision : ITenantEntity
{
    public string? AssignmentPolicyVersion { get; set; }
    public string? AssignmentPolicyFingerprint { get; set; }
    public string? PositionPolicyVersion { get; set; }
    public string? PositionPolicyFingerprint { get; set; }
    public Guid TenantId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid VesselId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? PositionId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public long Revision { get; set; }
    public string Status { get; set; } = "Draft";
    public required string Provenance { get; set; }
    public DateTimeOffset? SourceObservedAt { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

internal static class CrewStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.HasPostgresExtension("btree_gist");
        model.Entity<Person>(b => {
            b.ToTable("Persons", "crew", t => t.HasCheckConstraint("CK_Person_ProtectedIdentity", "\"IdentityStorageState\" IN ('LegacyUnavailable','ProtectedV1') AND (\"IdentityStorageState\" <> 'ProtectedV1' OR (\"FullName\" IS NULL AND \"EmployeeNo\" IS NULL AND \"IdentityRevision\" > 0))")); b.HasKey(x => new { x.TenantId, x.Id }); b.HasIndex(x => x.TenantId);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.FullName).HasMaxLength(200); b.Property(x => x.EmployeeNo).HasMaxLength(64);
            b.Property(x => x.IdentityStorageState).HasMaxLength(32);
            b.Property(x => x.EmploymentState).HasMaxLength(32); b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<CrewIdentityRevision>(b => {
            b.ToTable("IdentityRevisions", "crew"); b.HasKey(x => new { x.TenantId, x.PersonId, x.Revision });
            b.HasOne<Person>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PolicyVersion).HasMaxLength(80); b.Property(x => x.Purpose).HasMaxLength(80);
            b.Property(x => x.Operation).HasMaxLength(32);
        });
        model.Entity<CrewPosition>(b => {
            b.ToTable("Positions", "crew"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.VesselId });
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Code).HasMaxLength(64); b.Property(x => x.Rank).HasMaxLength(100);
            b.Property(x => x.Capacity).HasMaxLength(100); b.Property(x => x.Department).HasMaxLength(100);
            b.Property(x => x.Provenance).HasMaxLength(32); b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<CrewAssignment>(b => {
            b.Property(x => x.AssignmentPolicyVersion).HasMaxLength(80); b.Property(x => x.AssignmentPolicyFingerprint).HasMaxLength(64);
            b.Property(x => x.PositionPolicyVersion).HasMaxLength(80); b.Property(x => x.PositionPolicyFingerprint).HasMaxLength(64);
            b.ToTable("Assignments", "crew", t => t.HasCheckConstraint("CK_Assignment_Period", "\"To\" IS NULL OR (\"From\" IS NOT NULL AND \"To\" > \"From\")"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId, x.PersonId });
            b.HasOne<Person>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewPosition>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.PersonId, x.From });
            b.HasIndex(x => new { x.TenantId, x.PersonId });
            b.Property(x => x.WatchGroup).HasMaxLength(64); b.Property(x => x.Status).HasMaxLength(32);
            b.Property(x => x.Provenance).HasMaxLength(32); b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<CrewAssignmentRevision>(b => {
            b.Property(x => x.AssignmentPolicyVersion).HasMaxLength(80); b.Property(x => x.AssignmentPolicyFingerprint).HasMaxLength(64);
            b.Property(x => x.PositionPolicyVersion).HasMaxLength(80); b.Property(x => x.PositionPolicyFingerprint).HasMaxLength(64);
            b.ToTable("AssignmentRevisions", "crew"); b.HasKey(x => new { x.TenantId, x.AssignmentId, x.Revision });
            b.HasOne<CrewAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Status).HasMaxLength(32); b.Property(x => x.Provenance).HasMaxLength(32);
        });
    }
}
