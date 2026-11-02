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
    public Guid? ConstructionFileId { get; set; }
    public long? ConstructionFileRevision { get; set; }
    public string? ConstructionFileChecksum { get; set; }
    public RegulatoryDraftRevision Snapshot() => new()
    {
        TenantId = TenantId, VesselId = VesselId, Revision = Revision,
        ContentJson = ContentJson, Provenance = Provenance, SavedAt = SavedAt,
        ConstructionFileId = ConstructionFileId, ConstructionFileRevision = ConstructionFileRevision, ConstructionFileChecksum = ConstructionFileChecksum
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
    public Guid? ConstructionFileId { get; set; }
    public long? ConstructionFileRevision { get; set; }
    public string? ConstructionFileChecksum { get; set; }
}

public static class RegulatoryDraftStorage
{
    internal const string ConstructionConstraint = "(\"ConstructionFileId\" IS NULL AND \"ConstructionFileRevision\" IS NULL AND \"ConstructionFileChecksum\" IS NULL) OR (\"ConstructionFileId\" IS NOT NULL AND \"ConstructionFileRevision\" IS NOT NULL AND \"ConstructionFileRevision\" > 0 AND \"ConstructionFileChecksum\" IS NOT NULL AND \"ConstructionFileChecksum\" ~ '^[A-F0-9]{64}$')";
    internal const string ReceiptConstraint = "(\"ProfileVesselId\" IS NULL AND \"ProfileOutcomeJson\" IS NULL) OR (\"Action\" = 'ProfileSave' AND \"ProfileVesselId\" IS NOT NULL AND \"ProfileVesselId\" = \"ResourceId\" AND \"ProfileOutcomeJson\" IS NOT NULL AND \"RegisterRevision\" IS NOT NULL AND \"RegisterRevision\" > 0 AND \"RegisterVersion\" IS NOT NULL AND \"RegisterVersion\" > 0)";
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
            b.Property(x => x.ConstructionFileChecksum).HasMaxLength(64);
            b.ToTable("RegulatoryDrafts", t => t.HasCheckConstraint("CK_RegulatoryDrafts_ConstructionRecord", ConstructionConstraint));
            b.HasIndex(x => new { x.TenantId, x.ConstructionFileId, x.VesselId, x.ConstructionFileRevision, x.ConstructionFileChecksum }).HasDatabaseName("IX_RegulatoryDrafts_ConstructionRecord");
            b.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConstructionFileId, x.VesselId, x.ConstructionFileRevision, x.ConstructionFileChecksum })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId, x.Revision, x.Checksum }).HasConstraintName("FK_RegulatoryDrafts_ConstructionRecord").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegulatoryDraftRevision>(b =>
        {
            b.HasKey(x => new { x.TenantId, x.VesselId, x.Revision });
            b.HasOne<RegulatoryDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Provenance).HasMaxLength(32);
            b.Property(x => x.ConstructionFileChecksum).HasMaxLength(64);
            b.ToTable("RegulatoryDraftRevisions", t => t.HasCheckConstraint("CK_RegulatoryDraftRevisions_ConstructionRecord", ConstructionConstraint));
            b.HasIndex(x => new { x.TenantId, x.ConstructionFileId, x.VesselId, x.ConstructionFileRevision, x.ConstructionFileChecksum }).HasDatabaseName("IX_RegulatoryDraftRevisions_ConstructionRecord");
            b.HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.TenantId, x.ConstructionFileId, x.VesselId, x.ConstructionFileRevision, x.ConstructionFileChecksum })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId, x.Revision, x.Checksum }).HasConstraintName("FK_RegulatoryDraftRevisions_ConstructionRecord").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
