using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored. Snapshot/designer reconciliation belongs to the combined gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102190000_TugCapabilityBindings")]
public sealed class TugCapabilityBindings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateTable("CapabilityBindings", schema: "tug", columns: t => new {
            TenantId = t.Column<Guid>(nullable: false), VesselId = t.Column<Guid>(nullable: false), Kind = t.Column<string>(maxLength: 16, nullable: false),
            Id = t.Column<Guid>(nullable: false), Revision = t.Column<long>(nullable: false), AssetId = t.Column<Guid>(nullable: true), AssetRevision = t.Column<long>(nullable: true),
            ProfileVersion = t.Column<long>(nullable: true), ConditionKind = t.Column<string>(maxLength: 16, nullable: false),
            ConditionId = t.Column<Guid>(nullable: true), ConditionRevision = t.Column<long>(nullable: true), ConditionVersion = t.Column<long>(nullable: true)
        }, constraints: t => {
            t.PrimaryKey("PK_CapabilityBindings", x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision });
            t.ForeignKey("FK_CapabilityBindings_Evidence", x => new { x.TenantId, x.VesselId, x.Kind, x.Id, x.Revision }, "ControlEvidenceRevisions", new[] { "TenantId", "VesselId", "Kind", "Id", "Revision" }, principalSchema: "core", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_CapabilityBindings_Asset", x => new { x.TenantId, x.VesselId, x.AssetId, x.AssetRevision }, "AssetRevisions", new[] { "TenantId", "VesselId", "AssetId", "Revision" }, principalSchema: "technical", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_CapabilityBindings_Profile", x => new { x.TenantId, x.VesselId }, "TugProfiles", new[] { "TenantId", "VesselId" }, principalSchema: "core", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_CapabilityBindings_Condition", x => new { x.TenantId, x.VesselId, x.ConditionKind, x.ConditionId, x.ConditionRevision }, "ControlEvidenceRevisions", new[] { "TenantId", "VesselId", "Kind", "Id", "Revision" }, principalSchema: "core", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_TugCapabilityBinding", "\"Kind\" = 'Evidence' AND \"ConditionKind\" = 'Evidence' AND ((\"AssetId\" IS NULL AND \"AssetRevision\" IS NULL) OR (\"AssetId\" IS NOT NULL AND \"AssetRevision\" IS NOT NULL AND \"AssetRevision\" > 0)) AND ((\"ConditionId\" IS NULL AND \"ConditionRevision\" IS NULL AND \"ConditionVersion\" IS NULL) OR (\"ConditionId\" IS NOT NULL AND \"ConditionRevision\" IS NOT NULL AND \"ConditionVersion\" IS NOT NULL AND \"ConditionRevision\" > 0 AND \"ConditionVersion\" > 0 AND \"ConditionId\" <> \"Id\"))");
        });
        migrationBuilder.CreateIndex("IX_CapabilityBindings_Asset", "CapabilityBindings", new[] { "TenantId", "VesselId", "AssetId", "AssetRevision" }, "tug");
        migrationBuilder.CreateIndex("IX_CapabilityBindings_Condition", "CapabilityBindings", new[] { "TenantId", "VesselId", "ConditionKind", "ConditionId", "ConditionRevision" }, "tug");
        migrationBuilder.Sql("""
            CREATE TRIGGER "CapabilityBindings_Immutable" BEFORE UPDATE OR DELETE ON tug."CapabilityBindings"
            FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
            CREATE TRIGGER "CapabilityBindings_NoTruncate" BEFORE TRUNCATE ON tug."CapabilityBindings"
            FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("CapabilityBindings", "tug");
}
