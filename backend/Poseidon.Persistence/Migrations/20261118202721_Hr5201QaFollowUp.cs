using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hr5201QaFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "hr",
                table: "PersonFieldRevisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "hr",
                table: "LegalEntityRoles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                schema: "hr",
                table: "LegalEntityRoles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "hr",
                table: "LegalEntityRoles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Recorded");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "hr",
                table: "LegalEntityFieldRevisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StateChanges",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ToState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateChanges", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_HrStateChange", "\"SubjectKind\" IN ('LegalEntityIdentifier','LegalEntityRole','Employment','PlannedAssignment') AND \"FromState\" <> \"ToState\" AND length(btrim(\"Reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_StateChanges_Companies_TenantId_LegalEntityId",
                        columns: x => new { x.TenantId, x.LegalEntityId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LegalEntityRole_State",
                schema: "hr",
                table: "LegalEntityRoles",
                sql: "\"State\" IN ('Recorded','Cancelled') AND ((\"State\" = 'Cancelled') = (\"CancelledBy\" IS NOT NULL AND \"CancelledAt\" IS NOT NULL)) AND (\"CancelledBy\" IS NULL OR \"CancelledBy\" <> \"CreatedBy\")");

            migrationBuilder.CreateIndex(
                name: "IX_StateChanges_TenantId_LegalEntityId",
                schema: "hr",
                table: "StateChanges",
                columns: new[] { "TenantId", "LegalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_StateChanges_TenantId_SubjectKind_SubjectId_RecordedAt",
                schema: "hr",
                table: "StateChanges",
                columns: new[] { "TenantId", "SubjectKind", "SubjectId", "RecordedAt" });

            // PLAN-1511-5201 QA follow-up: a Cancelled (mistaken) relation is retained but no longer counts for cardinality,
            // and state decisions with their reasons are append-only history (52.01 paragraph:50).
            migrationBuilder.Sql("""
                ALTER TABLE hr."LegalEntityRoles" DROP CONSTRAINT "EX_LegalEntityRole_ExclusivePerVessel";
                ALTER TABLE hr."LegalEntityRoles" DROP CONSTRAINT "EX_LegalEntityRole_SameHolder";
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_ExclusivePerVessel"
                  EXCLUDE USING gist ("TenantId" WITH =, "VesselId" WITH =, "Role" WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&)
                  WHERE ("Role" IN ('Operator','TechnicalManager','CrewManager','CommercialManager','DocCompany') AND "State" = 'Recorded');
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_SameHolder"
                  EXCLUDE USING gist ("TenantId" WITH =, "LegalEntityId" WITH =, "Role" WITH =, (COALESCE("VesselId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&)
                  WHERE ("State" = 'Recorded');
                CREATE TRIGGER "GuardStateChanges" BEFORE UPDATE OR DELETE OR TRUNCATE ON hr."StateChanges" FOR EACH STATEMENT EXECUTE FUNCTION hr.guard_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the foundation constraints; it fails rather than silently discarding history if cancelled overlaps exist.
            migrationBuilder.Sql("""
                DROP TRIGGER "GuardStateChanges" ON hr."StateChanges";
                ALTER TABLE hr."LegalEntityRoles" DROP CONSTRAINT "EX_LegalEntityRole_ExclusivePerVessel";
                ALTER TABLE hr."LegalEntityRoles" DROP CONSTRAINT "EX_LegalEntityRole_SameHolder";
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_ExclusivePerVessel"
                  EXCLUDE USING gist ("TenantId" WITH =, "VesselId" WITH =, "Role" WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&)
                  WHERE ("Role" IN ('Operator','TechnicalManager','CrewManager','CommercialManager','DocCompany'));
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_SameHolder"
                  EXCLUDE USING gist ("TenantId" WITH =, "LegalEntityId" WITH =, "Role" WITH =, (COALESCE("VesselId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&);
                """);
            migrationBuilder.DropTable(
                name: "StateChanges",
                schema: "hr");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LegalEntityRole_State",
                schema: "hr",
                table: "LegalEntityRoles");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "hr",
                table: "PersonFieldRevisions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "hr",
                table: "LegalEntityRoles");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "hr",
                table: "LegalEntityRoles");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "hr",
                table: "LegalEntityRoles");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "hr",
                table: "LegalEntityFieldRevisions");
        }
    }
}
