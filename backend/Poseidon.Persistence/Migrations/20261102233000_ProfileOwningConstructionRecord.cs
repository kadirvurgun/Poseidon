using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Poseidon.Persistence.Migrations;

// Hand-authored; generated parity and application belong to the single full gate.
[DbContext(typeof(PoseidonDbContext))]
[Migration("20261102233000_ProfileOwningConstructionRecord")]
public sealed class ProfileOwningConstructionRecord : Migration
{
    protected override void Up(MigrationBuilder b)
    {
        b.AddColumn<string>("ProfileOutcomeJson", "CommandReceipts", "text", schema: "core", nullable: true);
        b.AddColumn<Guid>("ProfileVesselId", "CommandReceipts", "uuid", schema: "core", nullable: true);
        b.AddCheckConstraint("CK_CommandReceipts_ProfileOutcome", "CommandReceipts", RegulatoryDraftStorage.ReceiptConstraint, "core");
        b.CreateIndex("IX_CommandReceipts_ProfileOutcome", "CommandReceipts", new[] { "TenantId", "ProfileVesselId", "RegisterRevision" }, "core");
        b.AddForeignKey("FK_CommandReceipts_ProfileOutcome", "CommandReceipts", new[] { "TenantId", "ProfileVesselId", "RegisterRevision" },
            "RegulatoryDraftRevisions", principalColumns: new[] { "TenantId", "VesselId", "Revision" }, schema: "core", principalSchema: "core", onDelete: ReferentialAction.Restrict);
        b.AddUniqueConstraint("AK_StoredFiles_TenantId_Id_VesselId_Revision_Checksum", "StoredFiles",
            new[] { "TenantId", "Id", "VesselId", "Revision", "Checksum" }, "files");
        foreach (var table in new[] { "RegulatoryDrafts", "RegulatoryDraftRevisions" }) {
            b.AddColumn<Guid>("ConstructionFileId", table, "uuid", schema: "core", nullable: true);
            b.AddColumn<long>("ConstructionFileRevision", table, "bigint", schema: "core", nullable: true);
            b.AddColumn<string>("ConstructionFileChecksum", table, "character varying(64)", schema: "core", maxLength: 64, nullable: true);
            b.AddCheckConstraint("CK_" + table + "_ConstructionRecord", table,
                RegulatoryDraftStorage.ConstructionConstraint, "core");
            b.CreateIndex("IX_" + table + "_ConstructionRecord", table,
                new[] { "TenantId", "ConstructionFileId", "VesselId", "ConstructionFileRevision", "ConstructionFileChecksum" }, "core");
            b.AddForeignKey("FK_" + table + "_ConstructionRecord", table,
                new[] { "TenantId", "ConstructionFileId", "VesselId", "ConstructionFileRevision", "ConstructionFileChecksum" },
                "StoredFiles", principalColumns: new[] { "TenantId", "Id", "VesselId", "Revision", "Checksum" }, schema: "core", principalSchema: "files", onDelete: ReferentialAction.Restrict);
        }
        // Existing rows remain null; no historical record, verification or source is invented.
    }
    protected override void Down(MigrationBuilder b) => throw new NotSupportedException("Retained source/document lineage requires a forward migration or controlled restore.");
}
