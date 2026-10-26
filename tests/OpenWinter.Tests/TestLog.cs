using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>
/// A test's output as a sink for a static engine log. Tests in other classes run in parallel and may write to the
/// static log after the test that set it has ended; those lines are dropped instead of failing the other test.
/// </summary>
static class TestLog
{
    public static Action<string> To(ITestOutputHelper output) => line =>
    {
        try { output.WriteLine(line); }
        catch (InvalidOperationException) { }
    };
}
