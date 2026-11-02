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
        if (OperatingSystem.IsWindows())
        {
            // Directory junctions exercise the actual reparse boundary without the symlink privilege.
            var junction = Path.Combine(root, "junction");
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", junction, outside }) info.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(info)!;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken); Assert.Equal(0, process.ExitCode);
            var redirected = new LocalFileObjects(new() { Root = junction });
            Assert.Throws<IOException>(() => redirected.Open(tenant, id)); Assert.Throws<IOException>(() => redirected.Remove(tenant, id));
            Assert.Throws<IOException>(() => redirected.Create(tenant, id));
            Directory.Delete(junction, false);
        }
        else
        {
            File.CreateSymbolicLink(path, target);
            Assert.Throws<IOException>(() => objects.Open(tenant, id)); Assert.Throws<IOException>(() => objects.Remove(tenant, id));
            File.Delete(path);
        }
        Assert.Equal("synthetic protected content", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        File.Delete(target); Directory.Delete(root, false); Directory.Delete(outside, false);
    }
}
