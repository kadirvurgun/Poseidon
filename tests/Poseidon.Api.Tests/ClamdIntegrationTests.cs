using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

// Required at the combined gate. No skip, permissive scanner substitute, or deployment verdict from configuration.
[Trait("Category", "Integration")]
public sealed class ClamdIntegrationTests
{
    [Fact]
    public async Task Operator_supplied_real_clamd_accepts_passive_bytes_and_rejects_standard_test_signature()
    {
        var value = Environment.GetEnvironmentVariable("POSEIDON_GATE_CLAMD_PORT");
        Assert.True(int.TryParse(value, out var port) && port is > 0 and <= 65535, "Real loopback clamd is required at AUDIT-FILES-BUNDLE-01.");
        var scanner = new ClamdScanner(new() { ClamdPort = port, ScanSeconds = 10 });
        using var clean = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("POSEIDON synthetic passive scanner transport fixture"));
        await scanner.ScanAsync(clean, TestContext.Current.CancellationToken);
        // EICAR is an inert antivirus test signature, assembled here; it is never imported/seeded as evidence.
        var marker = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$" + "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        using var test = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(marker));
        await Assert.ThrowsAsync<InvalidDataException>(() => scanner.ScanAsync(test, TestContext.Current.CancellationToken));
    }
}
