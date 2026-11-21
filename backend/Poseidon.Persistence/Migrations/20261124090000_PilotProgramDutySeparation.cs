using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // Final-gate QA N3b follow-up: database-level separation of duties for the pilot program kit. Mirrors
    // PilotProgramPolicy.WeekCloseDutyConflicts / DecisionDutyConflicts so that a week close or program decision whose
    // Product Owner authored the evidence it rests on cannot be inserted even outside the API path.
    // - Week close: no active link of the week, no feedback of the week or linked from it, no measurement linked from the
    //   week and, for W4 (the only week whose checklist uses the value readout), no measurement the readout uses
    //   (latest per metric/mode/phase) authored by the closer.
    // - Decision: no active link of the program and no measurement the readout uses or a link references authored by
    //   the decider.
    // No model change: the target model equals the current snapshot.
    /// <inheritdoc />
    public partial class PilotProgramDutySeparation : Migration
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
                    THEN RAISE EXCEPTION 'Pilot program decision requires separation of duties' USING ERRCODE = '23514'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "PilotWeekClose_DutySeparation" BEFORE INSERT ON pilot."PilotWeekCloses" FOR EACH ROW EXECUTE FUNCTION pilot.guard_pilot_duty();
                CREATE TRIGGER "PilotProgramDecision_DutySeparation" BEFORE INSERT ON pilot."PilotProgramDecisions" FOR EACH ROW EXECUTE FUNCTION pilot.guard_pilot_duty();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "PilotProgramDecision_DutySeparation" ON pilot."PilotProgramDecisions";
                DROP TRIGGER IF EXISTS "PilotWeekClose_DutySeparation" ON pilot."PilotWeekCloses";
                DROP FUNCTION IF EXISTS pilot.guard_pilot_duty();
                """);
        }
    }
}
