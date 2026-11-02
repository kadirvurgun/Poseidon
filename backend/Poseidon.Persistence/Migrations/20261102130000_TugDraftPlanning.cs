using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// AUTHORED_UNVERIFIED: provider generation, model parity and application are DEFERRED.
public partial class TugDraftPlanning : Migration
{
    protected override void Up(MigrationBuilder b) {
        b.Sql("""
CREATE TABLE tug."DraftPlans" (
 "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL,
 "Revision" bigint NOT NULL, "RequestRevision" bigint NOT NULL,
 "PolicyVersion" character varying(80) NOT NULL, "SourceVersion" character varying(80) NOT NULL,
 "PlannedStart" timestamp with time zone, "PlannedEnd" timestamp with time zone, "SavedAt" timestamp with time zone NOT NULL,
 CONSTRAINT "PK_DraftPlans" PRIMARY KEY ("TenantId","VesselId","JobId"),
 CONSTRAINT "CK_DraftPlan_Window" CHECK ("PlannedStart" IS NULL OR "PlannedEnd" IS NULL OR "PlannedEnd" >= "PlannedStart"),
 CONSTRAINT "FK_DraftPlans_JobRevisions" FOREIGN KEY ("TenantId","VesselId","JobId","RequestRevision") REFERENCES tug."JobRevisions" ("TenantId","VesselId","JobId","Revision") ON DELETE RESTRICT
);
CREATE INDEX "IX_DraftPlans_TenantId_VesselId_JobId_RequestRevision" ON tug."DraftPlans" ("TenantId","VesselId","JobId","RequestRevision");
CREATE TABLE tug."DraftPlanRevisions" (
 "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL, "Revision" bigint NOT NULL,
 "RequestRevision" bigint NOT NULL, "PolicyVersion" character varying(80) NOT NULL, "SourceVersion" character varying(80) NOT NULL,
 "Operation" character varying(16) NOT NULL, "ContentJson" jsonb NOT NULL, "Provenance" character varying(40) NOT NULL, "SavedAt" timestamp with time zone NOT NULL,
 CONSTRAINT "PK_DraftPlanRevisions" PRIMARY KEY ("TenantId","VesselId","JobId","Revision"),
 CONSTRAINT "FK_DraftPlanRevisions_Plan" FOREIGN KEY ("TenantId","VesselId","JobId") REFERENCES tug."DraftPlans" ("TenantId","VesselId","JobId") ON DELETE RESTRICT,
 CONSTRAINT "FK_DraftPlanRevisions_Request" FOREIGN KEY ("TenantId","VesselId","JobId","RequestRevision") REFERENCES tug."JobRevisions" ("TenantId","VesselId","JobId","Revision") ON DELETE RESTRICT
);
CREATE INDEX "IX_DraftPlanRevisions_TenantId_VesselId_JobId_RequestRevision" ON tug."DraftPlanRevisions" ("TenantId","VesselId","JobId","RequestRevision");
CREATE TABLE tug."DraftPlanTugs" (
 "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "JobId" uuid NOT NULL, "Revision" bigint NOT NULL, "TugId" uuid NOT NULL,
 CONSTRAINT "PK_DraftPlanTugs" PRIMARY KEY ("TenantId","VesselId","JobId","Revision","TugId"),
 CONSTRAINT "FK_DraftPlanTugs_Revision" FOREIGN KEY ("TenantId","VesselId","JobId","Revision") REFERENCES tug."DraftPlanRevisions" ("TenantId","VesselId","JobId","Revision") ON DELETE RESTRICT,
 CONSTRAINT "FK_DraftPlanTugs_Tug" FOREIGN KEY ("TenantId","TugId") REFERENCES core."Vessels" ("TenantId","Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_DraftPlanTugs_TenantId_TugId" ON tug."DraftPlanTugs" ("TenantId","TugId");
CREATE TRIGGER draft_plan_revision_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON tug."DraftPlanRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
CREATE TRIGGER draft_plan_tug_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON tug."DraftPlanTugs" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
CREATE FUNCTION tug.guard_draft_plan_tug() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM core."Vessels" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."TugId" AND "Kind"='Tug') THEN RAISE EXCEPTION 'invalid_tug_reference'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER draft_plan_tug_kind BEFORE INSERT ON tug."DraftPlanTugs" FOR EACH ROW EXECUTE FUNCTION tug.guard_draft_plan_tug();
CREATE FUNCTION tug.guard_planned_vessel_kind() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW."Kind" <> 'Tug' AND EXISTS (SELECT 1 FROM tug."DraftPlanTugs" WHERE "TenantId"=NEW."TenantId" AND "TugId"=NEW."Id") THEN RAISE EXCEPTION 'referenced_tug_kind_immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER planned_vessel_kind BEFORE UPDATE OF "Kind" ON core."Vessels" FOR EACH ROW EXECUTE FUNCTION tug.guard_planned_vessel_kind();
CREATE FUNCTION tug.guard_draft_plan() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'immutable_plan_identity'; END IF;
 IF (NEW."TenantId",NEW."VesselId",NEW."JobId") IS DISTINCT FROM (OLD."TenantId",OLD."VesselId",OLD."JobId") OR NEW."Revision" != OLD."Revision"+1 THEN RAISE EXCEPTION 'invalid_plan_revision'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER draft_plan_identity BEFORE UPDATE OR DELETE ON tug."DraftPlans" FOR EACH ROW EXECUTE FUNCTION tug.guard_draft_plan();
CREATE TRIGGER draft_plan_truncate BEFORE TRUNCATE ON tug."DraftPlans" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
""");
    }
    protected override void Down(MigrationBuilder b) {
        b.Sql("""
DROP TABLE tug."DraftPlanTugs";
DROP TABLE tug."DraftPlanRevisions";
DROP TABLE tug."DraftPlans";
DROP TRIGGER planned_vessel_kind ON core."Vessels";
DROP FUNCTION tug.guard_planned_vessel_kind();
DROP FUNCTION tug.guard_draft_plan_tug();
DROP FUNCTION tug.guard_draft_plan();
""");
    }
}
