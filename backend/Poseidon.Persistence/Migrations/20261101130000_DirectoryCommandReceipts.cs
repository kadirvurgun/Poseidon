using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

public partial class DirectoryCommandReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "CommandReceipts", schema: "core", columns: table => new
        {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            ActorId = table.Column<Guid>(type: "uuid", nullable: false),
            Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
            ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
            CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_CommandReceipts", x => new { x.TenantId, x.ActorId, x.Key });
            table.ForeignKey(name: "FK_CommandReceipts_Tenants_TenantId", column: x => x.TenantId, principalSchema: "core", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.Sql("""
            CREATE TRIGGER command_receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."CommandReceipts"
            FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("CommandReceipts", "core");
}
