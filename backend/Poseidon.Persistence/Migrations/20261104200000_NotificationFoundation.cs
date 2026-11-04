using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Source-authored migration; actual scaffolding/parity/application wait for the PR gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261104200000_NotificationFoundation")]
public sealed class NotificationFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("engagement");
        migrationBuilder.Sql("""
            CREATE TABLE engagement."Notifications" (
              "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "VesselId" uuid NOT NULL,
              "PlanId" uuid NOT NULL, "PlanRevision" bigint NOT NULL, "RuleKey" varchar(64) NOT NULL,
              "PolicyVersion" varchar(80) NOT NULL, "PolicyDigest" varchar(64) NOT NULL, "SourceContextHash" varchar(64) NOT NULL,
              "SourceState" varchar(24) NOT NULL, "Severity" varchar(16) NOT NULL, "Mandatory" boolean NOT NULL,
              "RecipientRoleId" uuid NOT NULL, "EscalationRoleId" uuid, "DueDate" date,
              "EscalateAt" timestamptz, "CreatedAt" timestamptz NOT NULL, "State" varchar(24) NOT NULL,
              "Revision" bigint NOT NULL,
              CONSTRAINT "PK_Notifications" PRIMARY KEY ("TenantId", "Id"),
              CONSTRAINT "AK_Notifications_TenantId_VesselId_Id" UNIQUE ("TenantId", "VesselId", "Id"),
              CONSTRAINT "FK_Notification_SourceRevision" FOREIGN KEY ("TenantId", "VesselId", "PlanId", "PlanRevision")
                REFERENCES technical."PlanRevisions" ("TenantId", "VesselId", "PlanId", "Revision") ON DELETE RESTRICT,
              CONSTRAINT "CK_Notification_State" CHECK (
                "State" IN ('Open','Acknowledged','Escalated','SourceChanged','SyntheticUnverified')
                AND "Revision" > 0 AND "PlanRevision" > 0
                AND "Severity" IN ('Info','Warning','Critical','Unknown') AND ("Severity" <> 'Critical' OR "Mandatory"))
            );
            CREATE UNIQUE INDEX "UX_Notification_SourceTrigger" ON engagement."Notifications"
              ("TenantId", "VesselId", "RuleKey", "PolicyDigest", "PlanId", "PlanRevision", "SourceContextHash");
            CREATE INDEX "IX_Notification_SourceRevision" ON engagement."Notifications" ("TenantId", "VesselId", "PlanId", "PlanRevision");
            CREATE TABLE engagement."NotificationEvents" (
              "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "VesselId" uuid NOT NULL, "NotificationId" uuid NOT NULL,
              "Sequence" bigint NOT NULL, "Kind" varchar(24) NOT NULL, "ActorId" uuid, "RecordedAt" timestamptz NOT NULL,
              CONSTRAINT "PK_NotificationEvents" PRIMARY KEY ("TenantId", "Id"),
              CONSTRAINT "FK_NotificationEvent_Notification" FOREIGN KEY ("TenantId", "VesselId", "NotificationId")
                REFERENCES engagement."Notifications" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "UX_NotificationEvent_Sequence" ON engagement."NotificationEvents" ("TenantId", "NotificationId", "Sequence");
            CREATE INDEX "IX_NotificationEvent_Notification" ON engagement."NotificationEvents" ("TenantId", "VesselId", "NotificationId");
            CREATE TABLE engagement."NotificationPreferences" (
              "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL,
              "MutedAdvisory" boolean NOT NULL, "Revision" bigint NOT NULL,
              CONSTRAINT "PK_NotificationPreferences" PRIMARY KEY ("TenantId", "VesselId", "ActorId"),
              CONSTRAINT "FK_NotificationPreference_Vessel" FOREIGN KEY ("TenantId", "VesselId")
                REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
            );
            CREATE TABLE engagement."NotificationReceipts" (
              "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL,
              "Action" varchar(24) NOT NULL, "ResourceId" uuid NOT NULL, "NotificationId" uuid, "PreferenceActorId" uuid,
              "Fingerprint" varchar(64) NOT NULL, "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
              CONSTRAINT "PK_NotificationReceipts" PRIMARY KEY ("TenantId", "ActorId", "Key"),
              CONSTRAINT "FK_NotificationReceipt_Notification" FOREIGN KEY ("TenantId", "VesselId", "NotificationId")
                REFERENCES engagement."Notifications" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT,
              CONSTRAINT "FK_NotificationReceipt_Preference" FOREIGN KEY ("TenantId", "VesselId", "PreferenceActorId")
                REFERENCES engagement."NotificationPreferences" ("TenantId", "VesselId", "ActorId") ON DELETE RESTRICT,
              CONSTRAINT "CK_Notification_Receipt" CHECK (
                ("Action" = 'Acknowledge' AND "NotificationId" IS NOT NULL AND "ResourceId" = "NotificationId" AND "PreferenceActorId" IS NULL)
                OR ("Action" = 'Preference' AND "NotificationId" IS NULL AND "PreferenceActorId" IS NOT NULL AND "PreferenceActorId" = "ActorId" AND "ResourceId" = "ActorId"))
            );
            CREATE INDEX "IX_NotificationReceipt_Notification" ON engagement."NotificationReceipts" ("TenantId", "VesselId", "NotificationId");
            CREATE INDEX "IX_NotificationReceipt_Preference" ON engagement."NotificationReceipts" ("TenantId", "VesselId", "PreferenceActorId");
            CREATE FUNCTION engagement.guard_notification() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Notification history cannot be removed'; END IF;
              IF (to_jsonb(OLD) - ARRAY['State','Revision']) IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['State','Revision'])
                OR NEW."Revision" <> OLD."Revision" + 1
                OR NOT ((OLD."State" = 'Open' AND NEW."State" IN ('Acknowledged','Escalated','SourceChanged'))
                  OR (OLD."State" = 'Escalated' AND NEW."State" IN ('Acknowledged','SourceChanged'))
                  OR (OLD."State" = 'Acknowledged' AND NEW."State" = 'SourceChanged')) THEN
                RAISE EXCEPTION 'Immutable notification source/routing or invalid transition';
              END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER "Notification_Guard" BEFORE UPDATE OR DELETE ON engagement."Notifications"
              FOR EACH ROW EXECUTE FUNCTION engagement.guard_notification();
            CREATE TRIGGER "Notification_NoTruncate" BEFORE TRUNCATE ON engagement."Notifications"
              FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            CREATE FUNCTION engagement.guard_notification_preference() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Notification preference history cannot be removed'; END IF;
              IF (to_jsonb(OLD) - ARRAY['MutedAdvisory','Revision']) IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['MutedAdvisory','Revision'])
                OR NEW."Revision" <> OLD."Revision" + 1 THEN RAISE EXCEPTION 'Immutable preference ownership'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER "NotificationPreference_Guard" BEFORE UPDATE OR DELETE ON engagement."NotificationPreferences"
              FOR EACH ROW EXECUTE FUNCTION engagement.guard_notification_preference();
            CREATE TRIGGER "NotificationPreference_NoTruncate" BEFORE TRUNCATE ON engagement."NotificationPreferences"
              FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            CREATE TRIGGER "NotificationEvent_Immutable" BEFORE UPDATE OR DELETE ON engagement."NotificationEvents"
              FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
            CREATE TRIGGER "NotificationEvent_NoTruncate" BEFORE TRUNCATE ON engagement."NotificationEvents"
              FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            CREATE TRIGGER "NotificationReceipt_Immutable" BEFORE UPDATE OR DELETE ON engagement."NotificationReceipts"
              FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
            CREATE TRIGGER "NotificationReceipt_NoTruncate" BEFORE TRUNCATE ON engagement."NotificationReceipts"
              FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE engagement."NotificationReceipts";
        DROP TABLE engagement."NotificationEvents";
        DROP TABLE engagement."NotificationPreferences";
        DROP TABLE engagement."Notifications";
        DROP FUNCTION engagement.guard_notification_preference();
        DROP FUNCTION engagement.guard_notification();
        """);
}
