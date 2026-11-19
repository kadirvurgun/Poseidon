using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S6WpaRenewalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CertificateReceipt_Resource",
                schema: "assurance",
                table: "CertificateReceipts");

            migrationBuilder.AddColumn<string>(
                name: "DecisionJson",
                schema: "assurance",
                table: "RenewalRevisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "assurance",
                table: "RenewalRevisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "DRAFT");

            migrationBuilder.AddColumn<string>(
                name: "Transition",
                schema: "assurance",
                table: "RenewalRevisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificateSupersessions",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateRevision = table.Column<long>(type: "bigint", nullable: false),
                    ReplacementCertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplacementRevision = table.Column<long>(type: "bigint", nullable: false),
                    RenewalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalRevision = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateSupersessions", x => new { x.TenantId, x.CertificateId });
                    table.CheckConstraint("CK_CertificateSupersession_Binding", "\"CertificateRevision\" > 0 AND \"ReplacementRevision\" > 0 AND \"RenewalRevision\" > 0 AND \"CertificateId\" <> \"ReplacementCertificateId\"");
                    table.ForeignKey(
                        name: "FK_CertificateSupersession_Original",
                        columns: x => new { x.TenantId, x.VesselId, x.CertificateId, x.CertificateRevision },
                        principalSchema: "assurance",
                        principalTable: "CertificateRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "CertificateId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateSupersession_Renewal",
                        columns: x => new { x.TenantId, x.RenewalId, x.RenewalRevision },
                        principalSchema: "assurance",
                        principalTable: "RenewalRevisions",
                        principalColumns: new[] { "TenantId", "RenewalId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateSupersession_Replacement",
                        columns: x => new { x.TenantId, x.VesselId, x.ReplacementCertificateId, x.ReplacementRevision },
                        principalSchema: "assurance",
                        principalTable: "CertificateRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "CertificateId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RenewalRevision_State",
                schema: "assurance",
                table: "RenewalRevisions",
                sql: "\"State\" IN ('DRAFT','PREPARING','READY_TO_APPLY','APPLICATION_RECORDED','AWAITING_AUTHORITY','ISSUED_UNVERIFIED','VERIFIED','COMPLETED','CANCELLED','REJECTED') AND (\"Transition\" IS NULL OR \"Transition\" IN ('OPEN','VALIDATE','RECORD_APPLICATION','AWAIT_AUTHORITY','RECORD_ISSUED_CANDIDATE','VERIFY','COMPLETE','REJECT','CANCEL'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CertificateReceipt_Resource",
                schema: "assurance",
                table: "CertificateReceipts",
                sql: "(\"Action\" IN ('CertificateSave','CertificateAssertion') AND \"RenewalId\" IS NULL AND \"ResourceId\"=\"CertificateId\") OR (\"Action\" IN ('RenewalSave','RenewalTransition') AND \"RenewalId\" IS NOT NULL AND \"ResourceId\"=\"RenewalId\")");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSupersession_Original",
                schema: "assurance",
                table: "CertificateSupersessions",
                columns: new[] { "TenantId", "VesselId", "CertificateId", "CertificateRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSupersession_Renewal",
                schema: "assurance",
                table: "CertificateSupersessions",
                columns: new[] { "TenantId", "RenewalId", "RenewalRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSupersession_Replacement",
                schema: "assurance",
                table: "CertificateSupersessions",
                columns: new[] { "TenantId", "ReplacementCertificateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateSupersession_ReplacementRevision",
                schema: "assurance",
                table: "CertificateSupersessions",
                columns: new[] { "TenantId", "VesselId", "ReplacementCertificateId", "ReplacementRevision" });

            // Supersession is immutable history, enforced in the database as well as the context.
            migrationBuilder.Sql("""
                CREATE TRIGGER "CertificateSupersession_Immutable" BEFORE UPDATE OR DELETE ON assurance."CertificateSupersessions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "CertificateSupersession_NoTruncate" BEFORE TRUNCATE ON assurance."CertificateSupersessions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateSupersessions",
                schema: "assurance");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RenewalRevision_State",
                schema: "assurance",
                table: "RenewalRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CertificateReceipt_Resource",
                schema: "assurance",
                table: "CertificateReceipts");

            migrationBuilder.DropColumn(
                name: "DecisionJson",
                schema: "assurance",
                table: "RenewalRevisions");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "assurance",
                table: "RenewalRevisions");

            migrationBuilder.DropColumn(
                name: "Transition",
                schema: "assurance",
                table: "RenewalRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CertificateReceipt_Resource",
                schema: "assurance",
                table: "CertificateReceipts",
                sql: "(\"Action\" IN ('CertificateSave','CertificateAssertion') AND \"RenewalId\" IS NULL AND \"ResourceId\"=\"CertificateId\") OR (\"Action\"='RenewalSave' AND \"RenewalId\" IS NOT NULL AND \"ResourceId\"=\"RenewalId\")");
        }
    }
}
