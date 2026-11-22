using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // RATE-LIMIT-01: shared technical rate-limit counters (no tenant, actor, subject, IP or token; keyed HMAC partitions only).
    // Written exclusively by PostgresRateLimitStore (single-statement upsert, bounded expiry delete) outside the audited
    // business SaveChanges path; PoseidonDbContext refuses tracked counter changes.
    /// <inheritdoc />
    public partial class RateLimitCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ratelimit");

            migrationBuilder.CreateTable(
                name: "RateLimitCounters",
                schema: "ratelimit",
                columns: table => new
                {
                    Policy = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PartitionHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    WindowIndex = table.Column<long>(type: "bigint", nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimitCounters", x => new { x.Policy, x.PartitionHash });
                    table.CheckConstraint("CK_RateLimitCounters_Count", "\"Count\" >= 1 AND \"WindowIndex\" >= 0");
                    table.CheckConstraint("CK_RateLimitCounters_Hash", "\"PartitionHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_RateLimitCounters_Policy", "\"Policy\" ~ '^[a-z0-9][a-z0-9.-]{0,47}$'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RateLimitCounters_ExpiresAt",
                schema: "ratelimit",
                table: "RateLimitCounters",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateLimitCounters",
                schema: "ratelimit");
        }
    }
}
