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

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("core");
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

    private List<AuditEvent> PrepareAudit()
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (changes.Count == 0) return [];
        if (string.IsNullOrWhiteSpace(actor.Id) || string.IsNullOrWhiteSpace(actor.Type) || string.IsNullOrWhiteSpace(actor.CorrelationId))
            throw new InvalidOperationException("An authenticated or explicit system actor and correlation ID are required.");
        if (changes.Any(e => e.Entity is AuditEvent or SecurityEvent))
            throw new InvalidOperationException("Audit events are generated by the context and cannot be directly changed.");
        if (changes.Any(e => e.Entity is CommandReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Command receipts are immutable.");
        var events = new List<AuditEvent>();
        foreach (var entry in changes)
        {
            var scope = entry.Entity is Tenant tenant ? tenant.Id : ((ITenantEntity)entry.Entity).TenantId;
            if (scope == Guid.Empty || (!IsAdministrativeImport && scope != TenantId))
                throw new UnauthorizedAccessException("The write is outside the current tenant scope.");
            if (entry.State == EntityState.Modified && entry.Entity is ITenantEntity && entry.Property(nameof(ITenantEntity.TenantId)).IsModified)
                throw new UnauthorizedAccessException("Tenant ownership cannot be changed.");
            var key = entry.Metadata.FindPrimaryKey()!.Properties;
            events.Add(new AuditEvent
            {
                Id = Guid.NewGuid(), TenantId = scope, ActorId = actor.Id, ActorType = actor.Type,
                Action = entry.State.ToString(), EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity is CommandReceipt receipt ? receipt.ResourceId.ToString() :
                    string.Join('/', key.Select(p => entry.Property(p.Name).CurrentValue)),
                CorrelationId = actor.CorrelationId, OccurredAt = DateTimeOffset.UtcNow
            });
        }
        AuditEvents.AddRange(events);
        return events;
    }
}
