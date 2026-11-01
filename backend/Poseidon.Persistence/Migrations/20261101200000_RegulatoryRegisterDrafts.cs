using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Authored migration; application and model parity verification deferred to CORE-BUNDLE-01.
public partial class RegulatoryRegisterDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "RegisterDrafts", schema: "core", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            ContentJson = table.Column<string>(type: "text", nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_RegisterDrafts", x => new { x.TenantId, x.Kind, x.Id });
            table.ForeignKey("FK_RegisterDrafts_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", "core", onDelete: ReferentialAction.Restrict);
            table.CheckConstraint("CK_RegisterDrafts_Kind", "\"Kind\" IN ('Source','Requirement') AND \"Revision\" > 0 AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified')");
        });
        migrationBuilder.CreateTable(name: "RegisterRevisions", schema: "core", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            ContentJson = table.Column<string>(type: "text", nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            SourceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
            SourceId = table.Column<Guid>(type: "uuid", nullable: true),
            SourceRevision = table.Column<long>(type: "bigint", nullable: true)
        }, constraints: table => {
            table.PrimaryKey("PK_RegisterRevisions", x => new { x.TenantId, x.Kind, x.Id, x.Revision });
            table.ForeignKey("FK_RegisterRevisions_RegisterDrafts_TenantId_Kind_Id", x => new { x.TenantId, x.Kind, x.Id }, "RegisterDrafts", new[] { "TenantId", "Kind", "Id" }, "core", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_RegisterRevisions_Source", x => new { x.TenantId, x.SourceKind, x.SourceId, x.SourceRevision }, "RegisterRevisions", new[] { "TenantId", "Kind", "Id", "Revision" }, "core", onDelete: ReferentialAction.Restrict);
            table.CheckConstraint("CK_RegisterRevisions_Lineage", "\"Revision\" > 0 AND \"Kind\" IN ('Source','Requirement') AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND ((\"SourceKind\" IS NULL AND \"SourceId\" IS NULL AND \"SourceRevision\" IS NULL) OR (\"Kind\" = 'Requirement' AND \"SourceKind\" IS NOT NULL AND \"SourceKind\" = 'Source' AND \"SourceId\" IS NOT NULL AND \"SourceRevision\" IS NOT NULL AND \"SourceRevision\" > 0))");
        });
        migrationBuilder.CreateIndex("IX_RegisterRevisions_TenantId_SourceKind_SourceId_SourceRevision", "RegisterRevisions", new[] { "TenantId", "SourceKind", "SourceId", "SourceRevision" }, "core");
        migrationBuilder.Sql("""
            CREATE TRIGGER register_revisions_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RegisterRevisions"
            FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("RegisterRevisions", "core");
        migrationBuilder.DropTable("RegisterDrafts", "core");
    }
}
