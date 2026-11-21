using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S11WpdUatGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pilot");

            migrationBuilder.CreateTable(
                name: "PilotGateDecisions",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReviewHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ConditionsJson = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_PilotGateDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotGateDecision_Decision", "\"Decision\" IN ('GO', 'NO_GO', 'CONDITIONAL_GO') AND \"Recommendation\" IN ('GO_ELIGIBLE_PENDING_HUMAN_DECISION', 'NO_GO_RECOMMENDED')");
                    table.CheckConstraint("CK_PilotGateDecision_Duty", "\"Duty\" = 'ProductOwner'");
                    table.CheckConstraint("CK_PilotGateDecision_Go", "\"Decision\" <> 'GO' OR \"Recommendation\" = 'GO_ELIGIBLE_PENDING_HUMAN_DECISION'");
                    table.CheckConstraint("CK_PilotGateDecision_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousDecisionId\" IS NULL)");
                    table.CheckConstraint("CK_PilotGateDecision_Rationale", "char_length(\"Rationale\") >= 10");
                });

            migrationBuilder.CreateTable(
                name: "PilotReceipts",
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
                    table.PrimaryKey("PK_PilotReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_PilotReceipt_Action", "\"Action\" IN ('UatThresholdRecord', 'UatRunCreate', 'UatResultRecord', 'UatFindingRecord', 'UatFindingResolve', 'UatRunSubmit', 'UatRunAttest', 'PilotSupportCheckRecord', 'PilotGateDecide')");
                });

            migrationBuilder.CreateTable(
                name: "PilotSupportChecks",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Item = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousCheckId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PilotSupportChecks", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_PilotSupportCheck_Evidence", "\"State\" <> 'READY' OR \"EvidenceReference\" IS NOT NULL");
                    table.CheckConstraint("CK_PilotSupportCheck_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousCheckId\" IS NULL)");
                    table.CheckConstraint("CK_PilotSupportCheck_Values", "\"Item\" IN ('SUPPORT_CONTACTS', 'ONCALL_ROTA', 'ESCALATION_PATH', 'ROLLBACK_OWNER', 'INCIDENT_COMMUNICATION', 'MONITORING_ALERTING') AND \"State\" IN ('READY', 'NOT_READY')");
                });

            migrationBuilder.CreateTable(
                name: "UatRuns",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuildCommit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParticipantPseudonym = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CatalogVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    FacilitatorActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttestedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AttestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttestationStatement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatRuns", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_UatRun_Attestation", "(\"State\" = 'ATTESTED') = (\"AttestedBy\" IS NOT NULL AND \"AttestedAt\" IS NOT NULL AND \"AttestationStatement\" IS NOT NULL) AND (\"AttestedBy\" IS NULL OR \"AttestedBy\" <> \"FacilitatorActorId\")");
                    table.CheckConstraint("CK_UatRun_ScopeRole", "\"Scope\" = 'SHIP' AND \"Role\" IN ('MASTER', 'DPA', 'CHIEF_ENGINEER', 'CREW') OR \"Scope\" = 'TUG' AND \"Role\" IN ('TUG_MASTER', 'TUG_OPERATIONS_RESPONSIBLE', 'TUG_CREW')");
                    table.CheckConstraint("CK_UatRun_State", "\"State\" IN ('OPEN', 'SUBMITTED', 'ATTESTED') AND \"Environment\" IN ('UAT', 'PILOT_STAGING', 'LAB') AND \"Provenance\" IN ('UserDeclaredUnverified', 'SyntheticUnverified')");
                    table.CheckConstraint("CK_UatRun_Submitted", "(\"State\" = 'OPEN') = (\"SubmittedAt\" IS NULL)");
                    table.CheckConstraint("CK_UatRun_Synthetic", "\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN'");
                });

            migrationBuilder.CreateTable(
                name: "UatThresholds",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    PreviousThresholdId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinScenarioPassPercent = table.Column<int>(type: "integer", nullable: false),
                    MaxOpenMajorFindings = table.Column<int>(type: "integer", nullable: false),
                    RequireDesignConformance = table.Column<bool>(type: "boolean", nullable: false),
                    AgreementReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatThresholds", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_UatThreshold_Lineage", "\"Sequence\" >= 1 AND (\"Sequence\" = 1) = (\"PreviousThresholdId\" IS NULL)");
                    table.CheckConstraint("CK_UatThreshold_Rationale", "char_length(\"Rationale\") >= 10");
                    table.CheckConstraint("CK_UatThreshold_Scope", "\"Scope\" IN ('SHIP', 'TUG')");
                    table.CheckConstraint("CK_UatThreshold_Values", "\"MinScenarioPassPercent\" BETWEEN 1 AND 100 AND \"MaxOpenMajorFindings\" BETWEEN 0 AND 1000");
                });

            migrationBuilder.CreateTable(
                name: "UatFindings",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsUx = table.Column<bool>(type: "boolean", nullable: false),
                    DesignConformance = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolutionReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatFindings", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_UatFinding_Blocker", "\"Severity\" <> 'BLOCKER' OR \"State\" <> 'ACCEPTED_RISK'");
                    table.CheckConstraint("CK_UatFinding_Resolution", "(\"State\" = 'OPEN') = (\"ResolvedBy\" IS NULL AND \"ResolvedAt\" IS NULL AND \"Resolution\" IS NULL) AND (\"ResolvedBy\" IS NULL OR \"ResolvedBy\" <> \"ReportedBy\")");
                    table.CheckConstraint("CK_UatFinding_Values", "\"Severity\" IN ('BLOCKER', 'MAJOR', 'MINOR') AND \"State\" IN ('OPEN', 'RESOLVED', 'ACCEPTED_RISK') AND \"Scope\" IN ('SHIP', 'TUG')");
                    table.ForeignKey(
                        name: "FK_UatFindings_UatRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "pilot",
                        principalTable: "UatRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UatStepResults",
                schema: "pilot",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    Assisted = table.Column<bool>(type: "boolean", nullable: false),
                    AutomatedEvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UatStepResults", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_UatStepResult_Duration", "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" BETWEEN 0 AND 86400");
                    table.CheckConstraint("CK_UatStepResult_Outcome", "\"Outcome\" IN ('PASS', 'FAIL', 'BLOCKED', 'NOT_RUN') AND \"Attempt\" >= 1");
                    table.ForeignKey(
                        name: "FK_UatStepResults_UatRuns_TenantId_RunId",
                        columns: x => new { x.TenantId, x.RunId },
                        principalSchema: "pilot",
                        principalTable: "UatRuns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PilotGateDecisions_TenantId_Sequence",
                schema: "pilot",
                table: "PilotGateDecisions",
                columns: new[] { "TenantId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PilotSupportChecks_TenantId_Item_Sequence",
                schema: "pilot",
                table: "PilotSupportChecks",
                columns: new[] { "TenantId", "Item", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UatFindings_TenantId_RunId",
                schema: "pilot",
                table: "UatFindings",
                columns: new[] { "TenantId", "RunId" });

            migrationBuilder.CreateIndex(
                name: "IX_UatFindings_TenantId_Scope_State",
                schema: "pilot",
                table: "UatFindings",
                columns: new[] { "TenantId", "Scope", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_UatRuns_TenantId_Scope_CreatedAt",
                schema: "pilot",
                table: "UatRuns",
                columns: new[] { "TenantId", "Scope", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UatStepResults_TenantId_RunId_ScenarioId_Attempt",
                schema: "pilot",
                table: "UatStepResults",
                columns: new[] { "TenantId", "RunId", "ScenarioId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UatThresholds_TenantId_Scope_Sequence",
                schema: "pilot",
                table: "UatThresholds",
                columns: new[] { "TenantId", "Scope", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PilotGateDecisions",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotReceipts",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "PilotSupportChecks",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "UatFindings",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "UatStepResults",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "UatThresholds",
                schema: "pilot");

            migrationBuilder.DropTable(
                name: "UatRuns",
                schema: "pilot");
        }
    }
}
