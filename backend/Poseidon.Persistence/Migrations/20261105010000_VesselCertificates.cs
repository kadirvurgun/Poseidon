using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261105010000_VesselCertificates")]
public sealed class VesselCertificates : Migration
{
    // Source authored only; no scaffold/model/migration execution has occurred.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE SCHEMA IF NOT EXISTS assurance;
        CREATE TABLE assurance."Certificates" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"Revision" bigint NOT NULL,
          CONSTRAINT "PK_Certificates" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_Certificates_TenantId_VesselId_Id" UNIQUE ("TenantId","VesselId","Id"),
          CONSTRAINT "FK_Certificates_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_VesselCertificate_Revision" CHECK ("Revision">0)
        );
        CREATE INDEX "IX_Certificates_TenantId_VesselId" ON assurance."Certificates" ("TenantId","VesselId");
        CREATE TABLE assurance."CertificateRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"CertificateId" uuid NOT NULL,"Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,
          "ContentJson" text NOT NULL,"Hash" varchar(64) NOT NULL,"SourceVersion" varchar(192) NOT NULL,"Provenance" varchar(32) NOT NULL,
          "FileId" uuid,"FileRevision" bigint,"Checksum" varchar(64),"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          "IssueDate" date,"ExpiryDate" date,"SurveyWindowStart" date,"SurveyWindowEnd" date,"SurveyWindowDeclared" boolean NOT NULL,
          CONSTRAINT "PK_CertificateRevisions" PRIMARY KEY ("TenantId","CertificateId","Revision"),
          CONSTRAINT "AK_CertificateRevision_Scope" UNIQUE ("TenantId","VesselId","CertificateId","Revision"),
          CONSTRAINT "FK_CertificateRevision_Head" FOREIGN KEY ("TenantId","VesselId","CertificateId") REFERENCES assurance."Certificates" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_CertificateRevision_File" FOREIGN KEY ("TenantId","VesselId","FileId","FileRevision","Checksum") REFERENCES files."StoredFiles" ("TenantId","VesselId","Id","Revision","Checksum") ON DELETE RESTRICT,
          CONSTRAINT "CK_VesselCertificateRevision_Binding" CHECK ("Revision" > 0 AND "RecordVersion" > 0 AND (("FileId" IS NULL AND "FileRevision" IS NULL AND "Checksum" IS NULL) OR ("FileId" IS NOT NULL AND "FileRevision" > 0 AND "Checksum" IS NOT NULL)))
        );
        CREATE INDEX "IX_CertificateRevision_FilePin" ON assurance."CertificateRevisions" ("TenantId","VesselId","FileId","FileRevision","Checksum");
        CREATE TABLE assurance."Renewals" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"CertificateId" uuid NOT NULL,"Revision" bigint NOT NULL,
          CONSTRAINT "PK_Renewals" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_Renewals_TenantId_VesselId_CertificateId_Id" UNIQUE ("TenantId","VesselId","CertificateId","Id"),
          CONSTRAINT "FK_Renewal_Certificate" FOREIGN KEY ("TenantId","VesselId","CertificateId") REFERENCES assurance."Certificates" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_CertificateRenewal_Revision" CHECK ("Revision">0)
        );
        CREATE TABLE assurance."RenewalRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"RenewalId" uuid NOT NULL,"CertificateId" uuid NOT NULL,"CertificateRevision" bigint NOT NULL,"CertificateVersion" bigint NOT NULL,
          "Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,"ContentJson" text NOT NULL,"Hash" varchar(64) NOT NULL,"SourceVersion" varchar(192) NOT NULL,
          "NewCertificateId" uuid,"NewCertificateRevision" bigint,"NewCertificateVersion" bigint,"FileId" uuid,"FileRevision" bigint,"Checksum" varchar(64),"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_RenewalRevisions" PRIMARY KEY ("TenantId","RenewalId","Revision"),
          CONSTRAINT "FK_RenewalRevision_Head" FOREIGN KEY ("TenantId","VesselId","CertificateId","RenewalId") REFERENCES assurance."Renewals" ("TenantId","VesselId","CertificateId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_RenewalRevision_Original" FOREIGN KEY ("TenantId","VesselId","CertificateId","CertificateRevision") REFERENCES assurance."CertificateRevisions" ("TenantId","VesselId","CertificateId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "FK_RenewalRevision_Replacement" FOREIGN KEY ("TenantId","VesselId","NewCertificateId","NewCertificateRevision") REFERENCES assurance."CertificateRevisions" ("TenantId","VesselId","CertificateId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "FK_RenewalRevision_File" FOREIGN KEY ("TenantId","VesselId","FileId","FileRevision","Checksum") REFERENCES files."StoredFiles" ("TenantId","VesselId","Id","Revision","Checksum") ON DELETE RESTRICT,
          CONSTRAINT "CK_RenewalRevision_Binding" CHECK ("Revision" > 0 AND "RecordVersion" > 0 AND "CertificateRevision" > 0 AND "CertificateVersion" > 0 AND (("NewCertificateId" IS NULL AND "NewCertificateRevision" IS NULL AND "NewCertificateVersion" IS NULL) OR ("NewCertificateId" IS NOT NULL AND "NewCertificateRevision" > 0 AND "NewCertificateVersion" > 0)) AND (("FileId" IS NULL AND "FileRevision" IS NULL AND "Checksum" IS NULL) OR ("FileId" IS NOT NULL AND "FileRevision" > 0 AND "Checksum" IS NOT NULL)))
        );
        CREATE INDEX "IX_RenewalRevision_Head" ON assurance."RenewalRevisions" ("TenantId","VesselId","CertificateId","RenewalId");
        CREATE INDEX "IX_RenewalRevision_Original" ON assurance."RenewalRevisions" ("TenantId","VesselId","CertificateId","CertificateRevision");
        CREATE INDEX "IX_RenewalRevision_Replacement" ON assurance."RenewalRevisions" ("TenantId","VesselId","NewCertificateId","NewCertificateRevision");
        CREATE INDEX "IX_RenewalRevision_FilePin" ON assurance."RenewalRevisions" ("TenantId","VesselId","FileId","FileRevision","Checksum");
        CREATE TABLE assurance."CertificateReceipts" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"ActorId" uuid NOT NULL,"Key" varchar(80) NOT NULL,"ResourceId" uuid NOT NULL,"CertificateId" uuid NOT NULL,"RenewalId" uuid,
          "Action" varchar(32) NOT NULL,"Fingerprint" varchar(64) NOT NULL,"OutcomeJson" text NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_CertificateReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_CertificateReceipt_Certificate" FOREIGN KEY ("TenantId","VesselId","CertificateId") REFERENCES assurance."Certificates" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_CertificateReceipt_Renewal" FOREIGN KEY ("TenantId","VesselId","CertificateId","RenewalId") REFERENCES assurance."Renewals" ("TenantId","VesselId","CertificateId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_CertificateReceipt_Resource" CHECK (("Action"='CertificateSave' AND "RenewalId" IS NULL AND "ResourceId"="CertificateId") OR ("Action"='RenewalSave' AND "RenewalId" IS NOT NULL AND "ResourceId"="RenewalId"))
        );
        CREATE INDEX "IX_CertificateReceipt_Owner" ON assurance."CertificateReceipts" ("TenantId","VesselId","CertificateId","RenewalId");
        CREATE FUNCTION assurance.guard_certificate_head() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Certificate history cannot be removed'; END IF;
          IF (to_jsonb(OLD)-'Revision') IS DISTINCT FROM (to_jsonb(NEW)-'Revision') OR NEW."Revision" <> OLD."Revision"+1
            THEN RAISE EXCEPTION 'Immutable certificate identity'; END IF;RETURN NEW;
        END $$;
        CREATE TRIGGER "CertificateHead_Guard" BEFORE UPDATE OR DELETE ON assurance."Certificates" FOR EACH ROW EXECUTE FUNCTION assurance.guard_certificate_head();
        CREATE TRIGGER "RenewalHead_Guard" BEFORE UPDATE OR DELETE ON assurance."Renewals" FOR EACH ROW EXECUTE FUNCTION assurance.guard_certificate_head();
        CREATE TRIGGER "CertificateHead_NoTruncate" BEFORE TRUNCATE ON assurance."Certificates" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "RenewalHead_NoTruncate" BEFORE TRUNCATE ON assurance."Renewals" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateRevision_Immutable" BEFORE UPDATE OR DELETE ON assurance."CertificateRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateRevision_NoTruncate" BEFORE TRUNCATE ON assurance."CertificateRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "RenewalRevision_Immutable" BEFORE UPDATE OR DELETE ON assurance."RenewalRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "RenewalRevision_NoTruncate" BEFORE TRUNCATE ON assurance."RenewalRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateReceipt_Immutable" BEFORE UPDATE OR DELETE ON assurance."CertificateReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "CertificateReceipt_NoTruncate" BEFORE TRUNCATE ON assurance."CertificateReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS(SELECT 1 FROM assurance."Certificates") THEN RAISE EXCEPTION 'Certificate history must be retained'; END IF;END $$;
        DROP TABLE assurance."CertificateReceipts";DROP TABLE assurance."RenewalRevisions";DROP TABLE assurance."Renewals";
        DROP TABLE assurance."CertificateRevisions";DROP TABLE assurance."Certificates";DROP FUNCTION assurance.guard_certificate_head();
        """);
}
