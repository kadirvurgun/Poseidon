using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S9WpcVoiceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "intelligence");

            migrationBuilder.AddColumn<bool>(
                name: "IspsRestricted",
                schema: "files",
                table: "ControlledDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ControlledDocumentAccessRecords",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Classification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PolicyBasis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attribution = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentAccessRecords", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ControlledDocumentAccessRecords", "\"Revision\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$' AND \"Purpose\" IN ('VIEW','DOWNLOAD') AND \"Classification\" IN ('INTERNAL','CONFIDENTIAL','RESTRICTED') AND \"PolicyBasis\" IN ('INTERNAL_DEFAULT','CONFIDENTIAL_ATTRIBUTED','RESTRICTED_EXPLICIT_PERMISSION') AND length(\"Attribution\") > 0");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentAccessRecords_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentAccessRecords_Revision",
                        columns: x => new { x.TenantId, x.DocumentId, x.Revision },
                        principalSchema: "files",
                        principalTable: "ControlledDocumentRevisions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ControlledDocumentAcknowledgements",
                schema: "files",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Statement = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlledDocumentAcknowledgements", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ControlledDocumentAcknowledgements", "\"Revision\" > 0 AND \"ContentSha256\" ~ '^[0-9A-F]{64}$' AND \"Statement\" = 'READ_AND_ACCESSED_V1'");
                    table.ForeignKey(
                        name: "FK_ControlledDocumentAcknowledgements_Document",
                        columns: x => new { x.TenantId, x.DocumentId, x.VesselId },
                        principalSchema: "files",
                        principalTable: "ControlledDocuments",
                        principalColumns: new[] { "TenantId", "Id", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlledDocumentAcknowledgements_Revision",
                        columns: x => new { x.TenantId, x.DocumentId, x.Revision },
                        principalSchema: "files",
                        principalTable: "ControlledDocumentRevisions",
                        principalColumns: new[] { "TenantId", "DocumentId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoiceEventDraftReceipts",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceEventDraftReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_VoiceEventDraftReceipt_Revision", "\"Revision\">0");
                    table.ForeignKey(
                        name: "FK_VoiceEventDraftReceipts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoiceEventDrafts",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TranscriptOrigin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Transcript = table.Column<string>(type: "text", nullable: false),
                    TranscriptSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SpeechEngineVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SpeechModelVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SpeechConfidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    ExtractorVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RuleSetVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CandidateJson = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrectionsJson = table.Column<string>(type: "text", nullable: true),
                    RejectedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    RejectionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AudioRetention = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceEventDrafts", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_VoiceEventDrafts_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_VoiceEventDraft_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"TranscriptOrigin\" IN ('TYPED','LOCAL_SPEECH') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND \"AudioRetention\"='NOT_RETAINED' AND length(\"Transcript\") BETWEEN 1 AND 4000 AND \"TranscriptSha256\" ~ '^[0-9a-f]{64}$' AND ((\"TranscriptOrigin\"='LOCAL_SPEECH')=(\"SpeechEngineVersion\" IS NOT NULL AND \"SpeechModelVersion\" IS NOT NULL)) AND (\"SpeechConfidence\" IS NULL OR \"SpeechConfidence\" BETWEEN 0 AND 1) AND ((\"State\"='CONFIRMED')=(\"ConfirmedBy\" IS NOT NULL AND \"ConfirmedAt\" IS NOT NULL AND \"CorrectionsJson\" IS NOT NULL)) AND ((\"State\"='REJECTED')=(\"RejectedBy\" IS NOT NULL AND \"RejectedAt\" IS NOT NULL AND \"RejectionReason\" IS NOT NULL)) AND (\"RejectionReason\" IS NULL OR \"RejectionReason\" IN ('TRANSCRIPT_WRONG','FIELDS_WRONG','NOT_AN_EVENT','DUPLICATE','OTHER')) AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"ConfirmedBy\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_VoiceEventDrafts_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoiceEventDraftRevisions",
                schema: "intelligence",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FieldsJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceEventDraftRevisions", x => new { x.TenantId, x.DraftId, x.Revision });
                    table.CheckConstraint("CK_VoiceEventDraftRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"ReasonCode\" IN ('CREATE','EDIT','CONFIRM','REJECT')");
                    table.ForeignKey(
                        name: "FK_VoiceEventDraftRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.DraftId },
                        principalSchema: "intelligence",
                        principalTable: "VoiceEventDrafts",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ControlledDocuments_Isps",
                schema: "files",
                table: "ControlledDocuments",
                sql: "NOT \"IspsRestricted\" OR \"Classification\" = 'RESTRICTED'");

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentAccessRecords_Actor",
                schema: "files",
                table: "ControlledDocumentAccessRecords",
                columns: new[] { "TenantId", "ActorId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentAccessRecords_Document",
                schema: "files",
                table: "ControlledDocumentAccessRecords",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentAccessRecords_TenantId_DocumentId_Revision",
                schema: "files",
                table: "ControlledDocumentAccessRecords",
                columns: new[] { "TenantId", "DocumentId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentAcknowledgements_Document",
                schema: "files",
                table: "ControlledDocumentAcknowledgements",
                columns: new[] { "TenantId", "DocumentId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlledDocumentAcknowledgements_Reader",
                schema: "files",
                table: "ControlledDocumentAcknowledgements",
                columns: new[] { "TenantId", "DocumentId", "Revision", "ActorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDraftReceipts_TenantId_VesselId",
                schema: "intelligence",
                table: "VoiceEventDraftReceipts",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDraftRevisions_TenantId_VesselId_DraftId",
                schema: "intelligence",
                table: "VoiceEventDraftRevisions",
                columns: new[] { "TenantId", "VesselId", "DraftId" });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDrafts_TenantId_VesselId_SavedAt_Id",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                columns: new[] { "TenantId", "VesselId", "SavedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDrafts_TenantId_VesselId_State",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                columns: new[] { "TenantId", "VesselId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlledDocumentAccessRecords",
                schema: "files");

            migrationBuilder.DropTable(
                name: "ControlledDocumentAcknowledgements",
                schema: "files");

            migrationBuilder.DropTable(
                name: "VoiceEventDraftReceipts",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "VoiceEventDraftRevisions",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "VoiceEventDrafts",
                schema: "intelligence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ControlledDocuments_Isps",
                schema: "files",
                table: "ControlledDocuments");

            migrationBuilder.DropColumn(
                name: "IspsRestricted",
                schema: "files",
                table: "ControlledDocuments");
        }
    }
}
