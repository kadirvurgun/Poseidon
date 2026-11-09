using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Domain;
using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    private static void RenewalPermissions(IServiceProvider services,bool owner=true) {
        CertificatePermissions(services);var settings=services.GetRequiredService<FederationSettings>();var m=settings.Memberships[0];
        settings.Memberships[0]=m with{ActorKind="Human",Permissions=[..m.Permissions,"certificate.renewal.write",owner?"certificate.renewal.owner":"unused"]};
    }
    private string RenewalPath(Guid? id=null,string suffix="")=>"renewal-workflow"+(id==null?"":"/"+id)+suffix+"?vesselId="+SeedVessel.Id;
    private async Task<CertificateDto> CertificateForRenewal(HttpClient client) {
        using var response=await Send(client,CertificatePath(),new CertificateSaveCommand(Guid.NewGuid(),0,new(new(new()),DocumentName:"FICTIONAL renewal source"),"INITIAL_AUTHORING"));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return (await response.Content.ReadFromJsonAsync<CertificateDto>(Ct))!;
    }
    [Fact]
    public async Task Renewal_draft_link_keeps_original_current_and_preserves_exact_source_and_replacement_revisions() {
        await using var host=Host();using var client=Client(host);RenewalPermissions(host.Services);await Token(client);
        var original=await CertificateForRenewal(client);var replacement=await CertificateForRenewal(client);
        var role=Guid.Parse(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0]);
        var content=new CertificateRenewalContent(OwnerRoleId:role,RequiredSurvey:true,RequiredDocuments:true,RequiredActions:null,
            PlannedLocation:"FICTIONAL pending",NewCertificateId:replacement.Id,NewCertificateRevision:replacement.Revision,NewCertificateVersion:replacement.Version);
        var command=new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,content,"INITIAL_AUTHORING");var key=Guid.NewGuid().ToString("D");
        using var created=await Send(client,RenewalPath(),command,key);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var bytes=await created.Content.ReadAsStringAsync(Ct);
        var renewal=(await created.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!;Assert.False(renewal.CanComplete);Assert.False(renewal.CurrentRevision);Assert.False(renewal.CanEdit);
        Assert.Equal("DRAFT",renewal.State);Assert.Equal("REVIEW_REQUIRED",renewal.Qualification);Assert.Equal(original.Version,renewal.CertificateVersion);
        using var replay=await Send(client,RenewalPath(),command,key);Assert.Equal(bytes,await replay.Content.ReadAsStringAsync(Ct));
        var old=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(original.Id),Ct))!;
        Assert.True(old.CurrentRevision);Assert.Equal(original.Version,old.Version);Assert.Null(old.SupersededBy);Assert.Equal("DRAFT",old.State);
        var edit=command with{ExpectedVersion=renewal.Version,ReasonCode="AUTHORING_CORRECTION",Content=content with{PlannedLocation="FICTIONAL corrected plan"}};
        using var updated=await Send(client,RenewalPath(renewal.Id),edit,update:true);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(client,RenewalPath(renewal.Id),edit,update:true)).StatusCode);
        var history=(await client.GetFromJsonAsync<RenewalPageDto>("/api/v1/"+RenewalPath(renewal.Id,"/history"),Ct))!;
        Assert.Equal(2,history.Items.Length);Assert.Equal(renewal.Version,history.Items.Single(r=>r.Revision==1).Version);
        Assert.Equal(replacement.Revision,history.Items.Single(r=>r.Revision==1).Content.NewCertificateRevision);
        await using var db=Db();Assert.Equal(2,await db.CertificateCommandReceipts.CountAsync(r=>r.RenewalId==renewal.Id,Ct));
        var immutable=await db.CertificateRenewalRevisions.SingleAsync(r=>r.RenewalId==renewal.Id && r.Revision==1,Ct);immutable.ContentJson="{}";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync(Ct));
    }
    [Fact]
    public async Task Renewal_requires_explicit_owner_capability_and_current_original_or_replacement_pins() {
        await using var host=Host();using var client=Client(host);RenewalPermissions(host.Services,false);await Token(client);
        var original=await CertificateForRenewal(client);var replacement=await CertificateForRenewal(client);
        var role=Guid.Parse(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0]);
        var caps=(await client.GetFromJsonAsync<RenewalCapabilitiesDto>("/api/v1/"+RenewalPath(suffix:"/capabilities"),Ct))!;
        Assert.True(caps.CanWrite);Assert.Empty(caps.OwnerOptions);Assert.False(caps.CanComplete);
        var command=new CertificateRenewalCommand(Guid.NewGuid(),0,original.Id,original.Revision,original.Version,new(OwnerRoleId:role),"INITIAL_AUTHORING");
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(client,RenewalPath(),command)).StatusCode);
        RenewalPermissions(host.Services);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(client,RenewalPath(),command with{CertificateVersion=original.Version+1})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(client,RenewalPath(),command with{Content=new(NewCertificateId:replacement.Id,NewCertificateRevision:replacement.Revision,NewCertificateVersion:replacement.Version+1)})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,RenewalPath(),command with{Content=new(NewCertificateId:original.Id,NewCertificateRevision:original.Revision,NewCertificateVersion:original.Version)})).StatusCode);
        var partial=command with{Content=new(RequiredSurvey:null,RequiredDocuments:null,RequiredActions:null)};
        using var saved=await Send(client,RenewalPath(),partial);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var row=(await saved.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!;Assert.Contains("RENEWAL_REQUIREMENTS_UNKNOWN",row.Blockers);Assert.False(row.CanComplete);
    }
    [Fact]
    public async Task Optional_renewal_fixture_is_incomplete_and_reseed_preserves_a_real_operator_revision() {
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:renewal-fixture","System","renewal-fixture"),administrativeImport:true))
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await VesselCertificateFixtures.AddMissingAsync(db,[SeedVessel],Ct);await CertificateRenewalFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
        await using var host=Host();using var client=Client(host);RenewalPermissions(host.Services);await Token(client);
        var id=CertificateRenewalFixtures.Id(SeedVessel.Id);var certificateId=VesselCertificateFixtures.Id(SeedVessel.Id);
        var before=(await client.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(id),Ct))!;Assert.Equal("SyntheticUnverified",before.Provenance);Assert.Null(before.Content.OwnerRoleId);Assert.False(before.CanComplete);
        var certificate=(await client.GetFromJsonAsync<CertificateDto>("/api/v1/"+CertificatePath(certificateId),Ct))!;
        using var updated=await Send(client,RenewalPath(id),new CertificateRenewalCommand(id,before.Version,certificate.Id,certificate.Revision,certificate.Version,
            new(PlannedLocation:"FICTIONAL operator revision retained",DueDate:new(2030,1,1)),"AUTHORING_CORRECTION"),update:true);Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        var saved=(await updated.Content.ReadFromJsonAsync<CertificateRenewalDto>(Ct))!;
        await using(var db=new PoseidonDbContext(SeedImporter.Options(postgres.GetConnectionString()),Guid.Empty,new("system:renewal-fixture","System","renewal-reseed"),administrativeImport:true))
        await using(var tx=await db.Database.BeginTransactionAsync(Ct)){await VesselCertificateFixtures.AddMissingAsync(db,[SeedVessel],Ct);await CertificateRenewalFixtures.AddMissingAsync(db,[SeedVessel],Ct);await tx.CommitAsync(Ct);}
        var after=(await client.GetFromJsonAsync<CertificateRenewalDto>("/api/v1/"+RenewalPath(id),Ct))!;
        Assert.Equal(saved.Version,after.Version);Assert.Equal(saved.Hash,after.Hash);Assert.Equal(2,after.Revision);Assert.Equal("UserDeclaredUnverified",after.Provenance);
        Assert.Equal("FICTIONAL operator revision retained",after.Content.PlannedLocation);Assert.False(after.CanComplete);
    }
    [Fact]
    public async Task Certificate_schema_rejects_foreign_tenant_vessel_and_cross_owner_renewal_lineage() {
        var foreign=seed.Vessels.First(v=>v.TenantId!=SeedVessel.TenantId);
        await using(var db=Db()){db.VesselCertificates.Add(new(){TenantId=SeedVessel.TenantId,VesselId=foreign.Id,Id=Guid.NewGuid()});var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync(Ct));Assert.Equal("23503",PostgresFailure.Provider(error)?.SqlState);}
        await using var host=Host();using var client=Client(host);RenewalPermissions(host.Services);await Token(client);var source=await CertificateForRenewal(client);
        var command=new CertificateRenewalCommand(Guid.NewGuid(),0,source.Id,source.Revision,source.Version,new(),"INITIAL_AUTHORING");
        using var saved=await Send(client,RenewalPath(),command);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        await using var scoped=Db();scoped.CertificateRenewalRevisions.Add(new(){TenantId=SeedVessel.TenantId,VesselId=foreign.Id,RenewalId=command.Id,CertificateId=source.Id,
            CertificateRevision=source.Revision,CertificateVersion=source.Version,Revision=2,RecordVersion=source.Version,ContentJson="{}",Hash=new string('A',64),SourceVersion=VesselCertificatePolicy.SourceVersion,SavedAt=DateTimeOffset.UtcNow});
        var failure=await Assert.ThrowsAsync<DbUpdateException>(()=>scoped.SaveChangesAsync(Ct));Assert.Equal("23503",PostgresFailure.Provider(failure)?.SqlState);
    }
}
