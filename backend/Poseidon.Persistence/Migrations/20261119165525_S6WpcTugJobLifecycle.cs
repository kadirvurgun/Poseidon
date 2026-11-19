using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S6WpcTugJobLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TugJob_Draft",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                schema: "tug",
                table: "Jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "tug",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                schema: "tug",
                table: "Jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobState",
                schema: "tug",
                table: "JobRevisions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_TenantId_VesselId_Status_RequestedStart",
                schema: "tug",
                table: "Jobs",
                columns: new[] { "TenantId", "VesselId", "Status", "RequestedStart" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TugJob_State",
                schema: "tug",
                table: "Jobs",
                sql: "\"Status\" IN ('Draft', 'Submitted', 'Cancelled') AND (\"Status\" <> 'Submitted' OR \"SubmittedAt\" IS NOT NULL) AND (\"Status\" <> 'Cancelled' OR \"CancelledAt\" IS NOT NULL AND \"CancelReason\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TugJobRevision_State",
                schema: "tug",
                table: "JobRevisions",
                sql: "\"JobState\" IN ('Draft', 'Submitted', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_TenantId_VesselId_Status_RequestedStart",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TugJob_State",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TugJobRevision_State",
                schema: "tug",
                table: "JobRevisions");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                schema: "tug",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "JobState",
                schema: "tug",
                table: "JobRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TugJob_Draft",
                schema: "tug",
                table: "Jobs",
                sql: "\"Status\" = 'Draft'");
        }
    }
}
