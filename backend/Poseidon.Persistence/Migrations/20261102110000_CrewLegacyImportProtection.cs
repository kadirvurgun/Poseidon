using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED; generation/model parity/application remain DEFERRED.
public partial class CrewLegacyImportProtection : Migration
{
    protected override void Up(MigrationBuilder b) {
        foreach (var column in new[] { "LegacyProtectionPolicyVersion", "LegacyProtectionIdentityVersion", "LegacyProtectionPurpose" })
            b.AddColumn<string>(column, "ImportBatches", "text", schema: "integration", nullable: true);
        b.AddColumn<DateTimeOffset>("LegacyProtectedAt", "ImportBatches", "timestamp with time zone", schema: "integration", nullable: true);
        b.AddCheckConstraint("CK_ImportBatch_LegacyProtection", "ImportBatches", """
("LegacyProtectedAt" IS NULL AND "LegacyProtectionPolicyVersion" IS NULL AND "LegacyProtectionIdentityVersion" IS NULL AND "LegacyProtectionPurpose" IS NULL)
OR ("Category" = 'crew' AND "LegacyProtectedAt" IS NOT NULL
AND "LegacyProtectionPolicyVersion" IS NOT NULL AND "LegacyProtectionIdentityVersion" IS NOT NULL AND "LegacyProtectionPurpose" IS NOT NULL
AND length("LegacyProtectionPolicyVersion") BETWEEN 1 AND 80 AND length("LegacyProtectionIdentityVersion") BETWEEN 1 AND 80
AND length("LegacyProtectionPurpose") BETWEEN 1 AND 80
AND "TableJson" LIKE 'protected-legacy-v1:%' AND "BindingsJson" = '[]' AND "SourceHash" = ''
AND "SourceName" = 'name-withheld.xlsx' AND "SourceLocator" = 'protected-history')
""", "integration");
        b.Sql("""
CREATE OR REPLACE FUNCTION integration.guard_import_source() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD."Category" = 'crew' AND OLD."LegacyProtectedAt" IS NULL AND OLD."TableJson" NOT LIKE 'protected-%'
     AND NEW."LegacyProtectedAt" IS NOT NULL AND NEW."TableJson" LIKE 'protected-legacy-v1:%'
     AND (to_jsonb(NEW) - ARRAY['TableJson','BindingsJson','SourceHash','SourceName','SourceLocator','LegacyProtectedAt','LegacyProtectionPolicyVersion','LegacyProtectionIdentityVersion','LegacyProtectionPurpose']) = (to_jsonb(OLD) - ARRAY['TableJson','BindingsJson','SourceHash','SourceName','SourceLocator','LegacyProtectedAt','LegacyProtectionPolicyVersion','LegacyProtectionIdentityVersion','LegacyProtectionPurpose']) THEN
    RETURN NEW;
  END IF;
  IF OLD."LegacyProtectedAt" IS NOT NULL OR OLD."State" = 'Committed'
     OR (to_jsonb(NEW) - ARRAY['BindingsJson','Revision','State','RowCount','CommittedAt','CommittedPreviewId','ReviewerState'])
       IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['BindingsJson','Revision','State','RowCount','CommittedAt','CommittedPreviewId','ReviewerState'])
  THEN RAISE EXCEPTION 'immutable import source'; END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER import_source_no_delete BEFORE DELETE ON integration."ImportBatches"
FOR EACH ROW EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER import_source_no_truncate BEFORE TRUNCATE ON integration."ImportBatches"
FOR EACH STATEMENT EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER crew_position_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON crew."Positions"
FOR EACH STATEMENT EXECUTE FUNCTION integration.reject_import_observation_change();
""");
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Protected historical imports require a controlled retention/rollback design; never drop encrypted history.");
}
