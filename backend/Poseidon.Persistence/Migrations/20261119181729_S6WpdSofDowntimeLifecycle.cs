using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S6WpdSofDowntimeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipOperation_State",
                schema: "operations",
                table: "ShipOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipOperationRevision_State",
                schema: "operations",
                table: "ShipOperationRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Downtime_Draft",
                schema: "operations",
                table: "Downtimes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DowntimeRevision_Draft",
                schema: "operations",
                table: "DowntimeRevisions");

            migrationBuilder.AddColumn<long>(
                name: "FinalRevision",
                schema: "operations",
                table: "ShipOperations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SofState",
                schema: "operations",
                table: "ShipOperations",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FinalRevision",
                schema: "operations",
                table: "ShipOperationRevisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SofState",
                schema: "operations",
                table: "ShipOperationRevisions",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimEvidenceJson",
                schema: "operations",
                table: "Downtimes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureState",
                schema: "operations",
                table: "Downtimes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimEvidenceJson",
                schema: "operations",
                table: "DowntimeRevisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureState",
                schema: "operations",
                table: "DowntimeRevisions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShipOpsLifecycleEvents",
                schema: "operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromRevision = table.Column<long>(type: "bigint", nullable: false),
                    ToRevision = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ToState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: false),
                    SignatureState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipOpsLifecycleEvents", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ShipOpsLifecycleEvent_State", "\"Module\" IN ('PortSof','Downtime') AND \"FromRevision\">0 AND \"ToRevision\"=\"FromRevision\"+1 AND \"SignatureState\"='SOF_SIGNATURE_NOT_RECORDED' AND (\"Module\"<>'PortSof' OR \"Action\"='FINALIZE' AND \"AuthorityId\" IS NOT NULL AND \"Duty\" IN ('Master','ChiefOfficer','ChiefEngineer'))");
                    table.ForeignKey(
                        name: "FK_ShipOpsLifecycleEvents_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipOperation_State",
                schema: "operations",
                table: "ShipOperations",
                sql: "\"Kind\" IN ('Fuel','Port') AND \"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"SofState\" IS NULL OR \"Kind\"='Port' AND \"SofState\" IN ('DRAFT','FINAL')) AND (\"FinalRevision\" IS NULL OR \"FinalRevision\">0 AND \"FinalRevision\"<=\"Revision\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipOperationRevision_State",
                schema: "operations",
                table: "ShipOperationRevisions",
                sql: "\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"SofState\" IS NULL OR \"SofState\" IN ('DRAFT','FINAL')) AND (\"FinalRevision\" IS NULL OR \"FinalRevision\">0 AND \"FinalRevision\"<=\"Revision\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Downtime_Draft",
                schema: "operations",
                table: "Downtimes",
                sql: "\"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"ClosureState\" IS NULL OR \"ClosureState\" IN ('OPEN','IN_REVIEW','VERIFIED','CLOSED'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DowntimeRevision_Draft",
                schema: "operations",
                table: "DowntimeRevisions",
                sql: "\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"ClosureState\" IS NULL OR \"ClosureState\" IN ('OPEN','IN_REVIEW','VERIFIED','CLOSED'))");

            migrationBuilder.CreateIndex(
                name: "IX_ShipOpsLifecycleEvents_TenantId_VesselId_Module_RecordId_To~",
                schema: "operations",
                table: "ShipOpsLifecycleEvents",
                columns: new[] { "TenantId", "VesselId", "Module", "RecordId", "ToRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipOpsLifecycleEvents",
                schema: "operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipOperation_State",
                schema: "operations",
                table: "ShipOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ShipOperationRevision_State",
                schema: "operations",
                table: "ShipOperationRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Downtime_Draft",
                schema: "operations",
                table: "Downtimes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DowntimeRevision_Draft",
                schema: "operations",
                table: "DowntimeRevisions");

            migrationBuilder.DropColumn(
                name: "FinalRevision",
                schema: "operations",
                table: "ShipOperations");

            migrationBuilder.DropColumn(
                name: "SofState",
                schema: "operations",
                table: "ShipOperations");

            migrationBuilder.DropColumn(
                name: "FinalRevision",
                schema: "operations",
                table: "ShipOperationRevisions");

            migrationBuilder.DropColumn(
                name: "SofState",
                schema: "operations",
                table: "ShipOperationRevisions");

            migrationBuilder.DropColumn(
                name: "ClaimEvidenceJson",
                schema: "operations",
                table: "Downtimes");

            migrationBuilder.DropColumn(
                name: "ClosureState",
                schema: "operations",
                table: "Downtimes");

            migrationBuilder.DropColumn(
                name: "ClaimEvidenceJson",
                schema: "operations",
                table: "DowntimeRevisions");

            migrationBuilder.DropColumn(
                name: "ClosureState",
                schema: "operations",
                table: "DowntimeRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipOperation_State",
                schema: "operations",
                table: "ShipOperations",
                sql: "\"Kind\" IN ('Fuel','Port') AND \"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShipOperationRevision_State",
                schema: "operations",
                table: "ShipOperationRevisions",
                sql: "\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Downtime_Draft",
                schema: "operations",
                table: "Downtimes",
                sql: "\"Revision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DowntimeRevision_Draft",
                schema: "operations",
                table: "DowntimeRevisions",
                sql: "\"Revision\">0 AND \"RecordVersion\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");
        }
    }
}
