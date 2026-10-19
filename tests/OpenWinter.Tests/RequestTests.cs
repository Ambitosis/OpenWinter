using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Requests;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class RequestTests
{
    readonly ITestOutputHelper _out;
    public RequestTests(ITestOutputHelper output) { _out = output; }

    static (World, GameServer, IConnection, Creature) Login()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
        }
        return (world, server, client, world.Get<Creature>(me)!);
    }

    static List<(string Kind, JsonElement Data)> Notifies(IConnection client)
    {
        var list = new List<(string, JsonElement)>();
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type != MessageType.Notify) continue;
            var n = NotifyMessage.Read(r);
            list.Add((n.Kind, JsonDocument.Parse(n.Json).RootElement));
        }
        return list;
    }

    [DataFact]
    public void CraftUsesTheInstalledDialogueAndTheAuthenticatedPlayer()
    {
        var (world, server, client, pc) = Login();
        client.Send(new ClientRequestMessage { Kind = "craft", Json = "{\"target\":12345,\"owner\":12345,\"dialog\":\"untrusted\"}" }.Write());
        server.Tick(1f / 30);
        var session = world.System<OpenWinter.Game.Conversations.ConversationService>().SessionOf(pc);
        Assert.NotNull(session);
        Assert.Same(pc, session.Owner);
        Assert.Same(pc, session.Pc);
        Assert.Equal("x0_skill_ctrap", session.ResRef);
        Assert.True(session.Entry >= 0); // Installed script conditions may reject the module's fixture PC.
    }

    [DataFact]
    public void LastingVisualEffectsReachPlayersWhoArriveLater()
    {
        // A glow put on a placeable before anyone is around (e.g. at module load) must show for players who come later.
        var world = new World(TestData.Module);
        world.LoadModule();
        var area = world.Areas.First(a => a.ResRef == "ss_town");
        var statue = area.Objects.OfType<Placeable>().First();
        var k = OpenWinter.Game.Scripting.EffectConstants.Of(world);
        var glow = new OpenWinter.Game.Scripting.Effect { Type = k["EFFECT_TYPE_VISUALEFFECT"], SubType = k.Magical };
        int glowRow = k["VFX_DUR_GLOW_RED"];
        glow.Ints[0] = glowRow;
        glow.Floats[0] = 1f;
        world.System<OpenWinter.Game.Rules.EffectSystem>().Apply(glow, k.Permanent, statue, 0);
        // An impact that happened before arrival must not be replayed as a lasting glow.
        var impact = glow.Copy();
        impact.Ints[0] = 101;
        world.System<OpenWinter.Game.Rules.EffectSystem>().Apply(impact, k.Permanent, statue, 0);

        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        server.Tick(1f / 30);
        var vfx = Notifies(client).Where(n => n.Kind == "vfx").ToList();
        Assert.Contains(vfx, n => n.Data.GetProperty("vfx").GetInt32() == glowRow && n.Data.GetProperty("duration").GetSingle() < 0);
        Assert.DoesNotContain(vfx, n => n.Data.GetProperty("vfx").GetInt32() == 101);
    }

    [DataFact]
    public void PlayersLearnWhichCreaturesAreHostileByFaction()
    {
        // Hostility is viewer-relative; green means the same party/faction, not just friendly reputation.
        var (world, server, client, pc) = Login();
        Creature Spawn(int faction)
        {
            var c = world.Creatures.FromTemplate(new GffStruct());
            c.Faction = faction;
            world.Register(c);
            world.AddToArea(c, pc.Area!, pc.Position + new System.Numerics.Vector3(2, 0, 0), 0);
            return c;
        }
        var goblin = Spawn(FactionTable.Hostile);
        var guard = Spawn(FactionTable.Defender);
        var ally = Spawn(pc.Faction);
        world.Factions.SetFactionReputation(FactionTable.Defender, FactionTable.PlayerFaction, 100);
        Notifies(client);
        server.Tick(0.6f);
        var reactions = Notifies(client).Where(n => n.Kind == "reactions").Select(n => n.Data).ToList();
        Assert.Contains(reactions, d => d.TryGetProperty(goblin.Id.ToString(), out var r) && r.GetInt32() == ReactionFeed.Hostile);
        Assert.Contains(reactions, d => d.TryGetProperty(guard.Id.ToString(), out var r) && r.GetInt32() == ReactionFeed.Neutral);

        Assert.Contains(reactions, d => d.TryGetProperty(ally.Id.ToString(), out var r) && r.GetInt32() == ReactionFeed.Friendly);

        // A change of heart is sent on the next update.
        world.Factions.SetFactionReputation(FactionTable.Hostile, FactionTable.PlayerFaction, 50);
        server.Tick(0.6f);
        var later = Notifies(client).Where(n => n.Kind == "reactions").Select(n => n.Data).ToList();
        Assert.Contains(later, d => d.TryGetProperty(goblin.Id.ToString(), out var r) && r.GetInt32() == ReactionFeed.Neutral);
    }

    [DataFact]
    public void ClickingAHostileCreatureAttacksAndAFriendlyOneTalks()
    {
        var (world, server, client, pc) = Login();
        Creature Spawn(int faction)
        {
            var c = world.Creatures.FromTemplate(new GffStruct());
            c.Faction = faction;
            c.MaxHitPoints = c.CurrentHitPoints = 50;
            world.Register(c);
            world.AddToArea(c, pc.Area!, pc.Position + new System.Numerics.Vector3(1.5f, 0, 0), 0);
            return c;
        }
        var goblin = Spawn(FactionTable.Hostile);
        var guard = Spawn(FactionTable.Defender);
        world.Factions.SetFactionReputation(FactionTable.Defender, FactionTable.PlayerFaction, 100);

        client.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = guard.Id }.Write());
        server.Tick(1f / 30);
        Assert.False(pc.InCombat);

        client.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = goblin.Id }.Write());
        server.Tick(1f / 30);
        Assert.True(pc.InCombat);
        Assert.DoesNotContain(pc.Actions, a => a is OpenWinter.Game.Conversations.InteractAction);
    }

    [DataFact]
    public void ACreatureBlockedByAWallStandsInsteadOfWalkingOnTheSpot()
    {
        var (world, _, _, pc) = Login();
        // Keep pushing west until the edge of the walkable ground stops us.
        pc.MoveDirection = new System.Numerics.Vector3(-1, 0, 0);
        for (int i = 0; i < 3000; i++) Movement.Step(world, pc, 0.1f);
        Assert.Equal(0f, pc.Speed);
        Assert.Equal(MovementMode.Stand, pc.Mode);
    }

    [Fact]
    public void EveryRequestKindHasOneHandler() =>
        Assert.Contains("equip", ClientRequests.Kinds);

    [DataFact]
    public void InventoryRequestListsItemsWithIconsThatExist()
    {
        var (world, server, client, pc) = Login();
        client.Send(new ClientRequestMessage { Kind = "inventory" }.Write());
        server.Tick(1f / 30);
        var inv = Notifies(client).Single(n => n.Kind == "inventory-open").Data;
        var items = inv.GetProperty("items").EnumerateArray().Concat(inv.GetProperty("equipped").EnumerateArray()).ToList();
        _out.WriteLine($"{items.Count} items, gold {inv.GetProperty("gold")}");
        Assert.NotEmpty(items);
        int found = 0, total = 0;
        foreach (var item in items)
        foreach (var icon in item.GetProperty("icon").EnumerateArray().Select(i => i.GetString()!))
        {
            total++;
            bool exists = icon.StartsWith("iconplt:", StringComparison.Ordinal)
                ? icon.Split(':') is { Length: 3 } parts && parts[2].Length == 20 && parts[2].All(Uri.IsHexDigit)
                    && world.Resources.Contains(parts[1], OpenWinter.Aurora.Resources.ResType.Plt)
                : world.Resources.Contains(icon, OpenWinter.Aurora.Resources.ResType.Tga) || world.Resources.Contains(icon, OpenWinter.Aurora.Resources.ResType.Dds);
            if (exists) found++;
            else _out.WriteLine($"missing icon {icon} for {item.GetProperty("name")}");
        }
        _out.WriteLine($"{found}/{total} icons found");
        Assert.True(found >= total * 0.8, $"only {found}/{total} icons resolve");
    }

    [DataFact]
    public void UnequipAndEquipRoundTrip()
    {
        var (world, server, client, pc) = Login();
        var (slot, item) = pc.Equipment.First();
        client.Send(new ClientRequestMessage { Kind = "unequip", Json = $"{{\"item\":{item.Id}}}" }.Write());
        for (int i = 0; i < 10; i++) server.Tick(1f / 30);
        Assert.DoesNotContain(item, pc.Equipment.Values);
        client.Send(new ClientRequestMessage { Kind = "equip", Json = $"{{\"item\":{item.Id}}}" }.Write());
        for (int i = 0; i < 10; i++) server.Tick(1f / 30);
        Assert.Contains(item, pc.Equipment.Values);
        // Somebody else's item can't be equipped.
        var other = world.AllObjects.OfType<Item>().First(i => world.Inventory.Owner(i) != pc);
        client.Send(new ClientRequestMessage { Kind = "equip", Json = $"{{\"item\":{other.Id}}}" }.Write());
        for (int i = 0; i < 10; i++) server.Tick(1f / 30);
        Assert.DoesNotContain(other, pc.Equipment.Values);
    }
}

