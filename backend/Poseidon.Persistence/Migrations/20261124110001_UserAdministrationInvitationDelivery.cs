using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserAdministrationInvitationDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.CreateTable(
                name: "InvitationDeliveries",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProtectedPayload = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationDeliveries", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_InvitationDeliveries_Closed", "\"State\" IN ('QUEUED','SENT','FAILED','NOT_CONFIGURED','CANCELLED') AND \"Attempts\" >= 0 AND \"Attempts\" <= 5 AND length(\"CreatedBy\") > 0 AND (\"LastErrorCode\" IS NULL OR \"LastErrorCode\" IN ('smtp_transient','smtp_rejected','smtp_retries_exhausted','not_configured','payload_unavailable','invitation_closed','invitation_expired')) AND ((\"State\" = 'QUEUED' AND \"ProtectedPayload\" IS NOT NULL AND \"NextAttemptAt\" IS NOT NULL AND \"CompletedAt\" IS NULL) OR (\"State\" <> 'QUEUED' AND \"ProtectedPayload\" IS NULL AND \"CompletedAt\" IS NOT NULL AND \"LeaseUntil\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_InvitationDeliveries_Invitation",
                        columns: x => new { x.TenantId, x.InvitationId },
                        principalSchema: "identity",
                        principalTable: "MembershipInvitations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantInvitationSettings",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifetimeHours = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvitationSettings", x => x.TenantId);
                    table.CheckConstraint("CK_TenantInvitationSettings_Lifetime", "\"LifetimeHours\" >= 1 AND \"LifetimeHours\" <= 168 AND length(\"UpdatedBy\") > 0");
                    table.ForeignKey(
                        name: "FK_TenantInvitationSettings_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts",
                sql: "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke','MembershipCreate','MembershipGrantUpdate','MembershipInvitationReissue','MembershipInvitationAccept','InvitationLifetimeUpdate')");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationDeliveries_Due",
                schema: "identity",
                table: "InvitationDeliveries",
                column: "NextAttemptAt",
                filter: "\"State\" = 'QUEUED'");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationDeliveries_Invitation",
                schema: "identity",
                table: "InvitationDeliveries",
                columns: new[] { "TenantId", "InvitationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitationDeliveries_Membership",
                schema: "identity",
                table: "InvitationDeliveries",
                columns: new[] { "TenantId", "MembershipId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationDeliveries",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "TenantInvitationSettings",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts",
                sql: "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke','MembershipCreate','MembershipGrantUpdate','MembershipInvitationReissue','MembershipInvitationAccept')");
        }
    }
}
