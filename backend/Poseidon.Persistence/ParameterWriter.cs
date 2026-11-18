using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence;

public sealed record ParameterWriteResult(string? Code, Guid RootId, Guid RevisionId, long Revision, uint Version, string State,
    DateTimeOffset? SelectedFrom, bool Replayed = false);

// PLAN-1511-5401 command core shared by the HTTP host and PostgreSQL tests. One SERIALIZABLE
// transaction holds the CAS check, the append-only rows, the durable receipt and the caller's
// commit-time authorization revalidation. Every failure is classified through PostgresFailure.
public static class ParameterWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string Propose = "ParameterPropose", Approve = "ParameterApprove", Reject = "ParameterReject";

    public static string Fingerprint(string action, params object?[] parts) =>
        ParameterValuePolicy.Hash(action + "\n" + JsonSerializer.Serialize(parts, Json));

    private static ParameterWriteResult Fail(string code) => new(code, Guid.Empty, Guid.Empty, 0, 0, "", null);

    private static async Task<ParameterWriteResult?> Replay(PoseidonDbContext db, Guid actor, string key, string action, string fingerprint, CancellationToken ct)
    {
        var receipt = await db.ParameterCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        if (receipt == null) return null;
        // The key is tenant-unique: another actor, route or payload never receives the stored outcome.
        if (receipt.ActorId != actor || receipt.Action != action || receipt.Fingerprint != fingerprint) return Fail("idempotency_key_reused");
        return JsonSerializer.Deserialize<ParameterWriteResult>(receipt.OutcomeJson, Json)! with { Replayed = true };
    }

    public static async Task<ParameterWriteResult> ProposeAsync(PoseidonDbContext db, Guid actor, string key, uint expectedVersion,
        ParameterDefinition definition, ParameterProposal proposal, Guid scopeId, DateTimeOffset now, Func<Task<bool>> stillAuthorized, CancellationToken ct)
    {
        var fingerprint = Fingerprint(Propose, definition.Key, definition.DefinitionVersion, expectedVersion, proposal.ScopeKind, scopeId,
            proposal.SelectorHash, proposal.ValueMode, proposal.ValueJson, proposal.EffectiveFrom, proposal.EffectiveTo, proposal.Reason);
        return await Run(db, actor, key, Propose, fingerprint, ct, async () =>
        {
            var root = await db.ParameterRoots.SingleOrDefaultAsync(x => x.Key == definition.Key && x.ScopeKind == proposal.ScopeKind &&
                x.ScopeId == scopeId && x.SelectorHash == proposal.SelectorHash, ct);
            if ((root?.Version ?? 0) != expectedVersion) return Fail("version_conflict");
            if (root?.PendingRevisionId != null) return Fail("pending_change_exists");
            if (root == null)
            {
                root = new() { TenantId = db.TenantId, Id = Guid.NewGuid(), Key = definition.Key, ScopeKind = proposal.ScopeKind, ScopeId = scopeId,
                    SelectorJson = proposal.SelectorJson, SelectorHash = proposal.SelectorHash, CreatedAt = now, Revision = 1 };
                db.ParameterRoots.Add(root);
            }
            else
            {
                db.Entry(root).Property(x => x.Version).OriginalValue = expectedVersion;
                root.Revision = checked(root.Revision + 1);
            }
            var revision = new ParameterRevision
            {
                TenantId = db.TenantId, Id = Guid.NewGuid(), RootId = root.Id, Sequence = root.Revision, Key = definition.Key,
                DefinitionVersion = definition.DefinitionVersion, ValueMode = proposal.ValueMode, ValueJson = proposal.ValueJson,
                EffectiveFrom = proposal.EffectiveFrom, EffectiveTo = proposal.EffectiveTo, Reason = proposal.Reason, AuthorId = actor,
                AuthoredAt = now, Provenance = "DeclaredUnverified",
                ContentHash = ParameterValuePolicy.Hash(JsonSerializer.Serialize(new { definition.Key, definition.DefinitionVersion, proposal.ScopeKind,
                    scopeId, proposal.SelectorJson, proposal.ValueMode, proposal.ValueJson, proposal.EffectiveFrom, proposal.EffectiveTo }, Json))
            };
            root.PendingRevisionId = revision.Id;
            db.ParameterRevisions.Add(revision);
            await db.SaveChangesAsync(ct);
            return new ParameterWriteResult(null, root.Id, revision.Id, root.Revision, root.Version, "IN_REVIEW", null);
        }, stillAuthorized, now);
    }

    public static async Task<ParameterWriteResult> DecideAsync(PoseidonDbContext db, Guid actor, string key, Guid rootId, uint expectedVersion,
        Guid revisionId, bool approve, string reason, DateTimeOffset now, Func<Task<bool>> stillAuthorized, CancellationToken ct)
    {
        var action = approve ? Approve : Reject;
        var fingerprint = Fingerprint(action, rootId, expectedVersion, revisionId, reason);
        return await Run(db, actor, key, action, fingerprint, ct, async () =>
        {
            var root = await db.ParameterRoots.SingleOrDefaultAsync(x => x.Id == rootId, ct);
            if (root == null) return Fail("resource_not_found");
            if (root.Version != expectedVersion) return Fail("version_conflict");
            if (root.PendingRevisionId != revisionId) return Fail("invalid_state");
            var revision = await db.ParameterRevisions.AsNoTracking().SingleAsync(x => x.RootId == rootId && x.Id == revisionId, ct);
            if (ParameterGovernance.DecisionViolation(revision.AuthorId, actor, revision.Provenance, approve) is { } violation) return Fail(violation);
            DateTimeOffset? selected = null;
            if (approve)
            {
                selected = ParameterValuePolicy.Micro(ParameterGovernance.SelectedFrom(revision.EffectiveFrom, now));
                if (revision.EffectiveTo is { } end && end <= selected) return Fail("outside_effective_dates");
                // Equal-precedence selections are never resolved by last-writer-wins (CFG3-T05).
                if (await db.ParameterDecisions.AnyAsync(x => x.RootId == rootId && x.Outcome == "APPROVED" && x.SelectedFrom == selected, ct))
                    return Fail("ambiguous_selection");
            }
            db.Entry(root).Property(x => x.Version).OriginalValue = expectedVersion;
            root.Revision = checked(root.Revision + 1);
            root.PendingRevisionId = null;
            db.ParameterDecisions.Add(new()
            {
                TenantId = db.TenantId, Id = Guid.NewGuid(), RootId = rootId, RevisionId = revisionId, Sequence = root.Revision,
                Outcome = approve ? "APPROVED" : "REJECTED", DeciderId = actor, DecidedAt = now, SelectedFrom = selected, Reason = reason
            });
            await db.SaveChangesAsync(ct);
            return new ParameterWriteResult(null, root.Id, revisionId, root.Revision, root.Version, approve ? "APPROVED" : "REJECTED", selected);
        }, stillAuthorized, now);
    }

    private static async Task<ParameterWriteResult> Run(PoseidonDbContext db, Guid actor, string key, string action, string fingerprint,
        CancellationToken ct, Func<Task<ParameterWriteResult>> body, Func<Task<bool>> stillAuthorized, DateTimeOffset now)
    {
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Unrelated pending work cannot join a parameter command.");
        if (await Replay(db, actor, key, action, fingerprint, ct) is { } early) return early;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await Replay(db, actor, key, action, fingerprint, ct) is { } replay) return replay;
            var result = await body();
            if (result.Code != null) { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return result; }
            db.ParameterCommandReceipts.Add(new()
            {
                TenantId = db.TenantId, Key = key, ActorId = actor, Action = action, Fingerprint = fingerprint, RootId = result.RootId,
                RevisionId = result.RevisionId, RootRevision = result.Revision, CommittedAt = now, OutcomeJson = JsonSerializer.Serialize(result, Json)
            });
            await db.SaveChangesAsync(ct);
            // Commit-time authorization: a grant revoked during the transaction rolls back everything.
            if (!await stillAuthorized()) { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return Fail("permission_denied"); }
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();
            return result;
        }
        catch (Exception error) when (error is DbUpdateConcurrencyException || PostgresFailure.IsDatabase(error))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            if (await Replay(db, actor, key, action, fingerprint, CancellationToken.None) is { } raced) return raced;
            return Fail(error is DbUpdateConcurrencyException || PostgresFailure.IsSerialization(error) ||
                PostgresFailure.Provider(error)?.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation ? "version_conflict" : "service_unavailable");
        }
    }

    // Approved selections only; IN_REVIEW and REJECTED revisions are never selectable.
    public static async Task<ParameterCandidate[]> CandidatesAsync(PoseidonDbContext db, string key, IReadOnlyCollection<ParameterScopeLevel> scopes, string selectorHash, CancellationToken ct)
    {
        var kinds = scopes.Where(s => s.Id != null).Select(s => s.Kind + ":" + s.Id!.Value.ToString("D")).ToArray();
        var rows = await (from root in db.ParameterRoots.AsNoTracking()
                          join decision in db.ParameterDecisions.AsNoTracking() on new { root.TenantId, RootId = root.Id } equals new { decision.TenantId, decision.RootId }
                          join revision in db.ParameterRevisions.AsNoTracking() on new { decision.TenantId, decision.RootId, Id = decision.RevisionId } equals new { revision.TenantId, revision.RootId, revision.Id }
                          where root.Key == key && root.SelectorHash == selectorHash && decision.Outcome == "APPROVED"
                          select new { root.ScopeKind, root.ScopeId, revision, decision.SelectedFrom, decision.DecidedAt }).ToArrayAsync(ct);
        return rows.Where(x => kinds.Contains(x.ScopeKind + ":" + x.ScopeId.ToString("D"))).Select(x => new ParameterCandidate(x.revision.Id, x.revision.RootId,
            x.revision.Sequence, x.ScopeKind, x.ScopeId, selectorHash, x.revision.ValueMode, x.revision.ValueJson, x.SelectedFrom!.Value,
            x.revision.EffectiveTo, x.DecidedAt, x.revision.ContentHash)).ToArray();
    }
}
