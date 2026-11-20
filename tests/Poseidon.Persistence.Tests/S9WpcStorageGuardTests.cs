using Microsoft.EntityFrameworkCore;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-062 / DEV-114: voice draft history, receipts, acknowledgements and content access records are
// append-only; a confirmed or rejected voice draft is terminal; transcript/candidate and the ISPS flag
// are immutable; raw audio is never retained (DB CHECK).
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-062")]
[Trait("Requirement", "DEV-114")]
public sealed class S9WpcStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000c962");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));
    private static VoiceEventDraft Draft(string state = "DRAFT") => new() { TenantId = Tenant, VesselId = Guid.NewGuid(), Id = Guid.NewGuid(), Revision = 2, State = state,
        Transcript = "FICTIONAL note", TranscriptSha256 = new string('a', 64), ExtractorVersion = "x", RuleSetVersion = "y" };

    [Fact]
    public void Voice_draft_history_and_receipts_cannot_be_rewritten()
    {
        using var db = Context();
        var revision = new VoiceEventDraftRevision { TenantId = Tenant, VesselId = Guid.NewGuid(), DraftId = Guid.NewGuid(), Revision = 1, ReasonCode = "CREATE" };
        db.Attach(revision); revision.FieldsJson = "{\"tampered\":true}";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var receipt = new VoiceEventDraftReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "VoiceDraftConfirm", Revision = 3 };
        db.Attach(receipt); db.Remove(receipt);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Transcript_is_immutable_and_terminal_drafts_cannot_change()
    {
        using var db = Context();
        var draft = Draft(); db.Attach(draft); draft.Transcript = "rewritten"; draft.Revision++;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var confirmed = Draft("CONFIRMED"); db.Attach(confirmed); confirmed.FieldsJson = "{}"; confirmed.Revision++;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var skipped = Draft(); db.Attach(skipped); skipped.FieldsJson = "{\"a\":1}"; skipped.Revision += 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        db.Add(Draft("CONFIRMED"));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Acknowledgements_and_access_records_are_append_only()
    {
        using var db = Context();
        var ack = new ControlledDocumentAcknowledgement { TenantId = Tenant, Id = Guid.NewGuid(), VesselId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Revision = 1,
            ActorId = Guid.NewGuid(), ContentSha256 = new string('A', 64) };
        db.Attach(ack); db.Remove(ack);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var access = new ControlledDocumentAccessRecord { TenantId = Tenant, Id = Guid.NewGuid(), VesselId = Guid.NewGuid(), DocumentId = Guid.NewGuid(), Revision = 1, FileId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(), Classification = "RESTRICTED", Purpose = "VIEW", PolicyBasis = "RESTRICTED_EXPLICIT_PERMISSION", Attribution = "x", ContentSha256 = new string('A', 64) };
        db.Attach(access); access.Purpose = "DOWNLOAD";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Model_carries_the_audio_retention_isps_and_human_record_checks()
    {
        using var db = Context();
        var model = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(db).Model;
        string Checks<T>() => string.Join(" ", model.FindEntityType(typeof(T))!.GetCheckConstraints().Select(c => c.Sql));
        var voice = Checks<VoiceEventDraft>();
        Assert.Contains("\"AudioRetention\"='NOT_RETAINED'", voice, StringComparison.Ordinal);
        Assert.Contains("\"ConfirmedBy\" IS NOT NULL", voice, StringComparison.Ordinal);
        Assert.Contains("\"Provenance\"<>'SyntheticUnverified' OR \"State\"='DRAFT'", voice, StringComparison.Ordinal);
        Assert.Contains("NOT \"IspsRestricted\" OR \"Classification\" = 'RESTRICTED'", Checks<ControlledDocument>(), StringComparison.Ordinal);
        Assert.Contains("'READ_AND_ACCESSED_V1'", Checks<ControlledDocumentAcknowledgement>(), StringComparison.Ordinal);
        var unique = model.FindEntityType(typeof(ControlledDocumentAcknowledgement))!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_ControlledDocumentAcknowledgements_Reader");
        Assert.True(unique.IsUnique);
    }
}
