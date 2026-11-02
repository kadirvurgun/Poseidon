using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED. No provider/model generation or migration execution performed.
public partial class OfflineApplication : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.EnsureSchema("offline");
        b.AddUniqueConstraint("AK_Assignments_TenantId_Id_VesselId", "Assignments", new[] { "TenantId", "Id", "VesselId" }, "crew");
        // Schema-only SQL is a controlled migration; business requests never use raw writes.
        b.Sql("""
CREATE TABLE offline."Enrollments" (
 "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "ActorId" uuid NOT NULL,
 "VesselId" uuid NOT NULL, "AssignmentId" uuid NOT NULL, "DeviceId" uuid NOT NULL, "KeyId" uuid NOT NULL,
 "KeyChallenge" character varying(64) NOT NULL, "PublicSigningKey" character varying(256),
 "PolicyVersion" character varying(80) NOT NULL, "PolicyDigest" character varying(64) NOT NULL, "SourceVersion" character varying(64) NOT NULL,
 "BaseVersion" bigint NOT NULL, "BaseRevision" bigint NOT NULL, "ProtectedBase" text NOT NULL,
 "CreatedAt" timestamp with time zone NOT NULL, "ExpiresAt" timestamp with time zone NOT NULL, "RevokedAt" timestamp with time zone,
 CONSTRAINT "PK_Enrollments" PRIMARY KEY ("TenantId","Id"),
 CONSTRAINT "AK_Enrollments_TenantId_Id_ActorId_VesselId_AssignmentId" UNIQUE ("TenantId","Id","ActorId","VesselId","AssignmentId"),
 CONSTRAINT "FK_OfflineEnrollment_Assignment" FOREIGN KEY ("TenantId","AssignmentId","VesselId") REFERENCES crew."Assignments" ("TenantId","Id","VesselId") ON DELETE RESTRICT
);
CREATE INDEX "IX_Enrollments_TenantId_ActorId_VesselId_AssignmentId" ON offline."Enrollments" ("TenantId","ActorId","VesselId","AssignmentId");
CREATE INDEX "IX_Enrollments_TenantId_AssignmentId_VesselId" ON offline."Enrollments" ("TenantId","AssignmentId","VesselId");
CREATE TABLE offline."Lifecycle" (
 "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "EnrollmentId" uuid NOT NULL,
 "ActorId" uuid NOT NULL, "VesselId" uuid NOT NULL, "AssignmentId" uuid NOT NULL,
 "Action" character varying(16) NOT NULL, "RecordedAt" timestamp with time zone NOT NULL,
 CONSTRAINT "PK_Lifecycle" PRIMARY KEY ("TenantId","Id"),
 CONSTRAINT "FK_OfflineLifecycle_Enrollment" FOREIGN KEY ("TenantId","EnrollmentId","ActorId","VesselId","AssignmentId") REFERENCES offline."Enrollments" ("TenantId","Id","ActorId","VesselId","AssignmentId") ON DELETE RESTRICT
);
CREATE INDEX "IX_Lifecycle_TenantId_EnrollmentId_ActorId_VesselId_AssignmentId" ON offline."Lifecycle" ("TenantId","EnrollmentId","ActorId","VesselId","AssignmentId");
CREATE TABLE offline."Outcomes" (
 "TenantId" uuid NOT NULL, "ActorId" uuid NOT NULL, "CommandId" uuid NOT NULL,
 "EnrollmentId" uuid NOT NULL, "ParentCommandId" uuid, "VesselId" uuid NOT NULL, "AssignmentId" uuid NOT NULL,
 "Digest" character varying(64) NOT NULL, "Result" character varying(24) NOT NULL,
 "BaseVersion" bigint NOT NULL, "ServerVersion" bigint NOT NULL, "Revision" bigint NOT NULL,
 "CapturedAt" timestamp with time zone NOT NULL, "OffsetMinutes" integer NOT NULL, "ReceivedAt" timestamp with time zone NOT NULL,
 "ProtectedContext" text NOT NULL,
 CONSTRAINT "PK_Outcomes" PRIMARY KEY ("TenantId","ActorId","CommandId"),
 CONSTRAINT "AK_Outcomes_TenantId_ActorId_CommandId_EnrollmentId" UNIQUE ("TenantId","ActorId","CommandId","EnrollmentId"),
 CONSTRAINT "FK_OfflineOutcome_Enrollment" FOREIGN KEY ("TenantId","EnrollmentId","ActorId","VesselId","AssignmentId") REFERENCES offline."Enrollments" ("TenantId","Id","ActorId","VesselId","AssignmentId") ON DELETE RESTRICT,
 CONSTRAINT "FK_OfflineOutcome_Parent" FOREIGN KEY ("TenantId","ActorId","ParentCommandId","EnrollmentId") REFERENCES offline."Outcomes" ("TenantId","ActorId","CommandId","EnrollmentId") ON DELETE RESTRICT
);
CREATE INDEX "IX_Outcomes_TenantId_EnrollmentId_ActorId_VesselId_AssignmentId" ON offline."Outcomes" ("TenantId","EnrollmentId","ActorId","VesselId","AssignmentId");
CREATE INDEX "IX_Outcomes_TenantId_ActorId_EnrollmentId_ReceivedAt" ON offline."Outcomes" ("TenantId","ActorId","EnrollmentId","ReceivedAt");
CREATE UNIQUE INDEX "IX_Outcomes_TenantId_ActorId_ParentCommandId" ON offline."Outcomes" ("TenantId","ActorId","ParentCommandId") WHERE "ParentCommandId" IS NOT NULL;
CREATE INDEX "IX_OfflineOutcome_ParentScope" ON offline."Outcomes" ("TenantId","ActorId","ParentCommandId","EnrollmentId");
CREATE FUNCTION offline.reject_lineage_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'immutable_offline_lineage'; END $$;
CREATE TRIGGER offline_outcome_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON offline."Outcomes" FOR EACH STATEMENT EXECUTE FUNCTION offline.reject_lineage_mutation();
CREATE TRIGGER offline_lifecycle_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON offline."Lifecycle" FOR EACH STATEMENT EXECUTE FUNCTION offline.reject_lineage_mutation();
CREATE FUNCTION offline.guard_enrollment() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF (to_jsonb(NEW) - ARRAY['ExpiresAt','RevokedAt','PublicSigningKey']) IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['ExpiresAt','RevokedAt','PublicSigningKey'])
 OR OLD."RevokedAt" IS NOT NULL OR NEW."ExpiresAt" < OLD."ExpiresAt"
 OR (OLD."PublicSigningKey" IS NOT NULL AND NEW."PublicSigningKey" IS DISTINCT FROM OLD."PublicSigningKey")
 THEN RAISE EXCEPTION 'immutable_offline_context'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER offline_enrollment_guard BEFORE UPDATE ON offline."Enrollments" FOR EACH ROW EXECUTE FUNCTION offline.guard_enrollment();
CREATE TRIGGER offline_enrollment_no_delete BEFORE DELETE OR TRUNCATE ON offline."Enrollments" FOR EACH STATEMENT EXECUTE FUNCTION offline.reject_lineage_mutation();
""");
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Offline lineage retention requires a controlled rollback design.");
}
