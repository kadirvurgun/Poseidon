using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // Guide finding 22.11.2026: database-level independent review for ship daily logs (Noon / Deck / Engine). Mirrors
    // DailyReviewPolicy.IndependentReviewer and the tug daily log precedent (QA S9 N5, CK_TugDailyLogReview_Kind): a review
    // event whose actor authored any revision of the record up to and including the reviewed revision cannot be inserted,
    // even outside the API path. Sources: 13.03 paragraph:35-37/86-89, 60.01 Rev3 paragraph:544-546, ISM Code 10.2/12.
    // Existing (historical, immutable) review events are not rewritten; the trigger guards every new insert.
    // No model change: the target model equals the current snapshot.
    /// <inheritdoc />
    public partial class DailyReviewIndependence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION operations.guard_daily_review_independence() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM operations."DailyOperationRevisions" r WHERE r."TenantId" = NEW."TenantId" AND r."VesselId" = NEW."VesselId"
                        AND r."Kind" = NEW."Kind" AND r."RecordId" = NEW."RecordId" AND r."Revision" <= NEW."RecordRevision" AND r."ActorId" = NEW."ActorId")
                  THEN RAISE EXCEPTION 'Daily log review requires an independent reviewer' USING ERRCODE = '23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "DailyReviewEvent_ReviewerNotAuthor" BEFORE INSERT ON operations."DailyReviewEvents" FOR EACH ROW EXECUTE FUNCTION operations.guard_daily_review_independence();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "DailyReviewEvent_ReviewerNotAuthor" ON operations."DailyReviewEvents";
                DROP FUNCTION IF EXISTS operations.guard_daily_review_independence();
                """);
        }
    }
}
