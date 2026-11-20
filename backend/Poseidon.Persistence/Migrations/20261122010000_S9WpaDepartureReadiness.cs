using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S9WpaDepartureReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ship");

            migrationBuilder.CreateTable(
                name: "DepartureDecisions",
                schema: "ship",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssessedState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssessmentMode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssessmentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WindowFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    WindowTo = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartureDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_DepartureDecision_Decision", "\"Decision\" IN ('RELEASE', 'HOLD', 'DENY') AND (\"Decision\" <> 'RELEASE' OR \"AssessedState\" = 'READY' AND \"AssessmentMode\" = 'CURRENT')");
                    table.CheckConstraint("CK_DepartureDecision_Duty", "\"Duty\" = 'Master'");
                    table.CheckConstraint("CK_DepartureDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                    table.CheckConstraint("CK_DepartureDecision_Mode", "\"AssessmentMode\" IN ('CURRENT', 'FORECAST')");
                    table.CheckConstraint("CK_DepartureDecision_Reason", "char_length(\"Reason\") >= 10");
                    table.CheckConstraint("CK_DepartureDecision_State", "\"AssessedState\" IN ('READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED')");
                    table.CheckConstraint("CK_DepartureDecision_Window", "\"WindowTo\" >= \"WindowFrom\"");
                    table.ForeignKey(
                        name: "FK_DepartureDecisions_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepartureReceipts",
                schema: "ship",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartureReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_DepartureReceipt_Action", "\"Action\" IN ('DepartureSignoffRecord')");
                    table.ForeignKey(
                        name: "FK_DepartureReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartureDecisions_TenantId_VesselId_Sequence",
                schema: "ship",
                table: "DepartureDecisions",
                columns: new[] { "TenantId", "VesselId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureReceipts_TenantId_VesselId",
                schema: "ship",
                table: "DepartureReceipts",
                columns: new[] { "TenantId", "VesselId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartureDecisions",
                schema: "ship");

            migrationBuilder.DropTable(
                name: "DepartureReceipts",
                schema: "ship");
        }
    }
}
