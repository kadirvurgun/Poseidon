using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Poseidon.Persistence.Migrations;

// Authored checkpoint; PostgreSQL execution and generated model parity are DEFERRED.
public partial class DocumentObjectStorage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Mime", schema: "files", table: "FileStages", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "image/png");
        migrationBuilder.AddColumn<Guid>(name: "DocumentId", schema: "files", table: "FileStages", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<long>(name: "DocumentRevision", schema: "files", table: "FileStages", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "StorageReference", schema: "files", table: "FileStages", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProviderVersion", schema: "files", table: "StoredFiles", type: "character varying(1024)", maxLength: 1024, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProviderETag", schema: "files", table: "StoredFiles", type: "character varying(256)", maxLength: 256, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ProviderReference", schema: "files", table: "StoredFiles", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.CreateTable(name: "Documents", schema: "files", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false), Id = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false), Revision = table.Column<long>(type: "bigint", nullable: false),
            xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_Documents", x => new { x.TenantId, x.Id });
            table.UniqueConstraint("AK_Documents_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId });
            table.ForeignKey("FK_Documents_Vessels_TenantId_VesselId", x => new { x.TenantId, x.VesselId }, principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable(name: "DocumentVersions", schema: "files", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false), DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false), VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_DocumentVersions", x => new { x.TenantId, x.DocumentId, x.Revision });
            table.UniqueConstraint("AK_DocumentVersions_TenantId_DocumentId_Revision_VesselId", x => new { x.TenantId, x.DocumentId, x.Revision, x.VesselId });
            table.ForeignKey("FK_DocumentVersions_Documents_TenantId_DocumentId_VesselId", x => new { x.TenantId, x.DocumentId, x.VesselId }, principalSchema: "files", principalTable: "Documents", principalColumns: new[] { "TenantId", "Id", "VesselId" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_Documents_TenantId_VesselId", schema: "files", table: "Documents", columns: new[] { "TenantId", "VesselId" });
        migrationBuilder.CreateIndex(name: "IX_DocumentVersions_TenantId_DocumentId_VesselId", schema: "files", table: "DocumentVersions", columns: new[] { "TenantId", "DocumentId", "VesselId" });
        migrationBuilder.CreateIndex(name: "IX_FileStages_TenantId_DocumentId_DocumentRevision_VesselId", schema: "files", table: "FileStages", columns: new[] { "TenantId", "DocumentId", "DocumentRevision", "VesselId" });
        migrationBuilder.AddForeignKey(name: "FK_FileStages_DocumentVersion", schema: "files", table: "FileStages", columns: new[] { "TenantId", "DocumentId", "DocumentRevision", "VesselId" }, principalSchema: "files", principalTable: "DocumentVersions", principalColumns: new[] { "TenantId", "DocumentId", "Revision", "VesselId" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddCheckConstraint(name: "CK_FileStages_DocumentOwner", schema: "files", table: "FileStages", sql: "(\"DocumentId\" IS NULL AND \"DocumentRevision\" IS NULL) OR (\"DocumentId\" IS NOT NULL AND \"DocumentRevision\" IS NOT NULL AND \"DocumentRevision\" > 0)");
        migrationBuilder.Sql("""
            CREATE TRIGGER document_versions_no_mutation BEFORE UPDATE OR DELETE OR TRUNCATE ON files."DocumentVersions"
            FOR EACH STATEMENT EXECUTE FUNCTION files.deny_revision_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_FileStages_DocumentVersion", schema: "files", table: "FileStages");
        migrationBuilder.DropCheckConstraint(name: "CK_FileStages_DocumentOwner", schema: "files", table: "FileStages");
        migrationBuilder.DropIndex(name: "IX_FileStages_TenantId_DocumentId_DocumentRevision_VesselId", schema: "files", table: "FileStages");
        migrationBuilder.DropTable(name: "DocumentVersions", schema: "files"); migrationBuilder.DropTable(name: "Documents", schema: "files");
        foreach (var name in new[] { "Mime", "DocumentId", "DocumentRevision", "StorageReference" }) migrationBuilder.DropColumn(name: name, schema: "files", table: "FileStages");
        foreach (var name in new[] { "ProviderVersion", "ProviderETag", "ProviderReference" }) migrationBuilder.DropColumn(name: name, schema: "files", table: "StoredFiles");
    }
}
