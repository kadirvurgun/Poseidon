using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S3WpbControlLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControlState",
                schema: "core",
                table: "ControlEvidenceHeads",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            // DEV-028 schema-only change: no existing row is rewritten. A control head with a NULL
            // state has no lifecycle event and derives DRAFT (ControlLifecyclePolicy.State), so no
            // unaudited business-state mutation runs outside the audited SaveChanges path.

            migrationBuilder.AddColumn<long>(
                name: "PublishedRevision",
                schema: "core",
                table: "ControlEvidenceHeads",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ControlStateEvents",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ControlKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ControlRevision = table.Column<long>(type: "bigint", nullable: false),
                    FromState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ToState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SupersedesRevision = table.Column<long>(type: "bigint", nullable: true),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleContext = table.Column<string>(type: "text", nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlStateEvents", x => new { x.TenantId, x.VesselId, x.ControlId, x.Revision });
                    table.CheckConstraint("CK_ControlStateEvents", "\"ControlKind\" = 'Control' AND \"Revision\" > 0 AND \"ControlRevision\" > 0 AND \"FromState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED','RETIRED') AND ((\"ReasonCode\" = 'SUBMIT_FOR_REVIEW' AND \"FromState\" = 'DRAFT' AND \"ToState\" = 'IN_REVIEW') OR (\"ReasonCode\" = 'RETURN_FOR_CORRECTION' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'DRAFT') OR (\"ReasonCode\" = 'APPROVE' AND \"FromState\" = 'IN_REVIEW' AND \"ToState\" = 'APPROVED') OR (\"ReasonCode\" = 'PUBLISH' AND \"FromState\" = 'APPROVED' AND \"ToState\" = 'PUBLISHED') OR (\"ReasonCode\" = 'RETIRE' AND \"FromState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" = 'RETIRED') OR (\"ReasonCode\" = 'CONTENT_REVISED' AND \"FromState\" IN ('IN_REVIEW','APPROVED','PUBLISHED') AND \"ToState\" = 'DRAFT')) AND (\"SupersedesRevision\" IS NULL OR (\"ReasonCode\" = 'PUBLISH' AND \"SupersedesRevision\" > 0 AND \"SupersedesRevision\" < \"ControlRevision\"))");
                    table.ForeignKey(
                        name: "FK_ControlStateEvents_ControlEvidenceHeads_TenantId_VesselId_C~",
                        columns: x => new { x.TenantId, x.VesselId, x.ControlKind, x.ControlId },
                        principalSchema: "core",
                        principalTable: "ControlEvidenceHeads",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlStateEvents_ControlEvidenceRevisions_TenantId_Vessel~",
                        columns: x => new { x.TenantId, x.VesselId, x.ControlKind, x.ControlId, x.ControlRevision },
                        principalSchema: "core",
                        principalTable: "ControlEvidenceRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "Id", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""CREATE TRIGGER control_state_events_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ControlStateEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();""");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ControlEvidenceHeads_ControlState",
                schema: "core",
                table: "ControlEvidenceHeads",
                sql: "(\"Kind\" = 'Control' AND (\"ControlState\" IS NULL OR \"ControlState\" IN ('DRAFT','IN_REVIEW','APPROVED','PUBLISHED','RETIRED')) AND (\"PublishedRevision\" IS NULL OR (\"PublishedRevision\" > 0 AND \"PublishedRevision\" <= \"Revision\" AND \"ControlState\" IS NOT NULL AND \"ControlState\" <> 'RETIRED')) AND (\"ControlState\" IS DISTINCT FROM 'PUBLISHED' OR \"PublishedRevision\" IS NOT DISTINCT FROM \"Revision\")) OR (\"Kind\" = 'Evidence' AND \"ControlState\" IS NULL AND \"PublishedRevision\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ControlStateEvents_TenantId_VesselId_ControlKind_ControlId_~",
                schema: "core",
                table: "ControlStateEvents",
                columns: new[] { "TenantId", "VesselId", "ControlKind", "ControlId", "ControlRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlStateEvents",
                schema: "core");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ControlEvidenceHeads_ControlState",
                schema: "core",
                table: "ControlEvidenceHeads");

            migrationBuilder.DropColumn(
                name: "ControlState",
                schema: "core",
                table: "ControlEvidenceHeads");

            migrationBuilder.DropColumn(
                name: "PublishedRevision",
                schema: "core",
                table: "ControlEvidenceHeads");
        }
    }
}
