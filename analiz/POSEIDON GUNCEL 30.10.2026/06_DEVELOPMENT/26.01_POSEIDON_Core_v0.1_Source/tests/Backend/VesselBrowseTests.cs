using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Xunit;
namespace Poseidon.Tests;
// Serialize fixture DDL; the explicit concurrent-client transaction tests still run in parallel.
[Collection("PostgreSQL schema")]
public class VesselBrowseTests
{
    static Session Scope => new("query-test",Guid.NewGuid(),Guid.NewGuid(),[],false);
    static VesselBrowse Parse(string query,Session scope) => VesselBrowse.Parse(new QueryCollection(QueryHelpers.ParseQuery(query)),scope);
    [Theory][InlineData("?pageSize=0")][InlineData("?pageSize=101")][InlineData("?pageSize=x")][InlineData("?q=a&q=b")][InlineData("?cursor=bad")][InlineData("?cursor=")][InlineData("?tenant_id=x")][InlineData("?flag=%0A")]
    public void Invalid_queries_fail_closed(string query) => Assert.Equal(400,Assert.Throws<ApiFault>(()=>Parse(query,Scope)).Status);
    [Fact] public void Bounds_and_cursor_query_scope_binding()
    {
        var s=Scope;var q=Parse("?q= Atlas &flag=TR&pageSize=20",s);Assert.Equal("atlas",q.Q);Assert.Equal(20,q.PageSize);
        var cursor=q.CursorAfter(Guid.NewGuid());
        Assert.NotNull(Parse("?q=ATLAS&flag=TR&pageSize=20&cursor="+cursor,s).After);
        foreach(var changed in new[]{"?q=other&flag=TR&pageSize=20","?q=atlas&flag=GB&pageSize=20","?q=atlas&flag=TR&pageSize=21"})
            Assert.Throws<ApiFault>(()=>Parse(changed+"&cursor="+cursor,s));
        Assert.Throws<ApiFault>(()=>Parse("?q=atlas&flag=TR&pageSize=20&cursor="+cursor,s with { CompanyId=Guid.NewGuid() }));
        Assert.Throws<ApiFault>(()=>Parse("?q="+new string('a',101),s));
        Assert.Throws<ApiFault>(()=>Parse("?flag="+new string('a',201),s));
        Assert.Throws<ApiFault>(()=>Parse("?cursor="+new string('a',513),s));
        Assert.Equal(50,Parse("",s).PageSize);
        Assert.Equal("istanbul İ ı",VesselBrowse.Fold("ISTANBUL İ ı"));
    }
    [PostgresFact] public async Task More_than_500_scoped_records_keysets_filters_literals_and_mutation_caveats()
    {
        await using var source=NpgsqlDataSource.Create(TestDatabase.Connection);var store=new DraftStore(source);await store.Migrate(default);
        var s=Scope;await using var connection=await source.OpenConnectionAsync();
        await using(var seed=new NpgsqlCommand("""
          INSERT INTO companies(tenant_id,id) VALUES(@tenant,@company);
          INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data)
          SELECT @tenant,md5(@tenant::text||n::text)::uuid,@company,'vessels',@actor,1,
           jsonb_build_object('name','Fleet '||n,'official_no','REG_'||n,'flag','TR','vessel_type',CASE WHEN n%2=0 THEN 'tug' ELSE 'ship' END)
          FROM generate_series(1,620) n;
          INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data)
          SELECT @tenant,gen_random_uuid(),@company,'vessels',@actor,1,jsonb_build_object('name',v)
          FROM unnest(ARRAY['Literal %_\ marker','Literal XX marker','İSTANBUL','istanbul','IĞDIR','ığdır']) v;
          INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data)
          VALUES(@tenant,gen_random_uuid(),@company,'vessels','someone-else',1,'{"name":"Hidden Fleet","flag":"TR","vessel_type":"tug"}');
          """,connection)) {
            seed.Parameters.AddWithValue("tenant",s.TenantId);seed.Parameters.AddWithValue("company",s.CompanyId);seed.Parameters.AddWithValue("actor",s.Subject);await seed.ExecuteNonQueryAsync();
        }
        var otherCompany=s with { CompanyId=Guid.NewGuid() };var otherTenant=s with { TenantId=Guid.NewGuid(),CompanyId=Guid.NewGuid() };
        foreach(var foreign in new[]{otherCompany,otherTenant}) {
            await using var seedForeign=new NpgsqlCommand("INSERT INTO companies VALUES(@t,@c); INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data) VALUES(@t,gen_random_uuid(),@c,'vessels',@s,1,'{\"name\":\"Fleet LEAK foreign\",\"flag\":\"TR\",\"vessel_type\":\"tug\"}')",connection);
            seedForeign.Parameters.AddWithValue("t",foreign.TenantId);seedForeign.Parameters.AddWithValue("c",foreign.CompanyId);seedForeign.Parameters.AddWithValue("s",foreign.Subject);await seedForeign.ExecuteNonQueryAsync();
            Assert.Single((await store.BrowseVessels(foreign,Parse("?q=LEAK&flag=TR",foreign),default)).Items);
        }
        Assert.Empty((await store.BrowseVessels(s,Parse("?q=LEAK&flag=TR",s),default)).Items);
        var ids=new List<Guid>();string? cursor=null;VesselPage first=null!;
        do {var page=await store.BrowseVessels(s,Parse(cursor is null?"":"?cursor="+cursor,s),default);first??=page;Assert.InRange(page.Items.Count,1,50);ids.AddRange(page.Items.Select(x=>x.DraftId));Assert.Equal(page.HasMore,page.NextCursor is not null);cursor=page.NextCursor;}while(cursor is not null);
        Assert.Equal(626,ids.Count);Assert.Equal(626,ids.Distinct().Count());
        var hidden=await store.BrowseVessels(s with { Subject="no-owner" },Parse("",s with { Subject="no-owner" }),default);Assert.Empty(hidden.Items);
        var granted=s with { Subject="no-owner",VesselIds=[ids[^1]] };Assert.Equal(ids[^1],Assert.Single((await store.BrowseVessels(granted,Parse("",granted),default)).Items).DraftId);
        foreach(var other in new[]{s with { TenantId=Guid.NewGuid() },s with { CompanyId=Guid.NewGuid() }})Assert.Empty((await store.BrowseVessels(other,Parse("?q=Fleet&flag=TR",other),default)).Items);
        var filtered=await store.BrowseVessels(s,Parse("?q=REG_620&flag=TR&vessel_type=tug",s),default);Assert.Single(filtered.Items);
        Assert.Empty((await store.BrowseVessels(s,Parse("?q=REG_620&flag=tr",s),default)).Items);
        Assert.Single((await store.BrowseVessels(s,Parse("?q="+Uri.EscapeDataString("%_\\"),s),default)).Items);
        var ascii=await store.BrowseVessels(s,Parse("?q=ISTANBUL",s),default);Assert.Equal("istanbul",Assert.Single(ascii.Items).Data.GetProperty("name").GetString());
        // Keyset is not a snapshot: a new ID before the cursor is seen on restart, not this continuation.
        var early=Guid.Parse("00000000-0000-0000-0000-000000000001");
        await using(var insert=new NpgsqlCommand("INSERT INTO drafts(tenant_id,id,company_id,kind,owner_subject,version,data) VALUES(@t,@id,@c,'vessels',@s,1,'{}')",connection)) {
            insert.Parameters.AddWithValue("t",s.TenantId);insert.Parameters.AddWithValue("id",early);insert.Parameters.AddWithValue("c",s.CompanyId);insert.Parameters.AddWithValue("s",s.Subject);await insert.ExecuteNonQueryAsync();
        }
        Assert.DoesNotContain((await store.BrowseVessels(s,Parse("?cursor="+first.NextCursor,s),default)).Items,x=>x.DraftId==early);
        Assert.Equal(early,(await store.BrowseVessels(s,Parse("",s),default)).Items[0].DraftId);
        // An update changing filter membership after page one is observed on continuation;
        // no point-in-time snapshot is promised across requests.
        var fleetFirst=await store.BrowseVessels(s,Parse("?q=Fleet",s),default);
        Guid target;
        await using(var pick=new NpgsqlCommand("SELECT id FROM drafts WHERE tenant_id=@t AND data->>'name' LIKE 'Fleet %' ORDER BY id DESC LIMIT 1",connection)) { pick.Parameters.AddWithValue("t",s.TenantId);target=(Guid)(await pick.ExecuteScalarAsync())!; }
        var before=(await store.Get(s,"vessels",target,default))!;
        using var replacement=JsonDocument.Parse("{\"name\":\"Renamed out of filter\"}");
        await store.Save(s,"vessels",target,before.Version,replacement.RootElement,Guid.NewGuid().ToString(),"inventory-update-test",default);
        var afterUpdate=new List<Guid>(fleetFirst.Items.Select(x=>x.DraftId));cursor=fleetFirst.NextCursor;
        while(cursor is not null){var page=await store.BrowseVessels(s,Parse("?q=Fleet&cursor="+cursor,s),default);afterUpdate.AddRange(page.Items.Select(x=>x.DraftId));cursor=page.NextCursor;}
        Assert.Equal(619,afterUpdate.Count);Assert.DoesNotContain(target,afterUpdate);Assert.Equal(afterUpdate.Count,afterUpdate.Distinct().Count());
        // Same identity but no current explicit grant cannot use its former cursor.
        Assert.Throws<ApiFault>(()=>Parse("?cursor="+first.NextCursor,s with { Subject="no-owner" }));
    }
    [PostgresFact] public async Task Http_page_contract_and_invalid_query_status_are_explicit()
    {
        await using var app=new AppFactory();using var client=app.CreateClient();
        var login=await client.PostAsJsonAsync("/api/v1/dev/session",new{fixture="tenant-a"});
        var token=(await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        var response=await client.GetAsync("/api/v1/vessels?pageSize=1");Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var page=(await response.Content.ReadFromJsonAsync<JsonElement>());Assert.True(page.TryGetProperty("items",out _));Assert.True(page.TryGetProperty("nextCursor",out _));Assert.True(page.TryGetProperty("hasMore",out _));
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/vessels?pageSize=101")).StatusCode);
    }

}
