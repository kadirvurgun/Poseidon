using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261109010000_DowntimeDrafts")]
public sealed class DowntimeDrafts : Migration
{
    // Source only. No model/migration generation or execution has occurred.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE SCHEMA IF NOT EXISTS operations;
        CREATE TABLE operations."Downtimes" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"Id" uuid NOT NULL,"Revision" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"OriginJson" text,"StartedAt" timestamptz,
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_Downtimes" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_Downtimes_TenantId_VesselId_Id" UNIQUE ("TenantId","VesselId","Id"),
          CONSTRAINT "FK_Downtimes_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_Downtime_Draft" CHECK ("Revision">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_Downtimes_TenantId_VesselId_SavedAt_Id" ON operations."Downtimes" ("TenantId","VesselId","SavedAt","Id");
        CREATE TABLE operations."DowntimeRevisions" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"DowntimeId" uuid NOT NULL,"Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,
          "FieldsJson" text NOT NULL,"OriginJson" text,
          "SourceVersion" varchar(192) NOT NULL,"Hash" varchar(64) NOT NULL,"Provenance" varchar(32) NOT NULL,"ActorId" uuid NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DowntimeRevisions" PRIMARY KEY ("TenantId","DowntimeId","Revision"),
          CONSTRAINT "AK_DowntimeRevision_Version" UNIQUE ("TenantId","DowntimeId","Revision","RecordVersion"),
          CONSTRAINT "FK_DowntimeRevision_Head" FOREIGN KEY ("TenantId","VesselId","DowntimeId") REFERENCES operations."Downtimes" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_DowntimeRevision_Draft" CHECK ("Revision">0 AND "RecordVersion">0 AND "Provenance" IN ('UserDeclaredUnverified','SyntheticUnverified'))
        );
        CREATE INDEX "IX_DowntimeRevisions_TenantId_VesselId_DowntimeId" ON operations."DowntimeRevisions" ("TenantId","VesselId","DowntimeId");
        CREATE TABLE operations."DowntimeReceipts" (
          "TenantId" uuid NOT NULL,"VesselId" uuid NOT NULL,"ActorId" uuid NOT NULL,"Key" varchar(80) NOT NULL,"DowntimeId" uuid NOT NULL,
          "Revision" bigint NOT NULL,"RecordVersion" bigint NOT NULL,"Fingerprint" varchar(64) NOT NULL,"OutcomeJson" text NOT NULL,"SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DowntimeReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_DowntimeReceipt_Head" FOREIGN KEY ("TenantId","VesselId","DowntimeId") REFERENCES operations."Downtimes" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_DowntimeReceipt_Revision" FOREIGN KEY ("TenantId","DowntimeId","Revision","RecordVersion") REFERENCES operations."DowntimeRevisions" ("TenantId","DowntimeId","Revision","RecordVersion") ON DELETE RESTRICT,
          CONSTRAINT "CK_DowntimeReceipt_Version" CHECK ("Revision">0 AND "RecordVersion">0)
        );
        CREATE INDEX "IX_DowntimeReceipts_TenantId_VesselId_DowntimeId" ON operations."DowntimeReceipts" ("TenantId","VesselId","DowntimeId");
        CREATE INDEX "IX_DowntimeReceipts_TenantId_DowntimeId_Revision_RecordVersion" ON operations."DowntimeReceipts" ("TenantId","DowntimeId","Revision","RecordVersion");
        CREATE FUNCTION operations.guard_downtime_draft() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Downtime history cannot be removed'; END IF;
          IF (to_jsonb(OLD)-ARRAY['FieldsJson','OriginJson','StartedAt','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            IS DISTINCT FROM (to_jsonb(NEW)-ARRAY['FieldsJson','OriginJson','StartedAt','SourceVersion','Hash','Provenance','ActorId','SavedAt','Revision'])
            OR NEW."Revision" <> OLD."Revision"+1 THEN RAISE EXCEPTION 'Immutable downtime ownership'; END IF;RETURN NEW;
        END $$;
        CREATE TRIGGER "Downtime_Guard" BEFORE UPDATE OR DELETE ON operations."Downtimes" FOR EACH ROW EXECUTE FUNCTION operations.guard_downtime_draft();
        CREATE TRIGGER "Downtime_NoTruncate" BEFORE TRUNCATE ON operations."Downtimes" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DowntimeRevision_Immutable" BEFORE UPDATE OR DELETE ON operations."DowntimeRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DowntimeRevision_NoTruncate" BEFORE TRUNCATE ON operations."DowntimeRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DowntimeReceipt_Immutable" BEFORE UPDATE OR DELETE ON operations."DowntimeReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DowntimeReceipt_NoTruncate" BEFORE TRUNCATE ON operations."DowntimeReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS(SELECT 1 FROM operations."Downtimes") THEN RAISE EXCEPTION 'Downtime retention requires controlled archival'; END IF;END $$;
        DROP TABLE operations."DowntimeReceipts";DROP TABLE operations."DowntimeRevisions";DROP TABLE operations."Downtimes";DROP FUNCTION operations.guard_downtime_draft();
        """);
}
