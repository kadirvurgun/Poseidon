using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S7WpdPreSurveyChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreSurveyChecklists",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreSurveyChecklists", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_PreSurveyChecklists_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_PreSurveyChecklist_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_PreSurveyChecklist_Certificate",
                        columns: x => new { x.TenantId, x.VesselId, x.CertificateId },
                        principalSchema: "assurance",
                        principalTable: "Certificates",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreSurveyChecklistRevisions",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateRevision = table.Column<long>(type: "bigint", nullable: false),
                    CertificateVersion = table.Column<long>(type: "bigint", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    TemplateCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Transition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Readiness = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PlannedStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PlannedEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    PortCallId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortCallRevision = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreSurveyChecklistRevisions", x => new { x.TenantId, x.ChecklistId, x.Revision });
                    table.CheckConstraint("CK_PreSurveyRevision_Binding", "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"CertificateRevision\" > 0 AND \"CertificateVersion\" > 0 AND ((\"PortCallId\" IS NULL AND \"PortCallRevision\" IS NULL) OR (\"PortCallId\" IS NOT NULL AND \"PortCallRevision\" > 0)) AND (\"PlannedStart\" IS NULL OR \"PlannedEnd\" IS NULL OR \"PlannedEnd\" >= \"PlannedStart\")");
                    table.CheckConstraint("CK_PreSurveyRevision_State", "\"Transition\" IN ('CREATE','UPDATE','VERIFY') AND \"Readiness\" IN ('BLOCKED','INCOMPLETE','PREPARED_PENDING_HUMAN_CONFIRMATION') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");
                    table.ForeignKey(
                        name: "FK_PreSurveyRevision_Certificate",
                        columns: x => new { x.TenantId, x.VesselId, x.CertificateId, x.CertificateRevision },
                        principalSchema: "assurance",
                        principalTable: "CertificateRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "CertificateId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreSurveyRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.ChecklistId },
                        principalSchema: "assurance",
                        principalTable: "PreSurveyChecklists",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreSurveyReceipts",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreSurveyReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_PreSurveyReceipt_Action", "\"Action\" IN ('PreSurveySave','PreSurveyVerify')");
                    table.ForeignKey(
                        name: "FK_PreSurveyReceipt_Checklist",
                        columns: x => new { x.TenantId, x.VesselId, x.ChecklistId },
                        principalSchema: "assurance",
                        principalTable: "PreSurveyChecklists",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreSurveyRevision_Certificate",
                schema: "assurance",
                table: "PreSurveyChecklistRevisions",
                columns: new[] { "TenantId", "VesselId", "CertificateId", "CertificateRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_PreSurveyRevision_Head",
                schema: "assurance",
                table: "PreSurveyChecklistRevisions",
                columns: new[] { "TenantId", "VesselId", "ChecklistId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreSurveyChecklist_Certificate",
                schema: "assurance",
                table: "PreSurveyChecklists",
                columns: new[] { "TenantId", "VesselId", "CertificateId" });

            migrationBuilder.CreateIndex(
                name: "IX_PreSurveyReceipt_Checklist",
                schema: "assurance",
                table: "PreSurveyReceipts",
                columns: new[] { "TenantId", "VesselId", "ChecklistId" });

            // DEV-104: checklist history and command receipts are append-only at the database too.
            migrationBuilder.Sql("""
                CREATE TRIGGER "PreSurveyChecklistRevision_Immutable" BEFORE UPDATE OR DELETE ON assurance."PreSurveyChecklistRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "PreSurveyChecklistRevision_NoTruncate" BEFORE TRUNCATE ON assurance."PreSurveyChecklistRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "PreSurveyReceipt_Immutable" BEFORE UPDATE OR DELETE ON assurance."PreSurveyReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "PreSurveyReceipt_NoTruncate" BEFORE TRUNCATE ON assurance."PreSurveyReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreSurveyChecklistRevisions",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "PreSurveyReceipts",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "PreSurveyChecklists",
                schema: "assurance");
        }
    }
}
