using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Poseidon.Persistence.Migrations;

// Hand-authored with model/designer; generation/parity verification DEFERRED to AUDIT-FILES-BUNDLE-01.
public partial class AuditHistoryMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "VesselId", schema: "audit", table: "AuditEvents", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "ResourceId", schema: "audit", table: "AuditEvents", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CapabilityContext", schema: "audit", table: "AuditEvents", type: "character varying(512)", nullable: true, maxLength: 512);
        migrationBuilder.AddColumn<string>(name: "ChangeSummary", schema: "audit", table: "AuditEvents", type: "character varying(2048)", nullable: true, maxLength: 2048);
        migrationBuilder.AddColumn<long>(name: "BeforeRevision", schema: "audit", table: "AuditEvents", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "AfterRevision", schema: "audit", table: "AuditEvents", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ResourceKind", schema: "audit", table: "AuditEvents", type: "character varying(16)", nullable: true, maxLength: 16);
        migrationBuilder.AddColumn<string>(name: "ReasonStatus", schema: "audit", table: "AuditEvents", type: "character varying(32)", nullable: true, maxLength: 32);
        migrationBuilder.AddColumn<string>(name: "Source", schema: "audit", table: "AuditEvents", type: "character varying(32)", nullable: true, maxLength: 32);
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_TenantId_VesselId_OccurredAt_Id", schema: "audit", table: "AuditEvents", columns: new[] { "TenantId", "VesselId", "OccurredAt", "Id" });
        migrationBuilder.AddColumn<long>(name: "BeforeVersion", schema: "audit", table: "AuditEvents", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<long>(name: "AfterVersion", schema: "audit", table: "AuditEvents", type: "bigint", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AuditEvents_TenantId_ResourceId_CorrelationId", schema: "audit", table: "AuditEvents", columns: new[] { "TenantId", "ResourceId", "CorrelationId" });
        // No UPDATE/backfill: historic actor, scope, reason and values cannot be inferred.
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AuditEvents_TenantId_ResourceId_CorrelationId", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropIndex(name: "IX_AuditEvents_TenantId_VesselId_OccurredAt_Id", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "BeforeVersion", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "AfterVersion", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "VesselId", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "ResourceId", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "CapabilityContext", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "ChangeSummary", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "BeforeRevision", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "AfterRevision", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "ResourceKind", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "ReasonStatus", schema: "audit", table: "AuditEvents");
        migrationBuilder.DropColumn(name: "Source", schema: "audit", table: "AuditEvents");
    }
}
