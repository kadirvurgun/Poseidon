using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Authoring only. Generated model parity and application are deferred to the bundle gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102234000_OperationEquipmentReferences")]
public sealed class OperationEquipmentReferences : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.AddColumn<bool>("IncludesOwningInputs", "ApplicabilityEvaluations", "boolean", schema: "core", nullable: false, defaultValue: false);
        b.AddColumn<bool>("IncludesOperationInputs", "ApplicabilityEvaluations", "boolean", schema: "core", nullable: false, defaultValue: false);
        b.Sql("""
        CREATE TABLE core."OperationEquipmentBindings" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(16) NOT NULL,
          "Id" uuid NOT NULL, "Revision" bigint NOT NULL, "DocumentKind" varchar(32) NOT NULL,
          "JobId" uuid NULL, "JobRevision" bigint NULL, "PlanRevision" bigint NULL,
          "AssetId" uuid NULL, "AssetRevision" bigint NULL, "ServiceKind" varchar(16) NOT NULL,
          "ServiceId" uuid NULL, "ServiceRevision" bigint NULL,
          PRIMARY KEY ("TenantId", "VesselId", "Kind", "Id", "Revision"),
          CONSTRAINT "CK_Owning_Document" CHECK ("Kind" = 'Evidence' AND "ServiceKind" = 'Evidence' AND "Revision" > 0 AND
            "DocumentKind" IN ('Operation','ServiceAuthorization','CargoPlan','WinchFitment')),
          CONSTRAINT "CK_Owning_Job" CHECK (("JobId" IS NULL AND "JobRevision" IS NULL AND "PlanRevision" IS NULL) OR
            ("JobId" IS NOT NULL AND "JobRevision" IS NOT NULL AND "JobRevision" > 0 AND ("PlanRevision" IS NULL OR "PlanRevision" > 0))),
          CONSTRAINT "CK_Owning_Asset" CHECK (("AssetId" IS NULL AND "AssetRevision" IS NULL) OR ("AssetId" IS NOT NULL AND "AssetRevision" IS NOT NULL AND "AssetRevision" > 0)),
          CONSTRAINT "CK_Owning_Service" CHECK (("ServiceId" IS NULL AND "ServiceRevision" IS NULL) OR ("ServiceId" IS NOT NULL AND "ServiceRevision" IS NOT NULL AND "ServiceRevision" > 0 AND "ServiceId" <> "Id")),
          FOREIGN KEY ("TenantId","VesselId","Kind","Id","Revision") REFERENCES core."ControlEvidenceRevisions"("TenantId","VesselId","Kind","Id","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","JobId","JobRevision") REFERENCES tug."JobRevisions"("TenantId","VesselId","JobId","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","JobId","PlanRevision") REFERENCES tug."DraftPlanRevisions"("TenantId","VesselId","JobId","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","AssetId","AssetRevision") REFERENCES technical."AssetRevisions"("TenantId","VesselId","AssetId","Revision") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","ServiceKind","ServiceId","ServiceRevision") REFERENCES core."ControlEvidenceRevisions"("TenantId","VesselId","Kind","Id","Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","JobId","JobRevision");
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","JobId","PlanRevision");
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","AssetId","AssetRevision");
        CREATE INDEX ON core."OperationEquipmentBindings"("TenantId","VesselId","ServiceKind","ServiceId","ServiceRevision");
        CREATE TRIGGER operation_equipment_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
          ON core."OperationEquipmentBindings" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TABLE core."EvaluationOwningReferences" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "EvaluationId" uuid NOT NULL,
          "Kind" varchar(16) NOT NULL, "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          PRIMARY KEY ("TenantId","EvaluationId","Id"),
          FOREIGN KEY ("TenantId","VesselId","EvaluationId") REFERENCES core."ApplicabilityEvaluations"("TenantId","VesselId","Id") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId","VesselId","Kind","Id","Revision") REFERENCES core."OperationEquipmentBindings"("TenantId","VesselId","Kind","Id","Revision") ON DELETE RESTRICT);
        CREATE INDEX ON core."EvaluationOwningReferences"("TenantId","VesselId","EvaluationId");
        CREATE INDEX ON core."EvaluationOwningReferences"("TenantId","VesselId","Kind","Id","Revision");
        CREATE TRIGGER evaluation_owning_append_only BEFORE UPDATE OR DELETE OR TRUNCATE
          ON core."EvaluationOwningReferences" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Retained owning lineage requires a controlled forward migration.");
}
