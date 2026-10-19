using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Disabling conditions as measured on nwserver (parity suite condition_actions): who refuses them, what their
/// arrival stops, whether a creature can move under them, and what wakes a sleeper.
/// </summary>
public class ConditionTests
{
    sealed record Setup(World World, Area Area)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        public Creature Spawn(float x = 5)
        {
            var c = new Creature { Tag = "t", MaxHitPoints = 20, CurrentHitPoints = 20 };
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
            World.AddToArea(c, Area, new Vector3(x, 5, 0), 0);
            return c;
        }

        public Effect New(string type, params int[] ints)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_" + type], SubType = K.Magical, ScriptId = Fx.NextId() };
            for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
            return e;
        }

        public bool Lands(Creature c, Effect e)
        {
            Fx.Apply(e, K.Permanent, c, 0);
            bool landed = c.Effects.Any(x => x.Type == e.Type);
            Fx.RemoveWhere(c, _ => true);
            return landed;
        }

        public void Step(float seconds) { for (float t = 0; t < seconds; t += 0.25f) World.Step(0.25f); }
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        var area = new Area { Tag = "cond_area", ResRef = "cond_area" };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area);
    }

    [DataFact]
    public void PlotAndImmortalCreaturesRefuseConditions()
    {
        var s = Make();
        var normal = s.Spawn(4);
        var plot = s.Spawn(6);
        plot.Plot = true;
        var immortal = s.Spawn(8);
        immortal.IsImmortal = true;

        foreach (var type in new[] { "SLEEP", "STUNNED", "PARALYZE", "DAZED", "CONFUSED", "FRIGHTENED", "CHARMED", "DOMINATED", "PETRIFY", "TURNED" })
        {
            Assert.True(s.Lands(normal, s.New(type)), type);
            Assert.False(s.Lands(plot, s.New(type)), type);
            Assert.False(s.Lands(immortal, s.New(type)), type);
            // IgnoreEffectImmunity doesn't get these past plot or immortality.
            var ignoring = s.New(type);
            ignoring.IgnoreImmunity = true;
            Assert.False(s.Lands(plot, ignoring), type);
        }
        foreach (var type in new[] { "KNOCKDOWN", "ENTANGLE", "SLOW", "BLINDNESS", "SILENCE", "CURSE", "AC_DECREASE", "CUTSCENE_PARALYZE" })
        {
            Assert.False(s.Lands(plot, s.New(type)), type);
            Assert.True(s.Lands(immortal, s.New(type)), type);
        }
        var slow = s.New("SLOW");
        slow.IgnoreImmunity = true;
        Assert.True(s.Lands(plot, slow));
        // Bonuses and cutscene immobilisation still land on a plot creature.
        Assert.True(s.Lands(plot, s.New("HASTE")));
        Assert.True(s.Lands(plot, s.New("CUTSCENEIMMOBILIZE")));
    }

    [DataFact]
    public void ConditionsStopWhatTheCreatureWasDoing()
    {
        var s = Make();
        var dazed = s.Spawn(4);
        dazed.Actions.Add(new WaitAction { Remaining = 10 });
        s.Fx.Apply(s.New("DAZED"), s.K.Permanent, dazed, 0);
        Assert.Empty(dazed.Actions);
        // Dazed creatures stay commandable; frightened ones don't, at once.
        Assert.True(dazed.Commandable);

        var afraid = s.Spawn(6);
        afraid.Actions.Add(new WaitAction { Remaining = 10 });
        s.Fx.Apply(s.New("FRIGHTENED"), s.K.Permanent, afraid, 0);
        Assert.Empty(afraid.Actions);
        // Still commandable for a moment (0.1 s on the original), locked by 0.5 s.
        Assert.True(afraid.Commandable);
        s.Step(0.5f);
        Assert.False(afraid.Commandable);

        // Slowness and blindness change nothing about the queue.
        var slowed = s.Spawn(8);
        slowed.Actions.Add(new WaitAction { Remaining = 10 });
        s.Fx.Apply(s.New("SLOW"), s.K.Permanent, slowed, 0);
        Assert.Single(slowed.Actions);
        Assert.True(slowed.Commandable);
    }

    [DataFact]
    public void HelplessCreaturesGiveUpMoving()
    {
        var s = Make();
        var c = s.Spawn(4);
        s.Fx.Apply(s.New("ENTANGLE"), s.K.Temporary, c, 1f);
        c.Actions.Add(new MoveAction { Target = new Vector3(12, 5, 0) });
        s.Step(0.5f);
        // The move failed at once and is not resumed when the entanglement ends.
        Assert.Empty(c.Actions);
        s.Step(2f);
        Assert.Equal(4, c.Position.X, 2);
    }

    [DataFact]
    public void DamageWakesASleeperButBreaksNothingElse()
    {
        var s = Make();
        var sleeper = s.Spawn(4);
        var stunned = s.Spawn(6);
        s.Fx.Apply(s.New("SLEEP"), s.K.Permanent, sleeper, 0);
        s.Fx.Apply(s.New("STUNNED"), s.K.Permanent, stunned, 0);
        var magical = new Dictionary<int, int> { [s.K.DamageMagical] = 1 };
        HitPoints.Damage(s.World, sleeper, GameObject.InvalidId, magical, 0);
        HitPoints.Damage(s.World, stunned, GameObject.InvalidId, magical, 0);
        Assert.DoesNotContain(sleeper.Effects, e => e.Type == NwRules.EffectSleep);
        // Commandable again once the waking is over (nwserver state_commandable: not inside the removing script).
        s.Step(0.05f);
        Assert.True(sleeper.Commandable);
        Assert.Contains(stunned.Effects, e => e.Type == NwRules.EffectStunned);
    }
}
