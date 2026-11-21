using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QaNotesHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_VoiceEventDraftRevision_State",
                schema: "intelligence",
                table: "VoiceEventDraftRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TugDailyLogReview_Kind",
                schema: "tug",
                table: "TugDailyLogReviews");

            migrationBuilder.AddColumn<string>(
                name: "AuthorsJson",
                schema: "tug",
                table: "TugDailyLogReviews",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedBy",
                schema: "tug",
                table: "TugDailyLogReviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // QA S9 N5: derive the submitter and authors of every existing reviewed revision from the immutable revision
            // history (the reviewed revision is the SUBMIT revision; authors are the actors of the non-review revisions (SUBMIT included, as WithAuthor does) up
            // to it). Schema backfill of derived columns only: no business state changes, tenant/log keys are preserved.
            migrationBuilder.Sql("""
                UPDATE tug."TugDailyLogReviews" r SET
                  "SubmittedBy" = s."ActorId",
                  "AuthorsJson" = COALESCE((SELECT jsonb_agg(DISTINCT a."ActorId"::text)::text FROM tug."TugDailyLogRevisions" a
                      WHERE a."TenantId" = r."TenantId" AND a."LogId" = r."LogId" AND a."Revision" <= r."Revision"
                        AND a."ReasonCode" NOT IN ('VERIFY','RETURN')), '[]')
                FROM tug."TugDailyLogRevisions" s
                WHERE s."TenantId" = r."TenantId" AND s."LogId" = r."LogId" AND s."Revision" = r."Revision";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_VoiceEventDraftRevision_State",
                schema: "intelligence",
                table: "VoiceEventDraftRevisions",
                sql: "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"ReasonCode\" IN ('CREATE','EDIT','CONFIRM','REJECT','LINK')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TugDailyLogReview_Kind",
                schema: "tug",
                table: "TugDailyLogReviews",
                sql: "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND \"Duty\" IN ('TugMaster','Master') AND \"Reconciliation\" IN ('RECONCILED','REVIEW_REQUIRED','INCOMPLETE') AND \"ActorId\"<>\"SubmittedBy\" AND NOT (\"AuthorsJson\"::jsonb @> jsonb_build_array(\"ActorId\"::text))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_VoiceEventDraftRevision_State",
                schema: "intelligence",
                table: "VoiceEventDraftRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TugDailyLogReview_Kind",
                schema: "tug",
                table: "TugDailyLogReviews");

            migrationBuilder.DropColumn(
                name: "AuthorsJson",
                schema: "tug",
                table: "TugDailyLogReviews");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                schema: "tug",
                table: "TugDailyLogReviews");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VoiceEventDraftRevision_State",
                schema: "intelligence",
                table: "VoiceEventDraftRevisions",
                sql: "\"Revision\">0 AND \"State\" IN ('DRAFT','CONFIRMED','REJECTED') AND \"ReasonCode\" IN ('CREATE','EDIT','CONFIRM','REJECT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TugDailyLogReview_Kind",
                schema: "tug",
                table: "TugDailyLogReviews",
                sql: "\"Decision\" IN ('VERIFY','RETURN') AND \"ResultRevision\"=\"Revision\"+1 AND length(\"Reason\")>=10 AND \"Duty\" IN ('TugMaster','Master') AND \"Reconciliation\" IN ('RECONCILED','REVIEW_REQUIRED','INCOMPLETE')");
        }
    }
}
