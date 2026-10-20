using OpenWinter.Aurora.Resources;
using Xunit;

namespace OpenWinter.Tests;

public class GamePathsTests
{
    [Fact]
    public void HaksTlksAndModulesMatchWithoutRegardToCase()
    {
        var root = Directory.CreateTempSubdirectory("openwinter-paths").FullName;
        try
        {
            var user = Path.Combine(root, "user");
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(user, "hak"));
            Directory.CreateDirectory(Path.Combine(game, "data", "tlk"));
            File.WriteAllText(Path.Combine(user, "hak", "CEP2_Top.hak"), "");
            File.WriteAllText(Path.Combine(game, "data", "tlk", "MyServer.tlk"), "");
            var paths = new GamePaths(game, user);

            Assert.Equal("CEP2_Top.hak", Path.GetFileName(paths.Hak("cep2_top")), ignoreCase: true);
            Assert.True(File.Exists(paths.Hak("cep2_top")));
            Assert.True(File.Exists(paths.Tlk("myserver")));
            Assert.Null(paths.Tlk("missing"));
            Assert.Null(GamePaths.Find(Path.Combine(root, "absent"), "x.mod"));
        }
        finally { Directory.Delete(root, true); }
    }
}
