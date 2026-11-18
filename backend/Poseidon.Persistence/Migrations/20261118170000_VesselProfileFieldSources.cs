using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// DEV-020 / T-S2-001. Append-only declared field provenance pinned to the exact immutable
// profile revision that introduced the value. Model snapshot spliced from the EF-generated
// model for exactly these two tables; DDL/guards only, business writes use audited SaveChanges.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261118170000_VesselProfileFieldSources")]
public sealed class VesselProfileFieldSources : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE core."VesselProfileFieldSources" (
          "TenantId" uuid NOT NULL, "VesselId" uuid NOT NULL, "FieldPath" varchar(64) NOT NULL, "Sequence" bigint NOT NULL,
          "ValueRevision" bigint NOT NULL, "ValueState" varchar(16) NOT NULL, "ValueHash" varchar(64) NOT NULL,
          "SourceKind" varchar(32) NOT NULL, "ReferenceState" varchar(16) NOT NULL, "Reference" varchar(500) NULL,
          "ObservedAtState" varchar(16) NOT NULL, "ObservedAt" timestamptz NULL,
          "DeclaredBy" uuid NOT NULL, "StewardCompanyId" uuid NOT NULL, "RecordedAt" timestamptz NOT NULL, "Provenance" varchar(32) NOT NULL,
          CONSTRAINT "PK_VesselProfileFieldSources" PRIMARY KEY ("TenantId","VesselId","FieldPath","Sequence"),
          CONSTRAINT "CK_VesselProfileFieldSources_Declaration" CHECK ("Sequence" > 0 AND "ValueRevision" > 0 AND "FieldPath" IN ('breadth','buildDate','cargoContext','classNumber','classSociety','constructionRecordReference','contractForConstructionDate','draft','dwt','flag','gt','imo','loa','mainPropulsionKw','nt','shipType','tradingArea') AND "SourceKind" IN ('DOCUMENT','REGISTRY_RECORD','CLASS_RECORD','OWNER_DECLARATION','SURVEY_OR_MEASUREMENT','OTHER','UNKNOWN') AND "ValueState" IN ('Missing','Unknown','Known') AND "ValueHash" ~ '^[0-9a-f]{64}$' AND "ReferenceState" IN ('Missing','Unknown','Known') AND (("ReferenceState" = 'Known') = ("Reference" IS NOT NULL)) AND "ObservedAtState" IN ('Missing','Unknown','Known') AND (("ObservedAtState" = 'Known') = ("ObservedAt" IS NOT NULL)) AND "Provenance" IN ('DeclaredUnverified','SyntheticUnverified') AND ("ValueState" <> 'Missing' OR ("Provenance" = 'SyntheticUnverified' AND "SourceKind" = 'UNKNOWN'))),
          CONSTRAINT "FK_VesselProfileFieldSource_ValueRevision" FOREIGN KEY ("TenantId","VesselId","ValueRevision")
            REFERENCES core."RegulatoryDraftRevisions" ("TenantId","VesselId","Revision") ON DELETE RESTRICT,
          CONSTRAINT "FK_VesselProfileFieldSource_StewardCompany" FOREIGN KEY ("TenantId","StewardCompanyId")
            REFERENCES core."Companies" ("TenantId","Id") ON DELETE RESTRICT);
        CREATE INDEX "IX_VesselProfileFieldSources_ValueRevision" ON core."VesselProfileFieldSources" ("TenantId","VesselId","ValueRevision");
        CREATE INDEX "IX_VesselProfileFieldSources_StewardCompany" ON core."VesselProfileFieldSources" ("TenantId","StewardCompanyId");
        CREATE TABLE core."VesselProfileFieldSourceReceipts" (
          "TenantId" uuid NOT NULL, "ActorId" uuid NOT NULL, "Key" varchar(80) NOT NULL, "VesselId" uuid NOT NULL,
          "FieldPath" varchar(64) NOT NULL, "Sequence" bigint NOT NULL, "Fingerprint" varchar(64) NOT NULL,
          "OutcomeJson" text NOT NULL, "SavedAt" timestamptz NOT NULL,
          CONSTRAINT "PK_VesselProfileFieldSourceReceipts" PRIMARY KEY ("TenantId","ActorId","Key"),
          CONSTRAINT "CK_VesselProfileFieldSourceReceipts_Sequence" CHECK ("Sequence" > 0),
          CONSTRAINT "FK_VesselProfileFieldSourceReceipt_Declaration" FOREIGN KEY ("TenantId","VesselId","FieldPath","Sequence")
            REFERENCES core."VesselProfileFieldSources" ("TenantId","VesselId","FieldPath","Sequence") ON DELETE RESTRICT);
        CREATE INDEX "IX_VesselProfileFieldSourceReceipts_Declaration" ON core."VesselProfileFieldSourceReceipts" ("TenantId","VesselId","FieldPath","Sequence");
        CREATE FUNCTION core.guard_vessel_profile_field_source() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE prior bigint;
        BEGIN
          IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Profile field provenance is immutable' USING ERRCODE='55000'; END IF;
          IF NEW."Sequence" > 1 THEN
            SELECT s."ValueRevision" INTO prior FROM core."VesselProfileFieldSources" s WHERE s."TenantId"=NEW."TenantId"
              AND s."VesselId"=NEW."VesselId" AND s."FieldPath"=NEW."FieldPath" AND s."Sequence"=NEW."Sequence"-1;
            IF prior IS NULL OR prior > NEW."ValueRevision" THEN
              RAISE EXCEPTION 'Invalid profile field provenance lineage' USING ERRCODE='23514'; END IF;
          END IF;
          IF NEW."DeclaredBy" = '00000000-0000-0000-0000-000000000000'::uuid AND NEW."Provenance" <> 'SyntheticUnverified' THEN
            RAISE EXCEPTION 'Profile field provenance requires an attributed declarer' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "GuardVesselProfileFieldSource" BEFORE INSERT OR UPDATE OR DELETE ON core."VesselProfileFieldSources"
          FOR EACH ROW EXECUTE FUNCTION core.guard_vessel_profile_field_source();
        CREATE TRIGGER "GuardVesselProfileFieldSourceTruncate" BEFORE TRUNCATE ON core."VesselProfileFieldSources"
          FOR EACH STATEMENT EXECUTE FUNCTION core.guard_vessel_profile_field_source();
        CREATE FUNCTION core.guard_vessel_profile_field_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          RAISE EXCEPTION 'Profile field provenance receipts are immutable' USING ERRCODE='55000';
        END $$;
        CREATE TRIGGER "GuardVesselProfileFieldReceipt" BEFORE UPDATE OR DELETE ON core."VesselProfileFieldSourceReceipts"
          FOR EACH ROW EXECUTE FUNCTION core.guard_vessel_profile_field_receipt();
        CREATE TRIGGER "GuardVesselProfileFieldReceiptTruncate" BEFORE TRUNCATE ON core."VesselProfileFieldSourceReceipts"
          FOR EACH STATEMENT EXECUTE FUNCTION core.guard_vessel_profile_field_receipt();
        """);

    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TABLE core."VesselProfileFieldSourceReceipts";
        DROP TABLE core."VesselProfileFieldSources";
        DROP FUNCTION core.guard_vessel_profile_field_receipt();
        DROP FUNCTION core.guard_vessel_profile_field_source();
        """);
}
