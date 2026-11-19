using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S7WpaCrewEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                schema: "crew",
                table: "CredentialRevisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompetenceRequirements",
                schema: "crew",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityMonths = table.Column<int>(type: "integer", nullable: true),
                    VesselTypeScope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CargoScope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompetenceLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    FamiliarizationLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetenceRequirements", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_CompetenceRequirements_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId });
                    table.CheckConstraint("CK_CrewCompetenceRequirement_Values", "\"RequirementType\" IN ('CERTIFICATE','ENDORSEMENT','MEDICAL','TRAINING','COMPETENCE','FAMILIARIZATION') AND \"Status\" IN ('ACTIVE','RETIRED') AND (\"ValidityMonths\" IS NULL OR \"ValidityMonths\" BETWEEN 1 AND 240) AND \"Revision\">0");
                    table.ForeignKey(
                        name: "FK_CompetenceRequirements_Positions_TenantId_PositionId_Vessel~",
                        columns: x => new { x.TenantId, x.PositionId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "Positions",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CredentialAssertions",
                schema: "crew",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialAssertions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_CrewCredentialAssertion_State", "\"State\" IN ('Verified','Not Verified','Suspicious','Suspended / Revoked') AND \"Duty\" IN ('Crewing','DPA','Master') AND \"CredentialRevision\">0 AND \"Sequence\">0");
                    table.ForeignKey(
                        name: "FK_CredentialAssertions_Credentials_TenantId_CredentialId_Vess~",
                        columns: x => new { x.TenantId, x.CredentialId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "Credentials",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafeManningDecisions",
                schema: "crew",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvaluationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvaluationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WindowFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    WindowTo = table.Column<DateOnly>(type: "date", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafeManningDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_CrewSafeManningDecision_Values", "\"Decision\" IN ('MANNING_ACCEPTED','MANNING_NOT_ACCEPTED') AND \"Duty\" IN ('Master','DPA') AND \"EvaluationState\" IN ('ELIGIBLE','ATTENTION','NOT_ELIGIBLE','REVIEW_REQUIRED') AND \"Sequence\">0 AND \"WindowTo\">=\"WindowFrom\"");
                    table.ForeignKey(
                        name: "FK_SafeManningDecisions_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetenceRecords",
                schema: "crew",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetenceRecords", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_CompetenceRecords_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId });
                    table.CheckConstraint("CK_CrewCompetenceRecord_Values", "\"Kind\" IN ('TRAINING_COMPLETION','FAMILIARIZATION','COMPETENCE_ASSESSMENT') AND \"Revision\">0 AND (\"ValidUntil\" IS NULL OR \"CompletedOn\" IS NULL OR \"ValidUntil\">=\"CompletedOn\")");
                    table.ForeignKey(
                        name: "FK_CompetenceRecords_Assignments_TenantId_AssignmentId_VesselI~",
                        columns: x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId },
                        principalSchema: "crew",
                        principalTable: "Assignments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId", "PersonId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetenceRecords_CompetenceRequirements_TenantId_Requireme~",
                        columns: x => new { x.TenantId, x.RequirementId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "CompetenceRequirements",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompetenceSignOffs",
                schema: "crew",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordRevision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetenceSignOffs", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_CrewCompetenceSignOff_Values", "\"Outcome\" IN ('CONFIRMED','NOT_CONFIRMED') AND \"Duty\" IN ('Assessor','Master','ChiefOfficer','ChiefEngineer') AND \"RecordRevision\">0 AND \"Sequence\">0");
                    table.ForeignKey(
                        name: "FK_CompetenceSignOffs_CompetenceRecords_TenantId_RecordId_Vess~",
                        columns: x => new { x.TenantId, x.RecordId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "CompetenceRecords",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceRecords_TenantId_AssignmentId_VesselId_PersonId",
                schema: "crew",
                table: "CompetenceRecords",
                columns: new[] { "TenantId", "AssignmentId", "VesselId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceRecords_TenantId_RequirementId_VesselId",
                schema: "crew",
                table: "CompetenceRecords",
                columns: new[] { "TenantId", "RequirementId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceRecords_TenantId_VesselId_AssignmentId",
                schema: "crew",
                table: "CompetenceRecords",
                columns: new[] { "TenantId", "VesselId", "AssignmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceRequirements_TenantId_PositionId_VesselId",
                schema: "crew",
                table: "CompetenceRequirements",
                columns: new[] { "TenantId", "PositionId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceRequirements_TenantId_VesselId_PositionId_Require~",
                schema: "crew",
                table: "CompetenceRequirements",
                columns: new[] { "TenantId", "VesselId", "PositionId", "RequirementType", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceSignOffs_TenantId_RecordId_Sequence",
                schema: "crew",
                table: "CompetenceSignOffs",
                columns: new[] { "TenantId", "RecordId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompetenceSignOffs_TenantId_RecordId_VesselId",
                schema: "crew",
                table: "CompetenceSignOffs",
                columns: new[] { "TenantId", "RecordId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_CredentialAssertions_TenantId_CredentialId_Sequence",
                schema: "crew",
                table: "CredentialAssertions",
                columns: new[] { "TenantId", "CredentialId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CredentialAssertions_TenantId_CredentialId_VesselId",
                schema: "crew",
                table: "CredentialAssertions",
                columns: new[] { "TenantId", "CredentialId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafeManningDecisions_TenantId_VesselId_Sequence",
                schema: "crew",
                table: "SafeManningDecisions",
                columns: new[] { "TenantId", "VesselId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetenceSignOffs",
                schema: "crew");

            migrationBuilder.DropTable(
                name: "CredentialAssertions",
                schema: "crew");

            migrationBuilder.DropTable(
                name: "SafeManningDecisions",
                schema: "crew");

            migrationBuilder.DropTable(
                name: "CompetenceRecords",
                schema: "crew");

            migrationBuilder.DropTable(
                name: "CompetenceRequirements",
                schema: "crew");

            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "crew",
                table: "CredentialRevisions");
        }
    }
}
