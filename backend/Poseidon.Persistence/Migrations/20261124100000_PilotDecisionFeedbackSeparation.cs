using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // Independent QA of 2177c023 (Medium): the program decision's separation of duties must also exclude feedback
    // authors, mirroring the week-close rule across the program's weeks. pilot.guard_pilot_duty() is replaced so that a
    // decision is refused (23514) when the decider authored any feedback of the program's weeks (W1-W4) or feedback
    // linked from any active checklist link of the program. Mirrors PilotProgramPolicy.DecisionDutyConflicts. The week
    // branch (including the NEW."Week" = 'W4' readout rule pinned by PilotProgramPolicyTests) is unchanged; the
    // triggers installed by 20261124090000_PilotProgramDutySeparation keep pointing at the replaced function.
    // DDL only; every lookup is tenant-scoped by NEW."TenantId". Down restores the previous function body.
    // No model change: the target model equals the current snapshot.
    /// <inheritdoc />
    public partial class PilotDecisionFeedbackSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pilot.guard_pilot_duty() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME = 'PilotWeekCloses' THEN
                    IF EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                          AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."ActorId" = NEW."ActorId")
                      OR EXISTS (SELECT 1 FROM pilot."PilotFeedback" f WHERE f."TenantId" = NEW."TenantId" AND f."ProgramId" = NEW."ProgramId" AND f."ActorId" = NEW."ActorId"
                          AND (f."Week" = NEW."Week" OR EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                            AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_FEEDBACK' AND l."RecordId" = f."Id")))
                      OR EXISTS (SELECT 1 FROM pilot."PilotMeasurements" m WHERE m."TenantId" = NEW."TenantId" AND m."ProgramId" = NEW."ProgramId" AND m."ActorId" = NEW."ActorId"
                          AND (EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                                AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_MEASUREMENT' AND l."RecordId" = m."Id")
                            OR NEW."Week" = 'W4' AND NOT EXISTS (SELECT 1 FROM pilot."PilotMeasurements" n WHERE n."TenantId" = m."TenantId" AND n."ProgramId" = m."ProgramId"
                                AND n."MetricId" = m."MetricId" AND n."Mode" = m."Mode" AND n."Phase" = m."Phase" AND n."Sequence" > m."Sequence")))
                    THEN RAISE EXCEPTION 'Pilot week close requires separation of duties' USING ERRCODE = '23514'; END IF;
                  ELSE
                    IF EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                          AND l."WithdrawnAt" IS NULL AND l."ActorId" = NEW."ActorId")
                      OR EXISTS (SELECT 1 FROM pilot."PilotMeasurements" m WHERE m."TenantId" = NEW."TenantId" AND m."ProgramId" = NEW."ProgramId" AND m."ActorId" = NEW."ActorId"
                          AND (EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                                AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_MEASUREMENT' AND l."RecordId" = m."Id")
                            OR NOT EXISTS (SELECT 1 FROM pilot."PilotMeasurements" n WHERE n."TenantId" = m."TenantId" AND n."ProgramId" = m."ProgramId"
                                AND n."MetricId" = m."MetricId" AND n."Mode" = m."Mode" AND n."Phase" = m."Phase" AND n."Sequence" > m."Sequence")))
                      OR EXISTS (SELECT 1 FROM pilot."PilotFeedback" f WHERE f."TenantId" = NEW."TenantId" AND f."ProgramId" = NEW."ProgramId" AND f."ActorId" = NEW."ActorId"
                          AND (f."Week" IN ('W1', 'W2', 'W3', 'W4') OR EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                            AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_FEEDBACK' AND l."RecordId" = f."Id")))
                    THEN RAISE EXCEPTION 'Pilot program decision requires separation of duties' USING ERRCODE = '23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pilot.guard_pilot_duty() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_TABLE_NAME = 'PilotWeekCloses' THEN
                    IF EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                          AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."ActorId" = NEW."ActorId")
                      OR EXISTS (SELECT 1 FROM pilot."PilotFeedback" f WHERE f."TenantId" = NEW."TenantId" AND f."ProgramId" = NEW."ProgramId" AND f."ActorId" = NEW."ActorId"
                          AND (f."Week" = NEW."Week" OR EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                            AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_FEEDBACK' AND l."RecordId" = f."Id")))
                      OR EXISTS (SELECT 1 FROM pilot."PilotMeasurements" m WHERE m."TenantId" = NEW."TenantId" AND m."ProgramId" = NEW."ProgramId" AND m."ActorId" = NEW."ActorId"
                          AND (EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                                AND l."Week" = NEW."Week" AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_MEASUREMENT' AND l."RecordId" = m."Id")
                            OR NEW."Week" = 'W4' AND NOT EXISTS (SELECT 1 FROM pilot."PilotMeasurements" n WHERE n."TenantId" = m."TenantId" AND n."ProgramId" = m."ProgramId"
                                AND n."MetricId" = m."MetricId" AND n."Mode" = m."Mode" AND n."Phase" = m."Phase" AND n."Sequence" > m."Sequence")))
                    THEN RAISE EXCEPTION 'Pilot week close requires separation of duties' USING ERRCODE = '23514'; END IF;
                  ELSE
                    IF EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                          AND l."WithdrawnAt" IS NULL AND l."ActorId" = NEW."ActorId")
                      OR EXISTS (SELECT 1 FROM pilot."PilotMeasurements" m WHERE m."TenantId" = NEW."TenantId" AND m."ProgramId" = NEW."ProgramId" AND m."ActorId" = NEW."ActorId"
                          AND (EXISTS (SELECT 1 FROM pilot."PilotChecklistLinks" l WHERE l."TenantId" = NEW."TenantId" AND l."ProgramId" = NEW."ProgramId"
                                AND l."WithdrawnAt" IS NULL AND l."RecordType" = 'PILOT_MEASUREMENT' AND l."RecordId" = m."Id")
                            OR NOT EXISTS (SELECT 1 FROM pilot."PilotMeasurements" n WHERE n."TenantId" = m."TenantId" AND n."ProgramId" = m."ProgramId"
                                AND n."MetricId" = m."MetricId" AND n."Mode" = m."Mode" AND n."Phase" = m."Phase" AND n."Sequence" > m."Sequence")))
                    THEN RAISE EXCEPTION 'Pilot program decision requires separation of duties' USING ERRCODE = '23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                """);
        }
    }
}
