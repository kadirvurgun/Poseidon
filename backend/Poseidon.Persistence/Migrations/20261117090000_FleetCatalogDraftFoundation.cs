using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Authored migration. Model snapshot hand-reconciled at S2 integration (2026-11-18); real application deferred.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261117090000_FleetCatalogDraftFoundation")]
public sealed class FleetCatalogDraftFoundation : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE SCHEMA IF NOT EXISTS fleet;
        CREATE TABLE fleet."CatalogDrafts" (
          "TenantId" uuid NOT NULL,
          "Id" uuid NOT NULL, "Revision" bigint NOT NULL,
          CONSTRAINT "PK_CatalogDrafts" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "FK_FleetCatalogDraft_Tenant" FOREIGN KEY ("TenantId") REFERENCES core."Tenants"("Id") ON DELETE RESTRICT,
          CONSTRAINT "CK_FleetCatalogDrafts_Revision" CHECK ("Revision" > 0));
        CREATE TABLE fleet."CatalogDraftRevisions" (
          "TenantId" uuid NOT NULL, "Id" uuid NOT NULL, "CatalogId" uuid NOT NULL,
          "Revision" bigint NOT NULL, "PriorRevisionId" uuid NULL, "CommandId" uuid NULL,
          "ExpectedRevision" bigint NOT NULL, "ActorId" varchar(300) NOT NULL,
          "ContentJson" text NOT NULL, "ContentHash" varchar(64) NOT NULL, "IntentHash" varchar(64) NOT NULL,
          "Contract" varchar(64) NOT NULL,
          "RecordedAt" timestamptz NOT NULL, "Provenance" varchar(32) NOT NULL,
          CONSTRAINT "PK_CatalogDraftRevisions" PRIMARY KEY ("TenantId","Id"),
          CONSTRAINT "AK_FleetCatalogDraftRevision_Lineage" UNIQUE ("TenantId","CatalogId","Id","Revision"),
          CONSTRAINT "CK_FleetCatalogDraftRevisions" CHECK (
            "Revision" > 0 AND "ExpectedRevision" = "Revision" - 1 AND ("PriorRevisionId" IS NULL) = ("Revision" = 1)
            AND "Provenance" IN ('DeclaredUnverified','SyntheticUnverified')
            AND ("CommandId" IS NOT NULL OR "Provenance" = 'SyntheticUnverified')),
          CONSTRAINT "FK_FleetCatalogDraftRevision_Head" FOREIGN KEY ("TenantId","CatalogId") REFERENCES fleet."CatalogDrafts"("TenantId","Id") ON DELETE RESTRICT,
          CONSTRAINT "FK_FleetCatalogDraftRevision_Prior" FOREIGN KEY ("TenantId","CatalogId","PriorRevisionId","ExpectedRevision")
            REFERENCES fleet."CatalogDraftRevisions"("TenantId","CatalogId","Id","Revision") ON DELETE RESTRICT);
        CREATE UNIQUE INDEX "IX_FleetCatalogDraftRevision_Revision" ON fleet."CatalogDraftRevisions"("TenantId","CatalogId","Revision");
        CREATE UNIQUE INDEX "IX_FleetCatalogDraftRevision_Command" ON fleet."CatalogDraftRevisions"("TenantId","CommandId");
        CREATE INDEX "IX_FleetCatalogDraftRevision_Prior" ON fleet."CatalogDraftRevisions"("TenantId","CatalogId","PriorRevisionId","ExpectedRevision");
        CREATE FUNCTION fleet.guard_catalog_draft_revision() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Catalog draft history is immutable' USING ERRCODE='55000'; END IF;
          IF NEW."Id" = '00000000-0000-0000-0000-000000000000'::uuid OR length(NEW."ActorId") = 0
             OR NEW."ContentHash" !~ '^[0-9a-f]{64}$' OR NEW."IntentHash" !~ '^[0-9a-f]{64}$'
             OR NEW."Contract" <> 'FLEET_TYPE_CATALOG_DRAFT_V1' OR octet_length(NEW."ContentJson") > 1048576
             OR NEW."CommandId" = '00000000-0000-0000-0000-000000000000'::uuid
             OR (NEW."Provenance" = 'SyntheticUnverified' AND (NEW."Revision" <> 1 OR NEW."CommandId" IS NOT NULL OR NEW."ContentJson"::jsonb->'entries' IS DISTINCT FROM '[]'::jsonb))
          THEN RAISE EXCEPTION 'Invalid catalog draft lineage' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "GuardCatalogDraftRevision" BEFORE INSERT OR UPDATE OR DELETE ON fleet."CatalogDraftRevisions"
          FOR EACH ROW EXECUTE FUNCTION fleet.guard_catalog_draft_revision();
        CREATE TRIGGER "GuardCatalogDraftRevisionTruncate" BEFORE TRUNCATE ON fleet."CatalogDraftRevisions"
          FOR EACH STATEMENT EXECUTE FUNCTION fleet.guard_catalog_draft_revision();
        CREATE FUNCTION fleet.guard_catalog_draft_head() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Catalog draft roots cannot be removed' USING ERRCODE='55000'; END IF;
          IF NEW."Id" = '00000000-0000-0000-0000-000000000000'::uuid
             OR (TG_OP = 'INSERT' AND NEW."Revision" <> 1)
             OR (TG_OP = 'UPDATE' AND (NEW."TenantId" <> OLD."TenantId" OR NEW."Id" <> OLD."Id" OR NEW."Revision" <> OLD."Revision" + 1))
          THEN RAISE EXCEPTION 'Invalid catalog draft head' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "GuardCatalogDraftHead" BEFORE INSERT OR UPDATE OR DELETE ON fleet."CatalogDrafts"
          FOR EACH ROW EXECUTE FUNCTION fleet.guard_catalog_draft_head();
        CREATE TRIGGER "GuardCatalogDraftHeadTruncate" BEFORE TRUNCATE ON fleet."CatalogDrafts"
          FOR EACH STATEMENT EXECUTE FUNCTION fleet.guard_catalog_draft_head();
        CREATE FUNCTION fleet.require_catalog_draft_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM fleet."CatalogDraftRevisions" r WHERE r."TenantId"=NEW."TenantId" AND r."CatalogId"=NEW."Id" AND r."Revision"=NEW."Revision")
          THEN RAISE EXCEPTION 'Catalog draft snapshot missing' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE CONSTRAINT TRIGGER "RequireCatalogDraftSnapshot" AFTER INSERT OR UPDATE ON fleet."CatalogDrafts"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fleet.require_catalog_draft_snapshot();
        """);

    protected override void Down(MigrationBuilder m) => m.Sql("""
        DROP TABLE fleet."CatalogDraftRevisions";
        DROP TABLE fleet."CatalogDrafts";
        DROP FUNCTION fleet.require_catalog_draft_snapshot();
        DROP FUNCTION fleet.guard_catalog_draft_head();
        DROP FUNCTION fleet.guard_catalog_draft_revision();
        """);
}
