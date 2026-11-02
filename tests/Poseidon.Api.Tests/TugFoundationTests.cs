using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Xunit;
namespace Poseidon.Api.Tests;

// Real isolated PostgreSQL/API + synthetic authentication only. Signed-provider proof belongs
// to the separately provisioned real-host browser gate, never these injected identities.
public sealed partial class DirectoryWorkspaceTests
{
    private Vessel Tug => seed.Vessels.First(v => v.TenantId == SeedVessel.TenantId && v.Kind == VesselKind.Tug);
    private string TugUrl => $"tug-jobs?vessel={Tug.Id}";
    private TugSaveCommand Draft() => new(Guid.NewGuid(), null, new(AssistedVessel: "FICTIONAL assisted vessel", RequiredBollardPull: "45.01"));
    [Fact]
    public async Task Tug_receipt_returns_its_exact_revision_after_update_restart_and_revocation_reauthorizes() {
        var cmd = Draft(); var key = Guid.NewGuid().ToString(); TugRequestDto initial;
        using (var host = Host(tugRead:true, tugWrite:true)) {
            using var c = Client(host); await Token(c);
            var r = await Send(c, TugUrl, cmd, key); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            initial = (await r.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
            Assert.StartsWith("TUG-", initial.JobNumber); Assert.True(initial.Version > 0); Assert.Null(initial.Inputs.RequestedEnd);
            var update = cmd with { BaseVersion = initial.Version, Inputs = cmd.Inputs with { RequiredBollardPull = "46.02" } };
            Assert.Equal(HttpStatusCode.OK, (await Send(c, $"tug-jobs/{cmd.ClientId}?vessel={Tug.Id}", update, update:true)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Send(c, $"tug-jobs/{cmd.ClientId}?vessel={Tug.Id}", update, update:true)).StatusCode);
        }
        using (var host = Host(tugRead:true, tugWrite:true)) {
            using var c = Client(host); await Token(c);
            var replay = await (await Send(c, TugUrl, cmd, key)).Content.ReadFromJsonAsync<TugRequestDto>(Ct);
            Assert.True(replay!.Replayed); Assert.Equal(initial.Version, replay.Version); Assert.Equal(initial.Revision, replay.Revision);
            Assert.Equal("45.01", replay.Inputs.RequiredBollardPull);
            Assert.Equal(HttpStatusCode.Conflict, (await Send(c, TugUrl, cmd with { Inputs = new() }, key)).StatusCode);
        }
        using var revoked = Host(tugRead:true); using var retry = Client(revoked); await Token(retry);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(retry, TugUrl, cmd, key)).StatusCode);
        await using var db = Db(); Assert.Equal(2, await db.TugJobRevisions.CountAsync(r => r.JobId == cmd.ClientId, Ct));
        Assert.Single(await db.CommandReceipts.Where(r => r.Key == key).ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Trusted_modes_filter_before_search_paging_direct_reads_and_tug_context_denies_ship_or_other_tenant() {
        using var ship = Host(crossMode:false, tugRead:true); using var s = Client(ship);
        s.DefaultRequestHeaders.Add("X-Role", "Tug Master");
        Assert.Equal(HttpStatusCode.Forbidden, (await s.GetAsync("/api/v1/tug-command", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/v1/vessels/{Tug.Id}", Ct)).StatusCode);
        var shipPage = await s.GetFromJsonAsync<PageDto<VesselDto>>("/api/v1/vessels?limit=1&kind=Tug", Ct); Assert.Empty(shipPage!.Items); Assert.False(shipPage.HasMore);
        using var tug = Host(tugMode:true, crossMode:false, tugRead:true, search:true); using var c = Client(tug);
        var ships = await c.GetFromJsonAsync<PageDto<SearchItemDto>>("/api/v1/workspace/search?q=" + SeedVessel.Code, Ct);
        Assert.Empty(ships!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/regulatory-profile", Ct)).StatusCode);
        var other = seed.Vessels.First(v => v.TenantId != Tug.TenantId);
        foreach (var id in new[] { SeedVessel.Id, other.Id }) Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/v1/tug-command?vessel={id}", Ct)).StatusCode);
        using var scoped = Host(manage:false, read:false, tugMode:true, crossMode:false, tugRead:true); using var onlyShipGrant = Client(scoped);
        Assert.Equal(HttpStatusCode.NotFound, (await onlyShipGrant.GetAsync($"/api/v1/tug-job-board?vessel={Tug.Id}", Ct)).StatusCode);
    }
    [Fact]
    public async Task Board_uses_committed_inputs_without_inventing_assignments_readiness_or_plan_and_compares_entered_boundaries() {
        using var host = Host(tugRead:true, tugWrite:true); using var c = Client(host); await Token(c);
        var time = new DateTimeOffset(2026,11,1,12,0,0,TimeSpan.Zero);
        var first = Draft() with { Inputs = new(RequestedStart:time, RequestedEnd:time.AddHours(1), Priority:"High", RequiredBollardPull:"45.01") };
        var second = Draft() with { Inputs = new(RequestedStart:time.AddHours(1), RequestedEnd:time.AddHours(2), Priority:"High") };
        Assert.Equal(HttpStatusCode.OK, (await Send(c,TugUrl,first)).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Send(c,TugUrl,second)).StatusCode);
        var b = await c.GetFromJsonAsync<PageDto<TugBoardRow>>($"/api/v1/tug-job-board?vessel={Tug.Id}&priority=High", Ct);
        var row = b!.Items.Single(r => r.Request.Id == first.ClientId);
        Assert.Equal("45.01", row.Request.Inputs.RequiredBollardPull); Assert.Empty(row.AssignedTugs); Assert.Null(row.AvailableBollardPull);
        Assert.Equal("Unknown",row.Readiness); Assert.Equal("Unknown",row.Conflict); Assert.Contains("NoApprovedPlan",row.PlannedTimeSource);
        Assert.Contains(row.DeclaredComparisons,x=>x.JobId == second.ClientId && x.Relation == "BoundaryTouch");
        Assert.Contains("AssignmentCoverageUnavailable",row.BlockingReasons);
    }
    [Fact]
    public async Task Invalid_keys_duplicate_members_owner_time_precision_and_query_multiplicity_leave_no_receipts() {
        using var host = Host(tugRead:true,tugWrite:true); using var c = Client(host); await Token(c);
        foreach (var x in new TugRequestInputs[] { new(RequiredBollardPull:"0.001"), new(JobType:"Ready"), new(RequiredTugCount:0), new(AssistedVesselImo:"9074720"), new(RequestedStart:DateTimeOffset.UnixEpoch,RequestedEnd:DateTimeOffset.UnixEpoch.AddTicks(-1)) })
            Assert.Equal(HttpStatusCode.BadRequest,(await Send(c,TugUrl,Draft() with {Inputs=x})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(c,TugUrl,Draft() with {Inputs=new(RequestOwner:Guid.NewGuid())})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(c,TugUrl + $"&vessel={Tug.Id}",Draft())).StatusCode);
        var id = Guid.NewGuid();
        foreach (var body in new[] { $"{{\"clientId\":\"{id}\",\"baseVersion\":null,\"inputs\":{{\"priority\":\"High\",\"priority\":\"Normal\"}}}}", $"{{\"clientId\":\"{id}\",\"inputs\":{{\"requestedStart\":\"2026-11-01T12:00:00\"}}}}", $"{{\"clientId\":\"{id}\",\"actorId\":\"spoofed\",\"inputs\":{{}}}}" }) {
            using var msg = new HttpRequestMessage(HttpMethod.Post,"/api/v1/"+TugUrl) {Content=new StringContent(body,Encoding.UTF8,"application/json")}; msg.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
            var r = await c.SendAsync(msg,Ct); Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode); Assert.DoesNotContain("spoofed",await r.Content.ReadAsStringAsync(Ct));
        }
        await using var db=Db(); Assert.Empty(await db.CommandReceipts.ToArrayAsync(Ct));
    }
    [Fact]
    public async Task Required_owner_is_provisioned_and_revocation_denies_exact_retry_without_personal_data() {
        var cmd = Draft() with { Inputs = new(RequestOwner:Guid.Parse("91c26eb5-cf16-4553-981d-1c275358c535")) }; var key=Guid.NewGuid().ToString();
        using(var host=Host(tugRead:true,tugWrite:true,tugOwner:true)) { using var c=Client(host); await Token(c); Assert.Equal(HttpStatusCode.OK,(await Send(c,TugUrl,cmd,key)).StatusCode); }
        using var revoked=Host(tugRead:true,tugWrite:true); using var retry=Client(revoked); await Token(retry);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(retry,TugUrl,cmd,key)).StatusCode);
    }
    [Fact]
    public async Task Concurrent_duplicates_commit_once_and_receipt_failure_rolls_back_job_revision_and_audit() {
        using var host=Host(tugRead:true,tugWrite:true); using var a=Client(host); using var b=Client(host); await Token(a);await Token(b);
        var cmd=Draft();var key=Guid.NewGuid().ToString(); var replies=await Task.WhenAll(Send(a,TugUrl,cmd,key),Send(b,TugUrl,cmd,key));
        Assert.Contains(replies,r=>r.StatusCode==HttpStatusCode.OK); Assert.All(replies,r=>Assert.Contains(r.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Conflict}));
        Assert.Equal(HttpStatusCode.OK,(await Send(a,TugUrl,cmd,key)).StatusCode);
        await using(var db=Db()) {Assert.Single(await db.TugJobRevisions.Where(r=>r.JobId==cmd.ClientId).ToArrayAsync(Ct));}
        await Sql("CREATE TRIGGER reject_tug_receipt BEFORE INSERT ON core.\"CommandReceipts\" FOR EACH STATEMENT EXECUTE FUNCTION audit.reject_mutation();");
        var fail=Draft(); var failedKey=Guid.NewGuid().ToString(); var response=await Send(a,TugUrl,fail,failedKey); Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        await using(var db=Db()) { Assert.False(await db.TugJobs.AnyAsync(j=>j.Id==fail.ClientId,Ct)); Assert.False(await db.TugJobRevisions.AnyAsync(r=>r.JobId==fail.ClientId,Ct)); Assert.False(await db.CommandReceipts.AnyAsync(r=>r.Key==failedKey,Ct)); Assert.DoesNotContain(await db.AuditEvents.ToArrayAsync(Ct),e=>e.ResourceId==fail.ClientId); }
        await Sql("DROP TRIGGER reject_tug_receipt ON core.\"CommandReceipts\";"); Assert.Equal(HttpStatusCode.OK,(await Send(a,TugUrl,fail,failedKey)).StatusCode);
    }
    [Fact]
    public async Task Tug_audit_has_scope_revision_role_source_and_withholds_free_text_and_receipt_secrets() {
        using var host=Host(tugRead:true,tugWrite:true); using var c=Client(host); await Token(c); var cmd=Draft(); var key=Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK,(await Send(c,TugUrl,cmd,key)).StatusCode);
        await using var db=Db();var rows=await db.AuditEvents.Where(e=>e.ResourceId==cmd.ClientId).ToArrayAsync(Ct);
        var write=Assert.Single(rows,e=>e.EntityType==nameof(TugJob));Assert.Equal(Tug.Id,write.VesselId); Assert.NotNull(write.RoleContext); Assert.Equal("WebCommand",write.Source); Assert.Equal(1,write.AfterRevision);
        Assert.All(rows,e=> {Assert.DoesNotContain(cmd.Inputs.AssistedVessel!,e.ChangeSummary??"");Assert.DoesNotContain(key,e.ChangeSummary??"");});
        await using var security = new SecurityEventWriter(new DbContextOptionsBuilder<SecurityEventWriter>().UseNpgsql(postgres.GetConnectionString()).Options);
        Assert.Contains(await security.Set<SecurityEvent>().ToArrayAsync(Ct),e=>e.Action=="TugSave"&&e.Result=="Attempted");
    }
}
