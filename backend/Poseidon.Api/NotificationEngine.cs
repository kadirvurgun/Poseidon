using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

internal sealed record NotificationEvaluationResult(string Status, int Created = 0, int Escalated = 0, int SourceChanged = 0,
    int OwnerUnavailable = 0, bool HasMore = false);

// No HTTP, principal, administrative context or external messaging dependency.
// Both callers establish a trusted actor/tenant/vessel boundary and supply a live fence.
public sealed class NotificationEngine(NotificationSettings settings, MaintenanceObservationSettings observations, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 4 };
    internal static string Digest(NotificationRule rule) => RegisterStorage.Hash(JsonSerializer.Serialize(rule, Json));
    internal static string SourceHash(MaintenanceCurrentProjection source) => RegisterStorage.Hash(JsonSerializer.Serialize(new {
        adapter = "notification-source-episodes-v1",
        source.Row.Id, source.Row.Revision, source.Row.Inputs.ObservationId, source.SourceObservedAt,
        source.CurrentAssetRevision, source.JobAvailable, source.OwnerEligible, source.Row.OwnerRoleId,
        source.Row.Due, source.Row.ObservationState
    }, Json));
    internal static bool? Active(NotificationRule rule, MaintenanceCurrentProjection source, Guid[] eligible)
    {
        if (!source.OwnerEligible || source.Row.OwnerRoleId == null || !eligible.Contains(source.Row.OwnerRoleId.Value)) return null;
        if (source.Row.Due.State == rule.TriggerState) return true;
        // ReviewRequired is unresolved for a Due rule; it is not proof of NotDue.
        return source.Row.Due.State is "Due" or "NotDue" ? false : null;
    }
    private static NotificationEvent Event(Notification n, string kind, Guid actor, DateTimeOffset now) => new() {
        TenantId = n.TenantId, VesselId = n.VesselId, Id = Guid.NewGuid(), NotificationId = n.Id,
        Sequence = n.Revision, Kind = kind, ActorId = actor, RecordedAt = now
    };
    private static DateTimeOffset Instant(DateTimeOffset value) {
        var utc = value.ToUniversalTime(); return utc.AddTicks(-(utc.Ticks % 10));
    }
    internal async Task<NotificationEvaluationResult> EvaluateAsync(PoseidonDbContext db, Guid vessel, Guid actor,
        Func<Guid[]> owners, Func<bool> authorized, CancellationToken ct, bool honorSchedule = false,
        int batchSize = 32, int leaseSeconds = 120, int intervalSeconds = 30)
    {
        if (db.IsAdministrativeImport || actor == Guid.Empty || vessel == Guid.Empty || db.TenantId == Guid.Empty ||
            batchSize is < 1 or > 64 || leaseSeconds is < 10 or > 600 || intervalSeconds is < 1 or > 3600 || !authorized())
            return new("Unavailable");
        var rules = settings.For(db.TenantId, vessel);
        if (rules is not { Length: > 0 }) return new("Unavailable");
        var policyHash = RegisterStorage.Hash(JsonSerializer.Serialize(rules, Json));
        var leaseOwner = Guid.NewGuid();
        IDbContextTransaction? transaction = null;
        try {
            transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var now = Instant(time.GetUtcNow());
            if (!await db.Vessels.AnyAsync(v => v.Id == vessel, ct)) return new("Unavailable");
            var dispatch = await db.NotificationDispatchStates.SingleOrDefaultAsync(s => s.VesselId == vessel, ct);
            if (dispatch?.LeaseUntil > now) return new("LeaseBusy");
            if (honorSchedule && dispatch?.NextRunAt > now) return new("NotDue");
            if (dispatch == null) {
                dispatch = new() { TenantId = db.TenantId, VesselId = vessel }; db.NotificationDispatchStates.Add(dispatch);
            } else dispatch.Revision++;
            dispatch.LeaseOwner = leaseOwner; dispatch.LeaseUntil = now.AddSeconds(leaseSeconds);
            if (!authorized()) return new("Denied");
            await db.SaveChangesAsync(ct);
            if (!authorized()) return new("Denied");
            await transaction.CommitAsync(ct);
        } catch (DbUpdateConcurrencyException) { return new("Conflict"); }
        catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return new(PostgresFailure.IsSerialization(e) || PostgresFailure.Provider(e)?.SqlState == "23505" ? "Conflict" : "StorageUnavailable");
        } finally { if (transaction != null) await transaction.DisposeAsync(); db.ChangeTracker.Clear(); }

        // Acquisition is a real attributed control-plane observation. Cancellation
        // or a failed work transaction leaves only this bounded expiring lease.
        transaction = null;
        try {
            transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var now = Instant(time.GetUtcNow());
            var dispatch = await db.NotificationDispatchStates.SingleAsync(s => s.VesselId == vessel, ct);
            if (dispatch.LeaseOwner != leaseOwner || dispatch.LeaseUntil <= now || !authorized()) return new("LeaseLost");
            var leaseDeadline = dispatch.LeaseUntil;
            var eligible = owners();
            var page = await MaintenanceCurrentReader.ReadOwned(vessel, db, eligible, observations, now, ct,
                offset: dispatch.SourceOffset, limit: batchSize + 1);
            var more = page.Length > batchSize; var sources = page.Take(batchSize).ToArray();
            var ids = sources.Select(s => s.Row.Id).ToArray();
            var unavailable = await db.NotificationEpisodes.Where(e => e.VesselId == vessel && e.Active != null &&
                !db.MaintenancePlans.Any(p => p.VesselId == vessel && p.Id == e.PlanId &&
                    db.MaintenancePlanRevisions.Any(r => r.VesselId == vessel && r.PlanId == p.Id && r.Revision == p.Revision)))
                .OrderBy(e => e.PlanId).ThenBy(e => e.RuleKey).ThenBy(e => e.PolicyDigest).Take(batchSize).ToArrayAsync(ct);
            foreach (var missing in unavailable) { missing.Active = null; missing.Revision++; }
            var episodes = await db.NotificationEpisodes.Where(e => e.VesselId == vessel && ids.Contains(e.PlanId)).Take(4097).ToArrayAsync(ct);
            var live = await db.Notifications.Where(n => n.VesselId == vessel && ids.Contains(n.PlanId) &&
                n.State != "SyntheticUnverified" && n.State != "SourceChanged").Take(4097).ToArrayAsync(ct);
            // Upgrade bootstrap reads only the latest retained basis per identity,
            // rather than materializing an ever-growing notification history.
            var bootstrap = await db.Notifications.Where(n => n.VesselId == vessel && ids.Contains(n.PlanId) && n.State != "SyntheticUnverified" &&
                !db.NotificationEpisodes.Any(e => e.VesselId == vessel && e.PlanId == n.PlanId && e.RuleKey == n.RuleKey && e.PolicyDigest == n.PolicyDigest) &&
                !db.Notifications.Any(later => later.VesselId == vessel && later.PlanId == n.PlanId && later.RuleKey == n.RuleKey && later.PolicyDigest == n.PolicyDigest &&
                    (later.Episode > n.Episode || later.Episode == n.Episode && later.CreatedAt > n.CreatedAt)))
                .Take(4097).ToArrayAsync(ct);
            if (episodes.Length > 4096 || live.Length > 4096 || bootstrap.Length > 4096) return new("CapacityUnavailable");
            var notices = live.Concat(bootstrap).DistinctBy(n => n.Id).ToArray();
            var expected = new Dictionary<(Guid Plan, string Rule, string Policy), (MaintenanceCurrentProjection Source, NotificationRule Rule, NotificationEpisode Episode)>();
            var unknown = new HashSet<(Guid Plan, string Rule, string Policy)>();
            foreach (var old in episodes.Where(e => (e.Active != false || e.LastKnownActive != false) && !rules.Any(r => r.Key == e.RuleKey && Digest(r) == e.PolicyDigest))) {
                old.Active = false; old.LastKnownActive = false; old.Revision++; // An observed configured policy withdrawal is a real boundary.
            }
            var ownerUnavailable = 0;
            foreach (var source in sources) foreach (var rule in rules) {
                var digest = Digest(rule); var hash = SourceHash(source);
                var episode = episodes.SingleOrDefault(e => e.PlanId == source.Row.Id && e.RuleKey == rule.Key && e.PolicyDigest == digest);
                var active = Active(rule, source, eligible);
                if (active == null) ownerUnavailable++;
                if (episode == null) {
                    var prior = notices.Where(n => n.PlanId == source.Row.Id && n.RuleKey == rule.Key && n.PolicyDigest == digest)
                        .OrderByDescending(n => n.Episode).ThenByDescending(n => n.CreatedAt).ThenBy(n => n.State == "SourceChanged").FirstOrDefault();
                    episode = new() { TenantId = db.TenantId, VesselId = vessel, PlanId = source.Row.Id, RuleKey = rule.Key,
                        PolicyDigest = digest, Episode = prior?.Episode ?? 0, Active = prior == null || prior.State == "SourceChanged" ? null : true,
                        // A retained retirement is a known closed notification episode,
                        // not proof that the maintenance source itself is NotDue.
                        LastKnownActive = prior == null ? null : prior.State != "SourceChanged", SourceContextHash = prior?.SourceContextHash };
                    db.NotificationEpisodes.Add(episode);
                }
                var advance = active == true && (episode.Episode == 0 || episode.LastKnownActive == false || episode.SourceContextHash != hash);
                var changed = advance || episode.Active != active || active != null && (episode.LastKnownActive != active || episode.SourceContextHash != hash);
                if (changed) {
                    if (db.Entry(episode).State != EntityState.Added) episode.Revision++;
                    if (advance) episode.Episode++;
                    episode.Active = active;
                    if (active != null) { episode.LastKnownActive = active; episode.SourceContextHash = hash; } // Preserve known facts through unavailability.
                }
                if (active == true) expected.Add((source.Row.Id, rule.Key, digest), (source, rule, episode));
                if (active == null) unknown.Add((source.Row.Id, rule.Key, digest));
            }
            var created = 0; var escalated = 0; var retired = 0;
            foreach (var notice in notices.Where(n => n.State is "Open" or "Escalated" or "Acknowledged")) {
                if (unknown.Contains((notice.PlanId, notice.RuleKey, notice.PolicyDigest))) continue;
                if (!expected.TryGetValue((notice.PlanId, notice.RuleKey, notice.PolicyDigest), out var current) ||
                    current.Episode.Episode != notice.Episode || current.Episode.SourceContextHash != notice.SourceContextHash ||
                    current.Source.Row.Revision != notice.PlanRevision || current.Source.Row.OwnerRoleId != notice.RecipientRoleId) {
                    notice.State = "SourceChanged"; notice.Revision++; db.NotificationEvents.Add(Event(notice, "SourceChanged", actor, now)); retired++; continue;
                }
                if (NotificationPolicy.EscalationDue(notice.State, notice.EscalateAt, now)) {
                    if (notice.EscalationRoleId == null || !eligible.Contains(notice.EscalationRoleId.Value)) { ownerUnavailable++; continue; }
                    notice.State = "Escalated"; notice.Revision++; db.NotificationEvents.Add(Event(notice, "Escalated", actor, now)); escalated++;
                }
            }
            foreach (var current in expected.Values) {
                var source = current.Source; var rule = current.Rule; var episode = current.Episode;
                if (notices.Any(n => n.PlanId == source.Row.Id && n.RuleKey == rule.Key && n.PolicyDigest == episode.PolicyDigest &&
                    n.Episode == episode.Episode && n.SourceContextHash == episode.SourceContextHash)) continue;
                var head = await db.MaintenancePlans.SingleAsync(p => p.VesselId == vessel && p.Id == source.Row.Id, ct);
                if (head.Revision != source.Row.Revision) return new("Conflict");
                db.Entry(head).Property(p => p.Revision).IsModified = true;
                var notice = new Notification { TenantId = db.TenantId, VesselId = vessel, Id = Guid.NewGuid(),
                    PlanId = source.Row.Id, PlanRevision = source.Row.Revision, RuleKey = rule.Key, PolicyVersion = rule.Version,
                    PolicyDigest = episode.PolicyDigest, SourceContextHash = episode.SourceContextHash!, Episode = episode.Episode,
                    SourceState = source.Row.Due.State, Severity = rule.Severity, Mandatory = NotificationPolicy.IsMandatory(rule.Severity, rule.Mandatory),
                    RecipientRoleId = source.Row.OwnerRoleId!.Value, EscalationRoleId = rule.EscalationRoleId, DueDate = source.Row.Due.NextDueDate,
                    EscalateAt = rule.EscalateAfterSeconds == null ? null : now.AddSeconds(rule.EscalateAfterSeconds.Value), CreatedAt = now };
                db.Notifications.Add(notice); db.NotificationEvents.Add(Event(notice, "Raised", actor, now)); created++;
            }
            bool Current() => authorized() && settings.For(db.TenantId, vessel) is { } current &&
                RegisterStorage.Hash(JsonSerializer.Serialize(current, Json)) == policyHash && eligible.Order().SequenceEqual(owners().Order()) &&
                dispatch.LeaseOwner == leaseOwner && dispatch.LeaseUntil > time.GetUtcNow();
            if (!Current()) return new("Denied");
            dispatch.SourceOffset = more ? checked(dispatch.SourceOffset + sources.Length) : 0;
            dispatch.LastCompletedAt = Instant(time.GetUtcNow()); dispatch.NextRunAt = more ? dispatch.LastCompletedAt : dispatch.LastCompletedAt.Value.AddSeconds(intervalSeconds);
            dispatch.LeaseOwner = null; dispatch.LeaseUntil = null; dispatch.Revision++;
            await db.SaveChangesAsync(ct);
            // The released lease is still locked by this transaction until commit.
            if (!authorized() || settings.For(db.TenantId, vessel) is not { } final || RegisterStorage.Hash(JsonSerializer.Serialize(final, Json)) != policyHash ||
                !eligible.Order().SequenceEqual(owners().Order())) return new("Denied");
            if (leaseDeadline <= time.GetUtcNow()) return new("LeaseLost");
            await transaction.CommitAsync(ct);
            return new("Completed", created, escalated, retired, ownerUnavailable, more);
        } catch (DbUpdateConcurrencyException) { return new("Conflict"); }
        catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            return new(PostgresFailure.IsSerialization(e) || PostgresFailure.Provider(e)?.SqlState == "23505" ? "Conflict" : "StorageUnavailable");
        } finally { if (transaction != null) await transaction.DisposeAsync(); db.ChangeTracker.Clear(); }
    }
}
