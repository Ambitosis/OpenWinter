using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;
using JsonValue = OpenWinter.Game.Scripting.JsonValue;

namespace OpenWinter.Tests;

public class PlayerTests
{
    readonly ITestOutputHelper _out;
    public PlayerTests(ITestOutputHelper output) { _out = output; }

    /// <summary>Records notifies instead of sending them.</summary>
    sealed class RecordingOutput : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, string Json, uint Player)> Notifies = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notifies.Add((kind, subject?.Id ?? GameObject.InvalidId, System.Text.Json.JsonSerializer.Serialize(data), player?.Id ?? GameObject.InvalidId));
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    (World World, RecordingOutput Output, List<string> Log) MakeWorld(bool withModule = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        if (withModule) rm.Add(new DirectorySource(TestData.Config!.Module));
        rm.AddBaseGame();
        var world = new World(rm);
        var output = new RecordingOutput();
        world.Output = output;
        world.EnableScripts();
        world.Register(world.Module);
        var log = new List<string>();
        world.Scripts!.Log = s => { log.Add(s); _out.WriteLine(s); };
        world.System<NwnxRegistry>().Log = s => { log.Add(s); _out.WriteLine(s); };
        return (world, output, log);
    }

    static Creature MakePc(World world)
    {
        var (_, server) = LoopbackConnection.CreatePair();
        var pc = new Creature { Tag = "pc" };
        pc.Player = new PlayerSession { Connection = server, PlayerName = "tester" };
        world.Register(pc);
        return pc;
    }

    [DataFact]
    public void TwoDaMetaAndJumps()
    {
        var (world, _, log) = MakeWorld();
        world.Scripts!.Run("player_basic", world.Module.Id);
        var v = world.Module.Locals;

        Assert.Equal("Barbarian", v.Strings["2da_label"]);
        Assert.Equal("12", v.Strings["2da_hitdie"]);
        Assert.Equal("|", v.Strings["2da_missing"]);
        Assert.Equal("label", v.Strings["2da_col0"]); // lower case, as the original reports column names
        Assert.Equal("", v.Strings["2da_colbad"]);
        Assert.True(v.Ints["2da_rows"] > 10);

        var name = v.Strings["name"];
        _out.WriteLine($"random name: {name}");
        Assert.Matches("^[A-Z][a-z'-]{2,14}$", name);
        Assert.Equal(3.048f, v.Floats["feet"], 3);

        Assert.Equal(1, v.Ints["evt_set"]);
        Assert.Equal("my_hb", v.Strings["evt_get"]);
        Assert.Equal("my_hb", world.Module.Script("Mod_OnHeartbeat"));
        Assert.Equal(0, v.Ints["evt_bad"]);

        Assert.Equal("player_basic", v.Strings["script_name"]);
        Assert.Equal(0, v.Ints["recursion"]);
        Assert.Equal(1, v.Ints["instr_ok"]);

        // LongJmp from inside a nested call lands back at SetJmp with the value.
        Assert.Equal(1, v.Ints["jmp_valid"]);
        Assert.False(v.Ints.ContainsKey("jmp_notreached"));
        Assert.Equal(42, v.Ints["jmp_result"]);

        // AbortRunningScript stops silently.
        Assert.Equal(1, v.Ints["done"]);
        Assert.False(v.Ints.ContainsKey("after_abort"));
        Assert.DoesNotContain(log, l => l.Contains("player_basic") && l.Contains("[script"));
    }

    [DataFact]
    public void HashStringMatchesCompilerCaseHashes()
    {
        var (world, _, _) = MakeWorld();
        world.Scripts!.Run("player_hash", world.Module.Id);
        var v = world.Module.Locals;
        Assert.Equal(0, v.Ints["hash_empty"]);
        Assert.Equal(2, v.Ints["hash_switch"]); // the compiler hashed "BBB" at compile time
    }

    [DataFact]
    public void RandomNamesArePlausible()
    {
        var rm = TestData.Module;
        foreach (int type in new[] { -1, 0, 2, 5, 13, 20, 21, 22 })
        {
            var names = Enumerable.Range(0, 20).Select(_ => MiscFunctions.RandomName(rm, type)).ToList();
            _out.WriteLine($"{type}: {string.Join(", ", names)}");
            Assert.All(names, n => Assert.Matches("^[A-Z][a-z'-]{2,14}$", n));
            Assert.True(names.Distinct().Count() > 10);
        }
        var ltr = Ltr.Parse(rm.Read("humanm", ResType.Ltr)!);
        Assert.Equal(28, ltr.LetterCount);
    }

    [DataFact]
    public void JsonPatchDiffAndArrays()
    {
        var (world, _, _) = MakeWorld();
        world.Scripts!.Run("player_json", world.Module.Id);
        var v = world.Module.Locals.Strings;
        _out.WriteLine(v["diff"]);
        Assert.True(JsonExtraFunctions.Equal(JsonNode.Parse(v["b"]), JsonNode.Parse(v["patched"])));
        Assert.Equal("{\"b\":[1,2,3],\"c\":{\"d\":\"x\"},\"x\":5,\"z\":1}", Canonical(v["patch2"]));
        Assert.Equal(0, world.Module.Locals.Ints["patch_bad_type"]);
        Assert.Equal("[json.exception.other_error.501] unsuccessful: {\"op\":\"test\",\"path\":\"/a\",\"value\":99}", v["patch_bad_err"]); // as the original reports it
        Assert.Equal("[null,true,1,1,3,99,\"100\"]", v["sorted"]);
        Assert.Equal("[1,\"1\",2]", v["unique"]);
        Assert.Equal("[3,2,1]", v["reverse"]);
        Assert.Equal("\"\"", v["coalesce"]);
        Assert.Equal("[1,2,3,4][0,1,2,3,4][][1]", v["range"]);
        Assert.Equal("[1,2,3,4][2,3][1][1,4]false", v["setops"]); // SUBSET asks whether the second set is within the first
        Assert.Equal("{\"k\":7}[0,1,2]", v["inplace"]);
    }

    static string Canonical(string json)
    {
        var o = (JsonObject)JsonNode.Parse(json)!;
        var sorted = new JsonObject();
        foreach (var kv in o.OrderBy(kv => kv.Key, StringComparer.Ordinal)) sorted[kv.Key] = kv.Value?.DeepClone();
        return sorted.ToJsonString();
    }

    [DataFact]
    public void NwnxStackAndPlugins()
    {
        var (world, _, _) = MakeWorld();
        var c = new Creature { Tag = "npc" };
        world.Register(c);
        world.Module.Locals.Objects["creature"] = c.Id;
        world.Scripts!.Run("player_nwnx", world.Module.Id);
        var v = world.Module.Locals;
        Assert.Equal(1, v.Ints["available"]);
        Assert.Contains(1001, c.Feats);
        Assert.Equal(1, v.Ints["knows"]);
        Assert.Equal(7, c.Skills[4]);
        Assert.Equal("red text", v.Strings["stripped"]);
        Assert.Equal(2.5f, v.Floats["generic"]);
        Assert.Equal("{\"x\":1}|2.0|5.5|five|5|0", v.Strings["stack"]);
        Assert.Equal(0, world.System<NwnxRegistry>().Stack.Count);
    }

    [DataFact]
    public void NwnxLegacyAbiRoundTrip()
    {
        var (world, _, _) = MakeWorld();
        var nwnx = world.System<NwnxRegistry>();
        var c = new Creature { Tag = "npc" };
        c.Abilities[0] = 12;
        world.Register(c);

        // NWNX_Creature_ModifyRawAbilityScore(c, ABILITY_STRENGTH, 4) through the ABIv2 local-variable hooks.
        Assert.True(nwnx.TryLegacyPush(GameObject.InvalidId, NwnxRegistry.LegacyPush, NwnxValue.Int(4)));
        Assert.True(nwnx.TryLegacyPush(GameObject.InvalidId, NwnxRegistry.LegacyPush, NwnxValue.Int(0)));
        Assert.True(nwnx.TryLegacyPush(GameObject.InvalidId, NwnxRegistry.LegacyPush, NwnxValue.Object(c.Id)));
        Assert.True(nwnx.TryLegacyCall("NWNXEE!ABIv2!NWNX_Creature!ModifyRawAbilityScore!CALL"));
        Assert.Equal(16, c.Abilities[0]);

        nwnx.TryLegacyPush(GameObject.InvalidId, NwnxRegistry.LegacyPush, NwnxValue.Int(0));
        nwnx.TryLegacyPush(GameObject.InvalidId, NwnxRegistry.LegacyPush, NwnxValue.Object(c.Id));
        nwnx.TryLegacyCall("NWNXEE!ABIv2!NWNX_Creature!GetRawAbilityScore!CALL");
        Assert.True(nwnx.TryLegacyPop(GameObject.InvalidId, NwnxRegistry.LegacyPop, NwnxType.Int, out var score));
        Assert.Equal(16, score);

        Assert.False(nwnx.TryLegacyCall("as_an_ambient_sound"));
        Assert.False(nwnx.TryLegacyPush(c.Id, NwnxRegistry.LegacyPush, NwnxValue.Int(1)));

        // Struct results come back in the order the wrapper pops them.
        var pc = MakePc(world);
        nwnx.Stack.Push(NwnxValue.Object(GameObject.InvalidId)); // oItem
        nwnx.Stack.Push(NwnxValue.Object(GameObject.InvalidId)); // oSecondaryItem
        nwnx.Stack.Push(NwnxValue.Int(2)); // nObjectType
        nwnx.Stack.Push(NwnxValue.Int(0)); // nMultiClass
        nwnx.Stack.Push(NwnxValue.String("nw_it_torch001")); // sResRef
        nwnx.Stack.Push(NwnxValue.String("label"));
        nwnx.Stack.Push(NwnxValue.String("line"));
        nwnx.Stack.Push(NwnxValue.String("tip"));
        nwnx.Stack.Push(NwnxValue.Int(11));
        nwnx.Stack.Push(NwnxValue.Int(0));
        nwnx.Stack.Push(NwnxValue.Int(0));
        nwnx.Stack.Push(NwnxValue.Int(0));
        nwnx.Stack.Push(NwnxValue.Object(GameObject.InvalidId)); // oAssociate
        nwnx.Stack.Push(NwnxValue.Int(5)); // slot
        nwnx.Stack.Push(NwnxValue.Object(pc.Id));
        nwnx.Call("NWNX_Player", "SetQuickBarSlot");
        Assert.Equal(0, nwnx.Stack.Count);
        nwnx.Stack.Push(NwnxValue.Int(5));
        nwnx.Stack.Push(NwnxValue.Object(pc.Id));
        nwnx.Call("NWNX_Player", "GetQuickBarSlot");
        Assert.Equal(GameObject.InvalidId, nwnx.PopAs(NwnxType.Object)); // oAssociate
        for (int i = 0; i < 3; i++) nwnx.PopAs(NwnxType.Int);
        Assert.Equal(11, nwnx.PopAs(NwnxType.Int)); // nINTParam1
        Assert.Equal("tip", nwnx.PopAs(NwnxType.String));
        nwnx.PopAs(NwnxType.String); nwnx.PopAs(NwnxType.String);
        Assert.Equal("nw_it_torch001", nwnx.PopAs(NwnxType.String));
    }

    [DataFact]
    public void NuiCreateBindAndEvents()
    {
        var (world, output, _) = MakeWorld();
        var pc = MakePc(world);
        world.Module.Locals.Objects["pc"] = pc.Id;
        world.Scripts!.Run("player_nui", world.Module.Id);
        var v = world.Module.Locals;

        int token = v.Ints["token"];
        Assert.True(token > 0);
        Assert.Equal(1, v.Ints["found"]);
        Assert.Equal("testwin", v.Strings["winid"]);
        Assert.Equal("title,text,geometry", v.Strings["binds"]);
        Assert.Equal("hello", v.Strings["text"]);
        Assert.Equal(1, v.Ints["watch_ok"]);
        Assert.Equal("text", v.Strings["watched"]);
        Assert.Equal("{\"u\":1}", v.Strings["userdata"]);
        Assert.Equal(1, v.Ints["nth"]);
        Assert.Equal(0, v.Ints["bad_create"]);

        // Setting a watched bind ran the window's event script synchronously.
        Assert.Equal(1, v.Ints["events"]);
        Assert.Equal("watch:text:-1:1:1:null:changed", v.Strings["event1"]);

        Assert.Contains(output.Notifies, n => n.Kind == "nui-open" && n.Player == pc.Id && n.Json.Contains("\"testwin\""));
        Assert.Equal(2, output.Notifies.Count(n => n.Kind == "nui-bind" && n.Player == pc.Id));

        // Client input: a click with payload, then a client-side bind change, then close.
        var nui = world.System<NuiService>();
        nui.HandleEvent(pc, token, "click", "ok", 3, new JsonValue(JsonNode.Parse("{\"mouse_btn\":0}")));
        Assert.Equal("click:ok:3:1:1:{\"mouse_btn\":0}", v.Strings["event2"]);
        nui.ClientSetBind(pc, token, "text", System.Text.Json.Nodes.JsonValue.Create("typed"));
        Assert.Equal("watch:text:-1:1:1:null:typed", v.Strings["event3"]);
        nui.ClientSetBind(pc, token, "text", System.Text.Json.Nodes.JsonValue.Create("typed"));
        Assert.Equal(3, v.Ints["events"]); // unchanged value: no watch event
        // The client names the row whose element it changed; the watch reports it as the array index.
        nui.ClientSetBind(pc, token, "text", System.Text.Json.Nodes.JsonValue.Create("row"), 2);
        Assert.Equal("watch:text:2:1:1:null:row", v.Strings["event4"]);
        nui.HandleEvent(pc, token, "close", "_window_", -1, null);
        Assert.Equal(5, v.Ints["events"]);
        Assert.Null(nui.Get(pc, token));

        // Re-creating a window with the same id replaces it.
        int t1 = nui.Create(pc, new JsonObject(), "", "dup", "");
        int t2 = nui.Create(pc, new JsonObject(), "", "dup", "");
        Assert.NotEqual(t1, t2);
        Assert.Null(nui.Get(pc, t1));
        Assert.Contains(output.Notifies, n => n.Kind == "nui-close" && n.Json.Contains($"\"token\":{t1}"));
    }

    [DataFact]
    public void JournalEntries()
    {
        var (world, output, _) = MakeWorld(withModule: true);
        var pc = MakePc(world);
        world.Module.Locals.Objects["pc"] = pc.Id;
        world.Scripts!.Run("player_journal", world.Module.Id);
        var v = world.Module.Locals.Ints;

        Assert.Equal(1, v["j1"]);
        Assert.Equal(3, v["j3"]);
        Assert.Equal(3, v["j_nolower"]);
        Assert.Equal(2, v["j_override"]);
        // An entry module.jrl doesn't have still sets the local and updates the journal (journalpc, journal_funcs).
        Assert.Equal(77, v["j_badstate"]);
        Assert.Equal(0, v["xp"]);
        Assert.Equal(0, v["j_removed"]);
        Assert.Empty(pc.Journal);

        var journal = output.Notifies.Where(n => n.Kind == "journal").ToList();
        Assert.Equal(5, journal.Count); // 1, 3, override to 2, unknown entry 77, removal
        Assert.All(journal, n => Assert.Contains("\"update\":true", n.Json));
        Assert.Contains("Stacy", journal[0].Json);
        Assert.Contains("\"removed\":true", journal[^1].Json);
        // Each update carries the earlier entries, newest first.
        Assert.Contains("\"history\":[]", journal[0].Json);
        var second = System.Text.Json.JsonDocument.Parse(journal[1].Json).RootElement.GetProperty("history");
        Assert.Equal(1, second.GetArrayLength());
        Assert.Equal(1, second[0].GetProperty("state").GetInt32());
        Assert.NotEqual("", second[0].GetProperty("text").GetString());
        var third = System.Text.Json.JsonDocument.Parse(journal[2].Json).RootElement.GetProperty("history");
        Assert.Equal(new[] { 3, 1 }, third.EnumerateArray().Select(e => e.GetProperty("state").GetInt32()).ToArray());

        var cats = world.System<JournalService>().Categories;
        Assert.True(cats.Count >= 10);
        Assert.True(cats["Q_SS_STACYRATS"].Entries[3].End);
    }

    /// <summary>Who a journal update reaches (journal_funcs on nwserver, journalpc on nwmain; docs/JOURNAL.md).</summary>
    [DataFact]
    public void JournalUpdatesReachTheMeasuredCreatures()
    {
        var (world, output, _) = MakeWorld(withModule: true);
        var pc = MakePc(world);
        var journal = world.System<JournalService>();
        const string q = "Q_SS_STACYRATS", key = JournalService.LocalPrefix + q;
        var hench = new Creature { Tag = "HENCH", Master = pc.Id };
        int notifies = output.Notifies.Count(n => n.Kind == "journal");

        // A henchman's update with bAllPartyMembers reaches only the henchman.
        journal.Add(q, 1, hench, true, false, false);
        Assert.Equal(1, hench.Locals.Ints[key]);
        Assert.False(pc.Locals.Ints.ContainsKey(key));
        // bAllPlayers reaches the players and not the creature named.
        var npc = new Creature { Tag = "NPC" };
        journal.Add(q, 2, npc, false, true, false);
        Assert.False(npc.Locals.Ints.ContainsKey(key));
        // A script-set local counts against bAllowOverrideHigher; quest tags are case-sensitive.
        pc.Locals.Ints[key] = 7;
        journal.Add(q, 3, pc, true, false, false);
        Assert.Equal(7, pc.Locals.Ints[key]);
        journal.Add(q.ToLowerInvariant(), 3, pc, true, false, false);
        Assert.Equal(3, pc.Locals.Ints[JournalService.LocalPrefix + q.ToLowerInvariant()]);
        Assert.Equal(0, journal.Experience(q.ToLowerInvariant()));
        // Nothing above reached a journal window except the bAllPlayers update (the player is connected).
        Assert.True(output.Notifies.Count(n => n.Kind == "journal") - notifies <= 1);
    }

    [DataFact]
    public void PortraitResRefFallsBackToPortraitsTable()
    {
        var (world, _, _) = MakeWorld();
        var c = new Creature { Portrait = "po_custom_" };
        Assert.Equal("po_custom_", Portraits.ResRef(world, c));
        c.Portrait = "";
        Assert.Equal("", Portraits.ResRef(world, c));
        var table = world.Resources.TwoDA("portraits");
        int row = Enumerable.Range(1, 40).First(r => table.Get(r, "BaseResRef").Length > 0);
        c.PortraitId = row;
        Assert.Equal("po_" + table.Get(row, "BaseResRef"), Portraits.ResRef(world, c));
    }

    [DataFact]
    public void CassowarySolvesLayouts()
    {
        var (world, _, _) = MakeWorld();
        world.Scripts!.Run("player_cassowary", world.Module.Id);
        var v = world.Module.Locals;
        Assert.Equal("", v.Strings["err1"]);
        Assert.Equal(10f, v.Floats["left"], 3);
        Assert.Equal(110f, v.Floats["right"], 3);
        Assert.Equal(200f, v.Floats["left2"], 3);
        Assert.Equal(300f, v.Floats["right2"], 3);
        Assert.NotEqual("", v.Strings["err_bad"]);
        Assert.NotEqual("", v.Strings["err_unsat"]);
        Assert.Equal(100f, v.Floats["shared"], 3);
        Assert.Equal(0f, v.Floats["reset"], 3);
    }

    [Fact]
    public void CassowaryHonoursStrengths()
    {
        var s = new CassowarySolver();
        Assert.Equal("", s.Constrain("x + y == 100", CassowarySolver.Required));
        Assert.Equal("", s.Constrain("x == 30", CassowarySolver.Strong));
        Assert.Equal("", s.Constrain("x == 80", CassowarySolver.Weak));
        Assert.Equal(30, s.Value("x"), 6);
        Assert.Equal(70, s.Value("y"), 6);
        Assert.Equal("", s.Constrain("(y - 10) / 2 >= 40", CassowarySolver.Required));
        Assert.Equal(90, s.Value("y"), 6);
        Assert.Equal(10, s.Value("x"), 6);
        Assert.Contains("Constraints", s.Debug());
    }

    [Fact]
    public void XxHashMatchesReference()
    {
        Assert.Equal(0x02CC5D05u, MiscFunctions.XxHash32(Array.Empty<byte>()));
        Assert.Equal(unchecked((int)0xb0193469), MiscFunctions.HashString("AAA"));
        Assert.Equal(unchecked((int)0x599fe9c0), MiscFunctions.HashString("BBB"));
    }

    [DataFact]
    public void PlayerNotifiesGoToThatPlayer()
    {
        var (world, output, _) = MakeWorld();
        var pc = MakePc(world);
        var other = MakePc(world);
        var host = world.Scripts!;
        var defs = host.Definitions;
        // Call engine functions directly through the host's dispatch, as a script would.
        object? Call(string name, params object?[] args)
        {
            var f = defs.ByName[name];
            return ((IScriptEngine)host).Call(new ScriptThread(host.VM, new NcsProgram("t", System.Text.Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0").Concat(new byte[] { 0x20, 0 }).ToArray()), pc.Id), f, args);
        }

        Call("FadeToBlack", pc.Id, 0.01f);
        Call("SetCutsceneMode", pc.Id, 1, 0);
        Assert.True(pc.InCutscene);
        Assert.True(pc.Plot);
        Assert.Equal(1, Call("GetCutsceneMode", pc.Id));
        Call("SetCutsceneMode", pc.Id, 0, 0);
        Assert.False(pc.Plot);
        Call("SetCameraFacing", 90f, 10f, 45f, 0);
        Call("PopUpGUIPanel", pc.Id, 6);
        Call("PopUpGUIPanel", pc.Id, 3); // compass: not allowed
        Call("StartAudioStream", pc.Id, 1, "mus_test", 1, 0f, -1f, 1f);
        Assert.All(output.Notifies, n => Assert.Equal(pc.Id, n.Player));
        Assert.Equal(new[] { "fade", "cutscene", "cutscene", "camera", "gui-panel", "audio-stream" }, output.Notifies.Select(n => n.Kind));

        Assert.Equal(8193, Call("GetPlayerBuildVersionMajor", pc.Id));
        Assert.Equal(-1, Call("GetPlayerDeviceProperty", pc.Id, "gui_scale"));
        Assert.Equal(0, Call("GetPlayerBuildVersionMajor", world.Module.Id));

        Call("SetTlkOverride", 5213, "Berserker");
        Assert.Equal("Berserker", world.TlkStringOverridden(5213));
        Assert.Equal(2, output.Notifies.Count(n => n.Kind == "tlk-override"));

        Call("BootPC", other.Id, "bye");
        Assert.False(other.Player!.Connection.IsConnected);
    }
}
