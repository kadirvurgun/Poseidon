using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;
public partial class EquipmentMaintenanceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Controlled DDL only; all product writes use attributed SaveChanges.
        migrationBuilder.Sql("""
CREATE SCHEMA IF NOT EXISTS technical;
CREATE TABLE technical."Owners" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"Id" uuid NOT NULL,
CONSTRAINT "PK_Owners" PRIMARY KEY ("TenantId", "VesselId", "Id"),
CONSTRAINT "FK_Owners_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_Owners_TenantId_VesselId" ON technical."Owners" ("TenantId", "VesselId");
CREATE TABLE technical."Assets" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"ParentId" uuid,
"Code" text,
"Name" text,
"Level" text NOT NULL,
"SystemFamily" text,
"EquipmentType" text,
"Criticality" text,
"Maker" text,
"Model" text,
"SerialNumber" text,
"Location" text,
"CommissionDate" date,
"ResponsibleRoleId" uuid,
"Notes" text,
"Status" text NOT NULL,
"Provenance" text NOT NULL,
"Revision" bigint NOT NULL,
CONSTRAINT "PK_Assets" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_Assets_TenantId_VesselId_Id" UNIQUE ("TenantId", "VesselId", "Id"),
CONSTRAINT "FK_Assets_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_Assets_Assets_TenantId_VesselId_ParentId" FOREIGN KEY ("TenantId", "VesselId", "ParentId") REFERENCES technical."Assets" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_Assets_Owners_TenantId_VesselId_ResponsibleRoleId" FOREIGN KEY ("TenantId", "VesselId", "ResponsibleRoleId") REFERENCES technical."Owners" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "CK_Asset_SelfParent" CHECK ("ParentId" IS NULL OR "ParentId" <> "Id"),
CONSTRAINT "CK_Asset_Level" CHECK ("Level" IN ('System','Equipment','Component')),
CONSTRAINT "CK_Asset_Draft" CHECK ("Status" = 'Draft')
);
CREATE UNIQUE INDEX "IX_Assets_TenantId_VesselId_Code" ON technical."Assets" ("TenantId", "VesselId", "Code");
CREATE INDEX "IX_Assets_TenantId_VesselId" ON technical."Assets" ("TenantId", "VesselId");
CREATE INDEX "IX_Assets_TenantId_VesselId_ParentId" ON technical."Assets" ("TenantId", "VesselId", "ParentId");
CREATE INDEX "IX_Assets_TenantId_VesselId_ResponsibleRoleId" ON technical."Assets" ("TenantId", "VesselId", "ResponsibleRoleId");
CREATE TABLE technical."AssetRevisions" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"AssetId" uuid NOT NULL,
"Revision" bigint NOT NULL,
"ContentJson" jsonb NOT NULL,
"SavedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_AssetRevisions" PRIMARY KEY ("TenantId", "VesselId", "AssetId", "Revision"),
CONSTRAINT "FK_AssetRevisions_Assets_TenantId_VesselId_AssetId" FOREIGN KEY ("TenantId", "VesselId", "AssetId") REFERENCES technical."Assets" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_AssetRevisions_TenantId_VesselId_AssetId" ON technical."AssetRevisions" ("TenantId", "VesselId", "AssetId");
CREATE TABLE technical."Jobs" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"Id" uuid NOT NULL,
"Code" text NOT NULL,
"Title" text NOT NULL,
"Provenance" text NOT NULL,
CONSTRAINT "PK_Jobs" PRIMARY KEY ("TenantId", "VesselId", "Id"),
CONSTRAINT "FK_Jobs_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX "IX_Jobs_TenantId_VesselId_Code" ON technical."Jobs" ("TenantId", "VesselId", "Code");
CREATE INDEX "IX_Jobs_TenantId_VesselId" ON technical."Jobs" ("TenantId", "VesselId");
CREATE TABLE technical."Plans" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"Id" uuid NOT NULL,
"AssetId" uuid NOT NULL,
"JobId" uuid,
"OwnerRoleId" uuid,
"ContentJson" jsonb NOT NULL,
"Status" text NOT NULL,
"Provenance" text NOT NULL,
"Revision" bigint NOT NULL,
CONSTRAINT "PK_Plans" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_Plans_TenantId_VesselId_Id" UNIQUE ("TenantId", "VesselId", "Id"),
CONSTRAINT "FK_Plans_Assets_TenantId_VesselId_AssetId" FOREIGN KEY ("TenantId", "VesselId", "AssetId") REFERENCES technical."Assets" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_Plans_Jobs_TenantId_VesselId_JobId" FOREIGN KEY ("TenantId", "VesselId", "JobId") REFERENCES technical."Jobs" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_Plans_Owners_TenantId_VesselId_OwnerRoleId" FOREIGN KEY ("TenantId", "VesselId", "OwnerRoleId") REFERENCES technical."Owners" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "CK_Plan_Draft" CHECK ("Status" = 'Draft')
);
CREATE INDEX "IX_Plans_TenantId_VesselId_AssetId" ON technical."Plans" ("TenantId", "VesselId", "AssetId");
CREATE INDEX "IX_Plans_TenantId_VesselId_JobId" ON technical."Plans" ("TenantId", "VesselId", "JobId");
CREATE INDEX "IX_Plans_TenantId_VesselId_OwnerRoleId" ON technical."Plans" ("TenantId", "VesselId", "OwnerRoleId");
CREATE TABLE technical."PlanRevisions" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"PlanId" uuid NOT NULL,
"AssetId" uuid NOT NULL,
"AssetRevision" bigint NOT NULL,
"JobId" uuid,
"OwnerRoleId" uuid,
"Revision" bigint NOT NULL,
"ContentJson" jsonb NOT NULL,
"CalculationJson" jsonb NOT NULL,
"Provenance" text NOT NULL,
"ReferenceAt" timestamp with time zone NOT NULL,
"SavedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_PlanRevisions" PRIMARY KEY ("TenantId", "VesselId", "PlanId", "Revision"),
CONSTRAINT "FK_PlanRevisions_Plans_TenantId_VesselId_PlanId" FOREIGN KEY ("TenantId", "VesselId", "PlanId") REFERENCES technical."Plans" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_PlanRevisions_AssetRevisions_TenantId_VesselId_AssetId_Asse~" FOREIGN KEY ("TenantId", "VesselId", "AssetId", "AssetRevision") REFERENCES technical."AssetRevisions" ("TenantId", "VesselId", "AssetId", "Revision") ON DELETE RESTRICT,
CONSTRAINT "FK_PlanRevisions_Jobs_TenantId_VesselId_JobId" FOREIGN KEY ("TenantId", "VesselId", "JobId") REFERENCES technical."Jobs" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_PlanRevisions_Owners_TenantId_VesselId_OwnerRoleId" FOREIGN KEY ("TenantId", "VesselId", "OwnerRoleId") REFERENCES technical."Owners" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_PlanRevisions_TenantId_VesselId_PlanId" ON technical."PlanRevisions" ("TenantId", "VesselId", "PlanId");
CREATE INDEX "IX_PlanRevisions_TenantId_VesselId_AssetId_AssetRevision" ON technical."PlanRevisions" ("TenantId", "VesselId", "AssetId", "AssetRevision");
CREATE INDEX "IX_PlanRevisions_TenantId_VesselId_JobId" ON technical."PlanRevisions" ("TenantId", "VesselId", "JobId");
CREATE INDEX "IX_PlanRevisions_TenantId_VesselId_OwnerRoleId" ON technical."PlanRevisions" ("TenantId", "VesselId", "OwnerRoleId");
CREATE FUNCTION technical.guard_asset_tree() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
BEGIN
 IF TG_OP = 'UPDATE' AND (OLD."TenantId", OLD."VesselId", OLD."Id") IS DISTINCT FROM (NEW."TenantId", NEW."VesselId", NEW."Id") THEN
  RAISE EXCEPTION 'Asset ownership is immutable' USING ERRCODE = '23514';
 END IF;
 -- Transaction-scoped serialization, fixed tenant/vessel inputs, no business SQL write or audit bypass.
 PERFORM pg_advisory_xact_lock(('x' || substr(md5(NEW."TenantId"::text || NEW."VesselId"::text),1,16))::bit(64)::bigint);
 IF NEW."ParentId" = NEW."Id" OR EXISTS (
  WITH RECURSIVE ancestors AS (
   SELECT a."Id", a."ParentId" FROM technical."Assets" a WHERE a."TenantId"=NEW."TenantId" AND a."VesselId"=NEW."VesselId" AND a."Id"=NEW."ParentId"
   UNION
   SELECT a."Id", a."ParentId" FROM technical."Assets" a JOIN ancestors p ON a."Id"=p."ParentId"
    WHERE a."TenantId"=NEW."TenantId" AND a."VesselId"=NEW."VesselId"
  ) SELECT 1 FROM ancestors WHERE "Id"=NEW."Id"
 ) THEN RAISE EXCEPTION 'Invalid asset cycle' USING ERRCODE = '23514'; END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER asset_tree_guard BEFORE INSERT OR UPDATE ON technical."Assets" FOR EACH ROW EXECUTE FUNCTION technical.guard_asset_tree();
CREATE FUNCTION technical.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Technical history/reference is immutable'; END; $$;
CREATE TRIGGER immutable_technical BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."AssetRevisions" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_mutation();
CREATE TRIGGER immutable_technical BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."PlanRevisions" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_mutation();
CREATE TRIGGER immutable_technical BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."Owners" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_mutation();
CREATE TRIGGER immutable_technical BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."Jobs" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_mutation();

""");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TABLE technical."PlanRevisions";
DROP TABLE technical."Plans";
DROP TABLE technical."Jobs";
DROP TABLE technical."AssetRevisions";
DROP TABLE technical."Assets";
DROP TABLE technical."Owners";
DROP FUNCTION technical.guard_asset_tree();
DROP FUNCTION technical.reject_mutation();
""");
    }
}
