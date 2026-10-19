using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ScriptHostilityTests
{
    sealed class NoCombatDice : IDice
    {
        public int Roll(int sides) => throw new InvalidOperationException("Script hostility must not roll combat dice.");
    }

    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Creature Source, Target;
        public readonly List<(uint Id, bool Combat)> Changes = new();
        public CombatSystem Combat => World.System<CombatSystem>();

        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            World.EnableScripts();
            Area = new Area { ResRef = "hostility_test" };
            World.Register(Area); World.Areas.Add(Area);
            Source = Actor(new(20, 20, 0)); Target = Actor(new(26, 20, 0));
            Source.Locals.Objects["target"] = Target.Id;
            Target.Locals.Objects["source"] = Source.Id;
            Script("observe", """
                object a=GetLocalObject(OBJECT_SELF,"source");
                SetLocalInt(OBJECT_SELF,"callback",GetLocalInt(OBJECT_SELF,"callback")+1);
                SetLocalInt(OBJECT_SELF,"source_combat",GetIsInCombat(a));
                SetLocalInt(OBJECT_SELF,"target_combat",GetIsInCombat(OBJECT_SELF));
                SetLocalInt(OBJECT_SELF,"source_action",GetCurrentAction(a));
                SetLocalInt(OBJECT_SELF,"target_action",GetCurrentAction(OBJECT_SELF));
                """);
            Target.Scripts["ScriptSpellAt"] = Target.Scripts["ScriptDamaged"] = "observe";
            Combat.Dice = new NoCombatDice();
            World.ObjectChanged += o => { if (o is Creature c) Changes.Add((c.Id, c.InCombat)); };
        }

        Creature Actor(Vector3 position)
        {
            var c = World.Creatures.FromTemplate(new GffStruct());
            c.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            c.Race = 6;
            for (int i = 0; i < c.Abilities.Length; i++) c.Abilities[i] = 10;
            c.MaxHitPoints = c.CurrentHitPoints = 100;
            World.Register(c); CreatureRules.EnsureInit(World, c);
            World.AddToArea(c, Area, position, 0, exact: true);
            return c;
        }

        void Script(string name, string body)
        {
            var bytes = NssCompiler.Compile("void main(){" + body + "}", name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error);
            World.Scripts.Register(name, new NcsProgram(name, bytes!));
        }

        public void Run(string body)
        {
            Script("request", body);
            World.Scripts!.Run("request", Source.Id);
            Assert.False(World.Scripts.LastRunFailed);
        }

        public void Signal(bool harmful = true) => Run("SignalEvent(GetLocalObject(OBJECT_SELF,\"target\"),EventSpellCastAt(OBJECT_SELF,601," + (harmful ? "TRUE" : "FALSE") + ")); ");
        public void Damage(int amount = 1) => Run("ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(" + amount + ",DAMAGE_TYPE_MAGICAL),GetLocalObject(OBJECT_SELF,\"target\"));");
        public void Flags(bool active)
        {
            Assert.Equal(active, Source.InCombat); Assert.Equal(active, Target.InCombat);
            Assert.Empty(Source.Actions); Assert.Empty(Target.Actions);
            Assert.Equal(GameObject.InvalidId, Combat.TargetOf(Source));
            Assert.Equal(GameObject.InvalidId, Combat.TargetOf(Target));
        }
        public void Callback(int active)
        {
            Assert.Equal(1, Target.Locals.Ints["callback"]);
            Assert.Equal(active, Target.Locals.Ints["source_combat"]);
            Assert.Equal(active, Target.Locals.Ints["target_combat"]);
            Assert.Equal(65535, Target.Locals.Ints["source_action"]);
            Assert.Equal(65535, Target.Locals.Ints["target_action"]);
        }
    }

    [DataFact]
    public void HarmfulSignalActivatesAfterCallbackAndExpiresFromRequestTime()
    {
        var g = new Game();
        g.Signal(); g.Flags(false);
        Assert.False(g.Target.Locals.Ints.ContainsKey("callback"));
        // Deliberately deliver late: the callback must not shift the timeout by a second.
        g.World.Step(1); g.Callback(0); g.Flags(true);
        Assert.Contains((g.Source.Id, true), g.Changes);
        Assert.Contains((g.Target.Id, true), g.Changes);
        g.World.Step(6.9f); g.Flags(true);
        g.Changes.Clear();
        g.World.Step(.2f); g.Flags(false);
        Assert.Contains((g.Source.Id, false), g.Changes);
        Assert.Contains((g.Target.Id, false), g.Changes);
    }

    [DataFact]
    public void DirectPositiveDamageActivatesBeforeDamagedCallback()
    {
        var g = new Game();
        g.Damage(); g.Flags(true);
        Assert.Equal(99, g.Target.CurrentHitPoints);
        Assert.False(g.Target.Locals.Ints.ContainsKey("callback"));
        g.World.Step(.05f); g.Callback(1); g.Flags(true);
        g.World.Step(7.9f); g.Flags(true);
        g.World.Step(.1f); g.Flags(false);
    }

    [DataFact]
    public void NonhostileAndZeroDamageDoNotEnterCombat()
    {
        var g = new Game();
        g.Signal(false); g.Damage(0);
        g.World.Step(.1f); g.Callback(0); g.Flags(false);
        Assert.Equal(100, g.Target.CurrentHitPoints);
        g.World.Step(9); g.Flags(false);
    }

    [DataFact]
    public void RepeatedHostilityRefreshesWithoutQueuingAttacksAndNonhostileDoesNotRefresh()
    {
        // A conservative extension for repeated triggers; the native control isolates single triggers.
        var g = new Game();
        g.Damage(); g.World.Step(5);
        g.Signal(); g.World.Step(.1f); g.Flags(true);
        g.World.Step(3); g.Flags(true); // beyond the first deadline
        g.Signal(false); g.World.Step(4.8f); g.Flags(true);
        g.World.Step(.2f); g.Flags(false);
    }

    [DataFact]
    public void StopStillClearsImmediatelyWithoutDeadlineReenteringCombat()
    {
        var g = new Game();
        g.Damage(); g.World.Step(.1f);
        g.Combat.Stop(g.Source);
        Assert.False(g.Source.InCombat); Assert.True(g.Target.InCombat);
        g.World.Step(1);
        Assert.False(g.Source.InCombat); Assert.True(g.Target.InCombat);
        g.World.Step(7); g.Flags(false);
    }

    [DataFact]
    public void UnmeasuredActorCategoriesStayOutsideScriptHostilityPath()
    {
        foreach (string scenario in new[] { "self", "plot", "dead", "other_area", "destroyed", "invalid" })
        {
            var g = new Game();
            if (scenario == "self") g.Source.Locals.Objects["target"] = g.Source.Id;
            if (scenario == "plot") g.Target.Plot = true;
            if (scenario == "dead") g.Target.CurrentHitPoints = 0;
            if (scenario == "other_area")
            {
                var other = new Area { ResRef = "other" }; g.World.Register(other); g.World.Areas.Add(other);
                g.World.AddToArea(g.Target, other, Vector3.Zero, 0, exact: true);
            }
            if (scenario == "invalid") g.Source.Locals.Objects["target"] = GameObject.InvalidId;
            g.Signal();
            if (scenario == "destroyed") g.World.Destroy(g.Target);
            g.World.Step(.1f); g.Flags(false);
        }
    }

    [DataFact]
    public void OrdinaryWeaponDamageKeepsCombatForEightSeconds()
    {
        var g = new Game();
        g.Combat.Dice = new ScriptedDice(7, 20, 10, 2, 2, 2);
        var hit = g.Combat.Resolve(new AttackContext
        {
            World = g.World, Attacker = g.Source, Target = g.Target, Dice = g.Combat.Dice,
        });
        Assert.True(hit.Hit);
        Assert.True(g.Target.CurrentHitPoints < 100);
        g.Flags(true);
        g.Combat.Dice = new NoCombatDice();
        // About eight seconds after the last attack (nwserver ai_queries "cs").
        g.World.Step(7.9f); g.Flags(true);
        g.World.Step(.2f); g.Flags(false);
    }
}
