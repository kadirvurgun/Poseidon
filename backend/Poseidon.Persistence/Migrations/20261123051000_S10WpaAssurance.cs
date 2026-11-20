using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S10WpaAssurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssuranceImportMappings",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    BindingsJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceImportMappings", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AssuranceImportMapping_Version", "\"Version\">0 AND length(\"Key\") BETWEEN 1 AND 64 AND \"Hash\" ~ '^[A-F0-9]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "AssuranceInspections",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Programme = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InspectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Classification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Applicability = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceInspections", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AssuranceInspections_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_AssuranceInspection_State", "\"Revision\">0 AND \"Programme\"='SIRE_2_0' AND \"State\" IN ('DRAFT') AND \"Classification\" IN ('TANKER','NOT_TANKER','UNKNOWN') AND \"Applicability\" IN ('APPLICABLE','REVIEW_REQUIRED') AND (\"Classification\"<>'NOT_TANKER') AND ((\"Classification\"='TANKER') = (\"Applicability\"='APPLICABLE')) AND \"Provenance\" IN ('ImportedUnverified','SyntheticUnverified') AND length(\"ReferenceNo\") BETWEEN 1 AND 64");
                    table.ForeignKey(
                        name: "FK_AssuranceInspections_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssuranceReceipts",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_AssuranceReceipt_Action", "\"Action\" IN ('AuditPackCreate','AssuranceMappingCreate','AssuranceImportStage','AssuranceImportDecide','AssuranceEvidenceCapture','AssuranceFindingLink') AND \"Revision\">0");
                });

            migrationBuilder.CreateTable(
                name: "AuditPackManifests",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    AsOf = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ManifestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    UnavailableChains = table.Column<int>(type: "integer", nullable: false),
                    Truncated = table.Column<bool>(type: "boolean", nullable: false),
                    PreviousPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditPackManifests", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_AuditPackManifests_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_AuditPackManifest_Label", "\"Label\"='SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD' AND \"ItemCount\">=0 AND \"ItemCount\"<=2048 AND \"UnavailableChains\">=0 AND \"ManifestHash\" ~ '^[A-F0-9]{64}$' AND (\"PreviousPackId\" IS NULL OR \"PreviousPackId\"<>\"Id\")");
                    table.ForeignKey(
                        name: "FK_AuditPackManifests_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssuranceEvidence",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionRef = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileRevision = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MediaKind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Origin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Verification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceEvidence", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AssuranceEvidence_Unverified", "\"Verification\"='UNVERIFIED' AND \"MediaKind\" IN ('PHOTO','VIDEO') AND \"Origin\" IN ('ONLINE','OFFLINE') AND \"FileRevision\">0 AND \"Checksum\" ~ '^[A-Fa-f0-9]{64}$' AND \"CapturedAt\"<=\"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_AssuranceEvidence_Inspection",
                        columns: x => new { x.TenantId, x.VesselId, x.InspectionId },
                        principalSchema: "assurance",
                        principalTable: "AssuranceInspections",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssuranceFindings",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionRef = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FirstItemRevision = table.Column<long>(type: "bigint", nullable: false),
                    LatestItemRevision = table.Column<long>(type: "bigint", nullable: false),
                    ObservationPresent = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Verification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionLinkState = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LinkedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceFindings", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AssuranceFinding_Open", "\"Revision\">0 AND \"State\"='OPEN' AND \"Verification\"='UNVERIFIED' AND \"LatestItemRevision\">=\"FirstItemRevision\" AND \"ActionLinkState\" IN ('UNLINKED','LINKED') AND ((\"ActionLinkState\"='LINKED') = (\"ActionId\" IS NOT NULL)) AND ((\"ActionId\" IS NULL) = (\"LinkedBy\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_AssuranceFindings_Inspection",
                        columns: x => new { x.TenantId, x.VesselId, x.InspectionId },
                        principalSchema: "assurance",
                        principalTable: "AssuranceInspections",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssuranceFindings_SafetyAction",
                        columns: x => new { x.TenantId, x.VesselId, x.ActionId },
                        principalSchema: "hse",
                        principalTable: "SafetyActions",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssuranceImports",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingVersion = table.Column<int>(type: "integer", nullable: false),
                    MappingHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    TableHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    DiffJson = table.Column<string>(type: "text", nullable: false),
                    DiffHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionJson = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceImports", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AssuranceImport_Decision", "\"State\" IN ('PENDING_REVIEW','ACCEPTED','REJECTED') AND \"MappingVersion\">0 AND \"DiffHash\" ~ '^[A-F0-9]{64}$' AND ((\"State\"='PENDING_REVIEW') = (\"DecidedBy\" IS NULL)) AND ((\"DecidedBy\" IS NULL) = (\"DecidedAt\" IS NULL)) AND (\"DecidedBy\" IS NULL OR \"DecidedBy\"<>\"ImportedBy\" AND length(\"Reason\")>=10)");
                    table.ForeignKey(
                        name: "FK_AssuranceImports_Inspection",
                        columns: x => new { x.TenantId, x.VesselId, x.InspectionId },
                        principalSchema: "assurance",
                        principalTable: "AssuranceInspections",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssuranceImports_Mapping",
                        columns: x => new { x.TenantId, x.MappingId },
                        principalSchema: "assurance",
                        principalTable: "AssuranceImportMappings",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssuranceInspectionItems",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionRef = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeKind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssuranceInspectionItems", x => new { x.TenantId, x.InspectionId, x.QuestionRef, x.Revision });
                    table.CheckConstraint("CK_AssuranceInspectionItem_Change", "\"Revision\">0 AND \"ChangeKind\" IN ('NEW','CHANGED') AND (\"ChangeKind\"='NEW') = (\"Revision\"=1) AND \"Hash\" ~ '^[A-F0-9]{64}$'");
                    table.ForeignKey(
                        name: "FK_AssuranceInspectionItems_Inspection",
                        columns: x => new { x.TenantId, x.VesselId, x.InspectionId },
                        principalSchema: "assurance",
                        principalTable: "AssuranceInspections",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditPackItems",
                schema: "assurance",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileRevision = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditPackItems", x => new { x.TenantId, x.PackId, x.Ordinal });
                    table.CheckConstraint("CK_AuditPackItem_Kind", "\"Ordinal\">=0 AND \"Revision\">=0 AND \"Kind\" IN ('Source','Requirement','Control','Evidence','Verification','Decision','History') AND \"State\" IN ('Current','Superseded','Unavailable','Recorded') AND ((\"State\"='Unavailable') = (\"Hash\" IS NULL)) AND ((\"FileId\" IS NULL) = (\"FileRevision\" IS NULL)) AND (\"State\"<>'Recorded' OR \"Kind\" IN ('Verification','Decision')) AND (\"State\"<>'Superseded' OR \"Kind\"='Evidence')");
                    table.ForeignKey(
                        name: "FK_AuditPackItems_Manifest",
                        columns: x => new { x.TenantId, x.VesselId, x.PackId },
                        principalSchema: "assurance",
                        principalTable: "AuditPackManifests",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceEvidence_TenantId_InspectionId_QuestionRef",
                schema: "assurance",
                table: "AssuranceEvidence",
                columns: new[] { "TenantId", "InspectionId", "QuestionRef" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceEvidence_TenantId_VesselId_InspectionId",
                schema: "assurance",
                table: "AssuranceEvidence",
                columns: new[] { "TenantId", "VesselId", "InspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceFindings_TenantId_InspectionId_QuestionRef",
                schema: "assurance",
                table: "AssuranceFindings",
                columns: new[] { "TenantId", "InspectionId", "QuestionRef" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceFindings_TenantId_VesselId_ActionId",
                schema: "assurance",
                table: "AssuranceFindings",
                columns: new[] { "TenantId", "VesselId", "ActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceFindings_TenantId_VesselId_InspectionId",
                schema: "assurance",
                table: "AssuranceFindings",
                columns: new[] { "TenantId", "VesselId", "InspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceImportMappings_TenantId_Key_Version",
                schema: "assurance",
                table: "AssuranceImportMappings",
                columns: new[] { "TenantId", "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceImports_OnePending",
                schema: "assurance",
                table: "AssuranceImports",
                columns: new[] { "TenantId", "InspectionId" },
                unique: true,
                filter: "\"State\"='PENDING_REVIEW'");

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceImports_TenantId_InspectionId_State",
                schema: "assurance",
                table: "AssuranceImports",
                columns: new[] { "TenantId", "InspectionId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceImports_TenantId_MappingId",
                schema: "assurance",
                table: "AssuranceImports",
                columns: new[] { "TenantId", "MappingId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceImports_TenantId_VesselId_InspectionId",
                schema: "assurance",
                table: "AssuranceImports",
                columns: new[] { "TenantId", "VesselId", "InspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceInspectionItems_TenantId_VesselId_InspectionId",
                schema: "assurance",
                table: "AssuranceInspectionItems",
                columns: new[] { "TenantId", "VesselId", "InspectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssuranceInspections_TenantId_VesselId_ReferenceNo",
                schema: "assurance",
                table: "AssuranceInspections",
                columns: new[] { "TenantId", "VesselId", "ReferenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditPackItems_TenantId_VesselId_PackId",
                schema: "assurance",
                table: "AuditPackItems",
                columns: new[] { "TenantId", "VesselId", "PackId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditPackManifests_TenantId_VesselId_CreatedAt",
                schema: "assurance",
                table: "AuditPackManifests",
                columns: new[] { "TenantId", "VesselId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssuranceEvidence",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceFindings",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceImports",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceInspectionItems",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceReceipts",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AuditPackItems",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceImportMappings",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AssuranceInspections",
                schema: "assurance");

            migrationBuilder.DropTable(
                name: "AuditPackManifests",
                schema: "assurance");
        }
    }
}
