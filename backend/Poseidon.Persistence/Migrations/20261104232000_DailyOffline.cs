using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Poseidon.Persistence.Migrations;

// Source authored. Actual scaffold/model parity/application await the PR gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261104232000_DailyOffline")]
public sealed class DailyOffline : Migration
{
    protected override void Up(MigrationBuilder b) => b.Sql("""
CREATE TABLE offline."DailyEnrollments" (
 "TenantId" uuid NOT NULL,
 "Id" uuid NOT NULL,
 "ActorId" uuid NOT NULL,
 "VesselId" uuid NOT NULL,
 "Kind" character varying(12) NOT NULL,
 "RecordId" uuid NOT NULL,
 "DeviceId" uuid NOT NULL,
 "KeyId" uuid NOT NULL,
 "Challenge" character varying(64) NOT NULL,
 "PublicKey" character varying(256),
 "PolicyVersion" character varying(80) NOT NULL,
 "PolicyDigest" character varying(64) NOT NULL,
 "AuthorityDigest" character varying(64) NOT NULL,
 "SourceVersion" character varying(192) NOT NULL,
 "SchemaHash" character varying(64) NOT NULL,
 "BaseVersion" bigint NOT NULL,
 "BaseRevision" bigint NOT NULL,
 "ProtectedBase" text NOT NULL,
 "CreatedAt" timestamptz NOT NULL,
 "ExpiresAt" timestamptz NOT NULL,
 "RetainUntil" timestamptz NOT NULL,
 "RevokedAt" timestamptz,
 CONSTRAINT "PK_DailyEnrollments" PRIMARY KEY ("TenantId","Id"),
 CONSTRAINT "AK_DailyOfflineEnrollment_Scope" UNIQUE ("TenantId","Id","ActorId","VesselId","Kind","RecordId"),
 CONSTRAINT "FK_DailyOffline_Vessel" FOREIGN KEY ("TenantId","VesselId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
 CONSTRAINT "CK_DailyOfflineEnrollment_State" CHECK ("Kind" IN ('Noon','Deck','Engine') AND "BaseVersion" >= 0 AND "BaseRevision" >= 0 AND (("BaseVersion" = 0) = ("BaseRevision" = 0)) AND "ExpiresAt" > "CreatedAt" AND "RetainUntil" >= "ExpiresAt")
);
CREATE INDEX "IX_DailyOfflineEnrollment_Owner" ON offline."DailyEnrollments" ("TenantId","ActorId","VesselId","Kind","RecordId","CreatedAt");
CREATE INDEX "IX_DailyOfflineEnrollment_Vessel" ON offline."DailyEnrollments" ("TenantId","VesselId");
CREATE TABLE offline."DailyLifecycle" (
 "TenantId" uuid NOT NULL,
 "Id" uuid NOT NULL,
 "EnrollmentId" uuid NOT NULL,
 "ActorId" uuid NOT NULL,
 "VesselId" uuid NOT NULL,
 "Kind" character varying(12) NOT NULL,
 "RecordId" uuid NOT NULL,
 "Action" character varying(24) NOT NULL,
 "RecordedAt" timestamptz NOT NULL,
 CONSTRAINT "PK_DailyLifecycle" PRIMARY KEY ("TenantId","Id"),
 CONSTRAINT "FK_DailyOfflineLifecycle_Enrollment" FOREIGN KEY ("TenantId","EnrollmentId","ActorId","VesselId","Kind","RecordId") REFERENCES offline."DailyEnrollments" ("TenantId","Id","ActorId","VesselId","Kind","RecordId") ON DELETE RESTRICT
);
CREATE INDEX "IX_DailyOfflineLifecycle_Scope" ON offline."DailyLifecycle" ("TenantId","EnrollmentId","ActorId","VesselId","Kind","RecordId");
CREATE TABLE offline."DailyOutcomes" (
 "TenantId" uuid NOT NULL,
 "ActorId" uuid NOT NULL,
 "CommandId" uuid NOT NULL,
 "EnrollmentId" uuid NOT NULL,
 "VesselId" uuid NOT NULL,
 "Kind" character varying(12) NOT NULL,
 "RecordId" uuid NOT NULL,
 "ParentCommandId" uuid,
 "Digest" character varying(64) NOT NULL,
 "Result" character varying(24) NOT NULL,
 "BaseVersion" bigint NOT NULL,
 "ServerVersion" bigint NOT NULL,
 "Revision" bigint NOT NULL,
 "OutcomeJson" text NOT NULL,
 "ProtectedContext" text NOT NULL,
 "ReceivedAt" timestamptz NOT NULL,
 CONSTRAINT "PK_DailyOutcomes" PRIMARY KEY ("TenantId","ActorId","CommandId"),
 CONSTRAINT "AK_DailyOfflineOutcome_ParentScope" UNIQUE ("TenantId","ActorId","CommandId","EnrollmentId"),
 CONSTRAINT "FK_DailyOfflineOutcome_Enrollment" FOREIGN KEY ("TenantId","EnrollmentId","ActorId","VesselId","Kind","RecordId") REFERENCES offline."DailyEnrollments" ("TenantId","Id","ActorId","VesselId","Kind","RecordId") ON DELETE RESTRICT,
 CONSTRAINT "FK_DailyOfflineOutcome_Parent" FOREIGN KEY ("TenantId","ActorId","ParentCommandId","EnrollmentId") REFERENCES offline."DailyOutcomes" ("TenantId","ActorId","CommandId","EnrollmentId") ON DELETE RESTRICT,
 CONSTRAINT "CK_DailyOfflineOutcome_State" CHECK ("Kind" IN ('Noon','Deck','Engine') AND "BaseVersion" >= 0 AND "ServerVersion" >= 0 AND "Revision" >= 0 AND "Result" IN ('Committed','MaterialConflict','ServerRetained') AND ("Result" <> 'ServerRetained' OR "ParentCommandId" IS NOT NULL) AND ("Result" <> 'Committed' OR ("ServerVersion" > 0 AND "Revision" > 0)))
);
CREATE INDEX "IX_DailyOfflineOutcome_Scope" ON offline."DailyOutcomes" ("TenantId","EnrollmentId","ActorId","VesselId","Kind","RecordId");
CREATE INDEX "IX_DailyOfflineOutcome_ParentScope" ON offline."DailyOutcomes" ("TenantId","ActorId","ParentCommandId","EnrollmentId");
CREATE INDEX "IX_DailyOfflineOutcome_History" ON offline."DailyOutcomes" ("TenantId","ActorId","EnrollmentId","ReceivedAt","CommandId");
CREATE UNIQUE INDEX "UX_DailyOfflineOutcome_Resolution" ON offline."DailyOutcomes" ("TenantId","ActorId","ParentCommandId") WHERE "ParentCommandId" IS NOT NULL;
CREATE FUNCTION offline.guard_daily_enrollment() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (to_jsonb(NEW)-ARRAY['ExpiresAt','RevokedAt','PublicKey']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['ExpiresAt','RevokedAt','PublicKey'])
 OR OLD."RevokedAt" IS NOT NULL OR NEW."ExpiresAt" < OLD."ExpiresAt"
 OR (OLD."PublicKey" IS NOT NULL AND NEW."PublicKey" IS DISTINCT FROM OLD."PublicKey")
 THEN RAISE EXCEPTION 'immutable_daily_offline_context'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER "DailyEnrollment_Guard" BEFORE UPDATE ON offline."DailyEnrollments" FOR EACH ROW EXECUTE FUNCTION offline.guard_daily_enrollment();
CREATE TRIGGER "DailyEnrollment_NoDelete" BEFORE DELETE OR TRUNCATE ON offline."DailyEnrollments" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
CREATE TRIGGER "DailyLifecycle_Immutable" BEFORE UPDATE OR DELETE OR TRUNCATE ON offline."DailyLifecycle" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
CREATE TRIGGER "DailyOutcome_Immutable" BEFORE UPDATE OR DELETE OR TRUNCATE ON offline."DailyOutcomes" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
""");
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Retained daily offline lineage requires an explicit controlled rollback design.");
}
