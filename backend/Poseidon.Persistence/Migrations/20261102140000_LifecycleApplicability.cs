using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED. DDL only; generation/model parity/application DEFERRED.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102140000_LifecycleApplicability")]
public sealed class LifecycleApplicability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE core."RuleDrafts" (
          "TenantId" uuid NOT NULL REFERENCES core."Tenants"("Id") ON DELETE RESTRICT,
          "Id" uuid NOT NULL, "Revision" bigint NOT NULL CONSTRAINT "CK_RuleDrafts_Revision" CHECK ("Revision" > 0),
          PRIMARY KEY ("TenantId", "Id"));
        CREATE TABLE core."RuleRevisions" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "Contract" text NOT NULL, "RuleJson" text NULL, "Hash" text NOT NULL,
          "SourceKind" varchar(16) NULL, "SourceId" uuid NULL, "SourceRevision" bigint NULL,
          "RequirementKind" varchar(16) NULL, "RequirementId" uuid NULL, "RequirementRevision" bigint NULL,
          "ActorId" uuid NOT NULL, "RoleContext" text NOT NULL, "ReasonCode" text NOT NULL,
          "RequestedLifecycle" text NOT NULL, "Lifecycle" text NOT NULL,
          "ReviewStatus" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "Id", "Revision"),
          FOREIGN KEY ("TenantId", "Id") REFERENCES core."RuleDrafts"("TenantId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "SourceKind", "SourceId", "SourceRevision") REFERENCES core."RegisterRevisions"("TenantId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "RequirementKind", "RequirementId", "RequirementRevision") REFERENCES core."RegisterRevisions"("TenantId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_RuleRevisions_Admission" CHECK (
            "Revision" > 0 AND "Lifecycle" = 'DRAFT' AND "ReviewStatus" IN ('NOT_REQUESTED','BLOCKED_SOURCE_ADMISSION') AND
            (("SourceKind" IS NULL AND "SourceId" IS NULL AND "SourceRevision" IS NULL) OR
              ("SourceKind" IS NOT NULL AND "SourceKind" = 'Source' AND "SourceId" IS NOT NULL AND "SourceRevision" IS NOT NULL AND "SourceRevision" > 0)) AND
            (("RequirementKind" IS NULL AND "RequirementId" IS NULL AND "RequirementRevision" IS NULL) OR
              ("RequirementKind" IS NOT NULL AND "RequirementKind" = 'Requirement' AND "RequirementId" IS NOT NULL AND "RequirementRevision" IS NOT NULL AND "RequirementRevision" > 0))));
        CREATE INDEX ON core."RuleRevisions"("TenantId", "SourceKind", "SourceId", "SourceRevision");
        CREATE INDEX ON core."RuleRevisions"("TenantId", "RequirementKind", "RequirementId", "RequirementRevision");
        CREATE TABLE core."ApplicabilityEvaluations" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "VesselId" uuid NOT NULL, "ProfileRevision" bigint NULL,
          "ActorId" uuid NOT NULL, "SavedAt" timestamptz NOT NULL, "AsOf" timestamptz NOT NULL,
          "AdapterVersion" text NOT NULL, "SnapshotJson" text NOT NULL, "SnapshotHash" text NOT NULL,
          "ResultJson" text NOT NULL, "RuleSetHash" text NOT NULL,
          PRIMARY KEY ("TenantId", "Id"),
          FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels"("TenantId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "ProfileRevision") REFERENCES core."RegulatoryDraftRevisions"("TenantId", "VesselId", "Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."ApplicabilityEvaluations"("TenantId", "VesselId", "ProfileRevision");
        CREATE TABLE core."EvaluationRules" (
          "TenantId" uuid NOT NULL, "EvaluationId" uuid NOT NULL, "RuleId" uuid NOT NULL, "RuleRevision" bigint NOT NULL,
          PRIMARY KEY ("TenantId", "EvaluationId", "RuleId"),
          FOREIGN KEY ("TenantId", "EvaluationId") REFERENCES core."ApplicabilityEvaluations"("TenantId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "RuleId", "RuleRevision") REFERENCES core."RuleRevisions"("TenantId", "Id", "Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."EvaluationRules"("TenantId", "RuleId", "RuleRevision");
        CREATE TABLE core."ApplicabilityReceipts" (
          "TenantId" uuid NOT NULL REFERENCES core."Tenants"("Id") ON DELETE RESTRICT,
          "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL, "Action" text NOT NULL,
          "Fingerprint" text NOT NULL, "ResourceId" uuid NOT NULL, "VesselId" uuid NULL,
          "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "ActorId", "Key"),
          FOREIGN KEY ("TenantId", "ResourceId") REFERENCES core."RuleDrafts"("TenantId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels"("TenantId", "Id") ON DELETE RESTRICT);
        CREATE INDEX ON core."ApplicabilityReceipts"("TenantId", "VesselId");
        CREATE INDEX ON core."ApplicabilityReceipts"("TenantId", "ResourceId");
        CREATE TRIGGER rule_revisions_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."RuleRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER evaluations_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ApplicabilityEvaluations" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER evaluation_rules_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."EvaluationRules" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER applicability_receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ApplicabilityReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE core."ApplicabilityReceipts";
        DROP TABLE core."EvaluationRules";
        DROP TABLE core."ApplicabilityEvaluations";
        DROP TABLE core."RuleRevisions";
        DROP TABLE core."RuleDrafts";
        """);
}
