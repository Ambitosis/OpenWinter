using System.IO.Compression;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.SaveGames;
using Xunit;
using static OpenWinter.Tests.Formats.MalformedInputTests;

namespace OpenWinter.Tests.Formats;

/// <summary>
/// Names and files that come from servers, modules, other players or downloaded saves: hak and TLK names, KEY
/// archive names, compressed database buffers, campaign databases, saved games and character quickbars.
/// </summary>
public sealed class LoaderHardeningTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("ow-loader").FullName;

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ---------------------------------------------------------------- path containment

    [Theory]
    [InlineData("cep2_top_v23", true)]
    [InlineData("my.hak.name", true)]
    [InlineData("..evil", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("../evil", false)]
    [InlineData("..\\evil", false)]
    [InlineData("sub/evil", false)]
    [InlineData("C:evil", false)]
    [InlineData("C:/Windows/evil", false)]
    [InlineData("/etc/evil", false)]
    [InlineData("evil\0", false)]
    [InlineData("evil:stream", false)]
    public void PlainNamesOnly(string name, bool plain) => Assert.Equal(plain, GamePaths.IsPlainName(name));

    [Theory]
    [InlineData("data/x.bif", true)]
    [InlineData("data\\x.bif", true)]
    [InlineData("data/../data/x.bif", true)]
    [InlineData("../x.bif", false)]
    [InlineData("data/../../x.bif", false)]
    [InlineData("/x.bif", false)]
    [InlineData("C:/x.bif", false)]
    [InlineData("C:x.bif", false)]
    [InlineData("", false)]
    public void ContainedStaysUnderTheRoot(string relative, bool inside)
    {
        var game = Path.Combine(_root, "game");
        var path = GamePaths.Contained(game, relative.Replace('\\', Path.DirectorySeparatorChar));
        Assert.Equal(inside, path != null);
        if (path != null) Assert.StartsWith(Path.GetFullPath(game), path, StringComparison.OrdinalIgnoreCase);
    }

    GamePaths Layout()
    {
        var game = Path.Combine(_root, "game");
        var user = Path.Combine(_root, "user");
        Directory.CreateDirectory(Path.Combine(game, "data", "hak"));
        Directory.CreateDirectory(Path.Combine(user, "hak"));
        Directory.CreateDirectory(Path.Combine(user, "tlk"));
        var erf = Erf.Write(new List<ErfEntry> { new("thing", 2017, Encoding.ASCII.GetBytes("2DA V2.0\n\nA\n0 x\n")) }, "HAK ");
        File.WriteAllBytes(Path.Combine(user, "hak", "good.hak"), erf);
        // Files outside the hak and TLK folders that a hostile name would try to reach.
        File.WriteAllBytes(Path.Combine(_root, "evil.hak"), erf);
        File.WriteAllBytes(Path.Combine(user, "evil.hak"), erf);
        File.WriteAllBytes(Path.Combine(_root, "evil.tlk"), new byte[40]);
        return new GamePaths(game, user);
    }

    [Fact]
    public void HakAndTlkNamesCannotLeaveTheirFolders()
    {
        var paths = Layout();
        Assert.True(File.Exists(paths.Hak("good")));
        foreach (var name in new[] { "../evil", "..\\evil", "../../evil", Path.Combine(_root, "evil"), "C:evil" })
        {
            Assert.False(File.Exists(paths.Hak(name)), name);
            Assert.Null(paths.Tlk(name));
            Assert.Null(paths.Tlk("../../" + name));
        }
        Assert.Null(GamePaths.Find(Path.Combine(_root, "user", "hak"), "../evil.hak"));
    }

    [Fact]
    public void ServerSuppliedNamesAreReportedMissing()
    {
        var paths = Layout();
        string[] haks = { "../evil", Path.Combine(_root, "evil"), "good", "..\\..\\evil" };
        using var rm = ResMan.ForClient(paths, haks, "../../evil");
        Assert.Equal(new[] { "../evil", Path.Combine(_root, "evil"), "..\\..\\evil" }, rm.MissingHaks);
        Assert.Single(rm.Sources.OfType<ErfSource>());
        Assert.Equal("", rm.Talk.Get(TalkTable.CustomFlag));
    }

    [Fact]
    public void KeyFilesCannotNameArchivesOutsideTheGameFolder()
    {
        var (key, bif) = ValidKeyBif("..\\outside.bif");
        var game = Path.Combine(_root, "game");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(_root, "outside.bif"), bif);
        File.WriteAllBytes(Path.Combine(game, "test.key"), key);
        var source = new KeyBifSource(Path.Combine(game, "test.key"), game);
        Assert.Contains(ResKey.Of("classes", ResType.TwoDA), source.Keys);
        Assert.Null(source.Read(ResKey.Of("classes", ResType.TwoDA)));

        var (inside, insideBif) = ValidKeyBif("data\\test.bif");
        Directory.CreateDirectory(Path.Combine(game, "data"));
        File.WriteAllBytes(Path.Combine(game, "data", "test.bif"), insideBif);
        File.WriteAllBytes(Path.Combine(game, "inside.key"), inside);
        Assert.NotNull(new KeyBifSource(Path.Combine(game, "inside.key"), game).Read(ResKey.Of("classes", ResType.TwoDA)));
    }

    [Fact]
    public void DownloadCatalogueCannotNameFilesOutsideTheCache()
    {
        var paths = Layout();
        var evil = File.ReadAllBytes(Path.Combine(_root, "evil.hak"));
        string sha1 = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(evil)).ToLowerInvariant();
        var cache = Path.Combine(_root, "cache");
        using var sync = new OpenWinter.Net.ContentSync(new[] { Path.Combine(paths.UserRoot, "hak") }, Array.Empty<string>(), cache);
        sync.Plan(new[]
        {
            // A name that climbs out of the hak folder to a file with the offered hash is not used.
            new OpenWinter.Net.ContentFile { Kind = OpenWinter.Net.ContentKind.Hak, Name = "../../evil", Size = evil.Length, Sha1 = sha1 },
            // A "hash" that is a path would name the downloaded file; it is refused outright.
            new OpenWinter.Net.ContentFile { Kind = OpenWinter.Net.ContentKind.Hak, Name = "x", Size = 1, Sha1 = "../../../outside" },
        });
        Assert.Empty(sync.Resolved);
        Assert.Empty(sync.Missing);
        Assert.False(File.Exists(Path.Combine(_root, "outside.hak")));
    }

    // ---------------------------------------------------------------- compressed buffers and databases

    static byte[] Zlib(string magic, byte[] data)
    {
        var z = new MemoryStream();
        using (var s = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) s.Write(data);
        var o = new byte[20 + z.Length];
        Encoding.ASCII.GetBytes(magic, 0, 4, o, 0);
        Put32(o, 4, 3);
        Put32(o, 8, CompressedBuffer.AlgorithmZlib);
        Put32(o, 12, (uint)data.Length);
        Put32(o, 16, 1);
        z.ToArray().CopyTo(o, 20);
        return o;
    }

    [Fact]
    public void CompressedBuffersSurviveMutation()
    {
        var data = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("campaign value ", 40)));
        Assert.Equal(data, CompressedBuffer.Unwrap("CPDB", Zlib("CPDB", data)));
        Survives("zstd buffer", Mutations(CompressedBuffer.Wrap("CPDB", data), 100), b => CompressedBuffer.Unwrap("CPDB", b), typeof(InvalidDataException));
        Survives("zlib buffer", Mutations(Zlib("CPDB", data), 101), b => CompressedBuffer.Unwrap("CPDB", b), typeof(InvalidDataException));
        Survives("raw buffer", Mutations(CompressedBuffer.Wrap("CPDB", data, CompressedBuffer.AlgorithmNone), 102), b => CompressedBuffer.Unwrap("CPDB", b), typeof(InvalidDataException));

        // Declared sizes far beyond the data must not be allocated up front.
        var hostile = new List<byte[]>();
        foreach (uint size in new uint[] { 0x7FFFFFFF, 0xFFFFFFFF, 200_000_000 })
            foreach (var b in new[] { Zlib("CPDB", data), CompressedBuffer.Wrap("CPDB", data) })
            {
                Put32(b, 12, size);
                hostile.Add(b);
            }
        Survives("buffer sizes", hostile, b => CompressedBuffer.Unwrap("CPDB", b), typeof(InvalidDataException));
    }

    [Fact]
    public void DatabaseImagesSurviveMutation()
    {
        byte[] image;
        using (var db = SqlDb.OpenMemory())
        {
            db.Exec("CREATE TABLE t (k TEXT PRIMARY KEY, v BLOB); INSERT INTO t VALUES ('gold', x'0102030405');");
            image = db.Serialize();
        }
        Survives("SQLite image", Mutations(image, 110, 150, everyTruncation: false), b =>
        {
            using var db = SqlDb.OpenMemory();
            db.Load(b);
        }, typeof(InvalidDataException));
    }

    [Fact]
    public void UnreadableCampaignDatabaseReadsAsMissing()
    {
        var world = new World(new ResMan(new GamePaths(Path.Combine(_root, "game"), Path.Combine(_root, "user"))));
        var sys = world.System<PersistenceSystem>();
        sys.RootOverride = Path.Combine(_root, "user");
        sys.Log = _ => { };
        try
        {
            Directory.CreateDirectory(sys.DatabaseDir);
            var garbage = Enumerable.Range(0, 8192).Select(i => (byte)(i * 31)).ToArray();
            File.WriteAllBytes(sys.CampaignPath("broken"), garbage);
            Assert.Null(sys.Campaign("broken"));
            Assert.Null(sys.Campaign("broken", create: false));
            Assert.Equal(garbage, File.ReadAllBytes(sys.CampaignPath("broken")));
            Assert.NotNull(sys.Campaign("fine"));
        }
        finally
        {
            sys.Dispose();
        }
    }

    // ---------------------------------------------------------------- saved games

    static byte[] OwnSave()
    {
        var info = new GffStruct().SetString("Name", "Before the boss").SetString("SavedAt", "2024-01-02T03:04:05Z")
            .SetString("PlayedSeconds", "1234.5").SetString("Module", "Test").SetString("AreaName", "Cave").SetString("PlayerName", "Hero");
        var snapshot = Snapshot("{\"world\":{\"objects\":[1,2,3]}}");
        return Erf.Write(new List<ErfEntry>
        {
            new(SaveGameService.InfoResRef, ResType.Gff, Gff.Write(info, "GFF ")),
            new(SaveGameService.SnapshotResRef, ResType.Gff, Gff.Write(snapshot, "GFF ")),
        }, "SAV ");
    }

    static GffStruct Snapshot(string json)
    {
        var z = new MemoryStream();
        using (var gz = new GZipStream(z, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(Encoding.UTF8.GetBytes(json));
        return new GffStruct().SetString("Encoding", "json+gzip").Set("Snapshot", GffType.Void, z.ToArray());
    }

    static byte[] NativeSave()
    {
        var name = new LocString();
        name.Strings[0] = "Aribeth";
        var ifo = new GffStruct().SetLocString("Mod_Name", new LocString("Chapter 1")).SetInt("Mod_StartYear", 1372)
            .SetList("Mod_PlayerList", new List<GffStruct> { new GffStruct().SetLocString("FirstName", name).SetDWord("AreaId", 7) })
            .SetList("Mod_Area_list", new List<GffStruct> { new GffStruct().SetDWord("ObjectId", 7).SetResRef("Area_Name", "cave01") });
        var are = new GffStruct().SetLocString("Name", new LocString("Cave"));
        return Erf.Write(new List<ErfEntry>
        {
            new("module", ResType.Ifo, Gff.Write(ifo, "IFO ")),
            new("cave01", ResType.Are, Gff.Write(are, "ARE ")),
        }, "MOD ");
    }

    [Fact]
    public void SaveListingSurvivesDamagedSaves()
    {
        var saves = Path.Combine(_root, "saves");
        var own = Path.Combine(saves, "000002 - own");
        var native = Path.Combine(saves, "000003 - native");
        Directory.CreateDirectory(own);
        Directory.CreateDirectory(native);
        File.WriteAllBytes(Path.Combine(own, SaveGameService.SaveFile), OwnSave());
        File.WriteAllBytes(Path.Combine(native, "chapter1.sav"), NativeSave());
        var listed = SaveGameService.List(saves);
        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, s => s.PlayerName == "Aribeth" && s.AreaName == "Cave");

        var log = SaveGameService.Log;
        SaveGameService.Log = _ => { };
        try
        {
            Survives("own save", Mutations(OwnSave(), 120, 200, everyTruncation: false), b =>
            {
                File.WriteAllBytes(Path.Combine(own, SaveGameService.SaveFile), b);
                Assert.True(SaveGameService.List(saves).Count <= 2);
            });
            File.WriteAllBytes(Path.Combine(own, SaveGameService.SaveFile), OwnSave());
            Survives("native save", Mutations(NativeSave(), 121, 200, everyTruncation: false), b =>
            {
                File.WriteAllBytes(Path.Combine(native, "chapter1.sav"), b);
                Assert.True(SaveGameService.List(saves).Count <= 2);
            });
            Survives("native save info", Mutations(NativeSave(), 122, 200, everyTruncation: false), b =>
            {
                File.WriteAllBytes(Path.Combine(native, "chapter1.sav"), b);
                NativeSaveGame.ReadInfo(native);
            }, typeof(InvalidDataException), typeof(FileNotFoundException));
        }
        finally
        {
            SaveGameService.Log = log;
        }
    }

    [Fact]
    public void SnapshotSurvivesMutation()
    {
        var gff = Gff.Write(Snapshot("{\"world\":{\"objects\":[1,2,3],\"name\":\"x\"}}"), "GFF ");
        Assert.NotNull(SaveGameService.ReadSnapshot(Gff.Read(gff))["world"]);
        Survives("snapshot", Mutations(gff, 130), b => SaveGameService.ReadSnapshot(Gff.Read(b)), typeof(InvalidDataException));

        // A small stream that inflates past the limit is refused.
        var bomb = Snapshot("{\"pad\":\"" + new string(' ', 1 << 20) + "\"}");
        Assert.Throws<InvalidDataException>(() => SaveGameService.ReadSnapshot(bomb, maxBytes: 1 << 16));
        Assert.Throws<InvalidDataException>(() => SaveGameService.ReadSnapshot(Snapshot("[1,2,3]")));
        Assert.Throws<InvalidDataException>(() => SaveGameService.ReadSnapshot(Snapshot("{\"open\":")));
    }

    // ---------------------------------------------------------------- character quickbars

    [Fact]
    public void QuickbarSurvivesMutation()
    {
        var sword = new GffStruct(4).SetString("UUID", "sword").SetByte("Repos_PosX", 0).SetByte("Repos_Posy", 0);
        var bag = new GffStruct().SetString("UUID", "bag").SetByte("Repos_PosX", 1).SetByte("Repos_Posy", 0)
            .SetList("ItemList", new List<GffStruct> { new GffStruct().SetString("UUID", "potion") });
        var slots = new List<GffStruct>
        {
            new GffStruct().SetByte("QBObjectType", 1).SetDWord("QBItemInvSlot", uint.MaxValue).SetByte("QBItemReposX", 0).SetByte("QBItemReposY", 0),
            new GffStruct().SetByte("QBObjectType", 2).SetInt("QBINTParam1", 12).SetByte("QBMultiClass", 0),
            new GffStruct().SetByte("QBObjectType", 18).SetString("QBCommandLabel", "Hi").SetString("QBCommandLine", "hello"),
            new GffStruct().SetByte("QBObjectType", 0).SetString("ElySlot", "{\"Kind\":\"item\"").SetString("ElyItemUuid", "potion"),
            new GffStruct().SetByte("QBObjectType", 1).SetDWord("QBItemInvSlot", 16).SetDWord("QBSecItemInvSlot", 32),
        };
        var character = new GffStruct()
            .SetList("ClassList", new List<GffStruct> { new GffStruct().SetInt("Class", 10) })
            .SetList("Equip_ItemList", new List<GffStruct> { new GffStruct(16).SetString("UUID", "axe") })
            .SetList("ItemList", new List<GffStruct> { sword, bag })
            .SetList("QBList", slots);
        var bic = Gff.Write(character, "BIC ");
        Assert.Equal(5, BicQuickbar.Read(Gff.Read(bic))!.Length);
        Survives("quickbar", Mutations(bic, 140, 400, everyTruncation: false), b => BicQuickbar.Read(Gff.Read(b)), typeof(InvalidDataException));
    }
}
