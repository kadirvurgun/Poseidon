using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S11WpaReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProvenance",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TemplateVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TemplateFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRevision = table.Column<long>(type: "bigint", nullable: false),
                    RevisionKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Operation = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProvenance", x => new { x.TenantId, x.BatchId, x.RowNumber });
                    table.CheckConstraint("CK_ImportProvenance_Kind", "\"RowNumber\" > 1 AND \"TargetRevision\" > 0 AND \"TargetKind\" IN ('Vessel', 'CrewAssignment', 'Credential', 'Asset') AND \"Operation\" IN ('CREATE', 'UPDATE') AND \"RevisionKind\" IN ('Revision', 'RowVersion') AND \"Category\" IN ('vessel', 'crew', 'certificate', 'equipment')");
                    table.ForeignKey(
                        name: "FK_ImportProvenance_ImportBatches_Scope",
                        columns: x => new { x.TenantId, x.BatchId, x.VesselId },
                        principalSchema: "integration",
                        principalTable: "ImportBatches",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportProvenance_ImportPreviews_Scope",
                        columns: x => new { x.TenantId, x.PreviewId, x.VesselId },
                        principalSchema: "integration",
                        principalTable: "ImportPreviews",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportReconciliationReceipts",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportReconciliationReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_ImportReconciliationReceipt_Action", "\"Action\" IN ('ReconciliationReportCreate', 'ReconciliationSignOff') AND \"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_ImportReconciliationReceipts_Vessels_Scope",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationReports",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BatchesJson = table.Column<string>(type: "text", nullable: false),
                    SourceCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    NotCommittedCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCriticalCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCriticalJson = table.Column<string>(type: "text", nullable: false),
                    SampleSeed = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SampleJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignOffId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationReports", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_ReconciliationReports_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_ReconciliationReport_State", "\"Revision\" > 0 AND \"State\" IN ('AWAITING_SIGN_OFF', 'SIGNED_OFF', 'SUPERSEDED') AND \"Provenance\" IN ('UserDeclaredUnverified', 'SyntheticUnverified') AND \"SourceCount\" = \"AcceptedCount\" + \"RejectedCount\" + \"NotCommittedCount\" AND \"DuplicateCount\" <= \"RejectedCount\" AND (\"State\" <> 'SIGNED_OFF' OR \"SignOffId\" IS NOT NULL AND \"SignedBy\" IS NOT NULL AND \"SignedBy\" <> \"CreatedBy\") AND (\"State\" <> 'SUPERSEDED' OR \"SupersededAt\" IS NOT NULL) AND (\"Provenance\" <> 'SyntheticUnverified' OR \"SignOffId\" IS NULL AND \"SignedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ReconciliationReports_Vessels_Scope",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationSignOffs",
                schema: "integration",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportAuthor = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExceptionsJson = table.Column<string>(type: "text", nullable: false),
                    SampleVerifiedJson = table.Column<string>(type: "text", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationSignOffs", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ReconciliationSignOff_Decision", "\"Decision\" IN ('ACCEPTED', 'ACCEPTED_WITH_EXCEPTIONS', 'REJECTED') AND \"Duty\" = 'DataOwner' AND \"SignedBy\" <> \"ReportAuthor\" AND char_length(\"Reason\") >= 10");
                    table.ForeignKey(
                        name: "FK_ReconciliationSignOffs_Report",
                        columns: x => new { x.TenantId, x.VesselId, x.ReportId },
                        principalSchema: "integration",
                        principalTable: "ReconciliationReports",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProvenance_TenantId_BatchId_VesselId",
                schema: "integration",
                table: "ImportProvenance",
                columns: new[] { "TenantId", "BatchId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProvenance_TenantId_PreviewId_VesselId",
                schema: "integration",
                table: "ImportProvenance",
                columns: new[] { "TenantId", "PreviewId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProvenance_TenantId_VesselId_TargetKind_TargetId",
                schema: "integration",
                table: "ImportProvenance",
                columns: new[] { "TenantId", "VesselId", "TargetKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportReconciliationReceipts_TenantId_VesselId",
                schema: "integration",
                table: "ImportReconciliationReceipts",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationReports_Current",
                schema: "integration",
                table: "ReconciliationReports",
                columns: new[] { "TenantId", "VesselId" },
                unique: true,
                filter: "\"SupersededAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationReports_TenantId_VesselId_CreatedAt",
                schema: "integration",
                table: "ReconciliationReports",
                columns: new[] { "TenantId", "VesselId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSignOffs_TenantId_ReportId",
                schema: "integration",
                table: "ReconciliationSignOffs",
                columns: new[] { "TenantId", "ReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSignOffs_TenantId_VesselId_ReportId",
                schema: "integration",
                table: "ReconciliationSignOffs",
                columns: new[] { "TenantId", "VesselId", "ReportId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProvenance",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "ImportReconciliationReceipts",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "ReconciliationSignOffs",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "ReconciliationReports",
                schema: "integration");
        }
    }
}
