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
    // USER-ADMIN-02 (QA N3): for a caller WITHOUT a trusted actor (an invitation session before binding) only. A keyed,
    // pseudonymous reference to the IdP-validated identity (never the subject) and the tenant a token CLAIMS, unverified
    // (no foreign key: recording it must never depend on, or reveal, whether that tenant exists).
    public string? IdentityReference { get; set; }
    public Guid? UnverifiedTenantId { get; set; }

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<SecurityEvent>(b =>
        {
            b.ToTable("SecurityEvents", "audit");
            b.HasKey(x => x.Id);
            b.Property(x => x.ActorId).HasMaxLength(200);
            b.Property(x => x.Action).HasMaxLength(64);
            b.Property(x => x.Result).HasMaxLength(64);
            b.Property(x => x.CorrelationId).HasMaxLength(128);
            b.Property(x => x.IdentityReference).HasMaxLength(48);
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
        if (!SecurityEventCodes.Allows(entry.Action, entry.Result))
            throw new InvalidOperationException("Unknown security event code.");
        if (entry.TenantId == Guid.Empty || (entry.ActorId != null && entry.TenantId == null))
            throw new InvalidOperationException("Attributed events require trusted tenant scope.");
        if (entry.IdentityReference != null && (entry.ActorId != null || !System.Text.RegularExpressions.Regex.IsMatch(entry.IdentityReference, "^idref:(v1|p1):[0-9a-f]{32}$")) ||
            entry.UnverifiedTenantId != null && (entry.TenantId != null || entry.UnverifiedTenantId == Guid.Empty))
            throw new InvalidOperationException("Pseudonymous references are only for events without a trusted actor or tenant.");
        Add(entry);
        await base.SaveChangesAsync(true, cancellationToken);
        ChangeTracker.Clear();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw new InvalidOperationException("Use AppendAsync.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Use AppendAsync.");
}
