using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261106010000_DefectDrafts")]
public sealed class DefectDrafts : Migration
{
    // Source only. No model/migration generation or execution has occurred.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE technical."Defects" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"Revision" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"AssetId" uuid,"AssetRevision" bigint,"AssetVersion" bigint,"AssetHash" varchar(64),
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_Defects" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_Defects_TenantId_VesselId_Id" UNIQUE ("TenantId","VesselId","Id"),
          CONSTRAINT "FK_Defects_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_Defect_AssetRevision" FOREIGN KEY ("TenantId","VesselId","AssetId","AssetRevision") REFERENCES technical."AssetRevisions" ("TenantId","VesselId","AssetId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_Defect_Draft" CHECK ("Revision">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified') AND
            (("AssetId" IS NULL AND "AssetRevision" IS NULL AND "AssetVersion" IS NULL AND "AssetHash" IS NULL) OR
             ("AssetId" IS NOT NULL AND "AssetRevision" IS NOT NULL AND "AssetRevision">0 AND "AssetVersion" IS NOT NULL AND "AssetVersion">0 AND "AssetHash" IS NOT NULL)))
        );
        CREATE INDEX "IX_Defects_TenantId_VesselId_SavedAt_Id" ON technical."Defects" ("TenantId","VesselId","SavedAt","Id");
        CREATE INDEX "IX_Defects_TenantId_VesselId_AssetId_AssetRevision" ON technical."Defects" ("TenantId","VesselId","AssetId","AssetRevision");
        CREATE TABLE technical."DefectRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"DefectId" uuid NOT NULL,"Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"AssetId" uuid,"AssetRevision" bigint,"AssetVersion" bigint,"AssetHash" varchar(64),
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DefectRevisions" PRIMARY KEY ("TenantId","DefectId","Revision"),
          CONSTRAINT "AK_DefectRevision_Version" UNIQUE ("TenantId","DefectId","Revision","RecordVersion"),
          CONSTRAINT "FK_DefectRevision_Head" FOREIGN KEY ("TenantId","VesselId","DefectId") REFERENCES technical."Defects" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_DefectRevision_AssetRevision" FOREIGN KEY ("TenantId","VesselId","AssetId","AssetRevision") REFERENCES technical."AssetRevisions" ("TenantId","VesselId","AssetId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_DefectRevision_Draft" CHECK ("Revision">0 AND "RecordVersion">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified') AND
            (("AssetId" IS NULL AND "AssetRevision" IS NULL AND "AssetVersion" IS NULL AND "AssetHash" IS NULL) OR
             ("AssetId" IS NOT NULL AND "AssetRevision" IS NOT NULL AND "AssetRevision">0 AND "AssetVersion" IS NOT NULL AND "AssetVersion">0 AND "AssetHash" IS NOT NULL)))
        );
        CREATE INDEX "IX_DefectRevisions_TenantId_VesselId_DefectId" ON technical."DefectRevisions" ("TenantId","VesselId","DefectId");
        CREATE INDEX "IX_DefectRevisions_TenantId_VesselId_AssetId_AssetRevision" ON technical."DefectRevisions" ("TenantId","VesselId","AssetId","AssetRevision");
        CREATE TABLE technical."DefectReceipts" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"ActorId" uuid NOT NULL,"Key" varchar(80) NOT NULL,"DefectId" uuid NOT NULL,
          "Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,"Fingerprint" varchar(64) NOT NULL,"OutcomeJson" text NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DefectReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_DefectReceipt_Head" FOREIGN KEY ("TenantId","VesselId","DefectId") REFERENCES technical."Defects" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_DefectReceipt_Revision" FOREIGN KEY ("TenantId","DefectId","Revision","RecordVersion") REFERENCES technical."DefectRevisions" ("TenantId","DefectId","Revision","RecordVersion") ON DELETE RESTRICT,
          CONSTRAINT "CK_DefectReceipt_Version" CHECK ("Revision">0 AND "RecordVersion">0)
        );
        CREATE INDEX "IX_DefectReceipts_TenantId_VesselId_DefectId" ON technical."DefectReceipts" ("TenantId","VesselId","DefectId");
        CREATE INDEX "IX_DefectReceipts_TenantId_DefectId_Revision_RecordVersion" ON technical."DefectReceipts" ("TenantId","DefectId","Revision","RecordVersion");
        CREATE FUNCTION technical.guard_defect_draft() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Defect history cannot be removed'; END IF;
          IF (to_jsonb(OLD)-ARRAY['FieldsJson','AssetId','AssetRevision','AssetVersion','AssetHash','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FieldsJson','AssetId','AssetRevision','AssetVersion','AssetHash','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            OR NEW."Revision" <> OLD."Revision"+1 THEN RAISE EXCEPTION 'Immutable defect ownership'; END IF;RETURN NEW;
        END $$;
        CREATE TRIGGER "Defect_Guard" BEFORE UPDATE OR DELETE ON technical."Defects" FOR EACH ROW EXECUTE FUNCTION technical.guard_defect_draft();
        CREATE TRIGGER "Defect_NoTruncate" BEFORE TRUNCATE ON technical."Defects" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DefectRevision_Immutable" BEFORE UPDATE OR DELETE ON technical."DefectRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DefectRevision_NoTruncate" BEFORE TRUNCATE ON technical."DefectRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DefectReceipt_Immutable" BEFORE UPDATE OR DELETE ON technical."DefectReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DefectReceipt_NoTruncate" BEFORE TRUNCATE ON technical."DefectReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS(SELECT 1 FROM technical."Defects") THEN RAISE EXCEPTION 'Defect retention requires controlled archival'; END IF;END $$;
        DROP TABLE technical."DefectReceipts";DROP TABLE technical."DefectRevisions";DROP TABLE technical."Defects";DROP FUNCTION technical.guard_defect_draft();
        """);
}
