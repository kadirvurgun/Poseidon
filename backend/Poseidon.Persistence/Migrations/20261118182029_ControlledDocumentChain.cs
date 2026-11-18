using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // PLAN-1511-5802 controlled document/evidence custody chain. Additive only: existing
    // Credential/VesselCertificate/ControlEvidence/StoredFile rows are not migrated or changed.
    [DbContext(typeof(PoseidonDbContext))]
    [Migration("20261118182029_ControlledDocumentChain")]
    public partial class ControlledDocumentChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlledDocuments",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Classification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentRevision = table.Column<long>(type: "bigint", nullable: true),
                    LatestRevision = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    HoldActive = table.Column<bool>(type: "boolean", nullable: false),
                    HoldAuthorityRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HoldPlacedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    HoldPlacedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetentionPolicyRef = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RetainUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DisposalState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisposalRequestedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DisposalRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisposalApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DisposalApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocuments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_ControlledDocuments_Scope", x => new { x.TenantId, x.Id, x.VesselId });
                    table.CheckConstraint("CK_ControlledDocuments_Disposal", "((\"DisposalState\" = 'NONE') = (\"DisposalRequestedBy\" IS NULL AND \"DisposalRequestedAt\" IS NULL)) AND ((\"DisposalState\" = 'APPROVED') = (\"DisposalApprovedBy\" IS NOT NULL AND \"DisposalApprovedAt\" IS NOT NULL)) AND (\"DisposalApprovedBy\" IS NULL OR \"DisposalApprovedBy\" <> \"DisposalRequestedBy\") AND (\"DisposalState\" = 'NONE' OR (\"RetentionPolicyRef\" IS NOT NULL AND \"RetainUntil\" IS NOT NULL))");
                    table.CheckConstraint("CK_ControlledDocuments_Hold", "NOT \"HoldActive\" OR (\"HoldPlacedBy\" IS NOT NULL AND \"HoldPlacedAt\" IS NOT NULL AND \"HoldAuthorityRef\" IS NOT NULL)");
                    table.CheckConstraint("CK_ControlledDocuments_State", "\"Classification\" IN ('INTERNAL','CONFIDENTIAL','RESTRICTED') AND \"DisposalState\" IN ('NONE','REQUESTED','APPROVED') AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND \"Sequence\" > 0 AND \"LatestRevision\" >= 0 AND (\"CurrentRevision\" IS NULL OR (\"CurrentRevision\" > 0 AND \"CurrentRevision\" <= \"LatestRevision\"))");
                    table.ForeignKey(
                        name: "FK_ControlledDocuments_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentCertificateLinks",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentCertificateLinks", x => new { x.TenantId, x.DocumentId, x.CertificateId });
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCertificateLinks_Certificate",
                        columns: x => new { x.TenantId, x.VesselId, x.CertificateId },
                        principalSchema: "assurance",
                        principalTable: "Certificates",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCertificateLinks_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentCredentialLinks",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentCredentialLinks", x => new { x.TenantId, x.DocumentId, x.CredentialId });
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCredentialLinks_Credential",
                        columns: x => new { x.TenantId, x.CredentialId, x.VesselId },
                        principalSchema: "crew",
                        principalTable: "Credentials",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCredentialLinks_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentEvidenceLinks",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentEvidenceLinks", x => new { x.TenantId, x.DocumentId, x.EvidenceId });
                    table.CheckConstraint("CK_ControlledDocumentEvidenceLinks", "\"EvidenceKind\" = 'Evidence'");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentEvidenceLinks_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentEvidenceLinks_Evidence",
                        columns: x => new { x.TenantId, x.VesselId, x.EvidenceKind, x.EvidenceId },
                        principalSchema: "core",
                        principalTable: "ControlEvidenceHeads",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentReceipts",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.ForeignKey(
                        name: "FK_ControlledDocumentReceipts_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentRevisions",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    Mime = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntakeState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ObjectGeneration = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublicationState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentRevisions", x => new { x.TenantId, x.DocumentId, x.Revision });
                    table.CheckConstraint("CK_ControlledDocumentRevisions", "\"Revision\" > 0 AND \"ContentLength\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$' AND \"IntakeState\" IN ('QUARANTINED','RELEASED') AND \"PublicationState\" IN ('UNPUBLISHED','EFFECTIVE','SUPERSEDED') AND ((\"IntakeState\" = 'RELEASED') = (\"ReleasedAt\" IS NOT NULL)) AND ((\"PublicationState\" = 'UNPUBLISHED') = (\"PublishedBy\" IS NULL AND \"PublishedAt\" IS NULL)) AND (\"PublicationState\" = 'UNPUBLISHED' OR \"IntakeState\" = 'RELEASED') AND (\"PublishedBy\" IS NULL OR (\"PublishedBy\" <> \"UploadedBy\" AND \"PublishedBy\" <> \"RegisteredBy\"))");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentRevisions_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentRevisions_FileStage",
                        columns: x => new { x.TenantId, x.FileId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "FileStages",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentCustodyEvents",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SubjectActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Basis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentCustodyEvents", x => new { x.TenantId, x.DocumentId, x.Sequence });
                    table.CheckConstraint("CK_ControlledDocumentCustodyEvents", "\"Sequence\" > 0 AND \"EventType\" IN ('CREATED','RECEIVED','QUARANTINED','SCANNED_CLEAN','RELEASED','PUBLISHED','SUPERSEDED','VERIFICATION_RECORDED','HOLD_PLACED','HOLD_RELEASED','RETENTION_SET','DISPOSAL_REQUESTED','DISPOSAL_APPROVED','LINKED') AND \"Source\" IN ('COMMAND','SECURE_FILE_PIPELINE','SYNTHETIC_SEED') AND length(\"RecordedBy\") > 0 AND (\"Revision\" IS NULL OR \"Revision\" > 0) AND (\"Source\" <> 'SECURE_FILE_PIPELINE' OR \"SubjectActorId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCustodyEvents_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentCustodyEvents_Revision",
                        columns: x => new { x.TenantId, x.DocumentId, x.Revision },
                        principalSchema: "files",
                        principalTable: "ControlledDocumentRevisions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentVerifications",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Basis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentVerifications", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ControlledDocumentVerifications", "\"Method\" IN ('EVIDENCE_IDENTITY_CHECK','AUTHORITY_RESPONSE','MANUAL_SOURCE_COMPARISON') AND \"Result\" IN ('VALID','INVALID','INCONCLUSIVE') AND length(btrim(\"Basis\")) > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$'");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentVerifications_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentVerifications_Revision",
                        columns: x => new { x.TenantId, x.DocumentId, x.Revision },
                        principalSchema: "files",
                        principalTable: "ControlledDocumentRevisions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCertificateLinks_Certificate",
                schema: "files",
                table: "ControlledDocumentCertificateLinks",
                columns: new[] { "TenantId", "VesselId", "CertificateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCertificateLinks_Document",
                schema: "files",
                table: "ControlledDocumentCertificateLinks",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCredentialLinks_Credential",
                schema: "files",
                table: "ControlledDocumentCredentialLinks",
                columns: new[] { "TenantId", "CredentialId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCredentialLinks_Document",
                schema: "files",
                table: "ControlledDocumentCredentialLinks",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCustodyEvents_Document",
                schema: "files",
                table: "ControlledDocumentCustodyEvents",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentCustodyEvents_Revision",
                schema: "files",
                table: "ControlledDocumentCustodyEvents",
                columns: new[] { "TenantId", "DocumentId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentEvidenceLinks_Document",
                schema: "files",
                table: "ControlledDocumentEvidenceLinks",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentEvidenceLinks_Evidence",
                schema: "files",
                table: "ControlledDocumentEvidenceLinks",
                columns: new[] { "TenantId", "VesselId", "EvidenceKind", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentReceipts_Document",
                schema: "files",
                table: "ControlledDocumentReceipts",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentRevisions_Document",
                schema: "files",
                table: "ControlledDocumentRevisions",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentRevisions_File",
                schema: "files",
                table: "ControlledDocumentRevisions",
                columns: new[] { "TenantId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentRevisions_Stage",
                schema: "files",
                table: "ControlledDocumentRevisions",
                columns: new[] { "TenantId", "FileId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocuments_Code",
                schema: "files",
                table: "ControlledDocuments",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocuments_Vessel",
                schema: "files",
                table: "ControlledDocuments",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentVerifications_Document",
                schema: "files",
                table: "ControlledDocumentVerifications",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentVerifications_Revision",
                schema: "files",
                table: "ControlledDocumentVerifications",
                columns: new[] { "TenantId", "DocumentId", "Revision" });
            // DB-level mirror of ControlledDocumentStorage.Guard: append-only custody, immutable
            // revision content, forward-only states, independent verification and hold-before-disposal.
            migrationBuilder.Sql("""
                CREATE FUNCTION files.controlled_document_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Controlled document history is append-only' USING ERRCODE='55000'; END $$;
                CREATE TRIGGER "AppendOnlyControlledDocumentCustody" BEFORE UPDATE OR DELETE ON files."ControlledDocumentCustodyEvents" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentCustodyTruncate" BEFORE TRUNCATE ON files."ControlledDocumentCustodyEvents" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentVerifications" BEFORE UPDATE OR DELETE ON files."ControlledDocumentVerifications" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentVerificationsTruncate" BEFORE TRUNCATE ON files."ControlledDocumentVerifications" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentReceipts" BEFORE UPDATE OR DELETE ON files."ControlledDocumentReceipts" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentReceiptsTruncate" BEFORE TRUNCATE ON files."ControlledDocumentReceipts" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentCertificateLinks" BEFORE UPDATE OR DELETE ON files."ControlledDocumentCertificateLinks" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentCertificateLinksTruncate" BEFORE TRUNCATE ON files."ControlledDocumentCertificateLinks" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentEvidenceLinks" BEFORE UPDATE OR DELETE ON files."ControlledDocumentEvidenceLinks" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentEvidenceLinksTruncate" BEFORE TRUNCATE ON files."ControlledDocumentEvidenceLinks" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentCredentialLinks" BEFORE UPDATE OR DELETE ON files."ControlledDocumentCredentialLinks" FOR EACH ROW EXECUTE FUNCTION files.controlled_document_append_only();
                CREATE TRIGGER "AppendOnlyControlledDocumentCredentialLinksTruncate" BEFORE TRUNCATE ON files."ControlledDocumentCredentialLinks" FOR EACH STATEMENT EXECUTE FUNCTION files.controlled_document_append_only();

                CREATE FUNCTION files.guard_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocuments" d WHERE d."TenantId" = NEW."TenantId" AND d."Id" = NEW."DocumentId"
                       AND d."VesselId" = NEW."VesselId" AND d."Sequence" >= NEW."Sequence")
                  THEN RAISE EXCEPTION 'Custody event requires its document sequence' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "GuardControlledDocumentCustody" AFTER INSERT ON files."ControlledDocumentCustodyEvents" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.guard_controlled_document_custody();

                CREATE FUNCTION files.guard_controlled_document() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER "GuardControlledDocument" BEFORE INSERT OR UPDATE OR DELETE ON files."ControlledDocuments" FOR EACH ROW EXECUTE FUNCTION files.guard_controlled_document();
                CREATE TRIGGER "GuardControlledDocumentTruncate" BEFORE TRUNCATE ON files."ControlledDocuments" FOR EACH STATEMENT EXECUTE FUNCTION files.guard_controlled_document();
                CREATE FUNCTION files.require_controlled_document_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId" AND c."DocumentId" = NEW."Id" AND c."Sequence" = NEW."Sequence")
                  THEN RAISE EXCEPTION 'Controlled document change requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "RequireControlledDocumentCustody" AFTER INSERT OR UPDATE ON files."ControlledDocuments"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_document_custody();

                CREATE FUNCTION files.guard_controlled_document_revision() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER "GuardControlledDocumentRevision" BEFORE INSERT OR UPDATE OR DELETE ON files."ControlledDocumentRevisions" FOR EACH ROW EXECUTE FUNCTION files.guard_controlled_document_revision();
                CREATE TRIGGER "GuardControlledDocumentRevisionTruncate" BEFORE TRUNCATE ON files."ControlledDocumentRevisions" FOR EACH STATEMENT EXECUTE FUNCTION files.guard_controlled_document_revision();
                CREATE FUNCTION files.require_controlled_revision_custody() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (NEW."IntakeState" <> OLD."IntakeState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = 'RELEASED'))
                     OR (NEW."PublicationState" <> OLD."PublicationState" AND NOT EXISTS (SELECT 1 FROM files."ControlledDocumentCustodyEvents" c WHERE c."TenantId" = NEW."TenantId"
                        AND c."DocumentId" = NEW."DocumentId" AND c."Revision" = NEW."Revision" AND c."EventType" = CASE NEW."PublicationState" WHEN 'EFFECTIVE' THEN 'PUBLISHED' ELSE 'SUPERSEDED' END))
                  THEN RAISE EXCEPTION 'Revision state change requires its custody event' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE CONSTRAINT TRIGGER "RequireControlledRevisionCustody" AFTER UPDATE ON files."ControlledDocumentRevisions"
                  DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION files.require_controlled_revision_custody();

                CREATE FUNCTION files.guard_controlled_document_verification() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM files."ControlledDocumentRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."DocumentId" = NEW."DocumentId"
                       AND r."Revision" = NEW."Revision" AND r."VesselId" = NEW."VesselId" AND r."IntakeState" = 'RELEASED' AND r."ContentSha256" = NEW."ContentSha256"
                       AND r."UploadedBy" <> NEW."VerifierId" AND r."RegisteredBy" <> NEW."VerifierId")
                  THEN RAISE EXCEPTION 'Verification requires an independent verifier of released exact content' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "GuardControlledDocumentVerification" BEFORE INSERT ON files."ControlledDocumentVerifications" FOR EACH ROW EXECUTE FUNCTION files.guard_controlled_document_verification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlledDocumentCertificateLinks",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentCredentialLinks",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentCustodyEvents",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentEvidenceLinks",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentReceipts",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentVerifications",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentRevisions",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocuments",
                schema: "files");
            migrationBuilder.Sql("""
                DROP FUNCTION files.guard_controlled_document_verification();
                DROP FUNCTION files.require_controlled_revision_custody();
                DROP FUNCTION files.guard_controlled_document_revision();
                DROP FUNCTION files.require_controlled_document_custody();
                DROP FUNCTION files.guard_controlled_document();
                DROP FUNCTION files.guard_controlled_document_custody();
                DROP FUNCTION files.controlled_document_append_only();
                """);
        }
    }
}
