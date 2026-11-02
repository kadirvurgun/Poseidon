using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class SecureFilePolicyTests
{
    [Theory]
    [InlineData("../a.png")][InlineData("a\\b.png")][InlineData("a.png\n")][InlineData("x.svg")]
    [InlineData("a.zip")][InlineData("a.html.png\u2028")][InlineData("a..png")][InlineData("a.png:stream")]
    public void Traversal_archives_active_and_noncomplete_names_are_rejected(string name) => Assert.False(SecureFilePolicy.ValidName(name));

    [Fact]
    public async Task Byte_limit_uses_actual_stream_not_declared_length_and_never_writes_over_limit()
    {
        using var body = new MemoryStream(new byte[1025]); using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => SecureFilePolicy.CopyAsync(body, output, 1024, TestContext.Current.CancellationToken));
        Assert.True(output.Length <= 1024);
    }

    [Fact]
    public async Task Checksum_is_computed_from_exact_bytes_and_cancellation_is_preserved()
    {
        var bytes = new byte[] {1,2,3,4}; using var body = new MemoryStream(bytes); using var output = new MemoryStream();
        var observed = await SecureFilePolicy.CopyAsync(body, output, 4, TestContext.Current.CancellationToken);
        Assert.Equal(4, observed.Length);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), observed.Checksum);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); body.Position = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SecureFilePolicy.CopyAsync(body, output, 4, cancelled.Token));
    }

    [Theory]
    [InlineData("<html>active</html>")][InlineData("PK\u0003\u0004archive")][InlineData("%PDF-active")]
    public async Task Disguised_magic_is_rejected(string value)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
        var error = await Record.ExceptionAsync(() => SecureFilePolicy.ValidatePngAsync(stream, 100, TestContext.Current.CancellationToken));
        Assert.True(error is InvalidDataException or IOException);
    }
}
