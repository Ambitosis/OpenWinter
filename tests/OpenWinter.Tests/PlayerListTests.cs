using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The pause key (single player only) and the player list's like / dislike (PvP) buttons.</summary>
public class PlayerListTests
{
    sealed class TestClient
    {
        public required LoopbackConnection Connection;
        public uint Me;
        public readonly List<NotifyMessage> Notifies = new();

        public void Pump()
        {
            while (Connection.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                if (r.Type == MessageType.ControlObject) Me = ControlObjectMessage.Read(r).ObjectId;
                else if (r.Type == MessageType.Notify) Notifies.Add(NotifyMessage.Read(r));
            }
        }

        public void Request(string kind, object? data = null) =>
            Connection.Send(new ClientRequestMessage { Kind = kind, Json = data == null ? "" : JsonSerializer.Serialize(data) }.Write());

        public JsonElement Last(string kind) => JsonDocument.Parse(Notifies.Last(n => n.Kind == kind).Json).RootElement;
    }

    static TestClient Join(GameServer server, string name)
    {
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = name }.Write());
        return new TestClient { Connection = client };
    }

    static void Tick(GameServer server, params TestClient[] clients)
    {
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        foreach (var c in clients) c.Pump();
    }

    static (World, GameServer) MakeServer(bool singlePlayer)
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        return (world, new GameServer(world, new ServerOptions { StartArea = "ss_town", SaveGames = singlePlayer }));
    }

    [DataFact]
    public void PauseTogglesOnlyInSinglePlayer()
    {
        var (world, server) = MakeServer(singlePlayer: true);
        var a = Join(server, "Alice");
        Tick(server, a);
        a.Request("pause");
        Tick(server, a);
        Assert.Equal(2, world.PauseState);
        Assert.Equal(2, a.Last("pause").GetProperty("state").GetInt32());
        a.Request("pause");
        Tick(server, a);
        Assert.Equal(0, world.PauseState);

        var (mpWorld, mp) = MakeServer(singlePlayer: false);
        var b = Join(mp, "Bob");
        Tick(mp, b);
        b.Request("pause");
        Tick(mp, b);
        Assert.Equal(0, mpWorld.PauseState);
    }

    [DataFact]
    public void AMultiplayerGamePausesForADungeonMasterOrWhenTheServerAllowsPlayers()
    {
        // server.allow-player-pausing (PauseAndPlay) lets players pause; Dungeon Masters always can.
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", AllowPlayerPausing = true });
        var a = Join(server, "Alice");
        Tick(server, a);
        a.Request("pause");
        Tick(server, a);
        Assert.Equal(2, world.PauseState);

        var dmWorld = new World(TestData.Module);
        dmWorld.LoadModule();
        var dmServer = new GameServer(dmWorld, new ServerOptions { StartArea = "ss_town", DMPassword = "" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        dmServer.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Dungeon Master", DM = true, DMPassword = "" }.Write());
        var dm = new TestClient { Connection = client };
        Tick(dmServer, dm);
        dm.Request("pause");
        Tick(dmServer, dm);
        Assert.Equal(2, dmWorld.PauseState);
    }

    [DataFact]
    public void ScriptPauseStopsTimersAndActionsButStillAcceptsClientUnpause()
    {
        var (world, server) = MakeServer(singlePlayer: true);
        var client = Join(server, "PauseTest");
        Tick(server, client);
        var actor = world.Get<Creature>(client.Me)!;
        world.EnableScripts();
        const string script = "void main() { SetGameActivePause(TRUE); SetGameActivePause(TRUE); }";
        var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(script, "pause_probe",
            world.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        world.Scripts.Register("pause_probe", new OpenWinter.Game.Scripting.NcsProgram("pause_probe", bytes!));
        actor.Actions.Clear();
        var wait = new WaitAction { Remaining = 1 };
        actor.Actions.Add(wait);
        bool delayed = false;
        world.After(0.5, () => delayed = true);
        world.Scripts.Run("pause_probe", actor.Id);
        client.Pump();
        double pausedAt = world.Time, gameAt = world.GameTime;
        for (int i = 0; i < 120; i++) server.Tick(1f / 30);
        Assert.Equal(2, world.PauseState);
        Assert.Equal(pausedAt, world.Time);
        Assert.Equal(gameAt, world.GameTime);
        // Four paused seconds would have finished the one-second wait; it is still queued.
        Assert.Contains(wait, actor.Actions);
        Assert.False(delayed);
        Assert.Single(client.Notifies, n => n.Kind == "pause");
        // Input is processed even while simulation is frozen, so the player can resume.
        client.Request("pause");
        Tick(server, client);
        Assert.Equal(0, world.PauseState);
        Assert.True(world.Time > pausedAt);
        for (int i = 0; i < 60; i++) server.Tick(1f / 30);
        Assert.True(delayed);
        Assert.DoesNotContain(wait, actor.Actions);
    }

    [DataFact]
    public void PlayersListEachOtherAndDislikeBothWays()
    {
        var (world, server) = MakeServer(singlePlayer: false);
        var a = Join(server, "Alice");
        Tick(server, a);
        var b = Join(server, "Bob");
        Tick(server, a, b);
        a.Request("player-list");
        Tick(server, a, b);
        // The other players only (the original's list is empty with nobody else on the server; docs/PARTY.md).
        var list = a.Last("player-list").GetProperty("players").EnumerateArray().ToList();
        var bobRow = Assert.Single(list);
        Assert.Equal(b.Me, bobRow.GetProperty("id").GetUInt32());
        Assert.False(bobRow.GetProperty("dislike").GetBoolean());

        a.Request("pc-like", new { other = b.Me, like = false });
        Tick(server, a, b);
        var alice = world.Get<Creature>(a.Me)!;
        var bob = world.Get<Creature>(b.Me)!;
        Assert.Contains(bob.Id, alice.Dislikes);
        Assert.Contains(alice.Id, bob.Dislikes);
        var after = a.Last("player-list").GetProperty("players").EnumerateArray().Single(p => p.GetProperty("id").GetUInt32() == b.Me);
        Assert.True(after.GetProperty("dislike").GetBoolean());
        Assert.True(after.GetProperty("dislikedBy").GetBoolean());
        Assert.False(b.Last("pc-like").GetProperty("like").GetBoolean());

        a.Request("pc-like", new { other = b.Me, like = true });
        Tick(server, a, b);
        Assert.Empty(alice.Dislikes);
        Assert.Empty(bob.Dislikes);
        // Yourself is not on the list and cannot be disliked.
        a.Request("pc-like", new { other = a.Me, like = false });
        Tick(server, a, b);
        Assert.Empty(alice.Dislikes);
    }
}
