using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5803 / 58.03 paragraph:8, :57, :59, :278. Actual host EndpointDataSource;
// no database is needed to enumerate the mapped transport surface.
[Trait("Requirement", "PLAN-1511-5803")]
public sealed class EndpointProfileRegistryTests
{
    private static WebApplicationFactory<Program> Host(string environment = "Test") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    private static IReadOnlyList<Endpoint> Endpoints(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<EndpointDataSource>().Endpoints;

    // P5803-AC-01/02: every mapped endpoint has exactly one profile, and no stale entry exists.
    [Fact]
    public async Task Every_mapped_endpoint_has_exactly_one_registered_profile_and_matching_host_metadata()
    {
        await using var host = Host();
        var endpoints = Endpoints(host);
        // Regeneration aid only: writes observed host facts; profile assignment stays a reviewed edit.
        var output = Environment.GetEnvironmentVariable("POSEIDON_ENDPOINT_PROFILE_OBSERVED_OUT");
        if (!string.IsNullOrEmpty(output))
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(EndpointProfiles.Observe(endpoints),
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }), TestContext.Current.CancellationToken);
        Assert.Empty(EndpointProfiles.Validate(endpoints));
        var observed = EndpointProfiles.Observe(endpoints);
        Assert.Equal(EndpointProfiles.Document.Endpoints.Length, observed.Count);

