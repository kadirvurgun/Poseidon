using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Poseidon.Tests;
public sealed class BffBrowserFactAttribute : FactAttribute
{
    public BffBrowserFactAttribute() {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSEIDON_BFF_BROWSER_DIST")) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("POSEIDON_TEST_POSTGRES")))
            Skip = "Requires an explicitly built BFF frontend, installed Chromium/Playwright and disposable PostgreSQL.";
    }
}
public partial class BffTests
{
    [BffBrowserFact] public async Task Real_browser_ui_through_actual_oidc_and_cookie_handlers()
    {
        await using var evidence=new EvidenceApp();
        await using(var db=Npgsql.NpgsqlDataSource.Create(evidence.Connection)){var data=new DraftStore(db);await data.Migrate(default);await data.Seed(default);}
        using var provider = new FakeProvider("valid");
        await using var app = new App(provider: provider, database: true, certificateClock: new CertificateClock(), evidenceRoot: evidence.Root, evidenceDatabase:evidence.Connection);
        app.Grants.CanManageInventory = true;
        app.Grants.CanManageCertificates = true;
        app.Grants.CanCaptureEvidence = true;
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        await app.Services.GetRequiredService<DraftStore>().Migrate(default);
        await app.Services.GetRequiredService<DraftStore>().Seed(default);
        using var process = new Process { StartInfo = new ProcessStartInfo("node", Path.Combine(AppContext.BaseDirectory, "bff-browser.cjs")) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        process.Start();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(240));
        try {
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line) {
                var message = JsonDocument.Parse(line).RootElement;
                var id = message.GetProperty("id").GetInt32(); var op = message.GetProperty("op").GetString();
                object response;
                if (op == "authorize") {
                    provider.Nonce = message.GetProperty("nonce").GetString()!;
                    provider.Challenge = message.GetProperty("challenge").GetString()!;
                    response = new { id, ok = true };
                } else if (op == "write_permission") { app.Grants.CanWrite = message.GetProperty("allowed").GetBoolean(); response = new { id, ok = true }; }
                else if (op == "expire") { app.Store.Expire(); response = new { id, ok = true }; }
                else if (op == "request") {
                    var uri = new Uri(message.GetProperty("url").GetString()!);
                    Assert.Equal("https://localhost", uri.GetLeftPart(UriPartial.Authority));
                    using var request = new HttpRequestMessage(new HttpMethod(message.GetProperty("method").GetString()!), uri);
                    var body = message.GetProperty("body").GetString();
                    if (message.TryGetProperty("bodyBase64",out var raw) && raw.ValueKind==JsonValueKind.String) request.Content=new ByteArrayContent(Convert.FromBase64String(raw.GetString()!));
                    else if (body is not null) request.Content = new StringContent(body, Encoding.UTF8);
                    foreach (var header in message.GetProperty("headers").EnumerateObject()) {
                        if (header.Name is "host" or "content-length") continue;
                        if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString()) && request.Content is not null) {
                            request.Content.Headers.Remove(header.Name); request.Content.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
                        }
                    }
                    using var result = await client.SendAsync(request, timeout.Token);
                    response = new { id, status = (int)result.StatusCode,
                        headers = result.Headers.Concat(result.Content.Headers).ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join("\n", h.Value)),
                        body = Convert.ToBase64String(await result.Content.ReadAsByteArrayAsync(timeout.Token)) };
                } else throw new InvalidOperationException("Unexpected browser bridge operation.");
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(response));
                await process.StandardInput.FlushAsync(timeout.Token);
            }
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode == 0, await stderr);
            Assert.True(provider.Redemptions >= 2);
            Assert.Equal(0, app.Store.Count);
        } finally { if (!process.HasExited) process.Kill(true); }
    }
}
