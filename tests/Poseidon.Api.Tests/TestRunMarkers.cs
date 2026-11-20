using System.Reflection;
using Xunit;
using Xunit.v3;

[assembly: Poseidon.Api.Tests.TestRunMarker]

namespace Poseidon.Api.Tests;

// CI hang attribution only (api_test_gate.py). When the gate names a marker file, each test
// appends its static class.method identifier on start and finish. No arguments, output,
// exception text or data values are written, so a deadline can name the unfinished test
// without persisting raw runner output. Absent the variable this is a no-op.
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TestRunMarkerAttribute : BeforeAfterTestAttribute
{
    private static readonly string? File = Environment.GetEnvironmentVariable("POSEIDON_TEST_MARKER_FILE");
    private static readonly Lock Gate = new();

    public override void Before(MethodInfo methodUnderTest, IXunitTest test) => Write('S', methodUnderTest);
    public override void After(MethodInfo methodUnderTest, IXunitTest test) => Write('E', methodUnderTest);

    private static void Write(char kind, MethodInfo method)
    {
        if (string.IsNullOrEmpty(File)) return;
        var type = method.ReflectedType ?? method.DeclaringType;
        if (type?.FullName is not { } name) return;
        // Open/append/close per line: the content survives the gate killing the process tree.
        lock (Gate) System.IO.File.AppendAllText(File, $"{kind}\t{name.Replace('+', '.')}.{method.Name}\n");
    }
}
