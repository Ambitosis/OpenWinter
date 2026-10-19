using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public sealed class SaveGameTests : IDisposable
{
    readonly ITestOutputHelper _out;
    readonly string _root = Path.Combine(Path.GetTempPath(), "openwinter-savegame-" + Guid.NewGuid().ToString("N"));
    readonly List<World> _worlds = new();

    public SaveGameTests(ITestOutputHelper output)
    {
        _out = output;
        SaveGameService.Log = output.WriteLine;
    }

    public void Dispose()
    {
        foreach (var w in _worlds) w.System<PersistenceSystem>().Dispose();
        SaveGameService.Log = Console.WriteLine;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    // ------------------------------------------------------------------ ERF

    [Fact]
    public void ErfRoundTripsByteForByte()
    {
        var date = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        var entries = new List<ErfEntry>
        {
            new("module", ResType.Ifo, Gff.Write(new GffStruct().SetString("Mod_Tag", "test"), "IFO ")),
            new("area001", ResType.Git, new byte[] { 1, 2, 3, 4, 5 }),
            new("area001", ResType.Are, Array.Empty<byte>()),
            new("openwinter", ResType.Gff, Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray()),
        };
        var bytes = Erf.Write(entries, "SAV ", buildDate: date);
        Assert.Equal("SAV V1.0", System.Text.Encoding.ASCII.GetString(bytes, 0, 8));

        var read = Erf.Read(bytes, out var type);
        Assert.Equal("SAV ", type);
        Assert.Equal(entries.Count, read.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Assert.Equal(entries[i].ResRef, read[i].ResRef);
            Assert.Equal(entries[i].ResType, read[i].ResType);
            Assert.Equal(entries[i].Data, read[i].Data);
        }
        Assert.Equal(bytes, Erf.Write(read, type, buildDate: date));

        // The engine's archive reader sees the same resources.
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "test.sav");
        File.WriteAllBytes(path, bytes);
        using (var src = new ErfSource(path))
        {
            Assert.Equal("SAV", src.FileType);
            foreach (var e in entries) Assert.Equal(e.Data, src.Read(ResKey.Of(e.ResRef, e.ResType)));
        }

        // Long resrefs switch to V1.1 (32 characters).
        var longEntries = new List<ErfEntry> { new("a_resref_longer_than_sixteen", ResType.Git, new byte[] { 9 }) };
        var v11 = Erf.Write(longEntries, "ERF ", buildDate: date);
        Assert.Equal("ERF V1.1", System.Text.Encoding.ASCII.GetString(v11, 0, 8));
        Assert.Equal(longEntries[0].ResRef, Erf.Read(v11, out _)[0].ResRef);
        Assert.Equal(v11, Erf.Write(Erf.Read(v11, out _), "ERF ", buildDate: date));

        // The reference tool lists the archive, when available.
        var nwnErf = TestData.NimTool("nwn_erf.exe");
        if (File.Exists(nwnErf))
        {
            var psi = new ProcessStartInfo(nwnErf, $"-f \"{path}\" -t") { RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            var listing = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            _out.WriteLine(listing);
            Assert.Equal(0, p.ExitCode);
            Assert.Contains("area001.git", listing);
            Assert.Contains("openwinter.gff", listing);
        }
    }

    // ------------------------------------------------------------------ full save / load

    sealed record Session(World World, GameServer Server, LoopbackConnection Client, Creature Pc);

    Session StartGame()
    {
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
        rm.AddFirst(new DirectorySource(ScriptsDir()));
        var world = new World(rm);
        _worlds.Add(world);
        world.EnableScripts();
        world.Scripts!.Log = _out.WriteLine;
        var persistence = world.System<PersistenceSystem>();
        persistence.RootOverride = _root;
        persistence.Log = _out.WriteLine;
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", SaveGames = true });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / GameServer.TickRate);
        var pc = server.Sessions[0].Creature!;
        Drain(client);
        return new Session(world, server, client, pc);
    }

    static void Run(GameServer server, LoopbackConnection client, double seconds)
    {
        int ticks = (int)Math.Round(seconds * GameServer.TickRate);
        for (int i = 0; i < ticks; i++)
        {
            server.Tick(1f / GameServer.TickRate);
            Drain(client);
        }
    }

    static List<(MessageType Type, byte[] Data)> Drain(LoopbackConnection client)
    {
        var list = new List<(MessageType, byte[])>();
        while (client.TryReceive(out var m)) list.Add((new MessageReader(m).Type, m));
        return list;
    }

    [DataFact]
    public void SavedGameRestoresTheWholeSimulation()
    {
        var sw = Stopwatch.StartNew();
        var (world, server, client, pc) = StartGame();
        Run(server, client, 3);
        _out.WriteLine($"module running: {world.AllObjects.Count()} objects, {sw.ElapsedMilliseconds} ms");
        var area = pc.Area!;

        // ---- mutate everything the save has to carry
        world.Module.Locals.Ints["sg_mod"] = 11;
        world.Module.Locals.Jsons["sg_json"] = "{\"a\":[1,2]}";
        area.Locals.Strings["sg_area"] = "hello";
        area.Locals.Locations["sg_where"] = new Location(area.Id, new Vector3(1, 2, 3), 90);
        pc.Locals.Floats["sg_pc"] = 2.5f;
        pc.Locals.Objects["sg_area_obj"] = area.Id;

        var npcs = world.AllObjects.OfType<Creature>().Where(c => !c.IsPC && !c.IsDead && c.Area != null).OrderBy(c => c.Id).ToList();
        var victim = npcs.First(c => !c.Plot && !c.IsImmortal);
        var other = npcs.First(c => c != victim);
        OpenWinter.Game.Rules.HitPoints.Kill(world, victim, pc.Id);

        var given = world.Items.FromBlueprint("nw_it_mpotion001") ?? throw new InvalidOperationException("potion blueprint missing");
        world.Register(given);
        world.Inventory.MoveTo(given, pc);
        var dropped = world.Items.FromBlueprint("nw_it_mpotion001")!;
        world.Register(dropped);
        world.Inventory.Drop(dropped, new Location(area.Id, pc.Position + new Vector3(1, 0, 0), 0));
        var destroyed = world.AllObjects.OfType<Item>().Where(i => i != given && i != dropped).OrderBy(i => i.Id).First();
        uint destroyedId = destroyed.Id;
        world.Destroy(destroyed);

        var door = world.AllObjects.OfType<Door>().Where(d => !d.IsOpen && !d.Locked && d.Area != null).OrderBy(d => d.Id).First();
        Assert.True(DoorService.Open(world, door, null));

        var journal = world.System<JournalService>();
        var quest = journal.Categories.Values.First(c => c.Entries.Count > 0);
        int questState = quest.Entries.Keys.Min();
        journal.Add(quest.Tag, questState, pc, false, false, true);

        var tiled = world.Areas.First(a => a.Tiles.Length > 1 && a.Tiles.Select(t => t.Id).Distinct().Count() > 1);
        int tileIndex = Array.FindIndex(tiled.Tiles, t => t.Id != tiled.Tiles[0].Id);
        tiled.Tiles[0].Id = tiled.Tiles[tileIndex].Id;
        tiled.Tiles[0].Orientation = (byte)((tiled.Tiles[0].Orientation + 1) & 3);
        WalkmeshBuilder.Rebuild(world, tiled);
        int tiledTriangles = tiled.Walkmesh.TriangleCount;

        var instance = AreaInstancing.Create(world, world.Areas.First(a => a != area && a != tiled).ResRef, "sg_instance", "Instanced")!;
        instance.Locals.Ints["sg_inst"] = 3;
        int instanceObjects = instance.Objects.Count, instanceTriangles = instance.Walkmesh.TriangleCount;

        world.Factions.SetFactionReputation(2, 1, 77);
        world.Factions.SetPersonal(other, pc, 5);

        var doomed = world.AllObjects.OfType<Placeable>().Where(p => p.Area != null && !p.Plot).OrderBy(p => p.Id).First();
        world.Module.Locals.Objects["sg_doomed"] = doomed.Id;
        world.Module.Locals.Objects["sg_target"] = pc.Id;
        world.Scripts!.Run("sg_setup", world.Module.Id);
        Assert.Equal(1, world.Module.Locals.Ints.GetValueOrDefault("sg_setup_ran"));
        // AssignCommand runs after the script: one tick queues the actions.
        Run(server, client, 0.1);
        Assert.True(pc.Actions.Count >= 2, $"pc actions: {pc.Actions.Count}");
        Assert.True(world.Scripts.PendingCommands >= 3, $"pending commands: {world.Scripts.PendingCommands}");
        int acEffects = pc.Effects.Count;
        Assert.True(acEffects > 0);
        var setupLocation = pc.Location;
        Run(server, client, 2);

        // ---- save
        sw.Restart();
        var info = SaveGameService.Save(world, "Test Save");
        _out.WriteLine($"saved in {sw.ElapsedMilliseconds} ms to {info.Folder}");
        var folder = Path.Combine(_root, "saves", info.Folder);
        Assert.True(File.Exists(Path.Combine(folder, "savegame.sav")));
        Assert.Equal("Test Save", File.ReadAllText(Path.Combine(folder, "savename.txt")));
        Assert.Single(Directory.GetFiles(folder, "*.bic"));
        _out.WriteLine($"savegame.sav: {new FileInfo(Path.Combine(folder, "savegame.sav")).Length / 1024} KB");
        foreach (var g in Erf.Read(File.ReadAllBytes(Path.Combine(folder, "savegame.sav")), out _).GroupBy(e => ResType.Extension(e.ResType)))
            _out.WriteLine($"  {g.Key}: {g.Count()} resources, {g.Sum(e => e.Data.Length) / 1024} KB");
        using (var erf = new ErfSource(Path.Combine(folder, "savegame.sav")))
        {
            Assert.Equal("SAV", erf.FileType);
            Assert.True(erf.Contains(ResKey.Of("module", ResType.Ifo)));
            var git = Gff.Read(erf.Read(ResKey.Of(area.ResRef, ResType.Git))!);
            Assert.Contains(git.GetList("List"), s => s.GetInt("ObjectId") == dropped.Id);
            var are = Gff.Read(erf.Read(ResKey.Of(tiled.ResRef, ResType.Are))!);
            Assert.Equal(tiled.Tiles[0].Id, are.GetList("Tile_List")[0].GetInt("Tile_ID"));
        }
        var listed = SaveGameService.List(world);
        Assert.Contains(listed, s => s.Folder == info.Folder && s.Name == "Test Save" && s.AreaResRef == area.ResRef);

        var before = WorldSnapshot.Capture(world, out _);
        _out.WriteLine($"snapshot: {before["objects"]!.AsArray().Count} objects, {before["timers"]!.AsArray().Count} timers, "
            + $"{before["host"]!["scheduled"]!.AsArray().Count} scheduled, {before.ToJsonString().Length / 1024} KB json");
        Assert.NotEmpty(before["timers"]!.AsArray());
        int pendingBefore = world.Scripts.PendingCommands;

        // ---- load into a fresh world through the server (as the client's load-game does)
        sw.Restart();
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _out.WriteLine($"loaded in {sw.ElapsedMilliseconds} ms; {loaded.Restored.Warnings.Count} warnings");
        foreach (var w in loaded.Restored.Warnings.Take(40)) _out.WriteLine("  warning: " + w);
        var w2 = server.World;
        _worlds.Add(w2);
        Assert.NotSame(world, w2);
        var messages = Drain(client);
        Assert.Contains(messages, m => m.Type == MessageType.LoadArea);
        Assert.Contains(messages, m => m.Type == MessageType.ControlObject);

        // Everything the reflection snapshot sees matches (both are taken at the same point between
        // world steps, so objects, systems, scheduler and timers must be identical).
        var after = WorldSnapshot.Capture(w2, out _);
        var diffs = new List<string>();
        Diff("", before, after, diffs);
        foreach (var d in diffs.Take(30)) _out.WriteLine("diff: " + d);
        Assert.Empty(diffs);

        // ---- explicit checks of the mutated state, with ids
        var pc2 = server.Sessions[0].Creature!;
        Assert.Equal(pc.Id, pc2.Id);
        Assert.True(pc2.IsPC);
        Assert.Equal(pc.Position, pc2.Position);
        Assert.Equal(area.Id, pc2.Area!.Id);
        Assert.Equal(world.Time, w2.Time);
        Assert.Equal(world.Clock.Hour, w2.Clock.Hour);
        Assert.Equal(world.Clock.Minute, w2.Clock.Minute);
        Assert.Equal(11, w2.Module.Locals.Ints["sg_mod"]);
        Assert.Equal("{\"a\":[1,2]}", w2.Module.Locals.Jsons["sg_json"]);
        Assert.Equal("hello", w2.Get<Area>(area.Id)!.Locals.Strings["sg_area"]);
        Assert.Equal(new Location(area.Id, new Vector3(1, 2, 3), 90), w2.Get<Area>(area.Id)!.Locals.Locations["sg_where"]);
        Assert.Equal(2.5f, pc2.Locals.Floats["sg_pc"]);
        Assert.Equal(area.Id, pc2.Locals.Objects["sg_area_obj"]);
        Assert.Contains(pc2.Inventory, i => i.Id == given.Id && i.ResRef == given.ResRef);
        Assert.Equal(dropped.Position, w2.Get<Item>(dropped.Id)!.Position);
        Assert.Contains(w2.Get<Item>(dropped.Id)!, w2.Get<Area>(area.Id)!.Objects);
        Assert.Null(w2.Get(destroyedId));
        Assert.True(w2.Get<Door>(door.Id)!.IsOpen);
        var victim2 = w2.Get<Creature>(victim.Id);
        Assert.Equal(victim.IsDestroyed || world.Get(victim.Id) == null, victim2 == null);
        if (victim2 != null) Assert.True(victim2.IsDead);
        Assert.Equal(questState, pc2.Journal[quest.Tag].State);
        var tiled2 = w2.Get<Area>(tiled.Id)!;
        Assert.Equal(tiled.Tiles, tiled2.Tiles);
        Assert.Equal(tiledTriangles, tiled2.Walkmesh.TriangleCount);
        var instance2 = w2.Get<Area>(instance.Id)!;
        Assert.Equal("sg_instance", instance2.Tag);
        Assert.Equal(instance.ResRef, instance2.ResRef);
        Assert.Contains(instance2, w2.Areas);
        Assert.Equal(instanceObjects, instance2.Objects.Count);
        Assert.Equal(3, instance2.Locals.Ints["sg_inst"]);
        Assert.Equal(instanceTriangles, instance2.Walkmesh.TriangleCount);
        Assert.Same(instance2, w2.ByTag("sg_instance").Single());
        Assert.Equal(77, w2.Factions.FactionReputation(2, 1));
        Assert.Equal(5, w2.Factions.Reputation(w2.Get<Creature>(other.Id)!, pc2));
        Assert.Equal(acEffects, pc2.Effects.Count);
        Assert.Equal(pc.Effects.Select(e => (e.Type, e.ExpiresAt)), pc2.Effects.Select(e => (e.Type, e.ExpiresAt)));
        Assert.Equal(pc.Actions.Select(a => a.GetType()), pc2.Actions.Select(a => a.GetType()));
        Assert.Equal(pendingBefore, w2.Scripts!.PendingCommands);
        Assert.Equal(world.ByTag(other.Tag).Select(o => o.Id), w2.ByTag(other.Tag).Select(o => o.Id));
        Assert.False(w2.Module.Locals.Ints.ContainsKey("sg_delayed"));
        Assert.NotNull(w2.Get(doomed.Id));

        // ---- the restored closures and actions run with the values they captured
        Run(server, client, 31);
        Assert.Equal(42, w2.Module.Locals.Ints.GetValueOrDefault("sg_delayed"));
        Assert.Equal("seven!", w2.Module.Locals.Strings.GetValueOrDefault("sg_delayed_str"));
        Assert.Equal(setupLocation, w2.Module.Locals.Locations["sg_delayed_loc"]);
        Assert.Equal(8, w2.Module.Locals.Ints.GetValueOrDefault("sg_action"));
        var hasteType = int.Parse(w2.Scripts.Definitions.Constants["EFFECT_TYPE_HASTE"].Value);
        Assert.Contains(pc2.Effects, e => e.Type == hasteType);
        Run(server, client, 3);
        Assert.Null(w2.Get(doomed.Id));
        // The new world keeps allocating ids after the saved ones.
        var fresh = w2.Items.FromBlueprint("nw_it_mpotion001")!;
        w2.Register(fresh);
        Assert.True(fresh.Id > before["nextId"]!.GetValue<uint>() - 1);
    }

    [DataFact]
    public void SavingMidCombatAndMidSpellRoundTrips()
    {
        var (world, server, client, pc) = StartGame();
        Run(server, client, 1);
        var foe = world.Creatures.FromBlueprint("nw_goblina") ?? throw new InvalidOperationException("goblin blueprint missing");
        world.AddToArea(foe, pc.Area!, pc.Position + new Vector3(2, 0, 0), 0);
        var caster = world.AllObjects.OfType<Creature>().Where(c => !c.IsPC && !c.IsDead && c.Area != null && c != foe).OrderBy(c => c.Id).First();
        world.System<OpenWinter.Game.Combat.CombatSystem>().Attack(pc, foe);
        // Bless (58) cheat-cast by another creature: conjuring, flight and impact span several ticks.
        caster.Actions.Insert(0, new OpenWinter.Game.Spells.CastSpellAction { Spell = 58, Cheat = true, Target = caster.Id });
        Run(server, client, 0.5);
        Assert.Contains(caster.Actions, a => a is OpenWinter.Game.Spells.CastSpellAction);

        var info = SaveGameService.Save(world, "Combat");
        var before = WorldSnapshot.Capture(world, out var warnings);
        var combat = before["systems"]!.AsArray().Single(x => x!["type"]!.GetValue<string>() == typeof(OpenWinter.Game.Combat.CombatSystem).FullName);
        Assert.NotEmpty(combat!["fields"]!["_states"]!.AsArray());
        foreach (var w in warnings) _out.WriteLine("save warning: " + w);
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(server.World);
        foreach (var w in loaded.Restored.Warnings) _out.WriteLine("load warning: " + w);
        var after = WorldSnapshot.Capture(server.World, out _);
        var diffs = new List<string>();
        Diff("", before, after, diffs);
        foreach (var d in diffs.Take(30)) _out.WriteLine("diff: " + d);
        Assert.Empty(diffs);
        Assert.Empty(loaded.Restored.Warnings);

        // Both worlds carry on the same way from here (no exceptions, same scheduler shape).
        Run(server, client, 10);
        Assert.True(server.World.Time > world.Time + 9);
    }

    [DataFact]
    public void ReloadDropsPendingSummonArrivalButPreservesLandedSummonAndExpiry()
    {
        var (world, server, client, pc) = StartGame();
        var fx = world.System<OpenWinter.Game.Rules.EffectSystem>();
        var k = EffectConstants.Of(world);
        Creature Master(string tag, float x)
        {
            var c = new Creature { Tag = tag, MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(c, pc.Area!, pc.Position + new Vector3(x, 0, 0), 0);
            return c;
        }
        var landed = Master("save_landed", 5);
        var pending = Master("save_pending", 10);
        foreach (var master in new[] { landed, pending })
        {
            var e = new Effect
            {
                Type = k.SummonCreature, Creator = master.Id, ScriptId = fx.NextId(),
                Strings = { [0] = "nw_s_badgerdire" }, Floats = { [0] = master == landed ? 0 : 5 },
                Ints = { [0] = -1, [2] = -1 },
            };
            fx.ApplyAtLocation(e, k.Temporary, master.Location, 20);
        }
        Run(server, client, 1);
        uint summonId = Assert.Single(landed.Effects).SummonedObject;
        Assert.IsType<Creature>(world.Get(summonId));
        Assert.Equal(GameObject.InvalidId, Assert.Single(pending.Effects).SummonedObject);
        var info = SaveGameService.Save(world, "Pending summon");

        // Saving alone does not cancel arrival: the uninterrupted control lands.
        Run(server, client, 6);
        Assert.IsType<Creature>(world.Get(Assert.Single(pending.Effects).SummonedObject));
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(server.World);
        Assert.Empty(loaded.Restored.Warnings);
        var landed2 = server.World.Get<Creature>(landed.Id)!;
        var pending2 = server.World.Get<Creature>(pending.Id)!;
        Assert.Equal(summonId, Assert.Single(landed2.Effects).SummonedObject);
        Run(server, client, 6);
        Assert.IsType<Creature>(server.World.Get(summonId));
        Assert.Equal(GameObject.InvalidId, Assert.Single(pending2.Effects).SummonedObject);
        Assert.DoesNotContain(server.World.AllObjects.OfType<Creature>(), c => c.Master == pending2.Id);
        Run(server, client, 15);
        Assert.Null(server.World.Get(summonId));
        Assert.Empty(landed2.Effects);
        Assert.Empty(pending2.Effects);
    }

    [DataFact]
    public void ReloadReevaluatesCursePenaltiesAgainstRestoredImmunity()
    {
        var (world, server, client, pc) = StartGame();
        var fx = world.System<OpenWinter.Game.Rules.EffectSystem>();
        var k = EffectConstants.Of(world);
        Creature Target(string tag, float x)
        {
            var c = new Creature { Tag = tag, MaxHitPoints = 100, CurrentHitPoints = 100 };
            Array.Fill(c.Abilities, 14);
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 10 });
            world.AddToArea(c, pc.Area!, pc.Position + new Vector3(x, 0, 0), 0);
            OpenWinter.Game.Rules.CreatureRules.EnsureInit(world, c);
            return c;
        }
        Effect Curse(bool ignore = false) => new() { Type = NwRules.EffectCurse, IgnoreImmunity = ignore,
            Ints = { [0] = 2, [1] = 2, [2] = 2, [3] = 2, [4] = 2, [5] = 2 } };
        Effect Immunity() => new() { Type = NwRules.EffectImmunity, Ints = { [0] = NwRules.ImmunityAbilityDecrease } };
        var expired = Target("save_expired_immunity", 5);
        var late = Target("save_late_immunity", 10);
        var bypass = Target("save_ignore_immunity", 15);
        int hp = expired.MaxHitPoints;
        fx.Apply(Immunity(), k.Temporary, expired, 1);
        fx.Apply(Curse(), k.Permanent, expired, 0);
        fx.Apply(Curse(), k.Permanent, late, 0);
        fx.Apply(Immunity(), k.Permanent, bypass, 0);
        fx.Apply(Curse(true), k.Permanent, bypass, 0);
        Run(server, client, 2);
        fx.Apply(Immunity(), k.Permanent, late, 0);
        int Score(Creature c, int a) => OpenWinter.Game.Rules.CreatureRules.AbilityScore(server.World, c, a);
        Assert.Equal(14, Score(expired, 0));
        Assert.Equal(12, Score(late, 0));
        var info = SaveGameService.Save(world, "Curse immunity");
        // Saving alone leaves the application's original suppression intact.
        Assert.Equal(14, Score(expired, 0));
        Assert.Equal(12, Score(late, 0));
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(server.World);
        Assert.Empty(loaded.Restored.Warnings);
        var expired2 = server.World.Get<Creature>(expired.Id)!;
        var late2 = server.World.Get<Creature>(late.Id)!;
        var bypass2 = server.World.Get<Creature>(bypass.Id)!;
        for (int a = 0; a < 6; a++)
        {
            Assert.Equal(12, Score(expired2, a));
            Assert.Equal(14, Score(late2, a));
            Assert.Equal(12, Score(bypass2, a));
        }
        Assert.Equal(hp - 10, expired2.MaxHitPoints);
        Assert.Equal(hp - 10, expired2.CurrentHitPoints);
        Assert.Equal(hp, late2.MaxHitPoints);
        Assert.Equal(hp, late2.CurrentHitPoints);
        Assert.All(Assert.Single(expired2.Effects).Ints.Take(6), value => Assert.Equal(2, value));
    }

    [DataFact]
    public void QuickSaveOverwritesItsSlotAndManualSavesGetNewSlots()
    {
        var (world, server, client, _) = StartGame();
        var q1 = SaveGameService.Save(world, "", SaveSlotKind.Quick);
        Run(server, client, 0.5);
        var q2 = SaveGameService.Save(world, "", SaveSlotKind.Quick);
        Assert.Equal("000000 - QUICKSAVE", q1.Folder);
        Assert.Equal(q1.Folder, q2.Folder);
        var a = SaveGameService.Save(world, "First");
        var b = SaveGameService.Save(world, "Second");
        var a2 = SaveGameService.Save(world, "first");
        Assert.Equal("000002 - First", a.Folder);
        Assert.Equal("000003 - Second", b.Folder);
        Assert.Equal(a.Folder, a2.Folder);
        var saves = Directory.GetDirectories(Path.Combine(_root, "saves")).Select(Path.GetFileName).ToList();
        Assert.Equal(3, saves.Count);
        Assert.Equal(3, SaveGameService.List(world).Count);

        // DoSinglePlayerAutoSave saves after the tick, into the autosave slot.
        world.Scripts!.Run("sg_autosave", world.Module.Id);
        Assert.False(Directory.Exists(Path.Combine(_root, "saves", "000001 - AUTOSAVE")));
        Run(server, client, 0.1);
        Assert.True(File.Exists(Path.Combine(_root, "saves", "000001 - AUTOSAVE", "savegame.sav")));
        Assert.Contains(SaveGameService.List(world), s => s.Kind == SaveSlotKind.Auto);

        // Requests from the client go through the server between ticks.
        client.Send(new ClientRequestMessage { Kind = "quickload", Json = "" }.Write());
        var old = server.World;
        Run(server, client, 0.1);
        Assert.NotSame(old, server.World);
        _worlds.Add(server.World);
    }

    [DataFact]
    public void SavesCarryModulePathPlayTimeAndThumbnailAndLoadAtLogin()
    {
        var (world, server, client, _) = StartGame();
        server.Options.ModulePath = TestData.Config!.Module;
        Run(server, client, 2);
        var tga = new byte[18 + 4 * 4 * 3];
        tga[2] = 2; tga[12] = 4; tga[14] = 4; tga[16] = 24;
        var info = SaveGameService.Save(world, "Menu test", screenshotTga: tga);
        Assert.True(info.PlayedSeconds >= 2);
        // The main menu reads the saves folder without a running world.
        var listed = SaveGameService.List(Path.Combine(_root, "saves")).Single(s => s.Folder == info.Folder);
        Assert.Equal(TestData.Config.Module, listed.ModulePath);
        Assert.True(listed.HasScreen);
        Assert.Equal(info.PlayedSeconds, listed.PlayedSeconds, 3);
        Assert.Equal(tga, SaveGameService.ReadScreen(Path.Combine(_root, "saves"), info.Folder));
        Assert.Null(SaveGameService.ReadScreen(Path.Combine(_root, "saves"), "../escape"));

        // A loaded game keeps counting from the save's play time.
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(loaded.World);
        Run(server, client, 1);
        Assert.True(SaveGameService.PlayedSeconds(server.World) >= info.PlayedSeconds + 0.9);

        // Load Game from the main menu: a new server loads the save when the player logs in (no character list).
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config.Module);
        rm.AddFirst(new DirectorySource(ScriptsDir()));
        var fresh = new World(rm);
        _worlds.Add(fresh);
        fresh.EnableScripts();
        fresh.System<PersistenceSystem>().RootOverride = _root;
        fresh.LoadModule();
        var server2 = new GameServer(fresh, new ServerOptions { SaveGames = true, CharacterSelection = true, Vault = VaultMode.Local, LoadOnLogin = info.Folder });
        var (client2, end2) = LoopbackConnection.CreatePair();
        server2.Accept(end2);
        client2.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server2.Tick(1f / GameServer.TickRate);
        server2.Tick(1f / GameServer.TickRate);
        var messages = Drain(client2);
        Assert.DoesNotContain(messages, m => m.Type == MessageType.CharacterList);
        Assert.Contains(messages, m => m.Type == MessageType.LoadArea);
        Assert.NotNull(server2.Sessions[0].Creature);
        Assert.NotSame(fresh, server2.World);
        _worlds.Add(server2.World);
        // LoadScreen travels with LoadArea.
        var load = LoadAreaMessage.Read(new MessageReader(messages.First(m => m.Type == MessageType.LoadArea).Data));
        Assert.Equal((int)server2.World.Areas.First(a => a.ResRef == load.ResRef).Are.GetInt("LoadScreenID"), load.LoadScreen);
    }

    /// <summary>Lists differing JSON paths.</summary>
    static void Diff(string path, JsonNode? a, JsonNode? b, List<string> diffs)
    {
        if (diffs.Count > 200) return;
        switch (a, b)
        {
            case (null, null): return;
            case (JsonObject oa, JsonObject ob):
                foreach (var (k, v) in oa)
                    if (!ob.ContainsKey(k)) diffs.Add($"{path}.{k} missing after load");
                    else Diff($"{path}.{k}", v, ob[k], diffs);
                foreach (var (k, _) in ob)
                    if (!oa.ContainsKey(k)) diffs.Add($"{path}.{k} only after load");
                return;
            case (JsonArray xa, JsonArray xb):
                if (xa.Count != xb.Count) { diffs.Add($"{path}: {xa.Count} items before, {xb.Count} after"); return; }
                for (int i = 0; i < xa.Count; i++) Diff($"{path}[{i}]", xa[i], xb[i], diffs);
                return;
            default:
                if (!JsonNode.DeepEquals(a, b)) diffs.Add($"{path}: {Short(a)} != {Short(b)}");
                return;
        }
    }

    static string Short(JsonNode? n)
    {
        var s = n?.ToJsonString() ?? "null";
        return s.Length > 120 ? s[..120] + "..." : s;
    }
}
