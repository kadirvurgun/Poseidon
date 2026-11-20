using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record DataTrustItemDto(string SubjectKind, Guid? SubjectId, string? FieldPath, long Revision, string? Title, string? Provenance, string? SourceKind,
    DateTimeOffset? ObservedAt, DateTimeOffset? FreshUntil, string Freshness, string Verification, string Disposition, DataQualitySignal[] Signals, DateTimeOffset? SavedAt);
public sealed record DataTrustCountsDto(int Missing, int Stale, int Conflicting, int Unverified, int Orphan);
public sealed record DataTrustSnapshot(string State, string Disposition, DataTrustCountsDto Counts, DataTrustItemDto[] Items, int Withheld, bool Truncated);
public sealed record DataTrustPageDto(Guid VesselId, DateTimeOffset AsOf, string State, string Disposition, string ReadinessEffect, DataTrustCountsDto Counts,
    DataTrustItemDto[] Items, DataTrustItemDto[] ProfileFields, int Offset, int Limit, int Total, bool HasMore, int Withheld, bool Truncated, string SourceVersion);

// S10 WP-A DEV-065 read contract for readiness consumers (departure readiness today; Today/tug readiness can adopt it).
// AVAILABLE + REVIEW_REQUIRED when any material signal exists; UNAVAILABLE when the actor cannot read the trust inputs.
// The snapshot never says READY: NO_MATERIAL_SIGNAL is the best state and it is not a readiness statement.
public interface IDataTrustSignalSource
{
    Task<DataTrustSnapshot> ForVessel(PoseidonDbContext db, TrustedActor actor, Guid vessel, DateTimeOffset now, CancellationToken ct);
}

