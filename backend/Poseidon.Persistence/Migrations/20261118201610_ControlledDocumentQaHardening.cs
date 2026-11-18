using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // PLAN-1511-5802 independent security QA follow-up (F1, F2, F4). Additive on top of
    // 20261118182029_ControlledDocumentChain, which is not edited: hold releaser column and
    // check, one-EFFECTIVE partial unique index, the DISPOSAL_REQUEST_INVALIDATED custody event
    // and replacement guard functions that mirror ControlledDocumentStorage.Guard/Chain.
    [DbContext(typeof(PoseidonDbContext))]
    [Migration("20261118201610_ControlledDocumentQaHardening")]
    public partial class ControlledDocumentQaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ControlledDocumentCustodyEvents",
                schema: "files",
                table: "ControlledDocumentCustodyEvents");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HoldReleasedAt",
                schema: "files",
                table: "ControlledDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HoldReleasedBy",
                schema: "files",
                table: "ControlledDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ControlledDocuments_HoldRelease",
                schema: "files",
                table: "ControlledDocuments",
                sql: "(\"HoldReleasedBy\" IS NULL) = (\"HoldReleasedAt\" IS NULL) AND (\"HoldReleasedBy\" IS NULL OR (NOT \"HoldActive\" AND \"HoldPlacedBy\" IS NOT NULL AND \"HoldReleasedBy\" <> \"HoldPlacedBy\"))");

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentRevisions_Effective",
                schema: "files",
                table: "ControlledDocumentRevisions",
                columns: new[] { "TenantId", "DocumentId" },
                unique: true,
                filter: "\"PublicationState\" = 'EFFECTIVE'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ControlledDocumentCustodyEvents",
                schema: "files",
                table: "ControlledDocumentCustodyEvents",
                sql: "\"Sequence\" > 0 AND \"EventType\" IN ('CREATED','RECEIVED','QUARANTINED','SCANNED_CLEAN','RELEASED','PUBLISHED','SUPERSEDED','VERIFICATION_RECORDED','HOLD_PLACED','HOLD_RELEASED','RETENTION_SET','DISPOSAL_REQUESTED','DISPOSAL_APPROVED','LINKED','DISPOSAL_REQUEST_INVALIDATED') AND \"Source\" IN ('COMMAND','SECURE_FILE_PIPELINE','SYNTHETIC_SEED') AND length(\"RecordedBy\") > 0 AND (\"Revision\" IS NULL OR \"Revision\" > 0) AND (\"Source\" <> 'SECURE_FILE_PIPELINE' OR \"SubjectActorId\" IS NOT NULL)");

            migrationBuilder.Sql("""
                -- Document row: forward-only disposal (NONE->REQUESTED->APPROVED; REQUESTED->NONE only as
                -- a recorded invalidation), hold release by another actor, closed active use (58.02 §271)
                -- and no back-dated retention.
                CREATE OR REPLACE FUNCTION files.guard_controlled_document() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE today date := (now() AT TIME ZONE 'UTC')::date;
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Controlled documents cannot be removed; disposal is a recorded state' USING ERRCODE='55000'; END IF;
                  IF TG_OP = 'INSERT' THEN
                    IF NEW."LatestRevision" <> 0 OR NEW."CurrentRevision" IS NOT NULL OR NEW."DisposalState" <> 'NONE' OR NEW."HoldActive" OR NEW."HoldReleasedBy" IS NOT NULL
                    THEN RAISE EXCEPTION 'A controlled document starts empty, unheld and undisposed' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                  END IF;
                  IF (NEW."TenantId", NEW."Id", NEW."VesselId", NEW."Code", NEW."Title", NEW."Classification", NEW."CreatedBy", NEW."CreatedAt", NEW."Provenance")
                     IS DISTINCT FROM (OLD."TenantId", OLD."Id", OLD."VesselId", OLD."Code", OLD."Title", OLD."Classification", OLD."CreatedBy", OLD."CreatedAt", OLD."Provenance")
                  THEN RAISE EXCEPTION 'Controlled document identity is immutable' USING ERRCODE='55000'; END IF;
                  IF NEW."Sequence" <= OLD."Sequence" OR NEW."LatestRevision" < OLD."LatestRevision"
                     OR (NEW."DisposalState" <> OLD."DisposalState" AND NOT ((OLD."DisposalState" = 'NONE' AND NEW."DisposalState" = 'REQUESTED')
                         OR (OLD."DisposalState" = 'REQUESTED' AND NEW."DisposalState" IN ('NONE','APPROVED'))))
                     OR (OLD."DisposalState" = 'REQUESTED' AND NEW."DisposalState" = 'APPROVED'
                         AND (NEW."DisposalRequestedBy", NEW."DisposalRequestedAt") IS DISTINCT FROM (OLD."DisposalRequestedBy", OLD."DisposalRequestedAt"))
                     OR (OLD."DisposalState" <> 'NONE' AND (NEW."RetentionPolicyRef" IS DISTINCT FROM OLD."RetentionPolicyRef" OR NEW."RetainUntil" IS DISTINCT FROM OLD."RetainUntil"))
                     OR (NEW."RetainUntil" IS DISTINCT FROM OLD."RetainUntil" AND NEW."RetainUntil" < today)
                     OR (OLD."HoldActive" AND NOT NEW."HoldActive" AND (NEW."HoldReleasedBy" IS NULL
                         OR (NEW."HoldPlacedBy", NEW."HoldPlacedAt", NEW."HoldAuthorityRef") IS DISTINCT FROM (OLD."HoldPlacedBy", OLD."HoldPlacedAt", OLD."HoldAuthorityRef")))
                     OR (OLD."HoldActive" AND NEW."HoldActive"
                         AND (NEW."HoldPlacedBy", NEW."HoldPlacedAt", NEW."HoldAuthorityRef") IS DISTINCT FROM (OLD."HoldPlacedBy", OLD."HoldPlacedAt", OLD."HoldAuthorityRef"))
                     OR (NOT OLD."HoldActive" AND NOT NEW."HoldActive"
                         AND (NEW."HoldPlacedBy", NEW."HoldPlacedAt", NEW."HoldReleasedBy", NEW."HoldReleasedAt") IS DISTINCT FROM (OLD."HoldPlacedBy", OLD."HoldPlacedAt", OLD."HoldReleasedBy", OLD."HoldReleasedAt"))
                     OR (NOT OLD."HoldActive" AND NEW."HoldActive" AND NEW."HoldReleasedBy" IS NOT NULL)
                  THEN RAISE EXCEPTION 'Controlled document state only advances' USING ERRCODE='23514'; END IF;
                  IF NEW."DisposalState" <> OLD."DisposalState" AND NEW."DisposalState" IN ('REQUESTED','APPROVED') THEN
                    IF NEW."HoldActive" OR NEW."RetainUntil" > today
                    THEN RAISE EXCEPTION 'Legal hold or unelapsed retention blocks disposal' USING ERRCODE='23514'; END IF;
                    IF EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."DocumentId" = NEW."Id" AND r."PublicationState" = 'EFFECTIVE')
                       OR EXISTS (SELECT 1 FROM files."ControlledDocumentCertificateLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."DocumentId" = NEW."Id")
                       OR EXISTS (SELECT 1 FROM files."ControlledDocumentEvidenceLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."DocumentId" = NEW."Id")
                       OR EXISTS (SELECT 1 FROM files."ControlledDocumentCredentialLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."DocumentId" = NEW."Id")
                    THEN RAISE EXCEPTION 'Disposal requires closed active use' USING ERRCODE='23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;

                -- Deferred: gap-free custody up to the document sequence, and every hold/disposal
                -- transition carries its custody event recorded by the attributed actor.
                CREATE OR REPLACE FUNCTION files.require_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (SELECT count(*) FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id"
                        AND c."Sequence" <= NEW."Sequence") <> NEW."Sequence"
                  THEN RAISE EXCEPTION 'Controlled document change requires its gap-free custody events' USING ERRCODE='23514'; END IF;
                  IF TG_OP = 'UPDATE' THEN
                    IF (OLD."HoldActive" AND NOT NEW."HoldActive" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                          AND c."DocumentId" = NEW."Id" AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence" AND c."EventType" = 'HOLD_RELEASED'
                          AND c."RecordedBy" = NEW."HoldReleasedBy"::text))
                       OR (NOT OLD."HoldActive" AND NEW."HoldActive" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                          AND c."DocumentId" = NEW."Id" AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence" AND c."EventType" = 'HOLD_PLACED'
                          AND c."RecordedBy" = NEW."HoldPlacedBy"::text))
                       OR (OLD."DisposalState" <> 'REQUESTED' AND NEW."DisposalState" = 'REQUESTED' AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c
                          WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id" AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence"
                          AND c."EventType" = 'DISPOSAL_REQUESTED' AND c."RecordedBy" = NEW."DisposalRequestedBy"::text))
                       OR (OLD."DisposalState" <> 'APPROVED' AND NEW."DisposalState" = 'APPROVED' AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c
                          WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id" AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence"
                          AND c."EventType" = 'DISPOSAL_APPROVED' AND c."RecordedBy" = NEW."DisposalApprovedBy"::text))
                       OR (OLD."DisposalState" = 'REQUESTED' AND NEW."DisposalState" = 'NONE' AND NOT (
                          EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id"
                            AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence" AND c."EventType" = 'DISPOSAL_REQUEST_INVALIDATED')
                          AND EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id"
                            AND c."Sequence" > OLD."Sequence" AND c."Sequence" <= NEW."Sequence" AND c."EventType" IN ('HOLD_PLACED','LINKED'))))
                    THEN RAISE EXCEPTION 'Hold and disposal transitions require their attributed custody event' USING ERRCODE='23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;

                -- Deferred custody insert: the document reached this sequence and the predecessor exists.
                CREATE OR REPLACE FUNCTION files.guard_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId"
                       AND d."VesselId" = NEW."VesselId" AND d."Sequence" >= NEW."Sequence")
                     OR (NEW."Sequence" > 1 AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                       AND c."DocumentId" = NEW."DocumentId" AND c."Sequence" = NEW."Sequence" - 1))
                  THEN RAISE EXCEPTION 'Custody event requires its document sequence without gaps' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                -- Revision row: registration only while undisposed; release needs CleanTransport and a
                -- StoredFile with the pinned hash/length/MIME; no publish older than a published revision.
                CREATE OR REPLACE FUNCTION files.guard_controlled_document_revision() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Controlled document revisions cannot be removed' USING ERRCODE='55000'; END IF;
                  IF TG_OP = 'INSERT' THEN
                    IF NEW."IntakeState" <> 'QUARANTINED' OR NEW."PublicationState" <> 'UNPUBLISHED' OR NEW."ObjectGeneration" IS NOT NULL
                       OR NOT EXISTS (SELECT 1 FROM files."FileStages" s WHERE s."TenantId" = NEW."TenantId" AND s."Id" = NEW."FileId" AND s."VesselId" = NEW."VesselId"
                         AND s."PersonId" IS NULL AND s."DocumentId" IS NULL AND s."ExpectedChecksum" = NEW."ContentSha256"
                         AND s."ExpectedLength" = NEW."ContentLength" AND s."Mime" = NEW."Mime" AND s."ActorId" = NEW."UploadedBy")
                       OR NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."DisposalState" = 'NONE')
                    THEN RAISE EXCEPTION 'A registered revision starts quarantined from its exact transport intent' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                  END IF;
                  IF (NEW."TenantId", NEW."DocumentId", NEW."Revision", NEW."VesselId", NEW."FileId", NEW."ContentSha256", NEW."ContentLength", NEW."Mime",
                      NEW."UploadedBy", NEW."RegisteredBy", NEW."RegisteredAt")
                     IS DISTINCT FROM (OLD."TenantId", OLD."DocumentId", OLD."Revision", OLD."VesselId", OLD."FileId", OLD."ContentSha256", OLD."ContentLength", OLD."Mime",
                      OLD."UploadedBy", OLD."RegisteredBy", OLD."RegisteredAt")
                  THEN RAISE EXCEPTION 'Controlled document revision content is immutable' USING ERRCODE='55000'; END IF;
                  IF (NEW."IntakeState" <> OLD."IntakeState" AND NOT (OLD."IntakeState" = 'QUARANTINED' AND NEW."IntakeState" = 'RELEASED'))
                     OR (NEW."PublicationState" <> OLD."PublicationState" AND NOT ((OLD."PublicationState" = 'UNPUBLISHED' AND NEW."PublicationState" = 'EFFECTIVE')
                         OR (OLD."PublicationState" = 'EFFECTIVE' AND NEW."PublicationState" = 'SUPERSEDED')))
                     OR (OLD."ObjectGeneration" IS NOT NULL AND NEW."ObjectGeneration" IS DISTINCT FROM OLD."ObjectGeneration")
                     OR (OLD."ReleasedAt" IS NOT NULL AND NEW."ReleasedAt" IS DISTINCT FROM OLD."ReleasedAt")
                     OR (OLD."PublishedBy" IS NOT NULL AND (NEW."PublishedBy" IS DISTINCT FROM OLD."PublishedBy" OR NEW."PublishedAt" IS DISTINCT FROM OLD."PublishedAt"))
                  THEN RAISE EXCEPTION 'Controlled document revision states only advance' USING ERRCODE='23514'; END IF;
                  IF OLD."IntakeState" = 'QUARANTINED' AND NEW."IntakeState" = 'RELEASED' AND (NOT EXISTS (SELECT 1 FROM files."StoredFiles" f
                       WHERE f."TenantId" = NEW."TenantId" AND f."Id" = NEW."FileId" AND f."VesselId" = NEW."VesselId" AND f."Checksum" = NEW."ContentSha256"
                         AND f."Length" = NEW."ContentLength" AND f."Mime" = NEW."Mime")
                     OR NOT EXISTS (SELECT 1 FROM files."FileStages" s WHERE s."TenantId" = NEW."TenantId" AND s."Id" = NEW."FileId" AND s."VesselId" = NEW."VesselId"
                       AND s."State" = 'CleanTransport')
                     OR EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."DisposalState" = 'APPROVED'))
                  THEN RAISE EXCEPTION 'Release requires clean transport bytes with the pinned hash, length and MIME' USING ERRCODE='23514'; END IF;
                  IF OLD."PublicationState" = 'UNPUBLISHED' AND NEW."PublicationState" = 'EFFECTIVE' AND (
                       NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."DisposalState" = 'NONE')
                       OR EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."DocumentId" = NEW."DocumentId"
                         AND r."Revision" > NEW."Revision" AND r."PublicationState" <> 'UNPUBLISHED'))
                  THEN RAISE EXCEPTION 'Publication requires an undisposed document and no newer published revision' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.require_controlled_revision_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (NEW."IntakeState" <> OLD."IntakeState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = 'RELEASED'))
                     OR (NEW."PublicationState" <> OLD."PublicationState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = CASE NEW."PublicationState" WHEN 'EFFECTIVE' THEN 'PUBLISHED' ELSE 'SUPERSEDED' END
                        AND (NEW."PublicationState" <> 'EFFECTIVE' OR c."RecordedBy" = NEW."PublishedBy"::text)))
                  THEN RAISE EXCEPTION 'Revision state change requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                -- Deferred revision insert: RECEIVED (registrar, uploader subject) + QUARANTINED custody,
                -- LatestRevision bumped and no revision-number gap.
                CREATE FUNCTION files.require_controlled_revision_registration() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."DocumentId"
                       AND c."Revision" = NEW."Revision" AND c."EventType" = 'RECEIVED' AND c."SubjectActorId" = NEW."UploadedBy" AND c."RecordedBy" = NEW."RegisteredBy"::text)
                     OR NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."DocumentId"
                       AND c."Revision" = NEW."Revision" AND c."EventType" = 'QUARANTINED')
                     OR NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."LatestRevision" >= NEW."Revision")
                     OR (NEW."Revision" > 1 AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId"
                       AND r."DocumentId" = NEW."DocumentId" AND r."Revision" = NEW."Revision" - 1))
                  THEN RAISE EXCEPTION 'Revision registration requires its custody events and revision counter' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "RequireControlledRevisionRegistration" AFTER INSERT ON files."ControlledDocumentRevisions"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_revision_registration();

                CREATE OR REPLACE FUNCTION files.guard_controlled_document_verification() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."DocumentId" = NEW."DocumentId"
                       AND r."Revision" = NEW."Revision" AND r."VesselId" = NEW."VesselId" AND r."IntakeState" = 'RELEASED' AND r."ContentSha256" = NEW."ContentSha256"
                       AND r."UploadedBy" <> NEW."VerifierId" AND r."RegisteredBy" <> NEW."VerifierId")
                  THEN RAISE EXCEPTION 'Verification requires an independent verifier of released exact content' USING ERRCODE='23514'; END IF;
                  IF EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."DisposalState" = 'APPROVED')
                  THEN RAISE EXCEPTION 'A disposed document accepts no verification' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                -- Deferred verification insert: one VERIFICATION_RECORDED custody event per verification
                -- of that revision by the same verifier.
                CREATE FUNCTION files.require_controlled_verification_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (SELECT count(*) FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."DocumentId"
                        AND c."Revision" = NEW."Revision" AND c."EventType" = 'VERIFICATION_RECORDED' AND c."RecordedBy" = NEW."VerifierId"::text)
                     < (SELECT count(*) FROM files."ControlledDocumentVerifications" v WHERE v."TenantId" = NEW."TenantId" AND v."DocumentId" = NEW."DocumentId"
                        AND v."Revision" = NEW."Revision" AND v."VerifierId" = NEW."VerifierId")
                  THEN RAISE EXCEPTION 'Verification requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "RequireControlledVerificationCustody" AFTER INSERT ON files."ControlledDocumentVerifications"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_verification_custody();

                -- Deferred link insert: one LINKED custody event of that kind per link, and the document is
                -- undisposed with no pending request at commit (a pending request is invalidated first).
                CREATE FUNCTION files.require_controlled_document_link_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE links bigint; events bigint;
                BEGIN
                  EXECUTE format('SELECT count(*) FROM files.%I WHERE "TenantId" = $1 AND "DocumentId" = $2', TG_TABLE_NAME) INTO links USING NEW."TenantId", NEW."DocumentId";
                  SELECT count(*) INTO events FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."DocumentId"
                    AND c."EventType" = 'LINKED' AND c."Basis" = TG_ARGV[0] AND c."RecordedBy" = NEW."LinkedBy"::text;
                  IF events < links OR NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId" AND d."DisposalState" = 'NONE')
                  THEN RAISE EXCEPTION 'Link requires its custody event and an undisposed document' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "RequireControlledCertificateLinkCustody" AFTER INSERT ON files."ControlledDocumentCertificateLinks"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_document_link_custody('VesselCertificate');
                CREATE CONSTRAINT TRIGGER "RequireControlledEvidenceLinkCustody" AFTER INSERT ON files."ControlledDocumentEvidenceLinks"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_document_link_custody('ControlEvidence');
                CREATE CONSTRAINT TRIGGER "RequireControlledCredentialLinkCustody" AFTER INSERT ON files."ControlledDocumentCredentialLinks"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_document_link_custody('Credential');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the 20261118182029_ControlledDocumentChain function bodies verbatim.
            migrationBuilder.Sql("""
                DROP TRIGGER "RequireControlledCredentialLinkCustody" ON files."ControlledDocumentCredentialLinks";
                DROP TRIGGER "RequireControlledEvidenceLinkCustody" ON files."ControlledDocumentEvidenceLinks";
                DROP TRIGGER "RequireControlledCertificateLinkCustody" ON files."ControlledDocumentCertificateLinks";
                DROP FUNCTION files.require_controlled_document_link_custody();
                DROP TRIGGER "RequireControlledVerificationCustody" ON files."ControlledDocumentVerifications";
                DROP FUNCTION files.require_controlled_verification_custody();
                DROP TRIGGER "RequireControlledRevisionRegistration" ON files."ControlledDocumentRevisions";
                DROP FUNCTION files.require_controlled_revision_registration();

                CREATE OR REPLACE FUNCTION files.guard_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId"
                       AND d."VesselId" = NEW."VesselId" AND d."Sequence" >= NEW."Sequence")
                  THEN RAISE EXCEPTION 'Custody event requires its document sequence' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.guard_controlled_document() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Controlled documents cannot be removed; disposal is a recorded state' USING ERRCODE='55000'; END IF;
                  IF TG_OP = 'INSERT' THEN
                    IF NEW."LatestRevision" <> 0 OR NEW."CurrentRevision" IS NOT NULL OR NEW."DisposalState" <> 'NONE' OR NEW."HoldActive"
                    THEN RAISE EXCEPTION 'A controlled document starts empty, unheld and undisposed' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                  END IF;
                  IF (NEW."TenantId", NEW."Id", NEW."VesselId", NEW."Code", NEW."Title", NEW."Classification", NEW."CreatedBy", NEW."CreatedAt", NEW."Provenance")
                     IS DISTINCT FROM (OLD."TenantId", OLD."Id", OLD."VesselId", OLD."Code", OLD."Title", OLD."Classification", OLD."CreatedBy", OLD."CreatedAt", OLD."Provenance")
                  THEN RAISE EXCEPTION 'Controlled document identity is immutable' USING ERRCODE='55000'; END IF;
                  IF NEW."Sequence" <= OLD."Sequence" OR NEW."LatestRevision" < OLD."LatestRevision"
                     OR (OLD."DisposalState" = 'APPROVED' AND NEW."DisposalState" <> 'APPROVED') OR (OLD."DisposalState" = 'REQUESTED' AND NEW."DisposalState" = 'NONE')
                     OR (OLD."DisposalState" <> 'NONE' AND (NEW."RetentionPolicyRef" IS DISTINCT FROM OLD."RetentionPolicyRef" OR NEW."RetainUntil" IS DISTINCT FROM OLD."RetainUntil"))
                  THEN RAISE EXCEPTION 'Controlled document state only advances' USING ERRCODE='23514'; END IF;
                  IF (NEW."DisposalState" <> OLD."DisposalState") AND (NEW."HoldActive" OR NEW."RetainUntil" > (now() AT TIME ZONE 'UTC')::date)
                  THEN RAISE EXCEPTION 'Legal hold or unelapsed retention blocks disposal' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.require_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id" AND c."Sequence" = NEW."Sequence")
                  THEN RAISE EXCEPTION 'Controlled document change requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.guard_controlled_document_revision() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Controlled document revisions cannot be removed' USING ERRCODE='55000'; END IF;
                  IF TG_OP = 'INSERT' THEN
                    IF NEW."IntakeState" <> 'QUARANTINED' OR NEW."PublicationState" <> 'UNPUBLISHED' OR NEW."ObjectGeneration" IS NOT NULL
                       OR NOT EXISTS (SELECT 1 FROM files."FileStages" s WHERE s."TenantId" = NEW."TenantId" AND s."Id" = NEW."FileId" AND s."VesselId" = NEW."VesselId"
                         AND s."PersonId" IS NULL AND s."DocumentId" IS NULL AND s."ExpectedChecksum" = NEW."ContentSha256"
                         AND s."ExpectedLength" = NEW."ContentLength" AND s."Mime" = NEW."Mime" AND s."ActorId" = NEW."UploadedBy")
                    THEN RAISE EXCEPTION 'A registered revision starts quarantined from its exact transport intent' USING ERRCODE='23514'; END IF;
                    RETURN NEW;
                  END IF;
                  IF (NEW."TenantId", NEW."DocumentId", NEW."Revision", NEW."VesselId", NEW."FileId", NEW."ContentSha256", NEW."ContentLength", NEW."Mime",
                      NEW."UploadedBy", NEW."RegisteredBy", NEW."RegisteredAt")
                     IS DISTINCT FROM (OLD."TenantId", OLD."DocumentId", OLD."Revision", OLD."VesselId", OLD."FileId", OLD."ContentSha256", OLD."ContentLength", OLD."Mime",
                      OLD."UploadedBy", OLD."RegisteredBy", OLD."RegisteredAt")
                  THEN RAISE EXCEPTION 'Controlled document revision content is immutable' USING ERRCODE='55000'; END IF;
                  IF (NEW."IntakeState" <> OLD."IntakeState" AND NOT (OLD."IntakeState" = 'QUARANTINED' AND NEW."IntakeState" = 'RELEASED'))
                     OR (NEW."PublicationState" <> OLD."PublicationState" AND NOT ((OLD."PublicationState" = 'UNPUBLISHED' AND NEW."PublicationState" = 'EFFECTIVE')
                         OR (OLD."PublicationState" = 'EFFECTIVE' AND NEW."PublicationState" = 'SUPERSEDED')))
                     OR (OLD."ObjectGeneration" IS NOT NULL AND NEW."ObjectGeneration" IS DISTINCT FROM OLD."ObjectGeneration")
                     OR (OLD."ReleasedAt" IS NOT NULL AND NEW."ReleasedAt" IS DISTINCT FROM OLD."ReleasedAt")
                     OR (OLD."PublishedBy" IS NOT NULL AND (NEW."PublishedBy" IS DISTINCT FROM OLD."PublishedBy" OR NEW."PublishedAt" IS DISTINCT FROM OLD."PublishedAt"))
                  THEN RAISE EXCEPTION 'Controlled document revision states only advance' USING ERRCODE='23514'; END IF;
                  IF OLD."IntakeState" = 'QUARANTINED' AND NEW."IntakeState" = 'RELEASED' AND NOT EXISTS (SELECT 1 FROM files."StoredFiles" f
                       WHERE f."TenantId" = NEW."TenantId" AND f."Id" = NEW."FileId" AND f."VesselId" = NEW."VesselId" AND f."Checksum" = NEW."ContentSha256" AND f."Length" = NEW."ContentLength")
                  THEN RAISE EXCEPTION 'Release requires accepted clean bytes with the pinned hash' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.require_controlled_revision_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (NEW."IntakeState" <> OLD."IntakeState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = 'RELEASED'))
                     OR (NEW."PublicationState" <> OLD."PublicationState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = CASE NEW."PublicationState" WHEN 'EFFECTIVE' THEN 'PUBLISHED' ELSE 'SUPERSEDED' END))
                  THEN RAISE EXCEPTION 'Revision state change requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;

                CREATE OR REPLACE FUNCTION files.guard_controlled_document_verification() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."DocumentId" = NEW."DocumentId"
                       AND r."Revision" = NEW."Revision" AND r."VesselId" = NEW."VesselId" AND r."IntakeState" = 'RELEASED' AND r."ContentSha256" = NEW."ContentSha256"
                       AND r."UploadedBy" <> NEW."VerifierId" AND r."RegisteredBy" <> NEW."VerifierId")
                  THEN RAISE EXCEPTION 'Verification requires an independent verifier of released exact content' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ControlledDocuments_HoldRelease",
                schema: "files",
                table: "ControlledDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ControlledDocumentRevisions_Effective",
                schema: "files",
                table: "ControlledDocumentRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ControlledDocumentCustodyEvents",
                schema: "files",
                table: "ControlledDocumentCustodyEvents");

            migrationBuilder.DropColumn(
                name: "HoldReleasedAt",
                schema: "files",
                table: "ControlledDocuments");

            migrationBuilder.DropColumn(
                name: "HoldReleasedBy",
                schema: "files",
                table: "ControlledDocuments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ControlledDocumentCustodyEvents",
                schema: "files",
                table: "ControlledDocumentCustodyEvents",
                sql: "\"Sequence\" > 0 AND \"EventType\" IN ('CREATED','RECEIVED','QUARANTINED','SCANNED_CLEAN','RELEASED','PUBLISHED','SUPERSEDED','VERIFICATION_RECORDED','HOLD_PLACED','HOLD_RELEASED','RETENTION_SET','DISPOSAL_REQUESTED','DISPOSAL_APPROVED','LINKED') AND \"Source\" IN ('COMMAND','SECURE_FILE_PIPELINE','SYNTHETIC_SEED') AND length(\"RecordedBy\") > 0 AND (\"Revision\" IS NULL OR \"Revision\" > 0) AND (\"Source\" <> 'SECURE_FILE_PIPELINE' OR \"SubjectActorId\" IS NOT NULL)");
        }
    }
}
