using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED. Reconcile via EF generation/parity at the deferred foundation gate.
public partial class CrewPhotoSelection : Migration
{
    protected override void Up(MigrationBuilder b) {
        foreach (var table in new[] { "PersonalProfiles", "PersonalProfileRevisions" }) {
            b.AddColumn<Guid>("PhotoFileId", table, "uuid", schema: "crew", nullable: true);
            b.AddColumn<Guid>("PhotoVesselId", table, "uuid", schema: "crew", nullable: true);
            b.AddColumn<string>("PhotoPurpose", table, "character varying(80)", schema: "crew", maxLength: 80, nullable: true);
            b.CreateIndex($"IX_{table}_TenantId_PhotoFileId_PhotoVesselId", table, new[] { "TenantId", "PhotoFileId", "PhotoVesselId" }, schema: "crew");
            b.AddForeignKey(table == "PersonalProfiles" ? "FK_ProfilePhotoFile" : "FK_ProfileRevisionPhotoFile", table, new[] { "TenantId", "PhotoFileId", "PhotoVesselId" },
                "FileStages", principalColumns: new[] { "TenantId", "Id", "VesselId" }, schema: "crew", principalSchema: "files", onDelete: ReferentialAction.Restrict);
        }
        b.AddColumn<string>("PhotoPolicyVersion", "PersonalProfiles", "character varying(80)", schema: "crew", maxLength: 80, nullable: true);
        b.AddColumn<bool>("IsPhotoSelection", "PersonalProfileRevisions", "boolean", schema: "crew", nullable: false, defaultValue: false);
        b.Sql("ALTER TABLE crew.\"PersonalProfileRevisions\" ALTER COLUMN \"IsPhotoSelection\" DROP DEFAULT;");
        b.Sql("""
            CREATE FUNCTION crew.guard_photo_selection() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'UPDATE' AND NEW."PhotoFileId" IS NOT DISTINCT FROM OLD."PhotoFileId"
                    AND NEW."PhotoVesselId" IS NOT DISTINCT FROM OLD."PhotoVesselId" THEN RETURN NEW; END IF;
                IF NEW."PhotoFileId" IS NULL THEN
                    IF NEW."PhotoVesselId" IS NOT NULL THEN RAISE EXCEPTION 'invalid_photo_scope'; END IF;
                ELSIF NEW."PhotoVesselId" IS NULL OR NOT EXISTS (
                    SELECT 1 FROM files."FileStages" f JOIN files."StoredFiles" s
                    ON s."TenantId" = f."TenantId" AND s."Id" = f."Id" AND s."VesselId" = f."VesselId"
                    WHERE f."TenantId" = NEW."TenantId" AND f."Id" = NEW."PhotoFileId"
                    AND f."VesselId" = NEW."PhotoVesselId" AND f."PersonId" = NEW."PersonId"
                    AND f."PersonalKind" = 'Photo' AND f."State" = 'CleanTransport'
                ) THEN RAISE EXCEPTION 'invalid_person_photo'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER profile_photo_owner BEFORE INSERT OR UPDATE ON crew."PersonalProfiles"
            FOR EACH ROW EXECUTE FUNCTION crew.guard_photo_selection();
            CREATE TRIGGER revision_photo_owner BEFORE INSERT ON crew."PersonalProfileRevisions"
            FOR EACH ROW EXECUTE FUNCTION crew.guard_photo_selection();
            """);
    }
    protected override void Down(MigrationBuilder b) {
        // Never erase selected private identity lineage to manufacture rollback success.
        b.Sql("""
            DO $$ BEGIN IF EXISTS (SELECT 1 FROM crew."PersonalProfileRevisions" WHERE "IsPhotoSelection")
                THEN RAISE EXCEPTION 'photo_rollback_requires_controlled_archive'; END IF; END $$;
            DROP TRIGGER profile_photo_owner ON crew."PersonalProfiles";
            DROP TRIGGER revision_photo_owner ON crew."PersonalProfileRevisions";
            DROP FUNCTION crew.guard_photo_selection();
            """);
        foreach (var table in new[] { "PersonalProfiles", "PersonalProfileRevisions" }) {
            b.DropForeignKey(table == "PersonalProfiles" ? "FK_ProfilePhotoFile" : "FK_ProfileRevisionPhotoFile", table, "crew");
            b.DropIndex($"IX_{table}_TenantId_PhotoFileId_PhotoVesselId", table, "crew");
            foreach (var name in new[] { "PhotoFileId", "PhotoVesselId", "PhotoPurpose" }) b.DropColumn(name, table, "crew");
        }
        b.DropColumn("PhotoPolicyVersion", "PersonalProfiles", "crew");
        b.DropColumn("IsPhotoSelection", "PersonalProfileRevisions", "crew");
    }
}
