using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S9IntegrationVoiceSafetyEventLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandoffReason",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDrafts_TenantId_SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                columns: new[] { "TenantId", "SafetyEventId" },
                unique: true,
                filter: "\"SafetyEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceEventDrafts_TenantId_VesselId_SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                columns: new[] { "TenantId", "VesselId", "SafetyEventId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_VoiceEventDraft_SafetyEventHandoff",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                sql: "(\"SafetyEventId\" IS NULL OR \"State\"='CONFIRMED' AND \"HandoffReason\" IS NULL) AND (\"HandoffReason\" IS NULL OR \"State\"='CONFIRMED' AND \"HandoffReason\" IN ('SAFETY_EVENT_REPORT_PERMISSION_REQUIRED'))");

            migrationBuilder.AddForeignKey(
                name: "FK_VoiceEventDraft_SafetyEvent",
                schema: "intelligence",
                table: "VoiceEventDrafts",
                columns: new[] { "TenantId", "VesselId", "SafetyEventId" },
                principalSchema: "hse",
                principalTable: "SafetyEvents",
                principalColumns: new[] { "TenantId", "VesselId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VoiceEventDraft_SafetyEvent",
                schema: "intelligence",
                table: "VoiceEventDrafts");

            migrationBuilder.DropIndex(
                name: "IX_VoiceEventDrafts_TenantId_SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts");

            migrationBuilder.DropIndex(
                name: "IX_VoiceEventDrafts_TenantId_VesselId_SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VoiceEventDraft_SafetyEventHandoff",
                schema: "intelligence",
                table: "VoiceEventDrafts");

            migrationBuilder.DropColumn(
                name: "HandoffReason",
                schema: "intelligence",
                table: "VoiceEventDrafts");

            migrationBuilder.DropColumn(
                name: "SafetyEventId",
                schema: "intelligence",
                table: "VoiceEventDrafts");
        }
    }
}
