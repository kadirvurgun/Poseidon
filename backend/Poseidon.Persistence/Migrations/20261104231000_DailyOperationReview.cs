using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

[DbContext(typeof(PoseidonDbContext))]
[Migration("20261104231000_DailyOperationReview")]
public sealed class DailyOperationReview : Migration
{
    // Manually authored, never applied or scaffold-validated during this bundle.
    protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        CREATE TABLE operations."DailyReviewHeads" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "RecordId" uuid NOT NULL, "Kind" varchar(12) NOT NULL, "Sequence" bigint NOT NULL,
          CONSTRAINT "PK_DailyReviewHeads" PRIMARY KEY ("TenantId","RecordId"),
          CONSTRAINT "AK_DailyReviewHeads_TenantId_VesselId_Kind_RecordId" UNIQUE ("TenantId","VesselId","Kind","RecordId"),
          CONSTRAINT "FK_DailyReviewHead_Record" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."DailyOperations" ("TenantId","VesselId","Kind","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_DailyReviewHead_Sequence" CHECK ("Sequence" > 0)
        );
        CREATE TABLE operations."DailyReviewEvents" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "RecordId" uuid NOT NULL, "Kind" varchar(12) NOT NULL, "Id" uuid NOT NULL,
          "RecordRevision" bigint NOT NULL, "Sequence" bigint NOT NULL, "RecordVersion" bigint NOT NULL, "ReviewVersion" bigint NOT NULL,
          "ActorId" uuid NOT NULL, "RoleId" uuid NOT NULL, "AuthorityId" uuid NOT NULL, "Duty" varchar(24) NOT NULL, "Decision" varchar(16) NOT NULL,
          "ReasonCode" varchar(32) NOT NULL, "Rationale" varchar(2000) NOT NULL, "SourceVersion" varchar(160) NOT NULL,
          "AuthorityHash" varchar(64) NOT NULL, "AuthorityJson" text NOT NULL, "RecordHash" varchar(64) NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DailyReviewEvents" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_DailyReviewEvents_TenantId_VesselId_Kind_RecordId_Id" UNIQUE ("TenantId","VesselId","Kind","RecordId","Id"),
          CONSTRAINT "FK_DailyReviewEvent_Head" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId") REFERENCES operations."DailyReviewHeads" ("TenantId","VesselId","Kind","RecordId") ON DELETE RESTRICT,
          CONSTRAINT "FK_DailyReviewEvent_Revision" FOREIGN KEY ("TenantId","RecordId","RecordRevision") REFERENCES operations."DailyOperationRevisions" ("TenantId","RecordId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_DailyReviewEvent_State" CHECK ("Sequence" > 0 AND "RecordVersion" > 0 AND "ReviewVersion" > 0 AND "ActorId" <> '00000000-0000-0000-0000-000000000000'::uuid AND
            (("Kind" = 'Noon' AND "Decision" = 'REVIEWED' AND "Duty" = 'Master') OR
             ("Kind" = 'Deck' AND "Decision" IN ('REVIEWED','RETURNED') AND "Duty" IN ('Master','ChiefOfficer')) OR
             ("Kind" = 'Engine' AND "Decision" IN ('REVIEWED','RETURNED') AND "Duty" = 'ChiefEngineer')))
        );
        CREATE UNIQUE INDEX "IX_DailyReviewEvents_TenantId_RecordId_Sequence" ON operations."DailyReviewEvents" ("TenantId","RecordId","Sequence");
        CREATE INDEX "IX_DailyReviewEvents_TenantId_RecordId_RecordRevision" ON operations."DailyReviewEvents" ("TenantId","RecordId","RecordRevision");
        CREATE TABLE operations."DailyReviewReceipts" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL,
          "RecordId" uuid NOT NULL, "Kind" varchar(12) NOT NULL, "EventId" uuid NOT NULL,
          "Fingerprint" varchar(64) NOT NULL, "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_DailyReviewReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "FK_DailyReviewReceipt_Event" FOREIGN KEY ("TenantId","VesselId","Kind","RecordId","EventId") REFERENCES operations."DailyReviewEvents" ("TenantId","VesselId","Kind","RecordId","Id") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_DailyReviewReceipts_TenantId_VesselId_Kind_RecordId_EventId" ON operations."DailyReviewReceipts" ("TenantId","VesselId","Kind","RecordId","EventId");
        CREATE FUNCTION operations.guard_daily_review() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Daily human review history cannot be removed'; END IF;
          IF (to_jsonb(OLD) - 'Sequence') IS DISTINCT FROM (to_jsonb(NEW) - 'Sequence') OR NEW."Sequence" <> OLD."Sequence"+1
            THEN RAISE EXCEPTION 'Immutable daily review ownership'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "DailyReviewHead_Guard" BEFORE UPDATE OR DELETE ON operations."DailyReviewHeads" FOR EACH ROW EXECUTE FUNCTION operations.guard_daily_review();
        CREATE TRIGGER "DailyReviewHead_NoTruncate" BEFORE TRUNCATE ON operations."DailyReviewHeads" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReviewEvent_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyReviewEvents" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReviewEvent_NoTruncate" BEFORE TRUNCATE ON operations."DailyReviewEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReviewReceipt_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyReviewReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "DailyReviewReceipt_NoTruncate" BEFORE TRUNCATE ON operations."DailyReviewReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder)=>migrationBuilder.Sql("""
        DO $$ BEGIN IF EXISTS (SELECT 1 FROM operations."DailyReviewEvents") THEN RAISE EXCEPTION 'Recorded human review history must be retained'; END IF; END $$;
        DROP TABLE operations."DailyReviewReceipts";DROP TABLE operations."DailyReviewEvents";DROP TABLE operations."DailyReviewHeads";
        DROP FUNCTION operations.guard_daily_review();
        """);
}
