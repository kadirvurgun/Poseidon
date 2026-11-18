using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// PLAN-1511-5201 / 52.01 Rev6: actual host, antiforgery, Idempotency-Key, CAS and owned PostgreSQL.
public sealed partial class DirectoryWorkspaceTests
{
    private static readonly Guid HrApprover = Guid.Parse("5201a0b0-5201-4201-8201-520100000002");
    private static readonly string[] HrPermissions = ["directory.read", "directory.write", "mode.tug", "mode.cross",
        "hr.legal_entity.read", "hr.legal_entity.write", "hr.person.read", "hr.person.write", "hr.person.verify",
        "hr.employment.read", "hr.employment.write", "hr.employment.approve", "hr.assignment.read", "hr.assignment.write",
        "hr.assignment.approve", "hr.actual.record"];

    // Two genuinely separate synthetic humans in the same tenant; HR grants are explicit, no admin fallback.
    private WebApplicationFactory<Program> HrHost(bool approver = false, bool hr = true) =>
        Host().WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => {
            var config = new Dictionary<string, string?> {
                ["Authentication:Oidc:Issuer"] = "https://directory-test.example",
                ["Authentication:Oidc:Authority"] = "https://directory-test.example",
                ["Authentication:Oidc:ClientId"] = "synthetic-hr"
            };
            for (var i = 0; i < 2; i++)
            {
                var prefix = $"Authentication:Oidc:Memberships:{i}:";
                config[prefix + "Issuer"] = "https://directory-test.example";
                config[prefix + "Subject"] = i == 0 ? "synthetic" : "hr-approver";
                config[prefix + "ActorId"] = (i == 0 ? actorId : HrApprover).ToString();
                config[prefix + "TenantId"] = seed.Tenants[0].Id.ToString();
                config[prefix + "ActorKind"] = "Human";
                config[prefix + "RoleReferences:0"] = "91c26eb5-cf16-4553-981d-1c275358c535";
                config[prefix + "TenantWideRead"] = "true"; config[prefix + "TenantWideManage"] = "true";
                var permissions = hr ? HrPermissions : HrPermissions.Where(p => !p.StartsWith("hr.", StringComparison.Ordinal)).ToArray();
                for (var n = 0; n < 50; n++) config[prefix + $"Permissions:{n}"] = n < permissions.Length ? permissions[n] : "unused";
            }
            c.AddInMemoryCollection(config);
        }).ConfigureTestServices(s => s.AddAuthentication(o => {
            o.DefaultAuthenticateScheme = "CatalogSynthetic"; o.DefaultChallengeScheme = "CatalogSynthetic"; o.DefaultForbidScheme = "CatalogSynthetic";
        }).AddScheme<CatalogAuthenticationOptions, CatalogTestHandler>("CatalogSynthetic", o => o.Subject = approver ? "hr-approver" : "synthetic")));

    private static DateTimeOffset HrNow() { var now = DateTimeOffset.UtcNow; return new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero); }
    private async Task<JsonElement> HrJson(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected}, actual {(int)response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    private static string HrCode(JsonElement problem) => problem.GetProperty("code").GetString()!;
    private async Task<JsonElement> HrGet(HttpClient client, string path, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.GetAsync("/api/v1/" + path, Ct);
        Assert.True(response.Headers.CacheControl?.NoStore, "HR reads must be no-store.");
        return await HrJson(response, expected);
    }
    private Task<HttpResponseMessage> HrSend(HttpClient client, string path, object body, string? key = null, bool put = false) =>
        Send(client, path, body, key ?? Guid.NewGuid().ToString(), put);
    private object HrEntityUpdate(uint version, string? legalName = "Synthetic Denizcilik A.S.") => new {
        expectedVersion = version, legalName, countryCode = "TR", hrTimeZone = "Europe/Istanbul", baseLocale = "tr-TR",
        effectiveFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), effectiveTo = (DateTimeOffset?)null,
        sourceKind = "UserDeclared", sourceRef = "synthetic-fixture/legal-entity" };

    [Fact]
    [Trait("Requirement", "PLAN-1511-5201")]
    public async Task Hr_legal_entity_person_employment_plan_and_actual_follow_the_audited_command_contract()
    {
        await using var authorHost = HrHost(); await using var approverHost = HrHost(approver: true);
        using var author = Client(authorHost); using var approver = Client(approverHost);
        await Token(author); await Token(approver);
        var company = SeedVessel.CompanyId;

        // P5201-AC-01/02: the existing Company row is the LegalEntity; fields carry field/source revisions; CAS protects updates.
        var entity = await HrGet(author, $"hr/legal-entities/{company}");
        Assert.Equal("Draft", entity.GetProperty("status").GetString());
        Assert.Equal(5, entity.GetProperty("missingForActivation").GetArrayLength());
        var version = entity.GetProperty("version").GetUInt32();
        var key = Guid.NewGuid().ToString();
        var updated = await HrJson(await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate(version), key, put: true));
        Assert.Equal("Synthetic Denizcilik A.S.", updated.GetProperty("legalName").GetString());
        Assert.Equal(1, updated.GetProperty("revision").GetInt64());
        using (var replay = await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate(version), key, put: true))
        {
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
            Assert.Equal(1, (await HrJson(replay)).GetProperty("revision").GetInt64());
        }
        Assert.Equal("idempotency_key_reused", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate(version, "Other"), key, put: true), HttpStatusCode.Conflict)));
        Assert.Equal("version_conflict", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}", HrEntityUpdate(version, "Stale"), put: true), HttpStatusCode.Conflict)));
        var revisions = await HrGet(author, $"hr/legal-entities/{company}/field-revisions");
        Assert.Equal(5, revisions.GetProperty("items").GetArrayLength());
        Assert.All(revisions.GetProperty("items").EnumerateArray(), r => {
            Assert.Equal(actorId.ToString(), r.GetProperty("actorId").GetString());
            Assert.Equal("UserDeclared", r.GetProperty("sourceKind").GetString());
            Assert.Equal("synthetic-fixture/legal-entity", r.GetProperty("sourceRef").GetString());
        });
        version = updated.GetProperty("version").GetUInt32();
        var active = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/status", new { expectedVersion = version, targetState = "Active", reason = "Synthetic activation" }));
        Assert.Equal("Active", active.GetProperty("status").GetString());
        Assert.Empty(active.GetProperty("missingForActivation").EnumerateArray());

        // An incomplete legal entity cannot be activated; nothing is defaulted (HR_REFERENCE_INCOMPLETE).
        var created = await HrJson(await Send(author, "companies", new { code = "HR-INCOMPLETE-" + Guid.NewGuid().ToString("N")[..6], name = "Synthetic Draft Co" }));
        var draftCompany = created.GetProperty("id").GetGuid();
        var incomplete = await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/status",
            new { expectedVersion = created.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Attempt" }), (HttpStatusCode)422);
        Assert.Equal("hr_reference_incomplete", HrCode(incomplete));
        Assert.True(incomplete.GetProperty("errors").TryGetProperty("legalName", out _));

        // P5201-AC-03: typed registration identifiers; duplicates rejected; detail shows mask.
        var identifier = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers",
            new { type = "TradeRegistry", issuer = "tr-synthetic-registry", value = "SYN 990011", sourceKind = "RegistryExtract", sourceRef = "synthetic-extract-1" }));
        Assert.Equal("SYN990011", identifier.GetProperty("value").GetString());
        Assert.Equal("*****0011", identifier.GetProperty("maskedValue").GetString());
        Assert.Equal("hr_identifier_duplicate", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/identifiers",
            new { type = "TradeRegistry", issuer = "TR-SYNTHETIC-REGISTRY", value = "syn990011", sourceKind = "UserDeclared", sourceRef = (string?)null }), HttpStatusCode.Conflict)));

        // Identifiers are never edited in place: retirement keeps the history and frees the value.
        var retired = await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers/{identifier.GetProperty("id").GetGuid()}/retire",
            new { expectedVersion = identifier.GetProperty("version").GetUInt32(), reason = "Synthetic correction" }));
        Assert.NotEqual(JsonValueKind.Null, retired.GetProperty("retiredAt").ValueKind);
        Assert.Equal("invalid_transition", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers/{identifier.GetProperty("id").GetGuid()}/retire",
            new { expectedVersion = retired.GetProperty("version").GetUInt32(), reason = "Again" }), HttpStatusCode.Conflict)));
        await HrJson(await HrSend(author, $"hr/legal-entities/{company}/identifiers",
            new { type = "TradeRegistry", issuer = "TR-SYNTHETIC-REGISTRY", value = "SYN990011", sourceKind = "RegistryExtract", sourceRef = "synthetic-extract-2" }));

        // P5201-AC-04: typed roles; one operator per vessel and period.
        var from = HrNow().AddDays(-365);
        await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles",
            new { role = "Operator", vesselId = SeedVessel.Id, effectiveFrom = from, effectiveTo = (DateTimeOffset?)null, sourceKind = "UserDeclared", sourceRef = (string?)null }));
        Assert.Equal("hr_role_overlap", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/roles",
            new { role = "Operator", vesselId = SeedVessel.Id, effectiveFrom = from.AddDays(10), effectiveTo = (DateTimeOffset?)null, sourceKind = "UserDeclared", sourceRef = (string?)null }), HttpStatusCode.Conflict)));
        // An ended relation keeps its start; the end must follow it and can be set once.
        var ownerRole = await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/roles",
            new { role = "RegisteredOwner", vesselId = SeedVessel.Id, effectiveFrom = from, effectiveTo = (DateTimeOffset?)null, sourceKind = "Document", sourceRef = "synthetic-registry-page" }));
        Assert.Equal("Unverified", ownerRole.GetProperty("verificationState").GetString());
        Assert.Equal("validation_failed", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/roles/{ownerRole.GetProperty("id").GetGuid()}/end",
            new { expectedVersion = ownerRole.GetProperty("version").GetUInt32(), reason = "End", effectiveTo = from.AddDays(-1) }), HttpStatusCode.BadRequest)));
        var ended = await HrJson(await HrSend(author, $"hr/legal-entities/{draftCompany}/roles/{ownerRole.GetProperty("id").GetGuid()}/end",
            new { expectedVersion = ownerRole.GetProperty("version").GetUInt32(), reason = "Synthetic sale", effectiveTo = from.AddDays(30) }));
        Assert.Equal(from.AddDays(30), ended.GetProperty("effectiveTo").GetDateTimeOffset());
        Assert.Equal("validation_failed", HrCode(await HrJson(await HrSend(author, $"hr/legal-entities/{company}/roles",
            new { role = "Payroll", vesselId = (Guid?)null, effectiveFrom = from, effectiveTo = (DateTimeOffset?)null, sourceKind = "UserDeclared", sourceRef = (string?)null }), HttpStatusCode.BadRequest)));

        // P5201-AC-05: the single person master; verification is independent.
        var person = SeedVessel.Id;
        var personRow = await HrGet(author, $"hr/people/{person}");
        Assert.Equal("Unknown", personRow.GetProperty("personType").GetString());
        personRow = await HrJson(await HrSend(author, $"hr/people/{person}", new { expectedVersion = personRow.GetProperty("version").GetUInt32(),
            personType = "Seafarer", sourceKind = "Document", sourceRef = "synthetic-crew-file" }, put: true));
        Assert.Equal("separation_of_duties", HrCode(await HrJson(await HrSend(author, $"hr/people/{person}/verify",
            new { expectedVersion = personRow.GetProperty("version").GetUInt32(), reason = "Self verification" }), HttpStatusCode.Forbidden)));
        personRow = await HrJson(await HrSend(approver, $"hr/people/{person}/verify",
            new { expectedVersion = personRow.GetProperty("version").GetUInt32(), reason = "Independent synthetic verification" }));
        Assert.Equal("Verified", personRow.GetProperty("masterState").GetString());
        Assert.Equal(2, (await HrGet(author, $"hr/people/{person}/field-revisions")).GetProperty("items").GetArrayLength());

        // P5201-AC-06: employment is a record (no wage/payroll field) activated by an independent approver.
        var start = HrNow().AddDays(-120);
        var employment = await HrJson(await HrSend(author, "hr/employments", new { personId = person, legalEntityId = company,
            employeeNo = "HR-E-" + Guid.NewGuid().ToString("N")[..8], employmentType = "Employee", startAt = start, endAt = (DateTimeOffset?)null }));
        Assert.Equal("Draft", employment.GetProperty("state").GetString());
        foreach (var forbidden in new[] { "wage", "salary", "payroll", "bankAccount", "bonus" })
            Assert.False(employment.TryGetProperty(forbidden, out _));
        var employmentId = employment.GetProperty("id").GetGuid();
        Assert.Equal("separation_of_duties", HrCode(await HrJson(await HrSend(author, $"hr/employments/{employmentId}/status",
            new { expectedVersion = employment.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Self activation" }), HttpStatusCode.Forbidden)));
        employment = await HrJson(await HrSend(approver, $"hr/employments/{employmentId}/status",
            new { expectedVersion = employment.GetProperty("version").GetUInt32(), targetState = "Active", reason = "Independent activation" }));
        Assert.Equal("Active", employment.GetProperty("state").GetString());

        // P5201-AC-07: approved plan occupancy never overlaps; the planner cannot approve.
        var plan = await HrJson(await HrSend(author, "hr/assignments", new { employmentId, kind = "Vessel", vesselId = SeedVessel.Id, positionId = (Guid?)null,
            plannedFrom = HrNow().AddDays(-60), plannedTo = HrNow().AddDays(30), plannedZoneId = "Europe/Istanbul" }));
        var planId = plan.GetProperty("id").GetGuid();
        Assert.Equal("Planned", plan.GetProperty("plan").GetProperty("state").GetString());
        Assert.Equal("NotStarted", plan.GetProperty("actual").GetProperty("status").GetString());
        Assert.Equal("separation_of_duties", HrCode(await HrJson(await HrSend(author, $"hr/assignments/{planId}/status",
            new { expectedVersion = plan.GetProperty("version").GetUInt32(), targetState = "Approved", reason = "Self approval" }), HttpStatusCode.Forbidden)));
        plan = await HrJson(await HrSend(approver, $"hr/assignments/{planId}/status",
            new { expectedVersion = plan.GetProperty("version").GetUInt32(), targetState = "Approved", reason = "Independent approval" }));
        Assert.Equal("Approved", plan.GetProperty("plan").GetProperty("state").GetString());
        // A planned date in the past never becomes an actual sign-on.
        Assert.Equal("NotStarted", plan.GetProperty("actual").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, plan.GetProperty("actual").GetProperty("actualFrom").ValueKind);
        var alternative = await HrJson(await HrSend(author, "hr/assignments", new { employmentId, kind = "Vessel", vesselId = SeedVessel.Id, positionId = (Guid?)null,
            plannedFrom = HrNow().AddDays(-10), plannedTo = HrNow().AddDays(40), plannedZoneId = "UTC" }));
        Assert.Equal("hr_assignment_overlap", HrCode(await HrJson(await HrSend(approver, $"hr/assignments/{alternative.GetProperty("id").GetGuid()}/status",
            new { expectedVersion = alternative.GetProperty("version").GetUInt32(), targetState = "Approved", reason = "Overlap" }), HttpStatusCode.Conflict)));

        // P5201-AC-08: actual facts are separate, deduplicated by source event id and retained when doubtful.
        var checkIn = new { kind = "CheckIn", occurredAt = HrNow().AddDays(-50), originZoneId = "Europe/Istanbul", origin = "SupervisorReported",
            sourceSystem = "synthetic-logbook", sourceEventId = "evt-" + Guid.NewGuid().ToString("N") };
        var withActual = await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events", checkIn));
        var actual = withActual.GetProperty("actual");
        Assert.Equal("InProgress", actual.GetProperty("status").GetString());
        Assert.Equal(checkIn.occurredAt, actual.GetProperty("actualFrom").GetDateTimeOffset());
        Assert.Equal(plan.GetProperty("plan").GetProperty("plannedFrom").GetDateTimeOffset(), withActual.GetProperty("plan").GetProperty("plannedFrom").GetDateTimeOffset());
        await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events", checkIn));
        Assert.Equal(1, (await HrGet(author, $"hr/assignments/{planId}")).GetProperty("actual").GetProperty("events").GetArrayLength());
        Assert.Equal("hr_event_conflict", HrCode(await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events",
            checkIn with { occurredAt = checkIn.occurredAt.AddHours(1) }), HttpStatusCode.Conflict)));
        var second = await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events", checkIn with { occurredAt = checkIn.occurredAt.AddDays(1), sourceEventId = "evt-" + Guid.NewGuid().ToString("N") }));
        Assert.Equal(1, second.GetProperty("actual").GetProperty("pendingReconciliation").GetInt32());
        Assert.Single(second.GetProperty("actual").GetProperty("sessions").EnumerateArray());
        Assert.Equal("validation_failed", HrCode(await HrJson(await HrSend(author, $"hr/assignments/{planId}/actual-events",
            checkIn with { origin = "Self", sourceEventId = "evt-self" }), HttpStatusCode.BadRequest)));
        // A plan with accepted actual history is not cancellable intent.
        Assert.Equal("hr_actual_exists", HrCode(await HrJson(await HrSend(author, $"hr/assignments/{planId}/status",
            new { expectedVersion = second.GetProperty("version").GetUInt32(), targetState = "Cancelled", reason = "Cancel" }), HttpStatusCode.Conflict)));

        // P5201-AC-09: attributed write audit through the audited SaveChanges path, without copied values.
        await using var db = Db();
        var audits = await db.AuditEvents.Where(x => x.ResourceId == company && x.Source == "WebCommand" && (x.EntityType == nameof(Company) || x.EntityType == nameof(LegalEntityFieldRevision))).ToArrayAsync(Ct);
        Assert.NotEmpty(audits);
        Assert.All(audits, a => { Assert.Equal(actorId.ToString(), a.ActorId); Assert.DoesNotContain("Synthetic Denizcilik", a.ChangeSummary); });
        Assert.Contains(audits, a => a.ChangeSummary!.Contains("HrUpdateLegalEntity", StringComparison.Ordinal));
        Assert.All(await db.AuditEvents.Where(x => x.EntityType == nameof(LegalEntityIdentifier)).ToArrayAsync(Ct), a => Assert.DoesNotContain("990011", a.ChangeSummary));
        Assert.All(await db.AuditEvents.Where(x => x.EntityType == nameof(Employment)).ToArrayAsync(Ct), a => Assert.DoesNotContain("HR-E-", a.ChangeSummary));
    }

    [Fact]
    [Trait("Requirement", "PLAN-1511-5201")]
    public async Task Hr_endpoints_deny_without_hr_permission_and_hide_other_tenant_records()
    {
        await using var plainHost = HrHost(hr: false); using var plain = Client(plainHost); await Token(plain);
        var company = SeedVessel.CompanyId;
        await using (var before = Db()) Assert.Equal("Draft", (await before.Companies.AsNoTracking().SingleAsync(x => x.Id == company, Ct)).LegalEntityStatus);
        Assert.Equal("permission_denied", HrCode(await HrGet(plain, $"hr/legal-entities/{company}", HttpStatusCode.Forbidden)));
        Assert.Equal("permission_denied", HrCode(await HrJson(await HrSend(plain, $"hr/legal-entities/{company}", HrEntityUpdate(1), put: true), HttpStatusCode.Forbidden)));
        Assert.Equal("permission_denied", HrCode(await HrJson(await HrSend(plain, "hr/employments", new { personId = SeedVessel.Id, legalEntityId = company,
            employeeNo = "DENIED", employmentType = "Employee", startAt = HrNow(), endAt = (DateTimeOffset?)null }), HttpStatusCode.Forbidden)));

        await using var hrHost = HrHost(); using var hr = Client(hrHost); await Token(hr);
        var foreign = seed.Companies.First(x => x.TenantId == seed.Tenants[1].Id);
        var foreignVessel = seed.Vessels.First(x => x.TenantId == seed.Tenants[1].Id);
        Assert.Equal("resource_not_found", HrCode(await HrGet(hr, $"hr/legal-entities/{foreign.Id}", HttpStatusCode.NotFound)));
        Assert.Equal("resource_not_found", HrCode(await HrJson(await HrSend(hr, $"hr/legal-entities/{foreign.Id}", HrEntityUpdate(1), put: true), HttpStatusCode.NotFound)));
        Assert.Equal("resource_not_found", HrCode(await HrGet(hr, $"hr/people/{foreignVessel.Id}", HttpStatusCode.NotFound)));
        Assert.Equal("resource_not_found", HrCode(await HrJson(await HrSend(hr, $"hr/legal-entities/{company}/roles",
            new { role = "Operator", vesselId = foreignVessel.Id, effectiveFrom = HrNow(), effectiveTo = (DateTimeOffset?)null, sourceKind = "UserDeclared", sourceRef = (string?)null }), HttpStatusCode.NotFound)));
        Assert.Equal("resource_not_found", HrCode(await HrJson(await HrSend(hr, "hr/employments", new { personId = foreignVessel.Id, legalEntityId = company,
            employeeNo = "X-TENANT", employmentType = "Employee", startAt = HrNow(), endAt = (DateTimeOffset?)null }), HttpStatusCode.NotFound)));
        var list = await HrGet(hr, "hr/legal-entities");
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == foreign.Id);
        Assert.Equal("invalid_query", HrCode(await HrGet(hr, "hr/employments", HttpStatusCode.BadRequest)));
        Assert.Equal("invalid_query", HrCode(await HrGet(hr, "hr/employments?personId=not-a-guid", HttpStatusCode.BadRequest)));

        // Closed command contract: unknown members, missing key and missing antiforgery are rejected without business effects.
        Assert.Equal("invalid_request", HrCode(await HrJson(await HrSend(hr, $"hr/legal-entities/{company}/identifiers",
            new { type = "Other", issuer = "X", value = "1", sourceKind = "UserDeclared", salary = 1 }), HttpStatusCode.BadRequest)));
        Assert.Equal("invalid_idempotency_key", HrCode(await HrJson(await Send(hr, $"hr/legal-entities/{company}/identifiers",
            new { type = "Other", issuer = "X", value = "1", sourceKind = "UserDeclared" }, ""), HttpStatusCode.BadRequest)));
        hr.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal("csrf_invalid", HrCode(await HrJson(await HrSend(hr, $"hr/legal-entities/{company}/identifiers",
            new { type = "Other", issuer = "X", value = "1", sourceKind = "UserDeclared" }), HttpStatusCode.Forbidden)));
        await using var db = Db();
        Assert.Equal("Draft", (await db.Companies.AsNoTracking().SingleAsync(x => x.Id == company, Ct)).LegalEntityStatus);
        Assert.False(await db.LegalEntityIdentifiers.AnyAsync(x => x.Issuer == "X", Ct));
        Assert.False(await db.Employments.AnyAsync(x => x.EmployeeNo == "DENIED" || x.EmployeeNo == "X-TENANT", Ct));
        Assert.False(await db.CommandReceipts.AnyAsync(x => x.Action.StartsWith("Hr"), Ct));
    }
}
