using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV-014/015/016/019: real HTTP aliases/actions and owned PostgreSQL. AUTHORED; execution DEFERRED.
public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData("operational-control-library", "ControlLibrary", "Control")]
    [InlineData("evidence-graph", "EvidenceGraph", "Evidence")]
    [InlineData("evidence", "Evidence", "Evidence")]
    public async Task S1_graph_alias_lifecycle_records_actual_command_identity_and_preserves_immutable_replay_and_authority(
        string route, string operationPrefix, string kind)
    {
        await using var host = Host(); using var client = Client(host);
        GraphPermissionsForTest(host.Services); await Token(client);
        // Genuine ordinary draft/review/independent qualification/evaluation commands, all explicitly fictional.
        var beforeEvaluationFence = await S1VesselFenceSnapshot();
        var (rule, _, evaluation) = await AdmittedByCommands(client, host.Services);
        await using (var evaluationDb = Db())
        {
            var evaluationAudit = await evaluationDb.AuditEvents.AsNoTracking().SingleAsync(x => x.Source == "WebCommand" &&
                x.EntityType == nameof(ApplicabilityEvaluation) && x.ResourceId == evaluation.Id, Ct);
            await S1AssertVesselNoOpFence(beforeEvaluationFence, "ApplicabilityEvaluate", "REGULATORY_WORKFLOW", evaluationAudit.CorrelationId);
        }
        var settings = host.Services.GetRequiredService<FederationSettings>();
        var member = settings.Memberships[0];
        var pin = new RequirementPin(rule.Current.RequirementId!.Value, rule.Current.RequirementRevision!.Value,
            rule.Current.SourceId!.Value, rule.Current.SourceRevision!.Value, rule.Current.Id, rule.Current.Revision);
        var now = DateTimeOffset.UtcNow;
        var content = kind == "Control"
            ? new ControlEvidenceContent(Title: "FICTIONAL S1 alias control", OwnerId: member.ActorId, OwnerRole: ReviewRole,
                Frequency: "FICTIONAL frequency", EvidencePolicy: "FICTIONAL evidence policy",
                VerificationPolicy: "FICTIONAL independent review", Critical: true)
            : new ControlEvidenceContent(Title: "FICTIONAL S1 alias evidence", OwnerId: member.ActorId, OwnerRole: ReviewRole,
                Provenance: "FICTIONAL software declaration, no legal approval", ObservedAt: now.AddMinutes(-1),
                ValidFrom: now.AddMinutes(-1), ValidUntil: now.AddHours(1), FreshUntil: now.AddHours(1));
        var create = GraphCommand(content: content) with { Requirements = [pin] };
        using var createdResponse = await Send(client, GraphRoute(route), create);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        await S1AssertCommittedCommand(createdResponse, operationPrefix + "Create", "PREPARATION_COMMAND", created.Id, SeedVessel.Id, member.ActorId);

        var correctedContent = content with { Title = "FICTIONAL corrected S1 alias declaration" };
        var update = create with { ExpectedVersion = created.Version, Content = correctedContent, ReasonCode = "AUTHORING_CORRECTION" };
        using var updatedResponse = await Send(client, GraphRoute($"{route}/{created.Id}"), update, update: true);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        Assert.Equal(created.Revision + 1, updated.Revision); Assert.NotEqual(created.Version, updated.Version);
        await S1AssertCommittedCommand(updatedResponse, operationPrefix + "Update", "PREPARATION_COMMAND", created.Id, SeedVessel.Id, member.ActorId);

        var submit = update with { ExpectedVersion = updated.Version, ReasonCode = "SUBMIT" };
        var beforeSubmitFence = await S1VesselFenceSnapshot();
        using var submittedResponse = await Send(client, GraphRoute($"{route}/{created.Id}/submit"), submit);
        Assert.Equal(HttpStatusCode.OK, submittedResponse.StatusCode);
        var submitted = (await submittedResponse.Content.ReadFromJsonAsync<ControlEvidenceDto>(Ct))!;
        Assert.Equal("SUBMITTED", submitted.Lifecycle); Assert.Equal("UNVERIFIED", submitted.Verification);
        await S1AssertCommittedCommand(submittedResponse, operationPrefix + "Submit", "PREPARATION_COMMAND", created.Id, SeedVessel.Id, member.ActorId);
        await S1AssertVesselNoOpFence(beforeSubmitFence, operationPrefix + "Submit", "PREPARATION_COMMAND", Assert.Single(submittedResponse.Headers.GetValues("X-Correlation-ID")));

        var complete = submit with { ExpectedVersion = submitted.Version, ReasonCode = "COMPLETE" };
        var key = Guid.NewGuid().ToString();
        var beforeCompleteFence = await S1VesselFenceSnapshot();
        using var completedResponse = await Send(client, GraphRoute($"{route}/{created.Id}/complete"), complete, key);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        var exactOutcome = await completedResponse.Content.ReadAsStringAsync(Ct);
        var completed = JsonSerializer.Deserialize<ControlEvidenceDto>(exactOutcome, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("COMPLETED", completed.Lifecycle); Assert.Equal("COMPLETED", completed.Completion);
        Assert.Equal("UNVERIFIED", completed.Verification); Assert.Equal("UNKNOWN", completed.Effectiveness);
        await S1AssertCommittedCommand(completedResponse, operationPrefix + "Complete", "PREPARATION_COMMAND", created.Id, SeedVessel.Id, member.ActorId);
        await S1AssertVesselNoOpFence(beforeCompleteFence, operationPrefix + "Complete", "PREPARATION_COMMAND", Assert.Single(completedResponse.Headers.GetValues("X-Correlation-ID")));

        await using var before = Db();
        var auditIds = await before.AuditEvents.Where(x => x.ResourceId == created.Id).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct);
        using var replay = await Send(client, GraphRoute($"{route}/{created.Id}/complete"), complete, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(exactOutcome, await replay.Content.ReadAsStringAsync(Ct));
        using var stale = await Send(client, GraphRoute($"{route}/{created.Id}"), update, update: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        settings.Memberships[0] = member with { Permissions = member.Permissions.Where(p => p != (kind == "Control" ? "control.write" : "evidence.write")).ToArray() };
        using var revoked = await Send(client, GraphRoute($"{route}/{created.Id}/complete"), complete, key);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);

        await using var verify = Db();
        var history = await verify.ControlEvidenceRevisions.Where(x => x.Id == created.Id && x.Kind == kind).OrderBy(x => x.Revision).ToArrayAsync(Ct);
        Assert.Equal(4, history.Length);
        Assert.Equal(content, JsonSerializer.Deserialize<ControlEvidenceContent>(history[0].ContentJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(new[] { "DRAFT", "DRAFT", "SUBMITTED", "COMPLETED" }, history.Select(x => x.Lifecycle).ToArray());
        Assert.Equal(auditIds, await verify.AuditEvents.Where(x => x.ResourceId == created.Id).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(Ct));
        Assert.Equal(4, await verify.ControlEvidenceReceipts.CountAsync(x => x.ResourceId == created.Id, Ct));
        Assert.False(await verify.HumanReviewEvents.AnyAsync(Ct));
    }

    [Theory]
    [InlineData("equipment-hierarchy", "assets", "EquipmentCanonicalSave")]
    [InlineData("assets", "equipment-hierarchy", "EquipmentAssetsSave")]
    public async Task S1_technical_root_aliases_each_commit_a_real_change_then_share_replay_without_extra_history(
        string route, string otherAlias, string operation)
    {
        await using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var path = $"{route}?vessel={SeedVessel.Id}";
        var command = NewEquipment(parent: SeedVessel.Id);
        var key = Guid.NewGuid().ToString();
        using var createdResponse = await Send(client, path, command, key);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.False(created.Replayed); Assert.True(created.Version > 0); Assert.Equal(1, created.Revision);
        await S1AssertCommittedCommand(createdResponse, operation, "PREPARATION_COMMAND", command.Id, SeedVessel.Id, actorId);
        await using var before = Db();
        var firstAuditCount = await before.AuditEvents.CountAsync(x => x.ResourceId == command.Id, Ct);
        using var replay = await Send(client, $"{otherAlias}?vessel={SeedVessel.Id}", command, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = (await replay.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.True(replayed.Replayed); Assert.Equal(created.Version, replayed.Version);
        Assert.Equal(firstAuditCount, await before.AuditEvents.CountAsync(x => x.ResourceId == command.Id, Ct));

        var update = command with { BaseVersion = created.Version, Name = "FICTIONAL alias equipment correction" };
        var updateKey = Guid.NewGuid().ToString();
        using var changed = await Send(client, path, update, updateKey);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var current = (await changed.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.Equal(2, current.Revision); Assert.NotEqual(created.Version, current.Version);
        await S1AssertCommittedCommand(changed, operation, "PREPARATION_COMMAND", command.Id, SeedVessel.Id, actorId);
        var finalAuditCount = await before.AuditEvents.CountAsync(x => x.ResourceId == command.Id, Ct);
        using var stale = await Send(client, path, update);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "equipment.write").ToArray() };
        using var forbidden = await Send(client, path, update, updateKey);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await using var verify = Db();
        Assert.Equal(finalAuditCount, await verify.AuditEvents.CountAsync(x => x.ResourceId == command.Id, Ct));
        Assert.Equal(2, await verify.AssetRevisions.CountAsync(x => x.AssetId == command.Id, Ct));
        Assert.Equal(2, await verify.CommandReceipts.CountAsync(x => x.ResourceId == command.Id, Ct));
        Assert.Equal(SeedVessel.Id, (await verify.Assets.SingleAsync(x => x.Id == command.Id, Ct)).ParentId);
    }

    [Fact]
    public async Task S1_canonical_maintenance_and_job_reference_commands_are_observed_without_fabricating_operational_readiness()
    {
        await using var host = Host(technicalRead: true, technicalWrite: true); using var client = Client(host); await Token(client);
        var job = new MaintenanceJobCommand(Guid.NewGuid(), "S1-FICTIONAL-JOB", "FICTIONAL draft job reference");
        using var jobResponse = await Send(client, PlansUrl + "/jobs", job);
        Assert.Equal(HttpStatusCode.OK, jobResponse.StatusCode);
        await S1AssertCommittedCommand(jobResponse, "MaintenanceJobSave", "PREPARATION_COMMAND", job.Id, SeedVessel.Id, actorId);
        await using var before = Db();
        var jobAudits = await before.AuditEvents.CountAsync(x => x.ResourceId == job.Id, Ct);
        using var duplicateJob = await Send(client, PlansUrl + "/jobs", job);
        Assert.Equal(HttpStatusCode.OK, duplicateJob.StatusCode);
        using var changedJob = await Send(client, PlansUrl + "/jobs", job with { Title = "FICTIONAL conflicting title" });
        Assert.Equal(HttpStatusCode.Conflict, changedJob.StatusCode);
        Assert.Equal(jobAudits, await before.AuditEvents.CountAsync(x => x.ResourceId == job.Id, Ct));

        var path = $"maintenance-plan?vessel={SeedVessel.Id}";
        var command = new MaintenanceCommand(Guid.NewGuid(), null, SeedVessel.Id, job.Id, null, new(), DateTimeOffset.UnixEpoch);
        var key = Guid.NewGuid().ToString();
        using var savedResponse = await Send(client, path, command, key);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!;
        Assert.False(saved.Replayed);
        await S1AssertCommittedCommand(savedResponse, "MaintenanceCanonicalSave", "PREPARATION_COMMAND", command.Id, SeedVessel.Id, actorId);
        using var replay = await Send(client, $"maintenance-plans?vessel={SeedVessel.Id}", command, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True((await replay.Content.ReadFromJsonAsync<TechnicalSaved>(Ct))!.Replayed);
        var page = (await client.GetFromJsonAsync<MaintenancePage>("/api/v1/" + path, Ct))!;
        var row = Assert.Single(page.Items, x => x.Id == command.Id);
        Assert.Equal("ReviewRequired", row.Due.State); Assert.Equal("Draft", row.Status); Assert.Equal(job.Id, row.JobId);
        Assert.Null(row.OwnerRoleId);
        await using var verify = Db();
        Assert.Single(await verify.MaintenanceJobs.Where(x => x.Id == job.Id).ToArrayAsync(Ct));
        Assert.Single(await verify.MaintenancePlanRevisions.Where(x => x.PlanId == command.Id).ToArrayAsync(Ct));
        Assert.Single(await verify.CommandReceipts.Where(x => x.ResourceId == command.Id).ToArrayAsync(Ct));
    }

    [Theory]
    [InlineData("lost", "KeyLost", "TugOfflineActionLost", true)]
    [InlineData("cleanup", "CleanupAllowed", "TugOfflineActionCleanup", false)]
    public async Task S1_tug_lifecycle_terminal_actions_have_real_device_context_and_do_not_change_the_draft_plan(
        string action, string lifecycle, string operation, bool revoked)
    {
        await using var host = PlanningHost(OfflinePlanningPolicy()); using var client = Client(host); await Token(client);
        var job = await PlanningJob(client); var plan = await ChangePlan(client, job, null, new("Window"));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var enrollment = await EnrollTug(client, job.Id, key);
        var capture = TugCaptured(enrollment);
        using var changed = await Send(client, PlanningUrl(job.Id, $"/offline/{enrollment.Id}/{action}"), new { });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var state = await changed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("RetainedLocked", state.GetProperty("state").GetString());
        await S1AssertCommittedCommand(changed, operation, "OFFLINE_LIFECYCLE", job.Id, Tug.Id, actorId);
        await using var before = Db();
        var retained = await before.TugOfflineEnrollments.AsNoTracking().SingleAsync(x => x.Id == enrollment.Id, Ct);
        Assert.Equal(revoked, retained.RevokedAt != null);
        Assert.Equal(enrollment.BaseVersion, retained.BaseVersion); Assert.Equal(enrollment.BaseRevision, retained.BaseRevision);
        Assert.Equal(enrollment.RequestVersion, retained.RequestVersion);
        Assert.Single(await before.TugOfflineLifecycles.Where(x => x.EnrollmentId == enrollment.Id && x.Action == lifecycle).ToArrayAsync(Ct));
        if (revoked)
        {
            using var deniedReplay = await DeliverTug(client, enrollment, capture, key);
            Assert.Equal(HttpStatusCode.Forbidden, deniedReplay.StatusCode);
        }
        var auditCount = await before.AuditEvents.CountAsync(x => x.ResourceId == job.Id, Ct);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = settings.Memberships[0].Permissions.Where(p => p != "tug.plan.write").ToArray() };
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "untrusted-tug-correlation");
        using var deniedAction = await Send(client, PlanningUrl(job.Id, $"/offline/{enrollment.Id}/{action}"), new { });
        Assert.Equal(HttpStatusCode.NotFound, deniedAction.StatusCode);
        var denialBody = await deniedAction.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("resource_not_found", denialBody.GetProperty("code").GetString());
        var correlation = Assert.Single(deniedAction.Headers.GetValues("X-Correlation-ID"));
        Assert.True(Guid.TryParseExact(correlation, "N", out _));
        Assert.NotEqual("untrusted-tug-correlation", correlation);
        Assert.Equal(correlation, denialBody.GetProperty("correlationId").GetString());
        await using var verify = Db();
        var denial = Assert.Single(await verify.Set<SecurityEvent>().Where(x => x.CorrelationId == correlation).ToArrayAsync(Ct));
        Assert.Equal("TugSave", denial.Action); Assert.Equal("resource_not_found", denial.Result);
        Assert.Equal(actorId.ToString(), denial.ActorId); Assert.Equal(retained.TenantId, denial.TenantId);
        Assert.Null(denial.ResourceId); Assert.NotEqual(default, denial.OccurredAt);
        Assert.Equal(auditCount, await verify.AuditEvents.CountAsync(x => x.ResourceId == job.Id, Ct));
        Assert.Single(await verify.TugOfflineLifecycles.Where(x => x.EnrollmentId == enrollment.Id && x.Action == lifecycle).ToArrayAsync(Ct));
        Assert.False(await verify.TugOfflineOutcomes.AnyAsync(x => x.EnrollmentId == enrollment.Id, Ct));
        var currentPlan = await verify.TugDraftPlans.SingleAsync(x => x.JobId == job.Id, Ct);
        Assert.Equal(plan.Version, currentPlan.Version); Assert.Equal(plan.Revision, currentPlan.Revision);
        Assert.Equal(job.Version, (await verify.TugJobs.SingleAsync(x => x.Id == job.Id, Ct)).Version);
        var summaries = await verify.AuditEvents.Where(x => x.ResourceId == job.Id).Select(x => x.ChangeSummary ?? "").ToArrayAsync(Ct);
        Assert.All(summaries, summary =>
        {
            Assert.DoesNotContain(enrollment.Challenge, summary);
            Assert.DoesNotContain(retained.ProtectedBase, summary);
            Assert.DoesNotContain(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), summary);
        });
    }

    private async Task S1AssertCommittedCommand(HttpResponseMessage response, string operation, string reason, Guid resource, Guid vessel, Guid actor)
    {
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.True(Guid.TryParseExact(correlation, "N", out _));
        await using var db = Db();
        var rows = await db.AuditEvents.Where(x => x.CorrelationId == correlation && x.ResourceId == resource).ToArrayAsync(Ct);
        Assert.NotEmpty(rows);
        Assert.All(rows, row =>
        {
            Assert.Equal("WebCommand", row.Source); Assert.Equal(actor.ToString(), row.ActorId);
            Assert.Equal(seed.Tenants[0].Id, row.TenantId); Assert.Equal(vessel, row.VesselId);
            Assert.Equal("RecordedControlledCode", row.ReasonStatus);
            Assert.Equal(ReviewRole.ToString("D"), row.RoleContext); Assert.NotEqual(default, row.OccurredAt);
            using var summary = JsonDocument.Parse(row.ChangeSummary!);
            string? Field(string name) => Assert.Single(summary.RootElement.EnumerateArray(), x => x.GetProperty("field").GetString() == name).GetProperty("after").GetString();
            Assert.Equal(operation, Field("CommandName")); Assert.Equal(reason, Field("TechnicalReasonCode"));
            Assert.Equal("Vessel", Field("ScopeKind"));
            Assert.DoesNotContain("FICTIONAL", row.ChangeSummary);
        });
    }

    private async Task<Vessel> S1VesselFenceSnapshot()
    {
        await using var db = Db();
        return await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == SeedVessel.Id, Ct);
    }

    private async Task S1AssertVesselNoOpFence(Vessel before, string operation, string reason, string correlation)
    {
        await using var db = Db();
        var current = await db.Vessels.AsNoTracking().SingleAsync(x => x.Id == before.Id, Ct);
        Assert.Equal((before.Id, before.TenantId, before.CompanyId, before.Kind, before.Code, before.Name),
            (current.Id, current.TenantId, current.CompanyId, current.Kind, current.Code, current.Name));
        Assert.NotEqual(before.Version, current.Version); // Actual PostgreSQL xmin fence, not a semantic fact edit.
        var rows = await db.AuditEvents.AsNoTracking().Where(x => x.CorrelationId == correlation).ToArrayAsync(Ct);
        var audit = Assert.Single(rows, x => x.EntityType == nameof(Vessel) && x.ResourceId == before.Id);
        Assert.Equal(before.Version, audit.BeforeVersion); Assert.Null(audit.AfterVersion);
        Assert.Null(audit.BeforeRevision); Assert.Null(audit.AfterRevision);
        Assert.Equal(before.Id, audit.VesselId); Assert.Equal(before.TenantId, audit.TenantId);
        Assert.Equal("Vessel", audit.ResourceKind); Assert.Equal("Modified", audit.Action);
        Assert.DoesNotContain(rows, x => x.ResourceId == before.Id && x.AfterVersion > 0);
        using var summary = JsonDocument.Parse(audit.ChangeSummary!);
        var fields = summary.RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("field").GetString()!);
        Assert.Equal(new[] { "Binding", "CommandName", "Name", "ScopeKind", "TechnicalReasonCode" }, fields.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("OwnerReference", fields["Binding"].GetProperty("before").GetString());
        Assert.Equal("OwnerReference", fields["Binding"].GetProperty("after").GetString());
        Assert.Equal("ValuesWithheld", fields["Name"].GetProperty("evidence").GetString());
        Assert.Equal(JsonValueKind.Null, fields["Name"].GetProperty("before").ValueKind);
        Assert.Equal(JsonValueKind.Null, fields["Name"].GetProperty("after").ValueKind);
        Assert.Equal(operation, fields["CommandName"].GetProperty("after").GetString());
        Assert.Equal(reason, fields["TechnicalReasonCode"].GetProperty("after").GetString());
        Assert.Equal("Vessel", fields["ScopeKind"].GetProperty("after").GetString());
        Assert.DoesNotContain(before.Name, audit.ChangeSummary!);
    }
}
