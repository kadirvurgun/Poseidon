using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5802 CI gate regression (run 44486092548): LinkControlledDocument, PublishControlledDocumentRevision and
// ReleaseControlledDocumentRevision each left one AUDIT_BEFORE_AFTER_UNPROVEN row. Controlled-document receipts carry no
// committed xmin, so the in-place revision update needs its revision reference and the append-only link its owner binding.
// Rows are checked through the unchanged CriticalWriteCoverageCapture.BeforeAfter rule. Projection-only EF tracking:
// no connection opens, no SaveChanges, no persisted audit rows or runtime coverage evidence.
[Trait("Requirement", "PLAN-1511-5802")]
public sealed class ControlledDocumentAuditBeforeAfterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Vessel = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Correlation = "synthetic-controlled-document-correlation";
    private static readonly HashSet<(string, Guid, Guid?)> NoCommittedVersion = [];

    private static PoseidonDbContext Db()
    {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>();
        options.UseNpgsql(_ => { });
        return new PoseidonDbContext(options.Options, Tenant, new("synthetic-projection", "System", "synthetic-projection"));
    }

    // Mirrors the audited SaveChanges row construction for a non-receipt entry.
    private static (AuditEvent Row, Dictionary<string, JsonElement> Fields) Row(EntityEntry entry, string command, string reason)
    {
        var type = typeof(PoseidonDbContext).Assembly.GetType("Poseidon.Persistence.AuditMetadata", throwOnError: true)!;
        var projection = type.GetMethod("From", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entry, null, new AuditCommandContext(command, reason), true])!;
        T? Get<T>(string name) => (T?)type.GetProperty(name)!.GetValue(projection);
        var row = new AuditEvent
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ActorId = "synthetic", ActorType = "User", Action = entry.State.ToString(),
            EntityType = entry.Metadata.ClrType.Name, EntityId = "synthetic", CorrelationId = Correlation,
            ResourceId = Get<Guid?>("ResourceId"), VesselId = Get<Guid?>("VesselId"),
            BeforeVersion = entry.State == EntityState.Modified && entry.Metadata.FindProperty("Version") != null ? (uint?)entry.Property("Version").OriginalValue : null,
            BeforeRevision = Get<long?>("BeforeRevision"), AfterRevision = Get<long?>("AfterRevision"), ChangeSummary = Get<string>("Summary")
        };
        using var summary = JsonDocument.Parse(row.ChangeSummary!);
        return (row, summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!, x => x.Clone(), StringComparer.Ordinal));
    }

    private static ControlledDocumentRevision Revision(Guid document, string intake, string publication) => new()
    {
        TenantId = Tenant, DocumentId = document, Revision = 2, VesselId = Vessel, FileId = Guid.NewGuid(), ContentSha256 = new string('f', 64),
        ContentLength = 1024, Mime = "application/pdf", UploadedBy = Guid.NewGuid(), RegisteredBy = Guid.NewGuid(), RegisteredAt = T0,
        IntakeState = intake, ObjectGeneration = "FICTIONAL-GENERATION", PublicationState = publication
    };

    [Theory]
    [InlineData("release", "ReleaseControlledDocumentRevision", "FILE_TRANSPORT")]
    [InlineData("publish", "PublishControlledDocumentRevision", "HUMAN_REVIEW_WORKFLOW")]
    [InlineData("supersede", "PublishControlledDocumentRevision", "HUMAN_REVIEW_WORKFLOW")]
    public void In_place_revision_transitions_are_proven_by_their_immutable_revision_reference(string scenario, string command, string reason)
    {
        using var db = Db();
        var document = Guid.NewGuid();
        var revision = scenario switch
        {
            "release" => Revision(document, "QUARANTINED", "UNPUBLISHED"),
            "publish" => Revision(document, "RELEASED", "UNPUBLISHED"),
            _ => Revision(document, "RELEASED", "EFFECTIVE")
        };
        var entry = db.Attach(revision);
        if (scenario == "release") { revision.IntakeState = "RELEASED"; revision.ReleasedAt = T0; }
        else if (scenario == "publish") { revision.PublicationState = "EFFECTIVE"; revision.PublishedBy = Guid.NewGuid(); revision.PublishedAt = T0; }
        else revision.PublicationState = "SUPERSEDED";
        entry.DetectChanges();
        Assert.Equal(EntityState.Modified, entry.State);

        var (row, fields) = Row(entry, command, reason);
        Assert.Equal(document, row.ResourceId);
        Assert.Equal(2L, row.BeforeRevision);
        Assert.Equal(2L, row.AfterRevision);
        Assert.Null(row.BeforeVersion);
        var changed = scenario == "release" ? fields["IntakeState"] : fields["PublicationState"];
        Assert.Equal(scenario switch { "release" => ("QUARANTINED", "RELEASED"), "publish" => ("UNPUBLISHED", "EFFECTIVE"), _ => ("EFFECTIVE", "SUPERSEDED") },
            (changed.GetProperty("before").GetString(), changed.GetProperty("after").GetString()));
        // Controlled-document receipts carry no committed xmin: the row must prove itself.
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        Assert.DoesNotContain("FICTIONAL", row.ChangeSummary);
        Assert.DoesNotContain(new string('f', 64), row.ChangeSummary);
    }

    [Fact]
    public void Registered_revision_rows_stay_added_without_a_before_reference()
    {
        using var db = Db();
        var revision = Revision(Guid.NewGuid(), "QUARANTINED", "UNPUBLISHED");
        var entry = db.Entry(revision); entry.State = EntityState.Added;
        var (row, fields) = Row(entry, "RegisterControlledDocumentRevision", "PREPARATION_COMMAND");
        Assert.Null(row.BeforeRevision);
        Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
    }

    [Fact]
    public void Append_only_link_rows_bind_to_their_owning_document_without_a_committed_version()
    {
        using var db = Db();
        var document = Guid.NewGuid();
        object[] links =
        [
            new ControlledDocumentEvidenceLink { TenantId = Tenant, DocumentId = document, EvidenceId = Guid.NewGuid(), VesselId = Vessel, LinkedBy = Guid.NewGuid(), LinkedAt = T0 },
            new ControlledDocumentCertificateLink { TenantId = Tenant, DocumentId = document, CertificateId = Guid.NewGuid(), VesselId = Vessel, LinkedBy = Guid.NewGuid(), LinkedAt = T0 },
            new ControlledDocumentCredentialLink { TenantId = Tenant, DocumentId = document, CredentialId = Guid.NewGuid(), VesselId = Vessel, LinkedBy = Guid.NewGuid(), LinkedAt = T0 }
        ];
        foreach (var link in links)
        {
            var entry = db.Entry(link); entry.State = EntityState.Added;
            var (row, fields) = Row(entry, "LinkControlledDocument", "PREPARATION_COMMAND");
            Assert.Equal(document, row.ResourceId);
            Assert.Equal(Vessel, row.VesselId);
            Assert.Equal("OwnerReference", fields["Binding"].GetProperty("after").GetString());
            Assert.True(CriticalWriteCoverageCapture.BeforeAfter(row, NoCommittedVersion, fields));
        }
    }
}
