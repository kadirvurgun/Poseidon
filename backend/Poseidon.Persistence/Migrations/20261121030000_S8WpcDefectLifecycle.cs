using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S8WpcDefectLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DefectLifecycles",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssessedCriticality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectLifecycles", x => new { x.TenantId, x.DefectId });
                    table.UniqueConstraint("AK_DefectLifecycles_TenantId_VesselId_DefectId", x => new { x.TenantId, x.VesselId, x.DefectId });
                    table.CheckConstraint("CK_DefectLifecycle_State", "\"Revision\">0 AND \"Stage\" IN ('DETECT','ASSESS','SAFEGUARD','ACTION','VERIFY','EFFECTIVENESS','CLOSED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"AssessedCriticality\" IS NULL OR \"AssessedCriticality\" IN ('Critical','High','Medium','Low')) AND ((\"Stage\"='CLOSED')=(\"ClosedBy\" IS NOT NULL AND \"ClosedAt\" IS NOT NULL)) AND (\"Provenance\"<>'SyntheticUnverified' OR \"Stage\" IN ('DETECT','ASSESS') AND \"ClosedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_DefectLifecycle_Defect",
                        columns: x => new { x.TenantId, x.VesselId, x.DefectId },
                        principalSchema: "technical",
                        principalTable: "Defects",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceReports",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetRevision = table.Column<long>(type: "bigint", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderRevision = table.Column<long>(type: "bigint", nullable: true),
                    DefectLinksJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedRevision = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceReports", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_ServiceReports_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_ServiceReport_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','VERIFIED','RETURNED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'VERIFIED' OR \"VerifiedRevision\"=\"Revision\") AND (\"AssetId\" IS NULL)=(\"AssetRevision\" IS NULL) AND (\"WorkOrderId\" IS NULL)=(\"WorkOrderRevision\" IS NULL) AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedRevision\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ServiceReport_AssetRevision",
                        columns: x => new { x.TenantId, x.VesselId, x.AssetId, x.AssetRevision },
                        principalSchema: "technical",
                        principalTable: "AssetRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "AssetId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceReports_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalRecordReceipts",
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
                    table.PrimaryKey("PK_TechnicalRecordReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_TechnicalRecordReceipt_Revision", "\"Revision\">0");
                    table.ForeignKey(
                        name: "FK_TechnicalRecordReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DefectLifecycleRecords",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StageAfter = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DefectRevision = table.Column<long>(type: "bigint", nullable: false),
                    Criticality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    MeasureKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Temporary = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MeasureId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectLifecycleRecords", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_DefectLifecycleRecord_Kind", "\"Sequence\">0 AND \"DefectRevision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND length(\"Text\")<=4000 AND \"StageAfter\" IN ('DETECT','ASSESS','SAFEGUARD','ACTION','VERIFY','EFFECTIVENESS','CLOSED') AND (\"Kind\"='START' AND \"StageAfter\"='DETECT' OR \"Kind\"='ASSESS' AND \"Criticality\" IN ('Critical','High','Medium','Low') AND length(\"Text\")>=10 OR \"Kind\"='SAFEGUARD' AND \"MeasureKind\" IN ('SAFEGUARD','RESTRICTION') AND length(\"Text\")>=1 AND (NOT \"Temporary\" OR \"ExpiresAt\" IS NOT NULL) OR \"Kind\"='LIFT' AND \"MeasureId\" IS NOT NULL AND length(\"Text\")>=10 OR \"Kind\"='ACTION' AND \"StageAfter\"='ACTION' AND length(\"Text\")>=1 OR \"Kind\"='VERIFY' AND \"Outcome\" IN ('VERIFIED','NOT_VERIFIED') AND length(\"Text\")>=10 OR \"Kind\"='EFFECTIVENESS' AND \"Outcome\" IN ('EFFECTIVE','NOT_EFFECTIVE') AND length(\"Text\")>=10 OR \"Kind\"='CLOSE' AND \"StageAfter\"='CLOSED' AND \"Outcome\"='RETURNED_TO_SERVICE' AND length(\"Text\")>=10) AND (\"Provenance\"<>'SyntheticUnverified' OR \"Kind\" IN ('START','ASSESS'))");
                    table.ForeignKey(
                        name: "FK_DefectLifecycleRecord_DefectRevision",
                        columns: x => new { x.TenantId, x.DefectId, x.DefectRevision },
                        principalSchema: "technical",
                        principalTable: "DefectRevisions",
                        principalColumns: new[] { "TenantId", "DefectId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefectLifecycleRecord_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.DefectId },
                        principalSchema: "technical",
                        principalTable: "DefectLifecycles",
                        principalColumns: new[] { "TenantId", "VesselId", "DefectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceReportRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetRevision = table.Column<long>(type: "bigint", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderRevision = table.Column<long>(type: "bigint", nullable: true),
                    DefectLinksJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceReportRevisions", x => new { x.TenantId, x.ReportId, x.Revision });
                    table.CheckConstraint("CK_ServiceReportRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','VERIFIED','RETURNED') AND \"ReasonCode\" IN ('CREATE','EDIT','SUBMIT','VERIFY','RETURN','AMEND') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT')");
                    table.ForeignKey(
                        name: "FK_ServiceReportRevision_AssetRevision",
                        columns: x => new { x.TenantId, x.VesselId, x.AssetId, x.AssetRevision },
                        principalSchema: "technical",
                        principalTable: "AssetRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "AssetId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceReportRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.ReportId },
                        principalSchema: "technical",
                        principalTable: "ServiceReports",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceReportVerifications",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ResultRevision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceReportVerifications", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ServiceReportVerification_Kind", "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10");
                    table.ForeignKey(
                        name: "FK_ServiceReportVerification_Revision",
                        columns: x => new { x.TenantId, x.ReportId, x.Revision },
                        principalSchema: "technical",
                        principalTable: "ServiceReportRevisions",
                        principalColumns: new[] { "TenantId", "ReportId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefectLifecycleRecords_TenantId_DefectId_DefectRevision",
                schema: "technical",
                table: "DefectLifecycleRecords",
                columns: new[] { "TenantId", "DefectId", "DefectRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_DefectLifecycleRecords_TenantId_DefectId_Sequence",
                schema: "technical",
                table: "DefectLifecycleRecords",
                columns: new[] { "TenantId", "DefectId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefectLifecycleRecords_TenantId_VesselId_DefectId",
                schema: "technical",
                table: "DefectLifecycleRecords",
                columns: new[] { "TenantId", "VesselId", "DefectId" });

            migrationBuilder.CreateIndex(
                name: "IX_DefectLifecycles_TenantId_VesselId_Stage",
                schema: "technical",
                table: "DefectLifecycles",
                columns: new[] { "TenantId", "VesselId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReportRevisions_TenantId_VesselId_AssetId_AssetRevis~",
                schema: "technical",
                table: "ServiceReportRevisions",
                columns: new[] { "TenantId", "VesselId", "AssetId", "AssetRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReportRevisions_TenantId_VesselId_ReportId",
                schema: "technical",
                table: "ServiceReportRevisions",
                columns: new[] { "TenantId", "VesselId", "ReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReports_TenantId_ReportNo",
                schema: "technical",
                table: "ServiceReports",
                columns: new[] { "TenantId", "ReportNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReports_TenantId_VesselId_AssetId",
                schema: "technical",
                table: "ServiceReports",
                columns: new[] { "TenantId", "VesselId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReports_TenantId_VesselId_AssetId_AssetRevision",
                schema: "technical",
                table: "ServiceReports",
                columns: new[] { "TenantId", "VesselId", "AssetId", "AssetRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReports_TenantId_VesselId_SavedAt_Id",
                schema: "technical",
                table: "ServiceReports",
                columns: new[] { "TenantId", "VesselId", "SavedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReports_TenantId_VesselId_WorkOrderId",
                schema: "technical",
                table: "ServiceReports",
                columns: new[] { "TenantId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReportVerifications_TenantId_ReportId_Revision",
                schema: "technical",
                table: "ServiceReportVerifications",
                columns: new[] { "TenantId", "ReportId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRecordReceipts_TenantId_VesselId",
                schema: "technical",
                table: "TechnicalRecordReceipts",
                columns: new[] { "TenantId", "VesselId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefectLifecycleRecords",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "ServiceReportVerifications",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "TechnicalRecordReceipts",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "DefectLifecycles",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "ServiceReportRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "ServiceReports",
                schema: "technical");
        }
    }
}
