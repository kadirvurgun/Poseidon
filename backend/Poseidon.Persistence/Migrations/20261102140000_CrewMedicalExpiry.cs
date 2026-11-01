using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED: nullable encrypted declaration, no backfill/medical decision.
public partial class CrewMedicalExpiry : Migration
{
    protected override void Up(MigrationBuilder b) =>
        b.AddColumn<string>(name: "MedicalExpiry", schema: "crew", table: "PersonalProfiles", type: "text", nullable: true);

    // Destructive downgrade must not silently discard protected operator declarations.
    protected override void Down(MigrationBuilder b) =>
        b.Sql("""
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM crew."PersonalProfiles" WHERE "MedicalExpiry" IS NOT NULL) THEN
  RAISE EXCEPTION 'protected_declaration_restore_or_forward_fix_required';
 END IF;
END $$;
ALTER TABLE crew."PersonalProfiles" DROP COLUMN "MedicalExpiry";
""");
}
