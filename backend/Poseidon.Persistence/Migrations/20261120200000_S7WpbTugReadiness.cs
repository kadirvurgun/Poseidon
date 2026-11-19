using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S7WpbTugReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentProposals",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRevision = table.Column<long>(type: "bigint", nullable: false),
                    TugId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MasterAck = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProposedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentProposals", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AssignmentProposals_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TugAssignmentProposal_Ack", "\"MasterAck\" IN ('Pending', 'Accepted', 'Rejected')");
                    table.CheckConstraint("CK_TugAssignmentProposal_Role", "\"Role\" IN ('Lead', 'Support', 'Escort')");
                    table.CheckConstraint("CK_TugAssignmentProposal_State", "\"State\" IN ('Proposed', 'Cancelled') AND (\"State\" <> 'Cancelled' OR \"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL AND \"CancelledBy\" IS NOT NULL)");
                    table.CheckConstraint("CK_TugAssignmentProposal_Window", "\"PlannedEnd\" > \"PlannedStart\"");
                    table.ForeignKey(
                        name: "FK_AssignmentProposals_Jobs_TenantId_VesselId_JobId",
                        columns: x => new { x.TenantId, x.VesselId, x.JobId },
                        principalSchema: "tug",
                        principalTable: "Jobs",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentProposals_Vessels_TenantId_TugId",
                        columns: x => new { x.TenantId, x.TugId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessReceipts",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_TugReadinessReceipt_Action", "\"Action\" IN ('TugAssignmentPropose', 'TugAssignmentRespond', 'TugAssignmentWithdraw', 'TugReadinessDecide', 'TugTowEquipmentSave', 'TugTowEquipmentUpdate', 'TugTowEquipmentVerify')");
                    table.ForeignKey(
                        name: "FK_ReadinessReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowEquipment",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SwlTonnes = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    LastTestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextTestDue = table.Column<DateOnly>(type: "date", nullable: true),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Critical = table.Column<bool>(type: "boolean", nullable: true),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowEquipment", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TowEquipment_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TowEquipment_Condition", "\"Condition\" IN ('Serviceable', 'Degraded', 'Failed', 'Unknown')");
                    table.CheckConstraint("CK_TowEquipment_Kind", "\"Kind\" IN ('Winch', 'TowHook', 'Towline', 'EmergencyRelease', 'FiFi')");
                    table.CheckConstraint("CK_TowEquipment_Swl", "\"SwlTonnes\" IS NULL OR \"SwlTonnes\" > 0");
                    table.CheckConstraint("CK_TowEquipment_Test", "\"NextTestDue\" IS NULL OR \"LastTestDate\" IS NULL OR \"NextTestDue\" >= \"LastTestDate\"");
                    table.ForeignKey(
                        name: "FK_TowEquipment_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentResponses",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TugId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ScopeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentResponses", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TugAssignmentResponse_Decision", "\"Decision\" IN ('ACCEPT', 'REJECT') AND (\"Decision\" <> 'REJECT' OR \"Reason\" IS NOT NULL)");
                    table.CheckConstraint("CK_TugAssignmentResponse_Duty", "\"Duty\" IN ('TugMaster', 'Master')");
                    table.ForeignKey(
                        name: "FK_AssignmentResponses_AssignmentProposals_TenantId_VesselId_A~",
                        columns: x => new { x.TenantId, x.VesselId, x.AssignmentId },
                        principalSchema: "tug",
                        principalTable: "AssignmentProposals",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessDecisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    TugId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AssessedState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssessmentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TugReadinessDecision_Decision", "\"Decision\" IN ('GO', 'NO_GO') AND (\"Decision\" <> 'GO' OR \"AssessedState\" IN ('READY', 'CONDITIONAL'))");
                    table.CheckConstraint("CK_TugReadinessDecision_Duty", "\"Duty\" IN ('TugMaster', 'Master')");
                    table.CheckConstraint("CK_TugReadinessDecision_State", "\"AssessedState\" IN ('READY', 'CONDITIONAL', 'NOT_READY', 'REVIEW_REQUIRED')");
                    table.ForeignKey(
                        name: "FK_ReadinessDecisions_AssignmentProposals_TenantId_VesselId_As~",
                        columns: x => new { x.TenantId, x.VesselId, x.AssignmentId },
                        principalSchema: "tug",
                        principalTable: "AssignmentProposals",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowEquipmentRevisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowEquipmentRevisions", x => new { x.TenantId, x.VesselId, x.ItemId, x.Revision });
                    table.ForeignKey(
                        name: "FK_TowEquipmentRevisions_TowEquipment_TenantId_VesselId_ItemId",
                        columns: x => new { x.TenantId, x.VesselId, x.ItemId },
                        principalSchema: "tug",
                        principalTable: "TowEquipment",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowEquipmentVerifications",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemRevision = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowEquipmentVerifications", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TowEquipmentVerification_Duty", "\"Duty\" = 'ChiefEngineer'");
                    table.CheckConstraint("CK_TowEquipmentVerification_Outcome", "\"Outcome\" IN ('VERIFIED', 'NOT_VERIFIED') AND (\"Outcome\" <> 'NOT_VERIFIED' OR \"Reason\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TowEquipmentVerifications_TowEquipmentRevisions_TenantId_Ve~",
                        columns: x => new { x.TenantId, x.VesselId, x.ItemId, x.ItemRevision },
                        principalSchema: "tug",
                        principalTable: "TowEquipmentRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "ItemId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentProposals_TenantId_TugId_State_PlannedStart",
                schema: "tug",
                table: "AssignmentProposals",
                columns: new[] { "TenantId", "TugId", "State", "PlannedStart" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentProposals_TenantId_VesselId_JobId_TugId",
                schema: "tug",
                table: "AssignmentProposals",
                columns: new[] { "TenantId", "VesselId", "JobId", "TugId" },
                unique: true,
                filter: "\"State\" = 'Proposed'");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentResponses_TenantId_AssignmentId",
                schema: "tug",
                table: "AssignmentResponses",
                columns: new[] { "TenantId", "AssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentResponses_TenantId_VesselId_AssignmentId",
                schema: "tug",
                table: "AssignmentResponses",
                columns: new[] { "TenantId", "VesselId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessDecisions_TenantId_VesselId_AssignmentId_RecordedAt",
                schema: "tug",
                table: "ReadinessDecisions",
                columns: new[] { "TenantId", "VesselId", "AssignmentId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessReceipts_TenantId_VesselId",
                schema: "tug",
                table: "ReadinessReceipts",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_TowEquipment_TenantId_VesselId_Kind",
                schema: "tug",
                table: "TowEquipment",
                columns: new[] { "TenantId", "VesselId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_TowEquipmentVerifications_TenantId_VesselId_ItemId_ItemRevi~",
                schema: "tug",
                table: "TowEquipmentVerifications",
                columns: new[] { "TenantId", "VesselId", "ItemId", "ItemRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentResponses",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "ReadinessDecisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "ReadinessReceipts",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowEquipmentVerifications",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "AssignmentProposals",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowEquipmentRevisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowEquipment",
                schema: "tug");
        }
    }
}
