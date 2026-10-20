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

    [Fact]
    public void AnInstalledCopyWritesBesideItsConfigAndACheckoutIntoOut()
    {
        var root = Directory.CreateTempSubdirectory("openwinter-config").FullName;
        try
        {
            var path = Path.Combine(Directory.CreateDirectory(Path.Combine(root, "config")).FullName, "local.json");
            File.WriteAllText(path, """{ "gameRoot": "/srv/nwn" }""");
            Assert.Equal(Path.Combine(root, "userdata"), LocalConfig.Load(path).WriteRoot);
            File.WriteAllText(Path.Combine(root, "OpenWinter.slnx"), "");
            Assert.Equal(Path.Combine(root, "out", "sandbox-userdata"), LocalConfig.Load(path).WriteRoot);
        }
        finally { Directory.Delete(root, true); }
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
