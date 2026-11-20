using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S8WpbWorkOrderExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CriticalSpares",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalSpares", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_CriticalSpares_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_CriticalSpare_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_CriticalSpare_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalSupplyReceipts",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalSupplyReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_TechnicalSupplyReceipt_Action", "\"Action\" IN ('CriticalSpareCreate','CriticalSpareUpdate','TechnicalRequisitionPropose','TechnicalRequisitionDecide') AND \"Revision\" > 0 AND \"RecordVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_TechnicalSupplyReceipt_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderExecutions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderExecutions", x => new { x.TenantId, x.WorkOrderId });
                    table.UniqueConstraint("AK_WorkOrderExecutions_TenantId_VesselId_WorkOrderId", x => new { x.TenantId, x.VesselId, x.WorkOrderId });
                    table.CheckConstraint("CK_WorkOrderExecution_State", "\"Revision\" > 0 AND \"State\" IN ('RELEASED','IN_PROGRESS','EVIDENCE_SUBMITTED','VERIFIED','CLOSED')");
                    table.ForeignKey(
                        name: "FK_WorkOrderExecution_WorkOrder",
                        columns: x => new { x.TenantId, x.VesselId, x.WorkOrderId },
                        principalSchema: "technical",
                        principalTable: "WorkOrders",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CriticalSpareRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpareId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    Transition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalSpareRevisions", x => new { x.TenantId, x.SpareId, x.Revision });
                    table.UniqueConstraint("AK_CriticalSpareRevision_Scope", x => new { x.TenantId, x.VesselId, x.SpareId, x.Revision });
                    table.CheckConstraint("CK_CriticalSpareRevision_State", "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"Transition\" IN ('CREATE','UPDATE') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified')");
                    table.ForeignKey(
                        name: "FK_CriticalSpareRevision_Asset",
                        columns: x => new { x.TenantId, x.VesselId, x.AssetId },
                        principalSchema: "technical",
                        principalTable: "Assets",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalSpareRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.SpareId },
                        principalSchema: "technical",
                        principalTable: "CriticalSpares",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalRequisitions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpareId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalRequisitions", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TechnicalRequisitions_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_TechnicalRequisition_State", "\"Revision\" > 0 AND \"State\" IN ('PROPOSED','APPROVED_FOR_ERP_EXPORT','REJECTED')");
                    table.ForeignKey(
                        name: "FK_TechnicalRequisition_Spare",
                        columns: x => new { x.TenantId, x.VesselId, x.SpareId },
                        principalSchema: "technical",
                        principalTable: "CriticalSpares",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEnrollments",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorityDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    BaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    BaseRevision = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedBase = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetainUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEnrollments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_WorkOrderOfflineEnrollment_Scope", x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.WorkOrderId });
                    table.CheckConstraint("CK_WorkOrderOfflineEnrollment_State", "\"BaseVersion\" > 0 AND \"BaseRevision\" > 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"");
                    table.ForeignKey(
                        name: "FK_WorkOrderOffline_Execution",
                        columns: x => new { x.TenantId, x.VesselId, x.WorkOrderId },
                        principalSchema: "technical",
                        principalTable: "WorkOrderExecutions",
                        principalColumns: new[] { "TenantId", "VesselId", "WorkOrderId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderExecutionReceipts",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderExecutionReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_WorkOrderExecutionReceipt_Action", "\"Action\" IN ('WorkOrderExecutionTransition','WorkOrderExecutionEvidence') AND \"Revision\" > 0 AND \"RecordVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkOrderExecutionReceipt_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.WorkOrderId },
                        principalSchema: "technical",
                        principalTable: "WorkOrderExecutions",
                        principalColumns: new[] { "TenantId", "VesselId", "WorkOrderId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderExecutionRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderRevision = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderVersion = table.Column<long>(type: "bigint", nullable: false),
                    WorkOrderHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Transition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    EvidenceGate = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderExecutionRevisions", x => new { x.TenantId, x.WorkOrderId, x.Revision });
                    table.CheckConstraint("CK_WorkOrderExecutionRevision_Binding", "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"WorkOrderRevision\" > 0 AND \"WorkOrderVersion\" > 0");
                    table.CheckConstraint("CK_WorkOrderExecutionRevision_State", "\"Transition\" IN ('RELEASE','REQUIREMENTS','START','EVIDENCE','SUBMIT','VERIFY','RETURN','CLOSE') AND \"State\" IN ('RELEASED','IN_PROGRESS','EVIDENCE_SUBMITTED','VERIFIED','CLOSED') AND \"EvidenceGate\" IN ('REVIEW_REQUIRED','MISSING','PRESENT_PENDING_HUMAN_VERIFICATION','HUMAN_VERIFIED') AND (\"Provenance\" = 'UserDeclaredUnverified' OR (\"Provenance\" = 'SyntheticUnverified' AND \"State\" IN ('RELEASED','IN_PROGRESS') AND \"EvidenceGate\" <> 'HUMAN_VERIFIED'))");
                    table.ForeignKey(
                        name: "FK_WorkOrderExecutionRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.WorkOrderId },
                        principalSchema: "technical",
                        principalTable: "WorkOrderExecutions",
                        principalColumns: new[] { "TenantId", "VesselId", "WorkOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderExecutionRevision_WorkOrder",
                        columns: x => new { x.TenantId, x.WorkOrderId, x.WorkOrderRevision, x.WorkOrderVersion },
                        principalSchema: "technical",
                        principalTable: "WorkOrderRevisions",
                        principalColumns: new[] { "TenantId", "WorkOrderId", "Revision", "RecordVersion" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalRequisitionRevisions",
                schema: "technical",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpareId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpareRevision = table.Column<long>(type: "bigint", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    Transition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalRequisitionRevisions", x => new { x.TenantId, x.RequisitionId, x.Revision });
                    table.CheckConstraint("CK_TechnicalRequisitionRevision_State", "\"Revision\" > 0 AND \"RecordVersion\" > 0 AND \"SpareRevision\" > 0 AND \"Transition\" IN ('PROPOSE','APPROVE','REJECT') AND \"State\" IN ('PROPOSED','APPROVED_FOR_ERP_EXPORT','REJECTED') AND (\"Provenance\" = 'UserDeclaredUnverified' OR (\"Provenance\" = 'SyntheticUnverified' AND \"State\" = 'PROPOSED'))");
                    table.ForeignKey(
                        name: "FK_TechnicalRequisitionRevision_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.RequisitionId },
                        principalSchema: "technical",
                        principalTable: "TechnicalRequisitions",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalRequisitionRevision_Spare",
                        columns: x => new { x.TenantId, x.VesselId, x.SpareId, x.SpareRevision },
                        principalSchema: "technical",
                        principalTable: "CriticalSpareRevisions",
                        principalColumns: new[] { "TenantId", "VesselId", "SpareId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderLifecycle",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderLifecycle", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_WorkOrderOfflineLifecycle_Action", "\"Action\" IN ('Enrolled','Renewed','KeyEnrolled','Revoked','KeyLost','CleanupAllowed')");
                    table.ForeignKey(
                        name: "FK_WorkOrderOfflineLifecycle_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.WorkOrderId },
                        principalSchema: "offline",
                        principalTable: "WorkOrderEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId", "WorkOrderId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderOutcomes",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    BaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    ProtectedContext = table.Column<string>(type: "text", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderOutcomes", x => new { x.TenantId, x.ActorId, x.CommandId });
                    table.UniqueConstraint("AK_WorkOrderOfflineOutcome_ParentScope", x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId });
                    table.CheckConstraint("CK_WorkOrderOfflineOutcome_State", "\"BaseVersion\" > 0 AND \"ServerVersion\" > 0 AND \"Revision\" > 0 AND \"Result\" IN ('Committed','MaterialConflict','ServerRetained') AND (\"Result\" <> 'ServerRetained' OR \"ParentCommandId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkOrderOfflineOutcome_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.WorkOrderId },
                        principalSchema: "offline",
                        principalTable: "WorkOrderEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId", "WorkOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderOfflineOutcome_Parent",
                        columns: x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.EnrollmentId },
                        principalSchema: "offline",
                        principalTable: "WorkOrderOutcomes",
                        principalColumns: new[] { "TenantId", "ActorId", "CommandId", "EnrollmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalSpareRevision_Head",
                schema: "technical",
                table: "CriticalSpareRevisions",
                columns: new[] { "TenantId", "VesselId", "SpareId" });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalSpareRevisions_TenantId_VesselId_AssetId",
                schema: "technical",
                table: "CriticalSpareRevisions",
                columns: new[] { "TenantId", "VesselId", "AssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRequisitionRevision_Head",
                schema: "technical",
                table: "TechnicalRequisitionRevisions",
                columns: new[] { "TenantId", "VesselId", "RequisitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRequisitionRevisions_TenantId_VesselId_SpareId_Spa~",
                schema: "technical",
                table: "TechnicalRequisitionRevisions",
                columns: new[] { "TenantId", "VesselId", "SpareId", "SpareRevision" });

            migrationBuilder.CreateIndex(
                name: "UX_TechnicalRequisition_OpenProposal",
                schema: "technical",
                table: "TechnicalRequisitions",
                columns: new[] { "TenantId", "VesselId", "SpareId" },
                unique: true,
                filter: "\"State\" = 'PROPOSED'");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSupplyReceipt_Resource",
                schema: "technical",
                table: "TechnicalSupplyReceipts",
                columns: new[] { "TenantId", "VesselId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEnrollments_TenantId_VesselId_WorkOrderId",
                schema: "offline",
                table: "WorkOrderEnrollments",
                columns: new[] { "TenantId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderOfflineEnrollment_Owner",
                schema: "offline",
                table: "WorkOrderEnrollments",
                columns: new[] { "TenantId", "ActorId", "VesselId", "WorkOrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecutionReceipt_Head",
                schema: "technical",
                table: "WorkOrderExecutionReceipts",
                columns: new[] { "TenantId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecutionRevision_Head",
                schema: "technical",
                table: "WorkOrderExecutionRevisions",
                columns: new[] { "TenantId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecutionRevisions_TenantId_WorkOrderId_WorkOrderR~",
                schema: "technical",
                table: "WorkOrderExecutionRevisions",
                columns: new[] { "TenantId", "WorkOrderId", "WorkOrderRevision", "WorkOrderVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecution_State",
                schema: "technical",
                table: "WorkOrderExecutions",
                columns: new[] { "TenantId", "VesselId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderOfflineLifecycle_Scope",
                schema: "offline",
                table: "WorkOrderLifecycle",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderOfflineOutcome_ParentScope",
                schema: "offline",
                table: "WorkOrderOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderOfflineOutcome_Scope",
                schema: "offline",
                table: "WorkOrderOutcomes",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkOrderOfflineOutcome_Resolution",
                schema: "offline",
                table: "WorkOrderOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId" },
                unique: true,
                filter: "\"ParentCommandId\" IS NOT NULL");

            // DEV-054/DEV-057/DEV-110: history, receipts and offline lineage are append-only at the database too.
            migrationBuilder.Sql("""
                CREATE TRIGGER "WorkOrderExecutionRevisions_Immutable" BEFORE UPDATE OR DELETE ON technical."WorkOrderExecutionRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderExecutionRevisions_NoTruncate" BEFORE TRUNCATE ON technical."WorkOrderExecutionRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderExecutionReceipts_Immutable" BEFORE UPDATE OR DELETE ON technical."WorkOrderExecutionReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderExecutionReceipts_NoTruncate" BEFORE TRUNCATE ON technical."WorkOrderExecutionReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderLifecycle_Immutable" BEFORE UPDATE OR DELETE ON offline."WorkOrderLifecycle" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderLifecycle_NoTruncate" BEFORE TRUNCATE ON offline."WorkOrderLifecycle" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderOutcomes_Immutable" BEFORE UPDATE OR DELETE ON offline."WorkOrderOutcomes" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "WorkOrderOutcomes_NoTruncate" BEFORE TRUNCATE ON offline."WorkOrderOutcomes" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "CriticalSpareRevisions_Immutable" BEFORE UPDATE OR DELETE ON technical."CriticalSpareRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "CriticalSpareRevisions_NoTruncate" BEFORE TRUNCATE ON technical."CriticalSpareRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "TechnicalRequisitionRevisions_Immutable" BEFORE UPDATE OR DELETE ON technical."TechnicalRequisitionRevisions" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "TechnicalRequisitionRevisions_NoTruncate" BEFORE TRUNCATE ON technical."TechnicalRequisitionRevisions" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "TechnicalSupplyReceipts_Immutable" BEFORE UPDATE OR DELETE ON technical."TechnicalSupplyReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "TechnicalSupplyReceipts_NoTruncate" BEFORE TRUNCATE ON technical."TechnicalSupplyReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalRequisitionRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "TechnicalSupplyReceipts",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "WorkOrderExecutionReceipts",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "WorkOrderExecutionRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "WorkOrderLifecycle",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "WorkOrderOutcomes",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "TechnicalRequisitions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "CriticalSpareRevisions",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "WorkOrderEnrollments",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "CriticalSpares",
                schema: "technical");

            migrationBuilder.DropTable(
                name: "WorkOrderExecutions",
                schema: "technical");
        }
    }
}
