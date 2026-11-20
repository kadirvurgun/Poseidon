using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Server-provisioned appointment of one human as pilot Product Owner of the tenant (18.01 row 76 DEV-075 "Pilot
// go/no-go review"; brief: "Product Owner exact authority"). Never derived from a request field, IdP role name,
// tenant-wide manage or administrator; AI, automation and service principals never hold it.
public sealed record PilotGateAuthority(Guid Id, Guid RoleId, string Duty, string AppointmentSource, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
public sealed record PilotGateAuthorityDto(Guid Id, string Duty);

internal static class PilotAccess
{
    private static bool Tenant(TrustedActor? a) => a != null && a.CanRead && (a.TenantWideRead || a.TenantWideManage);
    internal static bool UatRead(TrustedActor? a) => Tenant(a) && a!.Permissions.Contains("uat.read");
    internal static bool UatRecord(TrustedActor? a) => UatRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("uat.record");
    internal static bool UatThreshold(TrustedActor? a) => UatRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("uat.threshold.record");
    internal static bool UatAttest(TrustedActor? a) => UatRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("uat.attest");
    internal static bool GateRead(TrustedActor? a) => Tenant(a) && a!.Permissions.Contains("pilot.gate.read");
    internal static bool SupportRecord(TrustedActor? a) => GateRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("pilot.gate.support.record");
    // The permission is necessary, never sufficient: an exact current Product Owner appointment is also required.
    internal static bool Decide(TrustedActor? a) => GateRead(a) && a!.HasWriteAttribution && a.Permissions.Contains("pilot.gate.decide");

    // UAT outcomes, attestations and support marks are human observations: exactly one membership whose ActorKind is
    // exact ordinal "Human". An automation (e.g. a Playwright runner) or AI principal cannot record them.
    internal static bool Human(FederationSettings settings, TrustedActor a) {
        var members = settings.Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        return members.Length == 1 && string.Equals(members[0].ActorKind, "Human", StringComparison.Ordinal);
    }

    internal static PilotGateAuthority[] Resolve(FederationSettings settings, TrustedActor? actor, DateTimeOffset now) {
        if (actor == null || !Decide(actor) || actor.RoleContext == null || !Human(settings, actor)) return [];
        var grants = settings.Memberships.Single(m => m.ActorId == actor.Id && m.TenantId == actor.TenantId).PilotGateAuthorities ?? [];
        if (grants.Length > 64) return [];
        var roles = actor.RoleContext.Split(',');
        return grants.Where(g => g != null && g.Id != Guid.Empty && grants.Count(o => o != null && o.Id == g.Id) == 1 &&
            string.Equals(g.Duty, PilotGatePolicy.ProductOwnerDuty, StringComparison.Ordinal) && g.RoleId != Guid.Empty && roles.Contains(g.RoleId.ToString("D")) &&
            g.AppointmentSource is { Length: > 0 and <= 200 } source && source.Trim() == source && !source.Any(char.IsControl) &&
            g.ValidUntil > g.ValidFrom && g.ValidFrom <= now && now < g.ValidUntil).OrderBy(g => g.Id).ToArray();
    }
    internal static PilotGateAuthorityDto[] Dto(PilotGateAuthority[] grants) => grants.Select(g => new PilotGateAuthorityDto(g.Id, g.Duty)).ToArray();
    internal static string Hash(PilotGateAuthority grant, TrustedActor actor) =>
        RegisterStorage.Hash(JsonSerializer.Serialize(new { grant, actorId = actor.Id, tenantId = actor.TenantId, actorKind = "Human" }));
}

// DEV-075 gate reader contract. Each gate has exactly one registered source; none or several -> UNKNOWN (engine).
// Owning work packages replace a DeferredPilotGateSource registration with their real reader at integration.
public interface IPilotGateSource
{
    string Gate { get; }
    Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct);
}

// Open P0 count for the review. The default counts open UAT BLOCKER findings (19.01 p276-277); an external defect
// tracker can replace it. null = unknown -> NO-GO recommendation.
public interface IPilotP0Source
{
    Task<int?> OpenP0Async(PoseidonDbContext db, CancellationToken ct);
}

// A gate whose owning source is not integrated on this branch: DEFERRED with an explicit reason, never PASS.
public sealed class DeferredPilotGateSource(string gate, string reason) : IPilotGateSource
{
    public string Gate { get; } = gate;
    public Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(new PilotGateInput(Gate, PilotGatePolicy.Deferred, [reason], "DeferredSource"));

    // S10 PR #48 (DEV-068 security gate, DEV-066/092 offline), S11 WP-A/B/C and DEV-115 owners register real readers.
    public static IPilotGateSource[] Defaults() => [
        new DeferredPilotGateSource(PilotGatePolicy.Security, "SECURITY_GATE_SOURCE_NOT_INTEGRATED:DEV-068"),
        new DeferredPilotGateSource(PilotGatePolicy.Regulatory, "REGULATORY_REVIEW_SOURCE_NOT_INTEGRATED:DEV-073-093"),
        new DeferredPilotGateSource(PilotGatePolicy.Offline, "OFFLINE_CHAOS_SOURCE_NOT_INTEGRATED:DEV-066-092"),
        new DeferredPilotGateSource(PilotGatePolicy.Dr, "DR_REHEARSAL_SOURCE_NOT_INTEGRATED:DEV-071"),
        new DeferredPilotGateSource(PilotGatePolicy.Data, "DATA_RECONCILIATION_SOURCE_NOT_INTEGRATED:DEV-070"),
        new DeferredPilotGateSource(PilotGatePolicy.Trace, "TRACE_ORPHAN_SOURCE_NOT_INTEGRATED:DEV-115"),
    ];
}

public sealed record UatThresholdDto(Guid Id, string Scope, long Sequence, Guid? PreviousThresholdId, int MinScenarioPassPercent, int MaxOpenMajorFindings,
    bool RequireDesignConformance, string AgreementReference, string Rationale, Guid ActorId, DateTimeOffset RecordedAt);
public sealed record UatRunDto(Guid Id, string Scope, string Role, string Environment, string BuildCommit, string ParticipantPseudonym, string CatalogVersion,
    string State, Guid FacilitatorActorId, DateTimeOffset CreatedAt, DateTimeOffset? SubmittedAt, Guid? AttestedBy, DateTimeOffset? AttestedAt,
    string? AttestationStatement, string Provenance, uint Version);
public sealed record UatResultDto(Guid Id, Guid RunId, string ScenarioId, int Attempt, string Outcome, int? DurationSeconds, bool Assisted,
    string? AutomatedEvidenceReference, string? Note, Guid RecordedBy, DateTimeOffset RecordedAt, string EvidenceRule = "AutomatedEvidenceIsAttachmentOnly");
public sealed record UatFindingDto(Guid Id, Guid RunId, string Scope, string ScenarioId, string Severity, bool IsUx, bool DesignConformance, string Title, string State,
    Guid ReportedBy, DateTimeOffset ReportedAt, Guid? ResolvedBy, DateTimeOffset? ResolvedAt, string? Resolution, string? ResolutionReference, uint Version);

// Server-side UAT read model shared by the UAT summary endpoint and the pilot gate UAT sources.
internal static class UatReader
{
    internal static UatThresholdDto Dto(UatThreshold t) => new(t.Id, t.Scope, t.Sequence, t.PreviousThresholdId, t.MinScenarioPassPercent, t.MaxOpenMajorFindings,
        t.RequireDesignConformance, t.AgreementReference, t.Rationale, t.ActorId, t.RecordedAt);
    internal static UatRunDto Dto(UatRun r) => new(r.Id, r.Scope, r.Role, r.Environment, r.BuildCommit, r.ParticipantPseudonym, r.CatalogVersion, r.State,
        r.FacilitatorActorId, r.CreatedAt, r.SubmittedAt, r.AttestedBy, r.AttestedAt, r.AttestationStatement, r.Provenance, r.Version);
    internal static UatResultDto Dto(UatStepResult x) => new(x.Id, x.RunId, x.ScenarioId, x.Attempt, x.Outcome, x.DurationSeconds, x.Assisted,
        x.AutomatedEvidenceReference, x.Note, x.RecordedBy, x.RecordedAt);
    internal static UatFindingDto Dto(UatFinding f) => new(f.Id, f.RunId, f.Scope, f.ScenarioId, f.Severity, f.IsUx, f.DesignConformance, f.Title, f.State,
        f.ReportedBy, f.ReportedAt, f.ResolvedBy, f.ResolvedAt, f.Resolution, f.ResolutionReference, f.Version);

    internal sealed record Snapshot(UatThreshold? Threshold, UatRun[] Runs, UatFinding[] Findings, UatGateOutcome Gate);

    internal static async Task<Snapshot> Evaluate(PoseidonDbContext db, string scope, CancellationToken ct) {
        var threshold = await db.UatThresholds.AsNoTracking().Where(t => t.Scope == scope).OrderByDescending(t => t.Sequence).FirstOrDefaultAsync(ct);
        var runs = await db.UatRuns.AsNoTracking().Where(r => r.Scope == scope && r.CatalogVersion == UatScenarioCatalog.Version)
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Take(UatPolicy.MaxRunsEvaluated + 1).ToArrayAsync(ct);
        var findings = await db.UatFindings.AsNoTracking().Where(f => f.Scope == scope).OrderBy(f => f.ReportedAt).ThenBy(f => f.Id)
            .Take(UatPolicy.MaxFindingsEvaluated + 1).ToArrayAsync(ct);
        var input = threshold == null ? null : new UatThresholdInput(threshold.MinScenarioPassPercent, threshold.MaxOpenMajorFindings, threshold.RequireDesignConformance);
        if (runs.Length > UatPolicy.MaxRunsEvaluated || findings.Length > UatPolicy.MaxFindingsEvaluated)
            return new(threshold, runs.Take(UatPolicy.MaxRunsEvaluated).ToArray(), findings.Take(UatPolicy.MaxFindingsEvaluated).ToArray(),
                new UatGateOutcome(scope, UatPolicy.EvidenceIncomplete, PilotGatePolicy.Unknown, ["UAT_PROJECTION_LIMIT_EXCEEDED"], [], 0, 0, 0, 0, 0));
        var ids = runs.Where(r => r.State == UatPolicy.RunAttested).Select(r => r.Id).ToArray();
        var results = await db.UatStepResults.AsNoTracking().Where(x => ids.Contains(x.RunId)).Select(x => new { x.RunId, x.ScenarioId, x.Attempt, x.Outcome }).ToArrayAsync(ct);
        var latest = results.GroupBy(x => x.RunId).ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g.GroupBy(x => x.ScenarioId)
            .ToDictionary(s => s.Key, s => s.OrderByDescending(x => x.Attempt).First().Outcome, StringComparer.Ordinal));
        var inputs = runs.Select(r => new UatRunInput(r.Id, r.Role, r.State,
            latest.TryGetValue(r.Id, out var outcomes) ? outcomes : new Dictionary<string, string>(StringComparer.Ordinal))).ToArray();
        var gate = UatPolicy.Evaluate(scope, input, inputs, findings.Select(f => new UatFindingInput(f.Id, f.ScenarioId, f.Severity, f.DesignConformance, f.State)).ToArray());
        return new(threshold, runs, findings, gate);
    }
}

