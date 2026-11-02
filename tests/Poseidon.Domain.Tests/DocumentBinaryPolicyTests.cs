using System.Text;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

public sealed class DocumentBinaryPolicyTests
{
    [Theory]
    [InlineData("draft.pdf", "application/pdf", true)]
    [InlineData("Gemi belgesi.pdf", "application/pdf", true)]
    [InlineData("Çekici raporu.jpg", "image/jpeg", true)]
    [InlineData("e\u0301.pdf", "application/pdf", false)]
    [InlineData("scan.jpeg", "image/jpeg", true)]
    [InlineData("scan.jpg", "image/jpeg", true)]
    [InlineData("scan.png", "image/png", true)]
    [InlineData("scan.pdf", "image/png", false)]
    [InlineData("../scan.pdf", "application/pdf", false)]
    [InlineData("scan%2fpdf.pdf", "application/pdf", false)]
    [InlineData("scan.exe.pdf", "application/pdf", false)]
    [InlineData("scan.pdf\u202e", "application/pdf", false)]
    [InlineData("scan.svg", "image/svg+xml", false)]
    public void Explicit_type_name_policy_does_not_admit_active_or_disguised_types(string name, string mime, bool valid) =>
        Assert.Equal(valid, DocumentBinaryPolicy.ValidName(name, mime));

    [Fact]
    public async Task Limited_pdf_signature_is_a_type_check_and_checksum_limit_is_independent()
    {
        using var pdf = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nsynthetic unverified document\n%%EOF\n"));
        await DocumentBinaryPolicy.ValidateAsync(pdf, "application/pdf", 10, TestContext.Current.CancellationToken);
        pdf.Position = 0; using var output = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => SecureFilePolicy.CopyAsync(pdf, output, pdf.Length - 1, TestContext.Current.CancellationToken));
        using var disguised = new MemoryStream(Encoding.ASCII.GetBytes("<script>synthetic</script>"));
        await Assert.ThrowsAsync<InvalidDataException>(() => DocumentBinaryPolicy.ValidateAsync(disguised, "application/pdf", 10, TestContext.Current.CancellationToken));
        using var appended = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n<script/>"));
        await Assert.ThrowsAsync<InvalidDataException>(() => DocumentBinaryPolicy.ValidateAsync(appended, "application/pdf", 10, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Jpeg_requires_both_start_and_end_and_unknown_types_fail_closed()
    {
        using var jpeg = new MemoryStream(new byte[] { 255, 216, 255, 224, 0, 2, 255, 217 });
        await DocumentBinaryPolicy.ValidateAsync(jpeg, "image/jpeg", 10, TestContext.Current.CancellationToken);
        jpeg.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => DocumentBinaryPolicy.ValidateAsync(jpeg, "application/zip", 10, TestContext.Current.CancellationToken));
        using var truncated = new MemoryStream(new byte[] { 255, 216, 255, 224, 0, 2, 0, 0 });
        await Assert.ThrowsAsync<InvalidDataException>(() => DocumentBinaryPolicy.ValidateAsync(truncated, "image/jpeg", 10, TestContext.Current.CancellationToken));
    }
}
