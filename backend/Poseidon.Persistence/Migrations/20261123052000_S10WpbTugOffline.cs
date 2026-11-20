using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S10WpbTugOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationOfflineSnapshots",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorityDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationOfflineSnapshots", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_TugOperationOfflineSnapshot_Scope", x => new { x.TenantId, x.Id, x.ActorId, x.VesselId, x.JobId });
                    table.CheckConstraint("CK_TugOperationOfflineSnapshot_Lease", "\"ExpiresAt\" > \"IssuedAt\"");
                    table.ForeignKey(
                        name: "FK_TugOperationOfflineSnapshot_Vessel",
                        columns: x => new { x.TenantId, x.VesselId },
                        principalSchema: "core",
                        principalTable: "Vessels",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationOfflineOutcomes",
                schema: "tug",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    VesselId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: false),
                    ConflictFields = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProtectedContext = table.Column<string>(type: "text", nullable: false),
                    OutcomeJson = table.Column<string>(type: "text", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationOfflineOutcomes", x => new { x.TenantId, x.ActorId, x.CommandId });
                    table.UniqueConstraint("AK_TugOperationOfflineOutcome_Snapshot", x => new { x.TenantId, x.ActorId, x.CommandId, x.SnapshotId });
                    table.CheckConstraint("CK_TugOperationOfflineOutcome_State", "\"Result\" IN ('Committed','MaterialConflict','ServerRetained') AND \"Operation\" IN ('tug.briefing.ack.v1','tug.execution.event.v1','tug.daily.entry.v1') AND (\"Result\"<>'ServerRetained' OR \"ParentCommandId\" IS NOT NULL) AND (\"Result\"<>'MaterialConflict' OR length(\"ConflictFields\")>0)");
                    table.ForeignKey(
                        name: "FK_TugOperationOfflineOutcome_Parent",
                        columns: x => new { x.TenantId, x.ActorId, x.ParentCommandId, x.SnapshotId },
                        principalSchema: "tug",
                        principalTable: "OperationOfflineOutcomes",
                        principalColumns: new[] { "TenantId", "ActorId", "CommandId", "SnapshotId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TugOperationOfflineOutcome_Snapshot",
                        columns: x => new { x.TenantId, x.SnapshotId, x.ActorId, x.VesselId, x.JobId },
                        principalSchema: "tug",
                        principalTable: "OperationOfflineSnapshots",
                        principalColumns: new[] { "TenantId", "Id", "ActorId", "VesselId", "JobId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationOfflineOutcomes_TenantId_ActorId_ParentCommandId_S~",
                schema: "tug",
                table: "OperationOfflineOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId", "SnapshotId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationOfflineOutcomes_TenantId_SnapshotId_ActorId_Vessel~",
                schema: "tug",
                table: "OperationOfflineOutcomes",
                columns: new[] { "TenantId", "SnapshotId", "ActorId", "VesselId", "JobId" });

            migrationBuilder.CreateIndex(
                name: "IX_TugOperationOfflineOutcome_Conflicts",
                schema: "tug",
                table: "OperationOfflineOutcomes",
                columns: new[] { "TenantId", "ActorId", "VesselId", "Result", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TugOperationOfflineOutcome_Parent",
                schema: "tug",
                table: "OperationOfflineOutcomes",
                columns: new[] { "TenantId", "ActorId", "ParentCommandId" },
                unique: true,
                filter: "\"ParentCommandId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperationOfflineSnapshots_TenantId_VesselId",
                schema: "tug",
                table: "OperationOfflineSnapshots",
                columns: new[] { "TenantId", "VesselId" });

            migrationBuilder.CreateIndex(
                name: "IX_TugOperationOfflineSnapshot_Owner",
                schema: "tug",
                table: "OperationOfflineSnapshots",
                columns: new[] { "TenantId", "ActorId", "VesselId", "JobId", "IssuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationOfflineOutcomes",
                schema: "tug");

            migrationBuilder.DropTable(
                name: "OperationOfflineSnapshots",
                schema: "tug");
        }
    }
}