public sealed class UatPilotGateSource(string scope) : IPilotGateSource
{
    public string Gate { get; } = scope == UatScenarioCatalog.Ship ? PilotGatePolicy.UatShip : PilotGatePolicy.UatTug;
    public async Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) {
        var s = await UatReader.Evaluate(db, scope, ct);
        return new(Gate, s.Gate.GateState, [.. s.Gate.Reasons.Take(255), "UAT_STATE:" + s.Gate.State], "UatRecords");
    }
}

public sealed class SupportPilotGateSource : IPilotGateSource
{
    public string Gate => PilotGatePolicy.Support;
    public async Task<PilotGateInput> ReadAsync(PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) {
        var rows = await PilotGateReviewer.LatestSupport(db, ct);
        return PilotGatePolicy.SupportGate(rows.ToDictionary(r => r.Item, r => r.State, StringComparer.Ordinal));
    }
}

public sealed class UatBlockerP0Source : IPilotP0Source
{
    public async Task<int?> OpenP0Async(PoseidonDbContext db, CancellationToken ct) =>
        await db.UatFindings.AsNoTracking().CountAsync(f => f.Severity == UatPolicy.Blocker && f.State == UatPolicy.Open, ct);
}

internal static class PilotGateReviewer
{
    internal static async Task<PilotSupportCheck[]> LatestSupport(PoseidonDbContext db, CancellationToken ct) {
        var latest = new List<PilotSupportCheck>();
        foreach (var item in PilotGatePolicy.SupportItems)
            if (await db.PilotSupportChecks.AsNoTracking().Where(c => c.Item == item).OrderByDescending(c => c.Sequence).FirstOrDefaultAsync(ct) is { } row) latest.Add(row);
        return latest.ToArray();
    }

    internal static async Task<PilotGateReviewOutcome> Review(HttpContext h, PoseidonDbContext db, DateTimeOffset now, CancellationToken ct) {
        var inputs = new List<PilotGateInput>();
        foreach (var source in h.RequestServices.GetServices<IPilotGateSource>()) {
            try { inputs.Add(await source.ReadAsync(db, now, ct)); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or JsonException) {
                inputs.Add(new(source.Gate, PilotGatePolicy.Unknown, ["GATE_SOURCE_UNAVAILABLE"], "None"));
            }
        }
        var p0 = await h.RequestServices.GetRequiredService<IPilotP0Source>().OpenP0Async(db, ct);
        return PilotGatePolicy.Evaluate(inputs, p0);
    }

    internal static string Snapshot(PilotGateReviewOutcome o) => JsonSerializer.Serialize(new {
        o.Recommendation, o.OpenP0, gates = o.Gates.Select(g => new { g.Gate, g.State, g.Source, reasons = g.Reasons.Take(64) }), noGo = o.NoGoReasons });
}
