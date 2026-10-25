using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Client requests act only on what the player may act on, and only when the server is waiting for them.</summary>
public sealed class RequestAuthorizationTests
{
    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection ClientA, ClientB;
        public readonly Creature A, B;

        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "mp", Width = 8, Height = 8 };
            World.Register(area); World.Areas.Add(area);
            area.Walkmesh.Add(new(0, 0, 0), new(80, 0, 0), new(80, 80, 0), 4, true);
            area.Walkmesh.Add(new(0, 0, 0), new(80, 80, 0), new(0, 80, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = "mp", Vault = VaultMode.Local });
            (ClientA, A) = Join("Player A");
            (ClientB, B) = Join("Player B");
            A.Position = new Vector3(10, 10, 0);
            B.Position = new Vector3(12, 10, 0);
            Tick();
        }

        (LoopbackConnection, Creature) Join(string name)
        {
            var pair = LoopbackConnection.CreatePair();
            Server.Accept(pair.Server);
            pair.Client.Send(new HelloMessage { PlayerName = name }.Write());
            Server.Tick(.01f);
            return (pair.Client, Server.Sessions.Single(s => s.PlayerName == name).Creature!);
        }

        public void Tick(int n = 1) { for (int i = 0; i < n; i++) Server.Tick(1f / 30); }

        public void Send(LoopbackConnection c, string kind, object args)
        {
            c.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(args) }.Write());
            Tick();
            while (c.TryReceive(out _)) { }
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void ConnectionsThatNeverLogInAreDropped()
    {
        using var f = new Fixture();
        var (idle, end) = LoopbackConnection.CreatePair();
        f.Server.Accept(end);
        for (int i = 0; i < 25; i++) f.Server.Tick(1);
        Assert.True(idle.IsConnected);
        for (int i = 0; i < 10; i++) f.Server.Tick(1);
        Assert.False(idle.IsConnected);
        Assert.True(f.ClientA.IsConnected);
    }

    [DataFact]
    public void TargetPicksCountOnlyAfterEnterTargetingMode()
    {
        using var f = new Fixture();
        f.Send(f.ClientA, "target", new { @object = f.B.Id, position = new[] { 1f, 2f, 0f } });
        Assert.Null(f.World.LastTarget);
        f.A.AwaitingTarget = true;
        f.Send(f.ClientA, "target", new { @object = f.B.Id, position = new[] { 1f, 2f, 0f } });
        Assert.Equal(f.B.Id, f.World.LastTarget?.Target);
        // One pick per EnterTargetingMode.
        f.Send(f.ClientA, "target", new { @object = f.A.Id, position = new[] { 1f, 2f, 0f } });
        Assert.Equal(f.B.Id, f.World.LastTarget?.Target);
    }
}
