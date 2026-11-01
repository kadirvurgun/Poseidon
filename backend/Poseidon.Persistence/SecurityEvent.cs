using Microsoft.EntityFrameworkCore;

namespace Poseidon.Persistence;

// Security events are real observations, never seed data. No request payloads or identity claims.
public sealed class SecurityEvent
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? ActorId { get; set; }
    public Guid? ResourceId { get; set; }
    public required string Action { get; set; }
    public required string Result { get; set; }
    public required string CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<SecurityEvent>(b =>
        {
            b.ToTable("SecurityEvents", "audit");
            b.HasKey(x => x.Id);
            b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.Result).HasMaxLength(32);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.HasIndex(x => new { x.TenantId, x.OccurredAt });
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

// Separate connection/context: denial events survive rollback of business transactions.
// Deliberately has no projection endpoint or administrative tenant filter bypass.
public sealed class SecurityEventWriter(DbContextOptions<SecurityEventWriter> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        SecurityEvent.Configure(model);
        model.Entity<Tenant>().ToTable("Tenants", "core");
        model.Entity<Tenant>().Property(x => x.Version).IsRowVersion();
    }

    public async Task AppendAsync(SecurityEvent entry, CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.HasChanges()) throw new InvalidOperationException("Only one security event may be appended at a time.");
        if (entry.Action is not ("Login" or "Logout" or "CompaniesRead" or "VesselsRead" or "AccessDenied" or "Session" or
            "CompanyCreate" or "CompanyUpdate" or "VesselCreate" or "VesselUpdate" or "DirectoryWrite" or
            "ProfileRead" or "ProfileHistory" or "ProfileSave" or "WorkspaceRead" or "WorkspaceSearch") ||
            entry.Result is not ("Success" or "Denied" or "Unauthenticated" or "Failed" or "SetupRequired" or "NotFound" or
            "Attempted" or "permission_denied" or "csrf_invalid" or "invalid_request" or "validation_failed" or
            "invalid_idempotency_key" or "resource_not_found" or "idempotency_key_reused" or "version_conflict" or
            "duplicate_code" or "service_unavailable" or "Invalid"))
            throw new InvalidOperationException("Unknown security event code.");
        if (entry.TenantId == Guid.Empty || (entry.ActorId != null && entry.TenantId == null))
            throw new InvalidOperationException("Attributed events require trusted tenant scope.");
        Add(entry);
        await base.SaveChangesAsync(true, cancellationToken);
        ChangeTracker.Clear();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new InvalidOperationException("Use AppendAsync.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Use AppendAsync.");
}
