using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S5WpcEngineLogReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyEngineReferenceHeads",
                schema: "operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyEngineReferenceHeads", x => new { x.TenantId, x.RecordId });
                    table.UniqueConstraint("AK_DailyEngineReferenceHeads_TenantId_VesselId_Kind_RecordId", x => new { x.TenantId, x.VesselId, x.Kind, x.RecordId });
                    table.CheckConstraint("CK_DailyEngineReferenceHead_State", "\"Sequence\" > 0 AND \"Kind\" = 'Engine'");
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceHead_Record",
                        columns: x => new { x.TenantId, x.VesselId, x.Kind, x.RecordId },
                        principalSchema: "operations",
                        principalTable: "DailyOperations",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyEngineReferenceEvents",
                schema: "operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    RecordRevision = table.Column<long>(type: "bigint", nullable: false),
                    RecordVersion = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkOrderRevision = table.Column<long>(type: "bigint", nullable: true),
                    DefectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefectRevision = table.Column<long>(type: "bigint", nullable: true),
                    TargetVersion = table.Column<long>(type: "bigint", nullable: false),
                    TargetHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetSourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(192)", maxLength: 192, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyEngineReferenceEvents", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_DailyEngineReferenceEvents_TenantId_VesselId_Kind_RecordId_~", x => new { x.TenantId, x.VesselId, x.Kind, x.RecordId, x.Id });
                    table.CheckConstraint("CK_DailyEngineReferenceEvent_State", "\"Sequence\" > 0 AND \"RecordRevision\" > 0 AND \"RecordVersion\" > 0 AND \"TargetVersion\" > 0 AND \"Kind\" = 'Engine' AND \"ActorId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Operation\" IN ('LINK','UNLINK') AND ((\"TargetKind\" = 'WorkOrder' AND \"WorkOrderId\" IS NOT NULL AND \"WorkOrderRevision\" > 0 AND \"DefectId\" IS NULL AND \"DefectRevision\" IS NULL) OR (\"TargetKind\" = 'Defect' AND \"DefectId\" IS NOT NULL AND \"DefectRevision\" > 0 AND \"WorkOrderId\" IS NULL AND \"WorkOrderRevision\" IS NULL))");
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_Defect",
                        columns: x => new { x.TenantId, x.VesselId, x.DefectId },
                        principalSchema: "technical",
                        principalTable: "Defects",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_DefectRevision",
                        columns: x => new { x.TenantId, x.DefectId, x.DefectRevision },
                        principalSchema: "technical",
                        principalTable: "DefectRevisions",
                        principalColumns: new[] { "TenantId", "DefectId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_Head",
                        columns: x => new { x.TenantId, x.VesselId, x.Kind, x.RecordId },
                        principalSchema: "operations",
                        principalTable: "DailyEngineReferenceHeads",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "RecordId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_Revision",
                        columns: x => new { x.TenantId, x.RecordId, x.RecordRevision },
                        principalSchema: "operations",
                        principalTable: "DailyOperationRevisions",
                        principalColumns: new[] { "TenantId", "RecordId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_WorkOrder",
                        columns: x => new { x.TenantId, x.VesselId, x.WorkOrderId },
                        principalSchema: "technical",
                        principalTable: "WorkOrders",
                        principalColumns: new[] { "TenantId", "VesselId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceEvent_WorkOrderRevision",
                        columns: x => new { x.TenantId, x.WorkOrderId, x.WorkOrderRevision },
                        principalSchema: "technical",
                        principalTable: "WorkOrderRevisions",
                        principalColumns: new[] { "TenantId", "WorkOrderId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyEngineReferenceReceipts",
                schema: "operations",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyEngineReferenceReceipts", x => new { x.TenantId, x.ActorId, x.Key });
                    table.ForeignKey(
                        name: "FK_DailyEngineReferenceReceipt_Event",
                        columns: x => new { x.TenantId, x.VesselId, x.Kind, x.RecordId, x.EventId },
                        principalSchema: "operations",
                        principalTable: "DailyEngineReferenceEvents",
                        principalColumns: new[] { "TenantId", "VesselId", "Kind", "RecordId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_DefectId_DefectRevision",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "DefectId", "DefectRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_RecordId_RecordRevision",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "RecordId", "RecordRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_RecordId_Sequence",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "RecordId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_VesselId_DefectId",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "VesselId", "DefectId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_VesselId_WorkOrderId",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "VesselId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceEvents_TenantId_WorkOrderId_WorkOrderRe~",
                schema: "operations",
                table: "DailyEngineReferenceEvents",
                columns: new[] { "TenantId", "WorkOrderId", "WorkOrderRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyEngineReferenceReceipts_TenantId_VesselId_Kind_RecordI~",
                schema: "operations",
                table: "DailyEngineReferenceReceipts",
                columns: new[] { "TenantId", "VesselId", "Kind", "RecordId", "EventId" });

            // Database-level append-only guard, matching the daily review pattern.
            migrationBuilder.Sql("""
                CREATE FUNCTION operations.guard_daily_engine_reference() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP <> 'UPDATE' THEN RAISE EXCEPTION 'Engine log reference history cannot be removed'; END IF;
                  IF (to_jsonb(OLD) - 'Sequence') IS DISTINCT FROM (to_jsonb(NEW) - 'Sequence') OR NEW."Sequence" <> OLD."Sequence"+1
                    THEN RAISE EXCEPTION 'Immutable engine log reference ownership'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "DailyEngineReferenceHead_Guard" BEFORE UPDATE OR DELETE ON operations."DailyEngineReferenceHeads" FOR EACH ROW EXECUTE FUNCTION operations.guard_daily_engine_reference();
                CREATE TRIGGER "DailyEngineReferenceHead_NoTruncate" BEFORE TRUNCATE ON operations."DailyEngineReferenceHeads" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "DailyEngineReferenceEvent_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyEngineReferenceEvents" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "DailyEngineReferenceEvent_NoTruncate" BEFORE TRUNCATE ON operations."DailyEngineReferenceEvents" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "DailyEngineReferenceReceipt_Immutable" BEFORE UPDATE OR DELETE ON operations."DailyEngineReferenceReceipts" FOR EACH ROW EXECUTE FUNCTION audit.reject_mutation();
                CREATE TRIGGER "DailyEngineReferenceReceipt_NoTruncate" BEFORE TRUNCATE ON operations."DailyEngineReferenceReceipts" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS (SELECT 1 FROM operations."DailyEngineReferenceEvents") THEN RAISE EXCEPTION 'Recorded engine log reference history must be retained'; END IF; END $$;
                """);
            migrationBuilder.DropTable(
                name: "DailyEngineReferenceReceipts",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "DailyEngineReferenceEvents",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "DailyEngineReferenceHeads",
                schema: "operations");

            migrationBuilder.Sql("DROP FUNCTION operations.guard_daily_engine_reference();");
        }
    }
}
