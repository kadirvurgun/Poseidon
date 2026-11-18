using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Persistence.Seeding;
using Testcontainers.PostgreSql;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class ControlledDocumentPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    public SeedData Data { get; private set; } = null!;
    public string ConnectionString => postgres.GetConnectionString();
    public PoseidonDbContext Db(Guid actor, Guid? tenant = null) =>
        new(SeedImporter.Options(postgres.GetConnectionString()), tenant ?? Data.Tenants[0].Id, new(actor.ToString(), "User", "controlled-document-test"));
    public PoseidonDbContext Import() => new(SeedImporter.Options(postgres.GetConnectionString()), Guid.Empty, ActorContext.SeedImport(), true);
    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync(); Data = SeedValidationTests.Demo();
        await using var db = Import(); await db.Database.MigrateAsync(); await SeedImporter.ImportAsync(db, Data);
    }
    public async ValueTask DisposeAsync() => await postgres.DisposeAsync();
}

// PLAN-1511-5802 real PostgreSQL: append-only custody (EF guard + DB triggers), SoD, hold vs
// disposal, tenant isolation and idempotent synthetic fixture. No fake verified evidence.
[Trait("Category", "Integration")]
[Trait("Requirement", "PLAN-1511-5802")]
public sealed class ControlledDocumentPersistenceTests(ControlledDocumentPostgresFixture fixture) : IClassFixture<ControlledDocumentPostgresFixture>
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;
    private Vessel Vessel => fixture.Data.Vessels.First(v => v.TenantId == fixture.Data.Tenants[0].Id);
    private readonly Guid uploader = Guid.NewGuid(), registrar = Guid.NewGuid(), verifier = Guid.NewGuid(), publisher = Guid.NewGuid();

    private async Task<Guid> Stage(Guid actor, bool clean)
    {
        await using var db = fixture.Db(actor);
        var id = Guid.NewGuid(); var hash = Convert.ToHexString(SHA256.HashData(id.ToByteArray())); var now = DateTimeOffset.UtcNow;
        db.FileStages.Add(new() { TenantId = db.TenantId, Id = id, VesselId = Vessel.Id, ActorId = actor, Key = "k-" + id.ToString("N"),
            Fingerprint = new string('A', 64), Name = "synthetic.png", ExpectedChecksum = hash, ExpectedLength = 16, Mime = "image/png",
            CreatedAt = now, State = clean ? "CleanTransport" : "Pending" });
        if (clean) db.StoredFiles.Add(Stored(db.TenantId, id, hash, now));
        await db.SaveChangesAsync(Ct);
        return id;
    }
    private StoredFile Stored(Guid tenant, Guid id, string hash, DateTimeOffset at) => new() { TenantId = tenant, Id = id, VesselId = Vessel.Id, Name = "synthetic.png",
        Checksum = hash, Length = 16, Mime = "image/png", RetentionReference = "synthetic-policy", ScannerReference = "synthetic-scanner-peer", ScannedAt = at.AddSeconds(-5) };

    private async Task<Guid> Create(Guid actor)
    {
        await using var db = fixture.Db(actor);
        var d = ControlledDocumentChain.Create(db, actor, Vessel.Id, "SYN-" + Guid.NewGuid().ToString("N")[..12], "FICTIONAL controlled document", "INTERNAL");
        await db.SaveChangesAsync(Ct);
        return d.Id;
    }
    private async Task Act(Guid actor, Guid document, Func<PoseidonDbContext, ControlledDocument, Task> action)
    {
        await using var db = fixture.Db(actor);
        var version = (await db.ControlledDocuments.AsNoTracking().SingleAsync(x => x.Id == document, Ct)).Version;
        var d = await ControlledDocumentChain.LoadAsync(db, Vessel.Id, document, version, Ct);
        await action(db, d);
        await db.SaveChangesAsync(Ct);
    }
    private async Task<string> Denied(Guid actor, Guid document, Func<PoseidonDbContext, ControlledDocument, Task> action) =>
        (await Assert.ThrowsAsync<ControlledDocumentException>(() => Act(actor, document, action))).Code;
    private async Task<ControlledDocumentRevision> Revision(Guid document, long revision = 1)
    {
        await using var db = fixture.Db(registrar);
        return await db.ControlledDocumentRevisions.AsNoTracking().SingleAsync(x => x.DocumentId == document && x.Revision == revision, Ct);
    }
    private async Task<string[]> Events(Guid document)
    {
        await using var db = fixture.Db(registrar);
        return await db.ControlledDocumentCustodyEvents.AsNoTracking().Where(x => x.DocumentId == document).OrderBy(x => x.Sequence).Select(x => x.EventType).ToArrayAsync(Ct);
    }
    private async Task<PostgresException> Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
    }

    [Fact]
    public async Task Upload_is_quarantined_then_scan_release_publication_and_independent_verification_stay_separate()
    {
        var stage = await Stage(uploader, clean: false);
        var document = await Create(registrar);
        await Act(registrar, document, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, stage, Ct));
        var registered = await Revision(document);
        Assert.Equal(("QUARANTINED", "UNPUBLISHED", uploader, registrar), (registered.IntakeState, registered.PublicationState, registered.UploadedBy, registered.RegisteredBy));
        Assert.Equal(["CREATED", "RECEIVED", "QUARANTINED"], await Events(document));
        Assert.Equal("intake_not_released", await Denied(publisher, document, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 1, Ct)));
        Assert.Equal("intake_not_released", await Denied(verifier, document, (db, d) => ControlledDocumentChain.VerifyAsync(db, verifier, d, 1, "MANUAL_SOURCE_COMPARISON", "VALID", "Compared with synthetic register", Ct)));
        // Pending transport is not clean bytes: release stays closed.
        Assert.Equal("clean_file_unavailable", await Denied(registrar, document, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct)));

        await using (var db = fixture.Db(uploader))
        {
            var row = await db.FileStages.SingleAsync(x => x.Id == stage, Ct); row.State = "Receiving"; await db.SaveChangesAsync(Ct); row.State = "CleanTransport";
            db.StoredFiles.Add(Stored(db.TenantId, stage, row.ExpectedChecksum, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }
        await Act(registrar, document, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        Assert.Equal("RELEASED", (await Revision(document)).IntakeState);
        await using (var db = fixture.Db(registrar))
        {
            var scan = await db.ControlledDocumentCustodyEvents.AsNoTracking().SingleAsync(x => x.DocumentId == document && x.EventType == "SCANNED_CLEAN", Ct);
            Assert.Equal(("SECURE_FILE_PIPELINE", uploader, registrar.ToString()), (scan.Source, scan.SubjectActorId!.Value, scan.RecordedBy));
            Assert.True(scan.OccurredAt < scan.RecordedAt);
            Assert.False(await db.ControlledDocumentVerifications.AnyAsync(x => x.DocumentId == document, Ct)); // scanner CLEAN is not verification
        }
        Assert.Equal("separation_of_duties", await Denied(uploader, document, (db, d) => ControlledDocumentChain.VerifyAsync(db, uploader, d, 1, "MANUAL_SOURCE_COMPARISON", "VALID", "self check", Ct)));
        Assert.Equal("separation_of_duties", await Denied(registrar, document, (db, d) => ControlledDocumentChain.VerifyAsync(db, registrar, d, 1, "MANUAL_SOURCE_COMPARISON", "VALID", "self check", Ct)));
        Assert.Equal("separation_of_duties", await Denied(registrar, document, (db, d) => ControlledDocumentChain.PublishAsync(db, registrar, d, 1, Ct)));
        await Act(verifier, document, async (db, d) => await ControlledDocumentChain.VerifyAsync(db, verifier, d, 1, "MANUAL_SOURCE_COMPARISON", "VALID", "Withheld basis text", Ct));
        Assert.Equal("UNPUBLISHED", (await Revision(document)).PublicationState); // verification is not publication
        await Act(publisher, document, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 1, Ct));
        var published = await Revision(document);
        Assert.Equal(("RELEASED", "EFFECTIVE", publisher), (published.IntakeState, published.PublicationState, published.PublishedBy!.Value));
        Assert.Equal(["CREATED", "RECEIVED", "QUARANTINED", "SCANNED_CLEAN", "RELEASED", "VERIFICATION_RECORDED", "PUBLISHED"], await Events(document));

        await using var audit = fixture.Db(registrar);
        var rows = await audit.AuditEvents.AsNoTracking().Where(x => x.ResourceId == document).ToArrayAsync(Ct);
        Assert.All(rows, x => Assert.Equal(Vessel.Id, x.VesselId));
        var summaries = string.Join('\n', rows.Select(x => x.ChangeSummary));
        Assert.DoesNotContain("Withheld basis text", summaries); Assert.DoesNotContain("FICTIONAL controlled document", summaries);
        Assert.DoesNotContain(published.ContentSha256, summaries);
        Assert.Contains(rows, x => x.EntityType == nameof(ControlledDocumentVerification) && x.ActorId == verifier.ToString());
    }

    [Fact]
    public async Task Custody_and_revision_content_are_append_only_in_EF_and_database_triggers()
    {
        var stage = await Stage(uploader, clean: true);
        var document = await Create(registrar);
        await Act(registrar, document, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, stage, Ct));
        await using (var db = fixture.Db(registrar))
        {
            var e = await db.ControlledDocumentCustodyEvents.SingleAsync(x => x.DocumentId == document && x.Sequence == 1, Ct);
            e.Basis = "tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
            db.ChangeTracker.Clear();
            var r = await db.ControlledDocumentRevisions.SingleAsync(x => x.DocumentId == document, Ct);
            r.ContentSha256 = new string('0', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
            db.ChangeTracker.Clear();
            var r2 = await db.ControlledDocumentRevisions.SingleAsync(x => x.DocumentId == document, Ct);
            r2.IntakeState = "RELEASED"; r2.ReleasedAt = DateTimeOffset.UtcNow; // state change without custody append
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));
        }
        var t = $"'{fixture.Data.Tenants[0].Id}'"; var id = $"'{document}'";
        Assert.Equal("55000", (await Sql($"UPDATE files.\"ControlledDocumentCustodyEvents\" SET \"Basis\"='x' WHERE \"TenantId\"={t} AND \"DocumentId\"={id}")).SqlState);
        Assert.Equal("55000", (await Sql($"DELETE FROM files.\"ControlledDocumentCustodyEvents\" WHERE \"TenantId\"={t} AND \"DocumentId\"={id}")).SqlState);
        Assert.Equal("55000", (await Sql("TRUNCATE files.\"ControlledDocumentCustodyEvents\" CASCADE")).SqlState);
        Assert.Equal("55000", (await Sql($"UPDATE files.\"ControlledDocumentRevisions\" SET \"ContentSha256\"=repeat('0',64) WHERE \"TenantId\"={t} AND \"DocumentId\"={id}")).SqlState);
        Assert.Equal("55000", (await Sql($"DELETE FROM files.\"ControlledDocumentRevisions\" WHERE \"TenantId\"={t} AND \"DocumentId\"={id}")).SqlState);
        Assert.Equal("55000", (await Sql($"DELETE FROM files.\"ControlledDocuments\" WHERE \"TenantId\"={t} AND \"Id\"={id}")).SqlState);
        Assert.Equal("23514", (await Sql($"UPDATE files.\"ControlledDocumentRevisions\" SET \"PublicationState\"='EFFECTIVE', \"PublishedBy\"='{publisher}', \"PublishedAt\"=now() WHERE \"TenantId\"={t} AND \"DocumentId\"={id}")).SqlState);
        // A document change without its custody event fails at commit (deferred constraint trigger).
        Assert.Equal("23514", (await Sql($"UPDATE files.\"ControlledDocuments\" SET \"Sequence\"=\"Sequence\"+1 WHERE \"TenantId\"={t} AND \"Id\"={id}")).SqlState);
        // Self-verification is rejected by the database even when the application is bypassed.
        await Act(registrar, document, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        var hash = (await Revision(document)).ContentSha256;
        Assert.Equal("23514", (await Sql($"INSERT INTO files.\"ControlledDocumentVerifications\" (\"TenantId\",\"Id\",\"DocumentId\",\"Revision\",\"VesselId\",\"VerifierId\",\"Method\",\"Result\",\"Basis\",\"ContentSha256\",\"CheckedAt\") VALUES ({t}, '{Guid.NewGuid()}', {id}, 1, '{Vessel.Id}', '{uploader}', 'MANUAL_SOURCE_COMPARISON', 'VALID', 'self', '{hash}', now())")).SqlState);
    }

    [Fact]
    public async Task Legal_hold_blocks_disposal_and_release_and_approval_need_independent_actors()
    {
        var records = Guid.NewGuid(); var approver = Guid.NewGuid();
        var document = await Create(registrar);
        Assert.Equal("retention_unconfigured", await Denied(records, document, (db, d) => { ControlledDocumentChain.RequestDisposal(db, records, d, "end"); return Task.CompletedTask; }));
        await Act(records, document, (db, d) => { ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-1", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)); return Task.CompletedTask; });
        await Act(records, document, (db, d) => { ControlledDocumentChain.PlaceHold(db, records, d, "SYN-CASE-1", "synthetic litigation hold"); return Task.CompletedTask; });
        Assert.Equal("legal_hold_active", await Denied(records, document, (db, d) => { ControlledDocumentChain.RequestDisposal(db, records, d, "end"); return Task.CompletedTask; }));
        Assert.Equal("separation_of_duties", await Denied(records, document, (db, d) => { ControlledDocumentChain.ReleaseHold(db, records, d, "case closed"); return Task.CompletedTask; }));
        await Act(approver, document, (db, d) => { ControlledDocumentChain.ReleaseHold(db, approver, d, "case closed"); return Task.CompletedTask; });
        await Act(records, document, (db, d) => { ControlledDocumentChain.RequestDisposal(db, records, d, "retention elapsed"); return Task.CompletedTask; });
        Assert.Equal("separation_of_duties", await Denied(records, document, (db, d) => { ControlledDocumentChain.ApproveDisposal(db, records, d, "approve"); return Task.CompletedTask; }));
        // A hold placed between request and approval blocks approval (application and DB).
        await Act(records, document, (db, d) => { ControlledDocumentChain.PlaceHold(db, records, d, "SYN-CASE-2", "late hold"); return Task.CompletedTask; });
        Assert.Equal("legal_hold_active", await Denied(approver, document, (db, d) => { ControlledDocumentChain.ApproveDisposal(db, approver, d, "approve"); return Task.CompletedTask; }));
        var t = $"'{fixture.Data.Tenants[0].Id}'";
        Assert.Equal("23514", (await Sql($"UPDATE files.\"ControlledDocuments\" SET \"DisposalState\"='APPROVED', \"DisposalApprovedBy\"='{approver}', \"DisposalApprovedAt\"=now() WHERE \"TenantId\"={t} AND \"Id\"='{document}'")).SqlState);
        await Act(approver, document, (db, d) => { ControlledDocumentChain.ReleaseHold(db, approver, d, "case closed"); return Task.CompletedTask; });
        await Act(approver, document, (db, d) => { ControlledDocumentChain.ApproveDisposal(db, approver, d, "approve"); return Task.CompletedTask; });
        await using var check = fixture.Db(registrar);
        var row = await check.ControlledDocuments.AsNoTracking().SingleAsync(x => x.Id == document, Ct);
        Assert.Equal(("APPROVED", records, approver), (row.DisposalState, row.DisposalRequestedBy!.Value, row.DisposalApprovedBy!.Value));
        Assert.Equal(["CREATED", "RETENTION_SET", "HOLD_PLACED", "HOLD_RELEASED", "DISPOSAL_REQUESTED", "HOLD_PLACED", "HOLD_RELEASED", "DISPOSAL_APPROVED"], await Events(document));
        Assert.Equal("document_disposal_pending", await Denied(records, document, (db, d) => { ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-2", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(5)); return Task.CompletedTask; }));
    }

    [Fact]
    public async Task Tenant_filters_hide_custody_and_foreign_transport_cannot_be_registered()
    {
        var document = await Create(registrar);
        var foreignTenant = fixture.Data.Tenants[1].Id;
        await using (var other = fixture.Db(registrar, foreignTenant))
        {
            Assert.False(await other.ControlledDocuments.AnyAsync(x => x.Id == document, Ct));
            Assert.False(await other.ControlledDocumentCustodyEvents.AnyAsync(x => x.DocumentId == document, Ct));
            Assert.False(await other.AuditEvents.AnyAsync(x => x.ResourceId == document, Ct));
        }
        // A transport intent in another vessel's scope (here: never staged in this tenant) is unavailable.
        Assert.Equal("clean_file_unavailable", await Denied(registrar, document, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, Guid.NewGuid(), Ct)));
        var stage = await Stage(uploader, clean: true);
        await Act(registrar, document, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, stage, Ct));
        var second = await Create(registrar);
        Assert.Equal("immutable_file_reference", await Denied(registrar, second, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, stage, Ct)));
        await using var wrongVessel = fixture.Db(registrar);
        var otherVessel = fixture.Data.Vessels.First(v => v.TenantId == fixture.Data.Tenants[0].Id && v.Id != Vessel.Id);
        var version = (await wrongVessel.ControlledDocuments.AsNoTracking().SingleAsync(x => x.Id == document, Ct)).Version;
        Assert.Equal("resource_not_found", (await Assert.ThrowsAsync<ControlledDocumentException>(() => ControlledDocumentChain.LoadAsync(wrongVessel, otherVessel.Id, document, version, Ct))).Code);
        Assert.Equal("version_conflict", (await Assert.ThrowsAsync<ControlledDocumentException>(() => ControlledDocumentChain.LoadAsync(wrongVessel, Vessel.Id, document, version + 1, Ct))).Code);
    }

    [Fact]
    public async Task Synthetic_fixture_is_idempotent_empty_and_never_seeds_verified_evidence()
    {
        await using (var db = fixture.Import())
        {
            await ControlledDocumentFixtures.AddMissingAsync(db, fixture.Data.Vessels, Ct); await db.SaveChangesAsync(Ct);
        }
        await using (var db = fixture.Import())
        {
            var audits = await db.AuditEvents.CountAsync(Ct);
            await ControlledDocumentFixtures.AddMissingAsync(db, fixture.Data.Vessels, Ct); await db.SaveChangesAsync(Ct);
            Assert.Equal(audits, await db.AuditEvents.CountAsync(Ct));
            var ids = fixture.Data.Vessels.Select(v => ControlledDocumentFixtures.Id(v.Id)).ToArray();
            var shells = await db.ControlledDocuments.AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(Ct);
            Assert.Equal(fixture.Data.Vessels.Count(), shells.Length);
            Assert.All(shells, x => Assert.Equal(("SyntheticUnverified", 0L, (long?)null, false, "NONE", (string?)null), (x.Provenance, x.LatestRevision, x.CurrentRevision, x.HoldActive, x.DisposalState, x.RetentionPolicyRef)));
            Assert.False(await db.ControlledDocumentRevisions.AnyAsync(x => ids.Contains(x.DocumentId), Ct));
            Assert.False(await db.ControlledDocumentVerifications.AnyAsync(x => ids.Contains(x.DocumentId), Ct));
            Assert.All(await db.ControlledDocumentCustodyEvents.AsNoTracking().Where(x => ids.Contains(x.DocumentId)).ToArrayAsync(Ct),
                e => Assert.Equal(("CREATED", "SYNTHETIC_SEED"), (e.EventType, e.Source)));
        }
        await using var user = fixture.Db(registrar);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ControlledDocumentFixtures.AddMissingAsync(user, fixture.Data.Vessels, Ct));
    }
}
