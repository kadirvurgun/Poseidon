using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED. No database/schema generation was executed.
public partial class CrewPersonFiles : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.AddColumn<Guid>("PersonId", "FileStages", "uuid", schema: "files", nullable: true);
        b.AddColumn<string>("PersonalKind", "FileStages", "character varying(24)", schema: "files", maxLength: 24, nullable: true);
        b.AddColumn<long>("PersonalRevision", "FileStages", "bigint", schema: "files", nullable: true);
        b.AddColumn<DateOnly>("PersonalExpiresOn", "FileStages", "date", schema: "files", nullable: true);
        foreach (var name in new[] { "PersonalPolicyVersion", "PersonalPurpose", "PersonalSourceVersion" })
            b.AddColumn<string>(name, "FileStages", "character varying(80)", schema: "files", maxLength: 80, nullable: true);
        b.CreateIndex("IX_FileStages_TenantId_PersonId_PersonalKind_PersonalRevision", "FileStages",
            new[] { "TenantId", "PersonId", "PersonalKind", "PersonalRevision" }, schema: "files", unique: true, filter: "\"PersonId\" IS NOT NULL");
        b.AddForeignKey("FK_FileStages_Persons_TenantId_PersonId", "FileStages", new[] { "TenantId", "PersonId" }, "Persons",
            principalColumns: new[] { "TenantId", "Id" }, schema: "files", principalSchema: "crew", onDelete: ReferentialAction.Restrict);
        b.AddCheckConstraint("CK_FileStages_PersonOwner", "FileStages", "(\"PersonId\" IS NULL AND \"PersonalKind\" IS NULL AND \"PersonalRevision\" IS NULL AND \"PersonalExpiresOn\" IS NULL AND \"PersonalPolicyVersion\" IS NULL AND \"PersonalPurpose\" IS NULL AND \"PersonalSourceVersion\" IS NULL) OR (\"PersonId\" IS NOT NULL AND \"DocumentId\" IS NULL AND \"DocumentRevision\" IS NULL AND \"PersonalKind\" IS NOT NULL AND \"PersonalRevision\" IS NOT NULL AND \"PersonalRevision\" > 0 AND \"PersonalPolicyVersion\" IS NOT NULL AND \"PersonalPurpose\" IS NOT NULL AND \"PersonalSourceVersion\" IS NOT NULL)", schema: "files");
        b.AddColumn<Guid>("PersonFileReference", "Credentials", "uuid", schema: "crew", nullable: true);
        b.CreateIndex("IX_Credentials_TenantId_PersonFileReference_VesselId", "Credentials", new[] { "TenantId", "PersonFileReference", "VesselId" }, schema: "crew");
        b.AddForeignKey("FK_Credentials_PersonFile_Scope", "Credentials", new[] { "TenantId", "PersonFileReference", "VesselId" }, "FileStages",
            principalColumns: new[] { "TenantId", "Id", "VesselId" }, schema: "crew", principalSchema: "files", onDelete: ReferentialAction.Restrict);
        b.Sql("""
            CREATE FUNCTION files.guard_person_file_owner() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF (OLD."PersonId" IS NOT NULL OR NEW."PersonId" IS NOT NULL)
                   AND (to_jsonb(OLD) - 'State') IS DISTINCT FROM (to_jsonb(NEW) - 'State') THEN
                    RAISE EXCEPTION 'immutable_person_file_version';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER person_file_owner_immutable BEFORE UPDATE ON files."FileStages"
            FOR EACH ROW EXECUTE FUNCTION files.guard_person_file_owner();
            """);
    }
    protected override void Down(MigrationBuilder b)
    {
        // Removing person ownership from existing private bytes would expose them to generic file routes.
        b.Sql("""
            DO $$ BEGIN IF EXISTS (SELECT 1 FROM files."FileStages" WHERE "PersonId" IS NOT NULL)
                THEN RAISE EXCEPTION 'private_file_rollback_requires_controlled_archive'; END IF; END $$;
            DROP TRIGGER person_file_owner_immutable ON files."FileStages";
            DROP FUNCTION files.guard_person_file_owner();
            """);
        b.DropForeignKey("FK_Credentials_PersonFile_Scope", "Credentials", "crew");
        b.DropIndex("IX_Credentials_TenantId_PersonFileReference_VesselId", "Credentials", "crew");
        b.DropColumn("PersonFileReference", "Credentials", "crew");
        b.DropForeignKey("FK_FileStages_Persons_TenantId_PersonId", "FileStages", "files");
        b.DropCheckConstraint("CK_FileStages_PersonOwner", "FileStages", "files");
        b.DropIndex("IX_FileStages_TenantId_PersonId_PersonalKind_PersonalRevision", "FileStages", "files");
        foreach (var name in new[] { "PersonId", "PersonalKind", "PersonalRevision", "PersonalExpiresOn", "PersonalPolicyVersion", "PersonalPurpose", "PersonalSourceVersion" })
            b.DropColumn(name, "FileStages", "files");
    }
}
