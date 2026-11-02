using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Additive authoring only. Generated designer/snapshot and actual application are DEFERRED.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102180000_RegulatoryQualification")]
public sealed class RegulatoryQualification : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        ALTER TABLE core."HumanReviewEvents" ADD COLUMN "DecisionType" text NULL;
        ALTER TABLE core."ControlEvidenceRevisions" DROP CONSTRAINT "CK_ControlEvidenceRevisions";
        ALTER TABLE core."ControlEvidenceRevisions" ADD CONSTRAINT "CK_ControlEvidenceRevisions" CHECK ("Revision" > 0 AND "Lifecycle" IN ('DRAFT','SUBMITTED','COMPLETED') AND
          (("FileId" IS NULL AND "FileRevision" IS NULL AND "Checksum" IS NULL) OR ("Kind" = 'Evidence' AND "FileId" IS NOT NULL AND "FileRevision" IS NOT NULL AND "FileRevision" > 0 AND "Checksum" IS NOT NULL)));
        CREATE TABLE core."RegulatoryQualificationHeads" (
          "TenantId" uuid NOT NULL, "RuleId" uuid NOT NULL, "Revision" bigint NOT NULL,
          PRIMARY KEY ("TenantId","RuleId"),
          FOREIGN KEY ("TenantId","RuleId") REFERENCES core."RuleDrafts"("TenantId","Id") ON DELETE RESTRICT);
        CREATE TABLE core."RegulatoryQualificationEvents" (
          "TenantId" uuid NOT NULL, "RuleId" uuid NOT NULL, "Revision" bigint NOT NULL, "RuleRevision" bigint NOT NULL,
          "State" text NOT NULL, "ContentJson" text NOT NULL, "SourceHash" text NOT NULL, "Hash" text NOT NULL, "PriorHash" text NULL,
          "SupersedingRuleId" uuid NULL, "SupersedingRuleRevision" bigint NULL,
          "AuthorId" uuid NOT NULL, "ActorId" uuid NOT NULL, "AuthorityId" uuid NOT NULL, "RoleId" uuid NOT NULL,
          "AuthorityHash" text NOT NULL, "ContractJson" text NOT NULL, "PolicyVersion" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId","RuleId","Revision"),
          CONSTRAINT "CK_RegulatoryQualificationEvents" CHECK ("Revision" > 0 AND "State" IN ('DRAFT','IN_REVIEW','ADOPTED','AWAITING','FUTURE_EFFECTIVE','IN_FORCE','SUPERSEDED')),
          FOREIGN KEY ("TenantId","RuleId") REFERENCES core."RegulatoryQualificationHeads"("TenantId","RuleId") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","RuleId","RuleRevision") REFERENCES core."RuleRevisions"("TenantId","Id","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","SupersedingRuleId","SupersedingRuleRevision") REFERENCES core."RuleRevisions"("TenantId","Id","Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."RegulatoryQualificationEvents"("TenantId","RuleId","RuleRevision");
        CREATE INDEX ON core."RegulatoryQualificationEvents"("TenantId","SupersedingRuleId","SupersedingRuleRevision");
        CREATE TABLE core."RegulatoryFactAttestations" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "RuleId" uuid NOT NULL, "QualificationRevision" bigint NOT NULL,
          "VesselId" uuid NOT NULL, "ProfileRevision" bigint NOT NULL, "FieldsJson" text NOT NULL, "ActorId" uuid NOT NULL,
          "RoleId" uuid NOT NULL, "AuthorityId" uuid NOT NULL, "AuthorityHash" text NOT NULL, "Hash" text NOT NULL,
          "SavedAt" timestamptz NOT NULL, "ValidUntil" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId","Id"),
          FOREIGN KEY ("TenantId","RuleId","QualificationRevision") REFERENCES core."RegulatoryQualificationEvents"("TenantId","RuleId","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","ProfileRevision") REFERENCES core."RegulatoryDraftRevisions"("TenantId","VesselId","Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."RegulatoryFactAttestations"("TenantId","RuleId","QualificationRevision");
        CREATE INDEX ON core."RegulatoryFactAttestations"("TenantId","VesselId","ProfileRevision");
        CREATE TABLE core."EvaluationQualifications" (
          "TenantId" uuid NOT NULL, "EvaluationId" uuid NOT NULL, "RuleId" uuid NOT NULL,
          "QualificationRevision" bigint NOT NULL, "AttestationId" uuid NULL,
          PRIMARY KEY ("TenantId","EvaluationId","RuleId"),
          FOREIGN KEY ("TenantId","EvaluationId") REFERENCES core."ApplicabilityEvaluations"("TenantId","Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","RuleId","QualificationRevision") REFERENCES core."RegulatoryQualificationEvents"("TenantId","RuleId","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","AttestationId") REFERENCES core."RegulatoryFactAttestations"("TenantId","Id") ON DELETE RESTRICT);
        CREATE INDEX ON core."EvaluationQualifications"("TenantId","RuleId","QualificationRevision");
        CREATE INDEX ON core."EvaluationQualifications"("TenantId","AttestationId");
        CREATE TABLE core."RegulatoryQualificationReceipts" (
          "TenantId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL, "RuleId" uuid NOT NULL,
          "Fingerprint" text NOT NULL, "AuthorityHash" text NOT NULL, "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId","ActorId","Key"),
          FOREIGN KEY ("TenantId","RuleId") REFERENCES core."RuleDrafts"("TenantId","Id") ON DELETE RESTRICT);
        CREATE INDEX ON core."RegulatoryQualificationReceipts"("TenantId","RuleId");
        ALTER TABLE core."ApplicabilityReceipts" ADD COLUMN "RuleResourceId" uuid NULL, ADD COLUMN "EvaluationResourceId" uuid NULL;
        ALTER TABLE core."ApplicabilityReceipts" DROP CONSTRAINT "ApplicabilityReceipts_TenantId_ResourceId_fkey";
        ALTER TABLE core."ApplicabilityReceipts" ADD FOREIGN KEY ("TenantId","RuleResourceId") REFERENCES core."RuleDrafts"("TenantId","Id") ON DELETE RESTRICT;
        ALTER TABLE core."ApplicabilityReceipts" ADD FOREIGN KEY ("TenantId","EvaluationResourceId") REFERENCES core."ApplicabilityEvaluations"("TenantId","Id") ON DELETE RESTRICT;
        CREATE INDEX ON core."ApplicabilityReceipts"("TenantId","RuleResourceId");
        CREATE INDEX ON core."ApplicabilityReceipts"("TenantId","EvaluationResourceId");
        CREATE TRIGGER regulatory_qualification_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RegulatoryQualificationEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER regulatory_fact_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RegulatoryFactAttestations" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER evaluation_qualification_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."EvaluationQualifications" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER regulatory_receipt_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RegulatoryQualificationReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Retain regulatory source and evaluation history; use a reviewed forward migration.");
}
