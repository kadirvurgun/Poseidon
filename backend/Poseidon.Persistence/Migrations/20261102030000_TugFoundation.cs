using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Poseidon.Persistence.Migrations;
public partial class TugFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) {
        // Controlled DDL only. Business writes use audited SaveChanges.
        migrationBuilder.Sql("""
CREATE SCHEMA IF NOT EXISTS tug;
CREATE TABLE tug."Owners" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"Id" uuid NOT NULL,
CONSTRAINT "PK_Owners" PRIMARY KEY ("TenantId", "VesselId", "Id"),
CONSTRAINT "FK_Owners_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
);
CREATE TABLE tug."Jobs" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"JobNumber" character varying(40) NOT NULL,
"RequestSource" text,
"CustomerRef" text,
"AssistedVessel" text,
"AssistedVesselImo" text,
"JobType" text,
"OperationArea" text,
"PickupPoint" text,
"DestinationPoint" text,
"SpecialRequirements" text,
"Priority" text,
"Notes" text,
"Status" text NOT NULL,
"SourceVersion" text NOT NULL,
"Provenance" text NOT NULL,
"RequestedStart" timestamp with time zone,
"RequestedEnd" timestamp with time zone,
"RequiredBollardPull" numeric(10,2),
"RequiredTugCount" integer,
"RequestOwner" uuid,
"Revision" bigint NOT NULL,
"SavedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_Jobs" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "AK_Jobs_TenantId_VesselId_Id" UNIQUE ("TenantId", "VesselId", "Id"),
CONSTRAINT "CK_TugJob_Draft" CHECK ("Status" = 'Draft'),
CONSTRAINT "CK_TugJob_Window" CHECK ("RequestedEnd" IS NULL OR "RequestedStart" IS NULL OR "RequestedEnd" >= "RequestedStart"),
CONSTRAINT "CK_TugJob_Count" CHECK ("RequiredTugCount" IS NULL OR "RequiredTugCount" BETWEEN 1 AND 1000),
CONSTRAINT "CK_TugJob_BP" CHECK ("RequiredBollardPull" IS NULL OR "RequiredBollardPull" >= 0),
CONSTRAINT "FK_Jobs_Vessels_TenantId_VesselId" FOREIGN KEY ("TenantId", "VesselId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT,
CONSTRAINT "FK_Jobs_Owners_TenantId_VesselId_RequestOwner" FOREIGN KEY ("TenantId", "VesselId", "RequestOwner") REFERENCES tug."Owners" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT
);
CREATE TABLE tug."JobRevisions" (
"TenantId" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"JobId" uuid NOT NULL,
"Revision" bigint NOT NULL,
"JobNumber" character varying(40) NOT NULL,
"ContentJson" jsonb NOT NULL,
"SourceVersion" text NOT NULL,
"Provenance" text NOT NULL,
"SavedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_JobRevisions" PRIMARY KEY ("TenantId", "VesselId", "JobId", "Revision"),
CONSTRAINT "FK_JobRevisions_Jobs_TenantId_VesselId_JobId" FOREIGN KEY ("TenantId", "VesselId", "JobId") REFERENCES tug."Jobs" ("TenantId", "VesselId", "Id") ON DELETE RESTRICT
);
CREATE TABLE tug."Assignments" (
"TenantId" uuid NOT NULL,
"Id" uuid NOT NULL,
"VesselId" uuid NOT NULL,
"JobId" uuid NOT NULL,
"TugId" uuid NOT NULL,
"JobRevision" bigint NOT NULL,
"DecisionReference" character varying(200) NOT NULL,
"RecordedAt" timestamp with time zone NOT NULL,
CONSTRAINT "PK_Assignments" PRIMARY KEY ("TenantId", "Id"),
CONSTRAINT "FK_Assignments_JobRevisions_TenantId_VesselId_JobId_JobRevision" FOREIGN KEY ("TenantId", "VesselId", "JobId", "JobRevision") REFERENCES tug."JobRevisions" ("TenantId", "VesselId", "JobId", "Revision") ON DELETE RESTRICT,
CONSTRAINT "FK_Assignments_Vessels_TenantId_TugId" FOREIGN KEY ("TenantId", "TugId") REFERENCES core."Vessels" ("TenantId", "Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX "IX_Jobs_TenantId_JobNumber" ON tug."Jobs" ("TenantId", "JobNumber");
CREATE INDEX "IX_Jobs_TenantId_VesselId_RequestedStart" ON tug."Jobs" ("TenantId", "VesselId", "RequestedStart");
CREATE INDEX "IX_Jobs_TenantId_VesselId_RequestOwner" ON tug."Jobs" ("TenantId", "VesselId", "RequestOwner");
CREATE UNIQUE INDEX "IX_Assignments_TenantId_VesselId_JobId_TugId" ON tug."Assignments" ("TenantId", "VesselId", "JobId", "TugId");
CREATE INDEX "IX_Assignments_TenantId_VesselId_JobId_JobRevision" ON tug."Assignments" ("TenantId", "VesselId", "JobId", "JobRevision");
CREATE INDEX "IX_Assignments_TenantId_TugId" ON tug."Assignments" ("TenantId", "TugId");
CREATE FUNCTION tug.reject_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Tug lineage is immutable'; END; $$;
CREATE TRIGGER immutable_tug BEFORE UPDATE OR DELETE OR TRUNCATE ON tug."JobRevisions" FOR EACH STATEMENT EXECUTE FUNCTION tug.reject_mutation();
CREATE TRIGGER immutable_tug BEFORE UPDATE OR DELETE OR TRUNCATE ON tug."Owners" FOR EACH STATEMENT EXECUTE FUNCTION tug.reject_mutation();
CREATE TRIGGER immutable_tug BEFORE UPDATE OR DELETE OR TRUNCATE ON tug."Assignments" FOR EACH STATEMENT EXECUTE FUNCTION tug.reject_mutation();
CREATE FUNCTION tug.guard_context() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM core."Vessels" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."VesselId" AND "Kind"='Tug') THEN
 RAISE EXCEPTION 'Tug context required' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND (OLD."VesselId"<>NEW."VesselId" OR OLD."JobNumber"<>NEW."JobNumber") THEN
 RAISE EXCEPTION 'Tug request identity is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER tug_context BEFORE INSERT OR UPDATE ON tug."Jobs" FOR EACH ROW EXECUTE FUNCTION tug.guard_context();
CREATE FUNCTION tug.guard_assignment_tug() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM core."Vessels" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."TugId" AND "Kind"='Tug') THEN
 RAISE EXCEPTION 'Assignment tug context required' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER assignment_tug BEFORE INSERT ON tug."Assignments" FOR EACH ROW EXECUTE FUNCTION tug.guard_assignment_tug();
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("""
DROP TABLE tug."Assignments";
DROP TABLE tug."JobRevisions";
DROP TABLE tug."Jobs";
DROP TABLE tug."Owners";
DROP FUNCTION tug.guard_assignment_tug();
DROP FUNCTION tug.guard_context();
DROP FUNCTION tug.reject_mutation();
""");
    }
}
