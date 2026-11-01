using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;
// Hand-authored, execution/model parity DEFERRED to AUDIT-FILES-BUNDLE-01.
public partial class SecureFilesFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "files");
        migrationBuilder.AddColumn<string>(name: "RoleContext", schema: "audit", table: "AuditEvents", type: "character varying(600)", maxLength: 600, nullable: true);
        migrationBuilder.CreateTable(name: "FileStages", schema: "files", columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ExpectedChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ExpectedLength = table.Column<long>(type: "bigint", nullable: false),
                State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            }, constraints: table => {
                table.PrimaryKey("PK_FileStages", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_FileStages_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId });
                table.ForeignKey("FK_FileStages_Vessels_TenantId_VesselId", x => new { x.TenantId, x.VesselId }, principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_FileStages_TenantId_VesselId", schema: "files", table: "FileStages", columns: new[] { "TenantId", "VesselId" });
        migrationBuilder.CreateIndex(name: "IX_FileStages_TenantId_ActorId_Key", schema: "files", table: "FileStages", columns: new[] { "TenantId", "ActorId", "Key" }, unique: true);
        migrationBuilder.CreateTable(name: "StoredFiles", schema: "files", columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                Mime = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                RetentionReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ScannerReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ScannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            }, constraints: table => {
                table.PrimaryKey("PK_StoredFiles", x => new { x.TenantId, x.Id });
                table.ForeignKey("FK_StoredFiles_Vessels_TenantId_VesselId", x => new { x.TenantId, x.VesselId }, principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_StoredFiles_FileStages_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId }, principalSchema: "files", principalTable: "FileStages", principalColumns: new[] { "TenantId", "Id", "VesselId" }, onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_StoredFiles_TenantId_Id_VesselId", schema: "files", table: "StoredFiles", columns: new[] { "TenantId", "Id", "VesselId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_StoredFiles_TenantId_VesselId", schema: "files", table: "StoredFiles", columns: new[] { "TenantId", "VesselId" });
        migrationBuilder.Sql("""
            CREATE FUNCTION files.deny_revision_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'File revisions are immutable'; END; $$;
            CREATE TRIGGER stored_file_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON files."StoredFiles"
              FOR EACH STATEMENT EXECUTE FUNCTION files.deny_revision_mutation();
            CREATE FUNCTION files.guard_stage() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP <> 'UPDATE' OR (to_jsonb(NEW) - 'State') IS DISTINCT FROM (to_jsonb(OLD) - 'State') OR
                 NOT ((OLD."State" = 'Pending' AND NEW."State" IN ('Receiving', 'Abandoned')) OR
                      (OLD."State" = 'Receiving' AND NEW."State" IN ('CleanTransport', 'Abandoned'))) THEN
                RAISE EXCEPTION 'File intent is immutable';
              END IF;
              RETURN NEW;
            END; $$;
            CREATE TRIGGER file_stage_guard BEFORE UPDATE OR DELETE ON files."FileStages"
              FOR EACH ROW EXECUTE FUNCTION files.guard_stage();
            CREATE TRIGGER file_stage_truncate BEFORE TRUNCATE ON files."FileStages"
              FOR EACH STATEMENT EXECUTE FUNCTION files.deny_revision_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StoredFiles", schema: "files");
        migrationBuilder.DropTable(name: "FileStages", schema: "files");
        migrationBuilder.Sql("DROP FUNCTION files.guard_stage(); DROP FUNCTION files.deny_revision_mutation();");
        migrationBuilder.DropColumn(name: "RoleContext", schema: "audit", table: "AuditEvents");
    }
}
