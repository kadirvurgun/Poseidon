using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

public sealed class PoseidonDbContext(
    DbContextOptions<PoseidonDbContext> options,
    Guid tenantId,
    ActorContext actor,
    bool administrativeImport = false) : DbContext(options)
{
    public Guid TenantId { get; } = tenantId;
    public bool IsAdministrativeImport { get; } = administrativeImport;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<VesselProfile> VesselProfiles => Set<VesselProfile>();
    public DbSet<TugProfile> TugProfiles => Set<TugProfile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();
    public DbSet<RegulatoryDraft> RegulatoryDrafts => Set<RegulatoryDraft>();
    public DbSet<RegulatoryDraftRevision> RegulatoryDraftRevisions => Set<RegulatoryDraftRevision>();
    public DbSet<RegisterDraft> RegisterDrafts => Set<RegisterDraft>();
    public DbSet<RegisterRevision> RegisterRevisions => Set<RegisterRevision>();

    public DbSet<FileStage> FileStages => Set<FileStage>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<CredentialRevision> CredentialRevisions => Set<CredentialRevision>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportPreview> ImportPreviews => Set<ImportPreview>();
    public DbSet<ImportRowResult> ImportRowResults => Set<ImportRowResult>();
    public DbSet<CrewPersonalProfile> CrewPersonalProfiles => Set<CrewPersonalProfile>();
    public DbSet<CrewPersonalProfileRevision> CrewPersonalProfileRevisions => Set<CrewPersonalProfileRevision>();
    public DbSet<MaintenanceObservation> MaintenanceObservations => Set<MaintenanceObservation>();
    public DbSet<MaintenanceObservationRevocation> MaintenanceObservationRevocations => Set<MaintenanceObservationRevocation>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetRevision> AssetRevisions => Set<AssetRevision>();
    public DbSet<TechnicalOwner> TechnicalOwners => Set<TechnicalOwner>();
    public DbSet<MaintenanceJob> MaintenanceJobs => Set<MaintenanceJob>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<MaintenancePlanRevision> MaintenancePlanRevisions => Set<MaintenancePlanRevision>();
    public DbSet<CrewPosition> CrewPositions => Set<CrewPosition>();
    public DbSet<CrewAssignment> CrewAssignments => Set<CrewAssignment>();
    public DbSet<CrewAssignmentRevision> CrewAssignmentRevisions => Set<CrewAssignmentRevision>();

    public DbSet<TugJob> TugJobs => Set<TugJob>();
    public DbSet<TugJobRevision> TugJobRevisions => Set<TugJobRevision>();
    public DbSet<TugOwner> TugOwners => Set<TugOwner>();
    public DbSet<TugAssignment> TugAssignments => Set<TugAssignment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("core");
        ImportStorage.Configure(model);
        model.Entity<Credential>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CredentialRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportBatch>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportPreview>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportRowResult>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        TugStorage.Configure(model);
        model.Entity<TugJob>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugJobRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugOwner>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugAssignment>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        MaintenanceObservationStorage.Configure(model);
        model.Entity<MaintenanceObservation>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<MaintenanceObservationRevocation>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        TechnicalStorage.Configure(model);
        model.Entity<Asset>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<AssetRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TechnicalOwner>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<MaintenanceJob>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<MaintenancePlan>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<MaintenancePlanRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        CrewPersonalProfileStorage.Configure(model);
        model.Entity<CrewPersonalProfile>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CrewPersonalProfileRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        CrewStorage.Configure(model);
        model.Entity<Person>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CrewPosition>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CrewAssignment>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CrewAssignmentRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        SecureFileStorage.Configure(model);
        DocumentStorage.Configure(model);
        model.Entity<Document>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<DocumentVersion>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<FileStage>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<StoredFile>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        RegisterStorage.Configure(model);
        model.Entity<RegisterDraft>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RegisterRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        RegulatoryDraftStorage.Configure(model);
        model.Entity<RegulatoryDraft>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RegulatoryDraftRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CommandReceipt>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(80);
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
        SecurityEvent.Configure(model);
        model.Entity<SecurityEvent>().HasQueryFilter(x => x.TenantId == TenantId);
        model.Entity<Tenant>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
            b.HasQueryFilter(x => IsAdministrativeImport || x.Id == TenantId);
        });
        model.Entity<Company>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Version).IsRowVersion();
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
        model.Entity<Vessel>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            b.Property(x => x.Version).IsRowVersion();
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
        model.Entity<VesselProfile>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.VesselId });
            b.HasOne<Vessel>().WithOne().HasForeignKey<VesselProfile>(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.GrossTonnage).HasPrecision(14, 2);
            b.Property(x => x.Version).IsRowVersion();
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
        model.Entity<TugProfile>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.VesselId });
            b.HasOne<Vessel>().WithOne().HasForeignKey<TugProfile>(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.BollardPullTonnes).HasPrecision(10, 2);
            b.Property(x => x.Version).IsRowVersion();
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
        model.Entity<AuditEvent>(b =>
        {
            b.ToTable("AuditEvents", "audit");
            b.HasKey(x => x.Id);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.ActorType).HasMaxLength(32);
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.EntityType).HasMaxLength(128);
            b.Property(x => x.EntityId).HasMaxLength(128);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.RoleContext).HasMaxLength(600);
            b.Property(x => x.CapabilityContext).HasMaxLength(2048);
            b.Property(x => x.ChangeSummary).HasMaxLength(2048);
            b.Property(x => x.ResourceKind).HasMaxLength(16);
            b.Property(x => x.ReasonStatus).HasMaxLength(32);
            b.Property(x => x.Source).HasMaxLength(32);
            b.HasIndex(x => new { x.TenantId, x.ResourceId, x.CorrelationId });
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.OccurredAt, x.Id });
            b.HasIndex(x => new { x.TenantId, x.OccurredAt });
            b.HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var generated = PrepareAudit();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch
        {
            foreach (var entry in generated) Entry(entry).State = EntityState.Detached;
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var generated = PrepareAudit();
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch
        {
            foreach (var entry in generated) Entry(entry).State = EntityState.Detached;
            throw;
        }
    }

    private uint? CommittedVersion(CommandReceipt receipt)
    {
        var resource = ChangeTracker.Entries().FirstOrDefault(e => e.Entity switch {
            TugJob j => receipt.Action == "TugSave" && j.Id == receipt.ResourceId && j.TenantId == receipt.TenantId,
            Asset a => receipt.Action == "EquipmentSave" && a.Id == receipt.ResourceId && a.TenantId == receipt.TenantId,
            MaintenancePlan p => receipt.Action == "MaintenanceSave" && p.Id == receipt.ResourceId && p.TenantId == receipt.TenantId,
            CrewPersonalProfile p => receipt.Action is "CrewProfileSave" or "CrewPhotoSave" && p.PersonId == receipt.ResourceId && p.TenantId == receipt.TenantId,
            Person p => receipt.Action == "CrewPersonSave" && p.Id == receipt.ResourceId && p.TenantId == receipt.TenantId,
            CrewAssignment c => receipt.Action == "CrewSave" && c.Id == receipt.ResourceId && c.TenantId == receipt.TenantId,
            Document d => receipt.Action == "DocumentDraftSave" && d.Id == receipt.ResourceId && d.TenantId == receipt.TenantId && d.Revision == receipt.RegisterRevision,
            StoredFile f => receipt.Action == "FileFinalize" && f.Id == receipt.ResourceId && f.TenantId == receipt.TenantId,
            Company c => receipt.Action.StartsWith("Company", StringComparison.Ordinal) && c.Id == receipt.ResourceId && c.TenantId == receipt.TenantId,
            Vessel v => receipt.Action.StartsWith("Vessel", StringComparison.Ordinal) && v.Id == receipt.ResourceId && v.TenantId == receipt.TenantId,
            RegulatoryDraft d => receipt.Action == "ProfileSave" && d.VesselId == receipt.ResourceId && d.TenantId == receipt.TenantId,
            RegisterDraft d => receipt.Action == d.Kind + "DraftSave" && d.Id == receipt.ResourceId && d.TenantId == receipt.TenantId && d.Revision == receipt.RegisterRevision,
            _ => false
        });
        return resource is { State: EntityState.Unchanged } && resource.Metadata.FindProperty("Version") != null &&
            resource.Property("Version").CurrentValue is uint version && version > 0 ? version : null;
    }

    private List<AuditEvent> PrepareAudit()
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (changes.Count == 0) return [];
        if (string.IsNullOrWhiteSpace(actor.Id) || string.IsNullOrWhiteSpace(actor.Type) || string.IsNullOrWhiteSpace(actor.CorrelationId))
            throw new InvalidOperationException("An authenticated or explicit system actor and correlation ID are required.");
        if (changes.Any(e => e.Entity is AuditEvent or SecurityEvent))
            throw new InvalidOperationException("Audit events are generated by the context and cannot be directly changed.");
        if (changes.Any(e => e.Entity is TugJobRevision or TugOwner or TugAssignment && e.State != EntityState.Added))
            throw new InvalidOperationException("Tug lineage is immutable.");
        if (changes.Any(e => e.Entity is TugJob && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Property("VesselId").IsModified || e.Property("JobNumber").IsModified))))
            throw new InvalidOperationException("Tug request identity is immutable.");
        if (changes.Any(e => e.Entity is MaintenanceObservation or MaintenanceObservationRevocation && e.State != EntityState.Added))
            throw new InvalidOperationException("Observation lineage is immutable.");
        if (changes.Any(e => e.Entity is AssetRevision or MaintenancePlanRevision or TechnicalOwner or MaintenanceJob && e.State != EntityState.Added))
            throw new InvalidOperationException("Technical history and references are immutable.");
        if (changes.Any(e => e.Entity is Asset or MaintenancePlan && (e.State == EntityState.Deleted ||
            e.State == EntityState.Modified && e.Property("VesselId").IsModified)))
            throw new InvalidOperationException("Technical ownership is immutable.");
        if (changes.Any(e => e.Entity is CrewPersonalProfileRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Crew profile lineage is immutable.");
        if (changes.Any(e => e.Entity is CrewPersonalProfile && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Property("PersonId").IsModified)))
            throw new InvalidOperationException("Crew profile ownership is immutable.");
        if (changes.Any(e => e.Entity is CrewAssignmentRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Crew revisions are immutable.");
        if (changes.Any(e => e.Entity is CrewAssignment && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Property(nameof(CrewAssignment.PersonId)).IsModified || e.Property(nameof(CrewAssignment.VesselId)).IsModified))))
            throw new InvalidOperationException("Crew ownership is immutable.");
        if (changes.Any(e => e.Entity is StoredFile && e.State != EntityState.Added))
            throw new InvalidOperationException("Stored file revisions are immutable.");
        if (changes.Any(e => e.Entity is DocumentVersion && e.State != EntityState.Added))
            throw new InvalidOperationException("Document draft snapshots are immutable; append a revision.");
        if (changes.Any(e => e.Entity is Document && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(Document.Revision)))))
            throw new InvalidOperationException("Document ownership is immutable.");
        if (changes.Any(e => e.Entity is FileStage && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(FileStage.State)))))
            throw new InvalidOperationException("File intent and ownership are immutable.");
        if (changes.Any(e => e.Entity is CommandReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Command receipts are immutable.");
        if (changes.Any(e => e.Entity is RegulatoryDraftRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Draft revisions are immutable.");
        if (changes.Any(e => e.Entity is RegisterRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Register revisions are immutable.");
        if (changes.Any(e => e.Entity is RegisterDraft && (e.State == EntityState.Deleted ||
            e.State == EntityState.Modified && e.Property(nameof(RegisterDraft.Kind)).IsModified)))
            throw new InvalidOperationException("Register kind and history cannot be removed or reassigned.");
        if (changes.Any(e => e.Entity is ImportPreview or ImportRowResult or CredentialRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Import observations and credential history are immutable.");
        if (changes.Any(e => e.Entity is Credential && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            new[] { "VesselId", "AssignmentId", "PersonId" }.Any(n => e.Property(n).IsModified))))
            throw new InvalidOperationException("Credential ownership is immutable.");
        if (changes.Any(e => e.Entity is ImportBatch && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            new[] { "VesselId", "ActorId", "SessionHash", "AssignmentId", "Category", "TemplateVersion", "TemplateFingerprint", "SourceHash", "TableJson", "SourceName", "SourceLocator" }.Any(n => e.Property(n).IsModified))))
            throw new InvalidOperationException("Import source and ownership are immutable.");
        var events = new List<AuditEvent>();
        foreach (var entry in changes)
        {
            var scope = entry.Entity is Tenant tenant ? tenant.Id : ((ITenantEntity)entry.Entity).TenantId;
            if (scope == Guid.Empty || (!IsAdministrativeImport && scope != TenantId))
                throw new UnauthorizedAccessException("The write is outside the current tenant scope.");
            if (entry.State == EntityState.Modified && entry.Entity is ITenantEntity && entry.Property(nameof(ITenantEntity.TenantId)).IsModified)
                throw new UnauthorizedAccessException("Tenant ownership cannot be changed.");
            var key = entry.Metadata.FindPrimaryKey()!.Properties;
            var metadata = AuditMetadata.From(entry);
            events.Add(new AuditEvent
            {
                Id = Guid.NewGuid(), TenantId = scope, ActorId = actor.Id, ActorType = actor.Type,
                Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity is CommandReceipt receipt ? receipt.ResourceId.ToString() :
                    string.Join('/', key.Select(p => entry.Property(p.Name).CurrentValue)),
                CorrelationId = actor.CorrelationId, OccurredAt = DateTimeOffset.UtcNow,
                VesselId = metadata.VesselId, ResourceId = metadata.ResourceId,
                BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
                // Receipt save happens after PostgreSQL returned the business xmin, inside the same transaction.
                AfterVersion = entry.Entity is CommandReceipt command ? CommittedVersion(command) : null,
                RoleContext = actor.RoleContext, CapabilityContext = actor.CapabilityContext, ChangeSummary = metadata.Summary,
                BeforeRevision = metadata.BeforeRevision, AfterRevision = metadata.AfterRevision,
                ResourceKind = metadata.Kind, ReasonStatus = "NotRequiredForCommand", Source = actor.Source
            });
        }
        AuditEvents.AddRange(events);
        return events;
    }
}
