using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Hr5201LegalEntityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.AddColumn<long>(
                name: "MasterRevision",
                schema: "crew",
                table: "Persons",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "MasterState",
                schema: "crew",
                table: "Persons",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "PersonType",
                schema: "crew",
                table: "Persons",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "BaseLocale",
                schema: "core",
                table: "Companies",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "core",
                table: "Companies",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFrom",
                schema: "core",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveTo",
                schema: "core",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrTimeZone",
                schema: "core",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LegalEntityRevision",
                schema: "core",
                table: "Companies",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "LegalEntityStatus",
                schema: "core",
                table: "Companies",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "core",
                table: "Companies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Employments",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmploymentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_Employments_TenantId_Id_PersonId_LegalEntityId", x => new { x.TenantId, x.Id, x.PersonId, x.LegalEntityId });
                    table.CheckConstraint("CK_Employment_Period", "\"EndAt\" IS NULL OR \"EndAt\" > \"StartAt\"");
                    table.CheckConstraint("CK_Employment_State", "\"State\" IN ('Draft','Active','Ended','Cancelled') AND \"EmploymentType\" IN ('Employee','Contractor','Agency') AND (\"State\" NOT IN ('Active','Ended') OR (\"ActivatedBy\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"ActivatedBy\" <> \"CreatedBy\")) AND (\"State\" <> 'Ended' OR \"EndAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Employments_Companies_TenantId_LegalEntityId",
                        columns: x => new { x.TenantId, x.LegalEntityId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employments_Persons_TenantId_PersonId",
                        columns: x => new { x.TenantId, x.PersonId },
                        principalSchema: "crew",
                        principalTable: "Persons",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalEntityFieldRevisions",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalEntityFieldRevisions", x => new { x.TenantId, x.LegalEntityId, x.Revision, x.Field });
                    table.CheckConstraint("CK_LegalEntityFieldRevision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_LegalEntityFieldRevisions_Companies_TenantId_LegalEntityId",
                        columns: x => new { x.TenantId, x.LegalEntityId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalEntityIdentifiers",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalEntityIdentifiers", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LegalEntityIdentifier_Retired", "(\"RetiredAt\" IS NULL) = (\"RetiredBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_LegalEntityIdentifiers_Companies_TenantId_LegalEntityId",
                        columns: x => new { x.TenantId, x.LegalEntityId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegalEntityRoles",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerificationState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalEntityRoles", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_LegalEntityRole_Kind", "\"Role\" IN ('RegisteredOwner','Operator','TechnicalManager','CrewManager','CommercialManager','DocCompany','Employer') AND ((\"Role\" = 'Employer') = (\"VesselId\" IS NULL)) AND \"VerificationState\" = 'Unverified'");
                    table.CheckConstraint("CK_LegalEntityRole_Period", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_LegalEntityRoles_Companies_TenantId_LegalEntityId",
                        columns: x => new { x.TenantId, x.LegalEntityId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LegalEntityRoles_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonFieldRevisions",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonFieldRevisions", x => new { x.TenantId, x.PersonId, x.Revision, x.Field });
                    table.CheckConstraint("CK_PersonFieldRevision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_PersonFieldRevisions_Persons_TenantId_PersonId",
                        columns: x => new { x.TenantId, x.PersonId },
                        principalSchema: "crew",
                        principalTable: "Persons",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlannedAssignments",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PlannedZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PlannedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedAssignments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_PlannedAssignments_TenantId_Id_PersonId", x => new { x.TenantId, x.Id, x.PersonId });
                    table.CheckConstraint("CK_HrAssignment_Kind", "\"Kind\" IN ('Vessel','Shore') AND ((\"Kind\" = 'Vessel') = (\"VesselId\" IS NOT NULL)) AND (\"PositionId\" IS NULL OR \"VesselId\" IS NOT NULL)");
                    table.CheckConstraint("CK_HrAssignment_Period", "\"PlannedTo\" > \"PlannedFrom\"");
                    table.CheckConstraint("CK_HrAssignment_State", "\"State\" IN ('Planned','Approved','Cancelled') AND (\"State\" = 'Planned' OR \"State\" = 'Cancelled' OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedBy\" <> \"PlannedBy\"))");
                    table.ForeignKey(
                        name: "FK_PlannedAssignments_Employments_TenantId_EmploymentId_Person~",
                        columns: x => new { x.TenantId, x.EmploymentId, x.PersonId, x.LegalEntityId },
                        principalSchema: "hr",
                        principalTable: "Employments",
                        principalColumns: new[] { "TenantId", "Id", "PersonId", "LegalEntityId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlannedAssignments_Positions_TenantId_PositionId_VesselId",
                        columns: x => new { x.TenantId, x.PositionId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "Positions",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlannedAssignments_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActualEvents",
                schema: "hr",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Origin = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Disposition = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActualEvents", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_HrActualEvent", "\"Kind\" IN ('CheckIn','CheckOut') AND \"Origin\" IN ('Self','SupervisorReported') AND \"Quality\" IN ('Valid','Suspect') AND \"Disposition\" IN ('Accepted','PendingReconciliation') AND (\"Quality\" = 'Valid' OR \"Disposition\" = 'PendingReconciliation')");
                    table.ForeignKey(
                        name: "FK_ActualEvents_PlannedAssignments_TenantId_AssignmentId_Perso~",
                        columns: x => new { x.TenantId, x.AssignmentId, x.PersonId },
                        principalSchema: "hr",
                        principalTable: "PlannedAssignments",
                        principalColumns: new[] { "TenantId", "Id", "PersonId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Company_LegalEntityActive",
                schema: "core",
                table: "Companies",
                sql: "\"LegalEntityStatus\" = 'Draft' OR (\"LegalName\" IS NOT NULL AND \"CountryCode\" IS NOT NULL AND \"HrTimeZone\" IS NOT NULL AND \"BaseLocale\" IS NOT NULL AND \"EffectiveFrom\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Company_LegalEntityPeriod",
                schema: "core",
                table: "Companies",
                sql: "\"EffectiveTo\" IS NULL OR (\"EffectiveFrom\" IS NOT NULL AND \"EffectiveTo\" > \"EffectiveFrom\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Company_LegalEntityStatus",
                schema: "core",
                table: "Companies",
                sql: "\"LegalEntityStatus\" IN ('Draft','Active','Inactive')");

            migrationBuilder.CreateIndex(
                name: "IX_ActualEvents_TenantId_AssignmentId_OccurredAt",
                schema: "hr",
                table: "ActualEvents",
                columns: new[] { "TenantId", "AssignmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActualEvents_TenantId_AssignmentId_PersonId",
                schema: "hr",
                table: "ActualEvents",
                columns: new[] { "TenantId", "AssignmentId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActualEvents_TenantId_SourceSystem_SourceEventId",
                schema: "hr",
                table: "ActualEvents",
                columns: new[] { "TenantId", "SourceSystem", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employments_TenantId_LegalEntityId_EmployeeNo",
                schema: "hr",
                table: "Employments",
                columns: new[] { "TenantId", "LegalEntityId", "EmployeeNo" });

            migrationBuilder.CreateIndex(
                name: "IX_Employments_TenantId_PersonId",
                schema: "hr",
                table: "Employments",
                columns: new[] { "TenantId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntityIdentifiers_TenantId_LegalEntityId",
                schema: "hr",
                table: "LegalEntityIdentifiers",
                columns: new[] { "TenantId", "LegalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntityIdentifiers_TenantId_Type_Issuer_Value",
                schema: "hr",
                table: "LegalEntityIdentifiers",
                columns: new[] { "TenantId", "Type", "Issuer", "Value" },
                unique: true,
                filter: "\"RetiredAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntityRoles_TenantId_LegalEntityId",
                schema: "hr",
                table: "LegalEntityRoles",
                columns: new[] { "TenantId", "LegalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntityRoles_TenantId_VesselId",
                schema: "hr",
                table: "LegalEntityRoles",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAssignments_TenantId_EmploymentId_PersonId_LegalEnti~",
                schema: "hr",
                table: "PlannedAssignments",
                columns: new[] { "TenantId", "EmploymentId", "PersonId", "LegalEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAssignments_TenantId_PersonId_PlannedFrom",
                schema: "hr",
                table: "PlannedAssignments",
                columns: new[] { "TenantId", "PersonId", "PlannedFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAssignments_TenantId_PositionId_VesselId",
                schema: "hr",
                table: "PlannedAssignments",
                columns: new[] { "TenantId", "PositionId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedAssignments_TenantId_VesselId",
                schema: "hr",
                table: "PlannedAssignments",
                columns: new[] { "TenantId", "VesselId" });

            // PLAN-1511-5201 race backstops and append-only history. Domain checks give the readable error first;
            // these database rules keep concurrent commands from violating the same invariant (btree_gist already enabled).
            migrationBuilder.Sql("""
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_ExclusivePerVessel"
                  EXCLUDE USING gist ("TenantId" WITH =, "VesselId" WITH =, "Role" WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&)
                  WHERE ("Role" IN ('Operator','TechnicalManager','CrewManager','CommercialManager','DocCompany'));
                ALTER TABLE hr."LegalEntityRoles" ADD CONSTRAINT "EX_LegalEntityRole_SameHolder"
                  EXCLUDE USING gist ("TenantId" WITH =, "LegalEntityId" WITH =, "Role" WITH =, (COALESCE("VesselId", '00000000-0000-0000-0000-000000000000'::uuid)) WITH =, tstzrange("EffectiveFrom", "EffectiveTo", '[)') WITH &&);
                ALTER TABLE hr."Employments" ADD CONSTRAINT "EX_Employment_EmployeeNo"
                  EXCLUDE USING gist ("TenantId" WITH =, "LegalEntityId" WITH =, "EmployeeNo" WITH =, tstzrange("StartAt", "EndAt", '[)') WITH &&)
                  WHERE ("State" <> 'Cancelled');
                ALTER TABLE hr."PlannedAssignments" ADD CONSTRAINT "EX_HrAssignment_ApprovedOccupancy"
                  EXCLUDE USING gist ("TenantId" WITH =, "PersonId" WITH =, tstzrange("PlannedFrom", "PlannedTo", '[)') WITH &&)
                  WHERE ("State" = 'Approved');
                CREATE FUNCTION hr.guard_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'HR field/source revisions and actual facts are append-only' USING ERRCODE='55000';
                END $$;
                CREATE TRIGGER "GuardLegalEntityFieldRevisions" BEFORE UPDATE OR DELETE OR TRUNCATE ON hr."LegalEntityFieldRevisions" FOR EACH STATEMENT EXECUTE FUNCTION hr.guard_append_only();
                CREATE TRIGGER "GuardPersonFieldRevisions" BEFORE UPDATE OR DELETE OR TRUNCATE ON hr."PersonFieldRevisions" FOR EACH STATEMENT EXECUTE FUNCTION hr.guard_append_only();
                CREATE TRIGGER "GuardActualEvents" BEFORE UPDATE OR DELETE OR TRUNCATE ON hr."ActualEvents" FOR EACH STATEMENT EXECUTE FUNCTION hr.guard_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "GuardLegalEntityFieldRevisions" ON hr."LegalEntityFieldRevisions";
                DROP TRIGGER "GuardPersonFieldRevisions" ON hr."PersonFieldRevisions";
                DROP TRIGGER "GuardActualEvents" ON hr."ActualEvents";
                DROP FUNCTION hr.guard_append_only();
                """);
            migrationBuilder.DropTable(
                name: "ActualEvents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "LegalEntityFieldRevisions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "LegalEntityIdentifiers",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "LegalEntityRoles",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PersonFieldRevisions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "PlannedAssignments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "Employments",
                schema: "hr");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Company_LegalEntityActive",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Company_LegalEntityPeriod",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Company_LegalEntityStatus",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MasterRevision",
                schema: "crew",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "MasterState",
                schema: "crew",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PersonType",
                schema: "crew",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "BaseLocale",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EffectiveFrom",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EffectiveTo",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "HrTimeZone",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LegalEntityRevision",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LegalEntityStatus",
                schema: "core",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "core",
                table: "Companies");
        }
    }
}
