using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text;
using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// Staging only. No Published/Approved/Effective column or usable catalog selector.
public sealed class FleetCatalogDraft : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public uint Version { get; set; }
}

public sealed class FleetCatalogDraftRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid CatalogId { get; set; }
    public long Revision { get; set; }
    public Guid? PriorRevisionId { get; set; }
    public Guid? CommandId { get; set; }
    public long ExpectedRevision { get; set; }
    public required string ActorId { get; set; }
    public required string ContentJson { get; set; }
    public required string ContentHash { get; set; }
    public required string IntentHash { get; set; }
    public string Contract { get; set; } = FleetCatalogDraftWriter.Contract;
    public DateTimeOffset RecordedAt { get; set; }
    public required string Provenance { get; set; }
}

internal static class FleetCatalogDraftStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<FleetCatalogDraft>(b => {
            b.ToTable("CatalogDrafts", "fleet", t => t.HasCheckConstraint("CK_FleetCatalogDrafts_Revision", "\"Revision\" > 0"));
            b.HasKey(x => new { x.TenantId, x.Id }); b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).HasConstraintName("FK_FleetCatalogDraft_Tenant").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FleetCatalogDraftRevision>(b => {
            b.ToTable("CatalogDraftRevisions", "fleet", t => t.HasCheckConstraint("CK_FleetCatalogDraftRevisions", "\"Revision\" > 0 AND \"ExpectedRevision\" = \"Revision\" - 1 AND (\"PriorRevisionId\" IS NULL) = (\"Revision\" = 1) AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND (\"CommandId\" IS NOT NULL OR \"Provenance\" = 'SyntheticUnverified')"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.CatalogId, x.Id, x.Revision }).HasName("AK_FleetCatalogDraftRevision_Lineage");
            b.HasIndex(x => new { x.TenantId, x.CatalogId, x.Revision }).IsUnique().HasDatabaseName("IX_FleetCatalogDraftRevision_Revision");
            b.HasIndex(x => new { x.TenantId, x.CommandId }).IsUnique().HasDatabaseName("IX_FleetCatalogDraftRevision_Command");
            b.HasIndex(x => new { x.TenantId, x.CatalogId, x.PriorRevisionId, x.ExpectedRevision }).HasDatabaseName("IX_FleetCatalogDraftRevision_Prior");
            b.Property(x => x.ActorId).HasMaxLength(300); b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.ContentHash).HasMaxLength(64); b.Property(x => x.IntentHash).HasMaxLength(64);
            b.Property(x => x.Contract).HasMaxLength(64);
            b.HasOne<FleetCatalogDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.CatalogId }).HasConstraintName("FK_FleetCatalogDraftRevision_Head").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FleetCatalogDraftRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.CatalogId, x.PriorRevisionId, x.ExpectedRevision })
                .HasPrincipalKey(x => new { x.TenantId, x.CatalogId, x.Id, x.Revision }).HasConstraintName("FK_FleetCatalogDraftRevision_Prior").OnDelete(DeleteBehavior.Restrict);
        });
    }

    internal static void Guard(PoseidonDbContext db, IReadOnlyCollection<EntityEntry> changes)
    {
        foreach (var entry in changes.Where(x => x.Entity is FleetCatalogDraft))
        {
            var head = (FleetCatalogDraft)entry.Entity;
            if (entry.State == EntityState.Deleted || head.Id == Guid.Empty ||
                entry.State == EntityState.Modified && entry.Properties.Any(x => x.IsModified && x.Metadata.Name != nameof(FleetCatalogDraft.Revision)) ||
                entry.State == EntityState.Added && head.Revision != 1 ||
                entry.State == EntityState.Modified && ((long)entry.Property(nameof(head.Revision)).OriginalValue! == long.MaxValue || head.Revision != (long)entry.Property(nameof(head.Revision)).OriginalValue! + 1) ||
                !changes.Any(x => x.State == EntityState.Added && x.Entity is FleetCatalogDraftRevision r && r.TenantId == head.TenantId && r.CatalogId == head.Id && r.Revision == head.Revision))
                throw new InvalidOperationException("Catalog draft head requires its exact next immutable revision.");
        }
        foreach (var entry in changes.Where(x => x.Entity is FleetCatalogDraftRevision))
        {
            if (entry.State != EntityState.Added) throw new InvalidOperationException("Catalog draft history is immutable.");
            var revision = (FleetCatalogDraftRevision)entry.Entity;
            if (revision.Id == Guid.Empty || revision.ActorId != db.AuditActorId || revision.Contract != FleetCatalogDraftWriter.Contract ||
                !changes.Any(x => x.State is EntityState.Added or EntityState.Modified && x.Entity is FleetCatalogDraft h && h.TenantId == revision.TenantId && h.Id == revision.CatalogId && h.Revision == revision.Revision))
                throw new InvalidOperationException("Catalog draft revision requires its attributed head change.");
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var content = JsonSerializer.Deserialize<FleetCatalogDraftContent>(revision.ContentJson, json) ?? throw new InvalidOperationException("Catalog draft content is unavailable.");
            if (FleetCatalogDraftWriter.Serialize(content) != revision.ContentJson || Encoding.UTF8.GetByteCount(revision.ContentJson) > FleetCatalogDraftPolicy.MaximumDocumentBytes ||
                FleetCatalogDraftWriter.Hash(revision.ContentJson) != revision.ContentHash)
                throw new InvalidOperationException("Catalog draft content lineage is invalid.");
            if (revision.CommandId is null)
            {
                if (!db.IsAdministrativeImport || revision.Provenance != "SyntheticUnverified" || revision.Revision != 1)
                    throw new InvalidOperationException("Catalog fixtures must be incomplete administrative drafts.");
                if (content.Entries.Length != 0) throw new InvalidOperationException("Catalog fixture cannot seed selectable members.");
            }
            else if (revision.CommandId == Guid.Empty || revision.Provenance != "DeclaredUnverified" || revision.IntentHash != FleetCatalogDraftWriter.Intent(revision.CatalogId, revision.ExpectedRevision, revision.ContentJson))
                throw new InvalidOperationException("Catalog draft command lineage is invalid.");
        }
    }
}
