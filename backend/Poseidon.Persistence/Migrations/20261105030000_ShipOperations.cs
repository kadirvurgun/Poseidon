using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261105030000_ShipOperations")]
public sealed class ShipOperations : Migration
{
    // Source authored only. No migration, model generation or validation ran.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE operations."ShipOperations" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"Kind" varchar(8) NOT NULL,"Revision" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"OriginJson" text,"SourceVersion" varchar(160) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,
          "ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,"OccurredAt" timestamptz,"EndedAt" timestamptz,
          CONSTRAINT "PK_ShipOperations" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_ShipOperations_TenantId_VesselId_Kind_Id" UNIQUE ("TenantId","VesselId","Kind","Id"),
          CONSTRAINT "FK_ShipOperations_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_ShipOperation_State" CHECK ("Kind" IN ('Fuel','Port') AND "Revision">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_ShipOperations_TenantId_VesselId_Kind_OccurredAt_Id" ON operations."ShipOperations" ("TenantId","VesselId","Kind","OccurredAt","Id");
        CREATE TABLE operations."ShipOperationRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"RecordId" uuid NOT NULL,"Kind" varchar(8) NOT NULL,"Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"OriginJson" text,"SourceVersion" varchar(160) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,
          "ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,"OccurredAt" timestamptz,"EndedAt" timestamptz,
          CONSTRAINT "PK_ShipOperationRevisions" PRIMARY KEY ("TenantId","RecordId","Revision"),
          CONSTRAINT "FK_ShipOperationRevision_Head" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."ShipOperations" ("TenantId","VesselId","Kind","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_ShipOperationRevision_State" CHECK ("Revision">0 AND "RecordVersion">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_ShipOperationRevisions_TenantId_VesselId_Kind_RecordId" ON operations."ShipOperationRevisions" ("TenantId","VesselId","Kind","RecordId");
        CREATE TABLE operations."ShipOperationReceipts" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"ActorId" uuid NOT NULL,"Key" varchar(80) NOT NULL,"RecordId" uuid NOT NULL,"Kind" varchar(8) NOT NULL,
          "OriginJson" text,"Fingerprint" varchar(64) NOT NULL,"OutcomeJson" text NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_ShipOperationReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_ShipOperationReceipt_Head" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."ShipOperations" ("TenantId","VesselId","Kind","Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_ShipOperationReceipts_TenantId_VesselId_Kind_RecordId" ON operations."ShipOperationReceipts" ("TenantId","VesselId","Kind","RecordId");
        CREATE FUNCTION operations.guard_ship_operation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Ship operation history cannot be removed'; END IF;
          IF (to_jsonb(OLD)-ARRAY['FieldsJson','OriginJson','SourceVersion','Hash','Provenance','ActorId','SavedAt','OccurredAt','EndedAt','Revision'])
            IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FieldsJson','OriginJson','SourceVersion','Hash','Provenance','ActorId','SavedAt','OccurredAt','EndedAt','Revision'])
            OR NEW."Revision" <> OLD."Revision"+1 THEN RAISE EXCEPTION 'Immutable ship operation ownership'; END IF;RETURN NEW;
        END $$;
        CREATE TRIGGER "ShipOperation_Guard" BEFORE UPDATE OR DELETE ON operations."ShipOperations" FOR EACH ROW EXECUTE FUNCTION operations.guard_ship_operation();
        CREATE TRIGGER "ShipOperation_NoTruncate" BEFORE TRUNCATE ON operations."ShipOperations" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "ShipOperationRevision_Immutable" BEFORE UPDATE OR DELETE ON operations."ShipOperationRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "ShipOperationRevision_NoTruncate" BEFORE TRUNCATE ON operations."ShipOperationRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "ShipOperationReceipt_Immutable" BEFORE UPDATE OR DELETE ON operations."ShipOperationReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "ShipOperationReceipt_NoTruncate" BEFORE TRUNCATE ON operations."ShipOperationReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS(SELECT 1 FROM operations."ShipOperations") THEN RAISE EXCEPTION 'Ship operation retention requires controlled archival'; END IF;END $$;
        DROP TABLE operations."ShipOperationReceipts";DROP TABLE operations."ShipOperationRevisions";DROP TABLE operations."ShipOperations";DROP FUNCTION operations.guard_ship_operation();
        """);
}
