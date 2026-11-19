using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// DEV-020 / T-S2-001. Append-only declared provenance of ONE field value version of the
// single controlled vessel profile. A field value version is the immutable profile
// revision in which the current value was introduced (derived from RegulatoryDraftRevisions,
// never a second copy of the value). A declaration is not verification or authority.
public sealed class VesselProfileFieldSource : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VesselId { get; set; }
    public string FieldPath { get; set; } = "";
    public long Sequence { get; set; }
    public long ValueRevision { get; set; }
    public string ValueState { get; set; } = "";
    public string ValueHash { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string ReferenceState { get; set; } = "Missing";
    public string? Reference { get; set; }
    public string ObservedAtState { get; set; } = "Missing";
    public DateTimeOffset? ObservedAt { get; set; }
    // Server actor of the declaring command; never a client-supplied name.
    public Guid DeclaredBy { get; set; }
    // Vessel.CompanyId (legacy LegalEntity binding, 59.01 §2) when declared; not registered owner.
    public Guid StewardCompanyId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Provenance { get; set; } = "DeclaredUnverified";
}

public sealed class VesselProfileFieldSourceReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string Key { get; set; } = "";
    public Guid VesselId { get; set; }
    public string FieldPath { get; set; } = "";
    public long Sequence { get; set; }
    public string Fingerprint { get; set; } = "";
    public string OutcomeJson { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; }
}

public sealed class ProfileFieldRevisionRow
{
    public string Path { get; set; } = "";
    public long Revision { get; set; }
}

public sealed class ProfileFieldVersionRow
{
    public long Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Provenance { get; set; } = "";
    public string State { get; set; } = "";
    public string? Value { get; set; }
}

public static class VesselProfileFieldStorage
{
    internal static readonly string PathList = string.Join(",", VesselProfileFieldPolicy.Paths.Order(StringComparer.Ordinal).Select(x => $"'{x}'"));
    internal static readonly string KindList = string.Join(",", VesselProfileFieldPolicy.SourceKinds.Select(x => $"'{x}'"));
    internal static readonly string SourceConstraint =
        $"\"Sequence\" > 0 AND \"ValueRevision\" > 0 AND \"FieldPath\" IN ({PathList}) AND \"SourceKind\" IN ({KindList}) " +
        "AND \"ValueState\" IN ('Missing','Unknown','Known') AND \"ValueHash\" ~ '^[0-9a-f]{64}$' " +
        "AND \"ReferenceState\" IN ('Missing','Unknown','Known') AND ((\"ReferenceState\" = 'Known') = (\"Reference\" IS NOT NULL)) " +
        "AND \"ObservedAtState\" IN ('Missing','Unknown','Known') AND ((\"ObservedAtState\" = 'Known') = (\"ObservedAt\" IS NOT NULL)) " +
        "AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') " +
        "AND (\"ValueState\" <> 'Missing' OR (\"Provenance\" = 'SyntheticUnverified' AND \"SourceKind\" = 'UNKNOWN'))";

