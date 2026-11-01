using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored DDL. Actual scaffold/designer/snapshot parity is DEFERRED to the bundle.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102150000_ControlEvidence")]
public sealed class ControlEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE files."StoredFiles" ADD CONSTRAINT "AK_StoredFiles_Evidence" UNIQUE ("TenantId", "VesselId", "Id", "Revision", "Checksum");
        CREATE TABLE core."ControlEvidenceHeads" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Kind", "Id"),
          CONSTRAINT "CK_ControlEvidenceHeads" CHECK ("Kind" IN ('Control','Evidence') AND "Revision" > 0),
          FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels"("TenantId", "Id") ON DELETE RESTRICT);
        CREATE TABLE core."ControlEvidenceRevisions" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "ContentJson" text NOT NULL, "Hash" text NOT NULL, "FileId" uuid NULL, "FileRevision" bigint NULL, "Checksum" varchar(64) NULL,
          "CriticalDeclaration" boolean NULL, "ActorId" uuid NULL, "RoleContext" text NULL, "ReasonCode" text NOT NULL, "Lifecycle" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Kind", "Id", "Revision"),
          CONSTRAINT "CK_ControlEvidenceRevisions" CHECK ("Revision" > 0 AND "Lifecycle" = 'DRAFT' AND
            (("FileId" IS NULL AND "FileRevision" IS NULL AND "Checksum" IS NULL) OR
             ("Kind" = 'Evidence' AND "FileId" IS NOT NULL AND "FileRevision" IS NOT NULL AND "FileRevision" > 0 AND "Checksum" IS NOT NULL))),
          FOREIGN KEY ("TenantId", "VesselId", "Kind", "Id") REFERENCES core."ControlEvidenceHeads"("TenantId", "VesselId", "Kind", "Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "FileId", "FileRevision", "Checksum") REFERENCES files."StoredFiles"("TenantId", "VesselId", "Id", "Revision", "Checksum") ON DELETE RESTRICT);
        CREATE TABLE core."ControlEvidenceRequirements" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "RequirementId" uuid NOT NULL, "RequirementRevision" bigint NOT NULL, "RequirementKind" varchar(16) NOT NULL,
          "SourceId" uuid NOT NULL, "SourceRevision" bigint NOT NULL, "SourceKind" varchar(16) NOT NULL, "RuleId" uuid NULL, "RuleRevision" bigint NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Kind", "Id", "Revision", "RequirementId", "RequirementRevision"),
          CONSTRAINT "CK_ControlEvidenceRequirements" CHECK ("RequirementKind" = 'Requirement' AND "SourceKind" = 'Source' AND "RequirementRevision" > 0 AND "SourceRevision" > 0 AND
            (("RuleId" IS NULL AND "RuleRevision" IS NULL) OR ("RuleId" IS NOT NULL AND "RuleRevision" IS NOT NULL AND "RuleRevision" > 0))),
          FOREIGN KEY ("TenantId", "VesselId", "Kind", "Id", "Revision") REFERENCES core."ControlEvidenceRevisions"("TenantId", "VesselId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "RequirementKind", "RequirementId", "RequirementRevision") REFERENCES core."RegisterRevisions"("TenantId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "SourceKind", "SourceId", "SourceRevision") REFERENCES core."RegisterRevisions"("TenantId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "RuleId", "RuleRevision") REFERENCES core."RuleRevisions"("TenantId", "Id", "Revision") ON DELETE RESTRICT);
        CREATE TABLE core."ControlEvidenceEdges" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "TargetKind" varchar(16) NOT NULL, "TargetId" uuid NOT NULL, "TargetRevision" bigint NOT NULL, "TargetVersion" bigint NOT NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Kind", "Id", "Revision", "TargetKind", "TargetId", "TargetRevision"),
          CONSTRAINT "CK_ControlEvidenceEdges" CHECK ("Kind" = 'Evidence' AND "TargetKind" IN ('Control','Evidence') AND "TargetRevision" > 0 AND "Id" <> "TargetId"),
          FOREIGN KEY ("TenantId", "VesselId", "Kind", "Id", "Revision") REFERENCES core."ControlEvidenceRevisions"("TenantId", "VesselId", "Kind", "Id", "Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "TargetKind", "TargetId", "TargetRevision") REFERENCES core."ControlEvidenceRevisions"("TenantId", "VesselId", "Kind", "Id", "Revision") ON DELETE RESTRICT);
        CREATE TABLE core."ControlEvidenceReceipts" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL, "Kind" varchar(16) NOT NULL,
          "ResourceId" uuid NOT NULL, "Fingerprint" text NOT NULL, "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "ActorId", "Key"),
          FOREIGN KEY ("TenantId", "VesselId", "Kind", "ResourceId") REFERENCES core."ControlEvidenceHeads"("TenantId", "VesselId", "Kind", "Id") ON DELETE RESTRICT);
        CREATE INDEX ON core."ControlEvidenceRevisions"("TenantId", "VesselId", "FileId", "FileRevision", "Checksum");
        CREATE INDEX ON core."ControlEvidenceRequirements"("TenantId", "RequirementKind", "RequirementId", "RequirementRevision");
        CREATE INDEX ON core."ControlEvidenceRequirements"("TenantId", "SourceKind", "SourceId", "SourceRevision");
        CREATE INDEX ON core."ControlEvidenceRequirements"("TenantId", "RuleId", "RuleRevision");
        CREATE INDEX ON core."ControlEvidenceEdges"("TenantId", "VesselId", "TargetKind", "TargetId", "TargetRevision");
        CREATE INDEX ON core."ControlEvidenceReceipts"("TenantId", "VesselId", "Kind", "ResourceId");
        CREATE TRIGGER control_evidence_revisions_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ControlEvidenceRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER control_evidence_requirements_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ControlEvidenceRequirements" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER control_evidence_edges_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ControlEvidenceEdges" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER control_evidence_receipts_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."ControlEvidenceReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE core."ControlEvidenceReceipts";
        DROP TABLE core."ControlEvidenceEdges";
        DROP TABLE core."ControlEvidenceRequirements";
        DROP TABLE core."ControlEvidenceRevisions";
        DROP TABLE core."ControlEvidenceHeads";
        ALTER TABLE files."StoredFiles" DROP CONSTRAINT "AK_StoredFiles_Evidence";
        """);
}
