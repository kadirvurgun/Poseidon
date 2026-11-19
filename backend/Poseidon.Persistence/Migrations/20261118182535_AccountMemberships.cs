using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "Accounts",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_Accounts_Id", x => x.Id);
                    table.CheckConstraint("CK_Accounts_Closed", "\"AccountType\" = 'Human' AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND length(\"Issuer\") > 0 AND length(\"Subject\") > 0");
                    table.ForeignKey(
                        name: "FK_Accounts_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipCommandReceipts",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipCommandReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.CheckConstraint("CK_MembershipCommandReceipts_Action", "\"Action\" IN ('MembershipImport','MembershipSuspend','MembershipResume','MembershipRevoke','MembershipSecuritySuspend','DelegationCreate','DelegationRevoke')");
                    table.ForeignKey(
                        name: "FK_MembershipCommandReceipts_Tenant",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Memberships",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AccessRevision = table.Column<long>(type: "bigint", nullable: false),
                    RoleContext = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ActiveCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memberships", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Memberships_Closed", "\"Status\" IN ('Active','Suspended','Revoked') AND \"AccessRevision\" >= 0 AND \"Source\" IN ('ConfigurationImport','SyntheticUnverified') AND \"StatusReason\" IN ('ConfigurationImport','SyntheticFixture','AdministrativeSuspension','AdministrativeResume','AdministrativeRevocation','SecuritySuspension')");
                    table.ForeignKey(
                        name: "FK_Memberships_Account",
                        columns: x => new { x.TenantId, x.AccountId },
                        principalSchema: "identity",
                        principalTable: "Accounts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Memberships_ActiveCompany",
                        columns: x => new { x.TenantId, x.ActiveCompanyId },
                        principalSchema: "core",
                        principalTable: "Companies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessRecoveries",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cause = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRecoveries", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_AccessRecoveries_Cause", "\"Cause\" IN ('SecuritySuspension')");
                    table.ForeignKey(
                        name: "FK_AccessRecoveries_Membership",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Delegations",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegatorMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    DelegateMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RevocationReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Delegations", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_Delegations_Closed", "\"Status\" IN ('Active','Revoked') AND \"ValidUntil\" > \"ValidFrom\" AND \"DelegatorMembershipId\" <> \"DelegateMembershipId\" AND length(\"Permissions\") > 0 AND ((\"Status\" = 'Active' AND \"RevokedAt\" IS NULL AND \"RevocationReason\" IS NULL) OR (\"Status\" = 'Revoked' AND \"RevokedAt\" IS NOT NULL AND \"RevocationReason\" IN ('AdministrativeRevocation','MembershipSuspended','MembershipRevoked')))");
                    table.ForeignKey(
                        name: "FK_Delegations_Delegate",
                        columns: x => new { x.TenantId, x.DelegateMembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Delegations_Delegator",
                        columns: x => new { x.TenantId, x.DelegatorMembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipVesselGrants",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipVesselGrants", x => new { x.TenantId, x.MembershipId, x.VesselId });
                    table.ForeignKey(
                        name: "FK_MembershipVesselGrants_Membership",
                        columns: x => new { x.TenantId, x.MembershipId },
                        principalSchema: "identity",
                        principalTable: "Memberships",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipVesselGrants_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRecoveries_Raised",
                schema: "identity",
                table: "AccessRecoveries",
                columns: new[] { "TenantId", "RaisedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRecoveries_TenantId_MembershipId",
                schema: "identity",
                table: "AccessRecoveries",
                columns: new[] { "TenantId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_IssuerSubject",
                schema: "identity",
                table: "Accounts",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_Delegate",
                schema: "identity",
                table: "Delegations",
                columns: new[] { "TenantId", "DelegateMembershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Delegations_Delegator",
                schema: "identity",
                table: "Delegations",
                columns: new[] { "TenantId", "DelegatorMembershipId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_Account",
                schema: "identity",
                table: "Memberships",
                columns: new[] { "TenantId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_Status",
                schema: "identity",
                table: "Memberships",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_TenantId_ActiveCompanyId",
                schema: "identity",
                table: "Memberships",
                columns: new[] { "TenantId", "ActiveCompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipVesselGrants_TenantId_VesselId",
                schema: "identity",
                table: "MembershipVesselGrants",
                columns: new[] { "TenantId", "VesselId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRecoveries",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Delegations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "MembershipCommandReceipts",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "MembershipVesselGrants",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Accounts",
                schema: "identity");
        }
    }
}
