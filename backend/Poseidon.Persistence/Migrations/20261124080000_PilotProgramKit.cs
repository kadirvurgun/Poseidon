using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PilotProgramKit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PilotProgramReceipts",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotProgramReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_PilotProgramReceipt_Action", "\"Action\" IN ('PilotProgramCreate', 'PilotChecklistLink', 'PilotChecklistWithdraw', 'PilotFeedbackRecord', 'PilotIssueReport', 'PilotIssueTriage', 'PilotIssueResolve', 'PilotMeasurementRecord', 'PilotWeekClose', 'PilotProgramDecide')");
                });

            migrationBuilder.CreateTable(
                name: "PilotPrograms",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ShipVesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    TugVesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CatalogVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotPrograms", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotProgram_Name", "char_length(\"Name\") >= 3");
                    table.CheckConstraint("CK_PilotProgram_Provenance", "\"Provenance\" IN ('UserDeclaredUnverified', 'SyntheticUnverified')");
                    table.CheckConstraint("CK_PilotProgram_Vessels", "\"TugVesselId\" IS NULL OR \"TugVesselId\" <> \"ShipVesselId\"");
                    table.ForeignKey(
                        name: "FK_PilotPrograms_ShipVessel",
                        columns: x => new { x.TenantId, x.ShipVesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PilotPrograms_TugVessel",
                        columns: x => new { x.TenantId, x.TugVesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotChecklistLinks",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordVesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditTracedAtLink = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WithdrawnBy = table.Column<Guid>(type: "uuid", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotChecklistLinks", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotChecklistLink_Values", "\"Week\" IN ('W1', 'W2', 'W3', 'W4') AND \"ItemCode\" IN ('W1_DAILY_FEEDBACK', 'W1_FIRST_LIVE_TRACE', 'W1_SHIP_CERTIFICATE', 'W1_SHIP_CREW', 'W1_TUG_JOB_INTAKE', 'W2_WORK_ORDER', 'W2_DEFECT_SAFEGUARD', 'W2_DEPARTURE_REVIEW', 'W2_OFFLINE_RECONNECT', 'W2_DUAL_SHIP_SAFETY', 'W2_DUAL_TUG_JOB', 'W2_DUAL_TUG_TOWAGE', 'W2_DUAL_SHARED_CREW_TUG', 'W2_DUAL_SHARED_PMS_TUG', 'W2_DUAL_SHARED_EVIDENCE', 'W2_DUAL_TUG_OFFLINE', 'W3_EVENT_FLOW', 'W3_AUDIT_PACK_RETRIEVAL', 'W3_VOICE_OCR_CORRECTION', 'W3_NO_AI_AUTHORITY_BYPASS', 'W3_TUG_TOWAGE_PLAN', 'W3_TUG_BRIEFING', 'W4_BEFORE_AFTER_BASELINE', 'W4_SHIP_UAT_SIGNOFF', 'W4_TUG_UAT_SIGNOFF', 'W4_TUG_DAILY_LOG') AND \"RecordType\" IN ('WORK_ORDER', 'DEFECT', 'DEPARTURE_DECISION', 'OFFLINE_OUTCOME', 'WORK_ORDER_OFFLINE_OUTCOME', 'DAILY_OFFLINE_OUTCOME', 'SAFETY_EVENT_OFFLINE_OUTCOME', 'TUG_OFFLINE_OUTCOME', 'SAFETY_EVENT', 'RISK_ASSESSMENT', 'AUDIT_PACK', 'TOWAGE_EXECUTION', 'TOWAGE_PLAN', 'TOWAGE_BRIEFING', 'TUG_JOB', 'TUG_DAILY_LOG', 'VESSEL_CERTIFICATE', 'CREW_ASSIGNMENT', 'CONTROL_EVIDENCE', 'VOICE_EVENT_DRAFT', 'UAT_RUN', 'PILOT_FEEDBACK', 'PILOT_MEASUREMENT')");
                    table.CheckConstraint("CK_PilotChecklistLink_Withdrawal", "(\"WithdrawnAt\" IS NULL) = (\"WithdrawnBy\" IS NULL) AND (\"WithdrawnAt\" IS NULL) = (\"WithdrawalReason\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PilotChecklistLinks_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotFeedback",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Area = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sentiment = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParticipantPseudonym = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionPolicyRef = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotFeedback", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotFeedback_Retention", "\"ExpiresAt\" > \"RecordedAt\"");
                    table.CheckConstraint("CK_PilotFeedback_Values", "\"Week\" IN ('W1', 'W2', 'W3', 'W4') AND \"Area\" IN ('SHIP', 'TUG', 'SHARED') AND \"Category\" IN ('USABILITY', 'DATA_QUALITY', 'PERFORMANCE', 'OFFLINE', 'AI_ASSIST', 'TRAINING', 'SAFETY_CONCERN', 'OTHER') AND \"Sentiment\" IN ('POSITIVE', 'NEUTRAL', 'NEGATIVE') AND \"Role\" IN ('MASTER', 'DPA', 'CHIEF_ENGINEER', 'CREW', 'TUG_MASTER', 'TUG_OPERATIONS_RESPONSIBLE', 'TUG_CREW', 'SHORE_OFFICE', 'OTHER')");
                    table.ForeignKey(
                        name: "FK_PilotFeedback_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotIssues",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Area = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Severity = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TriagedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TriagedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TriageNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolutionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotIssues", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotIssue_Resolution", "(\"State\" = 'RESOLVED') = (\"ResolvedAt\" IS NOT NULL AND \"ResolvedBy\" IS NOT NULL AND \"Resolution\" IS NOT NULL) AND (\"State\" = 'RESOLVED' OR \"ResolutionReference\" IS NULL) AND (\"ResolvedAt\" IS NULL OR \"ResolvedAt\" >= \"TriagedAt\")");
                    table.CheckConstraint("CK_PilotIssue_Triage", "(\"State\" = 'OPEN') = (\"TriagedAt\" IS NULL) AND (\"TriagedAt\" IS NULL) = (\"TriagedBy\" IS NULL) AND (\"TriagedAt\" IS NULL) = (\"TriageNote\" IS NULL) AND (\"TriagedAt\" IS NULL OR \"TriagedAt\" >= \"ReportedAt\")");
                    table.CheckConstraint("CK_PilotIssue_Values", "\"Week\" IN ('W1', 'W2', 'W3', 'W4') AND \"Area\" IN ('SHIP', 'TUG', 'SHARED') AND \"Category\" IN ('ONBOARDING', 'CERTIFICATE', 'CREW', 'PMS', 'DEFECT', 'DEPARTURE', 'OFFLINE', 'SAFETY_EVENT', 'EVIDENCE', 'AUDIT', 'AI_ASSIST', 'TUG_JOB', 'TOWAGE', 'UAT', 'VALUE', 'OTHER') AND \"Severity\" IN ('P0', 'P1', 'P2') AND \"State\" IN ('OPEN', 'TRIAGED', 'RESOLVED')");
                    table.ForeignKey(
                        name: "FK_PilotIssues_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotMeasurements",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricId = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Mode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Phase = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousMeasurementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Method = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotMeasurements", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotMeasurement_Evidence", "char_length(\"EvidenceReference\") >= 1 AND char_length(\"Method\") >= 10");
                    table.CheckConstraint("CK_PilotMeasurement_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousMeasurementId\" IS NULL)");
                    table.CheckConstraint("CK_PilotMeasurement_Range", "\"Value\" >= 0 AND (\"Unit\" <> 'PERCENT' OR \"Value\" <= 100) AND \"SampleSize\" >= 1 AND \"PeriodTo\" >= \"PeriodFrom\"");
                    table.CheckConstraint("CK_PilotMeasurement_Values", "\"MetricId\" IN ('SHIP_TIME_TO_COMPLETE', 'SHIP_DUPLICATE_ENTRY', 'SHIP_EVIDENCE_COMPLETENESS', 'SHIP_READINESS_ACCURACY', 'SHIP_OFFLINE_SUCCESS', 'SHIP_USER_ADOPTION', 'SHIP_AI_CORRECTION', 'TUG_TIME_TO_COMPLETE', 'TUG_JOB_PREP_TIME', 'TUG_ASSIGNMENT_CONFLICT_VISIBILITY', 'TUG_EVIDENCE_COMPLETENESS', 'TUG_READINESS_ACCURACY', 'TUG_OFFLINE_SUCCESS', 'TUG_USER_ADOPTION', 'TUG_AI_CORRECTION') AND \"Mode\" IN ('SHIP', 'TUG') AND \"Phase\" IN ('BASELINE', 'PILOT') AND \"Unit\" IN ('SECOND', 'COUNT', 'PERCENT', 'PERSON')");
                    table.ForeignKey(
                        name: "FK_PilotMeasurements_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotProgramDecisions",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProgramHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotProgramDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotProgramDecision_Duty", "\"Duty\" = 'ProductOwner'");
                    table.CheckConstraint("CK_PilotProgramDecision_Expand", "\"Decision\" <> 'EXPAND' OR \"Recommendation\" = 'EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION'");
                    table.CheckConstraint("CK_PilotProgramDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                    table.CheckConstraint("CK_PilotProgramDecision_Rationale", "char_length(\"Rationale\") >= 10");
                    table.CheckConstraint("CK_PilotProgramDecision_Values", "\"Decision\" IN ('CONTINUE', 'EXPAND', 'FIX', 'STOP') AND \"Recommendation\" IN ('EXPAND_ELIGIBLE_PENDING_HUMAN_DECISION', 'FIX_RECOMMENDED')");
                    table.ForeignKey(
                        name: "FK_PilotProgramDecisions_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PilotWeekCloses",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Week = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCloseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WeekHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotWeekCloses", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotWeekClose_Accepted", "\"Outcome\" <> 'ACCEPTED' OR \"Recommendation\" = 'READY_FOR_HUMAN_CLOSE'");
                    table.CheckConstraint("CK_PilotWeekClose_Duty", "\"Duty\" = 'ProductOwner'");
                    table.CheckConstraint("CK_PilotWeekClose_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousCloseId\" IS NULL)");
                    table.CheckConstraint("CK_PilotWeekClose_Rationale", "char_length(\"Rationale\") >= 10");
                    table.CheckConstraint("CK_PilotWeekClose_Values", "\"Week\" IN ('W1', 'W2', 'W3', 'W4') AND \"Outcome\" IN ('ACCEPTED', 'NOT_ACCEPTED') AND \"Recommendation\" IN ('READY_FOR_HUMAN_CLOSE', 'NOT_READY')");
                    table.ForeignKey(
                        name: "FK_PilotWeekCloses_PilotPrograms_TenantId_ProgramId",
                        columns: x => new { x.TenantId, x.ProgramId },
                        principalSchema: "pilot",
                        principalTable: "PilotPrograms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotChecklistLinks_TenantId_ProgramId_ItemCode_RecordType_~",
                schema: "pilot",
                table: "PilotChecklistLinks",
                columns: new[] { "TenantId", "ProgramId", "ItemCode", "RecordType", "RecordId" },
                unique: true,
                filter: "\"WithdrawnAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PilotFeedback_TenantId_ExpiresAt",
                schema: "pilot",
                table: "PilotFeedback",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotFeedback_TenantId_ProgramId_RecordedAt",
                schema: "pilot",
                table: "PilotFeedback",
                columns: new[] { "TenantId", "ProgramId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotIssues_TenantId_ProgramId_Week_State",
                schema: "pilot",
                table: "PilotIssues",
                columns: new[] { "TenantId", "ProgramId", "Week", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotMeasurements_TenantId_ProgramId_MetricId_Mode_Phase_Se~",
                schema: "pilot",
                table: "PilotMeasurements",
                columns: new[] { "TenantId", "ProgramId", "MetricId", "Mode", "Phase", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PilotProgramDecisions_TenantId_ProgramId_Sequence",
                schema: "pilot",
                table: "PilotProgramDecisions",
                columns: new[] { "TenantId", "ProgramId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PilotPrograms_TenantId_CreatedAt",
                schema: "pilot",
                table: "PilotPrograms",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotPrograms_TenantId_ShipVesselId",
                schema: "pilot",
                table: "PilotPrograms",
                columns: new[] { "TenantId", "ShipVesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotPrograms_TenantId_TugVesselId",
                schema: "pilot",
                table: "PilotPrograms",
                columns: new[] { "TenantId", "TugVesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_PilotWeekCloses_TenantId_ProgramId_Week_Sequence",
                schema: "pilot",
                table: "PilotWeekCloses",
                columns: new[] { "TenantId", "ProgramId", "Week", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotChecklistLinks",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotFeedback",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotIssues",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotMeasurements",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotProgramDecisions",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotProgramReceipts",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotWeekCloses",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotPrograms",
                schema: "pilot");
        }
    }
}
