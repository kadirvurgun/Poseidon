using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Poseidon.Persistence;

// S11 WP-B DEV-072 privacy-safe pilot telemetry. One immutable batch row per accepted client
// submission: closed event codes in EventsJson, pseudonymous tenant-salted contributor/role
// buckets and hour-truncated timestamps. No actor id, vessel id, record id or free text is
// stored in a batch. The audited SaveChanges path still attributes the submitting actor in the
// restricted security audit trail (security baseline); analytics projections never read it.
// Expired batches are removed only by the retention purge in the ingest transaction.
public sealed class PilotMetricBatch : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public string SchemaVersion {get;set;}="pilot-event.v1";
    public string ActorBucket {get;set;}="";
    public string RoleBucket {get;set;}="";
    public int EventCount {get;set;}
    public string EventsJson {get;set;}="[]";
    public DateTimeOffset ReceivedHour {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public string RetentionPolicyRef {get;set;}="";
}
// Per-tenant random salt for pseudonymous buckets. Created once on first contribution, never
// changed, never returned by any endpoint and withheld from audit summaries.
public sealed class PilotMetricTenantSalt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public byte[] Salt {get;set;}=[];
    public DateTimeOffset CreatedAt {get;set;}
}
// Idempotency receipt (actor + key). It binds the committed batch id; it carries no event content.
public sealed class PilotMetricReceipt : ITenantEntity
{
    public Guid TenantId {get;set;}
    public Guid ActorId {get;set;}
    public string Key {get;set;}="";
    public string Fingerprint {get;set;}="";
    public Guid BatchId {get;set;}
    public int Accepted {get;set;}
    public DateTimeOffset SavedAt {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
}

internal static class PilotMetricsStorage
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<PilotMetricBatch>(b => {
            b.ToTable("PilotMetricBatches", "intelligence", t => t.HasCheckConstraint("CK_PilotMetricBatch_Shape",
                "\"SchemaVersion\" = 'pilot-event.v1' AND \"ActorBucket\" ~ '^[0-9a-f]{32}$' AND \"RoleBucket\" ~ '^[0-9a-f]{32}$' AND " +
                "\"EventCount\" BETWEEN 1 AND 50 AND \"ExpiresAt\" > \"ReceivedHour\" AND length(\"RetentionPolicyRef\") BETWEEN 1 AND 128"));
            b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.ReceivedHour });
            b.HasIndex(x => new { x.TenantId, x.ExpiresAt });
            b.Property(x => x.SchemaVersion).HasMaxLength(32); b.Property(x => x.ActorBucket).HasMaxLength(32); b.Property(x => x.RoleBucket).HasMaxLength(32);
            b.Property(x => x.EventsJson).HasColumnType("text"); b.Property(x => x.RetentionPolicyRef).HasMaxLength(128);
        });
        model.Entity<PilotMetricTenantSalt>(b => {
            b.ToTable("PilotMetricTenantSalts", "intelligence", t => t.HasCheckConstraint("CK_PilotMetricTenantSalt_Length", "length(\"Salt\") = 32"));
            b.HasKey(x => x.TenantId);
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PilotMetricReceipt>(b => {
            b.ToTable("PilotMetricReceipts", "intelligence", t => t.HasCheckConstraint("CK_PilotMetricReceipt_Accepted", "\"Accepted\" BETWEEN 1 AND 50"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.HasIndex(x => new { x.TenantId, x.ExpiresAt });
            b.Property(x => x.Key).HasMaxLength(80); b.Property(x => x.Fingerprint).HasMaxLength(64);
        });
    }

    // Insert-only; the retention purge is the only deletion and may remove only expired rows.
    internal static void Guard(IReadOnlyCollection<EntityEntry> changes, DateTimeOffset now)
    {
        if (changes.Any(e => e.Entity is PilotMetricBatch or PilotMetricTenantSalt or PilotMetricReceipt && e.State == EntityState.Modified))
            throw new InvalidOperationException("Pilot metric batches, salts and receipts are immutable.");
        if (changes.Any(e => e.State == EntityState.Deleted && (e.Entity is PilotMetricTenantSalt ||
            e.Entity is PilotMetricBatch b && b.ExpiresAt > now || e.Entity is PilotMetricReceipt r && r.ExpiresAt > now)))
            throw new InvalidOperationException("Only expired pilot metric batches and receipts can be purged.");
    }
}
