using TeleFlow.Telegram.Internal.Handlers;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramRuntimeMetadataTests
{
    [Fact]
    public void FilterCompatibility_RejectsUnsupportedFilterKind()
    {
        var unsupported = (TelegramFilterKind)int.MaxValue;

        var exception = Assert.Throws<InvalidOperationException>(
            () => TelegramFilterCompatibility.Supports(unsupported, TelegramHandlerKind.Message));

        Assert.Contains("Unsupported Telegram filter kind", exception.Message);
    }
}
