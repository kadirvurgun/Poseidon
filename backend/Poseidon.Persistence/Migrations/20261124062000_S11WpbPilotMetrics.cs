using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S11WpbPilotMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PilotMetricBatches",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorBucket = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RoleBucket = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedHour = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionPolicyRef = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotMetricBatches", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotMetricBatch_Shape", "\"SchemaVersion\" = 'pilot-event.v1' AND \"ActorBucket\" ~ '^[0-9a-f]{32}$' AND \"RoleBucket\" ~ '^[0-9a-f]{32}$' AND \"EventCount\" BETWEEN 1 AND 50 AND \"ExpiresAt\" > \"ReceivedHour\" AND length(\"RetentionPolicyRef\") BETWEEN 1 AND 128");
                    table.ForeignKey(
                        name: "FK_PilotMetricBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotMetricReceipts",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Accepted = table.Column<int>(type: "integer", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotMetricReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_PilotMetricReceipt_Accepted", "\"Accepted\" BETWEEN 1 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "PilotMetricTenantSalts",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotMetricTenantSalts", x => x.TenantId);
                    table.CheckConstraint("CK_PilotMetricTenantSalt_Length", "length(\"Salt\") = 32");
                    table.ForeignKey(
                        name: "FK_PilotMetricTenantSalts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotMetricBatches_TenantId_ExpiresAt",
                schema: "intelligence",
                table: "PilotMetricBatches",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotMetricBatches_TenantId_ReceivedHour",
                schema: "intelligence",
                table: "PilotMetricBatches",
                columns: new[] { "TenantId", "ReceivedHour" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotMetricReceipts_TenantId_ExpiresAt",
                schema: "intelligence",
                table: "PilotMetricReceipts",
                columns: new[] { "TenantId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotMetricBatches",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "PilotMetricReceipts",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "PilotMetricTenantSalts",
                schema: "intelligence");
        }
    }
}
