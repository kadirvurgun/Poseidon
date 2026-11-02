using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// Provisioned server-side for an exact source/type. No client claims, wildcard or admin fallback.
public sealed record RegulatorySourceContract(string SourceLocator, string Type, string? RequiredMechanism = null);
public sealed record RegulatoryAuthority(Guid Id, Guid RuleId, Guid SourceId, long SourceRevision, Guid RoleId,
    string[] Actions, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil, RegulatorySourceContract Contract, Guid? VesselId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record QualificationCommand([property: JsonRequired] uint ExpectedVersion, long RuleRevision, Guid AuthorityId,
    string State, QualificationContent? Content = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactAttestationCommand(Guid AuthorityId, Guid VesselId, long ProfileRevision, uint ProfileVersion, long QualificationRevision, uint QualificationVersion,
    string[] Fields, DateTimeOffset ValidUntil);
public sealed record QualificationDto(Guid RuleId, long Revision, uint Version, long RuleRevision, string State,
    QualificationContent Content, string SourceHash, string Hash, string? PriorHash, string PolicyVersion,
    Guid ActorId, Guid RoleId, Guid AuthorityId, string AuthorityHash, RegulatorySourceContract? Contract, DateTimeOffset SavedAt, string[] Blockers);

public static class RegulatoryQualificationEndpoints
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, MaxDepth = 16 };
    private static TrustedActor? Actor(HttpContext h) => h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User);
    private static RegulatoryAuthority? Grant(HttpContext h, TrustedActor a, Guid rule, string action, Guid? vessel = null)
    {
        var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var memberships = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.ActorId == a.Id && m.TenantId == a.TenantId).ToArray();
        if (!a.CanReadRegulatory || !a.HasWriteAttribution || !a.Permissions.Contains(action == "attest" ? "regulatory.facts.attest" : "regulatory.qualify") ||
            memberships.Length != 1 || memberships[0].ActorKind != "Human") return null;
        var grants = memberships[0].RegulatoryAuthorities;
        var candidates = grants.Where(g => g.Id != Guid.Empty && grants.Count(x => x.Id == g.Id) == 1 && g.RuleId == rule && g.SourceId != Guid.Empty && g.SourceRevision > 0 &&
            g.RoleId != Guid.Empty && a.RoleContext!.Split(',').Contains(g.RoleId.ToString("D")) && g.Actions.Contains(action) &&
            g.ValidFrom <= now && now < g.ValidUntil && g.Contract != null && !string.IsNullOrWhiteSpace(g.Contract.SourceLocator) && !string.IsNullOrWhiteSpace(g.Contract.Type) &&
            (action == "attest" ? vessel != null && g.VesselId == vessel && a.CanEditVessel(vessel.Value) : g.VesselId == null)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static string GrantHash(RegulatoryAuthority g, TrustedActor a) => RegisterStorage.Hash(JsonSerializer.Serialize(new { g, a.Id, a.TenantId }, Json));
    internal static QualificationContent Content(RegulatoryQualificationEvent e) => JsonSerializer.Deserialize<QualificationContent>(e.ContentJson, Json)!;
    private static QualificationDto Dto(RegulatoryQualificationEvent e, uint version) => new(e.RuleId, e.Revision, version, e.RuleRevision, e.State,
        Content(e) with { SourceBytesBase64 = null }, e.SourceHash, e.Hash, e.PriorHash, e.PolicyVersion, e.ActorId, e.RoleId, e.AuthorityId, e.AuthorityHash,
        JsonSerializer.Deserialize<RegulatorySourceContract>(e.ContractJson, Json)!, e.SavedAt, RegulatoryQualificationPolicy.Missing(Content(e)));
    private static async Task<IResult> Fail(HttpContext h, int status, string code) {
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "RegulatoryQualification", code);
        return Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
    public static void MapRegulatoryQualifications(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/regulatory-source-governance").RequireAuthorization("DirectoryRead");
        g.MapGet("/{rule:guid}", Read);
        g.MapGet("/{rule:guid}/revisions/{revision:long}/source", SourceBytes);
        g.MapPost("/{rule:guid}/events", (Guid rule, HttpContext h) => Save(rule, h, false)).Accepts<QualificationCommand>("application/json");
        g.MapPost("/{rule:guid}/fact-attestations", (Guid rule, HttpContext h) => Save(rule, h, true)).Accepts<FactAttestationCommand>("application/json");
    }
    private static async Task<IResult> SourceBytes(Guid rule, long revision, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h);
        if (a == null || !new[] { "draft", "review", "qualify" }.Any(action => Grant(h, a, rule, action) != null)) return await Fail(h, 403, "human_authority_required");
        if (revision <= 0 || h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        var row = await db.RegulatoryQualificationEvents.AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == rule && x.Revision == revision, h.RequestAborted);
        if (row == null || Content(row).SourceBytesBase64 == null) return await Fail(h, 404, "resource_not_found");
        var r = await db.RuleRevisions.AsNoTracking().SingleAsync(x => x.Id == rule && x.Revision == row.RuleRevision, h.RequestAborted);
        var grant = new[] { "draft", "review", "qualify" }.Select(action => Grant(h, a, rule, action)).FirstOrDefault(g => g?.SourceId == r.SourceId && g?.SourceRevision == r.SourceRevision);
        if (grant == null) return await Fail(h, 403, "source_authority_mismatch");
        var bytes = Convert.FromBase64String(Content(row).SourceBytesBase64!);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != row.SourceHash) return await Fail(h, 503, "source_integrity_unavailable");
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "RegulatorySourceBytesRead", "Success", resourceId: rule);
        h.Response.Headers.CacheControl = "no-store";
        return Results.File(bytes, "application/octet-stream", $"source-{rule:D}-{revision}.bin");
    }
    private static async Task<IResult> Read(Guid rule, HttpContext h, PoseidonDbContext db)
    {
        var a = Actor(h); if (a?.CanReadRegulatory != true || !a.HasWriteAttribution || !a.Permissions.Contains("applicability.read")) return await Fail(h, 403, "permission_denied");
        Guid? vessel = null; var before = long.MaxValue;
        if (h.Request.Query.Any(q => q.Value.Count != 1 || q.Key is not ("vesselId" or "before"))) return await Fail(h, 400, "invalid_query");
        if (h.Request.Query.ContainsKey("before") && (!long.TryParse(h.Request.Query["before"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out before) || before <= 0 || before > 9007199254740991)) return await Fail(h, 400, "invalid_query");
        if (h.Request.Query.ContainsKey("vesselId")) {
            if (!Guid.TryParseExact(h.Request.Query["vesselId"], "D", out var v)) return await Fail(h, 400, "invalid_query");
            if (!await a.Scope(db.Vessels).AnyAsync(x => x.Id == v, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
            vessel = v;
        }
        if (!await db.RuleDrafts.AnyAsync(x => x.Id == rule, h.RequestAborted)) return await Fail(h, 404, "resource_not_found");
        var head = await db.RegulatoryQualificationHeads.AsNoTracking().SingleOrDefaultAsync(x => x.RuleId == rule, h.RequestAborted);
        var events = await db.RegulatoryQualificationEvents.AsNoTracking().Where(x => x.RuleId == rule && x.Revision < before).OrderByDescending(x => x.Revision).Take(101).ToArrayAsync(h.RequestAborted);
        await h.RequestServices.GetRequiredService<SecurityAudit>().WriteAsync(h, "RegulatoryQualificationRead", "Success", resourceId: rule);
        var actions = new[] { "draft", "review", "qualify", "attest" }.Where(action => Grant(h, a, rule, action, action == "attest" ? vessel : null) != null).ToArray();
        var now = DateTimeOffset.UtcNow;
        var owners = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.TenantId == a.TenantId && m.ActorKind == "Human" && m.ActorId != Guid.Empty && ActorResolver.TrustedRoleContext(m.RoleReferences) != null)
            .SelectMany(m => m.RegulatoryAuthorities.Where(g => g.RuleId == rule && g.Actions.Contains("own") && g.ValidFrom <= now && now < g.ValidUntil && m.RoleReferences!.Contains(g.RoleId.ToString("D"))).Select(g => new { actorId = m.ActorId, roleId = g.RoleId })).Distinct().Take(100).ToArray();
        return Results.Ok(new { items = events.Take(100).Select(e => Dto(e, head!.Version)), hasMore = events.Length > 100, actions, owners,
            authorities = new[] { "draft", "review", "qualify", "attest" }.Select(action => new { action, authorityId = Grant(h, a, rule, action, action == "attest" ? vessel : null)?.Id }).Where(x => x.authorityId != null).ToArray() });
    }
    private static async Task<IResult> Save(Guid rule, HttpContext h, bool attest)
    {
        var a = Actor(h); var ct = h.RequestAborted;
        if (a?.CanReadRegulatory != true) return await Fail(h, 403, "permission_denied");
        if (h.Request.Query.Count != 0) return await Fail(h, 400, "invalid_query");
        try { await h.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(h); }
        catch (AntiforgeryValidationException) { return await Fail(h, 403, "csrf_invalid"); }
        var keys = h.Request.Headers["Idempotency-Key"]; var key = keys.Count == 1 ? keys[0] : null;
        if (key == null || key.Length is < 16 or > 80 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return await Fail(h, 400, "invalid_idempotency_key");
        QualificationCommand? cmd = null; FactAttestationCommand? fact = null; string body;
        try {
            if (!h.Request.HasJsonContentType()) throw new JsonException();
            using var b = new MemoryStream(); await SecureFilePolicy.CopyAsync(h.Request.Body, b, 3145728, ct);
            using var doc = JsonDocument.Parse(b.ToArray(), new() { MaxDepth = 16 }); ApplicabilityEngine.RejectDuplicateKeys(doc.RootElement); body = doc.RootElement.GetRawText();
            if (attest) {
                fact = JsonSerializer.Deserialize<FactAttestationCommand>(body, Json) ?? throw new JsonException();
                if (fact.AuthorityId == Guid.Empty || fact.VesselId == Guid.Empty || fact.ProfileRevision <= 0 || fact.ProfileVersion == 0 || fact.QualificationRevision <= 0 || fact.QualificationVersion == 0 ||
                    !doc.RootElement.TryGetProperty("validUntil", out var until) || until.ValueKind != JsonValueKind.String || !System.Text.RegularExpressions.Regex.IsMatch(until.GetString()!, @"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})\z")) throw new JsonException();
            }
            else { cmd = JsonSerializer.Deserialize<QualificationCommand>(body, Json) ?? throw new JsonException(); if (cmd.Content != null) RegulatoryQualificationPolicy.Validate(cmd.Content); if (cmd.RuleRevision <= 0 || cmd.AuthorityId == Guid.Empty || cmd.State is not ("DRAFT" or "IN_REVIEW" or "ADOPTED" or "AWAITING" or "FUTURE_EFFECTIVE" or "IN_FORCE" or "SUPERSEDED")) throw new JsonException(); }
        } catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException) { return await Fail(h, 400, "invalid_request"); }
        var action = attest ? "attest" : cmd!.State == "DRAFT" ? "draft" : cmd.State == "IN_REVIEW" ? "review" : "qualify";
        RegulatoryAuthority? Live() { var actor = Actor(h); return actor?.Id == a.Id && actor.TenantId == a.TenantId ? Grant(h, actor, rule, action, fact?.VesselId) : null; }
        var grant = Live(); if (grant == null || grant.Id != (cmd?.AuthorityId ?? fact!.AuthorityId)) return await Fail(h, 403, "human_authority_required");
        var authorityHash = GrantHash(grant, a);
        var db = h.RequestServices.GetRequiredService<PoseidonDbContext>();
        if (fact != null && !await a.Scope(db.Vessels).AnyAsync(x => x.Id == fact.VesselId, ct)) return await Fail(h, 404, "resource_not_found");
        var fingerprint = RegisterStorage.Hash(JsonSerializer.Serialize(new { rule, attest, body }, Json));
        async Task<IResult?> Replay() {
            var live = Live(); if (live == null || GrantHash(live, a) != authorityHash) return await Fail(h, 403, "human_authority_required");
            var r = await db.RegulatoryQualificationReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorId == a.Id && x.Key == key, ct);
            if (r == null) return null;
            if (r.AuthorityHash != authorityHash) return await Fail(h, 403, "human_authority_required");
            if (r.Fingerprint != fingerprint) return await Fail(h, 409, "idempotency_key_reused");
            if (fact != null && !await db.RegulatoryDrafts.AnyAsync(x => x.VesselId == fact.VesselId && x.Revision == fact.ProfileRevision, ct)) return await Fail(h, 409, "profile_context_conflict");
            return Results.Content(r.OutcomeJson, "application/json");
        }
        var replay = await Replay(); if (replay != null) return replay;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        try {
            var now = h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(); now = now.AddTicks(-(now.Ticks % 10));
            var ruleHead = await db.RuleDrafts.SingleOrDefaultAsync(x => x.Id == rule, ct);
            if (ruleHead == null) return await Fail(h, 404, "resource_not_found");
            var r = await db.RuleRevisions.AsNoTracking().SingleAsync(x => x.Id == rule && x.Revision == ruleHead.Revision, ct);
            if (r.SourceId != grant.SourceId || r.SourceRevision != grant.SourceRevision) return await Fail(h, 403, "source_authority_mismatch");
            // Serialize every no-op with the exact mutable input heads and their xmin.
            db.Entry(ruleHead).Property(x => x.Revision).IsModified = true;
            foreach (var (kind, id, revision) in new[] { ("Source", r.SourceId, r.SourceRevision), ("Requirement", r.RequirementId, r.RequirementRevision) }) {
                var input = await db.RegisterDrafts.SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id, ct);
                if (input == null || input.Revision != revision) return await Fail(h, 409, "source_revision_changed");
                db.Entry(input).Property(x => x.Revision).IsModified = true;
            }
            var head = await db.RegulatoryQualificationHeads.SingleOrDefaultAsync(x => x.RuleId == rule, ct);
            var prior = head == null ? null : await db.RegulatoryQualificationEvents.AsNoTracking().SingleAsync(x => x.RuleId == rule && x.Revision == head.Revision, ct);
            object outcome;
            if (fact != null) {
                if (prior == null || prior.RuleRevision != r.Revision || prior.State != "IN_FORCE" || prior.Revision != fact.QualificationRevision || head!.Version != fact.QualificationVersion) return await Fail(h, 409, "qualified_source_required");
                if (fact.Fields == null || fact.Fields.Length < 1 || fact.Fields.Length > RegulatoryQualificationPolicy.ProfileFields.Length || fact.Fields.Distinct().Count() != fact.Fields.Length || fact.Fields.Any(f => !RegulatoryQualificationPolicy.ProfileFields.Contains(f)) || fact.ValidUntil <= now || fact.ValidUntil > grant.ValidUntil) return await Fail(h, 400, "invalid_fact_attestation");
                var policy = Content(prior);
                if (fact.Fields.Any(f => policy.Inputs?.Any(p => p.Field == f && p.Basis == "HumanAttested") != true)) return await Fail(h, 409, "attestation_policy_mismatch");
                var profile = await db.RegulatoryDrafts.SingleOrDefaultAsync(x => x.VesselId == fact.VesselId, ct);
                if (profile == null || profile.Revision != fact.ProfileRevision || profile.Version != fact.ProfileVersion) return await Fail(h, 409, "profile_context_conflict");
                var vessel = await a.Scope(db.Vessels).AsNoTracking().SingleAsync(x => x.Id == fact.VesselId, ct);
                if (fact.Fields.Contains("vessel.contractForConstructionDate") && !a.Permissions.Contains("file.read")) return await Fail(h, 403, "permission_denied");
                var (_, facts) = await ApplicabilityContextResolver.Resolve(db, vessel, now, ct);
                if (fact.Fields.Any(f => facts[f].Knowledge != "Unverified")) return await Fail(h, 409, "known_profile_value_required");
                if (fact.Fields.Contains("vessel.contractForConstructionDate")) {
                    var record = RegulatoryDraftStorage.Deserialize(profile.ContentJson).ConstructionRecord;
                    if (record == null) return await Fail(h, 409, "construction_record_required");
                    var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(f => f.Id == record.Id && f.VesselId == vessel.Id && f.Revision == record.Revision && f.Checksum == record.Checksum &&
                        db.FileStages.Any(s => s.Id == f.Id && s.VesselId == vessel.Id && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport"), ct);
                    if (file == null) return await Fail(h, 409, "construction_record_required");
                    var fileStage = await db.FileStages.SingleAsync(s => s.Id == file.Id && s.VesselId == vessel.Id && s.PersonId == null && s.DocumentId == null && s.State == "CleanTransport", ct);
                    db.Entry(fileStage).Property(s => s.State).IsModified = true;
                    var settings = h.RequestServices.GetRequiredService<SecureFileSettings>();
                    if (!settings.Ready(h.RequestServices.GetRequiredService<IWebHostEnvironment>())) return await Fail(h, 503, "file_settings_unavailable");
                    try {
                        await using var bytes = await h.RequestServices.GetRequiredService<FileObjects>().OpenAsync(file, ct);
                        if (bytes.Length != file.Length || bytes.Length > settings.MaxBytes || Convert.ToHexString(await SHA256.HashDataAsync(bytes, ct)) != file.Checksum)
                            return await Fail(h, 503, "file_integrity_unavailable");
                    } catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or CryptographicException) { return await Fail(h, 503, "file_integrity_unavailable"); }
                }
                db.Entry(profile).Property(x => x.Revision).IsModified = true; db.Entry(head!).Property(x => x.Revision).IsModified = true;
                var e = new RegulatoryFactAttestation { TenantId = a.TenantId, Id = Guid.NewGuid(), RuleId = rule, QualificationRevision = prior.Revision,
                    VesselId = fact.VesselId, ProfileRevision = profile.Revision, FieldsJson = JsonSerializer.Serialize(fact.Fields.Order(), Json), ActorId = a.Id, RoleId = grant.RoleId,
                    AuthorityId = grant.Id, AuthorityHash = authorityHash, Hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { fact, prior.Hash, profile.ContentJson, authorityHash }, Json)), SavedAt = now, ValidUntil = fact.ValidUntil };
                db.RegulatoryFactAttestations.Add(e); await db.SaveChangesAsync(ct);
                outcome = new { e.Id, e.RuleId, e.QualificationRevision, e.VesselId, e.ProfileRevision, fields = fact.Fields, e.Hash, e.SavedAt, e.ValidUntil };
            } else {
                if (cmd!.RuleRevision != r.Revision || cmd.ExpectedVersion != (head?.Version ?? 0)) return await Fail(h, 409, "version_conflict");
                if (cmd.State != "DRAFT" && cmd.Content != null) return await Fail(h, 400, "immutable_review_content");
                if (cmd.State != "DRAFT" && prior != null && prior.ContractJson != JsonSerializer.Serialize(grant.Contract, Json)) return await Fail(h, 409, "source_contract_changed");
                var content = cmd.State == "DRAFT" ? cmd.Content ?? new() : prior == null ? new() : Content(prior);
                if (cmd.State == "DRAFT" && content.SourceBytesBase64 == null && prior != null && prior.RuleRevision == r.Revision) content = content with { SourceBytesBase64 = Content(prior).SourceBytesBase64 };
                if (cmd.State != "DRAFT" && (prior == null || prior.RuleRevision != r.Revision)) return await Fail(h, 409, "review_context_changed");
                if (cmd.State != "DRAFT" && r.RuleJson == null) return await Fail(h, 409, "incomplete_rule");
                if (cmd.State != "DRAFT" && ApplicabilityEngine.Parse(r.RuleJson!).ExampleOnly) return await Fail(h, 409, "example_rule_not_admissible");
                if (cmd.State is not ("DRAFT" or "IN_REVIEW") && grant.Contract.RequiredMechanism != null) return Results.Problem(statusCode: 409, title: "source_mechanism_unavailable",
                    extensions: new Dictionary<string, object?> { ["code"] = "source_mechanism_unavailable", ["mechanism"] = grant.Contract.RequiredMechanism, ["sourceLocator"] = grant.Contract.SourceLocator });
                var failure = RegulatoryQualificationPolicy.Transition(prior?.State, cmd.State, content, prior?.AuthorId != a.Id && r.ActorId != a.Id,
                    DateOnly.FromDateTime(now.UtcDateTime), r.RuleJson == null ? new("DRAFT", "9999-12-31") : ApplicabilityEngine.Parse(r.RuleJson).Lifecycle);
                if (failure != null) return await Fail(h, 409, failure);
                if (content.OwnerId != null) {
                    var owners = h.RequestServices.GetRequiredService<FederationSettings>().Memberships.Where(m => m.TenantId == a.TenantId && m.ActorId == content.OwnerId && m.ActorKind == "Human" &&
                        ActorResolver.TrustedRoleContext(m.RoleReferences)?.Split(',').Contains(content.OwnerRole?.ToString("D")) == true &&
                        m.RegulatoryAuthorities.Any(g => g.RuleId == rule && g.SourceId == r.SourceId && g.SourceRevision == r.SourceRevision && g.RoleId == content.OwnerRole && g.Actions.Contains("own") && g.ValidFrom <= now && now < g.ValidUntil)).ToArray();
                    if (owners.Length != 1) return await Fail(h, 409, "source_owner_authority_required");
                }
                if (content.SupersedingRuleId != null && (content.SupersedingRuleId == rule || !await db.RuleRevisions.AnyAsync(x => x.Id == content.SupersedingRuleId && x.Revision == content.SupersedingRuleRevision, ct))) return await Fail(h, 409, "superseding_rule_unavailable");
                if (head == null) { head = new() { TenantId = a.TenantId, RuleId = rule }; db.RegulatoryQualificationHeads.Add(head); }
                else db.Entry(head).Property(x => x.Revision).IsModified = true;
                head.Revision++;
                var contentJson = JsonSerializer.Serialize(content, Json);
                var e = new RegulatoryQualificationEvent { TenantId = a.TenantId, RuleId = rule, Revision = head.Revision, RuleRevision = r.Revision,
                    State = cmd.State, ContentJson = contentJson, SourceHash = RegulatoryQualificationPolicy.SourceHash(content), PriorHash = prior?.Hash, SupersedingRuleId = content.SupersedingRuleId, SupersedingRuleRevision = content.SupersedingRuleRevision,
                    AuthorId = cmd.State == "DRAFT" ? a.Id : prior!.AuthorId, ActorId = a.Id, AuthorityId = grant.Id, RoleId = grant.RoleId,
                    AuthorityHash = authorityHash, ContractJson = JsonSerializer.Serialize(grant.Contract, Json), PolicyVersion = RegulatoryQualificationPolicy.Version, SavedAt = now,
                    Hash = RegisterStorage.Hash(JsonSerializer.Serialize(new { contentJson, r.Hash, cmd.State, priorHash = prior?.Hash, authorityHash, now, policy = RegulatoryQualificationPolicy.Version }, Json)) };
                db.RegulatoryQualificationEvents.Add(e); await db.SaveChangesAsync(ct); outcome = Dto(e, head.Version);
            }
            var json = JsonSerializer.Serialize(outcome, Json);
            db.RegulatoryQualificationReceipts.Add(new() { TenantId = a.TenantId, ActorId = a.Id, Key = key, RuleId = rule, Fingerprint = fingerprint, AuthorityHash = authorityHash, OutcomeJson = json, SavedAt = now });
            await db.SaveChangesAsync(ct);
            var commit = Live(); if (commit == null || GrantHash(commit, a) != authorityHash) return await Fail(h, 403, "human_authority_required");
            await tx.CommitAsync(ct); return Results.Content(json, "application/json");
        } catch (DbUpdateConcurrencyException) {
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); return await Replay() ?? await Fail(h, 409, "version_conflict");
        } catch (Exception e) when (PostgresFailure.IsDatabase(e)) {
            DatabaseFailureObservation.Record(h, e);
            await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var state = PostgresFailure.Provider(e)?.SqlState;
            return await Replay() ?? await Fail(h, state is "23505" or "40001" or "40P01" ? 409 : 503, state is "23505" or "40001" or "40P01" ? "version_conflict" : "service_unavailable");
        }
    }
}
