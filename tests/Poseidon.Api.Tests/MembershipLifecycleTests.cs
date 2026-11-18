using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5202 / P5202-AC-08..14. Actual host, antiforgery, idempotency and real PostgreSQL.
// Synthetic identities only; no production login path or credential is introduced.
public sealed partial class DirectoryWorkspaceTests
{
    private const string MemberIssuer = "https://directory-test.example";
    private readonly Guid memberB = Guid.NewGuid(), adminC = Guid.NewGuid(), securityD = Guid.NewGuid(), otherTenantAdmin = Guid.NewGuid();
    private const string Role = "91c26eb5-cf16-4553-981d-1c275358c535";

    private sealed class MembershipClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private WebApplicationFactory<Program> MembershipHost(MembershipClock? clock = null) => Host().WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Index 0 ("synthetic") is the tenant identity administrator A.
            ["Authentication:Oidc:Memberships:0:Permissions:44"] = "membership.manage",
            ["Authentication:Oidc:Memberships:1:Issuer"] = MemberIssuer, ["Authentication:Oidc:Memberships:1:Subject"] = "member-b",
            ["Authentication:Oidc:Memberships:1:ActorId"] = memberB.ToString(), ["Authentication:Oidc:Memberships:1:TenantId"] = seed.Tenants[0].Id.ToString(),
            ["Authentication:Oidc:Memberships:1:TenantWideRead"] = "true", ["Authentication:Oidc:Memberships:1:RoleReferences:0"] = Role,
            ["Authentication:Oidc:Memberships:1:Permissions:0"] = "directory.read", ["Authentication:Oidc:Memberships:1:Permissions:1"] = "crew.read",
            ["Authentication:Oidc:Memberships:1:Permissions:2"] = "crew.write",
            ["Authentication:Oidc:Memberships:2:Issuer"] = MemberIssuer, ["Authentication:Oidc:Memberships:2:Subject"] = "admin-c",
            ["Authentication:Oidc:Memberships:2:ActorId"] = adminC.ToString(), ["Authentication:Oidc:Memberships:2:TenantId"] = seed.Tenants[0].Id.ToString(),
            ["Authentication:Oidc:Memberships:2:TenantWideManage"] = "true", ["Authentication:Oidc:Memberships:2:RoleReferences:0"] = Role,
            ["Authentication:Oidc:Memberships:2:Permissions:0"] = "directory.read", ["Authentication:Oidc:Memberships:2:Permissions:1"] = "membership.manage",
            ["Authentication:Oidc:Memberships:3:Issuer"] = MemberIssuer, ["Authentication:Oidc:Memberships:3:Subject"] = "security-d",
            ["Authentication:Oidc:Memberships:3:ActorId"] = securityD.ToString(), ["Authentication:Oidc:Memberships:3:TenantId"] = seed.Tenants[0].Id.ToString(),
            ["Authentication:Oidc:Memberships:3:TenantWideManage"] = "true", ["Authentication:Oidc:Memberships:3:RoleReferences:0"] = Role,
            ["Authentication:Oidc:Memberships:3:Permissions:0"] = "directory.read", ["Authentication:Oidc:Memberships:3:Permissions:1"] = "membership.security.suspend",
            ["Authentication:Oidc:Memberships:4:Issuer"] = MemberIssuer, ["Authentication:Oidc:Memberships:4:Subject"] = "other-tenant-admin",
            ["Authentication:Oidc:Memberships:4:ActorId"] = otherTenantAdmin.ToString(), ["Authentication:Oidc:Memberships:4:TenantId"] = seed.Tenants[1].Id.ToString(),
            ["Authentication:Oidc:Memberships:4:TenantWideManage"] = "true", ["Authentication:Oidc:Memberships:4:RoleReferences:0"] = Role,
            ["Authentication:Oidc:Memberships:4:Permissions:0"] = "directory.read", ["Authentication:Oidc:Memberships:4:Permissions:1"] = "membership.manage",
        }));
        b.ConfigureTestServices(s =>
        {
            s.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = "MembershipSynthetic"; o.DefaultChallengeScheme = "MembershipSynthetic"; o.DefaultForbidScheme = "MembershipSynthetic";
            }).AddScheme<AuthenticationSchemeOptions, MembershipTestHandler>("MembershipSynthetic", _ => { });
            if (clock != null) { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); }
        });
    });

    private static async Task<HttpClient> Member(WebApplicationFactory<Program> host, string subject, long? accessRevision = null)
    {
        var client = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Membership-Subject", subject);
        if (accessRevision != null) client.DefaultRequestHeaders.Add("X-Membership-Arv", accessRevision.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var session = await client.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", TestContext.Current.CancellationToken);
        if (!string.IsNullOrEmpty(session?.CsrfToken)) client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }

    private async Task<HttpResponseMessage> Post(HttpClient client, string path, object body, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/" + path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(message, Ct);
    }

    private async Task<MembershipDto> MembershipOf(HttpClient admin, Guid account) =>
        Assert.Single((await admin.GetFromJsonAsync<MembershipPageDto>("/api/v1/memberships?limit=100", Ct))!.Items, x => x.AccountId == account);

    private async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).TryGetProperty("code", out var code) ? code.GetString() : null;

    private async Task Imported(HttpClient admin)
    {
        using var import = await Post(admin, "memberships/import", new { });
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Import_is_deterministic_and_never_widens_the_operator_grant()
    {
        await using var host = MembershipHost();
        using var admin = await Member(host, "synthetic");
        using var first = await Post(admin, "memberships/import", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = (await first.Content.ReadFromJsonAsync<MembershipImportDto>(Ct))!;
        Assert.Equal(4, result.Imported); // Only the caller's tenant; other tenant entries are untouched.
        using var second = await Post(admin, "memberships/import", new { });
        Assert.Equal(new MembershipImportDto(0, 4, 0), await second.Content.ReadFromJsonAsync<MembershipImportDto>(Ct));
        var b = await MembershipOf(admin, memberB);
        Assert.Equal("Active", b.Status); Assert.Equal(0, b.AccessRevision); Assert.True(b.RoleBound); Assert.False(b.Administrator);
        using (var detail = await admin.GetAsync($"/api/v1/memberships/{b.Id}", Ct))
        {
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            Assert.True(detail.Headers.CacheControl?.NoStore);
            Assert.Equal($"\"{b.Version}\"", detail.Headers.ETag?.Tag);
            var body = await detail.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("member-b", body); Assert.DoesNotContain(MemberIssuer, body);
        }
        // Persisted state alone grants nothing: removing the operator entry removes access.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        using var member = await Member(host, "member-b");
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        var original = settings.Memberships[1];
        settings.Memberships[1] = original with { Subject = "removed-from-operator-source" };
        // Authenticated principal without a trusted operator membership: existing 403 semantics.
        try { Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode); }
        finally { settings.Memberships[1] = original; }
        await using var db = Db();
        var accounts = await db.UserAccounts.AsNoTracking().Where(a => a.Issuer == MemberIssuer).ToArrayAsync(Ct);
        Assert.Equal(4, accounts.Length);
        Assert.All(accounts, a => Assert.Equal("ConfigurationImport", a.Source));
        Assert.DoesNotContain(await db.UserAccounts.AsNoTracking().Select(a => a.Id).ToArrayAsync(Ct), id => id == otherTenantAdmin);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Suspension_and_revocation_deny_the_next_request_and_resume_requires_a_new_session()
    {
        await using var host = MembershipHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        using var member = await Member(host, "member-b");
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        var b = await MembershipOf(admin, memberB);

        using var stale = await Post(admin, $"memberships/{b.Id}/suspend", new { expectedVersion = b.Version + 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("version_conflict", await Code(stale));
        using var missingVersion = await Post(admin, $"memberships/{b.Id}/suspend", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        using var noKey = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/memberships/{b.Id}/suspend") { Content = JsonContent.Create(new { expectedVersion = b.Version }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.SendAsync(noKey, Ct)).StatusCode);

        var key = Guid.NewGuid().ToString();
        using var suspended = await Post(admin, $"memberships/{b.Id}/suspend", new { expectedVersion = b.Version }, key);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var afterSuspend = (await suspended.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        Assert.Equal("Suspended", afterSuspend.Status); Assert.Equal(1, afterSuspend.AccessRevision);
        using var replay = await Post(admin, $"memberships/{b.Id}/suspend", new { expectedVersion = b.Version }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        using var reused = await Post(admin, $"memberships/{b.Id}/revoke", new { expectedVersion = afterSuspend.Version }, key);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode); Assert.Equal("idempotency_key_reused", await Code(reused));

        // Existing session: the very next request is denied and the session observation is attributed.
        using var denied = await member.GetAsync("/api/v1/vessels", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var correlation = Assert.Single(denied.Headers.GetValues("X-Correlation-ID"));
        await using (var db = Db())
            Assert.True(await db.Set<SecurityEvent>().AnyAsync(e => e.CorrelationId == correlation && e.Action == "SessionRevalidation" &&
                e.Result == "membership_inactive" && e.ActorId == memberB.ToString(), Ct));
        var revokedSession = (await member.GetFromJsonAsync<SessionDto>("/api/v1/auth/session", Ct))!;
        Assert.False(revokedSession.Authenticated); Assert.Null(revokedSession.ActorId); Assert.Equal("", revokedSession.CsrfToken);

        using var resumed = await Post(admin, $"memberships/{b.Id}/resume", new { expectedVersion = afterSuspend.Version });
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var afterResume = (await resumed.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        Assert.Equal("Active", afterResume.Status); Assert.Equal(2, afterResume.AccessRevision);
        // The pre-suspension session does not revive; a new session stamped with the current revision does.
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        using var fresh = await Member(host, "member-b", 2);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/v1/vessels", Ct)).StatusCode);

        using var revoked = await Post(admin, $"memberships/{b.Id}/revoke", new { expectedVersion = afterResume.Version });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var afterRevoke = (await revoked.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        Assert.Equal("Revoked", afterRevoke.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        using var newest = await Member(host, "member-b", afterRevoke.AccessRevision);
        Assert.Equal(HttpStatusCode.Unauthorized, (await newest.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        using var terminal = await Post(admin, $"memberships/{b.Id}/resume", new { expectedVersion = afterRevoke.Version });
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode); Assert.Equal("invalid_transition", await Code(terminal));

        await using var audit = Db();
        var events = await audit.AuditEvents.AsNoTracking().Where(e => e.ResourceId == b.Id && e.ResourceKind == "Membership").ToArrayAsync(Ct);
        Assert.Equal(4, events.Length); // Import + suspend + resume + revoke; replay and rejected commands write no business audit.
        Assert.All(events, e => { Assert.Equal(actorId.ToString(), e.ActorId); Assert.Equal("WebCommand", e.Source); Assert.DoesNotContain("member-b", e.ChangeSummary); });
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Last_admin_is_protected_from_voluntary_removal_but_not_from_security_suspension()
    {
        await using var host = MembershipHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        var c = await MembershipOf(admin, adminC);
        Assert.True(c.Administrator);
        using var suspendC = await Post(admin, $"memberships/{c.Id}/suspend", new { expectedVersion = c.Version });
        Assert.Equal(HttpStatusCode.OK, suspendC.StatusCode);
        var a = await MembershipOf(admin, actorId);
        foreach (var segment in new[] { "suspend", "revoke" })
        {
            using var last = await Post(admin, $"memberships/{a.Id}/{segment}", new { expectedVersion = a.Version });
            Assert.Equal(HttpStatusCode.Conflict, last.StatusCode); Assert.Equal("last_admin", await Code(last));
        }
        Assert.Equal("Active", (await MembershipOf(admin, actorId)).Status);
        // The general administrator cannot use the forced path; it is a separate exact permission.
        using var notSecurity = await Post(admin, $"memberships/{a.Id}/security-suspend", new { expectedVersion = a.Version });
        Assert.Equal(HttpStatusCode.Forbidden, notSecurity.StatusCode);
        using var security = await Member(host, "security-d");
        using var forced = await Post(security, $"memberships/{a.Id}/security-suspend", new { expectedVersion = a.Version });
        Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
        Assert.Equal("Suspended", (await forced.Content.ReadFromJsonAsync<MembershipDto>(Ct))!.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/memberships", Ct)).StatusCode);
        await using var db = Db();
        var recovery = Assert.Single(await db.TenantAccessRecoveries.AsNoTracking().ToArrayAsync(Ct));
        Assert.Equal(a.Id, recovery.MembershipId); Assert.Equal("SecuritySuspension", recovery.Cause);
        // No hidden administrator account or membership is created by recovery.
        Assert.Equal(4, await db.UserAccounts.CountAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Delegation_is_bounded_attributed_and_expires_by_server_time()
    {
        var clock = new MembershipClock(DateTimeOffset.UtcNow);
        await using var host = MembershipHost(clock);
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        var b = await MembershipOf(admin, memberB); var d = await MembershipOf(admin, securityD); var a = await MembershipOf(admin, actorId);
        object Body(Guid delegateId, string[] permissions, TimeSpan? window = null) =>
            new { delegateMembershipId = delegateId, permissions, validFrom = clock.Now, validUntil = clock.Now + (window ?? TimeSpan.FromHours(1)) };

        using var wider = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["tug.write"]));
        Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode);
        using var administrative = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["membership.manage"]));
        Assert.Equal(HttpStatusCode.BadRequest, administrative.StatusCode);
        using var selfApproved = await Post(admin, $"memberships/{b.Id}/delegations", Body(a.Id, ["crew.read"]));
        Assert.Equal(HttpStatusCode.Forbidden, selfApproved.StatusCode); Assert.Equal("separation_of_duties", await Code(selfApproved));
        using var tooLong = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], TimeSpan.FromDays(91)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using var created = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"]));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var delegation = (await created.Content.ReadFromJsonAsync<DelegationDto>(Ct))!;
        Assert.Equal("Effective", delegation.State);
        using var delegate_ = await Member(host, "security-d");
        var current = (await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!;
        var effective = Assert.Single(current.ReceivedDelegations);
        Assert.Equal(delegation.Id, effective.DelegationId); Assert.Equal(memberB, effective.DelegatorActorId); Assert.Equal(["crew.read"], effective.Permissions);

        clock.Now = delegation.ValidUntil; // Exclusive end: the window never extends itself.
        Assert.Empty((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
        Assert.Equal("Expired", Assert.Single((await admin.GetFromJsonAsync<DelegationDto[]>($"/api/v1/memberships/{b.Id}/delegations", Ct))!).State);

        // Explicit revoke and cascade on source suspension.
        clock.Now = clock.Now.AddMinutes(1);
        using var second = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"]));
        var secondDelegation = (await second.Content.ReadFromJsonAsync<DelegationDto>(Ct))!;
        using var revoke = await Post(admin, $"delegations/{secondDelegation.Id}/revoke", new { expectedVersion = secondDelegation.Version });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal("Revoked", (await revoke.Content.ReadFromJsonAsync<DelegationDto>(Ct))!.State);
        using var third = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"]));
        var thirdDelegation = (await third.Content.ReadFromJsonAsync<DelegationDto>(Ct))!;
        var currentB = await MembershipOf(admin, memberB);
        using var suspend = await Post(admin, $"memberships/{b.Id}/suspend", new { expectedVersion = currentB.Version });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        Assert.Empty((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
        await using var db = Db();
        var stored = await db.MembershipDelegations.AsNoTracking().SingleAsync(x => x.Id == thirdDelegation.Id, Ct);
        Assert.Equal("Revoked", stored.Status); Assert.Equal("MembershipSuspended", stored.RevocationReason);
        Assert.Equal(actorId.ToString(), stored.ApprovedBy);
        var audit = await db.AuditEvents.AsNoTracking().Where(e => e.ResourceId == delegation.Id).ToArrayAsync(Ct);
        Assert.NotEmpty(audit); Assert.All(audit, e => Assert.DoesNotContain("crew.read", e.ChangeSummary));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Membership_administration_is_tenant_scoped_and_permission_gated()
    {
        await using var host = MembershipHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        var b = await MembershipOf(admin, memberB);
        using var other = await Member(host, "other-tenant-admin");
        await Imported(other);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/memberships/{b.Id}", Ct)).StatusCode);
        using var crossSuspend = await Post(other, $"memberships/{b.Id}/suspend", new { expectedVersion = b.Version });
        Assert.Equal(HttpStatusCode.NotFound, crossSuspend.StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<MembershipPageDto>("/api/v1/memberships", Ct))!.Items, x => x.AccountId == memberB);
        using var member = await Member(host, "member-b");
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/memberships", Ct)).StatusCode);
        using var memberImport = await Post(member, "memberships/import", new { });
        Assert.Equal(HttpStatusCode.Forbidden, memberImport.StatusCode);
        using var forgedBody = await Post(admin, $"memberships/{b.Id}/suspend", new { expectedVersion = b.Version, status = "Active", tenantId = seed.Tenants[1].Id });
        Assert.Equal(HttpStatusCode.BadRequest, forgedBody.StatusCode);
        using var noCsrf = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/memberships/{b.Id}/suspend") { Content = JsonContent.Create(new { expectedVersion = b.Version }) };
        noCsrf.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var anonymousCsrf = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        anonymousCsrf.DefaultRequestHeaders.Add("X-Membership-Subject", "synthetic");
        Assert.Equal(HttpStatusCode.Forbidden, (await anonymousCsrf.SendAsync(noCsrf, Ct)).StatusCode);
        Assert.Equal("Active", (await MembershipOf(admin, memberB)).Status);
    }

    private sealed class MembershipTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Membership-Subject", out var subject)) return Task.FromResult(AuthenticateResult.NoResult());
            List<Claim> claims = [new("sub", subject.ToString()), new("iss", MemberIssuer)];
            if (Request.Headers.TryGetValue("X-Membership-Arv", out var revision)) claims.Add(new(MembershipSession.AccessRevisionClaim, revision.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
