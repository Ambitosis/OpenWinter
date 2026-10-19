using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Distribution;
using OpenWinter.Game.Persistence;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ModuleClientResourcesTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "openwinter-module-content-" + Guid.NewGuid().ToString("N"));
    readonly List<ResMan> _resources = new();
    public void Dispose()
    {
        foreach (var rm in _resources)
            foreach (var source in rm.Sources.OfType<IDisposable>()) source.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModulePresentationDownloadsWithoutServerStateAndKeepsLookupPriority(bool archive)
    {
        var paths = new GamePaths(Path.Combine(_root, "game"), Path.Combine(_root, "user"));
        Directory.CreateDirectory(Path.Combine(paths.GameRoot, "ovr"));
        File.WriteAllText(Path.Combine(paths.GameRoot, "ovr", "moduleonly.2da"), "base");
        Directory.CreateDirectory(Path.Combine(paths.UserRoot, "hak"));
        var hakEntries = new[] { new ErfEntry("shared", ResType.TwoDA, Encoding.UTF8.GetBytes("hak")) };
        File.WriteAllBytes(Path.Combine(paths.UserRoot, "hak", "priority.hak"), Erf.Write(hakEntries, "HAK "));
        var ifo = new GffStruct().SetString("Mod_Hak", "priority");
        var entries = new List<ErfEntry>
        {
            new("module", ResType.Ifo, Gff.Write(ifo, "IFO ")),
            new("shared", ResType.TwoDA, Encoding.UTF8.GetBytes("module")),
            new("moduleonly", ResType.TwoDA, Encoding.UTF8.GetBytes("custom")),
            new("model", ResType.Mdl, Encoding.UTF8.GetBytes("authored model")),
            new("texture", ResType.Tga, [1, 2, 3]),
            new("sound", ResType.Wav, [4, 5]),
            new("nui", ResType.Jui, Encoding.UTF8.GetBytes("{\"root\":{}}")),
            new("secret", ResType.Ncs, [6]), new("secret", ResType.Nss, [7]),
            new("creature", ResType.Utc, [8]), new("area", ResType.Are, [9]),
            new("state", ResType.Git, [10]), new("dialog", ResType.Dlg, [11])
        };
        string module = Path.Combine(_root, archive ? "module.mod" : "module");
        if (archive) File.WriteAllBytes(module, Erf.Write(entries, "MOD "));
        else
        {
            Directory.CreateDirectory(module);
            foreach (var e in entries) File.WriteAllBytes(Path.Combine(module, e.ResRef + "." + ResType.Extension(e.ResType)), e.Data);
        }
        var rm = ResMan.ForModule(paths, module); _resources.Add(rm);
        var world = new World(rm);
        world.Module.Ifo = ifo;
        world.System<PersistenceSystem>().RootOverride = Path.Combine(_root, "sandbox");
        var server = new ContentServer(world);
        var offered = WelcomeMessage.Read(new MessageReader(new WelcomeMessage { Files = server.Files.ToList() }.Write()));
        var package = Assert.Single(offered.Files, f => f.Kind == ContentKind.ModuleResources);
        var next = new ContentServer(world);
        Assert.Equal(package.Sha1, Assert.Single(next.Files, f => f.Kind == ContentKind.ModuleResources).Sha1);
        Assert.False(Directory.Exists(Path.Combine(paths.UserRoot, "cache")));
        var clientPaths = new GamePaths(paths.GameRoot, Path.Combine(_root, "client"));
        using var sync = new ContentSync([], [], Path.Combine(_root, "downloads"));
        sync.Plan(offered.Files);
        var (client, connection) = LoopbackConnection.CreatePair();
        var player = new PlayerSession { Connection = connection };
        foreach (var file in sync.Missing.ToArray()) server.Request(player, file.Sha1);
        for (int i = 0; i < 10 && !sync.Done; ++i)
        {
            server.Pump();
            while (client.TryReceive(out var data)) sync.Accept(ContentChunkMessage.Read(new MessageReader(data)));
        }
        Assert.True(sync.Done, sync.Error);
        var content = ResMan.ForClient(clientPaths, ["priority"], "", sync.Resolved, [package.Name]); _resources.Add(content);
        Assert.Null(content.ModuleSource);
        Assert.Equal("hak", Encoding.UTF8.GetString(content.Read("shared", ResType.TwoDA)!));
        Assert.Equal("custom", Encoding.UTF8.GetString(content.Read("moduleonly", ResType.TwoDA)!));
        foreach (var entry in entries.Where(e => ModuleClientResources.IsClientResource(e.ResType)))
            Assert.True(content.Contains(entry.ResRef, entry.ResType));
        foreach (var entry in entries.Where(e => !ModuleClientResources.IsClientResource(e.ResType)))
            Assert.False(content.Contains(entry.ResRef, entry.ResType));
        using var again = new ContentSync([], [], Path.Combine(_root, "downloads"));
        again.Plan(offered.Files);
        Assert.True(again.Done);
    }

    [Fact]
    public void ReplanningAbandonsPartialFilesAndIgnoresOldChunks()
    {
        using var sync = new ContentSync([], [], Path.Combine(_root, "downloads"));
        var bytes = Encoding.UTF8.GetBytes("new resource archive");
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes)).ToLowerInvariant();
        var old = new ContentFile { Kind = ContentKind.ModuleResources, Name = "module_resources", Size = 100, Sha1 = new string('a', 40) };
        sync.Plan([old]);
        Assert.False(sync.Accept(new ContentChunkMessage { Sha1 = old.Sha1, Total = 100, Data = [1, 2] }));
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "downloads"), "*.part"));
        var replacement = new ContentFile { Kind = ContentKind.ModuleResources, Name = old.Name, Size = bytes.Length, Sha1 = hash };
        sync.Plan([replacement]);
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "downloads"), "*.part"));
        Assert.Equal(0, sync.BytesReceived);
        Assert.False(sync.Accept(new ContentChunkMessage { Sha1 = old.Sha1, Total = 100, Offset = 2, Data = [3] }));
        Assert.True(sync.Accept(new ContentChunkMessage { Sha1 = hash, Total = bytes.Length, Data = bytes }));
        Assert.Equal(bytes, File.ReadAllBytes(sync.Resolved["module:module_resources"]));
    }

    [Fact]
    public void TruncatedCachedPackageIsRewrittenAndPackagingFailuresDoNotStopTheServer()
    {
        var paths = new GamePaths(Path.Combine(_root, "game"), Path.Combine(_root, "user"));
        string module = Path.Combine(_root, "module");
        Directory.CreateDirectory(module);
        File.WriteAllBytes(Path.Combine(module, "module.ifo"), Gff.Write(new GffStruct(), "IFO "));
        File.WriteAllBytes(Path.Combine(module, "texture.tga"), Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray());
        var rm = ResMan.ForModule(paths, module); _resources.Add(rm);
        string cache = Path.Combine(_root, "cache");
        var path = ModuleClientResources.Package(rm, cache)!;
        var good = File.ReadAllBytes(path);
        File.WriteAllBytes(path, good[..100]); // a crash mid-write left a short file under the hash name
        Assert.Equal(path, ModuleClientResources.Package(rm, cache));
        Assert.Equal(good, File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(cache));

        // The cache folder cannot be created (a file is in the way): the server still starts without the package.
        var world = new World(rm);
        world.System<PersistenceSystem>().RootOverride = Path.Combine(_root, "sandbox");
        Directory.CreateDirectory(Path.Combine(_root, "sandbox"));
        File.WriteAllText(Path.Combine(_root, "sandbox", "cache"), "not a folder");
        var server = new ContentServer(world);
        Assert.DoesNotContain(server.Files, f => f.Kind == ContentKind.ModuleResources);
    }
}
