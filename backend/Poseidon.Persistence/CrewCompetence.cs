using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// S7 WP-A (DEV-048/049/050). Every human act below is one immutable, attributed row
// pinned to the exact revision it judged and to the server-provisioned appointment
// used. None is a signature, legal certificate status, GO, permit or Safe Manning
// approval produced by the system. None of these acts is ever seeded.

// DEV-048: independent human verification assertion on a declared crew credential.
public sealed class CrewCredentialAssertion : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid CredentialId { get; set; }
    public long CredentialRevision { get; set; }
    public long Sequence { get; set; }
    public string State { get; set; } = "";
    public Guid ActorId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = "";
    public string AuthorityHash { get; set; } = "";
    public string Rationale { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}

// DEV-049: training-position/competence matrix row (tenant-local operator declaration,
// never an approved STCW catalog). Reference matches a credential type reference for
// credential-backed requirement types.
public sealed class CrewCompetenceRequirement : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid PositionId { get; set; }
    public string RequirementType { get; set; } = "";
    public string Reference { get; set; } = "";
    public bool Mandatory { get; set; }
    public int? ValidityMonths { get; set; }
    public string? VesselTypeScope { get; set; }
    public string? CargoScope { get; set; }
    public Guid? CompetenceLinkId { get; set; }
    public Guid? FamiliarizationLinkId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

// DEV-049: declared training completion / familiarization / competence assessment fact
// for one assignment. Declaration only; a human sign-off is a separate row.
public sealed class CrewCompetenceRecord : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid PersonId { get; set; }
    public Guid RequirementId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? Reference { get; set; }
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public Guid ActorId { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
}

// DEV-049: assessor/officer sign-off. A human record only; never inferred from training.
public sealed class CrewCompetenceSignOff : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public Guid RecordId { get; set; }
    public long RecordRevision { get; set; }
    public long Sequence { get; set; }
    public string Outcome { get; set; } = "";
    public Guid ActorId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = "";
    public string AuthorityHash { get; set; } = "";
    public string Rationale { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}

// DEV-050: the Safe Manning decision of an appointed Master/DPA. It pins the advisory
// evaluation hash it was taken against; the system never records one on its own.
public sealed class CrewSafeManningDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public long Sequence { get; set; }
    public string Decision { get; set; } = "";
    public string EvaluationState { get; set; } = "";
    public string EvaluationHash { get; set; } = "";
    public DateOnly WindowFrom { get; set; }
    public DateOnly WindowTo { get; set; }
    public Guid ActorId { get; set; }
    public Guid RoleId { get; set; }
    public Guid AuthorityId { get; set; }
    public string Duty { get; set; } = "";
    public string AuthorityHash { get; set; } = "";
    public string Rationale { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}

public static class CrewCompetenceStorage
{
    public static readonly string[] AssertionStates = ["Verified", "Not Verified", "Suspicious", "Suspended / Revoked"];
    public static readonly string[] SignOffOutcomes = ["CONFIRMED", "NOT_CONFIRMED"];
    public static readonly string[] Decisions = ["MANNING_ACCEPTED", "MANNING_NOT_ACCEPTED"];
    public static readonly string[] RequirementStatuses = ["ACTIVE", "RETIRED"];
    public static readonly string[] CredentialDuties = ["Crewing", "DPA", "Master"];
    public static readonly string[] AssessmentDuties = ["Assessor", "Master", "ChiefOfficer", "ChiefEngineer"];
    public static readonly string[] DecisionDuties = ["Master", "DPA"];
    private static string In(string column, string[] values) => $"\"{column}\" IN ({string.Join(',', values.Select(v => "'" + v + "'"))})";

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<CrewCredentialAssertion>(b => {
            b.ToTable("CredentialAssertions", "crew", t => t.HasCheckConstraint("CK_CrewCredentialAssertion_State",
                In("State", AssertionStates) + " AND " + In("Duty", CredentialDuties) + " AND \"CredentialRevision\">0 AND \"Sequence\">0"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.CredentialId, x.Sequence }).IsUnique();
            b.HasOne<Credential>().WithMany().HasForeignKey(x => new { x.TenantId, x.CredentialId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasMaxLength(32); b.Property(x => x.Duty).HasMaxLength(32);
            b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Rationale).HasMaxLength(2000);
        });
        model.Entity<CrewCompetenceRequirement>(b => {
            b.ToTable("CompetenceRequirements", "crew", t => t.HasCheckConstraint("CK_CrewCompetenceRequirement_Values",
                In("RequirementType", Poseidon.Domain.CrewRequirementTypes.All) + " AND " + In("Status", RequirementStatuses) +
                " AND (\"ValidityMonths\" IS NULL OR \"ValidityMonths\" BETWEEN 1 AND 240) AND \"Revision\">0"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.PositionId, x.RequirementType, x.Reference }).IsUnique();
            b.HasOne<CrewPosition>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.RequirementType).HasMaxLength(32); b.Property(x => x.Reference).HasMaxLength(200);
            b.Property(x => x.VesselTypeScope).HasMaxLength(100); b.Property(x => x.CargoScope).HasMaxLength(100);
            b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<CrewCompetenceRecord>(b => {
            b.ToTable("CompetenceRecords", "crew", t => t.HasCheckConstraint("CK_CrewCompetenceRecord_Values",
                In("Kind", Poseidon.Domain.CrewCompetenceKinds.All) + " AND \"Revision\">0 AND (\"ValidUntil\" IS NULL OR \"CompletedOn\" IS NULL OR \"ValidUntil\">=\"CompletedOn\")"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.VesselId });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.AssignmentId });
            b.HasOne<CrewAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<CrewCompetenceRequirement>().WithMany().HasForeignKey(x => new { x.TenantId, x.RequirementId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Kind).HasMaxLength(32); b.Property(x => x.Reference).HasMaxLength(200);
            b.Property(x => x.Provenance).HasMaxLength(32); b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<CrewCompetenceSignOff>(b => {
            b.ToTable("CompetenceSignOffs", "crew", t => t.HasCheckConstraint("CK_CrewCompetenceSignOff_Values",
                In("Outcome", SignOffOutcomes) + " AND " + In("Duty", AssessmentDuties) + " AND \"RecordRevision\">0 AND \"Sequence\">0"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.RecordId, x.Sequence }).IsUnique();
            b.HasOne<CrewCompetenceRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.RecordId, x.VesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Outcome).HasMaxLength(16); b.Property(x => x.Duty).HasMaxLength(32);
            b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Rationale).HasMaxLength(2000);
        });
        model.Entity<CrewSafeManningDecision>(b => {
            b.ToTable("SafeManningDecisions", "crew", t => t.HasCheckConstraint("CK_CrewSafeManningDecision_Values",
                In("Decision", Decisions) + " AND " + In("Duty", DecisionDuties) + " AND " + In("EvaluationState", Poseidon.Domain.CrewEligibilityStates.All) +
                " AND \"Sequence\">0 AND \"WindowTo\">=\"WindowFrom\""));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.Sequence }).IsUnique();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Decision).HasMaxLength(32); b.Property(x => x.EvaluationState).HasMaxLength(32);
            b.Property(x => x.EvaluationHash).HasMaxLength(64); b.Property(x => x.Duty).HasMaxLength(32);
            b.Property(x => x.AuthorityHash).HasMaxLength(64); b.Property(x => x.Rationale).HasMaxLength(2000);
        });
    }
}
