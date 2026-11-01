using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Poseidon.Persistence;

public sealed class RegisterDraft : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public uint Version { get; set; }
    public RegisterRevision Snapshot(Guid? sourceId, long? sourceRevision) => new() {
        TenantId = TenantId, Id = Id, Kind = Kind, Revision = Revision, ContentJson = ContentJson,
        Provenance = Provenance, SavedAt = SavedAt, SourceKind = sourceId == null ? null : "Source",
        SourceId = sourceId, SourceRevision = sourceRevision, Hash = RegisterStorage.Hash(ContentJson) };
}

public sealed class RegisterRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public long Revision { get; set; }
    public required string ContentJson { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public required string Hash { get; set; }
    public string? SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public long? SourceRevision { get; set; }
}

public static class RegisterStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize(RegisterContent content) => JsonSerializer.Serialize(content, Json);
    public static RegisterContent Deserialize(string content) => JsonSerializer.Deserialize<RegisterContent>(content, Json)!;
    public static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<RegisterDraft>(b => {
            b.ToTable("RegisterDrafts", t => t.HasCheckConstraint("CK_RegisterDrafts_Kind", "\"Kind\" IN ('Source','Requirement') AND \"Revision\" > 0 AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified')"));
            b.HasKey(x => new { x.TenantId, x.Kind, x.Id });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.Provenance).HasMaxLength(32); b.Property(x => x.Version).IsRowVersion();
            b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RegisterRevision>(b => {
            b.ToTable("RegisterRevisions", t => t.HasCheckConstraint("CK_RegisterRevisions_Lineage",
                "\"Revision\" > 0 AND \"Kind\" IN ('Source','Requirement') AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND ((\"SourceKind\" IS NULL AND \"SourceId\" IS NULL AND \"SourceRevision\" IS NULL) OR (\"Kind\" = 'Requirement' AND \"SourceKind\" IS NOT NULL AND \"SourceKind\" = 'Source' AND \"SourceId\" IS NOT NULL AND \"SourceRevision\" IS NOT NULL AND \"SourceRevision\" > 0))"));
            b.HasKey(x => new { x.TenantId, x.Kind, x.Id, x.Revision });
            b.Property(x => x.Kind).HasMaxLength(16); b.Property(x => x.SourceKind).HasMaxLength(16);
            b.Property(x => x.ContentJson).HasColumnType("text"); b.Property(x => x.Provenance).HasMaxLength(32); b.Property(x => x.Hash).HasMaxLength(64);
            b.HasOne<RegisterDraft>().WithMany().HasForeignKey(x => new { x.TenantId, x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RegisterRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.SourceKind, x.SourceId, x.SourceRevision })
                .HasPrincipalKey(x => new { x.TenantId, x.Kind, x.Id, x.Revision }).HasConstraintName("FK_RegisterRevisions_Source").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
