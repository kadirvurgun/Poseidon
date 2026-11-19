using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DelegationScopeAndRecoveryClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Delegations_Closed",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.AddColumn<string>(
                name: "Result",
                schema: "identity",
                table: "MembershipCommandReceipts",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "identity",
                table: "Delegations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "identity",
                table: "Delegations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                // Rows that predate the scope snapshot are marked LegacyUnscoped; they have no
                // vessel scope and the resolver never makes them effective. No business row is rewritten.
                defaultValue: "LegacyUnscoped");

            // New rows must always supply a closed reason; the backfill default is not kept.
            migrationBuilder.AlterColumn<string>(
                name: "ReasonCode",
                schema: "identity",
                table: "Delegations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "LegacyUnscoped");

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                schema: "identity",
                table: "AccessRecoveries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                schema: "identity",
                table: "AccessRecoveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DelegationVesselScopes",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationVesselScopes", x => new { x.TenantId, x.DelegationId, x.VesselId });
                    table.ForeignKey(
                        name: "FK_DelegationVesselScopes_Delegation",
                        columns: x => new { x.TenantId, x.DelegationId },
                        principalSchema: "identity",
                        principalTable: "Delegations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DelegationVesselScopes_Vessel",
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
                sql: "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension','SecurityResume')");

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_TenantId_CompanyId",
                schema: "identity",
                table: "Delegations",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Delegations_Closed",
                schema: "identity",
                table: "Delegations",
                sql: "\"Status\" IN ('Active','Revoked') AND \"ValidUntil\" > \"ValidFrom\" AND \"DelegatorMembershipId\" <> \"DelegateMembershipId\" AND length(\"Permissions\") > 0 AND \"ReasonCode\" IN ('PlannedAbsence','UnplannedAbsence','WorkloadCover','Handover','LegacyUnscoped') AND ((\"Status\" = 'Active' AND \"RevokedAt\" IS NULL AND \"RevocationReason\" IS NULL) OR (\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL AND \"RevocationReason\" IS NOT NULL AND \"RevocationReason\" IN ('AdministrativeRevocation','MembershipSuspended','MembershipRevoked')))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccessRecoveries_Resolution",
                schema: "identity",
                table: "AccessRecoveries",
                sql: "(\"ResolvedAt\" IS NULL AND \"Resolution\" IS NULL) OR (\"ResolvedAt\" IS NOT NULL AND \"Resolution\" IS NOT NULL AND \"Resolution\" IN ('SecuritySuspensionResumed'))");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationVesselScopes_TenantId_VesselId",
                schema: "identity",
                table: "DelegationVesselScopes",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Delegations_Company",
                schema: "identity",
                table: "Delegations",
                columns: new[] { "TenantId", "CompanyId" },
                principalSchema: "core",
                principalTable: "Companies",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Delegations_Company",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.DropTable(
                name: "DelegationVesselScopes",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Delegations_TenantId_CompanyId",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Delegations_Closed",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccessRecoveries_Resolution",
                schema: "identity",
                table: "AccessRecoveries");

            migrationBuilder.DropColumn(
                name: "Result",
                schema: "identity",
                table: "MembershipCommandReceipts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "identity",
                table: "Delegations");

            migrationBuilder.DropColumn(
                name: "Resolution",
                schema: "identity",
                table: "AccessRecoveries");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                schema: "identity",
                table: "AccessRecoveries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Memberships_Closed",
                schema: "identity",
                table: "Memberships",
                sql: "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Delegations_Closed",
                schema: "identity",
                table: "Delegations",
                sql: "\"Status\" IN ('Active','Revoked') AND \"ValidUntil\" > \"ValidFrom\" AND \"DelegatorMembershipId\" <> \"DelegateMembershipId\" AND length(\"Permissions\") > 0 AND ((\"Status\" = 'Active' AND \"RevokedAt\" IS NULL AND \"RevocationReason\" IS NULL) OR (\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL AND \"RevocationReason\" IN ('AdministrativeRevocation','MembershipSuspended','MembershipRevoked')))");
        }
    }
}
