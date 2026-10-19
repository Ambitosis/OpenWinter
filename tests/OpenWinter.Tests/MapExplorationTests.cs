using System.Numerics;
using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class MapExplorationTests
{
    sealed class Output : IWorldOutput
    {
        public readonly List<(string Kind, uint Player, JsonElement Data)> Notes = [];
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, player?.Id ?? GameObject.InvalidId, JsonSerializer.SerializeToElement(data)));
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Creature Pc, Other;
        public readonly Output Output = new();
        public Game(bool shiftIds = false)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources) { Output = Output };
            World.Register(World.Module);
            if (shiftIds) World.Register(new Waypoint());
            World.EnableScripts();
            Area = new Area { ResRef = "maptest", Width = 4, Height = 4 };
            World.Register(Area); World.Areas.Add(Area);
            Pc = Player("first"); Other = Player("second");
        }
        Creature Player(string name)
        {
            var (_, connection) = LoopbackConnection.CreatePair();
            var pc = new Creature { Player = new PlayerSession { PlayerName = name, Connection = connection } };
            World.AddToArea(pc, Area, new Vector3(15, 15, 0), 0);
            return pc;
        }
        public object? Call(string name, params object?[] args)
        {
            var host = World.Scripts!;
            var program = new NcsProgram("t", Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0").Concat(new byte[] { 0x20, 0 }).ToArray());
            return ((IScriptEngine)host).Call(new ScriptThread(host.VM, program, Pc.Id), host.Definitions.ByName[name], args);
        }
        public void Request(Creature pc, string kind, object data) =>
            Assert.True(ClientRequests.Handle(World, pc, kind, JsonSerializer.Serialize(data)));
        public IReadOnlyList<PersonalMapPin> Pins(Creature pc) => PersonalMapPins.Read(World, Area, pc);
    }

    [DataFact]
    public void DisabledAutomaticExplorationKeepsManualScriptStateAndPlayerIsolation()
    {
        var g = new Game();
        Assert.Equal(1, g.Call("SetCreatureExploresMinimap", g.Pc.Id, 0));
        Assert.Equal(0, g.Call("GetCreatureExploresMinimap", g.Pc.Id));
        g.Call("ExploreAreaForPlayer", g.Area.Id, g.Pc.Id, 0);
        Assert.Equal(0, g.Call("SetTileExplored", g.Pc.Id, g.Area.Id, 3, 3, 1));
        g.Output.Notes.Clear();
        g.World.System<MapExploration>().Step(.5f);
        Assert.Equal(0, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 1, 1));
        Assert.Equal(1, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 3, 3));
        Assert.DoesNotContain(g.Output.Notes, n => n.Kind == "tile-explored" && n.Player == g.Pc.Id);
        Assert.Contains(g.Output.Notes, n => n.Kind == "tile-explored" && n.Player == g.Other.Id);
        Assert.Equal(0, g.Call("SetCreatureExploresMinimap", g.Pc.Id, 1));
        g.World.System<MapExploration>().Step(.5f);
        Assert.Equal(1, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 1, 1));
        Assert.Equal(-1, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 4, 0));
        Assert.Equal(-1, g.Call("SetTileExplored", g.Pc.Id, g.Area.Id, -1, 0, 1));
        g.Call("ExploreAreaForPlayer", g.Area.Id, g.Pc.Id, 1);
        Assert.All(AreaSettings.Of(g.World, g.Area).Explored[g.Pc.Id], b => Assert.True(b));
    }

    [DataFact]
    public void ResettingExplorationRefillsWhatThePlayerSeesOnlyWhileExploring()
    {
        var g = new Game();
        g.World.System<MapExploration>().Step(.5f);
        g.Call("ExploreAreaForPlayer", g.Area.Id, g.Pc.Id, 0);
        Assert.Equal(1, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 1, 1)); // no pass needed
        g.Call("SetCreatureExploresMinimap", g.Pc.Id, 0);
        g.Call("ExploreAreaForPlayer", g.Area.Id, g.Pc.Id, 0);
        Assert.Equal(0, g.Call("GetTileExplored", g.Pc.Id, g.Area.Id, 1, 1));
    }

    [DataFact]
    public void PersonalPinRequestsRejectStaleAreasBoundsAndOtherPlayersIds()
    {
        var g = new Game();
        var waypoint = new Waypoint { HasMapNote = true, MapNote = new LocString("Module pin") };
        g.World.AddToArea(waypoint, g.Area, new Vector3(5, 6, 0), 0);
        foreach (var data in new object[]
        {
            new { area = g.Area.Id + 100, x = 5, y = 6, note = "stale" },
            new { area = g.Area.Id, x = -1, y = 6, note = "negative" },
            new { area = g.Area.Id, x = 40, y = 6, note = "outside" },
            new { area = g.Area.Id, y = 6, note = "missing" },
        }) g.Request(g.Pc, "map-pin-add", data);
        Assert.Empty(g.Pins(g.Pc));
        g.Request(g.Pc, "map-pin-add", new { area = g.Area.Id, x = 5, y = 6, note = "Mine" });
        string id = Assert.Single(g.Pins(g.Pc)).Id;
        g.Request(g.Other, "map-pin-add", new { area = g.Area.Id, x = 7, y = 8, note = "Theirs" });
        g.Request(g.Other, "map-pin-edit", new { area = g.Area.Id, id, note = "Stolen" });
        g.Request(g.Other, "map-pin-remove", new { area = g.Area.Id, id });
        g.Request(g.Pc, "map-pin-remove", new { area = g.Area.Id + 100, id });
        Assert.Equal("Mine", Assert.Single(g.Pins(g.Pc)).Note);
        g.Request(g.Pc, "map-pin-edit", new { area = g.Area.Id, id, note = "Changed" });
        Assert.Equal("Changed", Assert.Single(g.Pins(g.Pc)).Note);
        g.Output.Notes.Clear();
        g.Request(g.Other, "area-map", new { });
        var notes = Assert.Single(g.Output.Notes).Data.GetProperty("pins").EnumerateArray().Select(p => p.GetProperty("note").GetString()).ToList();
        Assert.Contains("Module pin", notes); Assert.Contains("Theirs", notes); Assert.DoesNotContain("Changed", notes);
        g.Request(g.Pc, "map-pin-remove", new { area = g.Area.Id, id });
        Assert.Empty(g.Pins(g.Pc));
        Assert.True(waypoint.HasMapNote && waypoint.MapNoteEnabled);
        g.Call("SetMapPinEnabled", waypoint.Id, 0);
        Assert.False(waypoint.MapNoteEnabled);
        g.Output.Notes.Clear(); g.Request(g.Pc, "area-map", new { });
        Assert.Empty(Assert.Single(g.Output.Notes).Data.GetProperty("pins").EnumerateArray());
    }

    [DataFact]
    public void SnapshotRetainsExplorationFlagsPinsAndSavedPlayerIds()
    {
        var g = new Game();
        g.Call("SetCreatureExploresMinimap", g.Pc.Id, 0);
        g.Call("SetTileExplored", g.Pc.Id, g.Area.Id, 2, 3, 1);
        g.Call("SetTileExplored", g.Other.Id, g.Area.Id, 1, 0, 1);
        g.Request(g.Pc, "map-pin-add", new { area = g.Area.Id, x = 4, y = 8, note = "First note" });
        g.Request(g.Other, "map-pin-add", new { area = g.Area.Id, x = 9, y = 7, note = "Second note" });
        var waypoint = new Waypoint { HasMapNote = true, MapNote = new LocString("Hidden module pin"), MapNoteEnabled = false };
        g.World.AddToArea(waypoint, g.Area, new Vector3(6, 6, 0), 0);
        var snapshot = WorldSnapshot.Capture(g.World, out var warnings);
        Assert.Empty(warnings);
        var fresh = new Game(shiftIds: true);
        Assert.NotEqual(g.Pc.Id, fresh.Pc.Id);
        var restored = WorldSnapshot.Apply(fresh.World, snapshot);
        Assert.Empty(restored.Warnings);
        var first = restored.Players.Single(p => p.Player == "first").Creature;
        var second = restored.Players.Single(p => p.Player == "second").Creature;
        first.Player = g.Pc.Player; second.Player = g.Other.Player;
        Assert.Equal(g.Pc.Id, first.Id); Assert.Equal(g.Other.Id, second.Id);
        Assert.False(first.ExploresMinimap);
        var area = first.Area!;
        var env = AreaSettings.Of(fresh.World, area);
        Assert.True(env.Explored[first.Id][14]); Assert.False(env.Explored[first.Id][1]);
        Assert.True(env.Explored[second.Id][1]); Assert.False(env.Explored[second.Id][14]);
        Assert.Equal("First note", Assert.Single(PersonalMapPins.Read(fresh.World, area, first)).Note);
        Assert.Equal("Second note", Assert.Single(PersonalMapPins.Read(fresh.World, area, second)).Note);
        Assert.False(fresh.World.Get<Waypoint>(waypoint.Id)!.MapNoteEnabled);
        MapExploration.Send(fresh.World, first);
        var payload = fresh.Output.Notes.Last(n => n.Kind == "area-map").Data;
        Assert.Equal("First note", Assert.Single(payload.GetProperty("pins").EnumerateArray()).GetProperty("note").GetString());
    }

    [DataFact]
    public void AreaCopyDoesNotSharePlayerExplorationOrPersonalPins()
    {
        var g = new Game();
        g.Request(g.Pc, "map-pin-add", new { area = g.Area.Id, x = 5, y = 6, note = "Original" });
        g.Call("SetTileExplored", g.Pc.Id, g.Area.Id, 1, 1, 1);
        var copy = AreaSettings.Of(g.World, g.Area).Clone();
        Assert.Empty(copy.Explored); Assert.Empty(copy.PersonalMapPins);
        copy.PersonalMapPins[g.Pc.Id] = [];
        Assert.Equal("Original", Assert.Single(g.Pins(g.Pc)).Note);
    }
}
