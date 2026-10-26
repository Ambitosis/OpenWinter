using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Conversations;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Rules;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>
/// Object serialization for every type with its state (ObjectToJson, campaign objects, the combined area format),
/// zstd buffers, the original's save games, and save/load round trips (docs/SAVEGAME.md).
/// </summary>
public sealed class SavePersistenceTests : IDisposable
{
    readonly ITestOutputHelper _out;
    readonly string _root = Path.Combine(Path.GetTempPath(), "openwinter-savepersist-" + Guid.NewGuid().ToString("N"));
    readonly List<World> _worlds = new();

    public SavePersistenceTests(ITestOutputHelper output)
    {
        _out = output;
        SaveGameService.Log = TestLog.To(output);
    }

    public void Dispose()
    {
        foreach (var w in _worlds) w.System<PersistenceSystem>().Dispose();
        SaveGameService.Log = Console.WriteLine;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
    }

    static string PreludePath => Path.Combine(TestData.Paths.GameRoot, "data", "nwm", "Prelude.nwm");

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OpenWinter.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }

    World Prelude()
    {
        var world = new World(ResMan.ForModule(TestData.Paths, PreludePath));
        _worlds.Add(world);
        world.EnableScripts();
        world.Scripts!.Log = _out.WriteLine;
        var persistence = world.System<PersistenceSystem>();
        persistence.RootOverride = _root;
        persistence.Log = _out.WriteLine;
        world.LoadModule();
        return world;
    }

    int RunScript(World world, string source, GameObject self)
    {
        var name = "t" + Guid.NewGuid().ToString("N")[..10];
        var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, name, world.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        world.Scripts.Register(name, new NcsProgram(name, bytes!));
        return world.Scripts.Run(name, self.Id);
    }

    // ------------------------------------------------------------------ compressed buffers

    [Fact]
    public void ZstdBuffersUseTheOriginalsLayout()
    {
        var data = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("persistence ", 400)));
        var packed = CompressedBuffer.Wrap("CPDB", data);
        // Magic, header version 3, algorithm 2 (zstd), size; then sub-header version 1, dictionary 0 and the zstd frame
        // (as the original's campaign databases hold them: parity suite persist_objects).
        Assert.Equal("CPDB", Encoding.ASCII.GetString(packed, 0, 4));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(4)));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(8)));
        Assert.Equal((uint)data.Length, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(12)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(16)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(20)));
        Assert.Equal(0xFD2FB528u, BinaryPrimitives.ReadUInt32LittleEndian(packed.AsSpan(24)));
        Assert.True(packed.Length < data.Length / 4);
        Assert.Equal(data, CompressedBuffer.Unwrap("CPDB", packed));
        Assert.Throws<InvalidDataException>(() => CompressedBuffer.Unwrap("SQL3", packed));

        // Uncompressed and zlib buffers still read.
        Assert.Equal(data, CompressedBuffer.Unwrap("CPDB", CompressedBuffer.Wrap("CPDB", data, CompressedBuffer.AlgorithmNone)));
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true)) z.Write(data);
        var zlib = new byte[20 + ms.Length];
        Encoding.ASCII.GetBytes("CPDB").CopyTo(zlib, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(zlib.AsSpan(4), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(zlib.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(zlib.AsSpan(12), (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(zlib.AsSpan(16), 1);
        ms.ToArray().CopyTo(zlib, 20);
        Assert.Equal(data, CompressedBuffer.Unwrap("CPDB", zlib));

        // A damaged frame is reported, not returned.
        var broken = (byte[])packed.Clone();
        for (int i = 30; i < broken.Length; i++) broken[i] ^= 0x33;
        Assert.Throws<InvalidDataException>(() => CompressedBuffer.Unwrap("CPDB", broken));
    }

    // ------------------------------------------------------------------ objects

    [DataFact]
    public void EveryObjectTypeRoundTripsWithItsState()
    {
        var world = Prelude();
        var area = world.Areas.First(a => a.ObjectsOf<Door>().Any());
        var door = area.ObjectsOf<Door>().First();
        var waypoint = world.AllObjects.OfType<Waypoint>().First(w => w.Area == area);
        var trigger = world.AllObjects.OfType<Trigger>().FirstOrDefault(t => t.Area == area) ?? world.AllObjects.OfType<Trigger>().First();
        var at = new Location(area.Id, area.Objects.OfType<Waypoint>().First().Position, 90);
        var store = (Store)ObjectSerializer.Deserialize(world, world.Resources.ReadGff("nw_storgenral001", ResType.Utm)!, "UTM", at, null)!;
        var encounter = (Encounter)ObjectSerializer.Deserialize(world, world.Resources.ReadGff("nw_goblingrp", ResType.Ute)!, "UTE", at, null)!;
        var sound = (Sound)ObjectSerializer.Deserialize(world, world.Resources.ReadGff("bellrings", ResType.Uts)!, "UTS", at, null)!;
        store.Gold = 4321;
        world.Inventory.Load(store);
        int stock = store.Inventory.Count;
        Assert.True(stock > 0);
        door.Locked = true;
        trigger.TransitionTargetId = waypoint.Id;
        trigger.TransitionResolved = true;

        foreach (GameObject o in new GameObject[] { door, waypoint, trigger, store, encounter, sound })
        {
            o.Locals.Ints["pi"] = 7;
            o.Locals.Jsons["pj"] = "[1,2]";
            o.Locals.Locations["pl"] = new Location(area.Id, new Vector3(1, 2, 0), 45);
            foreach (bool state in new[] { false, true })
            {
                var gff = ObjectSerializer.Serialize(world, o, state)!;
                Assert.Equal(state, gff.Has("VarTable"));
                Assert.Equal(state, gff.Has("ActionList"));
                Assert.Equal(state, gff.Has("Transition"));
                Assert.False(gff.Has("EffectList"));
                // Through json, as ObjectToJson / JsonToObject do.
                var json = GffJson.ToJson(gff, ObjectSerializer.FileType(o));
                var back = GffJson.FromJson(json, out var type);
                var target = new Location(area.Id, o.Position + new Vector3(2, 0, 0), 0);
                var copy = ObjectSerializer.Deserialize(world, back, type, target, null, state)!;
                Assert.NotNull(copy);
                Assert.NotSame(o, copy);
                Assert.Equal(o.GetType(), copy.GetType());
                Assert.Equal(o.Tag, copy.Tag);
                Assert.Same(area, copy.Area);
                Assert.Equal(state ? 7 : 0, copy.Locals.Ints.GetValueOrDefault("pi"));
                Assert.Equal(state ? "[1,2]" : null, copy.Locals.Jsons.GetValue("pj") is { } j ? JsonCore.Dump(j.Node) : null);
                Assert.Equal(state ? area.Id : (uint?)null, copy.Locals.Locations.TryGetValue("pl", out var l) ? l.Area : null);
                switch (copy)
                {
                    case Door d: Assert.True(d.Locked); break;
                    case Trigger t:
                        Assert.Equal(trigger.Geometry.Count, t.Geometry.Count);
                        Assert.Equal(state ? waypoint.Id : GameObject.InvalidId, t.TransitionTargetId);
                        break;
                    case Store s:
                        world.Inventory.Load(s);
                        Assert.Equal(4321, s.Gold);
                        Assert.Equal(stock, s.Inventory.Count);
                        break;
                    case Encounter e: Assert.Equal(encounter.State.Creatures.Count, e.State.Creatures.Count); break;
                    case Sound snd: Assert.Equal(sound.Sounds, snd.Sounds); break;
                }
            }
        }

        // Campaign objects: stored zstd-compressed (compressed 1), read back; areas can't be stored.
        world.Module.Locals.Objects["door"] = door.Id;
        world.Module.Locals.Objects["area"] = area.Id;
        RunScript(world, """
            void main()
            {
                object m = GetModule();
                object d = GetLocalObject(m, "door");
                SetLocalInt(m, "stored", StoreCampaignObject("tcamp", "door", d, OBJECT_INVALID, TRUE));
                SetLocalInt(m, "stored_area", StoreCampaignObject("tcamp", "area", GetLocalObject(m, "area")));
                object b = RetrieveCampaignObject("tcamp", "door", GetLocation(d), OBJECT_INVALID, OBJECT_INVALID, TRUE);
                SetLocalObject(m, "back", b);
            }
            """, world.Module);
        Assert.Equal(1, world.Module.Locals.Ints["stored"]);
        Assert.Equal(0, world.Module.Locals.Ints["stored_area"]);
        var retrieved = world.Get<Door>(world.Module.Locals.Objects["back"]);
        Assert.NotNull(retrieved);
        Assert.Equal(7, retrieved!.Locals.Ints["pi"]);
        var campaign = world.System<PersistenceSystem>().Campaign("tcamp")!;
        using var q = campaign.PrepareInternal("SELECT vartype, compressed, substr(payload, 1, 4) FROM db WHERE varname = 'door'");
        Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
        Assert.Equal('O', (char)q.Int(0));
        Assert.Equal(1, q.Int(1));
        Assert.Equal("CPDB", Encoding.ASCII.GetString(q.Blob(2)));
    }

    [DataFact]
    public void CreatureStateCarriesEffectsActionsAndLocals()
    {
        var world = Prelude();
        var area = world.Areas.First(a => a.ObjectsOf<Creature>().Any());
        var npc = area.ObjectsOf<Creature>().First(c => !c.IsDead);
        var k = EffectConstants.Of(world);
        var effects = world.System<EffectSystem>();
        effects.Apply(new Effect { Type = k["EFFECT_TYPE_AC_INCREASE"], SubType = k.Magical, Ints = new[] { 2, 0, 4103, 0, 0, 0, 0, 0 } }, k.Temporary, npc, 600);
        effects.Apply(new Effect { Type = k["EFFECT_TYPE_ABILITY_INCREASE"], SubType = k.Supernatural, Ints = new[] { 0, 2, 0, 0, 0, 0, 0, 0 }, Tag = "fx_tag" }, k.Permanent, npc, 0);
        npc.Actions.Clear();
        npc.Actions.Add(new WaitAction { Remaining = 45 });
        npc.Locals.Strings["ps"] = "hi";
        world.Step(1f);
        Assert.Equal(2, npc.Effects.Count(e => !e.Hidden));

        var gff = ObjectSerializer.Serialize(world, npc, saveObjectState: true)!;
        var list = gff.GetList("EffectList");
        Assert.Equal(2, list.Count);
        // The original's numbers and layout (persist_effects: AC increase is 48 with the subtype ORed with the duration type).
        var ac = list.Single(e => e.GetInt("Type") == 48);
        Assert.Equal(k.Magical | k.Temporary, ac.GetInt("SubType"));
        Assert.Equal(600f, ac.GetFloat("Duration"));
        Assert.Equal(new[] { 0, 2, 28, 0, 0, 4103, 0, 0 }, ac.GetList("IntList").Select(i => (int)i.GetInt("Value")).ToArray());
        Assert.Equal(30u, (uint)gff.GetList("ActionList").Single().GetInt("ActionId"));

        var at = new Location(area.Id, npc.Position + new Vector3(1, 0, 0), 0);
        var copy = (Creature)ObjectSerializer.Deserialize(world, gff.DeepClone(), "UTC", at, null, loadObjectState: true)!;
        Assert.Equal("hi", copy.Locals.Strings["ps"]);
        var restored = copy.Effects.Where(e => !e.Hidden).ToList();
        Assert.Equal(2, restored.Count);
        var acCopy = restored.Single(e => e.Type == k["EFFECT_TYPE_AC_INCREASE"]);
        Assert.InRange(acCopy.ExpiresAt - world.Time, 595, 600);
        Assert.Equal(2, acCopy.Ints[0]);
        Assert.Equal("fx_tag", restored.Single(e => e.Type == k["EFFECT_TYPE_ABILITY_INCREASE"]).Tag);
        Assert.IsType<WaitAction>(Assert.Single(copy.Actions));

        // The original's entries (no OpenWinter state) map through the measured table.
        foreach (var e in list) e.Remove(NativeEffects.OpenWinterField);
        var plain = (Creature)ObjectSerializer.Deserialize(world, gff, "UTC", at, null, loadObjectState: true)!;
        var plainAc = plain.Effects.Single(e => e.Type == k["EFFECT_TYPE_AC_INCREASE"]);
        Assert.Equal(new[] { 2, 0, 4103 }, plainAc.Ints[..3]);
        Assert.Equal(-1, plainAc.VersusRace);
        Assert.Equal(k.Supernatural, plain.Effects.Single(e => e.Type == k["EFFECT_TYPE_ABILITY_INCREASE"]).SubType);

        // Without the state, nothing of it comes back.
        var bare = (Creature)ObjectSerializer.Deserialize(world, ObjectSerializer.Serialize(world, npc)!, "UTC", at, null)!;
        Assert.Empty(bare.Effects.Where(e => !e.Hidden));
        Assert.Empty(bare.Actions);
        Assert.False(bare.Locals.Strings.ContainsKey("ps"));
    }

    [DataFact]
    public void CombinedAreaFormatMakesANewArea()
    {
        var world = Prelude();
        var area = world.Areas.First(a => a.ObjectsOf<Door>().Any());
        area.Locals.Ints["ai"] = 3;
        var door = area.ObjectsOf<Door>().First();
        door.Locals.Ints["di"] = 4;
        var caf = ObjectSerializer.Serialize(world, area)!;
        Assert.Equal("CAF ", ObjectSerializer.FileType(area));
        Assert.True(caf.GetStruct("ARE")!.Has("Tile_List"));
        var git = caf.GetStruct("GIT")!;
        Assert.False(git.GetList("Door List").Any(d => d.Has("ObjectId")));
        Assert.Equal(3, git.GetList("VarTable").Single(v => v.GetString("Name") == "ai").GetInt("Value"));

        var json = GffJson.ToJson(caf, "CAF ");
        var back = GffJson.FromJson(json, out var type);
        Assert.Equal("CAF ", type);
        var copy = Assert.IsType<Area>(ObjectSerializer.Deserialize(world, back, type, Location.Invalid, null));
        Assert.NotSame(area, copy);
        Assert.StartsWith("nw_", copy.ResRef);
        Assert.Equal(area.Tag, copy.Tag);
        Assert.Contains(copy, world.Areas);
        Assert.Equal(3, copy.Locals.Ints["ai"]);
        Assert.Equal(area.Objects.Count(o => o is not Creature { IsPC: true }), copy.Objects.Count);
        Assert.Equal(4, copy.ObjectsOf<Door>().Single(d => d.Tag == door.Tag && d.Position == door.Position).Locals.Ints["di"]);
        Assert.True(copy.Walkmesh.TriangleCount > 0);
    }

    // ------------------------------------------------------------------ the original's save games

    /// <summary>
    /// A save made by the original (parity scene nativesave, saved with DoSinglePlayerAutoSave in a sandboxed nwmain;
    /// the run's copy is kept in out/visual/shots/vis_nativesave/nwn/saves). Its notes.txt records what it holds.
    /// </summary>
    [DataFact]
    public void LoadsTheOriginalsSaveGame()
    {
        var saved = Path.Combine(RepoRoot(), "out", "visual", "shots", "vis_nativesave", "nwn", "saves", "000001 - Auto Save");
        if (NativeSaveGame.Archive(saved) == null) { _out.WriteLine($"no save of the original at {saved}: run the nativesave scene"); return; }
        var savesDir = Path.Combine(_root, "saves");
        var slot = Path.Combine(savesDir, "000001 - Auto Save");
        Directory.CreateDirectory(slot);
        foreach (var f in Directory.GetFiles(saved)) File.Copy(f, Path.Combine(slot, Path.GetFileName(f)));

        var archive = NativeSaveGame.Archive(slot)!;
        var current = new World(ResMan.ForModule(TestData.Paths, archive));
        _worlds.Add(current);
        current.EnableScripts();
        current.System<PersistenceSystem>().RootOverride = _root;
        current.LoadModule();

        // It is listed beside OpenWinter's own saves.
        var info = Assert.Single(SaveGameService.List(savesDir));
        Assert.Equal("Auto Save", info.Name);
        Assert.Equal(SaveSlotKind.Auto, info.Kind);
        Assert.Equal("Aluvian Darkstar", info.PlayerName);
        Assert.Equal("visual", info.AreaResRef);

        var loaded = SaveGameService.Load(current, "000001 - Auto Save");
        var world = loaded.World;
        _worlds.Add(world);
        foreach (var w in loaded.Restored.Warnings) _out.WriteLine("warning: " + w);
        var m = world.Module;

        // The clock (notes: time|6:0|1372-6-2) and the module's variables, an object reference among them.
        Assert.Equal((1372, 6, 2, 6), (world.Clock.Year, world.Clock.Month, world.Clock.Day, world.Clock.Hour));
        Assert.Equal(11, m.Locals.Ints["ns_mod_i"]);
        Assert.Equal("module", m.Locals.Strings["ns_mod_s"]);
        Assert.Equal(2.5f, m.Locals.Floats["ns_mod_f"]);
        Assert.Equal("{\"a\":[1,2]}", JsonCore.Dump(m.Locals.Jsons.GetValue("ns_mod_j")!.Node));
        var (pc, _) = Assert.Single(loaded.Restored.Players);
        Assert.Equal(pc.Id, m.Locals.Objects["ns_mod_o"]);
        var area = Assert.Single(world.Areas);
        Assert.Equal(area.Id, m.Locals.Locations["ns_mod_l"].Area);
        Assert.Equal(22, area.Locals.Ints["ns_area_i"]);

        // The player (notes: pc|18.00,21.00|270.0|hp=5/5|gold=1309|xp=500): position, gold, experience, items, the
        // permanent ability effect (the temporary ones ran out when the scene moved the clock on), variables, journal.
        Assert.Same(area, pc.Area);
        Assert.Equal(18, pc.Position.X, 2);
        Assert.Equal(21, pc.Position.Y, 2);
        Assert.Equal(1309, pc.Gold);
        Assert.Equal(500, pc.Experience);
        Assert.Equal(5, pc.CurrentHitPoints);
        Assert.Contains(pc.Inventory, i => i.Tag == "NS_RING" && i.Locals.Ints.GetValueOrDefault("ns_item_i") == 44);
        Assert.Contains(pc.Inventory, i => i.Tag == "NS_POT" && i.StackSize == 3);
        var k = EffectConstants.Of(world);
        var str = Assert.Single(pc.Effects, e => e.Type == k["EFFECT_TYPE_ABILITY_INCREASE"]);
        Assert.Equal(k.Permanent, str.DurationType);
        Assert.Equal(33, pc.Locals.Ints["ns_pc_i"]);
        Assert.Equal(2, pc.Journal["q_ns"].State);

        // Objects: the NPC (hp=9/12, action=36), the open door, the locked door, the chest, the store, the ground
        // item, the trigger's transition to the waypoint.
        var npc = world.AllObjects.OfType<Creature>().Single(c => c.Tag == "NS_NPC");
        Assert.Equal(55, npc.Locals.Ints["ns_npc_i"]);
        Assert.IsType<WaitAction>(Assert.Single(npc.Actions));
        Assert.True(world.AllObjects.OfType<Door>().Single(d => d.Tag == "NS_DOOR").IsOpen);
        Assert.Equal(77, world.AllObjects.OfType<Door>().Single(d => d.Tag == "NS_DOOR").Locals.Ints["ns_door_i"]);
        Assert.True(world.AllObjects.OfType<Door>().Single(d => d.Tag == "NS_DOOR2").Locked);
        var chest = world.AllObjects.OfType<Placeable>().Single(p => p.Tag == "NS_CHEST");
        world.Inventory.Load(chest);
        Assert.Equal(66, chest.Locals.Ints["ns_plc_i"]);
        Assert.Equal(2, Assert.Single(chest.Inventory).StackSize);
        var store = world.AllObjects.OfType<Store>().Single();
        world.Inventory.Load(store);
        Assert.Equal(4321, store.Gold);
        Assert.Contains(world.AllObjects.OfType<Item>(), i => i.Tag == "NS_GROUND" && i.Area == area);
        var wp = world.AllObjects.OfType<Waypoint>().Single(w => w.Tag == "NS_WP");
        Assert.Equal(wp.Id, world.AllObjects.OfType<Trigger>().Single(t => t.Tag == "NS_TRIG").TransitionTargetId);

        // The module database, and the pending DelayCommand(90.0, ...) that runs in the loaded world.
        using (var q = world.System<PersistenceSystem>().ModuleDb(m).PrepareInternal("SELECT v FROM ns WHERE k = 'saved'"))
        {
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal(88, q.Int(0));
        }
        Assert.Equal(3, world.Scripts!.PendingCommands);
        Assert.False(m.Locals.Ints.ContainsKey("ns_delayed"));
        for (int i = 0; i < 1000; i++) world.Step(0.1f);
        Assert.Equal(1, m.Locals.Ints["ns_delayed"]);
        Assert.Equal(9, npc.CurrentHitPoints);
        Assert.Equal(0, world.Scripts.PendingCommands);
    }

    // ------------------------------------------------------------------ OpenWinter's saves

    sealed record Session(World World, GameServer Server, LoopbackConnection Client, Creature Pc);

    Session StartPrelude()
    {
        var world = Prelude();
        var server = new GameServer(world, new ServerOptions { SaveGames = true, ModulePath = PreludePath });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        for (int i = 0; i < 10; i++) { server.Tick(1f / GameServer.TickRate); Drain(client); }
        var pc = server.Sessions[0].Creature!;
        return new Session(world, server, client, pc);
    }

    static List<(MessageType Type, byte[] Data)> Drain(LoopbackConnection client)
    {
        var list = new List<(MessageType, byte[])>();
        while (client.TryReceive(out var m)) list.Add((new MessageReader(m).Type, m));
        return list;
    }

    [DataFact]
    public void ConversationSavedInProgressIsShownAgainAfterLoading()
    {
        var (world, server, client, pc) = StartPrelude();
        var convo = world.System<ConversationService>();
        // A talker whose conversation opens a window (not a one-liner), with the player beside it.
        var npc = world.AllObjects.OfType<Creature>().First(c => !c.IsPC && !c.IsDead && c.Area == pc.Area && c.Conversation.Length > 0
            && world.Resources.Contains(c.Conversation, ResType.Dlg) && convo.Start(c, pc, c.Conversation) && convo.IsInConversation(pc));
        convo.Stop(pc);
        world.AddToArea(pc, npc.Area!, npc.Position + new Vector3(1, 0, 0), 0, exact: true, enterEvent: false);
        Drain(client);
        Assert.True(convo.Start(npc, pc, npc.Conversation));
        server.Tick(1f / GameServer.TickRate);
        Assert.Contains(Drain(client), m => m.Type == MessageType.Dialog);
        Assert.True(convo.IsInConversation(pc));

        // A script autosave (the player can't save in a conversation, a script can).
        var info = SaveGameService.Save(world, "", SaveSlotKind.Auto);
        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(loaded.World);
        var messages = Drain(client);
        Assert.Contains(messages, m => m.Type == MessageType.LoadArea);
        var dialog = messages.Where(m => m.Type == MessageType.Dialog).Select(m => DialogMessage.Read(new MessageReader(m.Data))).ToList();
        var shown = Assert.Single(dialog);
        Assert.False(shown.Ended);
        var newPc = server.Sessions[0].Creature!;
        Assert.True(loaded.World.System<ConversationService>().IsInConversation(newPc));
    }

    [DataFact]
    public void SaveFromAnEarlierBuildStillLoads()
    {
        // Fields a later build no longer has, or doesn't have yet, are skipped with a warning (a
        // "Senior Barracks" save from an earlier build carried EncounterState fields since removed).
        var (world, server, client, pc) = StartPrelude();
        pc.Locals.Ints["kept"] = 5;
        var info = SaveGameService.Save(world, "Older");
        var path = Path.Combine(_root, "saves", info.Folder, SaveGameService.SaveFile);
        var entries = Erf.Read(File.ReadAllBytes(path), out var erfType);
        int index = entries.FindIndex(e => e.ResRef == SaveGameService.SnapshotResRef);
        var g = Gff.Read(entries[index].Data);
        JsonObject snap;
        using (var gz = new GZipStream(new MemoryStream(g.GetBytes("Snapshot")), CompressionMode.Decompress))
            snap = JsonNode.Parse(gz, documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 4096 })!.AsObject();
        foreach (var o in snap["objects"]!.AsArray().OfType<JsonObject>())
            o["f"]!.AsObject()["NoLongerAField"] = 1;
        foreach (var s in snap["systems"]!.AsArray().OfType<JsonObject>())
            s["fields"]!.AsObject()["_retired"] = new JsonArray(1, 2);
        snap["systems"]!.AsArray().Add(new JsonObject { ["type"] = "OpenWinter.Game.NoSuchSystem", ["fields"] = new JsonObject() });
        var json = Encoding.UTF8.GetBytes(snap.ToJsonString());
        using var packed = new MemoryStream();
        using (var gz = new GZipStream(packed, CompressionLevel.Fastest, leaveOpen: true)) gz.Write(json);
        g.Set("Snapshot", GffType.Void, packed.ToArray());
        entries[index] = new ErfEntry(entries[index].ResRef, entries[index].ResType, Gff.Write(g, "ELYS"));
        File.WriteAllBytes(path, Erf.Write(entries, erfType));

        var loaded = SaveGameRequests.LoadInto(server, info.Folder);
        _worlds.Add(loaded.World);
        Assert.Contains(loaded.Restored.Warnings, w => w.Contains("NoLongerAField no longer exists"));
        Assert.Contains(loaded.Restored.Warnings, w => w.Contains("NoSuchSystem no longer exists"));
        Assert.Equal(5, server.Sessions[0].Creature!.Locals.Ints["kept"]);
    }
}
