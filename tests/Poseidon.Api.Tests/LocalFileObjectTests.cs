using Poseidon.Api;
using Xunit;

namespace Poseidon.Api.Tests;

public sealed class LocalFileObjectTests
{
    [Fact]
    public async Task Server_ids_are_exclusive_and_reparse_paths_cannot_escape_the_operator_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "poseidon-object-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "poseidon-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
        var tenant = Guid.NewGuid(); var id = Guid.NewGuid(); var objects = new LocalFileObjects(new() { Root = root });
        await using (var stream = objects.Create(tenant, id)) { await stream.WriteAsync(new byte[] {1,2,3}, TestContext.Current.CancellationToken); Assert.Throws<IOException>(() => objects.Create(tenant, id)); }
        await using (var read = objects.Open(tenant, id)) Assert.Equal(3, read.Length);
        objects.Remove(tenant, id);
        var path = Path.Combine(root, tenant.ToString("N") + "-" + id.ToString("N") + ".object");
        var target = Path.Combine(outside, "protected.txt"); await File.WriteAllTextAsync(target, "synthetic protected content", TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(path, target);
        Assert.Throws<IOException>(() => objects.Open(tenant, id)); Assert.Throws<IOException>(() => objects.Remove(tenant, id));
        Assert.Equal("synthetic protected content", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        File.Delete(path); File.Delete(target); Directory.Delete(root, false); Directory.Delete(outside, false);
    }
}
