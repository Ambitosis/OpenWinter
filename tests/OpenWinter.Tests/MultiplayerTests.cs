using System.Numerics;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class MultiplayerTests
{
    readonly ITestOutputHelper _out;
    public MultiplayerTests(ITestOutputHelper output) { _out = output; }

    sealed class TestClient
    {
        public required IConnection Connection;
        public uint Me;
        public readonly HashSet<uint> Known = new();
        public readonly Dictionary<uint, Vector3> Positions = new();
        public readonly List<string> Chat = new();

        public void Pump()
        {
            while (Connection.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                switch (r.Type)
                {
                    case MessageType.ControlObject: Me = ControlObjectMessage.Read(r).ObjectId; break;
                    case MessageType.LoadArea: Known.Clear(); break;
                    case MessageType.Spawn:
                    {
                        var s = SpawnMessage.Read(r);
                        Known.Add(s.ObjectId);
                        Positions[s.ObjectId] = s.Position;
                        break;
                    }
                    case MessageType.Update:
                    {
                        var u = UpdateMessage.Read(r);
                        Positions[u.ObjectId] = u.Position;
                        break;
                    }
                    case MessageType.Despawn: Known.Remove(DespawnMessage.Read(r).ObjectId); break;
                    case MessageType.Chat: Chat.Add(ChatMessage.Read(r).Text); break;
                }
            }
        }
    }

    static TestClient Join(GameServer server, string name)
    {
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = name }.Write());
        return new TestClient { Connection = client };
    }

    /// <summary>SetTlkOverride strings set before a player joins reach her as she enters (nwserver sends its list at login).</summary>
    [DataFact]
    public void AJoiningPlayerGetsTheTalkTableOverridesSetBefore()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        world.TlkOverrides[5213] = "Berserker";
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        var a = Join(server, "Alice");
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        var notices = new List<NotifyMessage>();
        while (a.Connection.TryReceive(out var m))
            if (new MessageReader(m) is { Type: MessageType.Notify } r) notices.Add(NotifyMessage.Read(r));
        Assert.Equal("""{"strref":5213,"text":"Berserker"}""", Assert.Single(notices, n => n.Kind == "tlk-override").Json);
    }

    /// <summary>Two players on one server: each sees the other arrive, move, talk and leave.</summary>
    [DataFact]
    public void PlayersSeeEachOtherMoveTalkAndLeave()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        var a = Join(server, "Alice");
        server.Tick(1f / 30);
        a.Pump();
        var b = Join(server, "Bob");
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        a.Pump(); b.Pump();
        Assert.NotEqual(0u, a.Me);
        Assert.NotEqual(0u, b.Me);
        Assert.Contains(b.Me, a.Known); // Alice was told about Bob arriving
        Assert.Contains(a.Me, b.Known); // Bob got Alice in his initial spawns

        // Bob walks; Alice sees him move.
        var start = a.Positions[b.Me];
        b.Connection.Send(new MoveInputMessage { Direction = new Vector3(1, 0, 0) }.Write());
        for (int i = 0; i < 45; i++) server.Tick(1f / 30);
        a.Pump(); b.Pump();
        _out.WriteLine($"Bob moved {Vector3.Distance(start, a.Positions[b.Me]):0.00} m as seen by Alice");
        Assert.True(Vector3.Distance(start, a.Positions[b.Me]) > 1f);

        // Talking reaches the other player.
        a.Connection.Send(new ChatSendMessage { Text = "hello Bob" }.Write());
        server.Tick(1f / 30);
        b.Pump();
        Assert.Contains(b.Chat, t => t.Contains("hello Bob"));

        // Bob leaves; Alice's client removes him.
        ((LoopbackConnection)b.Connection).Disconnect();
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        a.Pump();
        Assert.DoesNotContain(b.Me, a.Known);
        Assert.Single(world.AllObjects.OfType<Creature>().Where(c => c.IsPC));
    }
}

public class ContainerTests
{
    readonly Xunit.Abstractions.ITestOutputHelper _out;
    public ContainerTests(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }

    /// <summary>Using a chest opens it and shows its contents; taking an item moves it to the player; walking away closes it.</summary>
    [DataFact]
    public void LootAContainer()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        world.Step(0.1f);
        var chest = world.AllObjects.OfType<Placeable>().First(p => p.HasInventory && !p.Locked && p.Area != null && world.Inventory.Items(p).Count > 0);
        _out.WriteLine($"chest {chest.Tag} in {chest.Area!.ResRef} with {world.Inventory.Items(chest).Count} items");
        var server = new GameServer(world, new ServerOptions { StartArea = chest.Area.ResRef });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Looter" }.Write());
        server.Tick(1f / 30);
        var pc = world.AllObjects.OfType<Creature>().Single(c => c.IsPC);
        world.Jump(pc, new Location(chest.Area.Id, chest.Position + new Vector3(1, 0, 0), 0));
        server.Tick(1f / 30);
        while (client.TryReceive(out _)) { }