internal sealed class DataTrustSignalSource : IDataTrustSignalSource
{
    public const int MaxSubjects = 500;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 10 };
    internal static bool CanRead(TrustedActor? a) => a != null && ControlEvidenceEndpoints.Read(a);
    internal static readonly DataTrustSnapshot Unavailable = new("UNAVAILABLE", DataQualityPolicy.ReviewRequired, new(0, 0, 0, 0, 0), [], 0, false);

    public async Task<DataTrustSnapshot> ForVessel(PoseidonDbContext db, TrustedActor actor, Guid vessel, DateTimeOffset now, CancellationToken ct)
    {
        if (!CanRead(actor)) return Unavailable;
        var heads = await db.ControlEvidenceHeads.AsNoTracking().Where(x => x.VesselId == vessel && (x.Kind == "Evidence" || x.Kind == "Control"))
            .OrderBy(x => x.Kind).ThenBy(x => x.Id).Take(MaxSubjects + 1).Select(x => new { x.Kind, x.Id, x.Revision, x.ControlState }).ToArrayAsync(ct);
        var truncated = heads.Length > MaxSubjects; heads = heads.Take(MaxSubjects).ToArray();
        var ids = heads.Select(x => x.Id).ToArray();
        var revisions = ids.Length == 0 ? [] : await HumanReviewEndpoints.Visible(db, vessel, actor).Where(r => ids.Contains(r.Id)).ToArrayAsync(ct);
        var evidenceIds = heads.Where(x => x.Kind == "Evidence").Select(x => x.Id).ToArray();
        var verifications = evidenceIds.Length == 0 ? [] : await db.HumanReviewEvents.AsNoTracking()
            .Where(e => e.VesselId == vessel && e.Kind == "Verification" && evidenceIds.Contains(e.EvidenceId) &&
                db.HumanReviewHeads.Any(hd => hd.VesselId == vessel && hd.Id == e.Id && hd.Revision == e.Revision))
            .Select(e => new { e.EvidenceId, e.EvidenceRevision, e.State, e.ValidUntil }).ToArrayAsync(ct);
        var edges = evidenceIds.Length == 0 ? [] : await db.ControlEvidenceEdges.AsNoTracking()
            .Where(e => e.VesselId == vessel && e.Kind == "Evidence" && evidenceIds.Contains(e.Id) && e.TargetKind == "Control").Select(e => new { e.Id, e.Revision }).ToArrayAsync(ct);
        var controlIds = heads.Where(x => x.Kind == "Control").Select(x => x.Id).ToArray();
        var pins = controlIds.Length == 0 ? [] : await db.ControlEvidenceRequirements.AsNoTracking()
            .Where(p => p.VesselId == vessel && p.Kind == "Control" && controlIds.Contains(p.Id)).Select(p => new { p.Id, p.Revision }).ToArrayAsync(ct);
        var items = new List<DataTrustItemDto>(); var withheld = 0;
        foreach (var head in heads)
        {
            var r = revisions.FirstOrDefault(x => x.Kind == head.Kind && x.Id == head.Id && x.Revision == head.Revision);
            if (r == null) { withheld++; continue; } // not readable by this actor (tug/file/owning scope): counted, never shown
            ControlEvidenceContent content;
            try { content = JsonSerializer.Deserialize<ControlEvidenceContent>(r.ContentJson, Json)!; }
            catch (JsonException) { withheld++; continue; }
            DataQualityInput input;
            if (head.Kind == "Evidence")
            {
                var missing = new List<string>();
                if (content.Title == null) missing.Add("TITLE_MISSING");
                if (content.OwnerId == null) missing.Add("OWNER_MISSING");
                if (r.FileId == null) missing.Add("CLEAN_FILE_MISSING");
                if (content.Provenance == null) missing.Add("PROVENANCE_MISSING");
                if (content.ObservedAt == null) missing.Add("OBSERVATION_TIME_MISSING");
                var orphan = edges.Any(e => e.Id == head.Id && e.Revision == head.Revision) ? Array.Empty<string>() : ["CONTROL_LINK_MISSING"];
                var current = verifications.Where(v => v.EvidenceId == head.Id && v.EvidenceRevision == head.Revision).ToArray();
                var verification = current.Any(v => v.State == "VERIFIED" && v.ValidUntil > now) ? "VERIFIED" :
                    current.Any(v => v.State is "VERIFIED" or "EXPIRED") ? "VERIFICATION_EXPIRED" :
                    current.Any(v => v.State == "REJECTED") ? "VERIFICATION_REJECTED" : "UNVERIFIED";
                input = new("Evidence", DataQualityPolicy.Freshness(content.ObservedAt, content.FreshUntil, now), ControlEvidencePolicy.TimeState(content, now, false),
                    missing.ToArray(), orphan, verification, [], false);
            }
            else
            {
                var missing = new List<string>();
                if (content.Title == null) missing.Add("TITLE_MISSING");
                if (content.Frequency == null) missing.Add("FREQUENCY_UNSPECIFIED");
                if (content.EvidencePolicy == null) missing.Add("EVIDENCE_POLICY_MISSING");
                if (content.VerificationPolicy == null) missing.Add("VERIFICATION_POLICY_MISSING");
                var orphan = head.ControlState == "RETIRED" ? [] :
                    ControlEvidencePolicy.OrphanReasons(content.Critical, pins.Any(p => p.Id == head.Id && p.Revision == head.Revision), content.OwnerId != null);
                input = new("Control", SourceFreshness.Current, null, missing.ToArray(), orphan, "UNKNOWN", [], false);
            }
            var assessment = DataQualityPolicy.Assess(input);
            items.Add(new(head.Kind, head.Id, null, head.Revision, content.Title, content.Provenance, null, content.ObservedAt, content.FreshUntil,
                input.Freshness.ToString().ToUpperInvariant(), input.VerificationState, assessment.Disposition, assessment.Signals, r.SavedAt));
        }
        // Profile field source declarations (DEV-020): several sources pinning different values of one field = CONFLICTING.
        // A declared source is never verification and no freshness policy is configured, so these stay material.
        var declarations = await db.VesselProfileFieldSources.AsNoTracking().Where(s => s.VesselId == vessel)
            .OrderBy(s => s.FieldPath).ThenByDescending(s => s.Sequence).Take(1024).ToArrayAsync(ct);
        foreach (var field in declarations.GroupBy(s => s.FieldPath, StringComparer.Ordinal))
        {
            var latestPerSource = field.GroupBy(s => s.SourceKind, StringComparer.Ordinal).Select(g => g.First()).ToArray();
            var conflicts = DataQualityPolicy.Conflicts(latestPerSource.Select(s => (field.Key, s.SourceKind, (string?)s.ValueHash)));
            var latest = field.First();
            var freshness = DataQualityPolicy.Freshness(latest.ObservedAt, null, now);
            var assessment = DataQualityPolicy.Assess(new("ProfileField", freshness, null, latest.ObservedAtState == "Known" ? [] : ["OBSERVATION_TIME_MISSING"], [], "UNVERIFIED",
                conflicts.Length == 0 ? [] : ["SOURCES_DISAGREE"], false));
            items.Add(new("ProfileField", null, field.Key, latest.ValueRevision, null, latest.Provenance, latest.SourceKind, latest.ObservedAt, null,
                freshness.ToString().ToUpperInvariant(), "UNVERIFIED", assessment.Disposition, assessment.Signals, latest.RecordedAt));
        }
        var all = items.SelectMany(i => i.Signals).ToArray();
        var counts = new DataTrustCountsDto(Count(DataQualityPolicy.Missing), Count(DataQualityPolicy.Stale), Count(DataQualityPolicy.Conflicting),
            Count(DataQualityPolicy.Unverified), Count(DataQualityPolicy.Orphan));
        int Count(string kind) => items.Count(i => i.Signals.Any(s => s.Kind == kind && s.Material));
        // Truncated or withheld input can hide a material signal: the vessel summary is never better than REVIEW_REQUIRED then.
        var disposition = all.Any(s => s.Material) || truncated || withheld > 0 ? DataQualityPolicy.ReviewRequired : DataQualityPolicy.NoMaterialSignal;
        return new("AVAILABLE", disposition, counts, items.ToArray(), withheld, truncated);
    }
}

