using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S10WpcPilotOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CertificateRenewalEnrollments",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CertificateRenewalEnrollments", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_CertificateRenewalOfflineEnrollment_Scope", x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.RenewalId });
                    table.CheckConstraint("CK_CertificateRenewalOfflineEnrollment_State", "\"BaseVersion\" > 0 AND \"BaseRevision\" > 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"");
                    table.ForeignKey(
                        name: "FK_CertificateRenewalOffline_Renewal",
                        columns: x => new { x.TenantId, x.VesselId, x.CertificateId, x.RenewalId },
                        principalSchema: "assurance",
                        principalTable: "Renewals",
                        principalColumns: new[] { "TenantId", "VesselId", "CertificateId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRenewalLifecycle",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRenewalLifecycle", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_CertificateRenewalOfflineLifecycle_Action", "\"Action\" IN ('Enrolled','Renewed','KeyEnrolled','Revoked','KeyLost','CleanupAllowed')");
                    table.ForeignKey(
                        name: "FK_CertificateRenewalOfflineLifecycle_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.RenewalId },
                        principalSchema: "offline",
                        principalTable: "CertificateRenewalEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId", "RenewalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRenewalOutcomes",
                schema: "offline",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CertificateRenewalOutcomes", x => new { x.TenantId, x.ActorId, x.CommandId });
                    table.UniqueConstraint("AK_CertificateRenewalOfflineOutcome_ParentScope", x => new { x.TenantId, x.ActorId, x.CommandId, x.EnrollmentId });
                    table.CheckConstraint("CK_CertificateRenewalOfflineOutcome_State", "\"BaseVersion\" > 0 AND \"ServerVersion\" > 0 AND \"Revision\" > 0 AND \"Result\" IN ('Committed','MaterialConflict','ServerRetained') AND (\"Result\" <> 'ServerRetained' OR \"ParentCommandId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CertificateRenewalOfflineOutcome_Enrollment",
                        columns: x => new { x.TenantId, x.EnrollmentId, x.ActorId, x.VesselId, x.RenewalId },
                        principalSchema: "offline",
                        principalTable: "CertificateRenewalEnrollments",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId", "RenewalId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateRenewalOfflineOutcome_Parent",
                        columns: x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.EnrollmentId },
                        principalSchema: "offline",
                        principalTable: "CertificateRenewalOutcomes",
                        principalColumns: new[] { "TenantId", "ActorId", "CommandId", "EnrollmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalEnrollments_TenantId_VesselId_Certificate~",
                schema: "offline",
                table: "CertificateRenewalEnrollments",
                columns: new[] { "TenantId", "VesselId", "CertificateId", "RenewalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalOfflineEnrollment_Owner",
                schema: "offline",
                table: "CertificateRenewalEnrollments",
                columns: new[] { "TenantId", "ActorId", "VesselId", "RenewalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalOfflineLifecycle_Scope",
                schema: "offline",
                table: "CertificateRenewalLifecycle",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId", "RenewalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalOfflineOutcome_ParentScope",
                schema: "offline",
                table: "CertificateRenewalOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId", "EnrollmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalOfflineOutcome_Scope",
                schema: "offline",
                table: "CertificateRenewalOutcomes",
                columns: new[] { "TenantId", "EnrollmentId", "ActorId", "VesselId", "RenewalId" });

            migrationBuilder.CreateIndex(
                name: "UX_CertificateRenewalOfflineOutcome_Resolution",
                schema: "offline",
                table: "CertificateRenewalOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId" },
                unique: true,
                filter: "\"ParentCommandId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateRenewalLifecycle",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "CertificateRenewalOutcomes",
                schema: "offline");

            migrationBuilder.DropTable(
                name: "CertificateRenewalEnrollments",
                schema: "offline");
        }
    }
}
