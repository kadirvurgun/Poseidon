using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// Assembly-owned draft persistence, not a public API or an authorization service.
// Do not expose unchanged through HTTP: a future host must compose exact authoring
// rights and a commit-time authority fence within the owned transaction.
public static class FleetCatalogDraftWriter
{
    public const string Contract = "FLEET_TYPE_CATALOG_DRAFT_V1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static string Serialize(FleetCatalogDraftContent content) => JsonSerializer.Serialize(FleetCatalogDraftPolicy.Normalize(content), Json);
    internal static string Intent(Guid catalogId, long expectedRevision, string content) => Hash(JsonSerializer.Serialize(new { contract = Contract, catalogId, expectedRevision, content }, Json));

    public static async Task<FleetCatalogDraftRevision> AppendAsync(PoseidonDbContext db, Guid catalogId,
        long expectedRevision, Guid commandId, FleetCatalogDraftContent input, CancellationToken ct = default)
    {
        if (db.IsAdministrativeImport || db.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(db.AuditActorId) || db.AuditActorId.Length > 300 ||
            catalogId == Guid.Empty || commandId == Guid.Empty || expectedRevision < 0 || expectedRevision == long.MaxValue)
            throw new ArgumentException("CATALOG_DRAFT_COMMAND_INVALID");
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Catalog draft writer owns its transaction.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Catalog draft writer requires no pending unrelated changes.");
        var json = Serialize(input);
        if (Encoding.UTF8.GetByteCount(json) > FleetCatalogDraftPolicy.MaximumDocumentBytes) throw new ArgumentException("CATALOG_DRAFT_TOO_LARGE");
        var intent = Intent(catalogId, expectedRevision, json);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var existing = await db.FleetCatalogDraftRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.CommandId == commandId, ct);
            if (existing is not null)
            {
                if (existing.CatalogId != catalogId || existing.ActorId != db.AuditActorId || existing.IntentHash != intent)
                    throw new InvalidOperationException("CATALOG_DRAFT_COMMAND_CONFLICT");
                await transaction.CommitAsync(ct);
                return existing; // Exact immutable outcome, never the current head.
            }
            var head = await db.FleetCatalogDrafts.SingleOrDefaultAsync(x => x.Id == catalogId, ct);
            if ((head?.Revision ?? 0) != expectedRevision) throw new InvalidOperationException("CATALOG_DRAFT_VERSION_CONFLICT");
            var prior = head is null ? null : await db.FleetCatalogDraftRevisions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CatalogId == catalogId && x.Revision == expectedRevision, ct);
            if (head is not null && prior is null) throw new InvalidOperationException("CATALOG_DRAFT_HISTORY_UNAVAILABLE");
            if (head is null) { head = new() { TenantId = db.TenantId, Id = catalogId }; db.FleetCatalogDrafts.Add(head); }
            head.Revision = expectedRevision + 1;
            var now = db.Clock.GetUtcNow();
            var revision = new FleetCatalogDraftRevision {
                TenantId = db.TenantId, Id = Guid.NewGuid(), CatalogId = catalogId, Revision = head.Revision,
                PriorRevisionId = prior?.Id, CommandId = commandId, ExpectedRevision = expectedRevision,
                ActorId = db.AuditActorId, ContentJson = json, ContentHash = Hash(json), IntentHash = intent,
                RecordedAt = now.AddTicks(-(now.Ticks % 10)), Provenance = "DeclaredUnverified"
            };
            db.FleetCatalogDraftRevisions.Add(revision);
            await db.SaveChangesAsync(ct); // Head, immutable outcome and audit commit together.
            await transaction.CommitAsync(ct);
            return revision;
        }
        catch (Exception ex) when (PostgresFailure.IsSerialization(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            throw new InvalidOperationException("CATALOG_DRAFT_CONCURRENT_WRITE", ex);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw;
        }
    }
}
