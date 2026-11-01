using Microsoft.EntityFrameworkCore.Migrations;
namespace Poseidon.Persistence.Migrations;
public partial class AuditDomainCapabilities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AlterColumn<string>(
        name: "CapabilityContext", schema: "audit", table: "AuditEvents", type: "character varying(2048)", maxLength: 2048, nullable: true,
        oldClrType: typeof(string), oldType: "character varying(512)", oldMaxLength: 512, oldNullable: true);
    // PostgreSQL refuses a lossy shrink if later records exceed512. Never truncate/delete audit to force rollback.
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.AlterColumn<string>(
        name: "CapabilityContext", schema: "audit", table: "AuditEvents", type: "character varying(512)", maxLength: 512, nullable: true,
        oldClrType: typeof(string), oldType: "character varying(2048)", oldMaxLength: 2048, oldNullable: true);
}
