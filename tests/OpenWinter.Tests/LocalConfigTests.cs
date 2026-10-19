using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class LocalConfigTests
{
    [Fact]
    public void AFolderWithoutTheBaseKeyIsNotTheGame()
    {
        Assert.False(LocalConfig.IsGameFolder(Path.GetTempPath()));
    }

    /// <summary>An installed copy without config/local.json finds the game and writes only to its own folder.</summary>
    [DataFact]
    public void DetectFindsTheInstalledGameAndKeepsWritesInTheDataFolder()
    {
        if (!LocalConfig.IsGameFolder(TestData.Config!.GameRoot)) return;
        var data = Path.Combine(Path.GetTempPath(), "openwinter-detect-" + Guid.NewGuid().ToString("N"));
        var cfg = LocalConfig.Detect(data);
        Assert.NotNull(cfg);
        Assert.True(LocalConfig.IsGameFolder(cfg!.GameRoot));
        Assert.Equal(data, cfg.WriteRoot);
        Assert.False(cfg.LiveUserData);
        Assert.False(Directory.Exists(data));
    }
}
