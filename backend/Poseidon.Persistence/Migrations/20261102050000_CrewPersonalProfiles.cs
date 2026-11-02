using Microsoft.EntityFrameworkCore.Migrations;
namespace Poseidon.Persistence.Migrations;
public partial class CrewPersonalProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<string>("Status", "AssignmentRevisions", schema: "crew", type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Draft");
        migrationBuilder.Sql("ALTER TABLE crew.\"AssignmentRevisions\" ALTER COLUMN \"Status\" DROP DEFAULT;");
        migrationBuilder.CreateTable(name: "PersonalProfiles", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            PersonId = table.Column<Guid>(type: "uuid", nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            Nationality = table.Column<string>(type: "text", nullable: true),
            DateOfBirth = table.Column<string>(type: "text", nullable: true),
            PassportNo = table.Column<string>(type: "text", nullable: true),
            PassportExpiry = table.Column<string>(type: "text", nullable: true),
            SeamanBookNo = table.Column<string>(type: "text", nullable: true),
            SeamanBookExpiry = table.Column<string>(type: "text", nullable: true),
            CompanyStartDate = table.Column<string>(type: "text", nullable: true),
            SeaServiceMonths = table.Column<string>(type: "text", nullable: true),
            TankerExperienceMonths = table.Column<string>(type: "text", nullable: true),
            VesselTypeExperience = table.Column<string>(type: "text", nullable: true),
            CompanyExperienceMonths = table.Column<string>(type: "text", nullable: true),
            EmergencyContact = table.Column<string>(type: "text", nullable: true),
            PreferredLanguage = table.Column<string>(type: "text", nullable: true),
            ContactPhone = table.Column<string>(type: "text", nullable: true),
            Email = table.Column<string>(type: "text", nullable: true),
        }, constraints: table => {
            table.PrimaryKey("PK_PersonalProfiles", x => new { x.TenantId, x.PersonId });
            table.ForeignKey("FK_PersonalProfiles_Persons_TenantId_PersonId", x => new { x.TenantId, x.PersonId }, principalSchema: "crew", principalTable: "Persons", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateTable(name: "PersonalProfileRevisions", schema: "crew", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false), PersonId = table.Column<Guid>(type: "uuid", nullable: false),
            Revision = table.Column<long>(type: "bigint", nullable: false), PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Provenance = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false), SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_PersonalProfileRevisions", x => new { x.TenantId, x.PersonId, x.Revision });
            table.ForeignKey("FK_PersonalProfileRevisions_PersonalProfiles_TenantId_PersonId", x => new { x.TenantId, x.PersonId }, principalSchema: "crew", principalTable: "PersonalProfiles", principalColumns: new[] { "TenantId", "PersonId" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.Sql("""
            CREATE FUNCTION crew.guard_personal_profile_revision() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'immutable_lineage'; END $$;
            CREATE TRIGGER personal_profile_revision_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON crew."PersonalProfileRevisions" FOR EACH STATEMENT EXECUTE FUNCTION crew.guard_personal_profile_revision();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn("Status", "AssignmentRevisions", "crew");
        migrationBuilder.DropTable("PersonalProfileRevisions", "crew"); migrationBuilder.DropTable("PersonalProfiles", "crew");
        migrationBuilder.Sql("DROP FUNCTION crew.guard_personal_profile_revision();");
    }
}
