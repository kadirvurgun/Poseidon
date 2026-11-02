using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored only. Designer/snapshot parity is deferred to the combined gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102230000_CrewAssignmentPolicyLineage")]
public sealed class CrewAssignmentPolicyLineage : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        foreach (var table in new[] { "Assignments", "AssignmentRevisions" }) {
            b.AddColumn<string>(name: "AssignmentPolicyVersion", schema: "crew", table: table, type: "character varying(80)", maxLength: 80, nullable: true);
            b.AddColumn<string>(name: "AssignmentPolicyFingerprint", schema: "crew", table: table, type: "character varying(64)", maxLength: 64, nullable: true);
            b.AddColumn<string>(name: "PositionPolicyVersion", schema: "crew", table: table, type: "character varying(80)", maxLength: 80, nullable: true);
            b.AddColumn<string>(name: "PositionPolicyFingerprint", schema: "crew", table: table, type: "character varying(64)", maxLength: 64, nullable: true);
        }
        // Historical declarations keep null lineage; no invented policy receipt or backfill.
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Retained declaration lineage requires a forward migration or controlled restore.");
}
