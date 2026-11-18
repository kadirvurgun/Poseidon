using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

public sealed class ControlledDocumentException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

// PLAN-1511-5802 command core. It only stages tracked changes; the caller owns
// authorization, CSRF, idempotency, the transaction and the single audited SaveChanges.
// Actor IDs are server-resolved; never pass request-supplied identities.
public static class ControlledDocumentChain
{
    private static DateTimeOffset Now() { var now = DateTimeOffset.UtcNow; return now.AddTicks(-(now.Ticks % 10)); }
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    private static ControlledDocumentCustodyEvent Append(PoseidonDbContext db, ControlledDocument d, string type, DateTimeOffset now,
        long? revision = null, string? basis = null, Guid? subject = null, DateTimeOffset? occurredAt = null, string source = "COMMAND")
    {
        d.Sequence = checked(d.Sequence + 1);
        var e = new ControlledDocumentCustodyEvent { TenantId = d.TenantId, DocumentId = d.Id, Sequence = d.Sequence, VesselId = d.VesselId,
            Revision = revision, EventType = type, RecordedBy = db.AuditActorId, SubjectActorId = subject, OccurredAt = occurredAt ?? now,
            RecordedAt = now, Source = source, Basis = basis };
        db.ControlledDocumentCustodyEvents.Add(e);
        return e;
    }

    private static void Actor(PoseidonDbContext db, Guid actor)
    {
        if (db.IsAdministrativeImport || actor == Guid.Empty || db.AuditActorId != actor.ToString()) throw new ControlledDocumentException("permission_denied");
    }

    // Loads the tracked document within vessel scope and pins the caller's CAS version.
    public static async Task<ControlledDocument> LoadAsync(PoseidonDbContext db, Guid vessel, Guid document, uint expectedVersion, CancellationToken ct)
    {
        var d = await db.ControlledDocuments.SingleOrDefaultAsync(x => x.Id == document && x.VesselId == vessel, ct)
            ?? throw new ControlledDocumentException("resource_not_found");
        if (d.Version != expectedVersion) throw new ControlledDocumentException("version_conflict");
        db.Entry(d).Property(x => x.Version).OriginalValue = expectedVersion;
        return d;
    }

    public static ControlledDocument Create(PoseidonDbContext db, Guid actor, Guid vessel, string code, string title, string classification)
    {
        Actor(db, actor);
        if (!ControlledDocumentPolicy.Code(code) || !ControlledDocumentPolicy.Text(title, 200) || !ControlledDocumentPolicy.Classifications.Contains(classification))
            throw new ControlledDocumentException("invalid_request");
        var now = Now();
        var d = new ControlledDocument { TenantId = db.TenantId, Id = Guid.NewGuid(), VesselId = vessel, Code = code.Trim().ToUpperInvariant(), Title = title.Trim(),
            Classification = classification, CreatedBy = db.AuditActorId, CreatedAt = now };
        db.ControlledDocuments.Add(d);
        Append(db, d, "CREATED", now);
        return d;
    }