// GET /api/v1/data-trust?vesselId=&offset=&limit= (route data-trust-center). Paged (max 100) over control/evidence subjects;
// profile field rows are returned separately. Read-only; the summary is computed over all subjects, not just the page.
public static class DataTrustEndpoints
{
    public static void MapDataTrust(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/data-trust", Page).RequireAuthorization("DirectoryRead").WithName("GetDataTrust")
            .Produces<DataTrustPageDto>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(503);

    private static async Task<IResult> Page(HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store"; var ct = h.RequestAborted;
        var a = SafetyEventCommands.Actor(h);
        if (!DataTrustSignalSource.CanRead(a)) return await AssuranceCommands.Fail(h, "DataTrustRead", 403, "permission_denied");
        if (!SafetyEventCommands.Query(h, out var vessel, "offset", "limit")) return await AssuranceCommands.Fail(h, "DataTrustRead", 400, "invalid_query");
        int offset = 0, limit = 50;
        if (h.Request.Query.ContainsKey("offset") && (!int.TryParse(h.Request.Query["offset"], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > 100000) ||
            h.Request.Query.ContainsKey("limit") && (!int.TryParse(h.Request.Query["limit"], NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > DataQualityPolicy.MaxPage))
            return await AssuranceCommands.Fail(h, "DataTrustRead", 400, "invalid_query");
        if (!await SafetyEventCommands.Vessels(db, a!, vessel).AnyAsync(ct)) return await AssuranceCommands.Fail(h, "DataTrustRead", 404, "resource_not_found");
        var now = DateTimeOffset.UtcNow;
        DataTrustSnapshot snapshot;
        try { snapshot = await h.RequestServices.GetRequiredService<IDataTrustSignalSource>().ForVessel(db, a!, vessel, now, ct); }
        catch (Exception ex) when (PostgresFailure.IsDatabase(ex)) { return await AssuranceCommands.Fail(h, "DataTrustRead", 503, "service_unavailable"); }
        var subjects = snapshot.Items.Where(i => i.SubjectKind != "ProfileField").ToArray();
        var page = subjects.Skip(offset).Take(limit).ToArray();
        await SafetyEventCommands.Audit(h, "DataTrustRead", "Success", vessel);
        return Results.Json(new DataTrustPageDto(vessel, now, snapshot.State, snapshot.Disposition,
            snapshot.Disposition == DataQualityPolicy.ReviewRequired ? "READINESS_FLOOR_REVIEW_REQUIRED" : "NO_READINESS_EFFECT", snapshot.Counts, page,
            snapshot.Items.Where(i => i.SubjectKind == "ProfileField").ToArray(), offset, limit, subjects.Length, offset + page.Length < subjects.Length,
            snapshot.Withheld, snapshot.Truncated, DataQualityPolicy.SourceVersion), AssuranceCommands.Json);
    }
}
