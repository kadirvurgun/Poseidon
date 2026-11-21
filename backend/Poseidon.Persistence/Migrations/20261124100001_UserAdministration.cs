using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "MembershipGrantRevisions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Bundles = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IndividualPermissions = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Permissions = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    TenantWideRead = table.Column<bool>(type: "boolean", nullable: false),
                    TenantWideManage = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrantedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipGrantRevisions", x => new { x.TenantId, x.MembershipId, x.Revision });
                    table.CheckConstraint("CK_MembershipGrantRevisions_Closed", "\"Revision\" >= 1 AND length(\"Bundles\") > 0 AND length(\"Permissions\") > 0 AND length(\"GrantedBy\") > 0");
                    table.ForeignKey(
                        name: "FK_MembershipGrantRevisions_Company",
                        columns: x => new { x.TenantId, x.ActiveCompanyId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipGrantRevisions_Membership",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipGrantVessels",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipGrantVessels", x => new { x.TenantId, x.MembershipId, x.Revision, x.VesselId });
                    table.ForeignKey(
                        name: "FK_MembershipGrantVessels_Revision",
                        columns: x => new { x.TenantId, x.MembershipId, x.Revision },
                        principalSchema: "identity",
                        principalTable: "MembershipGrantRevisions",
                        principalColumns: new[] { "TenantId", "MembershipId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipGrantVessels_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts",
                sql: "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified','AdminProvisioned') AND length(\"Issuer\") > 0 AND length(\"Subject\") > 0");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipGrantRevisions_TenantId_ActiveCompanyId",
                schema: "identity",
                table: "MembershipGrantRevisions",
                columns: new[] { "TenantId", "ActiveCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipGrantVessels_TenantId_VesselId",
                schema: "identity",
                table: "MembershipGrantVessels",
                columns: new[] { "TenantId", "VesselId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipGrantVessels",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "MembershipGrantRevisions",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships",
                sql: "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension','SecurityResume')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MembershipCommandReceipts_Action",
                schema: "identity",
                table: "MembershipCommandReceipts",
                sql: "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Closed",
                schema: "identity",
                table: "Accounts",
                sql: "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND length(\"Issuer\") > 0 AND length(\"Subject\") > 0");
        }
    }
}
