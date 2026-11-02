using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed partial class DirectoryWorkspaceTests
{
    [Theory]
    [InlineData("9074729\n")]
    [InlineData("9074729\r\n")]
    [InlineData("9074729\u2028")]
    [InlineData("9074729\u2029")]
    [InlineData("٩٠٧٤٧٢٩")]
    public async Task Tug_rejects_noncanonical_imo_before_mutation_and_allows_corrected_same_attempt(string input)
    {
        using var host = Host(tugRead: true, tugWrite: true);
        using var client = Client(host); await Token(client);
        var key = Guid.NewGuid().ToString();
        var command = Draft(); command = command with { Inputs = command.Inputs with { AssistedVesselImo = input } };
        using var rejected = await Send(client, TugUrl, command, key);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using (var db = Db()) {
            Assert.False(await db.TugJobs.AnyAsync(row => row.Id == command.ClientId, Ct));
            Assert.False(await db.TugJobRevisions.AnyAsync(row => row.JobId == command.ClientId, Ct));
            Assert.False(await db.CommandReceipts.AnyAsync(row => row.Key == key, Ct));
        }
        var corrected = command with { Inputs = command.Inputs with { AssistedVesselImo = "9074729" } };
        using var accepted = await Send(client, TugUrl, corrected, key);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var result = (await accepted.Content.ReadFromJsonAsync<TugRequestDto>(Ct))!;
        Assert.Equal("9074729", result.Inputs.AssistedVesselImo);
        await using var committed = Db();
        Assert.Single(await committed.TugJobRevisions.Where(row => row.JobId == command.ClientId).ToArrayAsync(Ct));
    }
}