    internal static void Configure(ModelBuilder model)
    {
        model.Entity<VesselProfileFieldSource>(b =>
        {
            b.ToTable("VesselProfileFieldSources", "core", t => t.HasCheckConstraint("CK_VesselProfileFieldSources_Declaration", SourceConstraint));
            b.HasKey(x => new { x.TenantId, x.VesselId, x.FieldPath, x.Sequence });
            b.Property(x => x.FieldPath).HasMaxLength(64);
            b.Property(x => x.ValueState).HasMaxLength(16);
            b.Property(x => x.ValueHash).HasMaxLength(64);
            b.Property(x => x.SourceKind).HasMaxLength(32);
            b.Property(x => x.ReferenceState).HasMaxLength(16);
            b.Property(x => x.Reference).HasMaxLength(VesselProfileFieldPolicy.ReferenceLimit);
            b.Property(x => x.ObservedAtState).HasMaxLength(16);
            b.Property(x => x.Provenance).HasMaxLength(32);
            // The exact immutable profile revision that introduced the declared value.
            b.HasOne<RegulatoryDraftRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.ValueRevision })
                .HasConstraintName("FK_VesselProfileFieldSource_ValueRevision").OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Company>().WithMany().HasForeignKey(x => new { x.TenantId, x.StewardCompanyId })
                .HasConstraintName("FK_VesselProfileFieldSource_StewardCompany").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.ValueRevision }).HasDatabaseName("IX_VesselProfileFieldSources_ValueRevision");
            b.HasIndex(x => new { x.TenantId, x.StewardCompanyId }).HasDatabaseName("IX_VesselProfileFieldSources_StewardCompany");
        });
        model.Entity<VesselProfileFieldSourceReceipt>(b =>
        {
            b.ToTable("VesselProfileFieldSourceReceipts", "core", t => t.HasCheckConstraint("CK_VesselProfileFieldSourceReceipts_Sequence", "\"Sequence\" > 0"));
            b.HasKey(x => new { x.TenantId, x.ActorId, x.Key });
            b.Property(x => x.Key).HasMaxLength(80);
            b.Property(x => x.FieldPath).HasMaxLength(64);
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.Property(x => x.OutcomeJson).HasColumnType("text");
            b.HasOne<VesselProfileFieldSource>().WithMany().HasForeignKey(x => new { x.TenantId, x.VesselId, x.FieldPath, x.Sequence })
                .HasConstraintName("FK_VesselProfileFieldSourceReceipt_Declaration").OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.VesselId, x.FieldPath, x.Sequence }).HasDatabaseName("IX_VesselProfileFieldSourceReceipts_Declaration");
        });
    }

    // Controlled READ-ONLY SQL: derives field value versions from the immutable profile
    // revision chain. Explicit tenant+vessel predicates (raw SQL skips EF query filters);
    // no write passes this path. Legacy numeric JSON compares by its text form, which can
    // only move a value version later (conservative), never hide a change.
    public static async Task<Dictionary<string, long>> CurrentValueRevisions(PoseidonDbContext db, Guid tenant, Guid vessel, long head,
        IReadOnlyCollection<string> paths, CancellationToken ct)
    {
        if (paths.Any(p => !VesselProfileFieldPolicy.Paths.Contains(p))) throw new ArgumentOutOfRangeException(nameof(paths));
        var array = paths.ToArray();
        var rows = await db.Database.SqlQuery<ProfileFieldRevisionRow>($"""
            SELECT p.path AS "Path", COALESCE((SELECT max(r."Revision") FROM core."RegulatoryDraftRevisions" r
                WHERE r."TenantId" = {tenant} AND r."VesselId" = {vessel} AND r."Revision" <= {head}
                  AND (COALESCE(r."ContentJson"::jsonb -> p.path ->> 'state', 'Missing'), r."ContentJson"::jsonb -> p.path ->> 'value')
                      IS DISTINCT FROM (COALESCE(h.c -> p.path ->> 'state', 'Missing'), h.c -> p.path ->> 'value')), 0) + 1 AS "Revision"
            FROM unnest({array}) AS p(path)
            CROSS JOIN (SELECT r."ContentJson"::jsonb AS c FROM core."RegulatoryDraftRevisions" r
                WHERE r."TenantId" = {tenant} AND r."VesselId" = {vessel} AND r."Revision" = {head}) h
            """).ToArrayAsync(ct);
        return rows.ToDictionary(x => x.Path, x => x.Revision, StringComparer.Ordinal);
    }

    public static Task<ProfileFieldVersionRow[]> ValueVersions(PoseidonDbContext db, Guid tenant, Guid vessel, string path, int skip, int take, CancellationToken ct)
    {
        if (!VesselProfileFieldPolicy.Paths.Contains(path)) throw new ArgumentOutOfRangeException(nameof(path));
        return db.Database.SqlQuery<ProfileFieldVersionRow>($"""
            SELECT x."Revision", x."SavedAt", x."Provenance", x."State", x."Value" FROM (
              SELECT r."Revision", r."SavedAt", r."Provenance",
                COALESCE(r."ContentJson"::jsonb -> {path} ->> 'state', 'Missing') AS "State", r."ContentJson"::jsonb -> {path} ->> 'value' AS "Value",
                LAG(COALESCE(r."ContentJson"::jsonb -> {path} ->> 'state', 'Missing')) OVER (ORDER BY r."Revision") AS "PriorState",
                LAG(r."ContentJson"::jsonb -> {path} ->> 'value') OVER (ORDER BY r."Revision") AS "PriorValue",
                ROW_NUMBER() OVER (ORDER BY r."Revision") AS "Ordinal"
              FROM core."RegulatoryDraftRevisions" r WHERE r."TenantId" = {tenant} AND r."VesselId" = {vessel}) x
            WHERE x."Ordinal" = 1 OR (x."State", x."Value") IS DISTINCT FROM (x."PriorState", x."PriorValue")
            ORDER BY x."Revision" DESC OFFSET {skip} LIMIT {take}
            """).ToArrayAsync(ct);
    }
}
