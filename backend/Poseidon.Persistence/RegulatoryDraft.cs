using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using System.Text.Json;

namespace Poseidon.Persistence;

public sealed class RegulatoryDraft : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public uint Version { get; set; }
    public RegulatoryDraftRevision Snapshot() => new()
    {
        TenantId = TenantId, VesselId = VesselId, Revision = Revision,
        ContentJson = ContentJson, Provenance = Provenance, SavedAt = SavedAt
    };
}

public sealed class RegulatoryDraftRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

public static class RegulatoryDraftStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize(RegulatoryDraftContent content) => JsonSerializer.Serialize(content, Json);
    public static RegulatoryDraftContent Deserialize(string json) => JsonSerializer.Deserialize<RegulatoryDraftContent>(json, Json)!;
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RegulatoryDraft>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.VesselId });
            b.HasOne<Vessel>().WithOne().HasForeignKey<RegulatoryDraft>(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<RegulatoryDraftRevision>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Revision });
            b.HasOne<RegulatoryDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Provenance).HasMaxLength(32);
        });
    }
}
