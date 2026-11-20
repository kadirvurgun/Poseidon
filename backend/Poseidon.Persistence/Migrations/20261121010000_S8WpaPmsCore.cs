using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S8WpaPmsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeasurementPoints",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Precision = table.Column<int>(type: "integer", nullable: false),
                    RangeMin = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RangeMax = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ParentPointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementPoints", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_MeasurementPoints_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_MeasurementPoint_CounterParent", "\"ParentPointId\" IS NULL OR \"Kind\" = 'RUNNING_HOURS'");
                    table.CheckConstraint("CK_MeasurementPoint_Draft", "\"Status\" = 'Draft'");
                    table.CheckConstraint("CK_MeasurementPoint_Kind", "\"Kind\" IN ('RUNNING_HOURS','RPM','PRESSURE','TEMPERATURE','VIBRATION')");
                    table.CheckConstraint("CK_MeasurementPoint_Precision", "\"Precision\" BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_MeasurementPoint_Revision", "\"Revision\" > 0");
                    table.CheckConstraint("CK_MeasurementPoint_SelfParent", "\"ParentPointId\" IS NULL OR \"ParentPointId\" <> \"Id\"");
                    table.CheckConstraint("CK_MeasurementPoint_Source", "\"Source\" IN ('MANUAL','DEVICE')");
                    table.ForeignKey(
                        name: "FK_MeasurementPoints_Assets_TenantId_VesselId_AssetId",
                        columns: x => new { x.TenantId, x.VesselId, x.AssetId },
                        principalSchema: "technical",
                        principalTable: "Assets",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeasurementPoints_MeasurementPoints_TenantId_VesselId_Paren~",
                        columns: x => new { x.TenantId, x.VesselId, x.ParentPointId },
                        principalSchema: "technical",
                        principalTable: "MeasurementPoints",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PmsCommandReceipts",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PmsCommandReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_PmsCommandReceipt_Revision", "\"Revision\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Postponements",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanRevision = table.Column<long>(type: "bigint", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefectIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RequirementRefsJson = table.Column<string>(type: "jsonb", nullable: false),
                    OriginalDue = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedDeadline = table.Column<DateOnly>(type: "date", nullable: false),
                    ExtensionDays = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Criticality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PolicyRef = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MaxExtensionDays = table.Column<int>(type: "integer", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postponements", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_Postponements_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_Postponement_ApprovedPolicy", "\"State\" <> 'APPROVED' OR (\"PolicyRef\" IS NOT NULL AND \"PolicyVersion\" IS NOT NULL AND \"MaxExtensionDays\" IS NOT NULL AND \"ExtensionDays\" <= \"MaxExtensionDays\")");
                    table.CheckConstraint("CK_Postponement_Criticality", "\"Criticality\" IN ('CRITICAL','IMPORTANT','STANDARD','UNKNOWN')");
                    table.CheckConstraint("CK_Postponement_Deadline", "\"RequestedDeadline\" > \"OriginalDue\"");
                    table.CheckConstraint("CK_Postponement_Decision", "(\"State\" = 'REQUESTED' AND \"DecidedBy\" IS NULL AND \"DecidedAt\" IS NULL) OR (\"State\" <> 'REQUESTED' AND \"DecidedBy\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" <> \"RequestedBy\" AND \"DecisionReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_Postponement_Revision", "\"Revision\" > 0 AND \"PlanRevision\" > 0");
                    table.CheckConstraint("CK_Postponement_State", "\"State\" IN ('REQUESTED','APPROVED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_Postponements_Plans_TenantId_VesselId_PlanId",
                        columns: x => new { x.TenantId, x.VesselId, x.PlanId },
                        principalSchema: "technical",
                        principalTable: "Plans",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementPointRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementPointRevisions", x => new { x.TenantId, x.VesselId, x.PointId, x.Revision });
                    table.CheckConstraint("CK_MeasurementPointRevision_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_MeasurementPointRevisions_MeasurementPoints_TenantId_Vessel~",
                        columns: x => new { x.TenantId, x.VesselId, x.PointId },
                        principalSchema: "technical",
                        principalTable: "MeasurementPoints",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeasurementReadings",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvidenceFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousReadingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Delta = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PropagatedFromReadingId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeasurementReadings", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_MeasurementReadings_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_MeasurementReading_Propagated", "(\"SourceType\" = 'PROPAGATED') = (\"PropagatedFromReadingId\" IS NOT NULL)");
                    table.CheckConstraint("CK_MeasurementReading_Source", "\"SourceType\" IN ('MANUAL','DEVICE','IMPORT','PROPAGATED')");
                    table.ForeignKey(
                        name: "FK_MeasurementReadings_MeasurementPoints_TenantId_VesselId_Poi~",
                        columns: x => new { x.TenantId, x.VesselId, x.PointId },
                        principalSchema: "technical",
                        principalTable: "MeasurementPoints",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementPoints_TenantId_VesselId_AssetId",
                schema: "technical",
                table: "MeasurementPoints",
                columns: new[] { "TenantId", "VesselId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementPoints_TenantId_VesselId_Code",
                schema: "technical",
                table: "MeasurementPoints",
                columns: new[] { "TenantId", "VesselId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementPoints_TenantId_VesselId_ParentPointId",
                schema: "technical",
                table: "MeasurementPoints",
                columns: new[] { "TenantId", "VesselId", "ParentPointId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeasurementReadings_TenantId_VesselId_PointId_MeasuredAt",
                schema: "technical",
                table: "MeasurementReadings",
                columns: new[] { "TenantId", "VesselId", "PointId", "MeasuredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Postponements_TenantId_VesselId_PlanId",
                schema: "technical",
                table: "Postponements",
                columns: new[] { "TenantId", "VesselId", "PlanId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeasurementPointRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "MeasurementReadings",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "PmsCommandReceipts",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "Postponements",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "MeasurementPoints",
                schema: "technical");
        }
    }
}