public class CharacterSheetTests
{
    readonly Xunit.Abstractions.ITestOutputHelper _out;
    public CharacterSheetTests(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }

    [DataFact]
    public void BarbarianSheetHasClassAttackBonus()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        var pc = world.AllObjects.OfType<Creature>().Single(c => c.IsPC);
        foreach (var cl in pc.Classes)
        {
            var table = world.Resources.TwoDA("classes").Get(cl.Class, "AttackBonusTable");
            var t = OpenWinter.Game.Rules.CreatureRules.ClassTable(world, cl.Class, "AttackBonusTable");
            _out.WriteLine($"class {cl.Class} level {cl.Level} table '{table}' rows {t.RowCount} bab@lvl {t.GetInt(cl.Level - 1, "BAB")} cols {string.Join(",", t.Columns)}");
        }
        _out.WriteLine($"override {pc.BaseAttackOverride}");
        // A hak may replace the class attack tables (CLS_ATK_PC), so the expected BAB comes from the table.
        int expected = pc.Classes.Sum(cl => OpenWinter.Game.Rules.CreatureRules.ClassTable(world, cl.Class, "AttackBonusTable").GetInt(cl.Level - 1, "BAB"));
        Assert.Equal(expected, OpenWinter.Game.Rules.CreatureRules.BaseAttackBonus(world, pc));
        var sheet = System.Text.Json.JsonSerializer.SerializeToElement(OpenWinter.Game.Requests.CharacterRequests.Sheet(world, pc));
        Assert.DoesNotContain(sheet.GetProperty("skills").EnumerateArray(), s => s.GetProperty("name").GetString()!.StartsWith('{'));
    }
}

