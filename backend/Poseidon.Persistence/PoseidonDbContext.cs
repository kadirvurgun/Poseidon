using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;

namespace Poseidon.Persistence;

public sealed class PoseidonDbContext(
    DbContextOptions<PoseidonDbContext> options,
    Guid tenantId,
    ActorContext actor,
    bool administrativeImport = false, IQualifiedInputRuntime? qualifiedInputs = null) : DbContext(options)
{
    public IQualifiedInputRuntime? QualifiedInputs { get; } = qualifiedInputs;
    public Guid TenantId { get; } = tenantId;
    public bool IsAdministrativeImport { get; } = administrativeImport;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<VesselProfile> VesselProfiles => Set<VesselProfile>();
    public DbSet<TugProfile> TugProfiles => Set<TugProfile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationReceipt> NotificationReceipts => Set<NotificationReceipt>();
    public DbSet<NotificationDispatchState> NotificationDispatchStates => Set<NotificationDispatchState>();
    public DbSet<NotificationEpisode> NotificationEpisodes => Set<NotificationEpisode>();
    public DbSet<DailyOperation> DailyOperations => Set<DailyOperation>();
    public DbSet<DailyOperationRevision> DailyOperationRevisions => Set<DailyOperationRevision>();
    public DbSet<DailyOperationReceipt> DailyOperationReceipts => Set<DailyOperationReceipt>();
    public DbSet<OfflineEnrollment> OfflineEnrollments => Set<OfflineEnrollment>();
    public DbSet<OfflineLifecycle> OfflineLifecycles => Set<OfflineLifecycle>();
    public DbSet<OfflineOutcome> OfflineOutcomes => Set<OfflineOutcome>();
    public DbSet<RegulatoryDraft> RegulatoryDrafts => Set<RegulatoryDraft>();
    public DbSet<RegulatoryDraftRevision> RegulatoryDraftRevisions => Set<RegulatoryDraftRevision>();
    public DbSet<RegisterDraft> RegisterDrafts => Set<RegisterDraft>();
    public DbSet<RegisterRevision> RegisterRevisions => Set<RegisterRevision>();
    public DbSet<RuleDraft> RuleDrafts => Set<RuleDraft>();
    public DbSet<RuleRevision> RuleRevisions => Set<RuleRevision>();
    public DbSet<ApplicabilityEvaluation> ApplicabilityEvaluations => Set<ApplicabilityEvaluation>();
    public DbSet<EvaluationRule> EvaluationRules => Set<EvaluationRule>();
    public DbSet<ApplicabilityReceipt> ApplicabilityReceipts => Set<ApplicabilityReceipt>();

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
    public DbSet<CrewIdentityRevision> CrewIdentityRevisions => Set<CrewIdentityRevision>();
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

    public DbSet<TugDraftPlan> TugDraftPlans => Set<TugDraftPlan>();
    public DbSet<TugDraftPlanRevision> TugDraftPlanRevisions => Set<TugDraftPlanRevision>();
    public DbSet<TugDraftPlanTug> TugDraftPlanTugs => Set<TugDraftPlanTug>();
    public DbSet<TugJob> TugJobs => Set<TugJob>();
    public DbSet<TugJobRevision> TugJobRevisions => Set<TugJobRevision>();
    public DbSet<TugOwner> TugOwners => Set<TugOwner>();
    public DbSet<TugAssignment> TugAssignments => Set<TugAssignment>();
    public DbSet<TugOfflineEnrollment> TugOfflineEnrollments => Set<TugOfflineEnrollment>();
    public DbSet<TugOfflineOutcome> TugOfflineOutcomes => Set<TugOfflineOutcome>();
    public DbSet<TugOfflineLifecycle> TugOfflineLifecycles => Set<TugOfflineLifecycle>();
    public DbSet<TugCapabilityBinding> TugCapabilityBindings => Set<TugCapabilityBinding>();
    public DbSet<OperationEquipmentBinding> OperationEquipmentBindings => Set<OperationEquipmentBinding>();
    public DbSet<MsmdRoleBinding> MsmdRoleBindings => Set<MsmdRoleBinding>();
    public DbSet<EvaluationUpstreamReference> EvaluationUpstreamReferences => Set<EvaluationUpstreamReference>();
    public DbSet<EvaluationOwningReference> EvaluationOwningReferences => Set<EvaluationOwningReference>();

    public DbSet<HumanReviewHead> HumanReviewHeads => Set<HumanReviewHead>();
    public DbSet<HumanReviewEvent> HumanReviewEvents => Set<HumanReviewEvent>();
    public DbSet<HumanReviewReceipt> HumanReviewReceipts => Set<HumanReviewReceipt>();
    public DbSet<RegulatoryQualificationHead> RegulatoryQualificationHeads => Set<RegulatoryQualificationHead>();
    public DbSet<RegulatoryQualificationEvent> RegulatoryQualificationEvents => Set<RegulatoryQualificationEvent>();
    public DbSet<RegulatoryFactAttestation> RegulatoryFactAttestations => Set<RegulatoryFactAttestation>();
    public DbSet<EvaluationQualification> EvaluationQualifications => Set<EvaluationQualification>();
    public DbSet<RegulatoryQualificationReceipt> RegulatoryQualificationReceipts => Set<RegulatoryQualificationReceipt>();
    public DbSet<ControlEvidenceHead> ControlEvidenceHeads => Set<ControlEvidenceHead>();
    public DbSet<ControlEvidenceRevision> ControlEvidenceRevisions => Set<ControlEvidenceRevision>();
    public DbSet<ControlEvidenceRequirement> ControlEvidenceRequirements => Set<ControlEvidenceRequirement>();
    public DbSet<ControlEvidenceEdge> ControlEvidenceEdges => Set<ControlEvidenceEdge>();
    public DbSet<ControlEvidenceReceipt> ControlEvidenceReceipts => Set<ControlEvidenceReceipt>();

    public DbSet<CrewCatalogHead> CrewCatalogHeads => Set<CrewCatalogHead>();
    public DbSet<CrewCatalogEvent> CrewCatalogEvents => Set<CrewCatalogEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        CrewCatalogStorage.Configure(model);
        model.Entity<CrewCatalogHead>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CrewCatalogEvent>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.HasDefaultSchema("core");
        DailyOperationsStorage.Configure(model);
        model.Entity<DailyOperation>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<DailyOperationRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<DailyOperationReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        NotificationStorage.Configure(model);
        model.Entity<Notification>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<NotificationEvent>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<NotificationPreference>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<NotificationReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<NotificationDispatchState>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<NotificationEpisode>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        ControlEvidenceStorage.Configure(model);
        HumanReviewStorage.Configure(model);
        RegulatoryQualificationStorage.Configure(model);
        model.Entity<RegulatoryQualificationHead>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RegulatoryQualificationEvent>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RegulatoryFactAttestation>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<EvaluationQualification>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RegulatoryQualificationReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<HumanReviewHead>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<HumanReviewEvent>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<HumanReviewReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ControlEvidenceHead>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ControlEvidenceRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ControlEvidenceRequirement>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ControlEvidenceEdge>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ControlEvidenceReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        ApplicabilityStorage.Configure(model);
        model.Entity<RuleDraft>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<RuleRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ApplicabilityEvaluation>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<EvaluationRule>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ApplicabilityReceipt>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        OfflineApplicationStorage.Configure(model);
        model.Entity<OfflineEnrollment>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<OfflineLifecycle>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<OfflineOutcome>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        ImportStorage.Configure(model);
        model.Entity<Credential>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<CredentialRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportBatch>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportPreview>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<ImportRowResult>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        TugDraftPlanStorage.Configure(model);
        model.Entity<TugDraftPlan>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugDraftPlanRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugDraftPlanTug>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        TugStorage.Configure(model);
        TugOfflineStorage.Configure(model);
        model.Entity<TugOfflineEnrollment>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugOfflineOutcome>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugOfflineLifecycle>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        TugCapabilityBinding.Configure(model);
        OperationEquipmentBinding.Configure(model);
        model.Entity<EvaluationUpstreamReference>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        EvaluationUpstreamReference.Configure(model);
        MsmdRoleBinding.Configure(model);
        model.Entity<MsmdRoleBinding>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<EvaluationOwningReference>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<OperationEquipmentBinding>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
        model.Entity<TugCapabilityBinding>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
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
        model.Entity<CrewIdentityRevision>().HasQueryFilter(x => IsAdministrativeImport || x.TenantId == TenantId);
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
            b.ToTable("CommandReceipts", t => t.HasCheckConstraint("CK_CommandReceipts_ProfileOutcome", RegulatoryDraftStorage.ReceiptConstraint));
            b.HasIndex(x => new { x.TenantId, x.ProfileVesselId, x.RegisterRevision }).HasDatabaseName("IX_CommandReceipts_ProfileOutcome");
            b.HasOne<RegulatoryDraftRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProfileVesselId, x.RegisterRevision })
                .HasPrincipalKey(x => new { x.TenantId, x.VesselId, x.Revision }).HasConstraintName("FK_CommandReceipts_ProfileOutcome").OnDelete(DeleteBehavior.Restrict);
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
            b.Property(x => x.EntityId).HasMaxLength(512);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.RoleContext).HasMaxLength(600);
            b.Property(x => x.CapabilityContext).HasMaxLength(2048);
            b.Property(x => x.ChangeSummary).HasMaxLength(2048);
            b.Property(x => x.ResourceKind).HasMaxLength(64);
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
        var generated = PrepareAudit(false, CancellationToken.None).GetAwaiter().GetResult();
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch
        {
            foreach (var entry in generated) Entry(entry).State = EntityState.Detached;
            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var generated = await PrepareAudit(true, cancellationToken);
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
            CrewCatalogHead c => receipt.Action == "CrewCatalogSave" && c.VesselId == receipt.ResourceId && c.TenantId == receipt.TenantId,
            ImportBatch b => receipt.Action is "CrewImportLegacyConvert" or "ImportCommit" && b.Id == receipt.ResourceId && b.TenantId == receipt.TenantId,
            Credential c => receipt.Action == "CredentialSave" && c.Id == receipt.ResourceId && c.TenantId == receipt.TenantId,
            TugDraftPlan p => receipt.Action == "TugPlanSave" && p.JobId == receipt.ResourceId && p.TenantId == receipt.TenantId,
            TugJob j => receipt.Action == "TugSave" && j.Id == receipt.ResourceId && j.TenantId == receipt.TenantId,
            Asset a => receipt.Action == "EquipmentSave" && a.Id == receipt.ResourceId && a.TenantId == receipt.TenantId,
            MaintenancePlan p => receipt.Action == "MaintenanceSave" && p.Id == receipt.ResourceId && p.TenantId == receipt.TenantId,
            CrewPersonalProfile p => receipt.Action is "CrewProfileSave" or "CrewPhotoSave" && p.PersonId == receipt.ResourceId && p.TenantId == receipt.TenantId,
            Person p => receipt.Action is "CrewPersonSave" or "CrewIdentityConvert" && p.Id == receipt.ResourceId && p.TenantId == receipt.TenantId,
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

    private uint? CommittedVersion(ApplicabilityReceipt receipt)
    {
        var head = ChangeTracker.Entries<RuleDraft>().SingleOrDefault(e => e.Entity.Id == receipt.ResourceId && e.Entity.TenantId == receipt.TenantId && e.State == EntityState.Unchanged);
        return receipt.Action is "author" or "review" ? head?.Entity.Version : null;
    }

    private async ValueTask<Guid?> OwnedVessel<T>(DbSet<T> set, Expression<Func<T, bool>> owner,
        Expression<Func<T, Guid>> vessel, bool asynchronous, CancellationToken cancellationToken) where T : class
    {
        // A newly added or already loaded parent is authoritative in this same audited unit of work.
        // Every predicate includes the actual child tenant; database fallback retains the context query filter.
        var tracked = set.Local.SingleOrDefault(owner.Compile());
        Guid? id = tracked != null ? vessel.Compile()(tracked) : asynchronous
            ? await set.AsNoTracking().Where(owner).Select(vessel).Select(x => (Guid?)x).SingleOrDefaultAsync(cancellationToken)
            : set.AsNoTracking().Where(owner).Select(vessel).Select(x => (Guid?)x).SingleOrDefault();
        return id is { } value && value != Guid.Empty ? value
            : throw new InvalidOperationException("Audit parent ownership is unavailable.");
    }

    private ValueTask<Guid?> ReceiptVessel(CommandReceipt r, bool asynchronous, CancellationToken ct) => r.Action switch
    {
        "CompanyCreate" or "CompanyUpdate" => TenantResource(Companies, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, asynchronous, ct),
        "CrewPersonSave" or "CrewIdentityConvert" => TenantResource(Persons, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, asynchronous, ct),
        "CrewProfileSave" or "CrewPhotoSave" => TenantResource(CrewPersonalProfiles, x => x.TenantId == r.TenantId && x.PersonId == r.ResourceId, asynchronous, ct),
        "SourceDraftSave" => TenantResource(RegisterDrafts, x => x.TenantId == r.TenantId && x.Kind == "Source" && x.Id == r.ResourceId, asynchronous, ct),
        "RequirementDraftSave" => TenantResource(RegisterDrafts, x => x.TenantId == r.TenantId && x.Kind == "Requirement" && x.Id == r.ResourceId, asynchronous, ct),
        "VesselCreate" or "VesselUpdate" => OwnedVessel(Vessels, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.Id, asynchronous, ct),
        "ProfileSave" => OwnedVessel(RegulatoryDrafts, x => x.TenantId == r.TenantId && x.VesselId == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "CrewCatalogSave" => OwnedVessel(CrewCatalogHeads, x => x.TenantId == r.TenantId && x.VesselId == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "CrewSave" => OwnedVessel(CrewAssignments, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "CredentialSave" => OwnedVessel(Credentials, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "ImportCommit" or "CrewImportLegacyConvert" => OwnedVessel(ImportBatches, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "EquipmentSave" => OwnedVessel(Assets, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "MaintenanceSave" => OwnedVessel(MaintenancePlans, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "TugSave" => OwnedVessel(TugJobs, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "TugPlanSave" => OwnedVessel(TugDraftPlans, x => x.TenantId == r.TenantId && x.JobId == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "DocumentDraftSave" => OwnedVessel(Documents, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        "FileFinalize" => OwnedVessel(StoredFiles, x => x.TenantId == r.TenantId && x.Id == r.ResourceId, x => x.VesselId, asynchronous, ct),
        _ => new ValueTask<Guid?>((Guid?)null)
    };

    private async ValueTask<Guid?> TenantResource<T>(DbSet<T> set, Expression<Func<T, bool>> owner,
        bool asynchronous, CancellationToken cancellationToken) where T : class
    {
        if (!set.Local.Any(owner.Compile()) && !(asynchronous
            ? await set.AsNoTracking().AnyAsync(owner, cancellationToken) : set.AsNoTracking().Any(owner)))
            throw new InvalidOperationException("Audit parent ownership is unavailable.");
        // Actual tenant/person/catalog ownership is confirmed; it does not imply a vessel assignment.
        return null;
    }

    private ValueTask<Guid?> ParentVessel(EntityEntry entry, bool asynchronous, CancellationToken ct) => entry.Entity switch
    {
        MaintenanceObservationRevocation r => OwnedVessel(MaintenanceObservations,
            x => x.TenantId == r.TenantId && x.Id == r.ObservationId, x => x.VesselId, asynchronous, ct),
        EvaluationRule r => OwnedVessel(ApplicabilityEvaluations,
            x => x.TenantId == r.TenantId && x.Id == r.EvaluationId, x => x.VesselId, asynchronous, ct),
        EvaluationQualification r => OwnedVessel(ApplicabilityEvaluations,
            x => x.TenantId == r.TenantId && x.Id == r.EvaluationId, x => x.VesselId, asynchronous, ct),
        CommandReceipt r => ReceiptVessel(r, asynchronous, ct),
        _ => new ValueTask<Guid?>((Guid?)null)
    };

    private static bool TrustedRoleSnapshot(string? value)
    {
        if (value is not { Length: > 0 and <= 600 }) return false;
        var roles = value.Split(',');
        return roles.Length is > 0 and <= 16 && roles.All(x => x.Length == 36 && Guid.TryParseExact(x, "D", out var id) && id != Guid.Empty) &&
            roles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == roles.Length;
    }

    private async ValueTask<List<AuditEvent>> PrepareAudit(bool asynchronous, CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (changes.Count == 0) return [];
        if (string.IsNullOrWhiteSpace(actor.Id) || string.IsNullOrWhiteSpace(actor.Type) || string.IsNullOrWhiteSpace(actor.CorrelationId))
            throw new InvalidOperationException("An authenticated or explicit system actor and correlation ID are required.");
        if (actor.Command != null && !actor.Command.IsValid)
            throw new InvalidOperationException("Invalid audit command context.");
        var currentWebCommand = actor.Source == "WebCommand";
        if (currentWebCommand && (actor.Type != "User" || actor.Command == null || !TrustedRoleSnapshot(actor.RoleContext)))
            throw new InvalidOperationException("Current web writes require trusted role and command provenance.");
        if (changes.Any(e => e.Entity is AuditEvent or SecurityEvent))
            throw new InvalidOperationException("Audit events are generated by the context and cannot be directly changed.");
        if (changes.Any(e => e.Entity is DailyOperationRevision or DailyOperationReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Daily operation history and command outcomes are immutable.");
        if (changes.Any(e => e.Entity is DailyOperation && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("FieldsJson" or "Hash" or "Provenance" or "ActorId" or "SavedAt" or "Revision")) ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Daily operation identity is immutable; edits append one revision.");
        if (changes.Any(e => e.Entity is NotificationEvent or NotificationReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Notification lifecycle and receipts are immutable.");
        if (changes.Any(e => e.Entity is NotificationDispatchState && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("LeaseOwner" or "LeaseUntil" or "NextRunAt" or "LastCompletedAt" or "SourceOffset" or "Revision")) ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Notification dispatch ownership is immutable.");
        if (changes.Any(e => e.Entity is NotificationEpisode && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Active" or "Episode" or "SourceContextHash" or "Revision")) ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Notification episode ownership is immutable.");
        if (changes.Any(e => e.Entity is Notification && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not "State" and not "Revision") ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Notification source and routing are immutable.");
        if (changes.Any(e => e.Entity is NotificationPreference && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not "MutedAdvisory" and not "Revision") ||
                (long)e.Property("Revision").CurrentValue! != (long)e.Property("Revision").OriginalValue! + 1))))
            throw new InvalidOperationException("Notification preference ownership is immutable.");
        if (changes.Any(e => e.Entity is RegulatoryQualificationEvent or RegulatoryFactAttestation or EvaluationQualification or RegulatoryQualificationReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Regulatory qualification lineage is immutable.");
        if (changes.Any(e => e.Entity is RegulatoryQualificationHead && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && p.Metadata.Name != "Revision"))))
            throw new InvalidOperationException("Regulatory qualification scope is immutable.");
        if (changes.Any(e => e.Entity is HumanReviewEvent or HumanReviewReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Human review history is immutable.");
        if (changes.Any(e => e.Entity is HumanReviewHead && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && p.Metadata.Name != "Revision"))))
            throw new InvalidOperationException("Human review scope is immutable.");
        if (changes.Any(e => e.Entity is ControlEvidenceRevision or ControlEvidenceRequirement or ControlEvidenceEdge or ControlEvidenceReceipt or TugCapabilityBinding or OperationEquipmentBinding or EvaluationOwningReference or EvaluationUpstreamReference or MsmdRoleBinding && e.State != EntityState.Added))
            throw new InvalidOperationException("Control/evidence lineage is immutable.");
        if (changes.Any(e => e.Entity is ControlEvidenceHead && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && p.Metadata.Name != "Revision"))))
            throw new InvalidOperationException("Control/evidence ownership is immutable.");
        if (changes.Any(e => e.Entity is RuleRevision or ApplicabilityEvaluation or EvaluationRule or ApplicabilityReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Applicability lineage is immutable.");
        if (changes.Any(e => e.Entity is RuleDraft && e.State == EntityState.Deleted))
            throw new InvalidOperationException("Rule history cannot be removed.");
        if (changes.Any(e => e.Entity is TugOfflineOutcome or TugOfflineLifecycle && e.State != EntityState.Added))
            throw new InvalidOperationException("Tug offline lineage is immutable.");
        if (changes.Any(e => e.Entity is TugOfflineEnrollment && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            e.Properties.Any(p => p.IsModified && p.Metadata.Name is not "ExpiresAt" and not "RevokedAt" and not "PublicKey") ||
            e.State == EntityState.Modified && e.Property("PublicKey").IsModified && e.Property("PublicKey").OriginalValue != null)))
            throw new InvalidOperationException("Tug offline enrollment identity is immutable.");
        if (changes.Any(e => e.Entity is OfflineOutcome or OfflineLifecycle && e.State != EntityState.Added))
            throw new InvalidOperationException("Offline lineage is immutable.");
        if (changes.Any(e => e.Entity is OfflineEnrollment && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            e.Properties.Any(p => p.IsModified && p.Metadata.Name is not "ExpiresAt" and not "RevokedAt" and not "PublicSigningKey") ||
            e.State == EntityState.Modified && e.Property("PublicSigningKey").IsModified && e.Property("PublicSigningKey").OriginalValue != null)))
            throw new InvalidOperationException("Offline capture context is immutable.");
        if (changes.Any(e => e.Entity is TugJobRevision or TugOwner or TugAssignment or TugDraftPlanRevision or TugDraftPlanTug && e.State != EntityState.Added))
            throw new InvalidOperationException("Tug lineage is immutable.");
        if (changes.Any(e => e.Entity is TugDraftPlan && (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
            (e.Property("VesselId").IsModified || e.Property("JobId").IsModified))))
            throw new InvalidOperationException("Draft plan ownership is immutable.");
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
        if (changes.Any(e => e.Entity is CrewIdentityRevision && e.State != EntityState.Added))
            throw new InvalidOperationException("Crew identity lineage is immutable.");
        if (changes.Any(e => e.Entity is CrewCatalogEvent && e.State != EntityState.Added))
            throw new InvalidOperationException("Crew catalog lineage is immutable.");
        if (changes.Any(e => e.Entity is CrewCatalogHead && (e.State == EntityState.Deleted || e.State == EntityState.Modified && e.Property("VesselId").IsModified)))
            throw new InvalidOperationException("Crew catalog ownership is immutable.");
        if (changes.Any(e => e.Entity is CrewPosition && e.State != EntityState.Added))
            throw new InvalidOperationException("Crew position references are immutable; use a distinct configured reference.");
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
        if (changes.Any(e => e.Entity is ImportBatch && (e.State == EntityState.Deleted || e.State == EntityState.Modified && !LegacyImportStorage.IsConversion(e) &&
            (LegacyImportStorage.Protected(e) || new[] { "VesselId", "ActorId", "SessionHash", "AssignmentId", "Category", "TemplateVersion", "TemplateFingerprint", "CrewIdentityPolicyVersion", "SourceHash", "TableJson", "SourceName", "SourceLocator", "LegacyProtectedAt", "LegacyProtectionPolicyVersion", "LegacyProtectionIdentityVersion", "LegacyProtectionPurpose" }.Any(n => e.Property(n).IsModified)))))
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
            var metadata = AuditMetadata.From(entry, await ParentVessel(entry, asynchronous, cancellationToken), actor.Command, currentWebCommand);
            events.Add(new AuditEvent
            {
                Id = Guid.NewGuid(), TenantId = scope, ActorId = actor.Id, ActorType = actor.Type,
                Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity is CommandReceipt receipt ? receipt.ResourceId.ToString() :
                    entry.Entity is DailyOperationReceipt dailyReceipt ? dailyReceipt.RecordId.ToString() :
                    entry.Entity is ApplicabilityReceipt applicabilityReceipt ? applicabilityReceipt.ResourceId.ToString() :
                    entry.Entity is HumanReviewReceipt humanReceipt ? humanReceipt.ResourceId.ToString() :
                    entry.Entity is RegulatoryQualificationReceipt regulatoryReceipt ? regulatoryReceipt.RuleId.ToString() :
                    entry.Entity is ControlEvidenceReceipt controlReceipt ? controlReceipt.ResourceId.ToString() :
                    string.Join('/', key.Select(p => entry.Property(p.Name).CurrentValue)),
                CorrelationId = actor.CorrelationId, OccurredAt = DateTimeOffset.UtcNow,
                VesselId = metadata.VesselId, ResourceId = metadata.ResourceId,
                BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
                // Receipt save happens after PostgreSQL returned the business xmin, inside the same transaction.
                AfterVersion = entry.Entity is CommandReceipt command ? CommittedVersion(command) : entry.Entity is ApplicabilityReceipt applicability ? CommittedVersion(applicability) : entry.Entity is RegulatoryQualificationReceipt qr ? ChangeTracker.Entries<RegulatoryQualificationHead>().SingleOrDefault(e => e.Entity.TenantId == qr.TenantId && e.Entity.RuleId == qr.RuleId)?.Entity.Version : entry.Entity is HumanReviewReceipt hr ? ChangeTracker.Entries<HumanReviewHead>().SingleOrDefault(e => e.Entity.TenantId == hr.TenantId && e.Entity.VesselId == hr.VesselId && e.Entity.Id == hr.ResourceId)?.Entity.Version : entry.Entity is ControlEvidenceReceipt ce ? ChangeTracker.Entries<ControlEvidenceHead>().SingleOrDefault(e => e.Entity.TenantId == ce.TenantId && e.Entity.VesselId == ce.VesselId && e.Entity.Kind == ce.Kind && e.Entity.Id == ce.ResourceId)?.Entity.Version : null,
                RoleContext = actor.RoleContext, CapabilityContext = actor.CapabilityContext, ChangeSummary = metadata.Summary,
                BeforeRevision = metadata.BeforeRevision, AfterRevision = entry.Entity switch
                {
                    RegulatoryQualificationReceipt r => ChangeTracker.Entries<RegulatoryQualificationHead>().SingleOrDefault(e => e.Entity.TenantId == r.TenantId && e.Entity.RuleId == r.RuleId)?.Entity.Revision,
                    HumanReviewReceipt r => ChangeTracker.Entries<HumanReviewHead>().SingleOrDefault(e => e.Entity.TenantId == r.TenantId && e.Entity.VesselId == r.VesselId && e.Entity.Id == r.ResourceId)?.Entity.Revision,
                    ControlEvidenceReceipt r => ChangeTracker.Entries<ControlEvidenceHead>().SingleOrDefault(e => e.Entity.TenantId == r.TenantId && e.Entity.VesselId == r.VesselId && e.Entity.Kind == r.Kind && e.Entity.Id == r.ResourceId)?.Entity.Revision,
                    _ => metadata.AfterRevision
                },
                ResourceKind = metadata.Kind, ReasonStatus = actor.Type == "System" ? "SystemOperation" :
                    entry.Entity is RuleRevision or ControlEvidenceRevision or HumanReviewEvent or TugOfflineOutcome { ParentCommandId: not null } ? "RecordedControlledCode" : entry.Entity is OfflineOutcome { ParentCommandId: not null }
                    ? "RecordedProtected" : currentWebCommand ? "RecordedControlledCode" : "HistoricalUnavailable", Source = actor.Source
            });
            if (entry.Entity is DailyOperationReceipt dailyOutcome) {
                var saved = ChangeTracker.Entries<DailyOperation>().SingleOrDefault(e => e.State == EntityState.Unchanged &&
                    e.Entity.TenantId == dailyOutcome.TenantId && e.Entity.VesselId == dailyOutcome.VesselId &&
                    e.Entity.Kind == dailyOutcome.Kind && e.Entity.Id == dailyOutcome.RecordId)?.Entity;
                events[^1].AfterVersion = saved?.Version;
                events[^1].AfterRevision = saved?.Revision;
            }
        }
        AuditEvents.AddRange(events);
        return events;
    }
}
