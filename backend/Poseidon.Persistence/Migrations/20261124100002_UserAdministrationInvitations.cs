using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserAdministrationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // USER-ADMIN-02 migration path: rows provisioned before this change keep their subject and Active state, so
            // they are treated as already accepted bindings (no data rewrite). Only new admin-created memberships start
            // Invited with a null subject. The unique (Issuer, Subject) index now covers bound (accepted) accounts only.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_IssuerSubject",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                schema: "identity",
                table: "Accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateTable(
                name: "MembershipInvitations",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpectedSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipInvitations", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_MembershipInvitations_Closed", "\"Status\" IN ('Pending','Accepted','Revoked','Superseded') AND \"ExpiresAt\" > \"CreatedAt\" AND length(\"TokenHash\") = 64 AND length(\"CreatedBy\") > 0 AND (\"ExpectedSubject\" IS NULL OR length(\"ExpectedSubject\") > 0) AND ((\"Status\" = 'Pending' AND \"ClosedAt\" IS NULL) OR (\"Status\" <> 'Pending' AND \"ClosedAt\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_MembershipInvitations_Membership",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships",
                sql: "\"Status\" IN ('Active','Suspended','Revoked','Invited') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension','SecurityResume','AdminProvisioning','AdminInvitation','InvitationAccepted','InvitationRevoked') AND (\"Status\" <> 'Invited' OR \"Source\" = 'AdminProvisioned')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts",
                sql: "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke','MembershipCreate','MembershipGrantUpdate','MembershipInvitationReissue','MembershipInvitationAccept')");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_IssuerSubject",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "Issuer", "Subject" },
                unique: true,
                filter: "\"Subject\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts",
                sql: "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND length(\"Issuer\") > 0 AND (COALESCE(length(\"Subject\"), 0) > 0 OR (\"Subject\" IS NULL AND \"Source\" = 'AdminProvisioned'))");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_Pending",
                schema: "identity",
                table: "MembershipInvitations",
                columns: new[] { "TenantId", "MembershipId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_TokenHash",
                schema: "identity",
                table: "MembershipInvitations",
                columns: new[] { "TenantId", "TokenHash" },
                unique: true);

            // QA N3: pseudonymous identity reference and unverified claimed tenant for invitation-session security events.
            migrationBuilder.AddColumn<string>(
                name: "IdentityReference",
                schema: "audit",
                table: "SecurityEvents",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnverifiedTenantId",
                schema: "audit",
                table: "SecurityEvents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down is only valid while no unaccepted invitation account (null subject) exists; it fails closed otherwise.

            migrationBuilder.DropColumn(
                name: "IdentityReference",
                schema: "audit",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "UnverifiedTenantId",
                schema: "audit",
                table: "SecurityEvents");

            migrationBuilder.DropTable(
                name: "MembershipInvitations",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_IssuerSubject",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                schema: "identity",
                table: "Accounts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships",
                sql: "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension','SecurityResume','AdminProvisioning')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts",
                sql: "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke','MembershipCreate','MembershipGrantUpdate')");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_IssuerSubject",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts",
                sql: "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND length(\"Issuer\") > 0 AND length(\"Subject\") > 0");
        }
    }
}
