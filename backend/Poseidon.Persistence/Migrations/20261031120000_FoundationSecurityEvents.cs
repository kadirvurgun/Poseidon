using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

public partial class FoundationSecurityEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "SecurityEvents", schema: "audit", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            TenantId = table.Column<Guid>(type: "uuid", nullable: true),
            ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
            ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
            Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
            CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
            OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_SecurityEvents", x => x.Id);
            table.ForeignKey(name: "FK_SecurityEvents_Tenants_TenantId", column: x => x.TenantId, principalSchema: "core", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_SecurityEvents_TenantId_OccurredAt", "SecurityEvents", new[] { "TenantId", "OccurredAt" }, "audit");
        migrationBuilder.Sql("""
            CREATE TRIGGER security_events_append_only
            BEFORE UPDATE OR DELETE OR TRUNCATE ON audit."SecurityEvents"
            FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("SecurityEvents", "audit");
}
