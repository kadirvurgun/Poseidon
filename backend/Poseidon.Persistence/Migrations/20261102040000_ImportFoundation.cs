using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;
// Authored DDL only; execution/model parity deferred to the larger foundation gate.
public partial class ImportFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE SCHEMA IF NOT EXISTS integration;
CREATE TABLE crew."Credentials" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"AssignmentId" uuid NOT NULL,
"PersonId" uuid NOT NULL,
"CertificateTypeReference" text,
"CertificateNumber" text,
"Issuer" text,
"IssuingCountryNumeric" character varying(1024),
"IssueDate" date,
"ExpiryDate" date,
"Limitations" text,
"EndorsementRequired" boolean,
"EndorsementNumber" text,
"EndorsementExpiry" date,
"StcwReference" text,
"TankerRelevance" text,
"VesselTypeScope" text,
"DocumentReference" uuid,
"RenewalLeadDaysNumeric" character varying(1024),
"SourceSystem" text,
"Notes" text,
"VerificationState" text NOT NULL,
"AlertState" text NOT NULL,
"SyncState" text NOT NULL,
"Status" text NOT NULL,
"Provenance" text NOT NULL,
"Revision" bigint NOT NULL,
CONSTRAINT "PK_Credentials" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_Credentials_Scope" UNIQUE ("TenantId", "Id", "VesselId"),
CONSTRAINT "FK_Credentials_Assignments_Scope" FOREIGN KEY ("TenantId", "AssignmentId", "VesselId", "PersonId") REFERENCES crew."Assignments" ("TenantId", "Id", "VesselId", "PersonId") ON DELETE RESTRICT,
CONSTRAINT "FK_Credentials_Documents_Scope_Document" FOREIGN KEY ("TenantId", "DocumentReference", "VesselId") REFERENCES files."Documents" ("TenantId", "Id", "VesselId") ON DELETE RESTRICT
);
CREATE INDEX "IX_Credentials_TenantId_AssignmentId_VesselId_PersonId" ON crew."Credentials" ("TenantId", "AssignmentId", "VesselId", "PersonId");
CREATE INDEX "IX_Credentials_TenantId_DocumentReference_VesselId" ON crew."Credentials" ("TenantId", "DocumentReference", "VesselId");
CREATE TABLE crew."CredentialRevisions" (
"TenantId" uuid NOT NULL,
"CredentialId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"Revision" bigint NOT NULL,
"SavedAt" timestamp with time zone NOT NULL,
"Provenance" text NOT NULL,
CONSTRAINT "PK_CredentialRevisions" PRIMARY KEY ("TenantId", "CredentialId", "Revision"),
CONSTRAINT "FK_CredentialRevisions_Credentials_Scope" FOREIGN KEY ("TenantId", "CredentialId", "VesselId") REFERENCES crew."Credentials" ("TenantId", "Id", "VesselId") ON DELETE RESTRICT
);
CREATE INDEX "IX_CredentialRevisions_TenantId_CredentialId_VesselId" ON crew."CredentialRevisions" ("TenantId", "CredentialId", "VesselId");
CREATE TABLE integration."ImportBatches" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"ActorId" uuid NOT NULL,
"AssignmentId" uuid,
"SessionHash" text NOT NULL,
"Category" text NOT NULL,
"TemplateVersion" text NOT NULL,
"TemplateFingerprint" text NOT NULL,
"SourceHash" text NOT NULL,
"SourceName" text NOT NULL,
"SourceLocator" text NOT NULL,
"TableJson" text NOT NULL,
"BindingsJson" text NOT NULL,
"Revision" bigint NOT NULL,
"State" text NOT NULL,
"RowCount" integer NOT NULL,
"CreatedAt" timestamp with time zone NOT NULL,
"CommittedAt" timestamp with time zone,
"CommittedPreviewId" uuid,
"ReviewerState" text NOT NULL,
CONSTRAINT "PK_ImportBatches" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_ImportBatches_Scope" UNIQUE ("TenantId", "Id", "VesselId"),
CONSTRAINT "FK_ImportBatches_Vessels_Scope" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_ImportBatches_TenantId_VesselId" ON integration."ImportBatches" ("TenantId", "VesselId");
CREATE TABLE integration."ImportPreviews" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"BatchId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"BatchRevision" bigint NOT NULL,
"TemplateFingerprint" text NOT NULL,
"BindingsJson" text NOT NULL,
"TotalRows" integer NOT NULL,
"ValidRows" integer NOT NULL,
"RejectedRows" integer NOT NULL,
"CreatedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_ImportPreviews" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_ImportPreviews_Scope" UNIQUE ("TenantId", "Id", "VesselId"),
CONSTRAINT "FK_ImportPreviews_ImportBatches_Scope" FOREIGN KEY ("TenantId", "BatchId", "VesselId") REFERENCES integration."ImportBatches" ("TenantId", "Id", "VesselId") ON DELETE RESTRICT
);
CREATE INDEX "IX_ImportPreviews_TenantId_BatchId_VesselId" ON integration."ImportPreviews" ("TenantId", "BatchId", "VesselId");
CREATE TABLE integration."ImportRowResults" (
"TenantId" uuid NOT NULL,
"PreviewId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"RowNumber" integer NOT NULL,
"Outcome" text NOT NULL,
"IssuesJson" text NOT NULL,
CONSTRAINT "PK_ImportRowResults" PRIMARY KEY ("TenantId", "PreviewId", "RowNumber"),
CONSTRAINT "FK_ImportRowResults_ImportPreviews_Scope" FOREIGN KEY ("TenantId", "PreviewId", "VesselId") REFERENCES integration."ImportPreviews" ("TenantId", "Id", "VesselId") ON DELETE RESTRICT
);
CREATE INDEX "IX_ImportRowResults_TenantId_PreviewId_VesselId" ON integration."ImportRowResults" ("TenantId", "PreviewId", "VesselId");
CREATE INDEX "IX_ImportBatches_TenantId_VesselId_ActorId_CreatedAt" ON integration."ImportBatches" ("TenantId", "VesselId", "ActorId", "CreatedAt");
ALTER TABLE crew."Credentials" ADD CONSTRAINT "CK_Credential_IssuingCountryNumeric" CHECK ("IssuingCountryNumeric" IS NULL OR "IssuingCountryNumeric" ~ '^(0|[1-9][0-9]*)(\.[0-9]+)?$');
ALTER TABLE crew."Credentials" ADD CONSTRAINT "CK_Credential_RenewalLeadDaysNumeric" CHECK ("RenewalLeadDaysNumeric" IS NULL OR "RenewalLeadDaysNumeric" ~ '^(0|[1-9][0-9]*)(\.[0-9]+)?$');
CREATE FUNCTION integration.reject_import_observation_change() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'immutable observation'; END; $$;
CREATE TRIGGER immutable_observation BEFORE UPDATE OR DELETE ON crew."CredentialRevisions" FOR EACH ROW EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER immutable_truncate BEFORE TRUNCATE ON crew."CredentialRevisions" EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER immutable_observation BEFORE UPDATE OR DELETE ON integration."ImportPreviews" FOR EACH ROW EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER immutable_truncate BEFORE TRUNCATE ON integration."ImportPreviews" EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER immutable_observation BEFORE UPDATE OR DELETE ON integration."ImportRowResults" FOR EACH ROW EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE TRIGGER immutable_truncate BEFORE TRUNCATE ON integration."ImportRowResults" EXECUTE FUNCTION integration.reject_import_observation_change();
CREATE FUNCTION integration.guard_import_source() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
IF (NEW."TenantId",NEW."VesselId",NEW."ActorId",NEW."SessionHash",NEW."AssignmentId",NEW."Category",NEW."TemplateVersion",NEW."TemplateFingerprint",NEW."SourceHash",NEW."TableJson",NEW."SourceName",NEW."SourceLocator") IS DISTINCT FROM
(OLD."TenantId",OLD."VesselId",OLD."ActorId",OLD."SessionHash",OLD."AssignmentId",OLD."Category",OLD."TemplateVersion",OLD."TemplateFingerprint",OLD."SourceHash",OLD."TableJson",OLD."SourceName",OLD."SourceLocator") OR OLD."State" = 'Committed' THEN RAISE EXCEPTION 'immutable import source'; END IF; RETURN NEW; END; $$;
CREATE TRIGGER import_source_guard BEFORE UPDATE ON integration."ImportBatches" FOR EACH ROW EXECUTE FUNCTION integration.guard_import_source();
""");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
DROP TABLE integration."ImportRowResults";
DROP TABLE integration."ImportPreviews";
DROP TABLE integration."ImportBatches";
DROP TABLE crew."CredentialRevisions";
DROP TABLE crew."Credentials";
DROP FUNCTION integration.guard_import_source();
DROP FUNCTION integration.reject_import_observation_change();
""");
}
