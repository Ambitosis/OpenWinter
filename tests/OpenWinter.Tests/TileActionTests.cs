using System.Numerics;
using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class TileActionTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            var memory = new MemorySource("authored tile action test");
            memory.Set(ResKey.Of("surfacemat", ResType.TwoDA), Encoding.UTF8.GetBytes("2DA V2.0\n\nLabel Act1_Strref Act1_Icon Act2_Strref Act2_Icon Act3_Strref Act3_Icon Act8_Strref Act8_Icon\n3 Grass 1391 ir_use 1394 **** **** ir_cast 1393 ir_attack\n"));
            resources.Add(memory); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            Area = new Area { ResRef = "tiletest", Width = 4, Height = 4 }; World.Register(Area); World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0,0,0), new(40,0,0), new(40,40,0), 3, true);
            Area.Walkmesh.Add(new(0,0,0), new(40,40,0), new(0,40,0), 3, true);
            const string source = "void main(){object p=GetLastPlayerToDoTileAction();SetLocalInt(OBJECT_SELF,\"id\",GetLastTileActionId());SetLocalObject(OBJECT_SELF,\"player\",p);SetLocalLocation(OBJECT_SELF,\"point\",Location(GetArea(p),GetLastTileActionPosition(),0.0));SetLocalInt(OBJECT_SELF,\"event\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"count\",GetLocalInt(OBJECT_SELF,\"count\")+1);}";
            var bytes = NssCompiler.Compile(source, "tile_test", World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error);
            World.Scripts.Register("tile_test", new NcsProgram("tile_test", bytes!));
            World.Module.Scripts["Mod_OnPlrTileAct"] = "tile_test";
            Server = new GameServer(World, new ServerOptions { StartArea = Area.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Tile tester" }.Write()); Server.Tick(1f/30);
            Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(Area.Id, new(20,15,0), 0)); Drain();
        }
        public void Request(string kind, object data)
        {
            Client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write());
            Server.Tick(1f/30);
        }
        public List<JsonElement> Drain()
        {
            var result = new List<JsonElement>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type != MessageType.Notify) continue;
                var message = NotifyMessage.Read(reader);
                if (message.Kind != "tile-actions") continue;
                Assert.Equal(Pc.Id, message.Subject);
                result.Add(JsonDocument.Parse(message.Json).RootElement.Clone());
            }
            return result;
        }
    }

    [DataFact]
    public void AuthenticatedActionsPreserveSlotsPointPlayerAndExistingQueue()
    {
        var g = new Game(); var point = new Vector3(22,17,0);
        g.Request("tile-actions", new { area = g.Area.Id, position = new[]{point.X,point.Y,point.Z}, query = 42 });
        var payload = Assert.Single(g.Drain());
        Assert.Equal(42, payload.GetProperty("query").GetInt32());
        var entries = payload.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(new[]{1,2,8}, entries.Select(e => e.GetProperty("id").GetInt32()));
        Assert.Equal("", entries[1].GetProperty("icon").GetString());
        Assert.Equal("Use #1", entries[0].GetProperty("name").GetString());
        var move = new MoveAction { Target = new(35,15,0), Run = false }; g.Pc.Actions.Add(move);
        foreach (int id in new[]{1,2,8})
        {
            g.Request("tile-action", new { id, area = g.Area.Id, position = new[]{point.X,point.Y,point.Z}, player = g.World.Module.Id });
            Assert.Equal(id, g.World.Module.Locals.Ints["id"]);
            Assert.Equal(g.Pc.Id, g.World.Module.Locals.Objects["player"]);
            Assert.Equal(point, g.World.Module.Locals.Locations["point"].Position);
            Assert.Equal(3020, g.World.Module.Locals.Ints["event"]);
            Assert.Contains(move, g.Pc.Actions);
        }
        Assert.Equal(3, g.World.Module.Locals.Ints["count"]);
    }

    [DataFact]
    public void RejectsForeignAreaUndefinedActionsAndInvalidPositions()
    {
        var g = new Game();
        foreach (int id in new[]{0,3,4,9})
            g.Request("tile-action", new { id, area = g.Area.Id, position = new[]{22,17,0} });
        g.Request("tile-action", new { id = 1, area = g.Area.Id + 100, position = new[]{22,17,0} });
        g.Request("tile-action", new { id = 1, area = g.Area.Id, position = new[]{400,170,0} });
        g.Request("tile-action", new { id = 1, area = g.Area.Id, position = new[]{22,17,100} });
        g.Request("tile-action", new { id = 1, area = g.Area.Id });
        g.Request("tile-action", new { id = 1, area = g.Area.Id, position = new[]{22,17} });
        Assert.False(g.World.Module.Locals.Ints.ContainsKey("count"));
        g.Request("tile-actions", new { area = g.Area.Id+100, position = new[]{22,17,0}, query = 4 });
        Assert.Empty(Assert.Single(g.Drain()).GetProperty("entries").EnumerateArray());
    }
}