        client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = chest.Id }.Write());
        uint itemId = 0;
        // Furniture can block a container's close use point; it must not cause a multi-second freeze.
        for (int i = 0; i < 90 && itemId == 0; i++)
        {
            server.Tick(1f / 30);
            while (client.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                if (r.Type != MessageType.Notify) continue;
                var n = NotifyMessage.Read(r);
                if (n.Kind != "container-open") continue;
                var items = System.Text.Json.JsonDocument.Parse(n.Json).RootElement.GetProperty("items");
                itemId = items[0].GetProperty("id").GetUInt32();
            }
        }
        Assert.NotEqual(0u, itemId);
        Assert.True(chest.IsOpen);

        client.Send(new ClientRequestMessage { Kind = "container-take", Json = $"{{\"item\":{itemId}}}" }.Write());
        server.Tick(1f / 30);
        Assert.Equal(pc, world.Inventory.Owner(world.Get<Item>(itemId)!));

        // Walking away closes it.
        world.Jump(pc, new Location(chest.Area.Id, chest.Position + new Vector3(8, 0, 0), 0));
        for (int i = 0; i < 5; i++) server.Tick(1f / 30);
        Assert.False(chest.IsOpen);
    }
}

public class CorpseTests
{
    [DataFact]
    public void DeadCreatureDecaysAndLeavesDroppableItemsInABodyBag()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        world.Step(0.1f);
        var npc = world.AllObjects.OfType<Creature>().First(c => !c.IsPC && c.Area != null && !c.Plot && !c.IsImmortal && c.IsDestroyable);
        var potion = world.Inventory.CreateOn("nw_it_mpotion001", npc, 1, "")!;
        potion.Droppable = true;
        var keep = world.Inventory.CreateOn("nw_it_mpotion002", npc, 1, "")!; // a different potion, so it does not stack with the first
        keep.Droppable = false;
        npc.DecayTime = 2000; // lasts the five-second minimum
        OpenWinter.Game.Rules.HitPoints.Kill(world, npc, OpenWinter.Game.Objects.GameObject.InvalidId);
        for (int i = 0; i < 16; i++) world.Step(0.25f);
        Assert.False(npc.IsDestroyed);
        for (int i = 0; i < 10; i++) world.Step(0.25f);
        Assert.True(npc.IsDestroyed);
        var bag = Assert.Single(world.ByTag(OpenWinter.Game.Rules.CorpseSystem.BodyBagTag).OfType<Placeable>());
        Assert.Contains(potion, world.Inventory.Items(bag));
        Assert.DoesNotContain(keep, world.Inventory.Items(bag));
        Assert.True(bag.HasInventory && bag.Useable);
    }
}

public class PvpTests
{
    [DataFact]
    public void PvpSettingDecidesWhetherPlayersCanFight()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        foreach (var name in new[] { "A", "B" })
        {
            var (client, serverEnd) = LoopbackConnection.CreatePair();
            server.Accept(serverEnd);
            client.Send(new HelloMessage { PlayerName = name }.Write());
        }
        server.Tick(1f / 30);
        var pcs = world.AllObjects.OfType<Creature>().Where(c => c.IsPC).ToList();
        var combat = world.System<OpenWinter.Game.Combat.CombatSystem>();

        world.Pvp = PvpSetting.None;
        combat.Attack(pcs[0], pcs[1]);
        Assert.False(combat.IsInCombat(pcs[0]));

        world.Pvp = PvpSetting.Party; // different parties: allowed
        combat.Attack(pcs[0], pcs[1]);
        Assert.True(combat.IsInCombat(pcs[0]));
        combat.Stop(pcs[0]);

        world.System<OpenWinter.Game.Society.PartyService>().Join(pcs[1], pcs[0]); // same party: not allowed
        combat.Attack(pcs[0], pcs[1]);
        Assert.False(combat.IsInCombat(pcs[0]));

        world.Pvp = PvpSetting.Full;
        combat.Attack(pcs[0], pcs[1]);
        Assert.True(combat.IsInCombat(pcs[0]));
    }
}
