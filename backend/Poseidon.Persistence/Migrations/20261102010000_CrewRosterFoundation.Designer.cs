// Authored model metadata; generation/parity verification DEFERRED.
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Poseidon.Persistence;

#nullable disable

namespace Poseidon.Persistence.Migrations
{
    [DbContext(typeof(PoseidonDbContext))]
    [Migration("20261102010000_CrewRosterFoundation")]
    partial class CrewRosterFoundation
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("core")
                .HasAnnotation("ProductVersion", "10.0.14")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Poseidon.Persistence.AuditEvent", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("ActorId")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("ActorType")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("TenantId", "OccurredAt");

                    b.Property<long?>("BeforeVersion").HasColumnType("bigint");
                    b.Property<long?>("AfterVersion").HasColumnType("bigint");
                    b.Property<Guid?>("VesselId").HasColumnType("uuid");
                    b.Property<Guid?>("ResourceId").HasColumnType("uuid");
                    b.Property<string>("RoleContext").HasMaxLength(600).HasColumnType("character varying(600)");

                    b.Property<string>("CapabilityContext").HasMaxLength(512).HasColumnType("character varying(512)");
                    b.Property<string>("ChangeSummary").HasMaxLength(2048).HasColumnType("character varying(2048)");
                    b.Property<long?>("BeforeRevision").HasColumnType("bigint");
                    b.Property<long?>("AfterRevision").HasColumnType("bigint");
                    b.Property<string>("ResourceKind").HasMaxLength(16).HasColumnType("character varying(16)");
                    b.Property<string>("ReasonStatus").HasMaxLength(32).HasColumnType("character varying(32)");
                    b.Property<string>("Source").HasMaxLength(32).HasColumnType("character varying(32)");
                    b.HasIndex("TenantId", "ResourceId", "CorrelationId");
                    b.HasIndex("TenantId", "VesselId", "OccurredAt", "Id");

