using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Controlled additive migration, hand-authored. Actual parity/application NOT RUN.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102170000_HumanDecisionRecording")]
public sealed class HumanDecisionRecording : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("DecisionJson", "HumanReviewEvents", schema: "core", type: "text", nullable: true);
        m.DropCheckConstraint("CK_HumanReviewEvents", "HumanReviewEvents", "core");
        m.AddCheckConstraint("CK_HumanReviewEvents", "HumanReviewEvents", HumanReviewStorage.Constraint, "core");
        // Existing immutable events/receipts and append-only triggers are retained.
    }
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException(
        "Recording history must be retained. Use a reviewed forward migration; destructive downgrade is unavailable.");
}
