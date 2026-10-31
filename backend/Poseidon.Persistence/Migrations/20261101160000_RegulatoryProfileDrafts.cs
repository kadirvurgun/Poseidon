using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

public partial class RegulatoryProfileDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "RegulatoryDrafts", schema: "core", columns: table => new
        {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            ContentJson = table.Column<string>(type: "text", nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RegulatoryDrafts", x => new { x.TenantId, x.VesselId });
            table.ForeignKey(name: "FK_RegulatoryDrafts_Vessels_TenantId_VesselId", columns: x => new { x.TenantId, x.VesselId },
                principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable(name: "RegulatoryDraftRevisions", schema: "core", columns: table => new
        {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            ContentJson = table.Column<string>(type: "text", nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RegulatoryDraftRevisions", x => new { x.TenantId, x.VesselId, x.Revision });
            table.ForeignKey(name: "FK_RegulatoryDraftRevisions_RegulatoryDrafts_TenantId_VesselId", columns: x => new { x.TenantId, x.VesselId },
                principalSchema: "core", principalTable: "RegulatoryDrafts", principalColumns: new[] { "TenantId", "VesselId" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.Sql("""
            CREATE TRIGGER regulatory_revisions_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RegulatoryDraftRevisions"
            FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("RegulatoryDraftRevisions", "core");
        migrationBuilder.DropTable("RegulatoryDrafts", "core");
    }
}
