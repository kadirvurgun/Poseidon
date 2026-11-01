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
    public static RegulatoryDraftContent Deserialize(string json)
    {
        // Read prepublication numeric JSON without passing through floating point or
        // rewriting immutable revisions. New writes and every HTTP response use strings.
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        foreach (var key in new[] { "gt", "nt", "dwt", "loa", "breadth", "draft" })
        {
            if (node[key] is not System.Text.Json.Nodes.JsonObject field || field["value"] is not { } value) continue;
            using var document = JsonDocument.Parse(value.ToJsonString());
            if (document.RootElement.ValueKind != JsonValueKind.Number) continue;
            if (!InvariantDecimal.TryNormalize(document.RootElement.GetRawText(), out var exact)) throw new JsonException("Invalid stored decimal");
            field["value"] = exact;
        }
        return node.Deserialize<RegulatoryDraftContent>(Json)!;
    }
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
