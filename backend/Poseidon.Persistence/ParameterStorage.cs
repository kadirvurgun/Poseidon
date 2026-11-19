using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// PLAN-1511-5401 / 54.01 CFG3 value lineage. One root per tenant+key+security scope+exact selector.
// Revisions and decisions are append-only; the root carries only the CAS version, revision counter
// and the pending proposal pointer. No value is copied into audit summaries.
public sealed class ParameterRoot : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public required string ScopeKind { get; set; }
    public Guid ScopeId { get; set; }
    public required string SelectorJson { get; set; }
    public required string SelectorHash { get; set; }
    public long Revision { get; set; }
    public Guid? PendingRevisionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint Version { get; set; }
}

public sealed class ParameterRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RootId { get; set; }
    public long Sequence { get; set; }
    public required string Key { get; set; }
    public int DefinitionVersion { get; set; }
    public required string ValueMode { get; set; }
    public string? ValueJson { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public required string Reason { get; set; }
    public Guid AuthorId { get; set; }
    public DateTimeOffset AuthoredAt { get; set; }
    public required string ContentHash { get; set; }
    public required string Provenance { get; set; }
}

public sealed class ParameterDecision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid RootId { get; set; }
    public Guid RevisionId { get; set; }
    public long Sequence { get; set; }
    public required string Outcome { get; set; }
    public Guid DeciderId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public DateTimeOffset? SelectedFrom { get; set; }
    public required string Reason { get; set; }
}

