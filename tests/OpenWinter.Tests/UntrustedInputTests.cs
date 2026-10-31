using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Messages and downloads from an untrusted peer: bad lengths, paths and login sequences are refused.</summary>
public class UntrustedInputTests
{
    static byte[] WithCount(MessageType type, int count)
    {
        var bytes = new List<byte> { (byte)type };
        bytes.AddRange(BitConverter.GetBytes(count));
        return bytes.ToArray();
    }

    [Fact]
    public void LengthsBeyondTheMessageAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => new MessageReader(WithCount(MessageType.ContentChunk, int.MaxValue)).Bytes());
        Assert.Throws<InvalidDataException>(() => new MessageReader(WithCount(MessageType.ContentChunk, -1)).Bytes());
        Assert.Throws<InvalidDataException>(() => new MessageReader(WithCount(MessageType.Appearance, int.MaxValue)).U16s());
        var area = new LoadAreaMessage { Tiles = [new TileInfo()] }.Write();
        // The tile count sits after the fixed header; claim far more tiles than the message holds.
        int at = area.Length - 11 - 4 - 4 - 1 - 4;
        BitConverter.GetBytes(int.MaxValue).CopyTo(area, at);
        Assert.Throws<InvalidDataException>(() => LoadAreaMessage.Read(new MessageReader(area)));
        // Well-formed messages still round-trip.
        var chunk = ContentChunkMessage.Read(new MessageReader(new ContentChunkMessage { Sha1 = "ab", Data = [1, 2, 3] }.Write()));
        Assert.Equal([1, 2, 3], chunk.Data);
    }

    [Fact]
    public void DownloadsStayInTheCache()
    {
        var root = Path.Combine(Path.GetTempPath(), "openwinter-untrusted-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(root, "cache");
        try
        {
            var sync = new ContentSync([], [], cache);
            var good = new ContentFile { Kind = ContentKind.Hak, Name = "good", Size = 4, Sha1 = new string('a', 40) };
            sync.Plan([
                new ContentFile { Kind = ContentKind.Hak, Name = "x", Size = 4, Sha1 = "../../escaped" },
                new ContentFile { Kind = ContentKind.Hak, Name = "../escaped", Size = 4, Sha1 = new string('b', 40) },
                good,
            ]);
            Assert.Equal([good], sync.Missing);

            // A chunk outside the file's declared size is refused before anything is written.
            Assert.False(sync.Accept(new ContentChunkMessage { Sha1 = good.Sha1, Offset = 1L << 40, Data = [1] }));
            Assert.NotNull(sync.Error);
            Assert.False(File.Exists(Path.Combine(cache, good.Sha1 + ".hak.part")));
            sync.Plan([good]);
            Assert.False(sync.Accept(new ContentChunkMessage { Sha1 = good.Sha1, Offset = -1, Data = [1] }));
            Assert.NotNull(sync.Error);
            sync.Dispose();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root, "*escaped*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    static (World World, GameServer Server) Server(ServerOptions options)
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        options.StartArea = "ss_town";
        return (world, new GameServer(world, options));
    }

    static List<byte[]> Drain(LoopbackConnection c)
    {
        var list = new List<byte[]>();
        while (c.TryReceive(out var m)) list.Add(m);
        return list;
    }

    [DataFact]
    public void MalformedMessagesDropTheClientNotTheServer()
    {
        var (_, server) = Server(new ServerOptions());
        var (bad, badEnd) = LoopbackConnection.CreatePair();
        var (good, goodEnd) = LoopbackConnection.CreatePair();
        server.Accept(badEnd);
        server.Accept(goodEnd);
        bad.Send([(byte)MessageType.Hello, 5]);
        server.Tick(1f / 30);
        Assert.False(bad.IsConnected);
        foreach (var message in new[] { Array.Empty<byte>(), [(byte)MessageType.ClientRequest, 0x80] })
        {
            var (c, end) = LoopbackConnection.CreatePair();
            server.Accept(end);
            c.Send(message);
            server.Tick(1f / 30);
            Assert.False(c.IsConnected);
        }
        good.Send(new HelloMessage { PlayerName = "Survivor" }.Write());
        server.Tick(1f / 30);
        Assert.Contains(Drain(good), m => m[0] == (byte)MessageType.Welcome);
    }

    [DataFact]
    public void PlayersCannotNameServerFilesOrSkipLogin()
    {
        var (world, server) = Server(new ServerOptions { CharacterSelection = true, Vault = VaultMode.Server });
        var name = "Shortcut" + Guid.NewGuid().ToString("N")[..8];
        var (client, end) = LoopbackConnection.CreatePair();
        server.Accept(end);
        // A character choice before Hello is ignored.
        client.Send(new CharacterChoiceMessage { File = "anything" }.Write());
        server.Tick(1f / 30);
        Assert.Empty(Drain(client));
        // The local-vault shortcut does not skip character selection for players, with or without a path.
        client.Send(new HelloMessage { PlayerName = name, PlayerKey = "K1", CharacterResRef = "../localvault/freddiepharkasf1" }.Write());
        server.Tick(1f / 30);
        Assert.Contains(Drain(client), m => m[0] == (byte)MessageType.CharacterList);
        Assert.DoesNotContain(world.AllObjects.OfType<Creature>(), c => c.IsPC);
        // A second Hello on the same connection is ignored.
        client.Send(new HelloMessage { PlayerName = "Someone else", PlayerKey = "K1" }.Write());
        server.Tick(1f / 30);
        Assert.Empty(Drain(client));
        DeleteVault(world, name);
    }

    static void DeleteVault(World world, string player)
    {
        var dir = Path.Combine(world.System<OpenWinter.Game.Persistence.PersistenceSystem>().UserRoot, "servervault", player);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    [DataFact]
    public void StickyNamesStayWithTheFirstKey()
    {
        var (world, server) = Server(new ServerOptions { CharacterSelection = true, Vault = VaultMode.Server, StickyPlayerNames = true });
        var name = "Owner" + Guid.NewGuid().ToString("N")[..8];
        var known = Path.Combine(world.System<OpenWinter.Game.Persistence.PersistenceSystem>().UserRoot, "override",
            OpenWinter.Game.Persistence.StickyPlayerNames.FileName);
        LoopbackConnection Login(string player, string key)
        {
            var (c, end) = LoopbackConnection.CreatePair();
            server.Accept(end);
            c.Send(new HelloMessage { PlayerName = player, PlayerKey = key }.Write());
            server.Tick(1f / 30);
            return c;
        }
        try
        {
            var owner = Login(name, "OWNERKEY");
            Assert.Contains(Drain(owner), m => m[0] == (byte)MessageType.CharacterList);
            // nwserver refuses the name under another key, whatever its case, and lets one key hold several names.
            Assert.False(Login(name, "OTHERKEY").IsConnected);
            Assert.False(Login(name.ToUpperInvariant(), "OTHERKEY").IsConnected);
            Assert.False(Login(name, "").IsConnected);
            Assert.True(Login(name, "OWNERKEY").IsConnected);
            Assert.True(Login(name + "b", "OWNERKEY").IsConnected);
            Assert.Contains("\"OWNERKEY\"  016          \"" + name + "b\"", File.ReadAllText(known));
        }
        finally
        {
            DeleteVault(world, name);
            DeleteVault(world, name + "b");
            if (File.Exists(known)) File.Delete(known);
        }
    }

    [DataFact]
    public void WithoutStickyNamesAnyKeyUsesTheName()
    {
        var (world, server) = Server(new ServerOptions { CharacterSelection = true, Vault = VaultMode.Server });
        var name = "Shared" + Guid.NewGuid().ToString("N")[..8];
        foreach (var key in new[] { "FIRSTKEY", "OTHERKEY" })
        {
            var (c, end) = LoopbackConnection.CreatePair();
            server.Accept(end);
            c.Send(new HelloMessage { PlayerName = name, PlayerKey = key }.Write());
            server.Tick(1f / 30);
            Assert.Contains(Drain(c), m => m[0] == (byte)MessageType.CharacterList);
        }
        DeleteVault(world, name);
    }

    [DataFact]
    public void VaultFoldersFollowTheKeyUnlessByPlayerName()
    {
        var (world, _) = Server(new ServerOptions { VaultByPlayerName = false });
        var player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Item2, PlayerName = "Alice", PublicKey = "FNVKYDQ6" };
        Assert.Equal("FNVKYDQ6", OpenWinter.Game.Persistence.ServerVault.Folder(world, player));
        Assert.Equal("Alice", OpenWinter.Game.Persistence.ServerVault.Folder(world, new PlayerSession { Connection = LoopbackConnection.CreatePair().Item2, PlayerName = "Alice" }));
        world.System<OpenWinter.Game.Persistence.PersistenceSystem>().VaultByPlayerName = true;
        Assert.Equal("Alice", OpenWinter.Game.Persistence.ServerVault.Folder(world, player));
    }
}
