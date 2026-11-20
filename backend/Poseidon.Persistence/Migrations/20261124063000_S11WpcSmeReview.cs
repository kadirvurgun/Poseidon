using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S11WpcSmeReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegulatoryReviewCampaigns",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProfileRevision = table.Column<long>(type: "bigint", nullable: true),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PinHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OpenItemsAtClose = table.Column<int>(type: "integer", nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryReviewCampaigns", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_ClosedOpen", "\"State\" <> 'CLOSED' OR \"OpenItemsAtClose\" = 0");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_Closure", "(\"State\" IN ('CLOSED', 'CLOSED_WITH_OPEN_ITEMS')) = (\"ClosedBy\" IS NOT NULL AND \"ClosedAt\" IS NOT NULL AND \"CloseReason\" IS NOT NULL AND \"OpenItemsAtClose\" IS NOT NULL)");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_Items", "\"ItemCount\" >= 0");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_Scope", "\"Scope\" IN ('SHIP', 'TUG')");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_State", "\"State\" IN ('OPEN', 'IN_REVIEW', 'CLOSED_WITH_OPEN_ITEMS', 'CLOSED')");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_Synthetic", "\"Provenance\" IN ('Recorded', 'SyntheticUnverified') AND (\"Provenance\" <> 'SyntheticUnverified' OR \"State\" = 'OPEN')");
                    table.CheckConstraint("CK_RegulatoryReviewCampaign_WithOpen", "\"State\" <> 'CLOSED_WITH_OPEN_ITEMS' OR \"OpenItemsAtClose\" > 0");
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewCampaigns_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryReviewItems",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleRevision = table.Column<long>(type: "bigint", nullable: true),
                    RequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequirementRevision = table.Column<long>(type: "bigint", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: true),
                    CandidateSourceCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CandidateAuthority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CandidateUnverified = table.Column<bool>(type: "boolean", nullable: false),
                    PredictedResult = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceAuthority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OwnerRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceEdition = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TrackedIssueId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerAuthorityId = table.Column<Guid>(type: "uuid", nullable: true),
                    QualificationRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VerdictCount = table.Column<int>(type: "integer", nullable: false),
                    VerdictAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryReviewItems", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RegulatoryReviewItem_Authority", "(\"SourceAuthority\" IS NULL OR \"SourceAuthority\" IN ('STATUTORY', 'FLAG', 'CLASS', 'PORT_LOCAL', 'COMPANY_SMS', 'MANUFACTURER', 'INDUSTRY_GUIDANCE')) AND (\"CandidateAuthority\" IS NULL OR \"CandidateAuthority\" IN ('STATUTORY', 'FLAG', 'CLASS', 'PORT_LOCAL', 'COMPANY_SMS', 'MANUFACTURER', 'INDUSTRY_GUIDANCE'))");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Confirmed", "\"Verdict\" NOT IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"ReviewerId\" IS NOT NULL AND \"QualificationRef\" IS NOT NULL AND \"SourceAuthority\" IS NOT NULL AND \"OwnerRole\" <> 'UNASSIGNED' AND \"VerdictCount\" > 0");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Kind", "\"Kind\" IN ('APPLICABILITY_RULE', 'SOURCE_CANDIDATE', 'TUG_LIMIT', 'OWNERSHIP_SLOT')");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Owner", "\"OwnerRole\" IN ('UNASSIGNED', 'REGULATORY_LEAD', 'REGULATORY_SME', 'PRODUCT_OWNER', 'DPA', 'PILOT_CUSTOMER')");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Reviewer", "(\"VerdictCount\" = 0) = (\"ReviewerId\" IS NULL) AND (\"ReviewerId\" IS NULL OR \"ReviewerId\" <> \"CreatedBy\")");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Synthetic", "\"Provenance\" IN ('Recorded', 'SyntheticUnverified') AND (\"Provenance\" <> 'SyntheticUnverified' OR \"Verdict\" = 'REVIEW_REQUIRED' AND \"VerdictCount\" = 0)");
                    table.CheckConstraint("CK_RegulatoryReviewItem_Verdict", "\"Verdict\" IN ('CONFIRMED', 'AMBIGUOUS', 'NOT_APPLICABLE_CONFIRMED', 'REVIEW_REQUIRED')");
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewItems_RegulatoryReviewCampaigns_TenantId_Ca~",
                        columns: x => new { x.TenantId, x.CampaignId },
                        principalSchema: "core",
                        principalTable: "RegulatoryReviewCampaigns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewItems_RuleRevisions_TenantId_RuleId_RuleRev~",
                        columns: x => new { x.TenantId, x.RuleId, x.RuleRevision },
                        principalSchema: "core",
                        principalTable: "RuleRevisions",
                        principalColumns: new[] { "TenantId", "Id", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewItems_Vessels_TenantId_VesselId",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryReviewReceipts",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryReviewReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_RegulatoryReviewReceipt_Action", "\"Action\" IN ('RegulatoryReviewCreate', 'RegulatoryReviewVerdict', 'RegulatoryReviewClose')");
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewReceipts_RegulatoryReviewCampaigns_TenantId~",
                        columns: x => new { x.TenantId, x.CampaignId },
                        principalSchema: "core",
                        principalTable: "RegulatoryReviewCampaigns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryReviewVerdicts",
                schema: "core",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceAuthority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OwnerRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceEdition = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TrackedIssueId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PinHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    QualificationRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QualificationState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorityHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryReviewVerdicts", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_RegulatoryReviewVerdict_Confirmed", "\"Verdict\" NOT IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"QualificationState\" = 'VERIFIED' AND \"SourceAuthority\" IS NOT NULL AND \"OwnerRole\" <> 'UNASSIGNED'");
                    table.CheckConstraint("CK_RegulatoryReviewVerdict_Note", "char_length(\"Note\") >= 10 AND \"Sequence\" >= 1");
                    table.CheckConstraint("CK_RegulatoryReviewVerdict_Open", "\"Verdict\" IN ('CONFIRMED', 'NOT_APPLICABLE_CONFIRMED') OR \"TrackedIssueId\" IS NOT NULL");
                    table.CheckConstraint("CK_RegulatoryReviewVerdict_Verdict", "\"Verdict\" IN ('CONFIRMED', 'AMBIGUOUS', 'NOT_APPLICABLE_CONFIRMED', 'REVIEW_REQUIRED')");
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewVerdicts_RegulatoryReviewCampaigns_TenantId~",
                        columns: x => new { x.TenantId, x.CampaignId },
                        principalSchema: "core",
                        principalTable: "RegulatoryReviewCampaigns",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegulatoryReviewVerdicts_RegulatoryReviewItems_TenantId_Ite~",
                        columns: x => new { x.TenantId, x.ItemId },
                        principalSchema: "core",
                        principalTable: "RegulatoryReviewItems",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RegulatoryReviewCampaigns_Active",
                schema: "core",
                table: "RegulatoryReviewCampaigns",
                columns: new[] { "TenantId", "VesselId", "Scope" },
                unique: true,
                filter: "\"State\" = 'OPEN' OR \"State\" = 'IN_REVIEW'");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewItems_TenantId_CampaignId_Kind_SubjectKey",
                schema: "core",
                table: "RegulatoryReviewItems",
                columns: new[] { "TenantId", "CampaignId", "Kind", "SubjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewItems_TenantId_CampaignId_Ordinal",
                schema: "core",
                table: "RegulatoryReviewItems",
                columns: new[] { "TenantId", "CampaignId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewItems_TenantId_RuleId_RuleRevision",
                schema: "core",
                table: "RegulatoryReviewItems",
                columns: new[] { "TenantId", "RuleId", "RuleRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewItems_TenantId_VesselId",
                schema: "core",
                table: "RegulatoryReviewItems",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewReceipts_TenantId_CampaignId",
                schema: "core",
                table: "RegulatoryReviewReceipts",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewVerdicts_TenantId_CampaignId",
                schema: "core",
                table: "RegulatoryReviewVerdicts",
                columns: new[] { "TenantId", "CampaignId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryReviewVerdicts_TenantId_ItemId_Sequence",
                schema: "core",
                table: "RegulatoryReviewVerdicts",
                columns: new[] { "TenantId", "ItemId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegulatoryReviewReceipts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RegulatoryReviewVerdicts",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RegulatoryReviewItems",
                schema: "core");

            migrationBuilder.DropTable(
                name: "RegulatoryReviewCampaigns",
                schema: "core");
        }
    }
}
