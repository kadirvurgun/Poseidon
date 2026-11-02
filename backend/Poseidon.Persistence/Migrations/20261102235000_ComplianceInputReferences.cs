using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored, unapplied. Snapshot/designer parity belongs to the combined gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102235000_ComplianceInputReferences")]
public sealed class ComplianceInputReferences : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.AddColumn<bool>("IncludesProtectedInputs", "ApplicabilityEvaluations", "boolean", schema: "core", nullable: false, defaultValue: false);
        b.Sql("""
        ALTER TABLE core."OperationEquipmentBindings" DROP CONSTRAINT "CK_Owning_Document";
        ALTER TABLE core."OperationEquipmentBindings" ADD CONSTRAINT "CK_Owning_Document" CHECK
          ("Kind" = 'Evidence' AND "ServiceKind" = 'Evidence' AND "Revision" > 0 AND
           "DocumentKind" IN ('Operation','ServiceAuthorization','CargoPlan','WinchFitment','ClassPack','Msmd','CrewRoleAssessment'));
        ALTER TABLE crew."AssignmentRevisions" ADD CONSTRAINT "AK_AssignmentRevision_Vessel" UNIQUE ("TenantId","VesselId","AssignmentId","Revision");
        ALTER TABLE core."OperationEquipmentBindings" ADD COLUMN "AssignmentId" uuid NULL, ADD COLUMN "AssignmentRevision" bigint NULL, ADD COLUMN "PositionId" uuid NULL;
        ALTER TABLE core."OperationEquipmentBindings" ADD CONSTRAINT "FK_Compliance_Assignment" FOREIGN KEY ("TenantId","VesselId","AssignmentId","AssignmentRevision")
          REFERENCES crew."AssignmentRevisions"("TenantId","VesselId","AssignmentId","Revision") ON DELETE RESTRICT;
        ALTER TABLE core."OperationEquipmentBindings" ADD CONSTRAINT "FK_Compliance_Position" FOREIGN KEY ("TenantId","PositionId","VesselId")
          REFERENCES crew."Positions"("TenantId","Id","VesselId") ON DELETE RESTRICT;
        ALTER TABLE core."OperationEquipmentBindings" ADD CONSTRAINT "CK_Compliance_Assignment" CHECK
          (("AssignmentId" IS NULL AND "AssignmentRevision" IS NULL) OR ("AssignmentId" IS NOT NULL AND "AssignmentRevision" > 0));
        ALTER TABLE crew."CredentialRevisions" ADD CONSTRAINT "AK_CredentialRevision_Vessel" UNIQUE ("TenantId","VesselId","CredentialId","Revision");
        ALTER TABLE core."OperationEquipmentBindings" ADD COLUMN "CredentialId" uuid NULL, ADD COLUMN "CredentialRevision" bigint NULL;
        ALTER TABLE core."OperationEquipmentBindings" ADD CONSTRAINT "FK_Compliance_Credential" FOREIGN KEY ("TenantId","VesselId","CredentialId","CredentialRevision")
          REFERENCES crew."CredentialRevisions"("TenantId","VesselId","CredentialId","Revision") ON DELETE RESTRICT;
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","CredentialId","CredentialRevision");
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","AssignmentId","AssignmentRevision");
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","PositionId","VesselId");
        """);
        // Allow exact ordinary Evidence as well as typed owning Evidence. The
        // previous rows remain unchanged and retain their normal receipts.
        b.Sql("""
        DO $$ DECLARE fk text; BEGIN
          FOR fk IN SELECT c.conname FROM pg_constraint c WHERE c.conrelid = 'core."EvaluationOwningReferences"'::regclass
            AND c.confrelid = 'core."OperationEquipmentBindings"'::regclass AND c.contype = 'f'
          LOOP EXECUTE format('ALTER TABLE core."EvaluationOwningReferences" DROP CONSTRAINT %I', fk); END LOOP;
        END $$;
        ALTER TABLE core."EvaluationOwningReferences" ADD CONSTRAINT "FK_Evaluation_OwningEvidence" FOREIGN KEY ("TenantId","VesselId","Kind","Id","Revision")
          REFERENCES core."ControlEvidenceRevisions"("TenantId","VesselId","Kind","Id","Revision") ON DELETE RESTRICT;
        CREATE TABLE core."MsmdRoleBindings" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          "PositionId" uuid NOT NULL, "Count" integer NOT NULL, "SourceLocator" varchar(2000) NOT NULL,
          PRIMARY KEY ("TenantId","VesselId","Id","Revision","PositionId"), CONSTRAINT "CK_MsmdRole" CHECK ("Kind" = 'Evidence' AND "Revision" > 0 AND "Count" BETWEEN 1 AND 1000),
          FOREIGN KEY ("TenantId","VesselId","Kind","Id","Revision") REFERENCES core."ControlEvidenceRevisions"("TenantId","VesselId","Kind","Id","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","PositionId","VesselId") REFERENCES crew."Positions"("TenantId","Id","VesselId") ON DELETE RESTRICT);
        CREATE INDEX ON core."MsmdRoleBindings"("TenantId","PositionId","VesselId");
        CREATE INDEX ON core."MsmdRoleBindings"("TenantId","VesselId","Kind","Id","Revision");
        CREATE TRIGGER msmd_role_append_only BEFORE UPDATE OR DELETE OR TRUNCATE ON core."MsmdRoleBindings" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TABLE core."EvaluationUpstreamReferences" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "EvaluationId" uuid NOT NULL, "UpstreamId" uuid NOT NULL, "RuleId" uuid NOT NULL, "RuleRevision" bigint NOT NULL,
          PRIMARY KEY ("TenantId","EvaluationId"), CHECK ("EvaluationId" <> "UpstreamId" AND "RuleRevision" > 0),
          FOREIGN KEY ("TenantId","VesselId","EvaluationId") REFERENCES core."ApplicabilityEvaluations"("TenantId","VesselId","Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","UpstreamId") REFERENCES core."ApplicabilityEvaluations"("TenantId","VesselId","Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","RuleId","RuleRevision") REFERENCES core."RuleRevisions"("TenantId","Id","Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."EvaluationUpstreamReferences"("TenantId","VesselId","UpstreamId");
        CREATE INDEX ON core."EvaluationUpstreamReferences"("TenantId","RuleId","RuleRevision");
        CREATE TRIGGER evaluation_upstream_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
          ON core."EvaluationUpstreamReferences" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Immutable compliance lineage requires a controlled forward migration.");
}
