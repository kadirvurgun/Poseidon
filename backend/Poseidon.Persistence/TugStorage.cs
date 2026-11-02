using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

public sealed class TugOwner : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid Id { get; set; }
}
public sealed class TugJob : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; }
    public required string JobNumber { get; set; }
    public string? RequestSource { get; set; }
    public string? CustomerRef { get; set; }
    public string? AssistedVessel { get; set; }
    public string? AssistedVesselImo { get; set; }
    public string? JobType { get; set; }
    public DateTimeOffset? RequestedStart { get; set; }
    public DateTimeOffset? RequestedEnd { get; set; }
    public string? OperationArea { get; set; }
    public string? PickupPoint { get; set; }
    public string? DestinationPoint { get; set; }
    public decimal? RequiredBollardPull { get; set; }
    public int? RequiredTugCount { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? Priority { get; set; }
    public Guid? RequestOwner { get; set; }
    public string? Notes { get; set; }
    public string SourceVersion { get; set; } = TugRequestPolicy.SourceVersion;
    public string Status { get; set; } = "Draft";
    public string Provenance { get; set; } = "UserDeclaredUnverified";
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public TugRequestInputs Inputs() => new(CustomerRef, AssistedVessel, AssistedVesselImo, JobType, RequestedStart, RequestedEnd,
        OperationArea, PickupPoint, DestinationPoint, RequiredBollardPull?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        RequiredTugCount, SpecialRequirements, Priority, RequestOwner, Notes, RequestSource);
    public void Apply(TugRequestInputs x) {
        SourceVersion = TugRequestPolicy.SourceVersion; RequestSource = x.RequestSource; CustomerRef = x.CustomerRef; AssistedVessel = x.AssistedVessel; AssistedVesselImo = x.AssistedVesselImo;
        JobType = x.JobType; RequestedStart = x.RequestedStart; RequestedEnd = x.RequestedEnd; OperationArea = x.OperationArea;
        PickupPoint = x.PickupPoint; DestinationPoint = x.DestinationPoint; TugRequestPolicy.TryBp(x.RequiredBollardPull, out var bp);
        RequiredBollardPull = bp; RequiredTugCount = x.RequiredTugCount; SpecialRequirements = x.SpecialRequirements;
        Priority = x.Priority; RequestOwner = x.RequestOwner; Notes = x.Notes; SavedAt = DateTimeOffset.UtcNow;
    }
    public TugJobRevision Snapshot() => new() { TenantId = TenantId, VesselId = VesselId, JobId = Id, Revision = Revision,
        JobNumber = JobNumber, SourceVersion = SourceVersion, ContentJson = JsonSerializer.Serialize(Inputs()), Provenance = Provenance, SavedAt = SavedAt };
}
public sealed class TugJobRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public Guid JobId { get; set; }
    public long Revision { get; set; }
    public required string JobNumber { get; set; }
    public string SourceVersion { get; set; } = TugRequestPolicy.SourceVersion;
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

// Read adapter for actual controlled assignment observations. No assignment/dispatch writer
// or fabricated fixtures: policy/authorized human decision integration remains a separate gate.
public sealed class TugAssignment : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid VesselId { get; set; } // request's context
    public Guid JobId { get; set; }
    public Guid TugId { get; set; }
    public long JobRevision { get; set; }
    public required string DecisionReference { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

internal static class TugStorage
{
    internal static void Configure(ModelBuilder model) {
        model.Entity<TugOwner>(b => {
            b.ToTable("Owners", "tug"); b.HasKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugJob>(b => {
            b.ToTable("Jobs", "tug", t => {
                t.HasCheckConstraint("CK_TugJob_Draft", "\"Status\" = 'Draft'");
                t.HasCheckConstraint("CK_TugJob_Window", "\"RequestedEnd\" IS NULL OR \"RequestedStart\" IS NULL OR \"RequestedEnd\" >= \"RequestedStart\"");
                t.HasCheckConstraint("CK_TugJob_Count", "\"RequiredTugCount\" IS NULL OR \"RequiredTugCount\" BETWEEN 1 AND 1000");
                t.HasCheckConstraint("CK_TugJob_BP", "\"RequiredBollardPull\" IS NULL OR \"RequiredBollardPull\" >= 0");
            });
            b.HasKey(x => new { x.TenantId, x.Id }); b.HasAlternateKey(x => new { x.TenantId, x.VesselId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.JobNumber }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.RequestedStart });
            b.Property(x => x.JobNumber).HasMaxLength(40); b.Property(x => x.RequiredBollardPull).HasPrecision(10, 2);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TugOwner>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.RequestOwner }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugJobRevision>(b => {
            b.ToTable("JobRevisions", "tug"); b.HasKey(x => new { x.TenantId, x.VesselId, x.JobId, x.Revision });
            b.Property(x => x.JobNumber).HasMaxLength(40); b.Property(x => x.ContentJson).HasColumnType("jsonb");
            b.HasOne<TugJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<TugAssignment>(b => {
            b.ToTable("Assignments", "tug"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.JobId, x.TugId }).IsUnique();
            b.Property(x => x.DecisionReference).HasMaxLength(200);
            b.HasOne<TugJobRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.JobId, x.JobRevision }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vessel>().WithMany().HasForeignKey(x => new { x.TenantId, x.TugId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
