using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationQuietHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietEnd",
                schema: "engagement",
                table: "NotificationPreferences",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "QuietStart",
                schema: "engagement",
                table: "NotificationPreferences",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuietZoneId",
                schema: "engagement",
                table: "NotificationPreferences",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationPreference_QuietHours",
                schema: "engagement",
                table: "NotificationPreferences",
                sql: "(\"QuietStart\" IS NULL AND \"QuietEnd\" IS NULL AND \"QuietZoneId\" IS NULL) OR (\"QuietStart\" IS NOT NULL AND \"QuietEnd\" IS NOT NULL AND \"QuietZoneId\" IS NOT NULL AND \"QuietStart\" <> \"QuietEnd\")");

            // Owner/vessel identity stays immutable; only the actor's own preference values and revision may change.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION engagement.guard_notification_preference() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Notification preference history cannot be removed'; END IF;
                  IF (to_jsonb(OLD) - ARRAY['MutedAdvisory','QuietStart','QuietEnd','QuietZoneId','Revision']) IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['MutedAdvisory','QuietStart','QuietEnd','QuietZoneId','Revision'])
                    OR NEW."Revision" <> OLD."Revision" + 1 THEN RAISE EXCEPTION 'Immutable preference ownership'; END IF;
                  RETURN NEW;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION engagement.guard_notification_preference() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Notification preference history cannot be removed'; END IF;
                  IF (to_jsonb(OLD) - ARRAY['MutedAdvisory','Revision']) IS DISTINCT FROM (to_jsonb(NEW) - ARRAY['MutedAdvisory','Revision'])
                    OR NEW."Revision" <> OLD."Revision" + 1 THEN RAISE EXCEPTION 'Immutable preference ownership'; END IF;
                  RETURN NEW;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationPreference_QuietHours",
                schema: "engagement",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "QuietEnd",
                schema: "engagement",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "QuietStart",
                schema: "engagement",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "QuietZoneId",
                schema: "engagement",
                table: "NotificationPreferences");
        }
    }
}
