using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Api;
using Poseidon.Persistence;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    // DEV-016 / Backlog row17: no new human write with unknown role attribution.
    [Theory]
    [InlineData(null)]
    [InlineData("malformed-role")]
    [InlineData("ea624c89-1ccd-4e25-b439-42876e16f432")]
    public async Task Every_current_human_write_requires_trusted_role_and_records_complete_available_context(string? role)
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-role-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await using var host = FileHost(root, 1);
        using var client = Client(host);
        var settings = host.Services.GetRequiredService<FederationSettings>();
        settings.Memberships[0] = settings.Memberships[0] with { Permissions = ["directory.read", "directory.write", "regulatory.draft.read", "regulatory.draft.write", "file.read", "file.write", "audit.read"] };
        await Token(client);
        var source = await CreateRegister(client);
        var requirement = await CreateRegister(client, "requirement-drafts");
        var profile = await Profile(client);
        var vessel = await DirectoryRow(client, $"vessels/{SeedVessel.Id}", false);
        var companyId = SeedVessel.CompanyId;
        var company = await DirectoryRow(client, $"companies/{companyId}", true);
        await using var db = Db();
        var before = await db.AuditEvents.CountAsync(Ct);
        settings.Memberships[0] = settings.Memberships[0] with { RoleReferences = role == null ? null : [role] };
        var expected = Guid.TryParse(role, out _) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
        var commands = new (string Path, object Body, bool Update)[] {
            ("companies", new CompanyCreateCommand("ROLECO", "Synthetic company"), false),
            ($"companies/{companyId}", new CompanyUpdateCommand(company.Code, "Synthetic company edit", company.Version), true),
            ("vessels", new VesselCreateCommand("ROLESHIP", "Synthetic vessel", "Ship", companyId), false),
            ($"vessels/{SeedVessel.Id}", new VesselUpdateCommand(vessel.Code, "Synthetic vessel edit", vessel.Version), true),
            (ProfilePath(SeedVessel.Id), new SaveRegulatoryDraft(profile.Version, profile.Content), true),
            ("source-drafts", new RegisterCommand(0, new()), false),
            ("requirement-drafts", new RegisterCommand(0, new()), false),
            ($"source-drafts/{source.Current.Id}", new RegisterCommand(source.Version, source.Current.Content), true),
            ($"requirement-drafts/{requirement.Current.Id}", new RegisterCommand(requirement.Version, requirement.Current.Content), true),
            ($"vessels/{SeedVessel.Id}/files", new StageFileCommand("synthetic.png", "image/png", Png.Length, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png))), false),
        };
        var correlations = new List<string>();
        foreach (var command in commands)
        {
            using var response = await Send(client, command.Path, command.Body, update: command.Update);
            Assert.Equal(expected, response.StatusCode);
            correlations.Add(Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        }
        var writes = await db.AuditEvents.Where(x => correlations.Contains(x.CorrelationId)).ToArrayAsync(Ct);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Empty(writes); Assert.Equal(before, await db.AuditEvents.CountAsync(Ct));
            Assert.True((await Profile(client)).Vessel.CanEdit == false); // Reads stay available.
        }
        else
        {
            foreach (var correlation in correlations) Assert.Contains(writes, x => x.CorrelationId == correlation);
            Assert.All(writes, x => {
                Assert.Equal(actorId.ToString(), x.ActorId); Assert.Equal("User", x.ActorType);
                Assert.Equal(seed.Tenants[0].Id, x.TenantId); Assert.Equal(role, x.RoleContext);
                Assert.NotNull(x.ResourceId); Assert.NotEmpty(x.ChangeSummary!);
                Assert.Equal("RecordedControlledCode", x.ReasonStatus); Assert.NotEqual(default, x.OccurredAt);
                using var summary = System.Text.Json.JsonDocument.Parse(x.ChangeSummary!);
                Assert.Contains(summary.RootElement.EnumerateArray(), field => field.GetProperty("field").GetString() == "CommandName" && !string.IsNullOrEmpty(field.GetProperty("after").GetString()));
                Assert.Contains(summary.RootElement.EnumerateArray(), field => field.GetProperty("field").GetString() == "TechnicalReasonCode" && !string.IsNullOrEmpty(field.GetProperty("after").GetString()));
                if (x.EntityType is nameof(Vessel) or nameof(RegulatoryDraft) or nameof(FileStage)) Assert.NotNull(x.VesselId);
            });
        }
        Directory.Delete(root, false);
    }

    [Fact]
    public async Task Real_scanner_upload_finalization_and_signed_download_use_actual_bytes_and_trusted_role()
    {
        Assert.True(int.TryParse(Environment.GetEnvironmentVariable("POSEIDON_GATE_CLAMD_PORT"), out var port) && port > 0);
        var root = Path.Combine(Path.GetTempPath(), "poseidon-real-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var host = FileHost(root, port); using var client = Client(host);
            FilePermissions(host.Services); await Token(client);
            var stage = await StagePng(client);
            using var accepted = await PutPng(client, stage.Id); Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            using var access = await client.PostAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/access", null, Ct);
            Assert.Equal(HttpStatusCode.OK, access.StatusCode);
            var token = (await access.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("token").GetString();
            client.DefaultRequestHeaders.Add("X-File-Capability", token);
            using var downloaded = await client.GetAsync($"/api/v1/vessels/{SeedVessel.Id}/files/{stage.Id}/bytes", Ct);
            Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode); Assert.Equal(Png, await downloaded.Content.ReadAsByteArrayAsync(Ct));
            await using var db = Db();
            var audit = Assert.Single(await db.AuditEvents.Where(x => x.ResourceId == stage.Id && x.EntityType == nameof(StoredFile)).ToArrayAsync(Ct));
            Assert.Equal(host.Services.GetRequiredService<FederationSettings>().Memberships[0].RoleReferences![0], audit.RoleContext);
            var settings = host.Services.GetRequiredService<FederationSettings>();
            settings.Memberships[0] = settings.Memberships[0] with { RoleReferences = null };
            Assert.Equal(HttpStatusCode.Forbidden, (await PutPng(client, stage.Id)).StatusCode);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root, false);
        }
    }
}
