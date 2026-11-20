using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S9WpbSafetyEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SafetyEventEnrollments",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Challenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorityDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetainUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventEnrollments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_SafetyEventOfflineEnrollment_Scope", x => new { x.TenantId, x.Id, x.ActorId, x.VesselId });
                    table.CheckConstraint("CK_SafetyEventOfflineEnrollment_State", "\"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"");
                    table.ForeignKey(
                        name: "FK_SafetyEventOffline_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventReceipts",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_SafetyEventReceipt_Action", "\"Action\" IN ('SafetyEventCreate','SafetyEventUpdate','SafetyEventSubmit','SafetyEventWithdraw','SafetyEventTriage','SafetyEventIdentityAccess','SafetyEventMessage','SafetyInvestigationSave','SafetyInvestigationSubmit','SafetyInvestigationReview','SafetyActionCreate','SafetyActionStep','SafetyEventClose') AND \"Revision\">0");
                    table.ForeignKey(
                        name: "FK_SafetyEventReceipts_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEvents",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventNo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReportingMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    InvestigationRequired = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Origin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEvents", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_SafetyEvents_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_SafetyEvent_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','TRIAGED','INVESTIGATING','ACTION_TRACKING','CLOSED','WITHDRAWN') AND \"ReportingMode\" IN ('NAMED','CONFIDENTIAL') AND \"Origin\" IN ('ONLINE','OFFLINE') AND \"Provenance\" IN ('UserDeclaredUnverified','SyntheticUnverified') AND (\"Category\" IS NULL OR \"Category\" IN ('NEAR_MISS','INCIDENT','ACCIDENT','AUDIT_FINDING','DEFECT','FATIGUE','ENVIRONMENTAL','CYBER','OBSERVATION')) AND (\"Severity\" IS NULL OR \"Severity\" IN ('UNASSESSED','LOW','MEDIUM','HIGH','CRITICAL')) AND (\"InvestigationRequired\" IS NULL OR \"InvestigationRequired\" IN ('YES','NO','UNKNOWN')) AND ((\"ReportingMode\"='NAMED') = (\"ReportedBy\" IS NOT NULL)) AND (\"State\"<>'CLOSED' OR \"ClosedBy\" IS NOT NULL AND length(\"ClosureReason\")>=10) AND (\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT' AND \"Severity\" IS NULL AND \"SubmittedAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_SafetyEvents_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventLifecycle",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventLifecycle", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyEventOfflineLifecycle_Action", "\"Action\" IN ('Enrolled','Renewed','KeyEnrolled','Revoked','KeyLost','CleanupAllowed')");
                    table.ForeignKey(
                        name: "FK_SafetyEventOfflineLifecycle_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId },
                        principalSchema: "offline",
                        principalTable: "SafetyEventEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyActions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RootCauseIdsJson = table.Column<string>(type: "text", nullable: false),
                    BarrierIdsJson = table.Column<string>(type: "text", nullable: false),
                    FollowUpOf = table.Column<Guid>(type: "uuid", nullable: true),
                    InvestigationRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Effectiveness = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectivenessBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyActions", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_SafetyActions_TenantId_VesselId_Id", x => new { x.TenantId, x.VesselId, x.Id });
                    table.CheckConstraint("CK_SafetyAction_State", "\"Revision\">0 AND \"Kind\" IN ('CORRECTIVE','PREVENTIVE') AND \"State\" IN ('OPEN','COMPLETED','VERIFIED') AND \"Effectiveness\" IN ('PENDING','EFFECTIVE','NOT_EFFECTIVE','INCONCLUSIVE') AND (\"Effectiveness\"='PENDING' OR \"State\"='VERIFIED' AND \"EffectivenessBy\" IS NOT NULL) AND (\"State\"<>'VERIFIED' OR \"VerifiedBy\" IS NOT NULL AND \"CompletedBy\" IS NOT NULL) AND (\"FollowUpOf\" IS NULL OR \"FollowUpOf\"<>\"Id\")");
                    table.ForeignKey(
                        name: "FK_SafetyActions_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventIdentities",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactPreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpHandle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FollowUpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventIdentities", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyEventIdentity_Contact", "\"ContactPreference\" IN ('NONE','IN_APP_FOLLOW_UP') AND ((\"FollowUpHandle\" IS NULL) = (\"FollowUpHash\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_SafetyEventIdentities_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventIdentityAccesses",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventRevision = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventIdentityAccesses", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyEventIdentityAccess_Purpose", "\"Purpose\" IN ('INVESTIGATION_FOLLOW_UP','WELFARE_SUPPORT','LEGAL_OBLIGATION') AND length(\"Reason\")>=10");
                    table.ForeignKey(
                        name: "FK_SafetyEventIdentityAccesses_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventMessages",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventRevision = table.Column<long>(type: "bigint", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventMessages", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyEventMessage_Kind", "\"Visibility\" IN ('TEAM_ONLY','REPORTER_SHARED') AND \"AuthorKind\" IN ('TEAM','REPORTER') AND ((\"AuthorKind\"='REPORTER') = (\"ActorId\" IS NULL)) AND (\"AuthorKind\"<>'REPORTER' OR \"Visibility\"='REPORTER_SHARED') AND length(\"Body\") BETWEEN 1 AND 4000");
                    table.ForeignKey(
                        name: "FK_SafetyEventMessages_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventOutcomes",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventOutcomes", x => new { x.TenantId, x.ActorId, x.CommandId });
                    table.CheckConstraint("CK_SafetyEventOfflineOutcome_State", "\"Result\" IN ('Committed') AND \"Revision\">0");
                    table.ForeignKey(
                        name: "FK_SafetyEventOfflineOutcome_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId },
                        principalSchema: "offline",
                        principalTable: "SafetyEventEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyEventOfflineOutcome_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventRevisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventRevisions", x => new { x.TenantId, x.EventId, x.Revision });
                    table.CheckConstraint("CK_SafetyEventRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','TRIAGED','INVESTIGATING','ACTION_TRACKING','CLOSED','WITHDRAWN') AND \"ReasonCode\" IN ('CREATE','EDIT','SUBMIT','WITHDRAW','TRIAGE','RETURN','INVESTIGATION_OPENED','INVESTIGATION_APPROVED','INVESTIGATION_REJECTED','CLOSE','OFFLINE_CAPTURE','SYNTHETIC_INCOMPLETE_DRAFT')");
                    table.ForeignKey(
                        name: "FK_SafetyEventRevisions_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyEventTriages",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ResultRevision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    InvestigationRequired = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyEventTriages", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyEventTriage_Decision", "\"Decision\" IN ('TRIAGE','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND (\"Decision\"='RETURN') = (\"Severity\" IS NULL AND \"InvestigationRequired\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_SafetyEventTriages_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyInvestigations",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorsJson = table.Column<string>(type: "text", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedRevision = table.Column<long>(type: "bigint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyInvestigations", x => new { x.TenantId, x.EventId });
                    table.UniqueConstraint("AK_SafetyInvestigations_TenantId_VesselId_EventId", x => new { x.TenantId, x.VesselId, x.EventId });
                    table.CheckConstraint("CK_SafetyInvestigation_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','APPROVED','REJECTED') AND (\"State\"<>'APPROVED' OR \"ApprovedRevision\"=\"Revision\")");
                    table.ForeignKey(
                        name: "FK_SafetyInvestigations_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyActionSteps",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionRevision = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyActionSteps", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyActionStep_Kind", "\"Kind\" IN ('COMPLETE','VERIFY','EFFECTIVENESS') AND \"ActionRevision\">1 AND length(\"Text\")>=10 AND (\"Kind\"<>'COMPLETE' OR \"Outcome\" IS NULL) AND (\"Kind\"<>'VERIFY' OR \"Outcome\" IN ('VERIFIED','NOT_VERIFIED')) AND (\"Kind\"<>'EFFECTIVENESS' OR \"Outcome\" IN ('EFFECTIVE','NOT_EFFECTIVE','INCONCLUSIVE'))");
                    table.ForeignKey(
                        name: "FK_SafetyActionSteps_Action",
                        columns: x => new { x.TenantId, x.VesselId, x.ActionId },
                        principalSchema: "hse",
                        principalTable: "SafetyActions",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyInvestigationRevisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyInvestigationRevisions", x => new { x.TenantId, x.EventId, x.Revision });
                    table.CheckConstraint("CK_SafetyInvestigationRevision_State", "\"Revision\">0 AND \"State\" IN ('DRAFT','SUBMITTED','RETURNED','APPROVED','REJECTED') AND \"ReasonCode\" IN ('CREATE','EDIT','REVISE','SUBMIT','APPROVE','RETURN','REJECT')");
                    table.ForeignKey(
                        name: "FK_SafetyInvestigationRevisions_Investigation",
                        columns: x => new { x.TenantId, x.VesselId, x.EventId },
                        principalSchema: "hse",
                        principalTable: "SafetyInvestigations",
                        principalColumns: new[] { "TenantId", "VesselId", "EventId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyInvestigationDecisions",
                schema: "hse",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Decision = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyInvestigationDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_SafetyInvestigationDecision_Kind", "\"Decision\" IN ('APPROVE','RETURN','REJECT') AND length(\"Reason\")>=10");
                    table.ForeignKey(
                        name: "FK_SafetyInvestigationDecisions_Revision",
                        columns: x => new { x.TenantId, x.EventId, x.Revision },
                        principalSchema: "hse",
                        principalTable: "SafetyInvestigationRevisions",
                        principalColumns: new[] { "TenantId", "EventId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyActions_TenantId_EventId",
                schema: "hse",
                table: "SafetyActions",
                columns: new[] { "TenantId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyActions_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyActions",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyActionSteps_TenantId_ActionId_ActionRevision",
                schema: "hse",
                table: "SafetyActionSteps",
                columns: new[] { "TenantId", "ActionId", "ActionRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyActionSteps_TenantId_VesselId_ActionId",
                schema: "hse",
                table: "SafetyActionSteps",
                columns: new[] { "TenantId", "VesselId", "ActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventEnrollments_TenantId_VesselId",
                schema: "offline",
                table: "SafetyEventEnrollments",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventOfflineEnrollment_Owner",
                schema: "offline",
                table: "SafetyEventEnrollments",
                columns: new[] { "TenantId", "ActorId", "VesselId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventIdentities_FollowUpHandle",
                schema: "hse",
                table: "SafetyEventIdentities",
                column: "FollowUpHandle",
                unique: true,
                filter: "\"FollowUpHandle\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventIdentities_TenantId_EventId",
                schema: "hse",
                table: "SafetyEventIdentities",
                columns: new[] { "TenantId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventIdentities_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyEventIdentities",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventIdentityAccesses_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyEventIdentityAccesses",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventLifecycle_TenantId_EnrollmentId_ActorId_VesselId",
                schema: "offline",
                table: "SafetyEventLifecycle",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventMessages_TenantId_EventId_CreatedAt",
                schema: "hse",
                table: "SafetyEventMessages",
                columns: new[] { "TenantId", "EventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventMessages_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyEventMessages",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventOfflineOutcome_History",
                schema: "offline",
                table: "SafetyEventOutcomes",
                columns: new[] { "TenantId", "ActorId", "EnrollmentId", "ReceivedAt", "CommandId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventOutcomes_TenantId_EnrollmentId_ActorId_VesselId",
                schema: "offline",
                table: "SafetyEventOutcomes",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventOutcomes_TenantId_VesselId_EventId",
                schema: "offline",
                table: "SafetyEventOutcomes",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventReceipts_TenantId_VesselId",
                schema: "hse",
                table: "SafetyEventReceipts",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventRevisions_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyEventRevisions",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_TenantId_EventNo",
                schema: "hse",
                table: "SafetyEvents",
                columns: new[] { "TenantId", "EventNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEvents_TenantId_VesselId_State",
                schema: "hse",
                table: "SafetyEvents",
                columns: new[] { "TenantId", "VesselId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventTriages_TenantId_EventId_Revision",
                schema: "hse",
                table: "SafetyEventTriages",
                columns: new[] { "TenantId", "EventId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyEventTriages_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyEventTriages",
                columns: new[] { "TenantId", "VesselId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigationDecisions_TenantId_EventId_Revision",
                schema: "hse",
                table: "SafetyInvestigationDecisions",
                columns: new[] { "TenantId", "EventId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyInvestigationRevisions_TenantId_VesselId_EventId",
                schema: "hse",
                table: "SafetyInvestigationRevisions",
                columns: new[] { "TenantId", "VesselId", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafetyActionSteps",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventIdentities",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventIdentityAccesses",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventLifecycle",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "SafetyEventMessages",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventOutcomes",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "SafetyEventReceipts",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventRevisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventTriages",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyInvestigationDecisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyActions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEventEnrollments",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "SafetyInvestigationRevisions",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyInvestigations",
                schema: "hse");

            migrationBuilder.DropTable(
                name: "SafetyEvents",
                schema: "hse");
        }
    }
}
