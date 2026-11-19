using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S7WpaManningDispensation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CrewSafeManningDecision_Values",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.AddColumn<int>(
                name: "AcknowledgementCount",
                schema: "crew",
                table: "SafeManningDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementsJson",
                schema: "crew",
                table: "SafeManningDecisions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "DispensationCount",
                schema: "crew",
                table: "SafeManningDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DispensationsJson",
                schema: "crew",
                table: "SafeManningDecisions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrewSafeManningDecision_Values",
                schema: "crew",
                table: "SafeManningDecisions",
                sql: "\"Decision\" IN ('MANNING_ACCEPTED','MANNING_NOT_ACCEPTED','MANNING_HOLD') AND \"Duty\" IN ('Master','DPA') AND \"EvaluationState\" IN ('ELIGIBLE','ATTENTION','NOT_ELIGIBLE','REVIEW_REQUIRED') AND \"Sequence\">0 AND \"WindowTo\">=\"WindowFrom\" AND \"AcknowledgementCount\">=0 AND \"DispensationCount\">=0 AND (\"Decision\"='MANNING_ACCEPTED' OR \"AcknowledgementCount\"=0 AND \"DispensationCount\"=0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CrewSafeManningDecision_Values",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.DropColumn(
                name: "AcknowledgementCount",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.DropColumn(
                name: "AcknowledgementsJson",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.DropColumn(
                name: "DispensationCount",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.DropColumn(
                name: "DispensationsJson",
                schema: "crew",
                table: "SafeManningDecisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrewSafeManningDecision_Values",
                schema: "crew",
                table: "SafeManningDecisions",
                sql: "\"Decision\" IN ('MANNING_ACCEPTED','MANNING_NOT_ACCEPTED') AND \"Duty\" IN ('Master','DPA') AND \"EvaluationState\" IN ('ELIGIBLE','ATTENTION','NOT_ELIGIBLE','REVIEW_REQUIRED') AND \"Sequence\">0 AND \"WindowTo\">=\"WindowFrom\"");
        }
    }
}
