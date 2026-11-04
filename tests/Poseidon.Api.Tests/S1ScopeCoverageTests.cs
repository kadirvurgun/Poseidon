using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// T-S1-002/003/004/007. Actual host, all registered mutation routes and real
// PostgreSQL. These tests do not infer positive-write coverage from declarations.
public sealed partial class DirectoryWorkspaceTests
{
    [Fact]
    public async Task Current_mutation_routes_are_exhaustively_classified_and_authorized_in_the_actual_host()
    {
        await using var host = Host();
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var observed = S1SecurityContract.ValidateEndpointInventory(endpoints);
        // S4 adds three real notification commands. Historical S1 82-operation
        // receipts remain unchanged; this is the new source-authored denominator.
        Assert.Equal(86, observed.Count);
        Assert.Equal(80, observed.Count(x => x.IsCritical));
        Assert.Equal(85, S1SecurityContract.CriticalOperationIds.Count);
        Assert.Equal(6, observed.Count(x => !x.IsCritical));
        var extra = new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse("/api/v1/unclassified-write"), 0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["POST"]), new AuthorizeAttribute("DirectoryRead")), "unclassified");
        Assert.Throws<InvalidOperationException>(() => S1SecurityContract.ValidateEndpointInventory(endpoints.Append(extra)));
        Assert.Throws<InvalidOperationException>(() => S1SecurityContract.ValidateEndpointInventory(endpoints.Where(x => x != endpoints.First(e =>
            e is RouteEndpoint route && route.RoutePattern.RawText == "/api/v1/companies" &&
            e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains("POST") == true))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_current_mutation_routes_deny_anonymous_or_forged_IdP_privileges_without_business_effects(bool forgedClaims)
    {
        await using var baseline = Host(manage: false, read: false, edit: false, scope: false);
        await using var host = baseline.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = forgedClaims ? "S1ForgedClaims" : "DirectorySynthetic";
                options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
                options.DefaultForbidScheme = options.DefaultAuthenticateScheme;
            }).AddScheme<AuthenticationSchemeOptions, S1ForgedClaimsHandler>("S1ForgedClaims", _ => { })));
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with
        { Permissions = [], RoleReferences = [], TenantWideRead = false, TenantWideManage = false, VesselGrants = [] };
        using var client = Client(host, false);
        var before = await S1BusinessSnapshot();
        foreach (var operation in S1SecurityContract.Operations)
        {
            var path = S1ConcretePath(operation);
            using var request = new HttpRequestMessage(new HttpMethod(operation.Method), path)
            {
                Content = S1AdversarialContent(host.Services, operation,
                    "{\"roles\":[\"administrator\"],\"permissions\":[\"*\"],\"name\":\"untrusted-request-marker\"}")
            };
            request.Headers.Add("X-Correlation-ID", "untrusted-client-correlation");
            using var response = await client.SendAsync(request, Ct);
            S1AssertAuthorizationStatus(operation, forgedClaims ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("untrusted-request-marker", content);
            Assert.DoesNotContain(seed.Tenants[1].Id.ToString(), content);
            var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
            Assert.NotEqual("untrusted-client-correlation", correlation);
            Assert.True(Guid.TryParseExact(correlation, "N", out _));
            // Fixture-owned audit connection observes anonymous null-tenant rows
            // and catches incorrectly attributed rows hidden by a tenant filter.
            await using var auditDb = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>()
                .UseNpgsql(postgres.GetConnectionString()).Options);
            var security = await auditDb.Set<SecurityEvent>().AsNoTracking().Where(e => e.CorrelationId == correlation).ToArrayAsync(Ct);
            Assert.NotEmpty(security);
            Assert.All(security, e =>
            {
                Assert.Equal(forgedClaims ? actorId.ToString() : null, e.ActorId);
                Assert.Equal(forgedClaims ? seed.Tenants[0].Id : (Guid?)null, e.TenantId);
                Assert.NotEqual(default, e.OccurredAt);
            });
        }
        S1AssertSameBusinessSnapshot(before, await S1BusinessSnapshot());
    }

    [Fact]
    public async Task Every_current_critical_write_requires_a_real_server_role_even_when_read_scope_is_valid()
    {
        await using var host = Host();
        host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences = [];
        using var client = Client(host); await Token(client);
        var before = await S1BusinessSnapshot();
        foreach (var operation in S1SecurityContract.Operations.Where(x => x.IsCritical))
        {
            using var request = new HttpRequestMessage(new HttpMethod(operation.Method), S1ConcretePath(operation))
            { Content = S1AdversarialContent(host.Services, operation,
                JsonSerializer.Serialize(new { roles = new[] { "owner" }, roleContext = actorId.ToString() })) };
            using var response = await client.SendAsync(request, Ct);
            S1AssertAuthorizationStatus(operation, HttpStatusCode.Forbidden, response.StatusCode);
            var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
            await using var db = Db();
            Assert.True(await db.Set<SecurityEvent>().AnyAsync(e => e.CorrelationId == correlation &&
                e.ActorId == actorId.ToString() && e.Result == "Denied", Ct));
        }
        S1AssertSameBusinessSnapshot(before, await S1BusinessSnapshot());
    }

    private static StringContent S1AdversarialContent(IServiceProvider services, SecurityOperation operation, string body)
    {
        // Use the real route's advertised media contract so routing selects the
        // authorized endpoint, rather than the framework's media-rejection endpoint.
        // The adversarial body stays unchanged even for a binary media contract.
        var vetted = Assert.Single(S1SecurityContract.Operations, item => item.OperationId == operation.OperationId);
        var candidates = services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Where(route =>
            route.RoutePattern.RawText?.TrimEnd('/') == vetted.RoutePattern.TrimEnd('/') &&
            route.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(vetted.Method, StringComparer.Ordinal) == true).ToArray();
        Assert.True(candidates.Length == 1,
            $"S1 route {vetted.OperationId}: expected candidate count 1, actual candidate count {candidates.Length}.");
        var endpoint = Assert.Single(candidates);
        var types = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes;
        var media = types?.FirstOrDefault(x => string.Equals(x, "application/json", StringComparison.OrdinalIgnoreCase))
            ?? types?.FirstOrDefault() ?? "application/json";
        return new StringContent(body, Encoding.UTF8, media);
    }

    private static void S1AssertAuthorizationStatus(SecurityOperation operation, HttpStatusCode expected, HttpStatusCode actual)
    {
        var vetted = Assert.Single(S1SecurityContract.Operations, item => item.OperationId == operation.OperationId);
        Assert.True(expected == actual,
            $"S1 operation {vetted.OperationId}: expected HTTP {(int)expected}, actual HTTP {(int)actual}.");
    }

    private string S1ConcretePath(SecurityOperation operation) => Regex.Replace(operation.RoutePattern,
        @"\{([^}:]+)(?::[^}]+)?\}", match => match.Groups[1].Value switch
        {
            "vessel" => SeedVessel.Id.ToString(),
            "revision" or "personalRevision" => "1",
            "personalKind" => "medical",
            "category" => "crew",
            "action" => operation.Variants.First().RouteValue,
            _ => "10000000-0000-4000-8000-000000000011"
        });

    private async Task<Dictionary<string, byte[]>> S1BusinessSnapshot()
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var tenant in seed.Tenants)
        {
            await using var db = Db(tenant.Id);
            foreach (var entity in db.Model.GetEntityTypes().Where(x => x.FindPrimaryKey() != null &&
                x.ClrType != typeof(SecurityEvent)).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                var method = typeof(DirectoryWorkspaceTests).GetMethod(nameof(S1HashMappedRows), BindingFlags.Static | BindingFlags.NonPublic)!;
                var digest = await (Task<byte[]>)method.MakeGenericMethod(entity.ClrType).Invoke(null, [db, Ct])!;
                result.Add(tenant.Id + ":" + entity.Name, digest);
            }
        }
        return result;
    }

    private static async Task<byte[]> S1HashMappedRows<TEntity>(PoseidonDbContext db, CancellationToken ct) where TEntity : class
    {
        db.ChangeTracker.Clear();
        var rows = await db.Set<TEntity>().AsTracking().ToArrayAsync(ct);
        var values = rows.Select(row =>
        {
            var mapped = db.Entry(row).Properties.OrderBy(x => x.Metadata.Name, StringComparer.Ordinal)
                .ToDictionary(x => x.Metadata.Name, x => x.CurrentValue, StringComparer.Ordinal);
            return JsonSerializer.Serialize(mapped);
        }).Order(StringComparer.Ordinal);
        // Synthetic fixture values remain in memory. Only a fixed assertion message
        // may leave this method; no private snapshots or value hashes enter reports.
        return SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values)));
    }

    private static void S1AssertSameBusinessSnapshot(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
    {
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        Assert.All(before, pair => Assert.True(CryptographicOperations.FixedTimeEquals(pair.Value, after[pair.Key]),
            "A denied request changed persisted business/audit state."));
    }

    private sealed class S1ForgedClaimsHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("iss", "https://directory-test.example"), new Claim("sub", "synthetic"),
                new Claim(ClaimTypes.Role, "administrator"), new Claim("permissions", "*"),
                new Claim("tenant_id", "10000000-0000-4000-8000-000000000099")
            ], Scheme.Name)), Scheme.Name)));
    }
}

public sealed class S1AuditedWriteArchitectureTests
{
    [Theory]
    [InlineData(typeof(Startup))]
    [InlineData(typeof(PoseidonDbContext))]
    public void Business_write_assemblies_do_not_reference_unaudited_bulk_or_raw_execution(Type marker)
    {
        using var stream = File.OpenRead(marker.Assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        string[] forbidden = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync", "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw",
            "ExecuteSqlRawAsync", "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync", "ExecuteNonQuery", "ExecuteNonQueryAsync"];
        var bypasses = metadata.MemberReferences.Select(handle => metadata.GetString(metadata.GetMemberReference(handle).Name))
            .Where(name => forbidden.Contains(name, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Empty(bypasses);
    }
}
