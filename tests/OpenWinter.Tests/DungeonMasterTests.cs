using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.DungeonMaster;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Dungeon Masters: login, invisibility, possession, the creator and the DM tool actions.</summary>
public class DungeonMasterTests
{
    readonly ITestOutputHelper _out;
    public DungeonMasterTests(ITestOutputHelper output) { _out = output; }

    sealed class TestClient
    {
        public required LoopbackConnection Connection;
        public uint Me;
        public readonly HashSet<uint> Known = new();
        public readonly Dictionary<uint, Vector3> Positions = new();
        public readonly List<ChatMessage> Chat = new();
        public readonly List<NotifyMessage> Notifies = new();

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
                    case MessageType.Chat: Chat.Add(ChatMessage.Read(r)); break;
                    case MessageType.Notify: Notifies.Add(NotifyMessage.Read(r)); break;
                }
            }
        }

        public void Request(string kind, object data) =>
            Connection.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data) }.Write());
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    /// <summary>The module with the test scripts (and optional extra resources) on top, scripts on, module events replaced.</summary>
    (World World, GameServer Server) MakeServer(string? extraDir = null)
    {
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
        rm.AddFirst(new DirectorySource(ScriptsDir()));
        if (extraDir != null) rm.AddFirst(new DirectorySource(extraDir));
        var world = new World(rm);
        world.LoadModule();
        world.EnableScripts();
        world.Scripts!.Log = s => _out.WriteLine(s);
        // Only our OnClientEnter; the module's own events would need its database.
        world.Module.Scripts.Clear();
        world.Module.Scripts["Mod_OnClientEntr"] = "dm_onenter";
        foreach (var area in world.Areas) area.Scripts.Clear();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DMPassword = "secret" });
        world.Step(0.1f);
        return (world, server);
    }

    static TestClient Join(GameServer server, string name, bool dm = false, string password = "")
    {
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = name, DM = dm, DMPassword = password }.Write());
        return new TestClient { Connection = client };
    }

    static void Tick(GameServer server, int n = 3, params TestClient[] clients)
    {
        for (int i = 0; i < n; i++) server.Tick(1f / 30);
        foreach (var c in clients) c.Pump();
    }

    /// <summary>A fresh goblin a few metres from the area's first player (or its centre).</summary>
    static Creature Npc(World world, Area area)
    {
        var near = area.Objects.OfType<Creature>().FirstOrDefault(c => c.IsPC)?.Position ?? Vector3.Zero;
        var c = world.Creatures.FromBlueprint("nw_goblina")!;
        var pos = near + new Vector3(4, 0, 0);
        if (area.Walkmesh.HighestGround(pos.X, pos.Y) is { } z) pos.Z = z; else pos = near;
        world.AddToArea(c, area, pos, 0);
        c.Plot = false;
        c.IsImmortal = false;
        return c;
    }

    [DataFact]
    public void DMLoginNeedsThePasswordAndRunsOnClientEnter()
    {
        var (world, server) = MakeServer();
        var wrong = Join(server, "Mallory", dm: true, password: "guess");
        Tick(server, 3, wrong);
        Assert.Contains(wrong.Notifies, n => n.Kind == "login-refused");
        Assert.False(wrong.Connection.IsConnected);
        Assert.Equal(0u, wrong.Me);
        Assert.DoesNotContain(world.AllObjects.OfType<Creature>(), c => c.IsPC);

        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, dm);
        var avatar = world.Get<Creature>(dm.Me);
        Assert.NotNull(avatar);
        Assert.True(avatar!.IsDMAvatar);
        Assert.True(avatar.IsPC);
        Assert.Equal(1, world.Module.Locals.Ints.GetValueOrDefault("dm_entered")); // OnClientEnter saw GetIsDM
        world.Scripts!.Run("dm_isdm", avatar.Id);
        Assert.Equal(1, world.Module.Locals.Ints["dm_isdm"]);
        Assert.Equal(0, world.Module.Locals.Ints["dm_ispossessed"]);
        Assert.Equal(1, world.Module.Locals.Ints["dm_ispc"]);
        Assert.Contains(dm.Notifies, n => n.Kind == "dm-state");

        // A player is not a DM, and DM requests from players are ignored.
        var alice = Join(server, "Alice");
        Tick(server, 3, alice, dm);
        world.Scripts.Run("dm_isdm", alice.Me);
        Assert.Equal(0, world.Module.Locals.Ints["dm_isdm"]);
        var npc = Npc(world, avatar.Area!);
        alice.Request("dm-action", new { action = "kill", target = npc.Id });
        Tick(server, 2, alice);
        Assert.False(npc.IsDead);
    }

    [DataFact]
    public void HiddenDMIsInvisibleToPlayersAndCreaturesUntilItAppears()
    {
        var (world, server) = MakeServer();
        var alice = Join(server, "Alice");
        Tick(server, 3, alice);
        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, alice, dm);
        var avatar = world.Get<Creature>(dm.Me)!;
        world.Jump(avatar, new Location(avatar.Area!.Id, world.Get(alice.Me)!.Position + new Vector3(1, 0, 0), 0));
        Tick(server, 3, alice, dm);
        Assert.DoesNotContain(dm.Me, alice.Known); // no spawn for the player
        Assert.Contains(alice.Me, dm.Known);       // the DM sees the player

        // Creatures don't perceive the hidden DM.
        var npc = Npc(world, avatar.Area!);
        world.Jump(avatar, new Location(npc.Area!.Id, npc.Position + new Vector3(1.5f, 0, 0), 0));
        world.System<OpenWinter.Game.Society.PerceptionSystem>().Update(npc);
        Assert.DoesNotContain(avatar.Id, npc.Society.Seen);

        // Appear: players and creatures notice the DM; disappear: gone again.
        dm.Request("dm-action", new { action = "appear" });
        Tick(server, 3, alice, dm);
        Assert.Contains(dm.Me, alice.Known);
        dm.Request("dm-action", new { action = "disappear" });
        Tick(server, 3, alice, dm);
        Assert.DoesNotContain(dm.Me, alice.Known);

        // DM channel: players reach the DMs, but never each other.
        var bob = Join(server, "Bob");
        Tick(server, 3, alice, bob, dm);
        alice.Connection.Send(new ChatSendMessage { Channel = ChatChannel.Dm, Text = "help, DM" }.Write());
        Tick(server, 2, alice, bob, dm);
        Assert.Contains(dm.Chat, c => c.Channel == ChatChannel.Dm && c.Text == "help, DM");
        Assert.DoesNotContain(bob.Chat, c => c.Text == "help, DM");
    }

    [DataFact]
    public void DMSeesAStealthedCreature()
    {
        var (world, server) = MakeServer();
        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, dm);
        var avatar = world.Get<Creature>(dm.Me)!;
        var npc = Npc(world, avatar.Area!);
        npc.StealthMode = true;
        npc.ActionModes.Add(1);
        world.Jump(avatar, new Location(npc.Area!.Id, npc.Position + new Vector3(6, 0, 0), 0));
        Tick(server, 20, dm);
        Assert.True(world.System<OpenWinter.Game.Skills.SkillSystem>().AlwaysPerceives(avatar, npc));
        var seen = dm.Notifies.Where(n => n.Kind == "stealth-visibility" && n.Subject == npc.Id).Select(n => JsonDocument.Parse(n.Json).RootElement).ToList();
        Assert.NotEmpty(seen);
        Assert.True(seen[^1].GetProperty("seen").GetBoolean());
    }

    [DataFact]
    public void AHiderAPlayerCannotPerceiveIsNotSentToHer()
    {
        var (world, server) = MakeServer();
        var alice = Join(server, "Alice");
        Tick(server, 3, alice);
        var pc = world.Get<Creature>(alice.Me)!;
        var npc = Npc(world, pc.Area!);
        Tick(server, 10, alice);
        Assert.Contains(npc.Id, alice.Known);

        // In stealth with skills she can't beat, with Hide in Plain Sight (so being in view doesn't keep it seen):
        // despawned for her, so no position updates either.
        npc.Skills[5] = 80;
        npc.Skills[8] = 80;
        npc.Feats.Add(OpenWinter.Game.Skills.SkillIds.FeatHideInPlainSight);
        Assert.True(world.System<OpenWinter.Game.Skills.SkillSystem>().SetActionMode(npc, 1, true));
        Tick(server, 30, alice);
        Assert.DoesNotContain(npc.Id, pc.Society.Seen);
        Assert.DoesNotContain(npc.Id, alice.Known);

        // Out of stealth it comes back.
        world.System<OpenWinter.Game.Skills.SkillSystem>().SetActionMode(npc, 1, false);
        Tick(server, 30, alice);
        Assert.Contains(npc.Id, alice.Known);
    }

    [DataFact]
    public void PossessionMovesTheCreatureAndSpeaksAsIt()
    {
        var (world, server) = MakeServer();
        var alice = Join(server, "Alice");
        Tick(server, 3, alice);
        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, alice, dm);
        var avatar = world.Get<Creature>(dm.Me)!;
        var npc = Npc(world, avatar.Area!);
        dm.Request("dm-action", new { action = "goto", target = npc.Id });
        Tick(server, 3, alice, dm);
        Assert.True(Vector3.Distance(avatar.Position, npc.Position) < 3f);

        dm.Request("dm-action", new { action = "possess", target = npc.Id });
        Tick(server, 2, alice, dm);
        Assert.Equal(npc.Id, dm.Me); // the client now controls the creature
        Assert.True(npc.IsDMPossessed);
        world.Scripts!.Run("dm_isdm", npc.Id);
        Assert.Equal(0, world.Module.Locals.Ints["dm_isdm"]);
        Assert.Equal(1, world.Module.Locals.Ints["dm_ispossessed"]);
        Assert.Equal(1, world.Module.Locals.Ints["dm_ispc"]);
        world.Scripts.Run("dm_isdm", avatar.Id);
        Assert.Equal(1, world.Module.Locals.Ints["dm_isdm"]); // the avatar stays a DM while possessing

        // Movement input drives the possessed creature.
        var start = npc.Position;
        float moved = 0;
        foreach (var dir in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY })
        {
            dm.Connection.Send(new MoveInputMessage { Direction = dir }.Write());
            Tick(server, 30, alice, dm);
            moved = Math.Max(moved, Vector3.Distance(start, npc.Position));
            if (moved > 1f) break;
        }
        dm.Connection.Send(new MoveInputMessage { Direction = Vector3.Zero }.Write());
        _out.WriteLine($"possessed creature moved {moved:0.00} m");
        Assert.True(moved > 1f);

        // Talking speaks as the creature.
        dm.Connection.Send(new ChatSendMessage { Text = "I am possessed" }.Write());
        Tick(server, 2, alice, dm);
        var said = Assert.Single(alice.Chat, c => c.Text == "I am possessed");
        Assert.Equal(npc.Id, said.Speaker);

        dm.Request("dm-action", new { action = "unpossess" });
        Tick(server, 2, alice, dm);
        Assert.Equal(avatar.Id, dm.Me);
        Assert.False(npc.IsDMPossessed);
        Assert.False(npc.IsPC);
    }

    [DataFact]
    public void CreatorSpawnsFromThePaletteAndRunsOnSpawn()
    {
        // A custom palette with one blueprint whose OnSpawn marks the creature.
        var dir = Directory.CreateTempSubdirectory("openwinter-dm");
        var utc = new GffStruct()
            .SetString("Tag", "DM_TEST_CREATURE").SetResRef("TemplateResRef", "dm_testcre")
            .SetLocString("FirstName", new LocString("Palette Ogre")).SetWord("Appearance_Type", 6)
            .SetShort("HitPoints", 10).SetShort("CurrentHitPoints", 10).SetShort("MaxHitPoints", 10)
            .SetResRef("ScriptSpawn", "dm_onspawn").SetDWord("FactionID", 1);
        File.WriteAllBytes(Path.Combine(dir.FullName, "dm_testcre.utc"), Gff.Write(utc, "UTC "));
        var leaf = new GffStruct().SetString("NAME", "Palette Ogre").SetResRef("RESREF", "dm_testcre").SetFloat("CR", 1).SetString("FACTION", "Hostile");
        var category = new GffStruct().SetString("NAME", "Test monsters").SetByte("ID", 200).SetList("LIST", new List<GffStruct> { leaf });
        var itp = new GffStruct().SetList("MAIN", new List<GffStruct> { new GffStruct().SetString("NAME", "Tests").SetList("LIST", new List<GffStruct> { category }) });
        File.WriteAllBytes(Path.Combine(dir.FullName, "creaturepalcus.itp"), Gff.Write(itp, "ITP "));

        var (world, server) = MakeServer(dir.FullName);
        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, dm);
        var avatar = world.Get<Creature>(dm.Me)!;

        dm.Request("dm-palette", new { kind = "creature" });
        Tick(server, 2, dm);
        var palette = dm.Notifies.Last(n => n.Kind == "dm-palette");
        Assert.Contains("dm_testcre", palette.Json);
        Assert.Contains("Test monsters", palette.Json);
        var tree = world.System<DungeonMasterSystem>().Palettes.Tree(Palettes.Kind("creature")!);
        Assert.Equal("Custom", tree[0].Name);
        Assert.Contains(tree, r => r.Name == "Standard" && r.CountBlueprints() > 100);

        var at = avatar.Position + new Vector3(2, 0, 0);
        dm.Request("dm-spawn", new { kind = "creature", resref = "dm_testcre", pos = new[] { at.X, at.Y, at.Z }, facing = 90 });
        Tick(server, 2, dm);
        var spawned = Assert.Single(world.ByTag("DM_TEST_CREATURE").OfType<Creature>());
        Assert.Equal(avatar.Area, spawned.Area);
        Assert.True(Vector3.Distance(at with { Z = 0 }, spawned.Position with { Z = 0 }) < 0.5f);
        Assert.Equal(1, spawned.Locals.Ints.GetValueOrDefault("dm_spawned"));
        Assert.Contains(spawned.Id, dm.Known);

        // Items from the item palette go into a creature's inventory.
        dm.Request("dm-spawn", new { kind = "item", resref = "nw_it_mpotion001", pos = new[] { at.X, at.Y, at.Z }, into = spawned.Id });
        Tick(server, 2, dm);
        Assert.Contains(world.Inventory.Items(spawned), i => i.ResRef == "nw_it_mpotion001");
    }

    [DataFact]
    public void DMToolsGiveHealKillAndLimbo()
    {
        var (world, server) = MakeServer();
        var alice = Join(server, "Alice");
        Tick(server, 3, alice);
        var dm = Join(server, "Dana", dm: true, password: "secret");
        Tick(server, 3, alice, dm);
        var pc = world.Get<Creature>(alice.Me)!;

        int gold = pc.Gold, xp = pc.Experience;
        dm.Request("dm-action", new { action = "give-gold", target = pc.Id, amount = 250 });
        dm.Request("dm-action", new { action = "give-xp", target = pc.Id, amount = 1500 });
        dm.Request("dm-action", new { action = "give-item", target = pc.Id, resref = "nw_it_mpotion001" });
        Tick(server, 2, alice, dm);
        Assert.Equal(gold + 250, pc.Gold);
        Assert.Equal(xp + 1500, pc.Experience);
        var potion = Assert.Single(world.Inventory.Items(pc), i => i.ResRef == "nw_it_mpotion001");
        Assert.Contains(dm.Notifies, n => n.Kind == "dm-examine" && n.Subject == pc.Id);

        // Take it back into the DM's inventory.
        dm.Request("dm-action", new { action = "take-item", item = potion.Id });
        Tick(server, 2, dm);
        Assert.Equal(world.Get<Creature>(dm.Me), world.Inventory.Owner(potion));

        // Kill and heal a creature.
        var avatar = world.Get<Creature>(dm.Me)!;
        var npc = Npc(world, avatar.Area!);
        dm.Request("dm-action", new { action = "kill", target = npc.Id });
        Tick(server, 2, dm);
        Assert.True(npc.IsDead);
        dm.Request("dm-action", new { action = "heal", target = npc.Id });
        Tick(server, 2, dm);
        Assert.False(npc.IsDead);
        Assert.Equal(npc.MaxHitPoints, npc.CurrentHitPoints);

        // Invulnerable and immortal toggles.
        dm.Request("dm-action", new { action = "invulnerable", target = npc.Id });
        dm.Request("dm-action", new { action = "immortal", target = npc.Id });
        dm.Request("dm-action", new { action = "ai", target = npc.Id });
        Tick(server, 2, dm);
        Assert.True(npc.Plot);
        Assert.True(npc.IsImmortal);
        Assert.True(npc.DMAIOff);

        // Limbo takes the creature out of the world; it comes back next to the DM.
        world.Jump(avatar, new Location(npc.Area!.Id, npc.Position + new Vector3(2, 0, 0), 0));
        Tick(server, 3, alice, dm);
        Assert.Contains(npc.Id, dm.Known);
        dm.Request("dm-action", new { action = "limbo", target = npc.Id });
        Tick(server, 2, alice, dm);
        Assert.Null(npc.Area);
        Assert.Contains(npc.Id, world.System<DungeonMasterSystem>().Limbo);
        Assert.DoesNotContain(npc.Id, dm.Known);
        Assert.Contains(dm.Notifies, n => n.Kind == "dm-limbo" && n.Json.Contains(npc.Id.ToString()));
        dm.Request("dm-action", new { action = "from-limbo", target = npc.Id });
        Tick(server, 2, alice, dm);
        Assert.Equal(avatar.Area, npc.Area);
        Assert.Contains(npc.Id, dm.Known);
        Assert.Empty(world.System<DungeonMasterSystem>().Limbo);

        // Players can be muted.
        dm.Request("dm-action", new { action = "mute", target = pc.Id });
        Tick(server, 2, alice, dm);
        alice.Connection.Send(new ChatSendMessage { Text = "can anyone hear me" }.Write());
        Tick(server, 2, alice, dm);
        Assert.DoesNotContain(dm.Chat, c => c.Text == "can anyone hear me");
    }
}
