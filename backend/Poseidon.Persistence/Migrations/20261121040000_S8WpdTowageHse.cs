using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S8WpdTowageHse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TowageBriefings",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanRevision = table.Column<long>(type: "bigint", nullable: true),
                    RiskRevision = table.Column<long>(type: "bigint", nullable: true),
                    IssuedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuedAuthorityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageBriefings", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TowageBriefings_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TowageBriefing_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','ISSUED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'ISSUED' OR \"PlanRevision\" IS NOT NULL AND \"RiskRevision\" IS NOT NULL AND \"IssuedBy\" IS NOT NULL AND \"IssuedAuthorityId\" IS NOT NULL) AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"IssuedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TowageBriefings_Job",
                        columns: x => new { x.TenantId, x.VesselId, x.JobId },
                        principalSchema: "tug",
                        principalTable: "Jobs",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowagePlans",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedRevision = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowagePlans", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TowagePlans_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TowagePlan_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ApprovedRevision\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_TowagePlans_Job",
                        columns: x => new { x.TenantId, x.VesselId, x.JobId },
                        principalSchema: "tug",
                        principalTable: "Jobs",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowageReceipts",
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
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_TowageReceipt_Action", "\"Action\" IN ('TowagePlanCreate','TowagePlanUpdate','TowagePlanSubmit','TowagePlanDecide','TowagePlanAmend','TowageBriefingCreate','TowageBriefingUpdate','TowageBriefingIssue','TowageBriefingAmend','TowageBriefingAcknowledge') AND \"Revision\">0");
                    table.ForeignKey(
                        name: "FK_TowageReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowageBriefingRevisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanRevision = table.Column<long>(type: "bigint", nullable: true),
                    RiskRevision = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageBriefingRevisions", x => new { x.TenantId, x.BriefingId, x.Revision });
                    table.CheckConstraint("CK_TowageBriefingRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','ISSUED') AND \"ReasonCode\" IN ('CREATE','EDIT','ISSUE','AMEND','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_TowageBriefingRevisions_Briefing",
                        columns: x => new { x.TenantId, x.VesselId, x.BriefingId },
                        principalSchema: "tug",
                        principalTable: "TowageBriefings",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowagePlanRevisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRevision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowagePlanRevisions", x => new { x.TenantId, x.PlanId, x.Revision });
                    table.CheckConstraint("CK_TowagePlanRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"ReasonCode\" IN ('CREATE','EDIT','SUBMIT','APPROVE','RETURN','AMEND','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_TowagePlanRevisions_Plan",
                        columns: x => new { x.TenantId, x.VesselId, x.PlanId },
                        principalSchema: "tug",
                        principalTable: "TowagePlans",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowageBriefingAcknowledgements",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    BriefingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Choice = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowageBriefingAcknowledgements", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TowageBriefingAcknowledgement_Choice", "\"Choice\" IN ('ACK','DISPUTE') AND (\"Choice\"<>'DISPUTE' OR length(\"Reason\")>=10)");
                    table.ForeignKey(
                        name: "FK_TowageBriefingAcknowledgements_Revision",
                        columns: x => new { x.TenantId, x.BriefingId, x.Revision },
                        principalSchema: "tug",
                        principalTable: "TowageBriefingRevisions",
                        principalColumns: new[] { "TenantId", "BriefingId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TowagePlanDecisions",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ResultRevision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TugId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowagePlanDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_TowagePlanDecision_Duty", "\"Duty\" IN ('TugMaster','Master')");
                    table.CheckConstraint("CK_TowagePlanDecision_Kind", "\"Decision\" IN ('APPROVE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10");
                    table.ForeignKey(
                        name: "FK_TowagePlanDecisions_Revision",
                        columns: x => new { x.TenantId, x.PlanId, x.Revision },
                        principalSchema: "tug",
                        principalTable: "TowagePlanRevisions",
                        principalColumns: new[] { "TenantId", "PlanId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TowageBriefingAcknowledgements_TenantId_BriefingId_Revision~",
                schema: "tug",
                table: "TowageBriefingAcknowledgements",
                columns: new[] { "TenantId", "BriefingId", "Revision", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TowageBriefingRevisions_TenantId_VesselId_BriefingId",
                schema: "tug",
                table: "TowageBriefingRevisions",
                columns: new[] { "TenantId", "VesselId", "BriefingId" });

            migrationBuilder.CreateIndex(
                name: "IX_TowageBriefings_TenantId_VesselId_JobId",
                schema: "tug",
                table: "TowageBriefings",
                columns: new[] { "TenantId", "VesselId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TowagePlanDecisions_TenantId_PlanId_Revision",
                schema: "tug",
                table: "TowagePlanDecisions",
                columns: new[] { "TenantId", "PlanId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TowagePlanRevisions_TenantId_VesselId_PlanId",
                schema: "tug",
                table: "TowagePlanRevisions",
                columns: new[] { "TenantId", "VesselId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TowagePlans_TenantId_VesselId_JobId",
                schema: "tug",
                table: "TowagePlans",
                columns: new[] { "TenantId", "VesselId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TowageReceipts_TenantId_VesselId",
                schema: "tug",
                table: "TowageReceipts",
                columns: new[] { "TenantId", "VesselId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TowageBriefingAcknowledgements",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowagePlanDecisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowageReceipts",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowageBriefingRevisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowagePlanRevisions",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowageBriefings",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "TowagePlans",
                schema: "tug");
        }
    }
}
