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

    // storedMime/storedWithoutClean build the QA F4 negative shapes: bytes stored with another
    // MIME, or a StoredFile row while the transport stage is not CleanTransport.
    private async Task<Guid> Stage(Guid actor, bool clean, string? storedMime = null, bool storedWithoutClean = false)
    {
        await using var db = fixture.Db(actor);
        var id = Guid.NewGuid(); var hash = Convert.ToHexString(SHA256.HashData(id.ToByteArray())); var now = DateTimeOffset.UtcNow;
        db.FileStages.Add(new() { TenantId = db.TenantId, Id = id, VesselId = Vessel.Id, ActorId = actor, Key = "k-" + id.ToString("N"),
            Fingerprint = new string('A', 64), Name = "synthetic.png", ExpectedChecksum = hash, ExpectedLength = 16, Mime = "image/png",
            CreatedAt = now, State = clean ? "CleanTransport" : "Pending" });
        if (clean || storedWithoutClean) { var stored = Stored(db.TenantId, id, hash, now); stored.Mime = storedMime ?? stored.Mime; db.StoredFiles.Add(stored); }
        await db.SaveChangesAsync(Ct);
        return id;
    }
    private static Task Sync(Action action) { action(); return Task.CompletedTask; }
    private async Task<ControlledDocument> Row(Guid document)
    {
        await using var db = fixture.Db(registrar);
        return await db.ControlledDocuments.AsNoTracking().SingleAsync(x => x.Id == document, Ct);
    }
    private Task<Guid> Registered(Guid document, bool clean = true) => RegisteredCore(document, clean);
    private async Task<Guid> RegisteredCore(Guid document, bool clean)
    {
        var stage = await Stage(uploader, clean);
        await Act(registrar, document, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, stage, Ct));
        return stage;
    }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
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
        Assert.Equal("retention_unconfigured", await Denied(records, document, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "end", Ct)));
        // QA F5 LEAD decision: back-dated retain-until is rejected at set time.
        Assert.Equal("invalid_request", await Denied(records, document, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-1", Today.AddDays(-1)))));
        await Act(records, document, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-1", Today)));
        await Act(records, document, (db, d) => Sync(() => ControlledDocumentChain.PlaceHold(db, records, d, "SYN-CASE-1", "synthetic litigation hold")));
        Assert.Equal("legal_hold_active", await Denied(records, document, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "end", Ct)));
        Assert.Equal("separation_of_duties", await Denied(records, document, (db, d) => Sync(() => ControlledDocumentChain.ReleaseHold(db, records, d, "case closed"))));
        await Act(approver, document, (db, d) => Sync(() => ControlledDocumentChain.ReleaseHold(db, approver, d, "case closed")));
        var unheld = await Row(document);
        Assert.Equal((false, records, approver), (unheld.HoldActive, unheld.HoldPlacedBy!.Value, unheld.HoldReleasedBy!.Value));
        await Act(records, document, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "retention elapsed", Ct));
        Assert.Equal("separation_of_duties", await Denied(records, document, (db, d) => ControlledDocumentChain.ApproveDisposalAsync(db, records, d, "approve", Ct)));
        // QA F2 / 58.02 §100: a hold placed while the request is pending invalidates the request.
        await Act(records, document, (db, d) => Sync(() => ControlledDocumentChain.PlaceHold(db, records, d, "SYN-CASE-2", "late hold")));
        var invalidated = await Row(document);
        Assert.Equal(("NONE", (Guid?)null, true, (Guid?)null), (invalidated.DisposalState, invalidated.DisposalRequestedBy, invalidated.HoldActive, invalidated.HoldReleasedBy));
        Assert.Equal("invalid_state", await Denied(approver, document, (db, d) => ControlledDocumentChain.ApproveDisposalAsync(db, approver, d, "approve", Ct)));
        await Act(approver, document, (db, d) => Sync(() => ControlledDocumentChain.ReleaseHold(db, approver, d, "case closed")));
        await Act(records, document, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "retention elapsed again", Ct));
        await Act(approver, document, (db, d) => ControlledDocumentChain.ApproveDisposalAsync(db, approver, d, "approve", Ct));
        var row = await Row(document);
        Assert.Equal(("APPROVED", records, approver), (row.DisposalState, row.DisposalRequestedBy!.Value, row.DisposalApprovedBy!.Value));
        Assert.Equal(["CREATED", "RETENTION_SET", "HOLD_PLACED", "HOLD_RELEASED", "DISPOSAL_REQUESTED", "HOLD_PLACED", "DISPOSAL_REQUEST_INVALIDATED",
            "HOLD_RELEASED", "DISPOSAL_REQUESTED", "DISPOSAL_APPROVED"], await Events(document));
        Assert.Equal("document_disposal_pending", await Denied(records, document, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-2", Today.AddYears(5)))));
    }

    // QA F1: "hold releaser <> placer" holds in the database, not only in the application.
    [Fact]
    public async Task Hold_release_by_the_placer_is_rejected_by_the_database_when_the_application_is_bypassed()
    {
        var records = Guid.NewGuid(); var approver = Guid.NewGuid();
        var document = await Create(registrar);
        await Act(records, document, (db, d) => Sync(() => ControlledDocumentChain.PlaceHold(db, records, d, "SYN-CASE-DB", "synthetic hold")));
        var where = $"WHERE \"TenantId\"='{fixture.Data.Tenants[0].Id}' AND \"Id\"='{document}'";
        string Release(Guid releasedBy, Guid recordedBy, bool setReleaser = true) =>
            "BEGIN; INSERT INTO files.\"ControlledDocumentCustodyEvents\" (\"TenantId\",\"DocumentId\",\"Sequence\",\"VesselId\",\"EventType\",\"RecordedBy\",\"OccurredAt\",\"RecordedAt\",\"Source\",\"Basis\") " +
            $"SELECT \"TenantId\",\"Id\",\"Sequence\"+1,\"VesselId\",'HOLD_RELEASED','{recordedBy}',now(),now(),'COMMAND','raw' FROM files.\"ControlledDocuments\" {where}; " +
            $"UPDATE files.\"ControlledDocuments\" SET \"HoldActive\"=false, \"Sequence\"=\"Sequence\"+1" +
            (setReleaser ? $", \"HoldReleasedBy\"='{releasedBy}', \"HoldReleasedAt\"=now()" : "") + $" {where}; COMMIT;";
        var self = await Sql(Release(records, records));
        Assert.Equal(("23514", "CK_ControlledDocuments_HoldRelease"), (self.SqlState, self.ConstraintName));
        var anonymous = await Sql(Release(approver, approver, setReleaser: false));
        Assert.Equal("23514", anonymous.SqlState); Assert.Contains("only advances", anonymous.MessageText);
        // Releaser column names another actor but the custody event is the placer's: rejected at commit.
        var forged = await Sql(Release(approver, records));
        Assert.Equal("23514", forged.SqlState); Assert.Contains("attributed custody event", forged.MessageText);
        Assert.True((await Row(document)).HoldActive);
    }

    // QA F2 / 58.02 §271 + §100: active use must be closed; a new link invalidates a pending request.
    [Fact]
    public async Task Disposal_requires_closed_active_use_and_a_new_link_invalidates_a_pending_request()
    {
        var records = Guid.NewGuid(); var approver = Guid.NewGuid();
        var published = await Create(registrar);
        await Registered(published);
        await Act(registrar, published, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        await Act(publisher, published, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 1, Ct));
        await Act(records, published, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-1", Today)));
        Assert.Equal("active_use_open", await Denied(records, published, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "retention elapsed", Ct)));
        var t = $"'{fixture.Data.Tenants[0].Id}'";
        var raw = await Sql($"BEGIN; INSERT INTO files.\"ControlledDocumentCustodyEvents\" (\"TenantId\",\"DocumentId\",\"Sequence\",\"VesselId\",\"EventType\",\"RecordedBy\",\"OccurredAt\",\"RecordedAt\",\"Source\") " +
            $"SELECT \"TenantId\",\"Id\",\"Sequence\"+1,\"VesselId\",'DISPOSAL_REQUESTED','{records}',now(),now(),'COMMAND' FROM files.\"ControlledDocuments\" WHERE \"TenantId\"={t} AND \"Id\"='{published}'; " +
            $"UPDATE files.\"ControlledDocuments\" SET \"DisposalState\"='REQUESTED', \"DisposalRequestedBy\"='{records}', \"DisposalRequestedAt\"=now(), \"Sequence\"=\"Sequence\"+1 WHERE \"TenantId\"={t} AND \"Id\"='{published}'; COMMIT;");
        Assert.Equal("23514", raw.SqlState); Assert.Contains("closed active use", raw.MessageText);

        var linked = await Create(registrar);
        await Act(records, linked, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, records, d, "SYN-POLICY-1", Today)));
        await Act(records, linked, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "retention elapsed", Ct));
        var evidence = ControlEvidenceFixtures.Id(Vessel.Id, "Evidence");
        await Act(registrar, linked, (db, d) => ControlledDocumentChain.LinkAsync(db, registrar, d, "ControlEvidence", evidence, Ct));
        Assert.Equal(("NONE", (Guid?)null), ((await Row(linked)).DisposalState, (await Row(linked)).DisposalRequestedBy));
        Assert.Equal(["CREATED", "RETENTION_SET", "DISPOSAL_REQUESTED", "LINKED", "DISPOSAL_REQUEST_INVALIDATED"], await Events(linked));
        Assert.Equal("invalid_state", await Denied(approver, linked, (db, d) => ControlledDocumentChain.ApproveDisposalAsync(db, approver, d, "approve", Ct)));
        // The live link keeps active use open: a new request is refused.
        Assert.Equal("active_use_open", await Denied(records, linked, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, records, d, "again", Ct)));
    }

    // QA F4: database triggers mirror the application guard for release, registration, verification,
    // custody gaps, single EFFECTIVE revision, disposal transitions, rollback publish and links.
    [Fact]
    public async Task Database_triggers_mirror_release_registration_verification_publication_disposal_and_link_guards()
    {
        var t = $"'{fixture.Data.Tenants[0].Id}'";
        string Where(Guid document) => $"WHERE \"TenantId\"={t} AND \"DocumentId\"='{document}'";
        async Task Rejected(string sql, string message, string state = "23514")
        {
            var e = await Sql(sql);
            Assert.True(e.SqlState == state && e.MessageText.Contains(message, StringComparison.Ordinal), $"{e.SqlState} {e.ConstraintName} {e.MessageText}");
        }
        const string ReleaseSql = "UPDATE files.\"ControlledDocumentRevisions\" SET \"IntakeState\"='RELEASED', \"ReleasedAt\"=now() ";

        // Release: StoredFile present but the stage never reached CleanTransport.
        var notClean = await Create(registrar);
        var pendingStage = await Stage(uploader, clean: false, storedWithoutClean: true);
        await Act(registrar, notClean, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, pendingStage, Ct));
        Assert.Equal("clean_file_unavailable", await Denied(registrar, notClean, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct)));
        await Rejected(ReleaseSql + Where(notClean), "clean transport");
        // Release: clean transport but stored bytes carry another MIME.
        var wrongMime = await Create(registrar);
        var mimeStage = await Stage(uploader, clean: true, storedMime: "application/pdf");
        await Act(registrar, wrongMime, (db, d) => ControlledDocumentChain.RegisterAsync(db, registrar, d, mimeStage, Ct));
        Assert.Equal("clean_file_unavailable", await Denied(registrar, wrongMime, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct)));
        await Rejected(ReleaseSql + Where(wrongMime), "clean transport");

        // Registration without custody events and LatestRevision bump fails at commit.
        var document = await Create(registrar);
        var rawStage = await Stage(uploader, clean: true);
        var hash = Convert.ToHexString(SHA256.HashData(rawStage.ToByteArray()));
        await Rejected("INSERT INTO files.\"ControlledDocumentRevisions\" (\"TenantId\",\"DocumentId\",\"Revision\",\"VesselId\",\"FileId\",\"ContentSha256\",\"ContentLength\",\"Mime\",\"UploadedBy\",\"RegisteredBy\",\"RegisteredAt\",\"IntakeState\",\"PublicationState\") " +
            $"VALUES ({t},'{document}',1,'{Vessel.Id}','{rawStage}','{hash}',16,'image/png','{uploader}','{registrar}',now(),'QUARANTINED','UNPUBLISHED')", "Revision registration requires");

        // Custody sequence gap fails at commit.
        await Rejected("BEGIN; INSERT INTO files.\"ControlledDocumentCustodyEvents\" (\"TenantId\",\"DocumentId\",\"Sequence\",\"VesselId\",\"EventType\",\"RecordedBy\",\"OccurredAt\",\"RecordedAt\",\"Source\") " +
            $"SELECT \"TenantId\",\"Id\",\"Sequence\"+2,\"VesselId\",'RETENTION_SET','{registrar}',now(),now(),'COMMAND' FROM files.\"ControlledDocuments\" WHERE \"TenantId\"={t} AND \"Id\"='{document}'; " +
            $"UPDATE files.\"ControlledDocuments\" SET \"Sequence\"=\"Sequence\"+2 WHERE \"TenantId\"={t} AND \"Id\"='{document}'; COMMIT;", "gap");

        // Two released revisions: publish 2, then a rollback publish of 1 is refused (app 409 code + DB).
        await Registered(document); await Registered(document);
        await Act(registrar, document, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        await Act(registrar, document, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 2, Ct));
        // Verification without its custody event fails at commit.
        var released = (await Revision(document)).ContentSha256;
        await Rejected("INSERT INTO files.\"ControlledDocumentVerifications\" (\"TenantId\",\"Id\",\"DocumentId\",\"Revision\",\"VesselId\",\"VerifierId\",\"Method\",\"Result\",\"Basis\",\"ContentSha256\",\"CheckedAt\") " +
            $"VALUES ({t}, '{Guid.NewGuid()}', '{document}', 1, '{Vessel.Id}', '{verifier}', 'MANUAL_SOURCE_COMPARISON', 'VALID', 'raw', '{released}', now())", "Verification requires its custody event");
        await Act(publisher, document, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 2, Ct));
        Assert.Equal("revision_older_than_effective", await Denied(publisher, document, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 1, Ct)));
        const string PublishSql = "UPDATE files.\"ControlledDocumentRevisions\" SET \"PublicationState\"='EFFECTIVE', \"PublishedAt\"=now(), ";
        await Rejected(PublishSql + $"\"PublishedBy\"='{publisher}' {Where(document)} AND \"Revision\"=1", "no newer published revision");

        // One EFFECTIVE revision per document (partial unique index), independent of the trigger.
        var pair = await Create(registrar);
        await Registered(pair); await Registered(pair);
        await Act(registrar, pair, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        await Act(registrar, pair, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 2, Ct));
        await Act(publisher, pair, (db, d) => ControlledDocumentChain.PublishAsync(db, publisher, d, 1, Ct));
        var duplicate = await Sql(PublishSql + $"\"PublishedBy\"='{publisher}' {Where(pair)} AND \"Revision\"=2");
        Assert.Equal(("23505", "IX_ControlledDocumentRevisions_Effective"), (duplicate.SqlState, duplicate.ConstraintName));

        // NONE -> APPROVED directly is refused even with a complete-looking row.
        var direct = await Create(registrar);
        await Act(registrar, direct, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, registrar, d, "SYN-POLICY-1", Today)));
        await Rejected($"UPDATE files.\"ControlledDocuments\" SET \"DisposalState\"='APPROVED', \"DisposalRequestedBy\"='{registrar}', \"DisposalRequestedAt\"=now(), " +
            $"\"DisposalApprovedBy\"='{publisher}', \"DisposalApprovedAt\"=now(), \"Sequence\"=\"Sequence\"+1 WHERE \"TenantId\"={t} AND \"Id\"='{direct}'", "only advances");

        // After an approved disposal no verification is accepted (app + DB) and links are refused.
        var disposed = await Create(registrar);
        await Registered(disposed);
        await Act(registrar, disposed, (db, d) => ControlledDocumentChain.ReleaseAsync(db, registrar, d, 1, Ct));
        await Act(registrar, disposed, (db, d) => Sync(() => ControlledDocumentChain.SetRetention(db, registrar, d, "SYN-POLICY-1", Today)));
        await Act(registrar, disposed, (db, d) => ControlledDocumentChain.RequestDisposalAsync(db, registrar, d, "elapsed", Ct));
        await Act(publisher, disposed, (db, d) => ControlledDocumentChain.ApproveDisposalAsync(db, publisher, d, "approve", Ct));
        Assert.Equal("document_disposed", await Denied(verifier, disposed, (db, d) => ControlledDocumentChain.VerifyAsync(db, verifier, d, 1, "MANUAL_SOURCE_COMPARISON", "VALID", "late", Ct)));
        var disposedHash = (await Revision(disposed)).ContentSha256;
        await Rejected("INSERT INTO files.\"ControlledDocumentVerifications\" (\"TenantId\",\"Id\",\"DocumentId\",\"Revision\",\"VesselId\",\"VerifierId\",\"Method\",\"Result\",\"Basis\",\"ContentSha256\",\"CheckedAt\") " +
            $"VALUES ({t}, '{Guid.NewGuid()}', '{disposed}', 1, '{Vessel.Id}', '{verifier}', 'MANUAL_SOURCE_COMPARISON', 'VALID', 'raw', '{disposedHash}', now())", "disposed document");
        var evidence = ControlEvidenceFixtures.Id(Vessel.Id, "Evidence");
        await Rejected("INSERT INTO files.\"ControlledDocumentEvidenceLinks\" (\"TenantId\",\"DocumentId\",\"EvidenceId\",\"VesselId\",\"EvidenceKind\",\"LinkedBy\",\"LinkedAt\") " +
            $"VALUES ({t}, '{disposed}', '{evidence}', '{Vessel.Id}', 'Evidence', '{registrar}', now())", "Link requires");
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