    // DE-006: registration binds an exact secure-file transport intent. It is recorded as
    // RECEIVED + QUARANTINED regardless of the transport state; nothing is released yet.
    public static async Task<ControlledDocumentRevision> RegisterAsync(PoseidonDbContext db, Guid actor, ControlledDocument d, Guid fileId, CancellationToken ct)
    {
        Actor(db, actor);
        if (ControlledDocumentPolicy.RegisterBlocker(d.DisposalState) is { } blocked) throw new ControlledDocumentException(blocked);
        var stage = await db.FileStages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.VesselId == d.VesselId && x.PersonId == null && x.DocumentId == null, ct)
            ?? throw new ControlledDocumentException("clean_file_unavailable");
        if (await db.ControlledDocumentRevisions.AnyAsync(x => x.FileId == fileId, ct)) throw new ControlledDocumentException("immutable_file_reference");
        var now = Now();
        d.LatestRevision = checked(d.LatestRevision + 1);
        var r = new ControlledDocumentRevision { TenantId = d.TenantId, DocumentId = d.Id, Revision = d.LatestRevision, VesselId = d.VesselId, FileId = stage.Id,
            ContentSha256 = stage.ExpectedChecksum, ContentLength = stage.ExpectedLength, Mime = stage.Mime, UploadedBy = stage.ActorId, RegisteredBy = actor, RegisteredAt = now };
        db.ControlledDocumentRevisions.Add(r);
        Append(db, d, "RECEIVED", now, r.Revision, subject: stage.ActorId);
        Append(db, d, "QUARANTINED", now, r.Revision);
        return r;
    }

    private static async Task<ControlledDocumentRevision> RevisionAsync(PoseidonDbContext db, ControlledDocument d, long revision, CancellationToken ct) =>
        await db.ControlledDocumentRevisions.SingleOrDefaultAsync(x => x.DocumentId == d.Id && x.VesselId == d.VesselId && x.Revision == revision, ct)
            ?? throw new ControlledDocumentException("resource_not_found");

    // Release from quarantine only when the secure-file pipeline holds clean bytes whose
    // server hash/length equal the pinned content. The scanner fact is recorded with its own
    // time and actor; it is never a human verification.
    public static async Task ReleaseAsync(PoseidonDbContext db, Guid actor, ControlledDocument d, long revision, CancellationToken ct)
    {
        Actor(db, actor);
        var r = await RevisionAsync(db, d, revision, ct);
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.FileId && x.VesselId == d.VesselId &&
            db.FileStages.Any(s => s.Id == x.Id && s.VesselId == d.VesselId && s.State == "CleanTransport"), ct);
        var matches = file != null && file.Checksum == r.ContentSha256 && file.Length == r.ContentLength && file.Mime == r.Mime;
        if (ControlledDocumentPolicy.ReleaseBlocker(r.IntakeState, d.DisposalState, matches) is { } blocked) throw new ControlledDocumentException(blocked);
        var now = Now();
        Append(db, d, "SCANNED_CLEAN", now, revision, basis: file!.ScannerReference, subject: r.UploadedBy, occurredAt: file.ScannedAt, source: "SECURE_FILE_PIPELINE");
        r.IntakeState = "RELEASED"; r.ReleasedAt = now; r.ObjectGeneration = file.ProviderVersion;
        Append(db, d, "RELEASED", now, revision);
    }

    // Publication is a separate state: it requires a released intake and an independent
    // publisher, supersedes the prior effective revision and never implies verification.
    public static async Task PublishAsync(PoseidonDbContext db, Guid actor, ControlledDocument d, long revision, CancellationToken ct)
    {
        Actor(db, actor);
        var r = await RevisionAsync(db, d, revision, ct);
        if (ControlledDocumentPolicy.PublishBlocker(r.IntakeState, r.PublicationState, d.DisposalState, actor, r.UploadedBy, r.RegisteredBy) is { } blocked)
            throw new ControlledDocumentException(blocked);
        var now = Now();
        if (d.CurrentRevision is long current && current != revision)
        {
            var prior = await RevisionAsync(db, d, current, ct);
            if (prior.PublicationState == "EFFECTIVE") { prior.PublicationState = "SUPERSEDED"; Append(db, d, "SUPERSEDED", now, current); }
        }
        r.PublicationState = "EFFECTIVE"; r.PublishedBy = actor; r.PublishedAt = now; d.CurrentRevision = revision;
        Append(db, d, "PUBLISHED", now, revision);
    }

    // Independent verification: different human than uploader/registrar, closed method,
    // recorded basis, bound to the exact content hash. It changes no other state.
    public static async Task<ControlledDocumentVerification> VerifyAsync(PoseidonDbContext db, Guid actor, ControlledDocument d, long revision,
        string? method, string? result, string? basis, CancellationToken ct)
    {
        Actor(db, actor);
        var r = await RevisionAsync(db, d, revision, ct);
        if (ControlledDocumentPolicy.VerifyBlocker(r.IntakeState, d.DisposalState, actor, r.UploadedBy, r.RegisteredBy, method, result, basis) is { } blocked)
            throw new ControlledDocumentException(blocked);
        var now = Now();
        var v = new ControlledDocumentVerification { TenantId = d.TenantId, Id = Guid.NewGuid(), DocumentId = d.Id, Revision = revision, VesselId = d.VesselId,
            VerifierId = actor, Method = method!, Result = result!, Basis = basis!.Trim(), ContentSha256 = r.ContentSha256, CheckedAt = now };
        db.ControlledDocumentVerifications.Add(v);
        Append(db, d, "VERIFICATION_RECORDED", now, revision, basis: method + ":" + result);
        return v;
    }

    public static void PlaceHold(PoseidonDbContext db, Guid actor, ControlledDocument d, string? authorityRef, string? reason)
    {
        Actor(db, actor);
        if (ControlledDocumentPolicy.PlaceHoldBlocker(d.HoldActive, authorityRef, reason) is { } blocked) throw new ControlledDocumentException(blocked);
        var now = Now();
        d.HoldActive = true; d.HoldAuthorityRef = authorityRef!.Trim(); d.HoldPlacedBy = actor; d.HoldPlacedAt = now;
        Append(db, d, "HOLD_PLACED", now, basis: reason!.Trim());
    }

    public static void ReleaseHold(PoseidonDbContext db, Guid actor, ControlledDocument d, string? reason)
    {
        Actor(db, actor);
        if (ControlledDocumentPolicy.ReleaseHoldBlocker(d.HoldActive, d.HoldPlacedBy, actor, reason) is { } blocked) throw new ControlledDocumentException(blocked);
        var now = Now();
        d.HoldActive = false;
        Append(db, d, "HOLD_RELEASED", now, basis: reason!.Trim());
    }

    public static void SetRetention(PoseidonDbContext db, Guid actor, ControlledDocument d, string? policyRef, DateOnly? retainUntil)
    {
        Actor(db, actor);
        if (ControlledDocumentPolicy.RetentionBlocker(d.DisposalState, policyRef, retainUntil) is { } blocked) throw new ControlledDocumentException(blocked);
        var now = Now();
        d.RetentionPolicyRef = policyRef; d.RetainUntil = retainUntil;
        Append(db, d, "RETENTION_SET", now, basis: policyRef + "@" + retainUntil!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static void RequestDisposal(PoseidonDbContext db, Guid actor, ControlledDocument d, string? reason)
    {
        Actor(db, actor);
        var now = Now();
        if (ControlledDocumentPolicy.DisposalRequestBlocker(d.HoldActive, d.RetentionPolicyRef, d.RetainUntil, Today(now), d.DisposalState, reason) is { } blocked)
            throw new ControlledDocumentException(blocked);
        d.DisposalState = "REQUESTED"; d.DisposalRequestedBy = actor; d.DisposalRequestedAt = now;
        Append(db, d, "DISPOSAL_REQUESTED", now, basis: reason!.Trim());
    }

    // Approval records the disposal decision only. Physical object deletion (conditional,
    // generation-fenced, receipted) is a separate storage reconciler; nothing is deleted here.
    public static void ApproveDisposal(PoseidonDbContext db, Guid actor, ControlledDocument d, string? reason)
    {
        Actor(db, actor);
        var now = Now();
        if (ControlledDocumentPolicy.DisposalApproveBlocker(d.HoldActive, d.RetentionPolicyRef, d.RetainUntil, Today(now), d.DisposalState, d.DisposalRequestedBy, actor, reason) is { } blocked)
            throw new ControlledDocumentException(blocked);
        d.DisposalState = "APPROVED"; d.DisposalApprovedBy = actor; d.DisposalApprovedAt = now;
        Append(db, d, "DISPOSAL_APPROVED", now, basis: reason!.Trim());
    }

    // C04 adapter: link an existing domain record to the shared custody owner on the same
    // vessel. Linking transfers no verification, completion or ownership semantics.
    public static async Task LinkAsync(PoseidonDbContext db, Guid actor, ControlledDocument d, string? kind, Guid target, CancellationToken ct)
    {
        Actor(db, actor);
        if (kind == null || !ControlledDocumentPolicy.LinkKinds.Contains(kind) || target == Guid.Empty) throw new ControlledDocumentException("invalid_request");
        if (d.DisposalState == "APPROVED") throw new ControlledDocumentException("document_disposed");
        var now = Now();
        switch (kind)
        {
            case "VesselCertificate":
                if (!await db.VesselCertificates.AnyAsync(x => x.Id == target && x.VesselId == d.VesselId, ct)) throw new ControlledDocumentException("linked_resource_unavailable");
                if (await db.ControlledDocumentCertificateLinks.AnyAsync(x => x.DocumentId == d.Id && x.CertificateId == target, ct)) throw new ControlledDocumentException("duplicate_lineage");
                db.ControlledDocumentCertificateLinks.Add(new() { TenantId = d.TenantId, DocumentId = d.Id, CertificateId = target, VesselId = d.VesselId, LinkedBy = actor, LinkedAt = now });
                break;
            case "ControlEvidence":
                if (!await db.ControlEvidenceHeads.AnyAsync(x => x.Id == target && x.VesselId == d.VesselId && x.Kind == "Evidence", ct)) throw new ControlledDocumentException("linked_resource_unavailable");
                if (await db.ControlledDocumentEvidenceLinks.AnyAsync(x => x.DocumentId == d.Id && x.EvidenceId == target, ct)) throw new ControlledDocumentException("duplicate_lineage");
                db.ControlledDocumentEvidenceLinks.Add(new() { TenantId = d.TenantId, DocumentId = d.Id, EvidenceId = target, VesselId = d.VesselId, LinkedBy = actor, LinkedAt = now });
                break;
            default:
                if (!await db.Credentials.AnyAsync(x => x.Id == target && x.VesselId == d.VesselId, ct)) throw new ControlledDocumentException("linked_resource_unavailable");
                if (await db.ControlledDocumentCredentialLinks.AnyAsync(x => x.DocumentId == d.Id && x.CredentialId == target, ct)) throw new ControlledDocumentException("duplicate_lineage");
                db.ControlledDocumentCredentialLinks.Add(new() { TenantId = d.TenantId, DocumentId = d.Id, CredentialId = target, VesselId = d.VesselId, LinkedBy = actor, LinkedAt = now });
                break;
        }
        Append(db, d, "LINKED", now, basis: kind);
    }
}
