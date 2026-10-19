using System.Numerics;
using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class PlayerNoticeTransitionTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client, OtherClient;
        public readonly Creature Pc, OtherPc;
        public Game()
        {
            World = new World(TestData.Module); World.LoadModule();
            Server = new GameServer(World, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
            (Client, Pc) = Login("First");
            (OtherClient, OtherPc) = Login("Second");
            Drain(Client); Drain(OtherClient);
        }
        (IConnection, Creature) Login(string name)
        {
            var pair = LoopbackConnection.CreatePair();
            Server.Accept(pair.Server);
            pair.Client.Send(new HelloMessage { PlayerName = name }.Write());
            Server.Tick(1f / 30);
            var controlled = Drain(pair.Client).Select(bytes => new MessageReader(bytes))
                .Where(r => r.Type == MessageType.ControlObject).Select(r => ControlObjectMessage.Read(r).ObjectId).Last();
            return (pair.Client, World.Get<Creature>(controlled)!);
        }
        public Placeable Chest()
        {
            var chest = new Placeable { HasInventory = true, Useable = true };
            World.AddToArea(chest, Pc.Area!, Pc.Position + new Vector3(1, 0, 0), 0, exact: true);
            return chest;
        }
        public void Leave()
        {
            var next = World.Areas.First(a => a != Pc.Area);
            Server.EnterArea(Pc.Player!, next, new Vector3(10, 10, 0), 0);
            Drain(Client); Drain(OtherClient);
        }
    }
    static List<byte[]> Drain(IConnection connection)
    {
        var list = new List<byte[]>();
        while (connection.TryReceive(out var bytes)) list.Add(bytes);
        return list;
    }
    static List<NotifyMessage> Notices(IEnumerable<byte[]> messages) => messages
        .Select(bytes => new MessageReader(bytes)).Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).ToList();

    [DataFact]
    public void OpenContainerClosesAfterPlayerAreaChangesWithoutRespawningTheOldContainer()
    {
        var g = new Game(); var chest = g.Chest();
        var containers = g.World.System<ContainerService>();
        containers.Show(g.Pc, chest);
        Assert.Contains(Notices(Drain(g.Client)), n => n.Kind == "container-open" && n.Subject == chest.Id);
        Assert.Same(chest, containers.OpenFor(g.Pc));
        g.Leave();
        containers.Step(0);
        Assert.Null(containers.OpenFor(g.Pc));
        var messages = Drain(g.Client);
        var close = Assert.Single(Notices(messages), n => n.Kind == "container-close");
        Assert.Equal(chest.Id, close.Subject);
        Assert.Equal(chest.Id, JsonSerializer.Deserialize<JsonElement>(close.Json).GetProperty("container").GetUInt32());
        Assert.DoesNotContain(chest.Id, g.Pc.Player!.Known);
        Assert.DoesNotContain(messages, bytes => new MessageReader(bytes).Type == MessageType.Spawn);
        Assert.DoesNotContain(Notices(Drain(g.OtherClient)), n => n.Kind == "container-close");
    }

    [DataFact]
    public void ExplicitRemoteExamineAndIdentificationRemainPrivateWithoutSpawningRemoteObjects()
    {
        var g = new Game(); var chest = g.Chest();
        var item = g.World.Items.FromBlueprint("nw_wswls001")!;
        g.World.AddToArea(item, g.Pc.Area!, g.Pc.Position, 0, exact: true);
        g.Leave();
        Examine.Send(g.World, g.Pc, chest);
        g.Server.Notify("item-identified", item, new { item = item.Id }, g.Pc);
        var messages = Drain(g.Client);
        var notices = Notices(messages);
        Assert.Contains(notices, n => n.Kind == "examine" && n.Subject == chest.Id);
        Assert.Contains(notices, n => n.Kind == "item-identified" && n.Subject == item.Id);
        Assert.DoesNotContain(messages, bytes => new MessageReader(bytes).Type == MessageType.Spawn);
        Assert.DoesNotContain(chest.Id, g.Pc.Player!.Known);
        Assert.DoesNotContain(item.Id, g.Pc.Player.Known);
        Assert.Empty(Notices(Drain(g.OtherClient)));
        // Client requests still enforce ownership/area access independently of delivery.
        Assert.True(ClientRequests.Handle(g.World, g.Pc, "examine", JsonSerializer.Serialize(new { @object = chest.Id })));
        Assert.Empty(Notices(Drain(g.Client)));
        // Addressing one player does not make remote world visuals visible.
        g.Server.Notify("appear", chest, new { animation = 1 }, g.Pc);
        g.Server.Notify("vfx", chest, new { id = 1, vfx = 9 }, g.Pc);
        Assert.Empty(Drain(g.Client));
        var (_, hiddenConnection) = LoopbackConnection.CreatePair();
        var hidden = new Creature { DMOwner = new PlayerSession { Connection = hiddenConnection }, DMHidden = true };
        g.World.AddToArea(hidden, g.Pc.Area!, g.Pc.Position, 0, exact: true);
        Assert.True(ClientRequests.Handle(g.World, g.Pc, "examine", JsonSerializer.Serialize(new { @object = hidden.Id })));
        Assert.Empty(Drain(g.Client));
        Assert.DoesNotContain(hidden.Id, g.Pc.Player.Known);
    }

    [DataFact]
    public void AreaBroadcastsStayLocalAndAnAddressedLocalNoticeStillSpawnsIdentityFirst()
    {
        var g = new Game(); var chest = g.Chest();
        g.Leave();
        g.Server.Notify("test-area-event", chest, new { value = 1 });
        Assert.DoesNotContain(Notices(Drain(g.Client)), n => n.Kind == "test-area-event");
        Assert.Contains(Notices(Drain(g.OtherClient)), n => n.Kind == "test-area-event");
        var local = new Placeable { HasInventory = true };
        g.World.AddToArea(local, g.Pc.Area!, g.Pc.Position, 0, exact: true);
        Assert.DoesNotContain(local.Id, g.Pc.Player!.Known);
        g.Server.Notify("container-open", local, new { container = local.Id }, g.Pc);
        var messages = Drain(g.Client);
        int spawn = messages.FindIndex(bytes => new MessageReader(bytes).Type == MessageType.Spawn);
        int notice = messages.FindIndex(bytes =>
        {
            var reader = new MessageReader(bytes);
            return reader.Type == MessageType.Notify && NotifyMessage.Read(reader).Kind == "container-open";
        });
        Assert.True(spawn >= 0 && notice > spawn);
        Assert.Contains(local.Id, g.Pc.Player.Known);
        Assert.DoesNotContain(Notices(Drain(g.OtherClient)), n => n.Kind == "container-open");
    }
}
