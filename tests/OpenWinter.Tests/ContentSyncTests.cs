using OpenWinter.Game;
using OpenWinter.Game.Distribution;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class ContentSyncTests
{
    /// <summary>A client missing a hak and a TLK downloads both in chunks, verifies them, and finds them cached next time.</summary>
    [Fact]
    public void MissingFilesDownloadVerifyAndCache()
    {
        var root = Path.Combine(Path.GetTempPath(), "openwinter-contentsync-" + Guid.NewGuid().ToString("N"));
        var serverDir = Directory.CreateDirectory(Path.Combine(root, "server")).FullName;
        var rng = new Random(5);
        var hak = new byte[300_000]; rng.NextBytes(hak);
        var tlk = new byte[70_000]; rng.NextBytes(tlk);
        File.WriteAllBytes(Path.Combine(serverDir, "big.hak"), hak);
        File.WriteAllBytes(Path.Combine(serverDir, "custom.tlk"), tlk);
        var server = new ContentServer([(ContentKind.Hak, "big", Path.Combine(serverDir, "big.hak")), (ContentKind.Tlk, "custom", Path.Combine(serverDir, "custom.tlk"))]);

        var (client, serverEnd) = LoopbackConnection.CreatePair();
        var session = new PlayerSession { Connection = serverEnd };
        var userHaks = Directory.CreateDirectory(Path.Combine(root, "user", "hak")).FullName;
        var cache = Path.Combine(root, "cache");
        var sync = new ContentSync([userHaks], [userHaks], cache);
        sync.Plan(server.Files);
        Assert.Equal(2, sync.Missing.Count);

        foreach (var f in sync.Missing.ToList()) server.Request(session, f.Sha1);
        for (int tick = 0; tick < 100 && !sync.Done; tick++)
        {
            server.Pump();
            while (client.TryReceive(out var m)) sync.Accept(ContentChunkMessage.Read(new MessageReader(m)));
        }
        Assert.True(sync.Done, sync.Error);
        Assert.Equal(hak, File.ReadAllBytes(sync.Resolved["hak:big"]));
        Assert.Equal(tlk, File.ReadAllBytes(sync.Resolved["tlk:custom"]));

        // Next login: everything is found in the cache, nothing to fetch.
        var again = new ContentSync([userHaks], [userHaks], cache);
        again.Plan(server.Files);
        Assert.True(again.Done);
        Directory.Delete(root, true);
    }

    [Fact]
    public void UnknownHashesAreNotServed()
    {
        var server = new ContentServer(Array.Empty<(ContentKind, string, string)>());
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Request(new PlayerSession { Connection = serverEnd }, "deadbeef");
        server.Pump();
        Assert.False(client.TryReceive(out _));
    }
}
