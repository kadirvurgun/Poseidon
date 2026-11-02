using Microsoft.EntityFrameworkCore.Migrations;
namespace Poseidon.Persistence.Migrations;
public partial class MaintenanceObservations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateTable(name: "Observations", schema: "technical", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false),
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            VesselId = table.Column<Guid>(type: "uuid", nullable: false),
            AssetId = table.Column<Guid>(type: "uuid", nullable: false),
            Source = table.Column<string>(type: "character varying(80)", nullable: false, maxLength: 80),
            PolicyVersion = table.Column<string>(type: "character varying(80)", nullable: false, maxLength: 80),
            SourceRevision = table.Column<long>(type: "bigint", nullable: false),
            Kind = table.Column<string>(type: "character varying(16)", nullable: false, maxLength: 16),
            Unit = table.Column<string>(type: "character varying(40)", nullable: false, maxLength: 40),
            Value = table.Column<string>(type: "character varying(1024)", nullable: true, maxLength: 1024),
            ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            Provenance = table.Column<string>(type: "character varying(40)", nullable: false, maxLength: 40),
        }, constraints: table => {
            table.PrimaryKey("PK_Observations", x => new { x.TenantId, x.Id });
            table.UniqueConstraint("AK_Observation_Scope", x => new { x.TenantId, x.VesselId, x.AssetId, x.Id });
            table.ForeignKey("FK_Observations_Assets_TenantId_VesselId_AssetId", x => new { x.TenantId, x.VesselId, x.AssetId }, principalSchema: "technical", principalTable: "Assets", principalColumns: new[] { "TenantId", "VesselId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_Observations_SourceRevision", "Observations", new[] { "TenantId", "VesselId", "AssetId", "Source", "SourceRevision" }, "technical", unique: true);
        migrationBuilder.CreateTable(name: "ObservationRevocations", schema: "technical", columns: table => new {
            TenantId = table.Column<Guid>(type: "uuid", nullable: false), ObservationId = table.Column<Guid>(type: "uuid", nullable: false),
            RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => {
            table.PrimaryKey("PK_ObservationRevocations", x => new { x.TenantId, x.ObservationId });
            table.ForeignKey("FK_ObservationRevocations_Observations_TenantId_ObservationId", x => new { x.TenantId, x.ObservationId }, principalSchema: "technical", principalTable: "Observations", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.AddColumn<Guid>("ObservationId", "Plans", schema: "technical", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>("ObservationId", "PlanRevisions", schema: "technical", type: "uuid", nullable: true);
        migrationBuilder.CreateIndex("IX_Plan_Observation", "Plans", new[] { "TenantId", "VesselId", "AssetId", "ObservationId" }, "technical");
        migrationBuilder.CreateIndex("IX_PlanRevision_Observation", "PlanRevisions", new[] { "TenantId", "VesselId", "AssetId", "ObservationId" }, "technical");
        migrationBuilder.AddForeignKey("FK_Plans_ObservationScope", "Plans", new[] { "TenantId", "VesselId", "AssetId", "ObservationId" }, "Observations", principalColumns: new[] { "TenantId", "VesselId", "AssetId", "Id" }, schema: "technical", principalSchema: "technical", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_PlanRevisions_ObservationScope", "PlanRevisions", new[] { "TenantId", "VesselId", "AssetId", "ObservationId" }, "Observations", principalColumns: new[] { "TenantId", "VesselId", "AssetId", "Id" }, schema: "technical", principalSchema: "technical", onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("""
            CREATE FUNCTION technical.reject_observation_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'immutable_observation'; END $$;
            CREATE TRIGGER observation_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."Observations" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_observation_mutation();
            CREATE TRIGGER observation_revocation_immutable BEFORE UPDATE OR DELETE OR TRUNCATE ON technical."ObservationRevocations" FOR EACH STATEMENT EXECUTE FUNCTION technical.reject_observation_mutation();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropForeignKey("FK_Plans_ObservationScope", "Plans", "technical");
        migrationBuilder.DropForeignKey("FK_PlanRevisions_ObservationScope", "PlanRevisions", "technical");
        migrationBuilder.DropIndex("IX_Plan_Observation", "Plans", "technical"); migrationBuilder.DropIndex("IX_PlanRevision_Observation", "PlanRevisions", "technical");
        migrationBuilder.DropColumn("ObservationId", "Plans", "technical"); migrationBuilder.DropColumn("ObservationId", "PlanRevisions", "technical");
        migrationBuilder.DropTable("ObservationRevocations", "technical"); migrationBuilder.DropTable("Observations", "technical");
        migrationBuilder.Sql("DROP FUNCTION technical.reject_observation_mutation();");
    }
}
