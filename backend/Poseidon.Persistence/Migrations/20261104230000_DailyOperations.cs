using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Source authored. Model/migration parity and real PostgreSQL execution deferred to PR gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261104230000_DailyOperations")]
public sealed class DailyOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE SCHEMA IF NOT EXISTS operations;
        CREATE TABLE operations."DailyOperations" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "VesselId" uuid NOT NULL, "Kind" varchar(12) NOT NULL,
          "Revision" bigint NOT NULL, "SourceVersion" varchar(160) NOT NULL, "FieldsJson" text NOT NULL, "Hash" varchar(64) NOT NULL,
          "Provenance" varchar(32) NOT NULL, "ActorId" uuid NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DailyOperations" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_DailyOperations_TenantId_VesselId_Kind_Id" UNIQUE ("TenantId","VesselId","Kind","Id"),
          CONSTRAINT "FK_DailyOperations_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_DailyOperation_State" CHECK ("Kind" IN ('Noon','Deck','Engine') AND "Revision" > 0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_DailyOperations_TenantId_VesselId_Kind_SavedAt_Id" ON operations."DailyOperations" ("TenantId","VesselId","Kind","SavedAt","Id");
        CREATE TABLE operations."DailyOperationRevisions" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "RecordId" uuid NOT NULL, "Kind" varchar(12) NOT NULL,
          "Revision" bigint NOT NULL, "RecordVersion" bigint NOT NULL, "SourceVersion" varchar(160) NOT NULL, "FieldsJson" text NOT NULL, "Hash" varchar(64) NOT NULL,
          "Provenance" varchar(32) NOT NULL, "ActorId" uuid NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DailyOperationRevisions" PRIMARY KEY ("TenantId","RecordId","Revision"),
          CONSTRAINT "FK_DailyRevision_Head" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."DailyOperations" ("TenantId","VesselId","Kind","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_DailyOperationRevision_State" CHECK ("Revision" > 0 AND "RecordVersion" > 0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_DailyOperationRevisions_TenantId_VesselId_Kind_RecordId" ON operations."DailyOperationRevisions" ("TenantId","VesselId","Kind","RecordId");
        CREATE TABLE operations."DailyOperationReceipts" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL,
          "RecordId" uuid NOT NULL, "Kind" varchar(12) NOT NULL, "Fingerprint" varchar(64) NOT NULL,
          "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DailyOperationReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_DailyReceipt_Head" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."DailyOperations" ("TenantId","VesselId","Kind","Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_DailyOperationReceipts_TenantId_VesselId_Kind_RecordId" ON operations."DailyOperationReceipts" ("TenantId","VesselId","Kind","RecordId");
        CREATE FUNCTION operations.guard_daily_operation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Daily operation history cannot be removed'; END IF;
          IF (to_jsonb(OLD) - ARRAY['FieldsJson','Hash','Provenance','ActorId','SavedAt','Revision'])
              IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['FieldsJson','Hash','Provenance','ActorId','SavedAt','Revision'])
            OR NEW."Revision" <> OLD."Revision" + 1 THEN RAISE EXCEPTION 'Immutable daily operation ownership'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "DailyOperation_Guard" BEFORE UPDATE OR DELETE ON operations."DailyOperations" FOR EACH ROW EXECUTE FUNCTION operations.guard_daily_operation();
        CREATE TRIGGER "DailyOperation_NoTruncate" BEFORE TRUNCATE ON operations."DailyOperations" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyRevision_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyOperationRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyRevision_NoTruncate" BEFORE TRUNCATE ON operations."DailyOperationRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReceipt_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyOperationReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReceipt_NoTruncate" BEFORE TRUNCATE ON operations."DailyOperationReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
          IF EXISTS (SELECT 1 FROM operations."DailyOperations") THEN RAISE EXCEPTION 'Daily operation history must be retained'; END IF;
        END $$;
        DROP TABLE operations."DailyOperationReceipts";
        DROP TABLE operations."DailyOperationRevisions";
        DROP TABLE operations."DailyOperations";
        DROP FUNCTION operations.guard_daily_operation();
        """);
}