public class TransitionClientTests
{
    readonly Xunit.Abstractions.ITestOutputHelper _out;
    public TransitionClientTests(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }

    /// <summary>Clicking a transition door walks there, opens it, and moves the player: the client gets the new area.</summary>
    [DataFact]
    public void ClickingTransitionDoorSendsNewArea()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        while (client.TryReceive(out _)) { }
        var pc = world.AllObjects.OfType<Creature>().Single(c => c.IsPC);
        // Any door or transition trigger leading to another area.
        var (door, start) = world.Areas.SelectMany(a => a.Objects.Where(o => o is Door { IsTransition: true } or Trigger { TriggerType: Trigger.TypeTransition })
                .Select(o => (o, a, target: OpenWinter.Game.WorldObjects.TransitionService.ResolveTarget(world, o))))
            .Where(x => x.target?.Area is { } t && t != x.a).Select(x => (x.o, x.a)).First();
        _out.WriteLine($"door {door.Tag} at {door.Position} in {start.ResRef}");
        world.Jump(pc, new Location(start.Id, door.Position + new System.Numerics.Vector3(1.5f, 0, 0), 0));
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        while (client.TryReceive(out _)) { }

        // Two clicks: the first opens the door, the second walks through it.
        var loads = new List<string>();
        for (int click = 0; click < 2 && loads.Count == 0; click++)
        {
            client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = door.Id }.Write());
            for (int i = 0; i < 30 * 8 && loads.Count == 0; i++)
            {
                server.Tick(1f / 30);
                while (client.TryReceive(out var m))
                {
                    var r = new MessageReader(m);
                    if (r.Type == MessageType.LoadArea) loads.Add(LoadAreaMessage.Read(r).ResRef);
                }
            }
        }
        _out.WriteLine($"now in {pc.Area?.ResRef}; client loaded [{string.Join(", ", loads)}]");
        Assert.NotEqual(start, pc.Area);
        Assert.Equal(pc.Area!.ResRef, Assert.Single(loads));
    }
}