        var unregistered = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/unprofiled-read"), 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"]), new AuthorizeAttribute("DirectoryRead")), "unprofiled");
        Assert.Contains(EndpointProfiles.Validate(endpoints.Append(unregistered)), e => e.Contains("has no profile", StringComparison.Ordinal));

        var removed = endpoints.OfType<RouteEndpoint>().First(e => e.RoutePattern.RawText == "/api/v1/system/status");
        Assert.Contains(EndpointProfiles.Validate(endpoints.Where(e => e != removed)), e => e.Contains("no mapped endpoint", StringComparison.Ordinal));

        var duplicate = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/system/status"), 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"])), "duplicate");
        Assert.Contains(EndpointProfiles.Validate(endpoints.Append(duplicate)), e => e.Contains("mapped more than once", StringComparison.Ordinal));

        // Metadata drift (e.g. authorization dropped from a read) is detected, not silently re-profiled.
        var companies = endpoints.OfType<RouteEndpoint>().First(e => EndpointProfiles.Normalize(e.RoutePattern.RawText ?? "") == "/api/v1/companies" &&
            e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);
        var drifted = new RouteEndpoint(_ => Task.CompletedTask, companies.RoutePattern, 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"]), new EndpointNameMetadata(
                companies.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName)), "drifted");
        Assert.Contains(EndpointProfiles.Validate(endpoints.Where(e => e != companies).Append(drifted)),
            e => e.Contains("differs", StringComparison.Ordinal));
    }

    // P5803-AC-03/04: command profiles carry CSRF, Idempotency-Key, no-store and an explicit
    // lost-reply/after-window retry contract; every HTTP command is also S1-classified.
    [Fact]
    public void Command_profiles_declare_csrf_idempotency_no_store_and_retry_contracts()
    {
        var document = EndpointProfiles.Document;
        Assert.All(document.Profiles, p => Assert.Equal("no-store", p.CachePolicy));
        foreach (var profile in document.Profiles.Where(p => p.Kind == "COMMAND"))
        {
            Assert.Equal("REQUIRED", profile.Csrf);
            Assert.Equal("REQUIRED", profile.Authentication);
            var idempotency = Assert.IsType<EndpointIdempotencyContract>(profile.Idempotency);
            Assert.True(EndpointProfiles.IdempotencyModes.Contains(idempotency.Mode), idempotency.Mode);
            Assert.Equal(EndpointProfiles.HeaderKeyRequired(profile), idempotency.Required);
            Assert.Equal("Idempotency-Key", idempotency.Header);
            // Every mode that can create a business effect keeps a durable dedup record (58.03 paragraph:98).
            Assert.Equal(idempotency.Mode is not ("NO_BUSINESS_EFFECT_OBSERVATION" or "NOT_APPLICABLE_ALWAYS_DENIED"), idempotency.Durable);
            Assert.Equal(409, idempotency.ConflictStatus);
            Assert.False(string.IsNullOrWhiteSpace(idempotency.LostReplyRetry));
            Assert.False(string.IsNullOrWhiteSpace(idempotency.AfterResponseWindowRetry));
            var concurrency = Assert.IsType<EndpointConcurrencyContract>(profile.Concurrency);
            Assert.True(concurrency.CreateExempt);
            Assert.InRange(concurrency.StaleStatus, 409, 412);
        }
        // Native profiles never claim a canonical 58.03 profile id; they name the target and gaps.
        Assert.All(document.Profiles.Where(p => p.Conformance == "NATIVE_PRE_58_03"), p =>
        {
            Assert.False(EndpointProfiles.CanonicalProfileIds.Contains(p.ProfileId), p.ProfileId);
            Assert.NotEmpty(p.Gaps);
        });
        var commands = document.Endpoints.Where(e => EndpointProfiles.IsCommandMethod(e.Method)).ToArray();
        Assert.Equal(S1SecurityContract.Operations.Count, commands.Length);
        Assert.All(commands, e => Assert.Contains(e.SecurityOperationId!, S1SecurityContract.Operations.Select(o => o.OperationId)));
        Assert.All(document.Endpoints.Where(e => !EndpointProfiles.IsCommandMethod(e.Method)),
            e => Assert.Equal("GET", e.Method));
        // Header-key commands are exactly the S1 critical/observation writes not bound to another
        // native mechanism. Known header-key gaps are a ratchet: a new one fails until reviewed.
        var keyGaps = commands.Where(e => e.Deviations.Contains("IDEMPOTENCY_KEY_NOT_REQUIRED")).Select(e => e.OperationId).Order().ToArray();
        Assert.Equal(["ImportMappingDraft", "ImportPreview", "ImportUpload"], keyGaps);
        Assert.All(commands.Where(e => e.Deviations.Contains("IDEMPOTENCY_KEY_NOT_REQUIRED")),
            e => Assert.True(EndpointProfiles.HeaderKeyRequired(EndpointProfiles.Profile(e.ProfileId))));
        Assert.Equal("NATIVE-DENIED-1", Assert.Single(commands, e => S1SecurityContract.Operations
            .Single(o => o.OperationId == e.SecurityOperationId).Kind == "AlwaysDenied").ProfileId);
        Assert.All(commands.Where(e => S1SecurityContract.Operations.Single(o => o.OperationId == e.SecurityOperationId).IsCritical),
            e => Assert.NotEqual("NATIVE-OBSERVATION-COMMAND-1", e.ProfileId));
    }

    // P5803-AC-07: OpenAPI 3.1 operations expose the registry profileId (58.03 paragraph:278).
    [Fact]
    public async Task OpenApi_operations_expose_their_registered_profile()
    {
        await using var host = Host("Development");
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var operations = 0; var missing = new List<string>();
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (operation.Name is "parameters" or "summary" or "description") continue;
            operations++;
            var profile = operation.Value.TryGetProperty(EndpointProfiles.OpenApiExtension, out var value) ? value.GetString() : null;
            if (!EndpointProfiles.Document.Endpoints.Any(e => e.Method == operation.Name.ToUpperInvariant() && e.ProfileId == profile &&
                EndpointProfiles.DocumentedPath(e.NormalizedPath) == path.Name))
                missing.Add($"{operation.Name} {path.Name}: {profile ?? "(none)"}");
        }
        Assert.True(missing.Count == 0, string.Join("\n", missing));
        Assert.Equal(EndpointProfiles.Document.Endpoints.Length, operations);
    }

    // P5803 QA note (19.11.2026): the real-host probe must grant every permission the server
    // accepts, otherwise new commands stop at 403 permission_denied before CSRF/key checks and
    // the probe reports a false green. Fails when the server allowlist grows without the probe.
    [Fact]
    public void Profile_probe_grants_every_server_accepted_permission()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Directory.Packages.props"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "backend", "Poseidon.Api", "FoundationIdentity.cs"));
        var filter = Regex.Match(source, @"[Pp]ermissions \?\? \[\]\)\.Where\(x => (?<body>x is .+?)\)\.Distinct\(\)");
        Assert.True(filter.Success, "Server permission allowlist not found in FoundationIdentity.cs.");
        var accepted = Regex.Matches(filter.Groups["body"].Value, "\"([a-z0-9_.]+)\"").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(accepted);
        var missing = accepted.Except(DirectoryWorkspaceTests.ProfileProbePermissions, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "Probe lacks server permissions: " + string.Join(", ", missing));
    }
}

