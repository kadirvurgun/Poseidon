using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

[Trait("Category", "Integration")]
[Collection("directory-postgres")]
public sealed class DiagnosticsTests(OwnedPostgresFixture fixture)
{
    [Fact]
    public async Task Mandatory_anonymous_denial_audit_without_storage_fails_closed()
    {
        await using var host = Host("Production").WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["SecretReferences:Database"] = "POSEIDON_UNCONFIGURED_SYNTHETIC_AUDIT_GATE" })));
        using var client = host.CreateClient();
        foreach (var path in new[] { "/api/v1/vessels", "/api/v1/companies", "/api/v1/audit" })
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        using var denied = await client.PutAsJsonAsync("/api/v1/vessels/00000000-0000-0000-0000-000000000001/regulatory-profile", new { expectedVersion = 0, content = new { } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
    }
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Public_status_contains_only_technical_contract_and_does_not_require_a_database(string environment)
    {
        await using var host = Host(environment);
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/v1/system/status", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["service", "status"], json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal("poseidon", json.RootElement.GetProperty("service").GetString());
        Assert.Equal("available", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Liveness_is_process_only_and_business_routes_require_authentication()
    {
        await using var postgres = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var seed = SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));
        await using var db = new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString(), seed), Guid.Empty, ActorContext.SeedImport(), true);
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await using var host = Host("Test").WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Poseidon"] = postgres.GetConnectionString() })));
        using var client = host.CreateClient();
        Assert.Equal("alive", await client.GetStringAsync("/health/live", TestContext.Current.CancellationToken));
        using var vessels = await client.GetAsync("/api/v1/vessels", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, vessels.StatusCode);
        using var audit = await client.GetAsync("/api/v1/audit", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, audit.StatusCode);
        await using var observations = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        Assert.True(await observations.Set<SecurityEvent>().AnyAsync(x => x.Action == "AuditQuery" && x.Result == "Unauthenticated" && x.TenantId == null && x.ActorId == null, TestContext.Current.CancellationToken));
        foreach (var path in new[] { "/api/v1/auth" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var post = await client.PostAsync("/api/v1/system/status", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task OpenApi_matches_the_real_response_contract_in_development()
    {
        await using var host = Host("Development");
        using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var expected = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "openapi.v1.json"), TestContext.Current.CancellationToken);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(document.RootElement.GetRawText())), "Generated OpenAPI must match the committed consumer contract.");
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty("/api/v1/system/status").GetProperty("get");
        Assert.Equal("GetSystemStatus", operation.GetProperty("operationId").GetString());
        var schema = operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        var reference = schema.GetProperty("$ref").GetString()!;
        var properties = root.GetProperty("components").GetProperty("schemas").GetProperty(reference.Split('/')[^1]).GetProperty("properties");
        Assert.Equal(["service", "status"], properties.EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.All(properties.EnumerateObject(), property => Assert.Equal("string", property.Value.GetProperty("type").GetString()));
        // TW-02/03: actual host metadata, not a hand-maintained consumer-only fixture.
        var paths = root.GetProperty("paths");
        // REG-SRC-04: all mutations advertise the runtime-required security/retry headers.
        foreach (var draft in new[] { "source", "requirement" })
        foreach (var (suffix, method) in new[] { ("", "post"), ("/{id}", "put") }) {
            var mutation = paths.GetProperty($"/api/v1/{draft}-drafts{suffix}").GetProperty(method);
            foreach (var name in new[] { "Idempotency-Key", "X-CSRF-TOKEN" }) {
                var header = Assert.Single(mutation.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == name);
                Assert.Equal("header", header.GetProperty("in").GetString()); Assert.True(header.GetProperty("required").GetBoolean());
            }
        }
        Assert.False(paths.TryGetProperty("/api/v1/today", out _));
        Assert.False(paths.TryGetProperty("/api/v1/why", out _));
        foreach (var (path, operationId, parameters) in new[]
        {
            ("/api/v1/poseidon-today", "GetToday", new[] { "vesselId", "offset", "limit" }),
            ("/api/v1/why-engine", "GetTodayWhy", new[] { "vesselId", "cardKey" })
        })
        {
            var read = paths.GetProperty(path).GetProperty("get");
            Assert.Equal(operationId, read.GetProperty("operationId").GetString());
            Assert.Equal(parameters.Order(), read.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).Order());
            foreach (var code in new[] { "200", "400", "401", "403", "404", "503" })
                Assert.True(read.GetProperty("responses").TryGetProperty(code, out _));
        }
    }

    [Fact]
    public async Task Production_does_not_expose_openapi_or_allow_cross_origin_browser_access()
    {
        await using var host = Host("Production");
        using var client = host.CreateClient();
        using var openapi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, openapi.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/system/status");
        request.Headers.Add("Origin", "https://untrusted.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static WebApplicationFactory<Program> Host(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment(environment));
}
