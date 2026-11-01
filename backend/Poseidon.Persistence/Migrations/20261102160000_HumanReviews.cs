using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Authored only. Scaffold/designer/snapshot/parity and application DEFERRED.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102160000_HumanReviews")]
public sealed class HumanReviews : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        ALTER TABLE core."ApplicabilityEvaluations" ADD CONSTRAINT "AK_ApplicabilityEvaluations_TenantId_VesselId_Id" UNIQUE ("TenantId", "VesselId", "Id");
        CREATE TABLE core."HumanReviewHeads" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Id"),
          FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels"("TenantId", "Id") ON DELETE RESTRICT);
        CREATE TABLE core."HumanReviewEvents" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "Kind" text NOT NULL, "State" text NOT NULL, "EvidenceId" uuid NOT NULL, "EvidenceRevision" bigint NOT NULL, "EvidenceKind" varchar(16) NOT NULL,
          "ControlId" uuid NOT NULL, "ControlRevision" bigint NOT NULL, "ControlKind" varchar(16) NOT NULL, "EvaluationId" uuid NULL,
          "VerificationId" uuid NULL, "VerificationRevision" bigint NULL, "SubmitterId" uuid NOT NULL, "ActorId" uuid NOT NULL,
          "AuthorityId" uuid NOT NULL, "RoleId" uuid NOT NULL, "OperationId" uuid NOT NULL, "ReasonCode" text NOT NULL, "Rationale" text NOT NULL,
          "AuthorityHash" text NOT NULL, "ContextHash" text NOT NULL, "SavedAt" timestamptz NOT NULL, "ValidUntil" timestamptz NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Id", "Revision"),
          CONSTRAINT "CK_HumanReviewEvents" CHECK ("Revision" > 0 AND "EvidenceRevision" > 0 AND "ControlRevision" > 0 AND "EvidenceKind" = 'Evidence' AND "ControlKind" = 'Control' AND
            (("Kind" = 'Verification' AND "State" IN ('PENDING','VERIFIED','REJECTED','EXPIRED','SUPERSEDED') AND "VerificationId" IS NULL AND "VerificationRevision" IS NULL) OR
             ("Kind" = 'Decision' AND "State" IN ('PENDING','REJECTED') AND "VerificationId" IS NOT NULL AND "VerificationRevision" IS NOT NULL AND "VerificationRevision" > 0))),
          FOREIGN KEY ("TenantId", "VesselId", "Id") REFERENCES core."HumanReviewHeads"("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "EvidenceKind", "EvidenceId", "EvidenceRevision") REFERENCES core."ControlEvidenceRevisions"("TenantId", "VesselId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "ControlKind", "ControlId", "ControlRevision") REFERENCES core."ControlEvidenceRevisions"("TenantId", "VesselId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "EvaluationId") REFERENCES core."ApplicabilityEvaluations"("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "VerificationId", "VerificationRevision") REFERENCES core."HumanReviewEvents"("TenantId", "VesselId", "Id", "Revision") ON DELETE RESTRICT);
        CREATE TABLE core."HumanReviewReceipts" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL,
          "ResourceId" uuid NOT NULL, "Fingerprint" text NOT NULL, "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "ActorId", "Key"),
          FOREIGN KEY ("TenantId", "VesselId", "ResourceId") REFERENCES core."HumanReviewHeads"("TenantId", "VesselId", "Id") ON DELETE RESTRICT);
        CREATE INDEX ON core."HumanReviewEvents"("TenantId", "VesselId", "EvidenceId", "EvidenceRevision");
        CREATE INDEX ON core."HumanReviewEvents"("TenantId", "VesselId", "ControlId", "ControlRevision");
        CREATE INDEX ON core."HumanReviewEvents"("TenantId", "VesselId", "EvaluationId");
        CREATE INDEX ON core."HumanReviewEvents"("TenantId", "VesselId", "VerificationId", "VerificationRevision");
        CREATE INDEX ON core."HumanReviewReceipts"("TenantId", "VesselId", "ResourceId");
        CREATE TRIGGER human_review_events_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."HumanReviewEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER human_review_receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."HumanReviewReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TABLE core."HumanReviewReceipts";
        DROP TABLE core."HumanReviewEvents";
        DROP TABLE core."HumanReviewHeads";
        ALTER TABLE core."ApplicabilityEvaluations" DROP CONSTRAINT "AK_ApplicabilityEvaluations_TenantId_VesselId_Id";
        """);
}
