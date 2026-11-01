using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegisterCommittedReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RegisterRevision",
                schema: "core",
                table: "CommandReceipts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RegisterVersion",
                schema: "core",
                table: "CommandReceipts",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegisterRevision",
                schema: "core",
                table: "CommandReceipts");

            migrationBuilder.DropColumn(
                name: "RegisterVersion",
                schema: "core",
                table: "CommandReceipts");
        }
    }
}