                    b.ToTable("AuditEvents", "audit");
                });

            modelBuilder.Entity("Poseidon.Persistence.CommandReceipt", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("ActorId")
                        .HasColumnType("uuid");

                    b.Property<string>("Key")
                        .HasMaxLength(80)
                        .HasColumnType("character varying(80)");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<DateTimeOffset>("CommittedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Fingerprint")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<long?>("RegisterRevision")
                        .HasColumnType("bigint");

                    b.Property<long?>("RegisterVersion")
                        .HasColumnType("bigint");

                    b.Property<Guid>("ResourceId")
                        .HasColumnType("uuid");

                    b.HasKey("TenantId", "ActorId", "Key");

                    b.ToTable("CommandReceipts", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.Company", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "Id");

                    b.HasIndex("TenantId", "Code")
                        .IsUnique();

                    b.ToTable("Companies", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.RegisterDraft", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<string>("Kind")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<string>("ContentJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<long>("Revision")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("SavedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "Kind", "Id");

                    b.ToTable("RegisterDrafts", "core", t =>
                        {
                            t.HasCheckConstraint("CK_RegisterDrafts_Kind", "\"Kind\" IN ('Source','Requirement') AND \"Revision\" > 0 AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified')");
                        });
                });

            modelBuilder.Entity("Poseidon.Persistence.RegisterRevision", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<string>("Kind")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<long>("Revision")
                        .HasColumnType("bigint");

                    b.Property<string>("ContentJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<DateTimeOffset>("SavedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("SourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("SourceKind")
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<long?>("SourceRevision")
                        .HasColumnType("bigint");

                    b.HasKey("TenantId", "Kind", "Id", "Revision");

                    b.HasIndex("TenantId", "SourceKind", "SourceId", "SourceRevision");

                    b.ToTable("RegisterRevisions", "core", t =>
                        {
                            t.HasCheckConstraint("CK_RegisterRevisions_Lineage", "\"Revision\" > 0 AND \"Kind\" IN ('Source','Requirement') AND \"Provenance\" IN ('DeclaredUnverified','SyntheticUnverified') AND ((\"SourceKind\" IS NULL AND \"SourceId\" IS NULL AND \"SourceRevision\" IS NULL) OR (\"Kind\" = 'Requirement' AND \"SourceKind\" IS NOT NULL AND \"SourceKind\" = 'Source' AND \"SourceId\" IS NOT NULL AND \"SourceRevision\" IS NOT NULL AND \"SourceRevision\" > 0))");
                        });
                });

            modelBuilder.Entity("Poseidon.Persistence.RegulatoryDraft", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("VesselId")
                        .HasColumnType("uuid");

                    b.Property<string>("ContentJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<long>("Revision")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("SavedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "VesselId");

                    b.ToTable("RegulatoryDrafts", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.RegulatoryDraftRevision", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("VesselId")
                        .HasColumnType("uuid");

                    b.Property<long>("Revision")
                        .HasColumnType("bigint");

                    b.Property<string>("ContentJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Provenance")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<DateTimeOffset>("SavedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("TenantId", "VesselId", "Revision");

                    b.ToTable("RegulatoryDraftRevisions", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.SecurityEvent", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<string>("ActorId")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("ResourceId")
                        .HasColumnType("uuid");

                    b.Property<string>("Result")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.Property<Guid?>("TenantId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("TenantId", "OccurredAt");

                    b.ToTable("SecurityEvents", "audit");
                });

            modelBuilder.Entity("Poseidon.Persistence.Tenant", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("Tenants", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.TugProfile", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("VesselId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("BollardPullTonnes")
                        .HasPrecision(10, 2)
                        .HasColumnType("numeric(10,2)");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "VesselId");

                    b.ToTable("TugProfiles", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.Vessel", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("Id")
                        .HasColumnType("uuid");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid>("CompanyId")
                        .HasColumnType("uuid");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "Id");

                    b.HasIndex("TenantId", "Code")
                        .IsUnique();

                    b.HasIndex("TenantId", "CompanyId");

                    b.ToTable("Vessels", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.VesselProfile", b =>
                {
                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("VesselId")
                        .HasColumnType("uuid");

                    b.Property<decimal>("GrossTonnage")
                        .HasPrecision(14, 2)
                        .HasColumnType("numeric(14,2)");

                    b.Property<uint>("Version")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("TenantId", "VesselId");

                    b.ToTable("VesselProfiles", "core");
                });

            modelBuilder.Entity("Poseidon.Persistence.AuditEvent", b =>
                {
                    b.HasOne("Poseidon.Persistence.Tenant", null)
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.CommandReceipt", b =>
                {
                    b.HasOne("Poseidon.Persistence.Tenant", null)
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.Company", b =>
                {
                    b.HasOne("Poseidon.Persistence.Tenant", null)
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.RegisterDraft", b =>
                {
                    b.HasOne("Poseidon.Persistence.Tenant", null)
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.RegisterRevision", b =>
                {
                    b.HasOne("Poseidon.Persistence.RegisterDraft", null)
                        .WithMany()
                        .HasForeignKey("TenantId", "Kind", "Id")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Poseidon.Persistence.RegisterRevision", null)
                        .WithMany()
                        .HasForeignKey("TenantId", "SourceKind", "SourceId", "SourceRevision")
                        .OnDelete(DeleteBehavior.Restrict)
                        .HasConstraintName("FK_RegisterRevisions_Source");
                });

            modelBuilder.Entity("Poseidon.Persistence.RegulatoryDraft", b =>
                {
                    b.HasOne("Poseidon.Persistence.Vessel", null)
                        .WithOne()
                        .HasForeignKey("Poseidon.Persistence.RegulatoryDraft", "TenantId", "VesselId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.RegulatoryDraftRevision", b =>
                {
                    b.HasOne("Poseidon.Persistence.RegulatoryDraft", null)
                        .WithMany()
                        .HasForeignKey("TenantId", "VesselId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.SecurityEvent", b =>
                {
                    b.HasOne("Poseidon.Persistence.Tenant", null)
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("Poseidon.Persistence.TugProfile", b =>
                {
                    b.HasOne("Poseidon.Persistence.Vessel", null)
                        .WithOne()
                        .HasForeignKey("Poseidon.Persistence.TugProfile", "TenantId", "VesselId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.Vessel", b =>
                {
                    b.HasOne("Poseidon.Persistence.Company", null)
                        .WithMany()
                        .HasForeignKey("TenantId", "CompanyId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Poseidon.Persistence.VesselProfile", b =>
                {
                    b.HasOne("Poseidon.Persistence.Vessel", null)
                        .WithOne()
                        .HasForeignKey("Poseidon.Persistence.VesselProfile", "TenantId", "VesselId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
            modelBuilder.Entity("Poseidon.Persistence.Document", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<long>("Revision").HasColumnType("bigint");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnType("xid").HasColumnName("xmin");
                b.HasKey("TenantId", "Id"); b.HasAlternateKey("TenantId", "Id", "VesselId");
                b.HasIndex("TenantId", "VesselId"); b.ToTable("Documents", "files");
            });
            modelBuilder.Entity("Poseidon.Persistence.DocumentVersion", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("DocumentId").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<long>("Revision").HasColumnType("bigint");
                b.Property<string>("Title").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Status").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<string>("Provenance").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("TenantId", "DocumentId", "Revision"); b.HasAlternateKey("TenantId", "DocumentId", "Revision", "VesselId");
                b.HasIndex("TenantId", "DocumentId", "VesselId"); b.ToTable("DocumentVersions", "files");
            });
            modelBuilder.Entity("Poseidon.Persistence.Document", b => {
                b.HasOne("Poseidon.Persistence.Vessel", null).WithMany().HasForeignKey("TenantId", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
            modelBuilder.Entity("Poseidon.Persistence.DocumentVersion", b => {
                b.HasOne("Poseidon.Persistence.Document", null).WithMany().HasForeignKey("TenantId", "DocumentId", "VesselId").HasPrincipalKey("TenantId", "Id", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
            modelBuilder.Entity("Poseidon.Persistence.FileStage", b =>
            {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<Guid>("ActorId").HasColumnType("uuid");
                b.Property<string>("Key").IsRequired().HasMaxLength(80).HasColumnType("character varying(80)");
                b.Property<string>("Fingerprint").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
                b.Property<string>("ExpectedChecksum").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<long>("ExpectedLength").HasColumnType("bigint");
                b.Property<string>("State").IsRequired().HasMaxLength(24).HasColumnType("character varying(24)");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnType("xid").HasColumnName("xmin");
                b.HasKey("TenantId", "Id");
                b.HasAlternateKey("TenantId", "Id", "VesselId");
                b.HasIndex("TenantId", "VesselId");
                b.HasIndex("TenantId", "ActorId", "Key").IsUnique();
                b.Property<string>("StorageReference").HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<Guid?>("DocumentId").HasColumnType("uuid");
                b.Property<long?>("DocumentRevision").HasColumnType("bigint");
                b.Property<string>("Mime").IsRequired().ValueGeneratedOnAdd().HasMaxLength(32).HasColumnType("character varying(32)").HasDefaultValue("image/png");
                b.HasIndex("TenantId", "DocumentId", "DocumentRevision", "VesselId");
                b.ToTable("FileStages", "files", t => t.HasCheckConstraint("CK_FileStages_DocumentOwner", "(\"DocumentId\" IS NULL AND \"DocumentRevision\" IS NULL) OR (\"DocumentId\" IS NOT NULL AND \"DocumentRevision\" IS NOT NULL AND \"DocumentRevision\" > 0)"));
            });
            modelBuilder.Entity("Poseidon.Persistence.FileStage", b => {
                b.HasOne("Poseidon.Persistence.DocumentVersion", null).WithMany().HasForeignKey("TenantId", "DocumentId", "DocumentRevision", "VesselId").HasPrincipalKey("TenantId", "DocumentId", "Revision", "VesselId").HasConstraintName("FK_FileStages_DocumentVersion").OnDelete(DeleteBehavior.Restrict);
                b.HasOne("Poseidon.Persistence.Vessel", null).WithMany().HasForeignKey("TenantId", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });

            modelBuilder.Entity("Poseidon.Persistence.StoredFile", b =>
            {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<string>("Name").IsRequired().HasMaxLength(120).HasColumnType("character varying(120)");
                b.Property<string>("Checksum").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<long>("Length").HasColumnType("bigint");
                b.Property<long>("Revision").HasColumnType("bigint");
                b.Property<string>("Mime").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<string>("RetentionReference").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<string>("ScannerReference").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<DateTimeOffset>("ScannedAt").HasColumnType("timestamp with time zone");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnType("xid").HasColumnName("xmin");
                b.HasKey("TenantId", "Id");
                b.HasIndex("TenantId", "VesselId");
                b.HasIndex("TenantId", "Id", "VesselId").IsUnique();
                b.Property<string>("ProviderVersion").HasMaxLength(1024).HasColumnType("character varying(1024)");
                b.Property<string>("ProviderETag").HasMaxLength(256).HasColumnType("character varying(256)");
                b.Property<string>("ProviderReference").HasMaxLength(64).HasColumnType("character varying(64)");
                b.ToTable("StoredFiles", "files");
            });
            modelBuilder.Entity("Poseidon.Persistence.StoredFile", b => {
                b.HasOne("Poseidon.Persistence.Vessel", null).WithMany().HasForeignKey("TenantId", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne("Poseidon.Persistence.FileStage", null).WithOne().HasForeignKey("Poseidon.Persistence.StoredFile", "TenantId", "Id", "VesselId").HasPrincipalKey("Poseidon.Persistence.FileStage", "TenantId", "Id", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
            modelBuilder.HasPostgresExtension("btree_gist");
            modelBuilder.Entity("Poseidon.Persistence.Person", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string?>("FullName").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string?>("EmployeeNo").HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<string>("EmploymentState").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<string>("Provenance").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<DateTimeOffset?>("SourceObservedAt").HasColumnType("timestamp with time zone");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnName("xmin").HasColumnType("xid");
                b.HasKey("TenantId", "Id");
                b.HasIndex("TenantId");
                b.ToTable("Persons", "crew");
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewPosition", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<string>("Code").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<string?>("Rank").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string?>("Capacity").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string?>("Department").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Provenance").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnName("xmin").HasColumnType("xid");
                b.HasKey("TenantId", "Id");
                b.HasAlternateKey("TenantId", "Id", "VesselId");
                b.HasIndex("TenantId", "VesselId", "Code").IsUnique();
                b.HasIndex("TenantId", "VesselId");
                b.ToTable("Positions", "crew");
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewAssignment", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<Guid>("PersonId").HasColumnType("uuid");
                b.Property<Guid?>("PositionId").HasColumnType("uuid");
                b.Property<DateOnly?>("From").HasColumnType("date");
                b.Property<DateOnly?>("To").HasColumnType("date");
                b.Property<DateOnly?>("ContractEndDate").HasColumnType("date");
                b.Property<string?>("WatchGroup").HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<string>("Status").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<string>("Provenance").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<DateTimeOffset?>("SourceObservedAt").HasColumnType("timestamp with time zone");
                b.Property<long>("Revision").HasColumnType("bigint");
                b.Property<uint>("Version").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnName("xmin").HasColumnType("xid");
                b.HasKey("TenantId", "Id");
                b.HasAlternateKey("TenantId", "Id", "VesselId", "PersonId");
                b.HasIndex("TenantId", "PersonId", "From");
                b.HasIndex("TenantId", "PersonId");
                b.HasIndex("TenantId", "VesselId");
                b.HasIndex("TenantId", "PositionId", "VesselId");
                b.ToTable("Assignments", "crew", t => t.HasCheckConstraint("CK_Assignment_Period", "\"To\" IS NULL OR (\"From\" IS NOT NULL AND \"To\" > \"From\")"));
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewAssignmentRevision", b => {
                b.Property<Guid>("TenantId").HasColumnType("uuid");
                b.Property<Guid>("AssignmentId").HasColumnType("uuid");
                b.Property<Guid>("VesselId").HasColumnType("uuid");
                b.Property<Guid>("PersonId").HasColumnType("uuid");
                b.Property<Guid?>("PositionId").HasColumnType("uuid");
                b.Property<DateOnly?>("From").HasColumnType("date");
                b.Property<DateOnly?>("To").HasColumnType("date");
                b.Property<DateOnly?>("ContractEndDate").HasColumnType("date");
                b.Property<long>("Revision").HasColumnType("bigint");
                b.Property<string>("Provenance").IsRequired().HasMaxLength(32).HasColumnType("character varying(32)");
                b.Property<DateTimeOffset?>("SourceObservedAt").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset>("SavedAt").HasColumnType("timestamp with time zone");
                b.HasKey("TenantId", "AssignmentId", "Revision");
                b.HasIndex("TenantId", "AssignmentId", "VesselId", "PersonId");
                b.ToTable("AssignmentRevisions", "crew");
            });
            modelBuilder.Entity("Poseidon.Persistence.Person", b => {
                b.HasOne("Poseidon.Persistence.Tenant", null).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewPosition", b => {
                b.HasOne("Poseidon.Persistence.Vessel", null).WithMany().HasForeignKey("TenantId", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewAssignment", b => {
                b.HasOne("Poseidon.Persistence.Person", null).WithMany().HasForeignKey("TenantId", "PersonId").OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne("Poseidon.Persistence.Vessel", null).WithMany().HasForeignKey("TenantId", "VesselId").OnDelete(DeleteBehavior.Restrict).IsRequired();
                b.HasOne("Poseidon.Persistence.CrewPosition", null).WithMany().HasForeignKey("TenantId", "PositionId", "VesselId").HasPrincipalKey("TenantId", "Id", "VesselId").OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity("Poseidon.Persistence.CrewAssignmentRevision", b => {
                b.HasOne("Poseidon.Persistence.CrewAssignment", null).WithMany().HasForeignKey("TenantId", "AssignmentId", "VesselId", "PersonId").HasPrincipalKey("TenantId", "Id", "VesselId", "PersonId").OnDelete(DeleteBehavior.Restrict).IsRequired();
            });
#pragma warning restore 612, 618
        }
    }
}