// P5803-AC-05/06: actual host, real PostgreSQL and antiforgery. Commands without the
// profile's CSRF token or Idempotency-Key never succeed and never change business state;
// every response is no-store with a correlation id and the profile error envelope.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    [Trait("Requirement", "PLAN-1511-5803")]
    public async Task Registered_command_and_read_profiles_are_enforced_by_the_actual_host()
    {
        await using var baseline = Host(search: true, regulatoryRead: true, regulatoryWrite: true, auditRead: true, crewRead: true,
            crewWrite: true, crewIdentity: true, technicalRead: true, technicalWrite: true, tugRead: true, tugWrite: true,
            tugOwner: true, tugMode: true, importRead: true, importWrite: true, certificateRead: true, certificateWrite: true,
            applicabilityRead: true, applicabilityEvaluate: true, ruleWrite: true, ruleReview: true);
        // Grant every current server permission so probes reach transport checks instead of
        // stopping at the permission gate. Synthetic membership only; no production grant.
        await using var host = baseline.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            ProfileProbePermissions.Select((permission, index) => new KeyValuePair<string, string?>(
                $"Authentication:Oidc:Memberships:0:Permissions:{60 + index}", permission)))));
        using var withoutCsrf = Client(host);
        using var withCsrf = Client(host); await Token(withCsrf);
        var probe = new JsonArray();
        var failures = new List<string>();
        var before = await S1BusinessSnapshot();
        foreach (var entry in EndpointProfiles.Document.Endpoints)
        {
            var profile = EndpointProfiles.Profile(entry.ProfileId);
            var path = ProfileConcretePath(entry);
            if (!EndpointProfiles.IsCommandMethod(entry.Method))
            {
                using var read = await ProfileSend(host, withCsrf, entry, path, csrf: true, key: false);
                probe.Add(await ProfileObservation(entry, "read", read));
                ProfileAssertTransport(entry, profile, read, failures, "read");
                continue;
            }
            if (profile.Csrf == "REQUIRED" && !entry.Deviations.Contains("CSRF_NOT_ENFORCED_BEFORE_EFFECT"))
            {
                using var response = await ProfileSend(host, withoutCsrf, entry, path, csrf: false, key: true);
                probe.Add(await ProfileObservation(entry, "missing-csrf", response));
                if (response.IsSuccessStatusCode) failures.Add($"{entry.Method} {entry.NormalizedPath}: succeeded without CSRF.");
                ProfileAssertTransport(entry, profile, response, failures, "missing-csrf");
            }
            if (EndpointProfiles.HeaderKeyRequired(profile) && !entry.Deviations.Contains("IDEMPOTENCY_KEY_NOT_REQUIRED"))
            {
                using var response = await ProfileSend(host, withCsrf, entry, path, csrf: true, key: false);
                probe.Add(await ProfileObservation(entry, "missing-idempotency-key", response));
                if (response.IsSuccessStatusCode) failures.Add($"{entry.Method} {entry.NormalizedPath}: succeeded without Idempotency-Key.");
                ProfileAssertTransport(entry, profile, response, failures, "missing-idempotency-key");
            }
        }
        var output = Environment.GetEnvironmentVariable("POSEIDON_ENDPOINT_PROFILE_PROBE_OUT");
        if (!string.IsNullOrEmpty(output))
            await File.WriteAllTextAsync(output, probe.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Ct);
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        S1AssertSameBusinessSnapshot(before, await S1BusinessSnapshot());
    }

    private static void ProfileAssertTransport(EndpointProfileEntry entry, EndpointProfile profile, HttpResponseMessage response,
        List<string> failures, string variant)
    {
        var label = $"{entry.Method} {entry.NormalizedPath} [{variant}]";
        if (response.Headers.CacheControl?.NoStore != true) failures.Add($"{label}: response is not no-store.");
        if (!response.Headers.TryGetValues(profile.ErrorEnvelope.CorrelationHeader, out var correlation) || correlation.Count() != 1)
            failures.Add($"{label}: missing single correlation header.");
        var envelopeDeviation = entry.Deviations.Contains("NON_JSON_ERROR_ENVELOPE") ||
            entry.Deviations.Contains("BINDING_REJECTION_BEFORE_HANDLER_WITHOUT_ENVELOPE");
        if (!response.IsSuccessStatusCode && (int)response.StatusCode is not (401 or 405) && !envelopeDeviation &&
            response.Content.Headers.ContentLength == 0)
            failures.Add($"{label}: error has no profile envelope and no recorded deviation.");
        else if (!response.IsSuccessStatusCode && (int)response.StatusCode is not (401 or 405) && !envelopeDeviation)
        {
            var media = response.Content.Headers.ContentType?.MediaType;
            if (media is not ("application/json" or "application/problem+json"))
                failures.Add($"{label}: error media type {media} is not the profile JSON envelope.");
            else
            {
                try
                {
                    using var json = JsonDocument.Parse(response.Content.ReadAsStream());
                    foreach (var field in profile.ErrorEnvelope.RequiredFields)
                        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty(field, out _))
                            failures.Add($"{label}: error envelope lacks '{field}'.");
                }
                catch (JsonException) { failures.Add($"{label}: error body is not JSON."); }
            }
        }
    }

    internal static readonly string[] ProfileProbePermissions = ["applicability.evaluate", "applicability.override.approve", "applicability.override.author", "applicability.override.revoke", "applicability.read", "audit.read",
        "config.approve", "config.draft", "config.read", "config.reject", "config.resolve",
        "hr.actual.record", "hr.assignment.approve", "hr.assignment.read", "hr.assignment.write", "hr.employment.approve",
        "hr.employment.read", "hr.employment.write", "hr.legal_entity.identifier.read", "hr.legal_entity.read",
        "hr.legal_entity.write", "hr.person.read", "hr.person.verify", "hr.person.write",
        "certificate.extract", "certificate.read", "certificate.renewal.complete", "certificate.renewal.owner", "certificate.renewal.verify", "certificate.renewal.write", "certificate.verify.record",
        "certificate.write", "control.approve", "control.owner.eligible", "control.read", "control.write", "crew.assessment.read", "crew.assignment.declare",
        "crew.catalog.govern", "crew.certificate.read", "crew.certificate.write", "crew.documents.read", "crew.documents.write",
        "crew.history.read", "crew.identity.convert", "crew.identity.read", "crew.identity.write", "crew.position.write",
        "crew.profile.contact.read", "crew.profile.contact.write", "crew.profile.employment.read", "crew.profile.employment.write",
        "crew.profile.identity.read", "crew.profile.identity.write", "crew.profile.medical.read", "crew.profile.medical.write",
        "crew.read", "crew.write", "daily.deck.read", "daily.deck.review", "daily.deck.write", "daily.engine.read", "daily.engine.review",
        "daily.engine.write", "daily.noon.read", "daily.noon.review", "daily.noon.write", "defect.read", "defect.write",
        "directory.read", "directory.write", "equipment.history.read", "equipment.read", "equipment.write", "evidence.read",
        "evidence.write", "file.read", "file.write", "human.decide", "human.review.read", "human.review.submit", "human.trace.read",
        "human.verify", "import.legacy.convert", "import.read", "import.write", "maintenance.history.read",
        "maintenance.observation.write", "maintenance.owner.eligible", "maintenance.read", "maintenance.write", "mode.cross", "mode.tug",
        "notification.acknowledge", "notification.evaluate", "notification.manage", "notification.preference.write", "notification.read",
        "operations.downtime.read", "operations.downtime.write", "operations.fuel.read", "operations.fuel.write", "operations.port.read",
        "operations.port.write", "regulatory.draft.read", "regulatory.draft.write", "regulatory.facts.attest", "regulatory.qualify",
        "rules.review", "rules.write", "system.health.read", "tug.owner.eligible", "tug.plan.write", "tug.read", "tug.write",
        "workorder.read", "workorder.write", "workspace.search",
        "membership.read", "membership.manage", "membership.security.suspend",
        "document.publish", "evidence.verify", "record.hold", "record.retention.manage", "record.purge.request", "record.purge.approve",
        "hse.risk.read", "hse.risk.write", "hse.risk.approve", "hse.risk.acknowledge", "hse.risk_template.read", "hse.risk_template.write", "hse.risk_template.publish", "hse.risk_template.suggest", "hse.risk_method.write", "hse.risk_method.publish"];

    private async Task<HttpResponseMessage> ProfileSend(WebApplicationFactory<Program> host, HttpClient client, EndpointProfileEntry entry,
        string path, bool csrf, bool key)
    {
        var response = await ProfileSendOnce(host, client, entry, path, csrf, key);
        if (response.StatusCode != HttpStatusCode.BadRequest ||
            !(await response.Content.ReadAsStringAsync(Ct)).Contains("invalid_query", StringComparison.Ordinal)) return response;
        // Vessel-scoped routes that take the scope from the query: retry with the seeded vessel.
        response.Dispose();
        return await ProfileSendOnce(host, client, entry, path + "?vesselId=" + SeedVessel.Id, csrf, key);
    }

    private async Task<HttpResponseMessage> ProfileSendOnce(WebApplicationFactory<Program> host, HttpClient client, EndpointProfileEntry entry,
        string path, bool csrf, bool key)
    {
        using var request = new HttpRequestMessage(new HttpMethod(entry.Method), path);
        if (EndpointProfiles.IsCommandMethod(entry.Method))
        {
            var operation = Assert.Single(S1SecurityContract.Operations, o => o.OperationId == entry.SecurityOperationId);
            request.Content = S1AdversarialContent(host.Services, operation, "{\"name\":\"profile-probe-marker\"}");
            if (key) request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        }
        if (!csrf) request.Headers.Remove("X-CSRF-TOKEN");
        var response = await client.SendAsync(request, Ct);
        await response.Content.LoadIntoBufferAsync(Ct);
        return response;
    }

    private static async Task<JsonObject> ProfileObservation(EndpointProfileEntry entry, string variant, HttpResponseMessage response)
    {
        string? code = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json")
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("code", out var value))
                    code = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            }
            catch (JsonException) { code = "(invalid-json)"; }
        return new JsonObject
        {
            ["method"] = entry.Method, ["path"] = entry.NormalizedPath, ["variant"] = variant,
            ["status"] = (int)response.StatusCode, ["code"] = code,
            ["media"] = response.Content.Headers.ContentType?.MediaType,
            ["noStore"] = response.Headers.CacheControl?.NoStore == true
        };
    }

    private string ProfileConcretePath(EndpointProfileEntry entry)
    {
        var variant = entry.SecurityOperationId == null ? null :
            S1SecurityContract.Operations.Single(o => o.OperationId == entry.SecurityOperationId).Variants.FirstOrDefault()?.RouteValue;
        return Regex.Replace(entry.NormalizedPath, @"\{([^}:?]+)(?::[^}]+)?\??\}", match => match.Groups[1].Value switch
        {
            "vessel" or "vesselId" => SeedVessel.Id.ToString(),
            "revision" or "personalRevision" => "1",
            "personalKind" => "medical",
            "kind" => "Noon",
            "category" => "crew",
            "action" => variant ?? "renew",
            _ => "10000000-0000-4000-8000-000000000011"
        });
    }
}
