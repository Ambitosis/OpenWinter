using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Conditions measured on nwserver in the cond_* and state_scripts parity suites (docs/CONDITIONS.md): which
/// statescripts.2da script a creature under a condition runs and when (Rules.StateScripts), and the engine's part.
/// </summary>
public class ConditionAITests
{
    sealed record Setup(World World, Area Area)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        public Creature Spawn(float x, float y, int faction)
        {
            var c = new Creature { Tag = "t", MaxHitPoints = 20, CurrentHitPoints = 20, Faction = faction, AILevel = 2 };
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
            World.AddToArea(c, Area, new Vector3(x, y, 0), 0, exact: true);
            return c;
        }

        public Effect Apply(string type, Creature target, Creature? creator, float duration = 0)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_" + type], SubType = K.Magical, ScriptId = Fx.NextId(), Creator = creator?.Id ?? World.Module.Id };
            Fx.Apply(e, duration > 0 ? K.Temporary : K.Permanent, target, duration);
            return target.Effects.FirstOrDefault(x => x.Type == e.Type)!;
        }

        public void Step(double seconds) { for (double t = 0; t < seconds - 1e-4; t += 0.25) World.Step(0.25f); }
    }

    static Setup Make(bool scripts = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        if (scripts) world.EnableScripts();
        world.Register(world.Module);
        world.Factions.LoadStandard(); // Hostile hates the others, which like each other
        var area = new Area { Tag = "cai_area", ResRef = "cai_area" };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area);
    }

    /// <summary>Registers a script under a name (a stand-in for a stock state script, say).</summary>
    static void Script(Setup s, string name, string source)
    {
        var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, name, s.World.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        s.World.Scripts.Register(name, new NcsProgram(name, bytes!));
    }

    /// <summary>Stand-ins for the stock state scripts that only count their runs on the creature ("st" and the row).</summary>
    static void CountingStateScripts(Setup s)
    {
        (string Name, int Row)[] scripts = { ("nw_g0_charm", 1), ("nw_g0_confuse", 2), ("nw_g0_fear", 3), ("x2_sig_state", 5), ("nw_g0_dominate", 7), ("nw_g0_sleep", 9) };
        foreach (var (name, row) in scripts)
            Script(s, name, "void main(){SetLocalInt(OBJECT_SELF,\"st" + row + "\",GetLocalInt(OBJECT_SELF,\"st" + row + "\")+1);}");
        Script(s, "cai_hb", "void main(){SetLocalInt(OBJECT_SELF,\"hb\",GetLocalInt(OBJECT_SELF,\"hb\")+1);}");
    }

    static int Local(Creature c, string name) => c.Locals.Ints.GetValueOrDefault(name);

    [DataFact]
    public void TheHighestStateRunsItsScriptAtOnceAndOnEveryHeartbeat()
    {
        var s = Make(scripts: true);
        CountingStateScripts(s);
        // Confused: its script runs as the confusion lands (the heartbeat is brought forward), then on each beat.
        var b = s.Spawn(10, 10, FactionTable.Hostile);
        s.Apply("CONFUSED", b, null);
        s.Step(0.25f);
        Assert.Equal(1, Local(b, "st2"));
        s.Step(7.5f);
        Assert.Equal(2, Local(b, "st2"));
        // Confused and frightened: only the higher row (fear, 3).
        var f = s.Spawn(30, 10, FactionTable.Hostile);
        s.Apply("CONFUSED", f, null);
        s.Apply("FRIGHTENED", f, null);
        s.Step(0.25f);
        Assert.Equal((0, 1), (Local(f, "st2"), Local(f, "st3")));
        // Dazed or dominated runs nothing, and keeps a lower state's script from running.
        var d = s.Spawn(50, 10, FactionTable.Hostile);
        s.Apply("DAZED", d, null);
        s.Apply("CONFUSED", d, null);
        s.Step(8f);
        Assert.Equal((0, 0), (Local(d, "st5"), Local(d, "st2")));
        Assert.Equal(StateScripts.Dazed, StateScripts.Row(d));
    }

    [DataFact]
    public void OnHeartbeatRunsFirstAndAnAssociateSkipsItsOwnWhileInAState()
    {
        var s = Make(scripts: true);
        CountingStateScripts(s);
        var b = s.Spawn(10, 10, FactionTable.Hostile);
        b.Scripts["ScriptHeartbeat"] = "cai_hb";
        s.Apply("CONFUSED", b, null);
        s.Step(0.25f);
        Assert.Equal((1, 1), (Local(b, "hb"), Local(b, "st2")));
        // A henchman under a state runs only the state script.
        var master = s.Spawn(30, 10, FactionTable.Hostile);
        var h = s.Spawn(32, 10, FactionTable.Hostile);
        h.Master = master.Id;
        h.Scripts["ScriptHeartbeat"] = "cai_hb";
        s.Apply("CONFUSED", h, null);
        s.Step(8f);
        Assert.Equal(0, Local(h, "hb"));
        Assert.Equal(2, Local(h, "st2"));
    }

    [DataFact]
    public void TheStockFearScriptRunsFromANearEnemyAndOtherwiseStands()
    {
        var s = Make(scripts: true);
        var b = s.Spawn(60, 10, FactionTable.Hostile);
        var k = s.Spawn(64, 10, FactionTable.Defender);
        s.Apply("FRIGHTENED", b, k);
        // Commandable in the applying script, locked right after it.
        Assert.True(b.Commandable);
        s.Step(0.25f);
        Assert.False(b.Commandable);
        // NW_G0_fear: ActionMoveAwayFromObject from the enemy within 5 m, running.
        var flight = Assert.IsType<MoveAction>(Assert.Single(b.Actions));
        Assert.True(flight.Run && flight.Away);
        Assert.Equal(k.Id, flight.TargetObject);
        // With no enemy within 5 m it stands.
        var alone = s.Spawn(10, 40, FactionTable.Hostile);
        s.Spawn(15.5f, 40, FactionTable.Defender);
        s.Apply("FRIGHTENED", alone, null);
        s.Step(0.5f);
        Assert.Empty(alone.Actions);
    }

    [DataFact]
    public void AnNpcWontAttackAFriendlyMemberOfItsOwnFaction()
    {
        var s = Make();
        var a = s.Spawn(10, 10, FactionTable.Hostile);
        var mate = s.Spawn(11.5f, 10, FactionTable.Hostile);
        a.Actions.Add(new AttackAction { Target = mate.Id });
        s.Step(0.5f);
        // Hostile members regard each other as friends: the order is dropped.
        Assert.Empty(a.Actions);
        // A personal friend in another faction is attacked all the same.
        var f = s.Spawn(50, 10, FactionTable.Hostile);
        var n = s.Spawn(51.5f, 10, FactionTable.Commoner);
        s.World.Factions.SetPersonal(f, n, 100);
        f.Actions.Add(new AttackAction { Target = n.Id });
        s.Step(0.5f);
        Assert.IsType<AttackAction>(Assert.Single(f.Actions));
    }

    [DataFact]
    public void ACharmedNpcRegardsItsCharmerAsAFriend()
    {
        var s = Make();
        var b = s.Spawn(40, 10, FactionTable.Hostile);
        var k = s.Spawn(42.5f, 10, FactionTable.Defender);
        var k2 = s.Spawn(50, 10, FactionTable.Defender);
        s.Apply("CHARMED", b, k);
        // Only its own view of the charmer changes.
        Assert.Equal(100, s.World.Factions.ReputationOf(b, k));
        Assert.Equal(0, s.World.Factions.ReputationOf(k, b));
        Assert.Equal(0, s.World.Factions.ReputationOf(b, k2));
        // A charm the module made changes nothing.
        var c = s.Spawn(40, 60, FactionTable.Hostile);
        var ck = s.Spawn(42.5f, 60, FactionTable.Defender);
        s.Apply("CHARMED", c, null);
        Assert.Equal(0, s.World.Factions.ReputationOf(c, ck));
        // When the charm ends the reputation is back.
        s.Fx.RemoveWhere(b, x => x.Type == CombatK.EffectCharmed);
        Assert.Equal(0, s.World.Factions.ReputationOf(b, k));
    }

    [DataFact]
    public void DominationByAnNpcMakesNoAssociateAndTheCreatureStands()
    {
        var s = Make();
        var b = s.Spawn(10, 10, FactionTable.Hostile);
        var k = s.Spawn(12.5f, 10, FactionTable.Defender);
        s.Apply("DOMINATED", b, k);
        s.Step(13);
        Assert.Equal(GameObject.InvalidId, b.Master);
        Assert.Equal(0, s.World.Factions.ReputationOf(b, k));
        Assert.Empty(b.Actions);
        Assert.False(b.Commandable);
    }

    [DataFact]
    public void TimeStopNeverLandsOnTheServer()
    {
        var s = Make();
        var t = s.Spawn(10, 10, FactionTable.Defender);
        s.Apply("TIMESTOP", t, t, 6f);
        Assert.DoesNotContain(t.Effects, e => e.Type == CombatK.EffectTimeStop);
    }

    [DataFact]
    public void ANegativeAbilityDecreaseDoesNotLand()
    {
        var s = Make();
        var t = s.Spawn(10, 10, FactionTable.Defender);
        var e = new Effect { Type = s.K["EFFECT_TYPE_ABILITY_DECREASE"], SubType = s.K.Magical, ScriptId = s.Fx.NextId() };
        e.Ints[0] = NwRules.AbilityStr;
        e.Ints[1] = -1;
        s.Fx.Apply(e, s.K.Permanent, t, 0);
        Assert.Empty(t.Effects);
    }

    [DataFact]
    public void AnImmobilisedAttackerNeitherStepsNorReportsATargetOutOfReach()
    {
        var s = Make();
        var near = s.Spawn(10, 10, FactionTable.Hostile);
        var far = s.Spawn(10, 20, FactionTable.Hostile);
        var t1 = s.Spawn(11.4f, 10, FactionTable.Commoner);
        var t2 = s.Spawn(14f, 20, FactionTable.Commoner);
        s.Apply("CUTSCENEIMMOBILIZE", near, null);
        s.Apply("CUTSCENEIMMOBILIZE", far, null);
        near.Actions.Add(new AttackAction { Target = t1.Id });
        far.Actions.Add(new AttackAction { Target = t2.Id });
        s.Step(8f);
        var combat = s.World.System<CombatSystem>();
        Assert.Equal(new Vector3(10, 10, 0), near.Position);
        Assert.Equal(new Vector3(10, 20, 0), far.Position);
        Assert.Equal(t1.Id, combat.ReportedTarget(near));
        Assert.Equal(GameObject.InvalidId, combat.ReportedTarget(far));
        // The attack action stays current.
        Assert.Equal(3, far.Actions[0].ActionType);
    }

    [DataFact]
    public void ASwarmSendsItsCreaturesOneAfterAnotherAndEnds()
    {
        var s = Make();
        var c = s.Spawn(10, 10, FactionTable.Defender);
        var e = new Effect { Type = s.K["EFFECT_TYPE_SWARM"], SubType = s.K.Magical, ScriptId = s.Fx.NextId(), Creator = c.Id };
        e.Strings[0] = "nw_kobold001";
        e.Strings[1] = "nw_goblina";
        s.Fx.Apply(e, s.K.Permanent, c, 0);
        Creature? Current() => s.World.AllObjects.OfType<Creature>().FirstOrDefault(x => x.Master == c.Id && !x.IsDead && !x.IsDestroyed);
        var first = Current();
        Assert.NotNull(first);
        Assert.Equal("nw_kobold001", first!.ResRef, ignoreCase: true);
        Assert.Equal(c.Faction, first.Faction);
        HitPoints.Kill(s.World, first, GameObject.InvalidId);
        s.Step(0.5);
        var second = Current();
        Assert.Equal("nw_goblina", second?.ResRef, ignoreCase: true);
        // After the last one, a swarm that doesn't loop ends.
        HitPoints.Kill(s.World, second!, GameObject.InvalidId);
        s.Step(0.5);
        Assert.Null(Current());
        Assert.DoesNotContain(c.Effects, x => x.Type == s.K["EFFECT_TYPE_SWARM"]);
    }

    [DataFact]
    public void PoisonsRunTheirScriptsOnTheVictim()
    {
        var s = Make(scripts: true);
        s.Fx.Dice = n => 0; // every save fails
        var m = s.Spawn(30, 30, FactionTable.Hostile);
        var v = s.Spawn(10, 10, FactionTable.Commoner);
        CreatureRules.EnsureInit(s.World, v);
        // Carrion Crawler Brain Juice: Script_1 paralyses, the creature itself being the paralysis's creator.
        var e = new Effect { Type = s.K["EFFECT_TYPE_POISON"], SubType = s.K.Magical, ScriptId = s.Fx.NextId(), Creator = m.Id };
        e.Ints[0] = 18;
        s.Fx.Apply(e, s.K.Permanent, v, 0);
        s.Step(0.5);
        var held = Assert.Single(v.Effects, x => x.Type == CombatK.EffectParalyze);
        Assert.Equal(v.Id, held.Creator);

        // Blue Whinnis: no secondary ability damage; Script_2 puts the creature to sleep a minute later. Its poison
        // reads 2 in its second integer, as on the original.
        var w = s.Spawn(20, 10, FactionTable.Commoner);
        CreatureRules.EnsureInit(s.World, w);
        var p = new Effect { Type = s.K["EFFECT_TYPE_POISON"], SubType = s.K.Magical, ScriptId = s.Fx.NextId(), Creator = m.Id };
        p.Ints[0] = 8;
        s.Fx.Apply(p, s.K.Permanent, w, 0);
        Assert.Equal(2, w.Effects.First(x => x.Type == s.K["EFFECT_TYPE_POISON"]).Ints[1]);
        s.Step(61);
        Assert.Contains(w.Effects, x => x.Type == CombatK.EffectSleep && x.Creator == w.Id);
    }
}
