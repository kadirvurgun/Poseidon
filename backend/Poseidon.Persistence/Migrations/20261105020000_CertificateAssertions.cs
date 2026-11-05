using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261105020000_CertificateAssertions")]
public sealed class CertificateAssertions : Migration
{
    // Source authored only. Parity and actual migration execution are deferred.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        ALTER TABLE assurance."CertificateReceipts" DROP CONSTRAINT "CK_CertificateReceipt_Resource";
        ALTER TABLE assurance."CertificateReceipts" ADD CONSTRAINT "CK_CertificateReceipt_Resource" CHECK
          (("Action" IN ('CertificateSave','CertificateAssertion') AND "RenewalId" IS NULL AND "ResourceId"="CertificateId") OR
           ("Action"='RenewalSave' AND "RenewalId" IS NOT NULL AND "ResourceId"="RenewalId"));
        CREATE TABLE assurance."CertificateAssertionHeads" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"CertificateId" uuid NOT NULL,"Revision" bigint NOT NULL,
          CONSTRAINT "PK_CertificateAssertionHeads" PRIMARY KEY ("TenantId","CertificateId"),
          CONSTRAINT "AK_CertificateAssertionHead_Scope" UNIQUE ("TenantId","VesselId","CertificateId"),
          CONSTRAINT "FK_CertificateAssertionHead_Certificate" FOREIGN KEY ("TenantId","VesselId","CertificateId") REFERENCES assurance."Certificates" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_CertificateAssertionHead_Revision" CHECK ("Revision">0)
        );
        CREATE TABLE assurance."CertificateAssertions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"CertificateId" uuid NOT NULL,"Id" uuid NOT NULL,
          "Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,"CertificateRevision" bigint NOT NULL,"CertificateVersion" bigint NOT NULL,
          "FileId" uuid NOT NULL,"FileRevision" bigint NOT NULL,"Checksum" varchar(64) NOT NULL,"State" varchar(32) NOT NULL,
          "ActorId" uuid NOT NULL,"RoleId" uuid NOT NULL,"AuthorityId" uuid NOT NULL,"AuthorityHash" varchar(64) NOT NULL,"AuthorityBindingHash" varchar(64) NOT NULL,
          "AuthorityJson" text NOT NULL,"SourceVersion" varchar(192) NOT NULL,"Rationale" varchar(2000) NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_CertificateAssertions" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "FK_CertificateAssertion_Head" FOREIGN KEY ("TenantId","VesselId","CertificateId") REFERENCES assurance."CertificateAssertionHeads" ("TenantId","VesselId","CertificateId") ON DELETE RESTRICT,
          CONSTRAINT "FK_CertificateAssertion_Revision" FOREIGN KEY ("TenantId","VesselId","CertificateId","CertificateRevision") REFERENCES assurance."CertificateRevisions" ("TenantId","VesselId","CertificateId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "FK_CertificateAssertion_File" FOREIGN KEY ("TenantId","VesselId","FileId","FileRevision","Checksum") REFERENCES files."StoredFiles" ("TenantId","VesselId","Id","Revision","Checksum") ON DELETE RESTRICT,
          CONSTRAINT "CK_CertificateAssertion_State" CHECK ("Revision">0 AND "RecordVersion">0 AND "CertificateRevision">0 AND "CertificateVersion">0 AND "State" IN ('Verified','Not Verified','Suspicious','Suspended / Revoked'))
        );
        CREATE UNIQUE INDEX "UX_CertificateAssertion_Revision" ON assurance."CertificateAssertions" ("TenantId","CertificateId","Revision");
        CREATE INDEX "IX_CertificateAssertion_Certificate" ON assurance."CertificateAssertions" ("TenantId","VesselId","CertificateId","CertificateRevision");
        CREATE INDEX "IX_CertificateAssertion_File" ON assurance."CertificateAssertions" ("TenantId","VesselId","FileId","FileRevision","Checksum");
        CREATE TRIGGER "CertificateAssertionHead_Guard" BEFORE UPDATE OR DELETE ON assurance."CertificateAssertionHeads" FOR EACH ROW EXECUTE FUNCTION assurance.guard_certificate_head();
        CREATE TRIGGER "CertificateAssertionHead_NoTruncate" BEFORE TRUNCATE ON assurance."CertificateAssertionHeads" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateAssertion_Immutable" BEFORE UPDATE OR DELETE ON assurance."CertificateAssertions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateAssertion_NoTruncate" BEFORE TRUNCATE ON assurance."CertificateAssertions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN
          IF EXISTS(SELECT 1 FROM assurance."CertificateAssertions") OR EXISTS(SELECT 1 FROM assurance."CertificateReceipts" WHERE "Action"='CertificateAssertion')
            THEN RAISE EXCEPTION 'Recorded certificate assertions must be retained'; END IF;
        END $$;
        DROP TABLE assurance."CertificateAssertions";DROP TABLE assurance."CertificateAssertionHeads";
        ALTER TABLE assurance."CertificateReceipts" DROP CONSTRAINT "CK_CertificateReceipt_Resource";
        ALTER TABLE assurance."CertificateReceipts" ADD CONSTRAINT "CK_CertificateReceipt_Resource" CHECK
          (("Action"='CertificateSave' AND "RenewalId" IS NULL AND "ResourceId"="CertificateId") OR
           ("Action"='RenewalSave' AND "RenewalId" IS NOT NULL AND "ResourceId"="RenewalId"));
        """);
}
