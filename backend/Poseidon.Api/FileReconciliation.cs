using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Called only by an explicit trusted operator job with an audited system context, never request binding.
// Caller supplies policy cutoff and bounded batch, under an operator-owned tenant lease.
public static class FileReconciliation
{
    public static async Task<int> AbandonAsync(PoseidonDbContext db, LocalFileObjects objects, DateTimeOffset cutoff, int batch, CancellationToken ct)
        => await AbandonCoreAsync(db, (tenant, id, _) => { objects.Remove(tenant, id); return Task.CompletedTask; }, cutoff, batch, ct, stage => stage.StorageReference == null);

    public static Task<int> AbandonAsync(PoseidonDbContext db, FileObjects objects, DateTimeOffset cutoff, int batch, CancellationToken ct)
        => AbandonCoreAsync(db, objects.RemoveAsync, cutoff, batch, ct, objects.Owns);

    private static async Task<int> AbandonCoreAsync(PoseidonDbContext db, Func<Guid, Guid, CancellationToken, Task> remove, DateTimeOffset cutoff, int batch, CancellationToken ct, Func<FileStage, bool>? owns = null)
    {
        if (batch is < 1 or > 100 || cutoff >= DateTimeOffset.UtcNow || db.IsAdministrativeImport)
            throw new ArgumentException("Explicit tenant, bounded batch and elapsed policy cutoff required.");
        var stages = await db.FileStages.Where(x => (x.State == "Pending" || x.State == "Receiving" || x.State == "Abandoned") && x.CreatedAt < cutoff)
            .OrderBy(x => x.CreatedAt).Take(batch).ToArrayAsync(ct);
        int removed = 0;
        foreach (var stage in stages)
        {
            if (owns != null && !owns(stage)) continue; // Historical provider context needs explicit operator recovery.
            if (await db.StoredFiles.AnyAsync(x => x.Id == stage.Id, ct)) continue;
            if (stage.State != "Abandoned") {
                stage.State = "Abandoned";
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateConcurrencyException) { db.Entry(stage).State = EntityState.Detached; continue; }
            }
            // xmin transition fences a live finalizer before deletion. Accepted rows can never be Abandoned.
            // Busy handles/deletion failure leave an honest abandoned row and are retried by a later job.
            try { await remove(stage.TenantId, stage.Id, ct); removed++; }
            catch (IOException) { }
        }
        return removed;
    }
}
