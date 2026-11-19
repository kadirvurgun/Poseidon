using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S5WpaApplicabilityOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicabilityOverrides",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleRevision = table.Column<long>(type: "bigint", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequirementRevision = table.Column<long>(type: "bigint", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: true),
                    BaseResult = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RuleSetHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EvidenceIdsJson = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorRoleContext = table.Column<string>(type: "text", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorityGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicabilityOverrides", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_ApplicabilityOverrides", "\"State\" IN ('DRAFT','IN_REVIEW','ACTIVE','REVOKED','EXPIRED','SUPERSEDED') AND \"Sequence\" > 0 AND \"RuleRevision\" > 0 AND \"Purpose\" = 'COMPLIANCE_REVIEW' AND \"Action\" IN ('ACCEPT_INTERPRETATION','REQUIRE_ADDITIONAL_CONTROL') AND ((\"Action\" = 'ACCEPT_INTERPRETATION' AND \"ReasonCode\" IN ('VERIFIED_LEGAL_INTERPRETATION','FLAG_STATE_EXEMPTION_REFERENCE','CLASS_EXEMPTION_REFERENCE')) OR (\"Action\" = 'REQUIRE_ADDITIONAL_CONTROL' AND \"ReasonCode\" IN ('ADDITIONAL_PRECAUTION','SOURCE_CONFLICT_PRECAUTION'))) AND \"ExpiresAt\" > \"ValidFrom\" AND \"ReviewAt\" <= \"ExpiresAt\" AND char_length(\"Rationale\") BETWEEN 20 AND 4000 AND (\"State\" <> 'ACTIVE' OR (\"ApproverId\" IS NOT NULL AND \"AuthorityGrantId\" IS NOT NULL AND \"ApproverId\" <> \"AuthorId\")) AND (\"ApproverId\" IS NULL OR \"ApproverId\" <> \"AuthorId\")");
                    table.ForeignKey(
                        name: "FK_ApplicabilityOverrides_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceCommandReceipts",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCommandReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_ComplianceCommandReceipts", "\"Operation\" IN ('ApplicabilityOverridePropose','ApplicabilityOverrideDecision','ComplianceControlCandidates')");
                    table.ForeignKey(
                        name: "FK_ComplianceCommandReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicabilityOverrideEvents",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    FromState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ToState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EventCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleContext = table.Column<string>(type: "text", nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicabilityOverrideEvents", x => new { x.TenantId, x.VesselId, x.OverrideId, x.Sequence });
                    table.CheckConstraint("CK_ApplicabilityOverrideEvents", "\"Sequence\" > 0 AND \"ToState\" IN ('DRAFT','IN_REVIEW','ACTIVE','REVOKED','EXPIRED','SUPERSEDED') AND ((\"EventCode\" = 'PROPOSE' AND \"FromState\" IS NULL AND \"ToState\" = 'IN_REVIEW') OR (\"EventCode\" = 'APPROVE' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'ACTIVE') OR (\"EventCode\" = 'REJECT' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'REVOKED') OR (\"EventCode\" = 'WITHDRAW' AND \"FromState\" IN ('DRAFT','IN_REVIEW') AND \"ToState\" = 'REVOKED') OR (\"EventCode\" = 'REVOKE' AND \"FromState\" = 'ACTIVE' AND \"ToState\" = 'REVOKED'))");
                    table.ForeignKey(
                        name: "FK_ApplicabilityOverrideEvents_ApplicabilityOverrides_TenantId~",
                        columns: x => new { x.TenantId, x.VesselId, x.OverrideId },
                        principalSchema: "core",
                        principalTable: "ApplicabilityOverrides",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicabilityOverrides_TenantId_VesselId_RuleId_Purpose_Sta~",
                schema: "core",
                table: "ApplicabilityOverrides",
                columns: new[] { "TenantId", "VesselId", "RuleId", "Purpose", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCommandReceipts_TenantId_VesselId",
                schema: "core",
                table: "ComplianceCommandReceipts",
                columns: new[] { "TenantId", "VesselId" });

            // DEV-040: override history and command receipts are append-only at the database layer too.
            // Schema-only migration: no override, approval, grant or receipt is created here.
            migrationBuilder.Sql("""CREATE TRIGGER applicability_override_events_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ApplicabilityOverrideEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
            migrationBuilder.Sql("""CREATE TRIGGER compliance_command_receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ComplianceCommandReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicabilityOverrideEvents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ComplianceCommandReceipts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ApplicabilityOverrides",
                schema: "core");
        }
    }
}
