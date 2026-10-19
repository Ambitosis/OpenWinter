using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class SocietyTests
{
    readonly ITestOutputHelper _out;
    public SocietyTests(ITestOutputHelper output) { _out = output; }

    // ---- listen patterns (no game data needed) ----

    [Theory]
    [InlineData("**", "hello world", new[] { "hello world" })]
    [InlineData("**", "", new[] { "" })]
    [InlineData("hello", "hello", new[] { "hello" })]
    [InlineData("hello", "Hello", new[] { "Hello" })] // letters match in any case, as in the original
    [InlineData("hello", "hello there", null)]
    [InlineData("hello **", "hello world", new[] { "hello ", "world" })]
    [InlineData("**hello**", "say hello there", new[] { "say ", "hello", " there" })]
    [InlineData("**gold**", "I have 30 gold coins", new[] { "I have 30 ", "gold", " coins" })]
    [InlineData("give *n gold", "give 100 gold", new[] { "give ", "100", " gold" })]
    [InlineData("give *n gold", "give lots gold", null)]
    [InlineData("give *n gold", "give  gold", null)]
    [InlineData("*a", "abc", new[] { "abc" })]
    [InlineData("*a", "ab1", null)]
    [InlineData("*w", " \t ", new[] { " \t " })]
    [InlineData("*w", "", null)]
    [InlineData("*p", "?!.", new[] { "?!." })]
    [InlineData("*p", "?a", null)]
    [InlineData("attack*w**", "attack   the orc", new[] { "attack", "   ", "the orc" })]
    [InlineData("(hi|hello) **", "hello bob", new[] { "hello", " ", "bob" })]
    [InlineData("(hi|hello) **", "hi bob", new[] { "hi", " ", "bob" })]
    [InlineData("(hi|hello)", "hey", null)]
    [InlineData("yes|no", "no", new[] { "no" })]
    [InlineData("yes|no", "maybe", null)]
    [InlineData("**!", "wow!", new[] { "wow", "!" })]
    [InlineData("a*b", "a*b", null)] // a star before anything but * w n a p never matches
    [InlineData("(*n|*a)", "abc", new[] { "abc" })]
    [InlineData("(*n|*a)", "123", new[] { "123" })]
    [InlineData("(*n|*a)", "12ab", null)]
    [InlineData("(the|a) (red|blue) **", "the blue door", new[] { "the", " ", "blue", " ", "door" })]
    [InlineData("NW_I_WAS_ATTACKED", "NW_I_WAS_ATTACKED", new[] { "NW_I_WAS_ATTACKED" })]
    [InlineData("*n*w**", "42 is the answer", new[] { "42", " ", "is the answer" })]
    public void ListenPatternTable(string pattern, string text, string[]? expected)
    {
        var result = ListenPatterns.Match(pattern, text);
        if (expected == null) Assert.Null(result);
        else Assert.Equal(expected, result);
    }

    // ---- world scaffolding ----

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    const int Hostile = 1, Commoner = 2, Defender = 4;

    /// <summary>A world with one 40 m square flat area; optionally a 4 m wall along x = 20.</summary>
    static (World World, Area Area) MakeWorld(bool wall = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Factions.LoadStandard();
        world.Factions.SetFactionReputation(Hostile, Commoner, 0);
        world.Factions.SetFactionReputation(Commoner, Hostile, 0);
        world.Factions.SetFactionReputation(Hostile, FactionTable.PlayerFaction, 0);
        world.Factions.SetFactionReputation(Commoner, Defender, 100);
        world.Factions.SetFactionReputation(Commoner, FactionTable.PlayerFaction, 50);

        var area = new Area { ResRef = "society_test", Tag = "society_test", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        if (wall)
        {
            area.Walkmesh.Add(new(20, 0, 0), new(20, 40, 0), new(20, 40, 4), 2, false);
            area.Walkmesh.Add(new(20, 0, 0), new(20, 40, 4), new(20, 0, 4), 2, false);
        }
        return (world, area);
    }

    static Creature Spawn(World world, Area area, string tag, float x, float y, int faction = Commoner, bool pc = false)
    {
        var c = new Creature { Tag = tag, Faction = faction, CurrentHitPoints = 10, MaxHitPoints = 10, PerceptionRange = 9 };
        if (pc) c.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = tag };
        world.AddToArea(c, area, new Vector3(x, y, 0), 0);
        return c;
    }

    static void Steps(World world, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.1f) world.Step(0.1f);
    }

    static List<string> CaptureLog(World world)
    {
        var log = new List<string>();
        world.Scripts!.Log = log.Add;
        return log;
    }

    void AssertNoScriptErrors(List<string> log)
    {
        foreach (var l in log) _out.WriteLine(l);
        Assert.DoesNotContain(log, l => l.Contains("not implemented") || l.Contains("[script"));
    }

    // ---- scripts ----

    [DataFact]
    public void PatternsFromScript()
    {
        var (world, _) = MakeWorld();
        var log = CaptureLog(world);
        world.Scripts!.Run("society_pattern", world.Module.Id);
        AssertNoScriptErrors(log);
        var v = world.Module.Locals;
        Assert.Equal(1, v.Ints["t1"]);
        // TestStringAgainstPattern leaves the matched substrings alone (only heard chat sets them), as in the original.
        Assert.Equal(0, v.Ints["cnt"]);
        Assert.Equal("", v.Strings["s0"]);
        Assert.Equal("", v.Strings["s1"]);
        Assert.Equal("", v.Strings["s2"]);
        Assert.Equal("", v.Strings["s9"]);
        Assert.Equal(0, v.Ints["t2"]);
        Assert.Equal(1, v.Ints["t3"]);
        Assert.Equal(-1, v.Ints["lpn"]);
    }

    [DataFact]
    public void ListeningCreaturesHearMatchingChat()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var pc = Spawn(world, area, "pc", 10, 10, pc: true);
        var near = Spawn(world, area, "near", 12, 10);
        var far = Spawn(world, area, "far", 35, 35);
        foreach (var l in new[] { near, far })
        {
            l.Scripts["ScriptDialogue"] = "society_heard";
            world.Scripts!.Run("society_listen", l.Id);
        }
        Assert.Equal(1, near.Locals.Ints["listening"]);

        SocietyService.OnChat(world, pc, "I have 30 gold coins", SocietyService.VolumeTalk);
        Steps(world, 0.2f);
        Assert.Equal(1, near.Locals.Ints["heard"]);
        Assert.Equal(7, near.Locals.Ints["pattern"]);
        Assert.Equal(3, near.Locals.Ints["count"]);
        Assert.Equal("I have 30 ", near.Locals.Strings["sub0"]);
        Assert.Equal("gold", near.Locals.Strings["sub1"]);
        Assert.Equal(pc.Id, near.Locals.Objects["speaker"]);
        Assert.False(far.Locals.Ints.ContainsKey("heard")); // out of talk range

        SocietyService.OnChat(world, pc, "hello there", SocietyService.VolumeShout);
        Steps(world, 0.2f);
        Assert.Equal(2, near.Locals.Ints["heard"]);
        Assert.Equal(3, near.Locals.Ints["pattern"]);
        Assert.Equal(1, far.Locals.Ints["heard"]); // shouts reach the whole area

        SocietyService.OnChat(world, pc, "nothing to see", SocietyService.VolumeTalk);
        Steps(world, 0.2f);
        Assert.Equal(2, near.Locals.Ints["heard"]);

        SocietyService.OnChat(world, pc, "hello friend", SocietyService.VolumeWhisper);
        Steps(world, 0.2f);
        Assert.Equal(3, near.Locals.Ints["heard"]); // 2 m away: within a whisper
        Assert.Equal(1, far.Locals.Ints["heard"]);

        // Associate commands arrive as conversation events numbered ASSOCIATE_COMMAND_*.
        near.Society.AssociatePatterns = true;
        AssociateService.IssueCommand(world, pc, near, -3);
        Steps(world, 0.2f);
        Assert.Equal(4, near.Locals.Ints["heard"]);
        Assert.Equal(-3, near.Locals.Ints["pattern"]);
        Assert.Equal(-3, near.Locals.Ints["command"]);
        Assert.Equal(-1, near.Locals.Ints["count"]);
        AssertNoScriptErrors(log);
    }

    [DataFact]
    public void NearestCreatureFilters()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var observer = Spawn(world, area, "observer", 10, 10);
        observer.Race = 6;
        observer.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
        var pc = Spawn(world, area, "pc", 12, 10, pc: true);
        var elf = Spawn(world, area, "elf", 13, 10);
        elf.Race = 1;
        var orc = Spawn(world, area, "orc", 14, 10, Hostile);
        var wizard = Spawn(world, area, "wizard", 16, 10, Defender);
        wizard.Classes.Add(new CreatureClass { Class = 10, Level = 3 });
        var blessedOrc = Spawn(world, area, "blessed", 18, 10, Hostile);
        blessedOrc.Effects.Add(new Effect { Type = 1, SpellId = 6 });
        var corpse = Spawn(world, area, "corpse", 20, 10);
        corpse.IsDead = true;
        var fighter = Spawn(world, area, "fighter", 25, 10);
        fighter.Race = 6;
        fighter.Classes.Add(new CreatureClass { Class = 4, Level = 2 });

        // The wizard is invisible to the observer.
        wizard.Effects.Add(new Effect { Type = PerceptionSystem.EffectInvisibility, Ints = new[] { 1, 0, 0, 0, 0, 0, 0, 0 } });
        world.System<PerceptionSystem>().Update(observer);

        world.Scripts!.Run("society_nearest", observer.Id);
        AssertNoScriptErrors(log);
        var v = world.Module.Locals.Objects;
        Assert.Equal(pc.Id, v["pc"]);
        Assert.Equal(elf.Id, v["npc1"]);
        Assert.Equal(orc.Id, v["npc2"]);
        Assert.Equal(orc.Id, v["enemy"]);
        Assert.Equal(elf.Id, v["friend"]);
        Assert.Equal(pc.Id, v["neutral"]);
        Assert.Equal(elf.Id, v["elf"]);
        Assert.Equal(wizard.Id, v["wizard"]);
        Assert.Equal(corpse.Id, v["dead"]);
        Assert.Equal(blessedOrc.Id, v["blessed"]);
        Assert.Equal(orc.Id, v["unblessed_enemy"]);
        Assert.Equal(pc.Id, v["seen"]);
        Assert.Equal(wizard.Id, v["notseen"]);
        Assert.Equal(fighter.Id, v["three"]);
        Assert.Equal(fighter.Id, v["loc"]);
        Assert.Equal(GameObject.InvalidId, v["none"]);
    }

    [DataFact]
    public void PerceptionEventsFireWhenCreaturesComeAndGo()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var observer = Spawn(world, area, "observer", 5, 5);
        observer.Scripts["ScriptOnNotice"] = "society_notice";
        observer.AILevel = 2; // AI_LEVEL_NORMAL: an area without players would update it every 6 s
        var target = Spawn(world, area, "target", 38, 38);
        var l = observer.Locals;

        // Perception updates every 4 seconds (PerceptionSystem.Interval), like the original.
        Steps(world, 5);
        Assert.False(l.Ints.ContainsKey("seen")); // 46 m away: out of range

        target.Position = new Vector3(8, 5, 0);
        Steps(world, PerceptionSystem.Interval);
        Assert.Equal(1, l.Ints["seen"]);
        Assert.Equal(1, l.Ints["heard"]);
        Assert.Equal(target.Id, l.Objects["perceived"]);
        Assert.Equal(1, l.Ints["objseen"]);
        Assert.Equal(1, l.Ints["objheard"]);
        Assert.Contains(target.Id, observer.Society.Seen);

        // Invisible: vanishes from sight but can still be heard.
        target.Effects.Add(new Effect { Type = PerceptionSystem.EffectInvisibility, Ints = new[] { 1, 0, 0, 0, 0, 0, 0, 0 } });
        Steps(world, PerceptionSystem.Interval);
        Assert.Equal(1, l.Ints["vanished"]);
        Assert.False(l.Ints.ContainsKey("inaudible"));
        Assert.DoesNotContain(target.Id, observer.Society.Seen);
        Assert.Contains(target.Id, observer.Society.Heard);

        // True seeing sees through it again.
        observer.Effects.Add(new Effect { Type = PerceptionSystem.EffectTrueSeeing });
        Steps(world, PerceptionSystem.Interval);
        Assert.Equal(2, l.Ints["seen"]);

        target.Position = new Vector3(38, 38, 0);
        Steps(world, PerceptionSystem.Interval);
        Assert.Equal(2, l.Ints["vanished"]);
        Assert.Equal(1, l.Ints["inaudible"]);
        Assert.Empty(observer.Society.Heard);
        AssertNoScriptErrors(log);
    }

    /// <summary>
    /// The order of one pass's events (nwserver sched_percept): creatures on the perception list
    /// newest first, then new ones west to east; new ones heard then seen, regained ones seen then heard, lost ones
    /// inaudible then vanished. An invisible, silenced creature nearby stays on the list; one out of range leaves it.
    /// </summary>
    [DataFact]
    public void PerceptionEventsFollowTheListThenX()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var o = Spawn(world, area, "O", 20, 20);
        o.Scripts["ScriptOnNotice"] = "society_order";
        o.AILevel = 2;
        var a = Spawn(world, area, "A", 28, 20);
        var b = Spawn(world, area, "B", 14, 20);
        var e = Spawn(world, area, "E", 20, 26);
        var f = Spawn(world, area, "F", 24, 14);
        string Take() { var s = world.Module.Locals.Strings.GetValueOrDefault("order", "").Trim(); world.Module.Locals.Strings["order"] = ""; return s; }
        Effect Invisible() => new() { Type = PerceptionSystem.EffectInvisibility, Ints = new[] { 1, 0, 0, 0, 0, 0, 0, 0 } };
        var perception = world.System<PerceptionSystem>();
        perception.Update(o);
        Assert.Equal("Bh Bs Eh Es Fh Fs Ah As", Take());

        a.Effects.Add(Invisible());
        b.Effects.Add(Invisible());
        f.Effects.Add(Invisible());
        f.Effects.Add(new Effect { Type = PerceptionSystem.EffectSilence });
        e.Position = new Vector3(39.9f, 39.9f, 0); // 28 m: beyond sight and hearing (row 9: 20 m)
        Spawn(world, area, "D", 10, 20);
        perception.Update(o);
        Assert.Equal("Av Fi Fv Ei Ev Bv Dh Ds", Take());
        perception.Update(o);
        Assert.Equal("Ai Bi", Take());

        foreach (var c in new[] { a, b, f }) c.Effects.Clear();
        e.Position = new Vector3(20, 26, 0);
        perception.Update(o);
        // A, F and B stayed on the list with no senses; E left it when out of range and is new again.
        Assert.Equal("As Ah Fs Fh Bs Bh Eh Es", Take());
        AssertNoScriptErrors(log);
    }

    /// <summary>
    /// SpeakString from an NPC reaches listeners that hear it, west to east; a placeable's reaches listeners within
    /// their hearing range (nwserver sched_events).
    /// </summary>
    [DataFact]
    public void SpeechReachesHearingListenersWestToEast()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var speaker = Spawn(world, area, "S", 10, 10);
        var listeners = new[] { Spawn(world, area, "L18", 18, 12), Spawn(world, area, "L4", 4, 12), Spawn(world, area, "L12", 12, 12),
            Spawn(world, area, "L35", 35, 35) };
        var deaf = Spawn(world, area, "DEAF", 8, 8);
        deaf.Effects.Add(new Effect { Type = PerceptionSystem.EffectDeaf });
        foreach (var l in listeners.Append(deaf))
        {
            l.Scripts["ScriptDialogue"] = "society_order";
            l.Society.Listening = true;
            l.Society.ListenPatterns.Add(new ListenPattern(1, "**"));
            world.System<PerceptionSystem>().Update(l);
        }
        string Take() { var s = world.Module.Locals.Strings.GetValueOrDefault("order", "").Trim(); world.Module.Locals.Strings["order"] = ""; return s; }
        SocietyService.OnSpeech(world, speaker, "hi", SocietyService.VolumeShout);
        Steps(world, 0.1f);
        // L35 is 35 m away, beyond its hearing (20 m): the shout doesn't reach it. The deaf one hears nothing.
        Assert.Equal("L4c L12c L18c", Take());

        var plc = new Placeable { Tag = "P" };
        world.AddToArea(plc, area, new Vector3(30, 30, 0), 0);
        SocietyService.OnSpeech(world, plc, "hi", SocietyService.VolumeShout);
        Steps(world, 0.1f);
        // Only L35 (7 m) is within its own hearing range of the placeable; L18 is 21.6 m away.
        Assert.Equal("L35c", Take());
        AssertNoScriptErrors(log);
    }

    [DataFact]
    public void PlayerPerceptionCallbacksObserveFastUpdatedState()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var observer = Spawn(world, area, "observer", 5, 5, pc: true);
        observer.Scripts["ScriptOnNotice"] = "society_notice";
        var target = Spawn(world, area, "target", 38, 38);
        Steps(world, .2f);
        target.Position = new Vector3(8, 5, 0);
        Steps(world, .2f);
        Assert.Equal(1, observer.Locals.Ints["seen"]);
        Assert.Equal(1, observer.Locals.Ints["heard"]);
        Assert.Equal(1, observer.Locals.Ints["objseen"]);
        Assert.Equal(1, observer.Locals.Ints["objheard"]);
        target.Position = new Vector3(38, 38, 0);
        Steps(world, .2f);
        Assert.Equal(1, observer.Locals.Ints["vanished"]);
        Assert.Equal(1, observer.Locals.Ints["inaudible"]);
        Assert.Equal(0, observer.Locals.Ints["objseen"]);
        Assert.Equal(0, observer.Locals.Ints["objheard"]);
        AssertNoScriptErrors(log);
    }

    [DataFact]
    public void WallsBlockSightButNotHearing()
    {
        var (world, area) = MakeWorld(wall: true);
        var log = CaptureLog(world);
        var a = Spawn(world, area, "a", 10, 10);
        var b = Spawn(world, area, "b", 15, 12);
        var c = Spawn(world, area, "c", 25, 10);
        world.Module.Locals.Objects["a"] = a.Id;
        world.Module.Locals.Objects["b"] = b.Id;
        world.Module.Locals.Objects["c"] = c.Id;
        world.Scripts!.Run("society_los", a.Id);
        AssertNoScriptErrors(log);
        var v = world.Module.Locals.Ints;
        Assert.Equal(1, v["ab"]);
        Assert.Equal(0, v["ac"]);
        Assert.Equal(1, v["ab_vec"]);
        Assert.Equal(0, v["ac_vec"]);

        world.System<PerceptionSystem>().Update(a);
        Assert.Contains(b.Id, a.Society.Seen);
        Assert.DoesNotContain(c.Id, a.Society.Seen);
        Assert.Contains(c.Id, a.Society.Heard);
    }

    [DataFact]
    public void PartiesAndHenchmen()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var pc1 = Spawn(world, area, "pc1", 5, 5, pc: true);
        var pc2 = Spawn(world, area, "pc2", 6, 5, pc: true);
        var hench = Spawn(world, area, "hench", 7, 5);
        var hench2 = Spawn(world, area, "hench2", 8, 5);
        pc1.Gold = 100; pc2.Gold = 50; hench.Gold = 7;
        pc1.Classes.Add(new CreatureClass { Class = 4, Level = 5 });
        pc2.Classes.Add(new CreatureClass { Class = 4, Level = 3 });
        hench.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
        hench.CurrentHitPoints = 2;
        foreach (var (k, x) in new[] { ("pc1", pc1), ("pc2", pc2), ("hench", hench), ("hench2", hench2) }) world.Module.Locals.Objects[k] = x.Id;

        world.Scripts!.Run("society_party", pc1.Id);
        AssertNoScriptErrors(log);
        var i = world.Module.Locals.Ints;
        var o = world.Module.Locals.Objects;
        Assert.Equal(0, i["eq_before"]);
        Assert.Equal(1, i["eq_after"]);
        Assert.Equal(pc1.Id, o["leader"]);
        Assert.Equal(hench.Id, o["h1"]);
        Assert.Equal(GameObject.InvalidId, o["h2"]);
        Assert.Equal(hench.Id, o["assoc"]);
        Assert.Equal(pc1.Id, o["master"]);
        Assert.Equal(1, i["htype"]);
        Assert.Equal(1, i["heq"]);
        Assert.Equal(2, i["pcs"]);
        Assert.Equal(3, i["all"]);
        Assert.Equal(157, i["gold"]);
        Assert.Equal(3, i["avglevel"]);
        Assert.Equal(pc1.Id, o["strongest"]);
        Assert.Equal(hench.Id, o["damaged"]);
        Assert.Equal(hench2.Id, o["h2b"]);
        Assert.Equal(2, i["max"]);
        Assert.Equal(0, i["heq_removed"]);
        Assert.Equal(GameObject.InvalidId, o["master_removed"]);
        Assert.Equal(Commoner, hench.Faction);
        Assert.Equal(0, i["eq_removed"]);
        Assert.Equal(pc2.Id, o["leader2"]);
        Assert.Equal(pc1.Faction, hench2.Faction); // the remaining henchman stays with its master
    }

    [DataFact]
    public void ScriptedDispositionUsesFixedOffsetAndNativeDecaySteps()
    {
        var (world, area) = MakeWorld();
        var source = Spawn(world, area, "source", 5, 5);
        var target = Spawn(world, area, "target", 6, 5, Hostile);
        var factions = world.Factions;
        factions.SetFactionReputation(Commoner, Hostile, 100);
        factions.SetScriptedTemporary(source, target, 50, true, 10);
        Assert.Equal(50, factions.ReputationOf(source, target));
        Steps(world, 2);
        Assert.Equal(51, factions.ReputationOf(source, target));
        Steps(world, 3);
        Assert.Equal(51, factions.ReputationOf(source, target));
        Steps(world, 3);
        Assert.Equal(52, factions.ReputationOf(source, target));
        Steps(world, 3);
        Assert.Equal(100, factions.ReputationOf(source, target));
        factions.SetFactionReputation(Commoner, Hostile, 50);
        factions.SetScriptedTemporary(source, target, 100, false, 0);
        Assert.Equal(100, factions.ReputationOf(source, target));
        factions.SetScriptedTemporary(source, target, 50, false, 0);
        Assert.Equal(0, factions.ReputationOf(source, target));
        factions.ClearPersonal(source, target);
        Assert.Equal(50, factions.ReputationOf(source, target));
    }

    [DataFact]
    public void NeutralFactionDiagonalDoesNotMakeMasterAndHenchmanNeutral()
    {
        var (world, area) = MakeWorld();
        var master = Spawn(world, area, "master", 5, 5);
        var peer = Spawn(world, area, "peer", 6, 5);
        world.Factions.SetFactionReputation(Commoner, Commoner, 50);
        Assert.Equal(50, world.Factions.ReputationOf(master, peer));
        Assert.Equal(100, world.Factions.ReputationOf(master, master));
        world.Factions.SetScriptedTemporary(master, peer, 100, false, 0);
        Assert.Equal(50, world.Factions.ReputationOf(master, peer));
        AssociateService.Add(world, master, peer, AssociateService.Henchman);
        Assert.Equal(100, world.Factions.ReputationOf(master, peer));
        Assert.Equal(100, world.Factions.ReputationOf(peer, master));
        AssociateService.Remove(world, master, peer);
        Assert.Equal(50, world.Factions.ReputationOf(master, peer));
    }

    [DataFact]
    public void ReputationAndTemporaryFeelings()
    {
        var (world, area) = MakeWorld();
        var log = CaptureLog(world);
        var npc = Spawn(world, area, "npc", 5, 5);
        var orc = Spawn(world, area, "orc", 6, 5, Hostile);
        var pc1 = Spawn(world, area, "pc1", 7, 5, pc: true);
        var pc2 = Spawn(world, area, "pc2", 8, 5, pc: true);
        foreach (var (k, o) in new[] { ("orc", orc), ("pc1", pc1), ("pc2", pc2) }) world.Module.Locals.Objects[k] = o.Id;

        world.Scripts!.Run("society_rep", npc.Id);
        var v = world.Module.Locals.Ints;
        Assert.Equal(0, v["r_orc"]);
        Assert.Equal(1, v["enemy_orc"]);
        Assert.Equal(1, v["friend_orc"]);
        Assert.Equal(0, v["react_orc"]); // Native friendly reputation across NPC factions is a neutral reaction.
        Assert.Equal(1, v["enemy_orc2"]);
        Assert.Equal(1, v["enemy_pc1"]);
        Assert.Equal(0, v["pc1_view"]);
        Assert.Equal(0, v["std_pc1"]);
        Assert.Equal(0, v["enemy_pc2"]);
        Assert.Equal(1, v["neutral_npc_by_pc"]);
        Assert.Equal(1, v["temp_enemy"]);
        Assert.Equal(100, v["std_hostile"]);
        Assert.Equal(1, v["orc_friend"]);
        Assert.Equal(0, v["avg_rep"]);
        Assert.Equal(-1, v["bad"]);

        // Native uses a -100 offset, decays it over 180 seconds, and expires it after the requested 10.
        Steps(world, 1);
        world.Scripts.Run("society_rep2", npc.Id);
        Assert.Equal(1, v["temp_enemy_later"]);
        Steps(world, 2);
        world.Scripts.Run("society_rep2", npc.Id);
        Assert.Equal(1, v["temp_enemy_later"]);
        Steps(world, 8);
        world.Scripts.Run("society_rep2", npc.Id);
        Assert.Equal(0, v["temp_enemy_later"]);
        AssertNoScriptErrors(log);
    }
}
