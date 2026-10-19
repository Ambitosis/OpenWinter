using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Skills;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Skills package: ActionUseSkill, stealth and detect modes, trap search, Lore (compiled test scripts in tests/scripts/skill_*.nss).</summary>
public class SkillTests
{
    readonly ITestOutputHelper _out;
    public SkillTests(ITestOutputHelper output) { _out = output; }

    const int Commoner = 2, Hostile = 1;

    sealed class RecordingOutput : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, object Data, Creature? Player)> Notes = new();
        public readonly List<(Creature Pc, string Text)> Messages = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Messages.Add((pc, text));
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? GameObject.InvalidId, data, player));
    }

    sealed class Fixture
    {
        public required World World;
        public required Area Area;
        public required RecordingOutput Output;
        public readonly List<string> Log = new();
        public readonly Queue<int> Rolls = new();
        public SkillSystem Skills => World.System<SkillSystem>();
        public LocalVariables M => World.Module.Locals;
        public void Run(string script, GameObject self) => World.Scripts!.Run(script, self.Id);
        public void Tick(float seconds = 0.1f) { for (float t = 0; t < seconds - 1e-4f; t += 0.1f) World.Step(0.1f); }

        public Creature Spawn(string tag, float x, float y, int faction = Commoner, bool pc = false)
        {
            // Human (race 6): no racial ability adjustments.
            var c = new Creature { Tag = tag, Faction = faction, Race = 6, CurrentHitPoints = 20, MaxHitPoints = 20, PerceptionRange = 9, Name = new LocString(tag) };
            if (pc) c.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, PlayerName = tag, Creature = c };
            World.AddToArea(c, Area, new Vector3(x, y, Area.Walkmesh.HighestGround(x, y) ?? 0), 0);
            CreatureRules.EnsureInit(World, c);
            return c;
        }

        /// <summary>OBJECT_SELF = <paramref name="user"/> runs ActionUseSkill; steps until the action is done.</summary>
        public void UseSkill(Creature user, int skill, GameObject? target, int sub = 0, Item? item = null)
        {
            user.Locals.Ints["skill"] = skill;
            user.Locals.Ints["skill_sub"] = sub;
            user.Locals.Objects["skill_target"] = target?.Id ?? GameObject.InvalidId;
            user.Locals.Objects["skill_item"] = item?.Id ?? GameObject.InvalidId;
            Run("skill_use", user);
            for (int i = 0; i < 100 && user.Actions.Count > 0; i++) World.Step(0.1f);
        }

        public Item Give(Creature c, string resref)
        {
            c.Locals.Strings["resref"] = resref;
            c.Locals.Ints["on_ground"] = 0;
            Run("skill_items", c);
            return World.Get<Item>(c.Locals.Objects["made"])!;
        }
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static readonly object Gate = new();
    static ResMan? _moduleRm;

    /// <summary>The module's resources (haks, 2DAs) plus the compiled test scripts.</summary>
    static ResMan ModuleResources()
    {
        lock (Gate)
        {
            if (_moduleRm != null) return _moduleRm;
            var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
            rm.Add(new DirectorySource(ScriptsDir()));
            return _moduleRm = rm;
        }
    }

    Fixture Setup(World world, Area area)
    {
        var output = new RecordingOutput();
        world.Output = output;
        var f = new Fixture { World = world, Area = area, Output = output };
        world.Inventory.EnsureLoaded(); // as after the first world step
        world.Scripts!.Log = l => { f.Log.Add(l); _out.WriteLine(l); };
        // Deterministic dice: queued rolls first, then 10s; the perception's stealth dice are all 10s.
        f.Skills.Dice = sides => Math.Clamp(f.Rolls.Count > 0 ? f.Rolls.Dequeue() : 10, 1, Math.Max(1, sides));
        f.Skills.StealthDie = sides => Math.Min(10, sides);
        return f;
    }

    /// <summary>A module area (real doors and placeables) with scripts.</summary>
    Fixture ModuleArea(string resref)
    {
        var world = new World(ModuleResources());
        world.EnableScripts();
        world.Register(world.Module);
        world.Factions.Load(world.Resources);
        var area = new AreaLoader(world).Load(resref);
        world.Areas.Add(area);
        return Setup(world, area);
    }

    /// <summary>A 40 m flat area on the base game's data.</summary>
    Fixture FlatArea()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Factions.Load(rm);
        world.Factions.SetFactionReputation(Hostile, Commoner, 0);
        world.Factions.SetFactionReputation(Commoner, Hostile, 0);
        var area = new Area { ResRef = "skill_test", Tag = "skill_test", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        return Setup(world, area);
    }

    void AssertNoScriptErrors(Fixture f) =>
        Assert.DoesNotContain(f.Log, l => l.Contains("not implemented") || l.Contains("[script") || l.Contains("error", StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------------ open lock

    [DataFact]
    public void OpenLockOnModuleDoorRollsInCombatAndTakesTwentyOutOfIt()
    {
        var f = ModuleArea("ss_town");
        var door = f.Area.ObjectsOf<Door>().First(d => !d.IsTransition);
        door.Locked = true;
        door.OpenLockDC = 25;
        door.KeyRequired = 0;
        door.Scripts["OnUnlock"] = "skill_unlock";
        var facing = new Vector2(-MathF.Sin(door.Facing), MathF.Cos(door.Facing));
        var thief = f.Spawn("thief", door.Position.X + facing.X * 1.2f, door.Position.Y + facing.Y * 1.2f, pc: true);
        thief.Abilities[1] = 14; // DEX +2
        thief.Skills[SkillIds.OpenLock] = 5;
        Assert.Equal(7, f.Skills.Rank(thief, SkillIds.OpenLock));

        // In combat the d20 is rolled: 10 + 7 = 17 fails DC 25.
        thief.InCombat = true;
        f.Rolls.Enqueue(10);
        f.UseSkill(thief, SkillIds.OpenLock, door);
        Assert.Equal(SkillIds.ActionOpenLock, thief.Locals.Ints["action"]);
        Assert.True(door.Locked);
        Assert.False(f.M.Ints.ContainsKey("unlock_events"));
        var check = f.Output.Notes.Last(n => n.Kind == "skill-check");
        Assert.Contains("\"success\":false", System.Text.Json.JsonSerializer.Serialize(check.Data));

        // 18 + 7 = 25 succeeds; OnUnlock runs with GetLastUnlocked() = the thief.
        f.Rolls.Enqueue(18);
        f.UseSkill(thief, SkillIds.OpenLock, door);
        Assert.False(door.Locked);
        f.Tick(0.2f);
        Assert.Equal(1, f.M.Ints["unlock_events"]);
        Assert.Equal(thief.Id, f.M.Objects["unlocked_by"]);

        // Out of combat the check takes 20 (20 + 7 = 27), no matter the dice; thieves' tools add their bonus and are used up.
        door.Locked = true;
        thief.InCombat = false;
        var tools = f.Give(thief, "nw_it_picks001");
        int toolBonus = f.Skills.ToolBonus(tools, SkillIds.IpThievesTools);
        Assert.True(toolBonus > 0);
        door.OpenLockDC = 27 + toolBonus; // only reachable with the tools
        f.Rolls.Enqueue(1);
        f.UseSkill(thief, SkillIds.OpenLock, door, item: tools);
        Assert.False(door.Locked);
        Assert.Contains(f.Output.Messages, m => m.Pc == thief && m.Text.Contains("Take 20"));
        Assert.True(tools.IsDestroyed || tools.StackSize == 0);
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void KeyRequiredLocksCannotBePicked()
    {
        var f = FlatArea();
        var chest = new Placeable { Tag = "chest", Useable = true, HasInventory = true, Locked = true, KeyRequired = 1, KeyName = "chest_key", OpenLockDC = 1 };
        f.World.AddToArea(chest, f.Area, new Vector3(10, 10, 0), 0);
        var thief = f.Spawn("thief", 11, 10);
        thief.Skills[SkillIds.OpenLock] = 40;
        f.UseSkill(thief, SkillIds.OpenLock, chest);
        Assert.True(chest.Locked);
        Assert.DoesNotContain(f.Output.Notes, n => n.Kind == "skill-check");
    }

    // ------------------------------------------------------------------ traps

    [DataFact]
    public void TrapIsFoundBySearchThenDisarmed()
    {
        var f = FlatArea();
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "SKILL_TRAP", 1, "skill_disarm", "")!;
        var rogue = f.Spawn("rogue", 23, 20, pc: true);
        rogue.Skills[SkillIds.Search] = 4;
        rogue.Skills[SkillIds.DisableTrap] = 6;
        rogue.Abilities[3] = 14; // INT +2
        rogue.Locals.Objects["watch"] = trap.Id;
        int detectDc = ((ITrappable)trap).TrapDetectDCValue;

        // Passive search uses half the skill: 10 + (4 + 2) / 2 = 13 vs the minor spike's detect DC.
        f.Tick(0.2f);
        f.Run("skill_status", rogue);
        Assert.True(detectDc <= 13);
        Assert.Equal(1, rogue.Locals.Ints["trap_detected"]);
        Assert.Contains(f.Output.Notes, n => n.Kind == "trap-detected" && n.Player == rogue);

        // Examine opens the trap's examine window with a difficulty; disarming out of combat takes 20: 20 + 8 vs DC 22,
        // 4.5 s at the trap.
        f.UseSkill(rogue, SkillIds.DisableTrap, trap, SkillIds.SubSkillExamineTrap);
        Assert.Contains(f.Output.Notes, n => n.Kind == "examine" && n.Player == rogue && n.Subject == trap.Id);
        Assert.Contains(f.Output.Messages, m => m.Text.StartsWith("Take 20 : rogue : Examine Trap : *success*"));
        f.UseSkill(rogue, SkillIds.DisableTrap, trap);
        f.Tick(0.2f);
        Assert.Equal(1, f.M.Ints["disarm_events"]);
        Assert.Equal(rogue.Id, f.M.Objects["disarmed_by"]);
        Assert.True(trap.IsDestroyed); // trap triggers go away once disarmed
        Assert.Contains(f.Output.Messages, m => m.Pc == rogue && m.Text.Contains("*success*") && m.Text.Contains("vs. DC: 22"));
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void NpcsSearchByDetectModeAndTheTrapFactionsReputation()
    {
        // As measured (trap_rules_npc): passive within 3.33 m every 6 s with a d10, detect mode within 6.66 m
        // every 3 s with a d20; standing still is not detect mode; a trap whose faction likes the searcher is never found.
        var f = FlatArea();
        const int Merchant = 3;
        f.World.Factions.SetFactionReputation(Merchant, Hostile, 100);
        TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(10, 10, 0), 0), 1f, "T_NEAR", Commoner, "", "");
        TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(25, 10, 0), 0), 1f, "T_FAR", Commoner, "", "");
        TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(10, 30, 0), 0), 1f, "T_MODE", Commoner, "", "");
        var friend = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(30, 30, 0), 0), 1f, "T_FRIEND", Merchant, "", "")!;
        foreach (var t in f.Area.Objects.OfType<Trigger>()) ((ITrappable)t).TrapDetectDCValue = 1;
        var near = f.Spawn("near", 10, 14, Hostile);   // 3 m from the edge
        var far = f.Spawn("far", 25, 16, Hostile);     // 5 m
        var mode = f.Spawn("mode", 10, 36, Hostile);   // 5 m, in detect mode
        mode.DetectMode = true;
        var liked = f.Spawn("liked", 30, 33, Hostile);
        f.Tick(7f);
        bool Found(string tag, Creature c) => ((ITrappable)f.Area.Objects.First(o => o.Tag == tag)).Trap.DetectedBy.Contains(c.Id);
        Assert.True(Found("T_NEAR", near));
        Assert.False(Found("T_FAR", far));
        Assert.True(Found("T_MODE", mode));
        Assert.DoesNotContain(liked.Id, ((ITrappable)friend).Trap.DetectedBy);
    }

    [DataFact]
    public void NpcTrapsGoOffForNpcsTheirSetterDislikes()
    {
        var f = FlatArea();
        const int Defender = 4;
        // The walker's view of the setter (else the trap's faction) counts, not the other way round (nwserver trap_trigger
        // with Prelude's factions): Hostile dislikes Defender, Commoner likes it though Defender dislikes Commoner.
        f.World.Factions.SetFactionReputation(Hostile, Defender, 0);
        f.World.Factions.SetFactionReputation(Defender, Hostile, 100);
        f.World.Factions.SetFactionReputation(Commoner, Defender, 100);
        f.World.Factions.SetFactionReputation(Defender, Commoner, 0);
        var setter = f.Spawn("setter", 2, 2, Defender);
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "KIT", Defender, "", "", setter.Id)!;
        var data = ((ITrappable)trap).Trap;
        Assert.True(TrapSystem.NpcClash(f.World, data, f.Spawn("hostile", 30, 30, Hostile)));
        Assert.False(TrapSystem.NpcClash(f.World, data, f.Spawn("commoner", 32, 30, Commoner)));
        Assert.False(TrapSystem.NpcClash(f.World, data, f.Spawn("defender", 34, 30, Defender)));
        // The setter joins the hostiles: its own reputation counts while it exists.
        setter.Faction = Hostile;
        Assert.False(TrapSystem.NpcClash(f.World, data, f.Spawn("hostile2", 36, 30, Hostile)));
    }

    [DataFact]
    public void TrapsNeverGoOffOnPlotCreatures()
    {
        var f = FlatArea();
        const int Defender = 4;
        f.World.Factions.SetFactionReputation(Hostile, Defender, 0);
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "PLOT", Defender, "", "")!;
        var walker = f.Spawn("hostile", 30, 30, Hostile);
        Assert.True(TrapSystem.SpringsFor(f.World, trap, walker));
        // nwserver trap_crit_ctl: plot walkers 0 of 4, plot disarmers 0 of 12.
        walker.Plot = true;
        Assert.False(TrapSystem.SpringsFor(f.World, trap, walker));
        Assert.False(TrapSystem.NpcCriticalClash(f.World, trap, walker));
    }

    [DataFact]
    public void ATrappedPlaceablesOwnFactionDecidesACriticalFailure()
    {
        var f = FlatArea();
        const int Defender = 4;
        f.World.Factions.SetFactionReputation(Hostile, Commoner, 0);
        f.World.Factions.SetFactionReputation(Hostile, Hostile, 100);
        var chest = new Placeable { Tag = "CHEST" };
        f.World.AddToArea(chest, f.Area, new Vector3(20, 20, 0), 0);
        f.World.Factions.SetObjectFaction(chest, Hostile);
        TrapSystem.Arm(f.World, chest, 0, Defender, "", "");
        // nwserver trap_crit_ctl: a Hostile chest's trap went off on Commoner disarmers, never on Hostile ones whatever
        // faction the trap was given.
        Assert.True(TrapSystem.NpcCriticalClash(f.World, chest, f.Spawn("commoner", 22, 20, Commoner)));
        Assert.False(TrapSystem.NpcCriticalClash(f.World, chest, f.Spawn("hostile", 18, 20, Hostile)));
    }

    [DataFact]
    public void ATrapsCreatorAlwaysDisarmsIt()
    {
        var f = FlatArea();
        var setter = f.Spawn("setter", 23, 20);
        setter.Skills[SkillIds.DisableTrap] = 1;
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "OWN", Commoner, "", "", setter.Id)!;
        ((ITrappable)trap).TrapDisarmDCValue = 120;
        f.UseSkill(setter, SkillIds.DisableTrap, trap);
        f.Tick(0.2f);
        Assert.False(((ITrappable)trap).IsTrapped);
    }

    [DataFact]
    public void BotchedDisarmSetsTheTrapOffAndRecoverGivesTheKit()
    {
        var f = FlatArea();
        var loc = new Location(f.Area.Id, new Vector3(20, 20, 0), 0);
        var deadly = TrapSystem.CreateAt(f.World, 3, loc, 2f, "DEADLY", 1, "", "")!; // DC 36: rogues only
        var clumsy = f.Spawn("clumsy", 25, 20, pc: true);
        clumsy.Skills[SkillIds.DisableTrap] = 1; // needs training
        clumsy.IsImmortal = true;
        f.UseSkill(clumsy, SkillIds.DisableTrap, deadly);
        Assert.True(((ITrappable)deadly).IsTrapped); // a non-rogue can't disarm above DC 35
        // Taking 20 out of combat, a miss is "success not possible" and never sets the trap off (traprules C_FAIL).
        Assert.Contains(f.Output.Messages, m => m.Pc == clumsy && m.Text.Contains("*success not possible*"));
        f.Rolls.Clear();
        clumsy.InCombat = true; // no taking 20

        var minor = TrapSystem.CreateAt(f.World, 0, loc with { Position = new Vector3(30, 30, 0) }, 2f, "MINOR", 1, "", "")!;
        f.Rolls.Enqueue(1); // 1 + 1 vs 22: a critical failure
        f.UseSkill(clumsy, SkillIds.DisableTrap, minor);
        Assert.Contains(f.Output.Notes, n => n.Kind == "trap-triggered" && n.Subject == minor.Id);
        Assert.Contains(f.Output.Messages, m => m.Pc == clumsy && m.Text.Contains("*critical failure*"));
        Assert.Contains(f.Output.Messages, m => m.Pc == clumsy && m.Text == TrapSystem.TriggeredMessage);

        var rogue = f.Spawn("rogue", 31, 10, pc: true);
        rogue.Classes.Add(new CreatureClass { Class = SkillIds.ClassRogue, Level = 10 });
        rogue.Skills[SkillIds.DisableTrap] = 30;
        var spike = TrapSystem.CreateAt(f.World, 0, loc with { Position = new Vector3(40, 10, 0) }, 2f, "SPIKE", 1, "", "")!;
        f.UseSkill(rogue, SkillIds.DisableTrap, spike, SkillIds.SubSkillRecoverTrap);
        f.Tick(0.1f); // a trap trigger goes a moment after its OnDisarm
        Assert.False(((ITrappable)spike).IsTrapped);
        Assert.True(spike.IsDestroyed);
        Assert.Contains(rogue.Inventory, i => i.Tag.Equals("NW_IT_TRAP001", StringComparison.OrdinalIgnoreCase));
    }

    [DataFact]
    public void TrapKitSetsATrapThePartySees()
    {
        var f = FlatArea();
        var setter = f.Spawn("setter", 15, 15, pc: true);
        setter.Skills[SkillIds.SetTrap] = 10;
        var kit = f.Give(setter, "nw_it_trap001");
        Assert.Equal(0, f.Skills.TrapKitType(kit)); // minor spike
        // ActionUseSkill(SKILL_SET_TRAP) does nothing; the kit is used at a spot (trap_set).
        f.UseSkill(setter, SkillIds.SetTrap, null, item: kit);
        Assert.Empty(f.Area.ObjectsOf<Trigger>().Where(t => t.TriggerType == Trigger.TypeTrap));
        setter.Actions.Add(new UseItemAction { ItemId = kit.Id, Property = kit.Properties[0], TargetLocation = new Location(f.Area.Id, new Vector3(18, 15, 0), 30) });
        for (int i = 0; i < 100 && setter.Actions.Count > 0; i++) f.World.Step(0.1f);
        var trap = f.Area.ObjectsOf<Trigger>().Single(t => t.TriggerType == Trigger.TypeTrap);
        var t = (ITrappable)trap;
        Assert.True(t.IsTrapped);
        Assert.Equal(setter.Id, t.Trap.Creator);
        // Seen by the setter's party, though not "detected" by it.
        Assert.DoesNotContain(setter.Id, t.Trap.DetectedBy);
        Assert.True(TrapSystem.Shows(f.World, trap, setter));
        Assert.Contains(f.Output.Notes, n => n.Kind == "trap-detected" && n.Player == setter && n.Subject == trap.Id);
        Assert.True(kit.IsDestroyed);
        // DCs: DetectDCMod 10 / DisarmDCMod 22 plus the Set Trap bonus (10 ranks), no roll; a 4 m square at the spot.
        Assert.Equal(20, t.TrapDetectDCValue);
        Assert.Equal(32, t.TrapDisarmDCValue);
        Assert.Equal(new Vector3(18, 15, 0), trap.Position with { Z = 0 });
        Assert.Equal(4f, trap.Geometry.Max(p => p.X) - trap.Geometry.Min(p => p.X), 3);
        Assert.Contains(f.Output.Messages, m => m.Text.StartsWith("Take 20 : setter : Set Trap : *success*"));
    }

    /// <summary>A walker crossing a 2 m trap at (x, 20) from 3 m west to 3 m east.</summary>
    static void Cross(Fixture f, Creature c, float x)
    {
        f.World.Jump(c, new Location(f.Area.Id, new Vector3(x - 3, 20, 0), 0));
        c.Actions.Add(new MoveAction { Target = new Vector3(x + 3, 20, 0) });
        for (int i = 0; i < 80 && c.Actions.Count > 0; i++) f.World.Step(0.1f);
    }

    [DataFact]
    public void TrapTriggersGoOffOnlyBetweenThePlayersSideAndItsEnemies()
    {
        var f = FlatArea();
        Trigger Trap(float x, int faction)
        {
            var t = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(x, 20, 0), 0), 1f, "T" + x, faction, "", "world_evt")!;
            t.Scripts["ScriptOnEnter"] = "world_evt";
            return t;
        }
        var pc = f.Spawn("pc", 2, 2, pc: true);
        pc.IsImmortal = true;
        int party = f.World.Factions.FactionOf(pc)!.Value;

        // Between NPCs a trap notices a creature its faction dislikes (trap_rules_npc, with a faction table): a commoner
        // walks across a merchant trap (merchants like commoners) without OnEnter, and sets off a hostile one.
        const int Merchant = 3;
        f.World.Factions.SetFactionReputation(Merchant, Commoner, 100);
        var merchantTrap = Trap(5, Merchant);
        var commoner = f.Spawn("commoner", 2, 6);
        Cross(f, commoner, 5);
        Assert.True(((ITrappable)merchantTrap).IsTrapped);
        Assert.False(merchantTrap.Locals.Ints.ContainsKey("events"));
        var npcTrap = Trap(15, Hostile);
        Cross(f, commoner, 15);
        Assert.True(npcTrap.IsDestroyed);
        Assert.Equal(commoner.Id, npcTrap.Locals.Objects["entering"]);
        var hostileTrap = Trap(10, Hostile);

        // The player sets it off: the trap's script and OnEnter, then a one-shot trap trigger is gone (traprules WALK).
        Cross(f, pc, 10);
        Assert.Equal(2, hostileTrap.Locals.Ints["events"]);
        Assert.Equal(pc.Id, hostileTrap.Locals.Objects["entering"]);
        Assert.True(hostileTrap.IsDestroyed);
        Assert.Contains(f.Output.Messages, m => m.Pc == pc && m.Text == TrapSystem.TriggeredMessage);

        // Not a commoner trap (traplook COMMONER_TRAP).
        var commonerTrap = Trap(20, Commoner);
        Cross(f, pc, 20);
        Assert.True(((ITrappable)commonerTrap).IsTrapped);

        // The players' trap: a hostile creature sets it off, a commoner doesn't (traprules SET, traplook).
        var partyTrap = Trap(30, party);
        Cross(f, commoner, 30);
        Assert.True(((ITrappable)partyTrap).IsTrapped);
        var hostile = f.Spawn("hostile", 2, 10, Hostile);
        Cross(f, hostile, 30);
        Assert.True(partyTrap.IsDestroyed);
        Assert.Equal(hostile.Id, partyTrap.Locals.Objects["entering"]);

        // A henchman is on the player's side (traprules HWALK).
        var hench = f.Spawn("hench", 2, 14);
        hench.Master = pc.Id;
        var trap2 = Trap(40, Hostile);
        Cross(f, hench, 40);
        Assert.True(trap2.IsDestroyed);
        Assert.Equal(hench.Id, trap2.Locals.Objects["entering"]);
        Assert.DoesNotContain(f.Output.Messages, m => m.Text == TrapSystem.TriggeredMessage && m.Pc != pc);
    }

    [DataFact]
    public void NpcsSearchForThemselvesAndThePartyShares()
    {
        var f = FlatArea();
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 1f, "T", Hostile, "", "")!;
        ((ITrappable)trap).TrapDetectDCValue = 1;
        // An NPC finds a trap of a faction that dislikes it, for itself only (trap_rules_npc).
        var npc = f.Spawn("npc", 23, 20);
        npc.Skills[SkillIds.Search] = 20;
        f.Tick(7f);
        Assert.Equal(new[] { npc.Id }, ((ITrappable)trap).Trap.DetectedBy);

        // The henchman beside the trap finds it; its player 8 m away learns it too. GetLastTrapDetected doesn't change
        // (the original's stayed invalid after a find).
        var pc = f.Spawn("pc", 29, 20, pc: true);
        var hench = f.Spawn("hench", 22.5f, 20);
        hench.Master = pc.Id;
        hench.Skills[SkillIds.Search] = 20;
        f.Tick(0.3f);
        var t = ((ITrappable)trap).Trap;
        Assert.Contains(hench.Id, t.DetectedBy);
        Assert.Contains(pc.Id, t.DetectedBy);
        Assert.Equal(GameObject.InvalidId, hench.LastTrapDetected);
        Assert.Contains(f.Output.Notes, n => n.Kind == "trap-detected" && n.Player == pc && n.Subject == trap.Id);
    }

    [DataFact]
    public void DisableTrapKneelsThenResolvesAndOnlyRoguesDisarmAboveDc35()
    {
        var f = FlatArea();
        var chest = new Placeable { Tag = "CHEST", HasInventory = true, Name = new LocString("Chest") };
        f.World.AddToArea(chest, f.Area, new Vector3(20, 20, 0), 0);
        TrapSystem.Arm(f.World, chest, 0, Hostile, "skill_disarm", "");
        ((ITrappable)chest).TrapDisarmDCValue = 36;
        var fighter = f.Spawn("fighter", 21, 20, pc: true); // already within reach
        fighter.Skills[SkillIds.DisableTrap] = 60;
        fighter.Locals.Ints["skill"] = SkillIds.DisableTrap;
        fighter.Actions.Add(new UseSkillAction { Skill = SkillIds.DisableTrap, Target = chest.Id, Range = 2f });
        f.Tick(4.2f);
        Assert.Equal(SkillIds.ActionDisableTrap, fighter.Actions[0].ActionType); // still kneeling
        f.Tick(0.6f);
        Assert.Empty(fighter.Actions);
        Assert.True(((ITrappable)chest).IsTrapped); // DC 36: rogues only (trap_disarm ROGUE36)

        // Recovering isn't rogue-only (ROGUEREC36), and gives the kit; nobody has detected the trap afterwards.
        TrapSystem.SetDetected(f.World, chest, fighter, true);
        fighter.Actions.Add(new UseSkillAction { Skill = SkillIds.DisableTrap, SubSkill = SkillIds.SubSkillRecoverTrap, Target = chest.Id, Range = 2f });
        f.Tick(5f);
        Assert.False(((ITrappable)chest).IsTrapped);
        Assert.Empty(((ITrappable)chest).Trap.DetectedBy);
        Assert.Contains(fighter.Inventory, i => i.Tag.Equals("NW_IT_TRAP001", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(fighter.Id, f.M.Objects["disarmed_by"]);
    }

    [DataFact]
    public void FlaggedTrapsShowToEveryoneWithoutBeingDetected()
    {
        var f = FlatArea();
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 1f, "T", Hostile, "", "")!;
        ((ITrappable)trap).TrapDetectDCValue = 250;
        var flagger = f.Spawn("flagger", 23, 20, pc: true);
        flagger.Skills[SkillIds.DisableTrap] = 10;
        var other = f.Spawn("other", 30, 30, pc: true);
        f.UseSkill(flagger, SkillIds.DisableTrap, trap, SkillIds.SubSkillFlagTrap);
        var t = (ITrappable)trap;
        Assert.True(t.Trap.Flagged);
        Assert.DoesNotContain(other.Id, t.Trap.DetectedBy); // trap_disarm FLAG: detected.other 0
        Assert.True(TrapSystem.Shows(f.World, trap, other));
        Assert.Contains(f.Output.Notes, n => n.Kind == "trap-detected" && n.Player == other && n.Subject == trap.Id);
    }

    // ------------------------------------------------------------------ pick pocket

    [DataFact]
    public void PickPocketStealsUnnoticedOrAngersTheVictim()
    {
        var f = FlatArea();
        var thief = f.Spawn("thief", 10, 10, pc: true);
        thief.Skills[SkillIds.PickPocket] = 10;
        var mark = f.Spawn("mark", 11, 10);
        mark.Scripts["ScriptDisturbed"] = "skill_dist";
        var purse = f.Give(mark, "nw_it_gem001");
        purse.Pickpocketable = true;

        // Theft: 15 + 10 beats DC 20; the mark's Spot roll of 2 doesn't beat 25.
        f.Rolls.Enqueue(15); f.Rolls.Enqueue(2); f.Rolls.Enqueue(1);
        f.UseSkill(thief, SkillIds.PickPocket, mark);
        Assert.Equal(thief, f.World.Inventory.Owner(purse) ?? f.World.Inventory.Owner(thief.Inventory.First()));
        Assert.Contains(thief.Inventory, i => i.Stolen);
        f.Tick(0.2f);
        Assert.False(mark.Locals.Ints.ContainsKey("disturb_events"));
        Assert.False(f.World.Factions.IsEnemyOf(mark, thief));

        // Botched and seen: the mark turns hostile and its OnDisturbed runs with INVENTORY_DISTURB_TYPE_STOLEN.
        var ring = f.Give(mark, "nw_it_gem001");
        ring.Pickpocketable = true;
        f.Rolls.Enqueue(1); f.Rolls.Enqueue(20);
        f.UseSkill(thief, SkillIds.PickPocket, mark);
        f.Tick(0.2f);
        Assert.Contains(ring, mark.Inventory);
        Assert.Equal(1, mark.Locals.Ints["disturb_events"]);
        Assert.Equal(thief.Id, mark.Locals.Objects["disturber"]);
        Assert.Equal(SkillIds.DisturbStolen, mark.Locals.Ints["disturb_type"]);
        mark.Locals.Objects["watch"] = thief.Id;
        f.Run("skill_status", mark);
        Assert.Equal(1, mark.Locals.Ints["enemy"]);
        AssertNoScriptErrors(f);
    }

    // ------------------------------------------------------------------ taunt

    [DataFact]
    public void TauntLowersArmourClassAndAddsSpellFailure()
    {
        var f = FlatArea();
        var taunter = f.Spawn("taunter", 10, 10, pc: true);
        taunter.Skills[SkillIds.Taunt] = 10;
        var ogre = f.Spawn("ogre", 11, 10, Hostile);
        f.Run("skill_status", ogre);
        int acBefore = ogre.Locals.Ints["ac"];

        f.Rolls.Enqueue(5); f.Rolls.Enqueue(15); // Concentration 5 + 0 against Taunt 15 + 10: 20 points, capped at 6
        f.UseSkill(taunter, SkillIds.Taunt, ogre);
        Assert.Equal(SkillIds.ActionTaunt, taunter.Locals.Ints["action"]);
        f.Run("skill_status", ogre);
        Assert.Equal(6, ogre.Locals.Ints["ac_decrease"]);
        Assert.Equal(30, ogre.Locals.Ints["spell_failure"]);
        Assert.Equal(acBefore - 6, ogre.Locals.Ints["ac"]);

        // Not cumulative: a second taunt replaces the first.
        f.Rolls.Enqueue(15); f.Rolls.Enqueue(9); // 19 - 15 = 4
        f.UseSkill(taunter, SkillIds.Taunt, ogre);
        f.Run("skill_status", ogre);
        Assert.Equal(4, ogre.Locals.Ints["ac_decrease"]);
        Assert.Equal(2, ogre.Locals.Ints["effects"]);

        // Wears off after TAUNT_EFFECT_DURATION.
        f.Tick(31f);
        f.Run("skill_status", ogre);
        Assert.Equal(0, ogre.Locals.Ints["effects"]);
        AssertNoScriptErrors(f);
    }

    // ------------------------------------------------------------------ heal

    [DataFact]
    public void HealersKitHealsAndCuresPoison()
    {
        var f = FlatArea();
        var medic = f.Spawn("medic", 10, 10, pc: true);
        medic.Skills[SkillIds.Heal] = 5;
        var patient = f.Spawn("patient", 11, 10);
        patient.CurrentHitPoints = 2;
        var k = EffectConstants.Of(f.World);
        var poison = new Effect { Type = SkillIds.EffectPoison, SubType = k.Extraordinary };
        poison.Ints[0] = 0; // Nightshade, DC 10
        f.World.System<EffectSystem>().Apply(poison, k.Permanent, patient, 0);
        var kit = f.Give(medic, "nw_it_medkit001");
        int kitBonus = f.Skills.ToolBonus(kit, SkillIds.IpHealersKit);
        Assert.True(kitBonus > 0);

        f.Rolls.Enqueue(4); // 4 + 5 + kit
        f.UseSkill(medic, SkillIds.Heal, patient, item: kit);
        f.Run("skill_status", patient);
        Assert.Equal(Math.Min(20, 2 + 4 + 5 + kitBonus), patient.Locals.Ints["hp"]);
        Assert.False(patient.Locals.Ints.ContainsKey("poisoned"));
        Assert.True(kit.IsDestroyed || kit.StackSize == 0);

        // Nobody to treat: healthy and unpoisoned.
        patient.CurrentHitPoints = 20;
        Assert.False(f.Skills.Heal(medic, patient, null));
        AssertNoScriptErrors(f);
    }

    // ------------------------------------------------------------------ stealth

    [DataFact]
    public void StealthIsRefusedOnlyToACreatureThatIsAttacking()
    {
        var f = FlatArea();
        var sneak = f.Spawn("sneak", 15, 10, Hostile);
        var victim = f.Spawn("victim", 16.5f, 10, Commoner);
        sneak.Locals.Ints["mode"] = SkillIds.ModeStealth;
        sneak.Locals.Ints["mode_set"] = 1;
        sneak.Locals.Ints["mode_on"] = 1;

        // Being attacked (in combat, not attacking) still allows it (nwserver stealth_modes victim.plain).
        sneak.InCombat = true;
        f.Run("skill_mode", sneak);
        Assert.Equal(1, sneak.Locals.Ints["mode_now"]);
        sneak.Locals.Ints["mode_on"] = 0;
        f.Run("skill_mode", sneak);
        Assert.Equal(0, sneak.Locals.Ints["stealth"]);

        // Attacking refuses it, Hide in Plain Sight or not (fight.plain, fighthips).
        f.World.System<OpenWinter.Game.Combat.CombatSystem>().Attack(sneak, victim);
        sneak.Feats.Add(SkillIds.FeatHideInPlainSight);
        sneak.Locals.Ints["mode_on"] = 1;
        f.Run("skill_mode", sneak);
        Assert.Equal(0, sneak.Locals.Ints["mode_now"]);
        Assert.True(SkillSystem.ForcesWalk(f.World, victim) == false);
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void ASpottedHiderStaysSpottedUntilItGetsOutOfSight()
    {
        var f = FlatArea();
        var watcher = f.Spawn("watcher", 10, 10, Commoner, pc: true);
        var sneak = f.Spawn("sneak", 15, 10, Hostile);
        sneak.Skills[SkillIds.Hide] = 10;
        sneak.Skills[SkillIds.MoveSilently] = 10;
        watcher.Locals.Objects["watch"] = sneak.Id;
        Assert.True(f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true));

        // Dice of 10 all round against a passive watcher (half its skill): Spot 0 + 10 - 10 - 10 - 5 + 5 = -10,
        // Listen at 5 m one less.
        f.Tick(1f);
        f.Run("skill_mode", watcher);
        Assert.Equal(0, watcher.Locals.Ints["seen"]);
        Assert.Equal(0, watcher.Locals.Ints["heard"]);
        Assert.Contains(f.Output.Notes, n => n.Kind == "stealth-visibility" && n.Player == watcher && n.Subject == sneak.Id);

        // Every pass compares the cached dice, so better eyes help at the next pass: half of 40 makes it +10.
        watcher.Skills[SkillIds.Spot] = 40;
        f.Tick(0.3f);
        f.Run("skill_mode", watcher);
        Assert.Equal(1, watcher.Locals.Ints["seen"]);
        Assert.Equal(0, watcher.Locals.Ints["heard"]);

        // Spotted stays spotted while in view, whatever the skills do now (stealth_rules sticky).
        watcher.Skills[SkillIds.Spot] = 0;
        f.Tick(16f);
        f.Run("skill_mode", watcher);
        Assert.Equal(1, watcher.Locals.Ints["seen"]);

        // Out of sight range and back: hidden again (rehide).
        f.World.Jump(sneak, new Location(f.Area.Id, new Vector3(39, 39, 0), 0));
        f.Tick(1f);
        f.World.Jump(sneak, new Location(f.Area.Id, new Vector3(15, 10, 0), 0));
        f.Tick(1f);
        f.Run("skill_mode", watcher);
        Assert.Equal(0, watcher.Locals.Ints["seen"]);
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void SpotAndListenTotalsFollowTheMeasuredFormula()
    {
        var f = FlatArea();
        var watcher = f.Spawn("watcher", 10, 10, Commoner);
        var sneak = f.Spawn("sneak", 13, 10, Hostile);
        watcher.Skills[SkillIds.Spot] = 9;
        watcher.Skills[SkillIds.Listen] = 9;
        sneak.Skills[SkillIds.Hide] = 3;
        sneak.Skills[SkillIds.MoveSilently] = 3;
        var od = f.Skills.DiceOf(watcher);
        var hd = f.Skills.DiceOf(sneak);
        (od.Spot, od.Listen, hd.Hide, hd.MoveSilently) = (7, 6, 12, 11);

        // Standing in the idle stance (placed in the toolset, or stopped since it was made) it is active: full skill.
        Assert.Equal(1, f.Skills.SpotTotal(watcher, sneak));
        // Made at runtime and never moved (the combat-ready stance): passive (stealth_check_stance).
        watcher.IdleStance = false;
        // Passive: half the skill (9 -> 4) + die 7 - 12 - 3, a still hider -5, a still watcher +5.
        Assert.Equal(-4, f.Skills.SpotTotal(watcher, sneak));
        // Listen also loses a point per whole 3 m: 4 + 6 - 11 - 3 - 1.
        Assert.Equal(-5, f.Skills.ListenTotal(watcher, sneak, 3f));
        // Detect mode: the full skill.
        f.Skills.SetActionMode(watcher, SkillIds.ModeDetect, true);
        Assert.Equal(1, f.Skills.SpotTotal(watcher, sneak));
        Assert.True(f.Skills.SpotSucceeds(watcher, sneak));
        // A moving hider loses its -5, a watcher in combat takes -10.
        sneak.Speed = 1.5f;
        watcher.InCombat = true;
        Assert.Equal(-4, f.Skills.SpotTotal(watcher, sneak));
        sneak.Speed = 0;
        watcher.InCombat = false;
        // A tie goes to the hider; a 20 on its die is no more than its number (stealth_check_stance: 166 of 166 and 154
        // of 154 seen where the numbers can't fail).
        hd.Hide = 13;
        Assert.Equal(0, f.Skills.SpotTotal(watcher, sneak));
        Assert.False(f.Skills.SpotSucceeds(watcher, sneak));
        hd.Hide = 20;
        watcher.Skills[SkillIds.Spot] = 60;
        Assert.True(f.Skills.SpotTotal(watcher, sneak) > 0);
        Assert.True(f.Skills.SpotSucceeds(watcher, sneak));
        // The area's ModSpotCheck adds.
        f.Area.Are.SetInt("ModSpotCheck", -10);
        hd.Hide = 12;
        watcher.Skills[SkillIds.Spot] = 9;
        Assert.Equal(-9, f.Skills.SpotTotal(watcher, sneak));

        // Size: Spot by the difference (two steps or less), Listen by the hider's own size.
        Assert.Equal(new[] { 0, -8, -4, 0, 4, 8, 0 }, new[] { -3, -2, -1, 0, 1, 2, 3 }.Select(SkillSystem.SpotSizeModifier));
        Assert.Equal(new[] { -8, -4, 0, 4, 8 }, new[] { 1, 2, 3, 4, 5 }.Select(SkillSystem.ListenSizeModifier));
    }

    [DataFact]
    public void StealthDiceRefreshAtMostEverySixSecondsInTheCreaturesOwnPass()
    {
        var f = FlatArea();
        var sides = new List<int>();
        f.Skills.StealthDie = s => { sides.Add(s); return 7; };
        var sneak = f.Spawn("sneak", 13, 10, Hostile);
        sneak.IdleStance = false; // made at runtime, never moved: not active (stealth_check_stance)
        var d = f.Skills.DiceOf(sneak);
        // A new creature's dice are 1-10.
        Assert.Equal(new[] { 10, 10, 10, 10 }, sides);
        sides.Clear();
        // The first pass that evaluates someone refreshes them. Not in stealth, not active: new d10 Spot and Listen
        // dice only.
        f.Skills.RefreshDice(sneak);
        Assert.Equal(new[] { 10, 10 }, sides);
        sides.Clear();
        f.Tick(3f); // alone in the area, it evaluates nobody in its own passes
        f.Skills.RefreshDice(sneak);
        Assert.Empty(sides); // not 6 s yet
        sides.Clear();
        f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true);
        f.Skills.SetActionMode(sneak, SkillIds.ModeDetect, true);
        f.Tick(2f);
        f.Skills.RefreshDice(sneak);
        Assert.Empty(sides);
        f.Tick(1.1f);
        f.Skills.RefreshDice(sneak);
        // In stealth: Hide and Move Silently d20s; in detect mode: d20s for Spot and Listen.
        Assert.Equal(new[] { 20, 20, 20, 20 }, sides);
    }

    [DataFact]
    public void ADestroyedCreaturesStealthDiceAreForgotten()
    {
        var f = FlatArea();
        var watcher = f.Spawn("watcher", 10, 10, Commoner);
        var sneak = f.Spawn("sneak", 13, 10, Hostile);
        f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true);
        f.Skills.DiceOf(watcher);
        f.Skills.DiceOf(sneak);
        Assert.True(f.Skills.HasStealthDice(sneak.Id));
        f.World.Destroy(sneak);
        Assert.False(f.Skills.HasStealthDice(sneak.Id));
        f.Skills.DiceOf(sneak);
        Assert.False(f.Skills.HasStealthDice(sneak.Id));
        Assert.True(f.Skills.HasStealthDice(watcher.Id));
    }

    [DataFact]
    public void AnInvisibleCreatureIsHeardOnlyAtCloseRange()
    {
        var f = FlatArea();
        var k = EffectConstants.Of(f.World);
        var fx = f.World.System<EffectSystem>();
        var watcher = f.Spawn("watcher", 20, 20, Commoner);
        watcher.AppearanceType = 6; // human: PREFATCKDIST 1.7
        var near = f.Spawn("near", 23.4f, 20, Hostile);
        var far = f.Spawn("far", 20, 23.6f, Hostile);
        foreach (var t in new[] { near, far })
        {
            t.AppearanceType = 6; // HITDIST 0.3
            fx.Apply(new Effect { Type = k["EFFECT_TYPE_INVISIBILITY"], SubType = k.Magical, Ints = new[] { 1 } }, k.Permanent, t, 0);
        }
        Assert.Equal(3.5f, f.World.System<PerceptionSystem>().CloseRange(watcher, near), 3);
        f.World.System<PerceptionSystem>().Update(watcher);
        Assert.DoesNotContain(near.Id, watcher.Society.Seen);
        Assert.Contains(near.Id, watcher.Society.Heard);
        Assert.DoesNotContain(far.Id, watcher.Society.Heard);
    }

    [DataFact]
    public void ShortAndMediumPerceptionSeeFurtherAndHearLessInCombat()
    {
        var f = FlatArea();
        var c = f.Spawn("c", 20, 20, Commoner);
        var perception = f.World.System<PerceptionSystem>();
        c.PerceptionRange = 8;
        perception.Ranges(c, out float sight, out float hearing);
        Assert.Equal((10f, 10f), (sight, hearing));
        c.InCombat = true;
        perception.Ranges(c, out sight, out hearing);
        Assert.Equal((28f, 15f), (sight, hearing));
        c.PerceptionRange = 10;
        perception.Ranges(c, out sight, out hearing);
        Assert.Equal((35f, 20f), (sight, hearing));
    }

    [DataFact]
    public void AtNightAnObserverWithoutLightSpotsWorseAndALitHiderIsEasier()
    {
        var f = FlatArea();
        var k = EffectConstants.Of(f.World);
        var fx = f.World.System<EffectSystem>();
        var watcher = f.Spawn("watcher", 10, 10, Commoner);
        var sneak = f.Spawn("sneak", 13, 10, Hostile);
        f.World.Clock.Hour = 12;
        Assert.Equal(0, f.Skills.NightModifier(watcher, sneak));
        f.World.Clock.Hour = 22;
        Assert.Equal(-5, f.Skills.NightModifier(watcher, sneak));
        fx.Apply(new Effect { Type = k.VisualEffect, Ints = new[] { 176, 0 }, Floats = new[] { 1f }, Vectors = new Vector3[2] }, k.Permanent, sneak, 0); // VFX_DUR_LIGHT_WHITE_20
        Assert.Equal(5, f.Skills.NightModifier(watcher, sneak));
        fx.Apply(new Effect { Type = k.VisualEffect, Ints = new[] { 182, 0 }, Floats = new[] { 1f }, Vectors = new Vector3[2] }, k.Permanent, watcher, 0); // VFX_DUR_DARKVISION
        Assert.Equal(10, f.Skills.NightModifier(watcher, sneak));
        f.World.Clock.Hour = 18; // the dusk hour itself isn't night
        Assert.Equal(0, f.Skills.NightModifier(watcher, sneak));
    }

    [DataFact]
    public void EnteringStealthInViewHidesOnlyWithHideInPlainSight()
    {
        var f = FlatArea();
        var watcher = f.Spawn("watcher", 10, 10, Commoner, pc: true);
        var sneak = f.Spawn("sneak", 15, 10, Hostile);
        var shadow = f.Spawn("shadow", 10, 15, Hostile);
        foreach (var s in new[] { sneak, shadow }) { s.Skills[SkillIds.Hide] = 40; s.Skills[SkillIds.MoveSilently] = 40; }
        shadow.Feats.Add(SkillIds.FeatHideInPlainSight);
        f.Tick(1f);
        Assert.Contains(sneak.Id, watcher.Society.Seen);
        Assert.Contains(shadow.Id, watcher.Society.Seen);

        f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true);
        f.Skills.SetActionMode(shadow, SkillIds.ModeStealth, true);
        f.Tick(10f);
        Assert.Contains(sneak.Id, watcher.Society.Seen);
        Assert.Contains(sneak.Id, watcher.Society.Heard);
        Assert.DoesNotContain(shadow.Id, watcher.Society.Seen);
        Assert.DoesNotContain(shadow.Id, watcher.Society.Heard);
    }

    [DataFact]
    public void StealthHidesFromTheHidersOwnFactionButNotFromItsSummon()
    {
        var f = FlatArea();
        var sneak = f.Spawn("sneak", 15, 10, Hostile);
        var mate = f.Spawn("mate", 12, 10, Hostile);
        var pet = f.Spawn("pet", 15, 13, Hostile);
        pet.Master = sneak.Id;
        sneak.Skills[SkillIds.Hide] = 40;
        sneak.Skills[SkillIds.MoveSilently] = 40;
        f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true);
        f.Tick(12f);
        Assert.DoesNotContain(sneak.Id, mate.Society.Seen);
        Assert.DoesNotContain(sneak.Id, mate.Society.Heard);
        Assert.Contains(sneak.Id, pet.Society.Seen);
        Assert.Contains(sneak.Id, pet.Society.Heard);
    }

    [DataFact]
    public void ALoudHiderIsHeardButNotSeen()
    {
        var f = FlatArea();
        f.Skills.StealthDie = sides => Random.Shared.Next(1, sides + 1);
        var watcher = f.Spawn("watcher", 10, 10, Commoner, pc: true);
        var sneak = f.Spawn("sneak", 12, 14, Hostile);
        var k = EffectConstants.Of(f.World);
        var fx = f.World.System<EffectSystem>();
        fx.Apply(new Effect { Type = k["EFFECT_TYPE_SKILL_INCREASE"], SubType = k.Magical, Ints = new[] { SkillIds.Hide, 50 } }, k.Permanent, sneak, 0);
        fx.Apply(new Effect { Type = k["EFFECT_TYPE_SKILL_DECREASE"], SubType = k.Magical, Ints = new[] { SkillIds.MoveSilently, 40 } }, k.Permanent, sneak, 0);
        _out.WriteLine($"hide {f.Skills.Rank(sneak, SkillIds.Hide)} ms {f.Skills.Rank(sneak, SkillIds.MoveSilently)} listen {f.Skills.ListenTotal(watcher, sneak, 4.5f)}");
        f.Skills.SetActionMode(sneak, SkillIds.ModeStealth, true);
        f.Tick(9f);
        Assert.DoesNotContain(sneak.Id, watcher.Society.Seen);
        Assert.Contains(sneak.Id, watcher.Society.Heard);
    }

    [DataFact]
    public void GetDetectModeReadsTwoOnceHurt()
    {
        var f = FlatArea();
        var c = f.Spawn("c", 15, 10);
        c.AILevel = 2; // AI_LEVEL_NORMAL: perception every 4 s
        c.Locals.Ints["mode_set"] = 0;
        f.Tick(1f);
        f.Run("skill_mode", c);
        Assert.Equal(0, c.Locals.Ints["detect"]);
        HitPoints.Damage(f.World, c, GameObject.InvalidId, 1, 1);
        f.Tick(5f); // the next perception update
        f.Run("skill_mode", c);
        Assert.Equal(2, c.Locals.Ints["detect"]);
        Assert.True(f.Skills.SetActionMode(c, SkillIds.ModeDetect, true));
        f.Run("skill_mode", c);
        Assert.Equal(1, c.Locals.Ints["detect"]);
        Assert.True(SkillSystem.ForcesWalk(f.World, c));
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void ActiveDetectModeSearchesFurtherAndAtFullSkill()
    {
        var f = FlatArea();
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "T", 1, "", "")!;
        ((ITrappable)trap).TrapDetectDCValue = 18;
        var scout = f.Spawn("scout", 28, 20, pc: true); // only players and their associates search
        scout.Skills[SkillIds.Search] = 8;
        f.Tick(0.2f); // passive: 6 m from the trap's edge is out of reach, and 10 + 4 < 18 anyway
        Assert.DoesNotContain(scout.Id, ((ITrappable)trap).Trap.DetectedBy);

        Assert.True(f.Skills.SetActionMode(scout, SkillIds.ModeDetect, true));
        f.Tick(6.2f); // next round: active (6 m reach), full skill 10 + 8 = 18
        Assert.Contains(scout.Id, ((ITrappable)trap).Trap.DetectedBy);
    }

    // ------------------------------------------------------------------ lore

    [DataFact]
    public void KeenSenseKeepsScriptDetectActiveAndSearchesAtFullRange()
    {
        var f = FlatArea();
        var trap = TrapSystem.CreateAt(f.World, 0, new Location(f.Area.Id, new Vector3(20, 20, 0), 0), 2f, "T", 1, "", "")!;
        ((ITrappable)trap).TrapDetectDCValue = 18;
        var scout = f.Spawn("scout", 27, 20, pc: true);
        scout.Skills[SkillIds.Search] = 8;
        scout.Feats.Add(SkillIds.FeatKeenSense);
        scout.Locals.Ints["mode"] = 0;
        scout.Locals.Ints["mode_set"] = 1;
        scout.Locals.Ints["mode_on"] = 0;
        f.Run("skill_mode", scout);
        Assert.Equal(1, scout.Locals.Ints["mode_now"]);
        Assert.Equal(1, scout.Locals.Ints["detect"]);
        Assert.False(SkillSystem.ForcesWalk(f.World, scout));
        Assert.DoesNotContain(f.Output.Notes, n => n.Kind == "action-mode");
        f.Tick(0.2f);
        Assert.Contains(scout.Id, ((ITrappable)trap).Trap.DetectedBy);
        AssertNoScriptErrors(f);
    }

    [DataFact]
    public void PlayersIdentifyWhatTheirLoreCovers()
    {
        var f = FlatArea();
        var sage = f.Spawn("sage", 10, 10, pc: true);
        var oaf = f.Spawn("oaf", 12, 10, pc: true);
        sage.Skills[SkillIds.Lore] = 10;
        int limit = f.Skills.LoreLimit(sage);
        Assert.Equal(2500, limit);
        Assert.Equal(5, f.Skills.LoreLimit(oaf));

        foreach (var who in new[] { sage, oaf })
        {
            who.Locals.Strings["resref"] = "nw_it_mring001";
            who.Locals.Ints["on_ground"] = 1;
            f.Run("skill_items", who);
            Assert.Equal(0, who.Locals.Ints["made_identified"]);
        }
        var sageRing = f.World.Get<Item>(sage.Locals.Objects["made"])!;
        var oafRing = f.World.Get<Item>(oaf.Locals.Objects["made"])!;
        int value = (int)f.World.Inventory.Rules.UnitValue(sageRing, true);
        _out.WriteLine($"ring value {value}");
        Assert.InRange(value, 6, limit);

        f.World.Inventory.MoveTo(sageRing, sage, actor: sage);
        f.World.Inventory.MoveTo(oafRing, oaf, actor: oaf);
        Assert.True(sageRing.Identified);
        Assert.False(oafRing.Identified);
        Assert.Contains(f.Output.Notes, n => n.Kind == "item-identified" && n.Player == sage);
        AssertNoScriptErrors(f);
    }

    // ------------------------------------------------------------------ animal empathy and parry

    [DataFact]
    public void AnimalEmpathyDominatesAnAnimalForATurnPerLevel()
    {
        var f = FlatArea();
        var druid = f.Spawn("druid", 10, 10, pc: true);
        druid.Skills[SkillIds.AnimalEmpathy] = 10;
        var wolf = f.Spawn("wolf", 12, 10, Hostile);
        wolf.Race = SkillIds.RaceAnimal;
        var guard = f.Spawn("guard", 14, 10);
        f.UseSkill(druid, SkillIds.AnimalEmpathy, guard); // humans aren't animals
        Assert.Equal(GameObject.InvalidId, guard.Master);

        f.UseSkill(druid, SkillIds.AnimalEmpathy, wolf); // 10 + 10 vs DC 15 + 0 hit dice
        Assert.Equal(druid.Id, wolf.Master);
        Assert.Contains(wolf.Effects, e => e.Type == SkillIds.EffectDominated);
        f.Tick(61f); // one turn for a creature without levels
        Assert.Equal(GameObject.InvalidId, wolf.Master);
        Assert.Equal(Hostile, wolf.Faction);
    }

    [DataFact]
    public void ParryModeParriesAndRipostes()
    {
        var f = FlatArea();
        var duelist = f.Spawn("duelist", 10, 10);
        var brute = f.Spawn("brute", 11, 10, Hostile);
        duelist.Skills[SkillIds.Parry] = 15;
        Assert.Equal((false, false), f.Skills.TryParry(duelist, brute, 12)); // not in parry mode
        duelist.ActionModes.Add(SkillIds.ModeParry);
        Assert.Equal((true, true), f.Skills.TryParry(duelist, brute, 12));    // 10 + 15 = 25: 13 over
        Assert.Equal((false, false), f.Skills.TryParry(duelist, brute, 12));  // one attack, one parry a round
        f.Tick(6.1f);
        f.Rolls.Enqueue(1);
        Assert.Equal((true, false), f.Skills.TryParry(duelist, brute, 12));   // 1 + 15 = 16
    }

    // ------------------------------------------------------------------ use magic device

    [DataFact]
    public void UseMagicDeviceEmulatesAClassRestriction()
    {
        var f = FlatArea();
        var rogue = f.Spawn("rogue", 10, 10);
        rogue.Classes.Add(new CreatureClass { Class = SkillIds.ClassRogue, Level = 5 });
        var wand = f.Give(rogue, "nw_it_gem001");
        wand.Properties.Add(new ItemProperty { Type = SkillIds.IpUseLimitationClass, SubType = 10 }); // wizards only
        int need = f.Skills.UseMagicDeviceRequirement(wand, SkillSystem.Emulation.Class);
        Assert.True(need > 0);
        Assert.False(f.Skills.CanUseItem(rogue, wand, out _));
        rogue.Skills[SkillIds.UseMagicDevice] = need;
        Assert.True(f.Skills.CanUseItem(rogue, wand, out int required));
        Assert.Equal(need, required);
    }
}
