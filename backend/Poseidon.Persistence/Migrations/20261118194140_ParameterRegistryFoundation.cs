using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    // PLAN-1511-5401 / 54.01 CFG3 value lineage. EF-generated tables plus database guards that keep
    // revisions, decisions and receipts append-only, root identity immutable and maker != checker.
    [DbContext(typeof(PoseidonDbContext))]
    [Migration("20261118194140_ParameterRegistryFoundation")]
    public partial class ParameterRegistryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "config");

            migrationBuilder.CreateTable(
                name: "ParameterRoots",
                schema: "config",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectorJson = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SelectorHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PendingRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterRoots", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ParameterRoots_Revision", "\"Revision\" > 0 AND \"SelectorHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_ParameterRoots_Scope", "\"ScopeKind\" IN ('TENANT','COMPANY','VESSEL') AND (\"ScopeKind\" <> 'TENANT' OR \"ScopeId\" = \"TenantId\") AND \"ScopeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_ParameterRoots_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "core",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterCommandReceipts",
                schema: "config",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RootId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRevision = table.Column<long>(type: "bigint", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OutcomeJson = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterCommandReceipts", x => new { x.TenantId, x.Key });
                    table.CheckConstraint("CK_ParameterCommandReceipts_Action", "\"Action\" IN ('ParameterPropose','ParameterApprove','ParameterReject') AND \"RootRevision\" > 0 AND \"Fingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_ParameterCommandReceipts_ParameterRoots_TenantId_RootId",
                        columns: x => new { x.TenantId, x.RootId },
                        principalSchema: "config",
                        principalTable: "ParameterRoots",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterRevisions",
                schema: "config",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RootId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DefinitionVersion = table.Column<int>(type: "integer", nullable: false),
                    ValueMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ValueJson = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthoredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterRevisions", x => new { x.TenantId, x.Id });
                    table.UniqueConstraint("AK_ParameterRevisions_Root", x => new { x.TenantId, x.RootId, x.Id });
                    table.CheckConstraint("CK_ParameterRevisions_Lineage", "\"Sequence\" > 0 AND \"DefinitionVersion\" > 0 AND char_length(\"Reason\") BETWEEN 10 AND 2000 AND \"ContentHash\" ~ '^[0-9a-f]{64}$' AND ((\"Provenance\" = 'DeclaredUnverified' AND \"AuthorId\" <> '00000000-0000-0000-0000-000000000000'::uuid) OR (\"Provenance\" = 'SyntheticUnverified' AND \"AuthorId\" = '00000000-0000-0000-0000-000000000000'::uuid))");
                    table.CheckConstraint("CK_ParameterRevisions_Value", "\"ValueMode\" IN ('VALUE','INHERIT') AND ((\"ValueMode\" = 'INHERIT') = (\"ValueJson\" IS NULL))");
                    table.CheckConstraint("CK_ParameterRevisions_Window", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.ForeignKey(
                        name: "FK_ParameterRevisions_ParameterRoots_TenantId_RootId",
                        columns: x => new { x.TenantId, x.RootId },
                        principalSchema: "config",
                        principalTable: "ParameterRoots",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParameterDecisions",
                schema: "config",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RootId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeciderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SelectedFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterDecisions", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_ParameterDecisions_Outcome", "\"Outcome\" IN ('APPROVED','REJECTED') AND ((\"Outcome\" = 'APPROVED') = (\"SelectedFrom\" IS NOT NULL)) AND \"Sequence\" > 0 AND char_length(\"Reason\") BETWEEN 10 AND 2000 AND \"DeciderId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_ParameterDecisions_Revision",
                        columns: x => new { x.TenantId, x.RootId, x.RevisionId },
                        principalSchema: "config",
                        principalTable: "ParameterRevisions",
                        principalColumns: new[] { "TenantId", "RootId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterCommandReceipts_Root",
                schema: "config",
                table: "ParameterCommandReceipts",
                columns: new[] { "TenantId", "RootId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDecisions_Revision",
                schema: "config",
                table: "ParameterDecisions",
                columns: new[] { "TenantId", "RevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDecisions_RootRevision",
                schema: "config",
                table: "ParameterDecisions",
                columns: new[] { "TenantId", "RootId", "RevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDecisions_Sequence",
                schema: "config",
                table: "ParameterDecisions",
                columns: new[] { "TenantId", "RootId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterRevisions_Sequence",
                schema: "config",
                table: "ParameterRevisions",
                columns: new[] { "TenantId", "RootId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterRoots_Identity",
                schema: "config",
                table: "ParameterRoots",
                columns: new[] { "TenantId", "Key", "ScopeKind", "ScopeId", "SelectorHash" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION config.guard_parameter_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'Parameter lineage is immutable' USING ERRCODE='55000';
                END $$;
                CREATE FUNCTION config.guard_parameter_revision() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Parameter lineage is immutable' USING ERRCODE='55000'; END IF;
                  IF NOT EXISTS (SELECT 1 FROM config."ParameterRoots" r WHERE r."TenantId"=NEW."TenantId" AND r."Id"=NEW."RootId" AND r."Key"=NEW."Key")
                  THEN RAISE EXCEPTION 'Parameter revision root mismatch' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE FUNCTION config.guard_parameter_decision() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE revision record;
                BEGIN
                  IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Parameter lineage is immutable' USING ERRCODE='55000'; END IF;
                  SELECT v."AuthorId", v."Provenance", v."EffectiveFrom", v."EffectiveTo" INTO revision FROM config."ParameterRevisions" v
                    WHERE v."TenantId"=NEW."TenantId" AND v."RootId"=NEW."RootId" AND v."Id"=NEW."RevisionId";
                  IF NOT FOUND OR revision."AuthorId" = NEW."DeciderId"
                  THEN RAISE EXCEPTION 'Parameter decision requires an independent decider' USING ERRCODE='23514'; END IF;
                  IF NEW."Outcome" = 'APPROVED' AND (revision."Provenance" <> 'DeclaredUnverified' OR NEW."SelectedFrom" < revision."EffectiveFrom"
                     OR NEW."SelectedFrom" < NEW."DecidedAt" OR (revision."EffectiveTo" IS NOT NULL AND NEW."SelectedFrom" >= revision."EffectiveTo"))
                  THEN RAISE EXCEPTION 'Invalid parameter approval' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE FUNCTION config.guard_parameter_root() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP IN ('DELETE','TRUNCATE') THEN RAISE EXCEPTION 'Parameter roots cannot be removed' USING ERRCODE='55000'; END IF;
                  IF (TG_OP = 'INSERT' AND NEW."Revision" <> 1) OR (TG_OP = 'UPDATE' AND (NEW."TenantId" <> OLD."TenantId" OR NEW."Id" <> OLD."Id"
                     OR NEW."Key" <> OLD."Key" OR NEW."ScopeKind" <> OLD."ScopeKind" OR NEW."ScopeId" <> OLD."ScopeId" OR NEW."SelectorJson" <> OLD."SelectorJson"
                     OR NEW."SelectorHash" <> OLD."SelectorHash" OR NEW."CreatedAt" <> OLD."CreatedAt" OR NEW."Revision" <> OLD."Revision" + 1))
                  THEN RAISE EXCEPTION 'Invalid parameter root change' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "GuardParameterRevision" BEFORE INSERT OR UPDATE OR DELETE ON config."ParameterRevisions" FOR EACH ROW EXECUTE FUNCTION config.guard_parameter_revision();
                CREATE TRIGGER "GuardParameterRevisionTruncate" BEFORE TRUNCATE ON config."ParameterRevisions" FOR EACH STATEMENT EXECUTE FUNCTION config.guard_parameter_append_only();
                CREATE TRIGGER "GuardParameterDecision" BEFORE INSERT OR UPDATE OR DELETE ON config."ParameterDecisions" FOR EACH ROW EXECUTE FUNCTION config.guard_parameter_decision();
                CREATE TRIGGER "GuardParameterDecisionTruncate" BEFORE TRUNCATE ON config."ParameterDecisions" FOR EACH STATEMENT EXECUTE FUNCTION config.guard_parameter_append_only();
                CREATE TRIGGER "GuardParameterReceipt" BEFORE UPDATE OR DELETE ON config."ParameterCommandReceipts" FOR EACH ROW EXECUTE FUNCTION config.guard_parameter_append_only();
                CREATE TRIGGER "GuardParameterReceiptTruncate" BEFORE TRUNCATE ON config."ParameterCommandReceipts" FOR EACH STATEMENT EXECUTE FUNCTION config.guard_parameter_append_only();
                CREATE TRIGGER "GuardParameterRoot" BEFORE INSERT OR UPDATE OR DELETE ON config."ParameterRoots" FOR EACH ROW EXECUTE FUNCTION config.guard_parameter_root();
                CREATE TRIGGER "GuardParameterRootTruncate" BEFORE TRUNCATE ON config."ParameterRoots" FOR EACH STATEMENT EXECUTE FUNCTION config.guard_parameter_root();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "GuardParameterRevision" ON config."ParameterRevisions";
                DROP TRIGGER "GuardParameterRevisionTruncate" ON config."ParameterRevisions";
                DROP TRIGGER "GuardParameterDecision" ON config."ParameterDecisions";
                DROP TRIGGER "GuardParameterDecisionTruncate" ON config."ParameterDecisions";
                DROP TRIGGER "GuardParameterReceipt" ON config."ParameterCommandReceipts";
                DROP TRIGGER "GuardParameterReceiptTruncate" ON config."ParameterCommandReceipts";
                DROP TRIGGER "GuardParameterRoot" ON config."ParameterRoots";
                DROP TRIGGER "GuardParameterRootTruncate" ON config."ParameterRoots";
                """);
            migrationBuilder.DropTable(
                name: "ParameterCommandReceipts",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ParameterDecisions",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ParameterRevisions",
                schema: "config");

            migrationBuilder.DropTable(
                name: "ParameterRoots",
                schema: "config");

            migrationBuilder.Sql("""
                DROP FUNCTION config.guard_parameter_root();
                DROP FUNCTION config.guard_parameter_decision();
                DROP FUNCTION config.guard_parameter_revision();
                DROP FUNCTION config.guard_parameter_append_only();
                DROP SCHEMA config;
                """);
        }
    }
}
