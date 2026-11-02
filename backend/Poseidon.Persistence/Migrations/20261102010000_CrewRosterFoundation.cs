using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;
public partial class CrewRosterFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("crew");
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        migrationBuilder.CreateTable(name: "Persons", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            FullName = table.Column<string>(type: "character varying(200)", nullable: true, maxLength: 200),
            EmployeeNo = table.Column<string>(type: "character varying(64)", nullable: true, maxLength: 64),
            EmploymentState = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            Provenance = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            SourceObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            xmin = table.Column<uint>(type: "xid", nullable: false, rowVersion: true),
        }, constraints: table => {
            table.PrimaryKey("PK_Persons", x => new { x.TenantId, x.Id });
            table.ForeignKey("FK_Persons_Tenants_TenantId", x => new { x.TenantId }, principalSchema: "core", principalTable: "Tenants", principalColumns: new[] { "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_Persons_TenantId", schema: "crew", table: "Persons", columns: new[] { "TenantId" });
        migrationBuilder.CreateTable(name: "Positions", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            Code = table.Column<string>(type: "character varying(64)", nullable: false, maxLength: 64),
            Rank = table.Column<string>(type: "character varying(100)", nullable: true, maxLength: 100),
            Capacity = table.Column<string>(type: "character varying(100)", nullable: true, maxLength: 100),
            Department = table.Column<string>(type: "character varying(100)", nullable: true, maxLength: 100),
            Provenance = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            xmin = table.Column<uint>(type: "xid", nullable: false, rowVersion: true),
        }, constraints: table => {
            table.PrimaryKey("PK_Positions", x => new { x.TenantId, x.Id });
            table.UniqueConstraint("AK_Positions_TenantId_Id_VesselId", x => new { x.TenantId, x.Id, x.VesselId });
            table.ForeignKey("FK_Positions_Vessels_TenantId_VesselId", x => new { x.TenantId, x.VesselId }, principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_Positions_TenantId_VesselId_Code", schema: "crew", table: "Positions", columns: new[] { "TenantId", "VesselId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Positions_TenantId_VesselId", schema: "crew", table: "Positions", columns: new[] { "TenantId", "VesselId" });
        migrationBuilder.CreateTable(name: "Assignments", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            PersonId = table.Column<Guid>(type: "uuid", nullable: false),
            PositionId = table.Column<Guid>(type: "uuid", nullable: true),
            From = table.Column<DateOnly>(type: "date", nullable: true),
            To = table.Column<DateOnly>(type: "date", nullable: true),
            ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
            WatchGroup = table.Column<string>(type: "character varying(64)", nullable: true, maxLength: 64),
            Status = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            Provenance = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            SourceObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            xmin = table.Column<uint>(type: "xid", nullable: false, rowVersion: true),
        }, constraints: table => {
            table.PrimaryKey("PK_Assignments", x => new { x.TenantId, x.Id });
            table.UniqueConstraint("AK_Assignments_TenantId_Id_VesselId_PersonId", x => new { x.TenantId, x.Id, x.VesselId, x.PersonId });
            table.CheckConstraint("CK_Assignment_Period", "\"To\" IS NULL OR (\"From\" IS NOT NULL AND \"To\" > \"From\")");
            table.ForeignKey("FK_Assignments_Persons_TenantId_PersonId", x => new { x.TenantId, x.PersonId }, principalSchema: "crew", principalTable: "Persons", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_Assignments_Vessels_TenantId_VesselId", x => new { x.TenantId, x.VesselId }, principalSchema: "core", principalTable: "Vessels", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_Assignments_Positions_TenantId_PositionId_VesselId", x => new { x.TenantId, x.PositionId, x.VesselId }, principalSchema: "crew", principalTable: "Positions", principalColumns: new[] { "TenantId", "Id", "VesselId" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_Assignments_TenantId_PersonId_From", schema: "crew", table: "Assignments", columns: new[] { "TenantId", "PersonId", "From" });
        migrationBuilder.CreateIndex(name: "IX_Assignments_TenantId_PersonId", schema: "crew", table: "Assignments", columns: new[] { "TenantId", "PersonId" });
        migrationBuilder.CreateIndex(name: "IX_Assignments_TenantId_VesselId", schema: "crew", table: "Assignments", columns: new[] { "TenantId", "VesselId" });
        migrationBuilder.CreateIndex(name: "IX_Assignments_TenantId_PositionId_VesselId", schema: "crew", table: "Assignments", columns: new[] { "TenantId", "PositionId", "VesselId" });
        migrationBuilder.CreateTable(name: "AssignmentRevisions", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            PersonId = table.Column<Guid>(type: "uuid", nullable: false),
            PositionId = table.Column<Guid>(type: "uuid", nullable: true),
            From = table.Column<DateOnly>(type: "date", nullable: true),
            To = table.Column<DateOnly>(type: "date", nullable: true),
            ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            Provenance = table.Column<string>(type: "character varying(32)", nullable: false, maxLength: 32),
            SourceObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
        }, constraints: table => {
            table.PrimaryKey("PK_AssignmentRevisions", x => new { x.TenantId, x.AssignmentId, x.Revision });
            table.ForeignKey("FK_AssignmentRevisions_Assignments_TenantId_AssignmentId_Vesse~", x => new { x.TenantId, x.AssignmentId, x.VesselId, x.PersonId }, principalSchema: "crew", principalTable: "Assignments", principalColumns: new[] { "TenantId", "Id", "VesselId", "PersonId" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_AssignmentRevisions_TenantId_AssignmentId_VesselId_PersonId", schema: "crew", table: "AssignmentRevisions", columns: new[] { "TenantId", "AssignmentId", "VesselId", "PersonId" });
        // Controlled schema migration only. The exclusion constraint arbitrates concurrent reservations.
        migrationBuilder.Sql("""
            ALTER TABLE crew."Assignments" ADD CONSTRAINT "EX_Assignment_PersonPeriod"
            EXCLUDE USING gist ("TenantId" WITH =, "PersonId" WITH =, daterange("From", "To", '[)') WITH &&)
            WHERE ("From" IS NOT NULL);
            CREATE FUNCTION crew.reject_revision_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Crew revisions are immutable'; END; $$;
            CREATE TRIGGER crew_revision_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON crew."AssignmentRevisions"
            FOR EACH STATEMENT EXECUTE FUNCTION crew.reject_revision_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AssignmentRevisions", "crew");
        migrationBuilder.Sql("DROP FUNCTION crew.reject_revision_mutation();");
        migrationBuilder.DropTable("Assignments", "crew");
        migrationBuilder.DropTable("Positions", "crew");
        migrationBuilder.DropTable("Persons", "crew");
    }
}
