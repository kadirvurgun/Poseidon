using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditResourceKindCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ResourceKind",
                schema: "audit",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
            // Older authoring DDL created an unused physical Version column while
            // EF uses PostgreSQL xmin. Keep any historical bytes; allow ordinary
            // inserts to use the real system concurrency token.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='files' AND table_name='Documents' AND column_name='Version') THEN
                    ALTER TABLE files."Documents" ALTER COLUMN "Version" DROP NOT NULL;
                  END IF;
                END $$;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Use a forward migration or controlled restore; audit resource kinds must not be truncated.");
    }
}
