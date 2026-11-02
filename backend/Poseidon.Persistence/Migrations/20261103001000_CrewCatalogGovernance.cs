using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Additive authoring migration. Generated model/designer parity belongs to the combined gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261103001000_CrewCatalogGovernance")]
public sealed class CrewCatalogGovernance : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.CreateTable(name: "CatalogHeads", schema: "crew", columns: t => new {
            TenantId = t.Column<Guid>(type: "uuid", nullable: false), VesselId = t.Column<Guid>(type: "uuid", nullable: false),
            Revision = t.Column<long>(type: "bigint", nullable: false), ActiveRevision = t.Column<long>(type: "bigint", nullable: true),
            xmin = t.Column<uint>(type: "xid", rowVersion: true, nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_CatalogHeads", x => new { x.TenantId, x.VesselId });
            t.ForeignKey("FK_CatalogHeads_Vessels", x => new { x.TenantId, x.VesselId }, "Vessels", new[] { "TenantId", "Id" }, principalSchema: "core", onDelete: ReferentialAction.Restrict);
        });
        b.CreateTable(name: "CatalogEvents", schema: "crew", columns: t => new {
            TenantId = t.Column<Guid>(type: "uuid", nullable: false), VesselId = t.Column<Guid>(type: "uuid", nullable: false),
            Revision = t.Column<long>(type: "bigint", nullable: false), State = t.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            ContentJson = t.Column<string>(type: "text", nullable: false), PolicyVersion = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Hash = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false), PriorHash = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            ActorId = t.Column<Guid>(type: "uuid", nullable: false), RoleId = t.Column<Guid>(type: "uuid", nullable: false), AuthorityId = t.Column<Guid>(type: "uuid", nullable: false),
            AuthorityHash = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false), Reason = t.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
            SavedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_CatalogEvents", x => new { x.TenantId, x.VesselId, x.Revision });
            t.ForeignKey("FK_CatalogEvents_CatalogHeads", x => new { x.TenantId, x.VesselId }, "CatalogHeads", new[] { "TenantId", "VesselId" }, principalSchema: "crew", onDelete: ReferentialAction.Restrict);
        });
        b.AddForeignKey(name: "FK_CatalogHeads_CatalogEvents_Active", schema: "crew", table: "CatalogHeads",
            columns: new[] { "TenantId", "VesselId", "ActiveRevision" }, principalSchema: "crew", principalTable: "CatalogEvents",
            principalColumns: new[] { "TenantId", "VesselId", "Revision" }, onDelete: ReferentialAction.Restrict);
        b.CreateIndex(name: "IX_CatalogHeads_TenantId_VesselId_ActiveRevision", schema: "crew", table: "CatalogHeads",
            columns: new[] { "TenantId", "VesselId", "ActiveRevision" });
        b.Sql("""
            CREATE FUNCTION crew.reject_catalog_event_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Crew catalog lineage is immutable'; END; $$;
            CREATE TRIGGER catalog_events_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON crew."CatalogEvents"
                FOR EACH STATEMENT EXECUTE FUNCTION crew.reject_catalog_event_mutation();
            """);
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Catalog lineage requires a forward migration or controlled restore.");
}
