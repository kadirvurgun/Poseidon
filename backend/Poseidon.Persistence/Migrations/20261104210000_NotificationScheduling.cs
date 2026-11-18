using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Source-authored only. Scaffolding/model parity and application await the PR gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261104210000_NotificationScheduling")]
public sealed class NotificationScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE engagement."Notifications" ADD COLUMN "Episode" bigint NOT NULL DEFAULT 1;
        ALTER TABLE engagement."Notifications" ALTER COLUMN "Episode" DROP DEFAULT;
        DROP INDEX engagement."UX_Notification_SourceTrigger";
        CREATE UNIQUE INDEX "UX_Notification_SourceTrigger" ON engagement."Notifications"
          ("TenantId","VesselId","RuleKey","PolicyDigest","PlanId","PlanRevision","SourceContextHash","Episode");
        CREATE TABLE engagement."NotificationDispatchStates" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "LeaseOwner" uuid,
          "LeaseUntil" timestamptz, "NextRunAt" timestamptz, "LastCompletedAt" timestamptz,
          "SourceOffset" integer NOT NULL, "Revision" bigint NOT NULL,
          CONSTRAINT "PK_NotificationDispatchStates" PRIMARY KEY ("TenantId","VesselId"),
          CONSTRAINT "FK_NotificationDispatch_Vessel" FOREIGN KEY ("TenantId","VesselId")
            REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_NotificationDispatch_State" CHECK ("Revision" > 0 AND "SourceOffset" >= 0 AND
            (("LeaseOwner" IS NULL AND "LeaseUntil" IS NULL) OR ("LeaseOwner" IS NOT NULL AND "LeaseUntil" IS NOT NULL)))
        );
        CREATE TABLE engagement."NotificationEpisodes" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "PlanId" uuid NOT NULL,
          "RuleKey" varchar(64) NOT NULL, "PolicyDigest" varchar(64) NOT NULL, "Episode" bigint NOT NULL,
          "Active" boolean, "LastKnownActive" boolean, "SourceContextHash" varchar(64), "Revision" bigint NOT NULL,
          CONSTRAINT "PK_NotificationEpisodes" PRIMARY KEY ("TenantId","VesselId","PlanId","RuleKey","PolicyDigest"),
          CONSTRAINT "FK_NotificationEpisode_Plan" FOREIGN KEY ("TenantId","VesselId","PlanId")
            REFERENCES technical."Plans" ("TenantId","VesselId","Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_NotificationEpisode_State" CHECK ("Revision" > 0 AND "Episode" >= 0 AND
            ("Active" IS NULL OR "Active" IS NOT DISTINCT FROM "LastKnownActive") AND
            ("Active" IS DISTINCT FROM TRUE OR ("Episode" > 0 AND "SourceContextHash" IS NOT NULL)))
        );
        CREATE FUNCTION engagement.guard_notification_dispatch() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Dispatch control history cannot be removed'; END IF;
          IF (to_jsonb(OLD) - ARRAY['LeaseOwner','LeaseUntil','NextRunAt','LastCompletedAt','SourceOffset','Revision'])
              IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['LeaseOwner','LeaseUntil','NextRunAt','LastCompletedAt','SourceOffset','Revision'])
            OR NEW."Revision" <> OLD."Revision" + 1 THEN RAISE EXCEPTION 'Immutable dispatch ownership'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "NotificationDispatch_Guard" BEFORE UPDATE OR DELETE ON engagement."NotificationDispatchStates"
          FOR EACH ROW EXECUTE FUNCTION engagement.guard_notification_dispatch();
        CREATE TRIGGER "NotificationDispatch_NoTruncate" BEFORE TRUNCATE ON engagement."NotificationDispatchStates"
          FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        CREATE FUNCTION engagement.guard_notification_episode() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Episode history cannot be removed'; END IF;
          IF (to_jsonb(OLD) - ARRAY['Active','LastKnownActive','Episode','SourceContextHash','Revision'])
              IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['Active','LastKnownActive','Episode','SourceContextHash','Revision'])
            OR NEW."Revision" <> OLD."Revision" + 1 OR NEW."Episode" < OLD."Episode"
            OR (NEW."Active" IS NULL AND NEW."LastKnownActive" IS DISTINCT FROM OLD."LastKnownActive")
            OR (NEW."Episode" <> OLD."Episode" AND (NEW."Episode" <> OLD."Episode" + 1 OR NEW."Active" IS DISTINCT FROM TRUE
              OR (OLD."LastKnownActive" IS DISTINCT FROM FALSE AND OLD."Episode" > 0 AND OLD."SourceContextHash" IS NOT DISTINCT FROM NEW."SourceContextHash")))
            THEN RAISE EXCEPTION 'Immutable episode identity or invalid source boundary'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "NotificationEpisode_Guard" BEFORE UPDATE OR DELETE ON engagement."NotificationEpisodes"
          FOR EACH ROW EXECUTE FUNCTION engagement.guard_notification_episode();
        CREATE TRIGGER "NotificationEpisode_NoTruncate" BEFORE TRUNCATE ON engagement."NotificationEpisodes"
          FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $$ BEGIN
          IF EXISTS (SELECT 1 FROM engagement."Notifications"
            GROUP BY "TenantId","VesselId","RuleKey","PolicyDigest","PlanId","PlanRevision","SourceContextHash" HAVING count(*) > 1)
          THEN RAISE EXCEPTION 'Recurring history cannot be collapsed into the older schema'; END IF;
        END $$;
        DROP TABLE engagement."NotificationEpisodes";
        DROP TABLE engagement."NotificationDispatchStates";
        DROP FUNCTION engagement.guard_notification_episode();
        DROP FUNCTION engagement.guard_notification_dispatch();
        DROP INDEX engagement."UX_Notification_SourceTrigger";
        ALTER TABLE engagement."Notifications" DROP COLUMN "Episode";
        CREATE UNIQUE INDEX "UX_Notification_SourceTrigger" ON engagement."Notifications"
          ("TenantId","VesselId","RuleKey","PolicyDigest","PlanId","PlanRevision","SourceContextHash");
        """);
}
