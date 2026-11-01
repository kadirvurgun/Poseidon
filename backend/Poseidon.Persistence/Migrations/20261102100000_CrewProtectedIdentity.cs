using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED. Real EF generation/parity and application are DEFERRED.
public partial class CrewProtectedIdentity : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.AddColumn<string>("CrewIdentityPolicyVersion", "ImportBatches", "text", schema: "integration", nullable: true);
        b.AddColumn<string>("ProtectedFullName", "Persons", "text", schema: "crew", nullable: true);
        b.AddColumn<string>("ProtectedEmployeeNo", "Persons", "text", schema: "crew", nullable: true);
        b.AddColumn<string>("IdentityStorageState", "Persons", "character varying(32)", schema: "crew", maxLength: 32, nullable: false, defaultValue: "LegacyUnavailable");
        b.AddColumn<long>("IdentityRevision", "Persons", "bigint", schema: "crew", nullable: false, defaultValue: 0L);
        // Metadata defaults classify old rows; no PII is rewritten or falsely marked protected.
        b.Sql("ALTER TABLE crew.\"Persons\" ALTER COLUMN \"IdentityStorageState\" DROP DEFAULT, ALTER COLUMN \"IdentityRevision\" DROP DEFAULT;");
        b.AddCheckConstraint("CK_Person_ProtectedIdentity", "Persons", "\"IdentityStorageState\" IN ('LegacyUnavailable','ProtectedV1') AND (\"IdentityStorageState\" <> 'ProtectedV1' OR (\"FullName\" IS NULL AND \"EmployeeNo\" IS NULL AND \"IdentityRevision\" > 0))", "crew");
        b.CreateTable("IdentityRevisions", schema: "crew", columns: t => new {
            TenantId = t.Column<Guid>("uuid", nullable: false), PersonId = t.Column<Guid>("uuid", nullable: false), Revision = t.Column<long>("bigint", nullable: false),
            PolicyVersion = t.Column<string>("character varying(80)", maxLength: 80, nullable: false), Purpose = t.Column<string>("character varying(80)", maxLength: 80, nullable: false),
            Operation = t.Column<string>("character varying(32)", maxLength: 32, nullable: false), SavedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false)
        }, constraints: t => {
            t.PrimaryKey("PK_IdentityRevisions", x => new { x.TenantId, x.PersonId, x.Revision });
            t.ForeignKey("FK_IdentityRevisions_Persons_TenantId_PersonId", x => new { x.TenantId, x.PersonId }, "Persons", new[] { "TenantId", "Id" }, "crew", onDelete: ReferentialAction.Restrict);
        });
        b.Sql("""
            CREATE FUNCTION crew.guard_identity_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'immutable_identity_history'; END $$;
            CREATE TRIGGER identity_history_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON crew."IdentityRevisions"
            FOR EACH STATEMENT EXECUTE FUNCTION crew.guard_identity_history();
            CREATE FUNCTION crew.guard_identity_protection() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD."IdentityStorageState" = 'ProtectedV1' AND NEW."IdentityStorageState" <> 'ProtectedV1'
                THEN RAISE EXCEPTION 'identity_protection_downgrade'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER identity_no_downgrade BEFORE UPDATE ON crew."Persons"
            FOR EACH ROW EXECUTE FUNCTION crew.guard_identity_protection();
            """);
    }
    protected override void Down(MigrationBuilder b) {
        b.Sql("""
            DO $$ BEGIN IF EXISTS (SELECT 1 FROM crew."Persons" WHERE "IdentityStorageState" = 'ProtectedV1')
            OR EXISTS (SELECT 1 FROM crew."IdentityRevisions") THEN RAISE EXCEPTION 'identity_rollback_requires_controlled_retention_design'; END IF; END $$;
            DROP TRIGGER identity_no_downgrade ON crew."Persons";
            DROP FUNCTION crew.guard_identity_protection();
            DROP TABLE crew."IdentityRevisions";
            DROP FUNCTION crew.guard_identity_history();
            """);
        b.DropColumn("CrewIdentityPolicyVersion", "ImportBatches", "integration");
        b.DropCheckConstraint("CK_Person_ProtectedIdentity", "Persons", "crew");
        foreach (var column in new[] { "ProtectedFullName", "ProtectedEmployeeNo", "IdentityStorageState", "IdentityRevision" }) b.DropColumn(column, "Persons", "crew");
    }
}
