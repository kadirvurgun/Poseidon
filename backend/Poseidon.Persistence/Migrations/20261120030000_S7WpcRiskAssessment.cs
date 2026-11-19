using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S7WpcRiskAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hse");

            migrationBuilder.CreateTable(
                name: "RiskAssessments",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    LinksJson = table.Column<string>(type: "text", nullable: false),
                    TemplateJson = table.Column<string>(type: "text", nullable: true),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedRevision = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_RiskAssessments_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_RiskAssessment_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\") AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ApprovedRevision\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_RiskAssessments_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskCommandReceipts",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskCommandReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_RiskCommandReceipt_Revision", "\"Revision\">0");
                });

            migrationBuilder.CreateTable(
                name: "RiskMethods",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskMethods", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskMethod_Status", "\"Revision\">0 AND \"Status\" IN ('DRAFT','PUBLISHED') AND (\"Status\"<>'PUBLISHED' OR \"PublishedBy\" IS NOT NULL AND \"PublishedBy\"<>\"ActorId\")");
                    table.ForeignKey(
                        name: "FK_RiskMethods_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTemplates",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LatestRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTemplates", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskTemplate_Revision", "\"LatestRevision\">0 AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");
                    table.ForeignKey(
                        name: "FK_RiskTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessmentRevisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    LinksJson = table.Column<string>(type: "text", nullable: false),
                    TemplateJson = table.Column<string>(type: "text", nullable: true),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessmentRevisions", x => new { x.TenantId, x.AssessmentId, x.Revision });
                    table.CheckConstraint("CK_RiskAssessmentRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','IN_REVIEW','RETURNED','APPROVED') AND \"ReasonCode\" IN ('CREATE','TEMPLATE_COPY','EDIT','SUBMIT','APPROVE','RETURN','AMEND','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_RiskAssessmentRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.AssessmentId },
                        principalSchema: "hse",
                        principalTable: "RiskAssessments",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTemplateRevisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersededByRevision = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTemplateRevisions", x => new { x.TenantId, x.TemplateId, x.Revision });
                    table.CheckConstraint("CK_RiskTemplateRevision_Status", "\"Revision\">0 AND \"Status\" IN ('DRAFT','IN_REVIEW','RETURNED','PUBLISHED','SUPERSEDED') AND (\"Status\" NOT IN ('PUBLISHED','SUPERSEDED') OR \"PublishedBy\" IS NOT NULL AND \"EffectiveFrom\" IS NOT NULL) AND (\"Provenance\"<>'SyntheticUnverified' OR \"Status\"='DRAFT' AND \"PublishedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_RiskTemplateRevision_Template",
                        columns: x => new { x.TenantId, x.TemplateId },
                        principalSchema: "hse",
                        principalTable: "RiskTemplates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessmentAcknowledgements",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Choice = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAssessmentAcknowledgements", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskAssessmentAcknowledgement_Choice", "\"Choice\" IN ('ACK','DISPUTE') AND (\"Choice\"<>'DISPUTE' OR length(\"Reason\")>=10)");
                    table.ForeignKey(
                        name: "FK_RiskAssessmentAcknowledgement_Revision",
                        columns: x => new { x.TenantId, x.AssessmentId, x.Revision },
                        principalSchema: "hse",
                        principalTable: "RiskAssessmentRevisions",
                        principalColumns: new[] { "TenantId", "AssessmentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskAssessmentDecisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_RiskAssessmentDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskAssessmentDecision_Kind", "\"Decision\" IN ('APPROVE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10");
                    table.ForeignKey(
                        name: "FK_RiskAssessmentDecision_Revision",
                        columns: x => new { x.TenantId, x.AssessmentId, x.Revision },
                        principalSchema: "hse",
                        principalTable: "RiskAssessmentRevisions",
                        principalColumns: new[] { "TenantId", "AssessmentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTemplateDecisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTemplateDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskTemplateDecision_Kind", "\"Decision\" IN ('PUBLISH','RETURN') AND length(\"Reason\")>=10 AND (\"Decision\"<>'PUBLISH' OR \"EffectiveFrom\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskTemplateDecision_Revision",
                        columns: x => new { x.TenantId, x.TemplateId, x.Revision },
                        principalSchema: "hse",
                        principalTable: "RiskTemplateRevisions",
                        principalColumns: new[] { "TenantId", "TemplateId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskTemplateSuggestions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTemplateSuggestions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RiskTemplateSuggestion_Status", "\"Status\" IN ('OPEN','ACCEPTED','REJECTED') AND (\"Status\"='OPEN' OR \"ReviewedBy\" IS NOT NULL AND length(\"ReviewReason\")>=10)");
                    table.ForeignKey(
                        name: "FK_RiskTemplateSuggestion_Revision",
                        columns: x => new { x.TenantId, x.TemplateId, x.Revision },
                        principalSchema: "hse",
                        principalTable: "RiskTemplateRevisions",
                        principalColumns: new[] { "TenantId", "TemplateId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentAcknowledgements_TenantId_AssessmentId_Revisi~",
                schema: "hse",
                table: "RiskAssessmentAcknowledgements",
                columns: new[] { "TenantId", "AssessmentId", "Revision", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentDecisions_TenantId_AssessmentId_Revision",
                schema: "hse",
                table: "RiskAssessmentDecisions",
                columns: new[] { "TenantId", "AssessmentId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessmentRevisions_TenantId_VesselId_AssessmentId",
                schema: "hse",
                table: "RiskAssessmentRevisions",
                columns: new[] { "TenantId", "VesselId", "AssessmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_TenantId_AssessmentNo",
                schema: "hse",
                table: "RiskAssessments",
                columns: new[] { "TenantId", "AssessmentNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAssessments_TenantId_VesselId_SavedAt_Id",
                schema: "hse",
                table: "RiskAssessments",
                columns: new[] { "TenantId", "VesselId", "SavedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskMethods_TenantId_Code_Revision",
                schema: "hse",
                table: "RiskMethods",
                columns: new[] { "TenantId", "Code", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskTemplateDecisions_TenantId_TemplateId_Revision_DecidedAt",
                schema: "hse",
                table: "RiskTemplateDecisions",
                columns: new[] { "TenantId", "TemplateId", "Revision", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTemplates_TenantId_TemplateCode",
                schema: "hse",
                table: "RiskTemplates",
                columns: new[] { "TenantId", "TemplateCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskTemplateSuggestions_TenantId_TemplateId_Revision",
                schema: "hse",
                table: "RiskTemplateSuggestions",
                columns: new[] { "TenantId", "TemplateId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskTemplateSuggestions_TenantId_TemplateId_Status",
                schema: "hse",
                table: "RiskTemplateSuggestions",
                columns: new[] { "TenantId", "TemplateId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiskAssessmentAcknowledgements",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskAssessmentDecisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskCommandReceipts",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskMethods",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskTemplateDecisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskTemplateSuggestions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskAssessmentRevisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskTemplateRevisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskAssessments",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "RiskTemplates",
                schema: "hse");
        }
    }
}
