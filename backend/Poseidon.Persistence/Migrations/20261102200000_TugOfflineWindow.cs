using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored. Snapshot/designer reconciliation and PostgreSQL execution NOT RUN.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102200000_TugOfflineWindow")]
public sealed class TugOfflineWindow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE tug."OfflineEnrollments" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "ActorId" uuid NOT NULL,
          "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL, "DeviceId" uuid NOT NULL, "KeyId" uuid NOT NULL,
          "Challenge" varchar(64) NOT NULL, "PublicKey" varchar(256), "PolicyDigest" varchar(64) NOT NULL,
          "RequestVersion" bigint NOT NULL, "BaseVersion" bigint NOT NULL, "BaseRevision" bigint NOT NULL,
          "ProtectedBase" text NOT NULL, "CreatedAt" timestamptz NOT NULL, "ExpiresAt" timestamptz NOT NULL, "RevokedAt" timestamptz,
          PRIMARY KEY ("TenantId", "Id"), UNIQUE ("TenantId", "Id", "ActorId", "VesselId", "JobId"),
          FOREIGN KEY ("TenantId", "VesselId", "JobId", "BaseRevision") REFERENCES tug."DraftPlanRevisions" ("TenantId", "VesselId", "JobId", "Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_TugOfflineEnrollment" CHECK ("BaseRevision" > 0 AND "BaseVersion" > 0 AND "RequestVersion" > 0 AND "ExpiresAt" > "CreatedAt")
        );
        CREATE INDEX "IX_TugOfflineEnrollment_Base" ON tug."OfflineEnrollments" ("TenantId", "VesselId", "JobId", "BaseRevision");
        CREATE TABLE tug."OfflineOutcomes" (
          "TenantId" uuid NOT NULL, "ActorId" uuid NOT NULL, "CommandId" uuid NOT NULL, "EnrollmentId" uuid NOT NULL,
          "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL, "ParentCommandId" uuid,
          "Digest" varchar(64) NOT NULL, "Result" varchar(32) NOT NULL, "ServerVersion" bigint NOT NULL,
          "BaseVersion" bigint NOT NULL, "RequestVersion" bigint NOT NULL, "CapturedAt" timestamptz NOT NULL, "OffsetMinutes" integer NOT NULL,
          "Revision" bigint NOT NULL, "ProtectedContext" text NOT NULL, "ReceivedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "ActorId", "CommandId"), UNIQUE ("TenantId", "ActorId", "CommandId", "EnrollmentId"),
          FOREIGN KEY ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId") REFERENCES tug."OfflineEnrollments" ("TenantId", "Id", "ActorId", "VesselId", "JobId") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "ActorId", "ParentCommandId", "EnrollmentId") REFERENCES tug."OfflineOutcomes" ("TenantId", "ActorId", "CommandId", "EnrollmentId") ON DELETE RESTRICT,
          FOREIGN KEY ("TenantId", "VesselId", "JobId", "Revision") REFERENCES tug."DraftPlanRevisions" ("TenantId", "VesselId", "JobId", "Revision") ON DELETE RESTRICT,
          CONSTRAINT "CK_TugOfflineOutcome" CHECK ("Result" IN ('Committed', 'MaterialConflict') AND "Revision" > 0 AND "ServerVersion" > 0 AND "BaseVersion" > 0 AND "RequestVersion" > 0 AND "OffsetMinutes" BETWEEN -840 AND 840)
        );
        CREATE UNIQUE INDEX "IX_TugOfflineOutcome_Parent" ON tug."OfflineOutcomes" ("TenantId", "ActorId", "ParentCommandId") WHERE "ParentCommandId" IS NOT NULL;
        CREATE INDEX "IX_TugOfflineOutcome_Enrollment" ON tug."OfflineOutcomes" ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId");
        CREATE INDEX "IX_TugOfflineOutcome_ParentFK" ON tug."OfflineOutcomes" ("TenantId", "ActorId", "ParentCommandId", "EnrollmentId");
        CREATE INDEX "IX_TugOfflineOutcome_Revision" ON tug."OfflineOutcomes" ("TenantId", "VesselId", "JobId", "Revision");
        CREATE TABLE tug."OfflineLifecycle" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "EnrollmentId" uuid NOT NULL, "ActorId" uuid NOT NULL,
          "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL, "Action" varchar(16) NOT NULL, "RecordedAt" timestamptz NOT NULL,
          PRIMARY KEY ("TenantId", "Id"),
          FOREIGN KEY ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId") REFERENCES tug."OfflineEnrollments" ("TenantId", "Id", "ActorId", "VesselId", "JobId") ON DELETE RESTRICT
        );
        CREATE INDEX "IX_TugOfflineLifecycle_Enrollment" ON tug."OfflineLifecycle" ("TenantId", "EnrollmentId", "ActorId", "VesselId", "JobId");
        CREATE FUNCTION tug.guard_offline_enrollment() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Immutable offline enrollment'; END IF;
          IF (to_jsonb(OLD) - ARRAY['ExpiresAt','RevokedAt','PublicKey']) IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['ExpiresAt','RevokedAt','PublicKey'])
            OR OLD."PublicKey" IS NOT NULL AND NEW."PublicKey" IS DISTINCT FROM OLD."PublicKey"
            OR OLD."RevokedAt" IS NOT NULL AND NEW."RevokedAt" IS DISTINCT FROM OLD."RevokedAt" THEN
            RAISE EXCEPTION 'Immutable offline enrollment';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "TugOfflineEnrollment_Identity" BEFORE UPDATE OR DELETE ON tug."OfflineEnrollments" FOR EACH ROW EXECUTE FUNCTION tug.guard_offline_enrollment();
        CREATE TRIGGER "TugOfflineEnrollment_NoTruncate" BEFORE TRUNCATE ON tug."OfflineEnrollments" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "TugOfflineOutcome_Immutable" BEFORE UPDATE OR DELETE ON tug."OfflineOutcomes" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "TugOfflineOutcome_NoTruncate" BEFORE TRUNCATE ON tug."OfflineOutcomes" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "TugOfflineLifecycle_Immutable" BEFORE UPDATE OR DELETE ON tug."OfflineLifecycle" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
        CREATE TRIGGER "TugOfflineLifecycle_NoTruncate" BEFORE TRUNCATE ON tug."OfflineLifecycle" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE tug."OfflineLifecycle";
        DROP TABLE tug."OfflineOutcomes";
        DROP TABLE tug."OfflineEnrollments";
        DROP FUNCTION tug.guard_offline_enrollment();
        """);
}