// Durable idempotency receipt: unique per tenant+key, independent of actor (54.01 paragraph:96).
public sealed class ParameterCommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Key { get; set; }
    public Guid ActorId { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid RootId { get; set; }
    public Guid RevisionId { get; set; }
    public long RootRevision { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
    public required string OutcomeJson { get; set; }
}

public static class ParameterStorage
{
    internal const string Schema = "config";
    internal static readonly string[] RootIdentity = [nameof(ParameterRoot.Key), nameof(ParameterRoot.ScopeKind), nameof(ParameterRoot.ScopeId),
        nameof(ParameterRoot.SelectorJson), nameof(ParameterRoot.SelectorHash), nameof(ParameterRoot.CreatedAt)];

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<ParameterRoot>(b =>
        {
            b.ToTable("ParameterRoots", Schema, t =>
            {
                t.HasCheckConstraint("CK_ParameterRoots_Scope", "\"ScopeKind\" IN ('TENANT','COMPANY','VESSEL') AND (\"ScopeKind\" <> 'TENANT' OR \"ScopeId\" = \"TenantId\") AND \"ScopeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                t.HasCheckConstraint("CK_ParameterRoots_Revision", "\"Revision\" > 0 AND \"SelectorHash\" ~ '^[0-9a-f]{64}$'");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.Key, x.ScopeKind, x.ScopeId, x.SelectorHash }).IsUnique().HasDatabaseName("IX_ParameterRoots_Identity");
            b.Property(x => x.Key).HasMaxLength(128);
            b.Property(x => x.ScopeKind).HasMaxLength(16);
            b.Property(x => x.SelectorJson).HasMaxLength(2048);
            b.Property(x => x.SelectorHash).HasMaxLength(64);
            b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ParameterRevision>(b =>
        {
            b.ToTable("ParameterRevisions", Schema, t =>
            {
                t.HasCheckConstraint("CK_ParameterRevisions_Value", "\"ValueMode\" IN ('VALUE','INHERIT') AND ((\"ValueMode\" = 'INHERIT') = (\"ValueJson\" IS NULL))");
                t.HasCheckConstraint("CK_ParameterRevisions_Window", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                t.HasCheckConstraint("CK_ParameterRevisions_Lineage", "\"Sequence\" > 0 AND \"DefinitionVersion\" > 0 AND char_length(\"Reason\") BETWEEN 10 AND 2000 AND \"ContentHash\" ~ '^[0-9a-f]{64}$' AND ((\"Provenance\" = 'DeclaredUnverified' AND \"AuthorId\" <> '00000000-0000-0000-0000-000000000000'::uuid) OR (\"Provenance\" = 'SyntheticUnverified' AND \"AuthorId\" = '00000000-0000-0000-0000-000000000000'::uuid))");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.RootId, x.Id }).HasName("AK_ParameterRevisions_Root");
            b.HasIndex(x => new { x.TenantId, x.RootId, x.Sequence }).IsUnique().HasDatabaseName("IX_ParameterRevisions_Sequence");
            b.Property(x => x.Key).HasMaxLength(128);
            b.Property(x => x.ValueMode).HasMaxLength(16);
            b.Property(x => x.ValueJson).HasMaxLength(4096);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.Property(x => x.ContentHash).HasMaxLength(64);
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.HasOne<ParameterRoot>().WithMany().HasForeignKey(x => new { x.TenantId, x.RootId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ParameterDecision>(b =>
        {
            b.ToTable("ParameterDecisions", Schema, t =>
            {
                t.HasCheckConstraint("CK_ParameterDecisions_Outcome", "\"Outcome\" IN ('APPROVED','REJECTED') AND ((\"Outcome\" = 'APPROVED') = (\"SelectedFrom\" IS NOT NULL)) AND \"Sequence\" > 0 AND char_length(\"Reason\") BETWEEN 10 AND 2000 AND \"DeciderId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            });
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasIndex(x => new { x.TenantId, x.RevisionId }).IsUnique().HasDatabaseName("IX_ParameterDecisions_Revision");
            b.HasIndex(x => new { x.TenantId, x.RootId, x.Sequence }).IsUnique().HasDatabaseName("IX_ParameterDecisions_Sequence");
            b.HasIndex(x => new { x.TenantId, x.RootId, x.RevisionId }).HasDatabaseName("IX_ParameterDecisions_RootRevision");
            b.Property(x => x.Outcome).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.HasOne<ParameterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.RootId, x.RevisionId })
                .HasPrincipalKey(x => new { x.TenantId, x.RootId, x.Id }).HasConstraintName("FK_ParameterDecisions_Revision").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ParameterCommandReceipt>(b =>
        {
            b.ToTable("ParameterCommandReceipts", Schema, t =>
                t.HasCheckConstraint("CK_ParameterCommandReceipts_Action", "\"Action\" IN ('ParameterPropose','ParameterApprove','ParameterReject') AND \"RootRevision\" > 0 AND \"Fingerprint\" ~ '^[0-9a-f]{64}$'"));
            b.HasKey(x => new { x.TenantId, x.Key });
            b.HasIndex(x => new { x.TenantId, x.RootId }).HasDatabaseName("IX_ParameterCommandReceipts_Root");
            b.Property(x => x.Key).HasMaxLength(80);
            b.Property(x => x.Action).HasMaxLength(32);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.OutcomeJson).HasMaxLength(4096);
            b.HasOne<ParameterRoot>().WithMany().HasForeignKey(x => new { x.TenantId, x.RootId }).OnDelete(DeleteBehavior.Restrict);
        });
    }

    // Same rules as the migration triggers, enforced before any audit row is generated.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes)
    {
        if (changes.Any(e => e.Entity is ParameterRevision or ParameterDecision or ParameterCommandReceipt && e.State != EntityState.Added))
            throw new InvalidOperationException("Parameter revisions, decisions and receipts are immutable.");
        foreach (var entry in changes.Where(e => e.Entity is ParameterRoot))
        {
            var root = (ParameterRoot)entry.Entity;
            if (entry.State == EntityState.Deleted || root.Id == Guid.Empty ||
                entry.State == EntityState.Added && root.Revision != 1 ||
                entry.State == EntityState.Modified && (RootIdentity.Any(n => entry.Property(n).IsModified) ||
                    root.Revision != (long)entry.Property(nameof(ParameterRoot.Revision)).OriginalValue! + 1))
                throw new InvalidOperationException("Parameter root identity is immutable; append exactly one event.");
        }
    }
}
