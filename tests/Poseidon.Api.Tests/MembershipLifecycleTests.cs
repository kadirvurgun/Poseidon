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
using Microsoft.Extensions.Hosting;
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

    private WebApplicationFactory<Program> MembershipHost(MembershipClock? clock = null, Action<IServiceCollection>? services = null) => Host().WithWebHostBuilder(b =>
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
            // Source scope for delegation tests: read-only grant on one vessel plus its company.
            ["Authentication:Oidc:Memberships:1:VesselGrants:0:VesselId"] = SeedVessel.Id.ToString(), ["Authentication:Oidc:Memberships:1:VesselGrants:0:CanEdit"] = "false",
            ["Authentication:Oidc:Memberships:1:ActiveCompanyId"] = SeedVessel.CompanyId.ToString(),
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
            services?.Invoke(s);
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

    private async Task<string?> MembershipCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).TryGetProperty("code", out var code) ? code.GetString() : null;

    private async Task<string?> FieldError(HttpResponseMessage response, string field) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("errors").TryGetProperty(field, out var errors) ? errors[0].GetString() : null;

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
        var key = Guid.NewGuid().ToString();
        using var first = await Post(admin, "memberships/import", new { }, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = (await first.Content.ReadFromJsonAsync<MembershipImportDto>(Ct))!;
        Assert.Equal(4, result.Imported); // Only the caller's tenant; other tenant entries are untouched.
        Assert.Equal(0, result.Skipped); Assert.Empty(result.Skips);
        using var second = await Post(admin, "memberships/import", new { });
        var again = (await second.Content.ReadFromJsonAsync<MembershipImportDto>(Ct))!;
        Assert.Equal((0, 4, 0), (again.Imported, again.AlreadyPresent, again.Skipped));
        // QA follow-up: a replay returns the ORIGINAL committed counts stored with the receipt.
        using var replay = await Post(admin, "memberships/import", new { }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        var replayed = (await replay.Content.ReadFromJsonAsync<MembershipImportDto>(Ct))!;
        Assert.Equal((4, 0, 0), (replayed.Imported, replayed.AlreadyPresent, replayed.Skipped));
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
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal("version_conflict", await MembershipCode(stale));
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
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode); Assert.Equal("idempotency_key_reused", await MembershipCode(reused));

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
        Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode); Assert.Equal("invalid_transition", await MembershipCode(terminal));

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
        // The DTO flag and the last-admin count use the same predicate: a suspended admin is not counted.
        Assert.False((await suspendC.Content.ReadFromJsonAsync<MembershipDto>(Ct))!.Administrator);
        Assert.False((await MembershipOf(admin, adminC)).Administrator);
        var a = await MembershipOf(admin, actorId);
        Assert.True(a.Administrator);
        foreach (var segment in new[] { "suspend", "revoke" })
        {
            using var last = await Post(admin, $"memberships/{a.Id}/{segment}", new { expectedVersion = a.Version });
            Assert.Equal(HttpStatusCode.Conflict, last.StatusCode); Assert.Equal("last_admin", await MembershipCode(last));
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
        object Body(Guid delegateId, string[] permissions, TimeSpan? window = null, string? reason = "PlannedAbsence", Guid? company = null, object[]? vessels = null) =>
            new { delegateMembershipId = delegateId, permissions, validFrom = clock.Now, validUntil = clock.Now + (window ?? TimeSpan.FromHours(1)),
                reasonCode = reason, companyId = company, vesselScope = vessels ?? [new { vesselId = SeedVessel.Id, canEdit = false }] };

        // 52.02 paragraph:102: scope wider than the source's current scope and a missing/free-text reason are rejected.
        using var editEscalation = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], vessels: [new { vesselId = SeedVessel.Id, canEdit = true }]));
        Assert.Equal(HttpStatusCode.BadRequest, editEscalation.StatusCode); Assert.Equal("exceeds_source_scope", await FieldError(editEscalation, "vesselScope"));
        using var otherVessel = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], vessels: [new { vesselId = Guid.NewGuid(), canEdit = false }]));
        Assert.Equal(HttpStatusCode.BadRequest, otherVessel.StatusCode); Assert.Equal("exceeds_source_scope", await FieldError(otherVessel, "vesselScope"));
        using var otherCompany = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], company: Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, otherCompany.StatusCode); Assert.Equal("exceeds_source_scope", await FieldError(otherCompany, "companyId"));
        using var noReason = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], reason: null));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode); Assert.Equal("required", await FieldError(noReason, "reasonCode"));
        using var freeText = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], reason: "on leave until Monday"));
        Assert.Equal(HttpStatusCode.BadRequest, freeText.StatusCode); Assert.Equal("unknown_reason", await FieldError(freeText, "reasonCode"));
        using var unscoped = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], vessels: []));
        Assert.Equal(HttpStatusCode.BadRequest, unscoped.StatusCode); Assert.Equal("required", await FieldError(unscoped, "scope"));
        await using (var none = Db()) Assert.False(await none.MembershipDelegations.AnyAsync(Ct));

        using var wider = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["tug.write"]));
        Assert.Equal(HttpStatusCode.BadRequest, wider.StatusCode);
        using var administrative = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["membership.manage"]));
        Assert.Equal(HttpStatusCode.BadRequest, administrative.StatusCode);
        using var selfApproved = await Post(admin, $"memberships/{b.Id}/delegations", Body(a.Id, ["crew.read"]));
        Assert.Equal(HttpStatusCode.Forbidden, selfApproved.StatusCode); Assert.Equal("separation_of_duties", await MembershipCode(selfApproved));
        using var tooLong = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], TimeSpan.FromDays(91)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        using var created = await Post(admin, $"memberships/{b.Id}/delegations", Body(d.Id, ["crew.read"], company: SeedVessel.CompanyId));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var delegation = (await created.Content.ReadFromJsonAsync<DelegationDto>(Ct))!;
        Assert.Equal("Effective", delegation.State);
        Assert.Equal("PlannedAbsence", delegation.ReasonCode); Assert.Equal(SeedVessel.CompanyId, delegation.CompanyId);
        Assert.Equal([new VesselGrant(SeedVessel.Id, false)], delegation.VesselScope);
        using var delegate_ = await Member(host, "security-d");
        var current = (await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!;
        var effective = Assert.Single(current.ReceivedDelegations);
        Assert.Equal(delegation.Id, effective.DelegationId); Assert.Equal(memberB, effective.DelegatorActorId); Assert.Equal(["crew.read"], effective.Permissions);
        // The delegated scope travels with the delegation; it is never the delegate's own (D has no vessel grant).
        Assert.Equal([new VesselGrant(SeedVessel.Id, false)], effective.VesselGrants); Assert.Equal(SeedVessel.CompanyId, effective.ActiveCompanyId);
        Assert.False(effective.TenantWide); Assert.Equal("PlannedAbsence", effective.ReasonCode);

        // Request-time narrowing: the source loses its vessel grant, then its company, in the operator source.
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var original = settings.Memberships[1];
        try
        {
            settings.Memberships[1] = original with { VesselGrants = [] };
            var narrowed = Assert.Single((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
            Assert.Empty(narrowed.VesselGrants); Assert.Equal(SeedVessel.CompanyId, narrowed.ActiveCompanyId);
            settings.Memberships[1] = original with { VesselGrants = [] };
            settings.Memberships[1].ActiveCompanyId = Guid.NewGuid();
            Assert.Empty((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
            settings.Memberships[1] = original with { Permissions = ["directory.read"] };
            settings.Memberships[1].ActiveCompanyId = original.ActiveCompanyId;
            Assert.Empty((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);
        }
        finally { settings.Memberships[1] = original; }
        Assert.Single((await delegate_.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.ReceivedDelegations);

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

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Import_skips_an_identity_bound_in_another_tenant_and_configuration_keeps_governing_it()
    {
        await using var host = MembershipHost();
        // The same issuer/subject (global unique) already belongs to an account of tenant 1.
        await using (var other = Db(seed.Tenants[1].Id))
        {
            other.UserAccounts.Add(new UserAccount { TenantId = seed.Tenants[1].Id, Id = Guid.NewGuid(), Issuer = MemberIssuer, Subject = "member-b",
                Source = "ConfigurationImport", CreatedAt = DateTimeOffset.UtcNow });
            await other.SaveChangesAsync(Ct);
        }
        using var admin = await Member(host, "synthetic");
        using var import = await Post(admin, "memberships/import", new { });
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var result = (await import.Content.ReadFromJsonAsync<MembershipImportDto>(Ct))!;
        Assert.Equal((3, 0, 1), (result.Imported, result.AlreadyPresent, result.Skipped));
        var skip = Assert.Single(result.Skips);
        Assert.Equal(memberB, skip.ActorId); Assert.Equal("identity_bound_other_tenant", skip.Reason);
        Assert.DoesNotContain("member-b", await import.Content.ReadAsStringAsync(Ct));
        await using (var db = Db())
        {
            Assert.False(await db.AccountMemberships.AnyAsync(m => m.AccountId == memberB, Ct));
            Assert.Equal(3, await db.AccountMemberships.CountAsync(Ct));
        }
        // No persisted state in this tenant => the operator configuration keeps governing member-b.
        using var member = await Member(host, "member-b");
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/vessels", Ct)).StatusCode);
        Assert.False((await member.GetFromJsonAsync<CurrentMembershipDto>("/api/v1/memberships/current", Ct))!.Persisted);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Security_suspension_can_only_be_resumed_by_the_security_authority_and_closes_recovery()
    {
        await using var host = MembershipHost();
        using var admin = await Member(host, "synthetic");
        await Imported(admin);
        using var security = await Member(host, "security-d");
        var c = await MembershipOf(admin, adminC);
        using var forcedC = await Post(security, $"memberships/{c.Id}/security-suspend", new { expectedVersion = c.Version });
        Assert.Equal(HttpStatusCode.OK, forcedC.StatusCode);
        var suspendedC = (await forcedC.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        Assert.Equal("SecuritySuspension", suspendedC.StatusReason);
        await using (var db = Db()) Assert.False(await db.TenantAccessRecoveries.AnyAsync(Ct)); // A remained an active admin.
        // Ordinary membership.manage cannot lift a security suspension.
        using var ordinary = await Post(admin, $"memberships/{c.Id}/resume", new { expectedVersion = suspendedC.Version });
        Assert.Equal(HttpStatusCode.Forbidden, ordinary.StatusCode); Assert.Equal("permission_denied", await MembershipCode(ordinary));
        Assert.Equal("Suspended", (await MembershipOf(admin, adminC)).Status);
        using var lifted = await Post(security, $"memberships/{c.Id}/resume", new { expectedVersion = suspendedC.Version });
        Assert.Equal(HttpStatusCode.OK, lifted.StatusCode);
        var resumedC = (await lifted.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        Assert.Equal("Active", resumedC.Status); Assert.Equal("SecurityResume", resumedC.StatusReason);

        // An administrative suspension stays with membership.manage; the security authority cannot resume it.
        using var adminSuspend = await Post(admin, $"memberships/{c.Id}/suspend", new { expectedVersion = resumedC.Version });
        var administrative = (await adminSuspend.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        using var wrongAuthority = await Post(security, $"memberships/{c.Id}/resume", new { expectedVersion = administrative.Version });
        Assert.Equal(HttpStatusCode.Forbidden, wrongAuthority.StatusCode);

        // Forced removal of the last admin raises RECOVERY_REQUIRED; resuming it closes the case with an audited reason.
        var a = await MembershipOf(admin, actorId);
        using var forcedA = await Post(security, $"memberships/{a.Id}/security-suspend", new { expectedVersion = a.Version });
        Assert.Equal(HttpStatusCode.OK, forcedA.StatusCode);
        var suspendedA = (await forcedA.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        await using (var db = Db()) Assert.Null(Assert.Single(await db.TenantAccessRecoveries.AsNoTracking().ToArrayAsync(Ct)).ResolvedAt);
        using var resumeA = await Post(security, $"memberships/{a.Id}/resume", new { expectedVersion = suspendedA.Version });
        Assert.Equal(HttpStatusCode.OK, resumeA.StatusCode);
        var resumedA = (await resumeA.Content.ReadFromJsonAsync<MembershipDto>(Ct))!;
        await using (var db = Db())
        {
            var recovery = Assert.Single(await db.TenantAccessRecoveries.AsNoTracking().ToArrayAsync(Ct));
            Assert.NotNull(recovery.ResolvedAt); Assert.Equal("SecuritySuspensionResumed", recovery.Resolution);
            var audit = await db.AuditEvents.AsNoTracking().Where(e => e.ResourceId == a.Id && e.ResourceKind == "AccessRecovery").ToArrayAsync(Ct);
            Assert.Contains(audit, e => e.ChangeSummary!.Contains("SecuritySuspensionResumed") && e.ActorId == securityD.ToString());
        }
        using var freshAdmin = await Member(host, "synthetic", resumedA.AccessRevision);
        Assert.False((await freshAdmin.GetFromJsonAsync<MembershipPageDto>("/api/v1/memberships", Ct))!.RecoveryRequired);
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Two_administrators_suspending_each_other_concurrently_leave_exactly_one_active()
    {
        await using var host = MembershipHost();
        using var adminA = await Member(host, "synthetic");
        await Imported(adminA);
        using var adminCClient = await Member(host, "admin-c");
        var a = await MembershipOf(adminA, actorId); var c = await MembershipOf(adminA, adminC);
        // Hold both rows with a test-side row lock so both commands have passed the session gate and
        // read the administrator set before either can write; then release them together.
        await using var locker = new Npgsql.NpgsqlConnection(postgres.GetConnectionString());
        await locker.OpenAsync(Ct);
        await using var hold = await locker.BeginTransactionAsync(Ct);
        await using (var lockRows = new Npgsql.NpgsqlCommand("SELECT 1 FROM identity.\"Memberships\" WHERE \"Id\" = ANY(@ids) FOR UPDATE", locker, hold))
        {
            lockRows.Parameters.AddWithValue("ids", new[] { a.Id, c.Id });
            await lockRows.ExecuteNonQueryAsync(Ct);
        }
        var first = Post(adminA, $"memberships/{c.Id}/suspend", new { expectedVersion = c.Version });
        var second = Post(adminCClient, $"memberships/{a.Id}/suspend", new { expectedVersion = a.Version });
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        // Poll from a separate autocommit connection: pg_stat_activity is snapshotted per transaction.
        await using var observer = new Npgsql.NpgsqlConnection(postgres.GetConnectionString());
        await observer.OpenAsync(Ct);
        while (true)
        {
            await using var waiting = new Npgsql.NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", observer);
            if ((long)(await waiting.ExecuteScalarAsync(Ct))! >= 2 || first.IsCompleted || second.IsCompleted) break;
            Assert.True(DateTimeOffset.UtcNow < deadline, "Both membership commands should reach the held rows.");
            await Task.Delay(50, Ct);
        }
        await hold.CommitAsync(Ct);
        using var r1 = await first; using var r2 = await second;
        var statuses = new[] { r1.StatusCode, r2.StatusCode };
        // Each response body is a single-read stream: read each code exactly once.
        var codes = new[] { await MembershipCode(r1), await MembershipCode(r2) };
        var observed = $"{(int)r1.StatusCode}:{codes[0]} / {(int)r2.StatusCode}:{codes[1]}";
        Assert.True(statuses.Count(x => x == HttpStatusCode.OK) == 1 && statuses.Count(x => x == HttpStatusCode.Conflict) == 1, observed);
        Assert.Contains(codes[r1.StatusCode == HttpStatusCode.Conflict ? 0 : 1], new[] { "conflict_changed", "last_admin" });
        await using var db = Db();
        var states = await db.AccountMemberships.AsNoTracking().Where(m => m.Id == a.Id || m.Id == c.Id).Select(m => m.Status).ToArrayAsync(Ct);
        Assert.Equal(1, states.Count(x => x == "Active")); Assert.Equal(1, states.Count(x => x == "Suspended"));
        Assert.Equal(1, await db.MembershipCommandReceipts.CountAsync(r => r.Action == "MembershipSuspend", Ct));
        Assert.False(await db.TenantAccessRecoveries.AnyAsync(Ct));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5202")]
    public async Task Session_gate_fails_closed_with_503_when_membership_state_is_unavailable()
    {
        // The gate's dedicated reader points at an unavailable database; the rest of the host is intact.
        var unavailable = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Poseidon"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=unavailable;Timeout=2;Command Timeout=2"
        }).Build();
        await using var host = MembershipHost(services: s =>
        {
            s.RemoveAll<MembershipStateReader>();
            s.AddSingleton(sp => new MembershipStateReader(unavailable, sp.GetRequiredService<IHostEnvironment>()));
        });
        using var member = host.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        member.DefaultRequestHeaders.Add("X-Membership-Subject", "synthetic");
        using var read = await member.GetAsync("/api/v1/vessels", Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, read.StatusCode);
        Assert.Equal("service_unavailable", await MembershipCode(read));
        using var session = await member.GetAsync("/api/v1/auth/session", Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, session.StatusCode); // No authenticated session fallback.
        using var command = await Post(member, "memberships/import", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, command.StatusCode);
        await using var db = Db();
        Assert.False(await db.UserAccounts.AnyAsync(Ct)); Assert.False(await db.MembershipCommandReceipts.AnyAsync(Ct));
        // Unauthenticated public liveness is unaffected: only authenticated authority is withheld.
        using var anonymous = host.CreateClient(new() { BaseAddress = new("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live", Ct)).StatusCode);
    }

    private sealed class MembershipTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Membership-Subject", out var subject)) return Task.FromResult(AuthenticateResult.NoResult());
            List<Claim> claims = [new("sub", subject.ToString()), new("iss", MemberIssuer)];
            if (Request.Headers.TryGetValue("X-Membership-Arv", out var revision)) claims.Add(new(MembershipSession.AccessRevisionClaim, revision.ToString()));
            // USER-ADMIN-01: the binding the OIDC login stamps for an admin-provisioned identity ("tenant/actor").
            if (Request.Headers.TryGetValue("X-Membership-Provisioned", out var binding) && binding.ToString().Split('/') is [var tenant, var actor])
                claims.AddRange([new(ActorResolver.ProvisionedTenantClaim, tenant), new(ClaimTypes.NameIdentifier, actor), new(ActorResolver.SessionSourceClaim, ActorResolver.ProvisionedSource)]);
            // USER-ADMIN-02: the short invitation session the OIDC login issues for an identity without membership.
            if (Request.Headers.ContainsKey("X-Membership-Invitee")) claims.Add(new(ActorResolver.SessionSourceClaim, ActorResolver.InvitationSource));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
