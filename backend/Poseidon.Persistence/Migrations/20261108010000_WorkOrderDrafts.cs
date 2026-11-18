using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261108010000_WorkOrderDrafts")]
public sealed class WorkOrderDrafts : Migration
{
    // Source only. No model/migration generation or execution has occurred.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE technical."WorkOrders" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"Revision" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"PlanId" uuid,"PlanRevision" bigint,"PlanVersion" bigint,"PlanHash" varchar(64),"PlanSourceVersion" varchar(64),
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_WorkOrders" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_WorkOrders_TenantId_VesselId_Id" UNIQUE ("TenantId","VesselId","Id"),
          CONSTRAINT "FK_WorkOrders_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_WorkOrder_PlanRevision" FOREIGN KEY ("TenantId","VesselId","PlanId","PlanRevision") REFERENCES technical."PlanRevisions" ("TenantId","VesselId","PlanId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_WorkOrder_Draft" CHECK ("Revision">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified') AND
            (("PlanId" IS NULL AND "PlanRevision" IS NULL AND "PlanVersion" IS NULL AND "PlanHash" IS NULL AND "PlanSourceVersion" IS NULL) OR
             ("PlanId" IS NOT NULL AND "PlanRevision" IS NOT NULL AND "PlanRevision">0 AND "PlanVersion" IS NOT NULL AND "PlanVersion">0 AND "PlanHash" IS NOT NULL AND "PlanSourceVersion" IS NOT NULL)))
        );
        CREATE INDEX "IX_WorkOrders_TenantId_VesselId_SavedAt_Id" ON technical."WorkOrders" ("TenantId","VesselId","SavedAt","Id");
        CREATE INDEX "IX_WorkOrders_TenantId_VesselId_PlanId_PlanRevision" ON technical."WorkOrders" ("TenantId","VesselId","PlanId","PlanRevision");
        CREATE TABLE technical."WorkOrderRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"WorkOrderId" uuid NOT NULL,"Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"PlanId" uuid,"PlanRevision" bigint,"PlanVersion" bigint,"PlanHash" varchar(64),"PlanSourceVersion" varchar(64),
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_WorkOrderRevisions" PRIMARY KEY ("TenantId","WorkOrderId","Revision"),
          CONSTRAINT "AK_WorkOrderRevision_Version" UNIQUE ("TenantId","WorkOrderId","Revision","RecordVersion"),
          CONSTRAINT "FK_WorkOrderRevision_Head" FOREIGN KEY ("TenantId","VesselId","WorkOrderId") REFERENCES technical."WorkOrders" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_WorkOrderRevision_PlanRevision" FOREIGN KEY ("TenantId","VesselId","PlanId","PlanRevision") REFERENCES technical."PlanRevisions" ("TenantId","VesselId","PlanId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_WorkOrderRevision_Draft" CHECK ("Revision">0 AND "RecordVersion">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified') AND
            (("PlanId" IS NULL AND "PlanRevision" IS NULL AND "PlanVersion" IS NULL AND "PlanHash" IS NULL AND "PlanSourceVersion" IS NULL) OR
             ("PlanId" IS NOT NULL AND "PlanRevision" IS NOT NULL AND "PlanRevision">0 AND "PlanVersion" IS NOT NULL AND "PlanVersion">0 AND "PlanHash" IS NOT NULL AND "PlanSourceVersion" IS NOT NULL)))
        );
        CREATE INDEX "IX_WorkOrderRevisions_TenantId_VesselId_WorkOrderId" ON technical."WorkOrderRevisions" ("TenantId","VesselId","WorkOrderId");
        CREATE INDEX "IX_WorkOrderRevisions_TenantId_VesselId_PlanId_PlanRevision" ON technical."WorkOrderRevisions" ("TenantId","VesselId","PlanId","PlanRevision");
        CREATE TABLE technical."WorkOrderReceipts" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"ActorId" uuid NOT NULL,"Key" varchar(80) NOT NULL,"WorkOrderId" uuid NOT NULL,
          "Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,"Fingerprint" varchar(64) NOT NULL,"OutcomeJson" text NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_WorkOrderReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_WorkOrderReceipt_Head" FOREIGN KEY ("TenantId","VesselId","WorkOrderId") REFERENCES technical."WorkOrders" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_WorkOrderReceipt_Revision" FOREIGN KEY ("TenantId","WorkOrderId","Revision","RecordVersion") REFERENCES technical."WorkOrderRevisions" ("TenantId","WorkOrderId","Revision","RecordVersion") ON DELETE RESTRICT,
          CONSTRAINT "CK_WorkOrderReceipt_Version" CHECK ("Revision">0 AND "RecordVersion">0)
        );
        CREATE INDEX "IX_WorkOrderReceipts_TenantId_VesselId_WorkOrderId" ON technical."WorkOrderReceipts" ("TenantId","VesselId","WorkOrderId");
        CREATE INDEX "IX_WorkOrderReceipts_TenantId_WorkOrderId_Revision_RecordVersion" ON technical."WorkOrderReceipts" ("TenantId","WorkOrderId","Revision","RecordVersion");
        CREATE FUNCTION technical.guard_work_order_draft() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'WorkOrder history cannot be removed'; END IF;
          IF (to_jsonb(OLD)-ARRAY['FieldsJson','PlanId','PlanRevision','PlanVersion','PlanHash','PlanSourceVersion','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FieldsJson','PlanId','PlanRevision','PlanVersion','PlanHash','PlanSourceVersion','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            OR NEW."Revision" <> OLD."Revision"+1 THEN RAISE EXCEPTION 'Immutable work_order ownership'; END IF;RETURN NEW;
        END $$;
        CREATE TRIGGER "WorkOrder_Guard" BEFORE UPDATE OR DELETE ON technical."WorkOrders" FOR EACH ROW EXECUTE FUNCTION technical.guard_work_order_draft();
        CREATE TRIGGER "WorkOrder_NoTruncate" BEFORE TRUNCATE ON technical."WorkOrders" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "WorkOrderRevision_Immutable" BEFORE UPDATE OR DELETE ON technical."WorkOrderRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "WorkOrderRevision_NoTruncate" BEFORE TRUNCATE ON technical."WorkOrderRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "WorkOrderReceipt_Immutable" BEFORE UPDATE OR DELETE ON technical."WorkOrderReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "WorkOrderReceipt_NoTruncate" BEFORE TRUNCATE ON technical."WorkOrderReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS(SELECT 1 FROM technical."WorkOrders") THEN RAISE EXCEPTION 'WorkOrder retention requires controlled archival'; END IF;END $$;
        DROP TABLE technical."WorkOrderReceipts";DROP TABLE technical."WorkOrderRevisions";DROP TABLE technical."WorkOrders";DROP FUNCTION technical.guard_work_order_draft();
        """);
}
