using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S9WpdFieldOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrydockPackages",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActualStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActualFinish = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrydockPackages", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_DrydockPackages_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_DrydockPackage_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','PLANNED','IN_PROGRESS','COMPLETED','VERIFIED','CANCELLED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'VERIFIED' OR \"VerifiedBy\" IS NOT NULL AND \"CompletedBy\" IS NOT NULL AND \"VerifiedBy\"<>\"CompletedBy\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedBy\" IS NULL AND \"CompletedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_DrydockPackages_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldOperationReceipts",
                schema: "operations",
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
                    table.PrimaryKey("PK_FieldOperationReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_FieldOperationReceipt_Action", "\"Action\" IN ('TowageExecutionStart','TowageExecutionTransition','TowageExecutionEvent','TowageExecutionVerify','TugDailyLogCreate','TugDailyLogUpdate','TugDailyLogSubmit','TugDailyLogReview','DrydockCreate','DrydockUpdate','DrydockTransition','DrydockVerify') AND \"Revision\">0");
                    table.ForeignKey(
                        name: "FK_FieldOperationReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowageExecutions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRevision = table.Column<long>(type: "bigint", nullable: false),
                    TugId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentRevision = table.Column<long>(type: "bigint", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanRevision = table.Column<long>(type: "bigint", nullable: false),
                    BriefingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefingRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReadinessDecisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VerificationState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    LastEventHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartPosition = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EndPosition = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageExecutions", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TowageExecutions_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TowageExecution_State", "\"Revision\">0 AND \"Sequence\">0 AND \"State\" IN ('ACTIVE','SUSPENDED','COMPLETED','ABORTED') AND \"VerificationState\" IN ('PENDING','VERIFIED','REJECTED') AND \"Provenance\"='UserDeclaredUnverified' AND (\"State\" IN ('ACTIVE','SUSPENDED') OR \"EndedAt\" IS NOT NULL AND \"EndedBy\" IS NOT NULL)");
                    table.CheckConstraint("CK_TowageExecution_Verification", "\"VerificationState\"='PENDING' AND \"VerifiedBy\" IS NULL OR \"VerificationState\"<>'PENDING' AND \"State\" IN ('COMPLETED','ABORTED') AND \"VerifiedBy\" IS NOT NULL AND \"VerifiedBy\"<>\"StartedBy\" AND \"VerifiedBy\"<>\"EndedBy\"");
                    table.ForeignKey(
                        name: "FK_TowageExecutions_Job",
                        columns: x => new { x.TenantId, x.VesselId, x.JobId },
                        principalSchema: "tug",
                        principalTable: "Jobs",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TowageExecutions_Tug",
                        columns: x => new { x.TenantId, x.TugId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TugDailyLogs",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Watch = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedRevision = table.Column<long>(type: "bigint", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TugDailyLogs", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TugDailyLogs_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TugDailyLog_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','VERIFIED') AND \"Watch\" IN ('00-08','08-16','16-24','OTHER') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'VERIFIED' OR \"VerifiedRevision\"=\"Revision\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"VerifiedRevision\" IS NULL AND \"SubmittedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TugDailyLogs_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrydockPackageRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrydockPackageRevisions", x => new { x.TenantId, x.PackageId, x.Revision });
                    table.CheckConstraint("CK_DrydockPackageRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','PLANNED','IN_PROGRESS','COMPLETED','VERIFIED','CANCELLED') AND \"ReasonCode\" IN ('CREATE','EDIT','PLAN','REPLAN','START','COMPLETE','CANCEL','VERIFY','REJECT','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_DrydockPackageRevisions_Package",
                        columns: x => new { x.TenantId, x.VesselId, x.PackageId },
                        principalSchema: "technical",
                        principalTable: "DrydockPackages",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowageExecutionEvents",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PreviousHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageExecutionEvents", x => new { x.TenantId, x.ExecutionId, x.Sequence });
                    table.CheckConstraint("CK_TowageExecutionEvent_Kind", "\"Sequence\">0 AND \"Kind\" IN ('STARTED','SUSPENDED','RESUMED','ABORTED','COMPLETED','VERIFIED','VERIFICATION_REJECTED','TOWLINE_CONNECTED','TOWLINE_RELEASED','LOAD_OBSERVED','COMMS_CHECK','WEATHER_OBSERVED','WINCH_STATE','EMERGENCY_RELEASE_TEST','POSITION','EVIDENCE','NOTE') AND (\"Duty\" IS NULL OR \"Duty\" IN ('TugMaster','Master'))");
                    table.ForeignKey(
                        name: "FK_TowageExecutionEvents_Execution",
                        columns: x => new { x.TenantId, x.VesselId, x.ExecutionId },
                        principalSchema: "tug",
                        principalTable: "TowageExecutions",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TugDailyLogRevisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TugDailyLogRevisions", x => new { x.TenantId, x.LogId, x.Revision });
                    table.CheckConstraint("CK_TugDailyLogRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','VERIFIED') AND \"ReasonCode\" IN ('CREATE','EDIT','SUBMIT','VERIFY','RETURN','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_TugDailyLogRevisions_Log",
                        columns: x => new { x.TenantId, x.VesselId, x.LogId },
                        principalSchema: "tug",
                        principalTable: "TugDailyLogs",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TugDailyLogReviews",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ResultRevision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reconciliation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TugDailyLogReviews", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TugDailyLogReview_Kind", "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND \"Duty\" IN ('TugMaster','Master') AND \"Reconciliation\" IN ('RECONCILED','REVIEW_REQUIRED','INCOMPLETE')");
                    table.ForeignKey(
                        name: "FK_TugDailyLogReviews_Revision",
                        columns: x => new { x.TenantId, x.LogId, x.Revision },
                        principalSchema: "tug",
                        principalTable: "TugDailyLogRevisions",
                        principalColumns: new[] { "TenantId", "LogId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrydockPackageRevisions_TenantId_VesselId_PackageId",
                schema: "technical",
                table: "DrydockPackageRevisions",
                columns: new[] { "TenantId", "VesselId", "PackageId" });

            migrationBuilder.CreateIndex(
                name: "IX_DrydockPackages_TenantId_VesselId_SavedAt",
                schema: "technical",
                table: "DrydockPackages",
                columns: new[] { "TenantId", "VesselId", "SavedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldOperationReceipts_TenantId_VesselId",
                schema: "operations",
                table: "FieldOperationReceipts",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_TowageExecutionEvents_TenantId_VesselId_ExecutionId",
                schema: "tug",
                table: "TowageExecutionEvents",
                columns: new[] { "TenantId", "VesselId", "ExecutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TowageExecutions_TenantId_TugId_StartedAt",
                schema: "tug",
                table: "TowageExecutions",
                columns: new[] { "TenantId", "TugId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TowageExecutions_TenantId_VesselId_JobId_TugId",
                schema: "tug",
                table: "TowageExecutions",
                columns: new[] { "TenantId", "VesselId", "JobId", "TugId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TugDailyLogReviews_TenantId_LogId_Revision",
                schema: "tug",
                table: "TugDailyLogReviews",
                columns: new[] { "TenantId", "LogId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TugDailyLogRevisions_TenantId_VesselId_LogId",
                schema: "tug",
                table: "TugDailyLogRevisions",
                columns: new[] { "TenantId", "VesselId", "LogId" });

            migrationBuilder.CreateIndex(
                name: "IX_TugDailyLogs_TenantId_VesselId_LogDate_Watch",
                schema: "tug",
                table: "TugDailyLogs",
                columns: new[] { "TenantId", "VesselId", "LogDate", "Watch" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrydockPackageRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "FieldOperationReceipts",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "TowageExecutionEvents",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TugDailyLogReviews",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "DrydockPackages",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "TowageExecutions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TugDailyLogRevisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TugDailyLogs",
                schema: "tug");
        }
    }
}
