using Xunit;
namespace OpenWinter.Tests;

public class ParityResultGuardTests
{
    [Fact]
    public void EqualPartialOutputWithoutCompletionIsInvalid()
    {
        Assert.NotEmpty(ParityResultGuard.Failures(["PARITY|first|42"]));
        Assert.NotEmpty(ParityResultGuard.Failures([]));
    }
    [Theory]
    [InlineData("PARITY|__done__|0")]
    [InlineData("PARITY|__done__extra|1")]
    [InlineData("PARITY|test|__done__|1")]
    public void OnlyExactSuccessMarkerCompletes(string line) => Assert.False(ParityResultGuard.IsComplete(line));
    [Theory]
    [InlineData("Script error: fixture OID: 00000000, ERROR: TOO MANY INSTRUCTIONS")]
    [InlineData("PARITY|error.fixture|TOO MANY INSTRUCTIONS")]
    [InlineData("[script error] Invalid NCS header")]
    [InlineData("[script] not implemented: Example (in fixture)")]
    public void ErrorsFailEvenWhenCompletionExists(string error)
        => Assert.Single(ParityResultGuard.Failures([error, "PARITY|__done__|1"]));
    [Fact]
    public void CompleteErrorFreeRunIsValid()
        => Assert.Empty(ParityResultGuard.Failures(["[timestamp] PARITY|sample|42", "[timestamp] PARITY|__done__|1\r\n"]));
}
